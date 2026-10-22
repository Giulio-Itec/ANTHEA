using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.ConcreteSection;

/// <summary>A block of the summaries of the control panel (SummaryBlock of the historic view): the key of «Apri →» (a limit state or «Taglio»), the title, the cards.</summary>
public sealed record ConcreteSummaryBlock(string Key, string Title, ConcreteCardsViewModel Cards);

/// <summary>
/// What the XAML view of the sheet asks besides the children (F5.10, view after S9): the dividers saved in <c>layout</c> (ConcreteLayout.cs of the
/// historic view: every divider of a group follows the one moved by the user, the ratio saved with Modified) and «Apri →» of the summaries of the
/// control panel (the tab of the check and, for the domains, the limit state of the 3D panel; for the serviceability, the family).
/// </summary>
public sealed partial class ConcreteSheetViewModel
{
    /// <summary>The ratio of the dividers of <paramref name="group"/> («ingressi», «risultati», «righe»): the saved one, or <paramref name="initial"/>.</summary>
    public double LayoutRatio(string group, double initial) => ConcreteSheetPresentation.SplitRatio(Settings, group, initial);

    /// <summary>
    /// The user moved a divider of <paramref name="group"/> (DragCompleted of the historic splitters): the ratio, limited, saved in <c>layout</c>,
    /// every divider of the group moved (<see cref="LayoutChanged"/>), Modified. Also in a read-only revision, as the historic splitters.
    /// </summary>
    public void MoveLayout(string group, double ratio)
    {
        ArgumentException.ThrowIfNullOrEmpty(group);
        if (IsDisposed) return;
        ratio = ConcreteSheetPresentation.MovedSplitRatio(ratio);
        if (Settings["layout"] is not JsonObject) Settings["layout"] = new JsonObject();
        Settings["layout"]![group] = ratio;
        LayoutChanged?.Invoke(this, group);
        RaiseModified();
    }

    /// <summary>The ratio of a group of dividers changed: the view places every divider of the group again.</summary>
    public event EventHandler<string>? LayoutChanged;

    /// <summary>The blocks of the summaries of the control panel, in the order of the historic view: SLU, SLV, the three families SLE, the shear.</summary>
    public IReadOnlyList<ConcreteSummaryBlock> SummaryBlocks =>
        [.. ConcreteEditor.Sets.Select(key => new ConcreteSummaryBlock(key, SummaryTitle(key), summaries[key])), new("Taglio", "Taglio", ShearDashboard)];

    /// <summary>
    /// «Apri →» of a summary of the control panel (the key of the limit state, or «Taglio»): the tab of the check; for SLU and SLV the limit state of
    /// the 3D domain through its field «stato»; for the serviceability the family. Allowed in a read-only revision, as the historic buttons.
    /// </summary>
    public IRelayCommand<string> OpenSummaryCommand => openSummaryCommand ??= new RelayCommand<string>(OpenSummary);
    private IRelayCommand<string>? openSummaryCommand;

    private void OpenSummary(string? key)
    {
        if (key is null || IsDisposed) return;
        if (key == "Taglio") { SelectedTab = ShearTab; return; }
        if (key.StartsWith("SLE", StringComparison.Ordinal))
        {
            SelectedTab = StressTab;
            StressFamily = StressSets.ToList().IndexOf(key);
            return;
        }
        SelectedTab = 1;
        if (Domain3D.Form.Contains("stato")) Domain3D.Form["stato"].SelectedChoice = key;
    }
}
