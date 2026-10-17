using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules.HorizontalPile;

namespace X.Desktop.Views.HorizontalPile;

/// <summary>
/// The soil editor of a row of the table of a stratigraphy (the cell of HorizontalSoilEditor in the historic view): values row, stratigraphy and
/// <see cref="HorizontalPileSurveyViewModel.SoilsVersion"/> (so that the cell follows the editors the sheet creates); null while the row has none.
/// </summary>
internal sealed class SoilOfRowConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values.Length >= 2 && values[1] is HorizontalPileSurveyViewModel survey ? survey.SoilOf(values[0] as JsonRowViewModel) : null;

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Collapsed when the flag is true, visible otherwise.</summary>
internal sealed class CollapsedWhenTrueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Collapsed for a null value, visible otherwise.</summary>
internal sealed class CollapsedWhenNullConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
