using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace X.Desktop.Views.Common;

/// <summary>
/// Size that depends on a flag of the form (Compact, SymbolColumns): the parameter is «value when true|value when false», in
/// the invariant culture. Returns a <see cref="GridLength"/> for column widths and a number for the other sizes.
/// </summary>
internal sealed class FlagValueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string[] values = ((string)parameter).Split('|');
        double size = double.Parse(value is true ? values[0] : values[1], CultureInfo.InvariantCulture);
        return targetType == typeof(GridLength) ? new GridLength(size) : size;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
