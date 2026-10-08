using System.Text.Json.Nodes;
using Materiali;

namespace Anthea.Calculations;

public static partial class RetainingWall
{
    public static string RebarZoneName(string key) => key switch { "stem" => "Fusto inferiore / intero", "stem_upper" => "Fusto superiore", "toe" => "Mensola valle", "heel" => "Mensola monte", _ => key };
    public sealed record BarPoint(double X, double Y);
    public sealed record BarDetail(string Mark, string Zone, string Face, double Diameter, double Count,
        double Spacing, double Area, double Length, double RequiredAnchor, double Anchor,
        double RequiredLap, double Lap, double Mandrel, double Fbd, IReadOnlyList<BarPoint> Path);
    public sealed record ReinforcementDetails(IReadOnlyList<BarDetail> Bars, IReadOnlyList<Check> Checks, double SteelKg, string Note);
    public sealed record ReinforcementProposal(JsonObject Input, IReadOnlyList<Check> Checks, bool Passed, string Message);

    public static ReinforcementDetails CalculateReinforcementDetails(JsonObject input)
    {
        var d = (JsonObject)input.DeepClone(); CompleteAdvancedInput(d);
        var g = d["geometry"]!; var m = d["materials"]!; var opt = d["detailing"]!; var r = d["reinforcement"]!;
        double h = g.D("height"), t = g.D("slab"), a = g.D("toe"), s = g.D("stem_base"), b = a + s + g.D("heel"), back = a + s;
        bool two = r.B("two_zones"); double split = two ? r.D("lower_height") : h;
        var section = SectionInput(d, "Fusto", s); var workspace = SectionWorkspace.Prepare(SezioneCA.DefaultData());
        var strengths = ConcreteMaterials.DesignValues(section, workspace); var concrete = ConcreteMaterials.Concrete(section);
        var bond = new ConcreteAnchorageCalculator(); var bars = new List<BarDetail>(); var checks = new List<Check>(); double kg = 0;
        double aggregate = AdvancedNumber(opt, "aggregate", 4, 63), clearLap = AdvancedNumber(opt, "lap_clear", 0, 100);
        double cover = m.D("cover"), percent = AdvancedNumber(opt, "lap_percent", 1, 100);
        void Max(string name, double actual, double limit) => checks.Add(CheckValue(name, "Dettagli", actual, limit, "mm"));
        void Min(string name, double actual, double required, string unit = "mm") => checks.Add(CheckValue(name, "Dettagli", required, actual, unit));
        foreach (string key in new[] { "stem", "stem_upper", "toe", "heel" })
        {
            if (key == "stem_upper" && !two || key == "toe" && a == 0 || key == "heel" && g.D("heel") == 0) continue;
            var arm = r[key]!; bool stem = key.StartsWith("stem"), upper = key == "stem_upper";
            double secondaryDia = AdvancedNumber(arm, "secondary_diameter", 6, 32), secondaryStep = AdvancedNumber(arm, "secondary_spacing", 50, 500);
            for (int face = 0; face < 2; face++)
            {
                double dia = arm.D(face == 0 || arm.B("symmetric", true) ? "diameter" : "opposite_diameter");
                double count = arm.D(face == 0 || arm.B("symmetric", true) ? "count" : "opposite_count");
                if (count < 2 || count > 30 || count % 1 != 0 || dia < 6 || dia > 40) throw new ArgumentException("Armature: diametri 6–40 mm, 2–30 barre intere per metro.");
                string label = key + " · " + (stem ? face == 0 ? "monte" : "valle" : face == 0 ? "inferiore" : "superiore");
                double spacing = (1000 - 2 * cover - dia) / (count - 1), area = count * Math.PI * dia * dia / 4;
                Min(label + " · interferro", spacing - dia, Math.Max(20, Math.Max(dia, aggregate + 5)));
                Max(label + " · interasse principale", spacing, stem ? Math.Min(3 * g.D("stem_top") * 1000, 400) : Math.Min(2 * t * 1000, 250));
                int life = (int)AdvancedNumber(opt, "life", 50, 100); if (life is not (50 or 100)) throw new ArgumentException("Vita per il copriferro: 50 o 100 anni.");
                Min(label + " · copriferro", cover, MaterialCover.Required(CoverMaterialState(d), m.D("fck"), Math.Max(dia, secondaryDia)));
                var dev = bond.Calculate(new(dia, strengths.Fyd, concrete.Fctk05, section.D("gamma_c"), opt.B("good_bond"), 0, false, percent, clearLap));
                var lap = bond.Calculate(new(dia, strengths.Fyd, concrete.Fctk05, section.D("gamma_c"), opt.B("good_bond"), 0, true, percent, clearLap));
                double lapUsed = stem && two ? (arm.D("lap_length") == 0 ? Math.Ceiling(lap.RequiredLength / 10) * 10 : AdvancedNumber(arm, "lap_length", 0, 10000)) : 0;
                if (stem && two)
                {
                    var twin = r[upper ? "stem" : "stem_upper"]!;
                    double twinDia = twin.D(face == 0 || twin.B("symmetric", true) ? "diameter" : "opposite_diameter");
                    double twinRequired = bond.Calculate(new(twinDia, strengths.Fyd, concrete.Fctk05, section.D("gamma_c"), opt.B("good_bond"), 0, true, percent, clearLap)).RequiredLength;
                    double commonRequired = Math.Max(lap.RequiredLength, twinRequired);
                    if (arm.D("lap_length") == 0) lapUsed = Math.Ceiling(commonRequired / 10) * 10;
                    double twinLength = twin.D("lap_length") == 0 ? Math.Ceiling(commonRequired / 10) * 10 : AdvancedNumber(twin, "lap_length", 0, 10000);
                    Min(label + " · sovrapposizione effettiva comune", (lapUsed + twinLength) / 2, commonRequired);
                    Min(label + " · interferro tra coppie giuntate", spacing - dia - twinDia - clearLap, Math.Max(20, Math.Max(Math.Max(dia, twinDia), aggregate + 5)));
                    Max(label + " · distanza barre giuntate", clearLap, Math.Min(50, 4 * dia));
                    Min(label + " · percentuale giuntata alla quota h₁", percent, 100, "%");
                    Max(label + " · ingombro sovrapposizione", lapUsed, Math.Max(0, 2 * Math.Min(split, h - split) * 1000 - 2 * cover));
                    var other = r[upper ? "stem" : "stem_upper"]!;
                    double otherCount = other.D(face == 0 || other.B("symmetric", true) ? "count" : "opposite_count");
                    checks.Add(CheckValue(label + " · corrispondenza barre nelle due zone", "Dettagli", Math.Abs(count - otherCount), 0, "barre"));
                }
                double secArea = Math.PI * secondaryDia * secondaryDia / 4 * 1000 / secondaryStep;
                double thickness = stem ? (upper ? s - (s - g.D("stem_top")) * split / h : s) : t;
                Min(label + " · armatura secondaria", secArea, stem ? Math.Max(.25 * area, .0005 * thickness * 1e6) : .2 * area, "mm²/m");
                Max(label + " · passo secondaria", secondaryStep, stem ? 400 : Math.Min(3 * t * 1000, 400));
                double c = (cover + dia / 2) / 1000, anchor = arm.D("anchor_length") == 0 ? Math.Ceiling(dev.RequiredLength / 10) * 10 : AdvancedNumber(arm, "anchor_length", 0, 10000);
                var path = new List<BarPoint>(); double mandrel = 0;
                if (stem)
                {
                    double y0 = upper ? t + split - lapUsed / 2000 : t;
                    double y1 = upper || !two ? t + h - c : t + split + lapUsed / 2000;
                    double X(double y) => face == 0 ? back - c : a + (s - g.D("stem_top")) * Math.Max(0, y - t) / h + c * Math.Sqrt(1 + Math.Pow((s - g.D("stem_top")) / h, 2));
                    path.Add(new(X(y1), y1)); path.Add(new(X(y0), y0));
                    if (!upper)
                    {
                        double ab = Math.Min(cover + dia / 2, spacing / 2);
                        double requiredMandrel = Math.Max((dia <= 16 ? 4 : 7) * dia, Math.PI * dia * dia / 4 * strengths.Fyd / strengths.Fcd * (1 / ab + 1 / (2 * dia)));
                        mandrel = arm.D("bend_diameter") == 0 ? Math.Ceiling(requiredMandrel / 10) * 10 : AdvancedNumber(arm, "bend_diameter", 1, 2000);
                        Min(label + " · mandrino (acciaio e pressione CLS)", mandrel, requiredMandrel);
                        double radius = (mandrel + dia) / 2000, vertical = t - c - radius, direction = face == 0 ? 1 : -1;
                        Min(label + " · altezza per la piega", (t - 2 * c) * 1000, radius * 1000);
                        double tail = Math.Max(5 * dia / 1000, anchor / 1000 - Math.Max(0, vertical) - radius * Math.PI / 2);
                        double x = X(t), y = c + radius;
                        path.Add(new(x, y));
                        for (int j = 1; j <= 12; j++) { double angle = j * Math.PI / 24; path.Add(new(x + direction * radius * (1 - Math.Cos(angle)), y - radius * Math.Sin(angle))); }
                        path.Add(new(x + direction * (radius + tail), c));
                        double available = face == 0 ? b - c - x : x - c;
                        Max(label + " · ingombro coda", (radius + tail) * 1000, Math.Max(0, available) * 1000);
                        anchor = Math.Max(0, vertical) * 1000 + radius * Math.PI / 2 * 1000 + tail * 1000;
                        Min(label + " · ancoraggio al piede", anchor, dev.RequiredLength);
                    }
                    else anchor = 0;
                }
                else
                {
                    double root = key == "toe" ? a : back, direction = key == "toe" ? 1 : -1, end = key == "toe" ? c : b - c;
                    double y = face == 0 ? c : t - c;
                    path.Add(new(end, y)); path.Add(new(root + direction * anchor / 1000, y));
                    Min(label + " · ancoraggio oltre la radice", anchor, dev.RequiredLength);
                    Max(label + " · ingombro ancoraggio", anchor, Math.Max(0, key == "toe" ? b - c - root : root - c) * 1000);
                }
                double length = path.Zip(path.Skip(1), (p, q) => double.Hypot(p.X - q.X, p.Y - q.Y)).Sum();
                // The sampled arc is only for drawing; cut length uses its exact centre-line development.
                if (mandrel > 0) length += (mandrel + dia) / 2000 * (Math.PI / 2 - 24 * Math.Sin(Math.PI / 48));
                bars.Add(new("P" + (bars.Count + 1), key, label.Split(" · ")[1], dia, count, spacing, area, length, upper ? 0 : dev.RequiredLength, anchor, stem && two ? lap.RequiredLength : 0, lapUsed, mandrel, dev.Fbd, path));
                kg += area * 1e-6 * length * 7850;
                double regionLength = stem ? (upper ? h - split : split) : key == "toe" ? a + s / 2 : g.D("heel") + s / 2;
                kg += secArea * 1e-6 * regionLength * 7850;
            }
        }
        if (two)
        {
            double phi = bars.Where(x => x.Zone.StartsWith("stem")).Max(x => x.Diameter), l0 = bars.Max(x => x.Lap);
            double ties = AdvancedNumber(opt, "tie_diameter", 6, 32), step = AdvancedNumber(opt, "tie_spacing", 50, 400);
            Max("Giunzione · passo collegamenti fra facce", step, Math.Min(150, 12 * phi));
            Min("Giunzione · area trasversale in ciascun terzo estremo", Math.Floor(l0 / 3 / step) * Math.PI * ties * ties / 4, .5 * Math.PI * phi * phi / 4, "mm²");
            kg += 2 * (s + .1) * Math.Ceiling(l0 / step) * Math.PI * ties * ties / 4 * 1e-6 * 7850;
        }
        return new(bars, checks, kg, "Schema di predimensionamento per metro: principali, rete secondaria e collegamenti della giunzione. Ancoraggi a fyd senza riduzioni favorevoli; 100% giunzioni a h₁. Sfridi, giunti di costruzione, estremità lungo muro e piano di posa delle barre fuori piano richiedono il disegno esecutivo. Non è una distinta di officina.");
    }

    /// <param name="serviceabilityEngine">Motore SLE delle sezioni (tensioni e fessurazione) di ogni candidato e del calcolo finale; null =
    /// predefinito dell'adattatore (refactoring F2.7b, commit A4).</param>
    public static ReinforcementProposal DesignReinforcement(JsonObject input, CancellationToken token = default, ServiceabilityEngine? serviceabilityEngine = null)
    {
        var d = (JsonObject)input.DeepClone(); Upgrade(d);
        if (d.S("family") != "cantilever") throw new ArgumentException("Il predimensionamento delle armature richiede un muro a mensola in c.a.");
        var original = Calculate(d, token, serviceabilityEngine); var opt = d["detailing"]!;
        int maxCount = (int)AdvancedNumber(opt, "max_count", 4, 30); double maxDia = AdvancedNumber(opt, "max_diameter", 8, 40), target = AdvancedNumber(opt, "target_ratio", .1, 1);
        if (opt.D("max_count") != maxCount) throw new ArgumentException("Numero massimo di barre: inserire un intero.");
        var failures = new List<string>();
        foreach (string key in new[] { "stem", "stem_upper", "toe", "heel" })
        {
            if (key == "stem_upper" && !d["reinforcement"].B("two_zones")) continue;
            var selected = original.Cases.Where(c => c.Factors.S("purpose") != "Ribaltamento").Select(c => c with { Sections = c.Sections.Where(f => ReinforcementKey(d, f.Name, f.Position) == key).ToList(), Curvatures = [] }).Where(c => c.Sections.Count > 0).ToList();
            if (selected.Count == 0) continue;
            var candidates = (from dia in new double[] { 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32, 40 } where dia <= maxDia from n in Enumerable.Range(4, maxCount - 3) orderby n * dia * dia, dia select (dia, n)).ToArray();
            bool found = false;
            var previousArm = d["reinforcement"]![key]!.DeepClone();
            foreach (var (dia, count) in candidates)
            {
                token.ThrowIfCancellationRequested();
                double area = count * Math.PI * dia * dia / 4;
                if (selected.SelectMany(c => c.Sections).Any(f => Math.Abs(f.M) * 1e6 > area * d["materials"].D("fyk") / MaterialSectionInput(d).D("gamma_s") * f.Thickness * 1000)) continue;
                var arm = d["reinforcement"]![key]!; arm["diameter"] = dia; arm["count"] = count; arm["symmetric"] = true;
                List<Check> values;
                try { values = StructuralChecks(d, selected, token, out _, serviceabilityEngine); } catch (ArgumentException) { continue; }
                if (values.Any(c => c.Ratio is null || c.Ratio > (c.Combination == "Dettagli" ? 1 : target))) continue;
                arm["secondary_diameter"] = 10;
                double required = key.StartsWith("stem") ? Math.Max(.25 * area, .0005 * d["geometry"].D("stem_base") * 1e6) : .2 * area;
                arm["secondary_spacing"] = Math.Max(50, Math.Min(200, Math.Floor(Math.PI * 100 / 4 * 1000 / required / 10) * 10));
                found = true; break;
            }
            if (!found) { failures.Add(key + ": nessuna armatura verificata nei limiti scelti"); d["reinforcement"]![key] = previousArm; }
        }
        if (d["reinforcement"].B("two_zones"))
        {
            double n = Math.Max(d["reinforcement"]!["stem"].D("count"), d["reinforcement"]!["stem_upper"].D("count"));
            d["reinforcement"]!["stem"]!["count"] = n; d["reinforcement"]!["stem_upper"]!["count"] = n;
        }
        d["detailing"]!["enabled"] = true;
        var final = Calculate(d, token, serviceabilityEngine); bool pass = failures.Count == 0 && final.Structural.All(c => c.Ratio is double v && v <= (c.Combination == "Dettagli" ? 1 : target));
        return new(d, final.Structural, pass, pass ? "Proposta verificata per le combinazioni inserite; controllare il dettaglio costruttivo prima dell’esecuzione." : string.Join("; ", failures.Append("Predimensionamento con controlli non soddisfatti o incompleti: consultare la tabella; può essere necessario aumentare gli spessori.")));
    }
}
