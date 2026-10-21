using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Application.Presentation;
using Anthea.Calculations;

/// <summary>
/// Derivations of the serviceability tab of the RC section moved from the historic view in sub-step S4 of F5.10 (ConcreteStressPresentation:
/// outputs of the rows, fields enabled by the analysis, detail and steps of the cracking, bars and vertices, scale of the stress map), with
/// values written by hand: the stresses of the fixture «default» are those of its frozen historic trace (tests/fixtures/f5-sezione,
/// sle/opzioni-sle.txt, step «scheda [3]»). The identity of the historic view with these functions is proved by ui/check-sezione-historic.
/// </summary>
internal static class ConcreteStressPresentationChecks
{
    internal static async Task<int> Run(string root)
    {
        int checks = 0;
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); checks++; }
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("it-IT");
        try
        {
            // Without a stress state.
            var waiting = new StressOutcome(null, null, "Dati non validi", "Da calcolare");
            Check(string.Join("|", ConcreteStressPresentation.Outputs(waiting).Select(o => o.Key + "=" + o.Value))
                == "sigma_c=—|sigma_s=—|eta_sigma=—|stress_status=Dati non validi|wk=Da calcolare", "Uscite di una riga senza stato tensionale");
            var settings = J.Obj(("normativa", "NTC 2018"), ("sle", J.Obj(("SLE", J.Obj(("modello", "Lineare"))))));
            var row = J.Obj(("nome", "Combo 1"), ("N", "-2500"), ("Mx", "500"), ("My", "250"));
            Check(ConcreteStressPresentation.Detail("SLE", settings, null, null, false) == "Nessuna combinazione selezionata"
                && ConcreteStressPresentation.Detail("SLE", settings, row, null, false) == "Tensioni da calcolare"
                && ConcreteStressPresentation.Detail("SLE", settings, row, waiting, false) == "Dati non validi", "Dettaglio senza selezione, senza esito, senza stato");
            Check(ConcreteStressPresentation.CrackSteps("SLE", row, null).StartsWith("Rara · Combo 1\n\n", StringComparison.Ordinal)
                && !ConcreteStressPresentation.CrackSteps("SLE", null, null).StartsWith("Rara", StringComparison.Ordinal), "Passaggi della fessurazione: famiglia e nome della riga");
            // The fields of the analysis.
            var linear = J.Obj(("modello", "Lineare"), ("assi", "Locali"));
            var custom = J.Obj(("modello", "Non lineare"), ("assi", "Personalizzati"));
            Check(ConcreteStressPresentation.FieldEnabled(linear, "phi") && !ConcreteStressPresentation.FieldEnabled(linear, "origine_x")
                && !ConcreteStressPresentation.FieldEnabled(custom, "n_trefoli") && ConcreteStressPresentation.FieldEnabled(custom, "rotazione")
                && ConcreteStressPresentation.FieldEnabled(custom, "esposizione") && ConcreteStressPresentation.NonLinear(custom) && !ConcreteStressPresentation.NonLinear(linear)
                && string.Join("|", ConcreteStressPresentation.SwitchedFields) == "phi|phi_trefoli|n_armature|n_trefoli|origine_x|origine_y|rotazione",
                "Campi dell'analisi: φ e n con l'analisi lineare, origine e rotazione con gli assi personalizzati");
            Check(ConcreteStressPresentation.ContourScale(null, "CLS · gradiente tensioni") is null, "Mappa senza stato tensionale: nessuna scala");

            // The family Rara of the fixture «default», calculated.
            var data = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "tests/fixtures/f3/inputs/str_palo--default.json")))!["data"]!.AsObject();
            var workspace = SectionWorkspace.Prepare(data);
            var input = data["input"]!.AsObject();
            foreach (string key in SectionWorkspace.Sets.Skip(2)) ConcreteSheetPreparation.SynchronizeHomogenization(input, workspace, key);
            var rows = SectionWorkspace.Sets.ToDictionary(key => key, key => data["combinazioni"]!.Array(key).OfType<JsonObject>()
                .Select(r => J.Obj(("id", r.S("id")), ("nome", r.S("nome")), ("N", r["azioni"]![0]), ("Mx", r["azioni"]![1]), ("My", r["azioni"]![2]))).ToArray());
            var steps = new Dictionary<string, ConcreteAnalysisStep>();
            await new ConcreteAnalysisSession().CalculateBatchAsync(input, workspace, rows, new HashSet<string> { "SLE" }, step => steps.Add(step.Name, step));
            var first = rows["SLE"][0];
            var outcome = steps["SLE"].Stress![first.S("id")];
            var state = outcome.State!;
            Check(first.S("nome") == "Combo 1" && string.Join("|", ConcreteStressPresentation.Outputs(outcome).Take(3).Select(o => o.Value)) == "-20,73|96,23|0,99",
                "Uscite della riga: σc, |σs| e ησ della traccia storica");
            var bars = ConcreteStressPresentation.BarRows(state, 14, workspace);
            Check(bars.Count == 14 && bars[0].ToJsonString() == "{\"id\":\"B01\",\"stress\":\"-96,23\",\"strain\":\"-0,48\",\"type\":\"Compressione\"}"
                && bars[9].ToJsonString() == "{\"id\":\"B10\",\"stress\":\"50,94\",\"strain\":\"0,25\",\"type\":\"Trazione\"}", "Barre: identificativi, tensioni, deformazioni, stato");
            Check(ConcreteStressPresentation.BarRows(state, 12, workspace)[12].S("id") == "T1", "Barre oltre quelle della sezione: i trefoli senza identificativo diventano T1…");
            var vertices = ConcreteStressPresentation.ConcreteRows(state);
            Check(vertices.Count == 4 && vertices[3].ToJsonString() == "{\"id\":\"C4\",\"x\":\"-300,00\",\"y\":\"400,00\",\"stress\":\"-20,73\",\"strain\":\"-0,61\"}",
                "Vertici del calcestruzzo: posizione, tensione, deformazione");
            string detail = ConcreteStressPresentation.Detail("SLE", workspace, first, outcome, false);
            Check(detail.StartsWith("Rara · Lineare\nCombo 1 · assi Locali\nN = -2500,00 kN\nMx / My = 500,00 / 250,00 kNm\n\nTENSIONI · NTC 2018\n|σc,comp| ≤ 21,00 MPa\nσs ≤ 360,00 MPa (armatura ordinaria)\nησ = 0,99\nEntro limiti tensionali\n", StringComparison.Ordinal)
                && !detail.Contains("FESSURAZIONE", StringComparison.Ordinal)
                && detail.EndsWith("\n\nOMOGENEIZZAZIONE\nn armature = 5,87 · φ = 0,00\nn = Eacciaio (1 + φ) / Ec. Deformazioni incrementali da GPC Engine.", StringComparison.Ordinal),
                "Dettaglio della Rara: azione, limiti, esito, nessuna fessurazione, omogeneizzazione");
            Check(ConcreteStressPresentation.Detail("SLE", workspace, first, outcome, true).Contains("\nn trefoli = ", StringComparison.Ordinal), "Dettaglio con i trefoli");
            Check(ConcreteStressPresentation.ContourScale(state, "CLS · gradiente tensioni")!.Legend == "-20,73 … 0,00 MPa"
                && ConcreteStressPresentation.ContourScale(state, "Barre · tasso resistenza")!.Legend == UtilizationColors.Legend
                && ConcreteStressPresentation.ContourScale(state, "CLS · deformazioni")!.Legend.EndsWith(" ‰", StringComparison.Ordinal)
                && ConcreteStressPresentation.ContourScale(state, "Solo geometria") is null, "Legenda della mappa: tensioni, tassi, deformazioni, solo geometria");
            var scale = ConcreteStressPresentation.ContourScale(state, "Barre · tensioni")!;
            Check(scale.OnlyBars && scale.ConcreteValues.Length == 0 && scale.BarValues.Length == 14 && scale.Lower < 0 && scale.Upper > 0, "Scala delle sole barre");
        }
        finally { CultureInfo.CurrentCulture = culture; }
        return checks;
    }
}
