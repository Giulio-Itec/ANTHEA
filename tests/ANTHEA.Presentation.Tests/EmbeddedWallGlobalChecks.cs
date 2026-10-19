using System.Text.Json.Nodes;
using Anthea.Application.EmbeddedWalls;
using Anthea.Application.Reporting;
using Anthea.Reports;
using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Checkers.Geotechnics.Slopes;
using GPC.Checkers.Geotechnics.Walls;
using GPC.Model.Checking.Contracts;
using GPC.Model.Standards;

namespace Anthea.Presentation.Tests;

internal static partial class EmbeddedWallChecks
{
    internal static JsonObject GlobalExample(bool fem = false)
    {
        var data = HydraulicExample(); data["name"] = "Paratia continua — stabilità globale";
        data["fix_bottom_u"] = false; data["fix_bottom_rotation"] = false;
        data["segments"]![0]!["weight"] = 15; data["hydraulic_mode"] = fem ? "FEM 2D" : "Idrostatica";
        data["hydraulic_left_extent"] = 20; data["hydraulic_right_extent"] = 20; data["hydraulic_bottom"] = -20; data["hydraulic_mesh"] = 2;
        foreach (var stage in data["stages"]!.AsArray().OfType<JsonObject>())
        {
            if (!fem) { stage["left_water"] = null; stage["right_water"] = null; }
            stage["left_surcharge"] = 10;
            var row = EmbeddedWallDocument.NewRow("global_checks"); row["stage"] = stage["id"]!.DeepClone();
            row["global_thickness"] = .6; row["left_extent"] = 20; row["right_extent"] = 20;
            row["characteristic_confirmed"] = true; row["source"] = "Parete sintetica 600 mm, gamma 25 kN/m³, parametri caratteristici";
            row["exit_min"] = -16; row["exit_max"] = -1; row["entry_min"] = 1; row["entry_max"] = 16;
            row["depth_min"] = 6.5; row["depth_max"] = 15; row["grid"] = 3; row["slices"] = 20; row["refinements"] = 0;
            data["global_checks"]!.AsArray().Add(row);
            var action = EmbeddedWallDocument.NewRow("global_actions"); action["stage"] = stage["id"]!.DeepClone(); action["psi"] = .3;
            action["source"] = "Combinazione sintetica: Q accompagnatrice psi=0.3"; data["global_actions"]!.AsArray().Add(action);
        }
        return data;
    }
    private static async Task GlobalAsync(Expect e, string output)
    {
        var old = HydraulicExample(); old["schema"] = 3; old.Remove("global_checks"); old.Remove("global_actions"); string before = old.ToJsonString();
        var upgraded = EmbeddedWallDocument.Snapshot(old);
        e.That(old.ToJsonString() == before && upgraded["schema"]!.ToString() == "6" && upgraded["global_checks"]!.AsArray().Count == 0,
            "Migrazione globale ha attivato verifiche o mutato l'originale.");
        var data = GlobalExample(); string original = data.ToJsonString(); using var sheet = Open(data);
        e.That(await Expect.Within(sheet.CalculateAsync()) && sheet.Current?.Complete == true, "Analisi globale/meccanica non completata.");
        var result = sheet.Current!; var global = sheet.GlobalChecks.Last();
        e.That(data.ToJsonString() == original && sheet.GlobalChecks.Count == 2 && global.Stage == "F2" && global.Circle is not null && global.Slices.Count > 0,
            "Provenienza o geometria della stabilità globale assente: " + global.Diagnostic);
        e.That(global.Demand == 1.1 && global.Coefficients["gammaM_phi"] == 1.25 && Math.Abs(global.Actions.Single().CombinedFactor - .39) < 1e-15,
            "Coefficienti globali indipendenti: A2 Q=1.3, psi=.3, M2=1.25 e R2=1.1.");
        // Direct GPC invocation verifies orchestration; independent mechanics/units are tested in the library suite.
        var m = EmbeddedWallAdapter.ToModel(data); var p = new EmbeddedWallGlobalProfile(600, 20000, 20000, 1, true, "test",
            new SlopeSearch(-16000, -1000, 1000, 16000, 6500, 15000, 3, 20, 0));
        var prepared = EmbeddedWallGlobalStability.Prepare(m, "F2", p);
        var combination = new StandardNTC2018Geotechnics().Combinations(GeotechnicalCheck.GlobalStability, GeotechnicalSituation.PersistentTransient).Single();
        var selected = EmbeddedWallGlobalStability.CreateCase(prepared, combination,
            [new("LeftSurcharge", GeotechnicalCharacteristicActionCategory.Variable, true, .3, "test")], false);
        var direct = EmbeddedWallGlobalStability.Calculate(prepared, [selected.Factors]); var verdict = WallGlobalStability.Checks(direct).Single();
        e.That(global.Capacity == verdict.Resistance && global.Ratio == verdict.Ratio && global.Outcome == verdict.Outcome && global.Failures == direct.Cases[0].NumericalFailures,
            "Verdetto globale alterato fra GPC e ANTHEA.");
        e.That(global.Outcome != EngineeringOutcome.Satisfied || global.SearchStatus == "Converged", "Ricerca non convergente trasformata in verifica soddisfatta.");
        using var reopened = Open(JsonNode.Parse(sheet.Data.ToJsonString())!.AsObject());
        e.That(reopened.GlobalChecks[1].Slices.SequenceEqual(global.Slices) && reopened.GlobalChecks[1].Actions.SequenceEqual(global.Actions), "Riapertura globale altera conci o azioni.");
        string csv = EmbeddedWallReport.GlobalCsv(result); before = sheet.Data.ToJsonString();
        byte[] docx = sheet.Report.BuildReport("Stabilità globale della paratia", new HashSet<string>());
        ReportFiles.Write(Path.Combine(output, "paratie", "stabilita-globale.docx"), docx, "Stabilità globale");
        File.WriteAllText(Path.Combine(output, "paratie", "stabilita-globale.csv"), csv);
        e.That(sheet.Data.ToJsonString() == before && csv.Contains(result.Fingerprint) && csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length == 1 + result.GlobalChecks!.Sum(g => 1 + g.Slices.Count)
            && DocxWriter.Read(docx).GetXml("word/document.xml").ToString().Contains(global.Diagnostic), "Report/CSV globale diverso dallo snapshot o con ricalcolo.");
        reopened.Tables.Single(t => t.Key == "global_checks").Rows[0]["grid"] = "5";
        e.That(reopened.Current is null && reopened.GlobalChecks.Count == 0 && reopened.SelectedGlobalCheck is null && !reopened.GlobalCsvCommand.CanExecute(null), "Modifica globale non invalida risultati e comandi.");
        var service = new EmbeddedWallModule();
        foreach (string reason in new[] { "situation", "source", "actions", "support" })
        {
            var invalid = GlobalExample();
            if (reason == "situation") invalid["global_checks"]![0]!["situation"] = "Seismic";
            if (reason == "source") invalid["global_checks"]![0]!["source"] = "";
            if (reason == "actions") invalid["global_actions"]!.AsArray().RemoveAt(0);
            if (reason == "support")
            {
                var support = EmbeddedWallDocument.NewRow("supports"); support["stiffness"] = 10000; invalid["supports"]!.AsArray().Add(support);
                invalid["activations"]!.AsArray().Add(EmbeddedWallDocument.NewRow("activations"));
            }
            var bad = service.Calculate(invalid).NativeAs<EmbeddedWallReportData>().GlobalChecks![0];
            e.That(bad.Outcome == EngineeringOutcome.NotEvaluated && bad.Ratio is null, "Caso non supportato presentato soddisfatto: " + reason);
        }
        var femData = GlobalExample(true); var fem = service.Calculate(femData).NativeAs<EmbeddedWallReportData>();
        var femChecks = fem.GlobalChecks ?? throw new InvalidOperationException("Verifiche globali FEM assenti.");
        e.That(femChecks.All(g => g.Slices.Count > 0) && femChecks[1].Slices.Any(s => s.Pressure > 0), "Campo FEM non trasferito alla stabilità: " + femChecks[1].Diagnostic);
        femData["hydraulic_left_extent"] = 8; femData["hydraulic_right_extent"] = 8;
        var incomplete = service.Calculate(femData).NativeAs<EmbeddedWallReportData>();
        e.That(incomplete.GlobalChecks!.All(g => g.Outcome == EngineeringOutcome.NotEvaluated), "Stabilità ha extrapolato il campo fuori dominio.");
        femData = GlobalExample(true); femData["hydraulic_iterations"] = 1;
        var failed = service.Calculate(femData).NativeAs<EmbeddedWallReportData>();
        e.That(!failed.Complete && failed.GlobalChecks![1].Outcome == EngineeringOutcome.NotEvaluated && failed.GlobalChecks[1].Slices.Count == 0, "Fase idraulica fallita usata come campo convergente globale.");
        using var cancellation = new CancellationTokenSource();
        var cancelService = new EmbeddedWallModule(); cancelService.ProgressChanged += p => { if (p.Stage.StartsWith("Stabilità globale", StringComparison.Ordinal)) cancellation.Cancel(); };
        e.Throws<OperationCanceledException>(() => cancelService.Calculate(data, cancellation.Token), "Annullamento nella fase globale ignorato.");
        var isolated = service.Calculate(data).NativeAs<EmbeddedWallReportData>();
        e.That(isolated.GlobalChecks![1].Slices.SequenceEqual(global.Slices), "Analisi globale precedente contaminata da altre esecuzioni.");
    }
}
