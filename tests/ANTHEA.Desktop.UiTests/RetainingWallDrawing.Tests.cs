// Spostato da X.Desktop/Wpf/RetainingWallDrawing.Inspection.cs (commit 4144fe9e) con la decisione D3 del 15/10/2026: proprietà letta
// solo dalle prove WPF, compilata solo nella configurazione UiTests, fuori dall'installer.
namespace X.Desktop;

internal sealed partial class RetainingWallDrawing
{
    internal int InspectionCount => hits.Count + plots.Count;
}
