// Spostato da X.Desktop/Wpf/BridgeOptimizationPlot.cs (commit 4144fe9e) con la decisione D3 del 15/10/2026: proprietà letta solo
// dalle prove WPF, compilata solo nella configurazione UiTests, fuori dall'installer.
using Anthea.Application.BridgeDesign;

namespace X.Desktop;

internal sealed partial class BridgeOptimizationPlot
{
    internal int PointCount => hits.Count;

    /// <summary>
    /// UiTests (F5.12 traces): the data the chart draws. Moved from tests/Desktop/BridgeDesignHistoricTrace.cs when the historic view of Bridge Design
    /// was archived (23/10/2026); ui/check-bridge-design-view reads it on the charts of the XAML view.
    /// </summary>
    internal (int Trials, int Solutions, BridgeDesignModel.Result? Baseline) DataForChecks => (trials.Length, solutions.Length, baseline);
}
