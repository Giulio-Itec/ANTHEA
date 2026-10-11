using System.Globalization;
using System.Windows.Data;

namespace ANTHEA.ModelViewer.Wpf;

/// <summary>Pure layout: reserve room for the model when a lower details pane is open.</summary>
public sealed class ModelDrawerHeightConverter:IMultiValueConverter
{
    public object Convert(object[] values,Type targetType,object parameter,CultureInfo culture)
    {
        if(values.Length!=3 || values.Any(v=>v is not double d || !double.IsFinite(d)))return 460.0;
        return Math.Max(150,(double)values[0]-(double)values[1]-(double)values[2]-180);
    }
    public object[] ConvertBack(object value,Type[] targetTypes,object parameter,CultureInfo culture)=>throw new NotSupportedException();
}
