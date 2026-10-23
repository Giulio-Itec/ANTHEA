using System.Windows;
using System.Windows.Controls;

namespace X.Desktop;

// Probes of the box girder used by --smoke-bridge (BridgeSectionTypeSmokeChecks), moved out of BridgeTorsion.cs.
internal sealed partial class BridgeWorkspace
{
    /// <summary>The options of the box and its results are shown</summary>
    internal bool BoxInputsVisible => boxInputs?.Visibility == Visibility.Visible;
    internal bool TorsionResultsVisible => torsionGroup.Visibility == Visibility.Visible && torsionResults.Content is StackPanel;
    internal bool TorqueColumnVisible => ActionsTable.Columns.Single(c => c.SortMemberPath == "T").Visibility == Visibility.Visible;
    /// <summary>Scrolls to the options of the box or selects the checks with the results of the torsion</summary>
    internal void RevealTorsion(bool results)
    {
        if (!results) { if (boxInputs is not null) { boxInputs.IsExpanded = true; boxInputs.BringIntoView(); } return; }
        Results.SelectedItem = Results.Items.OfType<TabItem>().First(t => t.Header as string == "Verifiche");
        // after the layout of the selected tab
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() => torsionGroup.BringIntoView()));
    }
}
