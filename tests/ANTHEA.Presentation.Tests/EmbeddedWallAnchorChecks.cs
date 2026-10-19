using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Application.EmbeddedWalls;
using Anthea.Application.Reporting;
using Anthea.Reports;
using GPC.Model.Checking.Contracts;

namespace Anthea.Presentation.Tests;

internal static partial class EmbeddedWallChecks
{
    internal static JsonObject AnchorExample()
    {
        var data = SteelExample(); data["name"] = "Sfilamento tirante — prova sintetica"; data["steel_standard"] = "Non richieste";
        data["anchor_standard"] = "NTC 2018"; data["anchor_a1m1"] = true; data["action_basis"] = "Storia A1+M1, forze di progetto assegnate per collaudo";
        foreach (var row in data["activations"]!.AsArray().OfType<JsonObject>())
            if (row["support"]!.ToString() == "A") row["prestress"] = row["stage"]!.ToString() == "F1" ? 400 : 800;
        var check = EmbeddedWallDocument.NewRow("anchor_checks"); check["support"] = "A";
        check["free_length"] = 10; check["bond_length"] = 6; check["drill_diameter"] = 200; check["minimum_cover"] = 6;
        check["execution_source"] = "IRS, volumi e qualità della miscela documentati nel caso sintetico";
        check["applicability_source"] = "Bulbo isolato, confinato in terreno stabile";
        check["experimental_source"] = "Conferma sperimentale sintetica di collaudo"; check["stage_source"] = "Geometria e indagini valide in F1/F2";
        data["anchor_checks"]!.AsArray().Add(check);
        foreach (var item in new[] { (0d, 12d, "MediumSand", 2d, 3d, 1.5), (12d, 20d, "Clay", 1d, 2d, 1.8) })
        {
            var row = EmbeddedWallDocument.NewRow("anchor_layers"); row["support"] = "A"; row["profile"] = "Indagine A";
            row["axis_start"] = item.Item1; row["axis_end"] = item.Item2; row["soil_kind"] = item.Item3;
            row["limit_pressure"] = item.Item4; row["injection_pressure"] = item.Item5; row["diameter_factor"] = item.Item6;
            row["pressure_source"] = "Pressiometria sintetica, distinto dato d'iniezione"; data["anchor_layers"]!.AsArray().Add(row);
        }
        return data;
    }

    private static async Task AnchorsAsync(Expect e, string output)
    {
        var old = Example(); old["schema"] = 5;
        foreach (string key in new[] { "anchor_checks", "anchor_layers", "anchor_tests", "anchor_standard", "anchor_a1m1", "anchor_situation" }) old.Remove(key);
        string before = old.ToJsonString(); var migrated = EmbeddedWallDocument.Snapshot(old);
        e.That(old.ToJsonString() == before && migrated["schema"]!.ToString() == "9" && migrated["anchor_standard"]!.ToString() == "Non richieste"
            && migrated["anchor_checks"]!.AsArray().Count == 0, "Migrazione 5→6 altera input o attiva sfilamento.");
        var data = AnchorExample(); before = data.ToJsonString(); using var sheet = Open(data);
        e.That(await Expect.Within(sheet.CalculateAsync()), "Calcolo tiranti non pubblicato."); var result = sheet.Current!; var checks = sheet.AnchorChecks;
        e.That(result.Complete && data.ToJsonString() == before && checks.Count == 2 && checks[0].Stage == "F1" && checks[1].Stage == "F2"
            && checks.All(c => c.Support == "A" && c.CaseId == result.CaseId), "Provenienza, rimozione o esclusione puntone errate.");
        const double raw = 1285.5397138489434; double rd = raw / 1.8 / 1.2;
        e.That(Math.Abs(checks[0].Samples[0].Resistance!.Value - raw) < 1e-9 && Math.Abs(checks[0].Capacity!.Value - rd) < 1e-9
            && checks[0].MeanFactor == 1.8 && checks[0].ResistanceFactor == 1.2, "Conversione capacità o fattori non corretta.");
        e.That(Math.Abs(checks[0].Demand!.Value - 400) < 1e-8 && Math.Abs(checks[1].Demand!.Value - 800) < 1e-8
            && checks[0].Outcome == EngineeringOutcome.Satisfied && checks[1].Outcome == EngineeringOutcome.NotSatisfied, "Azioni individuali/esiti alterati.");
        e.That(checks[0].Segments.Count == 2 && checks[0].Segments[0].Start == 10 && checks[0].Segments[1].End == 16
            && checks[0].Segments[0].LimitPressure == 2 && checks[0].Segments[0].InjectionPressure == 3 && checks[0].Segments[0].BulbDiameter == 300,
            "Assi, pressioni distinte o diametro bulbo non conservati.");
        using var reopened = Open(JsonNode.Parse(sheet.Data.ToJsonString())!.AsObject());
        e.That(JsonSerializer.Serialize(reopened.AnchorChecks) == JsonSerializer.Serialize(checks), "Risultati sfilamento alterati dalla persistenza.");
        before = sheet.Data.ToJsonString(); string csv = EmbeddedWallReport.AnchorCsv(result);
        using var parser = new Microsoft.VisualBasic.FileIO.TextFieldParser(new StringReader(csv)) { TextFieldType = Microsoft.VisualBasic.FileIO.FieldType.Delimited, HasFieldsEnclosedInQuotes = true };
        parser.SetDelimiters(";"); var csvRows = new List<string[]>(); while (!parser.EndOfData) csvRows.Add(parser.ReadFields()!);
        e.That(csvRows.Count == 9 && csvRows.All(r => r.Length == 29) && csv.Contains(result.Fingerprint), "CSV sfilamento perde campioni, tratti o provenienza.");
        byte[] docx = sheet.Report.BuildReport("Sfilamento dei tiranti", new HashSet<string>());
        ReportFiles.Write(Path.Combine(output, "paratie", "sfilamento-tiranti.docx"), docx, "Sfilamento tiranti");
        File.WriteAllText(Path.Combine(output, "paratie", "sfilamento-tiranti.csv"), csv);
        File.WriteAllText(Path.Combine(output, "paratie", "sfilamento-tiranti.json"), sheet.Data.ToJsonString(new() { WriteIndented = true }));
        e.That(sheet.Data.ToJsonString() == before && DocxWriter.Read(docx).GetXml("word/document.xml").ToString().Contains("pl / pi [MPa]"), "Report modifica input o omette pressioni.");
        reopened.Tables.Single(t => t.Key == "anchor_layers").Rows[0]["limit_pressure"] = "2.5";
        e.That(reopened.Current is null && reopened.AnchorChecks.Count == 0 && reopened.SelectedAnchorCheck is null && !reopened.AnchorCsvCommand.CanExecute(null), "Risultati tiranti superati ancora esportabili.");
        var service = new EmbeddedWallModule();
        foreach (string key in new[] { "execution_source", "applicability_source", "stage_source", "bond_length" })
        {
            var missing = AnchorExample(); missing["anchor_checks"]![0]![key] = null;
            e.That(service.Calculate(missing).NativeAs<EmbeddedWallReportData>().AnchorChecks!.All(c => c.Outcome == EngineeringOutcome.NotEvaluated), "Dato mancante ignorato: " + key);
        }
        var preliminary = AnchorExample(); preliminary["anchor_checks"]![0]!["experimental_source"] = "";
        var preliminaryChecks = service.Calculate(preliminary).NativeAs<EmbeddedWallReportData>().AnchorChecks!;
        e.That(preliminaryChecks[0].Outcome == EngineeringOutcome.NotEvaluated && preliminaryChecks[0].Capacity.HasValue
            && preliminaryChecks[1].Outcome == EngineeringOutcome.NotSatisfied, "Predimensionamento certificato o insufficienza nascosta.");
        foreach (var changed in new[] { ("anchor_a1m1", (JsonNode)JsonValue.Create(false)!), ("anchor_standard", (JsonNode)JsonValue.Create("EN 1997-1 — profilo non disponibile")!),
            ("anchor_situation", (JsonNode)JsonValue.Create("Seismic")!) })
        {
            var invalid = AnchorExample(); invalid[changed.Item1] = changed.Item2.DeepClone();
            e.That(service.Calculate(invalid).NativeAs<EmbeddedWallReportData>().AnchorChecks!.All(c => c.Outcome == EngineeringOutcome.NotEvaluated), "Profilo o base non supportata certificati.");
        }
        var measured = AnchorExample(); measured["anchor_layers"] = new JsonArray(); measured["anchor_checks"]![0]!["evidence"] = "MeasuredAnchorTests";
        measured["anchor_checks"]![0]!["permanent"] = false;
        foreach (double capacity in new[] { 900d, 1200d, 1350d })
        {
            var row = EmbeddedWallDocument.NewRow("anchor_tests"); row["support"] = "A"; row["test_id"] = "T" + capacity;
            row["resistance"] = capacity; row["source"] = "Prova preliminare sintetica"; measured["anchor_tests"]!.AsArray().Add(row);
        }
        var actual = service.Calculate(measured).NativeAs<EmbeddedWallReportData>().AnchorChecks![0];
        e.That(actual.MeanFactor == 1.3 && actual.MinimumFactor == 1.2 && Math.Abs(actual.Capacity!.Value - 750 / 1.1) < 1e-9 && actual.Segments.Count == 0,
            "Resistenze misurate trattate come indagini o coefficiente temporaneo errato.");
        foreach (string key in new[] { "test_id", "source", "resistance" })
        {
            var missing = measured.DeepClone().AsObject(); missing["anchor_tests"]![0]![key] = null;
            var invalidChecks = service.Calculate(missing).NativeAs<EmbeddedWallReportData>().AnchorChecks!;
            e.That(invalidChecks.All(c => c.Outcome == EngineeringOutcome.NotEvaluated && c.Capacity is null), "Prova incompleta certificata: " + key);
        }
        var duplicate = measured.DeepClone().AsObject(); duplicate["anchor_tests"]!.AsArray().Add(duplicate["anchor_tests"]![0]!.DeepClone());
        e.That(service.Calculate(duplicate).NativeAs<EmbeddedWallReportData>().AnchorChecks!.All(c => c.Outcome == EngineeringOutcome.NotEvaluated),
            "Prova duplicata aumenta il numero di indagini.");
        e.Throws<OperationCanceledException>(() => service.Calculate(AnchorExample(), new CancellationToken(true)), "Annullamento sfilamento ignorato.");
        var isolated = service.Calculate(AnchorExample()).NativeAs<EmbeddedWallReportData>();
        e.That(JsonSerializer.Serialize(isolated.AnchorChecks) == JsonSerializer.Serialize(checks), "Risultati tiranti contaminati da altre analisi.");
    }
}
