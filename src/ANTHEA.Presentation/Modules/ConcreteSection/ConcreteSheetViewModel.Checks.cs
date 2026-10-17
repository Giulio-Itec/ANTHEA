using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Calculations;
using Anthea.Presentation.Forms;

namespace Anthea.Presentation.Modules.ConcreteSection;

/// <summary>
/// What the tabs of the later sub-steps (S2 actions, S3 domains, S4 serviceability, S5 shear, S6 detailing, S7 M–χ, S9 summaries) do to the
/// data and to the results that the common part of the traces reads, until their ViewModels: the rows of the actions and of the shear with
/// their outputs, the results published by the calculation, the automatic shear parameters, the derived values of the detailing, the
/// summaries of the control panel and the export. Every write and its order are those of the historic view; the controls of those tabs
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
    private readonly Dictionary<string, (string Key, Dictionary<string, ConcreteResults.Check>? Checks)> domainOutputs = new(StringComparer.Ordinal);
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
        if (key == DomainKey(prefix)) RefreshDomainOutputs(prefix);
    }

    void IConcreteCalculationHost.PublishStress(string key, Dictionary<string, StressOutcome> outcomes)
    {
        stressResults[key] = outcomes;
        using var notifications = JsonRowViewModel.DeferNotifications(actions[key]);
        foreach (var row in actions[key])
        {
            var outcome = outcomes[row.Values.S("id")];
            row.Output("sigma_c", EngineeringFormat.Number(outcome.State?.sigma_cls)); row.Output("sigma_s", EngineeringFormat.Number(outcome.State?.sigma_acciaio));
            row.Output("eta_sigma", EngineeringFormat.Number(outcome.Ratio)); row.Output("stress_status", outcome.Status);
            row.Output("wk", outcome.CrackResult?.Width is double width
                ? $"{EngineeringFormat.Number(width)} / {EngineeringFormat.Number(outcome.CrackResult.Limit)} mm · η={EngineeringFormat.Number(outcome.CrackResult.Ratio)}" : outcome.Cracking);
        }
    }

    void IConcreteCalculationHost.CalculateShear() => CalculateShear();

    void IConcreteCalculationHost.Published()
    {
        RefreshDomainOutputs("3D:"); RefreshDomainOutputs("2D:");
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
        domainOutputs.Clear();
    }

    private JsonRowViewModel CreateAction(string key, string name, string n, string mx, string my, string? id = null, bool visible = true)
    {
        var values = ConcreteActionPresentation.NewRow(name, n, mx, my, id, visible);
        return new JsonRowViewModel(values, field =>
        {
            if (synchronizing) return;
            SyncActions(key);
            if (field == "visible") { RefreshDomainOutputs("3D:"); RefreshDomainOutputs("2D:"); RaiseModified(); }
            else InvalidateActions(key);
        });
    }

    private IEnumerable<JsonRowViewModel> AllActionRows() => actions.Values.SelectMany(r => r);

    private void SyncActions(string key) => Data["combinazioni"]![key] = ConcreteSheetPreparation.StoreActions(actions[key].Select(r => r.Values));

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
            RefreshDomainOutputs("3D:"); RefreshDomainOutputs("2D:");
            InvalidateChecks("3D:" + key, "2D:" + key);
        }
        else
        {
            stressResults.Remove(key);
            foreach (var row in actions[key]) foreach (string field in new[] { "sigma_c", "sigma_s", "eta_sigma", "stress_status", "wk" }) row.Output(field, "Da calcolare");
            InvalidateChecks(key);
        }
        RefreshSummary();
    }

    // ---------------------------------------------------------------- domains (S3): the outputs of the rows

    // The limit state shown by the panel of the domain (its option «stato»).
    private string DomainKey(string prefix) => Settings[prefix == "3D:" ? "dominio3d" : "dominio2d"].S("stato", "SLU");

    // The outputs of the rows of the domain shown, written again when the limit state or its results change (RefreshDomainPanel).
    private void RefreshDomainOutputs(string prefix)
    {
        string key = DomainKey(prefix);
        domainResults.TryGetValue(prefix + key, out var checks);
        if (domainOutputs.TryGetValue(prefix, out var shown) && shown.Key == key && ReferenceEquals(shown.Checks, checks)) return;
        using var notifications = JsonRowViewModel.DeferNotifications(actions[key]);
        string eta = prefix == "3D:" ? "eta3d" : "eta2d", outcome = prefix == "3D:" ? "esito3d" : "esito2d";
        foreach (var row in actions[key])
        {
            var check = checks?.GetValueOrDefault(row.Values.S("id"));
            row.Output(eta, EngineeringFormat.Number(check?.Utilization)); row.Output(outcome, check?.Status ?? "Da calcolare");
        }
        domainOutputs[prefix] = (key, checks);
    }

    // ---------------------------------------------------------------- serviceability (S4): homogenization and contour at opening

    private void PrepareStressOptions()
    {
        foreach (string key in StressSets)
        {
            ConcreteSheetPreparation.SynchronizeHomogenization(Input, Settings, key);
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

    // The geometric parameters of the shear computed from the section (bw, d, Asl of both axes), or emptied when the section is not valid.
    private void RefreshAutomaticShear()
    {
        if (!shearBuilt) return;
        if (ShearOptions.S("parametri", "Automatici da sezione") != "Automatici da sezione") { ShearAutomaticNote = "Parametri manuali: verificare geometria, armatura tesa e ancoraggio."; return; }
        try
        {
            var geometry = ConcreteSheetPreparation.UpdateAutomaticShear(Input, ShearOptions);
            ShearAutomaticNote = geometry.Shape == "Circolare"
                ? "Circolare: bw = D (piena) o D−Di (cava); d dal baricentro delle barre di ciascun semicerchio, minimo fra i due versi. Asl minima dei due semicerchi. Suggerimenti geometrici: scegliere il modello e confermare la schematizzazione, oppure impostare parametri manuali."
                : "Automatici: bw minima (somma delle pareti per sezione cava); d dal baricentro delle barre di lembo, minimo fra i due versi; Asl minima dei due lembi. Per T: Vx usa lo spessore ala, Vy l’anima. Ancoraggio e disposizione resistente restano da verificare.";
        }
        catch (ArgumentException failure)
        {
            foreach (string axis in new[] { "x", "y" }) foreach (string field in new[] { "bw_", "d_", "asl_" }) ShearOptions[field + axis] = "";
            ShearAutomaticNote = failure.Message;
        }
    }

    private void InvalidateShear()
    {
        using var notifications = JsonRowViewModel.DeferNotifications(shearRows);
        shearResults.Clear(); torsionResults.Clear();
        ShearDashboard.Text = "Taglio e torsione · da calcolare";
        foreach (var row in shearRows) foreach (string key in new[] { "VRdx", "VRdy", "eta_x", "eta_y", "eta_t", "eta_vt", "esito" }) row.Output(key, "—");
    }

    // The shear and torsion of every row, on the UI thread inside the batch (decision C2); an error of the links stops the step.
    private void CalculateShear()
    {
        if (!shearBuilt) return;
        RefreshAutomaticShear();
        using var notifications = JsonRowViewModel.DeferNotifications(shearRows);
        InvalidateShear();
        ConcreteShearBatch.Calculate(Input, Settings, shearRows.Select(r => r.Values).ToArray(), (index, calculated, error) =>
        {
            var row = shearRows[index];
            if (calculated is not null)
            {
                var results = calculated.Shear;
                shearResults[row.Values.S("id")] = results;
                for (int i = 0; i < results.Length; i++)
                {
                    string axis = i == 0 ? "x" : "y";
                    row.Output("VRd" + axis, results[i].VRd.ToString("0.00"));
                    row.Output("eta_" + axis, results[i].Ratio?.ToString("0.00") ?? "—");
                }
                row.Output("esito", string.Join(" · ", results.Select((r, i) => (i == 0 ? "x: " : "y: ") + r.Status)));
                ShowTorsion(row, calculated.Torsion);
            }
            else
            {
                shearResults.Remove(row.Values.S("id")); torsionResults.Remove(row.Values.S("id"));
                foreach (string key in new[] { "VRdx", "VRdy", "eta_x", "eta_y", "eta_t", "eta_vt" }) row.Output(key, "—");
                row.Output("esito", error);
            }
        });
        RefreshSummary();
    }

    private void ShowTorsion(JsonRowViewModel row, ConcreteResults.TorsionResult? result)
    {
        if (result is null) { row.Output("eta_t", "—"); row.Output("eta_vt", "—"); return; }
        torsionResults[row.Values.S("id")] = result;
        row.Output("eta_t", result.TorsionRatio?.ToString("0.00") ?? "∞");
        row.Output("eta_vt", result.ConcreteCombinedRatio is double ec && result.SteelCombinedRatio is double es ? Math.Max(ec, es).ToString("0.00") : "∞");
        row.Output("esito", row.Values.S("esito") + " · " + result.Status);
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
