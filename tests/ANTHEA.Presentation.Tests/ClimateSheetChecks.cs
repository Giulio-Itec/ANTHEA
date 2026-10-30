using System.Text.Json.Nodes;
using Anthea.Application.Actions;
using Anthea.Application.Documents;
using Anthea.Application.Modules;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.Climate;
using Anthea.Reports;
using Anthea.Reports.Pdf;
using Anthea.Testing.Climate;

namespace Anthea.Presentation.Tests;

internal static class ClimateSheetChecks
{
    internal static int Run(string root,string output)=>SingleThreadContext.Run(async _=>
    {
        var expect=new Expect();Directory.CreateDirectory(output);
        foreach(var scenario in ClimateScenarios.All())
        {
            var data=scenario.Data;var services=Scripted.Services();
            using var sheet=new ClimateSheetViewModel(scenario.Module,new(data,false,new Host(),services,ModuleServices.Get(scenario.Module),new CalculationLimiter(1)));
            expect.That(ModuleDirectory.Get(scenario.Module).Area=="Azioni","Sottogruppo Azioni: "+scenario.Module);
            expect.That(await sheet.CalculateAsync()&&sheet.HasResults,"Calcolo "+scenario.Title+": "+sheet.CalculationMessage);
            expect.That(sheet.Cases.Count>0&&sheet.Parameters.Count>0&&sheet.CaseValues.Count>0,"Casi e parametri disponibili");
            var before=sheet.Data.DeepClone();foreach(var panel in sheet.Forms)panel.Form.Commit();expect.That(JsonNode.DeepEquals(before,sheet.Data),"La proiezione italiana non modifica il contratto GPC");
            sheet.IsReadOnly=true;var field=sheet.Forms.SelectMany(f=>f.Form.Fields).FirstOrDefault(f=>f.Kind==Anthea.Presentation.Forms.FieldKind.Text&&f.Spec.Write==Anthea.Presentation.Forms.FieldWriteRule.NumberOrText);
            expect.That(field is not null,"Campo numerico disponibile");field!.EditText="999";expect.That(JsonNode.DeepEquals(before,sheet.Data),"Sola lettura");sheet.IsReadOnly=false;
            var form=sheet.Forms.Last().Form;string key=scenario.Module is ClimateDocument.Snow or ClimateDocument.BuildingWind or ClimateDocument.BridgeWind?"WidthM":"LengthM";
            form[key].EditText="12,5";expect.That(!sheet.HasResults&&sheet.Data["input"]![key]!.GetValue<double>()==12.5,"Modifica decimale e invalidazione");
            expect.That(await sheet.CalculateAsync(),"Ricalcolo dopo modifica");sheet.Reload(before.AsObject());expect.That(await sheet.CalculateAsync(),"Ripristino dei dati e ricalcolo");
            sheet.Forms.Last().Form[key].EditText="abc";await sheet.CalculateAsync();expect.That(!sheet.HasResults&&sheet.CalculationMessage.Length>0,"Errore di input non conserva risultati");sheet.Reload(before.AsObject());expect.That(await sheet.CalculateAsync()&&sheet.HasResults,"Recupero da errore");
            string folder=Path.Combine(output,scenario.Module);Directory.CreateDirectory(folder);string path=Path.Combine(folder,scenario.File+".anthea");
            var document=DocumentService.CreateDocument(scenario.Module);document["dati"]=sheet.Data.DeepClone();DocumentService.Save(path,document);var reopened=DocumentService.Open(path);
            expect.That(JsonNode.DeepEquals(reopened["dati"],sheet.Data),"Salvataggio e apertura del contratto GPC");
            var recalculated=ModuleServices.Get(scenario.Module).Calculate(reopened["dati"]!.AsObject());expect.That(JsonNode.DeepEquals(recalculated.Values,sheet.Result),"Equivalenza dopo riapertura");
            string csv=ClimateDocument.Csv(sheet.Result!);expect.That(csv.Contains("Formula")&&!csv.Contains("NaN"),"CSV tracciabile");File.WriteAllText(Path.ChangeExtension(path,"csv"),csv);
            foreach(var unit in sheet.ChartUnits){sheet.ChartUnit=unit;expect.That(sheet.ChartValues.Count>0&&sheet.ChartValues.All(v=>v.Unit==unit),"Diagrammi con unità omogenee");}
            var report=ClimateReport.Create(scenario.Title,sheet.Data,sheet.Result!);expect.That(report.Contains("word/media/climate1.png"),"Relazione con schema incorporato");File.WriteAllBytes(Path.ChangeExtension(path,"docx"),DocxWriter.Write(report));var pdf=DocxPdf.Render(report,scenario.Title);File.WriteAllBytes(Path.ChangeExtension(path,"pdf"),pdf.Bytes);expect.That(pdf.Pages>0,"PDF non vuoto");
            expect.That(Anthea.Application.Reporting.ModuleReportService.Supports(scenario.Module),"Capitolo nella relazione di progetto");
        }
        var snow=ClimateDocument.Create(ClimateDocument.Snow);
        using(var sheet=new ClimateSheetViewModel(ClimateDocument.Snow,new(snow,false,new Host(),Scripted.Services(),ModuleServices.Get(ClimateDocument.Snow),new CalculationLimiter(1))))
        {
            sheet.Forms[0].Form["Roof"].SelectedChoice="Copertura inferiore presso salto";
            expect.That(sheet.Data["input"]!["Roof"]!.ToString()=="LowerRoof"&&sheet.Forms[0].Form.Fields.Any(f=>f.Key=="StepHeightM"),"Scelta italiana, contratto enum stabile e campi condizionali");
        }
        return expect.Count;
    });
    private sealed class Host:ISheetHost
    {
        public void OpenModuleCopy(string module,JsonObject data,string name)=>throw new InvalidOperationException();
        public void ReplaceSoil(JsonObject data)=>throw new InvalidOperationException();
        public IReadOnlyList<SoilSource> SoilSources()=>[];
        public void RequestReport()=>throw new InvalidOperationException();
    }
}
