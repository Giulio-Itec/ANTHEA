using System.ComponentModel;
using System.Text;
using System.Text.Json.Nodes;
using Anthea.Application.Documents;
using Anthea.Application.Modules;
using Anthea.Application.Presentation;
using Anthea.Calculations;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules.VerticalPile;
using Anthea.Presentation.Services;
using Anthea.Reports;
using X.Core;

namespace Anthea.Presentation.Modules.HorizontalPile;

/// <summary>
/// Sheet of the horizontal pile and of the horizontal micropile (geo_palo_orizzontale, geo_micropalo_orizzontale), F5.7: the tab of the
/// capacity of the historic view HorizontalWorkspace («Dati comuni e capacità · Broms / stratificato») without WPF, on the same document
/// and with the same writes, in the same order. The elastic response (ElasticPileWorkspace, the second tab) is F5.8: here only the part
/// of it that acts on the data and on Modified from the tab of the capacity (preparation at opening, «elastico.stratigrafia» when the
/// number of the stratigraphies changes, a second Modified for every edit) and the choice of the tab, saved in «vista_orizzontale».
/// <list type="bullet">
/// <item>Opening: HorizontalSheetPresentation.PrepareGeneral, PrepareFactors, PrepareSection (c.a.), the forms
/// (<see cref="HorizontalPileFields"/>), the stratigraphies, the preparation of the elastic data, then on the tab of the capacity the
/// automatic calculation after 450 ms.</item>
/// <item>Forms: the raw text into the document at every change, as InputForm; the materials of the c.a. section from the catalogue
/// (<see cref="HorizontalPilePresentation.ApplySectionMaterial"/>), the second inner ring filled when it is activated.</item>
/// <item>Stratigraphies: rows on display copies of the layers; an edit copies the cell into the layer; the soil editor of every shown
/// row (<see cref="HorizontalSoilRowViewModel"/>); layers and stratigraphies added, removed (confirmed) and copied (confirmed when a
/// stratigraphy is replaced).</item>
/// <item>Calculation (<see cref="SheetCalculationMode.Background"/>): HorizontalPileAnalysis (capacity through the
/// <see cref="IModuleService"/> of the context, then the section) on a copy of the data, on a background thread; automatic 450 ms after
/// the last edit, «Calcola» at once; only the calculation of the current data is published. The tab of the elastic response cancels
/// the pending calculation without clearing the result, as today.</item>
/// <item>Read-only (a previous revision): forms, table, soil editors and stratigraphies refuse the changes; the selection of the
/// stratigraphy, of the tab (which writes «vista_orizzontale» with Modified, as today) and the calculation stay available.</item>
/// </list>
/// Texts and rules come from <see cref="HorizontalPilePresentation"/>, <see cref="HorizontalPileDiagrams"/> and
/// <see cref="HorizontalPileFields"/>, shared with the historic view. The equivalence is proved without WPF by
/// tests/ANTHEA.Presentation.Tests (HorizontalPileSheetChecks) against the traces of the historic view frozen in
/// tests/fixtures/f5-palo-orizzontale and against the F3 reference.
/// </summary>
public sealed class HorizontalPileSheetViewModel : SheetViewModelBase
{
    /// <summary>The horizontal pile.</summary>
    public const string PileModule = "geo_palo_orizzontale";

    /// <summary>The horizontal micropile.</summary>
    public const string MicropileModule = "geo_micropalo_orizzontale";

    /// <summary>Interface state that the sheet writes into its data (<see cref="ModulePersistence"/>): the tab of the analyses.</summary>
    public static IReadOnlyList<string> UiStateKeys { get; } = ["vista_orizzontale"];

    /// <summary>Delay of the automatic recalculation after an edit, as the historic view.</summary>
    public static TimeSpan RecalculationDelay { get; } = TimeSpan.FromMilliseconds(450);

    private readonly IHorizontalPileFigureRenderer figures;
    private readonly AnalysisService analyses;
    private bool building = true;
    private int analysis, selectedSurvey = -1, elasticSurveys = -1;
    private FormViewModel generalForm = null!, modelForm = null!, factorsForm = null!, efficiencyForm = null!, momentForm = null!, sectionForm = null!, ejForm = null!;
    private IReadOnlyList<HorizontalPileSurveyViewModel> surveys = [];
    private string status = HorizontalPilePresentation.StatusToVerify, summary = HorizontalPilePresentation.SummaryInitial, momentText = HorizontalPilePresentation.MomentInitial;
    private string? momentToolTip, modelHint;
    private string modelText = HorizontalPilePresentation.ModelInitial, warningText = "";
    private HorizontalPileVerification verification = HorizontalPilePresentation.Verification(null);
    private HorizontalPileSectionDrawing sectionDrawing = HorizontalPileSectionDrawing.Empty;
    private JsonObject? chs;

    /// <param name="moduleId"><see cref="PileModule"/> or <see cref="MicropileModule"/>.</param>
    /// <param name="context">Data of the sheet (owned copy), host, services and the calculation service of the module.</param>
    /// <param name="figures">Draws the figures of the report.</param>
    public HorizontalPileSheetViewModel(string moduleId, SheetContext context, IHorizontalPileFigureRenderer figures)
        : this(moduleId, Check(moduleId, context), new AnalysisService(context.Calculation), figures) { }

    private HorizontalPileSheetViewModel(string moduleId, SheetContext context, AnalysisService analyses, IHorizontalPileFigureRenderer figures)
        : base(moduleId, new SheetContext(context.Data, context.ReadOnly, context.Host, context.Services, analyses, context.Limiter), RecalculationDelay, SheetCalculationMode.Background)
    {
        ArgumentNullException.ThrowIfNull(figures);
        this.figures = figures; this.analyses = analyses;
        Report = new HorizontalPileReport(this);
        Build();
        building = false;
        Preview();
        Verification = HorizontalPilePresentation.Verification(null);
        if (analysis == 0) { Status = HorizontalPilePresentation.StatusPending; _ = ScheduleCalculationAsync(); }
        RealizeRows();
    }

    private static SheetContext Check(string moduleId, SheetContext context)
    {
        if (moduleId is not (PileModule or MicropileModule)) throw new ArgumentException("Modulo dei pali orizzontali atteso: " + moduleId, nameof(moduleId));
        ArgumentNullException.ThrowIfNull(context);
        return context;
    }

    public override IModuleReportSource Report { get; }

    /// <summary>True for the micropile (CHS section), as the type of section of the data says.</summary>
    public bool IsMicro => Data.S("tipo_sezione") == "CHS";

    // ---------------------------------------------------------------- tabs

    /// <summary>
    /// The tab of the analyses: 0 the capacity, 1 the elastic response (F5.8). The choice is saved in «vista_orizzontale» with Modified,
    /// also when read-only (as today); the elastic tab cancels the pending calculation of the capacity without clearing its result, the
    /// tab of the capacity calculates when there is no result.
    /// </summary>
    public int SelectedAnalysis
    {
        get => analysis;
        set
        {
            if (value == analysis || value is < 0 or > 1 || IsDisposed) return;
            analysis = value;
            OnPropertyChanged();
            if (building) return;
            Commit();
            Data["vista_orizzontale"] = value == 1 ? "elastico" : "capacita";
            if (value == 1) CancelCalculation();
            else if (!HasResults) { Status = HorizontalPilePresentation.StatusPending; _ = ScheduleCalculationAsync(); }
            ChangedKeepingResults();
            RealizeRows();
        }
    }

    // ---------------------------------------------------------------- forms

    /// <summary>«Dati generali».</summary>
    public FormViewModel GeneralForm => generalForm;

    /// <summary>«Opzioni avanzate» of «Dati generali»: method, step of the diagrams, tolerance.</summary>
    public FormViewModel ModelForm => modelForm;

    /// <summary>«Coefficienti normativa».</summary>
    public FormViewModel FactorsForm => factorsForm;

    /// <summary>«Efficienza».</summary>
    public FormViewModel EfficiencyForm => efficiencyForm;

    /// <summary>Where the moment comes from (in «Opzioni avanzate · resistenza manuale ed EJ»).</summary>
    public FormViewModel MomentForm => momentForm;

    /// <summary>The c.a. section of the pile or the CHS of the micropile.</summary>
    public FormViewModel SectionForm => sectionForm;

    /// <summary>The assigned EJ.</summary>
    public FormViewModel EjForm => ejForm;

    /// <summary>The seven forms, in the order of the view, with their names.</summary>
    public IReadOnlyList<(string Name, FormViewModel Form)> Forms => [("generali", GeneralForm), ("modello", ModelForm), ("coefficienti", FactorsForm),
        ("efficienza", EfficiencyForm), ("momento", MomentForm), ("sezione", SectionForm), ("ej", EjForm)];

    /// <summary>The tool tips of the fields of the c.a. section.</summary>
    public IReadOnlyDictionary<string, string> SectionToolTips => IsMicro ? new Dictionary<string, string>() : HorizontalPileFields.ConcreteSectionToolTips();

    // ---------------------------------------------------------------- texts and verification

    /// <summary>Status line: waiting, running, completed, missing data or error.</summary>
    public string Status { get => status; private set => SetProperty(ref status, value); }

    /// <summary>The summary under the verification.</summary>
    public string Summary { get => summary; private set => SetProperty(ref summary, value); }

    /// <summary>The moment of the section, manual or not available.</summary>
    public string MomentText { get => momentText; private set => SetProperty(ref momentText, value); }

    /// <summary>The note of the moment (the model of the c.a. section); it stays until another c.a. section replaces it, as today.</summary>
    public string? MomentToolTip { get => momentToolTip; private set => SetProperty(ref momentToolTip, value); }

    /// <summary>The automatic model and its note.</summary>
    public string ModelText { get => modelText; private set => SetProperty(ref modelText, value); }

    public string? ModelHint { get => modelHint; private set => SetProperty(ref modelHint, value); }

    /// <summary>Warnings of the result or the text of the missing data; empty after an edit.</summary>
    public string WarningText { get => warningText; private set => SetProperty(ref warningText, value); }

    /// <summary>«Verifica».</summary>
    public HorizontalPileVerification Verification { get => verification; private set => SetProperty(ref verification, value); }

    /// <summary>The c.a. section drawn (the pile).</summary>
    public HorizontalPileSectionDrawing SectionDrawing { get => sectionDrawing; private set => SetProperty(ref sectionDrawing, value); }

    /// <summary>The CHS drawn (the micropile): its properties, null when the data are incomplete.</summary>
    public JsonObject? ChsProperties { get => chs; private set => SetProperty(ref chs, value); }

    /// <summary>«Diagrammi e dettagli» and «Esporta CSV» are available with a result.</summary>
    public bool CanShowResults => HasResults;

    /// <summary>«Diagrammi e dettagli»: the tables and the line of every survey of the result (none without result).</summary>
    public IReadOnlyList<HorizontalPileResultSurvey> Results => Result is { } result ? HorizontalPilePresentation.Results(result) : [];

    /// <summary>The diagrams of a survey of the result, soil or pile view (the figures of «Diagrammi e dettagli» and of the report).</summary>
    public HorizontalDiagram Diagram(int survey, bool soilView) => HorizontalPileDiagrams.Build(Result ?? throw new InvalidOperationException("Risultato non disponibile."), survey, soilView);

    /// <summary>The references of «Diagrammi e dettagli».</summary>
    public JsonArray References => HorizontalPileEditor.References();

    /// <summary>«Esporta CSV»: the tables of the result into the chosen file; a failure is shown, as today.</summary>
    public void ExportCsv()
    {
        if (Result is not { } result) return;
        string? path = Context.Services.Files.SaveFile(new SaveFileRequest([new FileFilter("Tabelle CSV", "*.csv")], "Palo_orizzontale.csv"));
        if (path is null) return;
        try { Archivio.ScriviAtomico(path, Encoding.UTF8.GetBytes(HorizontalPileEditor.Csv(result))); }
        catch (Exception error) { Context.Services.Messages.Inform("Esportazione non riuscita", error.Message); }
    }

    // ---------------------------------------------------------------- stratigraphies

    /// <summary>The stratigraphies, one tab each.</summary>
    public IReadOnlyList<HorizontalPileSurveyViewModel> Surveys { get => surveys; private set => SetProperty(ref surveys, value); }

    /// <summary>The selected stratigraphy: its profile is drawn and its rows show the soil editors.</summary>
    public int SelectedSurveyIndex
    {
        get => selectedSurvey;
        set
        {
            if (value == selectedSurvey || value < 0 || value >= Surveys.Count) return;
            selectedSurvey = value;
            OnPropertyChanged();
            RealizeRows();
        }
    }

    /// <summary>«+ Stratigrafia»: a new stratigraphy with one layer, selected.</summary>
    public void AddSurvey()
    {
        if (!CanEdit) return;
        Commit();
        Data.Array("stratigrafie").Add(new JsonArray(HorizontalPilePresentation.NewLayer()));
        RebuildSurveys(Data.Array("stratigrafie").Count - 1);
        UserChanged();
        RealizeRows();
    }

    /// <summary>The [−] of a tab: removes the stratigraphy <paramref name="index"/> after the confirmation of the user; the last one stays.</summary>
    public void RemoveSurvey(int index)
    {
        var all = Data.Array("stratigrafie");
        if (!CanEdit || index < 0 || index >= all.Count) return;
        if (all.Count == 1) { Context.Services.Messages.Inform("", "Mantenere almeno una stratigrafia. È possibile eliminare tutti i suoi strati."); return; }
        if (!Context.Services.Confirmations.ConfirmDelete("Stratigrafia", $"Eliminare la stratigrafia {index + 1} e tutti i suoi strati?")) return;
        Commit();
        int selected = SelectedSurveyIndex;
        all.RemoveAt(index);
        RebuildSurveys(index < selected ? selected - 1 : selected);
        UserChanged();
        RealizeRows();
    }

    /// <summary>«− Stratigrafia»: removes the selected stratigraphy.</summary>
    public void RemoveSelectedSurvey()
    {
        if (SelectedSurveyIndex >= 0) RemoveSurvey(SelectedSurveyIndex);
    }

    /// <summary>
    /// Copies the layers of the stratigraphy <paramref name="source"/> into <paramref name="destination"/> (−1: a new stratigraphy),
    /// replacing them after the confirmation of the user; the destination is selected. False when nothing was copied.
    /// </summary>
    public bool CopySurvey(int source, int destination)
    {
        if (!CanEdit) return false;
        var all = Data.Array("stratigrafie");
        if (source < 0 || source >= all.Count || destination < -1 || destination >= all.Count || source == destination) return false;
        if (destination >= 0 && !Context.Services.Confirmations.ConfirmDelete("Copia stratigrafia", $"Sostituire gli strati della stratigrafia {destination + 1}?")) return false;
        Commit();
        var copy = all[source]!.DeepClone();
        if (destination < 0) { all.Add(copy); destination = all.Count - 1; } else all[destination] = copy;
        RebuildSurveys(destination);
        UserChanged();
        RealizeRows();
        return true;
    }

    /// <summary>«Copia in…»: copies the selected stratigraphy into the one chosen in the dialog (or into a new one).</summary>
    public Task CopySurveyIntoAsync() => ChooseSurveyCopyAsync(copyInto: true);

    /// <summary>«Copia da…»: replaces the selected stratigraphy with a copy of the one chosen in the dialog.</summary>
    public Task CopySurveyFromAsync() => ChooseSurveyCopyAsync(copyInto: false);

    /// <summary>«Aggiungi strato» of the stratigraphy <paramref name="survey"/>.</summary>
    public void AddLayer(int survey)
    {
        if (!CanEdit || survey < 0 || survey >= Surveys.Count) return;
        var target = Surveys[survey];
        var layer = HorizontalPilePresentation.NewLayer();
        target.Layers.Add(layer);
        target.Rows.Add(Bind(layer));
        UserChanged();
        RealizeRows();
    }

    /// <summary>The [−] of a layer of the stratigraphy <paramref name="survey"/>.</summary>
    public void RemoveLayer(int survey, JsonRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (!CanEdit || survey < 0 || survey >= Surveys.Count) return;
        var target = Surveys[survey];
        int position = target.Rows.IndexOf(row);
        if (position < 0) return;
        target.Layers.RemoveAt(position);
        target.Rows.RemoveAt(position);
        if (target.Soils.Remove(row, out var soil)) soil.Deactivate();
        UserChanged();
    }

    // ---------------------------------------------------------------- sheet

    /// <summary>The fields write at every change: nothing waits for the commit (the historic Commit confirmed the cells being edited).</summary>
    protected override void OnCommit()
    {
        foreach (var (_, form) in Forms) form.Commit();
    }

    /// <summary>The data were replaced: the sheet is built again on them, as at opening; the results wait for the automatic recalculation.</summary>
    protected override void OnReloaded()
    {
        building = true;
        try
        {
            foreach (var survey in Surveys) foreach (var soil in survey.Soils.Values) soil.Deactivate();
            Build();
            Summary = HorizontalPilePresentation.SummaryInitial; MomentText = HorizontalPilePresentation.MomentInitial; WarningText = "";
        }
        finally { building = false; }
        Preview();
        Verification = HorizontalPilePresentation.Verification(null);
        RealizeRows();
        Status = HorizontalPilePresentation.StatusPending;
    }

    protected override void OnCalculationPublished()
    {
        var published = PublishedCalculation;
        if (CalculationState == CalculationState.Failed || published is null) { Failed(CalculationMessage); return; }
        try
        {
            var computed = published.NativeAs<HorizontalPileAnalysis>();
            ShowMoment(HorizontalPilePresentation.CalculatedMoment(computed, published.Data, IsMicro));
            if (Result is not { } result)
            {
                Summary = HorizontalPilePresentation.SummaryIncomplete(CalculationMessage); WarningText = CalculationMessage;
                Status = HorizontalPilePresentation.StatusIncomplete; Verification = HorizontalPilePresentation.Verification(null);
                OnPropertyChanged(nameof(CanShowResults));
                return;
            }
            ShowMoment(HorizontalPilePresentation.ResultMoment(result, IsMicro));
            Summary = HorizontalPilePresentation.Summary(result);
            Verification = HorizontalPilePresentation.Verification(result);
            WarningText = HorizontalPilePresentation.Warnings(result);
            Status = HorizontalPilePresentation.StatusCompleted;
            OnPropertyChanged(nameof(CanShowResults)); OnPropertyChanged(nameof(Results));
        }
        catch (Exception error) { Failed(error.Message); }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        // The historic view shows «Calcolo di sezione ed equilibri in corso…» when the engine starts; a request that waits keeps its text.
        if (e.PropertyName == nameof(CalculationState) && CalculationState == CalculationState.Running) Status = HorizontalPilePresentation.StatusRunning;
        if (e.PropertyName == nameof(CanEdit) && generalForm is not null) foreach (var (_, form) in Forms) form.IsReadOnly = !CanEdit;
    }

    protected override void OnDisposed()
    {
        base.OnDisposed();
        building = true;
        foreach (var survey in Surveys) foreach (var soil in survey.Soils.Values) soil.Deactivate();
    }

    // ---------------------------------------------------------------- construction

    /// <summary>The constructor of the historic view on the current data: preparations, forms, stratigraphies, elastic data, tab.</summary>
    private void Build()
    {
        HorizontalSheetPresentation.PrepareGeneral(Data);
        var general = Data["generali"]!.AsObject();
        generalForm = Form(general, HorizontalPileFields.General(IsMicro), _ => UserChanged(), HorizontalPileFields.GeneralGroups());
        modelForm = Form(general, HorizontalPileFields.Model(), _ => UserChanged());
        var coefficients = Data["verifica"]!.AsObject();
        HorizontalSheetPresentation.PrepareFactors(coefficients);
        factorsForm = Form(coefficients, HorizontalPileFields.Factors(), _ => UserChanged());
        efficiencyForm = Form(coefficients, HorizontalPileFields.Efficiency(), _ => UserChanged());
        RebuildSurveys(0);
        momentForm = Form(general, HorizontalPileFields.Moment(IsMicro), _ => UserChanged());
        var section = Data["sezione"]!.AsObject();
        if (IsMicro) sectionForm = Form(section, HorizontalPileFields.Chs(), _ => UserChanged(), HorizontalPileFields.ChsGroups());
        else
        {
            HorizontalSheetPresentation.PrepareSection(Data);
            sectionForm = Form(section, HorizontalPileFields.ConcreteSection(section), SectionMaterialChanged, HorizontalPileFields.ConcreteSectionGroups());
            foreach (string key in HorizontalPileFields.ConcreteSectionHidden()) sectionForm.ShowField(key, false);
            ShowInnerRing(false);
        }
        ejForm = new FormViewModel(section, HorizontalPileFields.Ej(), _ => UserChanged(), compact: true) { InheritedWikiModule = ModuleId, IsReadOnly = !CanEdit };
        // The elastic response (F5.8) prepares its data when it is built, also on the tab of the capacity.
        ElasticPileEditor.PrepareShared(Data);
        if (!ElasticPileEditor.NeedsMigration(Data)) ElasticPileEditor.PrepareReinforcement(Data);
        elasticSurveys = Data.Array("stratigrafie").Count;
        if (!ElasticPileEditor.NeedsMigration(Data)) HorizontalSheetPresentation.PrepareElasticView(Data);
        analysis = Data.S("vista_orizzontale") == "elastico" ? 1 : 0;
        OnPropertyChanged(nameof(SelectedAnalysis));
        foreach (string name in new[] { nameof(GeneralForm), nameof(ModelForm), nameof(FactorsForm), nameof(EfficiencyForm), nameof(MomentForm), nameof(SectionForm), nameof(EjForm), nameof(Forms) })
            OnPropertyChanged(name);
    }

    private FormViewModel Form(JsonObject values, FieldSpec[] fields, Action<string> changed, IEnumerable<HorizontalPileFieldGroup>? groups = null)
    {
        var form = new FormViewModel(values, fields, changed, compact: true, symbolColumns: true) { InheritedWikiModule = ModuleId, IsReadOnly = !CanEdit };
        foreach (var group in groups ?? []) form.GroupFields(group.Title, [.. group.Keys], group.Expanded);
        return form;
    }

    /// <summary>SectionMaterialChanged of the historic view: the materials of the catalogue, then the change.</summary>
    private void SectionMaterialChanged(string key)
    {
        var section = Data["sezione"]!.AsObject();
        foreach (string field in HorizontalPilePresentation.ApplySectionMaterial(section, key)) sectionForm.Set(field, section.S(field), display: true);
        if (key == "second_inner_enabled") ShowInnerRing(true);
        UserChanged();
    }

    /// <summary>ShowInnerRing of the historic view: the fields of the second ring shown when it is active, filled when it is activated.</summary>
    private void ShowInnerRing(bool fill)
    {
        var section = Data["sezione"]!.AsObject();
        bool enabled = section.B("second_inner_enabled");
        if (fill) foreach (string key in HorizontalPilePresentation.FillInnerRing(section)) sectionForm.Set(key, section.S(key), display: true);
        foreach (string key in HorizontalPilePresentation.InnerRingKeys) sectionForm.ShowField(key, enabled);
    }

    /// <summary>RebuildSurveys of the historic view: the tabs and the rows again from the data, the given stratigraphy selected.</summary>
    private void RebuildSurveys(int selected)
    {
        foreach (var survey in Surveys) foreach (var soil in survey.Soils.Values) soil.Deactivate();
        var list = new List<HorizontalPileSurveyViewModel>();
        var all = Data.Array("stratigrafie");
        for (int i = 0; i < all.Count; i++)
        {
            var survey = new HorizontalPileSurveyViewModel(i, all[i]!.AsArray());
            foreach (var layer in survey.Layers.OfType<JsonObject>()) survey.Rows.Add(Bind(layer));
            list.Add(survey);
        }
        Surveys = list;
        selectedSurvey = list.Count > 0 ? Math.Clamp(selected, 0, list.Count - 1) : -1;
        OnPropertyChanged(nameof(SelectedSurveyIndex));
    }

    /// <summary>A row of the table on a display copy of <paramref name="layer"/>; an edit copies the cell into the layer.</summary>
    private JsonRowViewModel Bind(JsonObject layer)
    {
        var display = (JsonObject)layer.DeepClone();
        JsonRowViewModel? row = null;
        row = new JsonRowViewModel(display, key => OnLayerEdited(layer, row!, key)) { Context = layer };
        return row;
    }

    private void OnLayerEdited(JsonObject layer, JsonRowViewModel row, string key)
    {
        if (IsDisposed) return;
        if (!CanEdit)
        {
            // The view shows the table read-only: an edit that reaches the row anyway is undone.
            row.Values[key] = layer[key]?.DeepClone();
            row.Refresh();
            return;
        }
        layer[key] = row.Values[key]?.DeepClone();
        UserChanged();
        row.Refresh();
    }

    /// <summary>
    /// The rows of the selected stratigraphy are shown when the tab of the capacity is: each gets its soil editor (built from the layer,
    /// which may receive its lazy defaults), while the editors of the rows no longer shown stop following them and keep what they show,
    /// as the cells of the historic tables (created at the first layout, unloaded with their tab).
    /// </summary>
    private void RealizeRows()
    {
        if (building || IsDisposed) return;
        for (int i = 0; i < Surveys.Count; i++)
        {
            var survey = Surveys[i];
            if (i != SelectedSurveyIndex || analysis != 0) { foreach (var soil in survey.Soils.Values) soil.Deactivate(); continue; }
            foreach (var row in survey.Rows)
            {
                if (!survey.Soils.TryGetValue(row, out var soil))
                    survey.Soils[row] = soil = new HorizontalSoilRowViewModel(row, (JsonObject)row.Context!, UserChanged, () => CanEdit);
                soil.Activate();
            }
        }
    }

    private async Task ChooseSurveyCopyAsync(bool copyInto)
    {
        if (!CanEdit) return;
        int current = SelectedSurveyIndex;
        if (current < 0) return;
        var all = Data.Array("stratigrafie");
        var choices = Enumerable.Range(0, all.Count).Where(i => i != current).Select(i => new SurveyCopyChoice(i, $"Stratigrafia {i + 1}")).ToList();
        if (copyInto) choices.Add(new(-1, "Nuova stratigrafia"));
        if (choices.Count == 0) { Context.Services.Messages.Inform("", "Non ci sono altre stratigrafie da cui copiare."); return; }
        var dialog = new SurveyCopyDialogViewModel(copyInto ? "Copia stratigrafia in…" : "Copia stratigrafia da…",
            copyInto ? $"Copia la stratigrafia {current + 1} in:" : $"Sostituisci la stratigrafia {current + 1} copiando da:", choices);
        var chosen = await Context.Services.Dialogs.ShowDialogAsync(dialog);
        if (chosen is null || IsDisposed) return;
        CopySurvey(copyInto ? current : chosen.Survey, copyInto ? chosen.Survey : current);
    }

    // ---------------------------------------------------------------- edits and preview

    /// <summary>
    /// Changed of the historic view: texts of a change, the shared data of the elastic response (with its Modified), preview,
    /// verification without result, status, then the base (new revision, automatic recalculation, Modified).
    /// </summary>
    private void UserChanged()
    {
        if (building || IsDisposed) return;
        Summary = HorizontalPilePresentation.SummaryChanged; MomentText = HorizontalPilePresentation.MomentChanged; WarningText = "";
        ElasticChanged();
        Preview();
        Verification = HorizontalPilePresentation.Verification(null);
        if (analysis == 0) Status = HorizontalPilePresentation.StatusPending;
        Changed();
        OnPropertyChanged(nameof(CanShowResults)); OnPropertyChanged(nameof(Results));
    }

    /// <summary>InvalidateShared of the elastic response: a new number of stratigraphies clears its choice; it raises Modified.</summary>
    private void ElasticChanged()
    {
        int count = Data.Array("stratigrafie").Count;
        if (elasticSurveys != count)
        {
            if (elasticSurveys >= 0 && !ElasticPileEditor.NeedsMigration(Data)) Data["elastico"]!["stratigrafia"] = -1;
            elasticSurveys = count;
            if (!ElasticPileEditor.NeedsMigration(Data)) HorizontalSheetPresentation.PrepareElasticView(Data);
        }
        ChangedKeepingResults();
    }

    /// <summary>Preview of the historic view: section values, model, field states, coefficients, efficiency, layer columns, section drawn.</summary>
    private void Preview()
    {
        if (IsMicro)
        {
            foreach (var (key, enabled) in HorizontalPilePresentation.ChsFieldStates(Data["sezione"]!.AsObject())) sectionForm.Enable(key, enabled, dim: true);
            var preview = HorizontalPreview.ChsSection(Data);
            foreach (var (key, value) in preview.Fields) sectionForm.Set(key, value, display: true);
            ChsProperties = preview.Geometry;
        }
        else foreach (var (key, value) in HorizontalPreview.ConcreteStrengths(Data["sezione"]!.AsObject())) sectionForm.Set(key, value, display: true);
        var model = HorizontalPreview.ReadModel(Data);
        ModelText = model.Label; ModelHint = model.Hint;
        foreach (var (key, enabled, dim) in HorizontalPilePresentation.FieldStates(Data))
            foreach (var form in new[] { generalForm, momentForm, efficiencyForm }) if (form.Contains(key)) form.Enable(key, enabled, dim);
        foreach (string key in HorizontalPileFields.EjValues()) ejForm.ShowField(key, HorizontalPilePresentation.EjShown(Data["sezione"]!.AsObject()));
        foreach (var (key, value) in model.Factors) factorsForm.Set(key, value, display: true);
        efficiencyForm.Set("__eta", model.Efficiency, display: true);
        foreach (var survey in Surveys)
            for (int i = 0; i < survey.Rows.Count; i++)
            {
                survey.Rows[i].Output("__strato", HorizontalPilePresentation.LayerName(i));
                survey.Rows[i].Output("__kp", HorizontalPreview.PassivePressure(survey.Rows[i].Values));
            }
        if (!IsMicro)
        {
            try
            {
                var section = HorizontalPreview.ConcreteSection(Data);
                SectionDrawing = new(section.Geometry.Outline, section.Geometry.Bars, section.LinkRadius, section.LinkDiameter, true, "Sezione principale · staffa verde");
            }
            catch (ArgumentException) { SectionDrawing = SectionDrawing with { Outline = [], Bars = [], LinkRadius = 0 }; }
        }
    }

    private void ShowMoment(HorizontalPileMoment shown)
    {
        MomentText = shown.Text;
        if (shown.ToolTip is not null) MomentToolTip = shown.ToolTip;
    }

    /// <summary>The catch of the historic publication: the texts of a failed calculation.</summary>
    private void Failed(string message)
    {
        Summary = HorizontalPilePresentation.SummaryFailed; MomentText = HorizontalPilePresentation.MomentUnavailable; Status = message;
        Verification = HorizontalPilePresentation.Verification(null);
        OnPropertyChanged(nameof(CanShowResults));
    }

    /// <summary>
    /// The calculation of the sheet as the historic view runs it (HorizontalPileAnalysis): the capacity through the service of the
    /// context, then the section of the moment when the capacity does not give it. The analysis travels as the typed result of the
    /// calculation, so it never enters the cache of the session (the historic view has none).
    /// </summary>
    private sealed class AnalysisService(IModuleService capacity) : IModuleService
    {
        public string ModuleId => capacity.ModuleId;

        public JsonObject Prepare(JsonObject source) => capacity.Prepare(source);

        public ModuleCalculation Calculate(JsonObject source, CancellationToken cancellation = default)
        {
            var computed = HorizontalPileAnalysis.Calculate(source, capacity, cancellation);
            return new ModuleCalculation(ModuleId, source, computed.Capacity, computed);
        }
    }

    /// <summary>Report of the sheet: ReportOrizzontale with the figures of the diagrams (reports frozen on 8/10).</summary>
    private sealed class HorizontalPileReport(HorizontalPileSheetViewModel sheet) : IModuleReportSource
    {
        public byte[] BuildReport(string title, IReadOnlySet<string> options, bool projectReport = false)
        {
            var result = sheet.Result ?? throw new InvalidOperationException("Premere Calcola prima di esportare.");
            return DocxWriter.Write(ReportOrizzontale.CreateDocument(title, result, !projectReport, sheet.figures.Render(result)));
        }
    }
}
