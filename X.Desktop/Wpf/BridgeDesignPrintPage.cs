using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Anthea.Application.BridgeDesign;

namespace X.Desktop;

/// <summary>
/// The page of «Stampa scheda» of Bridge Design (F5.12, decision BD8): the texts of the result (BridgeDesignPresentation.PrintLines, given by
/// the ViewModel) around the elevation, laid out on the printable area exactly as the historic view lays them out (BridgeDesignWorkspace.PrintPage).
/// It is a document, not a view: the XAML view prints it in the light appearance (Appearance.Document), as the historic view. The historic view
/// keeps its own copy until it is archived (Q11); then this page stays, with the drawings.
/// </summary>
internal static class BridgeDesignPrintPage
{
    /// <summary>The page of <paramref name="lines"/> with the elevation of <paramref name="data"/> and <paramref name="result"/>, measured and arranged.</summary>
    internal static FrameworkElement Create(JsonObject data, BridgeDesignModel.Result result, IReadOnlyList<string> lines, double width, double height)
    {
        var drawing = new BridgeDesignDrawing { Data = data, Result = result, Width = 1000, Height = 420 };
        var panel = Ui.Stack(Ui.Text(lines[0], 26, true), Ui.Text(lines[1], 14), drawing, Ui.Text(lines[2], 16), Ui.Text(lines[3], 18, true));
        for (int k = 4; k < lines.Count - 1; k++) panel.Children.Add(Ui.Text(lines[k], 14));
        panel.Children.Add(Ui.Text(lines[^1], 12));
        var box = new Viewbox { Child = panel, Width = width, Height = height, Stretch = Stretch.Uniform };
        box.Measure(new Size(box.Width, box.Height)); box.Arrange(new Rect(0, 0, box.Width, box.Height));
        return box;
    }
}
