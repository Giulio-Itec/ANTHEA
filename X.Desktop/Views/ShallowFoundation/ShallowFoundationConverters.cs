using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Anthea.Application.Presentation;
using X.Desktop.Themes;

namespace X.Desktop.Views.ShallowFoundation;

/// <summary>Collapsed when the value is true (the inputs collapsed by «Raccogli input»), visible otherwise.</summary>
internal sealed class CollapsedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Visible when there is something to show: a value that is not null and, for a list, not empty (the graphics of a result, the
/// rows of the additive corrections).</summary>
internal sealed class PresentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        null => Visibility.Collapsed,
        string text => text.Length > 0 ? Visibility.Visible : Visibility.Collapsed,
        IEnumerable items => items.GetEnumerator().MoveNext() ? Visibility.Visible : Visibility.Collapsed,
        _ => Visibility.Visible
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Visible when the text is empty or blank: the value of γ shown in grey in an empty γsat cell (StratigraphyTable).</summary>
internal sealed class BlankVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>The explanation of a column of the layers (<see cref="CalculationHelp.Field"/>, the key in the parameter), as the tooltip of the
/// historic cells; null when there is none.</summary>
internal sealed class CalculationHelpConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) => parameter is string key ? CalculationHelp.Field(key) : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>
/// The swatch of a layer: the token Swatch.Soil1…6 of its index in the table, as StratigraphyDrawing.LayerColors[i % 6] of the historic
/// view (identity role: the same colour in every appearance). Values: the row, the rows of the table, the letter of the layer (only to be
/// evaluated again when the letters are renewed after an addition or a removal).
/// </summary>
internal sealed class SoilSwatchConverter : IMultiValueConverter
{
    private const int Swatches = 6;

    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[1] is not IList rows) return null;
        int index = rows.IndexOf(values[0]);
        return index < 0 ? null : ThemeTokens.Brush("Swatch.Soil" + (index % Swatches + 1).ToString(CultureInfo.InvariantCulture));
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
