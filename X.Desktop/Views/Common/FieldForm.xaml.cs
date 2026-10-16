using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace X.Desktop.Views.Common;

/// <summary>
/// View of a <see cref="FormViewModel"/> (proposal F5, section 3.2): the layout of the historic InputForm in XAML. The code
/// behind only reports what WPF knows and the ViewModel does not: the keyboard focus of the editors, the inherited presentation
/// of the numbers and module of the Wiki, F1, the «?» label (opening the Wiki stays WPF, WikiContextHelp.Open) and the
/// forwarding of the mouse wheel of a closed choice to the scrolling form. No calculation and no direct write of the document.
/// </summary>
public partial class FieldForm : UserControl
{
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

    private void Editor_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.F1 || sender is not FrameworkElement { DataContext: FieldViewModel field } editor || field.HelpUri is not { } uri) return;
        e.Handled = true;
        WikiContextHelp.Open(editor, uri);
    }

    private void Choice_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ComboBox choice || choice.IsDropDownOpen) return;
        ChainedScrollViewer.ForwardWheel(choice, e);
        e.Handled = true;
    }

    private void Help_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FieldViewModel { HelpTopic: { } topic } } button) WikiContextHelp.Open(button, topic.Uri);
    }

    /// <summary>The «?» label stays usable while a revision is inspected, as in InputForm.</summary>
    private void Help_Initialized(object? sender, EventArgs e)
    {
        if (sender is DependencyObject button) RevisionInspection.Allow(button);
    }
}
