using System.Text.Json.Nodes;
using Anthea.Application.Actions;
using Anthea.Application.Documents;
using Anthea.Application.Modules;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.Spectra;
using Anthea.Reports;
using Anthea.Reports.Pdf;

namespace Anthea.Presentation.Tests;

internal static class SpectraSheetChecks
{
    internal static int Run(string root, string output) => SingleThreadContext.Run(async _ =>
    {
        var expect = new Expect(); SpectraEdgeChecks.Run(expect, root); var data = SpectraDocument.Create(); var services = Scripted.Services();
        using var sheet = new SpectraSheetViewModel(new(data, false, new Host(), services, ModuleServices.Get(SpectraDocument.Module), new CalculationLimiter(1)));
        expect.That(ModuleDirectory.Get(SpectraDocument.Module).Area == "Azioni", "Spettri nel sottogruppo Azioni");
        expect.That(await sheet.CalculateAsync() && sheet.HasResults, "Spettri: pubblicazione risultato");
        expect.That(sheet.Curves.Count == 3 && sheet.Summary.Count == 4, "Spettri: componenti e quattro stati limite");
        sheet.Stage = 0; expect.That(sheet.Curves.Count == 10 && sheet.Nodes.Count == 4 && sheet.Summary.Count == 9, "Fase 1: nove curve, tabulato e reticolo");
        sheet.Stage = 1; expect.That(sheet.Curves.Count == 4, "Fase 2: quattro spettri");
        sheet.SiteForm["longitude"].EditText = "7,768"; sheet.SiteForm["latitude"].EditText = "45,368";
        expect.That(!sheet.HasResults && sheet.Location == "Coordinate assegnate", "Modifica coordinate invalida i risultati");
        expect.That(await sheet.CalculateAsync(), "Calcolo con virgola decimale");
        var ag = sheet.Result!["states"]![2]!["Parameters"]!["AgG"]!.GetValue<double>(); expect.That(Math.Abs(ag - .04562261909287685) < 1e-12, "Caso indipendente Agliè SLV");
        var document = DocumentService.CreateDocument(SpectraDocument.Module); document["dati"] = sheet.Data.DeepClone();
        string folder = Path.Combine(output, "spettri"); Directory.CreateDirectory(folder); string file = Path.Combine(folder, "spettri.anthea"); DocumentService.Save(file, document); var opened = DocumentService.Open(file);
        expect.That(JsonNode.DeepEquals(opened["dati"], sheet.Data), "Salvataggio e riapertura Spettri");
        sheet.IsReadOnly = true; var before = sheet.Data.DeepClone(); sheet.SiteForm["latitude"].EditText = "43"; sheet.Town = SpectraDocument.Towns.First(); expect.That(JsonNode.DeepEquals(before, sheet.Data), "Revisione in sola lettura"); sheet.IsReadOnly = false;
        sheet.SiteForm["latitude"].EditText = "abc"; await sheet.CalculateAsync(); expect.That(!sheet.HasResults && sheet.CalculationMessage.Length > 0, "Input non valido non conserva risultati"); sheet.Reload(data = SpectraDocument.Create());
        // The user workbook is optional local evidence; the core numerical fixture is always exercised by Design.Checks.
        string example = Path.Combine(root, "supporto/artefatti/spettri-ntc/fonti/isolamento-ntc18.xlsx");
        if (File.Exists(example))
        {
            var imported = IsolationExampleImport.Read(example, data); sheet.Reload(imported);
            expect.That(sheet.Catalog.Count > 100, "Importazione catalogo senza eseguire formule");
            expect.That(await sheet.CalculateAsync(), "Calcolo isolamento dall’esempio Excel"); sheet.Stage = 3;
            expect.That(sheet.Summary.Count == 4 && sheet.Curves[0].Points.Count >= 400, "Isolamento: stati e soglia nel grafico");
            double t = sheet.Result!["isolation"]![3]!["PeriodSeconds"]!.GetValue<double>(); expect.That(Math.Abs(t - 1.419838014727065) < 1e-12, "Periodo dell’esempio");
            double sampled = sheet.Result!["isolation"]![3]!["ExampleSampleAccelerationG"]!.GetValue<double>(); expect.That(Math.Abs(sampled - .17628437253944718) < 1e-12, "Accelerazione campionata dell’esempio");
            var source = sheet.Data["import_source"]!.DeepClone(); sheet.PendulumForm["friction"].EditText = "0,055"; await sheet.CalculateAsync(); sheet.ApplyPendulumCommand.Execute(null);
            expect.That(sheet.Data["group1_name"]!.ToString() == "Pendolo" && !sheet.HasResults && JsonNode.DeepEquals(source, sheet.Data["import_source"]), "Trasferimento esplicito pendolo e provenienza");
            sheet.Reload(imported);
        }
        var valid = (JsonObject)sheet.Data.DeepClone(); var aboveLimit = (JsonObject)valid.DeepClone(); aboveLimit["isolation"] = true; aboveLimit["mass"] = 1e9;
        sheet.Reload(aboveLimit); await sheet.CalculateAsync();
        expect.That(!sheet.HasResults && sheet.CalculationMessage.Contains("4 s"), "Tis oltre il campo convenzionale rifiuta il risultato e ne espone il motivo");
        sheet.Reload(valid);
        expect.That(await sheet.CalculateAsync(), "Risultato finale disponibile");
        string csv = SpectraDocument.Csv(sheet.Result!); expect.That(csv.Contains("Verticale") && csv.Contains("Tabulato") && !csv.Contains("NaN"), "CSV con tutte le fasi"); File.WriteAllText(Path.Combine(folder, "spettri.csv"), csv);
        var report = SpectraReport.Create("Spettri e isolamento", sheet.Data, sheet.Result!); File.WriteAllBytes(Path.Combine(folder, "spettri.docx"), DocxWriter.Write(report)); var pdf = DocxPdf.Render(report, "Spettri e isolamento"); File.WriteAllBytes(Path.Combine(folder, "spettri.pdf"), pdf.Bytes); expect.That(pdf.Pages >= 4, "Report DOCX e PDF completo");
        expect.That(Anthea.Application.Reporting.ModuleReportService.Supports(SpectraDocument.Module), "Report di progetto disponibile");
        return expect.Count;
    });
    private sealed class Host : ISheetHost
    {
        public void OpenModuleCopy(string module, JsonObject data, string name) => throw new InvalidOperationException();
        public void ReplaceSoil(JsonObject data) => throw new InvalidOperationException();
        public IReadOnlyList<SoilSource> SoilSources() => [];
        public void RequestReport() => throw new InvalidOperationException();
    }
}
