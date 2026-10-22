using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Anthea.Presentation.Shell.Commands;

namespace X.Desktop.Views.Shell;

/// <summary>Visible when the value is present (not null, not an empty text); with the parameter «invert», visible when it is absent.</summary>
internal sealed class PresenceVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool present = value is not null && value is not "";
        return present ^ (parameter is "invert") ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Visible when the boolean is false (the opposite of BooleanToVisibilityConverter).</summary>
internal sealed class InverseVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Visible when every boolean is true (a project open and its tree open, for example).</summary>
internal sealed class AllVisibleConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture) => values.All(v => v is true) ? Visibility.Visible : Visibility.Collapsed;

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) => [];
}

/// <summary>Visible when the value is an instance of the type named by the parameter (the sheet bar above a sheet tab).</summary>
internal sealed class TypeVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && parameter is Type type && type.IsInstanceOfType(value) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>The text of a shortcut of the menu «File», as MainWindow writes it (KeyGesture in the culture of the user: «Ctrl+N», «Ctrl+Maiusc+S»).</summary>
internal sealed class ShortcutTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ShellShortcut shortcut ? Gesture(shortcut).GetDisplayStringForCulture(CultureInfo.CurrentCulture) : "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;

    /// <summary>The gesture of a shortcut: Ctrl plus the key, with Shift when asked.</summary>
    internal static KeyGesture Gesture(ShellShortcut shortcut)
    {
        var key = (Key)Enum.Parse(typeof(Key), shortcut.Key);
        return new KeyGesture(key, shortcut.Shift ? ModifierKeys.Control | ModifierKeys.Shift : ModifierKeys.Control);
    }
}
