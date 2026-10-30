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
            foreach(var scenario in ClimateScenarios.All().GroupBy(s=>s.Module).Select(g=>g.First()))
            {
                var factory=ModuleHostBench.SheetEditors([]);using var editor=factory.Open(scenario.Module,scenario.Data);
                Check(editor is MvvmSheetEditor,"Modulo climatico MVVM");var sheet=(ClimateSheetViewModel)((MvvmSheetEditor)editor).Sheet;
                var window=new Window{Content=editor.View,Width=1450,Height=1000,Left=-32000,Top=-32000,ShowActivated=false,ShowInTaskbar=false,WindowStyle=WindowStyle.None,WindowStartupLocation=WindowStartupLocation.Manual};Appearance.Watch(window);
                try
                {
                    window.Show();await editor.CalculateAsync();window.UpdateLayout();await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ContextIdle);
                    Check(sheet.HasResults,"Calcolo "+scenario.Module+": "+sheet.CalculationMessage);
                    Check(Visual(editor.View).OfType<ClimateSheetView>().Count()==1,"Template XAML risolto");
                    foreach(var theme in new[]{AppAppearance.Light,AppAppearance.Dark,AppAppearance.VeryDark})
                    {
                        Appearance.Set(theme,false);window.UpdateLayout();await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ContextIdle);
                        Check(Visual(editor.View).OfType<DataGrid>().Any(g=>g.IsVisible&&g.Items.Count>0),"Tabella visibile: "+theme);
                        var selector=Visual(editor.View).OfType<ComboBox>().Single(c=>c.DisplayMemberPath=="Name");selector.SelectedIndex=sheet.Cases.Count-1;
                        window.UpdateLayout();Check(sheet.SelectedCase==sheet.Cases.Count-1&&sheet.CaseValues.Count>0,"Selezione caso");
                        Check(Visual(editor.View).OfType<ClimateCaseChart>().Any(c=>c.IsVisible&&c.ActualHeight>50&&c.Values?.Count>0),"Diagramma con valori del caso");
                        File.WriteAllBytes(Path.Combine(output,scenario.Module+"-"+theme+".png"),Png(editor.View));
                    }
                    var tabs=Visual(editor.View).OfType<TabControl>().First(t=>t.Items.OfType<TabItem>().Any(i=>Equals(i.Header,"Schemi")));
                    tabs.SelectedItem=tabs.Items.OfType<TabItem>().Single(i=>Equals(i.Header,"Schemi"));window.UpdateLayout();await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ContextIdle);
                    Check(Visual(editor.View).OfType<Image>().Count(i=>i.IsVisible&&i.Source is BitmapSource b&&b.PixelWidth>1000)==sheet.Schematics.Count,"Schemi incorporati risolti");
                    File.WriteAllBytes(Path.Combine(output,scenario.Module+"-schemi.png"),Png(editor.View));tabs.SelectedIndex=0;
                    editor.InspectRevision();window.UpdateLayout();await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ContextIdle);
                    Check(sheet.Forms.All(f=>f.Form.IsReadOnly),"Revisione blocca tutti i moduli di input");
                    Check(Visual(editor.View).OfType<TextBox>().Where(t=>t.IsVisible).All(t=>t.IsReadOnly),"Campi effettivi in sola lettura");
                    window.Width=1000;window.UpdateLayout();await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ContextIdle);File.WriteAllBytes(Path.Combine(output,scenario.Module+"-stretta.png"),Png(editor.View));
                }
                finally{window.Content=null;window.Close();}
            }
            listener.Flush();Check(!bindings.ToString().Contains("Error:"),"Errori binding: "+bindings);
            File.WriteAllText(Path.Combine(output,"esito.txt"),$"PASS {checks} controlli climatici WPF, cinque moduli e tre temi");
        }
        finally{PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);listener.Dispose();Appearance.Set(AppAppearance.Light,false);}
    }
    private static IEnumerable<DependencyObject> Visual(DependencyObject root){yield return root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var c in Visual(VisualTreeHelper.GetChild(root,i)))yield return c;}
    private static byte[] Png(FrameworkElement view){var bitmap=new RenderTargetBitmap((int)view.ActualWidth,(int)view.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(view);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=new MemoryStream();encoder.Save(stream);return stream.ToArray();}
}
