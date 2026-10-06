using System.Text.Json.Nodes;

namespace Anthea.Calculations;

public static partial class RetainingWall
{
    public sealed record BarLeg(string Name, double Length);
    public sealed record ScheduledBar(string Mark, string Zone, string Face, string Kind, double Diameter,
        int Quantity, double? CutLength, double Spacing, double Mandrel, double Lap,
        IReadOnlyList<BarLeg> Legs, IReadOnlyList<BarPoint> Shape, string Note)
    {
        public double? TotalLength => Quantity * CutLength;
        public double? Weight => TotalLength * Math.PI * Diameter * Diameter / 4 * .00785;
    }
    public sealed record BarSchedule(double PanelLength, double EndCover, double StockLength,
        IReadOnlyList<ScheduledBar> Bars, IReadOnlyList<string> Warnings)
    {
        public double KnownWeight => Bars.Sum(b => b.Weight ?? 0);
        public bool CompleteQuantities => Bars.All(b => b.CutLength is not null);
    }

    // This take-off is separate from both section checks and rendering. Units: m, mm for diameters/spacing.
    public static BarSchedule CalculateBarSchedule(JsonObject input)
    {
        var d = (JsonObject)input.DeepClone(); Upgrade(d);
        if (d.S("family") != "cantilever") throw new ArgumentException("La distinta ferri richiede un muro a mensola in calcestruzzo armato.");
        var o = d["bar_schedule"]!; var g = d["geometry"]!; var r = d["reinforcement"]!;
        foreach (var field in GeometryFields) AdvancedNumber(g, field.Key, field.Min, field.Max);
        if (g.D("stem_top") > g.D("stem_base")) throw new ArgumentException("Lo spessore in testa deve essere ≤ quello al piede.");
        double length = AdvancedNumber(o, "panel_length", .2, 100), endCover = AdvancedNumber(o, "end_cover", 0, 200);
        double stock = AdvancedNumber(o, "stock_length", 1, 18), cover = d["materials"].D("cover") / 1000;
        double h = g.D("height"), t = g.D("slab"), a = g.D("toe"), s = g.D("stem_base"), heel = g.D("heel");
        bool two = r.B("two_zones"); double split = two ? r.D("lower_height") : h;
        if (two && (!double.IsFinite(split) || split <= 0 || split >= h)) throw new ArgumentException("Altezza dell’armatura inferiore compresa fra 0 e H.");
        var details = CalculateReinforcementDetails(d); var bars = new List<ScheduledBar>(); var warnings = new List<string>();
        foreach (var b in details.Bars)
        {
            double span = length * 1000 - 2 * endCover - b.Diameter;
            if (span <= 0 || b.Spacing <= 0) throw new ArgumentException("Tratto troppo corto per il copriferro di estremità e le barre.");
            int count = (int)Math.Ceiling(span / b.Spacing - 1e-10) + 1;
            var legs = new List<BarLeg>();
            if (b.Mandrel > 0)
            {
                legs.Add(new("A", Distance(b.Path[0], b.Path[1])));
                legs.Add(new("B", Distance(b.Path[1], b.Path[2])));
                legs.Add(new("arco 90°", (b.Mandrel + b.Diameter) / 2000 * Math.PI / 2));
                legs.Add(new("C", Distance(b.Path[^2], b.Path[^1])));
            }
            else legs.Add(new("A", b.Length));
            bars.Add(new(b.Mark, b.Zone, b.Face, "Principale", b.Diameter, count, legs.Sum(l => l.Length),
                span / (count - 1), b.Mandrel, b.Lap, legs, b.Path,
                b.Zone == "stem" && b.Face == "valle" && s != g.D("stem_top") ? "Raccordo del tratto inclinato al piede da dettagliare." : ""));
        }
        int secondaryMark = 0;
        foreach (string key in new[] { "stem", "stem_upper", "toe", "heel" })
        {
            if (key == "stem_upper" && !two || key == "toe" && a == 0 || key == "heel" && heel == 0) continue;
            var arm = r[key]!; double dia = arm.D("secondary_diameter"), step = arm.D("secondary_spacing");
            bool stem = key.StartsWith("stem"), upper = key == "stem_upper";
            double start = stem ? upper ? split : cover + dia / 2000 : key == "toe" || a == 0 ? cover + dia / 2000 : a + s / 2;
            double end = stem ? upper || !two ? h - cover - dia / 2000 : split : key == "toe" && heel > 0 ? a + s / 2 : a + s + heel - cover - dia / 2000;
            double span = end - start; if (span <= 0) throw new ArgumentException("Zona troppo corta per la rete secondaria: " + RebarZoneName(key));
            // Each shared boundary belongs to the following zone; no duplicate bar at h1 or slab mid-stem.
            bool omitLast = key == "stem" && two || key == "toe" && heel > 0;
            int intervals = Math.Max(1, (int)Math.Ceiling(span * 1000 / step - 1e-10));
            int count = intervals + (omitLast ? 0 : 1); double cut = length - 2 * endCover / 1000;
            if (cut <= 0) throw new ArgumentException("Lunghezza netta delle barre secondarie non positiva.");
            for (int face = 0; face < 2; face++)
                bars.Add(new("S" + (++secondaryMark), key, stem ? face == 0 ? "monte" : "valle" : face == 0 ? "inferiore" : "superiore",
                    "Secondaria", dia, count, cut, span * 1000 / intervals, 0, 0, [new("A", cut)], [new(0, 0), new(cut, 0)],
                    "Rettilinea lungo muro; estremità e sovrapposizioni longitudinali da dettagliare. Zona " + start.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "–" + end.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " m."));
        }
        if (two)
        {
            double tiesPerM = AdvancedNumber(o, "ties_per_m", 1, 30), cut = AdvancedNumber(o, "tie_cut_length", 0, 10000);
            if (tiesPerM % 1 != 0) throw new ArgumentException("Collegamenti per fila: inserire un numero intero per metro.");
            int rows = (int)Math.Ceiling(details.Bars.Max(b => b.Lap) / d["detailing"].D("tie_spacing")) + 1;
            int count = rows * (int)Math.Ceiling(tiesPerM * length - 1e-10);
            bars.Add(new("C1", "stem", "fra facce", "Collegamento", d["detailing"].D("tie_diameter"), count,
                cut == 0 ? null : cut / 1000, d["detailing"].D("tie_spacing"), 0, 0, [], [],
                $"{rows} file nella sovrapposizione, {tiesPerM:0}/m per fila. Sagoma e ganci da definire; sviluppo " + (cut == 0 ? "mancante." : "assegnato dall’utente.")));
        }
        int failed = details.Checks.Count(c => c.Ratio is null || c.Ratio > 1);
        if (failed > 0) warnings.Add($"{failed} controlli dei dettagli non soddisfatti o non disponibili: consultare le verifiche prima dell’esecuzione.");
        if (!d["detailing"].B("enabled")) warnings.Add("Verifica dei dettagli non attivata nel riepilogo del muro.");
        foreach (var b in bars.Where(b => b.CutLength > stock)) warnings.Add($"{b.Mark}: sviluppo {b.CutLength:0.###} m maggiore della barra commerciale {stock:0.###} m; definire un giunto, nessun taglio automatico.");
        if (bars.Any(b => b.CutLength is null)) warnings.Add("Peso parziale: manca lo sviluppo dei collegamenti C1.");
        warnings.Add("Distinta preliminare senza sfridi. Quote all’asse delle barre; archi sviluppati al raggio (mandrino + Ø)/2. Estremità lungo muro, disposizione degli strati, raccordi e ganci richiedono il dettaglio esecutivo.");
        return new(length, endCover, stock, bars, warnings);
    }
    public static BarSchedule CalculateBarSchedule(Result result)
    {
        var schedule = CalculateBarSchedule(result.Input);
        int failed = result.Checks.Concat(result.Structural).Count(c => c.Ratio is null || c.Ratio > 1);
        return failed == 0 ? schedule : schedule with { Warnings = new[] { $"Risultato del muro: {failed} verifiche non soddisfatte o non disponibili. La distinta rappresenta i ferri inseriti, non certifica l’esito delle verifiche." }.Concat(schedule.Warnings).ToArray() };
    }
    private static double Distance(BarPoint p, BarPoint q) => double.Hypot(p.X - q.X, p.Y - q.Y);
}
