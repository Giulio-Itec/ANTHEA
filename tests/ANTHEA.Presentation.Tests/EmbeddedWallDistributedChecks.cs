using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Application.EmbeddedWalls;
using Anthea.Application.Reporting;
using Anthea.Reports;
using GPC.Model.Checking.Contracts;

namespace Anthea.Presentation.Tests;

internal static partial class EmbeddedWallChecks
{
    private static JsonObject DistributedExample()
    {
        var data = Example(); data["name"] = "Carico trapezoidale — mensola sintetica";
        data["width"] = 2.5; data["loads"] = new JsonArray();
        var load = EmbeddedWallDocument.NewRow("distributed_loads"); load["top"] = -1; load["bottom"] = -4.5;
        load["q_top"] = 1; load["q_bottom"] = 4; load["source"] = "Trapezio assegnato per fascia 2.5 m; atteso razionale indipendente";
        data["distributed_loads"]!.AsArray().Add(load); return data;
    }

    private static async Task DistributedLoadsAsync(Expect e, string output)
    {
        // Immutable rational-integration fixture generated before the GPC implementation.
        var expected = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "paratie-distributed-expected.json")))!;
        var service = new EmbeddedWallModule();
        foreach (var example in expected["cases"]!.AsArray())
        {
            var input = DistributedExample(); input["distributed_loads"] = new JsonArray(); int id = 0;
            foreach (var values in example!["loads"]!.AsArray())
            {
                var row = EmbeddedWallDocument.NewRow("distributed_loads"); row["id"] = "Q" + ++id;
                row["top"] = -values![0]!.GetValue<double>() / 1000; row["bottom"] = -values[1]!.GetValue<double>() / 1000;
                row["q_top"] = values[2]!.DeepClone(); row["q_bottom"] = values[3]!.DeepClone(); row["source"] = "Integrazione razionale: " + example["name"];
                input["distributed_loads"]!.AsArray().Add(row);
            }
            var result = service.Calculate(input).NativeAs<EmbeddedWallReportData>();
            e.That(result.Complete && result.Stages.Count == 1, "Caso distribuito incompleto: " + example["name"]);
            foreach (var point in example["points"]!.AsArray())
            {
                double z = -point!["depth"]!.GetValue<double>() / 1000;
                var cuts = result.Stages[0].Points.Where(p => Math.Abs(p.Elevation - z) < 1e-10).ToArray();
                e.That(cuts.Length > 0, "Sezione del benchmark non campionata.");
                foreach (var cut in cuts)
                    e.That(Math.Abs(cut.Displacement - point["displacement"]!.GetValue<double>()) < 5e-8
                        && Math.Abs(cut.Rotation - point["rotation"]!.GetValue<double>()) < 5e-11
                        && Math.Abs(cut.Shear - point["shear"]!.GetValue<double>() / 1000) < 2e-8
                        && Math.Abs(cut.Moment - point["moment"]!.GetValue<double>() / 1e6) < 5e-8,
                        "Conversione della fascia distribuita errata: " + example["name"] + " z=" + z);
            }
        }
        var legacy = Example(); legacy["schema"] = 8; legacy.Remove("distributed_loads");
        string before = legacy.ToJsonString(); var upgraded = EmbeddedWallDocument.Snapshot(legacy);
        e.That(legacy.ToJsonString() == before && upgraded["schema"]!.ToString() == "9"
            && upgraded["distributed_loads"]!.AsArray().Count == 0, "Migrazione 8→9 cambia gli input o aggiunge carichi.");
        e.That(Math.Abs(service.Calculate(legacy).NativeAs<EmbeddedWallReportData>().Stages[0].Points[0].Displacement - 3.6) < 1e-7,
            "Migrazione modifica la mensola storica.");
        var data = DistributedExample();
        foreach (string name in new[] { "F2", "F3" })
        {
            var stage = data["stages"]![0]!.DeepClone(); stage["id"] = name; data["stages"]!.AsArray().Add(stage);
        }
        var reverse = data["distributed_loads"]![0]!.DeepClone(); reverse["stage"] = "F3";
        reverse["q_top"] = -1; reverse["q_bottom"] = -4; data["distributed_loads"]!.AsArray().Add(reverse);
        before = data.ToJsonString(); using var sheet = Open(data);
        e.That(await Expect.Within(sheet.CalculateAsync()) && sheet.Current?.Complete == true && sheet.Stages.Count == 3,
            "Storia carico/rimozione/inversione non completata.");
        var report = sheet.Current!;
        e.That(data.ToJsonString() == before && Math.Abs(report.Stages[0].Points[0].Displacement - .9843932291666667) < 5e-8
            && report.Stages[1].Points.All(p => Math.Abs(p.Displacement) < 5e-8 && Math.Abs(p.Moment) < 5e-8 && p.NetDistributedLoad == 0)
            && Math.Abs(report.Stages[2].Points[0].Displacement + .9843932291666667) < 5e-8,
            "Rimozione del carico omesso o inversione della fase errate.");
        var edge = report.Stages[0].Points.Where(p => p.Elevation == -1).ToArray();
        e.That(edge.Any(p => Math.Abs(p.NetDistributedLoad) < 1e-10) && edge.Any(p => Math.Abs(p.NetDistributedLoad - 1) < 1e-10),
            "Discontinuità del carico netto non conservata sui due lati.");
        e.That(report.Stages.All(s => Math.Abs(s.ForceResidualN) < 2e-5 && Math.Abs(s.MomentResidualNmm) < .1), "Equilibrio della storia distribuita errato.");
        using var reopened = Open(JsonNode.Parse(sheet.Data.ToJsonString())!.AsObject());
        e.That(JsonSerializer.Serialize(reopened.Current) == JsonSerializer.Serialize(report), "Riapertura perde lo snapshot dei carichi variabili.");
        var docx = sheet.Report.BuildReport("Carichi distribuiti per fasi", new HashSet<string>()); before = sheet.Data.ToJsonString();
        ReportFiles.Write(Path.Combine(output, "paratie", "carichi-distribuiti.docx"), docx, "Carichi distribuiti per fasi");
        var xml = DocxWriter.Read(docx).GetXml("word/document.xml").ToString();
        e.That(xml.Contains("q netto") && xml.Contains("q superiore") && xml.Contains("0,984393") && before == sheet.Data.ToJsonString(),
            "Report non rappresenta il risultato e gli input del carico distribuito.");
        string csv = EmbeddedWallReport.Csv(report); File.WriteAllText(Path.Combine(output, "paratie", "carichi-distribuiti.csv"), csv);
        File.WriteAllText(Path.Combine(output, "paratie", "carichi-distribuiti.json"), sheet.Data.ToJsonString(new() { WriteIndented = true }));
        using var parser = new Microsoft.VisualBasic.FileIO.TextFieldParser(new StringReader(csv)) { TextFieldType = Microsoft.VisualBasic.FileIO.FieldType.Delimited, HasFieldsEnclosedInQuotes = true };
        parser.SetDelimiters(";"); var header = parser.ReadFields()!; e.That(header.Length == 14 && header[13] == "q_netto_kN_m", "Colonna q netto CSV assente.");
        foreach (var stage in report.Stages) foreach (var point in stage.Points)
        {
            var row = parser.ReadFields()!;
            e.That(row.Length == 14 && row[1] == stage.Id && row[12] == report.Fingerprint
                && double.Parse(row[13], CultureInfo.InvariantCulture) == point.NetDistributedLoad, "CSV ricalcola o perde la provenienza di q netto.");
        }
        e.That(parser.EndOfData, "Righe CSV aggiunte allo snapshot.");
        reopened.Tables.Single(t => t.Key == "distributed_loads").Rows[0]["q_top"] = "2";
        e.That(reopened.Current is null && reopened.Stages.Count == 0 && !reopened.CsvCommand.CanExecute(null), "Modifica del carico non invalida i risultati.");
        e.Throws<InvalidOperationException>(() => reopened.Report.BuildReport("Superato", new HashSet<string>()), "Report con carichi superati consentito.");
        foreach (string reason in new[] { "phase", "range", "order", "source", "missing", "duplicate" })
        {
            var invalid = DistributedExample(); var row = invalid["distributed_loads"]![0]!;
            switch (reason)
            {
                case "phase": row["stage"] = "F99"; break;
                case "range": row["bottom"] = -9; break;
                case "order": row["top"] = -5; break;
                case "source": row["source"] = ""; break;
                case "missing": row["q_top"] = null; break;
                case "duplicate": invalid["distributed_loads"]!.AsArray().Add(row.DeepClone()); break;
            }
            e.Throws<ArgumentException>(() => service.Calculate(invalid), "Carico distribuito invalido accettato: " + reason);
        }
        e.Throws<OperationCanceledException>(() => service.Calculate(DistributedExample(), new CancellationToken(true)), "Annullamento dei carichi distribuiti ignorato.");
        var concurrent = await Task.WhenAll(Task.Run(() => service.Calculate(data).NativeAs<EmbeddedWallReportData>()), Task.Run(() => new EmbeddedWallModule().Calculate(DistributedExample()).NativeAs<EmbeddedWallReportData>()));
        e.That(concurrent[0].Stages.Count == 3 && concurrent[1].Stages.Count == 1
            && JsonSerializer.Serialize(concurrent[0].Stages) == JsonSerializer.Serialize(report.Stages), "Storie distribuite contaminate.");

        var globalData = GlobalExample(); var interval = DistributedExample()["distributed_loads"]![0]!.DeepClone();
        interval["q_top"] = .1; interval["q_bottom"] = -.1; globalData["distributed_loads"]!.AsArray().Add(interval);
        var action = EmbeddedWallDocument.NewRow("global_actions"); action["id"] = "StructureInterval-Q1";
        action["psi"] = .7; action["source"] = "Carico variabile accompagnatore del caso sintetico"; globalData["global_actions"]!.AsArray().Add(action);
        var global = service.Calculate(globalData).NativeAs<EmbeddedWallReportData>().GlobalChecks!;
        e.That(global.Count == 2 && global[0].Circle is not null && global[0].Actions.Any(a => a.Id == "StructureInterval-Q1" && Math.Abs(a.CombinedFactor - .91) < 1e-12)
            && global[1].Actions.All(a => a.Id != "StructureInterval-Q1"), "Coppia da carico a risultante nulla perduta o rifattorizzata nella stabilità globale.");
        globalData["global_actions"]!.AsArray().RemoveAt(2);
        var unsupported = service.Calculate(globalData).NativeAs<EmbeddedWallReportData>().GlobalChecks![0];
        e.That(unsupported.Outcome == EngineeringOutcome.NotEvaluated && unsupported.Ratio is null, "Carico globale senza fattore esplicito certificato.");
    }
}
