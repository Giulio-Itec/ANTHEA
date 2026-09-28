using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;

namespace X.Desktop;

internal sealed partial class BridgeDesignWorkspace
{
    private readonly ContentControl optimizationResults = new(), optimizationChanges = new();
    private readonly TextBlock optimizationStatus = Ui.Text("Scegli le variabili libere e avvia la ricerca.", 12, color: Ui.Muted);
    private readonly TextBlock optimizationSelection = Ui.Text("Anteprima delle alternative", 17, true);
    private readonly TextBlock optimizationBest = Ui.Text("L’ottimo verrà individuato fra le combinazioni esplorate.", 13, true);
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
    private readonly List<BridgeConcept.OptimizationTrial> liveTrials = new();
    private ComboBox optimizationObjective = new();
    private Button optimizationRun = new(), optimizationApply = new();
    private CancellationTokenSource? optimizationCancellation;
    private JsonObject? optimizationSource;
    private DataGrid? optimizationRanking;
    private int selectedSolution = -1;
    internal int SelectedOptimizationRank => selectedSolution + 1;
    internal int DisplayedOptimizationCount => optimizationRanking?.Items.Count ?? 0;
    internal BridgeConcept.OptimizationResult? Optimization { get; private set; }
    private static TextBox SearchEdit(string value) => new() { Text = value, Width = 58, Margin = new Thickness(3), VerticalContentAlignment = VerticalAlignment.Center };

    private void UpdateRangeAvailability()
    {
        foreach (var edit in new[] { depthFrom, depthTo, depthStep }) edit.IsEnabled = optimizationLocks["depth"].IsChecked != true;
        foreach (var edit in new[] { pileFrom, pileTo, pileStep }) edit.IsEnabled = optimizationLocks["foundation"].IsChecked != true;
    }
    private void SuggestOptimizationSpans()
    {
        if (Calculation is not { } r) return;
        if (optimizationLocks["spans"].IsChecked == true) { optimizationMin.Text = optimizationMax.Text = r.Spans.Length.ToString(); return; }
        var families = optimizationLocks["family"].IsChecked == true ? new[] { r.Family } : BridgeConcept.Families;
        var counts = Enumerable.Range(1, 30).Where(n => families.Any(f => BridgeConcept.HasTowers(f.Id) ? n == 3 && r.Length / 2 >= f.MinSpan && r.Length / 2 <= f.MaxSpan : r.Length / n >= f.MinSpan && r.Length / n <= f.MaxSpan)).ToArray();
        if (counts.Length == 0) { optimizationStatus.Text = "Nessun numero di campate tra 1 e 30 compatibile con le luci usuali. Rivedere la tipologia."; return; }
        optimizationMin.Text = counts.Min().ToString(); optimizationMax.Text = counts.Max().ToString();
        optimizationStatus.Text = "Intervallo suggerito dalle luci usuali e dalla lunghezza totale. Ostacoli e luce delle singole campate saranno controllati durante la ricerca.";
    }
    private static JsonObject OptimizationSnapshot(JsonObject source)
    { var result = Clone(source); result.Remove("alternative_a"); result.Remove("scene"); return result; }
    private void ResetOptimizationDisplay()
    {
        Optimization = null; selectedSolution = -1; optimizationRanking = null; optimizationResults.Content = null; optimizationChanges.Content = null;
        optimizationApply.IsEnabled = false; optimizationProgress.Value = 0; liveTrials.Clear();
        OptimizationCloud.SetData([], [], Calculation); OptimizationTrace.SetData([], [], Calculation);
        OptimizationPreview.Data = null; OptimizationPreview.Result = null; OptimizationPreview.InvalidateVisual();
        optimizationBest.Text = "L’ottimo verrà individuato fra le combinazioni esplorate."; optimizationSelection.Text = "Anteprima delle alternative";
    }
    private void ClearOptimizationChoice()
    {
        optimizationCancellation?.Cancel(); optimizationSource = null; ResetOptimizationDisplay();
        optimizationStatus.Text = "Vincoli modificati: avvia la ricerca per aggiornare le alternative.";
    }
    private void InvalidateOptimization()
    {
        if (optimizationSource is not null && !JsonNode.DeepEquals(optimizationSource, OptimizationSnapshot(Data)))
        { ClearOptimizationChoice(); optimizationStatus.Text = "Dati o listino modificati: ripetere l’ottimizzazione."; }
    }
    internal async Task RunOptimization(BridgeConcept.OptimizationOptions? requested = null)
    {
        if (optimizationCancellation is not null) return;
        if (Calculation is null) { optimizationStatus.Text = "Completa prima i dati del ponte."; return; }
        bool Keep(string k) => optimizationLocks[k].IsChecked == true;
        double Read(TextBox box) => double.Parse(box.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture);
        BridgeConcept.OptimizationOptions options;
        try
        {
            options = requested ?? new BridgeConcept.OptimizationOptions {
                Objective = (BridgeConcept.OptimizationObjective)Math.Max(0, optimizationObjective.SelectedIndex),
                KeepFamily = Keep("family"), KeepSpans = Keep("spans"), KeepDepth = Keep("depth"), KeepSection = Keep("section"),
                KeepPier = Keep("pier"), KeepFoundation = Keep("foundation"), KeepContinuity = Keep("continuous"),
                MinSpans = int.Parse(optimizationMin.Text), MaxSpans = int.Parse(optimizationMax.Text), MinDepth = Read(optimizationMinDepth), MaxDepth = Read(optimizationDepth),
                DepthPercentMin = Keep("depth") ? 100 : Read(depthFrom), DepthPercentMax = Keep("depth") ? 115 : Read(depthTo), DepthPercentStep = Keep("depth") ? 15 : Read(depthStep),
                PilePercentMin = Keep("foundation") ? 100 : Read(pileFrom), PilePercentMax = Keep("foundation") ? 150 : Read(pileTo), PilePercentStep = Keep("foundation") ? 25 : Read(pileStep)
            };
        }
        catch (Exception ex) when (ex is FormatException or OverflowException) { optimizationStatus.Text = "Inserire numeri validi nei limiti di ricerca."; return; }
        ResetOptimizationDisplay(); var snapshot = OptimizationSnapshot(Data); optimizationSource = snapshot;
        optimizationCancellation = new CancellationTokenSource(); var token = optimizationCancellation.Token;
        optimizationRun.IsEnabled = false; optimizationStatus.Text = "Ricerca in corso…";
        var watch = System.Diagnostics.Stopwatch.StartNew(); long previewTime = -500; string? previewKey = null;
        var progress = new Progress<BridgeConcept.OptimizationProgress>(p => {
            if (token.IsCancellationRequested || Optimization is not null || !ReferenceEquals(snapshot, optimizationSource) || optimizationCancellation is null) return;
            liveTrials.AddRange(p.NewTrials); OptimizationTrace.SetData(liveTrials.ToArray(), [], Calculation);
            OptimizationCloud.SetData(liveTrials.Where(t => t.Admissible).ToArray(), [], Calculation);
            optimizationProgress.Maximum = p.Planned; optimizationProgress.Value = p.Evaluated;
            optimizationStatus.Text = $"{p.Evaluated:N0} calcolate · {p.Admissible:N0} ammesse · griglia fino a {p.Planned:N0} tentativi (prima dei duplicati).";
            if (p.Best is { } best && watch.ElapsedMilliseconds - previewTime >= 500 && best.Data["input"]!.ToJsonString() != previewKey)
            {
                previewTime = watch.ElapsedMilliseconds; previewKey = best.Data["input"]!.ToJsonString();
                OptimizationPreview.Data = best.Data; OptimizationPreview.Result = best.Result; OptimizationPreview.InvalidateVisual();
                optimizationSelection.Text = "Migliore provvisorio · " + best.Result.Family.Name;
                optimizationBest.Text = $"{F(best.Result.TotalCost, "N0")} € · {F(best.Result.Carbon, "N0")} tCO₂e · graduatoria in corso";
            }
        });
        try
        {
            var result = await Task.Run(() => BridgeConcept.Optimize(snapshot, options, token, progress), token);
            token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(snapshot, optimizationSource) || !JsonNode.DeepEquals(snapshot, OptimizationSnapshot(Data))) return;
            Optimization = result; optimizationProgress.Maximum = Math.Max(1, result.Evaluated); optimizationProgress.Value = result.Evaluated;
            OptimizationTrace.SetData(result.Trials, result.Solutions, result.Baseline); OptimizationCloud.SetData(result.Trials, result.Solutions, result.Baseline);
            optimizationStatus.Text = $"{result.Evaluated:N0} calcolate · {result.Admissible:N0} ammesse · {result.Solutions.Length:N0} geometrie distinte · {result.Evaluated - result.Admissible:N0} escluse.";
            if (result.Solutions.Length > 0)
            {
                var best = result.Solutions[0].Trial;
                optimizationBest.Text = $"OTTIMO ESPLORATO · {F(best.Cost!.Value, "N0")} € · {F(best.Carbon!.Value, "N0")} tCO₂e";
                SelectOptimization(0);
            }
            else { OptimizationPreview.Result = null; OptimizationPreview.Data = null; OptimizationPreview.InvalidateVisual(); optimizationBest.Text = "Nessuna soluzione ammessa: esamina i motivi di esclusione."; optimizationSelection.Text = "Nessuna anteprima disponibile"; }
            RefreshOptimizationRanking();
        }
        catch (OperationCanceledException) { ResetOptimizationDisplay(); optimizationStatus.Text = "Ricerca interrotta. Nessuna modifica applicata al ponte."; }
        catch (ArgumentException ex) { ResetOptimizationDisplay(); optimizationStatus.Text = ex.Message; }
        finally { optimizationCancellation.Dispose(); optimizationCancellation = null; optimizationRun.IsEnabled = true; }
    }
    private void RefreshOptimizationRanking()
    {
        if (Optimization is not { } result) return;
        if (!int.TryParse(OptimizationTop.Text, out int n) || n < 1 || n > 50000)
        { optimizationStatus.Text = "N deve essere un intero fra 1 e 50.000. La ricerca eseguita è conservata."; return; }
        var panel = Ui.Stack(Ui.Text($"{Math.Min(n, result.Solutions.Length):N0} di {result.Solutions.Length:N0} soluzioni distinte · obiettivo: {new[] { "costo", "CO₂", "compromesso 50/50" }[(int)result.Options.Objective]}. Cambiare N non ripete il calcolo.", 12, true));
        if (result.BaselineExclusions.Length > 0) panel.Children.Add(Ui.Text("Il progetto corrente non soddisfa i filtri: " + string.Join("; ", result.BaselineExclusions), 12, color: Ui.Brush("#895C17")));
        optimizationRanking = Table(["# / tipologia", "n / d [m]", "Costo [€]", "Δ costo [€]", "CO₂ [t]", "Punteggio", "Pareto"], result.Solutions.Take(n).Select(c =>
            new[] { $"{c.Rank} · {BridgeConcept.Families.Single(f => f.Id == c.Trial.Family).Name}", $"{c.Trial.Spans} / {F(c.Trial.Depth!.Value, "N2")}", F(c.Trial.Cost!.Value, "N0"), F(c.Trial.Cost.Value - result.Baseline.TotalCost, "N0"), F(c.Trial.Carbon!.Value, "N0"), F(c.Score, "N3"), c.Pareto ? "Sì" : "—" }), 350);
        optimizationRanking.CanUserSortColumns = false; optimizationRanking.SelectedIndex = selectedSolution < Math.Min(n, result.Solutions.Length) ? selectedSolution : -1;
        optimizationRanking.SelectionChanged += (_, _) => { if (optimizationRanking.SelectedIndex >= 0 && optimizationRanking.SelectedIndex != selectedSolution) SelectOptimization(optimizationRanking.SelectedIndex); };
        panel.Children.Add(optimizationRanking);
        panel.Children.Add(Ui.Text("Seleziona una riga per confrontare quote e disegno. L’ordine considera il punteggio non arrotondato; a parità, costo, CO₂, tipologia e campate.", 11, color: Ui.Muted));
        panel.Children.Add(new Expander { Header = "Motivi di esclusione · una combinazione può avere più motivi", Content = Table(["Motivo", "Occorrenze"], result.Excluded.OrderByDescending(p => p.Value).Select(p => new[] { p.Key, p.Value.ToString() }), 300) });
        optimizationResults.Content = panel;
    }
    internal void SelectOptimization(int index)
    {
        if (Optimization is not { } result || index < 0 || index >= result.Solutions.Length) return;
        selectedSolution = index; var c = result.Solutions[index]; var r = BridgeConcept.Calculate(c.Data);
        OptimizationPreview.Data = c.Data; OptimizationPreview.Result = r; OptimizationPreview.InvalidateVisual(); optimizationApply.IsEnabled = true;
        OptimizationCloud.SelectedRank = c.Rank; OptimizationCloud.InvalidateVisual();
        if (optimizationRanking is not null) optimizationRanking.SelectedIndex = index < optimizationRanking.Items.Count ? index : -1;
        optimizationSelection.Text = $"#{c.Rank} · {r.Family.Name} · {(c.Pareto ? "frontiera Pareto" : "alternativa ammessa")}";
        var rows = new List<string[]>();
        void Row(string label, string before, string after, string delta = "—") => rows.Add([label, before, after, delta]);
        void Numeric(string label, double before, double after, string format = "N2") => Row(label, F(before, format), F(after, format), F(after - before, format));
        Row("Tipologia", result.Baseline.Family.Name, r.Family.Name);
        Numeric("Costo [€]", result.Baseline.TotalCost, r.TotalCost, "N0"); Numeric("CO₂ [t]", result.Baseline.Carbon, r.Carbon, "N1");
        Row("Luci [m]", string.Join(" + ", result.Baseline.Spans.Select(s => F(s, "N2"))), string.Join(" + ", r.Spans.Select(s => F(s, "N2"))));
        Row("Schema pila", BridgeConcept.HasTowers(result.Baseline.Family.Id) ? "Antenna · 2 fusti quadrati" : optimizationSource!["input"].S("pier"), c.Trial.Pier); Row("Fondazione", result.Baseline.Foundation, r.Foundation);
        Row("Continuità", optimizationSource!["input"].B("continuous") ? "Continua" : "Indipendente", c.Data["input"].B("continuous") ? "Continua" : "Indipendente");
        var old = BridgeConcept.TechnicalSchedule(optimizationSource!, result.Baseline).ToDictionary(t => (t.Component, t.Symbol, t.Unit));
        foreach (var t in BridgeConcept.TechnicalSchedule(c.Data, r))
        {
            if (old.TryGetValue((t.Component, t.Symbol, t.Unit), out var b)) Numeric($"{t.Component} · {t.Symbol} [{t.Unit}]", b.Value, t.Value, t.Unit == "n." ? "N0" : t.Unit == "mm" ? "N1" : "N3");
            else Row($"{t.Component} · {t.Symbol} [{t.Unit}]", "n.a.", F(t.Value, "N3"));
        }
        optimizationChanges.Content = Ui.Stack(Ui.Text("Variazioni rispetto al progetto corrente · quote adottate", 13, true), Table(["Variabile", "Corrente", "Selezionata", "Δ"], rows, 235));
    }
    internal void ApplyOptimization(int index)
    {
        if (Optimization is null || optimizationSource is null || !JsonNode.DeepEquals(optimizationSource, OptimizationSnapshot(Data)))
        { optimizationStatus.Text = "Risultati non aggiornati: ripetere la ricerca."; return; }
        if (index < 0 || index >= Optimization.Solutions.Length) throw new ArgumentOutOfRangeException(nameof(index));
        var candidate = Clone(Optimization.Solutions[index].Data); var current = Clone(Data); current.Remove("alternative_a");
        Mutate(() => { if (Data["alternative_a"] is null) Data["alternative_a"] = current; Data["input"] = candidate["input"]!.DeepClone(); }, true);
        WorkspaceTabs.SelectedIndex = 0; Outputs.SelectedIndex = 5; Outputs.BringIntoView();
    }
}
