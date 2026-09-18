using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;
internal sealed partial class ConcreteWorkspace
{
    internal async Task VerifyWorkspace(string directory)
    {
        int checks=0;
        void Assert(bool value,string name) { if(!value) throw new Exception("Workspace Checker: "+name+" · "+status.Text); checks++; }
        async Task Capture(string name) { await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout(); File.WriteAllBytes(Path.Combine(directory,"ca_"+name+".png"),Ui.Snapshot(this)); }
        Assert(tabs.Items.Count==5 && sleTabs.Items.Count==3,"Schede CA e taglio");
        Assert(actions["SLE_FREQ"].Count==0 && actions["SLE_QP"].Count==0,"Nessuna combinazione inventata");
        Assert(actions["SLU"][0].Values.D("N")<0,"Compressione negativa");
        Assert(checker3D.Count==2 && checker2D.Count==2,"Domini nativi 3D e 2D");
        var three=domainPanels[0]; var two=domainPanels[1];
        foreach(var mode in new[]{"SLU","SLV"})
        {
            tabs.SelectedIndex=1; three.Mode.SelectedItem=mode; three.Grid.SelectedItem=actions[mode][0];
            Assert(three.View3D!.TriangleCount>100 && three.View3D.SelectedResistance is not null,"Mesh e resistenza "+mode);
            Assert(domainResults["3D:"+mode].Values.First().Utilization is >0,"Tasso nativo "+mode);
            await Capture("dominio3d_"+mode);
        }
        three.Mode.SelectedItem="SLU"; three.View3D!.ToggleWireframe(); three.View3D.StandardView("N–Mx"); await Capture("mesh_frontale"); three.View3D.ToggleWireframe();
        Assert(ReferenceEquals(three.Grid.Items[0],two.Grid.Items[0]),"Azioni condivise");
        three.Form.Set("filtro","Da controllare"); Assert(three.Grid.Items.Count==0,"Filtro esiti"); three.Form.Set("filtro","Tutte");
        three.Grid.Focus(); three.Grid.CurrentCell=new DataGridCellInfo(actions["SLU"][0],three.Grid.Columns[2]);
        Assert(three.Grid.BeginEdit(),"Editing WPF"); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var edit=Ui.Descendants<TextBox>(three.Grid).First(t=>t.IsVisible&&!t.IsReadOnly); edit.Text="-2600";
        Assert(actions["SLU"][0].Values.S("N")=="-2600","Editing conserva transazione");
        three.Grid.Commit(); Assert(Data["combinazioni"]!["SLU"]![0]!["azioni"]![0]!.ToString()=="-2600","Salvataggio N");
        actions["SLU"][0]["N"]="-2500"; await CalculateAllAsync();
        two.Form.Set("tipo","Mx–My"); two.Form.Set("N","-2500"); tabs.SelectedIndex=2;
        await RunAnalysis(t=>CalculateDomain(two,t));
        Assert(two.Plot.Segments.Length>5 && domainResults["2D:SLU"].Values.First().Utilization is >0,"Dominio 2D nativo");
        await Capture("dominio2d");
        two.Form.Set("N","-2000"); await RunAnalysis(t=>CalculateDomain(two,t));
        Assert(domainResults["2D:SLU"].Values.First().Utilization is null,"Fuori piano escluso");
        two.Form.Set("proietta","Sì"); await RunAnalysis(t=>CalculateDomain(two,t));
        Assert(domainResults["2D:SLU"].Values.First().Status.Contains("proiettata"),"Proiezione esplicita");
        foreach(var key in new[]{"SLE_FREQ","SLE_QP"}) { actions[key].Add(CreateAction(key,key,"-100","150","0")); SyncActions(key); }
        tabs.SelectedIndex=3;
        foreach(var key in SectionWorkspace.Sets.Skip(2))
        {
            var panel=stressPanels[key]; sleTabs.SelectedIndex=Array.IndexOf(SectionWorkspace.Sets,key)-2; panel.Grid.SelectedItem=actions[key][0];
            panel.Options.Set("esposizione","XC1"); panel.Options.Set("spaziatura_fessure","150");
            await RunAnalysis(t=>CalculateStress(key,t));
            var check=stressResults[key].Values.First();
            Assert(check.State is not null && check.State.sigma_cls<0,"Tensioni Checker "+key);
            Assert(panel.Bars.Rows.Count==16 && panel.View.Stress is not null,"Viewport "+key);
            Assert(key!="SLE_FREQ"||check.Ratio is null,"Frequente non dichiara falso pass tensionale");
            if(key=="SLE") Assert(check.CrackResult?.Passed is null,"Fessurazione rara non applicabile");
            Assert(!actions[key][0].Values.S("wk").Contains("collegare"),"Fessurazione collegata alla tabella "+key);
            await Capture("tensioni_"+key);
        }
        var rare=stressPanels["SLE"]; rare.Options.Set("modello","Non lineare"); await RunAnalysis(t=>CalculateStress("SLE",t));
        Assert(stressResults["SLE"].Values.First().State?.Native.LinearElasticAnalysis==false,"Non lineare nativo");
        rare.Options.Set("modello","Lineare");
        actions["SLE"][0]["N"]="-2510";
        Assert(Result is null && checker3D.Count==0 && stressResults.Count==0 && two.Plot.Segments.Length==0,"Invalidazione");
        Assert(ParsePaste("Nome\tN\tMx\tMy\nA\t-100,5\t20\t30\n-200\t1\t2").Count==2,"Incolla");
        tendons.Rows.Add(new JsonRow(J.Obj(("id","T1"),("x","0"),("y","-100"),("area","150"),("sigma0","1000")), _=>TendonsChanged())); TendonsChanged();
        await CalculateAllAsync(); Assert(Result is null && preview.Tendons.Count==1,"Materiale trefolo incompleto non ignorato");
        tendons.Rows.Clear(); TendonsChanged();
        foreach(var shape in new[]{"Rettangolare","A T","Circolare"}) { geometry.Set("shape",shape); Assert(preview.Section is not null&&barInventory.Rows.Count>0,"Geometria "+shape); tabs.SelectedIndex=0; await Capture("sezione_"+shape.Replace(' ','_')); }
        geometry.Set("shape","Rettangolare"); tabs.SelectedIndex=4;
        foreach(var axis in new[]{"x","y"}) { shearForm.Set("bw_"+axis,"300"); shearForm.Set("d_"+axis,"450"); shearForm.Set("rami_"+axis,"2"); }
        shearGrid!.Rows.Add(ShearRow(J.Obj(("id","shear1"),("nome","Taglio test"),("N","-500"),("Vx","50"),("Vy","100"))));
        CalculateShear(); Assert(shearResults.Count==1 && shearResults["shear1"].All(r=>r.Ratio is >0),"Taglio NTC"); await Capture("taglio");
        Commit(); var copy=(JsonObject)Data.DeepClone(); using(var restored=new ConcreteWorkspace(copy))
        { Assert(restored.actions["SLE_QP"].Count==1 && restored.settings.D("versione")==2,"Riapertura senza doppio cambio segno"); Assert(restored.shearGrid!.Rows.Count==1,"Salvataggio tabella taglio"); }
        await CalculateAllAsync(); Assert(Result is not null && !Result.B("verifica_normativa_completa",true),"Export senza certificazione globale");
        var owner=Window.GetWindow(this); owner.Width=1450; owner.Height=900;
        for(int i=0;i<5;i++){tabs.SelectedIndex=i;await Capture("tab_"+(i+1));}
        tabs.SelectedIndex=0; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var frame=Ui.Descendants<ViewportFrame>(this).First(f=>ReferenceEquals(f.Host.Content,preview)); bool expanded=false;
        _=Dispatcher.BeginInvoke(new Action(()=>{var window=Application.Current.Windows.OfType<Window>().FirstOrDefault(w=>ReferenceEquals(w.Content,preview));expanded=window is not null;window?.Close();}),DispatcherPriority.ApplicationIdle);
        Ui.Descendants<Button>(frame).First(b=>b.Content?.ToString()=="Espandi").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert(expanded&&ReferenceEquals(frame.Host.Content,preview),"Espansione viewport");
        async Task Automatic()
        {
            var deadline = DateTime.UtcNow.AddSeconds(120);
            do { await Task.Delay(50); } while ((autoCalculate.IsEnabled || Busy) && DateTime.UtcNow < deadline);
            Assert(!autoCalculate.IsEnabled && !Busy, "Ricalcolo automatico termina");
        }
        var cachedMesh = checker3D["SLU"];
        actions["SLE"][0]["N"] = "-2511"; actions["SLE"][0]["N"] = "-2512";
        Assert(Result is null && autoCalculate.IsEnabled, "Digitazione invalida e pianifica il calcolo");
        await Automatic();
        Assert(stressResults["SLE"].Values.First().State?.Native.Force.N == -2512000, "Ultima modifica ricalcolata senza pulsanti");
        Assert(ReferenceEquals(cachedMesh, checker3D["SLU"]), "Cambio azioni riusa il dominio nativo");
        actions["SLU"][0]["N"] = "numero incompleto"; await Automatic();
        Assert(domainResults["3D:SLU"].Values.All(c => c.Utilization is null) && summaries["SLU"].Text.Contains("da controllare"), "Input invalido non mostra resistenze precedenti");
        actions["SLU"][0]["N"] = "-2500"; await Automatic();
        Assert(domainResults["3D:SLU"].Values.All(c => c.Utilization is > 0), "Correzione input recupera automaticamente");
        var canceled = RunAnalysis(async token => await Task.Delay(400, token));
        Assert(Busy && tabs.IsEnabled, "Input utilizzabili durante il calcolo");
        actions["SLE"][0]["N"] = "-2513"; await canceled; await Automatic();
        Assert(stressResults["SLE"].Values.First().State?.Native.Force.N == -2513000, "Risultato superato scartato");
        for (int i = 0; i < 5; i++)
        {
            tabs.SelectedIndex = i; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert(!Ui.Descendants<Button>(this).Any(b => b.Content?.ToString()?.StartsWith("Calcola") == true), "Nessun Calcola nella scheda " + i);
        }
        Assert(Ui.Descendants<Expander>(three.Form).Count() == 2 && Ui.Descendants<Expander>(three.Form).All(e => !e.IsExpanded), "Opzioni avanzate richiudibili");
        tabs.SelectedIndex = 1; three.Mode.SelectedItem = "SLU"; three.Grid.SelectedItem = actions["SLU"][0];
        three.Grid.CurrentCell = new DataGridCellInfo(actions["SLU"][0], three.Grid.Columns[2]);
        PasteCells(three.Grid, "SLU", "-100,5\t20\t30");
        Assert(actions["SLU"][0].Values.D("N") == -100.5 && actions["SLU"].Count == 1, "Incolla sulle celle esistenti");
        string before = actions["SLU"][0].Values.ToJsonString();
        bool rejected = false; try { PasteCells(three.Grid, "SLU", "-1\t2\t3\n-2\tnon numero\t4"); } catch (ArgumentException) { rejected = true; }
        Assert(rejected && actions["SLU"][0].Values.ToJsonString() == before && actions["SLU"].Count == 1, "Incolla non valido atomico");
        PasteCells(three.Grid, "SLU", "Aggiunta\t-200\t25\t10", true); await Automatic();
        Assert(actions["SLU"].Count == 2, "Aggiunta dagli appunti");
        Assert(three.View3D.VisibleActionCount == 2, "Tutte le forze visibili");
        three.ForceToggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert(three.View3D.VisibleActionCount == 1 && three.View3D.SelectedResistance is not null, "Sola selezionata e punto resistente");
        three.ForceToggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var export = Result;
        foreach (double opacity in new[] { 0d, .5, 1 }) { three.Transparency!.Value = 100 * (1 - opacity); Assert(Math.Abs(three.View3D.SurfaceOpacity - opacity) < 1e-12, "Trasparenza " + opacity); }
        three.Transparency!.Value = 45;
        Assert(ReferenceEquals(export, Result) && !autoCalculate.IsEnabled, "Opzioni grafiche non ricalcolano");
        Assert(three.Detail.Text.Contains("STATO AL PUNTO RESISTENTE") && three.Detail.Text.Contains("εc min"), "Riepilogo dominio con stato nativo");
        await Capture("dominio_automatico");
        tabs.SelectedIndex = 3; sleTabs.SelectedIndex = 0;
        foreach (string contour in ConcreteSectionViewport.Contours)
        {
            rare.View.Contour = contour; rare.View.InvalidateVisual(); await Capture("contour_" + Array.IndexOf(ConcreteSectionViewport.Contours, contour));
            Assert(contour == "Solo geometria" || rare.View.ContourLegend.Length > 0, "Legenda contour " + contour);
        }
        rare.View.Contour = ConcreteSectionViewport.Contours[0]; rare.View.InvalidateVisual();
        Assert(rare.Detail.Text.Contains("FESSURAZIONE") && rare.Detail.Text.Contains("As,eff"), "Riepilogo SLE esteso");
        int untouched = actions["SLV"].Count;
        ApplyImport(new SectionActionsExcel.Import([new("SLE_QP", "Da Excel", -10, 1, 2, null, null), new("Taglio", "Taglio Excel", -50, null, null, 10, 20)], 0), true);
        Assert(actions["SLE_QP"].Count == 1 && actions["SLE_QP"][0].Values.S("nome") == "Da Excel" && actions["SLV"].Count == untouched, "Excel sostituisce solo famiglie importate");
        Assert(shearGrid.Rows.Count == 1 && shearGrid.Rows[0].Values.D("N") == -50, "Excel carica tabella Taglio");
        await Automatic();
        Assert(Result is not null && shearResults.Count == 1 && stressResults["SLE_QP"].Count == 1, "Excel avvia il ricalcolo");
        owner.Width = 1366; owner.Height = 850;
        for (int i = 0; i < 5; i++) { tabs.SelectedIndex = i; await Capture("automatico_1366_tab_" + (i + 1)); }
        tabs.SelectedIndex = 0;
        File.WriteAllText(Path.Combine(directory,"ca_workspace_tests.txt"),$"{checks} controlli WPF Checker superati.");
    }
}
