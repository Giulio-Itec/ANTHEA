using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Anthea.Application.Actions;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules.Climate;
using Anthea.Presentation.Modules.Rheology;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Commands;
using Anthea.Testing.Climate;
using X.Desktop.Services;
using X.Desktop.Views.Climate;

namespace X.Desktop;

internal static class ClimateViewChecks
{
    internal static async Task Run(string output)
    {
        Directory.CreateDirectory(output);int checks=0;
        void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);checks++;}
        // Outside the debugger the WPF trace sources stay off until Refresh: without it the listener heard nothing (review of 2/11/2026).
        var bindings=new StringWriter();var listener=new TextWriterTraceListener(bindings);
        PresentationTraceSources.Refresh();PresentationTraceSources.DataBindingSource.Listeners.Add(listener);PresentationTraceSources.DataBindingSource.Switch.Level=SourceLevels.Error;
        try
        {
            await BrokenBinding();listener.Flush();Check(bindings.ToString().Contains("Error:")&&bindings.ToString().Contains("Inesistente"),"La traccia dei binding cattura un errore voluto");bindings.GetStringBuilder().Clear();
            foreach(var scenario in ClimateScenarios.All().GroupBy(s=>s.Module).SelectMany(g=>g.Key is Anthea.Application.Actions.ClimateDocument.Distribution or Anthea.Application.Actions.ClimateDocument.Hydraulics or Anthea.Application.Actions.ClimateDocument.Construction or Anthea.Application.Actions.ClimateDocument.TimeEffects or Anthea.Application.Actions.ClimateDocument.Prestressing or Anthea.Application.Actions.ClimateDocument.Cranes or Anthea.Application.Actions.ClimateDocument.Fire?g:g.Take(1)))
            {
                string captureName=(scenario.Module is Anthea.Application.Actions.ClimateDocument.Hydraulics or Anthea.Application.Actions.ClimateDocument.Construction or Anthea.Application.Actions.ClimateDocument.TimeEffects or Anthea.Application.Actions.ClimateDocument.Prestressing or Anthea.Application.Actions.ClimateDocument.Cranes or Anthea.Application.Actions.ClimateDocument.Fire?scenario.File:scenario.Module)+(scenario.File=="ripartizione-deformabile"?"-deformabile":"");
                var factory=ModuleHostBench.SheetEditors([]);using var editor=factory.Open(scenario.Module,scenario.Data);
                Check(editor is MvvmSheetEditor,"Modulo climatico MVVM");var sheet=(ClimateSheetViewModel)((MvvmSheetEditor)editor).Sheet;
                var window=new Window{Content=editor.View,Width=1450,Height=1000,Left=-32000,Top=-32000,ShowActivated=false,ShowInTaskbar=false,WindowStyle=WindowStyle.None,WindowStartupLocation=WindowStartupLocation.Manual};Appearance.Watch(window);
                try
                {
                    window.Show();await sheet.PendingCalculation;await Idle(window);
                    // At opening, without «Calcola» (the shell has none): results and a visible tab, the tab of the module or «Casi di carico».
                    var opened=Visual(editor.View).OfType<TabControl>().First(t=>t.Items.OfType<TabItem>().Any(i=>Equals(i.Header,"Casi di carico")));
                    Check(sheet.HasResults,"Calcolo all'apertura senza «Calcola»: "+scenario.Module+" "+sheet.CalculationMessage);
                    Check(opened.SelectedItem is TabItem{Visibility:Visibility.Visible} first&&Equals(first.Header,sheet.ModuleTabTitle??"Casi di carico"),"Scheda visibile all'apertura: "+scenario.Module+" → "+(opened.SelectedItem as TabItem)?.Header);
                    Check(!sheet.HasCalculationNotice||sheet.Warnings.Count>0,"Nessun messaggio di calcolo con risultati senza avvisi");
                    await editor.CalculateAsync();window.UpdateLayout();await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ContextIdle);
                    Check(sheet.HasResults,"Calcolo "+scenario.Module+": "+sheet.CalculationMessage);
                    var focusField=sheet.Forms.SelectMany(f=>f.Form.Fields).Concat(sheet.Tables.Where(t=>t.Editor is not null).SelectMany(t=>t.Editor!.Fields)).First(f=>sheet.AssistanceSketch.Parts.Any(p=>p.Keys.Split(' ').Contains(f.Key)));
                    focusField.IsFocused=true;window.UpdateLayout();
                    Check(Visual(editor.View).OfType<ActionInputSketch>().Any(c=>c.IsVisible&&c.HighlightedParts.Count>0),"Campo attivo evidenziato nello schema comune");
                    focusField.IsFocused=false;
                    // Every field opens the guide of its sheet («?», F1); the numbers use the separator of the application (Virgola in the checks).
                    var allFields=sheet.Forms.SelectMany(f=>f.Form.Fields).Concat(sheet.Tables.Where(t=>t.Editor is not null).SelectMany(t=>t.Editor!.Fields)).ToArray();
                    Check(allFields.All(f=>f.HasHelp&&f.HelpTopic?.Modules.Contains(scenario.Module)==true)&&Visual(editor.View).OfType<Button>().Any(b=>Equals(b.Content,"?")&&b.IsVisible),"«?» della guida del foglio su ogni campo");
                    Check(allFields.Where(f=>f.Spec.Write==FieldWriteRule.NumberOrText).All(f=>!f.EditText.Contains('.')),"Numeri dei campi con la virgola: "+string.Join(" ",allFields.Where(f=>f.EditText.Contains('.')).Select(f=>f.Key+"="+f.EditText)));
                    Check(sheet.Tables.SelectMany(t=>t.Rows).All(r=>!System.Text.RegularExpressions.Regex.IsMatch(r.Name,@"\d\.\d")),"Nomi delle righe con la virgola");
                    await ChoiceKeepsFocus(window,editor.View,sheet,Check);
                    Check(Visual(editor.View).OfType<ClimateSheetView>().Count()==1,"Template XAML risolto");
                    var actionTabs=Visual(editor.View).OfType<TabControl>().First(t=>t.Items.OfType<TabItem>().Any(i=>Equals(i.Header,"Casi di carico")));
                    if(sheet.IsDistribution)
                    {
                        actionTabs.SelectedItem=actionTabs.Items.OfType<TabItem>().Single(i=>Equals(i.Header,"Impalcato e influenza"));window.UpdateLayout();
                        Check(Visual(editor.View).OfType<TrafficDistributionChart>().Any(c=>c.IsVisible&&c.Drawing?.Influence.Count>0),"Pianta, sezione e influenza con dati calcolati");
                        File.WriteAllBytes(Path.Combine(output,captureName+"-impalcato.png"),Png(editor.View));
                    }
                    actionTabs.SelectedItem=actionTabs.Items.OfType<TabItem>().Single(i=>Equals(i.Header,"Casi di carico"));window.UpdateLayout();
                    foreach(var theme in new[]{AppAppearance.Light,AppAppearance.Dark,AppAppearance.VeryDark})
                    {
                        Appearance.Set(theme,false);window.UpdateLayout();await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ContextIdle);
                        Check(Visual(editor.View).OfType<ActionInputSketch>().Any(c=>c.IsVisible&&c.ActualHeight>100&&c.Drawing?.Parts.Count>0),"Assistenza visibile: "+theme);
                        Check(Visual(editor.View).OfType<DataGrid>().Any(g=>g.IsVisible&&g.Items.Count>0),"Tabella visibile: "+theme+" "+scenario.File+" risultati="+sheet.HasResults+" casi="+sheet.Cases.Count+" caso="+sheet.SelectedCase+" "+sheet.CalculationMessage);
                        var selector=Visual(editor.View).OfType<ComboBox>().Single(c=>c.IsVisible&&Equals(c.Tag,"ActionCases"));selector.SelectedIndex=sheet.Cases.Count-1;
                        window.UpdateLayout();Check(sheet.SelectedCase==sheet.Cases.Count-1&&sheet.CaseValues.Count>0,"Selezione caso");
                        Check(Visual(editor.View).OfType<ClimateCaseChart>().Any(c=>c.IsVisible&&c.ActualHeight>50&&c.Values?.Count>0),"Diagramma con valori del caso");
                        File.WriteAllBytes(Path.Combine(output,captureName+"-"+theme+".png"),Png(editor.View));
                        if(sheet.IsPrestressing)
                        {
                            actionTabs.SelectedItem=actionTabs.Items.OfType<TabItem>().Single(i=>Equals(i.Header,"Profilo e perdite"));window.UpdateLayout();await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ContextIdle);
                            Check(Visual(editor.View).OfType<ActionHistoryChart>().Count(c=>c.Drawing?.Series.Count>0)==4,"Profilo e curve delle perdite");
                            var stations=Visual(editor.View).OfType<ComboBox>().Single(c=>Equals(c.Tag,"PrestressStations"));stations.SelectedIndex=sheet.PrestressStations.Count/2;window.UpdateLayout();
                            Check(sheet.SelectedStation?.Index==sheet.PrestressStations.Count/2,"Selezione dell’ascissa dello stato efficace");
                            File.WriteAllBytes(Path.Combine(output,captureName+"-profilo-"+theme+".png"),Png(editor.View));
                            if(scenario.File=="precompressione-posteso")
                            {
                                var dialog=sheet.CreatePrestressTransferDialog(ClimateScenarios.PrestressTargetSection());var content=new ContentControl{Content=dialog};
                                var modal=new Window{Content=content,Width=820,Height=470,Left=-32000,Top=-32000,ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual};Appearance.Watch(modal);
                                try
                                {
                                    modal.Show();modal.UpdateLayout();await modal.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ContextIdle);
                                    Check(Visual(content).OfType<PrestressTransferDialogView>().Count()==1,"Dialogo MVVM del trasferimento risolto");
                                    var consent=Visual(content).OfType<CheckBox>().Single();consent.IsChecked=true;modal.UpdateLayout();Check(dialog.ConfirmCommand.CanExecute(null),"Associazione esplicita della sezione nel dialogo");
                                    File.WriteAllBytes(Path.Combine(output,captureName+"-trasferimento-"+theme+".png"),Png(content));
                                }
                                finally{modal.Content=null;modal.Close();}
                            }
                            actionTabs.SelectedItem=actionTabs.Items.OfType<TabItem>().Single(i=>Equals(i.Header,"Casi di carico"));window.UpdateLayout();
                        }
                        if(sheet.IsTimeEffects)
                        {
                            actionTabs.SelectedItem=actionTabs.Items.OfType<TabItem>().Single(i=>Equals(i.Header,"Evoluzione nel tempo"));window.UpdateLayout();await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ContextIdle);
                            Check(Visual(editor.View).OfType<ActionHistoryChart>().Count(c=>c.Drawing?.Series.Count>0)==4,"Quattro grafici temporali con dati calcolati");
                            File.WriteAllBytes(Path.Combine(output,captureName+"-tempo-"+theme+".png"),Png(editor.View));
                            actionTabs.SelectedItem=actionTabs.Items.OfType<TabItem>().Single(i=>Equals(i.Header,"Casi di carico"));window.UpdateLayout();
                        }
                        if(sheet.IsFire)
                        {
                            sheet.SelectedCase=sheet.Cases.Count-1;actionTabs.SelectedItem=actionTabs.Items.OfType<TabItem>().Single(i=>Equals(i.Header,"Incendio e facce"));window.UpdateLayout();await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ContextIdle);
                            Check(Visual(editor.View).OfType<FireBoundaryChart>().Any(c=>c.IsVisible&&c.Drawing?.Faces.Count>0&&c.SelectedFace==sheet.ActiveFaceIndex),"Facce e condizioni termiche disegnate");
                            Check(Visual(editor.View).OfType<ActionHistoryChart>().Any(c=>c.Drawing?.Series.Count>0),"Curva termica con dati calcolati");File.WriteAllBytes(Path.Combine(output,captureName+"-facce-"+theme+".png"),Png(editor.View));
                            var fireScroll=Visual(actionTabs.SelectedContent as DependencyObject??editor.View).OfType<ScrollViewer>().FirstOrDefault();if(fireScroll is not null){fireScroll.ScrollToBottom();window.UpdateLayout();File.WriteAllBytes(Path.Combine(output,captureName+"-flussi-"+theme+".png"),Png(editor.View));}
                            actionTabs.SelectedItem=actionTabs.Items.OfType<TabItem>().Single(i=>Equals(i.Header,"Casi di carico"));window.UpdateLayout();
                        }
                        if(sheet.IsCranes)
                        {
                            sheet.SelectedCase=0;actionTabs.SelectedItem=actionTabs.Items.OfType<TabItem>().Single(i=>Equals(i.Header,"Vie di corsa"));window.UpdateLayout();await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ContextIdle);
                            Check(Visual(editor.View).OfType<CraneActionsChart>().Any(c=>c.IsVisible&&c.Drawing?.Points.Count>=4),"Pianta delle ruote e azioni del gruppo");
                            File.WriteAllBytes(Path.Combine(output,captureName+"-vie-"+theme+".png"),Png(editor.View));actionTabs.SelectedItem=actionTabs.Items.OfType<TabItem>().Single(i=>Equals(i.Header,"Casi di carico"));window.UpdateLayout();
                        }
                        if(sheet.IsConstruction)
                        {
                            sheet.SelectedCase=0;
                            actionTabs.SelectedItem=actionTabs.Items.OfType<TabItem>().Single(i=>Equals(i.Header,"Fasi e pianta"));window.UpdateLayout();await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ContextIdle);
                            Check(Visual(editor.View).OfType<ConstructionActionsChart>().Any(c=>c.IsVisible&&c.Drawing?.Phases.Count==2&&c.Drawing.Loads.Count>0),"Sequenza e pianta dei carichi attivi");
                            File.WriteAllBytes(Path.Combine(output,captureName+"-fasi-"+theme+".png"),Png(editor.View));
                            actionTabs.SelectedItem=actionTabs.Items.OfType<TabItem>().Single(i=>Equals(i.Header,"Casi di carico"));window.UpdateLayout();
                        }
                        if(sheet.IsHydraulics)
                        {
                            actionTabs.SelectedItem=actionTabs.Items.OfType<TabItem>().Single(i=>Equals(i.Header,"Schema e pressioni"));window.UpdateLayout();await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ContextIdle);window.UpdateLayout();
                            Check(Visual(editor.View).OfType<HydraulicActionsChart>().Any(c=>c.IsVisible&&c.Drawing?.Points.Count>1),"Schema e pressioni idrauliche");
                            Check(Visual(editor.View).OfType<ComboBox>().Single(c=>c.IsVisible&&Equals(c.Tag,"ActionCases")).SelectedIndex==sheet.SelectedCase,"Il selettore dello schema conserva il caso visibile");
                            var hydraulicSelector=Visual(editor.View).OfType<ComboBox>().Single(c=>c.IsVisible&&Equals(c.Tag,"ActionCases"));
                            Check(Visual(hydraulicSelector).OfType<TextBlock>().Any(t=>t.IsVisible&&t.Text==sheet.Cases[sheet.SelectedCase].Name),"Nome nel selettore: modulo="+scenario.File+", indice="+hydraulicSelector.SelectedIndex+", item="+hydraulicSelector.SelectedItem+", box="+hydraulicSelector.SelectionBoxItem+", text="+hydraulicSelector.Text+", visual="+string.Join(" | ",Visual(hydraulicSelector).OfType<TextBlock>().Select(t=>t.Text+"/"+t.IsVisible+"/"+t.DataContext)));
                            File.WriteAllBytes(Path.Combine(output,captureName+"-pressioni-"+theme+".png"),Png(editor.View));
                            actionTabs.SelectedItem=actionTabs.Items.OfType<TabItem>().Single(i=>Equals(i.Header,"Casi di carico"));window.UpdateLayout();
                        }
                        if(sheet.IsDistribution)
                        {
                            actionTabs.SelectedItem=actionTabs.Items.OfType<TabItem>().Single(i=>Equals(i.Header,"Impalcato e influenza"));window.UpdateLayout();
                            File.WriteAllBytes(Path.Combine(output,captureName+"-impalcato-"+theme+".png"),Png(editor.View));
                            actionTabs.SelectedItem=actionTabs.Items.OfType<TabItem>().Single(i=>Equals(i.Header,"Casi di carico"));window.UpdateLayout();
                        }
                    }
                    var tabs=Visual(editor.View).OfType<TabControl>().First(t=>t.Items.OfType<TabItem>().Any(i=>Equals(i.Header,"Schemi")));
                    tabs.SelectedItem=tabs.Items.OfType<TabItem>().Single(i=>Equals(i.Header,"Schemi"));window.UpdateLayout();await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ContextIdle);
                    var reference=Visual(editor.View).OfType<Expander>().Single(e=>Equals(e.Header,"Convenzioni di riferimento"));reference.IsExpanded=true;window.UpdateLayout();
                    Check(Visual(editor.View).OfType<ActionReferenceSketch>().Count(i=>i.IsVisible&&i.PrimitiveCount>10)==sheet.Schematics.Count,"Schemi vettoriali incorporati risolti");
                    foreach(var theme in new[]{AppAppearance.Light,AppAppearance.Dark,AppAppearance.VeryDark})
                    {
                        Appearance.Set(theme,false);
                        window.UpdateLayout();
                        Check(Visual(editor.View).OfType<ActionReferenceSketch>().All(v=>v.PrimitiveCount>10),"Figure originali vettoriali nel tema "+theme);
                        int number=0;
                        foreach(var figure in Visual(editor.View).OfType<ActionReferenceSketch>())File.WriteAllBytes(Path.Combine(output,captureName+"-figura-"+(number++)+"-"+theme+".png"),Png(figure));
                        File.WriteAllBytes(Path.Combine(output,captureName+"-riferimenti-"+theme+".png"),Png(editor.View));
                    }
                    File.WriteAllBytes(Path.Combine(output,captureName+"-schemi.png"),Png(editor.View));tabs.SelectedItem=tabs.Items.OfType<TabItem>().Single(i=>Equals(i.Header,"Casi di carico"));
                    // The minimum sheet of the shell (SheetLayout 1120 × 600): the assistance shrinks, the table of the case keeps a usable height.
                    window.Width=1120;window.Height=600;actionTabs.SelectedItem=actionTabs.Items.OfType<TabItem>().Single(i=>Equals(i.Header,"Casi di carico"));await Idle(window);
                    var smallGrid=Visual(editor.View).OfType<DataGrid>().First(g=>g.IsVisible);var smallSketch=Visual(editor.View).OfType<ActionInputSketch>().First(c=>c.IsVisible);
                    // The table of the case shows all its rows, or at least 90 px (header and three rows) when it is longer.
                    var rowsScroll=Visual(smallGrid).OfType<ScrollViewer>().First();
                    Check(sheet.IsAssistanceExpanded&&(rowsScroll.ScrollableHeight<=0||smallGrid.ActualHeight>=90)&&smallSketch.ActualHeight>=50,
                        "Area minima 1120×600: tabella dei casi "+smallGrid.ActualHeight.ToString("0")+" px (da scorrere "+rowsScroll.ScrollableHeight.ToString("0")+"), schema "+smallSketch.ActualHeight.ToString("0")+" px");
                    File.WriteAllBytes(Path.Combine(output,captureName+"-minima.png"),Png(editor.View));
                    window.Width=1450;window.Height=1000;await Idle(window);
                    editor.InspectRevision();window.UpdateLayout();await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ContextIdle);
                    Check(sheet.Forms.All(f=>f.Form.IsReadOnly),"Revisione blocca tutti i moduli di input");
                    Check(Visual(editor.View).OfType<TextBox>().Where(t=>t.IsVisible).All(t=>t.IsReadOnly),"Campi effettivi in sola lettura");
                    window.Width=1000;window.UpdateLayout();await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ContextIdle);File.WriteAllBytes(Path.Combine(output,captureName+"-stretta.png"),Png(editor.View));
                }
                finally{window.Content=null;window.Close();}
            }
            await ShellChecks(output,Check);
            await RheologyChecks(output,Check);
            listener.Flush();Check(!bindings.ToString().Contains("Error:"),"Errori binding: "+bindings);
            File.WriteAllText(Path.Combine(output,"esito.txt"),$"PASS {checks} controlli Azioni WPF, tutti i moduli registrati e tre temi");
        }
        finally{PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);listener.Dispose();Appearance.Set(AppAppearance.Light,false);}
    }
    /// <summary>A binding to a missing property, to prove that the trace of the bindings hears the errors.</summary>
    private static async Task BrokenBinding()
    {
        var text=new TextBlock{DataContext=new object()};text.SetBinding(TextBlock.TextProperty,new Binding("Inesistente"));
        var window=new Window{Content=text,Width=200,Height=100,Left=-32000,Top=-32000,ShowActivated=false,ShowInTaskbar=false,WindowStyle=WindowStyle.None,WindowStartupLocation=WindowStartupLocation.Manual};
        try{window.Show();await Idle(window);}finally{window.Content=null;window.Close();}
    }
    private static async Task Idle(Window window){window.UpdateLayout();await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ContextIdle);await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);}
    /// <summary>A choice rebuilds the forms of the sheet: the focus (logical, the window is never activated) comes back to the same choice.</summary>
    private static async Task ChoiceKeepsFocus(Window window,FrameworkElement view,ClimateSheetViewModel sheet,Action<bool,string> check)
    {
        var choice=sheet.Forms.SelectMany(f=>f.Form.Fields).FirstOrDefault(f=>f.Kind==FieldKind.Choice&&f.Choices.Count>1&&f.CanChange);
        if(choice is null||Visual(view).OfType<ComboBox>().FirstOrDefault(c=>c.DataContext==choice&&c.IsVisible) is not { } box)return;
        FocusManager.SetFocusedElement(window,box);
        string before=choice.SelectedChoice!;var forms=sheet.Forms;
        choice.SelectedChoice=choice.Choices.First(c=>c!=before);await Idle(window);
        check(!ReferenceEquals(forms,sheet.Forms)&&FocusManager.GetFocusedElement(window) is FrameworkElement{IsVisible:true,DataContext:FieldViewModel focused}&&focused.Key==choice.Key&&!ReferenceEquals(focused,choice),
            "Il focus resta sulla scelta dopo la ricostruzione dei moduli: "+choice.Label);
        sheet.Forms.SelectMany(f=>f.Form.Fields).First(f=>f.Key==choice.Key).SelectedChoice=before;await sheet.PendingCalculation;
        check(await sheet.CalculateAsync()&&sheet.HasResults,"Risultati dopo il ripristino della scelta: "+sheet.CalculationMessage);await Idle(window);
    }
    /// <summary>
    /// The Actions sheets in the shell of the product (review of the interface, 2/11/2026). The shell shows all of them with one view: the tab and the
    /// assistance belong to each sheet. A calculation without results says why on screen. A text in a number field is shown under the field and the
    /// shell still changes tab.
    /// </summary>
    private static async Task ShellChecks(string output,Action<bool,string> check)
    {
        var services=new ShellDraftChecks.DraftServices();
        var parts=ShellComposition.Create(ShellComposition.WithShellWiki(services.Create()),DesktopComposition.CreateModules(),new Anthea.Presentation.Calculation.CalculationLimiter(Anthea.Presentation.Calculation.CalculationLimiter.DefaultMaximum),
            ShellComposition.ExamplesFolder,window=>new ShellDraftChecks.QuietWindow(ShellDraftChecks.Offscreen(window)));
        var main=ShellDraftChecks.Offscreen(parts.Window);main.Width=1500;main.Height=1000;main.Show();
        try
        {
            var workspace=parts.Shell.Workspace;
            async Task<ClimateSheetViewModel> Open(SheetSession session){var sheet=(ClimateSheetViewModel)session.ViewModel;await Idle(main);await sheet.PendingCalculation;await Idle(main);return sheet;}
            TabControl Tabs()=>Visual(Visual(main).OfType<ClimateSheetView>().Single()).OfType<TabControl>().First(t=>t.Items.OfType<TabItem>().Any(i=>Equals(i.Header,"Casi di carico")));
            string Selected()=>Tabs().SelectedItem is TabItem{Visibility:Visibility.Visible} tab?tab.Header as string??"":"(nessuna)";
            Expander Assistance()=>Visual(main).OfType<Expander>().Single(e=>Equals(e.Header,"Assistenza al dato · schema e selezione"));
            FrameworkElement Client()=>(FrameworkElement)VisualTreeHelper.GetChild(main,0);
            var hydraulicSession=workspace.NewCalculation(ClimateDocument.Hydraulics);var hydraulic=await Open(hydraulicSession);
            check(hydraulic.HasResults&&Selected()=="Schema e pressioni","Shell: idraulica calcolata all'apertura, con la scheda dello schema");
            Assistance().IsExpanded=false;await Idle(main);
            var snowSession=workspace.NewCalculation(ClimateDocument.Snow);var snow=await Open(snowSession);
            var shared=Visual(main).OfType<ClimateSheetView>().Single();
            check(snow.HasResults&&Selected()=="Casi di carico"&&snow.IsAssistanceExpanded&&Assistance().IsExpanded,"Shell: neve dopo l'idraulica con la propria scheda e l'assistenza aperta ("+Selected()+")");
            File.WriteAllBytes(Path.Combine(output,"shell-neve-dopo-idraulica.png"),Png(Client()));
            Tabs().SelectedItem=Tabs().Items.OfType<TabItem>().Single(i=>Equals(i.Header,"Parametri e formule"));await Idle(main);
            workspace.Activate(hydraulicSession);await Idle(main);
            check(Selected()=="Schema e pressioni"&&!hydraulic.IsAssistanceExpanded&&!Assistance().IsExpanded,"Shell: l'idraulica conserva la sua scheda e l'assistenza chiusa ("+Selected()+")");
            workspace.Activate(snowSession);await Idle(main);
            check(Selected()=="Parametri e formule"&&ReferenceEquals(shared,Visual(main).OfType<ClimateSheetView>().Single()),"Shell: la neve conserva la scheda scelta nella vista condivisa ("+Selected()+")");
            // The default fire scenario asks its sources: no results, and the message says so on screen.
            var fire=await Open(workspace.NewCalculation(ClimateDocument.Fire));
            check(!fire.HasResults&&fire.CalculationMessage.Length>0&&Visual(main).OfType<TextBlock>().Any(t=>t.IsVisible&&t.Text.Contains(fire.CalculationMessage)),"Shell: messaggio del calcolo senza risultati visibile: "+fire.CalculationMessage);
            File.WriteAllBytes(Path.Combine(output,"shell-incendio-messaggio.png"),Png(Client()));
            // A text in a number field: error under the field and message with its label; the sheet is confirmed and the shell changes tab.
            workspace.Activate(snowSession);await Idle(main);
            var altitude=snow.Forms.SelectMany(f=>f.Form.Fields).First(f=>f.Key=="AltitudeM");string before=altitude.Text;
            altitude.IsFocused=true;altitude.EditText="abc";altitude.IsFocused=false;await snow.PendingCalculation;await Idle(main);
            check(!snow.HasResults&&altitude.Error is not null&&snow.CalculationMessage.Contains(altitude.Label)&&Visual(main).OfType<TextBlock>().Any(t=>t.IsVisible&&t.Text==altitude.Error),
                "Shell: testo in un campo numerico segnalato sul campo e nel messaggio: "+snow.CalculationMessage);
            File.WriteAllBytes(Path.Combine(output,"shell-neve-testo.png"),Png(Client()));
            workspace.Activate(hydraulicSession);await Idle(main);
            check(ReferenceEquals(workspace.Active,hydraulicSession)&&services.Failures.Count==0,"Shell: cambio di scheda con un testo nel campo numerico "+string.Join(" | ",services.Failures));
            workspace.Activate(snowSession);await Idle(main);
            altitude=snow.Forms.SelectMany(f=>f.Form.Fields).First(f=>f.Key=="AltitudeM");altitude.IsFocused=true;altitude.EditText=before;altitude.IsFocused=false;await snow.PendingCalculation;await Idle(main);
            check(snow.HasResults&&altitude.Error is null,"Shell: il numero ripristina i risultati");
        }
        finally{main.Close();}
    }
    /// <summary>
    /// «Viscosità e maturazione» (review of 2/11/2026): on the minimum sheet of the shell the plot keeps its height (the column scrolls); the values
    /// follow the separator of «Aspetto»; a revision makes every text box read-only.
    /// </summary>
    private static async Task RheologyChecks(string output,Action<bool,string> check)
    {
        string module=Anthea.Application.Materials.RheologyDocument.Module;
        using var editor=ModuleHostBench.SheetEditors([]).Open(module,Anthea.Application.Modules.ModuleDirectory.CreateData(module));
        var sheet=(RheologySheetViewModel)((MvvmSheetEditor)editor).Sheet;
        var window=new Window{Content=editor.View,Width=1120,Height=600,Left=-32000,Top=-32000,ShowActivated=false,ShowInTaskbar=false,WindowStyle=WindowStyle.None,WindowStartupLocation=WindowStartupLocation.Manual};Appearance.Watch(window);
        try
        {
            window.Show();await Idle(window);
            var plot=Visual(editor.View).OfType<ContentControl>().Single(c=>c.Name=="PlotHost");
            check(plot.ActualHeight>=200,"Reologia 1120×600: area del grafico "+plot.ActualHeight.ToString("0")+" px");
            File.WriteAllBytes(Path.Combine(output,"reologia-minima.png"),Png(editor.View));
            WpfNumberSeparatorService.Apply(DecimalSeparatorChoice.Point);
            try
            {
                sheet.Model.CalculateCommand.Execute(null);await Idle(window);
                check(System.Text.RegularExpressions.Regex.IsMatch(sheet.Model.E28Text,@"^\d+ MPa$")&&sheet.Model.InfinityText.Contains('.')&&sheet.Model.Points.All(p=>!p.CreepText.Contains(',')&&!p.EcmText.Contains(',')),
                    "Reologia con il separatore Punto: "+sheet.Model.E28Text+", "+sheet.Model.InfinityText);
                File.WriteAllBytes(Path.Combine(output,"reologia-punto.png"),Png(editor.View));
            }
            finally{WpfNumberSeparatorService.Apply(DecimalSeparatorChoice.Comma);sheet.Model.CalculateCommand.Execute(null);await Idle(window);}
            check(sheet.Model.InfinityText.Contains(',')&&!sheet.Model.E28Text.Contains('.'),"Reologia con il separatore Virgola: "+sheet.Model.E28Text);
            editor.InspectRevision();await Idle(window);
            check(Visual(editor.View).OfType<TextBox>().Where(t=>t.IsVisible).All(t=>t.IsReadOnly),"Reologia: caselle in sola lettura nella revisione");
        }
        finally{window.Content=null;window.Close();}
    }
    private static IEnumerable<DependencyObject> Visual(DependencyObject root){yield return root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var c in Visual(VisualTreeHelper.GetChild(root,i)))yield return c;}
    private static byte[] Png(FrameworkElement view){var bitmap=new RenderTargetBitmap((int)view.ActualWidth,(int)view.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(view);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=new MemoryStream();encoder.Save(stream);return stream.ToArray();}
}
