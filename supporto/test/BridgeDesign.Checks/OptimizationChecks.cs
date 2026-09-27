using Anthea.Calculations;
using X.Core;
using System.Text.Json.Nodes;

internal static class OptimizationChecks
{
    internal static int Run(string output)
    {
        int checks = 0;
        void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
        void Near(double actual, double expected, string message) => Check(Math.Abs(actual - expected) < 1e-7 * Math.Max(1, Math.Abs(expected)), message);
        JsonObject Base() { var d = BridgeConcept.Defaults(); var i = d["input"]!; i["length"] = 120; i["spans"] = 3; i["height"] = 12; i["soil"] = "Roccia"; i["obstacle"] = "Nessuno"; return d; }
        var data = Base(); string before = data.ToJsonString();
        var options = new BridgeConcept.OptimizationOptions { MaxSpans = 12, Alternatives = 50, KeepPier = true, KeepFoundation = true };
        var result = BridgeConcept.Optimize(data, options);
        Check(data.ToJsonString() == before, "Ottimizzazione muta i dati del chiamante");
        Check(result.Admissible > 0 && result.Candidates.Length > 0, "Ricerca senza candidati sul riferimento ammissibile");
        Check(result.Candidates[0].Result.TotalCost <= result.Baseline.TotalCost + 1e-7, "Ottimizzatore costo peggiora un riferimento ammissibile");
        Check(result.Candidates.Zip(result.Candidates.Skip(1)).All(p => p.First.Score <= p.Second.Score), "Graduatoria costo non ordinata");
        foreach (var c in result.Candidates)
        {
            Check(JsonNode.DeepEquals(c.Data["rates"], data["rates"]), "Listino cambiato");
            Check(JsonNode.DeepEquals(c.Data["assumptions"], data["assumptions"]), "Carichi o coefficienti cambiati");
            foreach (string key in new[] { "length", "height", "lanes", "lane_width", "shoulder", "median", "barrier_width", "obstacle", "obstacle_width", "soil", "fc", "fc_sub", "start_pier", "end_pier", "low_carbon", "recycled", "continuous", "pier" })
                Check(JsonNode.DeepEquals(data["input"]![key], c.Data["input"]![key]), "Vincolo violato: " + key);
            Check(BridgeConcept.OptimizationExclusions(c.Data, c.Result, options).Length == 0, "Candidato presentato pur essendo escluso");
            Near(BridgeConcept.Calculate(c.Data).TotalCost, c.Result.TotalCost, "Candidato non riproducibile");
        }
        var again = BridgeConcept.Optimize(data, options);
        Check(result.Candidates.Select(c => c.Data.ToJsonString()).SequenceEqual(again.Candidates.Select(c => c.Data.ToJsonString())), "Ricerca non deterministica");
        foreach (var objective in new[] { BridgeConcept.OptimizationObjective.Carbon, BridgeConcept.OptimizationObjective.Balanced })
        {
            var test = BridgeConcept.Optimize(data, options with { Objective = objective });
            Check(test.Candidates.Length > 0 && test.Candidates.Zip(test.Candidates.Skip(1)).All(p => p.First.Score <= p.Second.Score), "Ordine obiettivo errato");
            if (objective == BridgeConcept.OptimizationObjective.Carbon) Check(test.Candidates[0].Result.Carbon <= test.Baseline.Carbon + 1e-7, "Ottimizzatore CO2 peggiora il riferimento ammissibile");
        }
        var fixedOptions = options with { KeepFamily = true, KeepSpans = true, KeepDepth = true, KeepSection = true };
        var locked = BridgeConcept.Optimize(data, fixedOptions);
        foreach (var c in locked.Candidates)
        {
            Check(c.Result.Family.Id == result.Baseline.Family.Id && c.Result.Spans.SequenceEqual(result.Baseline.Spans), "Tipologia / campate bloccate violate");
            Near(c.Result.Depth, result.Baseline.Depth, "Altezza automatica bloccata non congelata");
            Near(c.Result.Slab, result.Baseline.Slab, "Soletta automatica bloccata non congelata");
            Near(c.Result.TotalCost, result.Baseline.TotalCost, "Tutti i parametri bloccati cambiano il costo");
        }
        Check(BridgeConcept.Optimize(data, options with { MaxDepth = .1 }).Candidates.Length == 0, "Altezza limite impossibile accettata");
        var small = Base(); small["input"]!["depth"] = .6;
        Check(BridgeConcept.Optimize(small, fixedOptions).Candidates.Length == 0, "Sezione troppo bassa promossa come efficiente");
        try { BridgeConcept.Optimize(data, options with { KeepSection = true }); Check(false, "Sezione bloccata senza famiglia ammessa"); } catch (ArgumentException) { checks++; }
        using (var cancellation = new CancellationTokenSource())
        { cancellation.Cancel(); try { BridgeConcept.Optimize(data, options, cancellation.Token); Check(false, "Cancellazione ignorata"); } catch (OperationCanceledException) { checks++; } }
        var expensive = Base(); foreach (var price in BridgeConcept.Rates) expensive["rates"]![price.Key] = data["rates"].D(price.Key) * 2;
        var doubled = BridgeConcept.Optimize(expensive, options);
        Near(doubled.Candidates[0].Result.TotalCost, result.Candidates[0].Result.TotalCost * 2, "Listino non recepito nell’ottimizzazione");
        Near(doubled.Candidates[0].Result.Carbon, result.Candidates[0].Result.Carbon, "Prezzi alterano impronta");
        var free = BridgeConcept.Optimize(data, options with { KeepPier = false, KeepFoundation = false, KeepContinuity = false });
        Check(free.Evaluated > result.Evaluated && free.Admissible > 0, "Scelte liberate non ampliano la ricerca");
        foreach (var c in free.Candidates) Check(c.Result.TotalCost <= result.Candidates[0].Result.TotalCost || c != free.Candidates[0], "Ricerca ampliata perde soluzione precedente");
        Check(BridgeConcept.Optimize(BridgeConcept.Defaults(), new()).Admissible > 0, "Il primo avvio non trova soluzioni sul ponte predefinito");
        // Real site corpus case 0137: obstacle pushes the middle support to x=73.5 m.
        // Uplift on the short continuous end span previously propagated NaN through footing sqrt and costs.
        var uplift = BridgeConcept.Defaults(); var ui = uplift["input"]!;
        ui["family"] = "steel_i"; ui["length"] = 100; ui["height"] = 20; ui["obstacle_width"] = 45; ui["spans"] = 0;
        ui["obstacle"] = "Strada / ferrovia"; ui["soil"] = "Roccia"; ui["pier"] = "Testa a martello"; ui["end_pier"] = true; ui["fc"] = 35;
        try { BridgeConcept.Calculate(uplift); Check(false, "Caso reale con sollevamento produce un risultato non finito"); }
        catch (ArgumentException ex) { Check(ex.Message.Contains("sollevamento"), "Sollevamento non diagnosticato chiaramente"); }

        foreach (var family in BridgeConcept.Families)
        {
            var d = Base(); d["input"]!["family"] = family.Id; d["input"]!["length"] = (family.MinSpan + family.MaxSpan) / 2 * 3; d["input"]!["height"] = 35;
            var r = BridgeConcept.Calculate(d); var rows = BridgeConcept.TechnicalSchedule(d, r); var supports = BridgeConcept.SupportSchedule(d, r);
            Near(rows.Single(x => x.Symbol == "t_s").Value, r.Slab * 1000, "Conversione spessore soletta");
            Near(BridgeConcept.SpanSchedule(r).Sum(s => s.Length), r.Length, "Somma luci esportate");
            Near(BridgeConcept.SpanSchedule(r).Sum(s => s.DevelopedLength), r.Length * r.Girders, "Sviluppo elementi longitudinali");
            Near(supports.Sum(s => s.FootingLength * s.FootingWidth * s.FootingThickness), r.Quantities.Single(q => q.Item == "Calcestruzzo plinti").Amount, "Geometria plinti non ricostruisce il computo");
            Check(supports.Length == r.Spans.Length + 1 && rows.All(t => double.IsFinite(t.Value)), "Prospetto incompleto / non finito");
            if (family.Id == "steel_box")
            {
                double W = r.Width, n = r.Girders, h = r.Depth - r.Slab - .048, inclined = Math.Sqrt(h * h + Math.Pow(h / 4, 2));
                Near(rows.Single(t => t.Symbol == "l_w").Value, inclined, "Sviluppo anima inclinata errato");
                Near((n * ((.4 * W / n) * .024 + 2 * .55 * .024 + 2 * .012 * inclined)) * r.Length * 7.85 * 1.15, r.Steel, "Dimensioni tecniche non ricostruiscono massa cassone");
            }
            if (family.Id == "fcm") Near(rows.Single(t => t.Symbol == "d_eq").Value, r.Depth + (r.PierDepth - r.Depth) / 3, "Altezza media FCM errata");
            string csv = BridgeConceptExport.TechnicalCsv(d, r);
            Check(csv.Contains("Campata 1") && csv.Contains("Appoggio 1") && csv.Contains("Spessore"), "CSV tecnico incompleto");
            File.WriteAllText(Path.Combine(output, family.Id + "_quote.csv"), csv);
        }
        File.WriteAllText(Path.Combine(output, "optimization.txt"), $"PASS: {checks} controlli. Candidati valutati nel caso base: {result.Evaluated}; ammessi: {result.Admissible}. Ricerca ampliata: {free.Evaluated} / {free.Admissible}.\nVincoli, immutabilità, riproducibilità, determinismo, obiettivi, cancellazione, limiti, listini, otto famiglie, quantità ricostruite e anime inclinate.");
        return checks;
    }
}
