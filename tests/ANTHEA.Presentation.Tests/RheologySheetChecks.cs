using System.Text.Json.Nodes;
using Anthea.Application.Documents;
using Anthea.Application.Materials;
using Anthea.Application.Modules;
using Anthea.Application.Reporting;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.Rheology;
using Anthea.Reports;
using Anthea.Reports.Pdf;

namespace Anthea.Presentation.Tests;

internal static class RheologySheetChecks
{
    internal static int Run(string root, string output) => SingleThreadContext.Run(async _ =>
    {
        var expect = new Expect();
        string folder = Path.Combine(output, "reologia"); Directory.CreateDirectory(folder);
        RheologySheetViewModel Open(JsonObject data, bool readOnly = false) => new(new(data, readOnly, new Host(), Scripted.Services(), ModuleServices.Get(RheologyDocument.Module), new CalculationLimiter(1)));
        using var sheet = Open(RheologyDocument.Create());
        var module = new RheologySheetModule();
        expect.That(module.Info.Area == "Materiali" && module.Info.Name == "Viscosità e maturazione", "Reologia nel gruppo Materiali");
        expect.That(module.Help.GuideId?.Contains("reologia") == true, "Guida contestuale Reologia");
        expect.That(sheet.HasResults && sheet.Model.Points.Count > 160 && sheet.Revision == 0, "Apertura calcolata senza modificare il foglio");
        int modified = 0; sheet.Modified += (_, _) => modified++;
        await sheet.CalculateAsync();
        expect.That(modified == 0 && sheet.Revision == 0, "Ricalcolo non modifica gli ingressi");
        sheet.Model.Fck = "25"; sheet.Model.Humidity = "50"; sheet.Model.NotionalSize = "200"; sheet.Model.LoadingAge = "7";
        expect.That(!sheet.HasResults && sheet.Result is null && modified == 4, "Modifiche salvate una volta e risultati superati rimossi");
        expect.That(await sheet.CalculateAsync() && modified == 4, "Calcolo del foglio senza modifiche spurie");
        expect.That(Math.Abs(sheet.Model.CreepInfinity!.Value - 3.4427042104754673) < 1e-12, "Riferimento Python indipendente conservato");
        // Review of 2/11/2026, finding 14: after an edit the curves come back by themselves after the delay, without «Aggiorna le curve».
        sheet.Model.Humidity = "60";
        expect.That(!sheet.HasResults && sheet.Model.IsUpdating && sheet.Model.PlotMessage == "Aggiornamento delle curve…", "Modifica: curve in aggiornamento");
        await sheet.Model.PendingCalculation;
        expect.That(sheet.HasResults && !sheet.Model.IsUpdating && sheet.Result is not null, "Ricalcolo automatico dopo l'attesa, senza «Aggiorna le curve»");
        sheet.Model.Humidity = "50"; await sheet.Model.PendingCalculation;
        expect.That(Math.Abs(sheet.Model.CreepInfinity!.Value - 3.4427042104754673) < 1e-12, "Ricalcolo automatico: stesso riferimento Python");
        sheet.Model.Cement = sheet.Model.Cements[2]; sheet.Model.Samples = "80"; sheet.Model.QueryAge = "28";
        // Finding 12: the options of the plot are consultation: no edit, no new revision, the data and the document stay as they are.
        int edits = modified; long revision = sheet.Revision; var beforeOptions = (JsonObject)sheet.Data.DeepClone();
        sheet.Model.SelectedCurve = sheet.Model.Curves[2]; sheet.Model.LogX = false; sheet.Model.LogY = true;
        expect.That(modified == edits && sheet.Revision == revision && JsonNode.DeepEquals(sheet.Data, beforeOptions), "Opzioni del grafico: consultazione senza modifiche del foglio");
        await sheet.CalculateAsync(); sheet.Commit();
        expect.That(JsonNode.DeepEquals(sheet.Data, beforeOptions) && modified == edits, "Conferma: le opzioni del grafico non entrano nel documento");
        var saved = (JsonObject)sheet.Data.DeepClone();
        var document = DocumentService.CreateDocument(RheologyDocument.Module); document["dati"] = saved.DeepClone();
        string file = Path.Combine(folder, "reologia.anthea"); DocumentService.Save(file, document);
        using var reopened = Open(DocumentService.Open(file)["dati"]!.AsObject());
        expect.That(JsonNode.DeepEquals(reopened.Data, saved) && JsonNode.DeepEquals(reopened.Result, sheet.Result), "Salvataggio e riapertura esatti");
        expect.That(reopened.Model.Cement.Value == "ClassR" && reopened.Model.QueryAge == "28" && reopened.Model.Samples == "80" && reopened.Model.LogX && !reopened.Model.LogY && reopened.Model.SelectedCurve.Value == ConcreteTimeCurve.Creep,
            "Dati e lettura ripristinati, opzioni del grafico del documento");
        var stored = RheologyDocument.Create(); stored["SelectedCurve"] = "ModulusFactor"; stored["LogX"] = false; stored["LogY"] = true;
        using (var options = Open(stored))
            expect.That(!options.Model.LogX && options.Model.LogY && options.Model.SelectedCurve.Value == ConcreteTimeCurve.ModulusFactor, "Opzioni del grafico lette dal documento all'apertura");
        sheet.IsReadOnly = true;
        expect.That(!sheet.Model.CalculateCommand.CanExecute(null), "«Aggiorna le curve» disattivato in sola lettura");
        sheet.Model.Fck = "50"; sheet.Model.Cement = sheet.Model.Cements[0]; sheet.Model.Samples = "120";
        expect.That(JsonNode.DeepEquals(sheet.Data, saved) && sheet.Model.Fck == "25" && sheet.Model.Samples == "80" && !sheet.Model.CanEdit, "Revisione sola lettura protegge tutti gli ingressi");
        sheet.Model.QueryAge = "365"; sheet.Model.LogX = true;
        expect.That(sheet.Model.QueryPoint?.AgeDays == 365 && JsonNode.DeepEquals(sheet.Data, saved) && JsonNode.DeepEquals(sheet.PreviewData, saved), "Consultazione in sola lettura non altera i dati archiviati");
        sheet.Commit(); expect.That(JsonNode.DeepEquals(sheet.Data, saved), "Commit in sola lettura conserva la revisione");
        using var readOnly = Open((JsonObject)saved.DeepClone(), true);
        expect.That(!readOnly.Model.CanEdit && readOnly.Model.Fck == "25" && await readOnly.CalculateAsync(), "Apertura diretta di una revisione in sola lettura");
        sheet.IsReadOnly = false; sheet.Model.Fck = "abc";
        expect.That(!await sheet.CalculateAsync() && !sheet.HasResults && sheet.Result is null, "Input non valido non pubblica risultati");
        sheet.Commit(); expect.That(sheet.Data["Fck"]!.ToString() == "abc", "Bozza non numerica salvabile senza valori sostitutivi");
        sheet.Reload(saved);
        expect.That(sheet.HasResults && JsonNode.DeepEquals(sheet.Data, saved), "Ricarica ripristina i dati e le curve");
        // Finding 12: a row of the table sets the reading age with the digits of the table, not the 17 of the double.
        var odd = sheet.Model.Points.First(p => p.AgeDays != Math.Round(p.AgeDays, 3));
        sheet.Model.SelectedPoint = odd;
        expect.That(sheet.Model.QueryAge == odd.AgeText && sheet.Model.QueryPoint is not null, "Età letta dalla tabella con le cifre della tabella: " + sheet.Model.QueryAge);
        // Finding 14: «Esporta grafico…» through the file service; a cancelled dialog writes nothing.
        string png = Path.Combine(folder, "grafico.png"); if (File.Exists(png)) File.Delete(png);
        var files = new ScriptedFileDialogs().Then(null).Then(png);
        using (var exporting = new RheologySheetViewModel(new(RheologyDocument.Create(), false, new Host(), Scripted.Services(files: files), ModuleServices.Get(RheologyDocument.Module), new CalculationLimiter(1))))
        {
            exporting.Model.ExportPlot(() => [1]); bool cancelled = !File.Exists(png);
            exporting.Model.ExportPlot(() => [137, 80, 78, 71]);
            expect.That(cancelled && files.Requests.Count == 2 && File.ReadAllBytes(png).SequenceEqual(new byte[] { 137, 80, 78, 71 }), "Esportazione del grafico attraverso il servizio dei file");
        }
        sheet.Model.QueryAge = "non valida";
        expect.That(sheet.HasResults && sheet.Model.QueryPoint is null && sheet.Result!["QueryPoint"] is null, "Lettura non valida conserva le curve e cancella il punto");
        sheet.Model.QueryAge = "28";
        var report = RheologyReport.Create("Viscosità e maturazione", sheet.Data, sheet.Result!);
        File.WriteAllBytes(Path.Combine(folder, "reologia.docx"), sheet.Report.BuildReport("Viscosità e maturazione", new HashSet<string>()));
        var pdf = DocxPdf.Render(report, "Viscosità e maturazione"); File.WriteAllBytes(Path.Combine(folder, "reologia.pdf"), pdf.Bytes);
        expect.That(pdf.Pages is >= 1 and <= 5 && pdf.Bytes.Length > 1000, "Relazione DOCX e PDF");
        expect.That(ModuleReportService.Supports(RheologyDocument.Module), "Capitolo nella relazione di progetto");
        var built = await new ModuleReportService(new NoFigures()).BuildAsync(document, CancellationToken.None);
        expect.That(built.Document is not null, "Relazione di progetto autonoma dalla vista");
        var malformed = RheologyDocument.Create(); malformed["rheology_schema"] = 2;
        expect.Throws<ArgumentException>(() => RheologyDocument.Validate(malformed), "Schema futuro rifiutato");
        malformed = RheologyDocument.Create(); malformed["Cement"] = "NonEsistente";
        expect.Throws<ArgumentException>(() => RheologyDocument.Validate(malformed), "Cemento inesistente rifiutato");
        string examples = Path.Combine(folder, "esempi"); Directory.CreateDirectory(examples);
        foreach (string name in new[] { "c25-umidita-50", "c35-cemento-rapido" })
        {
            var example = DocumentService.Open(Path.Combine(root, "supporto/esempi/distribuzione/mat_reologia", name + ".anthea"));
            var exampleData = example["dati"]!.AsObject();
            var exampleResult = ModuleServices.Get(RheologyDocument.Module).Calculate(exampleData);
            var exampleReport = RheologyReport.Create("Esempio viscosità e maturazione · " + name, exampleData, exampleResult.Values);
            File.WriteAllBytes(Path.Combine(examples, name + ".docx"), DocxWriter.Write(exampleReport));
            File.WriteAllBytes(Path.Combine(examples, name + ".pdf"), DocxPdf.Render(exampleReport, "Viscosità e maturazione").Bytes);
            expect.That(!exampleResult.HasError, "Esempio originale calcolabile: " + name);
        }
        return expect.Count;
    });
    private sealed class NoFigures : IFigureRenderer
    {
        public Task<IReadOnlyList<ReportFigure>> RenderAsync(ModuleReportRequest request, CancellationToken cancellation) => throw new InvalidOperationException("Il capitolo Reologia non richiede una vista.");
    }
    private sealed class Host : ISheetHost
    {
        public void OpenModuleCopy(string module, JsonObject data, string name) => throw new InvalidOperationException();
        public void ReplaceSoil(JsonObject data) => throw new InvalidOperationException();
        public IReadOnlyList<SoilSource> SoilSources() => [];
        public void RequestReport() => throw new InvalidOperationException();
    }
}
