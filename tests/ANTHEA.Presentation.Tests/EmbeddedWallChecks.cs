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

internal static class EmbeddedWallChecks
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
        e.That(old.ToJsonString() == oldJson && migrated["schema"]!.ToString() == "2" && migrated["hydraulic_mode"]!.ToString() == "Idrostatica"
            && migrated["hydraulic_left"]!.AsArray().Count == 0 && migrated["hydraulic_mesh"] is null, "Migrazione ha mutato l'originale o inventato dati idraulici.");
        using (var legacy = Open(old)) e.That(legacy.Data["schema"]!.ToString() == "2" && legacy.Tables.Count == 10, "Documento precedente non apribile nell'editor.");
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
