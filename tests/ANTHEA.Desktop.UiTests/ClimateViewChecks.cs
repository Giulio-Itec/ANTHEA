using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Anthea.Presentation.Modules.Climate;
using Anthea.Testing.Climate;
using X.Desktop.Views.Climate;

namespace X.Desktop;

internal static class ClimateViewChecks
{
    internal static async Task Run(string output)
    {
        Directory.CreateDirectory(output);int checks=0;
        void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);checks++;}
        var bindings=new StringWriter();var listener=new TextWriterTraceListener(bindings);PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        try
        {
            foreach(var scenario in ClimateScenarios.All().GroupBy(s=>s.Module).SelectMany(g=>g.Key is Anthea.Application.Actions.ClimateDocument.Distribution or Anthea.Application.Actions.ClimateDocument.Hydraulics or Anthea.Application.Actions.ClimateDocument.Construction or Anthea.Application.Actions.ClimateDocument.TimeEffects or Anthea.Application.Actions.ClimateDocument.Prestressing or Anthea.Application.Actions.ClimateDocument.Cranes or Anthea.Application.Actions.ClimateDocument.Fire?g:g.Take(1)))
            {
                string captureName=(scenario.Module is Anthea.Application.Actions.ClimateDocument.Hydraulics or Anthea.Application.Actions.ClimateDocument.Construction or Anthea.Application.Actions.ClimateDocument.TimeEffects or Anthea.Application.Actions.ClimateDocument.Prestressing or Anthea.Application.Actions.ClimateDocument.Cranes or Anthea.Application.Actions.ClimateDocument.Fire?scenario.File:scenario.Module)+(scenario.File=="ripartizione-deformabile"?"-deformabile":"");
                var factory=ModuleHostBench.SheetEditors([]);using var editor=factory.Open(scenario.Module,scenario.Data);
                Check(editor is MvvmSheetEditor,"Modulo climatico MVVM");var sheet=(ClimateSheetViewModel)((MvvmSheetEditor)editor).Sheet;
                var window=new Window{Content=editor.View,Width=1450,Height=1000,Left=-32000,Top=-32000,ShowActivated=false,ShowInTaskbar=false,WindowStyle=WindowStyle.None,WindowStartupLocation=WindowStartupLocation.Manual};Appearance.Watch(window);
                try
                {
                    window.Show();await editor.CalculateAsync();window.UpdateLayout();await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ContextIdle);
                    Check(sheet.HasResults,"Calcolo "+scenario.Module+": "+sheet.CalculationMessage);
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
                        Check(Visual(editor.View).OfType<DataGrid>().Any(g=>g.IsVisible&&g.Items.Count>0),"Tabella visibile: "+theme);
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
                            Check(Visual(editor.View).OfType<FireBoundaryChart>().Any(c=>c.IsVisible&&c.Drawing?.Faces.Count>0),"Facce e condizioni termiche disegnate");
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
                    Check(Visual(editor.View).OfType<Image>().Count(i=>i.IsVisible&&i.Source is BitmapSource b&&b.PixelWidth>1000)==sheet.Schematics.Count,"Schemi incorporati risolti");
                    File.WriteAllBytes(Path.Combine(output,captureName+"-schemi.png"),Png(editor.View));tabs.SelectedItem=tabs.Items.OfType<TabItem>().Single(i=>Equals(i.Header,"Casi di carico"));
                    editor.InspectRevision();window.UpdateLayout();await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ContextIdle);
                    Check(sheet.Forms.All(f=>f.Form.IsReadOnly),"Revisione blocca tutti i moduli di input");
                    Check(Visual(editor.View).OfType<TextBox>().Where(t=>t.IsVisible).All(t=>t.IsReadOnly),"Campi effettivi in sola lettura");
                    window.Width=1000;window.UpdateLayout();await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ContextIdle);File.WriteAllBytes(Path.Combine(output,captureName+"-stretta.png"),Png(editor.View));
                }
                finally{window.Content=null;window.Close();}
            }
            listener.Flush();Check(!bindings.ToString().Contains("Error:"),"Errori binding: "+bindings);
            File.WriteAllText(Path.Combine(output,"esito.txt"),$"PASS {checks} controlli Azioni WPF, tutti i moduli registrati e tre temi");
        }
        finally{PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);listener.Dispose();Appearance.Set(AppAppearance.Light,false);}
    }
    private static IEnumerable<DependencyObject> Visual(DependencyObject root){yield return root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var c in Visual(VisualTreeHelper.GetChild(root,i)))yield return c;}
    private static byte[] Png(FrameworkElement view){var bitmap=new RenderTargetBitmap((int)view.ActualWidth,(int)view.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(view);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=new MemoryStream();encoder.Save(stream);return stream.ToArray();}
}
