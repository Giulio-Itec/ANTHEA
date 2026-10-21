using Anthea.Application.BridgeDesign;
using Anthea.Application.Presentation;
using Anthea.Application.Modules;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;

namespace X.Desktop;

internal sealed partial class BridgeDesignWorkspace
{
    private readonly ContentControl optimizationResults = new(), optimizationChanges = new();
    private readonly TextBlock optimizationStatus = Ui.Text(BridgeDesignOptimizationPresentation.Ready, 12, color: Ui.Muted);
    private readonly TextBlock optimizationSelection = Ui.Text(BridgeDesignOptimizationPresentation.SelectionDefault, 17, true);
    private readonly TextBlock optimizationBest = Ui.Text(BridgeDesignOptimizationPresentation.BestDefault, 13, true);
    private readonly Dictionary<string, CheckBox> optimizationLocks = new();
    private readonly TextBox optimizationMin = SearchEdit("1"), optimizationMax = SearchEdit("12"), optimizationDepth = SearchEdit("0"), optimizationMinDepth = SearchEdit("0");
    private readonly TextBox depthFrom = SearchEdit("100"), depthTo = SearchEdit("115"), depthStep = SearchEdit("15");
    private readonly TextBox pileFrom = SearchEdit("100"), pileTo = SearchEdit("150"), pileStep = SearchEdit("25");
    internal readonly TextBox OptimizationTop = SearchEdit("10");
    internal readonly BridgeDesignDrawing OptimizationPreview = new() { Height = 265 };
    internal readonly BridgeOptimizationPlot OptimizationCloud = new() { Height = 310, Scatter = true };
    internal readonly BridgeOptimizationPlot OptimizationTrace = new() { Height = 310 };
    internal ScrollViewer OptimizationScroll { get; private set; } = new();
    private readonly ProgressBar optimizationProgress = new() { Height = 5, Margin = new Thickness(0, 8, 0, 8) };
    private readonly List<BridgeDesignModel.OptimizationTrial> liveTrials = new();
    private ComboBox optimizationObjective = new();
    private Button optimizationRun = new(), optimizationApply = new();
    private CancellationTokenSource? optimizationCancellation;
    private JsonObject? optimizationSource;
    private DataGrid? optimizationRanking;
    private int selectedSolution = -1;
    internal BridgeDesignModel.OptimizationResult? Optimization { get; private set; }
    private static TextBox SearchEdit(string value) => new() { Text = value, Width = 58, Margin = new Thickness(3), VerticalContentAlignment = VerticalAlignment.Center };

    private void UpdateRangeAvailability()
    {
        foreach (var edit in new[] { depthFrom, depthTo, depthStep }) edit.IsEnabled = optimizationLocks["depth"].IsChecked != true;
        foreach (var edit in new[] { pileFrom, pileTo, pileStep }) edit.IsEnabled = optimizationLocks["foundation"].IsChecked != true;
    }
    private void SuggestOptimizationSpans()
    {
        if (Calculation is not { } r) return;
        var (minimum, maximum, message) = BridgeDesignOptimizationPresentation.SuggestSpans(r, optimizationLocks["spans"].IsChecked == true, optimizationLocks["family"].IsChecked == true);
        if (optimizationLocks["spans"].IsChecked == true) { optimizationMin.Text = optimizationMax.Text = maximum!; return; }
        if (minimum is null) { optimizationStatus.Text = message!; return; }
        optimizationMin.Text = minimum; optimizationMax.Text = maximum!;
        optimizationStatus.Text = message!;
    }
    private static JsonObject OptimizationSnapshot(JsonObject source)
    => BridgeDesignAnalysis.OptimizationSnapshot(source);
    private void ResetOptimizationDisplay()
    {
        Optimization = null; selectedSolution = -1; optimizationRanking = null; optimizationResults.Content = null; optimizationChanges.Content = null;
        optimizationApply.IsEnabled = false; optimizationProgress.Value = 0; liveTrials.Clear();
        OptimizationCloud.SetData([], [], Calculation); OptimizationTrace.SetData([], [], Calculation);
        OptimizationPreview.Data = null; OptimizationPreview.Result = null; OptimizationPreview.InvalidateVisual();
        optimizationBest.Text = BridgeDesignOptimizationPresentation.BestDefault; optimizationSelection.Text = BridgeDesignOptimizationPresentation.SelectionDefault;
    }
    private void ClearOptimizationChoice()
    {
        optimizationCancellation?.Cancel(); optimizationSource = null; ResetOptimizationDisplay();
        optimizationStatus.Text = BridgeDesignOptimizationPresentation.ConstraintsChanged;
    }
    private void InvalidateOptimization()
    {
        if (optimizationSource is not null && !JsonNode.DeepEquals(optimizationSource, OptimizationSnapshot(Data)))
        { ClearOptimizationChoice(); optimizationStatus.Text = BridgeDesignOptimizationPresentation.DataChanged; }
    }
    internal async Task RunOptimization(BridgeDesignModel.OptimizationOptions? requested = null)
    {
        if (disposed || optimizationCancellation is not null) return;
        using var operation = new CancellationTokenSource();
        optimizationCancellation = operation;
        var token = operation.Token;
        optimizationRun.IsEnabled = false;
        try
        {
            if (Calculation is null)
            {
                optimizationStatus.Text = BridgeDesignOptimizationPresentation.WaitingCalculation;
                await WaitForCalculationAsync().WaitAsync(token);
            }
            token.ThrowIfCancellationRequested();
            if (disposed) return;
            if (Calculation is null) { optimizationStatus.Text = BridgeDesignOptimizationPresentation.NeedsData; return; }
            BridgeDesignModel.OptimizationOptions options;
            try
            {
                options = requested ?? BridgeDesignOptimizationPresentation.Options(optimizationObjective.SelectedIndex, k => optimizationLocks[k].IsChecked == true,
                    optimizationMin.Text, optimizationMax.Text, optimizationMinDepth.Text, optimizationDepth.Text, depthFrom.Text, depthTo.Text, depthStep.Text,
                    pileFrom.Text, pileTo.Text, pileStep.Text);
            }
            catch (Exception ex) when (ex is FormatException or OverflowException) { optimizationStatus.Text = BridgeDesignOptimizationPresentation.InvalidLimits; return; }
            ResetOptimizationDisplay(); var snapshot = OptimizationSnapshot(Data); optimizationSource = snapshot;
            optimizationStatus.Text = BridgeDesignOptimizationPresentation.Running;
            var watch = System.Diagnostics.Stopwatch.StartNew(); long previewTime = -500; string? previewKey = null;
            var progress = new Progress<BridgeDesignModel.OptimizationProgress>(p => {
                if (disposed || token.IsCancellationRequested || Optimization is not null || !ReferenceEquals(snapshot, optimizationSource) || optimizationCancellation is null) return;
                liveTrials.AddRange(p.NewTrials); OptimizationTrace.SetData(liveTrials.ToArray(), [], Calculation);
                OptimizationCloud.SetData(liveTrials.Where(t => t.Admissible).ToArray(), [], Calculation);
                optimizationProgress.Maximum = p.Planned; optimizationProgress.Value = p.Evaluated;
                optimizationStatus.Text = BridgeDesignOptimizationPresentation.Progress(p);
                if (p.Best is { } best && watch.ElapsedMilliseconds - previewTime >= 500 && best.Data["input"]!.ToJsonString() != previewKey)
                {
                    previewTime = watch.ElapsedMilliseconds; previewKey = best.Data["input"]!.ToJsonString();
                    OptimizationPreview.Data = best.Data; OptimizationPreview.Result = best.Result; OptimizationPreview.InvalidateVisual();
                    optimizationSelection.Text = BridgeDesignOptimizationPresentation.ProvisionalSelection(best);
                    optimizationBest.Text = BridgeDesignOptimizationPresentation.ProvisionalBest(best);
                }
            });
            var result = await Task.Run(() => BridgeDesignAnalysis.Optimize(snapshot, options, token, progress), token);
            token.ThrowIfCancellationRequested();
            if (disposed || !ReferenceEquals(snapshot, optimizationSource) || !JsonNode.DeepEquals(snapshot, OptimizationSnapshot(Data))) return;
            Optimization = result; optimizationProgress.Maximum = Math.Max(1, result.Evaluated); optimizationProgress.Value = result.Evaluated;
            OptimizationTrace.SetData(result.Trials, result.Solutions, result.Baseline); OptimizationCloud.SetData(result.Trials, result.Solutions, result.Baseline);
            optimizationStatus.Text = BridgeDesignOptimizationPresentation.Completed(result);
            if (result.Solutions.Length > 0)
            {
                var best = result.Solutions[0].Trial;
                optimizationBest.Text = BridgeDesignOptimizationPresentation.Best(best);
                SelectOptimization(0);
            }
            else { OptimizationPreview.Result = null; OptimizationPreview.Data = null; OptimizationPreview.InvalidateVisual(); optimizationBest.Text = BridgeDesignOptimizationPresentation.NoSolution; optimizationSelection.Text = BridgeDesignOptimizationPresentation.NoPreview; }
            RefreshOptimizationRanking();
        }
        catch (OperationCanceledException) { if(disposed)return; ResetOptimizationDisplay(); optimizationStatus.Text = BridgeDesignOptimizationPresentation.Interrupted; }
        catch (ArgumentException ex) { if(disposed)return; ResetOptimizationDisplay(); optimizationStatus.Text = ex.Message; }
        finally
        {
            if (ReferenceEquals(optimizationCancellation, operation)) optimizationCancellation = null;
            if (!disposed) optimizationRun.IsEnabled = true;
        }
    }
    private void RefreshOptimizationRanking()
    {
        if (Optimization is not { } result) return;
        if (!BridgeDesignOptimizationPresentation.TryTop(OptimizationTop.Text, out int n))
        { optimizationStatus.Text = BridgeDesignOptimizationPresentation.TopInvalid; return; }
        var panel = Ui.Stack(Ui.Text(BridgeDesignOptimizationPresentation.RankingHeader(result, n), 12, true));
        if (BridgeDesignOptimizationPresentation.BaselineExclusions(result) is { } exclusions) panel.Children.Add(Ui.Text(exclusions, 12, color: Ui.Brush("#895C17")));
        optimizationRanking = Table([.. BridgeDesignOptimizationPresentation.RankingColumns], BridgeDesignOptimizationPresentation.RankingRows(result, n), 350);
        optimizationRanking.CanUserSortColumns = false; optimizationRanking.SelectedIndex = selectedSolution < Math.Min(n, result.Solutions.Length) ? selectedSolution : -1;
        optimizationRanking.SelectionChanged += (_, _) => { if (optimizationRanking.SelectedIndex >= 0 && optimizationRanking.SelectedIndex != selectedSolution) SelectOptimization(optimizationRanking.SelectedIndex); };
        panel.Children.Add(optimizationRanking);
        panel.Children.Add(Ui.Text(BridgeDesignOptimizationPresentation.RankingNote, 11, color: Ui.Muted));
        panel.Children.Add(new Expander { Header = BridgeDesignOptimizationPresentation.ExclusionsHeader, Content = Table([.. BridgeDesignOptimizationPresentation.ExclusionColumns], BridgeDesignOptimizationPresentation.ExclusionRows(result), 300) });
        optimizationResults.Content = panel;
    }
    internal void SelectOptimization(int index)
    {
        if (Optimization is not { } result || index < 0 || index >= result.Solutions.Length) return;
        selectedSolution = index; var c = result.Solutions[index]; var r = BridgeDesignModel.Read(ModuleServices.Get(BridgeDesignModel.Module).Calculate(c.Data));
        OptimizationPreview.Data = c.Data; OptimizationPreview.Result = r; OptimizationPreview.InvalidateVisual(); optimizationApply.IsEnabled = true;
        OptimizationCloud.SelectedRank = c.Rank; OptimizationCloud.InvalidateVisual();
        if (optimizationRanking is not null) optimizationRanking.SelectedIndex = index < optimizationRanking.Items.Count ? index : -1;
        optimizationSelection.Text = BridgeDesignOptimizationPresentation.Selection(c, r);
        optimizationChanges.Content = Ui.Stack(Ui.Text(BridgeDesignOptimizationPresentation.ChangesTitle, 13, true),
            Table([.. BridgeDesignOptimizationPresentation.ChangeColumns], BridgeDesignOptimizationPresentation.ChangeRows(optimizationSource!, result, c, r), 235));
    }
    internal void ApplyOptimization(int index)
    {
        if (Optimization is null || optimizationSource is null || !JsonNode.DeepEquals(optimizationSource, OptimizationSnapshot(Data)))
        { optimizationStatus.Text = BridgeDesignOptimizationPresentation.Stale; return; }
        if (index < 0 || index >= Optimization.Solutions.Length) throw new ArgumentOutOfRangeException(nameof(index));
        var candidate = Clone(Optimization.Solutions[index].Data); var current = Clone(Data); current.Remove("alternative_a");
        Mutate(() => BridgeDesignOptimizationPresentation.ApplySolution(Data, candidate, current), true);
        WorkspaceTabs.SelectedIndex = 0; Outputs.SelectedIndex = 5; Outputs.BringIntoView();
    }
}
