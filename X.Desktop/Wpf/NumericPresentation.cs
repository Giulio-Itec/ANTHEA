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
    internal static string Format(string text, string key)
    {
        if (key is "id" or "nome" or "name" or "descrizione" || J.Number(System.Text.Json.Nodes.JsonValue.Create(text)) is not double n || !double.IsFinite(n)) return text;
        bool strain = key.Contains("strain", StringComparison.OrdinalIgnoreCase) || key.Contains("eps", StringComparison.OrdinalIgnoreCase);
        if (strain && n != 0 && Math.Abs(n) < .01) return EngineeringFormat.Number(n);
        return (Math.Round(n, 2) == 0 ? 0 : n).ToString("0.##", CultureInfo.CurrentCulture);
    }
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
