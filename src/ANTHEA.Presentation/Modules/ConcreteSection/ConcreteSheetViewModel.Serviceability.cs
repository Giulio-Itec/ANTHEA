using Anthea.Application.Concrete;
using Anthea.Calculations;
using Anthea.Presentation.Forms;

namespace Anthea.Presentation.Modules.ConcreteSection;

/// <summary>
/// The side of the sheet that the serviceability families (sub-step S4, <see cref="ConcreteStressPanelViewModel"/>) use: the panels and the
/// family shown, the errors of the homogenization shared by the families, the options common to the families (SynchronizeSharedSle of the
/// historic ConcreteRefinements.cs) and the change of an option. The results of the stresses, their outputs in the rows and their
/// invalidations stay in ConcreteSheetViewModel.Checks.cs, where S1 put them.
/// </summary>
public sealed partial class ConcreteSheetViewModel
{
    /// <summary>The main tab of the serviceability («Tensioni e fessurazione»).</summary>
    public const int StressTab = 3;

    private readonly Dictionary<string, ConcreteStressPanelViewModel> stressPanels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<ConcreteSheetPreparation.HomogenizationUpdate>> preparedHomogenization = new(StringComparer.Ordinal);
    private int stressFamily;

    /// <summary>The families of the serviceability limit states (Rara, Frequente, Quasi permanente; tab 04), in order.</summary>
    public IReadOnlyList<ConcreteStressPanelViewModel> StressPanels => stressPanels.Values.ToArray();

    /// <summary>The family by its key (SLE, SLE_FREQ, SLE_QP).</summary>
    public ConcreteStressPanelViewModel StressPanel(string key) => stressPanels[key];

    /// <summary>
    /// The family shown in the tab 04 (the tabs of the families and the choice «Combinazione SLE» of every family): 0 Rara, 1 Frequente,
    /// 2 Quasi permanente. Allowed in a read-only revision; no write.
    /// </summary>
    public int StressFamily
    {
        get => stressFamily;
        set
        {
            if (value < 0 || value >= stressPanels.Count || value == stressFamily) return;
            stressFamily = value; OnPropertyChanged();
            StressPanels[value].Shown();
        }
    }

    /// <summary>The labels of the families, for the choice «Combinazione SLE».</summary>
    public IReadOnlyList<string> StressFamilyLabels => StressSets.Select(ConcreteEditor.Label).ToArray();

    /// <summary>The errors of n and φ of the families, shared by their forms (the historic view keeps one dictionary for the three tabs).</summary>
    internal Dictionary<string, string> HomogenizationErrors { get; } = new(StringComparer.Ordinal);

    /// <summary>The outcome of the row <paramref name="id"/> of the family <paramref name="key"/>, when calculated.</summary>
    internal StressOutcome? StressOutcome(string key, string id) => stressResults.GetValueOrDefault(key)?.GetValueOrDefault(id);

    // The families of the tab 04, built after the tables of the actions (BuildStressTabs of the historic view): no write, the homogenization of
    // the opening (PrepareStressOptions) shown in their forms.
    private void CreateStressPanels()
    {
        stressPanels.Clear(); HomogenizationErrors.Clear(); stressFamily = 0;
        int index = 0;
        foreach (string key in StressSets)
            stressPanels[key] = new ConcreteStressPanelViewModel(this, key, index++, Actions.Stress[key], preparedHomogenization.GetValueOrDefault(key) ?? []);
        preparedHomogenization.Clear();
        OnPropertyChanged(nameof(StressPanels)); OnPropertyChanged(nameof(StressFamily));
    }

    /// <summary>
    /// An option of a family changed (the callback of its form): n and φ, the options copied into the other families, the fields enabled; an
    /// option of the cracking calculates every family again, the others clear the results of the family first.
    /// </summary>
    internal void StressOptionChanged(ConcreteStressPanelViewModel panel, string field)
    {
        using var notifications = JsonRowViewModel.DeferNotifications(AllActionRows());
        panel.SynchronizeHomogenization(field); SynchronizeSharedSle(panel.Key); panel.EnableOptions();
        if (ConcreteAnalysisSession.CrackFields.Contains(field)) { InvalidateChecks(StressSets.ToArray()); return; }
        stressResults.Remove(panel.Key);
        foreach (var row in actions[panel.Key])
        {
            row.Output("sigma_c", "—"); row.Output("sigma_s", "—"); row.Output("stress_status", "Da calcolare"); row.Output("eta_sigma", "—"); row.Output("wk", "Da calcolare");
        }
        panel.UpdateSelection(); RefreshSummary(); InvalidateChecks(StressSets.ToArray());
    }

    /// <summary>
    /// SynchronizeSharedSle of the historic view: the common options of <paramref name="source"/> copied into <c>sle_comuni</c> and into the
    /// other families (shown as they are written, without the format of the numbers: behaviour kept, C5); every family enables its fields,
    /// loses its results and shows its selection again; the detailing follows.
    /// </summary>
    internal void SynchronizeSharedSle(string source)
    {
        using var notifications = JsonRowViewModel.DeferNotifications(AllActionRows());
        var from = Settings["sle"]![source]!;
        foreach (string field in ConcreteEditor.SharedSleFields)
        {
            Settings["sle_comuni"]![field] = from[field]?.DeepClone();
            foreach (var panel in StressPanels)
            {
                if (panel.Key == source) continue;
                Settings["sle"]![panel.Key]![field] = from[field]?.DeepClone(); panel.Form.Set(field, from.S(field), true);
            }
        }
        foreach (var panel in StressPanels)
        {
            panel.EnableFields();
            stressResults.Remove(panel.Key);
            foreach (var row in actions[panel.Key]) foreach (string field in new[] { "sigma_c", "sigma_s", "stress_status", "eta_sigma", "wk" }) row.Output(field, "Da calcolare");
            panel.UpdateSelection();
        }
        RefreshDetailing();
    }
}
