using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Application.EmbeddedWalls;
using Anthea.Reports;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// A4 of the paratie review (21-22/10/2026), sheet of 16/11/2026, decided by the user on 17/11/2026: when the first stage is not geostatic the GPC
/// analysis warns (GPCChecker.Geotechnics 0.30, WallAnalysisResult.Warnings and WallEnvelopeHistory.Warnings) and ANTHEA shows the warning in the
/// sheet, in the envelope and in the report. Results do not change. A geostatic first stage gives no warning and the snapshot is written as before
/// (no "Warnings" member). Expected gap by hand, the case of the library test (review G-207, A7): K0 0.6 against 0.5, γ 18 kN/m³, 12 m, elements
/// of 0.5 m, deepest Gauss point at −11.965 m: 0.1 · 18 · (11.5 + 0.5 · 0.93057) = 21.5375 kPa, written 21,538 kPa.
/// </summary>
internal static partial class EmbeddedWallChecks
{
    private static JsonObject FirstStageExample(double leftK0, double rightK0)
    {
        var d = Example(); d["name"] = "Prima fase non geostatica — esempio sintetico"; d["loads"] = new JsonArray(); d["bottom"] = -12;
        d["segments"]![0]!["bottom"] = -12;
        foreach (var (side, k0) in new[] { ("left_soils", leftK0), ("right_soils", rightK0) })
        {
            var soil = EmbeddedWallDocument.NewRow(side); soil["name"] = "Sabbia sintetica";
            soil["gamma"] = 18; soil["gamma_sat"] = 20; soil["phi"] = 30; soil["c"] = 0; soil["k0"] = k0; soil["kh"] = .001;
            soil["source"] = "Valori di collaudo"; soil["stiffness_source"] = "Parametro di collaudo"; d[side] = new JsonArray(soil);
        }
        var first = d["stages"]![0]!.AsObject(); first["left_ground"] = 0; first["right_ground"] = 0; first["left_water"] = null; first["right_water"] = null;
        return d;
    }

    private static async Task FirstStageWarningAsync(Expect e, string output)
    {
        // The text of the library keeps its words; only the decimal point of the gap and of the elevation becomes the comma of the report.
        var localized = EmbeddedWallModule.AnalysisWarnings(["Prima fase «F1.2» non geostatica: (scarto massimo ai punti di integrazione 21.538 kPa a quota -11.965 m). Testo."]);
        e.That(localized is { Count: 1 } && localized[0] == "Prima fase «F1.2» non geostatica: (scarto massimo ai punti di integrazione 21,538 kPa a quota -11,965 m). Testo.",
            "A4: separatore dei numeri dell'avviso: " + localized?.FirstOrDefault());
        e.That(EmbeddedWallModule.AnalysisWarnings([]) is null && EmbeddedWallModule.AnalysisWarnings(null) is null, "A4: elenco vuoto degli avvisi scritto nel risultato.");

        var service = new EmbeddedWallModule();
        var geostatic = service.Calculate(FirstStageExample(.5, .5)).NativeAs<EmbeddedWallReportData>();
        var geostaticJson = JsonSerializer.SerializeToNode(geostatic)!.AsObject();
        e.That(geostatic.Warnings is null && !geostaticJson.ContainsKey("Warnings")
            && geostatic.Envelope!.Histories.All(h => h.Warnings is null) && !geostaticJson["Envelope"]!["Histories"]![0]!.AsObject().ContainsKey("Warnings"),
            "A4: avviso o campo «Warnings» con una prima fase geostatica.");
        var mensola = service.Calculate(Example()).NativeAs<EmbeddedWallReportData>();
        e.That(mensola.Warnings is null && !JsonSerializer.SerializeToNode(mensola)!.AsObject().ContainsKey("Warnings"), "A4: avviso sulla mensola senza terreno.");

        var data = FirstStageExample(.6, .5);
        var result = service.Calculate(data).NativeAs<EmbeddedWallReportData>();
        string expected = "scarto massimo ai punti di integrazione 21,538 kPa a quota -11,965 m";
        e.That(result.Warnings is { Count: 1 } w && w[0].StartsWith("Prima fase «F1» non geostatica", StringComparison.Ordinal) && w[0].Contains(expected, StringComparison.Ordinal),
            "A4: avviso della prima fase non geostatica: " + string.Join(" | ", result.Warnings ?? []));
        e.That(result.Envelope!.Histories.Single().Warnings?.SequenceEqual(result.Warnings!) == true, "A4: l'inviluppo non porta l'avviso della storia.");
        var reference = service.Calculate(FirstStageExample(.6, .5)).NativeAs<EmbeddedWallReportData>();
        e.That(result.Stages.Count == reference.Stages.Count && result.Stages.Zip(reference.Stages).All(p => p.First.Points.SequenceEqual(p.Second.Points)),
            "A4: calcolo non ripetibile con l'avviso.");

        // Round trip of the saved result, sheet, envelope and report.
        EmbeddedWallDocument.StoreResult(data, result);
        var restored = EmbeddedWallDocument.ReadCurrentResult(JsonNode.Parse(data.ToJsonString())!.AsObject());
        e.That(restored?.Warnings?.SequenceEqual(result.Warnings!) == true && restored.Envelope!.Histories[0].Warnings?.Count == 1,
            "A4: avviso perduto nel risultato salvato.");
        using var sheet = Open(data);
        e.That(sheet.Current is not null && sheet.ResultStatus.Contains("Avviso: Prima fase «F1» non geostatica", StringComparison.Ordinal)
            && sheet.ResultStatus.Contains(expected, StringComparison.Ordinal), "A4: avviso assente dallo stato del foglio: " + sheet.ResultStatus);
        e.That(sheet.EnvelopeStatus.Contains("Avviso, caso " + result.CaseId + ": Prima fase", StringComparison.Ordinal), "A4: avviso assente dagli inviluppi del foglio: " + sheet.EnvelopeStatus);
        byte[] docx = sheet.Report.BuildReport("Prima fase non geostatica", new HashSet<string>());
        string dir = Path.Combine(output, "paratie"); Directory.CreateDirectory(dir);
        ReportFiles.Write(Path.Combine(dir, "prima-fase-non-geostatica.docx"), docx, "Prima fase non geostatica");
        string xml = DocxWriter.Read(docx).GetXml("word/document.xml").ToString();
        e.That(xml.Contains("Avviso: Prima fase «F1» non geostatica", StringComparison.Ordinal) && xml.Contains(expected, StringComparison.Ordinal)
            && xml.Contains("Storia completa. Avviso: Prima fase", StringComparison.Ordinal) == result.Complete,
            "A4: avviso assente dalla relazione o dalla riga della storia negli inviluppi.");
        using var plain = Open(EmbeddedWallDocument.Snapshot(FirstStageExample(.5, .5)));
        await Expect.Within(plain.CalculateAsync());
        e.That(plain.Current is not null && !plain.ResultStatus.Contains("Avviso", StringComparison.Ordinal) && !plain.EnvelopeStatus.Contains("Avviso", StringComparison.Ordinal),
            "A4: avviso nel foglio con una prima fase geostatica.");
    }
}
