using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using ANTHEA.ModelReports;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelViewer.Wpf;
using HelixToolkit.Wpf.SharpDX;

static partial class Program
{
    static void CategoryColorRenderingChecks(ModelViewerControl control,Viewport3DX viewport,string output)
    {
        var state=(ModelViewerViewModel)control.DataContext;string before=state.Snapshot!.Fingerprint();
        state.ShowResults=false;state.SelectedRepresentation=1;state.ShowMesh=false;state.ShowInspector=false;state.SelectedColorMode=3;
        string key=state.ColorCategories.MaxBy(c=>c.Count)!.Key;state.EditColorCategoryCommand.Execute(key);Wait(150);
        var editor=Children<ModelDisplayOptionsView>(control).Single();
        var box=Children<ComboBox>(editor).Single(c=>AutomationProperties.GetName(c)=="Categoria da personalizzare");
        Check(box.SelectedValue?.ToString()==key,"category editor follows the category chosen from the legend command");
        state.CategoryColorText="#73C5AC";state.ApplyCategoryColorCommand.Execute(null);Wait(100);
        Check(state.SelectedColorCategoryKey==key && state.ElementColors.Where(c=>c.Value.Key==key).All(c=>c.Value.Color=="#73C5AC"),"WPF item refresh preserves the selected category and applies its custom color");
        var legend=Children<Button>(control).First(b=>b.Command==state.SelectColorCategoryCommand && b.CommandParameter?.ToString()==key && b.ContextMenu!=null);
        ((IInvokeProvider)new ButtonAutomationPeer(legend).GetPattern(PatternInterface.Invoke)).Invoke();Wait(80);
        Check(state.SelectedElementIds.Count==state.ColorCategories.Single(c=>c.Key==key).Count,"clicking the visible legend selects its category");
        var menu=legend.ContextMenu!;menu.PlacementTarget=legend;menu.IsOpen=true;Wait(80);
        var isolate=menu.Items.OfType<MenuItem>().Single(m=>m.Header?.ToString()=="Isola categoria");
        Check(isolate.Command==state.IsolateColorCategoryCommand && isolate.CommandParameter?.ToString()==key,"legend context menu resolves the correct viewmodel command and category");
        isolate.Command.Execute(isolate.CommandParameter);menu.IsOpen=false;state.ClearSelectionCommand.Execute(null);Wait(100);
        Check(state.ColorLegend.Count==1 && state.ColorCategories.Count>1,"isolation filters the legend without losing hidden categories in settings");
        state.ShowAllCommand.Execute(null);state.EditColorCategoryCommand.Execute(key);
        foreach(string theme in new[]{"Dark","VeryDark","Light"})
        {
            SetAppearance(theme);Wait(120);editor.BringIntoView();Wait(80);
            Capture(control,viewport,Path.Combine(output,"spalla-colori-"+theme+".png"));
        }
        state.ReportTitle="Spalla · colori delle proprietà";state.AddReportFigureCommand.Execute(null);
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var task=WpfModelViewerServices.ExportReportToAsync(state.CaptureReport(),state.Snapshot,Path.Combine(output,"colori.pdf"),null,timeout.Token);
        while(!task.IsCompleted)Wait(25);var report=task.GetAwaiter().GetResult();
        Check(File.ReadAllText(report.HtmlPath).Contains("#73C5AC",StringComparison.OrdinalIgnoreCase) && ModelReportFiles.Read(report.RecipePath).Blocks[0].View!.Appearance!.CategoryColors!.Any(c=>c.Key==key),"report contains the custom category swatch and the portable color recipe");
        Check(state.Snapshot.Fingerprint()==before,"category rendering and report leave source data unchanged");
    }
}
