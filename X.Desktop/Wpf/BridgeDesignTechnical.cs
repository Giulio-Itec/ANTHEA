using System.Text;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;

internal sealed partial class BridgeDesignWorkspace
{
    private readonly ContentControl technical = new();
    private void ShowTechnical(BridgeConcept.Result r)
    {
        var panel = Ui.Stack(Ui.Text(r.Family.Name + " · dimensioni adottate", 17, true),
            Ui.Text("Quote del modello usato per calcolare quantità e costi. Le misure in mm identificano gli spessori; le lunghezze sono in m. Schema di predimensionamento, non distinta esecutiva.", 12, color: Ui.Muted));
        var sectionTable = Table(["Componente", "Simbolo", "Valore", "Unità", "Origine", "Significato"], BridgeConcept.TechnicalSchedule(Data, r).Select(t =>
            new[] { t.Component, t.Symbol, F(t.Value, t.Unit == "n." ? "N0" : t.Unit == "mm" ? "N1" : "N3"), t.Unit, t.Origin, t.Note }), 480);
        double[] columnWidths = [1.2, .7, .8, .5, .8, 3];
        for (int k = 0; k < columnWidths.Length; k++) sectionTable.Columns[k].Width = new DataGridLength(columnWidths[k], DataGridLengthUnitType.Star);
        panel.Children.Add(sectionTable);
        panel.Children.Add(Ui.Text("Campate · progressive e sviluppo longitudinale", 15, true));
        panel.Children.Add(Table(["Campata", "Da x [m]", "A x [m]", "Luce Li [m]", "Elementi n", "n × Li [m]"], BridgeConcept.SpanSchedule(r).Select(s =>
            new[] { s.Number.ToString(), F(s.Start, "N3"), F(s.End, "N3"), F(s.Length, "N3"), s.Elements.ToString(), F(s.DevelopedLength, "N3") }), 300));
        panel.Children.Add(Ui.Text("Pile, spalle e fondazioni · un record per appoggio", 15, true));
        panel.Children.Add(Ui.Text("B = dimensione longitudinale, W = trasversale, t = spessore. I pali sono indicati con numero × diametro × lunghezza. I volumi delle spalle sono equivalenti: il modello non definisce una carpenteria completa.", 12, color: Ui.Muted));
        panel.Children.Add(Table(["Appoggio / x", "Tipo / fusto", "H [m]", "Pulvino W×B×t [m]", "Fondazione B×W×t [m]", "Pali n×Ø×L [m]"], BridgeConcept.SupportSchedule(Data, r).Select(s =>
            new[] { $"{s.Number} · {F(s.X, "N2")} m", s.Type == "Spalla" ? "Spalla equivalente" : s.Type + (s.WallWidth > 0 ? $" · {F(s.Size, "N2")}×{F(s.WallWidth, "N2")} m" : $" · {s.Columns}×Ø{F(s.Size, "N2")} m"),
                F(s.Height, "N2"), s.CapLength == 0 ? "—" : $"{F(s.CapLength, "N2")}×{F(s.CapWidth, "N2")}×{F(s.CapThickness, "N2")}",
                $"{F(s.FootingLength, "N2")}×{F(s.FootingWidth, "N2")}×{F(s.FootingThickness, "N2")}", s.Piles == 0 ? "Diretta" : $"{s.Piles}×Ø{F(s.PileDiameter, "N2")}×{F(s.PileLength, "N2")}" }), 400));
        panel.Children.Add(Ui.Bar(Ui.Button("Esporta sezioni e quote CSV", () => Save("Sezioni e quote CSV|*.csv", "BridgeDesign_sezioni_quote.csv", filename =>
            Archivio.ScriviAtomico(filename, Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(BridgeConceptExport.TechnicalCsv(Data, Calculation!))).ToArray())), inspection: true)));
        technical.Content = Scroll(panel, 500);
    }
}
