using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Anthea.Application.BridgeDesign;
using Anthea.Application.Presentation;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules.BridgeDesign;
using Anthea.Presentation.Tests;
using X.Desktop.Services;
using X.Desktop.Views.BridgeDesign;

namespace X.Desktop;

/// <summary>The traces and the drawings through the XAML view of Bridge Design (F5.12).</summary>
internal static partial class BridgeDesignViewChecks
{
    // ---------------------------------------------------------------- 1. traces

    private static async Task Traces(string root, string output)
    {
        string folder = Path.Combine(output, "tracce");
        Directory.CreateDirectory(folder);
        var differences = new List<string>();
        int scenarios = 0, lines = 0;
        foreach (var scenario in BridgeDesignScenarios.All(root))
        {
            var trace = new List<string>();
            var engine = new HeldModuleService(Module);
            ViewSide? side = null;
            try
            {
                side = new ViewSide(BridgeDesignScenarios.FixtureData(root, scenario.Fixture), engine);
                // The state of the opening is read at once, before the dispatcher runs anything.
                var previous = side.State();
                trace.AddRange(BridgeDesignTrace.Lines("apertura", null, previous));
                foreach (var step in scenario.Steps)
                {
                    await side.Apply(step);
                    var state = side.State();
                    trace.AddRange(BridgeDesignTrace.Lines(step.ToString(), previous, state));
                    previous = state;
                }
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or TimeoutException or KeyNotFoundException or NullReferenceException)
            { trace.Add("errore " + error.GetType().Name + ": " + error.Message); Log.Add("   " + scenario.Name + ": " + error); }
            finally { engine.Release(); side?.Dispose(); }
            BridgeDesignTrace.Write(Path.Combine(folder, scenario.Name + ".txt"), trace);
            string stored = Path.Combine(BridgeDesignScenarios.Reference(root), scenario.Name + ".txt");
            string? difference = File.Exists(stored) ? BridgeDesignTrace.FirstDifference(BridgeDesignTrace.Read(stored), trace) : "traccia di riferimento assente";
            if (difference is not null) differences.Add(scenario.Name + ": " + difference);
            scenarios++; lines += trace.Count;
        }
        if (differences.Count > 0) File.WriteAllLines(Path.Combine(folder, "differenze.txt"), differences, new UTF8Encoding(false));
        Check(differences.Count == 0 && scenarios == 19, $"Tracce della vista XAML uguali a quelle della vista storica: {scenarios} scenari, {lines} righe"
            + (differences.Count == 0 ? "" : " — " + differences[0]));
    }

    /// <summary>
    /// The sheet with the XAML view, driven through its controls as the user does it: a control the user cannot use (not on the page or in the tab
    /// shown, disabled, or read-only) does nothing; read back from the controls of the view, the panels from the ViewModel (MU19). Three steps call
    /// what the control calls, as the historic trace: «Ponte casuale» (the button draws from the shared generator, the trace from a seeded one), the
    /// script of the price editor (it acts on the ViewModel of the window, then «Applica prezzario» or «Annulla» are pressed in the window) and the
    /// click on a point of the cloud (<see cref="BridgeOptimizationPlot.SelectAt"/> at the point of the rank).
    /// </summary>
    private sealed class ViewSide : IDisposable
    {
        private readonly HeldModuleService engine;
        private readonly Services services = new();
        private Hosted host = null!;
        private string message = "";
        private bool readOnly;
        private Task search = Task.CompletedTask;
        private string? described;

        internal ViewSide(JsonObject data, HeldModuleService engine) { this.engine = engine; Attach(data); }

        internal Hosted Host => host;
        private BridgeDesignSheetViewModel Sheet => host.Sheet;
        private BridgeDesignSheetView View => host.View;

        private void Attach(JsonObject data)
        {
            var module = DesktopComposition.CreateModules().SheetModule(Module);
            host = Idle(() => new Hosted(new MvvmSheetEditor(module, data, services.Create(), engine, new CalculationLimiter(CalculationLimiter.DefaultMaximum))));
            readOnly = false;
        }

        public void Dispose() => host.Dispose();

        // ---------------------------------------------------------------- steps

        internal async Task Apply(BridgeDesignStep step)
        {
            message = "";
            // The user opens the page and the tabs that hold the control, as the historic trace (PlaceForChecks).
            var (page, input, output) = BridgeDesignScenarios.PlaceOf(step);
            if (page is int p && View.Pages.SelectedIndex != p) View.Pages.SelectedIndex = p;
            if (input is int i && View.InputTabs.SelectedIndex != i) View.InputTabs.SelectedIndex = i;
            if (output is int o && View.OutputTabs.SelectedIndex != o) View.OutputTabs.SelectedIndex = o;
            host.Layout();
            var wait = Idle(() => Start(step));
            if (wait is not null) await wait.WaitAsync(TimeSpan.FromSeconds(300));
            await Commands();
            await host.Settle();
        }

        /// <summary>The asynchronous command of the search re-enables its button (CanExecuteChanged) in the continuation that follows its task.</summary>
        private async Task Commands()
        {
            var run = Sheet.RunOptimizationCommand;
            for (int i = 0; i < 200 && View.RunButton.IsEnabled != run.CanExecute(null); i++)
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        }

        private IEnumerable<T> Shown<T>() where T : FrameworkElement => Walk(View, popups: false).OfType<T>().Where(e => e.IsVisible);

        private Task? Start(BridgeDesignStep step)
        {
            var s = Sheet; var view = View;
            switch (step.Kind)
            {
                case BridgeDesignStep.Commit: host.Editor.Commit(); return null;
                // «Calcola» of the window: the editor commits the sheet, then calculates at once.
                case BridgeDesignStep.Calculate: return host.Editor.CalculateAsync();
                case BridgeDesignStep.Start: _ = host.Editor.CalculateAsync(); return null;
                case BridgeDesignStep.Wait: return s.WaitForCalculationAsync();
                case BridgeDesignStep.ReadOnly: host.Editor.InspectRevision(); readOnly = true; return null;
                case BridgeDesignStep.Reopen:
                {
                    var data = host.Editor.Data; host.Dispose(); Attach(data); return null;
                }
                case BridgeDesignStep.Hold: engine.Hold(); return null;
                case BridgeDesignStep.Entered: return engine.EnteredAsync();
                case BridgeDesignStep.Release: engine.Release(); return null;
                case BridgeDesignStep.Visit: Visit(host.Root); return null;
                case BridgeDesignStep.WaitSearch: return search;
                case BridgeDesignStep.Report:
                    try { host.Editor.BuildReport("Traccia F5.12", []); message = "report disponibile"; }
                    catch (Exception error) when (error is InvalidOperationException or ArgumentException) { message = "report non disponibile: " + error.Message; }
                    return null;
                case BridgeDesignStep.Print:
                    message = view.PrintPage(1000, 700) is Viewbox { Child: Panel page }
                        ? "stampa " + string.Join(" | ", page.Children.OfType<TextBlock>().Select(t => BridgeDesignTrace.Quote(t.Text))) : "stampa non disponibile";
                    return null;
                case BridgeDesignStep.Page: view.Pages.SelectedIndex = step.Index; return null;
                case BridgeDesignStep.InputTab: if (Usable(view.InputTabs)) view.InputTabs.SelectedIndex = step.Index; return null;
                case BridgeDesignStep.OutputTab: if (Usable(view.OutputTabs)) view.OutputTabs.SelectedIndex = step.Index; return null;
                case BridgeDesignStep.Field:
                    if (s.Parameters.TryGetValue(step.Key!, out var field) && Shown<TextBox>().FirstOrDefault(t => ReferenceEquals(t.DataContext, field)) is { IsEnabled: true, IsReadOnly: false } edit)
                        edit.Text = step.Text;
                    return null;
                case BridgeDesignStep.Slider:
                    if (s.Parameters.TryGetValue(step.Key!, out var owner) && Shown<Slider>().FirstOrDefault(r => ReferenceEquals(r.DataContext, owner)) is { IsEnabled: true } slider)
                        slider.Value = double.Parse(step.Text!, CultureInfo.InvariantCulture);
                    return null;
                case BridgeDesignStep.Choice:
                    if (Shown<ComboBox>().FirstOrDefault(c => c.DataContext is BridgeDesignChoice choice && choice.Key == step.Key) is { IsEnabled: true } combo) combo.SelectedItem = step.Text;
                    return null;
                case BridgeDesignStep.Option:
                    if (Shown<CheckBox>().FirstOrDefault(c => c.DataContext is BridgeDesignOption option && option.Key == step.Key) is { IsEnabled: true } box) box.IsChecked = box.IsChecked != true;
                    return null;
                case BridgeDesignStep.Family:
                    Press(Shown<Button>().FirstOrDefault(b => b.DataContext is BridgeDesignFamilyOption f && f.Id == step.Text));
                    return null;
                case BridgeDesignStep.Button when step.Key == "ricerca":
                    if (step.Text == "Avvia ottimizzazione")
                    {
                        if (!Usable(view.RunButton)) return null;
                        Press(view.RunButton);
                        search = s.RunOptimizationCommand.ExecutionTask ?? Task.CompletedTask;
                        return null;
                    }
                    Press(Caption(view.OptimizationScroll, step.Text!));
                    return null;
                case BridgeDesignStep.Button: Press(Caption(view.DesignScroll, step.Text!)); return null;
                case BridgeDesignStep.Random: if (Usable(view.RandomButton)) s.Randomize(new Random(step.Index)); return null;
                case BridgeDesignStep.Alternative:
                {
                    var buttons = Walk(view.AdvicePanel).OfType<Button>().Where(b => b.IsVisible && !Equals(b.Content, BridgeDesignPresentation.RestoreEnvironmentButton)).ToArray();
                    if (step.Index < buttons.Length) Press(buttons[step.Index]);
                    return null;
                }
                case BridgeDesignStep.Prices: message = Prices(step.Text!); return null;
                case BridgeDesignStep.Objective: if (Usable(view.ObjectiveChoice)) view.ObjectiveChoice.SelectedIndex = step.Index; return null;
                case BridgeDesignStep.Lock: Press(view.LockBoxes.Single(b => Equals(b.Tag, step.Key))); return null;
                case BridgeDesignStep.Limit:
                {
                    var limit = view.LimitBox(step.Key!);
                    if (!Usable(limit) || limit.IsReadOnly) return null;
                    // The user types (the binding of the box stays), then leaves the box: only «Mostra le prime N» reacts.
                    limit.SetCurrentValue(TextBox.TextProperty, step.Text);
                    limit.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, Environment.TickCount, limit, null) { RoutedEvent = UIElement.LostKeyboardFocusEvent });
                    return null;
                }
                case BridgeDesignStep.Rank:
                    if (Walk(view.SearchResultsPanel).OfType<BridgeDesignGrid>().FirstOrDefault(g => ReferenceEquals(g.Table?.Columns, BridgeDesignOptimizationPresentation.RankingColumns)) is { } ranking
                        && Usable(ranking) && step.Index < ranking.Items.Count) ranking.SelectedIndex = step.Index;
                    return null;
                case BridgeDesignStep.Point:
                    if (Usable(view.CloudPlot) && view.CloudPlot.Points.FirstOrDefault(pt => pt.Rank == step.Index) is { Rank: > 0 } point) view.CloudPlot.SelectAt(point.Point);
                    return null;
                case BridgeDesignStep.Variable: if (Usable(view.VariableChoice)) view.VariableChoice.SelectedIndex = step.Index; return null;
                case BridgeDesignStep.Filter: if (Usable(view.FilterChoice)) view.FilterChoice.SelectedIndex = step.Index; return null;
                case BridgeDesignStep.Pareto: if (Usable(view.ParetoBox)) view.ParetoBox.IsChecked = view.ParetoBox.IsChecked != true; return null;
                default: throw new ArgumentException("Passo sconosciuto: " + step.Kind);
            }
        }

        /// <summary>
        /// «Gestisci prezzario…» with the script of the trace: the editor shown by the dialog service in its window (the DataTemplate of the view); the
        /// script acts on the ViewModel of the window, as the historic trace, then «Applica prezzario» when the script asks for it and the button is
        /// enabled, otherwise «Annulla», pressed in the window.
        /// </summary>
        private string Prices(string script)
        {
            if (Caption(View.DesignScroll, BridgeDesignPresentation.ManagePricesButton) is not { } open || !Usable(open)) return "";
            described = null;
            services.PriceScript = (editor, window) =>
            {
                var view = Walk(window).OfType<PriceCatalogueView>().Single();
                bool apply = BridgeDesignPriceScripts.Run(script, editor);
                window.UpdateLayout();
                if (apply && view.ApplyButton.IsEnabled) Press(view.ApplyButton);
                else Press(view.CancelButton);
                described = BridgeDesignPriceScripts.Describe(editor);
            };
            string before = Sheet.Status;
            try { Press(open); }
            finally { services.PriceScript = null; }
            services.ThrowScriptError();
            return described ?? "prezzario non aperto · stato " + BridgeDesignTrace.Quote(Sheet.Status == before ? "" : Sheet.Status);
        }

        // ---------------------------------------------------------------- state

        internal BridgeDesignTraceState State()
        {
            var s = Sheet; var view = View;
            static string Q(string? text) => BridgeDesignTrace.Quote(text);
            static string N(int value) => BridgeDesignTrace.Number(value);
            static string D(double value) => BridgeDesignTrace.Number(value);
            static string YN(bool value) => BridgeDesignTrace.YesNo(value);
            var t = new BridgeDesignTraceState();
            t.Data(s.Data);
            t.Add("modificato", N(host.Modified));
            t.Add("messaggio", message);
            var a = s.Analysis;
            t.Add("calcolo", "occupato " + YN(host.Editor.Busy) + " · risultato " + (host.Editor.Result is { } result ? BridgeDesignTrace.Hash(result) : "-")
                + " · confronto " + (a is null ? "-" : a.Comparison is { } comparison ? BridgeDesignTrace.Hash(comparison.Json()) : a.ComparisonError is { } error ? "errore " + Q(error) : "nessuno")
                + " · alternative " + (a is null ? "-" : string.Join("|", a.Alternatives.Select(x => x.Label))));
            t.Add("stato", Q(view.StatusText.Text) + (SameColour(view.StatusText.Foreground, view.TryFindResource("Token.Status.Fail")) ? " · errore" : ""));
            var values = new[] { view.MetricValue0, view.MetricValue1, view.MetricValue2, view.MetricValue3 };
            var captions = new[] { view.MetricCaption0, view.MetricCaption1, view.MetricCaption2, view.MetricCaption3 };
            for (int k = 0; k < 4; k++) t.Add("metrica." + k, Q(values[k].Text) + " · " + Q(captions[k].Text));
            t.Add("configurazione", Q(view.ConfigurationText.Text));
            t.Add("pagina", N(view.Pages.SelectedIndex) + " · ingressi " + N(view.InputTabs.SelectedIndex) + " · risultati " + N(view.OutputTabs.SelectedIndex));
            t.Add("annulla", N(s.UndoCount));
            var drawing = view.Drawing;
            t.Add("disegno", "sezione " + YN(drawing.Section) + " · dati " + (drawing.Data is null ? "-" : ReferenceEquals(drawing.Data, s.Data) ? "foglio" : BridgeDesignTrace.Hash(drawing.Data))
                + " · risultato " + (drawing.Result is { } drawn ? BridgeDesignTrace.Hash(drawn.Json()) : "-"));
            t.Add("ingressi", Q(string.Join("|", view.InputTabs.Items.Cast<TabItem>().Select(x => x.Header as string))));
            for (int i = 0; i < s.Inputs.Count; i++) t.Panel("ingressi." + i, BridgeDesignItemBlocks.Blocks(s.Inputs[i]));
            t.Add("risultati", Q(string.Join("|", view.OutputTabs.Items.Cast<TabItem>().Select(x => x.Header as string))));
            t.Panel("pannello.quantita", BridgeDesignItemBlocks.Blocks(s.Quantities));
            t.Panel("pannello.dettagli", BridgeDesignItemBlocks.Blocks(s.Details));
            t.Panel("pannello.confronto", BridgeDesignItemBlocks.Blocks(s.Comparison));
            t.Panel("pannello.prezzario", BridgeDesignItemBlocks.Blocks(s.Prices));
            t.Panel("pannello.ipotesi", BridgeDesignItemBlocks.Blocks(s.Assumptions));
            t.Panel("pannello.quote", BridgeDesignItemBlocks.Blocks(s.Technical));
            t.Panel("pannello.consigli", BridgeDesignItemBlocks.Blocks(s.Advice));
            // The search of the optimum, from the controls of its page.
            t.Add("ricerca", "obiettivo " + N(view.ObjectiveChoice.SelectedIndex) + " · blocchi "
                + string.Join(" ", BridgeDesignScenarios.Locks.Select(k => k + "=" + YN(view.LockBoxes.Single(b => Equals(b.Tag, k)).IsChecked == true)))
                + " · limiti " + string.Join(" ", BridgeDesignScenarios.Limits.Select(k => view.LimitBox(k) is var box ? k + "=" + Q(box.Text) + (box.IsEnabled ? "" : "(spento)") : "")));
            t.Add("ricerca.stato", Q(view.SearchStatusText.Text));
            t.Add("ricerca.migliore", Q(view.BestText.Text));
            t.Add("ricerca.selezione", Q(view.SelectionText.Text) + " · rango " + N(s.SelectedRank));
            t.Add("ricerca.avanzamento", D(view.SearchProgress.Value) + "/" + D(view.SearchProgress.Maximum));
            // A read-only revision disables the buttons that change the sheet: their own state is read only when the user can use them.
            t.Add("ricerca.comandi", readOnly ? "-" : "avvia " + YN(view.RunButton.IsEnabled) + " · applica " + YN(view.ApplyButton.IsEnabled));
            t.Add("ricerca.risultato", s.Optimization is not { } found ? "-" : "valutati " + N(found.Evaluated) + " · ammessi " + N(found.Admissible)
                + " · soluzioni " + N(found.Solutions.Length) + " · tentativi " + N(found.Trials.Length) + " · riferimento " + BridgeDesignTrace.Hash(found.Baseline.Json()));
            t.Add("ricerca.fonte", s.OptimizationSource is null ? "-" : BridgeDesignTrace.Hash(s.OptimizationSource));
            t.Panel("pannello.ricerca", BridgeDesignItemBlocks.Blocks(s.SearchResults));
            t.Panel("pannello.variazioni", BridgeDesignItemBlocks.Blocks(s.SearchChanges));
            var preview = view.Preview;
            t.Add("ricerca.anteprima", "sezione " + YN(preview.Section) + " · dati " + (preview.Data is null ? "-" : BridgeDesignTrace.Hash(preview.Data))
                + " · risultato " + (preview.Result is { } previewed ? BridgeDesignTrace.Hash(previewed.Json()) : "-"));
            var cloud = view.CloudPlot.DataForChecks;
            t.Add("ricerca.nuvola", "tentativi " + N(cloud.Trials) + " · soluzioni " + N(cloud.Solutions) + " · riferimento " + (cloud.Baseline is { } cloudBaseline ? BridgeDesignTrace.Hash(cloudBaseline.Json()) : "-")
                + " · scelto " + N(view.CloudPlot.SelectedRank) + " · tipologia " + (view.CloudPlot.FamilyFilter ?? "-") + " · pareto " + YN(view.CloudPlot.ParetoOnly));
            var trace = view.TracePlot.DataForChecks;
            t.Add("ricerca.traccia", "tentativi " + N(trace.Trials) + " · soluzioni " + N(trace.Solutions) + " · riferimento " + (trace.Baseline is { } traceBaseline ? BridgeDesignTrace.Hash(traceBaseline.Json()) : "-")
                + " · variabile " + N(view.TracePlot.Variable));
            return t;
        }
    }

    /// <summary>Two brushes of the same colour (a token of the theme against the foreground of a text).</summary>
    private static bool SameColour(Brush? brush, object? token) => brush is SolidColorBrush a && token is SolidColorBrush b && a.Color == b.Color;

    // ---------------------------------------------------------------- 2. drawings

    /// <summary>
    /// For every F3 fixture of the module, opened and calculated in the XAML view: the drawing lists of the elevation and of the cross section, of
    /// the moment diagram of the details, and the pixels of the two figures of the report; for the first fixture also the icons of the typologies
    /// and, after a short search, the charts of the search (the cloud, the explored variables one by one, the preview). The same lines as
    /// BridgeDesignHistoricTrace.Drawings, compared with tests/fixtures/f5-bridge-design/disegni/elenco.txt.
    /// </summary>
    private static async Task Drawings(string root, string output)
    {
        string folder = Path.Combine(output, "disegni");
        Directory.CreateDirectory(folder);
        var list = new List<string>();
        bool first = true;
        foreach (string id in BridgeDesignScenarios.Fixtures(root))
        {
            var engine = new HeldModuleService(Module);
            var side = new ViewSide(BridgeDesignScenarios.FixtureData(root, id), engine);
            try
            {
                var host = side.Host; var view = host.View; var sheet = host.Sheet;
                await host.Settle();
                await side.Apply(new(BridgeDesignStep.Wait));
                await side.Apply(new(BridgeDesignStep.Calculate));
                // The drawing of the card in elevation and in section, through the buttons of the card, then back to the view of the sheet.
                bool section = sheet.SectionView;
                Idle(() => { sheet.SectionView = false; return 0; });
                list.Add(CompositeSectionHistoricTrace.Record(folder, id + " prospetto 1000x400", view.Drawing, 1000, 400));
                Idle(() => { sheet.SectionView = true; return 0; });
                list.Add(CompositeSectionHistoricTrace.Record(folder, id + " sezione 1000x400", view.Drawing, 1000, 400));
                Idle(() => { sheet.SectionView = section; return 0; });
                // The moment diagram of the details (in the tree once its tab has been shown).
                int output0 = view.OutputTabs.SelectedIndex;
                view.OutputTabs.SelectedIndex = 1; await host.Settle();
                if (Walk(view.OutputTabs).OfType<BridgeDesignMomentHost>().FirstOrDefault(m => m.IsVisible) is { } moments)
                { moments.Plot.InvalidateVisual(); list.Add(CompositeSectionHistoricTrace.Record(folder, id + " momenti 800x180", moments.Plot, 800, 180)); }
                view.OutputTabs.SelectedIndex = output0; await host.Settle();
                if (sheet.Calculation is not null)
                {
                    list.Add(id + " figura prospetto · " + CompositeSectionHistoricTrace.Pixels(view.Drawing.Png(false)));
                    list.Add(id + " figura sezione · " + CompositeSectionHistoricTrace.Pixels(view.Drawing.Png(true)));
                }
                if (first)
                {
                    view.Pages.SelectedIndex = 0; view.InputTabs.SelectedIndex = 2; await host.Settle();
                    foreach (var icon in Walk(view.InputTabs).OfType<BridgeDesignFamilyIcon>().Where(i => i.IsVisible).Select(i => i.Icon).ToArray())
                    { icon.InvalidateVisual(); list.Add(CompositeSectionHistoricTrace.Record(folder, id + " icona " + icon.Family + " 112x32", icon, 112, 32)); }
                    view.Pages.SelectedIndex = 1; await host.Settle();
                    await Idle(() => sheet.RunOptimizationAsync(new BridgeDesignModel.OptimizationOptions { KeepPier = true, KeepFoundation = true, MinSpans = 3, MaxSpans = 6,
                        DepthPercentMin = 100, DepthPercentMax = 115, DepthPercentStep = 15, PilePercentMin = 100, PilePercentMax = 150, PilePercentStep = 25 })).WaitAsync(TimeSpan.FromSeconds(300));
                    await host.Settle();
                    view.CloudPlot.InvalidateVisual();
                    list.Add(CompositeSectionHistoricTrace.Record(folder, id + " nuvola 900x310", view.CloudPlot, 900, 310));
                    int variable = view.TracePlot.Variable;
                    for (int v = 0; v < 9; v++)
                    {
                        view.TracePlot.Variable = v; view.TracePlot.InvalidateVisual();
                        list.Add(CompositeSectionHistoricTrace.Record(folder, id + " traccia " + v + " 900x310", view.TracePlot, 900, 310));
                    }
                    view.TracePlot.Variable = variable;
                    view.Preview.InvalidateVisual();
                    list.Add(CompositeSectionHistoricTrace.Record(folder, id + " anteprima 900x265", view.Preview, 900, 265));
                    first = false;
                }
            }
            finally { engine.Release(); side.Dispose(); }
        }
        BridgeDesignTrace.Write(Path.Combine(folder, "elenco.txt"), list);
        string stored = Path.Combine(BridgeDesignScenarios.Drawings(root), "elenco.txt");
        string? difference = File.Exists(stored) ? BridgeDesignTrace.FirstDifference(BridgeDesignTrace.Read(stored), list) : "riferimento dei disegni assente";
        Check(difference is null, $"Disegni della vista XAML uguali a quelli della vista storica: {list.Count} righe di disegni/elenco.txt" + (difference is null ? "" : " — " + difference));
    }
}
