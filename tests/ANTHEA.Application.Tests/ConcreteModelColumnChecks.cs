using System.Globalization;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Anthea.Application.Concrete;
using Anthea.Application.Reporting;
using Anthea.Calculations;
using Anthea.Reports;

/// <summary>
/// La colonna modello della sezione c.a. (1.15.0, GPCChecker.Concrete 0.27; fase 2 nella 1.16.0, Concrete 0.29): l'adattatore di ANTHEA sul caso
/// R400-3+3D24-l0-12 della libreria (attesi del README dei riferimenti della libreria e della pagina ca.colonna-modello §10, riferimento Python
/// indipendente, sezione netta), le unità di ANTHEA, le opzioni predefinite, i rifiuti e gli avvisi tradotti, l'esportazione JSON e il capitolo
/// della relazione di progetto; il dominio ridotto e l'inviluppo delle combinazioni SLU sui casi R400 a l0 = 6 m dello stesso riferimento.
/// </summary>
internal static class ConcreteModelColumnChecks
{
    /// <summary>
    /// I dati del caso R400-3+3D24 (400 × 400, 3Ø24 + 3Ø24 a 40 mm dai bordi all'asse: copriferro 18 + staffa 10 + 12, C25/30, B450C
    /// elastico-perfettamente plastico, Es = 200000 MPa, NTC 2018) sulla fixture F3 «default», preparati come per la relazione.
    /// </summary>
    internal static JsonObject R400Data(string root)
    {
        var data = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "tests/fixtures/f3/inputs/str_palo--default.json")))!["data"]!.AsObject();
        var input = data["input"]!.AsObject();
        foreach (var (key, value) in new (string, string)[] { ("width_mm", "400"), ("height_mm", "400"), ("cover_mm", "18"), ("top_bar_count", "3"),
            ("top_bar_diameter_mm", "24"), ("bottom_bar_count", "3"), ("bottom_bar_diameter_mm", "24"), ("side_bar_count_per_side", "0"),
            ("classe_cls", "C25/30"), ("materiale_cls_nome", "C25/30") })
            input[key] = value;
        input["fck_mpa"] = 25;
        return data;
    }

    /// <summary>Le opzioni del caso della fonte: NEd = 500 kN, l0 = 12 m, l = 6 m, c = 10, nessuna imperfezione né eccentricità minima, φef = 0.</summary>
    internal static JsonObject R400Options(string? moment = "137.5") => J.Obj(("N", "-500"), ("theta", "0"), ("M0Ed", moment), ("l0", "12"), ("l", "6"),
        ("imperfezione", ConcreteModelColumn.ImperfectionModes[4]), ("eccentricita_minima", ConcreteModelColumn.MinimumEccentricityModes[3]));

    internal static async Task<int> Run(string root, string output)
    {
        int checks = 0;
        void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException("Colonna modello: " + message); checks++; }
        void Near(double expected, double? actual, double tolerance, string what)
            => Check(actual is double a && Math.Abs(a - expected) <= Math.Abs(expected) * tolerance, $"{what}: atteso {expected:G9}, ottenuto {actual:G9}");
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("it-IT");
        try
        {
            var data = R400Data(root);
            ConcreteSheetPreparation.PrepareReport(data);
            var input = data["input"]!.AsObject(); var settings = data["workspace_ca"]!.AsObject();
            string before = data.ToJsonString();

            // ---- the case R400-3+3D24-l0-12 (README of the references of the library; independent Python reference, net section)
            var r = ConcreteModelColumn.Calculate(input, settings, R400Options());
            File.WriteAllText(Path.Combine(output, "colonna-modello-R400.json"), J.Node(r)!.ToJsonString(J.Options));
            Check(data.ToJsonString() == before, "il calcolo non modifica i dati");
            Near(156.083, r.Capacity.MaximumFirstOrderKnm, 2e-3, "M1Rd,max [kNm] del riferimento indipendente");
            Near(157.25, r.Capacity.MaximumFirstOrderKnm, 0.015, "M1Rd,max [kNm] della fonte (sezione lorda)");
            Near(1.069899e-2, r.Capacity.CurvatureAtMaximum, 0.02, "χ* [1/m]");
            Near(154.07, r.Capacity.DeflectionAtMaximum, 0.02, "e2* [mm]");
            Near(3.092206e-2, r.Capacity.UltimateCurvature, 2e-3, "χu [1/m]");
            Near(246.264, r.Capacity.UltimateMomentKnm, 2e-3, "M(χu) [kNm]");
            Check(r.Capacity.Instability && r.Capacity.Mode == "instabilità", "modo instabilità");
            var e = r.Equilibrium!;
            Check(e.Found && e.Verified && r.Verified == true && r.Outcome == "Verificata", "equilibrio a 137,5 kNm verificato");
            Near(8.831816e-3, e.Curvature, 2e-3, "χeq [1/m]");
            Near(201.0891, e.DesignKnm, 2e-3, "MEd [kNm]");
            Near(137.5 / 156.083, e.Ratio, 2e-3, "η");
            Near(137.5, e.FirstOrderKnm, 1e-12, "M0Ed,tot = M0Ed senza imperfezione né minimo");
            // ---- units of ANTHEA: kN, m, kN·m², 1/m, ‰, mm
            Check(r.AxialKn == -500 && r.EffectiveLength == 12 && r.MemberLength == 6 && r.CurvatureFactor == 10 && r.DirectionDegrees == 0, "N [kN], l0 e l [m], c, θ");
            Near(7200, r.SecondOrderStiffness, 1e-12, "k = NEd·l0²/c [kN·m²]");
            Check(r.Points.Count >= 201 && r.Points.All(p => Math.Abs(p.SecondOrderKnm - r.SecondOrderStiffness * p.Curvature) <= 1e-9 * Math.Max(1, p.SecondOrderKnm))
                && r.Points.All(p => Math.Abs(p.MomentKnm - p.SecondOrderKnm - p.FirstOrderCapacityKnm) <= 1e-9 * Math.Max(1, p.MomentKnm)),
                "punti: M2 = k·χ con χ in 1/m e momenti in kNm; M − M2");
            Check(r.Points.Count(p => p.Maximum) == 1 && r.Points.Single(p => p.Maximum).Curvature == r.Capacity.CurvatureAtMaximum
                && r.Points.Last().Ultimate && Math.Abs(r.Points.Last().ConcreteCompressionStrain - 3.5) < 1e-9, "punto del massimo; ultimo punto a εcu = 3,5 ‰");
            Near(115.470, r.Section.RadiusOfGyration, 1e-5, "i [mm]");
            Near(103.923, r.Slenderness.Lambda, 1e-5, "λ");
            Near(25 / Math.Sqrt(0.22058823529411767), r.Slenderness.Limit, 1e-6, "λlim NTC 2018 [4.1.41]");
            Check(!r.Slenderness.Negligible && r.Slenderness.Text == "effetti del secondo ordine da considerare", "frase della snellezza");
            Near(1, r.Nominal.Kr, 1e-12, "Kr");
            Near(1.207729468599034e-2, r.Nominal.BasicCurvature, 1e-6, "1/r0 [1/m]");
            Near(173.91, r.Nominal.Deflection, 1e-4, "e2 nominale [mm]");
            Near(86.957, r.Nominal.SecondOrderKnm, 1e-4, "M2 nominale [kNm]");
            Near(1.9565, r.Section.YieldStrain, 1e-3, "εyd [‰]");
            var axial = r.Trace.Single(s => s.Code == "AxialForce"); var stiffness = r.Trace.Single(s => s.Code == "SecondOrderStiffness");
            var ultimate = r.Trace.Single(s => s.Code == "UltimateCurvature"); var maximum = r.Trace.Single(s => s.Code == "MaximumFirstOrderMoment");
            Check(axial.Value == 500 && axial.Unit == "kN" && stiffness.Unit == "kN·m²" && Math.Abs(stiffness.Value!.Value - 7200) < 1e-6 && ultimate.Unit == "1/m"
                && maximum.Unit == "kNm" && maximum.Value == r.Capacity.MaximumFirstOrderKnm, "traccia nelle unità di ANTHEA");
            Check(r.Status.StartsWith("Colonna modello (EN 1992-1-1 5.8.6(6) e 5.8.8.2; NTC 2018", StringComparison.Ordinal) && !r.Status.Contains("Model column", StringComparison.Ordinal)
                && r.Status.Contains("instabilità", StringComparison.Ordinal), "stato in italiano: " + r.Status);
            Check(r.Warnings.Count == 1 && r.Warnings[0].StartsWith("φef = 0 senza le tre condizioni", StringComparison.Ordinal), "avviso di 5.8.4(4) tradotto (λ > 75)");
            Check(r.Inputs.Any(i => i.Label.StartsWith("Imperfezione", StringComparison.Ordinal) && i.Value == "Nessuna"), "righe dei dati per la relazione");
            // ---- the curve and the markers of the charts come from the points of the library
            var chart = ConcreteModelColumnPresentation.CapacityChart(r);
            Check(chart.Series.Count == 4 && chart.Series[0].Points.Count == r.Points.Count && chart.Markers.Count == 4
                && chart.Series[1].Points.Zip(r.Points).All(t => t.First[0] == t.Second.Curvature && t.First[1] == e.FirstOrderKnm + t.Second.SecondOrderKnm),
                "grafico M(χ): curva, momento esterno, tangente, M2, quattro punti");
            var first = ConcreteModelColumnPresentation.FirstOrderChart(r);
            Check(first.Series.Count == 3 && first.Markers.Single().Y == r.Capacity.MaximumFirstOrderKnm && first.Series[1].Points.All(p => p[1] == r.Capacity.MaximumFirstOrderKnm),
                "grafico M − M2 con il massimo (punto e retta) e la retta M0Ed,tot");
            // CSV: i punti della curva come nella 1.15.0, poi (1.16.0) il blocco del dominio ridotto
            string[] csvBlocks = ConcreteModelColumnPresentation.Csv(r).Replace("\r\n", "\n").Split("\n\n");
            Check(csvBlocks.Length == 2 && csvBlocks[0].Split('\n', StringSplitOptions.RemoveEmptyEntries).Length == r.Points.Count + 1
                && csvBlocks[0].StartsWith("chi_1_m;M_kNm;M2_kNm;", StringComparison.Ordinal) && csvBlocks[1].StartsWith("dominio_theta_gradi;phi_ef;N_kN;", StringComparison.Ordinal)
                && csvBlocks[1].Split('\n', StringSplitOptions.RemoveEmptyEntries).Length == 43, "CSV: punti della curva, poi i 42 punti del dominio ridotto");
            var cards = ConcreteModelColumnPresentation.Cards(r);
            Check(cards.Count == 7 && cards.Single(c => c.IsCheck).Passed == true && cards[^1].Title == "Avviso", "riquadri dei risultati");
            // ---- 1.16.0: the reduced domain of the single action: 41 points of the grid (40 intervals by default) and the N of the action
            Check(r.Source == ConcreteModelColumn.ActionSources[0] && r.Combinations.Count == 0 && r.Governing is null && r.Domains.Count == 1 && r.Theta == 0,
                "azione singola: un dominio ridotto, nessuna combinazione");
            var domain = r.Domains[0];
            Check(domain.Points.Count == 42 && domain.Points[0].AxialKn == 0 && domain.Points[^1].AxialKn == domain.CompressionResistanceKn && domain.DirectionDegrees == 0
                && domain.EffectiveCreepRatio == 0 && domain.MinimumEccentricity is null && domain.Points.All(p => !p.Rejected), "dominio: griglia di 41 punti e l'N dell'azione");
            Near(-3290.345, domain.CompressionResistanceKn, 1e-4, "NRd,c [kN] del riferimento");
            Near(174.198, domain.Points[0].MaximumFirstOrderKnm, 2e-3, "M1Rd,max a N = 0 [kNm] del riferimento");
            Check(domain.Points.Single(p => p.AxialKn == -500).MaximumFirstOrderKnm == r.Capacity.MaximumFirstOrderKnm
                && domain.Points.All(p => p.MaximumFirstOrderKnm <= p.SectionKnm + 1e-9), "il punto dell'azione è la capacità della colonna; M1Rd,max ≤ MRd");
            var domainChart = ConcreteModelColumnPresentation.DomainChart(r);
            Check(domainChart.Series.Count == 2 && domainChart.Series[0].Points.Count == 42 && domainChart.Markers.Single().Label == "Ed" && domainChart.Markers[0].X == e.FirstOrderKnm
                && domainChart.Markers[0].Y == -500 && domainChart.YLabel == "N [kN]", "grafico del dominio ridotto: M1Rd,max, MRd della sezione, l'azione");

            // ---- defaults of the norm: EN 5.2(7)a imperfection (l = 6 m: ei = 24,495 mm) and the minimum of NTC (l0/200 = 60 mm), as minimum of the total
            var defaults = ConcreteModelColumn.Calculate(input, settings, J.Obj(("N", "-500"), ("M0Ed", "137.5"), ("l0", "12"), ("l", "6")));
            Near(24.4949, defaults.Imperfection.Eccentricity, 1e-5, "ei EN 5.2(7)a [mm]");
            Near(60, defaults.FirstOrder.MinimumEccentricity, 1e-12, "e0 NTC [mm]");
            Near(137.5 + 500 * 0.0244949, defaults.FirstOrder.TotalKnm, 1e-5, "M0Ed,tot = M0Ed + NEd·ei");
            Check(!defaults.FirstOrder.MinimumGoverns && defaults.CurvatureFactor == 10 && defaults.Creep.EffectiveRatio == 0, "c = 10, φef = 0, minimo non governante");
            // ---- capacity only
            var capacity = ConcreteModelColumn.Calculate(input, settings, R400Options(null));
            Check(capacity.Equilibrium is null && capacity.Verified is null && capacity.Outcome == "Solo capacità" && capacity.Status.EndsWith("solo capacità.", StringComparison.Ordinal)
                && ConcreteModelColumnPresentation.CapacityChart(capacity).Series.Count == 3, "solo capacità con M0Ed vuoto");
            Near(r.Capacity.MaximumFirstOrderKnm, capacity.Capacity.MaximumFirstOrderKnm, 1e-12, "la capacità non dipende da M0Ed");
            // ---- creep from (5.19), without imperfection: φef = φ(∞, t0)·M0Eqp/M0Ed
            var creepOptions = R400Options(); creepOptions["viscosita"] = ConcreteModelColumn.CreepModes[2];
            creepOptions["phi_inf"] = "2"; creepOptions["M0Eqp"] = "80"; creepOptions["NEqp"] = "-350";
            var creep = ConcreteModelColumn.Calculate(input, settings, creepOptions);
            Near(2 * 80 / 137.5, creep.Creep.EffectiveRatio, 1e-9, "φef della (5.19)");
            Check(creep.Capacity.MaximumFirstOrderKnm < r.Capacity.MaximumFirstOrderKnm, "la viscosità riduce M1Rd,max");
            var assigned = R400Options(); assigned["viscosita"] = ConcreteModelColumn.CreepModes[1]; assigned["phi_ef"] = "1";
            Near(147.532, ConcreteModelColumn.Calculate(input, settings, assigned).Capacity.MaximumFirstOrderKnm, 2e-3, "φef = 1 (riferimento indipendente)");
            // ---- N = 0: warning translated, infinite limits written as null in the JSON
            var zero = R400Options(); zero["N"] = "0";
            var none = ConcreteModelColumn.Calculate(input, settings, zero);
            Near(174.198, none.Capacity.MaximumFirstOrderKnm, 2e-3, "N = 0: M1Rd,max = M(χu)");
            Check(none.Warnings.Any(w => w.StartsWith("N = 0: nessun effetto del secondo ordine", StringComparison.Ordinal)) && none.Slenderness.Limit is null
                && J.Node(none) is JsonObject json && json["Slenderness"]!["Limit"] is null, "N = 0: avviso tradotto, λlim infinito come null");

            // ---- 1.16.0: all the combinations SLU, case of ModelColumnEnvelopeTests of the library (R400, l0 = 6 m, l = 3 m, no imperfection, no
            // minimum, φef = 0): capacities and ratios of the independent reference; C7 governs (160/169,407), C4 in tension, C3 in θ + 180°
            var combined = (JsonObject)data.DeepClone();
            combined["combinazioni"]!["SLU"] = new JsonArray(new (string Name, string N, string Mx, string My)[] { ("C1", "-1000", "180", "0"), ("C2", "-2000", "100", "0"),
                ("C3", "-1000", "-150", "0"), ("C4", "200", "50", "0"), ("C5", "-2500", "40", "30"), ("C6", "-1000", "120", "0"), ("C7", "-1500", "160", "0") }
                .Select(c => (JsonNode)J.Obj(("id", c.Name), ("nome", c.Name), ("azioni", new[] { c.N, c.Mx, c.My }))).ToArray());
            var envelopeOptions = J.Obj(("azioni", ConcreteModelColumn.ActionSources[1]), ("l0", "6"), ("l", "3"), ("imperfezione", ConcreteModelColumn.ImperfectionModes[4]),
                ("eccentricita_minima", ConcreteModelColumn.MinimumEccentricityModes[3]), ("intervalli_dominio", "10"));
            var combinedInput = combined["input"]!.AsObject(); var combinedSettings = combined["workspace_ca"]!.AsObject();
            var progress = new List<string>();
            var all = ConcreteModelColumn.Calculate(combinedInput, combinedSettings, envelopeOptions, default, ConcreteSheetPreparation.ReadActions(combined), new Recorder(progress));
            File.WriteAllText(Path.Combine(output, "colonna-modello-combinazioni.json"), J.Node(all)!.ToJsonString(J.Options));
            Check(all.Combinations.Count == 7 && all.Governing == "C7" && all.Verified == true && all.Outcome == "Verificata (tutte le combinazioni)"
                && all.Source == "Combinazioni SLU" && all.AxialKn == -1500, "inviluppo: 7 combinazioni, governa C7, verificato");
            double[] expectedCapacity = { 223.656088, 121.874295, 223.656088, double.NaN, 65.607628, 223.656088, 169.406963 };
            double[] expectedMoment = { 180, 100, 150, double.NaN, 40, 120, 160 };
            for (int i = 0; i < 7; i++)
            {
                var c = all.Combinations[i];
                if (i == 3) { Check(c.Status == "Trazione" && c.Outcome == "Trazione: non pertinente" && c.Ratio is null && c.DirectionDegrees is null, "C4 di trazione: non pertinente"); continue; }
                Near(expectedCapacity[i], c.MaximumFirstOrderKnm, 2e-3, c.Name + " M1Rd,max [kNm] del riferimento");
                Near(expectedMoment[i] / expectedCapacity[i], c.Ratio, 2e-3, c.Name + " η");
                Check(c.Status == "Verificata" && c.FirstOrderTotalKnm == expectedMoment[i] && c.State == "SLU", c.Name + " verificata, M0Ed,tot = |Mθ|");
            }
            Check(all.Combinations[2].DirectionDegrees == 180 && all.Combinations[2].MomentKnm == -150 && all.Combinations[4].TransverseNeglected
                && all.Combinations[4].TransverseKnm == 30 && all.Combinations.Count(c => c.Governing) == 1 && all.Combinations[6].Governing && all.Combinations[6].Shown,
                "C3 in θ + 180°, C5 con il momento normale a θ, C7 governa ed è mostrata");
            Near(169.406963, all.Capacity.MaximumFirstOrderKnm, 2e-3, "riquadri e grafici della combinazione C7");
            Check(all.Warnings[0].StartsWith("Momento del primo ordine normale alla direzione θ in 1 combinazioni", StringComparison.Ordinal), "avviso del momento normale a θ: " + all.Warnings[0]);
            Check(all.Domains.Count == 2 && all.Domains[0].DirectionDegrees == 0 && all.Domains[1].DirectionDegrees == 180 && all.Domains[0].Points.Count == 11 + 4
                && all.Domains[1].Points.Count == 11 + 1, "domini di θ e θ + 180°: griglia di 11 punti e gli N delle combinazioni");
            Near(121.874295, all.Domains[0].Points.Single(p => p.AxialKn == -2000).MaximumFirstOrderKnm, 2e-3, "dominio a N = 2000 kN [kNm] del riferimento");
            Check(all.Status.StartsWith("Colonna modello, inviluppo di 7 combinazioni SLU (θ = 0°): 6 verificate, 0 non verificate, 1 di trazione", StringComparison.Ordinal)
                && all.Status.Contains("governa «C7»", StringComparison.Ordinal), "stato dell'inviluppo: " + all.Status);
            Check(progress.Count(p => p.StartsWith("Colonna modello: combinazione ", StringComparison.Ordinal)) == 7 && progress[6] == "Colonna modello: combinazione 7 di 7…"
                && progress.Count(p => p.StartsWith("Colonna modello: dominio ridotto", StringComparison.Ordinal)) == 15 + 12, "avanzamento: 7 combinazioni, poi i punti dei domini");
            var allCards = ConcreteModelColumnPresentation.Cards(all);
            Check(allCards[0].Title == "Inviluppo delle combinazioni" && allCards[0].IsCheck && allCards[0].Passed == true && allCards[0].Value.Contains("governa «C7»", StringComparison.Ordinal)
                && allCards.Any(c => c.Title == "Verifica della combinazione «C7»"), "riquadri: inviluppo, poi la combinazione C7");
            var allText = ConcreteModelColumnPresentation.Text(all);
            Check(allText.Contains("Combinazioni (Mθ = Mx·cos θ + My·sin θ, θ = 0°)", StringComparison.Ordinal) && allText.Contains(" · governa", StringComparison.Ordinal)
                && allText.Contains("Passaggi della combinazione «C7»", StringComparison.Ordinal), "testo: tabella delle combinazioni e passaggi di C7");
            string[] allCsv = ConcreteModelColumnPresentation.Csv(all).Replace("\r\n", "\n").Split("\n\n");
            Check(allCsv.Length == 3 && allCsv[2].StartsWith("combinazione;stato_limite;N_kN;", StringComparison.Ordinal) && allCsv[2].Split('\n', StringSplitOptions.RemoveEmptyEntries).Length == 8,
                "CSV: punti, domini, una riga per combinazione");
            var allChart = ConcreteModelColumnPresentation.DomainChart(all);
            Check(allChart.Series.Count == 4 && allChart.Markers.Count == 6 && allChart.Markers.Single(m => m.Label == "C7").Color == "#16703B"
                && allChart.Markers.Single(m => m.Y == -1000 && m.X < 0).X == -150, "grafico: due domini, sei azioni compresse, C3 con il momento negativo");
            // a combination not verified and one beyond NRd,c: the outcome of the envelope
            var failing = (JsonObject)combined.DeepClone();
            failing["combinazioni"]!["SLU"]!.AsArray().Add(J.Obj(("id", "C8"), ("nome", "C8"), ("azioni", new[] { "-3000", "25", "0" })));
            failing["combinazioni"]!["SLU"]!.AsArray().Add(J.Obj(("id", "C9"), ("nome", "C9"), ("azioni", new[] { "-3400", "0", "0" })));
            var failed = ConcreteModelColumn.Calculate(failing["input"]!.AsObject(), failing["workspace_ca"]!.AsObject(), envelopeOptions, default, ConcreteSheetPreparation.ReadActions(failing));
            var c8 = failed.Combinations.Single(c => c.Name == "C8"); var c9 = failed.Combinations.Single(c => c.Name == "C9");
            Near(25 / 17.828513, c8.Ratio, 2e-3, "C8 η del riferimento");
            Check(failed.Verified == false && failed.Outcome == "Non verificata (2 combinazioni)" && c8.Outcome == "Non verificata (instabilità)" && c9.BeyondResistance && c9.Ratio is null
                && c9.Governing && !c9.Shown && c8.Shown && failed.Governing == "C8" && c9.Outcome.StartsWith("Non verificata: |N| = 3400 kN oltre la resistenza a compressione centrata", StringComparison.Ordinal),
                "C8 non verificata, C9 oltre NRd,c (governa, η infinito), mostrata C8: " + c9.Outcome);
            // the messages of the combinations
            string Envelope(JsonObject source, JsonObject options)
            {
                try { ConcreteModelColumn.Calculate(source["input"]!.AsObject(), source["workspace_ca"]!.AsObject(), options, default, ConcreteSheetPreparation.ReadActions(source)); return ""; }
                catch (ArgumentException error) { return error.Message; }
            }
            var noCombination = (JsonObject)combined.DeepClone(); noCombination["combinazioni"]!["SLU"] = new JsonArray();
            Check(Envelope(noCombination, envelopeOptions) == "Nessuna combinazione SLU nella scheda «Combinazioni»: inserirle, oppure scegliere l'azione singola.", "nessuna combinazione");
            var tension = (JsonObject)combined.DeepClone();
            tension["combinazioni"]!["SLU"] = new JsonArray(J.Obj(("id", "T"), ("nome", "T"), ("azioni", new[] { "100", "10", "0" })));
            Check(Envelope(tension, envelopeOptions).StartsWith("Nessuna combinazione compressa", StringComparison.Ordinal), "sola trazione");
            var wrong = (JsonObject)combined.DeepClone(); wrong["combinazioni"]!["SLU"]![0]!["azioni"]![1] = "x";
            Check(Envelope(wrong, envelopeOptions) == "C1 · Mx: inserire un numero finito.", "valore non numerico con il nome della combinazione: " + Envelope(wrong, envelopeOptions));
            var choice = (JsonObject)envelopeOptions.DeepClone(); choice["azioni"] = "?";
            Check(Envelope(combined, choice) == "Azioni: scelta non riconosciuta.", "scelta delle azioni sconosciuta");
            var intervals = (JsonObject)envelopeOptions.DeepClone(); intervals["intervalli_dominio"] = "3";
            Check(Envelope(combined, intervals) == "Intervalli del dominio ridotto: intero tra 4 e 200.", "intervalli del dominio");
            Check(ConcreteModelColumn.Defaults["azioni"] == ConcreteModelColumn.ActionSources[0] && ConcreteModelColumn.Defaults["intervalli_dominio"] == "40",
                "predefiniti della 1.16.0: azione singola, 40 intervalli");

            // ---- rejections in Italian
            string Refused(JsonObject? options, Action<JsonObject>? change = null)
            {
                var copy = (JsonObject)settings.DeepClone(); change?.Invoke(copy);
                try { ConcreteModelColumn.Calculate(input, copy, options); return ""; }
                catch (ArgumentException error) { return error.Message; }
            }
            JsonObject With(string key, string value) { var o = R400Options(); o[key] = value; return o; }
            Check(Refused(null).StartsWith("Inserire la lunghezza libera di inflessione l0", StringComparison.Ordinal), "l0 mancante");
            Check(Refused(J.Obj(("l0", "12"))).StartsWith("Inserire la lunghezza dell'elemento l", StringComparison.Ordinal), "l mancante");
            Check(Refused(With("N", "100")).StartsWith("N di trazione: la colonna modello vale per gli elementi compressi", StringComparison.Ordinal), "N di trazione");
            string beyond = Refused(With("N", "-4000"));
            Check(beyond.StartsWith("|N| = 4000 kN oltre la resistenza a compressione centrata della sezione NRd,c = 3290,", StringComparison.Ordinal), "N oltre NRd,c: " + beyond);
            Check(Refused(With("c", "7")).StartsWith("Dati della colonna modello non validi", StringComparison.Ordinal), "c fuori da [8; 10]");
            Check(Refused(With("passi", "5")).StartsWith("Passi di curvatura: intero tra 10 e 500", StringComparison.Ordinal), "passi");
            Check(Refused(With("imperfezione", ConcreteModelColumn.ImperfectionModes[3])) == "Imperfezione assegnata: inserire ei.", "ei mancante");
            Check(Refused(With("viscosita", ConcreteModelColumn.CreepModes[2])) == "Viscosità dalla (5.19): inserire φ(∞, t0).", "φ(∞, t0) mancante");
            Check(Refused(With("imperfezione", "?")) == "Imperfezione: scelta non riconosciuta.", "scelta sconosciuta");
            string standard = Refused(R400Options(), s => { s["normativa"] = "Model Code 2010"; s["coefficienti"] = ConcreteStandards.Defaults("Model Code 2010"); });
            Check(standard.StartsWith("Colonna modello disponibile per NTC 2018, CNR-DT 200", StringComparison.Ordinal) && standard.EndsWith("non per Model Code 2010.", StringComparison.Ordinal),
                "norma senza profilo: " + standard);
            Check(Refused(R400Options(), s => s["trefoli"] = new JsonArray(J.Obj(("id", "T1")))) == "Colonna modello con trefoli: disponibile per la sola armatura ordinaria.", "trefoli");
            foreach (var message in new[] { beyond, standard, Refused(With("c", "7")) })
                Check(!message.Contains("Model column", StringComparison.Ordinal), "nessun messaggio in inglese");
            foreach (var warning in Enum.GetValues<GPC.Checkers.Concrete.Slenderness.ModelColumnWarning>())
                Check(ConcreteModelColumn.Warning(warning) != warning.ToString() && !ConcreteModelColumn.Warning(warning).Contains("Model column", StringComparison.Ordinal),
                    "avviso tradotto: " + warning);
            foreach (var mode in Enum.GetValues<GPC.Checkers.Concrete.Slenderness.ModelColumnMode>())
                Check(ConcreteModelColumn.Mode(mode) is "instabilità" or "rottura della sezione", "modo tradotto: " + mode);

            // ---- the project report: the chapter after the M–χ only for the sheets with the options of the column
            var renderer = new RecordingRenderer();
            var builder = new ModuleReportService(renderer);
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            JsonObject Sheet(JsonObject source) => J.Obj(("nome", "Colonna modello nel progetto"), ("modulo_id", "str_palo"), ("dati", source.DeepClone()));
            settings["momento_curvatura"]!["passi"] = "10";
            var plain = await builder.BuildAsync(Sheet(data), default);
            var plainTexts = plain.Document!.GetXml("word/document.xml").Descendants(w + "t").Select(t => t.Value).ToList();
            Check(renderer.Request!.Calculation.Values[ConcreteModelColumn.OptionsKey] is null && !plainTexts.Contains("Colonna modello"), "foglio senza opzioni: nessun capitolo, nessun risultato");
            settings[ConcreteModelColumn.OptionsKey] = R400Options();
            var chapter = await builder.BuildAsync(Sheet(data), default);
            var exported = renderer.Request!.Calculation.Values[ConcreteModelColumn.OptionsKey];
            Check(chapter.Error is null && exported is JsonObject && JsonNode.DeepEquals(exported, J.Node(r)), "risultato esportato uguale al calcolo diretto");
            var xml = chapter.Document!.GetXml("word/document.xml");
            var texts = xml.Descendants(w + "t").Select(t => t.Value).ToList();
            Check(texts.IndexOf("Colonna modello") > texts.IndexOf("Diagramma momento–curvatura") && texts.Contains("Passaggi del calcolo") && texts.Contains("Punti della curva")
                && texts.Contains("Esito: Verificata."), "capitolo della colonna modello dopo il M–χ");
            var table = xml.Descendants(w + "tbl").Single(t => t.Descendants(w + "t").Any(v => v.Value == "M − M2 [kNm]"));
            Check(table.Elements(w + "tr").Count() == r.Points.Count + 1, "tabella dei punti");
            File.WriteAllBytes(Path.Combine(output, "report-colonna-modello.docx"), DocxWriter.Write(chapter.Document));
            // 1.16.0: the chapter of the combinations, with their table and the reduced domain
            combinedSettings["momento_curvatura"]!["passi"] = "10";
            combinedSettings[ConcreteModelColumn.OptionsKey] = envelopeOptions.DeepClone();
            var envelopeChapter = await builder.BuildAsync(Sheet(combined), default);
            var envelopeXml = envelopeChapter.Document!.GetXml("word/document.xml");
            var envelopeTexts = envelopeXml.Descendants(w + "t").Select(t => t.Value).ToList();
            Check(envelopeChapter.Error is null && envelopeTexts.Contains("Combinazioni") && envelopeTexts.Contains("Dominio ridotto N–M1Rd,max") && envelopeTexts.Contains("C7 (governa)")
                && envelopeTexts.IndexOf("Combinazioni") < envelopeTexts.IndexOf("Passaggi del calcolo") && envelopeTexts.IndexOf("Dominio ridotto N–M1Rd,max") > envelopeTexts.IndexOf("Punti della curva")
                && envelopeTexts.Contains("Esito: Verificata (tutte le combinazioni)."), "capitolo con le combinazioni e il dominio ridotto");
            var combinationTable = envelopeXml.Descendants(w + "tbl").Single(t => t.Descendants(w + "t").Any(v => v.Value == "M⊥ [kNm]"));
            Check(combinationTable.Elements(w + "tr").Count() == 8 && envelopeXml.Descendants(w + "tbl").Count(t => t.Descendants(w + "t").Any(v => v.Value == "MRd sezione [kNm]")) == 2,
                "tabella delle 7 combinazioni e dei due domini");
            File.WriteAllBytes(Path.Combine(output, "report-colonna-modello-combinazioni.docx"), DocxWriter.Write(envelopeChapter.Document));
            settings[ConcreteModelColumn.OptionsKey] = With("N", "100");
            var refused = await builder.BuildAsync(Sheet(data), default);
            Check(refused.Error?.Contains("Colonna modello") == true && refused.Document!.GetXml("word/document.xml").Root!.Value.Contains("Colonna modello non disponibile: N di trazione"),
                "rifiuto nel capitolo e fra gli errori");
            Console.WriteLine($"Colonna modello: {checks} controlli (caso R400-3+3D24-l0-12, unità, predefiniti, viscosità, rifiuti, relazione)");
            return checks;
        }
        finally { CultureInfo.CurrentCulture = culture; }
    }

    /// <summary>Synchronous progress (Progress&lt;T&gt; posts to the thread pool).</summary>
    private sealed class Recorder(List<string> messages) : IProgress<string>
    {
        public void Report(string value) => messages.Add(value);
    }

    private sealed class RecordingRenderer : IFigureRenderer
    {
        internal ModuleReportRequest? Request;
        public Task<IReadOnlyList<ReportFigure>> RenderAsync(ModuleReportRequest request, CancellationToken cancellation)
        { Request = request; return Task.FromResult<IReadOnlyList<ReportFigure>>([]); }
    }
}
