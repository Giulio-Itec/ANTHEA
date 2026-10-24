using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X.Core;

namespace X.Desktop.Views.ConcreteSection;

/// <summary>
/// «PNG» and «Espandi» of the frames of the drawings of the RC section (ViewportFrame of the historic view), for the views of the tabs: the image of
/// the drawing saved where the sheet view chooses (a system dialog, a path in the checks), the drawing moved into a window of the view and back. Also
/// the sheet view that hosts a tab, for the tabs that change the main tab.
/// </summary>
internal static class ConcreteViewFrames
{
    /// <summary>The view of the sheet that hosts <paramref name="element"/>, when it is in its tree.</summary>
    internal static ConcreteSectionSheetView? SheetOf(DependencyObject element)
    {
        for (DependencyObject? node = element; node is not null; node = VisualTreeHelper.GetParent(node) ?? LogicalTreeHelper.GetParent(node))
            if (node is ConcreteSectionSheetView sheet) return sheet;
        return null;
    }

    /// <summary>«PNG»: the image of <paramref name="drawing"/> (Png of a drawing, the snapshot of the 3D viewport) in the file chosen.</summary>
    internal static void SavePng(FrameworkElement owner, FrameworkElement drawing, string title)
    {
        var choose = SheetOf(owner)?.ChooseImagePath ?? ConcreteSectionSheetView.ChooseImage;
        if (choose("ANTHEA_" + title.Replace(' ', '_') + ".png") is not { } path) return;
        try { Archivio.ScriviAtomico(path, drawing is DrawingView view ? view.Png() : Ui.DocumentSnapshot(drawing)); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { MessageBox.Show(Window.GetWindow(owner), error.Message, "Esportazione immagine"); }
    }

    /// <summary>«Espandi»: <paramref name="drawing"/> in a window of the view, back into <paramref name="host"/> when the window closes.</summary>
    internal static void Expand(FrameworkElement owner, ContentControl host, FrameworkElement drawing, string title)
    {
        host.Content = null;
        var window = Ui.Dialog(owner, title, drawing, 1200, 800);
        window.Closed += (_, _) => { window.Content = null; host.Content = drawing; };
        (SheetOf(owner)?.ShowWindow ?? (w => w.ShowDialog()))(window);
    }
}
