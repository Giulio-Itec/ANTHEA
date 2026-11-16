using System.Globalization;
using System.Windows.Data;
using Anthea.Presentation.Modules.EmbeddedWalls;

namespace X.Desktop.Views.EmbeddedWalls;

/// <summary>
/// A number of the paratie module as <see cref="EmbeddedWallDisplay.Number(double?, string?)"/> shows it (cifre significative, 16/11): the
/// unit is the parameter, or the second value of a multi-binding (the column «Unità» of a check). The culture of the application, never the
/// culture of the binding (WPF would use en-US and ignore the decimal separator chosen in «Aspetto»). Display only.
/// </summary>
internal sealed class EmbeddedWallNumberConverter : IValueConverter, IMultiValueConverter
{
    /// <summary>A column of a table of the results: fixed decimals with the final zeros (approved on 16/11).</summary>
    public bool Table { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => EmbeddedWallDisplay.Number(Value(value), parameter as string, Table);

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        EmbeddedWallDisplay.Number(values.Length > 0 ? Value(values[0]) : null, values.Length > 1 ? values[1] as string : parameter as string, Table);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => [];

    private static double? Value(object? value) => value switch
    {
        double d => d,
        float f => f,
        int i => i,
        long l => l,
        decimal m => (double)m,
        _ => null
    };
}

/// <summary>The Italian label of a value of a result column (<see cref="EmbeddedWallDisplay.Term"/>; the parameter names the domain). Display only.</summary>
internal sealed class EmbeddedWallTermConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => EmbeddedWallDisplay.Term(parameter as string, value as string);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>The value of a field in the cells of the table «Dati e fasi» (<see cref="EmbeddedWallDisplay.Field"/>): the label of a choice,
/// a permeability in m/s as a power of ten; other texts as typed.</summary>
internal sealed class EmbeddedWallFieldConverter(Anthea.Presentation.Forms.FieldSpec field) : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => EmbeddedWallDisplay.Field(field, value as string ?? "");

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
