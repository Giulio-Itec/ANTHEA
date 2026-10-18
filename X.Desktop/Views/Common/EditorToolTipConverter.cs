using System.Globalization;
using System.Windows.Data;

namespace X.Desktop.Views.Common;

/// <summary>
/// The tool tip of the editor of a field in <see cref="FieldForm"/>: the one the host view gives for its key
/// (<see cref="FieldForm.EditorToolTipsProperty"/>), otherwise the tool tip of the field. Values: EditorToolTip, Key, EditorToolTips.
/// </summary>
internal sealed class EditorToolTipConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values.Length == 3 && values[1] is string key && values[2] is IReadOnlyDictionary<string, string> tips && tips.TryGetValue(key, out var tip) ? tip
            : values.Length > 0 ? values[0] as string : null;

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
