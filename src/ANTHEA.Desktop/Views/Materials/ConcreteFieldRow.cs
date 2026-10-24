using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Anthea.Presentation.Forms;

namespace X.Desktop.Views.Materials;

/// <summary>
/// Row of a field of the concrete sheet (decision M11 of 17/10/2026: the layout of the historic view with a field template of its own,
/// instead of FieldForm): the label with its «?» in the free column, the editor in a column of 136 px, the unit in a column of 44 px, as
/// Materiali.MaterialView.Field. Its template is the implicit style of ConcreteMaterialSheetView.
/// <list type="bullet">
/// <item>A field of the form (<see cref="Field"/>): label, unit and editor come from the <see cref="FieldViewModel"/> (text box or
/// choice, by its kind); every write goes through it.</item>
/// <item>Otherwise the view gives <see cref="Label"/>, <see cref="Unit"/> and the editor as content (the deviation value, the values
/// shown in read-only boxes).</item>
/// </list>
/// The code only reports what WPF knows and the ViewModel does not, as FieldForm does: the keyboard focus of the editor, F1 in the
/// editor and the «?» label, through the Wiki navigator of the form (<see cref="FormViewModel.WikiNavigator"/>). A row without a field
/// leaves F1 to the host (the guide of the module). No calculation and no write of the document.
/// </summary>
public sealed class ConcreteFieldRow : ContentControl
{
    public static readonly DependencyProperty FieldProperty = DependencyProperty.Register(nameof(Field), typeof(FieldViewModel), typeof(ConcreteFieldRow),
        new PropertyMetadata(null));

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ConcreteFieldRow),
        new PropertyMetadata(""));

    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(nameof(Unit), typeof(string), typeof(ConcreteFieldRow),
        new PropertyMetadata(""));

    private Button? help;
    private FrameworkElement? editor;

    public ConcreteFieldRow() => KeyDown += OnKeyDown;

    /// <summary>The field of the form shown by the row; null for an editor given by the view.</summary>
    public FieldViewModel? Field
    {
        get => (FieldViewModel?)GetValue(FieldProperty);
        set => SetValue(FieldProperty, value);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Unit
    {
        get => (string)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public override void OnApplyTemplate()
    {
        if (help is not null) help.Click -= OnHelp;
        if (editor is not null) editor.IsKeyboardFocusWithinChanged -= OnEditorFocus;
        base.OnApplyTemplate();
        help = GetTemplateChild("PART_Help") as Button;
        editor = GetTemplateChild("PART_Editor") as FrameworkElement;
        if (help is not null)
        {
            help.Click += OnHelp;
            // The «?» label stays usable while a revision is inspected, as in the historic view.
            RevisionInspection.Allow(help);
        }
        if (editor is not null) editor.IsKeyboardFocusWithinChanged += OnEditorFocus;
    }

    private void OnEditorFocus(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (Field is { } field) field.IsFocused = (bool)e.NewValue;
    }

    /// <summary>F1 in the editor of a field: its topic, otherwise the guide of the module (decision D11), with the navigator of the form.</summary>
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.F1 || Field is not { } field || !IsInEditor(e.OriginalSource as DependencyObject)) return;
        if (field.OpenContextHelp()) e.Handled = true;
    }

    /// <summary>The source of the key is the editor or one of its parts (not the «?» label, which leaves F1 to the host).</summary>
    private bool IsInEditor(DependencyObject? source)
    {
        for (var node = source; node is not null && !ReferenceEquals(node, this); node = node is System.Windows.Media.Visual ? System.Windows.Media.VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
            if (ReferenceEquals(node, editor)) return true;
        return false;
    }

    /// <summary>The «?» label: the topic of the field, with the navigator of the form.</summary>
    private void OnHelp(object sender, RoutedEventArgs e) => Field?.OpenHelp();
}
