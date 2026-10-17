using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules.RetainingWall;

namespace X.Desktop.Views.RetainingWall;

/// <summary>
/// The form of a key (the parameter) among <see cref="RetainingWallSheetViewModel.Forms"/>, null when the sheet has none (the form of the
/// selected action, of a deep layer): the binding has the dictionary as its leaf, so that every notification of Forms reads it again.
/// </summary>
internal sealed class WallFormConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is IReadOnlyDictionary<string, FormViewModel> forms && parameter is string key && forms.TryGetValue(key, out var form) ? form : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>The form of the key given by the second value (the accelerogram of an item), as <see cref="WallFormConverter"/>.</summary>
internal sealed class WallFormOfConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values.Length == 2 && values[0] is IReadOnlyDictionary<string, FormViewModel> forms && values[1] is string key && forms.TryGetValue(key, out var form) ? form : null;

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Visible when the sheet has the form of the key (the parameter), collapsed otherwise (the details of the action, of a deep layer).</summary>
internal sealed class WallFormPresentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is IReadOnlyDictionary<string, FormViewModel> forms && parameter is string key && forms.ContainsKey(key) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>
/// The visibility of a form given by the rules of the fields (<see cref="RetainingWallSheetViewModel.FormVisibility"/>, the InputForm
/// collapsed by ApplyRules of the historic view): a form not listed is shown.
/// </summary>
internal sealed class WallFormShownConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is IReadOnlyDictionary<string, bool> shown && parameter is string key && shown.TryGetValue(key, out bool visible) && !visible ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>The card of a key (the parameter) among <see cref="RetainingWallSheetViewModel.Cards"/>, read again at every notification of Cards.</summary>
internal sealed class WallCardConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is IReadOnlyDictionary<string, WallCardViewModel> cards && parameter is string key && cards.TryGetValue(key, out var card) ? card : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>A table of the points of the profile (the parameter: «punti-valle», «punti-monte», «punti-falda»).</summary>
internal sealed class WallPointsConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is IReadOnlyDictionary<string, WallTableViewModel> tables && parameter is string key && tables.TryGetValue(key, out var table) ? table : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Collapsed for null (a panel of results not shown), visible otherwise.</summary>
internal sealed class WallPresentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
