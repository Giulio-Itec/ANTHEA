using Anthea.Calculations;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static class AuditChecks
{
    internal static JsonObject Case(string family, double length)
    {
        var d = BridgeConcept.Defaults(); var i = d["input"]!;
        i["family"] = family; i["length"] = length; i["spans"] = 3; i["lanes"] = 2; i["height"] = 25;
        i["obstacle"] = "Nessuno"; i["soil"] = "Roccia"; i["foundation"] = "Plinto diretto";
        i["continuous"] = family is not "tied_arch" and not "truss";
        if (family == "suspension") i["deck_type"] = BridgeConcept.DeckTypes[1];
        return d;
    }
    internal static int Run(string output)
    {
        int checks = 0;
        void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
        void Near(double x, double y, string message, double tolerance = 1e-8) => Check(Math.Abs(x - y) <= tolerance * Math.Max(1, Math.Abs(y)), message + $" ({x} / {y})");
        double Detail(BridgeConcept.Result r, string name) => r.Details.Single(d => d.Name == name).Value;
        var records = new List<object>(); int attempted = 0, calculated = 0, rejected = 0;
        foreach (var f in BridgeConcept.Families)
        foreach (double fraction in new[] { .15, .5, .85 })
        foreach (string soil in BridgeConcept.Soils)
        foreach (string foundation in BridgeConcept.Foundations.Skip(1))
        foreach (int lanes in new[] { 2, 4 })
        {
            attempted++;
            double span = f.MinSpan + fraction * (f.MaxSpan - f.MinSpan);
            var d = Case(f.Id, Math.Min(1980, span * (BridgeConcept.HasTowers(f.Id) ? 2 : 3)));
            var i = d["input"]!; i["soil"] = soil; i["foundation"] = foundation; i["lanes"] = lanes;
            string before = d.ToJsonString(); BridgeConcept.Result r;
            try { r = BridgeConcept.Calculate(d); }
            catch (ArgumentException ex)
            {
                Check(ex.Message.Contains("Antenna:") || ex.Message.Contains("non convergente"), "Rifiuto inatteso: " + f.Id + " " + ex.Message);
                rejected++; records.Add(new { attempted, f.Id, span, soil, foundation, lanes, status = "rejected", reason = ex.Message }); continue;
            }
            calculated++;
            Check(before == d.ToJsonString(), "Mutazione input audit");
            Check(r.Quantities.All(q => double.IsFinite(q.Amount) && q.Amount >= 0 && double.IsFinite(q.Cost)), "Quantità non finite");
            Near(r.Spans.Sum(), i.D("length"), "Somma campate");
            double qService = Detail(r, "Permanenti sull'intero impalcato") + Detail(r, "Traffico equivalente sull'intero impalcato");
            Near(r.Supports.Sum(s => s.Reaction), qService * r.Length, "Equilibrio verticale indipendente");
            Near(r.Supports.Sum(s => s.X * s.Reaction), qService * r.Length * r.Length / 2, "Equilibrio momenti indipendente");
            Near(r.DirectCost, r.Quantities.Sum(q => q.Amount * q.Rate), "Computo indipendente");
            Near(r.TotalCost, r.DirectCost * 1.12 * 1.15, "Maggiorazioni");
            Near(r.Concrete, r.Quantities.Where(q => q.Unit == "m³").Sum(q => q.Amount) + r.Supports.Sum(s => s.Piles) * r.PileLength * Math.PI * r.PileDiameter * r.PileDiameter / 4, "Volume totale cls senza duplicazione pali");
            var schedule = BridgeConcept.SupportSchedule(d, r);
            Near(schedule.Sum(s => s.FootingLength * s.FootingWidth * s.FootingThickness), r.Quantities.Single(q => q.Item == "Calcestruzzo plinti").Amount, "Volume plinti");
            Near(schedule.Sum(s => 2 * (s.FootingLength + s.FootingWidth) * s.FootingThickness), r.Quantities.Single(q => q.Item == "Casseforme laterali plinti").Amount, "Casseri plinti");
            double subVolume = 0, maxRatio = 0;
            foreach (var s in schedule)
            {
                double section = s.Type.StartsWith("Antenna") ? 2 * s.Size * s.Size : s.WallWidth > 0 ? s.Size * s.WallWidth : s.Columns * Math.PI * s.Size * s.Size / 4;
                double sv = s.Type == "Spalla" ? r.Width * (s.Height * Math.Max(.6, s.Height / 7) + 3) : section * s.Height + s.CapLength * s.CapWidth * s.CapThickness;
                subVolume += sv;
                double anchor = r.Advanced?.AnchorVolume > 0 && s.Type == "Spalla" ? r.Advanced.AnchorVolume / 2 : 0;
                double demand = r.Supports[s.Number - 1].Reaction + 25 * (sv + anchor + s.FootingLength * s.FootingWidth * s.FootingThickness);
                double capacity = s.Piles > 0 ? s.Piles * Detail(r, "Carico assiale indicativo per palo") : s.FootingLength * s.FootingWidth * Detail(r, "Pressione terreno adottata");
                maxRatio = Math.Max(maxRatio, demand / capacity);
                Check(demand / capacity <= 1 + 1e-8, "Autodimensionamento fondazione oltre soglia");
                if (s.Piles > 0) Check(s.FootingLength >= (Math.Ceiling(Math.Sqrt(s.Piles)) - 1) * 3 * s.PileDiameter + 2 * s.PileDiameter, "Griglia pali fuori plinto");
            }
            Near(subVolume, r.Quantities.Where(q => q.Group == "Sottostrutture" && q.Unit == "m³").Sum(q => q.Amount), "Geometria pile / spalle / antenne");
            Near(maxRatio, Detail(r, "Rapporto carico / riferimento fondazione"), "Rapporto fondazione indipendente");
            records.Add(new { attempted, f.Id, span, soil, foundation, lanes, status = "calculated", r.TotalCost, r.Depth, r.Concrete, maxRatio, piles = r.Supports.Sum(s => s.Piles) });
        }
        File.WriteAllText(Path.Combine(output, "audit-cases.json"), JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }));

        // Independent member equilibrium and net material quantities for the extended families.
        foreach (var (id, length) in new[] { ("tied_arch", 300d), ("truss", 180d), ("cable_stayed", 500d), ("suspension", 800d), ("filler_beam", 60d), ("orthotropic", 240d) })
        foreach (string deck in BridgeConcept.DeckTypes)
        {
            var d = Case(id, length); d["input"]!["deck_type"] = deck;
            var r = BridgeConcept.Calculate(d); var g = r.Advanced!;
            double qd = Detail(r, "Permanenti sull'intero impalcato") * 1.35 + Detail(r, "Traffico equivalente sull'intero impalcato") * 1.5;
            Check(g.GlobalBeam == (id is "filler_beam" or "orthotropic") && (r.Stations.Length > 0) == g.GlobalBeam, "Diagrammi globali non pertinenti");
            if (id == "tied_arch")
            {
                Near(g.Members.First(m => m.Kind == "Catena").Force * 2, qd * 100 * 100 / (8 * 20), "H arco e catena");
                Near(g.Members.Where(m => m.Kind == "Catena").Sum(m => m.Length * m.Count), length * 2, "Sviluppo catene");
            }
            if (id == "cable_stayed") Near(g.Members.Sum(m => m.Count * m.Force * Math.Abs(m.Z2 - m.Z1) / m.Length), qd * length, "Componenti verticali stralli");
            if (id == "suspension")
            {
                Near(g.HorizontalForce, qd * 400 * 400 / (8 * 40), "Spinta cavo principale");
                Check(g.Members.Any(m => m.Kind == "Pendino" && m.X1 < 200) && g.Members.Any(m => m.Kind == "Pendino" && m.X1 > 600), "Campate di riva senza pendini");
                Check(g.Members.Where(m => m.Kind == "Cavo di riva").All(m => m.Z1 >= 0 && m.Z2 >= 0), "Cavi sotto impalcato");
                double uplift = -r.Supports[0].Reaction / ServiceLoad(r) * qd;
                Near(g.AnchorVolume * 25 / 2, uplift + g.HorizontalForce / .5, "Bilancio peso blocco ancoraggio");
            }
            foreach (var m in g.Members)
            {
                bool cable = m.Kind is "Pendino" or "Strallo" or "Cavo principale" or "Cavo di riva";
                double stress = cable ? 600 : m.Force < 0 ? 100 : 180;
                Near(m.Area, Math.Max(1e-5, Math.Abs(m.Force) / (stress * 1000)), "Area da forza / tensione");
            }
            double aSteel = g.Dimensions.Single(t => t.Symbol == "A_acc").Value;
            if (id == "filler_beam") Near(Detail(r, "Area cls impalcato") + aSteel, r.Width * r.Depth, "Acciaio incorporato contato due volte");
            if (g.Orthotropic) Near(Detail(r, "Area cls impalcato"), 0, "Cls indebito in ortotropo");
            var doubled = (JsonObject)d.DeepClone(); foreach (var rate in BridgeConcept.Rates) doubled["rates"]![rate.Key] = 2 * d["rates"].D(rate.Key);
            Near(BridgeConcept.Calculate(doubled).TotalCost, r.TotalCost * 2, "Prezzi lineari su famiglie speciali");
        }
        double ServiceLoad(BridgeConcept.Result r) => Detail(r, "Permanenti sull'intero impalcato") + Detail(r, "Traffico equivalente sull'intero impalcato");

        var manual = Case("psc_i", 120); manual["input"]!["foundation"] = "Pali Ø 1,0 m"; manual["input"]!["pile_count"] = 1; manual["input"]!["footing_size"] = 1;
        var mr = BridgeConcept.Calculate(manual);
        Check(mr.Supports.All(s => s.Piles == 1 && s.FootingSize == 1), "Dimensioni manuali sovrascritte");
        Check(BridgeConcept.OptimizationExclusions(manual, mr, new()).Any(s => s.Contains("Pali non contenuti")), "Plinto manuale incompatibile ammesso");
        var towerManual = Case("cable_stayed", 500); towerManual["input"]!["pier_size"] = 1.5;
        var tm = BridgeConcept.Calculate(towerManual);
        Check(BridgeConcept.OptimizationExclusions(towerManual, tm, new()).Any(s => s.Contains("Compressione antenna")), "Antenna manuale sottodimensionata ammessa");
        var capManual = Case("psc_i", 120); capManual["input"]!["pier_size"] = 2; capManual["input"]!["footing_size"] = 1;
        Check(BridgeConcept.OptimizationExclusions(capManual, BridgeConcept.Calculate(capManual), new()).Any(s => s.Contains("Fusto non contenuto")), "Fusto fuori dal plinto ammesso");
        var archive = Case("psc_i", 120); var expected = BridgeConcept.Calculate(archive);
        archive["rates"]!.AsObject().Remove("steel_ortho"); archive["assumptions"]!.AsObject().Remove("concept_tower");
        string saved = archive.ToJsonString(); Near(BridgeConcept.Calculate(archive).TotalCost, expected.TotalCost, "Migrazione parziale archivio"); Check(archive.ToJsonString() == saved, "Migrazione muta archivio");
        Near(BridgeConcept.Rates.Single(p => p.Key == "rebar").Default, 1.66 * 1000, "Conversione ANAS kg / t");
        // Numerical integration along the actual inclined web, independent of the production section summation.
        foreach (double top in new[] { 1.4, 2.4, 3.4 })
        {
            var u = Case("psc_u", 105); u["input"]!["u_top"] = top;
            var ur = BridgeConcept.Calculate(u); var points = new List<(double A, double Y)>();
            void Strip(double area, double start, double height)
            { for (int n = 0; n < 5000; n++) points.Add((area / 5000, start + (n + .5) * height / 5000)); }
            double webHeight = ur.Depth - ur.Slab - ur.Bottom, slant = Math.Sqrt(webHeight * webHeight + Math.Pow((top - 1.4) / 2, 2));
            Strip(ur.Width * ur.Slab, ur.Depth - ur.Slab, ur.Slab);
            Strip(ur.Girders * 1.4 * ur.Bottom, 0, ur.Bottom);
            Strip(2 * ur.Girders * ur.Web * slant, ur.Bottom, webHeight);
            double center = points.Sum(p => p.A * p.Y) / points.Sum(p => p.A);
            Near(ur.Inertia, points.Sum(p => p.A * Math.Pow(p.Y - center, 2)), "Inerzia U inclinata per integrazione numerica", 1e-6);
            Near(Detail(ur, "Area cls impalcato"), points.Sum(p => p.A), "Area U inclinata per integrazione numerica");
        }
        File.WriteAllText(Path.Combine(output, "audit-dimensions.txt"), $"PASS {checks} controlli. Campagna: {attempted} casi; {calculated} calcolati e verificati; {rejected} incompatibili con le ipotesi, registrati con motivazione. Rifiuti non equiparati a calcoli verificati.");
        return checks;
    }

    internal static int Optimization(string output)
    {
        int checks = 0; var rows = new List<object>();
        void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
        foreach (var (id, length) in new[] { ("psc_i", 120d), ("tied_arch", 300d), ("truss", 180d), ("cable_stayed", 500d), ("suspension", 800d), ("filler_beam", 60d), ("orthotropic", 240d) })
        foreach (var objective in Enum.GetValues<BridgeConcept.OptimizationObjective>())
        {
            var d = Case(id, length); string before = d.ToJsonString();
            var opt = new BridgeConcept.OptimizationOptions { Objective = objective, KeepFamily = true, KeepSpans = true, KeepSection = true,
                KeepFoundation = true, KeepPier = true, DepthPercentMax = 120, DepthPercentStep = 10, Alternatives = 2 };
            var r = BridgeConcept.Optimize(d, opt);
            Check(before == d.ToJsonString(), "Ottimizzatore muta progetto");
            Check(r.Solutions.Length > 0, "Nessuna soluzione: " + id);
            Check(r.Solutions.All(s => s.Data["input"].S("family") == id && s.Trial.Spans == 3), "Vincoli famiglia / campate persi");
            Check(r.Evaluated == r.Trials.Length && r.Admissible == r.Trials.Count(t => t.Admissible), "Contabilità iterazioni");
            foreach (var s in r.Solutions)
            {
                var result = BridgeConcept.Calculate(s.Data);
                Check(result.TotalCost == s.Trial.Cost && result.Carbon == s.Trial.Carbon, "Alternativa non riproducibile");
                Check(s.Pareto == !r.Solutions.Any(o => o.Trial.Cost <= s.Trial.Cost && o.Trial.Carbon <= s.Trial.Carbon && (o.Trial.Cost < s.Trial.Cost || o.Trial.Carbon < s.Trial.Carbon)), "Pareto speciale errato");
            }
            // Enumerate the requested 3-depth grid directly, outside Optimize, and compare its minimum.
            var baseline = BridgeConcept.Calculate(d); var direct = new List<BridgeConcept.Result> { baseline };
            foreach (double factor in new[] { 1d, 1.1, 1.2 })
            {
                var c = (JsonObject)d.DeepClone(); c["input"]!["depth"] = factor == 1 ? baseline.Depth : Math.Ceiling(baseline.Depth * factor * 20) / 20;
                var x = BridgeConcept.Calculate(c); if (BridgeConcept.OptimizationExclusions(c, x, opt).Length == 0) direct.Add(x);
            }
            double mc = direct.Min(x => x.TotalCost), me = direct.Min(x => x.Carbon);
            double Score(BridgeConcept.Result x) => objective switch { BridgeConcept.OptimizationObjective.Cost => x.TotalCost, BridgeConcept.OptimizationObjective.Carbon => x.Carbon, _ => .5 * (x.TotalCost / mc + x.Carbon / me) };
            Check(Math.Abs(r.Solutions[0].Score - direct.Min(Score)) <= 1e-6 * Math.Max(1, r.Solutions[0].Score), "Ottimo diverso dalla griglia enumerata: " + id);
            Check(r.Candidates.Length == Math.Min(2, r.Solutions.Length), "Top N modifica famiglia");
            rows.Add(new { id, objective = objective.ToString(), r.Evaluated, r.Admissible, solutions = r.Solutions.Length, bestCost = r.Solutions[0].Trial.Cost, bestCarbon = r.Solutions[0].Trial.Carbon });
        }
        var reached = new HashSet<string>();
        foreach (double length in new[] { 120d, 480d })
        foreach (var objective in Enum.GetValues<BridgeConcept.OptimizationObjective>())
        {
            var d = Case("psc_i", length);
            var opt = new BridgeConcept.OptimizationOptions { Objective = objective, KeepContinuity = false,
                MaxSpans = 20, DepthPercentMax = 100, PilePercentMax = 100, Alternatives = 10 };
            var r = BridgeConcept.Optimize(d, opt);
            foreach (var t in r.Trials) reached.Add(t.Family);
            Check(r.Solutions.Length > 10, "Ricerca libera troppo ristretta");
            Check(r.Solutions.Zip(r.Solutions.Skip(1)).All(p => p.First.Score <= p.Second.Score), "Graduatoria globale non ordinata");
            double minCost = r.Solutions.Min(s => s.Trial.Cost!.Value), minCarbon = r.Solutions.Min(s => s.Trial.Carbon!.Value);
            foreach (var s in r.Solutions)
            {
                double score = objective switch { BridgeConcept.OptimizationObjective.Cost => s.Trial.Cost!.Value, BridgeConcept.OptimizationObjective.Carbon => s.Trial.Carbon!.Value,
                    _ => .5 * (s.Trial.Cost!.Value / minCost + s.Trial.Carbon!.Value / minCarbon) };
                Check(score == s.Score, "Punteggio globale errato");
                Check(s.Pareto == !r.Solutions.Any(o => o.Trial.Cost <= s.Trial.Cost && o.Trial.Carbon <= s.Trial.Carbon && (o.Trial.Cost < s.Trial.Cost || o.Trial.Carbon < s.Trial.Carbon)), "Pareto globale errato");
            }
            foreach (var s in r.Solutions.Take(10))
            {
                var calculated = BridgeConcept.Calculate(s.Data);
                Check(calculated.TotalCost == s.Trial.Cost && BridgeConcept.OptimizationExclusions(s.Data, calculated, opt).Length == 0, "Top 10 globale non riproducibile");
            }
            rows.Add(new { id = "free_" + length, objective = objective.ToString(), r.Evaluated, r.Admissible, solutions = r.Solutions.Length, bestCost = r.Solutions[0].Trial.Cost, bestCarbon = r.Solutions[0].Trial.Carbon });
        }
        Check(BridgeConcept.Families.All(f => reached.Contains(f.Id)), "Ricerca libera non raggiunge tutte le famiglie");
        var zero = Case("psc_i", 120);
        foreach (var p in BridgeConcept.Rates) zero["rates"]![p.Key] = 0;
        foreach (string key in new[] { "co2_concrete", "co2_rebar", "co2_steel", "co2_pt", "co2_cable" }) zero["assumptions"]![key] = 0;
        var zr = BridgeConcept.Optimize(zero, new() { Objective = BridgeConcept.OptimizationObjective.Balanced, KeepFamily = true,
            KeepSpans = true, KeepPier = true, KeepFoundation = true });
        Check(zr.Solutions.Length > 0 && zr.Solutions.All(s => s.Score == 0 && s.Pareto), "Costi e CO2 nulli causano punteggi non finiti / Pareto errato");
        File.WriteAllText(Path.Combine(output, "audit-optimization.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(output, "audit-optimization.txt"), $"PASS: {checks} controlli; 27 ricerche di campagna più ricerca limite con costi/CO2 nulli, sette tipologie vincolate e due griglie libere su tutte le quattordici famiglie, tre obiettivi; enumerazione diretta, riproducibilità, vincoli, top N e Pareto.");
        return checks;
    }
}
