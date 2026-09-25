using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Threading;
using X.Core;
namespace X.Desktop;
public sealed partial class MainWindow
{
    internal async Task SmokeConcreteExtensions(string directory)
    {
        testing=true;Directory.CreateDirectory(directory);document=Archivio.Documento("str_palo");currentSheet=null;ShowSheet(document);
        await editor!.CalculateAsync();await editor.VerifyConcreteExtensions(directory);dirty=false;
    }
}
internal sealed partial class SheetEditor
{
    internal Task VerifyConcreteExtensions(string directory)=>concrete!.VerifyExtensions(directory);
}
internal sealed partial class ConcreteWorkspace
{
    internal async Task VerifyExtensions(string directory)
    {
        int count=0;void Check(bool value,string text){if(!value)throw new Exception(text);count++;}
        async Task Idle(){await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);UpdateLayout();}
        async Task Updated(){while(Busy)await Task.Delay(40);await CalculateAllAsync();await Idle();}
        async Task Capture(string name){await Idle();File.WriteAllBytes(Path.Combine(directory,name+".png"),Ui.Snapshot(Window.GetWindow(this)));}
        Check(tabs.Items.Count==7,"Sette schede CA");
        tabs.SelectedIndex=0;
        var originalCls=Input.S("cls_diagramma");var originalSteel=Input.S("steel_diagramma");
        var clsName=Input.S("classe_cls");var steelName=Input.S("classe_acciaio");
        materials.Set("cls_diagramma","Bilineare");materials.Set("steel_diagramma","Incrudente");
        Check(materials.Editors["cls_diagramma"].IsEnabled && materials.Editors["steel_diagramma"].IsEnabled,"Legami dei materiali selezionabili");
        Check(Input.S("classe_cls")==clsName && Input.S("classe_acciaio")==steelName,"Cambio legame conserva le classi dei materiali");
        Check(ConcreteMaterials.Rebar(Input).StressStrainCurve==GPC.Model.Materials.SteelMaterial.StressStrainCurveType.ElasticHardening,"Legame incrudente passato al materiale nativo");
        RefreshStandardMaterialChoices();
        Check(Input.S("cls_diagramma")=="Bilineare" && Input.S("steel_diagramma")=="Incrudente","Aggiornamento catalogo conserva i legami scelti");
        materials.Set("cls_diagramma",originalCls);materials.Set("steel_diagramma",originalSteel);
        await Updated();
        foreach(var (group,ratio) in new[]{("ingressi",.32),("risultati",.62),("righe",.64)})
        {
            bool rows=group=="righe";SynchronizeWorkspaceSplit(group,rows,ratio);
            Check(workspaceSplits[group].All(g=>Math.Abs((rows?g.RowDefinitions[0].Height.Value:g.ColumnDefinitions[0].Width.Value)-ratio)<1e-10),"Divisori sincronizzati: "+group);
        }
        SynchronizeWorkspaceSplit("ingressi",false,.3);SynchronizeWorkspaceSplit("risultati",false,.6);SynchronizeWorkspaceSplit("righe",true,3.7/5.7);
        tabs.SelectedIndex=3;await Capture("00_allineamento_sle");
        Check(Ui.Descendants<System.Windows.Controls.ComboBox>(sleTabs).Any(c=>c.ItemsSource is string[] choices && choices.SequenceEqual(SectionWorkspace.Sets.Skip(2).Select(SectionWorkspace.Label))),"Selettore combinazione SLE visibile nella colonna dati");
        var sleChoice=Ui.Descendants<System.Windows.Controls.ComboBox>(sleTabs).Single(c=>c.ItemsSource is string[] choices && choices.SequenceEqual(SectionWorkspace.Sets.Skip(2).Select(SectionWorkspace.Label)));
        sleChoice.SelectedIndex=2;await Idle();
        Check(sleTabs.SelectedIndex==2 && stressPanels["SLE_QP"].View.IsVisible,"Selettore SLE apre Quasi permanente");
        sleTabs.SelectedIndex=0;await Idle();
        Check(sleChoice.SelectedIndex==0 && stressPanels["SLE"].View.IsVisible,"Navigazione SLE mantiene selettore e vista coerenti");
        tabs.SelectedIndex=0;await Idle();
        var tendonMaterialForm=Ui.Descendants<InputForm>(this).Single(f=>f.Editors.ContainsKey("Ep")&&f.Editors.ContainsKey("diagramma"));
        string savedTendonDiagram=settings["materiale_trefolo"].S("diagramma"),tendonId=settings["materiale_trefolo"].S("id");
        string changedTendonDiagram=savedTendonDiagram=="Incrudente"?"Elastoplastico":"Incrudente";
        tendonMaterialForm.Set("diagramma",changedTendonDiagram);
        Check(settings["materiale_trefolo"].S("diagramma")==changedTendonDiagram && AvailableTendonMaterials().Single(m=>m.S("id")==tendonId).S("diagramma")==changedTendonDiagram,"Legame trefolo predefinito aggiornato per nuovi cavi");
        tendonMaterialForm.Set("diagramma",savedTendonDiagram);
        await Capture("00_materiali");
        var preservedDomain=checker3D["SLU"];
        quickResistanceForm!.Set("N","-500");
        await RefreshQuickResistance();
        Check(quickResistanceText.Text.Split('\n').Length==4&&!quickResistanceText.Text.Contains("—"),"Quattro resistenze rapide plastiche: "+quickResistanceText.Text);
        quickResistanceForm.Set("tipo","Elastico");await RefreshQuickResistance();
        Check(quickResistanceText.Text.Split('\n').Length==4&&!quickResistanceText.Text.Contains("—"),"Quattro resistenze rapide elastiche");
        Check(ReferenceEquals(preservedDomain,checker3D["SLU"]),"Input resistenze rapide non rigenera il dominio");
        await Capture("00_resistenze_rapide");
        tabs.SelectedIndex=1;await Idle();var panel=domainPanels.First(p=>p.ThreeD);panel.Grid.SelectedIndex=0;
        await Capture("00b_linea_verifica");
        panel.Inspection.Tabs.SelectedIndex=1;UpdateSectionInspection(panel);
        for(int i=0;i<200&&panel.Inspection.View.Stress is null;i++)await Task.Delay(25);
        Check(panel.Inspection.View.Stress is not null,"Mappa del punto limite: "+panel.Inspection.Message.Text);
        await Capture("01_limite");
        foreach (var inspectedPanel in domainPanels)
        {
            tabs.SelectedIndex = inspectedPanel.ThreeD ? 1 : 2;
            if (!inspectedPanel.ThreeD)
            {
                // The sample action is biaxial: explicitly project it into the 2D test plane.
                inspectedPanel.Form.Set("proietta", "Sì");
                await RunAnalysis(t => CalculateDomain(inspectedPanel, t));
            }
            inspectedPanel.Grid.SelectedIndex = 0;
            inspectedPanel.Inspection.Tabs.SelectedIndex = 1;
            UpdateSectionInspection(inspectedPanel);
            for(int i=0;i<200&&inspectedPanel.Inspection.View.Stress is null;i++)await Task.Delay(25);
            Check(inspectedPanel.Inspection.View.Stress is not null, "Soluzione disponibile per il test del menu " + inspectedPanel.Prefix);
            await Idle();
            var view = inspectedPanel.Inspection.View;
            var contourChoice = Ui.Descendants<System.Windows.Controls.ComboBox>(inspectedPanel.Inspection.Tabs)
                .Single(c => c.ItemsSource is string[] choices && choices.SequenceEqual(ConcreteSectionViewport.Contours));
            foreach (var contour in ConcreteSectionViewport.Contours.Concat(ConcreteSectionViewport.Contours.Reverse()))
            {
                contourChoice.SelectedItem = contour;
                await Idle();
                Check(view.Contour == contour,
                    $"Menu dominio {inspectedPanel.Prefix}: selezionato '{contour}', visualizzato '{view.Contour}'");
            }
            for (int i = 0; i < ConcreteSectionViewport.Contours.Length; i++)
            {
                contourChoice.SelectedIndex = i;
                await Capture($"contour_{(inspectedPanel.ThreeD ? "3d" : "2d")}_{i}");
                Check(i == 7 ? view.ContourLegend == "" : view.ContourLegend.Length > 0, "Legenda della vista selezionata");
                if (i == 6)
                    Check(view.ContourLegend.Contains(EngineeringFormat.Number(view.Stress!.ConcreteVertices.Min(v => v.Strain))),
                        "Scala delle deformazioni include il bordo compresso");
            }
            contourChoice.SelectedIndex = 0;
        }
        tabs.SelectedIndex = 1;
        panel.Inspection.State.SelectedIndex=0;
        for(int i=0;i<300&&panel.Inspection.View.Stress is null;i++)await Task.Delay(25);
        Check(panel.Inspection.View.Stress is not null,"Mappa azione inserita: "+panel.Inspection.Message.Text);
        await Capture("02_azione");
        File.WriteAllBytes(Path.Combine(directory,"03_piano.png"),panel.Inspection.Plane.Png(800,600));
        detailingForm!.Set("elemento","Pilastro");anchorageForm.Set("lunghezza","1200");
        tabs.SelectedIndex=5;RefreshDetailing();
        Check(detailingResults.Any(r=>r.Name=="Diametro staffe"),"Esposizione non scelta non blocca le verifiche indipendenti");
        stressPanels["SLE_QP"].Options.Set("esposizione","XC1");
        double ordinary=DetailingOptions.D("cmin_dur");
        stressPanels["SLE_QP"].Options.Set("esposizione","XS3");
        Check(DetailingOptions.D("cmin_dur")>ordinary && coverDetailingForm.Get("esposizione_sle")=="XS3","Copriferro aggiornato automaticamente da esposizione SLE");
        stressPanels["SLE_QP"].Options.Set("esposizione","XC1");
        anchorageForm.Set("percentuale","");anchorageForm.Set("interferro","");
        Check(anchorageResult is not null,"Ancoraggio rettilineo indipendente dai dati di sovrapposizione");
        anchorageForm.Set("lunghezza","");
        Check(anchorageResult is null && anchorageText.Text.Contains("Inserire la lunghezza"),"Lunghezza vuota invalida il risultato");
        anchorageForm.Set("lunghezza","1200");anchorageForm.Set("tipo","Sovrapposizione rettilinea");
        anchorageForm.Set("percentuale","100");anchorageForm.Set("interferro","1000");
        Check(anchorageResult?.Passed==false && anchorageText.Text.Contains("Interferro della giunzione"),"Interferro giunzione verificato separatamente");
        anchorageForm.Set("confinamento","Non conforme");
        Check(anchorageText.Text.Contains("Non conforme"),"Riscontro esecutivo modificabile");
        anchorageForm.Set("tipo","Ancoraggio rettilineo");anchorageForm.Set("confinamento","Da verificare");
        await Capture("04_dettagli");
        Check(detailingTopics["cover"].Text.Contains("Interferro minimo")&&detailingTopics["stirrups"].Text.Contains("Diametro staffe")&&detailingTopics["bars"].Text.Contains("Armatura longitudinale"),"Verifiche affiancate per argomento");
        detailingTopics["stirrups"].BringIntoView();await Capture("04b_staffe");
        anchorageText.BringIntoView();await Capture("04c_ancoraggi");
        Check(detailingResults.Any(r=>r.Name=="Diametro staffe"),"Controlli pilastro eseguiti");
        curvatureForm.Set("N","-500");curvatureForm.Set("passi","12");curvatureForm.Set("angoli","16");
        tabs.SelectedIndex=6;CalculateCurvature();while(curvatureRunning)await Task.Delay(40);
        Check(curvatureResult?.Points.Count>=13,"Diagramma fino al limite: "+curvatureText.Text);await Capture("05_curvatura");
        Input["foro_presente"]=true;Input["inner_width_mm"]="200";Input["inner_height_mm"]="300";Invalidate();await Updated();
        tabs.SelectedIndex=0;await Capture("06_rettangolare_forata");Check(preview.Section!.Holes.Count==1,"Foro rettangolare in anteprima");
        geometry.Set("shape","Circolare");Input["inner_diameter_mm"]="500";Invalidate();await Updated();await Capture("07_circolare_forata");
        Check(checker3D.ContainsKey("SLU"),"Dominio circolare forato calcolato");
        Check(preview.Section!.Outline.Count==32&&preview.Section.Holes[0].Length==32,"Anteprima circolare: default 32 lati per contorno");
        geometry.Set("circular_sides","64");await Updated();
        Check(checker3D["SLU"].Section.Geometry.Outline.Count==64&&preview.Section!.Holes[0].Length==64,"Modifica dei lati: anteprima e verificatore aggiornati");
        geometry.Editors["circular_sides"].BringIntoView();await Capture("07_lati_circolari");
        shearGrid!.Rows.Add(ShearRow(J.Obj(("id","circle_shear"),("nome","Taglio circolare"),("N","-100"),("Vx","20"),("Vy","30"))));
        CalculateShear();Check(!shearResults.ContainsKey("circle_shear"),"Circolare senza scelta del modello non verificata");
        torsionForm.Set("modello_circolare","Pile · NTC §7.9.5.2");CalculateShear();Check(shearResults.ContainsKey("circle_shear"),"Taglio circolare cavo con scelta esplicita");
        shearGrid.Rows.RemoveAt(shearGrid.Rows.Count-1);
        geometry.Set("shape","Rettangolare");Input["foro_presente"]=false;Invalidate();await Updated();
        torsionForm.Set("chiusura_torsione","Confermato");torsionForm.Set("as_torsione","600");torsionForm.Set("cot_torsione","1");
        shearGrid!.Rows.Add(ShearRow(J.Obj(("id","test_t"),("nome","Taglio e torsione"),("N","-300"),("Vx","50"),("Vy","30"),("T","20"))));
        CalculateShear();tabs.SelectedIndex=4;shearGrid.SelectedIndex=shearGrid.Rows.Count-1;UpdateShearSelection();await Capture("08_torsione");
        Check(torsionResults.ContainsKey("test_t"),"Torsione calcolata dalla UI: "+shearGrid.Rows.Last().Values.S("esito"));
        stressPanels["SLE_QP"].Options.Set("esposizione","XC1");stressPanels["SLE_QP"].Options.Set("spaziatura_fessure","100");SynchronizeSharedSle("SLE_QP");
        var row=CreateAction("SLE_QP","Trazione pura","300","0","0");actions["SLE_QP"].Add(row);SyncActions("SLE_QP");InvalidateActions("SLE_QP");await Updated();
        tabs.SelectedIndex=3;sleTabs.SelectedIndex=2;await Idle();stressPanels["SLE_QP"].Grid.SelectedItem=row;UpdateStressSelection("SLE_QP");await Capture("09_fasce_tese");
        Check(stressPanels["SLE_QP"].Regions.Items.Count==4,"Quattro zone efficaci selezionabili");
        Check(Result?["torsione"]?["test_t"] is not null&&Result?["dettagli_costruttivi"] is JsonArray,"Esportazione dei nuovi risultati");
        tabs.SelectedIndex=6;CalculateCurvature();while(curvatureRunning)await Task.Delay(40);
        Check(Result?["momento_curvatura"]?["Points"] is JsonArray points&&points.Count>=13,"Esportazione della curva");
        var curveReport = BuildReport("Momento curvatura", ["curvatura", "grafici"]);
        using (var zip = new System.IO.Compression.ZipArchive(new MemoryStream(curveReport)))
            Check(zip.Entries.Count(e => e.FullName.StartsWith("word/media/")) == 1, "Grafico della curvatura disponibile anche senza selezionare Taglio");
        ExportReport(Path.Combine(directory,"estensioni.docx"),"Controllo integrazione CA",new(){"geometria","azioni","taglio","costruttivi","curvatura","grafici"});
        using(var zip=System.IO.Compression.ZipFile.OpenRead(Path.Combine(directory,"estensioni.docx")))
        using(var stream=new StreamReader(zip.GetEntry("word/document.xml")!.Open()))
        {string xml=stream.ReadToEnd();Check(xml.Contains("TRsd")&&xml.Contains("Dettagli costruttivi")&&xml.Contains("Momento–curvatura"),"Report delle estensioni");}
        _=Result;curvatureForm.Set("N","-600");Check(Result?["momento_curvatura"] is null,"Modifica del percorso invalida anche la curva esportata");
        Commit();using var reopened=new ConcreteWorkspace(JsonNode.Parse(Data.ToJsonString())!.AsObject());
        Check(reopened.shearGrid!.Rows.Any(r=>r.Values.S("T")=="20")&&reopened.DetailingOptions.S("elemento")=="Pilastro","Persistenza di torsione e tipo elemento");
        Check(reopened.Input.D("circular_sides")==64,"Numero di lati conservato alla riapertura");
        Check(reopened.settings["resistenze_rapide"].S("N")=="-500"&&reopened.settings["resistenze_rapide"].S("tipo")=="Elastico","Input resistenze rapide conservati alla riapertura");
        File.WriteAllText(Path.Combine(directory,"esito.txt"),count+" controlli interfaccia superati.");
    }
}
