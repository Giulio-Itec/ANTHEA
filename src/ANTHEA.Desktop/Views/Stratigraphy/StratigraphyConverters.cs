using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace X.Desktop.Views.Stratigraphy;

/// <summary>The editable colour swatch stays visible also while an incomplete colour is being typed.</summary>
internal sealed class SoilColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // This is a colour supplied by the document, not a UI palette colour. Let WPF convert the
        // complete hex value; while it is being typed the binding uses the theme's fallback token.
        return value is string { Length: 7 } text && text[0] == '#' && text.Skip(1).All(Uri.IsHexDigit)
            ? text : DependencyProperty.UnsetValue;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>
/// The width of the two columns of the stratigraphy (F5.6b): the width of the viewport less the margins of the layout, at least the minimum
/// of the historic view («minimo|margini», 730 and 32), as the SizeChanged of the viewport of StratigraphyWorkspace set it. Below the minimum
/// the viewport scrolls horizontally.
/// </summary>
internal sealed class LayoutWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string[] parts = ((string)parameter).Split('|');
        double minimum = double.Parse(parts[0], CultureInfo.InvariantCulture), margins = double.Parse(parts[1], CultureInfo.InvariantCulture);
        return value is double width && double.IsFinite(width) ? Math.Max(minimum, width - margins) : minimum;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Visible when the value is not null (the blocks of the selected layer), collapsed otherwise; with «Invert» the opposite (the note of the missing minima).</summary>
internal sealed class NullVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool shown = value is not null;
        if (parameter as string == "Invert") shown = !shown;
        return shown ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>A tooltip only for a text that is not empty (the import button has none when the module enables the import).</summary>
internal sealed class TextToolTipConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is string { Length: > 0 } text ? text : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
