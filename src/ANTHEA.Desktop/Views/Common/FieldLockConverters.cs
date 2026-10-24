using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace X.Desktop.Views.Common;

/// <summary>
/// Read-only state of the text box of a field in <see cref="FieldForm"/>: the field refuses the typing (<c>IsInputReadOnly</c>), or the
/// host view locks its key (<see cref="FieldForm.CalculatedKeysProperty"/>, <see cref="FieldForm.ReadOnlyKeysProperty"/>). Values:
/// IsInputReadOnly, Key, CalculatedKeys, ReadOnlyKeys.
/// </summary>
internal sealed class FieldLockConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values.Length == 4 && (values[0] is true || values[1] is string key && (Locked(values[2], key) || Locked(values[3], key)));

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => targetTypes.Select(_ => DependencyProperty.UnsetValue).ToArray();

    /// <summary>The key is mapped to true in the dictionary of the host (null: no lock).</summary>
    internal static bool Locked(object? keys, string key) => keys is IReadOnlyDictionary<string, bool> locks && locks.TryGetValue(key, out bool locked) && locked;
}

/// <summary>The key of a field (first value) is locked in the dictionary of the host (second value): the field looks calculated.</summary>
internal sealed class FieldKeyFlagConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values.Length == 2 && values[0] is string key && FieldLockConverter.Locked(values[1], key);

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => targetTypes.Select(_ => DependencyProperty.UnsetValue).ToArray();
}
