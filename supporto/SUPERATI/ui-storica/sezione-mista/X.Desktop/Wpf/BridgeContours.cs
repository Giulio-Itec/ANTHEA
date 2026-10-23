using System.Windows;
using X.Core;
using System.Windows.Controls;

namespace X.Desktop;

internal sealed partial class BridgeWorkspace
{
    internal readonly MenuItem ContourSectionToggle = new() { Header = "Colora la sezione · σ/limite", IsCheckable = true };
    internal readonly MenuItem ContourDiagramToggle = new() { Header = "Colora le tensioni disegnate · σ/limite", IsCheckable = true };
    internal readonly MenuItem StressLimitsToggle = new() { Header = "Mostra le rette dei limiti tensionali", IsCheckable = true };
    private void BuildContourControls()
    {
        var menu = new ContextMenu();
        var button = Ui.Button("Verifiche ▾", () => { menu.IsOpen = true; });
        menu.PlacementTarget = button; menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        foreach (var (toggle, key) in new[] { (ContourSectionToggle, "contour_sezione"), (ContourDiagramToggle, "contour_tensioni"), (StressLimitsToggle, "limiti_tensioni") })
        {
            menu.Items.Add(toggle); toggle.IsChecked = viewSettings.B(key);
            toggle.Click += (_, _) => { viewSettings[key] = toggle.IsChecked; ApplyContours(); ViewChanged(); };
        }
        Viewport.Toolbar.Children.Add(button); ApplyContours();
    }
    private void ApplyContours()
    {
        Drawing.ContourSection = ContourSectionToggle.IsChecked;
        Drawing.ContourDiagram = ContourDiagramToggle.IsChecked;
        Drawing.ShowStressLimits = StressLimitsToggle.IsChecked;
        Drawing.InvalidateVisual();
    }
}
