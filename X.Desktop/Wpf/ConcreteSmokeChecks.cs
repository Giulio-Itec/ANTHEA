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
        Assert(actions["SLU"][0].Values.S("N")=="-2500" && Result is not null,"Digitazione non confermata conserva i risultati fino al focus/commit");
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
        Assert(stressPanels.Values.All(p => p.Options.Editors["n_trefoli"].Visibility == Visibility.Visible) && tendonOnlyControls.All(c => c.Visibility == Visibility.Visible), "Inserire trefolo rende visibili le opzioni dedicate");
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
            do { await Task.Delay(50); } while ((calculationQueued || Busy) && DateTime.UtcNow < deadline);
            Assert(!calculationQueued && !Busy, "Ricalcolo automatico termina");
        }
        var cachedMesh = checker3D["SLU"];
        actions["SLE"][0]["N"] = "-2511"; actions["SLE"][0]["N"] = "-2512";
        Assert(Result is null && calculationQueued, "Conferma input invalida e pianifica il calcolo");
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
        Assert(ReferenceEquals(export, Result) && !calculationQueued, "Opzioni grafiche non ricalcolano");
        Assert(three.Detail.Text.Contains("STATO AL PUNTO RESISTENTE") && three.Detail.Text.Contains("εc min"), "Riepilogo dominio con stato nativo");
        await Capture("dominio_automatico");
        tabs.SelectedIndex = 3; sleTabs.SelectedIndex = 0;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout();
        var contourChoice = Ui.Descendants<ComboBox>(this).Single(c => c.ItemsSource is string[] choices && choices.SequenceEqual(ConcreteSectionViewport.Contours));
        var contourResult = Result;
        foreach (string contour in ConcreteSectionViewport.Contours)
        {
            contourChoice.SelectedItem = contour; await Capture("contour_" + Array.IndexOf(ConcreteSectionViewport.Contours, contour));
            Assert(rare.View.Contour == contour && settings["sle"]!["SLE"].S("contour") == contour && ReferenceEquals(Result, contourResult), "Menu contour seleziona subito e non ricalcola " + contour);
            Assert(contour == "Solo geometria" || rare.View.ContourLegend.Length > 0, "Legenda contour " + contour);
        }
        contourChoice.SelectedItem = ConcreteSectionViewport.Contours[0];
        var selectedContour = contourChoice.SelectedItem;
        contourChoice.RaiseEvent(new System.Windows.Input.MouseWheelEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, -120) { RoutedEvent = System.Windows.Input.Mouse.PreviewMouseWheelEvent });
        Assert(Equals(contourChoice.SelectedItem, selectedContour), "Rotella sul menu chiuso non cambia contour");
        Assert(rare.Detail.Text.Contains("FESSURAZIONE") && rare.Detail.Text.Contains("As,eff"), "Riepilogo SLE esteso");
        int untouched = actions["SLV"].Count;
        ApplyImport(new SectionActionsExcel.Import([new("SLE_QP", "Da Excel", -10, 1, 2, null, null), new("Taglio", "Taglio Excel", -50, null, null, 10, 20)], 0), true);
        Assert(actions["SLE_QP"].Count == 1 && actions["SLE_QP"][0].Values.S("nome") == "Da Excel" && actions["SLV"].Count == untouched, "Excel sostituisce solo famiglie importate");
        Assert(shearGrid.Rows.Count == 1 && shearGrid.Rows[0].Values.D("N") == -50, "Excel carica tabella Taglio");
        await Automatic();
        Assert(Result is not null && shearResults.Count == 1 && stressResults["SLE_QP"].Count == 1, "Excel avvia il ricalcolo");
        tabs.SelectedIndex = 0; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var widthBox = (TextBox)geometry.Editors["width_mm"]; widthBox.Focus();
        string oldWidth = Input.S("width_mm"); var previousResult = Result;
        widthBox.Text = "620";
        Assert(widthBox.IsKeyboardFocusWithin && Input.S("width_mm") == oldWidth && ReferenceEquals(previousResult, Result), "Input geometria resta bozza durante digitazione");
        ((TextBox)geometry.Editors["height_mm"]).Focus();
        Assert(Input.S("width_mm") == "620" && calculationQueued && Result is null, "Cambio focus conferma senza timer");
        await Automatic();
        geometry.Set("width_mm", oldWidth); await Automatic();
        rare.Options.Set("n_armature", "15"); await Automatic();
        double ec = ConcreteMaterials.Concrete(Input).E;
        Assert(Math.Abs(settings["sle"]!["SLE"].D("phi") - (15 * ec / Input.D("steel_modulus_mpa") - 1)) < 1e-10, "n armature aggiorna phi senza arrotondare");
        rare.Options.Set("n_trefoli", "12"); await Automatic();
        Assert(Math.Abs(settings["sle"]!["SLE"].D("phi_trefoli") - (12 * ec / 195000 - 1)) < 1e-10, "n trefoli aggiorna phi");
        Assert(rare.Concrete.Rows.Count == preview.Section!.Outline.Count && rare.Bars.Rows.All(r => r.Values.S("strain") != ""), "Dettagli CLS e deformazioni barre");
        Assert(!rare.View.BarValues && !rare.View.TendonValues && !rare.View.ConcreteValues, "Etichette tensionali disattivate di default");
        tabs.SelectedIndex = 2; two.Grid.SelectedItem = actions["SLU"][0]; UpdateSelection(two); await Capture("assi_centrati");
        Assert(two.Plot.CenteredAxes && two.Plot.AxisHalfRange.X % 10 == 0 && two.Plot.AxisHalfRange.Y % 10 == 0, "Assi simmetrici e arrotondati");
        Assert(two.Plot.VerificationSegments.Count == 2, "Linea origine Ed Rd");
        var noRecompute = Result;
        foreach (var panel in domainPanels)
        {
            panel.Options["mostra_ed"] = false; panel.Options["mostra_rd"] = false; panel.Options["mostra_linee"] = false; UpdateSelection(panel);
            Assert(panel.ThreeD ? panel.View3D!.VisibleActionCount == 0 : panel.Plot.Markers.Count == 0 && panel.Plot.VerificationSegments.Count == 0, "Livelli grafici indipendenti " + panel.Prefix);
            panel.Options["mostra_ed"] = true; panel.Options["mostra_rd"] = true; panel.Options["mostra_linee"] = true; panel.Options["colora_eta"] = true; UpdateSelection(panel);
        }
        Assert(ReferenceEquals(noRecompute, Result) && !calculationQueued, "Interruttori grafici non ricalcolano");
        var filter = tableFilters[three.Grid]; filter.Query = "Aggiunta"; filter.Key = "nome"; ApplyTableFilter(three.Grid);
        Assert(three.Grid.Items.Count == 1 && actions["SLU"].Count == 2, "Filtro testuale non elimina input");
        filter.Query = ""; filter.SortKey = "N"; filter.Descending = false; ApplyTableFilter(three.Grid);
        Assert(three.Grid.Items.OfType<JsonRow>().First().Values.D("N") <= three.Grid.Items.OfType<JsonRow>().Last().Values.D("N"), "Ordinamento numerico e non lessicografico");
        var xlsx = SectionActionsExcel.Write(ExportActionRows()); File.WriteAllBytes(Path.Combine(directory, "sollecitazioni.xlsx"), xlsx); RememberExcel(Path.Combine(directory, "sollecitazioni.xlsx"));
        Assert(SectionActionsExcel.Read(xlsx).Rows.Count == actions.Values.Sum(a => a.Count) + shearGrid.Rows.Count && excelPaths.All(t => t.Text == settings.S("file_sollecitazioni")), "Excel esportazione completa e percorso condiviso");
        ShearOptions["rami_x"] = "4"; SynchronizeStirrups(); Invalidate(); await Automatic();
        Assert(stirrupForms.Where(f => f.Editors.ContainsKey("rami_x")).All(f => f.Get("rami_x") == "4") && preview.Stirrups == ShearOptions && shearView.Stirrups == ShearOptions, "Staffe sincronizzate e disegnate nelle preview");
        settings["normativa"] = "DS EN 1992-1-1"; ResetCoefficients(); Invalidate(); await Automatic();
        Assert(Math.Abs(Input.D("gamma_c") - 1.4) < 1e-10 && Math.Abs(Input.D("gamma_s") - 1.2) < 1e-10 && checker3D.Count == 2, "Normativa nazionale collegata ai domini");
        Assert(stressResults["SLE"].Values.All(s => s.Cracking.Contains("da implementare")) && shearResults.Count == 0, "Nessuna equivalenza implicita per taglio e fessurazione non NTC");
        settings["normativa"] = "NTC 2018"; ResetCoefficients(); Invalidate(); await Automatic();
        Assert(!reinforcement.Editors.ContainsKey("transverse_bar_diameter_mm") && !reinforcement.Editors.ContainsKey("transverse_spacing_mm"), "Staffe assenti dal gruppo armature");
        Assert(SectionWorkspace.Label("SLU") == "Plastico" && SectionWorkspace.Label("SLV") == "Elastico" && three.Mode.ItemTemplate is not null, "Etichette Plastico ed Elastico senza cambiare ID archivi");
        var oldDomain = checker3D["SLU"]; var oldMesh = meshes["SLU"]; var oldTwo = checker2D["SLU"];
        var oldStress = stressResults["SLE"];
        three.Form.Set("criterio", "Eccentricità costante");
        Assert(ReferenceEquals(checker3D["SLU"], oldDomain) && three.View3D.TriangleCount > 100 && Result is null, "Cambio criterio conserva subito la mesh e invalida solo esiti");
        await Automatic();
        Assert(ReferenceEquals(checker3D["SLU"], oldDomain) && ReferenceEquals(meshes["SLU"], oldMesh) && oldDomain.Native.FailureAnalysisType == CheckerSection.Criterion("Eccentricità costante"), "Criterio aggiornato nella DLL senza ricostruire dominio");
        Assert(ReferenceEquals(stressResults["SLE"], oldStress) && ReferenceEquals(checker2D["SLU"], oldTwo), "Cambio criterio non ricalcola SLE né altro dominio");
        three.Form.Set("criterio", "N costante"); await Automatic();
        two.Form.Set("proietta", "No"); await Automatic();
        Assert(ReferenceEquals(checker2D["SLU"], oldTwo) && domainResults["2D:SLU"].Values.All(v => v.Utilization is null), "Proiezione aggiorna solo azioni 2D");
        two.Form.Set("proietta", "Sì"); await Automatic();
        Assert(!Ui.Descendants<Expander>(two.Form).Any(e => Ui.Descendants<FrameworkElement>(e).Contains(two.Form.Editors["proietta"])), "Proiezione fuori dalle opzioni avanzate");
        Assert(stressPanels.Values.All(p => p.Options.Editors["n_trefoli"].Visibility == Visibility.Collapsed) && tendonOnlyControls.All(c => c.Visibility == Visibility.Collapsed), "Impostazioni trefoli nascoste senza trefoli");
        rare.Options.Set("esposizione", "XC2"); await Automatic();
        Assert(SectionWorkspace.Sets.Skip(2).All(k => settings["sle"]![k].S("esposizione") == "XC2") && stressPanels.Values.All(p => p.Options.Get("esposizione") == "XC2"), "Opzioni SLE comuni nelle tre viste");
        rare.Options.Set("esposizione", "XC1"); await Automatic();
        Assert(three.Summary.Text.Contains("Governa:") && rare.Summary.Text.Contains("Quasi permanente") && rare.Summary.Text.Contains("η =") && shearWorst.Text.Contains("Governa:"), "Riepiloghi peggiori per dominio, SLE e taglio");
        double originalDepth = ShearOptions.D("d_y"); geometry.Set("height_mm", "820"); await Automatic();
        Assert(Math.Abs(ShearOptions.D("d_y") - originalDepth - 20) < 1e-8 && !shearForm.Editors["d_y"].IsEnabled, "d automatico segue il wizard ed è protetto");
        geometry.Set("height_mm", "800"); await Automatic();
        shearForm.Set("parametri", "Manuali"); shearForm.Set("d_y", "400"); await Automatic();
        Assert(ShearOptions.D("d_y") == 400 && shearForm.Editors["d_y"].IsEnabled, "Override manuale taglio disponibile");
        shearForm.Set("parametri", "Automatici da sezione"); await Automatic();
        shearForm.Set("modello", "Senza staffe"); await Automatic();
        Assert(shearResults.Count == 0 && shearGrid.Rows.All(r => r.Values.S("esito").Contains("ancoraggio")), "Asl geometrica non implica ancoraggio verificato");
        shearForm.Set("ancoraggio", "Confermato"); await Automatic();
        Assert(shearResults.Count == shearGrid.Rows.Count, "Conferma ancoraggio abilita modello senza staffe");
        geometry.Set("height_mm", "810"); await Automatic();
        Assert(ShearOptions.S("ancoraggio") == "Da verificare" && shearResults.Count == 0, "Modifica geometria richiede nuova conferma ancoraggio");
        geometry.Set("height_mm", "800"); shearForm.Set("modello", "Con staffe"); await Automatic();
        var scaleResult = Result;
        two.Plot.Zoom = 1; _ = two.Plot.Png(); double halfRange = two.Plot.AxisHalfRange.X;
        two.Plot.Zoom = 1.01; _ = two.Plot.Png();
        Assert(two.Plot.AxisHalfRange.X < halfRange, "Zoom 2D continuo senza scatti da arrotondamento"); two.Plot.ResetView();
        var originalMarkers = two.Plot.Markers.ToList(); two.Plot.Markers.Add(new(1e7, 1e7, "outlier", Ui.Blue)); _ = two.Plot.Png();
        Assert(two.Plot.AxisHalfRange.X == halfRange, "Fit 2D sul dominio stabile con azioni esterne");
        two.Plot.FitIncludesMarkers = true; _ = two.Plot.Png();
        Assert(two.Plot.AxisHalfRange.X >= 1e7, "Fit 2D può includere le azioni"); two.Plot.FitIncludesMarkers = false; two.Plot.Markers = originalMarkers;
        two.Plot.ScaleX = 2; two.Options["scala_x"] = 2; two.Plot.ResetView();
        Assert(two.Plot.ScaleX == 1 && two.Options.D("scala_x") == 1, "Fit e doppio clic 2D ripristinano anche la scala salvata");
        three.View3D.SetAxisScale(1.4, .8, 1); three.View3D.FitView(); three.View3D.Zoom = 1.2;
        Assert(Math.Abs(three.View3D.Zoom - 1.2) < 1e-9 && ReferenceEquals(scaleResult, Result) && !calculationQueued, "Scala e zoom 3D senza calcolo");
        three.View3D.SetAxisScale(1, 1, 1); three.View3D.FitView();
        tabs.SelectedIndex = 1; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var scaleFrame = Ui.Descendants<ViewportFrame>(this).First(f => ReferenceEquals(f.Host.Content, three.View3D)); bool scaleApplied = false;
        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            var dialog = Application.Current.Windows.OfType<Window>().First(w => w.Title == "Scala del dominio 3D");
            var scaleForm = Ui.Descendants<InputForm>(dialog).Single(); scaleForm.Set("scala_n", "0.8"); scaleForm.Set("zoom", "125");
            dialog.UpdateLayout(); File.WriteAllBytes(Path.Combine(directory, "ca_scala_dialog.png"), Ui.Snapshot(dialog));
            Ui.Descendants<Button>(dialog).First(b => b.Content?.ToString() == "Applica").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); scaleApplied = true;
        }), DispatcherPriority.ApplicationIdle);
        scaleFrame.Toolbar.Children.OfType<Button>().First(b => b.Content?.ToString() == "Scala…").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert(scaleApplied && three.Options.D("scala_n") == .8 && Math.Abs(three.View3D.Zoom - 1.25) < 1e-9 && ReferenceEquals(scaleResult, Result), "Dialog scala applica e persiste senza ricalcolo");
        scaleFrame.Toolbar.Children.OfType<Button>().First(b => b.Content?.ToString() == "Adatta").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert(three.Options.D("scala_n") == 1 && Math.Abs(three.View3D.Zoom - 1) < 1e-9, "Adatta ripristina scala e zoom");
        tabs.SelectedIndex = 0; owner.Width = 1000; owner.Height = 650; await Capture("finestra_ridotta");
        Assert(workspaceScroll.ScrollableHeight > 0 && workspaceScroll.ScrollableWidth > 0, "Finestra piccola ha entrambe le barre di scorrimento");
        workspaceScroll.ScrollToBottom(); workspaceScroll.ScrollToRightEnd(); await Capture("finestra_scorsa");
        Assert(workspaceScroll.VerticalOffset > 0 && workspaceScroll.HorizontalOffset > 0, "Barre finestra spostano realmente il contenuto");
        workspaceScroll.ScrollToTop(); workspaceScroll.ScrollToLeftEnd(); owner.Width = 1366; owner.Height = 850;
        tabs.SelectedIndex = 1; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout();
        var numericCell = three.Grid.Columns.OfType<DataGridTextColumn>().First(c => c.SortMemberPath == "N").GetCellContent(three.Grid.Items[0]) as TextBlock;
        var nameCell = three.Grid.Columns.OfType<DataGridTextColumn>().First(c => c.SortMemberPath == "nome").GetCellContent(three.Grid.Items[0]) as TextBlock;
        Assert(numericCell?.TextAlignment == TextAlignment.Center && nameCell?.TextAlignment == TextAlignment.Left, "Numeri centrati e nomi leggibili a sinistra");
        ExportReport(Path.Combine(directory, "relazione_ca.docx"), "Collaudo sezione rettangolare", ["geometria", "materiali", "coefficienti", "azioni", "dominio3d", "dominio2d", "SLE", "SLE_FREQ", "SLE_QP", "taglio", "grafici"]);
        ExportReport(Path.Combine(directory, "relazione_ca_completa.docx"), "SLE tutte le combinazioni", ["SLE", "SLE_FREQ", "SLE_QP", "sle_tutte", "dettagli"]);
        ExportReport(Path.Combine(directory, "relazione_ca_selettiva.docx"), "Solo SLE", ["SLE"]);
        using (var zip = System.IO.Compression.ZipFile.OpenRead(Path.Combine(directory, "relazione_ca_selettiva.docx")))
        using (var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open()))
        { string xml = reader.ReadToEnd(); Assert(xml.Contains("Ambito e limiti") && !xml.Contains("Armature longitudinali") && xml.Contains("SLE Rara"), "Report sezioni selettive e limiti sempre inclusi"); Assert(xml.Contains("Inviluppo tensioni e deformazioni") && xml.Contains("Combinazione di origine") && !xml.Contains("Stampa completa"), "Report SLE predefinito a inviluppo"); }
        using (var zip = System.IO.Compression.ZipFile.OpenRead(Path.Combine(directory, "relazione_ca.docx")))
        using (var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open()))
        {
            var xml = System.Xml.Linq.XDocument.Parse(reader.ReadToEnd()); System.Xml.Linq.XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var blocks = xml.Root!.Element(w + "body")!.Elements().ToList();
            int firstImage = blocks.FindIndex(b => b.Descendants(w + "drawing").Any());
            int materialsHeading = blocks.FindIndex(b => b.Descendants(w + "t").Any(t => t.Value == "Materiali"));
            Assert(firstImage >= 0 && firstImage < materialsHeading && blocks.Count(b => b.Descendants(w + "drawing").Any()) >= 4, "Grafici inseriti nelle sezioni pertinenti");
            Assert(blocks.Any(b => b.Descendants(w + "t").Any(t => t.Value.Contains("governante tensioni"))), "Contouring report riferito alla combinazione governante");
        }
        Commit(); string archivePath = Path.Combine(directory, "ca_impostazioni.programma"); var archive = J.Obj(("formato", "X"), ("versione", 1), ("tipo", "calcolo"), ("modulo_id", "str_palo"), ("dati", Data)); Archivio.Scrivi(archivePath, archive);
        Assert(System.Text.Json.Nodes.JsonNode.DeepEquals(Archivio.Leggi(archivePath)["dati"], Data), "Salvataggio integrale opzioni, normativa, staffe, Excel e report");
        var reopened = new ConcreteWorkspace((JsonObject)Data.DeepClone()); reopened.Commit();
        Assert(reopened.settings.S("file_sollecitazioni") == settings.S("file_sollecitazioni") && reopened.ShearOptions.S("rami_x") == "4" && reopened.domainPanels.All(p => p.Options.B("colora_eta")), "Riapertura delle nuove impostazioni"); reopened.Dispose();
        owner.Width = 1366; owner.Height = 850;
        for (int i = 0; i < 5; i++) { tabs.SelectedIndex = i; await Capture("automatico_1366_tab_" + (i + 1)); }
        tabs.SelectedIndex = 3; sleTabs.SelectedIndex = 0;
        var summaryTab = Ui.Descendants<TabControl>(this).First(t => t.Items.OfType<TabItem>().Any(i => i.Header?.ToString() == "Riepilogo verifiche"));
        summaryTab.SelectedIndex = 1; await Capture("riepilogo_verifiche");
        tabs.SelectedIndex = 0;
        File.WriteAllText(Path.Combine(directory,"ca_workspace_tests.txt"),$"{checks} controlli WPF Checker superati.");
    }
}
