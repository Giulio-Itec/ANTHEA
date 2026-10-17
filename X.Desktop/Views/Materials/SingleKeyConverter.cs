using System.Globalization;
using System.Windows.Data;

namespace X.Desktop.Views.Materials;

/// <summary>
/// A dictionary with one key (the parameter) mapped to the boolean value: a flag of the ViewModel given to FieldForm as the state of
/// one field (the name of the steel fixed by the catalogue, FieldForm.ReadOnlyKeys).
/// </summary>
internal sealed class SingleKeyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        new Dictionary<string, bool>(StringComparer.Ordinal) { [(string)parameter] = value is true };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
