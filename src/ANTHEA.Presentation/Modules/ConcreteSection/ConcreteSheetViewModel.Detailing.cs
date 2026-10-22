using System.Text.Json.Nodes;
using Anthea.Application.Concrete;

namespace Anthea.Presentation.Modules.ConcreteSection;

/// <summary>
/// The side of the sheet that the detailing (sub-step S6, <see cref="ConcreteDetailingViewModel"/>) uses: the panel, its options and the
/// calculation of the detailing at every change (RefreshDetailing of the historic view), whose checks and anchorage enter the export.
/// </summary>
public sealed partial class ConcreteSheetViewModel
{
    /// <summary>The main tab of the detailing («Dettagli costruttivi»).</summary>
    public const int DetailingTab = 5;

    /// <summary>The detailing and the anchorages (tab 06, S6).</summary>
    public ConcreteDetailingViewModel Detailing { get; private set; } = null!;

    /// <summary>The options of the detailing in the settings (<c>dettagli_costruttivi</c>).</summary>
    internal JsonObject DetailingOptions => Settings["dettagli_costruttivi"]!.AsObject();

    // The tab 06, built after the shear: no write; from here the detailing is refreshed.
    private void CreateDetailingPanel()
    {
        Detailing = new ConcreteDetailingViewModel(this);
        detailingBuilt = true;
        OnPropertyChanged(nameof(Detailing));
    }

    /// <summary>
    /// RefreshDetailing of the historic view: the fields of the tab 06 follow the element and the anchorage, the detailing is calculated from the
    /// data and the rows of the actions, its derived values (cmin,dur, the exposure) are written into the settings, the export waits for a new
    /// reading, the tab shows the checks.
    /// </summary>
    internal void RefreshDetailing()
    {
        if (!detailingBuilt) return;
        Detailing.ShowFields();
        var presentation = ConcreteDetailingPresentation.Calculate(Input, Settings, actions.ToDictionary(p => p.Key, p => p.Value.Select(r => r.Values).ToArray()));
        presentation.ApplyDerivedValues(Settings);
        detailing = presentation; ResetExport();
        Detailing.Show(presentation);
    }
}
