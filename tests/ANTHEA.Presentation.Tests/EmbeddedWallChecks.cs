using System.Text;
using System.Text.Json.Nodes;
using Anthea.Application.EmbeddedWalls;
using Anthea.Application.Documents;
using Anthea.Application.Stratigraphy;
using Anthea.Application.Modules;
using Anthea.Application.Reporting;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.EmbeddedWalls;
using Anthea.Reports;
using X.Core;

namespace Anthea.Presentation.Tests;

internal static partial class EmbeddedWallChecks
{
    internal static int Run(string output) => SingleThreadContext.Run(_ => CheckAsync(output));
    internal static JsonObject Example()
    {
        var d = EmbeddedWallDocument.Create(); d["name"] = "Mensola analitica — esempio sintetico"; d["bottom"] = -6;
        d["mesh"] = .5; d["fix_bottom_u"] = true; d["fix_bottom_rotation"] = true; d["action_basis"] = "Azioni assegnate senza coefficienti; esempio analitico di collaudo";
        d["left_soils"] = new JsonArray(); d["right_soils"] = new JsonArray();
        var segment = d["segments"]![0]!.AsObject(); segment["bottom"] = -6; segment["ei"] = 200000; segment["source"] = "EI assegnato: 2e14 Nmm²";
        var stage = d["stages"]![0]!.AsObject(); stage["left_ground"] = -6; stage["right_ground"] = -6;
        var load = EmbeddedWallDocument.NewRow("loads"); load["horizontal"] = 10; load["downward"] = 100; d["loads"]!.AsArray().Add(load);
        return d;
    }
    private static async Task<int> CheckAsync(string output)
    {
        var e = new Expect(); var input = Example(); string original = input.ToJsonString();
        var service = ModuleServices.Get(EmbeddedWallDocument.Module);
        var calculation = service.Calculate(input); var result = calculation.NativeAs<EmbeddedWallReportData>();
        e.That(input.ToJsonString() == original, "Il servizio ha modificato gli input.");
        e.That(result.Complete && result.Stages.Count == 1, "Mensola non convergente.");
        var stage = result.Stages.Single();
        e.That(Math.Abs(stage.Points[0].Displacement - 3.6) < 1e-7, "Conversione EI/lunghezze/forze: u atteso 3.6 mm.");
        e.That(Math.Abs(stage.Points.Last().Moment - 60) < 1e-6 && Math.Abs(stage.Points.Last().Normal - 100) < 1e-9, "Azioni della fascia: attesi 60 kNm e 100 kN.");
        e.That(Math.Abs(stage.ForceResidualN) < 1e-4 && Math.Abs(stage.MomentResidualNmm) < .1, "Equilibrio dopo adattamento.");
        e.That(EmbeddedWallDocument.ReadCurrentResult(input) is null, "Risultato presente prima del salvataggio.");
        EmbeddedWallDocument.StoreResult(input, result);
        var reopened = JsonNode.Parse(input.ToJsonString())!.AsObject(); var restored = EmbeddedWallDocument.ReadCurrentResult(reopened);
        e.That(restored is not null && restored.Stages[0].Points.SequenceEqual(result.Stages[0].Points), "Risultati alterati nel round trip JSON.");
        var archive = ProjectDocuments.CreateArchive(); var project = ProjectDocuments.AddProject(archive, "Collaudo paratie");
        var projectSheet = ProjectDocuments.AddSheet(project, EmbeddedWallDocument.Module); projectSheet["dati"] = input.DeepClone();
        Directory.CreateDirectory(output); string archivePath = Path.Combine(output, "paratie.anthea");
        DocumentService.Save(archivePath, archive); var archiveBack = DocumentService.Open(archivePath);
        var archived = archiveBack["progetti"]![0]!["fogli"]![0]!["dati"]!.AsObject();
        e.That(EmbeddedWallDocument.ReadCurrentResult(archived)?.Fingerprint == result.Fingerprint, "Archivio reale: risultato o impronta perduti.");
        var stratigraphy = StratigraphyDocument.Create(); stratigraphy["ground_elevation"] = 2;
        var stratum = stratigraphy["layers"]![0]!.AsObject(); stratum["infinite"] = true; stratum["thickness"] = null;
        stratum["gamma"] = 18; stratum["gamma_sat"] = 20; stratum["phi"] = 30; stratum["c"] = 0;
        stratum["source"] = "Sondaggio sintetico di collaudo"; stratum["qualification"] = "Caratteristico";
        var imported = EmbeddedWallDocument.WithStratigraphy(input, stratigraphy, "left_soils", "Medi");
        e.That(imported["left_soils"]![0]!["top"]!.GetValue<double>() == 2 && imported["left_soils"]![0]!["bottom"]!.GetValue<double>() == -6,
            "Trasferimento terreno: quote assolute errate.");
        e.That(imported["left_soils"]![0]!["kh"] is null && imported["left_soils"]![0]!["k0"] is null && EmbeddedWallDocument.ReadCurrentResult(imported) is null,
            "Trasferimento terreno: kh inventato o risultati ancora correnti.");
        e.That(imported["soil_transfer_left_soils"] is not null && stratigraphy["layers"]![0]!["gamma"]!.GetValue<int>() == 18, "Provenienza perduta o stratigrafia sorgente modificata.");
        reopened["mesh"] = .3; e.That(EmbeddedWallDocument.ReadCurrentResult(reopened) is null, "Cache non invalidata dalla mesh.");
        var future = Example(); future["schema"] = 99; e.Throws<ArgumentException>(() => EmbeddedWallDocument.Snapshot(future), "Schema futuro accettato.");
        var invalid = Example(); invalid["ei"] = 0; invalid["segments"]![0]!["ei"] = "non numerico";
        e.Throws<ArgumentException>(() => service.Calculate(invalid), "EI non numerico accettato.");
        var source = Example(); var clone = EmbeddedWallDocument.Snapshot(source); clone["loads"]![0]!["horizontal"] = 20;
        var second = service.Calculate(clone).NativeAs<EmbeddedWallReportData>();
        e.That(Math.Abs(second.Stages[0].Points[0].Displacement - 7.2) < 1e-7 && source["loads"]![0]!["horizontal"]!.ToString() == "10", "Analisi non isolate.");
        using var sheet = Open(input);
        e.That(sheet.Current is not null && sheet.HasResults, "Riapertura del risultato corrente.");
        e.That(sheet.Result?["risultato"]?["Fingerprint"]?.ToString() == result.Fingerprint, "Esportazione JSON alla riapertura priva del risultato.");
        int changes = 0; sheet.Modified += (_, _) => changes++;
        sheet.Tables.Single(t => t.Key == "loads").Rows[0]["horizontal"] = "20";
        e.That(sheet.Current is null && !sheet.HasResults && changes == 1, "Risultati non invalidati dall'editor.");
        e.Throws<InvalidOperationException>(() => sheet.Report.BuildReport("Paratia", new HashSet<string>()), "Report su risultati superati.");
        e.That(await Expect.Within(sheet.CalculateAsync()), "Calcolo asincrono non pubblicato.");
        e.That(sheet.Current is not null && Math.Abs(sheet.Current.Stages[0].Points[0].Displacement - 7.2) < 1e-7, "Risultato UI diverso dal servizio.");
        e.That(EmbeddedWallDocument.ReadCurrentResult(sheet.Data)?.Fingerprint == sheet.Current!.Fingerprint, "Risultato del foglio non persistito.");
        string beforeReport = sheet.Data.ToJsonString(); byte[] docx = sheet.Report.BuildReport("Paratia — prova indipendente", new HashSet<string>());
        string dir = Path.Combine(output, "paratie"); Directory.CreateDirectory(dir);
        var pdf = ReportFiles.Write(Path.Combine(dir, "mensola-analitica.docx"), docx, "Mensola analitica");
        File.WriteAllText(Path.Combine(dir, "mensola-analitica.csv"), EmbeddedWallReport.Csv(sheet.Current), new UTF8Encoding(true));
        File.WriteAllText(Path.Combine(dir, "documento.json"), sheet.Data.ToJsonString(new() { WriteIndented = true }), new UTF8Encoding(false));
        e.That(pdf.Pages > 0 && sheet.Data.ToJsonString() == beforeReport, "Report ha modificato il documento.");
        var xml = DocxWriter.Read(docx).GetXml("word/document.xml").ToString();
        e.That(xml.Contains(sheet.Current.Fingerprint) && xml.Contains("7,2"), "Report diverso dal risultato mostrato.");
        var builder = new ModuleReportService(new NoFigures());
        var chapter = await builder.BuildAsync(new JsonObject { ["modulo_id"] = EmbeddedWallDocument.Module, ["nome"] = "Paratia", ["dati"] = sheet.Data.DeepClone() }, default);
        e.That(chapter.Document is not null, "Capitolo di progetto assente.");
        using var readOnly = Open(sheet.Data, true);
        readOnly.Tables.Single(t => t.Key == "loads").Rows[0]["horizontal"] = "80";
        e.That(readOnly.Data.ToJsonString() == sheet.Data.ToJsonString(), "Modificata una revisione in sola lettura.");
        var mechanism = Example(); mechanism["fix_bottom_u"] = false; mechanism["fix_bottom_rotation"] = false;
        using var failed = Open(mechanism); await Expect.Within(failed.CalculateAsync());
        e.That(failed.Current is not null && !failed.Current.Complete && !failed.HasResults && failed.Current.Stages.Count == 0, "Meccanismo presentato come verifica valida.");
        using var gate = new HeldModule(); using var cancellable = Open(Example(), false, gate); using var cancel = new CancellationTokenSource();
        var task = cancellable.CalculateAsync(cancel.Token); await Expect.Within(gate.Started.Task); cancel.Cancel(); await Expect.Within(task);
        e.That(cancellable.Current is null && !cancellable.IsBusy, "Annullamento ha pubblicato risultati.");
        await HydraulicsAsync(e, output);
        await HydraulicSafetyAsync(e, output);
        await GlobalAsync(e, output);
        await SteelAsync(e, output);
        await AnchorsAsync(e, output);
        return e.Count;
    }
    internal static JsonObject HydraulicExample()
    {
        var d = Example(); d["name"] = "Filtrazione — scavo a due fasi"; d["loads"] = new JsonArray();
        foreach (string side in new[] { "left_soils", "right_soils" })
        {
            var soil = EmbeddedWallDocument.NewRow(side); soil["name"] = "Sabbia sintetica"; soil["gamma"] = 18; soil["gamma_sat"] = 20;
            soil["phi"] = 30; soil["c"] = 0; soil["k0"] = .5; soil["kh"] = .001;
            soil["source"] = "Valori di collaudo"; soil["stiffness_source"] = "Parametro di collaudo"; d[side] = new JsonArray(soil);
        }
        var first = d["stages"]![0]!.AsObject(); foreach (var key in new[] { "left_ground", "right_ground", "left_water", "right_water" }) first[key] = 0;
        var second = first.DeepClone().AsObject(); second["id"] = "F2"; second["right_ground"] = -2; second["right_water"] = -2; d["stages"]!.AsArray().Add(second);
        d["hydraulic_mode"] = "FEM 2D"; d["hydraulic_left_extent"] = 8; d["hydraulic_right_extent"] = 8;
        d["hydraulic_bottom"] = -12; d["hydraulic_thickness"] = .6; d["hydraulic_mesh"] = 1;
        foreach (string side in new[] { "hydraulic_left", "hydraulic_right" })
        {
            var soil = EmbeddedWallDocument.NewRow(side); soil["kxx"] = 1e-5; soil["kzz"] = 1e-5; soil["source"] = "Permeabilità sintetica";
            d[side] = new JsonArray(soil);
        }
        return d;
    }
    private static async Task HydraulicsAsync(Expect e, string output)
    {
        var old = Example(); old["schema"] = 1;
        foreach (string key in old.Select(p => p.Key).Where(k => k.StartsWith("hydraulic_", StringComparison.Ordinal) || k == "ignore_suction").ToArray()) old.Remove(key);
        string oldJson = old.ToJsonString(); var migrated = EmbeddedWallDocument.Snapshot(old);
        e.That(old.ToJsonString() == oldJson && migrated["schema"]!.ToString() == "6" && migrated["hydraulic_mode"]!.ToString() == "Idrostatica"
            && migrated["hydraulic_left"]!.AsArray().Count == 0 && migrated["hydraulic_mesh"] is null, "Migrazione ha mutato l'originale o inventato dati idraulici.");
        using (var legacy = Open(old)) e.That(legacy.Data["schema"]!.ToString() == "6" && legacy.Tables.Count == 19, "Documento precedente non apribile nell'editor.");
        var data = HydraulicExample(); string original = data.ToJsonString(); using var sheet = Open(data);
        e.That(await Expect.Within(sheet.CalculateAsync()), "Calcolo idraulico asincrono non pubblicato.");
        var result = sheet.Current ?? throw new InvalidOperationException("Risultato idraulico assente.");
        e.That(result.Complete && result.Hydraulics?.Count == 2 && result.Stages.Count == 2, "Storia idraulica/meccanica incompleta: " + result.Diagnostic);
        var fields = result.Hydraulics!;
        e.That(data.ToJsonString() == original && fields.All(h => h.Complete && h.Nodes.Count > 0), "Idraulica ha mutato gli input o prodotto campi vuoti.");
        // Independent hydrostatic solution: h=0, u=-gamma_w*z; unit conversions m, kPa, L/s/m.
        e.That(fields[0].Nodes.All(n => Math.Abs(n.Head) < 1e-9 && Math.Abs(n.Pressure + 9.81 * n.Z) < 1e-8)
            && Math.Abs(fields[0].InflowLitresPerSecondPerMetre) < 1e-9, "Campo iniziale non idrostatico o unità errate.");
        e.That(fields[1].InflowLitresPerSecondPerMetre > 0 && Math.Abs(fields[1].BalanceLitresPerSecondPerMetre) < 1e-8
            && fields[1].Elements.All(x => x.RelativeConductivity == 1), "Filtrazione satura: portata, bilancio o permeabilità errati.");
        var noFem = data.DeepClone().AsObject(); noFem["hydraulic_mode"] = "Idrostatica";
        var hydrostatic = new EmbeddedWallModule().Calculate(noFem).NativeAs<EmbeddedWallReportData>();
        e.That(Math.Abs(hydrostatic.Stages[1].Points[0].Displacement - result.Stages[1].Points[0].Displacement) > 1e-3, "Campo idraulico non trasferito alla paratia.");
        var saved = JsonNode.Parse(sheet.Data.ToJsonString())!.AsObject(); using var reopened = Open(saved);
        e.That(reopened.HydraulicStages[1].Nodes.SequenceEqual(fields[1].Nodes) && reopened.HydraulicStages[1].Elements.SequenceEqual(fields[1].Elements)
            && reopened.SelectedHydraulicStage?.Stage == "F2", "Riapertura ha alterato il campo o la provenienza.");
        string csv = EmbeddedWallReport.HydraulicCsv(result);
        e.That(csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length == 1 + fields.Sum(h => h.Nodes.Count + h.Elements.Count)
            && csv.Contains(result.Fingerprint), "Esportazione idraulica incompleta.");
        string before = sheet.Data.ToJsonString(); var report = sheet.Report.BuildReport("Filtrazione di collaudo", new HashSet<string>());
        string dir = Path.Combine(output, "paratie"); ReportFiles.Write(Path.Combine(dir, "filtrazione.docx"), report, "Filtrazione");
        File.WriteAllText(Path.Combine(dir, "filtrazione.csv"), csv, new UTF8Encoding(true));
        File.WriteAllText(Path.Combine(dir, "filtrazione.json"), sheet.Data.ToJsonString(new() { WriteIndented = true }));
        e.That(sheet.Data.ToJsonString() == before && DocxWriter.Read(report).GetXml("word/document.xml").ToString().Contains("L/s/m"), "Report idraulico incoerente o con ricalcolo.");
        reopened.Tables.Single(t => t.Key == "hydraulic_left").Rows[0]["kxx"] = "0.00002";
        e.That(reopened.Current is null && reopened.HydraulicNodes.Count == 0 && !reopened.HydraulicCsvCommand.CanExecute(null), "Permeabilità modificata senza invalidare tutti i risultati dipendenti.");
        var failed = HydraulicExample(); failed["hydraulic_iterations"] = 1;
        using var failedSheet = Open(failed); await Expect.Within(failedSheet.CalculateAsync());
        e.That(failedSheet.Current is { Complete: false, Status: "HydraulicNonConvergent" } && failedSheet.Stages.Count == 0
            && failedSheet.HydraulicStages[0].Complete && !failedSheet.HydraulicStages[1].Complete && failedSheet.HydraulicStages[1].Nodes.Count == 0
            && !failedSheet.HasResults, "Mancata convergenza idraulica trasformata in risultati meccanici validi.");
        var service = new EmbeddedWallModule(); using var cancelled = new CancellationTokenSource();
        service.ProgressChanged += _ => cancelled.Cancel();
        e.Throws<OperationCanceledException>(() => service.Calculate(data, cancelled.Token), "Annullamento durante la filtrazione ignorato.");
        var invalid = HydraulicExample(); invalid["hydraulic_left"]![0]!["kxx"] = -1;
        e.Throws<ArgumentException>(() => service.Calculate(invalid), "Permeabilità negativa accettata.");
    }
    private static async Task HydraulicSafetyAsync(Expect e, string output)
    {
        var old = HydraulicExample(); old["schema"] = 2;
        foreach (string key in new[] { "hydraulic_weight_mode", "hydraulic_standard", "hydraulic_situation", "hydraulic_upl", "hydraulic_actions", "hydraulic_hyd" }) old.Remove(key);
        string oldJson = old.ToJsonString(); var upgraded = EmbeddedWallDocument.Snapshot(old);
        e.That(old.ToJsonString() == oldJson && upgraded["schema"]!.ToString() == "6" && upgraded["hydraulic_upl"]!.AsArray().Count == 0
            && upgraded["hydraulic_weight_mode"]!.ToString() == "Quota di falda assegnata", "Migrazione 2→3 ha introdotto verifiche o mutato i pesi del terreno.");
        var input = SafetyExample(); using var sheet = Open(input);
        e.That(await Expect.Within(sheet.CalculateAsync()) && sheet.Current?.Complete == true, "Analisi con verifiche idrauliche non completata.");
        var checks = sheet.HydraulicChecks;
        // Independent characteristic balance: 1.1*15+1.5*1 = 18 kN; .9*20+.8*3+3 = 23.4 kN.
        var upl = checks.Single(c => c.Method == "UPL");
        e.That(Math.Abs(upl.Demand!.Value - 18) < 1e-10 && Math.Abs(upl.Capacity!.Value - 23.4) < 1e-10 && upl.Unit == "kN", "UPL: fattori o unità alterati dall'adattatore.");
        e.That(upl.Actions.Count == 5 && upl.Actions.Last().Factor == 0 && upl.Coefficients["gammaG1_unfavourable"] == 1.1
            && upl.Outcome == GPC.Model.Checking.Contracts.EngineeringOutcome.Satisfied, "UPL: azioni, provenienza o esito mancanti.");
        var mean = checks.Single(c => c.Method == "HYD-Mean"); var exit = checks.Single(c => c.Method == "HYD-Exit"); var loaded = checks.Single(c => c.Method == "HYD-loaded");
        e.That(Math.Abs(mean.Ratio!.Value - 1.2) < 1e-12 && Math.Abs(exit.Ratio!.Value - .8) < 1e-12 && mean.Outcome != exit.Outcome,
            "HYD: gradiente medio e di uscita non distinti.");
        e.That(Math.Abs(loaded.Demand!.Value - 20) < 1e-10 && Math.Abs(loaded.Capacity!.Value - 15) < 1e-10 && loaded.Unit == "kPa", "HYD caricato: sovrappressioni o unità errate.");
        e.That(checks.All(c => c.CaseId == input["case_id"]!.ToString() && c.Stage == "F1" && c.Reference.Contains("6.2.4.2")), "Provenienza normativa, combinazione o fase perduta.");
        var saved = JsonNode.Parse(sheet.Data.ToJsonString())!.AsObject(); using var reopened = Open(saved);
        e.That(System.Text.Json.JsonSerializer.Serialize(reopened.HydraulicChecks) == System.Text.Json.JsonSerializer.Serialize(checks), "Verifiche alterate nel salvataggio/riapertura.");
        string beforeReport = sheet.Data.ToJsonString(); var report = sheet.Report.BuildReport("UPL e HYD — collaudo con attesi manuali", new HashSet<string>());
        string xml = DocxWriter.Read(report).GetXml("word/document.xml").ToString();
        e.That(xml.Contains("23,4") && xml.Contains("HYD-Mean") && xml.Contains("23.4") == false && sheet.Data.ToJsonString() == beforeReport,
            "Report idraulico diverso dal risultato corrente o ricalcolo nascosto.");
        string dir = Path.Combine(output, "paratie"); ReportFiles.Write(Path.Combine(dir, "upl-hyd.docx"), report, "Verifiche idrauliche");
        string csv = EmbeddedWallReport.HydraulicChecksCsv(sheet.Current!); File.WriteAllText(Path.Combine(dir, "upl-hyd.csv"), csv, new UTF8Encoding(true));
        e.That(csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length == 5 && csv.Contains(sheet.Current!.Fingerprint), "CSV verifiche incompleto.");
        reopened.Tables.Single(t => t.Key == "hydraulic_actions").Rows[0]["upward_force"] = "16";
        e.That(reopened.Current is null && reopened.HydraulicChecks.Count == 0 && !reopened.HydraulicChecksCsvCommand.CanExecute(null), "Verifiche UPL superate ancora disponibili.");
        var service = new EmbeddedWallModule();
        foreach (bool seismic in new[] { true, false })
        {
            var unsupported = SafetyExample(); if (seismic) unsupported["hydraulic_situation"] = "Seismic"; else unsupported["hydraulic_standard"] = "EN 1997-1 — profilo non disponibile";
            var result = service.Calculate(unsupported).NativeAs<EmbeddedWallReportData>();
            e.That(result.HydraulicChecks!.All(c => c.Outcome == GPC.Model.Checking.Contracts.EngineeringOutcome.NotEvaluated && c.Capacity is null), "Profilo non disponibile dichiarato soddisfatto.");
        }
        var missing = SafetyExample(); missing["hydraulic_hyd"]![0]!["gamma_sat"] = null; missing["hydraulic_hyd"]![1]!["vertical_flow"] = false;
        var unavailable = service.Calculate(missing).NativeAs<EmbeddedWallReportData>();
        e.That(unavailable.HydraulicChecks!.Count(c => c.Outcome == GPC.Model.Checking.Contracts.EngineeringOutcome.NotEvaluated) == 2, "Dati mancanti o applicabilità non dichiarata ignorati.");
        var invalid = SafetyExample(); invalid["hydraulic_actions"]![0]!["stage"] = "inesistente";
        e.Throws<ArgumentException>(() => service.Calculate(invalid), "Azione di una fase inesistente ignorata.");
        var fem = HydraulicExample(); fem["hydraulic_weight_mode"] = "Pressioni FEM";
        var body = EmbeddedWallDocument.NewRow("hydraulic_upl"); body["water_resultant"] = "FEM 2D"; body["x_start"] = 1; body["x_end"] = 3;
        body["z"] = -4; body["body_width"] = 1; body["top_pressure"] = 10; body["source"] = "Corpo sintetico per integrale idrostatico; acqua G1"; fem["hydraulic_upl"]!.AsArray().Add(body);
        var weight = EmbeddedWallDocument.NewRow("hydraulic_actions"); weight["upward_force"] = -100; weight["source"] = "Peso totale sintetico"; fem["hydraulic_actions"]!.AsArray().Add(weight);
        var point = EmbeddedWallDocument.NewRow("hydraulic_hyd"); point["method"] = "Mean"; point["x"] = 4; point["z"] = 0; point["z_bottom"] = -4; point["gamma_sat"] = 20;
        point["vertical_flow"] = true; point["outflow_confirmed"] = true; point["source"] = "Colonna satura iniziale in quiete, frontiera libera"; fem["hydraulic_hyd"]!.AsArray().Add(point);
        var coupled = service.Calculate(fem).NativeAs<EmbeddedWallReportData>();
        e.That(coupled.Complete && Math.Abs(coupled.HydraulicChecks![0].Demand!.Value - 64.328) < 1e-8 && Math.Abs(coupled.HydraulicChecks[0].Capacity!.Value - 90) < 1e-8,
            "Integrale FEM UPL: atteso (9.81*4-10)*2*1*1.1 = 64.328 kN, capacità 90 kN.");
        e.That(Math.Abs(coupled.HydraulicChecks![1].Demand!.Value) < 1e-10 && coupled.Scope.Any(s => s.Contains("γsat dove")), "Gradiente idrostatico o criterio dei pesi incoerente.");
        point["x"] = 100;
        var outside = service.Calculate(fem).NativeAs<EmbeddedWallReportData>();
        e.That(outside.HydraulicChecks![1].Outcome == GPC.Model.Checking.Contracts.EngineeringOutcome.NotEvaluated, "Campionamento fuori dominio accettato.");
        point["x"] = 4; point["stage"] = "F2"; fem["hydraulic_iterations"] = 1;
        var partial = service.Calculate(fem).NativeAs<EmbeddedWallReportData>();
        e.That(!partial.Complete && partial.HydraulicChecks!.Single(c => c.Stage == "F2").Outcome == GPC.Model.Checking.Contracts.EngineeringOutcome.NotEvaluated,
            "Campo non convergente trasformato in verifica soddisfatta.");
    }
    internal static JsonObject SafetyExample()
    {
        var d = Example(); var upl = EmbeddedWallDocument.NewRow("hydraulic_upl"); upl["rd"] = 3; upl["rd_source"] = "Rd sintetica già con M2";
        d["hydraulic_upl"]!.AsArray().Add(upl);
        foreach (var item in new[] { ("Acqua", "PermanentStructural", 15d), ("Peso", "PermanentStructural", -20d),
            ("G2", "PermanentNonStructural", -3d), ("Qup", "Variable", 1d), ("Qdown", "Variable", -100d) })
        {
            var a = EmbeddedWallDocument.NewRow("hydraulic_actions"); a["id"] = item.Item1; a["category"] = item.Item2; a["upward_force"] = item.Item3; a["source"] = "Azione caratteristica sintetica";
            d["hydraulic_actions"]!.AsArray().Add(a);
        }
        foreach (string method in new[] { "Mean", "Exit", "Loaded" })
        {
            var p = EmbeddedWallDocument.NewRow("hydraulic_hyd"); p["id"] = method; p["method"] = method; p["field_source"] = "Assegnato";
            p["gradient"] = .4; p["gamma_sat"] = 19.62; p["pressure"] = 30; p["hydrostatic_pressure"] = 10; p["effective_stress"] = 30;
            p["vertical_flow"] = true; p["outflow_confirmed"] = true; p["source"] = "Colonna omogenea satura e verticale; frontiera libera per Mean/Exit, caricata per Loaded. Esempio sintetico.";
            d["hydraulic_hyd"]!.AsArray().Add(p);
        }
        return d;
    }
    private static EmbeddedWallSheetViewModel Open(JsonObject data, bool readOnly = false, IModuleService? module = null)
        => new(new(data.DeepClone().AsObject(), readOnly, new Host(), Scripted.Services(), module ?? ModuleServices.Get(EmbeddedWallDocument.Module), new CalculationLimiter(1)));
    private sealed class Host : ISheetHost
    {
        public void OpenModuleCopy(string module, JsonObject data, string name) => throw new NotSupportedException();
        public void ReplaceSoil(JsonObject data) => throw new NotSupportedException();
        public IReadOnlyList<SoilSource> SoilSources() => [];
        public void RequestReport() => throw new NotSupportedException();
    }
    private sealed class NoFigures : IFigureRenderer
    { public Task<IReadOnlyList<ReportFigure>> RenderAsync(ModuleReportRequest request, CancellationToken cancellation) => throw new InvalidOperationException("Il report paratie deve usare il risultato salvato senza ricalcolo o renderer esterno."); }
    private sealed class HeldModule : IModuleService, IDisposable
    {
        private readonly ManualResetEventSlim gate = new(false);
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string ModuleId => EmbeddedWallDocument.Module;
        public JsonObject Prepare(JsonObject data) => EmbeddedWallDocument.Snapshot(data);
        public ModuleCalculation Calculate(JsonObject source, CancellationToken cancellation = default)
        { Started.TrySetResult(); gate.Wait(cancellation); return new EmbeddedWallModule().Calculate(source, cancellation); }
        public void Dispose() { gate.Set(); gate.Dispose(); }
    }
}
