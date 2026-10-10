using System.Windows.Controls;
using System.Windows.Data;
namespace ANTHEA.ModelViewer.Wpf;
public partial class ModelTableView : UserControl
{
    public ModelTableView() => InitializeComponent();
    private void GenerateColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        if (e.PropertyName == "ObjectKey") { e.Cancel = true; return; }
        e.Column.MinWidth = 70;
        if (e.PropertyType == typeof(double) && e.Column is DataGridTextColumn column && column.Binding is Binding binding) binding.StringFormat = "G9";
    }
}
