using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Calculations;
using Anthea.Presentation.Modules.ConcreteSection;
using Anthea.Presentation.Services;

namespace Anthea.Presentation.Tests;

/// <summary>
/// The tab «Colonna modello» (1.15.0; phase 2 in 1.16.0) on <see cref="ConcreteModelColumnViewModel"/>, without WPF: the position of the tab and the
/// stable code saved in <c>tab</c> (documents with eight tabs reopen on the same tab), the defaults without writes for the sheets without the
/// options, the persistence, the calculation of the case R400-3+3D24-l0-12 of the library (M1Rd,max ≈ 156,08 kNm), the units of the charts, the
/// CSV, the invalidation, «Interrompi», the rejections in Italian and the read-only revision; the reduced domain and the combinations SLU of the
/// sheet (1.16.0) with their invalidation.
/// </summary>
internal static partial class ConcreteSectionSheetChecks
{
    /// <summary>The fixture «default» with the section of the case R400-3+3D24 (400 × 400, 3Ø24 + 3Ø24 at ±160 mm, C25/30, B450C, NTC 2018).</summary>
    private static JsonObject ModelColumnData(string root)
    {
        var data = (JsonObject)ConcreteSectionScenarios.FixtureData(root, "default").DeepClone();
        var input = data["input"]!.AsObject();
        foreach (var (key, value) in new (string, string)[] { ("width_mm", "400"), ("height_mm", "400"), ("cover_mm", "18"), ("top_bar_count", "3"),
            ("top_bar_diameter_mm", "24"), ("bottom_bar_count", "3"), ("bottom_bar_diameter_mm", "24"), ("side_bar_count_per_side", "0"),
            ("classe_cls", "C25/30"), ("materiale_cls_nome", "C25/30") })
            input[key] = value;
        input["fck_mpa"] = 25;
        return data;
    }

    private static async Task ModelColumnAsync(string root, Expect expect)
    {
        // ---- the tab after «Momento–curvatura», «Calcola armature» ninth; the stable code saved in «tab»
        var titles = ConcreteSheetViewModel.TabTitles;
        expect.That(titles.Length == 9 && titles[6] == "Momento–curvatura" && titles[ConcreteSheetViewModel.ModelColumnTab] == "Colonna modello"
            && titles[ConcreteSheetViewModel.DesignTab] == "Calcola armature" && ConcreteSheetViewModel.ModelColumnTab == 7 && ConcreteSheetViewModel.DesignTab == 8,
            "Colonna modello: scheda 08 dopo il M–χ, «Calcola armature» 09");
        expect.That(Enumerable.Range(0, 7).All(i => ConcreteSheetPresentation.SavedTab(i) == i) && ConcreteSheetPresentation.SavedTab(7) == 8 && ConcreteSheetPresentation.SavedTab(8) == 7
            && Enumerable.Range(0, 9).All(i => ConcreteSheetPresentation.ShownTab(ConcreteSheetPresentation.SavedTab(i)) == i),
            "Codici salvati: 0–6 invariati, «Calcola armature» resta 7, «Colonna modello» 8");
        foreach (var (saved, shown) in new[] { (7, ConcreteSheetViewModel.DesignTab), (6, 6), (1, 1), (8, ConcreteSheetViewModel.ModelColumnTab) })
        {
            var data = (JsonObject)ConcreteSectionScenarios.FixtureData(root, "soletta-dettagli-ricerca").DeepClone();
            data["workspace_ca"]!["tab"] = saved;
            using var sheet = OpenSheet(data);
            int modified = 0; sheet.Modified += (_, _) => modified++;
            await IdleAsync(sheet);
            expect.That(sheet.SelectedTab == shown && sheet.Data["workspace_ca"]!["tab"]!.GetValue<int>() == saved && modified == 0,
                $"Documento con «tab» = {saved}: si apre sulla scheda {titles[shown]}, senza scritture né Modified");
        }
        using (var sheet = OpenSheet(ConcreteSectionScenarios.FixtureData(root, "soletta-dettagli-ricerca")))
        {
            await IdleAsync(sheet);
            expect.That(sheet.SelectedTab == ConcreteSheetViewModel.DesignTab, "Documento salvato con otto schede sulla scheda 7: si riapre su «Calcola armature»");
            int modified = 0; sheet.Modified += (_, _) => modified++;
            sheet.SelectedTab = ConcreteSheetViewModel.ModelColumnTab;
            expect.That(sheet.Data["workspace_ca"]!["tab"]!.GetValue<int>() == 8 && modified == 1, "«Colonna modello» scelta: codice 8, Modified");
            using (var reopened = OpenSheet(sheet.Data)) { await IdleAsync(reopened); expect.That(reopened.SelectedTab == ConcreteSheetViewModel.ModelColumnTab, "Riaperto su «Colonna modello»"); }
            sheet.SelectedTab = ConcreteSheetViewModel.DesignTab;
            expect.That(sheet.Data["workspace_ca"]!["tab"]!.GetValue<int>() == 7, "«Calcola armature» scelta: codice 7, come nei documenti con otto schede");
            using (var reopened = OpenSheet(sheet.Data)) { await IdleAsync(reopened); expect.That(reopened.SelectedTab == ConcreteSheetViewModel.DesignTab, "Riaperto su «Calcola armature»"); }
        }

        // ---- a sheet without the options: defaults shown, nothing written, no Modified
        var files = new ScriptedFileDialogs();
        var messages = new ScriptedMessages();
        var services = new PresentationServices(messages, new ScriptedConfirmations(), files, new ScriptedDialogs(), new RecordingClipboard(), new RecordingLinks(), new RecordingWiki());
        var source = ModelColumnData(root);
        using (var sheet = OpenSheet(source, services: services))
        {
            int modified = 0; sheet.Modified += (_, _) => modified++;
            await IdleAsync(sheet);
            var column = sheet.ModelColumn;
            var form = column.Form;
            expect.That(sheet.Settings[ConcreteModelColumn.OptionsKey] is null && modified == 0 && column.Result is null && column.Text == "" && column.Cards.Cards.Count == 0
                && column.CapacityChart.Data is null && column.FirstOrderChart.Data is null && column.DomainChart.Data is null,
                "Foglio senza colonna modello: nessuna scrittura, nessun Modified, nessun risultato");
            expect.That(form.Get("azioni") == ConcreteModelColumn.ActionSources[0] && form.Get("intervalli_dominio") == "40" && form["N"].IsVisible && form["M0Ed"].IsVisible
                && column.DomainChart.XLabel == ConcreteModelColumnPresentation.DomainXLabel && column.DomainChart.YLabel == "N [kN]",
                "1.16.0: azione singola e 40 intervalli del dominio predefiniti, N e M0Ed visibili; assi del dominio ridotto");
            expect.That(form.Get("N") == "0" && form.Get("theta") == "0" && form.Get("M0Ed") == "" && form.Get("l0") == "" && form.Get("c") == "10"
                && form.Get("imperfezione") == ConcreteModelColumn.ImperfectionModes[0] && form.Get("eccentricita_minima") == ConcreteModelColumn.MinimumEccentricityModes[0]
                && form.Get("viscosita") == ConcreteModelColumn.CreepModes[0] && form.Get("passi") == "200", "Predefiniti mostrati: c = 10, EN 5.2(7)a, minimo della norma, viscosità trascurata");
            expect.That(!form["ei"].IsVisible && !form["phi_ef"].IsVisible && !form["M0Eqp"].IsVisible && !form["NEqp"].IsVisible && form["phi_inf"].IsVisible,
                "Campi delle scelte nascosti");
            // ---- the data of the case R400-3+3D24-l0-12: the options attached at the first change, Modified at every change
            form["N"].EditText = "-500";
            expect.That(sheet.Settings[ConcreteModelColumn.OptionsKey] is JsonObject options && options["N"]!.ToString() == "-500" && options.Count == 1 && modified == 1
                && column.Text == ConcreteModelColumnPresentation.Stale, "Prima modifica: opzioni nel foglio con la sola chiave scritta, Modified");
            form["M0Ed"].EditText = "137.5"; form["l0"].EditText = "12"; form["l"].EditText = "6";
            form["imperfezione"].SelectedChoice = ConcreteModelColumn.ImperfectionModes[3];
            expect.That(form["ei"].IsVisible, "Imperfezione assegnata: campo ei visibile");
            form["imperfezione"].SelectedChoice = ConcreteModelColumn.ImperfectionModes[4];
            form["eccentricita_minima"].SelectedChoice = ConcreteModelColumn.MinimumEccentricityModes[3];
            expect.That(!form["ei"].IsVisible && modified == 7, "Imperfezione nessuna: ei nascosto; un Modified per modifica");
            await column.CalculateCommand.ExecuteAsync(null);
            await IdleAsync(sheet);
            var result = column.Result;
            expect.That(result is not null && Math.Abs(result.Capacity.MaximumFirstOrderKnm - 156.083) <= 156.083 * 2e-3 && result.Verified == true && result.Capacity.Mode == "instabilità",
                "R400-3+3D24-l0-12: M1Rd,max ≈ 156,08 kNm, instabilità, verificata · " + result?.Capacity.MaximumFirstOrderKnm.ToString("G9", CultureInfo.InvariantCulture));
            var direct = ConcreteModelColumn.Calculate(sheet.Input, sheet.Settings, sheet.Settings[ConcreteModelColumn.OptionsKey]!.AsObject());
            expect.That(JsonNode.DeepEquals(J.Node(result), J.Node(direct)) && JsonNode.DeepEquals(sheet.Result?[ConcreteModelColumn.OptionsKey], J.Node(result)),
                "Risultato della scheda uguale al calcolo dell'applicazione e nell'esportazione dei risultati");
            // ---- units: the charts in 1/m and kNm, the cards, the text
            var capacity = column.CapacityChart.Data!; var first = column.FirstOrderChart.Data!;
            expect.That(capacity.Series.Count == 4 && capacity.Series[0].Points.Count == result!.Points.Count
                && capacity.Series[0].Points.Zip(result.Points).All(t => t.First[0] == t.Second.Curvature && t.First[1] == t.Second.MomentKnm)
                && capacity.Markers.Any(m => m.X == result.Capacity.CurvatureAtMaximum && m.Y == result.Capacity.MomentAtMaximumKnm)
                && first.Series[0].Points.Zip(result.Points).All(t => t.First[1] == t.Second.FirstOrderCapacityKnm) && first.Markers.Single().Y == result.Capacity.MaximumFirstOrderKnm
                && capacity.XLabel == "Curvatura χ [1/m]" && capacity.YLabel == "Momento M [kNm]",
                "Grafici: M(χ) con retta esterna, tangente e M2; M − M2 con il massimo; χ in 1/m, momenti in kNm");
            expect.That(Math.Abs(result.Points.Max(p => p.Curvature) - 3.092206e-2) <= 3.092206e-2 * 2e-3 && result.SecondOrderStiffness == 7200 && result.AxialKn == -500,
                "Unità: χu ≈ 0,0309 1/m, k = 7200 kN·m², N = −500 kN");
            expect.That(column.Cards.Cards.Count == 7 && column.Cards.Cards.Count(c => c.Tone == ConcreteCardTone.Passed) == 1 && column.Text.StartsWith(result.Status, StringComparison.Ordinal)
                && column.Text.Contains("Esito: Verificata", StringComparison.Ordinal), "Riquadri (snellezza, primo ordine, viscosità, capacità, verifica, nominale, avviso) e testo");
            expect.That(column.DomainChart.Data is { } domainChart && domainChart.Series.Count == 2 && domainChart.Markers.Single().Label == "Ed" && result.Domains.Single().Points.Count == 42
                && column.Text.Contains("Dominio ridotto N–M1Rd,max (θ = 0°", StringComparison.Ordinal), "1.16.0: dominio ridotto dell'azione singola, grafico e testo");
            await column.CalculateCommand.ExecuteAsync(null);
            expect.That(ReferenceEquals(result, column.Result), "Stessi dati: colonna non ricalcolata");
            string file = Path.Combine(Path.GetTempPath(), "anthea-colonna-modello-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + ".csv");
            files.Then(file);
            column.ExportCommand.Execute(null);
            string[] csv = File.ReadAllLines(file);
            File.Delete(file);
            expect.That(Array.IndexOf(csv, "") == result.Points.Count + 1 && csv[0].StartsWith("chi_1_m;M_kNm;M2_kNm;", StringComparison.Ordinal)
                && csv[result.Points.Count + 2].StartsWith("dominio_theta_gradi;", StringComparison.Ordinal) && csv.Length == result.Points.Count + 2 + 43,
                "Esporta CSV: intestazione e un punto per riga, poi (1.16.0) il dominio ridotto");
            // ---- a change of the form or of the section clears the result
            form["c"].EditText = "8";
            expect.That(column.Result is null && column.Text == ConcreteModelColumnPresentation.Stale && column.Cards.Cards.Count == 0 && column.CapacityChart.Data is null
                && sheet.Result?[ConcreteModelColumn.OptionsKey] is null, "Form della colonna: risultato azzerato, esportazione senza colonna");
            await column.CalculateCommand.ExecuteAsync(null);
            expect.That(column.Result is { CurvatureFactor: 8 } eight && eight.Capacity.MaximumFirstOrderKnm < result.Capacity.MaximumFirstOrderKnm, "c = 8: capacità minore");
            sheet.Inputs.Geometry["width_mm"].EditText = "450";
            expect.That(column.Result is null && column.Text == ConcreteModelColumnPresentation.Stale, "Sezione modificata: colonna azzerata");
            sheet.Inputs.Geometry["width_mm"].EditText = "400";
            await IdleAsync(sheet);
            var running = column.CalculateCommand.ExecuteAsync(null);
            column.StopCommand.Execute(null);
            await running;
            expect.That(column.Result is null && column.Text == ConcreteModelColumnPresentation.Interrupted, "Interrompi: nessun risultato parziale pubblicato");
            // ---- rejections in Italian
            form["N"].EditText = "100";
            await column.CalculateCommand.ExecuteAsync(null);
            expect.That(column.Result is null && column.Text.StartsWith("Colonna modello non disponibile: N di trazione", StringComparison.Ordinal) && column.Cards.Cards.Count == 0,
                "Rifiuto della libreria tradotto: " + column.Text);
            form["N"].EditText = "-500"; form["l0"].EditText = "";
            await column.CalculateCommand.ExecuteAsync(null);
            expect.That(column.Text == ConcreteModelColumnPresentation.Unavailable("Inserire la lunghezza libera di inflessione l0."), "l0 mancante: " + column.Text);
            form["l0"].EditText = "12";
            // ---- persistence: the options saved, read again at the reopening
            sheet.Commit();
            var saved = sheet.Settings[ConcreteModelColumn.OptionsKey]!.AsObject();
            expect.That(saved["N"]!.ToString() == "-500" && saved["c"]!.ToString() == "8" && saved["l0"]!.ToString() == "12" && saved["imperfezione"]!.ToString() == ConcreteModelColumn.ImperfectionModes[4]
                && saved["passi"] is null, "Persistenza: solo le chiavi modificate, i predefiniti non scritti");
            using var reopened = OpenSheet(sheet.Data, services: services);
            int reopenedModified = 0; reopened.Modified += (_, _) => reopenedModified++;
            await IdleAsync(reopened);
            var again = reopened.ModelColumn;
            expect.That(again.Form.Get("N") == "-500" && again.Form.Get("c") == "8" && again.Form.Get("M0Ed") == "137.5" && again.Form.Get("passi") == "200"
                && ReferenceEquals(again.Form.Values, reopened.Settings[ConcreteModelColumn.OptionsKey]) && reopenedModified == 0, "Riapertura: dati della colonna e predefiniti, senza Modified");
            await again.CalculateCommand.ExecuteAsync(null);
            expect.That(again.Result is { CurvatureFactor: 8 } && JsonNode.DeepEquals(J.Node(again.Result), J.Node(ConcreteModelColumn.Calculate(reopened.Input, reopened.Settings,
                reopened.Settings[ConcreteModelColumn.OptionsKey]!.AsObject()))), "Riapertura: stesso risultato");
        }
        // ---- 1.16.0: the combinations SLU of the sheet (case of the library at l0 = 6 m: C7 governs, C4 in tension), progress, invalidation by the combinations
        // the fixture is a sheet of version 1 (N positive in compression), converted at the opening: here C1 and C7 in compression, C4 in tension
        var combinedData = ModelColumnData(root);
        combinedData["combinazioni"]!["SLU"] = new JsonArray(new (string Name, string N, string Mx)[] { ("C1", "1000", "180"), ("C4", "-200", "50"), ("C7", "1500", "160") }
            .Select(c => (JsonNode)J.Obj(("id", c.Name), ("nome", c.Name), ("azioni", new[] { c.N, c.Mx, "0" }))).ToArray());
        using (var sheet = OpenSheet(combinedData, services: services))
        {
            await IdleAsync(sheet);
            var column = sheet.ModelColumn;
            var form = column.Form;
            form["azioni"].SelectedChoice = ConcreteModelColumn.ActionSources[1];
            expect.That(!form["N"].IsVisible && !form["M0Ed"].IsVisible && sheet.Settings[ConcreteModelColumn.OptionsKey]!["azioni"]!.ToString() == "Combinazioni SLU",
                "Combinazioni: N e M0Ed del form nascosti, scelta salvata");
            form["l0"].EditText = "6"; form["l"].EditText = "3"; form["imperfezione"].SelectedChoice = ConcreteModelColumn.ImperfectionModes[4];
            form["eccentricita_minima"].SelectedChoice = ConcreteModelColumn.MinimumEccentricityModes[3]; form["intervalli_dominio"].EditText = "10";
            var texts = new List<string>();
            column.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(column.Text)) lock (texts) texts.Add(column.Text); };
            await column.CalculateCommand.ExecuteAsync(null);
            await IdleAsync(sheet);
            var all = column.Result;
            expect.That(all is { Governing: "C7", Verified: true } && all.Combinations.Count == 3 && Math.Abs(all.Capacity.MaximumFirstOrderKnm - 169.406963) <= 169.406963 * 2e-3
                && all.Combinations[1].Status == "Trazione" && column.Cards.Cards[0].Heading.StartsWith("Inviluppo delle combinazioni · ", StringComparison.Ordinal) && column.Cards.Cards[0].Tone == ConcreteCardTone.Passed,
                "Combinazioni SLU: governa C7 (M1Rd,max ≈ 169,41 kNm del riferimento), C4 di trazione, riquadro dell'inviluppo · " + column.Text);
            var directAll = ConcreteModelColumn.Calculate(sheet.Input, sheet.Settings, sheet.Settings[ConcreteModelColumn.OptionsKey]!.AsObject(), default,
                ConcreteSheetPreparation.ReadActions(sheet.Data));
            expect.That(JsonNode.DeepEquals(J.Node(all), J.Node(directAll)) && JsonNode.DeepEquals(sheet.Result?[ConcreteModelColumn.OptionsKey], J.Node(all)),
                "Combinazioni: risultato della scheda uguale al calcolo dell'applicazione e nell'esportazione");
            List<string> seen; lock (texts) seen = [.. texts];
            expect.That(seen.Count >= 2 && seen[0] == ConcreteModelColumnPresentation.Running && seen[^1] == column.Text && column.Text.StartsWith(all!.Status, StringComparison.Ordinal)
                && seen.Skip(1).SkipLast(1).All(t => t.StartsWith("Colonna modello: combinazione ", StringComparison.Ordinal) || t.StartsWith("Colonna modello: dominio ridotto", StringComparison.Ordinal)),
                "Avanzamento: messaggi della libreria fra «Calcolo…» e il risultato, nessun messaggio dopo il risultato (" + seen.Count + " testi)");
            expect.That(column.DomainChart.Data is { } chart && chart.Markers.Count == 2 && chart.Series.Count == 2, "Dominio ridotto con le due combinazioni compresse");
            await column.CalculateCommand.ExecuteAsync(null);
            expect.That(ReferenceEquals(all, column.Result), "Stesse combinazioni: colonna non ricalcolata");
            // a change of a combination SLU clears the column; one of SLV does not
            if (sheet.ActionRowSets["SLV"].FirstOrDefault() is { } slv) slv["Mx"] = "1";
            expect.That(ReferenceEquals(all, column.Result), "Combinazione SLV modificata: colonna delle sole SLU conservata");
            sheet.ActionRowSets["SLU"][0]["Mx"] = "170";
            expect.That(column.Result is null && column.Text == ConcreteModelColumnPresentation.Stale && column.DomainChart.Data is null && sheet.Result?[ConcreteModelColumn.OptionsKey] is null,
                "Combinazione SLU modificata: colonna azzerata");
            await column.CalculateCommand.ExecuteAsync(null);
            expect.That(column.Result is { } again && again.Combinations[0].MomentKnm == 170 && !ReferenceEquals(again, all), "Ricalcolo con la combinazione modificata");
            sheet.ActionRowSets["SLU"][0]["Mx"] = "175";
            var running = column.CalculateCommand.ExecuteAsync(null);
            column.StopCommand.Execute(null);
            await running;
            expect.That(column.Result is null && column.Text == ConcreteModelColumnPresentation.Interrupted, "Combinazioni: Interrompi, nessun risultato parziale · " + column.Text);
            sheet.ActionRowSets["SLU"].Clear();
            sheet.Commit();
            await column.CalculateCommand.ExecuteAsync(null);
            expect.That(column.Result is null && column.Text == ConcreteModelColumnPresentation.Unavailable("Nessuna combinazione SLU nella scheda «Combinazioni»: inserirle, oppure scegliere l'azione singola."),
                "Nessuna combinazione SLU: messaggio · " + column.Text);
        }
        // ---- read-only revision: «Calcola» off, the form protected
        using (var sheet = OpenSheet(ModelColumnData(root), readOnly: true))
        {
            await IdleAsync(sheet);
            var column = sheet.ModelColumn;
            column.Form["l0"].EditText = "12";
            await column.CalculateCommand.ExecuteAsync(null);
            expect.That(column.Form.IsReadOnly && !column.CalculateCommand.CanExecute(null) && column.Result is null && sheet.Settings[ConcreteModelColumn.OptionsKey] is null,
                "Sola lettura: form protetto, «Calcola» spento, nessuna scrittura");
        }
    }
}
