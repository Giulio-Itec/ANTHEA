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
        tabs.SelectedIndex=1;await Idle();var panel=domainPanels.First(p=>p.ThreeD);panel.Grid.SelectedIndex=0;
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
        detailingForm!.Set("elemento","Pilastro");detailingForm.Set("cmin_dur","25");settings["ancoraggi"]!["lunghezza"]="1200";
        tabs.SelectedIndex=5;RefreshDetailing();await Capture("04_dettagli");
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
        ExportReport(Path.Combine(directory,"estensioni.docx"),"Controllo integrazione CA",new(){"geometria","azioni","taglio","costruttivi","curvatura","grafici"});
        using(var zip=System.IO.Compression.ZipFile.OpenRead(Path.Combine(directory,"estensioni.docx")))
        using(var stream=new StreamReader(zip.GetEntry("word/document.xml")!.Open()))
        {string xml=stream.ReadToEnd();Check(xml.Contains("TRsd")&&xml.Contains("Dettagli costruttivi")&&xml.Contains("Momento–curvatura"),"Report delle estensioni");}
        _=Result;curvatureForm.Set("N","-600");Check(Result?["momento_curvatura"] is null,"Modifica del percorso invalida anche la curva esportata");
        Commit();using var reopened=new ConcreteWorkspace(JsonNode.Parse(Data.ToJsonString())!.AsObject());
        Check(reopened.shearGrid!.Rows.Any(r=>r.Values.S("T")=="20")&&reopened.DetailingOptions.S("elemento")=="Pilastro","Persistenza di torsione e tipo elemento");
        Check(reopened.Input.D("circular_sides")==64,"Numero di lati conservato alla riapertura");
        File.WriteAllText(Path.Combine(directory,"esito.txt"),count+" controlli interfaccia superati.");
    }
}
