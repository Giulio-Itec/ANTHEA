using System.Collections.Immutable;
using System.ComponentModel;
using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Calculations;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Services;

namespace Anthea.Presentation.Modules.ConcreteSection;

/// <summary>
/// The sheet of the RC section (module <c>str_palo</c>, F5.10), sub-step S1: the shell of the historic ConcreteWorkspace without WPF.
/// <list type="bullet">
/// <item>Data: the working copy of the context, prepared at opening with the same writes in the same order as the historic view (its
/// constructor and the construction of every tab), so that the document is byte for byte the same; <see cref="Commit"/> leaves the rows of
/// the actions and of the shear in the data, as the historic Commit.</item>
/// <item>Main tab (<see cref="SelectedTab"/>, saved as <c>tab</c>), status line, progress, read-only revision (<see cref="SheetViewModelBase.IsReadOnly"/>
/// on every form), closing.</item>
/// <item>Calculation: <see cref="ConcreteCalculation"/> (decision C1); the sheet is in mode <see cref="SheetCalculationMode.None"/> and gives
/// <see cref="Result"/>, <see cref="HasResults"/> and «Calcola» (<see cref="CalculateAsync"/>) of its own. The result is the historic export
/// (ConcreteResultExport), built when read and kept until the next invalidation, as the historic view.</item>
/// <item>Children: <see cref="Inputs"/> (inputs and materials, with the sections of the wizard: S1, S1b), <see cref="Actions"/> (S2), <see cref="Domain3D"/>
/// and <see cref="Domain2D"/> (S3), <see cref="StressPanels"/> (S4), <see cref="Shear"/> (S5), <see cref="Detailing"/> (S6), the section σ/ε of the domain panels and <see cref="Curvature"/> (S7), <see cref="Design"/> (S8); the tab of
/// the last sub-step (S9) keeps, until
/// their ViewModels, only the writes into the document and the results that the common part of the traces reads (ConcreteSheetViewModel.Checks.cs).</item>
/// </list>
/// Works without a project and without a window (requirement of section 5 for the verifier of the elastic pile): the host is only asked for
/// the report (not in S1) and the non modal windows are requests the view may ignore.
/// </summary>
public sealed partial class ConcreteSheetViewModel : SheetViewModelBase, IConcreteCalculationHost
{
    /// <summary>The titles of the main tabs, in order.</summary>
    public static ImmutableArray<string> TabTitles { get; } = ["Pannello di controllo", "Dominio 3D", "Dominio 2D", "Tensioni e fessurazione", "Taglio e torsione",
        "Dettagli costruttivi", "Momento–curvatura", "Calcola armature"];

    /// <summary>The status line when the sheet opens.</summary>
    public const string OpeningStatus = "Dati della sezione · selezionare una scheda per iniziare";

    private readonly ConcreteAnalysisSession session;
    private readonly SynchronizationContext? ui;
    private bool initializing = true, synchronizing;
    private int tab;
    private string status = OpeningStatus;

    /// <param name="context">Data (owned copy), host, services; the calculation service of the context belongs to <c>str_palo</c> and is not used.</param>
    public ConcreteSheetViewModel(SheetContext context) : this(context, new ConcreteAnalysisSession()) { }

    /// <param name="context">Data (owned copy), host, services.</param>
    /// <param name="session">The analysis session of the sheet (the checks hold its batches to fix a calculation in progress).</param>
    public ConcreteSheetViewModel(SheetContext context, ConcreteAnalysisSession session) : base(ConcreteSectionSheetModule.Id, context, mode: SheetCalculationMode.None)
    {
        ArgumentNullException.ThrowIfNull(session);
        this.session = session;
        ui = SynchronizationContext.Current;
        PropertyChanged += OnOwnPropertyChanged;
        Open();
    }

    /// <summary>The settings of the section in the data (<c>workspace_ca</c>).</summary>
    public JsonObject Settings { get; private set; } = null!;
    /// <summary>The inputs of the section in the data (<c>input</c>).</summary>
    public JsonObject Input => Data["input"]!.AsObject();
    /// <summary>The shear options in the settings (<c>taglio</c>): also the scheme of the links.</summary>
    internal JsonObject ShearOptions => Settings["taglio"]!.AsObject();

    /// <summary>Inputs and materials (sub-step S1).</summary>
    public ConcreteInputViewModel Inputs { get; private set; } = null!;
    /// <summary>The calculation of the sheet (decision C1).</summary>
    public ConcreteCalculation Calculation { get; private set; } = null!;

    /// <summary>The main tab (0 control panel … 7 reinforcement design). A change by the user commits the sheet, refreshes the detailing and
    /// saves <c>tab</c> with Modified, also in a read-only revision (historic behaviour, kept as C5).</summary>
    public int SelectedTab
    {
        get => tab;
        set
        {
            if (value < 0 || value >= TabTitles.Length || tab == value) return;
            tab = value; OnPropertyChanged();
            if (initializing || IsDisposed) return;
            CommitSheet(); RefreshDetailing(); Settings["tab"] = value; RaiseModified();
            ShowPanel(value);
        }
    }

    /// <summary>The status line of the sheet.</summary>
    public string Status { get => status; private set => SetProperty(ref status, value); }

    /// <summary>True while a batch of checks runs.</summary>
    public override bool IsBusy => Calculation?.IsBusy ?? false;
    /// <summary>A recalculation waits for its turn of the UI thread.</summary>
    public bool IsCalculationQueued => Calculation.IsQueued;
    /// <summary>The progress bar under the tabs.</summary>
    public bool IsProgressVisible => Calculation.IsProgressVisible;

    /// <summary>The engine reference shown above the tabs.</summary>
    public string EngineReference => "Motore di calcolo: GPC Engine";

    public override IModuleReportSource Report { get; } = new PendingReport();

    /// <summary>The services of the sheet for its children.</summary>
    internal IWikiNavigator Wiki => Context.Services.Wiki;
    internal IDialogService Dialogs => Context.Services.Dialogs;

    /// <summary>True while the sheet is built or reloaded (the historic flag «initializing»): the callbacks of the forms do nothing.</summary>
    internal bool Initializing { get => initializing; set => initializing = value; }
    bool IConcreteCalculationHost.Initializing => initializing;

    /// <summary>«Reologia del calcestruzzo…»: the view opens the non modal window of F5.5 with the strength (fck as a magnitude).</summary>
    public event EventHandler<double>? RheologyRequested;

    /// <summary>«Calcola»: waits for the calculation in progress, then calculates the checks still pending (none when every check is current).</summary>
    public override async Task<bool> CalculateAsync(CancellationToken cancellation = default)
    {
        ThrowIfDisposed();
        long before = Calculation.Revision;
        await Calculation.CalculateAllAsync();
        return !IsDisposed && HasResults && Calculation.Revision == before;
    }

    /// <summary>Signals a change of the user that the results already reflect, or of interface state saved in the data (Modified only).</summary>
    internal void RaiseModified() { if (!IsDisposed) ChangedKeepingResults(); }

    internal void RequestRheology(double strength) => RheologyRequested?.Invoke(this, strength);

    /// <summary>«Proprietà / report…»: the view shows the non modal window of the properties and disposes it when the window closes.</summary>
    public event EventHandler<ConcreteSectionPropertiesViewModel>? PropertiesRequested;

    // The historic window commits the sheet, takes a copy of the data and calculates at once; without a view nothing opens.
    internal void OpenProperties()
    {
        if (!CanEdit || PropertiesRequested is null) return;
        CommitSheet();
        var properties = new ConcreteSectionPropertiesViewModel(Input, Settings, RaiseModified, Context.Services);
        PropertiesRequested.Invoke(this, properties);
        _ = properties.RefreshAsync();
    }

    // ---------------------------------------------------------------- opening

    // The constructor of the historic ConcreteWorkspace (and of every tab of it), in its order.
    private void Open()
    {
        initializing = true;
        // A new opening shows the status of a new historic workspace (also after Reload).
        Status = OpeningStatus;
        ResetState();
        Settings = ConcreteEditor.PrepareWorkspace(Data);
        PrepareCoefficients();
        ConcreteEditor.PrepareParameters(Input, Settings);
        CreateActions();
        Calculation = new ConcreteCalculation(session, this, ui);
        Calculation.PropertyChanged += OnCalculationChanged;
        // 01 · Pannello di controllo.
        Inputs = new ConcreteInputViewModel(this);
        CreateSummaries();
        // 02, 03 · Domini 3D e 2D: no write.
        // 04 · Tensioni e fessurazione: homogenization and contour of every family.
        PrepareStressOptions();
        // 05 · Taglio e torsione.
        PrepareShear();
        // The tables of the actions of tabs 02–05 (S2) and the panels of the domains (S3): no write.
        Actions = new ConcreteActionsViewModel(this);
        Domain3D = new ConcreteDomainPanelViewModel(this, Actions.Domain3D, true);
        Domain2D = new ConcreteDomainPanelViewModel(this, Actions.Domain2D, false);
        // 04 · Tensioni e fessurazione (S4): the families on the tables of the actions, the homogenization of the opening shown; no write.
        CreateStressPanels();
        // 05 · Taglio e torsione (S5) and the links shown read-only in 06: no write.
        CreateShearPanel();
        // 06 · Dettagli costruttivi (S6): no write; from here the detailing is refreshed.
        CreateDetailingPanel();
        // 07 · Momento–curvatura (S7): no write. 08 · Calcola armature.
        CreateCurvaturePanel();
        ConcreteEditor.PrepareDesign(Settings);
        CreateDesignPanel();
        tab = ConcreteSheetPresentation.InitialTab(Settings);
        OnPropertyChanged(nameof(SelectedTab)); OnPropertyChanged(nameof(Inputs)); OnPropertyChanged(nameof(Calculation)); OnPropertyChanged(nameof(Actions));
        OnPropertyChanged(nameof(Domain3D)); OnPropertyChanged(nameof(Domain2D));
        initializing = false; RefreshPreview(); RefreshSummary();
        // The view shown and loaded: the panel of the tab and the calculation of the opening (IsVisibleChanged and Loaded of the historic view).
        ViewLoaded();
    }

    private void PrepareCoefficients()
    {
        if (Settings["coefficienti"] is not JsonObject) Settings["coefficienti"] = ConcreteEditor.StandardDefaults(ConcreteEditor.RequiredStandardName(Settings));
        foreach (var (input, standard) in ConcreteEditor.CommonCoefficients) Settings["coefficienti"]![standard] = Input[input]?.DeepClone();
    }

    // ---------------------------------------------------------------- invalidation (ConcreteWorkspace.cs)

    /// <summary>
    /// The user changed the data: every check is calculated again, the results are cleared, the preview of the section is refreshed (unless
    /// <paramref name="geometryChanged"/> is false: the coordinates of a bar, whose preview follows one turn later), Modified, recalculation
    /// queued. Nothing while the sheet is built, closed or synchronizing its tables.
    /// </summary>
    internal void Invalidate(bool geometryChanged = true)
    {
        using var notifications = JsonRowViewModel.DeferNotifications(AllActionRows());
        if (initializing || IsDisposed || synchronizing) return;
        Calculation.InvalidateAll();
        ResetResult(); ClearResults(); InvalidateShear();
        synchronizing = true;
        try
        {
            foreach (var row in AllActionRows())
                foreach (string key in new[] { "eta3d", "eta2d", "esito3d", "esito2d", "sigma_c", "sigma_s", "eta_sigma", "stress_status", "wk" })
                    row.Output(key, key.StartsWith("esito", StringComparison.Ordinal) || key == "stress_status" || key == "wk" ? "Da calcolare" : "—");
        }
        finally { synchronizing = false; }
        // The panels of the domains: no mesh, no chart, the detail waits for the calculation; the families of the serviceability: no stress state.
        foreach (var panel in DomainPanels) panel.Invalidated();
        foreach (var panel in StressPanels) panel.Invalidated();
        InvalidateCurvature();
        if (!geometryChanged) _ = Inputs.RefreshQuickResistanceAsync();
        // The section σ/ε of both panels cleared (with the section of the preview before this change), then solved again when shown (S7).
        foreach (var panel in DomainPanels) _ = panel.Inspection.UpdateAsync();
        if (geometryChanged) RefreshPreview();
        RefreshSummary(); Status = "Modifiche acquisite · aggiornamento automatico in attesa…"; RaiseModified(); Calculation.Queue();
    }

    /// <summary>Only <paramref name="affected"/> are calculated again (every check when empty); results kept, Modified, recalculation queued.</summary>
    internal void InvalidateChecks(params string[] affected)
    {
        if (initializing || IsDisposed || synchronizing) return;
        Calculation.Invalidate(affected);
        ResetResult(); Calculation.Queue(); RaiseModified();
    }

    /// <summary>The preview of the section and everything that follows the geometry and the materials (RefreshPreview of the historic view).</summary>
    internal void RefreshPreview()
    {
        if (IsDisposed) return;
        ConcreteInputPresentation.NormalizeHole(Input);
        Inputs.RefreshHoleControls();
        Inputs.RefreshAdditionalLayers();
        Inputs.RefreshShapeFields();
        ConcreteGeometry? engine;
        try
        {
            var values = ConcretePreview.Read(Input, Settings);
            engine = values.Geometry; Inputs.Drawing.Message = "";
            Inputs.ShowPreviewValues(values);
        }
        catch (ArgumentException failure) { engine = null; Inputs.Drawing.Message = failure.Message; Inputs.ShowPreviewValues(null); }
        Inputs.Drawing.Section = engine; Inputs.Drawing.Tendons = Inputs.TendonPoints();
        SynchronizeStirrups();
        foreach (var panel in StressPanels) panel.SynchronizeHomogenization();
        foreach (string field in ConcreteEditor.SharedSleFields) Settings["sle_comuni"]![field] = Settings["sle"]!["SLE"]![field]?.DeepClone();
        foreach (var panel in StressPanels) panel.ShowTendonOptions(Inputs.Tendons.Rows.Count > 0);
        Inputs.RefreshTendonOptions(); RefreshAutomaticShear();
        RefreshDetailing();
        Inputs.Drawing.Invalidate();
        _ = Inputs.RefreshQuickResistanceAsync();
        Inputs.RefreshBars(engine);
        // The stress maps of the serviceability take the section of the preview (S4).
        foreach (var panel in StressPanels) panel.ShowSection(Inputs.Drawing);
    }

    /// <summary>The preview refreshed one turn of the UI thread later (after the edit of a cell of the bars).</summary>
    internal void PostRefreshPreview()
    {
        if (ui is null) RefreshPreview();
        else ui.Post(_ => RefreshPreview(), null);
    }

    /// <summary>The links of the panel and of the drawings from the data.</summary>
    internal void SynchronizeStirrups()
    {
        Inputs.SynchronizeStirrups(ShearOptions);
        // The links shown read-only in the tabs 05 and 06 (S5), then the drawings.
        foreach (var form in ReadOnlyStirrupForms()) ConcreteInputViewModel.SynchronizeStirrupForm(form, Input, ShearOptions);
        Inputs.Drawing.Stirrups = ShearOptions; Inputs.Drawing.Invalidate();
        Shear?.ShowStirrups(ShearOptions);
        foreach (var panel in StressPanels) panel.ShowStirrups(ShearOptions);
    }

    /// <summary>«Staffe presenti» set to «No»: the shear model without links, shown in the form of the shear without its callback (S5).</summary>
    internal void StirrupsAbsent()
    {
        ShearOptions["modello"] = "Senza staffe";
        Shear?.Form.Set("modello", "Senza staffe", true);
    }

    // ---------------------------------------------------------------- commit, reload, read-only, closing

    protected override void OnCommit() => CommitSheet();

    // The Commit of the historic view: the drafts of the forms, then the rows of the actions and of the shear into the data.
    internal void CommitSheet()
    {
        Inputs.Commit();
        foreach (var panel in DomainPanels) panel.Form.Commit();
        foreach (string key in actions.Keys) SyncActions(key);
        if (!SectionActionInputs.Unified(Data)) ShearOptions["azioni"] = ConcreteSheetPreparation.StoreShearRows(shearRows.Select(r => r.Values));
    }

    /// <summary>The data were replaced (shared data, the editor of the actions): the sheet opens them again, as the historic view builds a new workspace.</summary>
    protected override void OnReloaded()
    {
        CloseChildren();
        Open();
    }

    private void OnOwnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IsReadOnly)) return;
        Inputs?.RefreshEditable(IsReadOnly);
        Actions?.RefreshEditable();
        foreach (var panel in DomainPanels) panel.RefreshEditable(IsReadOnly);
        foreach (var panel in StressPanels) panel.RefreshEditable(IsReadOnly);
        Shear?.RefreshEditable(IsReadOnly);
        Detailing?.RefreshEditable(IsReadOnly);
        Curvature?.RefreshEditable(IsReadOnly);
        Design?.RefreshEditable(IsReadOnly);
    }

    private void OnCalculationChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ConcreteCalculation.IsBusy): OnPropertyChanged(nameof(IsBusy)); break;
            case nameof(ConcreteCalculation.IsQueued): OnPropertyChanged(nameof(IsCalculationQueued)); break;
            case nameof(ConcreteCalculation.IsProgressVisible): OnPropertyChanged(nameof(IsProgressVisible)); break;
        }
    }

    private void CloseChildren()
    {
        Calculation.PropertyChanged -= OnCalculationChanged;
        Calculation.Dispose();
        Inputs.DisposeSchedulers();
        // The section σ/ε of the panels and the curve: what is running is never published (S7).
        foreach (var panel in DomainPanels) panel.Inspection.Close();
        Curvature.Dispose();
        // The search of the reinforcement design: cancelled, never published (S8).
        Design.Dispose();
    }

    protected override void OnDisposed()
    {
        CloseChildren();
        PropertyChanged -= OnOwnPropertyChanged;
        base.OnDisposed();
    }

    void IConcreteCalculationHost.ShowStatus(string text) => Status = text;

    // The report of the sheet comes with sub-step S9 (frozen reports, ReportConcrete with the figures of the view).
    private sealed class PendingReport : IModuleReportSource
    {
        public byte[] BuildReport(string title, IReadOnlySet<string> options, bool projectReport = false) =>
            throw new InvalidOperationException("Report della sezione c.a. non ancora disponibile nel ViewModel (sottopasso S9 di F5.10).");
    }
}
