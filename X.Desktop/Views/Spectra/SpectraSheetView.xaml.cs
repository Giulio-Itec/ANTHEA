using System.Data;
using System.Windows.Controls;
namespace X.Desktop.Views.Spectra;
public partial class SpectraSheetView : UserControl
{
    public SpectraSheetView() => InitializeComponent();
    private void ResultColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        if (((DataGrid)sender).ItemsSource is DataView rows)
            e.Column.Header = rows.Table?.Columns[e.PropertyName]?.Caption ?? e.PropertyName;
    }
}
