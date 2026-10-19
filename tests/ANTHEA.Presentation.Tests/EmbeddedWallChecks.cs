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
        return e.Count;
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
