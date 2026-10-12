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
    void CombinationColumn(object sender,DataGridAutoGeneratingColumnEventArgs e)
    {
        if(e.PropertyName=="DefinitionId"){e.Cancel=true;return;}
        if(DataContext is ModelViewerViewModel vm&&vm.CombinationGridHeaders.TryGetValue(e.PropertyName,out var label))
        {e.Column.Header=label;e.Column.Width=110;}
        else e.Column.Width=e.PropertyName=="Nome"?190:145;
    }
}
