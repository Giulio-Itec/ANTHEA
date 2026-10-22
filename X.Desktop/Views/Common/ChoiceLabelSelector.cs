using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace X.Desktop.Views.Common;

/// <summary>
/// The template of an item of a choice in <see cref="FieldForm"/>, in the box and in the list: when the host view gives labels for the key of the
/// field (<see cref="FieldForm.ChoiceLabelsProperty"/>), a text with the label of the item (the ItemTemplate with DomainLabelConverter of the historic
/// RC section); otherwise none, and the item shows as it is. The selection and the document keep the item.
/// </summary>
internal sealed class ChoiceLabelSelector : DataTemplateSelector
{
    private readonly Dictionary<IReadOnlyDictionary<string, string>, DataTemplate> templates = new(ReferenceEqualityComparer.Instance);

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        var choice = Owner(container);
        if (choice?.DataContext is not FieldViewModel field || FieldForm.GetChoiceLabels(choice) is not { } labels || !labels.TryGetValue(field.Key, out var map)) return null;
        if (!templates.TryGetValue(map, out var template))
        {
            var text = new FrameworkElementFactory(typeof(TextBlock));
            text.SetBinding(TextBlock.TextProperty, new Binding { Mode = BindingMode.OneWay, Converter = new LabelConverter(map) });
            template = new DataTemplate { VisualTree = text };
            template.Seal();
            templates[map] = template;
        }
        return template;
    }

    // The combo box of the box (the presenter in its template) or of an item of the list (the presenter in a ComboBoxItem).
    private static ComboBox? Owner(DependencyObject container) => container switch
    {
        ComboBox combo => combo,
        FrameworkElement { TemplatedParent: ComboBox combo } => combo,
        FrameworkElement { TemplatedParent: ComboBoxItem item } => ItemsControl.ItemsControlFromItemContainer(item) as ComboBox,
        _ => null
    };

    private sealed class LabelConverter(IReadOnlyDictionary<string, string> labels) : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is string item && labels.TryGetValue(item, out var label) ? label : value?.ToString() ?? "";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}
