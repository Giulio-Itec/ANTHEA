using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace X.Desktop.Views.Common;

/// <summary>
/// View of a <see cref="FormViewModel"/> (proposal F5, section 3.2): the layout of the historic InputForm in XAML. The code
/// behind only reports what WPF knows and the ViewModel does not: the keyboard focus of the editors, the inherited presentation
/// of the numbers and module of the Wiki, F1, the «?» label (through the IWikiNavigator of the form when the sheet gives one,
/// FormViewModel.WikiNavigator; otherwise the Wiki of the window, WikiContextHelp.Open, as InputForm) and the forwarding of the
/// mouse wheel of a closed choice to the scrolling form. No calculation and no direct write of the document.
/// </summary>
public partial class FieldForm : UserControl
{
    /// <summary>
    /// Text fields locked by the host view, by key (true when locked): read-only with the background of the calculated fields. A state
    /// of the view, as the read-only text boxes of a historic view (decision M6 of F5.5: the mechanical properties of a catalogue steel);
    /// the ViewModel does not refuse a write. Inherited: set on the element that hosts the form.
    /// </summary>
    public static readonly DependencyProperty CalculatedKeysProperty = DependencyProperty.RegisterAttached("CalculatedKeys", typeof(IReadOnlyDictionary<string, bool>),
        typeof(FieldForm), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));

    /// <summary>Text fields locked by the host view, by key: read-only, with their background (the name of a catalogue steel). Inherited.</summary>
    public static readonly DependencyProperty ReadOnlyKeysProperty = DependencyProperty.RegisterAttached("ReadOnlyKeys", typeof(IReadOnlyDictionary<string, bool>),
        typeof(FieldForm), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));

    /// <summary>
    /// Tool tips of the editors given by the host view, by key, in place of the tool tip of the field (F5.7: the tool tips of the c.a. section
    /// of the horizontal pile, set on the editors by the historic view). A state of the view. Inherited: set on the element that hosts the form.
    /// </summary>
    public static readonly DependencyProperty EditorToolTipsProperty = DependencyProperty.RegisterAttached("EditorToolTips", typeof(IReadOnlyDictionary<string, string>),
        typeof(FieldForm), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));

    public static IReadOnlyDictionary<string, string>? GetEditorToolTips(DependencyObject target) => (IReadOnlyDictionary<string, string>?)target.GetValue(EditorToolTipsProperty);

    public static void SetEditorToolTips(DependencyObject target, IReadOnlyDictionary<string, string>? value) => target.SetValue(EditorToolTipsProperty, value);

    public static IReadOnlyDictionary<string, bool>? GetCalculatedKeys(DependencyObject target) => (IReadOnlyDictionary<string, bool>?)target.GetValue(CalculatedKeysProperty);

    public static void SetCalculatedKeys(DependencyObject target, IReadOnlyDictionary<string, bool>? value) => target.SetValue(CalculatedKeysProperty, value);

    public static IReadOnlyDictionary<string, bool>? GetReadOnlyKeys(DependencyObject target) => (IReadOnlyDictionary<string, bool>?)target.GetValue(ReadOnlyKeysProperty);

    public static void SetReadOnlyKeys(DependencyObject target, IReadOnlyDictionary<string, bool>? value) => target.SetValue(ReadOnlyKeysProperty, value);

    public FieldForm()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Synchronize();
    }

    /// <summary>Inherited settings of the host (NumericPresentation.Enabled, WikiContextHelp.Module) passed to the ViewModel.</summary>
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == NumericPresentation.EnabledProperty || e.Property == WikiContextHelp.ModuleProperty) Synchronize();
    }

    private void Synchronize()
    {
        if (DataContext is not FormViewModel form) return;
        form.FormatNumbers = NumericPresentation.Enabled(this);
        form.InheritedWikiModule = GetValue(WikiContextHelp.ModuleProperty) as string;
    }

    private void Editor_FocusChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FieldViewModel field }) field.IsFocused = (bool)e.NewValue;
    }

    /// <summary>F1: the topic of the field, otherwise the guide of the module; through the navigator of the form (MVVM sheets), or the window of a historic host.</summary>
    private void Editor_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.F1 || sender is not FrameworkElement { DataContext: FieldViewModel field } editor || field.HelpUri is not { } uri) return;
        e.Handled = true;
        if (!field.OpenContextHelp()) WikiContextHelp.Open(editor, uri);
    }

    private void Choice_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ComboBox choice || choice.IsDropDownOpen) return;
        ChainedScrollViewer.ForwardWheel(choice, e);
        e.Handled = true;
    }

    /// <summary>The «?» label: the topic of the field, through the navigator of the form (MVVM sheets) or the window of a historic host.</summary>
    private void Help_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FieldViewModel { HelpTopic: { } topic } field } button && !field.OpenHelp()) WikiContextHelp.Open(button, topic.Uri);
    }

    /// <summary>The «?» label stays usable while a revision is inspected, as in InputForm.</summary>
    private void Help_Initialized(object? sender, EventArgs e)
    {
        if (sender is DependencyObject button) RevisionInspection.Allow(button);
    }
}
