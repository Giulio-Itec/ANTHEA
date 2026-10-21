using System.Text.Json.Nodes;
using Anthea.Application.Presentation;
using Anthea.Calculations;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules.ConcreteSection;

namespace Anthea.Presentation.Modules.CompositeSection;

/// <summary>
/// The results and the properties of the section (ShowResults, ClearResults and BridgeSectionProperties of the historic view): the summary of
/// the situation, the panels of the results as blocks (<see cref="BridgePanel"/>), the properties of the steel, of the slab and of the phases.
/// </summary>
public sealed partial class CompositeSectionSheetViewModel
{
    private BridgePanel? stressPanel, verificationPanel, homogenizationPanel, equilibriumPanel, class4Panel, geometryPanel, shearPanel, studsPanel, torsionPanel;
    private string overview = "", warningText = "", convergenceText = "", propertyNotice = "";
    private bool torsionResultsShown = true, propertiesBuilt;
    private FormViewModel? slabPropertiesForm;
    private JsonObject? slabOptions;
    private IReadOnlyList<PresentationTable> steelPropertyTables = [], slabPropertyTables = [];
    private string? slabPropertyError;
    private IReadOnlyList<CompositeSectionPropertyTab> propertyTabs = [];

    /// <summary>The cards of the summary of the situation (the VerificationCards of the historic view), or its message.</summary>
    public ConcreteCardsViewModel SummaryCards { get; } = new();

    /// <summary>The overview of the situation under the cards: maximum stresses, effective web, convergence; «Risultati da aggiornare» after a change without results.</summary>
    public string Overview { get => overview; private set => SetProperty(ref overview, value); }

    /// <summary>The warnings of the situation.</summary>
    public string WarningText { get => warningText; private set => SetProperty(ref warningText, value); }

    /// <summary>«Convergenza ed equilibrio» of the tab «Sezione efficace».</summary>
    public string ConvergenceText { get => convergenceText; private set => SetProperty(ref convergenceText, value); }

    /// <summary>The stresses of the tab «Tensioni» (the tab «Sollecitazioni» is the table of the actions).</summary>
    public BridgePanel? StressPanel { get => stressPanel; private set => SetProperty(ref stressPanel, value); }

    /// <summary>The checks of the tab «Verifiche».</summary>
    public BridgePanel? VerificationPanel { get => verificationPanel; private set => SetProperty(ref verificationPanel, value); }

    /// <summary>«Omogeneizzazione e proprietà per fase» of the tab «Fasi e proprietà».</summary>
    public BridgePanel? HomogenizationPanel { get => homogenizationPanel; private set => SetProperty(ref homogenizationPanel, value); }

    /// <summary>«Azioni ed equilibrio» of the tab «Fasi e proprietà».</summary>
    public BridgePanel? EquilibriumPanel { get => equilibriumPanel; private set => SetProperty(ref equilibriumPanel, value); }

    /// <summary>«Pannelli di classe 4» of the tab «Sezione efficace».</summary>
    public BridgePanel? Class4Panel { get => class4Panel; private set => SetProperty(ref class4Panel, value); }

    /// <summary>«Proprietà geometriche · lorda ed efficace» of the tab «Sezione efficace».</summary>
    public BridgePanel? GeometryPanel { get => geometryPanel; private set => SetProperty(ref geometryPanel, value); }

    /// <summary>«Taglio · parametri e resistenze» of the tab «Verifiche».</summary>
    public BridgePanel? ShearPanel { get => shearPanel; private set => SetProperty(ref shearPanel, value); }

    /// <summary>«Pioli · flusso per fase e resistenze» of the tab «Verifiche».</summary>
    public BridgePanel? StudsPanel { get => studsPanel; private set => SetProperty(ref studsPanel, value); }

    /// <summary>«Cassoncino · torsione, distorsione e diaframmi» of the tab «Verifiche».</summary>
    public BridgePanel? TorsionPanel { get => torsionPanel; private set => SetProperty(ref torsionPanel, value); }

    /// <summary>The group of the results of the torsion is shown (the box girder; shown until the first result, as the historic view).</summary>
    public bool TorsionResultsShown { get => torsionResultsShown; private set => SetProperty(ref torsionResultsShown, value); }

    /// <summary>ShowResults of the historic view: the results of the situation shown, with the drawing.</summary>
    private void ShowResults()
    {
        if (displayed is not { } result || selectedSituation < 0 || selectedSituation >= result.Stages.Count) return;
        RefreshDrawing();
        var shown = BridgeResultPresentation.Show(result, selectedSituation);
        Overview = shown.Overview;
        SummaryCards.Start();
        foreach (var summary in shown.Summaries) SummaryCards.AddSummary(summary);
        if (shown.SummaryText is { } text) SummaryCards.Text = text;
        ShearPanel = shown.Shear; StudsPanel = shown.Studs;
        TorsionResultsShown = shown.TorsionShown; TorsionPanel = shown.Torsion;
        WarningText = shown.Warnings;
        StressPanel = shown.Stress;
        VerificationPanel = shown.Verification;
        HomogenizationPanel = shown.Properties;
        Class4Panel = shown.Class4;
        EquilibriumPanel = shown.Phases;
        GeometryPanel = shown.Geometry;
        ConvergenceText = shown.Details;
    }

    /// <summary>ClearResults of the historic view: no situation in the drawing, «Nessun risultato aggiornato» in every panel.</summary>
    private void ClearResults()
    {
        SetDrawing(drawing with { Stage = null });
        Overview = BridgeResultPresentation.OverviewToUpdate; WarningText = "";
        StressPanel = HomogenizationPanel = Class4Panel = EquilibriumPanel = GeometryPanel = VerificationPanel = ShearPanel = StudsPanel = TorsionPanel = BridgeResultPresentation.Empty;
        ConvergenceText = "";
        SummaryCards.Text = BridgeResultPresentation.NoResult;
    }

    /// <summary>The results before an opening (Reload): as the controls of a new historic view.</summary>
    private void ResetResults()
    {
        StressPanel = HomogenizationPanel = Class4Panel = EquilibriumPanel = GeometryPanel = VerificationPanel = ShearPanel = StudsPanel = TorsionPanel = null;
        Overview = WarningText = ConvergenceText = PropertyNotice = "";
        SummaryCards.Start(); TorsionResultsShown = true;
        propertiesBuilt = false; slabPropertiesForm = null; slabOptions = null;
        SteelPropertyTables = []; SlabPropertyTables = []; SlabPropertyError = null; PropertyTabs = []; selectedPropertyTab = -1;
        OnPropertyChanged(nameof(SlabPropertiesForm)); OnPropertyChanged(nameof(SelectedPropertyTab));
    }

    // ---------------------------------------------------------------- properties of the section

    /// <summary>The tabs of the properties of the section: «Acciaio», «Soletta», then one for each phase.</summary>
    public IReadOnlyList<CompositeSectionPropertyTab> PropertyTabs { get => propertyTabs; private set => SetProperty(ref propertyTabs, value); }

    /// <summary>The tab of the properties shown (also when read-only); the same phase stays chosen when the tabs are built again.</summary>
    public int SelectedPropertyTab
    {
        get => selectedPropertyTab;
        set
        {
            if (value == selectedPropertyTab || value < 0 || value >= propertyTabs.Count) return;
            selectedPropertyTab = value; OnPropertyChanged();
        }
    }

    /// <summary>The note under the properties: what they are, or why they wait for valid data.</summary>
    public string PropertyNotice { get => propertyNotice; private set => SetProperty(ref propertyNotice, value); }

    /// <summary>The tables of the steel (the last valid ones when the data are not).</summary>
    public IReadOnlyList<PresentationTable> SteelPropertyTables { get => steelPropertyTables; private set => SetProperty(ref steelPropertyTables, value); }

    /// <summary>φ, ψL and n of the exploration of the slab (saved in <c>ui_mista</c>, with Modified, without recalculation).</summary>
    public FormViewModel? SlabPropertiesForm => slabPropertiesForm;

    /// <summary>The tables of the slab; empty with <see cref="SlabPropertyError"/>.</summary>
    public IReadOnlyList<PresentationTable> SlabPropertyTables { get => slabPropertyTables; private set => SetProperty(ref slabPropertyTables, value); }

    /// <summary>The error that stops the properties of the slab, null without.</summary>
    public string? SlabPropertyError { get => slabPropertyError; private set => SetProperty(ref slabPropertyError, value); }

    /// <summary>The note under the form of the slab.</summary>
    public string SlabPropertiesNote => BridgeInputPresentation.SlabPropertiesNote;

    /// <summary>BuildSectionProperties of the historic view: the options of the slab saved in the sheet, its form, the tabs of steel and slab.</summary>
    private void BuildSectionProperties()
    {
        var options = BridgeSheetPresentation.SlabOptions(viewSettings);
        slabOptions = options;
        slabPropertiesForm = Configure(new FormViewModel(options, CompositeSectionFields.SlabProperties, key =>
        {
            BridgeSheetPresentation.HomogenizationEdited(options, key); RefreshSlabProperties(); StateChanged();
        }, compact: true));
        OnPropertyChanged(nameof(SlabPropertiesForm));
        PropertyTabs = [new(BridgeInputPresentation.SteelPropertiesHeader, null, null, null), new(BridgeInputPresentation.SlabPropertiesHeader, null, null, null)];
        selectedPropertyTab = 0; OnPropertyChanged(nameof(SelectedPropertyTab));
        propertiesBuilt = true;
    }

    private void RefreshSlabProperties()
    {
        if (slabOptions is null || slabPropertiesForm is null) return;
        try
        {
            var h = BridgeSheetPresentation.UpdateHomogenization(Data, slabOptions, false);
            string derived = slabOptions.S("modo") == "Da n" ? "phi" : "n";
            slabPropertiesForm.Set(derived, slabOptions.S(derived), true);
            SlabPropertyTables = BridgePropertyPresentation.Slab(Data, slabOptions, h.N); SlabPropertyError = null;
        }
        catch (Exception error) when (error is not OutOfMemoryException) { SlabPropertyTables = []; SlabPropertyError = error.Message; }
    }

    /// <summary>RefreshSectionProperties of the historic view: steel, slab and one tab for each phase, the phase chosen kept.</summary>
    private void RefreshSectionProperties()
    {
        if (!propertiesBuilt) return;
        try
        {
            var properties = BridgePropertyPresentation.Steel(Data);
            SteelPropertyTables = properties.Tables;
            PropertyNotice = properties.Notice;
        }
        catch (Exception error) when (error is not OutOfMemoryException) { PropertyNotice = BridgeInputPresentation.PropertiesStale(error.Message); }
        RefreshSlabProperties();
        var selection = selectedPropertyTab >= 0 && selectedPropertyTab < propertyTabs.Count ? propertyTabs[selectedPropertyTab].Phase : null;
        int selected = selectedPropertyTab;
        var tabs = propertyTabs.Take(2).ToList();
        int i = 0;
        foreach (var phase in Data.Array("fasi").OfType<JsonObject>())
        {
            string header = BridgeInputPresentation.PropertyTabHeader(i++, phase);
            try { tabs.Add(new(header, phase, BridgePropertyPresentation.Phase(Data, phase), null)); }
            catch (Exception error) when (error is not OutOfMemoryException) { tabs.Add(new(header, phase, null, error.Message)); }
            if (ReferenceEquals(selection, phase)) selected = i + 1;
        }
        PropertyTabs = tabs;
        selectedPropertyTab = Math.Clamp(selected, 0, tabs.Count - 1); OnPropertyChanged(nameof(SelectedPropertyTab));
    }
}
