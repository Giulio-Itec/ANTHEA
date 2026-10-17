using System.Globalization;
using System.Windows.Data;

namespace X.Desktop.Views.Materials;

/// <summary>
/// Widths of the concrete sheet from the width of its outer scroll viewer, as ResizeColumns of the historic view did at every resize:
/// the three columns share max(1140, width − 20) (parameter «sections»); the table of the properties is a third of it minus 62
/// (parameter «table»). Presentation only.
/// </summary>
internal sealed class ConcreteLayoutConverter : IValueConverter
{
    /// <summary>Minimum width of the three columns (MinWidth of the historic sections).</summary>
    internal const double Minimum = 1140;

    /// <summary>The margins of the sections (10 on each side).</summary>
    internal const double Margins = 20;

    /// <summary>Columns of the sheet: properties, exposure and cover, composition.</summary>
    internal const double Columns = 3;

    /// <summary>What the table leaves to the frame of its column, its card and the scroll bar.</summary>
    internal const double TableInset = 62;

    internal static double Sections(double available) => Math.Max(Minimum, (double.IsFinite(available) ? available : 0) - Margins);

    internal static double Table(double available) => Sections(available) / Columns - TableInset;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double available = value is double width ? width : 0;
        return parameter as string == "table" ? Table(available) : Sections(available);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
