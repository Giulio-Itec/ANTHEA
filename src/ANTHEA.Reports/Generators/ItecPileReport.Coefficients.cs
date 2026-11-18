using System.Globalization;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Anthea.Calculations;
using X.Core;

namespace Anthea.Reports;

/// <summary>
/// The coefficients of the shaft in the report of the vertical pile (18/11/2026): in the theory, K and μ by installation and α by cu in the form of
/// C. Viggiani, Fondazioni, Tab. 13.2 and 13.3, with the values of the GPC library (<see cref="Calcolo.Parametri"/>, <see cref="Calcolo.CoefficienteAlfa"/>)
/// and the rows of the pile highlighted; in the geotechnical model, K, μ and α of every layer as the engine used them (the shaft segments of the
/// final depth). Nothing is computed here.
/// </summary>
internal sealed partial class ItecPileReport
{
    const string HighlightFill = "FFF2CC", HeaderFill = "E7E6E6";

    /// <summary>The bounds of cu [kPa] of the three rows of α (Tab. 13.3); the values of α come from the library.</summary>
    const double AdhesionLow = 25, AdhesionHigh = 70;

    static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it-IT");

    /// <summary>The references of the two tables of the theory, for the note of the table of the layers; null when they are not printed.</summary>
    string? shaftTable, adhesionTable;

    /// <summary>A cell of a table with merged cells: <paramref name="Merge"/> "restart" or "continue" merges vertically.</summary>
    sealed record Cell(string Text, int Span = 1, string? Merge = null, bool Header = false, bool Highlight = false, bool Left = false);

    /// <summary>The technologies that are not driven: one row each in the table of K and μ, the row «Trivellato» of α.</summary>
    static bool Bored(string technology) => technology is "Trivellato" or "Elica continua";

    /// <summary>The technology of the pile, as the keys of <see cref="Calcolo.Parametri"/>: the subtype for the driven piles.</summary>
    static string Technology(JsonNode g)
    {
        string type = g.S("tipo_palo", "Trivellato");
        return type == "Battuto" ? g.S("sottotipo_palo_battuto", "Profilato d'acciaio") : type;
    }

    static string MuRule(string rule) => rule switch { "tan20" => "tan 20°", "tan3phi4" => "tan (¾ φ′)", "tanphi" => "tan φ′", _ => rule };
    static string Coefficient(double value) => value.ToString("0.0#", Italian);
    static string Engine(JsonNode? node) => J.Number(node) is double value && double.IsFinite(value) ? value.ToString("0.00", Italian) : "—";

    /// <summary>Tab. 13.2 of Viggiani: K loose and dense and μ by installation, the technology of the pile highlighted.</summary>
    void ShaftCoefficientTable(JsonNode g)
    {
        string current = Technology(g);
        shaftTable = $"Tabella {chapter}.{tableNumber + 1}";
        Paragraph($"I coefficienti K e μ del fusto dipendono dalla tecnologia del palo e dallo stato di addensamento del terreno. Il programma adotta i valori della {shaftTable}, "
            + "nella forma proposta da Viggiani (1999); la riga evidenziata è la tecnologia del palo in esame.");
        var rows = new List<Cell[]>
        {
            new Cell[] { new("Tipo di palo", 2, "restart", true), new("K — stato di addensamento", 2, Header: true), new("μ", Merge: "restart", Header: true) },
            new Cell[] { new("", 2, "continue", true), new("sciolto", Header: true), new("denso", Header: true), new("", Merge: "continue", Header: true) }
        };
        var driven = Calcolo.Parametri.Where(p => !Bored(p.Key)).ToArray();
        foreach (var (p, i) in driven.Select((p, i) => (p, i)))
        {
            bool mine = p.Key == current;
            rows.Add([new(i == 0 ? "Battuto" : "", Merge: i == 0 ? "restart" : "continue"), new(p.Key, Highlight: mine, Left: true),
                new(Coefficient(p.Value.Sciolto), Highlight: mine), new(Coefficient(p.Value.Denso), Highlight: mine), new(MuRule(p.Value.Mu), Highlight: mine)]);
        }
        foreach (var p in Calcolo.Parametri.Where(p => Bored(p.Key)))
        {
            bool mine = p.Key == current;
            rows.Add([new(p.Key, 2, Highlight: mine, Left: true), new(Coefficient(p.Value.Sciolto), Highlight: mine), new(Coefficient(p.Value.Denso), Highlight: mine),
                new(MuRule(p.Value.Mu), Highlight: mine)]);
        }
        GridTable("Coefficienti K e μ del fusto in condizioni drenate", [1.5, 3.1, 1.5, 1.5, 2], rows, 2,
            $"Riga evidenziata: «{current}», la tecnologia del palo nei dati generali. φ′ è l’angolo di attrito efficace dello strato.");
    }

    /// <summary>Tab. 13.3 of Viggiani: α by cu for driven and bored piles, the rows of the pile highlighted.</summary>
    void AdhesionTable(JsonNode g)
    {
        bool driven = !Bored(Technology(g));
        adhesionTable = $"Tabella {chapter}.{tableNumber + 1}";
        Paragraph($"In condizioni non drenate l’aderenza è τs = α cu. Il coefficiente α dipende dalla tecnologia e dalla coesione non drenata secondo la {adhesionTable}, "
            + "nella forma proposta da Viggiani (1999); le righe evidenziate sono quelle del palo in esame.");
        string Bound(double cu) => cu.ToString("0.##", Italian);
        var rows = new List<Cell[]> { new Cell[] { new("Tipo di palo", Header: true), new("cu [kPa]", Header: true), new("α", Header: true) } };
        // The middle row is the rule of the library (GPC AxialPileCapacity.Alpha); the bounds and the two constant values are read from it.
        foreach (var (name, type, linear) in new[] { ("Battuto", "Battuto", "1 − 0,0111 (cu − 25)"), ("Trivellato", "Trivellato", "0,7 − 0,008 (cu − 25)") })
        {
            bool mine = driven == (type == "Battuto");
            var pile = J.Obj(("tipo_palo", type));
            string[] ranges = [$"cu ≤ {Bound(AdhesionLow)}", $"{Bound(AdhesionLow)} < cu < {Bound(AdhesionHigh)}", $"cu ≥ {Bound(AdhesionHigh)}"];
            string[] values = [Coefficient(Calcolo.CoefficienteAlfa(pile, AdhesionLow)), linear, Coefficient(Calcolo.CoefficienteAlfa(pile, AdhesionHigh))];
            for (int i = 0; i < ranges.Length; i++)
                rows.Add([new(i == 0 ? name : "", Merge: i == 0 ? "restart" : "continue", Highlight: mine), new(ranges[i], Highlight: mine), new(values[i], Highlight: mine)]);
        }
        GridTable("Coefficiente di aderenza α in condizioni non drenate", [2.6, 2.9, 4.1], rows, 1,
            "Le righe «Trivellato» valgono per tutte le tecnologie non battute, compresa l’elica continua.");
    }

    /// <summary>
    /// K, μ and α of every layer of a vertical as the engine used them: the shaft segment of the final depth whose middle falls in the layer (the
    /// segments are measured from the head of the pile, the layers from the ground). The layers above the head or below the tip have none.
    /// </summary>
    void LayerCoefficientTable(int vertical, JsonArray layers, JsonNode g, JsonNode? last)
    {
        var segments = ((last?["sondaggi"] as JsonArray)?.ElementAtOrDefault(vertical)?["tratti"] as JsonArray)?.OfType<JsonNode>().ToArray() ?? [];
        double head = Calcolo.ProfonditaTestaMostrata(g), top = 0;
        var rows = new List<string[]>();
        foreach (var (layer, j) in layers.Select((s, j) => (s, j)))
        {
            double bottom = top + (J.Number(layer?["spessore"]) ?? 0);
            bool Inside(JsonNode t)
            {
                if (J.Number(t["cielo"]) is not double from || J.Number(t["fondo"]) is not double to) return false;
                double middle = (from + to) / 2 + head;
                return middle >= top && middle < bottom;
            }
            var segment = segments.FirstOrDefault(Inside);
            rows.Add([(j + 1).ToString(Italian), layer.S("tipologia"), Value(layer?["addensamento"]), Value(layer?["angolo_attrito"]), Value(layer?["coesione_non_drenata"]),
                Engine(segment?["k"]), Engine(segment?["mu"]), Engine(segment?["alfa"])]);
            top = bottom;
        }
        string current = Technology(g);
        Paragraph($"I coefficienti del fusto adottati dal calcolo per ciascuno strato sono riportati nella Tabella {chapter}.{tableNumber + 1}.");
        Table("Coefficienti del fusto per strato", ["Strato", "Tipologia", "Stato", "φ′ [°]", "cu [kPa]", "K [−]", "μ [−]", "α [−]"], rows, [0.85, 1.5, 1.5, 1.1, 1.1, 1.2, 1.2, 1.2], true,
            $"K dalla {shaftTable ?? "tabella dei coefficienti K e μ"} per la tecnologia «{current}» e lo stato di addensamento dello strato; "
            + $"μ = {MuRule(Calcolo.Parametri.TryGetValue(current, out var p) ? p.Mu : "")}; α dalla {adhesionTable ?? "tabella del coefficiente di aderenza"}, "
            + "solo negli strati coesivi sotto falda (ramo non drenato). «—»: coefficiente non usato nello strato o strato non attraversato dal fusto.");
    }

    /// <summary>A table with merged cells and highlighted rows, in the style of <see cref="Table(string, string[], List{string[]}, double[], bool, string)"/>.</summary>
    void GridTable(string title, double[] weights, IReadOnlyList<Cell[]> rows, int headerRows, string note)
    {
        var caption = P($"Tabella {chapter}.{++tableNumber}  {title}", "Didascalia");
        caption.Element(W + "pPr")!.Add(new XElement(W + "keepNext")); body.Add(caption);
        int[] widths = Widths(weights);
        var table = TableElement(widths);
        foreach (var (cells, i) in rows.Select((r, i) => (r, i)))
        {
            var row = new XElement(W + "tr", new XElement(W + "trPr", new XElement(W + "cantSplit"), i < headerRows ? new XElement(W + "tblHeader") : null));
            int column = 0;
            foreach (var cell in cells)
            {
                int width = widths.Skip(column).Take(cell.Span).Sum(); column += cell.Span;
                string? fill = cell.Header ? HeaderFill : cell.Highlight ? HighlightFill : null;
                var p = P(cell.Merge == "continue" ? "" : cell.Text);
                p.Element(W + "pPr")!.Add(new XElement(W + "keepNext"), new XElement(W + "spacing", new XAttribute(W + "before", 0), new XAttribute(W + "after", 0), new XAttribute(W + "line", 240), new XAttribute(W + "lineRule", "auto")),
                    new XElement(W + "jc", new XAttribute(W + "val", cell.Left ? "left" : "center")));
                p.Element(W + "r")!.AddFirst(new XElement(W + "rPr", cell.Header || cell.Highlight ? new XElement(W + "b") : null, new XElement(W + "sz", new XAttribute(W + "val", 20))));
                row.Add(new XElement(W + "tc", new XElement(W + "tcPr", new XElement(W + "tcW", new XAttribute(W + "w", width), new XAttribute(W + "type", "dxa")),
                    cell.Span > 1 ? new XElement(W + "gridSpan", new XAttribute(W + "val", cell.Span)) : null,
                    cell.Merge is null ? null : new XElement(W + "vMerge", cell.Merge == "restart" ? new XAttribute(W + "val", "restart") : null),
                    fill is null ? null : new XElement(W + "shd", new XAttribute(W + "val", "clear"), new XAttribute(W + "fill", fill)),
                    new XElement(W + "vAlign", new XAttribute(W + "val", "center"))), p));
            }
            table.Add(row);
        }
        body.Add(table); Note(note);
    }

    /// <summary>The note under a table: small italic text.</summary>
    void Note(string text)
    {
        var p = P(text);
        p.Element(W + "pPr")!.Add(new XElement(W + "spacing", new XAttribute(W + "after", 160)));
        p.Element(W + "r")!.AddFirst(new XElement(W + "rPr", new XElement(W + "i"), new XElement(W + "sz", new XAttribute(W + "val", 18))));
        body.Add(p);
    }
}
