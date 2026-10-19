using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Application.EmbeddedWalls;
using Anthea.Application.Reporting;
using Anthea.Reports;

namespace Anthea.Presentation.Tests;

internal static partial class EmbeddedWallChecks
{
    internal static JsonObject EnvelopeExample()
    {
        var d = Example(); d["name"] = "Inviluppi — tre fasi elastiche indipendentemente verificabili"; d["case_id"] = "C1";
        d["segments"]![0]!["weight"] = 3; d["stages"] = new JsonArray(); d["loads"] = new JsonArray();
        foreach (string id in new[] { "Carico", "Inversione", "Intermedio" })
        {
            var stage = EmbeddedWallDocument.NewRow("stages"); stage["id"] = id; stage["left_ground"] = -6; stage["right_ground"] = -6;
            d["stages"]!.AsArray().Add(stage);
        }
        foreach (var a in new[] { ("Carico", 0d, 10d, 0d, 20d), ("Inversione", 0d, -6d, 12d, -30d),
            ("Intermedio", -2d, 5d, 0d, 30d), ("Intermedio", -4d, 0d, -8d, 0d) })
        {
            var load = EmbeddedWallDocument.NewRow("loads"); load["stage"] = a.Item1; load["elevation"] = a.Item2;
            load["horizontal"] = a.Item3; load["moment"] = a.Item4; load["downward"] = a.Item5; d["loads"]!.AsArray().Add(load);
        }
        return d;
    }
    private static async Task EnvelopesAsync(Expect e, string output)
    {
        var input = EnvelopeExample(); string original = input.ToJsonString();
        using var sheet = Open(input); e.That(await Expect.Within(sheet.CalculateAsync()), "Inviluppi: risultato non pubblicato.");
        var result = sheet.Current!; var envelope = result.Envelope!;
        e.That(input.ToJsonString() == original && envelope.Complete && envelope.Series.Count == 8 && envelope.Histories.Single().IncludedStages.Count == 3,
            "Inviluppi incompleti o dati chiamanti modificati.");
        var moment = envelope.Series.Single(s => s.Quantity == "Moment"); var displacement = envelope.Series.Single(s => s.Quantity == "Displacement");
        // Exact equilibrium and integrated curvature: M=H*x-C; u_head=H*L^3/(3EI)-C*L^2/(2EI).
        e.That(Math.Abs(moment.Minimum.Value + 48) < 5e-8 && Math.Abs(moment.Maximum.Value - 60) < 5e-8
            && moment.Minimum.Stage == "Inversione" && moment.Maximum.Stage == "Carico", "Estremi M non coerenti con l'equilibrio indipendente.");
        e.That(Math.Abs(displacement.Minimum.Value + 3.24) < 2e-8 && Math.Abs(displacement.Maximum.Value - 3.6) < 2e-8,
            "Estremi u non coerenti con l'integrazione indipendente.");
        var m = moment.Stations.First(s => Math.Abs(s.Elevation + 3) < 1e-9).Maximum;
        var n = envelope.Series.Single(s => s.Quantity == "NormalForce").Stations.First(s => Math.Abs(s.Elevation + 3) < 1e-9).Maximum;
        e.That(m.Stage == "Carico" && n.Stage == "Intermedio" && Math.Abs(m.Point.Normal - 29) < 1e-8
            && Math.Abs(n.Point.Normal - 39) < 1e-8 && Math.Abs(m.Point.Moment - 30) < 5e-8 && Math.Abs(n.Point.Moment - 5) < 5e-8,
            "Massimi N e M di fasi diverse sono stati combinati artificialmente.");
        sheet.SelectedEnvelopeSeries = moment; sheet.SelectedEnvelopeStation = moment.Stations.First(s => s.Elevation == -3);
        e.That(sheet.EnvelopeConcomitant.Count == 2 && ReferenceEquals(sheet.EnvelopeConcomitant[1].Value, sheet.SelectedEnvelopeStation.Maximum)
            && sheet.EnvelopeMinimum.SequenceEqual(moment.Stations.Select(s => s.Minimum.Point)), "VM perde i risultati o la concomitanza.");
        e.That(sheet.EnvelopeMaximum.SequenceEqual(moment.Stations.Select(s => s.Maximum.Point)) && sheet.EnvelopeCsvCommand.CanExecute(null), "Curve e comando CSV non disponibili.");
        string stored = sheet.Data.ToJsonString(); using var reopened = Open(JsonNode.Parse(stored)!.AsObject());
        e.That(JsonSerializer.Serialize(reopened.Current!.Envelope) == JsonSerializer.Serialize(envelope), "Riapertura altera gli inviluppi.");
        var csv = EmbeddedWallReport.EnvelopeCsv(result);
        e.That(csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length == 1 + 2 * envelope.Series.Sum(s => s.Stations.Count)
            && csv.Contains(result.Fingerprint) && csv.Contains("Inversione") && csv.Contains("Intermedio"), "CSV degli inviluppi incompleto.");
        var report = sheet.Report.BuildReport("Inviluppi di collaudo", new HashSet<string>());
        e.That(sheet.Data.ToJsonString() == stored && DocxWriter.Read(report).GetXml("word/document.xml").ToString().Contains("azioni concomitanti"),
            "Report non usa lo snapshot corrente degli inviluppi.");
        string folder = Path.Combine(output, "paratie");
        ReportFiles.Write(Path.Combine(folder, "inviluppi.docx"), report, "Inviluppi paratie");
        File.WriteAllText(Path.Combine(folder, "inviluppi.csv"), csv, new UTF8Encoding(true));
        File.WriteAllText(Path.Combine(folder, "inviluppi.json"), stored, new UTF8Encoding(true));
        reopened.Tables.Single(t => t.Key == "loads").Rows[0]["horizontal"] = "12";
        e.That(reopened.Current is null && reopened.EnvelopeSeries.Count == 0 && reopened.EnvelopeMinimum.Count == 0
            && reopened.EnvelopeMaximum.Count == 0 && reopened.SelectedEnvelopeStation is null && !reopened.EnvelopeCsvCommand.CanExecute(null),
            "Modifica degli input conserva gli inviluppi superati.");
        var mechanism = EnvelopeExample(); mechanism["fix_bottom_u"] = false; mechanism["fix_bottom_rotation"] = false;
        using var failed = Open(mechanism); await Expect.Within(failed.CalculateAsync());
        e.That(failed.Current is { Complete: false, Envelope.Complete: false } && failed.EnvelopeStatus.Contains("parziale"),
            "Inviluppo di una storia non convergente presentato completo.");
    }
}
