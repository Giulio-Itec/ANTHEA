using System.Globalization;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Anthea.Application.Concrete;
using Anthea.Application.Reporting;
using Anthea.Calculations;
using Anthea.Reports;

/// <summary>
/// La colonna modello della sezione c.a. (1.15.0, GPCChecker.Concrete 0.27): l'adattatore di ANTHEA sul caso R400-3+3D24-l0-12 della libreria
/// (attesi del README dei riferimenti della libreria e della pagina ca.colonna-modello §10, riferimento Python indipendente, sezione netta), le
/// unità di ANTHEA, le opzioni predefinite, i rifiuti e gli avvisi tradotti, l'esportazione JSON e il capitolo della relazione di progetto.
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
            string csv = ConcreteModelColumnPresentation.Csv(r);
            Check(csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length == r.Points.Count + 1 && csv.StartsWith("chi_1_m;M_kNm;M2_kNm;", StringComparison.Ordinal), "CSV");
            var cards = ConcreteModelColumnPresentation.Cards(r);
            Check(cards.Count == 7 && cards.Single(c => c.IsCheck).Passed == true && cards[^1].Title == "Avviso", "riquadri dei risultati");

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
            settings[ConcreteModelColumn.OptionsKey] = With("N", "100");
            var refused = await builder.BuildAsync(Sheet(data), default);
            Check(refused.Error?.Contains("Colonna modello") == true && refused.Document!.GetXml("word/document.xml").Root!.Value.Contains("Colonna modello non disponibile: N di trazione"),
                "rifiuto nel capitolo e fra gli errori");
            Console.WriteLine($"Colonna modello: {checks} controlli (caso R400-3+3D24-l0-12, unità, predefiniti, viscosità, rifiuti, relazione)");
            return checks;
        }
        finally { CultureInfo.CurrentCulture = culture; }
    }

    private sealed class RecordingRenderer : IFigureRenderer
    {
        internal ModuleReportRequest? Request;
        public Task<IReadOnlyList<ReportFigure>> RenderAsync(ModuleReportRequest request, CancellationToken cancellation)
        { Request = request; return Task.FromResult<IReadOnlyList<ReportFigure>>([]); }
    }
}
