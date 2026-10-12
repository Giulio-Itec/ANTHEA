using System.Windows;
using System.IO;
using System.Windows.Automation;
using System.Windows.Controls;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelViewer.Wpf;
using HelixToolkit.Wpf.SharpDX;

partial class Program
{
    static void CombinationRenderingChecks(ModelViewerControl view,Viewport3DX scene,string output)
    {
        var state=(ModelViewerViewModel)view.DataContext;
        state.ShowInspector=false;state.IsNavigationCollapsed=true;
        state.OpenCombinationManagerCommand.Execute(null);state.NewCombinationCommand.Execute(null);Wait(200);
        var tabs=Children<TabControl>(view).Single(t=>AutomationProperties.GetName(t)=="Schede del modello");
        Check(tabs.Items.OfType<TabItem>().Select(t=>t.Header.ToString()).SequenceEqual(new[]{"Vista","Modello","Combinazioni","Risultati","Verifiche","Report"}),"left tabs follow the requested order with stable panel identities");
        Check(((TabItem)tabs.SelectedItem).Header.ToString()=="Combinazioni"&&!state.IsNavigationCollapsed,"combination manager opens inside the left navigation tab");
        var name=Children<TextBox>(view).Single(c=>AutomationProperties.GetName(c)=="Nome combinazione");
        name.Text="Combinazione di prova";Wait(100);
        Check(state.CombinationName==name.Text&&state.IsCombinationDirty,"WPF name editor updates the MVVM draft");
        var editor=Children<DataGrid>(view).Single(g=>AutomationProperties.GetName(g)=="Termini combinazione");
        Check(editor.Items.Count==1&&editor.ActualHeight>100,"term editor is visible and has working space");
        state.AddCombinationTermCommand.Execute(null);state.SelectedCombinationTerm=state.CombinationTerms[1];state.RemoveCombinationTermCommand.Execute(null);
        state.SaveCombinationCommand.Execute(null);Wait(150);
        Check(state.Combinations.Count==1&&!state.IsCombinationDirty,"combination saved through the production UI commands");
        Check(!state.CalculateCombinationCommand.CanExecute(null)&&state.CombinationOrigin.Contains("Sorgente incompleta"),"incomplete source has an explanatory state and cannot run GPC");
        foreach(string theme in new[]{"Light","Dark","VeryDark"})
        {SetAppearance(theme);Wait(300);Capture(view,scene,Path.Combine(output,"combinazioni-"+theme+".png"));}
        state.ShowCombinationGrid=true;state.ToggleCombinationWindowCommand.Execute(null);Wait(250);
        var detached=Application.Current.Windows.OfType<Window>().Single(w=>w.Title=="ANTHEA · Combinazioni");
        var matrix=Children<DataGrid>(detached).Single(g=>AutomationProperties.GetName(g)=="Griglia coefficienti combinazioni");
        Check(state.IsCombinationDetached&&!state.ShowCombinationManager&&ReferenceEquals(((ModelCombinationsView)detached.Content).DataContext,state),"detached window shares the same MVVM state and releases the dock");
        Check(matrix.FrozenColumnCount==3&&matrix.Columns.Any(c=>c.Header.ToString()!.Contains("SLU 01"))&&matrix.Items.Count==1,"Excel-style grid has source headers and frozen identity columns");
        CapturePage((FrameworkElement)detached.Content,Path.Combine(output,"combinazioni-griglia-staccata.png"));
        detached.Close();Wait(200);
        Check(!state.IsCombinationDetached&&state.ShowCombinationManager,"closing detached window redocks the existing view without losing model state");
        state.ShowCombinationGrid=false;
        double width=scene.ActualWidth;state.CloseCombinationManagerCommand.Execute(null);Wait(200);
        Check(!state.ShowCombinationManager&&scene.ActualWidth>width+400,"combination panel collapses and releases the viewport width");
        state.OpenCombinationManagerCommand.Execute(null);state.DuplicateCombinationCommand.Execute(null);Wait(100);
        Check(state.IsCombinationDirty&&state.CombinationName.Contains("copia"),"duplicate opens an independent draft");
        state.DiscardCombinationEditsCommand.Execute(null);state.DeleteCombinationCommand.Execute(null);
        Check(state.Combinations.Count==0,"saved custom combination can be deleted from the view");
    }
}
