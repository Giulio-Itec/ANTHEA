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
            sheet.IsReadOnly=true;var field=sheet.Forms.SelectMany(f=>f.Form.Fields).Concat(sheet.Tables.Where(t=>t.Editor is not null).SelectMany(t=>t.Editor!.Fields)).FirstOrDefault(f=>f.Kind==Anthea.Presentation.Forms.FieldKind.Text&&f.Spec.Write==Anthea.Presentation.Forms.FieldWriteRule.NumberOrText);
            expect.That(field is not null,"Campo numerico disponibile");field!.EditText="999";expect.That(JsonNode.DeepEquals(before,sheet.Data),"Sola lettura");sheet.IsReadOnly=false;
            var form=scenario.Module==ClimateDocument.Construction?sheet.Tables.Single(t=>t.Key=="ConstructionLoads").Editor!:scenario.Module==ClimateDocument.Combinations?sheet.Tables.Single().Editor!:sheet.Forms.Last().Form;
            string key=scenario.Module switch{ClimateDocument.Snow or ClimateDocument.BuildingWind or ClimateDocument.BridgeWind=>"WidthM",ClimateDocument.Hydraulics or ClimateDocument.Construction=>"WidthM",ClimateDocument.PermanentLoads=>"TributaryWidthM",ClimateDocument.Combinations=>"Effect",ClimateDocument.RoadTraffic=>"LoadedLengthM",ClimateDocument.Impact=>scenario.Data["input"]!["Target"]!.ToString()=="Superstructure"?"ClearanceM":"MemberWidthM",ClimateDocument.Cranes=>"RailHeadWidthMm",ClimateDocument.Distribution=>"SpanM",_=>"LengthM"};
            double edited=scenario.Module==ClimateDocument.Distribution?30:12.5;
            form[key].EditText=edited.ToString(System.Globalization.CultureInfo.GetCultureInfo("it-IT"));
            var changed=scenario.Module==ClimateDocument.Construction?sheet.Data["input"]!["ConstructionLoads"]![0]:scenario.Module==ClimateDocument.Combinations?sheet.Data["input"]!["Actions"]![0]:sheet.Data["input"];
            expect.That(!sheet.HasResults&&changed![key]!.GetValue<double>()==edited,"Modifica decimale e invalidazione");
            expect.That(await sheet.CalculateAsync(),"Ricalcolo dopo modifica");sheet.Reload(before.AsObject());expect.That(await sheet.CalculateAsync(),"Ripristino dei dati e ricalcolo");
            (scenario.Module==ClimateDocument.Construction?sheet.Tables.Single(t=>t.Key=="ConstructionLoads").Editor!:scenario.Module==ClimateDocument.Combinations?sheet.Tables.Single().Editor!:sheet.Forms.Last().Form)[key].EditText="abc";await sheet.CalculateAsync();expect.That(!sheet.HasResults&&sheet.CalculationMessage.Length>0,"Errore di input non conserva risultati");sheet.Reload(before.AsObject());expect.That(await sheet.CalculateAsync()&&sheet.HasResults,"Recupero da errore");
            string folder=Path.Combine(output,scenario.Module);Directory.CreateDirectory(folder);string path=Path.Combine(folder,scenario.File+".anthea");
            var document=DocumentService.CreateDocument(scenario.Module);document["dati"]=sheet.Data.DeepClone();DocumentService.Save(path,document);var reopened=DocumentService.Open(path);
            expect.That(JsonNode.DeepEquals(reopened["dati"],sheet.Data),"Salvataggio e apertura del contratto GPC");
            var recalculated=ModuleServices.Get(scenario.Module).Calculate(reopened["dati"]!.AsObject());expect.That(JsonNode.DeepEquals(recalculated.Values,sheet.Result),"Equivalenza dopo riapertura");
            string csv=ClimateDocument.Csv(sheet.Result!);expect.That(csv.Contains("Formula")&&!csv.Contains("NaN"),"CSV tracciabile");File.WriteAllText(Path.ChangeExtension(path,"csv"),csv);
            foreach(var unit in sheet.ChartUnits){sheet.ChartUnit=unit;expect.That(sheet.ChartValues.Count>0&&sheet.ChartValues.All(v=>v.Unit==unit),"Diagrammi con unità omogenee");}
            var inputRows=ClimateDocument.ReportInputs(scenario.Module,sheet.Data);expect.That(inputRows.Count>0&&inputRows.All(r=>r.Length==2),"Dati del report con etichette e unità");
            var report=ClimateReport.Create(scenario.Title,sheet.Data,sheet.Result!,inputRows);expect.That(report.Contains("word/media/climate1.png"),"Relazione con schema incorporato");File.WriteAllBytes(Path.ChangeExtension(path,"docx"),DocxWriter.Write(report));var pdf=DocxPdf.Render(report,scenario.Title);File.WriteAllBytes(Path.ChangeExtension(path,"pdf"),pdf.Bytes);expect.That(pdf.Pages>0,"PDF non vuoto");
            expect.That(Anthea.Application.Reporting.ModuleReportService.Supports(scenario.Module),"Capitolo nella relazione di progetto");
            foreach(var table in sheet.Tables)
            {
                int count=table.Rows.Count;table.AddCommand.Execute(null);expect.That(table.Rows.Count==count+1&&!sheet.HasResults,"Aggiunta riga e invalidazione");
                table.RemoveCommand.Execute(null);expect.That(table.Rows.Count==count,"Rimozione della riga appena aggiunta");
                sheet.IsReadOnly=true;table.AddCommand.Execute(null);expect.That(table.Rows.Count==count&&table.Editor?.IsReadOnly!=false,"Tabelle in sola lettura");sheet.IsReadOnly=false;
            }
            if(sheet.IsPrestressing)
            {
                expect.That(await sheet.CalculateAsync()&&sheet.PrestressPlots.Count==4&&sheet.PrestressStations.Count>100,"Profili di precompressione proiettati");
                sheet.SelectedCase=sheet.Cases.Count-1;sheet.SelectedStation=sheet.PrestressStations[sheet.PrestressStations.Count/2];
                expect.That(sheet.PrestressState.Contains("σeff")&&sheet.SelectedStation.EffectiveMpa<sheet.SelectedStation.InitialMpa,"Stato efficace distinto dal tiro e dal trasferimento");
                expect.That(csv.Contains("AfterAnchoringMpa")&&csv.Contains("EffectivePrestrain"),"CSV di tutte le stazioni e degli stati");
                expect.That(sheet.TransferPrestressCommand.CanExecute(null)==(scenario.File!="precompressione-non-aderente"),"Trasferimento a Concrete limitato ai cavi aderenti e agli stati validi");
                if(scenario.File=="precompressione-posteso")
                {
                    var source=ClimateScenarios.PrestressTargetSection();var original=source.DeepClone();var dialog=sheet.CreatePrestressTransferDialog(source);
                    expect.That(!dialog.ConfirmCommand.CanExecute(null),"Trasferimento: geometria e stati strutturali da associare esplicitamente");
                    dialog.Confirmed=true;dialog.ConfirmCommand.Execute(null);expect.That(dialog.Result is not null,"Copia della sezione con stato efficace: "+dialog.Error);
                    var copy=dialog.Result!;var native=Anthea.ConcreteWorkspace.ConcreteSectionArchive.Restore(copy["input"]!.AsObject(),copy["workspace_ca"]!.AsObject(),requireCurrent:true)!;
                    var tendon=native.Rebars.Single(r=>r.RebarMaterial.SteelType==GPC.Model.Materials.SteelMaterial.SteelTypes.Tendon);
                    expect.That(Math.Abs(tendon.EpsilonP-sheet.SelectedStation.EffectiveMpa/195000)<1e-12,"σeff diventa predeformazione GPC senza duplicare azioni esterne");
                    expect.That(JsonNode.DeepEquals(source,original)&&copy["workspace_ca"]!["trefoli"]![0]!["origine_precompressione"]!["sigma_martinetto_mpa"]!.GetValue<double>()==1350,"Origine conservata e sorgente invariata");
                    var targetDocument=DocumentService.CreateDocument("str_palo");targetDocument["dati"]=copy.DeepClone();string targetFile=Path.Combine(folder,"stato-efficace-concrete.anthea");DocumentService.Save(targetFile,targetDocument);
                    var reloaded=DocumentService.Open(targetFile)["dati"]!.AsObject();var restored=Anthea.ConcreteWorkspace.ConcreteSectionArchive.Restore(reloaded["input"]!.AsObject(),reloaded["workspace_ca"]!.AsObject(),requireCurrent:true)!;
                    expect.That(restored.Rebars.Single(r=>r.RebarMaterial.SteelType==GPC.Model.Materials.SteelMaterial.SteelTypes.Tendon).EpsilonP==tendon.EpsilonP,"Stato efficace persistito nelle classi GPC");
                    source["workspace_ca"]!["trefoli"]![0]!["area"]="150";
                    expect.Throws<ArgumentException>(()=>ClimateDocument.ApplyPrestressToSection(source,sheet.Result!,sheet.SelectedCase,sheet.SelectedStation.Index,"P1"),"Trasferimento rifiuta area non corrispondente");
                }
            }
            if(sheet.IsFire)
            {
                expect.That(await sheet.CalculateAsync()&&sheet.FireDrawing?.Faces.Count>0,"Esposizione delle facce disponibile");sheet.SelectedCase=sheet.Cases.Count-1;
                expect.That(sheet.FireDrawing?.Name==sheet.Cases.Last().Name,"Istante delle facce coincide con il selettore");expect.That(sheet.FirePlots[0].Series[0].Points.Last().X==120,"Curva dei gas sull’intera durata");
                bool flux=sheet.Data["input"]!["CalculateBoundaryFlux"]!.GetValue<bool>();expect.That(sheet.FirePlots.Count==(flux?5:1),"Grafici dei flussi solo con stato superficiale prescritto");expect.That(csv.Contains("Incendio · gas")&&(!flux||csv.Contains("NetWm2")),"CSV completo delle storie termiche");
            }
            if(sheet.IsCranes)
            {
                expect.That(await sheet.CalculateAsync()&&sheet.CraneDrawing?.Points.Count>=4,"Azioni alle ruote proiettate");sheet.SelectedCase=sheet.Cases.Count-1;
                expect.That(sheet.CraneDrawing?.Name==sheet.Cases.Last().Name,"Gruppo disegnato coincide con il caso selezionato");expect.That(csv.Contains("EccentricTorsionKnM"),"CSV delle componenti e torsioni locali");
            }
            if(sheet.IsTimeEffects)
            {
                expect.That(await sheet.CalculateAsync()&&sheet.TimePlots.Count==4,"Grafici temporali con unità distinte");
                sheet.SelectedCase=0;expect.That(sheet.TimePlots[0].Series[0].Points.Last().X==28,"Intervallo del grafico fino al caso selezionato");
                expect.That(csv.Contains("Storia temporale")&&csv.Contains("FreeDisplacementMm"),"CSV della storia temporale completa");
            }
            if(sheet.IsConstruction)
            {
                expect.That(await sheet.CalculateAsync()&&sheet.ConstructionDrawing?.Phases.Count==2,"Sequenza GPC proiettata");
                sheet.SelectedCase=1;expect.That(sheet.ConstructionDrawing?.Selected==1,"Grafico della fase selezionata");
                expect.That(csv.Contains("PressureKpa"),"CSV delle aree di carico");
            }
            if(sheet.IsHydraulics)
            {
                expect.That(await sheet.CalculateAsync()&&sheet.HydraulicDrawing?.Points.Count>=2,"Geometria e pressioni GPC proiettate");
                sheet.SelectedCase=sheet.Cases.Count-1;expect.That(sheet.HydraulicDrawing?.Name==sheet.Cases.Last().Name,"Diagramma del caso selezionato");
                expect.That(csv.Contains("NetPressureKpa"),"CSV delle ordinate di pressione");
            }
            if(sheet.IsDistribution){expect.That(sheet.TrafficDrawing?.Girders.Count==3,"Geometria delle tre travi proiettata");expect.That(await sheet.CalculateAsync()&&sheet.TrafficDrawing?.Influence.Count>0,"Linee d’influenza pubblicate");}
        }
        var snow=ClimateDocument.Create(ClimateDocument.Snow);
        using(var sheet=new ClimateSheetViewModel(ClimateDocument.Snow,new(snow,false,new Host(),Scripted.Services(),ModuleServices.Get(ClimateDocument.Snow),new CalculationLimiter(1))))
        {
            sheet.Forms[0].Form["Roof"].SelectedChoice="Copertura inferiore presso salto";
            expect.That(sheet.Data["input"]!["Roof"]!.ToString()=="LowerRoof"&&sheet.Forms[0].Form.Fields.Any(f=>f.Key=="StepHeightM"),"Scelta italiana, contratto enum stabile e campi condizionali");
        }
        using(var sheet=new ClimateSheetViewModel(ClimateDocument.Construction,new(ClimateDocument.Create(ClimateDocument.Construction),false,new Host(),Scripted.Services(),ModuleServices.Get(ClimateDocument.Construction),new CalculationLimiter(1))))
        {
            var table=sheet.Tables.Single(t=>t.Key=="ConstructionLoads");
            table.Editor!["Model"].SelectedChoice="Azioni assegnate";
            expect.That(sheet.Data["input"]!["ConstructionLoads"]![0]!["Model"]!.ToString()=="Assigned","Modello del carico: contratto enum stabile");
            expect.That(table.Editor!.Fields.Any(f=>f.Key=="AssignedPressureKpa")&&!table.Editor.Fields.Any(f=>f.Key=="ThicknessM"),"Cambio modello: aggiornamento dei campi della riga");
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
