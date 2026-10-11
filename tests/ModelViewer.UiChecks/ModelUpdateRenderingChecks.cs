using System.Diagnostics;
using System.IO;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelViewer.Wpf;
using HelixToolkit.Wpf.SharpDX;

static partial class Program
{
    static void ModelUpdateRenderingChecks(ModelViewerControl control,Viewport3DX viewport,string output)
    {
        var state=(ModelViewerViewModel)control.DataContext;var current=state.Snapshot!;string before=current.Fingerprint();
        var button=Children<Button>(control).Single(b=>b.Command==state.OpenImportOptionsCommand);
        ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();Wait(100);
        Check(state.SelectedPanel==0 && state.ShowImportOptions && !state.IsNavigationCollapsed,"update toolbar opens the single import settings location in the model tab");
        var view=Children<ModelImportOptionsView>(control).Single();var modes=Children<ComboBox>(view).Single(c=>AutomationProperties.GetName(c)=="Modalità di aggiornamento");
        Check(modes.Items.Count==4 && modes.SelectedIndex==3,"all four update modes are available with full import selected initially");
        modes.SelectedIndex=2;Wait(50);Check(state.SelectedUpdateScope==2 && state.UpdateScopeDescription.Contains("coincidere"),"results-only choice exposes its compatibility requirement before reading a file");
        SetAppearance("VeryDark");state.ShowInspector=false;Wait(100);Capture(control,viewport,Path.Combine(output,"spalla-aggiornamento-risultati.png"));
        var watch=Stopwatch.StartNew();var results=ModelSnapshotUpdate.Apply(current,current,ModelUpdateScope.Results);double resultsMs=watch.Elapsed.TotalMilliseconds;
        Call(state,"SetSnapshot",results);state.SelectedUpdateScope=2;state.OpenImportOptionsCommand.Execute(null);Wait(100);
        Check(state.Cases.Count==49 && state.UpdateProvenance.Contains("compatibilità verificata") && state.HasResults,"complete compatible results preserve all 49 cases and display checked provenance");
        var geometry=ModelSnapshotUpdate.Apply(current,current,ModelUpdateScope.Geometry);Call(state,"SetSnapshot",geometry);
        state.SelectedUpdateScope=0;state.OpenImportOptionsCommand.Execute(null);state.SelectedRepresentation=1;Wait(150);
        Check(!state.ShowResults && !state.HasResults && state.Cases.Count==0 && !state.CanDeform,"geometry-only model cannot display stale forces or a stale deformed shape");
        Check(viewport.Items.OfType<MeshGeometryModel3D>().Any(m=>m.Geometry?.Positions?.Count>0) && !state.Status.StartsWith("Vista non disponibile"),"geometry-only data still render the actual plate and supported section volumes");
        Capture(control,viewport,Path.Combine(output,"spalla-sola-geometria.png"));
        Check(current.Fingerprint()==before,"all update previews retain the untouched complete imported copy");
        File.WriteAllText(Path.Combine(output,"tempi-compatibilita.json"),System.Text.Json.JsonSerializer.Serialize(new {Cases=current.Results.Length,Elements=current.Elements.Length,CompatibilityAndCompositionMs=resultsMs,InputAlreadyLoaded=true}));
        Call(state,"SetSnapshot",current);SetAppearance("Light");
    }
}
