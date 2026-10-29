using System.Windows;
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
        if (e.Column is DataGridBoundColumn { Binding: Binding binding })
        {
            // 1.16.0: a column name with a unit in brackets ("Fz [kN]", "X [m]") is not a property path: bind through the row indexer,
            // with the path characters escaped, or the cells stay empty.
            if (e.PropertyName.IndexOfAny(['[', ']', '.', '/', '(', ')', ',', '^']) >= 0) binding.Path = new PropertyPath("[" + Escape(e.PropertyName) + "]");
            if (e.PropertyType == typeof(double)) binding.StringFormat = "G9";
        }
    }
    static string Escape(string name) => name.Replace("^", "^^").Replace("[", "^[").Replace("]", "^]").Replace(",", "^,");
}
