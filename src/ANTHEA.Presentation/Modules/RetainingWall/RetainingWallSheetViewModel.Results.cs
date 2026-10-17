using System.Text.Json.Nodes;
using Anthea.Application.Presentation;
using Anthea.Application.Walls;
using Anthea.Calculations;
using Anthea.Presentation.Forms;

namespace Anthea.Presentation.Modules.RetainingWall;

/// <summary>
/// The results of the wall sheet: combinations, views, filters, the panels as blocks, the selection of a section, the combination
/// matrix, the drawings as data and the background works that take the place of the calculation. The choices behave as the combo
/// boxes of the historic view: a choice that changes runs its refresh, a list of items that no longer holds the choice clears it.
/// </summary>
public sealed partial class RetainingWallSheetViewModel
{
    private WallTableViewModel? matrix;
    private string matrixStatus = "";
    private IReadOnlyList<string>? combinations;
    private string? selectedCombination;
    private string selectedView = RetainingWallFields.ViewModes[0], selectedMember = RetainingWallFields.Members[0], selectedFilter = RetainingWallFields.CheckFilters[0];
    private IReadOnlyList<WallPanelBlock>? checksPanel, pressurePanel;
    private Action<int>? resultRowSelected;
    private int resultRow = -1;
    private (string Member, double Position, string Combination)? selectedSection;
    private string sectionSelection = RetainingWallPresentation.SectionSelection(null);
    private bool sendEnabled;
    // The drawing of the inputs and that of the results (RetainingWallDrawing «Drawing» and «Diagrams» of the historic view).
    private WallModel.Result? drawingCalculation, diagramCalculation;
    private WallModel.LoadCase? drawingCase, diagramCase;
    private string? drawingAction;
    private int selectedLayer = -1, selectedPressure = -1;
    private bool selectedValley, showLoads = true, showLabels = true, showRebar = true, showPressures = true;
    private string diagramMode = RetainingWallFields.ViewModes[0], diagramMember = RetainingWallFields.Members[0];
    private bool diagramsVisible = true, diagramDiagrams = true, diagramShowRebar = true, diagramShowLoads = true, diagramWide = true, globalView;

    // ---------------------------------------------------------------- combinations and views

    /// <summary>The combinations of the result (null without one) and the one shown.</summary>
    public IReadOnlyList<string>? Combinations => combinations;

    public string? SelectedCombination { get => selectedCombination; set { if (value is not null) SelectCombination(value); } }

    /// <summary>The view of the results (<see cref="RetainingWallFields.ViewModes"/>).</summary>
    public string SelectedView { get => selectedView; set => SelectView(value); }

    /// <summary>The member of the diagrams (<see cref="RetainingWallFields.Members"/>); a choice of the user only while it is shown.</summary>
    public string SelectedMember { get => selectedMember; set { if (diagramDiagrams || !diagramsVisible) SelectMember(value); } }

    /// <summary>The choice of the member is shown (the view of the internal forces).</summary>
    public bool IsMemberShown => diagramDiagrams;

    /// <summary>The filter of «Riepilogo e dettagli» (<see cref="RetainingWallFields.CheckFilters"/>).</summary>
    public string SelectedFilter { get => selectedFilter; set => SelectFilter(value); }

    /// <summary>The view of the global stability: its drawing, its combination and «Intero profilo» are shown instead of the diagrams.</summary>
    public bool IsGlobalView => globalView;

    /// <summary>The panel «Calcolo delle spinte» of the inputs; null without a combination shown.</summary>
    public IReadOnlyList<WallPanelBlock>? PressurePanel { get => pressurePanel; private set => SetProperty(ref pressurePanel, value); }

    /// <summary>The panel of «Riepilogo e dettagli»; null without one.</summary>
    public IReadOnlyList<WallPanelBlock>? ChecksPanel { get => checksPanel; private set => SetProperty(ref checksPanel, value); }

    /// <summary>The row selected in the first table of <see cref="ChecksPanel"/>, −1 without.</summary>
    public int SelectedResultRow => resultRow;

    /// <summary>The section selected for the module c.a. (text) and whether «Apri sezione in c.a.» is usable.</summary>
    public string SectionSelection { get => sectionSelection; private set => SetProperty(ref sectionSelection, value); }
    public bool CanSendSection { get => sendEnabled; private set => SetProperty(ref sendEnabled, value); }

    /// <summary>The switches of the drawing of the inputs.</summary>
    public bool ShowLoads { get => showLoads; set { if (SetProperty(ref showLoads, value)) NotifyDrawings(); } }
    public bool ShowLabels { get => showLabels; set { if (SetProperty(ref showLabels, value)) NotifyDrawings(); } }
    public bool ShowRebar { get => showRebar; set { if (SetProperty(ref showRebar, value)) NotifyDrawings(); } }
    public bool ShowPressures { get => showPressures; set { if (SetProperty(ref showPressures, value)) NotifyDrawings(); } }

    /// <summary>The drawing of the inputs (characteristic loads, pressures and contact of the combination shown).</summary>
    public WallDrawingState Drawing => new(drawingCase?.Name, drawingCalculation is not null && ReferenceEquals(drawingCalculation, calculationResult), drawingAction, selectedLayer,
        selectedValley, selectedPressure, showLoads, showLabels, showRebar, showPressures);

    /// <summary>The case drawn in the inputs, for the drawing of the view.</summary>
    public WallModel.LoadCase? DrawingCase => drawingCase;

    /// <summary>The drawing of the results (the diagrams).</summary>
    public WallDiagramState Diagrams => new(diagramsVisible, diagramMode, diagramDiagrams, diagramMember, diagramCase?.Name, diagramShowRebar, diagramShowLoads, !diagramWide);

    /// <summary>The case drawn in the results.</summary>
    public WallModel.LoadCase? DiagramCase => diagramCase;

    /// <summary>The drawings changed (selection, switches, combination, result): the view redraws them.</summary>
    public event EventHandler? DrawingsChanged;

    private void NotifyDrawings()
    {
        OnPropertyChanged(nameof(Drawing)); OnPropertyChanged(nameof(Diagrams)); OnPropertyChanged(nameof(GlobalDrawing)); OnPropertyChanged(nameof(GlobalPreview));
        DrawingsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Preview of the historic view: the drawing of the inputs without a result.</summary>
    private void Preview() { drawingCalculation = null; drawingCase = null; NotifyDrawings(); }

    /// <summary>The items of the combination shown; a selection no longer among them is cleared (and the combination shown again).</summary>
    private void SetCombinations(IReadOnlyList<string>? items)
    {
        combinations = items; OnPropertyChanged(nameof(Combinations));
        if (selectedCombination is not null && (items is null || !items.Contains(selectedCombination)))
        {
            selectedCombination = null; OnPropertyChanged(nameof(SelectedCombination)); ShowCombination();
        }
    }

    private void SelectCombination(string name)
    {
        if (name == selectedCombination || combinations is null || !combinations.Contains(name)) return;
        selectedCombination = name; OnPropertyChanged(nameof(SelectedCombination)); ShowCombination();
    }

    private void SelectView(string view)
    {
        if (view == selectedView || !RetainingWallFields.ViewModes.Contains(view)) return;
        selectedView = view; OnPropertyChanged(nameof(SelectedView)); RefreshViews();
    }

    private void SelectMember(string member)
    {
        if (member == selectedMember || !RetainingWallFields.Members.Contains(member)) return;
        selectedMember = member; OnPropertyChanged(nameof(SelectedMember)); RefreshViews();
    }

    private void SelectFilter(string filter)
    {
        if (filter == selectedFilter || !RetainingWallFields.CheckFilters.Contains(filter)) return;
        selectedFilter = filter; OnPropertyChanged(nameof(SelectedFilter)); ShowCheckTable();
    }

    // ---------------------------------------------------------------- results

    /// <summary>A result published: the automatic combinations written into the data (ApplyResult), the matrices, the combinations and the combination shown.</summary>
    private void ShowResults()
    {
        var r = calculationResult!; string? name = selectedCombination; independentGlobal = null; independentInput = null; independentError = null;
        string? globalName = selectedGlobalCombination;
        var prepared = WallSheetPresentation.ApplyResult(Data, r);
        if (prepared.GlobalUpdated) BuildGlobalMatrix();
        if (prepared.GlobalError is { } globalError) GlobalMessage = globalError;
        SetGlobalCombinations(r.GlobalStability?.Cases.Select(c => c.Factors.Name).ToArray());
        string? global = RetainingWallPresentation.GlobalCombination(r.GlobalStability, globalName);
        SelectGlobalCombination(global, user: false);
        if (prepared.LocalUpdated || matrix is null) BuildMatrix();
        SetCombinations(r.Cases.Select(c => c.Name).ToArray()); SelectCombination(name is not null && r.Cases.Any(c => c.Name == name) ? name : r.Cases[0].Name);
        ShowCombination();
        UpdateSeismicStatus();
    }

    private void ShowCombination()
    {
        if (calculationResult is not { } r || selectedCombination is not string name) return;
        var c = r.Cases.FirstOrDefault(c => c.Name == name); if (c is null) return;
        drawingCalculation = r; drawingCase = c; diagramCalculation = r; diagramCase = c;
        Summary = RetainingWallPresentation.Summary(Data, r, c);
        PressurePanel = PressureBlocks(c);
        ShowCheckTable(); RefreshViews();
    }

    private IReadOnlyList<WallPanelBlock> PressureBlocks(WallModel.LoadCase c) => [
        WallPanelBlock.Of(RetainingWallPresentation.PressureFormula(Data), WallTextStyle.Small),
        WallPanelBlock.Of(RetainingWallPresentation.PressureHeader(c), WallTextStyle.Small),
        WallPanelBlock.Table(RetainingWallPresentation.PressureColumns, RetainingWallPresentation.PressureRows(c), 175, WallRowAction.Pressure),
        WallPanelBlock.Group(RetainingWallPresentation.PressureStemTitle, [WallPanelBlock.Table(RetainingWallPresentation.StemPressureColumns, RetainingWallPresentation.StemPressureRows(c), 130)]),
        WallPanelBlock.Group(RetainingWallPresentation.PressureValleyTitle, [WallPanelBlock.Table(RetainingWallPresentation.ValleyPressureColumns, RetainingWallPresentation.ValleyPressureRows(c), 110)]),
        WallPanelBlock.Of(RetainingWallPresentation.PressureThrust(Data, c), WallTextStyle.Small)];

    /// <summary>Selects a row of the table of the pressures (−1 none): the layer is highlighted in the drawing of the inputs.</summary>
    public void SelectPressure(int index)
    {
        if (index == selectedPressure) return;
        selectedPressure = index; NotifyDrawings();
    }

    private void RefreshViews()
    {
        bool global = selectedView == "Stabilità globale";
        globalView = global; diagramsVisible = !global; OnPropertyChanged(nameof(IsGlobalView));
        if (global) SelectFilter("Stabilità globale");
        RefreshGlobal();
        diagramMode = selectedView; diagramDiagrams = diagramMode == "Sollecitazioni"; diagramMember = selectedMember;
        diagramShowRebar = diagramMode is "Armature" or "Geometria e carichi"; OnPropertyChanged(nameof(IsMemberShown));
        diagramShowLoads = diagramMode != "Armature"; diagramWide = diagramDiagrams;
        if (diagramMode == "Armature") SelectFilter("Dettagli armature");
        NotifyDrawings();
    }

    private void ShowCheckTable()
    {
        SelectSection(null); resultRowSelected = null; resultRow = -1; OnPropertyChanged(nameof(SelectedResultRow));
        if (selectedFilter == "Stabilità globale") { ChecksPanel = GlobalResultsPanel(); return; }
        if (calculationResult is not { } r || selectedCombination is not string name) return;
        var c = r.Cases.FirstOrDefault(c => c.Name == name); if (c is null) return;
        if (selectedFilter is "Portanza sismica" or "Cedimenti e spostamenti" or "Dettagli armature") { ChecksPanel = AdvancedResults(selectedFilter, r, c); return; }
        if (selectedFilter == "Calcolo delle spinte") { resultRowSelected = SelectPressure; ChecksPanel = PressureBlocks(c); return; }
        if (selectedFilter == "Sollecitazioni numeriche")
        {
            resultRowSelected = index => SelectSection(RetainingWallPresentation.ForceSection(c, index));
            ChecksPanel = [WallPanelBlock.Table(RetainingWallPresentation.SectionColumns, RetainingWallPresentation.SectionRows(Data, c), 285, WallRowAction.ForceSection)];
            return;
        }
        var rows = RetainingWallPresentation.FilterChecks(r, selectedFilter, name);
        resultRowSelected = index => SelectSection(index >= 0 ? RetainingWallPresentation.CheckSection(rows[index], c) : null);
        ChecksPanel = [WallPanelBlock.Of(RetainingWallPresentation.CheckHeader(c)), WallPanelBlock.Table(RetainingWallPresentation.CheckColumns, RetainingWallPresentation.CheckRows(rows), 285, WallRowAction.CheckSection),
            WallPanelBlock.Of(RetainingWallPresentation.CheckFooter, WallTextStyle.SmallMuted)];
    }

    /// <summary>
    /// Selects a row of the first table of «Riepilogo e dettagli» (−1 clears the selection), as the grid does: a check or a section is
    /// the section that can be sent to the module c.a., a layer of the pressures is highlighted, a global combination is shown.
    /// </summary>
    public void SelectResultRow(int row)
    {
        if (checksPanel?.FirstOrDefault(b => b.Kind == WallBlockKind.Table) is not { } table || row < -1 || row >= table.Rows!.Count || row == resultRow) return;
        resultRow = row; OnPropertyChanged(nameof(SelectedResultRow));
        resultRowSelected?.Invoke(row);
    }

    private IReadOnlyList<WallPanelBlock> AdvancedResults(string mode, WallModel.Result r, WallModel.LoadCase c)
    {
        if (mode == "Dettagli armature")
        {
            if (r.Detailing is not { } detail) return [WallPanelBlock.Of(RetainingWallPresentation.DetailingUnavailable)];
            return [WallPanelBlock.Of(detail.Note, WallTextStyle.Small), WallPanelBlock.Of(RetainingWallPresentation.DetailingSteel(detail), WallTextStyle.NormalBold),
                WallPanelBlock.Table(RetainingWallPresentation.DetailingColumns, RetainingWallPresentation.DetailingRows(detail), 220),
                WallPanelBlock.Table(RetainingWallPresentation.CheckColumns, RetainingWallPresentation.CheckRows(detail.Checks), 220)];
        }
        if (mode == "Portanza sismica")
        {
            if (c.State != "SISMA") return [WallPanelBlock.Of(RetainingWallPresentation.SeismicBearingSelect)];
            if (c.SeismicBearing is not { } b) return [WallPanelBlock.Of(c.SeismicBearingError ?? RetainingWallPresentation.SeismicBearingUnavailable)];
            return [WallPanelBlock.Of(RetainingWallPresentation.SeismicBearingMethod, WallTextStyle.Small),
                WallPanelBlock.Table(RetainingWallPresentation.SeismicBearingColumns, [RetainingWallPresentation.SeismicBearingRow(b)])];
        }
        if (r.Serviceability is not { } service) return [WallPanelBlock.Of(RetainingWallPresentation.ServiceUnavailable)];
        var blocks = new List<WallPanelBlock> { WallPanelBlock.Table(RetainingWallPresentation.ServiceColumns, RetainingWallPresentation.ServiceRows(service), 180) };
        if (service.Cases.FirstOrDefault(s => s.Combination == c.Name) is { } current)
        {
            blocks.Add(WallPanelBlock.Group(RetainingWallPresentation.SettlementTitle, [WallPanelBlock.Table(RetainingWallPresentation.SettlementColumns, RetainingWallPresentation.SettlementRows(current))]));
            blocks.Add(WallPanelBlock.Group(RetainingWallPresentation.ShapeTitle, [WallPanelBlock.Table(RetainingWallPresentation.ShapeColumns, RetainingWallPresentation.ShapeRows(current))]));
        }
        foreach (var history in service.Earthquakes)
        {
            blocks.Add(WallPanelBlock.Of(RetainingWallPresentation.EarthquakeText(history)));
            if (history.History is { } values)
                blocks.Add(WallPanelBlock.Group(RetainingWallPresentation.EarthquakeTitle(history), [WallPanelBlock.Table(RetainingWallPresentation.EarthquakeColumns, RetainingWallPresentation.EarthquakeRows(values))]));
        }
        return blocks;
    }

    // ---------------------------------------------------------------- the section for the module c.a.

    private void SelectSection((string Member, double Position, string Combination)? selected)
    {
        selectedSection = selected;
        CanSendSection = selected is not null && calculationResult is not null && Data.S("family") == "cantilever" && CanEdit;
        SectionSelection = RetainingWallPresentation.SectionSelection(selected);
    }

    /// <summary>
    /// «Apri sezione in c.a.»: geometry, materials, effective reinforcement and N/M/V combinations of the section selected, copied into a
    /// new sheet of the module c.a. (the window opens it); the wall keeps its data. Refused while busy, for a gravity wall or when read-only.
    /// </summary>
    public void SendSelectedSection()
    {
        if (selectedSection is not { } s || calculationResult is null || IsBusy || Data.S("family") != "cantilever" || !CanEdit) return;
        var output = WallModel.ExportSection(calculationResult, s.Member, s.Position, s.Combination);
        Context.Host.OpenModuleCopy("str_palo", output, RetainingWallPresentation.SectionName(s));
    }

    // ---------------------------------------------------------------- the combination matrix

    /// <summary>The combination matrix (display copies of its rows); null until the data have combinations.</summary>
    public WallTableViewModel? Matrix => matrix;

    /// <summary>The text under the matrix: its mode, the edit, the confirmation or an error.</summary>
    public string MatrixStatus { get => matrixStatus; private set => SetProperty(ref matrixStatus, value); }

    private void BuildMatrix()
    {
        WallTableViewModel table = null!;
        table = new WallTableViewModel("matrice", RetainingWallFields.Matrix(Data), index =>
        {
            if (index >= 0 && calculationResult is not null) SelectCombination(table.Rows[index].Values.S("name"));
        });
        foreach (var c in Data.Array("combinations").OfType<JsonObject>())
        {
            var flat = RetainingWallPresentation.MatrixRow(Data, c);
            table.Rows.Add(new JsonRowViewModel(flat, key =>
            {
                RetainingWallPresentation.MatrixEdit(Data, c, flat, key);
                MatrixStatus = RetainingWallPresentation.MatrixEdited; UserChanged();
            }));
        }
        matrix = table; OnPropertyChanged(nameof(Matrix));
        MatrixStatus = RetainingWallPresentation.MatrixStatus(Data);
    }

    /// <summary>«Genera / ripristina automatiche».</summary>
    public void GenerateMatrix()
    {
        if (!CanEdit) return;
        Commit();
        try { RetainingWallPresentation.GenerateMatrix(Data); BuildMatrix(); UserChanged(); }
        catch (Exception error) when (error is not OutOfMemoryException) { MatrixStatus = error.Message; }
    }

    /// <summary>«Conferma matrice».</summary>
    public void ConfirmMatrix()
    {
        if (!CanEdit) return;
        Commit();
        try { RetainingWallPresentation.ConfirmMatrix(Data); MatrixStatus = RetainingWallPresentation.MatrixConfirmed; UserChanged(); }
        catch (Exception error) when (error is not OutOfMemoryException) { MatrixStatus = error.Message; }
    }

    /// <summary>«Copia riga»: a copy of the selected combination.</summary>
    public void CopyCombination()
    {
        if (!CanEdit || matrix is not { SelectedIndex: >= 0 } table) return;
        RetainingWallPresentation.CopyCombination(Data, table.SelectedIndex); BuildMatrix(); UserChanged();
    }

    /// <summary>«Elimina riga»: the selected combination.</summary>
    public void RemoveCombination()
    {
        if (!CanEdit || matrix is not { SelectedIndex: >= 0 } table) return;
        RetainingWallPresentation.RemoveCombination(Data, table.SelectedIndex); BuildMatrix(); UserChanged();
    }

    // ---------------------------------------------------------------- the bar schedule

    /// <summary>«Distinta ferri…»: the bar schedule of the current result; refused (ArgumentException) without a result or while busy.</summary>
    public WallModel.BarSchedule CreateBarSchedule()
    {
        Commit();
        if (calculationResult is null || IsBusy) throw new ArgumentException("Attendere il calcolo aggiornato prima di aprire la distinta.");
        return WallModel.CalculateBarSchedule(calculationResult);
    }

    // ---------------------------------------------------------------- background works

    /// <summary>
    /// A work that takes the place of the calculation (the search of the reinforcement, the independent global calculation), as on the
    /// one scheduler of the historic view: the calculation not yet published is cancelled; the work starts when the running engine
    /// returns (busy from then until it ends), only the last work requested is published, an edit or «Ricalcola» cancels it.
    /// </summary>
    private Task RunSecondaryAsync<T>(Func<CancellationToken, T> query, Action started, Action ended, Action<T> publish, Action<Exception> failed)
    {
        CancelCalculation();
        return RunSecondaryCoreAsync(query, started, ended, publish, failed);
    }

    private async Task RunSecondaryCoreAsync<T>(Func<CancellationToken, T> query, Action started, Action ended, Action<T> publish, Action<Exception> failed)
    {
        var completed = await secondary.ScheduleAsync(async token =>
        {
            await engine.Serial.WaitAsync(token);
            try
            {
                secondaryBusy = true; OnPropertyChanged(nameof(IsBusy)); started();
                try { return await Task.Run(() => query(token), token); }
                finally { secondaryBusy = false; OnPropertyChanged(nameof(IsBusy)); ended(); }
            }
            finally { engine.Serial.Release(); }
        });
        if (IsDisposed) return;
        completed.TryPublish(current =>
        {
            try
            {
                if (current.Error is not null) throw current.Error;
                publish(current.Value!);
            }
            catch (Exception error) when (error is not OutOfMemoryException) { failed(error); }
        });
    }
}
