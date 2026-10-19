using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Application.EmbeddedWalls;
using Anthea.Application.Modules;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules.EmbeddedWalls;
using Anthea.Reports;
using GPC.Model.Checking.Contracts;

namespace Anthea.Presentation.Tests;

internal static partial class EmbeddedWallChecks
{
    internal static JsonObject EmbedmentExample()
    {
        var d = Example(); d["name"] = "Infissione — benchmark elastico indipendente"; d["bottom"] = -10; d["mesh"] = .125;
        d["fix_bottom_u"] = false; d["fix_bottom_rotation"] = false; d["substeps"] = 2;
        d["segments"]![0]!["bottom"] = -10;
        foreach (string side in new[] { "left_soils", "right_soils" })
        {
            var soil = EmbeddedWallDocument.NewRow(side); soil["name"] = "Sabbia sintetica"; soil["gamma"] = 18; soil["gamma_sat"] = 20;
            soil["phi"] = 30; soil["c"] = 0; soil["kh"] = .001; soil["k0"] = .5;
            soil["source"] = "Benchmark"; soil["stiffness_source"] = "Winkler k=2 N/mm2 sulle due facce";
            d[side] = new JsonArray(soil);
        }
        d["stages"] = new JsonArray(); d["loads"] = new JsonArray();
        foreach (var pair in new[] { ("Initial", 0d), ("Loaded", 10d), ("Reversed", -5d) })
        {
            var stage = EmbeddedWallDocument.NewRow("stages"); stage["id"] = pair.Item1;
            stage["left_surcharge"] = 100; stage["right_surcharge"] = 100; d["stages"]!.AsArray().Add(stage);
            var load = EmbeddedWallDocument.NewRow("loads"); load["stage"] = pair.Item1; load["horizontal"] = pair.Item2; d["loads"]!.AsArray().Add(load);
        }
        d["embedment_reference"] = 0; d["embedment_minimum"] = 3.5; d["embedment_maximum"] = 10; d["embedment_increment"] = .5;
        d["embedment_head_limit"] = 3.25; d["embedment_source"] = "Limite assegnato per confronto con la soluzione chiusa EI u'''' + k u = 0";
        return d;
    }
    private sealed class EmbedmentProgress(Action<EmbeddedWallEmbedmentProgress> update) : IProgress<EmbeddedWallEmbedmentProgress>
    { public void Report(EmbeddedWallEmbedmentProgress value) => update(value); }

    private static async Task EmbedmentAsync(Expect e, string output)
    {
        var data = EmbedmentExample(); string original = data.ToJsonString(); var service = new EmbeddedWallModule();
        var report = service.SearchEmbedment(data); var search = report.EmbedmentSearch!;
        e.That(data.ToJsonString() == original && search.Completed && search.MinimumCertified && search.Candidates.Count == 14
            && search.BestEmbedment == 6.5 && search.BestToe == -6.5, "Ricerca applicativa: minimo, griglia o isolamento errati.");
        // Independent closed-form fixture Checker/tests/Design.Checks/Fixtures/embedment-winkler-expected.json.
        e.That(Math.Abs(search.Candidates.Single(c => c.Embedment == 6).Criteria[0].Demand!.Value - 3.43397078639164) < 2e-6
            && Math.Abs(search.Candidates.Single(c => c.Embedment == 6.5).Criteria[0].Demand!.Value - 3.2038458451501377) < 2e-6,
            "Ricerca non confrontabile con l'atteso indipendente.");
        foreach (var candidate in search.Candidates)
        {
            var saved = candidate.Result!; var actual = JsonNode.Parse(saved.InputJson)!.AsObject();
            e.That(saved.EmbedmentSearch is null && saved.Complete && saved.Stages.Count == 3
                && actual["bottom"]!.GetValue<double>() == candidate.ToeElevation
                && actual["segments"]![0]!["bottom"]!.GetValue<double>() == candidate.ToeElevation
                && saved.Stages.All(s => s.Points.Min(p => p.Elevation) == candidate.ToeElevation), "Geometria o fasi del candidato errate.");
            var direct = service.Calculate(actual).NativeAs<EmbeddedWallReportData>();
            e.That(JsonSerializer.Serialize(saved) == JsonSerializer.Serialize(direct), "Ricerca diversa dal ricalcolo indipendente dello stesso candidato.");
            e.That(candidate.Criteria[0].Stage == "Loaded" && candidate.Criteria[0].Elevation == 0, "Provenienza del criterio di testa persa.");
            e.That(saved.Envelope is { Complete: true } && saved.Envelope.Bottom == candidate.ToeElevation,
                "Inviluppo del candidato calcolato con una geometria diversa.");
        }
        e.That(JsonSerializer.Serialize(report.Stages) == JsonSerializer.Serialize(search.Candidates.Single(c => c.Embedment == 6.5).Result!.Stages),
            "Diagrammi del risultato non riferiti al candidato selezionato.");
        EmbeddedWallDocument.StoreResult(data, report); var reopened = JsonNode.Parse(data.ToJsonString())!.AsObject();
        e.That(JsonSerializer.Serialize(EmbeddedWallDocument.ReadCurrentResult(reopened)) == JsonSerializer.Serialize(report), "Persistenza perde risultati della ricerca.");
        reopened["embedment_head_limit"] = 4;
        e.That(EmbeddedWallDocument.ReadCurrentResult(reopened) is null, "Limite modificato non invalida la ricerca.");
        var legacy = Example(); legacy["schema"] = 9;
        foreach (string key in new[] { "embedment_reference", "embedment_minimum", "embedment_maximum", "embedment_increment", "embedment_head_limit", "embedment_source" }) legacy.Remove(key);
        string before = legacy.ToJsonString(); var upgraded = EmbeddedWallDocument.Snapshot(legacy);
        e.That(legacy.ToJsonString() == before && upgraded["schema"]!.ToString() == "10" && upgraded["embedment_head_limit"] is null,
            "Migrazione inventa un limite ingegneristico o modifica il sorgente.");
        e.That(Math.Abs(service.Calculate(legacy).NativeAs<EmbeddedWallReportData>().Stages[0].Points[0].Displacement - 3.6) < 1e-7,
            "Nuova migrazione altera il calcolo singolo.");
        var unsupported = EmbedmentExample(); unsupported["embedment_minimum"] = 6.5; unsupported["embedment_maximum"] = 6.5;
        unsupported["steel_standard"] = "EN 1993-1-1 — profilo non disponibile";
        var failed = service.SearchEmbedment(unsupported);
        e.That(failed.EmbedmentSearch!.BestToe is null && failed.EmbedmentSearch.Candidates[0].Outcome == EngineeringOutcome.NotEvaluated
            && failed.EmbedmentSearch.Candidates[0].Criteria.Any(c => c.Id == "SteelSupports" && c.Outcome == EngineeringOutcome.NotEvaluated),
            "Famiglia richiesta ma non verificata presentata soddisfatta.");
        using var cancellation = new CancellationTokenSource();
        var partial = service.SearchEmbedment(EmbedmentExample(), cancellation.Token, new EmbedmentProgress(p => { if (p.Evaluated == 3) cancellation.Cancel(); }));
        e.That(partial.EmbedmentSearch!.Cancelled && !partial.EmbedmentSearch.Completed && !partial.EmbedmentSearch.MinimumCertified
            && partial.EmbedmentSearch.Candidates.Count == 3 && !partial.Complete, "Annullamento non conserva esattamente i candidati conclusi.");
        var invalid = EmbedmentExample(); invalid["embedment_head_limit"] = 0;
        e.Throws<ArgumentException>(() => service.SearchEmbedment(invalid), "Accettato limite nullo.");

        var hydraulic = HydraulicExample(); hydraulic["fix_bottom_u"] = false; hydraulic["fix_bottom_rotation"] = false;
        foreach (string key in new[] { "embedment_reference", "embedment_increment", "embedment_source" }) hydraulic[key] = EmbedmentExample()[key]!.DeepClone();
        hydraulic["embedment_minimum"] = 5; hydraulic["embedment_maximum"] = 6; hydraulic["embedment_increment"] = 1; hydraulic["embedment_head_limit"] = 100;
        var hydraulicSearch = service.SearchEmbedment(hydraulic).EmbedmentSearch!;
        e.That(hydraulicSearch.Candidates.Count == 2 && hydraulicSearch.Candidates.All(c => c.Result?.Hydraulics?.Count == 2
            && c.Result.Hydraulics.All(h => h.Complete)), "Filtrazione non ricalcolata per tutte le fasi/candidati.");
        foreach (var c in hydraulicSearch.Candidates)
        {
            var direct = service.Calculate(JsonNode.Parse(c.Result!.InputJson)!.AsObject()).NativeAs<EmbeddedWallReportData>();
            e.That(JsonSerializer.Serialize(c.Result.Hydraulics) == JsonSerializer.Serialize(direct.Hydraulics), "Campo FEM riciclato dalla geometria iniziale.");
        }
        e.That(JsonSerializer.Serialize(hydraulicSearch.Candidates[0].Result!.Hydraulics) != JsonSerializer.Serialize(hydraulicSearch.Candidates[1].Result!.Hydraulics),
            "La variazione del piede non modifica la filtrazione del caso asimmetrico.");
        foreach (var steel in new[] { true, false })
        {
            var checkData = EmbedmentExample(); checkData["embedment_minimum"] = 6.5; checkData["embedment_maximum"] = 6.5;
            checkData["mesh"] = .5; checkData["stages"] = new JsonArray(checkData["stages"]![1]!.DeepClone());
            checkData["loads"] = new JsonArray(checkData["loads"]![1]!.DeepClone()); checkData["loads"]![0]!["horizontal"] = .2;
            var sample = steel ? TubeExample() : RcPileExample(); string prefix = steel ? "tube" : "rc_pile", table = steel ? "steel_tubes" : "rc_piles";
            foreach (string key in new[] { prefix + "_standard", prefix + "_check_kind", table, "action_basis" }) checkData[key] = sample[key]!.DeepClone();
            var evaluated = service.SearchEmbedment(checkData).EmbedmentSearch!.Candidates.Single();
            var native = service.Calculate(JsonNode.Parse(evaluated.Result!.InputJson)!.AsObject()).NativeAs<EmbeddedWallReportData>();
            e.That(evaluated.Criteria.Any(c => c.Id == (steel ? "SteelTubes" : "RcPiles")), "Famiglia strutturale assente dai criteri.");
            e.That(steel ? JsonSerializer.Serialize(evaluated.Result.SteelTubes) == JsonSerializer.Serialize(native.SteelTubes)
                : JsonSerializer.Serialize(evaluated.Result.RcPiles) == JsonSerializer.Serialize(native.RcPiles), "Passaggio a Concrete/Steel non identico al calcolo singolo.");
        }

        using var sheet = Open(EmbedmentExample());
        e.That(await Expect.Within(sheet.SearchEmbedmentAsync()) && sheet.Current!.EmbedmentSearch!.MinimumCertified
            && sheet.EmbedmentCandidates.Count == 14 && sheet.SelectedEmbedment!.Embedment == 6.5 && !sheet.IsBusy, "Ricerca MVVM non pubblicata.");
        sheet.SelectedEmbedment = sheet.EmbedmentCandidates[0];
        e.That(sheet.EmbedmentCriteria[0].Outcome == EngineeringOutcome.NotSatisfied, "Criteri non aggiornati alla selezione.");
        e.That(sheet.ResultStatus.Contains("Candidato accettabile") && sheet.Points.Min(p => p.Elevation) == -6.5,
            "Selezione criterio cambia il candidato dei diagrammi senza dichiararlo: " + sheet.ResultStatus);
        using var reopenedSheet = Open(JsonNode.Parse(sheet.Data.ToJsonString())!.AsObject());
        e.That(reopenedSheet.EmbedmentCandidates.Count == 14 && reopenedSheet.SelectedEmbedment!.Embedment == 6.5, "Riapertura MVVM perde la ricerca.");
        string csv = EmbeddedWallReport.EmbedmentCsv(report);
        e.That(csv.Contains("Criterio") && csv.Contains("Sezione") && csv.Contains(report.Fingerprint) && csv.Contains("3.203"), "CSV perde risultati e provenienza.");
        var docx = sheet.Report.BuildReport("Ricerca infissione", new HashSet<string>());
        string xml = DocxWriter.Read(docx).GetXml("word/document.xml").ToString();
        e.That(xml.Contains("Ricerca dell'infissione") && xml.Contains(search.Diagnostic) && xml.Contains("Spostamento in testa")
            && xml.Contains("6,5") && xml.Contains("3,203846"), "Relazione non coerente con lo snapshot.");
        ReportFiles.Write(Path.Combine(output, "paratie", "ricerca-infissione.docx"), docx, "Ricerca infissione");
        File.WriteAllText(Path.Combine(output, "paratie", "ricerca-infissione.csv"), csv);
        sheet.Embedment["embedment_head_limit"].EditText = "4"; sheet.Commit();
        e.That(sheet.Current is null && sheet.EmbedmentCandidates.Count == 0, "Modifica dell'editor non invalida la ricerca.");

        var limiter = new CalculationLimiter(1); using var held = await limiter.EnterAsync(default);
        using var delayed = new EmbeddedWallSheetViewModel(new(EmbedmentExample(), false, new Host(), Scripted.Services(), service, limiter));
        var pending = delayed.SearchEmbedmentAsync(); delayed.Embedment["embedment_head_limit"].EditText = "4";
        e.That(!await Expect.Within(pending) && delayed.Current is null && !delayed.IsBusy, "Modifica durante l'attesa pubblica risultati superati.");
        using var closing = new EmbeddedWallSheetViewModel(new(EmbedmentExample(), false, new Host(), Scripted.Services(), service, limiter));
        pending = closing.SearchEmbedmentAsync(); closing.Dispose();
        e.That(!await Expect.Within(pending) && closing.Current is null, "Chiusura del foglio pubblica la ricerca tardiva.");
    }
}
