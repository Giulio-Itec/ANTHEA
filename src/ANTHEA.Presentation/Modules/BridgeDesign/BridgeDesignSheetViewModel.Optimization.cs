using System.Text.Json.Nodes;
using Anthea.Application.BridgeDesign;
using Anthea.Application.Modules;
using Anthea.Application.Presentation;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.BridgeDesign;

/// <summary>
/// The page «Ottimizzazione» (BridgeDesignOptimization.cs and BridgeDesignOptimizationPanel.cs of the historic view): the choices of the
/// search (objective, locks, limits), the search on a background thread with its progress and its cancellation, the ranking, the selection
/// of a solution (from the table or from the cloud), the preview, the solution applied. The choices are not saved in the data. A change of
/// the choices, of the data or of the prices cancels the search and clears its results.
/// </summary>
public sealed partial class BridgeDesignSheetViewModel
{
    private readonly Dictionary<string, bool> locks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> limits = new(StringComparer.Ordinal);
    private readonly List<BridgeDesignModel.OptimizationTrial> liveTrials = [];
    private int objective, variable = 2, selectedSolution = -1, cloudRank;
    private string? familyFilter;
    private bool paretoOnly, runEnabled = true, applyEnabled, solutionSection;
    private string searchStatus = "", searchBest = "", searchSelection = "";
    private double progress, progressMaximum = 100;
    private CancellationTokenSource? optimizationCancellation;
    private JsonObject? optimizationSource;
    private BridgeDesignModel.OptimizationResult? optimization;
    private BridgeDesignTable? ranking;
    private IReadOnlyList<BridgeDesignItem>? searchResults, searchChanges;
    private JsonObject? solutionData;
    private BridgeDesignModel.Result? solutionResult;
    private BridgeDesignSearchData cloud = BridgeDesignSearchData.Empty(null), trace = BridgeDesignSearchData.Empty(null);

    /// <summary>«Avvia ottimizzazione» (refused when read-only, while a search runs).</summary>
    public IAsyncRelayCommand RunOptimizationCommand { get; private set; } = null!;
    /// <summary>«Interrompi» (also when read-only).</summary>
    public IRelayCommand StopOptimizationCommand { get; private set; } = null!;
    /// <summary>«Suggerisci campate dalla lunghezza» (refused when read-only).</summary>
    public IRelayCommand SuggestSpansCommand { get; private set; } = null!;
    /// <summary>«Aggiorna elenco» (also when read-only).</summary>
    public IRelayCommand RefreshRankingCommand { get; private set; } = null!;
    /// <summary>«Applica soluzione selezionata» (refused when read-only, without a selection).</summary>
    public IRelayCommand ApplySolutionCommand { get; private set; } = null!;
    /// <summary>«Prospetto» of the preview.</summary>
    public IRelayCommand SolutionElevationCommand { get; private set; } = null!;
    /// <summary>«Sezione» of the preview.</summary>
    public IRelayCommand SolutionSectionCommand { get; private set; } = null!;

    private void CreateOptimizationCommands()
    {
        RunOptimizationCommand = new AsyncRelayCommand(() => RunOptimizationAsync(), () => CanRunOptimization);
        StopOptimizationCommand = new RelayCommand(StopOptimization);
        SuggestSpansCommand = new RelayCommand(SuggestSpans, () => CanEdit);
        RefreshRankingCommand = new RelayCommand(RefreshRanking);
        ApplySolutionCommand = new RelayCommand(() => ApplySolution(selectedSolution), () => CanApplySolution);
        SolutionElevationCommand = new RelayCommand(() => SolutionSection = false);
        SolutionSectionCommand = new RelayCommand(() => SolutionSection = true);
    }

    private void NotifyOptimizationCommands()
    {
        RunOptimizationCommand.NotifyCanExecuteChanged(); ApplySolutionCommand.NotifyCanExecuteChanged(); SuggestSpansCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanRunOptimization)); OnPropertyChanged(nameof(CanApplySolution));
    }

    // The choices of the search at the opening of the sheet, as BuildOptimizationPanel of the historic view.
    private void CreateOptimization()
    {
        foreach (var (key, _, value) in BridgeDesignOptimizationPresentation.Locks) locks[key] = value;
        foreach (var (key, value) in new[] { ("campate-min", "1"), ("campate-max", "12"), ("altezza-min", "0"), ("altezza-max", "0"), ("altezza-da", "100"), ("altezza-a", "115"),
            ("altezza-passo", "15"), ("pali-da", "100"), ("pali-a", "150"), ("pali-passo", "25"), ("prime", "10") }) limits[key] = value;
        objective = 0; variable = 2; familyFilter = null; paretoOnly = false; solutionSection = false; runEnabled = true;
        searchStatus = BridgeDesignOptimizationPresentation.Ready; searchBest = BridgeDesignOptimizationPresentation.BestDefault;
        searchSelection = BridgeDesignOptimizationPresentation.SelectionDefault;
        selectedSolution = -1; optimization = null; ranking = null; searchResults = null; searchChanges = null; applyEnabled = false;
        progress = 0; progressMaximum = 100; liveTrials.Clear(); optimizationSource = null;
        cloud = BridgeDesignSearchData.Empty(null); trace = BridgeDesignSearchData.Empty(null); cloudRank = 0; solutionData = null; solutionResult = null;
    }

    // Reload: the search cancelled and its choices as at the opening.
    private void ResetOptimization()
    {
        CreateOptimization();
        foreach (string name in new[] { nameof(Objective), nameof(Locks), nameof(Limits), nameof(SearchStatus), nameof(SearchBest), nameof(SearchSelection), nameof(Progress),
            nameof(ProgressMaximum), nameof(Optimization), nameof(SearchResults), nameof(SearchChanges), nameof(SolutionData), nameof(SolutionResult), nameof(SolutionSection),
            nameof(Cloud), nameof(Trace), nameof(CloudRank), nameof(FamilyFilter), nameof(ParetoOnly), nameof(Variable) }) OnPropertyChanged(name);
        NotifyOptimizationCommands();
    }

    private void CancelOptimization() => optimizationCancellation?.Cancel();

    /// <summary>The objective of the search (<see cref="BridgeDesignOptimizationPresentation.Objectives"/>); refused when read-only. A change clears the search.</summary>
    public int Objective
    {
        get => objective;
        set
        {
            if (!CanEdit || value == objective || value < 0 || value >= BridgeDesignOptimizationPresentation.Objectives.Count) return;
            objective = value; OnPropertyChanged(); ClearOptimizationChoice();
        }
    }

    /// <summary>The locks of «Mantieni costante».</summary>
    public IReadOnlyDictionary<string, bool> Locks => locks;

    /// <summary>
    /// A lock of «Mantieni costante» (refused when read-only): the section kept keeps the typology, a free typology frees the section; the
    /// grids of a quantity kept are not editable; a change clears the search.
    /// </summary>
    public void SetLock(string key, bool value)
    {
        if (!CanEdit || !locks.TryGetValue(key, out bool current) || current == value) return;
        locks[key] = value; OnPropertyChanged(nameof(Locks));
        if (value && key == "section") SetLock("family", true);
        if (!value && key == "family") SetLock("section", false);
        OnPropertyChanged(nameof(DepthGridEnabled)); OnPropertyChanged(nameof(PileGridEnabled));
        ClearOptimizationChoice();
    }

    /// <summary>The texts of the limits (<c>campate-min</c> … <c>prime</c>, the keys of the trace).</summary>
    public IReadOnlyDictionary<string, string> Limits => limits;

    /// <summary>The grid of the depth can be edited (the depth is not kept).</summary>
    public bool DepthGridEnabled => !locks["depth"];

    /// <summary>The grid of the piles can be edited (the foundation is not kept).</summary>
    public bool PileGridEnabled => !locks["foundation"];

    /// <summary>
    /// The user types a limit (refused when read-only, and for a grid that cannot be edited): a change clears the search, except «Mostra
    /// le prime N», which acts when the user leaves it (<see cref="LeaveLimit"/>).
    /// </summary>
    public void SetLimit(string key, string text)
    {
        if (!CanEdit || !limits.ContainsKey(key) || !IsLimitEnabled(key)) return;
        WriteLimit(key, text);
    }

    /// <summary>The user leaves a limit: «Mostra le prime N» shows the ranking again.</summary>
    public void LeaveLimit(string key) { if (key == "prime") RefreshRanking(); }

    /// <summary>A limit can be edited: the grids of a quantity kept cannot.</summary>
    public bool IsLimitEnabled(string key) => key switch
    {
        "altezza-da" or "altezza-a" or "altezza-passo" => DepthGridEnabled,
        "pali-da" or "pali-a" or "pali-passo" => PileGridEnabled,
        _ => true
    };

    // A limit written (by the user or by «Suggerisci campate»): a change clears the search, as the TextChanged of the historic editors.
    private void WriteLimit(string key, string text)
    {
        if (limits[key] == text) return;
        limits[key] = text; OnPropertyChanged(nameof(Limits));
        if (key != "prime") ClearOptimizationChoice();
    }

    public string SearchStatus { get => searchStatus; private set => SetProperty(ref searchStatus, value); }
    public string SearchBest { get => searchBest; private set => SetProperty(ref searchBest, value); }
    public string SearchSelection { get => searchSelection; private set => SetProperty(ref searchSelection, value); }
    public double Progress { get => progress; private set => SetProperty(ref progress, value); }
    public double ProgressMaximum { get => progressMaximum; private set => SetProperty(ref progressMaximum, value); }

    /// <summary>«Avvia ottimizzazione» can run: not read-only, no search running.</summary>
    public bool CanRunOptimization => CanEdit && runEnabled;

    /// <summary>«Applica soluzione selezionata» can run: not read-only, a solution selected.</summary>
    public bool CanApplySolution => CanEdit && applyEnabled;

    /// <summary>The state of the two buttons of the search apart from the read-only revision: «Avvia» while no search runs, «Applica» with a selection.</summary>
    public (bool Run, bool Apply) SearchButtons => (runEnabled, applyEnabled);

    /// <summary>The result of the last search completed on the current data, null otherwise.</summary>
    public BridgeDesignModel.OptimizationResult? Optimization => optimization;

    /// <summary>The design searched (a copy of the data without the alternative A and the scenery), null after a change of choices or data.</summary>
    public JsonObject? OptimizationSource => optimizationSource;

    /// <summary>The rank selected (0 none).</summary>
    public int SelectedRank => selectedSolution + 1;

    /// <summary>«4 · Alternative ordinate per l'obiettivo scelto»: the ranking with its notes and the reasons of exclusion, null without a search.</summary>
    public IReadOnlyList<BridgeDesignItem>? SearchResults { get => searchResults; private set => SetProperty(ref searchResults, value); }

    /// <summary>The changes of the solution selected, null without a selection.</summary>
    public IReadOnlyList<BridgeDesignItem>? SearchChanges { get => searchChanges; private set => SetProperty(ref searchChanges, value); }

    /// <summary>The data of the preview of the search (the solution selected or the best during the search), null without one.</summary>
    public JsonObject? SolutionData { get => solutionData; private set => SetProperty(ref solutionData, value); }

    /// <summary>The result of the preview of the search, null without one.</summary>
    public BridgeDesignModel.Result? SolutionResult { get => solutionResult; private set => SetProperty(ref solutionResult, value); }

    /// <summary>The preview shows the cross section (view state, also when read-only).</summary>
    public bool SolutionSection { get => solutionSection; set => SetProperty(ref solutionSection, value); }

    /// <summary>What the cloud of the solutions draws.</summary>
    public BridgeDesignSearchData Cloud { get => cloud; private set => SetProperty(ref cloud, value); }

    /// <summary>What the chart of the explored variables draws.</summary>
    public BridgeDesignSearchData Trace { get => trace; private set => SetProperty(ref trace, value); }

    /// <summary>The rank marked in the cloud (0 none).</summary>
    public int CloudRank { get => cloudRank; private set => SetProperty(ref cloudRank, value); }

    /// <summary>The variable of the chart of the explored variables (<see cref="BridgeDesignOptimizationPresentation.Variables"/>); refused when read-only.</summary>
    public int Variable { get => variable; set { if (CanEdit && value >= 0 && value < BridgeDesignOptimizationPresentation.Variables.Count) SetProperty(ref variable, value); } }

    /// <summary>The typology shown by the cloud, null for every typology; refused when read-only.</summary>
    public string? FamilyFilter { get => familyFilter; set { if (CanEdit) SetProperty(ref familyFilter, value); } }

    /// <summary>The cloud shows only the Pareto frontier; refused when read-only.</summary>
    public bool ParetoOnly { get => paretoOnly; set { if (CanEdit) SetProperty(ref paretoOnly, value); } }

    private void SetRunEnabled(bool value) { runEnabled = value; NotifyOptimizationCommands(); }
    private void SetApplyEnabled(bool value) { applyEnabled = value; NotifyOptimizationCommands(); }

    /// <summary>
    /// «Suggerisci campate dalla lunghezza»: with a result, the spans of the limits from the length and the typologies. F5.15, BD10 point 5
    /// (rule T6): refused when read-only, where the historic view wrote the limits and cleared the search.
    /// </summary>
    public void SuggestSpans()
    {
        if (!CanEdit || calculationResult is not { } r) return;
        var (minimum, maximum, message) = BridgeDesignOptimizationPresentation.SuggestSpans(r, locks["spans"], locks["family"]);
        if (locks["spans"]) { WriteLimit("campate-max", maximum!); WriteLimit("campate-min", minimum!); return; }
        if (minimum is null) { SearchStatus = message!; return; }
        WriteLimit("campate-min", minimum); WriteLimit("campate-max", maximum!);
        SearchStatus = message!;
    }

    private void ResetOptimizationDisplay()
    {
        optimization = null; OnPropertyChanged(nameof(Optimization));
        selectedSolution = -1; ranking = null; SearchResults = null; SearchChanges = null;
        SetApplyEnabled(false); Progress = 0; liveTrials.Clear();
        Cloud = BridgeDesignSearchData.Empty(calculationResult); CloudRank = 0; Trace = BridgeDesignSearchData.Empty(calculationResult);
        SolutionData = null; SolutionResult = null;
        SearchBest = BridgeDesignOptimizationPresentation.BestDefault; SearchSelection = BridgeDesignOptimizationPresentation.SelectionDefault;
    }

    private void ClearOptimizationChoice()
    {
        optimizationCancellation?.Cancel(); optimizationSource = null; ResetOptimizationDisplay();
        SearchStatus = BridgeDesignOptimizationPresentation.ConstraintsChanged;
    }

    // The data or the prices changed after a search: its results no longer apply.
    private void InvalidateOptimization()
    {
        if (optimizationSource is not null && !JsonNode.DeepEquals(optimizationSource, BridgeDesignAnalysis.OptimizationSnapshot(Data)))
        { ClearOptimizationChoice(); SearchStatus = BridgeDesignOptimizationPresentation.DataChanged; }
    }

    /// <summary>«Interrompi» (also when read-only).</summary>
    public void StopOptimization() => optimizationCancellation?.Cancel();

    /// <summary>
    /// «Avvia ottimizzazione» (refused when read-only and while a search runs): waits for the calculation of the bridge when there is no
    /// result, reads the choices (<paramref name="requested"/> in their place), then searches on a background thread with its progress; the
    /// results are shown only if the data did not change meanwhile.
    /// </summary>
    public async Task RunOptimizationAsync(BridgeDesignModel.OptimizationOptions? requested = null)
    {
        if (IsDisposed || !CanEdit || optimizationCancellation is not null) return;
        using var operation = new CancellationTokenSource();
        optimizationCancellation = operation;
        var token = operation.Token;
        SetRunEnabled(false);
        try
        {
            if (calculationResult is null)
            {
                SearchStatus = BridgeDesignOptimizationPresentation.WaitingCalculation;
                await WaitForCalculationAsync().WaitAsync(token);
            }
            token.ThrowIfCancellationRequested();
            if (IsDisposed) return;
            if (calculationResult is null) { SearchStatus = BridgeDesignOptimizationPresentation.NeedsData; return; }
            BridgeDesignModel.OptimizationOptions options;
            try
            {
                options = requested ?? BridgeDesignOptimizationPresentation.Options(objective, k => locks[k], limits["campate-min"], limits["campate-max"], limits["altezza-min"],
                    limits["altezza-max"], limits["altezza-da"], limits["altezza-a"], limits["altezza-passo"], limits["pali-da"], limits["pali-a"], limits["pali-passo"]);
            }
            catch (Exception ex) when (ex is FormatException or OverflowException) { SearchStatus = BridgeDesignOptimizationPresentation.InvalidLimits; return; }
            ResetOptimizationDisplay(); var snapshot = BridgeDesignAnalysis.OptimizationSnapshot(Data); optimizationSource = snapshot;
            SearchStatus = BridgeDesignOptimizationPresentation.Running;
            var watch = System.Diagnostics.Stopwatch.StartNew(); long previewTime = -500; string? previewKey = null;
            var progressReport = new System.Progress<BridgeDesignModel.OptimizationProgress>(p =>
            {
                if (IsDisposed || token.IsCancellationRequested || optimization is not null || !ReferenceEquals(snapshot, optimizationSource) || optimizationCancellation is null) return;
                liveTrials.AddRange(p.NewTrials); Trace = new(liveTrials.ToArray(), [], calculationResult);
                Cloud = new(liveTrials.Where(t => t.Admissible).ToArray(), [], calculationResult); CloudRank = 0;
                ProgressMaximum = p.Planned; Progress = p.Evaluated;
                SearchStatus = BridgeDesignOptimizationPresentation.Progress(p);
                if (p.Best is { } best && watch.ElapsedMilliseconds - previewTime >= 500 && best.Data["input"]!.ToJsonString() != previewKey)
                {
                    previewTime = watch.ElapsedMilliseconds; previewKey = best.Data["input"]!.ToJsonString();
                    SolutionData = best.Data; SolutionResult = best.Result;
                    SearchSelection = BridgeDesignOptimizationPresentation.ProvisionalSelection(best);
                    SearchBest = BridgeDesignOptimizationPresentation.ProvisionalBest(best);
                }
            });
            var result = await Task.Run(() => BridgeDesignAnalysis.Optimize(snapshot, options, token, progressReport), token);
            token.ThrowIfCancellationRequested();
            if (IsDisposed || !ReferenceEquals(snapshot, optimizationSource) || !JsonNode.DeepEquals(snapshot, BridgeDesignAnalysis.OptimizationSnapshot(Data))) return;
            optimization = result; OnPropertyChanged(nameof(Optimization));
            ProgressMaximum = Math.Max(1, result.Evaluated); Progress = result.Evaluated;
            Trace = new(result.Trials, result.Solutions, result.Baseline); Cloud = new(result.Trials, result.Solutions, result.Baseline); CloudRank = 0;
            SearchStatus = BridgeDesignOptimizationPresentation.Completed(result);
            if (result.Solutions.Length > 0)
            {
                SearchBest = BridgeDesignOptimizationPresentation.Best(result.Solutions[0].Trial);
                SelectSolution(0);
            }
            else
            {
                SolutionResult = null; SolutionData = null;
                SearchBest = BridgeDesignOptimizationPresentation.NoSolution; SearchSelection = BridgeDesignOptimizationPresentation.NoPreview;
            }
            RefreshRanking();
        }
        catch (OperationCanceledException) { if (IsDisposed) return; ResetOptimizationDisplay(); SearchStatus = BridgeDesignOptimizationPresentation.Interrupted; }
        catch (ArgumentException ex) { if (IsDisposed) return; ResetOptimizationDisplay(); SearchStatus = ex.Message; }
        finally
        {
            if (ReferenceEquals(optimizationCancellation, operation)) optimizationCancellation = null;
            if (!IsDisposed) SetRunEnabled(true);
        }
    }

    /// <summary>«Aggiorna elenco» and leaving «Mostra le prime N» (also when read-only): the ranking with the first N solutions, without a new search.</summary>
    public void RefreshRanking()
    {
        if (optimization is not { } result) return;
        if (!BridgeDesignOptimizationPresentation.TryTop(limits["prime"], out int n)) { SearchStatus = BridgeDesignOptimizationPresentation.TopInvalid; return; }
        var items = new List<BridgeDesignItem> { new BridgeDesignText(BridgeDesignOptimizationPresentation.RankingHeader(result, n), BridgeDesignTextRole.Highlight) };
        if (BridgeDesignOptimizationPresentation.BaselineExclusions(result) is { } exclusions) items.Add(new BridgeDesignText(exclusions, BridgeDesignTextRole.Warning));
        ranking = new BridgeDesignTable(BridgeDesignOptimizationPresentation.RankingColumns, BridgeDesignOptimizationPresentation.RankingRows(result, n),
            index => { if (index >= 0 && index != selectedSolution) SelectSolution(index); });
        ranking.Select(selectedSolution < Math.Min(n, result.Solutions.Length) ? selectedSolution : -1);
        items.Add(ranking);
        items.Add(new BridgeDesignText(BridgeDesignOptimizationPresentation.RankingNote, BridgeDesignTextRole.Note));
        items.Add(new BridgeDesignGroup(BridgeDesignOptimizationPresentation.ExclusionsHeader,
            [new BridgeDesignTable(BridgeDesignOptimizationPresentation.ExclusionColumns, BridgeDesignOptimizationPresentation.ExclusionRows(result))]));
        SearchResults = items;
    }

    /// <summary>A solution selected in the ranking or in the cloud (also when read-only): its preview, calculated now, and its changes.</summary>
    public void SelectSolution(int index)
    {
        if (optimization is not { } result || index < 0 || index >= result.Solutions.Length) return;
        selectedSolution = index; var c = result.Solutions[index]; var r = BridgeDesignModel.Read(ModuleServices.Get(BridgeDesignModel.Module).Calculate(c.Data));
        SolutionData = c.Data; SolutionResult = r; SetApplyEnabled(true);
        CloudRank = c.Rank;
        ranking?.Select(index < ranking.Rows.Length ? index : -1);
        SearchSelection = BridgeDesignOptimizationPresentation.Selection(c, r);
        SearchChanges = [new BridgeDesignText(BridgeDesignOptimizationPresentation.ChangesTitle, BridgeDesignTextRole.Highlight),
            new BridgeDesignTable(BridgeDesignOptimizationPresentation.ChangeColumns, BridgeDesignOptimizationPresentation.ChangeRows(optimizationSource!, result, c, r))];
        OnPropertyChanged(nameof(SelectedRank));
    }

    /// <summary>
    /// «Applica soluzione selezionata» (refused when read-only): with the results of the current data, the current design becomes the
    /// alternative A when there is none and the inputs those of the solution; the page of the project on «Sezioni e quote».
    /// </summary>
    public void ApplySolution(int index)
    {
        if (!CanEdit) return;
        if (optimization is null || optimizationSource is null || !JsonNode.DeepEquals(optimizationSource, BridgeDesignAnalysis.OptimizationSnapshot(Data)))
        { SearchStatus = BridgeDesignOptimizationPresentation.Stale; return; }
        if (index < 0 || index >= optimization.Solutions.Length) throw new ArgumentOutOfRangeException(nameof(index));
        var candidate = (JsonObject)optimization.Solutions[index].Data.DeepClone(); var current = (JsonObject)Data.DeepClone(); current.Remove("alternative_a");
        Mutate(() => BridgeDesignOptimizationPresentation.ApplySolution(Data, candidate, current), true);
        Page = 0; OutputTab = 5;
    }
}
