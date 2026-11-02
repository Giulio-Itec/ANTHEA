using System.Windows.Controls;
using ANTHEA.ModelViewer.Presentation;
namespace ANTHEA.ModelViewer.Wpf;
public partial class ModelCombinationsView:UserControl
{
    public ModelCombinationsView()=>InitializeComponent();
    void RevealSelection(object sender,System.Windows.RoutedEventArgs args)
    {
        if(sender is DataGrid grid&&grid.SelectedItem!=null)
            grid.Dispatcher.BeginInvoke(new Action(()=>{if(grid.IsVisible&&grid.SelectedItem!=null)grid.ScrollIntoView(grid.SelectedItem);}));
    }
    /// <summary>The rows selected in the list (Ctrl or Shift + click) become the combinations of «Calcola selezionate».</summary>
    void CombinationRowsChanged(object sender,SelectionChangedEventArgs args)
    {
        RevealSelection(sender,args);
        if(sender is DataGrid grid&&DataContext is ModelViewerViewModel vm)
            vm.SelectedCombinationIds=grid.SelectedItems.OfType<ModelWorkspace.ModelCombinationAvailability>().Select(c=>c.Definition.Id).ToArray();
    }
    void CombinationColumn(object sender,DataGridAutoGeneratingColumnEventArgs e)
    {
        if(e.PropertyName=="DefinitionId"){e.Cancel=true;return;}
        if(DataContext is ModelViewerViewModel vm&&vm.CombinationGridHeaders.TryGetValue(e.PropertyName,out var label))
        {e.Column.Header=label;e.Column.Width=110;}
        else e.Column.Width=e.PropertyName=="Nome"?190:145;
    }
}
