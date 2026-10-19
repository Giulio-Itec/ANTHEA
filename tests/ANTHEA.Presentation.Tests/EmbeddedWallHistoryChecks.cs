using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Application.EmbeddedWalls;
using Anthea.Application.Modules;
using Anthea.Application.Reporting;
using Anthea.Application.Documents;
using Anthea.Reports;

namespace Anthea.Presentation.Tests;

internal static partial class EmbeddedWallChecks
{
    internal static JsonObject HistoriesExample()
    {
        var d = Example(); d["case_id"] = "C1";
        var other = EmbeddedWallDocument.NewRow("cases"); other["id"] = "C2";
        other["action_basis"] = "Azioni assegnate: H=-6 kN, C=+12 kNm, N=-30 kN; nessun coefficiente";
        d["cases"]!.AsArray().Add(other);
        var stage = d["stages"]![0]!.DeepClone().AsObject(); stage["case"] = "C2"; d["stages"]!.AsArray().Add(stage);
        var load = d["loads"]![0]!.DeepClone().AsObject(); load["case"] = "C2"; load["horizontal"] = -6;
        load["moment"] = 12; load["downward"] = -30; d["loads"]!.AsArray().Add(load);
        return d;
    }
    private static async Task HistoriesAsync(Expect e, string output)
    {
        var prior = Example(); prior["schema"] = 10; prior.Remove("cases"); string priorJson = prior.ToJsonString();
        var upgraded = EmbeddedWallDocument.Snapshot(prior);
        e.That(prior.ToJsonString() == priorJson && upgraded["schema"]!.ToString() == "11" && upgraded["cases"]!.AsArray().Count == 0
            && prior.All(p => p.Key == "schema" || JsonNode.DeepEquals(p.Value, upgraded[p.Key])),
            "Migrazione schema 10 modifica gli input o introduce un caso.");
        var input = HistoriesExample(); string original = input.ToJsonString(); var service = new EmbeddedWallModule();
        var result = service.Calculate(input).NativeAs<EmbeddedWallReportData>();
        e.That(input.ToJsonString() == original && result.Complete && result.Cases?.Count == 2 && result.Envelope?.Histories.Count == 2,
            "Storie multiple: input alterato, casi persi o inviluppo assente.");
        e.That(EmbeddedWallDocument.ResultValues(result)["fasi_complete"]!.GetValue<int>() == 2,
            "Riepilogo delle fasi limita il conteggio al caso principale.");
        var expected = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "histories-independent.json")))!.AsObject();
        foreach (var history in expected["cases"]!.AsArray().Cast<JsonObject>())
        {
            var actual = result.Cases!.Single(r => r.CaseId == history["id"]!.GetValue<string>());
            foreach (var row in history["rows"]!.AsArray().Cast<JsonObject>())
            {
                double z = row["z"]!.GetValue<double>() / 1000;
                var point = actual.Stages.Single().Points.First(p => Math.Abs(p.Elevation - z) < 1e-10);
                e.That(Math.Abs(point.Displacement - row["u"]!.GetValue<double>()) < 2e-8
                    && Math.Abs(point.Rotation - row["rotation"]!.GetValue<double>()) < 2e-11
                    && Math.Abs(point.Normal * 1000 - row["N"]!.GetValue<double>()) < 2e-7
                    && Math.Abs(point.Shear * 1000 - row["V"]!.GetValue<double>()) < 2e-5
                    && Math.Abs(point.Moment * 1e6 - row["M"]!.GetValue<double>()) < .05,
                    "Confronto razionale indipendente fallito: " + actual.CaseId + " / " + z);
            }
        }
        foreach (var single in EmbeddedWallDocument.SplitHistories(input))
        {
            var separate = service.Calculate(single).NativeAs<EmbeddedWallReportData>();
            e.That(JsonSerializer.Serialize(separate) == JsonSerializer.Serialize(result.Cases!.Single(r => r.CaseId == separate.CaseId)),
                "La storia nel gruppo differisce dal ricalcolo indipendente, incluse le verifiche.");
        }
        var moment = result.Envelope!.Series.Single(s => s.Quantity == "Moment");
        e.That(moment.Minimum.CaseId == "C2" && moment.Maximum.CaseId == "C1" && Math.Abs(moment.Minimum.Value + 48) < 5e-8
            && Math.Abs(moment.Maximum.Value - 60) < 5e-8 && Math.Abs(moment.Minimum.Point.Normal + 30) < 1e-9
            && Math.Abs(moment.Maximum.Point.Normal - 100) < 1e-9, "Inviluppo multi-caso perde concomitanza o provenienza.");
        using var sheet = Open(input); e.That(await Expect.Within(sheet.CalculateAsync()), "Gruppo non pubblicato nel foglio.");
        e.That(sheet.ResultCases.Count == 2 && sheet.SelectedResultCase?.CaseId == "C1", "Selezione iniziale dei casi errata.");
        sheet.SelectedResultCase = sheet.ResultCases.Single(r => r.CaseId == "C2");
        e.That(sheet.SelectedStage?.Id == "F1" && Math.Abs(sheet.Points.First().Displacement + 3.24) < 2e-8
            && sheet.ComparisonStage is null, "Cambio caso conserva dati della storia precedente.");
        string stored = sheet.Data.ToJsonString(); using var reopened = Open(JsonNode.Parse(stored)!.AsObject());
        e.That(JsonSerializer.Serialize(reopened.Current!.Cases) == JsonSerializer.Serialize(sheet.Current!.Cases)
            && reopened.ResultCases.Count == 2, "Persistenza delle storie multiple incompleta.");
        string csv = EmbeddedWallReport.Csv(sheet.Current!);
        e.That(csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length == 1 + sheet.Current!.Cases!.Sum(c => c.Stages.Sum(s => s.Points.Count))
            && csv.Contains("\"C1\"") && csv.Contains("\"C2\""), "CSV multi-caso omette righe o duplica l'intestazione.");
        var report = sheet.Report.BuildReport("Storie indipendenti", new HashSet<string>());
        string xml = DocxWriter.Read(report).GetXml("word/document.xml").ToString();
        e.That(xml.Contains("C1") && xml.Contains("C2") && sheet.Data.ToJsonString() == stored, "Report multi-caso altera o ricalcola il risultato.");
        string folder = Path.Combine(output, "paratie");
        ReportFiles.Write(Path.Combine(folder, "storie-multiple.docx"), report, "Storie indipendenti");
        File.WriteAllText(Path.Combine(folder, "storie-multiple.json"), stored);
        File.WriteAllText(Path.Combine(folder, "storie-multiple.csv"), csv);
        var sampleFolder = Path.Combine(folder, "esempi"); Directory.CreateDirectory(sampleFolder);
        foreach (var sample in new[] { ("01-mensola-analitica", Example()), ("02-storie-indipendenti", input),
            ("03-filtrazione", HydraulicExample()), ("04-pali-ca", RcPileExample()), ("05-tubolari-acciaio", TubeExample()),
            ("06-sostegni-acciaio", SteelExample()), ("07-sfilamento-tiranti", AnchorExample()), ("08-ricerca-infissione", EmbedmentExample()) })
        {
            var document = DocumentService.CreateDocument(EmbeddedWallDocument.Module); document["dati"] = sample.Item2.DeepClone();
            string path = Path.Combine(sampleFolder, sample.Item1 + ".anth"); DocumentService.Save(path, document);
            var opened = DocumentService.Open(path);
            e.That(JsonNode.DeepEquals(document, opened), "Esempio apribile alterato dal salvataggio: " + sample.Item1);
            using var sampleSheet = Open(opened["dati"]!.AsObject());
            e.That(sampleSheet.Data["schema"]!.ToString() == "11", "Esempio non apribile nel foglio: " + sample.Item1);
        }
        reopened.Tables.Single(t => t.Key == "cases").Rows[0]["action_basis"] = "Fonte modificata";
        e.That(reopened.Current is null && reopened.ResultCases.Count == 0 && reopened.EnvelopeSeries.Count == 0,
            "Modifica di un caso conserva risultati correnti.");
        var duplicate = HistoriesExample(); duplicate["cases"]![0]!["id"] = "C1";
        e.Throws<ArgumentException>(() => service.Calculate(duplicate), "Identificativo caso duplicato accettato.");
        var orphan = HistoriesExample(); orphan["loads"]![1]!["case"] = "Ignoto";
        e.Throws<ArgumentException>(() => service.Calculate(orphan), "Carico di un caso ignoto ignorato.");
        var cross = HistoriesExample(); cross["loads"]![1]!["stage"] = "F2";
        e.Throws<ArgumentException>(() => service.Calculate(cross), "Fase mancante nel caso accettata.");
        var undefined = HistoriesExample(); undefined["cases"]![0]!["action_basis"] = "";
        e.Throws<ArgumentException>(() => service.Calculate(undefined), "Provenienza mancante del caso accettata.");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        e.Throws<OperationCanceledException>(() => service.Calculate(input, cancelled.Token), "Annullamento dei casi ignorato.");
        HistoryIsolationAndChecks(e);
        await HistoryEmbedmentAsync(e);
    }

    private static JsonObject AddHistory(JsonObject data, string id)
    {
        var row = EmbeddedWallDocument.NewRow("cases"); row["id"] = id;
        foreach (string key in EmbeddedWallDocument.CaseOverrideKeys) row[key] = data[key]?.DeepClone();
        data["cases"]!.AsArray().Add(row);
        foreach (string table in EmbeddedWallDocument.HistoryTables)
        foreach (var source in data[table]!.AsArray().Cast<JsonObject>().Where(r => string.IsNullOrWhiteSpace(r["case"]?.ToString())).ToArray())
        {
            var copy = source.DeepClone().AsObject(); copy["case"] = id; data[table]!.AsArray().Add(copy);
        }
        return row;
    }
    private static void HistoryIsolationAndChecks(Expect e)
    {
        var service = new EmbeddedWallModule();
        foreach (var input in new[] { RcPileExample(), TubeExample(), SteelExample(), AnchorExample(), HydraulicExample(), SafetyExample(), GlobalExample() })
        {
            input["case_id"] = "C1"; var second = AddHistory(input, "C2"); second["action_basis"] = "Seconda storia di verifica, azioni assegnate";
            if (input["rc_pile_standard"]!.ToString() == "NTC 2018") second["rc_pile_check_kind"] = "Characteristic";
            foreach (var row in input["loads"]!.AsArray().Cast<JsonObject>().Where(r => r["case"]?.ToString() == "C2"))
                row["horizontal"] = -double.Parse(row["horizontal"]?.ToString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
            if (input["hydraulic_mode"]!.ToString() == "FEM 2D")
                foreach (var stage in input["stages"]!.AsArray().Cast<JsonObject>().Where(r => r["case"]?.ToString() == "C2")) stage["left_water"] = .5;
            var combined = service.Calculate(input).NativeAs<EmbeddedWallReportData>();
            e.That(combined.Cases?.Count == 2, "Famiglia di verifiche perde un caso.");
            foreach (var single in EmbeddedWallDocument.SplitHistories(input))
            {
                var separate = service.Calculate(single).NativeAs<EmbeddedWallReportData>();
                var actual = combined.Cases!.Single(c => c.CaseId == separate.CaseId);
                e.That(JsonSerializer.Serialize(actual) == JsonSerializer.Serialize(separate),
                    "Trasferimento delle azioni/verifiche o FEM non indipendente: " + input["name"] + "/" + separate.CaseId);
            }
            foreach (var csv in new[] { EmbeddedWallReport.SteelCsv(combined), EmbeddedWallReport.AnchorCsv(combined),
                EmbeddedWallReport.TubeCsv(combined), EmbeddedWallReport.RcPileCsv(combined), EmbeddedWallReport.HydraulicCsv(combined),
                EmbeddedWallReport.HydraulicChecksCsv(combined), EmbeddedWallReport.GlobalCsv(combined) })
                if (csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length > 1)
                    e.That(csv.Contains("\"C1\"") && csv.Contains("\"C2\""), "CSV di verifica/FEM omette la provenienza di un caso.");
        }
        var plastic = EmbedmentExample(); plastic["case_id"] = "C1";
        foreach (var row in plastic["loads"]!.AsArray().Cast<JsonObject>())
            row["horizontal"] = row["stage"]!.ToString() == "Loaded" ? 100 : row["stage"]!.ToString() == "Reversed" ? -50 : 0;
        AddHistory(plastic, "C2");
        foreach (var row in plastic["loads"]!.AsArray().Cast<JsonObject>().Where(r => r["case"]?.ToString() == "C2"))
            row["horizontal"] = row["stage"]!.ToString() == "Loaded" ? -75 : row["stage"]!.ToString() == "Reversed" ? 20 : 0;
        var nonlinear = service.Calculate(plastic).NativeAs<EmbeddedWallReportData>();
        e.That(nonlinear.Complete && nonlinear.Cases!.All(c => c.Stages.Any(s => s.Points.Any(p => p.LeftPlastic || p.RightPlastic))),
            "Il confronto di isolamento non ha mobilitato la plasticità in entrambi i casi.");
        foreach (var single in EmbeddedWallDocument.SplitHistories(plastic))
            e.That(JsonSerializer.Serialize(service.Calculate(single).NativeAs<EmbeddedWallReportData>())
                == JsonSerializer.Serialize(nonlinear.Cases!.Single(c => c.CaseId == single["case_id"]!.ToString())), "Memoria plastica condivisa fra casi.");
        var failure = EmbedmentExample(); failure["case_id"] = "C1"; AddHistory(failure, "C2"); AddHistory(failure, "C3");
        foreach (var row in failure["loads"]!.AsArray().Cast<JsonObject>().Where(r => r["case"]?.ToString() == "C2" && r["stage"]!.ToString() == "Loaded")) row["horizontal"] = 1e6;
        var incomplete = service.Calculate(failure).NativeAs<EmbeddedWallReportData>();
        e.That(!incomplete.Complete && incomplete.Cases is { Count: 3 } && incomplete.Cases[0].Complete && !incomplete.Cases[1].Complete
            && incomplete.Cases[2].Complete && incomplete.Envelope is { Complete: false }, "Caso non convergente ignorato o impedisce gli altri casi.");
        e.That(EmbeddedWallReport.Csv(incomplete).Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1)
            .All(line => line.Contains("HistoriesIncomplete /")), "CSV delle storie parziali perde lo stato incompleto del gruppo.");
        // An excessive load at the very first stage leaves no accepted mechanical phase in C2.
        foreach (var row in failure["loads"]!.AsArray().Cast<JsonObject>().Where(r => r["case"]?.ToString() == "C2")) row["horizontal"] = 1e6;
        var noPhase = service.Calculate(failure).NativeAs<EmbeddedWallReportData>();
        e.That(!noPhase.Complete && noPhase.Cases![1].Stages.Count == 0 && noPhase.Cases[2].Complete,
            "La prova del caso interamente fallito non raggiunge il ramo previsto.");
        var failedLines = EmbeddedWallReport.Csv(noPhase).Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.StartsWith("\"C2\"", StringComparison.Ordinal)).ToArray();
        e.That(failedLines.Length == 1 && failedLines[0].Contains("Nessuna fase meccanica conclusa")
            && failedLines[0].Contains("HistoriesIncomplete /"), "CSV omette o rappresenta come convergente il caso senza fasi accettate.");
        using var cancel = new CancellationTokenSource();
        var cancellable = new EmbeddedWallModule(); cancellable.ProgressChanged += p => { if (p.Stage.StartsWith("C2 /", StringComparison.Ordinal)) cancel.Cancel(); };
        e.Throws<OperationCanceledException>(() => cancellable.Calculate(plastic, cancel.Token), "Annullamento durante la seconda storia ignorato.");
    }

    private static async Task HistoryEmbedmentAsync(Expect e)
    {
        var d = EmbedmentExample(); d["case_id"] = "C1";
        var other = EmbeddedWallDocument.NewRow("cases"); other["id"] = "C2"; other["action_basis"] = "Seconda storia indipendente H=15 kN";
        other["embedment_head_limit"] = 4.5; other["embedment_source"] = "Secondo limite esplicito del benchmark indipendente";
        d["cases"]!.AsArray().Add(other);
        foreach (string table in new[] { "stages", "loads" })
        foreach (var original in d[table]!.AsArray().ToArray())
        {
            var row = original!.DeepClone().AsObject(); row["case"] = "C2";
            if (table == "loads" && row["stage"]!.ToString() == "Loaded") row["horizontal"] = 15;
            d[table]!.AsArray().Add(row);
        }
        using var sheet = Open(d); e.That(await sheet.SearchEmbedmentAsync().WaitAsync(TimeSpan.FromMinutes(2)), "Ricerca multi-caso non pubblicata.");
        var search = sheet.Current!.EmbedmentSearch!;
        // The independent cosh/sin benchmark already established 6.5 m for C1 and 7.5 m for C2.
        e.That(search.MinimumCertified && search.BestEmbedment == 7.5 && sheet.Current.Cases?.Count == 2
            && sheet.Current.Envelope is { Complete: true, Bottom: -7.5 }, "Minimo comune, storie o inviluppo del candidato errati.");
        e.That(search.Candidates.All(c => c.Criteria.Select(v => v.CaseId).Distinct().Count() == 2), "Ricerca omette i criteri di un caso.");
        foreach (var candidate in search.Candidates.Where(c => c.Result is not null))
            e.That(candidate.Result!.Cases!.All(c => c.Envelope?.Bottom == candidate.ToeElevation), "Candidato conserva campi di un'altra geometria.");
        string csv = EmbeddedWallReport.EmbedmentCsv(sheet.Current);
        e.That(csv.Contains("\"C2\"") && csv.Split('\n').Count(l => l.StartsWith("\"Sezione\""))
            == search.Candidates.Sum(c => c.Result?.Cases?.Sum(r => r.Stages.Sum(s => s.Points.Count)) ?? 0), "CSV ricerca omette una storia.");
    }
}
