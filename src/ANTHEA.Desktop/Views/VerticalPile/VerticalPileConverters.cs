using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace X.Desktop.Views.VerticalPile;

/// <summary>
/// Visible for an empty or blank text (the grey γ under an empty γsat, EmptyVisibility of StratigraphyTable), collapsed otherwise; with the
/// parameter «Invert» the opposite (the warnings of the sheet, shown only when there is a text).
/// </summary>
internal sealed class EmptyTextVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool empty = string.IsNullOrWhiteSpace(value as string);
        if (parameter as string == "Invert") empty = !empty;
        return empty ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Collapsed when the flag is true (for example «Invia / carica terreno…», only for the pile), visible otherwise.</summary>
internal sealed class CollapsedWhenConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
