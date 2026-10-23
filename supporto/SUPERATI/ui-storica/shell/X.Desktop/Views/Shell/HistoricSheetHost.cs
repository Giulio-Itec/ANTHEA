using System.Windows;
using System.Windows.Controls;

namespace X.Desktop.Views.Shell;

/// <summary>
/// The view of a historic sheet in a tab of the new shell (F5.13 c, night of 22-23/10; R5): the control of its <see cref="SheetEditor"/>
/// (<see cref="HistoricSheetViewModel.View"/>), one element for the whole life of the sheet. A template of the tab builds a new host every time the tab
/// comes on screen, in the main window or in a detached one: the new host takes the control from the previous host, which may still hold it, so that
/// the editor keeps its state (scroll, choices of the view) as in MainWindow. The control is unchanged.
/// </summary>
internal sealed class HistoricSheetHost : Decorator
{
    public static readonly DependencyProperty SheetProperty =
        DependencyProperty.Register(nameof(Sheet), typeof(HistoricSheetViewModel), typeof(HistoricSheetHost), new PropertyMetadata(null, (d, _) => ((HistoricSheetHost)d).Attach()));

    public HistoricSheetHost()
    {
        Focusable = false;
        Loaded += (_, _) => Attach();
    }

    /// <summary>The historic sheet shown.</summary>
    public HistoricSheetViewModel? Sheet { get => (HistoricSheetViewModel?)GetValue(SheetProperty); set => SetValue(SheetProperty, value); }

    /// <summary>The control of the sheet here, taken from the host that held it before.</summary>
    private void Attach()
    {
        var view = Sheet?.View;
        if (ReferenceEquals(Child, view)) return;
        if (view?.Parent is HistoricSheetHost previous) previous.Child = null;
        else if (view?.Parent is ContentControl owner) owner.Content = null;
        Child = view;
    }
}
