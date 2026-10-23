// Spostate da X.Desktop/Wpf/BridgeDesignOptimization.cs (commit 4144fe9e) con la decisione D3 del 15/10/2026: proprietà lette solo
// dalle prove WPF, compilate solo nella configurazione UiTests, fuori dall'installer.
namespace X.Desktop;

internal sealed partial class BridgeDesignWorkspace
{
    internal int SelectedOptimizationRank => selectedSolution + 1;
    internal int DisplayedOptimizationCount => optimizationRanking?.Items.Count ?? 0;
}
