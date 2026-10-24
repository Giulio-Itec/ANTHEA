using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using X.Core;

namespace X.Desktop;

// Presentation only: editable bindings and persisted values retain full precision.
internal sealed class NumericPresentation : IValueConverter
{
    internal static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(NumericPresentation), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));
    internal static bool Enabled(DependencyObject target) => (bool)target.GetValue(EnabledProperty);
    /// <summary>The pure function moved to Anthea.Presentation.Forms.NumericDisplay in F5.2.</summary>
    internal static string Format(string text, string key) => NumericDisplay.Format(text, key);
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => Format(value?.ToString() ?? "", parameter?.ToString() ?? "");
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

internal sealed class NumericDisplayColumn : DataGridTextColumn
{
    internal string Key { get; init; } = "";
    protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
    {
        var element = base.GenerateElement(cell, dataItem);
        if (NumericPresentation.Enabled(cell) && element is TextBlock text && Binding is Binding binding)
            text.SetBinding(TextBlock.TextProperty, new Binding(binding.Path.Path) { Mode = BindingMode.OneWay, Converter = new NumericPresentation(), ConverterParameter = Key });
        return element;
    }
}
