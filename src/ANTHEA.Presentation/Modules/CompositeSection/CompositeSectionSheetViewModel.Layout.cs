using System.Text.Json.Nodes;
using Anthea.Application.Bridge;
using Anthea.Application.Presentation;
using Anthea.Calculations;

namespace Anthea.Presentation.Modules.CompositeSection;

/// <summary>
/// The pages and the view state of the sheet (BridgeLayout, BridgeContours, BridgeStressScale, BridgeDetailsInputs and BridgeDetachedView
/// of the historic view): the page shown, the heading of the inputs, the side panel, the display and the switches of the drawing, the
/// situation, the tab of the results, the scale of the concrete diagram, the local sketch, the splitters and the detached view; all saved in
/// <c>ui_mista</c> as the historic view saves them.
/// </summary>
public sealed partial class CompositeSectionSheetViewModel
{
    private JsonObject viewSettings = null!;
    private int page = -1, currentPage = -1, displayIndex, resultsTab = -1, selectedSituation = -1, detailSection, selectedPropertyTab = -1;
    private bool selectingPage, detached, layoutBuilt, scaleBuilt, detailBuilt;
    private string inputTitle = "", inputSubtitle = "", resultNotice = "", neutralAxisToolTip = "", scaleText = "", scaleToolTip = BridgeInputPresentation.ScaleHelp;
    private bool analysisPanelsShown = true, stageControlsShown = true, displayShown = true, geometryLabels, rebarLabels, neutralAxis, neutralAxisShown = true;
    private bool contourSection, contourDiagram, stressLimits, scaleAuto, scaleShown = true, scaleError, detailShown = true, detailExpanded = true, detailAtSupport;
    private IReadOnlyList<string> situations = [];
    private CompositeSectionDrawing drawing = CompositeSectionDrawing.Initial;

    /// <summary>The view state of the sheet (<c>ui_mista</c>).</summary>
    public JsonObject ViewSettings => viewSettings;

    private static int Index(JsonObject? values, string key, int fallback = 0) => J.Number(values?[key]) is double value ? (int)value : fallback;

    private int ViewIndex(string key, int fallback = 0) => Index(viewSettings, key, fallback);

    /// <summary>The construction of the layout of the historic view (BuildLayout): the view state, the properties, the switches, the sketch, the curves, the page.</summary>
    private void BuildLayout()
    {
        if (Data["ui_mista"] is not JsonObject) Data["ui_mista"] = new JsonObject();
        viewSettings = Data["ui_mista"]!.AsObject();
        followLatestStage = viewSettings.B("segui_ultima_fase", !viewSettings.ContainsKey("fase"));
        BridgeSheetPresentation.MigrateViewSettings(viewSettings);
        BuildSectionProperties();
        neutralAxis = viewSettings.B("asse_neutro", true);
        drawing = drawing with { NeutralAxis = neutralAxis };
        scaleBuilt = true;
        contourSection = viewSettings.B("contour_sezione"); contourDiagram = viewSettings.B("contour_tensioni"); stressLimits = viewSettings.B("limiti_tensioni");
        ApplyContours();
        BuildDetailSketch();
        BuildResponsePage();
        layoutBuilt = true;
        page = Math.Clamp(ViewIndex("tab"), 0, 2);
        SelectPage();
        NotifyLayout();
    }

    /// <summary>The state of the layout before an opening (Reload): as the controls of a new historic view.</summary>
    private void ResetLayout()
    {
        page = -1; currentPage = -1; displayIndex = 0; resultsTab = -1; selectedSituation = -1; detailSection = 0; selectedPropertyTab = -1;
        layoutBuilt = scaleBuilt = detailBuilt = false;
        inputTitle = inputSubtitle = resultNotice = neutralAxisToolTip = scaleText = ""; scaleToolTip = BridgeInputPresentation.ScaleHelp;
        analysisPanelsShown = stageControlsShown = displayShown = neutralAxisShown = scaleShown = detailShown = detailExpanded = true;
        geometryLabels = rebarLabels = neutralAxis = contourSection = contourDiagram = stressLimits = scaleAuto = scaleError = detailAtSupport = false;
        situations = []; drawing = CompositeSectionDrawing.Initial;
    }

    private void NotifyLayout()
    {
        foreach (string name in new[] { nameof(Page), nameof(InputTitle), nameof(InputSubtitle), nameof(AnalysisPanelsShown), nameof(StageControlsShown),
            nameof(DisplayShown), nameof(DisplayIndex), nameof(ResultsTab), nameof(GeometryLabels), nameof(RebarLabels), nameof(LabelsShown), nameof(NeutralAxis),
            nameof(NeutralAxisShown), nameof(NeutralAxisToolTip), nameof(ContourSection), nameof(ContourDiagram), nameof(StressLimits), nameof(ScaleText),
            nameof(ScaleAuto), nameof(ScaleShown), nameof(ScaleError), nameof(ScaleToolTip), nameof(DetailShown), nameof(DetailExpanded), nameof(DetailSection),
            nameof(DetailAtSupport), nameof(Situations), nameof(SelectedSituation), nameof(ResultNotice), nameof(Drawing), nameof(IsDetached) })
            OnPropertyChanged(name);
    }

    // ---------------------------------------------------------------- pages

    /// <summary>
    /// The page shown: 0 «Pannello di controllo», 1 «Fasi e tensioni», 2 «Curve della sezione». Choosing it commits the sheet, saves the
    /// view of the page left and shows the page (the cards of the phases rebuilt on the page of the phases), with Modified.
    /// </summary>
    public int Page
    {
        get => page;
        set
        {
            if (value == page || value is < 0 or > 2 || IsDisposed) return;
            page = value; OnPropertyChanged();
            if (building) return;
            Commit(); SelectPage(); StateChanged();
        }
    }

    /// <summary>The page of the inputs shown on the left (0 or 1): the page 2 keeps the last one.</summary>
    public int InputPage => currentPage;

    /// <summary>The heading of the inputs («Definizione della sezione», «Fasi, omogeneizzazione e limiti»), empty before the first page of inputs.</summary>
    public string InputTitle { get => inputTitle; private set => SetProperty(ref inputTitle, value); }

    public string InputSubtitle { get => inputSubtitle; private set => SetProperty(ref inputSubtitle, value); }

    /// <summary>The side panel shows the summary of the situation and the table of the results (the page of the phases); otherwise the properties of the section.</summary>
    public bool AnalysisPanelsShown { get => analysisPanelsShown; private set => SetProperty(ref analysisPanelsShown, value); }

    /// <summary>«Risultati cumulati fino alla fase» and its commands are shown (not on the control panel).</summary>
    public bool StageControlsShown { get => stageControlsShown; private set => SetProperty(ref stageControlsShown, value); }

    /// <summary>The choice of the display is shown (not on the control panel, unless the view is detached).</summary>
    public bool DisplayShown { get => displayShown; private set => SetProperty(ref displayShown, value); }

    /// <summary>SelectPage of the historic view: the view of the page left saved, the page shown with its own view.</summary>
    private void SelectPage()
    {
        selectingPage = true;
        if (currentPage is 0 or 1) { viewSettings["vista_" + currentPage] = displayIndex; viewSettings["risultato_" + currentPage] = resultsTab; }
        if (page == 2)
        {
            currentPage = 2; viewSettings["tab"] = 2; EnterResponsePage(); selectingPage = false;
            OnPropertyChanged(nameof(InputPage));
            return;
        }
        int shown = Math.Clamp(page, 0, 1);
        if (shown == 1) RebuildPhases();
        currentPage = shown; viewSettings["tab"] = shown;
        (InputTitle, InputSubtitle) = BridgeInputPresentation.InputHeading(shown);
        StageControlsShown = shown != 0;
        DisplayShown = shown != 0 || detached;
        if (!detached) SetDisplay(shown == 0 ? 2 : Math.Clamp(ViewIndex("vista_1"), 0, 2));
        SetLabels(viewSettings.B("quote_" + shown, shown == 0), viewSettings.B("armature_" + shown, shown == 0));
        AnalysisPanelsShown = shown == 1;
        DetailShown = BridgeInputPresentation.DetailSketchShown(shown, Data);
        SetResultsTab(Math.Clamp(ViewIndex("risultato_" + shown, shown == 0 ? 2 : 0), 0, 4));
        selectingPage = false;
        OnPropertyChanged(nameof(InputPage));
        RefreshDrawing();
    }

    private void SaveView() => BridgeSheetPresentation.SaveView(viewSettings, currentPage, displayIndex, resultsTab, geometryLabels, rebarLabels, selectedSituation, followLatestStage);

    /// <summary>
    /// The ratio of a splitter of the view saved in the sheet («split_ingressi», «split_righe», «split_risultati» or «split_proprieta»,
    /// «split_curve_colonne», «split_curve_righe»), <paramref name="initial"/> when it is missing or outside 0,15–0,85.
    /// </summary>
    public double SplitRatio(string key, double initial)
    {
        double ratio = J.Number(viewSettings[key]) ?? initial;
        return double.IsFinite(ratio) && ratio is >= .15 and <= .85 ? ratio : initial;
    }

    /// <summary>
    /// A splitter of the view moved: the ratio saved in the sheet (the splitter of the summary as «split_proprieta» on the control panel),
    /// with Modified.
    /// </summary>
    public void SetSplit(string key, double ratio)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        if (IsDisposed || !double.IsFinite(ratio)) return;
        viewSettings["split_" + (key == "risultati" && currentPage == 0 ? "proprieta" : key)] = Math.Clamp(ratio, .15, .85);
        StateChanged();
    }

    // ---------------------------------------------------------------- display, situation, results

    /// <summary>The display of the drawing (<see cref="CompositeSectionFields.Displays"/>), also when read-only.</summary>
    public int DisplayIndex
    {
        get => displayIndex;
        set
        {
            if (value == displayIndex || value is < 0 or > 2 || IsDisposed) return;
            displayIndex = value; OnPropertyChanged();
            if (!layoutBuilt) return;
            RefreshDrawing(); ViewChanged();
        }
    }

    private void SetDisplay(int value)
    {
        if (value == displayIndex) return;
        displayIndex = value; OnPropertyChanged(nameof(DisplayIndex));
    }

    /// <summary>The tab of the results (0 «Sollecitazioni» … 4 «Verifiche»), also when read-only; −1 before the first page of inputs.</summary>
    public int ResultsTab
    {
        get => resultsTab;
        set
        {
            if (value == resultsTab || value is < 0 or > 4 || IsDisposed) return;
            resultsTab = value; OnPropertyChanged();
            if (layoutBuilt) ViewChanged();
        }
    }

    private void SetResultsTab(int value)
    {
        if (value == resultsTab) return;
        resultsTab = value; OnPropertyChanged(nameof(ResultsTab));
    }

    /// <summary>«Dettagli sezione efficace ↓» (also when read-only): the tab «Sezione efficace».</summary>
    public void ShowEffectiveDetails() => ResultsTab = 2;

    /// <summary>The situations of the last result («01 · Dopo G1 …»).</summary>
    public IReadOnlyList<string> Situations { get => situations; private set => SetProperty(ref situations, value); }

    /// <summary>
    /// The situation of the results (also when read-only, also from the detached view): the results and the drawing of that situation, the
    /// phase kept for the next calculation, with Modified. During a calculation the phase is kept at once, the results, the drawing and the
    /// view at its end (F5.15, SM5 point 3: the historic view changed only the choice, then showed the previous situation again). No situation
    /// (−1) is never chosen from outside: a choice of the view that loses its item when the situations are replaced writes −1, and the sheet
    /// then chooses the situation of the publication, as the historic view, whose handler is mute while it publishes.
    /// </summary>
    public int SelectedSituation
    {
        get => selectedSituation;
        set
        {
            if (value == selectedSituation || value < 0 || value >= situations.Count || IsDisposed) return;
            selectedSituation = value; OnPropertyChanged();
            if (value < displayedPhases.Length) selectedPhase = displayedPhases[value];
            followLatestStage = value == displayedPhases.Length - 1;
            if (IsBusy) { situationPending = true; return; }
            ShowResults(); ViewChanged();
        }
    }

    // The situation chosen during a calculation, shown when the engine returns (before the publication, which keeps its phase).
    private bool situationPending;
    private void ShowPendingSituation()
    {
        if (!situationPending || IsDisposed) return;
        situationPending = false;
        ShowResults(); ViewChanged();
    }

    /// <summary>«Mostra tutte le fasi → ultima situazione» (refused when read-only): the last situation, followed by the next calculations.</summary>
    public void ShowLatestSituation()
    {
        if (!CanEdit) return;
        followLatestStage = true;
        SelectedSituation = situations.Count - 1;
        ViewChanged();
    }

    // ---------------------------------------------------------------- switches of the drawing

    /// <summary>The labels of the drawing are shown with the geometry only.</summary>
    public bool LabelsShown => drawing.Mode == 2;

    /// <summary>«Quote sezione» (refused when read-only).</summary>
    public bool GeometryLabels
    {
        get => geometryLabels;
        set { if (!CanEdit || value == geometryLabels) return; geometryLabels = value; OnPropertyChanged(); LabelOptionsChanged(); }
    }

    /// <summary>«Info armature» (refused when read-only).</summary>
    public bool RebarLabels
    {
        get => rebarLabels;
        set { if (!CanEdit || value == rebarLabels) return; rebarLabels = value; OnPropertyChanged(); LabelOptionsChanged(); }
    }

    private void SetLabels(bool geometry, bool rebars)
    {
        if (geometry == geometryLabels && rebars == rebarLabels) return;
        geometryLabels = geometry; rebarLabels = rebars;
        OnPropertyChanged(nameof(GeometryLabels)); OnPropertyChanged(nameof(RebarLabels));
        SetDrawing(drawing with { GeometryLabels = geometry, RebarLabels = rebars });
    }

    private void LabelOptionsChanged()
    {
        SetDrawing(drawing with { GeometryLabels = geometryLabels, RebarLabels = rebarLabels });
        ViewChanged();
    }

    /// <summary>«Asse neutro» (also when read-only): saved in the sheet with Modified.</summary>
    public bool NeutralAxis
    {
        get => neutralAxis;
        set
        {
            if (value == neutralAxis || IsDisposed) return;
            neutralAxis = value; OnPropertyChanged();
            viewSettings["asse_neutro"] = value;
            SetDrawing(drawing with { NeutralAxis = value });
            StateChanged();
        }
    }

    /// <summary>The switch of the neutral axis is shown (not with the geometry of the control panel).</summary>
    public bool NeutralAxisShown { get => neutralAxisShown; private set => SetProperty(ref neutralAxisShown, value); }

    /// <summary>What the neutral axis of the situation is.</summary>
    public string NeutralAxisToolTip { get => neutralAxisToolTip; private set => SetProperty(ref neutralAxisToolTip, value); }

    /// <summary>«Verifiche ▾ · Colora la sezione · σ/limite» (refused when read-only).</summary>
    public bool ContourSection { get => contourSection; set => SetContour(ref contourSection, value, "contour_sezione"); }

    /// <summary>«Verifiche ▾ · Colora le tensioni disegnate · σ/limite» (refused when read-only).</summary>
    public bool ContourDiagram { get => contourDiagram; set => SetContour(ref contourDiagram, value, "contour_tensioni"); }

    /// <summary>«Verifiche ▾ · Mostra le rette dei limiti tensionali» (refused when read-only).</summary>
    public bool StressLimits { get => stressLimits; set => SetContour(ref stressLimits, value, "limiti_tensioni"); }

    private void SetContour(ref bool field, bool value, string key)
    {
        if (!CanEdit || field == value) return;
        field = value;
        OnPropertyChanged(key switch { "contour_sezione" => nameof(ContourSection), "contour_tensioni" => nameof(ContourDiagram), _ => nameof(StressLimits) });
        viewSettings[key] = value; ApplyContours(); ViewChanged();
    }

    private void ApplyContours() => SetDrawing(drawing with { ContourSection = contourSection, ContourDiagram = contourDiagram, StressLimits = stressLimits });

    // ---------------------------------------------------------------- scale of the concrete diagram

    /// <summary>The factor of the concrete diagram as shown («CLS ×»), or the text typed when it was refused.</summary>
    public string ScaleText { get => scaleText; private set => SetProperty(ref scaleText, value); }

    /// <summary>«Auto n» (refused when read-only): the factor follows the last composite phase.</summary>
    public bool ScaleAuto
    {
        get => scaleAuto;
        set
        {
            if (!CanEdit || value == scaleAuto || IsDisposed) return;
            scaleAuto = value; OnPropertyChanged();
            viewSettings["amplificazione_cls_auto"] = value;
            if (!value) viewSettings["amplificazione_cls"] = drawing.ConcreteAmplification;
            RefreshStressScale(); StateChanged();
        }
    }

    /// <summary>The scale of the concrete is shown: on the page of the phases or in the detached view, with the stresses.</summary>
    public bool ScaleShown { get => scaleShown; private set => SetProperty(ref scaleShown, value); }

    /// <summary>The text typed was refused (outside 0,01–1000): the diagram keeps the last valid scale.</summary>
    public bool ScaleError { get => scaleError; private set => SetProperty(ref scaleError, value); }

    /// <summary>The help of the scale with the reference of the factor.</summary>
    public string ScaleToolTip { get => scaleToolTip; private set => SetProperty(ref scaleToolTip, value); }

    /// <summary>
    /// A factor typed in «CLS ×» and confirmed (refused when read-only): outside 0,01–1000 the text stays and is marked
    /// (<see cref="ScaleError"/>); the factor shown again does nothing; otherwise the manual scale is saved, with Modified.
    /// </summary>
    public void CommitScale(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!CanEdit || IsDisposed) return;
        ScaleText = text;
        if (!BridgeInputPresentation.TryParseScale(text, out double value)) { ScaleError = true; return; }
        // A focus change without an edit must not turn a rounded automatic value into an override.
        if (text == BridgeResultPresentation.F(drawing.ConcreteAmplification)) { ScaleError = false; return; }
        viewSettings["amplificazione_cls_auto"] = false; viewSettings["amplificazione_cls"] = value;
        RefreshStressScale(); StateChanged();
    }

    /// <summary>Esc in «CLS ×»: the factor shown again, without error.</summary>
    public void RestoreScale()
    {
        ScaleText = BridgeResultPresentation.F(drawing.ConcreteAmplification); ScaleError = false;
    }

    private void RefreshStressScale()
    {
        if (!scaleBuilt) return;
        var scale = displayed?.StressScale(viewSettings) ?? BridgeSheetPresentation.StressScale(viewSettings);
        SetDrawing(drawing with { ConcreteAmplification = scale.Factor });
        ScaleShown = (currentPage == 1 || detached) && drawing.Mode != 2;
        if (scaleAuto != viewSettings.B("amplificazione_cls_auto", true)) { scaleAuto = !scaleAuto; OnPropertyChanged(nameof(ScaleAuto)); }
        ScaleText = BridgeResultPresentation.F(scale.Factor); ScaleError = false;
        ScaleToolTip = BridgeInputPresentation.ScaleHelp + "\nRiferimento attuale: " + scale.Source + " · ×" + BridgeResultPresentation.F(scale.Factor);
    }

    // ---------------------------------------------------------------- local sketch

    /// <summary>«Prospetto locale · pannelli e appoggio» is shown: on the control panel, with stiffeners or support.</summary>
    public bool DetailShown { get => detailShown; private set => SetProperty(ref detailShown, value); }

    /// <summary>The local sketch is open (also when read-only): saved in the sheet, without Modified, as the historic expander.</summary>
    public bool DetailExpanded
    {
        get => detailExpanded;
        set
        {
            if (value == detailExpanded || IsDisposed) return;
            detailExpanded = value; OnPropertyChanged();
            viewSettings["prospetto"] = value;
        }
    }

    /// <summary>«Sezione in vista» of the local sketch (<see cref="CompositeSectionFields.DetailSections"/>), refused when read-only.</summary>
    public int DetailSection
    {
        get => detailSection;
        set
        {
            if (!CanEdit || value == detailSection || value is < 0 or > 1 || IsDisposed) return;
            ChooseDetailSection(value);
        }
    }

    private void ChooseDetailSection(int value)
    {
        detailSection = value; OnPropertyChanged(nameof(DetailSection));
        SetDrawing(drawing with { DetailAtSupport = value == 1 });
        RefreshDetailSketch(); RefreshDrawing(); ViewChanged();
    }

    /// <summary>The local sketch shows the support (the sketch of the control panel, as the drawing).</summary>
    public bool DetailAtSupport { get => detailAtSupport; private set => SetProperty(ref detailAtSupport, value); }

    private void BuildDetailSketch()
    {
        detailBuilt = true;
        int saved = ViewIndex("dettaglio_sezione");
        int index = saved is 0 or 1 ? saved : -1;
        if (index != detailSection) ChooseDetailSection(index);
        bool expanded = viewSettings.B("prospetto", true);
        if (expanded != detailExpanded) { detailExpanded = expanded; viewSettings["prospetto"] = expanded; }
        RefreshDetailSketch();
    }

    private void RefreshDetailSketch()
    {
        DetailAtSupport = detailSection == 1;
        if (detailBuilt) DetailShown = BridgeInputPresentation.DetailSketchShown(currentPage, Data);
        if (viewSettings is not null) viewSettings["dettaglio_sezione"] = detailSection;
    }

    // ---------------------------------------------------------------- drawing

    /// <summary>What the drawing of the section shows.</summary>
    public CompositeSectionDrawing Drawing => drawing;

    private void SetDrawing(CompositeSectionDrawing value)
    {
        if (value == drawing) return;
        bool labels = value.Mode != drawing.Mode;
        drawing = value; OnPropertyChanged(nameof(Drawing));
        if (labels) OnPropertyChanged(nameof(LabelsShown));
    }

    /// <summary>
    /// «PNG» of the toolbar of the drawing (also when read-only, as the historic button): the file chosen by the user («ANTHEA_» and the title of the
    /// drawing) receives the image that <paramref name="png"/> draws; nothing when the user cancels; an error of the writing is shown as the historic
    /// message «Esportazione immagine».
    /// </summary>
    public void SaveDrawingImage(Func<byte[]> png)
    {
        ArgumentNullException.ThrowIfNull(png);
        string? path = Context.Services.Files.SaveFile(new Anthea.Presentation.Services.SaveFileRequest([new Anthea.Presentation.Services.FileFilter("Immagine PNG", "*.png")],
            "ANTHEA_" + DrawingTitle.Replace(' ', '_') + ".png"));
        if (path is null) return;
        try { X.Core.Archivio.ScriviAtomico(path, png()); }
        catch (Exception error) when (error is not OutOfMemoryException) { Context.Services.Messages.Inform("Esportazione immagine", error.Message); }
    }

    /// <summary>The title of the frame of the drawing (and of its expanded window).</summary>
    public const string DrawingTitle = "Sezione composta · geometria e tensioni";

    /// <summary>The notice over the drawing: the error of the geometry, or the results «DA AGGIORNARE»; empty without notice.</summary>
    public string ResultNotice { get => resultNotice; private set => SetProperty(ref resultNotice, value); }

    /// <summary>RefreshDrawing of the historic view: the preview or the result of the situation, the display, the points of N, the scale, the notice.</summary>
    private void RefreshDrawing()
    {
        bool geometryPage = currentPage == 0 && !detached;
        var result = displayed;
        var stage = result is not null && selectedSituation >= 0 && selectedSituation < result.Stages.Count ? result.Stages[selectedSituation] : null;
        int mode = geometryPage ? 2 : displayIndex;
        SetDrawing(drawing with
        {
            Geometry = geometryPage || stage is null ? previewGeometry : result!.Geometry,
            Input = geometryPage || stage is null ? previewInput : result!.Input,
            Stage = geometryPage ? null : stage,
            Mode = mode,
            Stale = !geometryPage && ResultsAreStale,
            LoadPoints = !geometryPage && stage is not null ? BridgeResultPresentation.LoadPoints(stage) : []
        });
        NeutralAxisShown = !geometryPage;
        NeutralAxisToolTip = stage is null ? BridgeResultPresentation.NeutralAxisToolTip : stage.NeutralAxis.Explanation;
        RefreshStressScale();
        UpdateResultNotice();
    }

    private void UpdateResultNotice()
    {
        bool geometryPage = currentPage == 0 && !detached;
        ResultNotice = BridgeInputPresentation.ResultNotice(geometryPage, geometryError, ResultsAreStale);
        SetDrawing(drawing with { Stale = !geometryPage && ResultsAreStale });
    }

    // ---------------------------------------------------------------- detached view

    /// <summary>The drawing is shown in a window of its own (the host shows it there, with the situation, the display and its switches).</summary>
    public bool IsDetached => detached;

    /// <summary>
    /// «Stacca vista» (also when read-only): the page of the phases, the total stresses instead of the geometry; the host then shows the
    /// drawing in a window of its own (BridgeDetachedView of the historic view), where it stays until <see cref="Reattach"/>.
    /// </summary>
    public void Detach()
    {
        if (detached || IsDisposed) return;
        Page = 1;
        if (displayIndex == 2) DisplayIndex = 0;
        detached = true; OnPropertyChanged(nameof(IsDetached));
        RefreshDrawing();
    }

    /// <summary>«Riaggancia» (the window of the drawing closed by the host): the drawing back in the sheet.</summary>
    public void Reattach()
    {
        if (!detached) return;
        detached = false; OnPropertyChanged(nameof(IsDetached));
        if (!IsDisposed) RefreshDrawing();
    }
}
