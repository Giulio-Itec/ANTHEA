using System.Windows;
using System.Windows.Controls;

namespace X.Desktop.Views.CompositeSection;

/// <summary>
/// The content of the window of the detached view (F5.11, SM3): its buttons ask the window that hosts it (CompositeSectionSheetView) for the full
/// screen and for the attachment; the drawing is put in <c>Host</c> by the sheet view.
/// </summary>
public partial class CompositeSectionDetachedView : UserControl
{
    public CompositeSectionDetachedView() => InitializeComponent();

    /// <summary>«Schermo intero · F11».</summary>
    internal event Action? FullScreenRequested;

    /// <summary>«Riaggancia»: the window closes and the drawing goes back to the sheet.</summary>
    internal event Action? ReattachRequested;

    private void FullScreen_Click(object sender, RoutedEventArgs e) => FullScreenRequested?.Invoke();

    private void Reattach_Click(object sender, RoutedEventArgs e) => ReattachRequested?.Invoke();
}
