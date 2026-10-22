using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Calculations;
using Anthea.Presentation.Forms;

namespace Anthea.Presentation.Modules.ConcreteSection;

/// <summary>
/// What the tabs of the later sub-steps (S6 detailing, S7 M–χ, S9 summaries) do to the data and to the results
/// that the common part of the traces reads, until their ViewModels, and what the sheet keeps for the panels of S2–S5: the rows of the
/// actions and of the shear with their outputs, the results published by the calculation, the automatic shear parameters, the derived values
/// of the detailing, the summaries of the control panel and the export. Every write and its order are those of the historic view; the controls of those tabs
/// are not here.
/// </summary>
public sealed partial class ConcreteSheetViewModel
{
    // The families of the serviceability limit states (SLE, SLE_FREQ, SLE_QP).
    private static IEnumerable<string> StressSets => ConcreteEditor.Sets.Skip(2);

    private readonly Dictionary<string, JsonRowCollection> actions = new(StringComparer.Ordinal);
    private JsonRowCollection shearRows = [];
    private readonly Dictionary<string, ConcreteResults.Domain3D> checker3D = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ConcreteResults.Domain2D> checker2D = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, ConcreteResults.Check>> domainResults = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, StressOutcome>> stressResults = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ConcreteResults.ShearResult[]> shearResults = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ConcreteResults.TorsionResult> torsionResults = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ConcreteCardsViewModel> summaries = new(StringComparer.Ordinal);
    private ConcreteDetailingPresentation? detailing;
    private ConcreteResults.CurvatureResult? curvatureResult;
    private bool detailingBuilt, hasResults;
    private JsonObject? export;

    // ---------------------------------------------------------------- results and export

    /// <summary>True once a batch published results for the current data; false after every invalidation (HasResults of the historic view).</summary>
    public override bool HasResults => hasResults;

    /// <summary>
    /// The export of the results (ConcreteResultExport, the historic contract): built when read, from the results, the summaries, the
    /// detailing and the data of that moment, and kept until the next change of the results or of the detailing, as the historic view
    /// (the data committed later are not in it until then). Null without results.
    /// </summary>
    public override JsonObject? Result
    {
        get
        {
            if (!hasResults) return null;
            if (export is null) RebuildExport();
            return export;
        }
    }

    /// <summary>The summaries of the control panel, one per limit state (SLU, SLV, SLE, SLE_FREQ, SLE_QP).</summary>
    public IReadOnlyDictionary<string, ConcreteCardsViewModel> Summaries => summaries;
    /// <summary>The summary of the shear in the control panel.</summary>
    public ConcreteCardsViewModel ShearDashboard { get; private set; } = new();

    /// <summary>The title of the summary of a limit state.</summary>
    public string SummaryTitle(string key) => key.StartsWith("SLE", StringComparison.Ordinal) ? "SLE · " + SectionActionInputs.Label(key, Settings) : SectionActionInputs.Label(key, Settings);

    // The cache of the export only (exportResult = null of the historic view).
    private void ResetExport() { if (export is null) return; export = null; OnPropertyChanged(nameof(Result)); }

    // Result = null of the historic view: no results until the next publication.
    private void ResetResult()
    {
        bool changed = hasResults || export is not null;
        export = null; hasResults = false;
        if (changed) { OnPropertyChanged(nameof(Result)); OnPropertyChanged(nameof(HasResults)); }
    }

    // A new opening (also after Reload, as the historic view builds a new workspace): no results, no detailing, tabs not built yet.
    private void ResetState()
    {
        ClearResults(); shearResults.Clear(); torsionResults.Clear();
        detailing = null; curvatureResult = null; detailingBuilt = false; shearBuilt = false; synchronizing = false;
        ResetResult();
    }

    private void ClearResults()
    {
        checker3D.Clear(); checker2D.Clear(); domainResults.Clear(); stressResults.Clear();
    }

    private void RebuildExport()
    {
        if (domainResults.Count == 0 && stressResults.Count == 0 && shearResults.Count == 0) { ResetResult(); return; }
        export = ConcreteResultExport.Create(Data, CollectVerificationSummaries(), domainResults, stressResults, shearResults, torsionResults,
            detailing?.Checks ?? [], detailing?.DetailsText ?? "", detailing?.Anchorage, detailing?.AnchorageDetails.Text ?? "", curvatureResult, Calculation.ErrorsForExport);
    }

    private Dictionary<string, VerificationSummary> CollectVerificationSummaries() => ConcreteVerificationSummaries.Create(Settings,
        actions.ToDictionary(p => p.Key, p => p.Value.Select(r => r.Values).ToArray()), domainResults, stressResults,
        shearRows.Select(r => r.Values).ToArray(), shearResults, torsionResults);

    private void CreateSummaries()
    {
        summaries.Clear();
        foreach (string key in ConcreteEditor.Sets) summaries[key] = new ConcreteCardsViewModel();
        ShearDashboard = new ConcreteCardsViewModel();
        OnPropertyChanged(nameof(Summaries)); OnPropertyChanged(nameof(ShearDashboard));
    }

    /// <summary>The summaries of the verifications (RefreshVerificationSummaries of the historic view, the cards of the control panel).</summary>
    private void RefreshSummary()
    {
        var results = CollectVerificationSummaries();
        void Fill(ConcreteCardsViewModel cards, Func<string, bool> include)
        {
            cards.Start();
            foreach (var (_, summary) in results.Where(p => include(p.Key))) cards.AddSummary(summary);
        }
        foreach (var (key, cards) in summaries)
            Fill(cards, k => key is "SLU" or "SLV" ? k.EndsWith(":" + key, StringComparison.Ordinal) : k.StartsWith(key + ":", StringComparison.Ordinal));
        foreach (var panel in DomainPanels) Fill(panel.Summary, k => k.StartsWith(panel.Prefix, StringComparison.Ordinal));
        foreach (var panel in StressPanels) Fill(panel.Summary, k => k.StartsWith("SLE", StringComparison.Ordinal));
        if (Shear is not null) Fill(Shear.Worst, k => k.StartsWith("Taglio:", StringComparison.Ordinal));
        Fill(ShearDashboard, k => k.StartsWith("Taglio:", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------- the calculation publishes (IConcreteCalculationHost)

    IReadOnlyDictionary<string, JsonObject[]> IConcreteCalculationHost.ActionRows() =>
        actions.ToDictionary(p => p.Key, p => p.Value.Select(r => (JsonObject)r.Values.DeepClone()).ToArray());

    void IConcreteCalculationHost.PublishDomain(string name, ConcreteDomainResult result)
    {
        string prefix = name[..3], key = name[3..];
        if (result.Three is not null) checker3D[key] = result.Three;
        if (result.Two is not null) checker2D[key] = result.Two;
        domainResults[prefix + key] = result.Checks;
        // PublishDomain of the historic view: the panel of the limit state shown attaches its rows again and is drawn again.
        var panel = DomainPanel(prefix);
        if (key == panel.Key) { panel.Attach(); panel.RefreshPanel(); }
    }

    void IConcreteCalculationHost.PublishStress(string key, Dictionary<string, StressOutcome> outcomes)
    {
        stressResults[key] = outcomes;
        using var notifications = JsonRowViewModel.DeferNotifications(actions[key]);
        foreach (var row in actions[key])
            foreach (var (field, value) in ConcreteStressPresentation.Outputs(outcomes[row.Values.S("id")])) row.Output(field, value);
        // PublishStress of the historic view: the family shows its selection again (S4).
        if (stressPanels.TryGetValue(key, out var panel)) panel.UpdateSelection();
    }

    void IConcreteCalculationHost.CalculateShear() => CalculateShear();

    void IConcreteCalculationHost.Published()
    {
        RefreshDomainViews();
        RefreshSummary();
        ResetExport();
        bool any = domainResults.Count != 0 || stressResults.Count != 0 || shearResults.Count != 0;
        if (hasResults != any) { hasResults = any; OnPropertyChanged(nameof(HasResults)); }
        OnPropertyChanged(nameof(Result));
    }

    void IConcreteCalculationHost.Blocked() { ResetResult(); RefreshSummary(); }

    // ---------------------------------------------------------------- actions (S2): rows of every limit state

    private void CreateActions()
    {
        actions.Clear();
        bool unified = SectionActionInputs.Unified(Data);
        var stored = ConcreteSheetPreparation.ReadActions(Data);
        foreach (string key in ConcreteEditor.Sets)
        {
            var rows = new JsonRowCollection(); actions[key] = rows;
            foreach (var item in stored[key])
            {
                var row = CreateAction(key, item.S("nome"), item.S("N"), item.S("Mx"), item.S("My"), item.S("id"), item.B("visible", true));
                if (unified) foreach (string field in ConcreteActionPresentation.UnifiedFields) row.Values[field] = item[field]?.DeepClone();
                rows.Add(row);
            }
        }
    }

    private JsonRowViewModel CreateAction(string key, string name, string n, string mx, string my, string? id = null, bool visible = true)
    {
        var values = ConcreteActionPresentation.NewRow(name, n, mx, my, id, visible);
        return new JsonRowViewModel(values, field =>
        {
            if (synchronizing) return;
            SyncActions(key);
            if (field == "visible") { foreach (var panel in DomainPanels) panel.NeedsVisualRefresh = true; RefreshDomainViews(); RaiseModified(); }
            else InvalidateActions(key);
        });
    }

    private IEnumerable<JsonRowViewModel> AllActionRows() => actions.Values.SelectMany(r => r);

    internal void SyncActions(string key) => Data["combinazioni"]![key] = ConcreteSheetPreparation.StoreActions(actions[key].Select(r => r.Values));

    /// <summary>The actions of <paramref name="key"/> changed (or the shear, «Taglio»): their checks are calculated again.</summary>
    internal void InvalidateActions(string key)
    {
        if (initializing || IsDisposed || synchronizing) return;
        RefreshDetailing();
        if (key == "Taglio") { InvalidateShear(); InvalidateChecks("Taglio"); return; }
        using var notifications = JsonRowViewModel.DeferNotifications(actions[key]);
        if (key is "SLU" or "SLV")
        {
            foreach (string prefix in new[] { "3D:", "2D:" }) domainResults.Remove(prefix + key);
            foreach (var row in actions[key]) foreach (string field in new[] { "eta3d", "eta2d", "esito3d", "esito2d" }) row.Output(field, "Da calcolare");
            RefreshDomainViews();
            InvalidateChecks("3D:" + key, "2D:" + key);
        }
        else
        {
            stressResults.Remove(key);
            foreach (var row in actions[key]) foreach (string field in new[] { "sigma_c", "sigma_s", "eta_sigma", "stress_status", "wk" }) row.Output(field, "Da calcolare");
            if (stressPanels.TryGetValue(key, out var panel)) panel.UpdateSelection();
            InvalidateChecks(key);
        }
        RefreshSummary();
    }

    // ---------------------------------------------------------------- domains (S3): the panels and their results

    /// <summary>The panel of the 3D domain (tab 02).</summary>
    public ConcreteDomainPanelViewModel Domain3D { get; private set; } = null!;
    /// <summary>The panel of the 2D domain (tab 03).</summary>
    public ConcreteDomainPanelViewModel Domain2D { get; private set; } = null!;

    // The panels, once built (the sheet builds them after the tables of the actions).
    private IEnumerable<ConcreteDomainPanelViewModel> DomainPanels => new[] { Domain3D, Domain2D }.Where(p => p is not null);

    internal ConcreteDomainPanelViewModel DomainPanel(string prefix) => prefix == "3D:" ? Domain3D : Domain2D;

    // The limit state shown by the panel of the domain (its option «stato»).
    private string DomainKey(string prefix) => Settings[prefix == "3D:" ? "dominio3d" : "dominio2d"].S("stato", "SLU");

    /// <summary>True while the sheet writes into its rows without their invalidations (the historic flag «synchronizing»).</summary>
    internal bool IsSynchronizing => synchronizing;

    /// <summary>The checks of a domain («3D:SLU», «2D:SLV»…), when calculated.</summary>
    internal Dictionary<string, ConcreteResults.Check>? DomainChecks(string name) => domainResults.GetValueOrDefault(name);

    /// <summary>The 3D domain of a limit state, when calculated.</summary>
    internal ConcreteResults.Domain3D? Checker3D(string key) => checker3D.GetValueOrDefault(key);

    /// <summary>The 2D domain of a limit state, when calculated.</summary>
    internal ConcreteResults.Domain2D? Checker2D(string key) => checker2D.GetValueOrDefault(key);

    // RefreshDomainViews of the historic view.
    private void RefreshDomainViews() { foreach (var panel in DomainPanels) panel.RefreshPanel(); }

    /// <summary>
    /// InvalidateDomainForces of the historic view: the resistant points and the ratios of both limit states of a panel are calculated again,
    /// the domain is kept; the outputs of its rows wait, the summaries follow, Modified.
    /// </summary>
    internal void InvalidateDomainForces(ConcreteDomainPanelViewModel panel)
    {
        using var notifications = JsonRowViewModel.DeferNotifications(AllActionRows());
        foreach (string key in new[] { "SLU", "SLV" })
        {
            domainResults.Remove(panel.Prefix + key);
            foreach (var row in actions[key]) { row.Output(panel.ThreeD ? "eta3d" : "eta2d", "—"); row.Output(panel.ThreeD ? "esito3d" : "esito2d", "Da calcolare"); }
        }
        panel.UpdateSelection(); RefreshSummary(); Status = "Aggiornamento punti resistenti e tassi · dominio conservato";
        InvalidateChecks(panel.Prefix + "SLU", panel.Prefix + "SLV");
    }

    /// <summary>The cache of the export only (a new mesh of the 3D domain).</summary>
    internal void ResetExportCache() => ResetExport();

    // ---------------------------------------------------------------- serviceability (S4): homogenization and contour at opening

    private void PrepareStressOptions()
    {
        preparedHomogenization.Clear();
        foreach (string key in StressSets)
        {
            // The values and the errors are shown by the forms of the families, built later (S4).
            preparedHomogenization[key] = ConcreteSheetPreparation.SynchronizeHomogenization(Input, Settings, key);
            var options = Settings["sle"]![key]!.AsObject();
            string contour = options.S("contour", ConcreteSectionDrawingViewModel.Contours[0]);
            options["contour"] = ConcreteSectionDrawingViewModel.Contours.Contains(contour) ? contour : ConcreteSectionDrawingViewModel.Contours[0];
        }
    }

    // ---------------------------------------------------------------- shear (S5): rows, automatic parameters, calculation on the UI thread (C2)

    /// <summary>The note of the automatic shear parameters (shown by the shear tab).</summary>
    public string ShearAutomaticNote { get; private set; } = "";

    private void PrepareShear()
    {
        ConcreteEditor.PrepareParameters(Input, Settings);
        shearBuilt = true;
        RefreshAutomaticShear();
        shearRows = [];
        foreach (var row in SectionActionInputs.ShearRows(Data)) shearRows.Add(ShearRow((JsonObject)row.DeepClone()));
    }

    private bool shearBuilt;

    private JsonRowViewModel ShearRow(JsonObject values) => new(ConcreteActionPresentation.ShearRow(values), _ =>
    {
        if (synchronizing || SectionActionInputs.Unified(Data)) return;
        ShearOptions["azioni"] = ConcreteSheetPreparation.StoreShearRows(shearRows.Select(r => r.Values));
        InvalidateActions("Taglio");
    });

    // RefreshAutomaticShear of the historic view: the fields of the panel of the shear (S5) follow the parameters; the geometric parameters
    // computed from the section (bw, d, Asl of both axes) and shown, or emptied when the section is not valid.
    private void RefreshAutomaticShear()
    {
        if (!shearBuilt) return;
        Shear?.EnableAutomaticFields();
        // With manual parameters, the note written by the wizard for its sections (S1b), otherwise the default one.
        if (!ConcreteShearPresentation.Automatic(ShearOptions)) { ShowShearNote(ConcreteInputPresentation.ManualShearNote(ShearOptions)); return; }
        try
        {
            var geometry = ConcreteSheetPreparation.UpdateAutomaticShear(Input, ShearOptions);
            AutomaticShearFailed = false;
            Shear?.ShowAutomaticValues();
            ShowShearNote(ConcreteShearPresentation.AutomaticNote(geometry.Shape));
        }
        catch (ArgumentException failure)
        {
            AutomaticShearFailed = true;
            foreach (string axis in new[] { "x", "y" }) foreach (string field in new[] { "bw_", "d_", "asl_" }) ShearOptions[field + axis] = "";
            Shear?.ClearAutomaticValues();
            ShowShearNote(failure.Message);
        }
    }

    private void ShowShearNote(string note) { ShearAutomaticNote = note; Shear?.RefreshNote(); }

    private void InvalidateShear()
    {
        using var notifications = JsonRowViewModel.DeferNotifications(shearRows);
        shearResults.Clear(); torsionResults.Clear();
        ShearDashboard.Text = ConcreteShearPresentation.Pending;
        Shear?.Invalidated();
        foreach (var row in shearRows) foreach (string key in new[] { "VRdx", "VRdy", "eta_x", "eta_y", "eta_t", "eta_vt", "esito" }) row.Output(key, "—");
    }

    // The shear and torsion of every row, on the UI thread inside the batch (decision C2); an error of the links stops the step. Then the
    // summaries of the tab, the first row selected when none is, the detail and the summaries of the checks (CalculateShear of the historic
    // view; its text of the worst checks is replaced at once by the summaries of the checks, and is not written).
    private void CalculateShear()
    {
        if (!shearBuilt) return;
        RefreshAutomaticShear();
        using var notifications = JsonRowViewModel.DeferNotifications(shearRows);
        InvalidateShear();
        ConcreteShearBatch.Calculate(Input, Settings, shearRows.Select(r => r.Values).ToArray(), (index, calculated, error) =>
        {
            var row = shearRows[index]; string id = row.Values.S("id");
            if (calculated is not null)
            {
                shearResults[id] = calculated.Shear;
                if (calculated.Torsion is not null) torsionResults[id] = calculated.Torsion;
                foreach (var (key, value) in ConcreteShearPresentation.Outputs(calculated.Shear, calculated.Torsion)) row.Output(key, value);
            }
            else
            {
                shearResults.Remove(id); torsionResults.Remove(id);
                foreach (var (key, value) in ConcreteShearPresentation.FailedOutputs(error)) row.Output(key, value);
            }
        });
        ShearDashboard.Text = ConcreteShearPresentation.ShearSummary(shearRows.Count, shearResults.Values);
        if (Shear is not null) Shear.Summary = ConcreteShearPresentation.Summary(shearRows.Count, shearResults.Values, torsionResults.Values);
        // The table of the shear selects its first row after a calculation, when none is selected (CalculateShear of the historic view).
        if (Actions?.Shear.Table is { Selected: null } table && shearRows.Count > 0) table.SelectedIndex = 0;
        Shear?.UpdateSelection();
        RefreshSummary();
    }

    // ---------------------------------------------------------------- detailing (S6): derived values and export

    /// <summary>The detailing of the sheet (checks, anchorages, durability) and its derived values written into the settings.</summary>
    internal void RefreshDetailing()
    {
        if (!detailingBuilt) return;
        var presentation = ConcreteDetailingPresentation.Calculate(Input, Settings, actions.ToDictionary(p => p.Key, p => p.Value.Select(r => r.Values).ToArray()));
        presentation.ApplyDerivedValues(Settings);
        detailing = presentation; ResetExport();
    }

    // ---------------------------------------------------------------- M–χ (S7): the curve is cleared by every change of the data

    private void InvalidateCurvature()
    {
        curvatureResult = null; ResetExport();
    }
}
