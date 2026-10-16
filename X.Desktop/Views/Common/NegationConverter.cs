using System.Globalization;
using System.Windows.Data;

namespace X.Desktop.Views.Common;

/// <summary>The opposite of a boolean, in both directions (for example IsReadOnly bound to a «can edit» state of the ViewModel).</summary>
internal sealed class NegationConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;
}
