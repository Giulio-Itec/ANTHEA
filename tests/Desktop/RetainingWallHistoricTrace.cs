using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Anthea.Presentation.Tests;

namespace X.Desktop;

/// <summary>
/// --check-muri-historic &lt;radice&gt; &lt;cartella&gt; and --check-muri-historic-capture &lt;radice&gt; &lt;cartella&gt; (UiTests, F5.9, proposal F5
/// section 7.2): the scenarios of tests/Shared/RetainingWallTrace.cs on the historic view <see cref="RetainingWallWorkspace"/>, off screen,
/// with the calculation service of the trace (<see cref="HeldModuleService"/>, which can hold the background calculation and the
/// validation of the window of the values). The view is created as SheetEditor creates it, on a copy of the data, with its events
/// counted. Every step is done as the user does it, through the controls: a control that the user cannot use (hidden, disabled, or
/// protected by the read-only revision) does nothing. After the opening the whole state seen by the user is written, then after every
/// step what changed (<see cref="RetainingWallTrace.Lines"/>). The capture writes the traces in the folder; the check also compares
/// them with the traces frozen in tests/fixtures/f5-muri/storica, which the ViewModel tests without WPF compare with
/// RetainingWallSheetViewModel. The check stays in the ui stage until the historic view is archived (Q11). Writes esito.txt
/// («PASS n scenari …»).
/// </summary>
internal static class RetainingWallHistoricTrace
{
    internal static async Task Run(string root, string output, bool check)
    {
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("La cartella della cattura deve essere vuota.");
        Directory.CreateDirectory(output);
        var culture = CultureInfo.GetCultureInfo("it-IT");
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
        var scenarios = RetainingWallScenarios.All(root);
        string reference = RetainingWallScenarios.Reference(root);
        int lines = 0;
        var differences = new List<string>();
        foreach (var scenario in scenarios)
        {
            var trace = await Trace(root, scenario);
            RetainingWallTrace.Write(Path.Combine(output, scenario.Name + ".txt"), trace);
            lines += trace.Count;
            if (!check) continue;
            string stored = Path.Combine(reference, scenario.Name + ".txt");
            string? difference = File.Exists(stored) ? RetainingWallTrace.FirstDifference(RetainingWallTrace.Read(stored), trace) : "traccia di riferimento assente";
            if (difference is not null) differences.Add(scenario.Name + ": " + difference);
        }
        if (check)
        {
            var names = scenarios.Select(s => s.Name + ".txt").ToHashSet(StringComparer.Ordinal);
            foreach (string extra in Directory.Exists(reference) ? Directory.EnumerateFiles(reference, "*.txt").Select(n => Path.GetFileName(n)).Where(n => !names.Contains(n)) : [])
                differences.Add(extra + ": traccia di riferimento senza scenario");
            if (differences.Count > 0)
            {
                File.WriteAllLines(Path.Combine(output, "differenze.txt"), differences, new UTF8Encoding(false));
                throw new InvalidOperationException("Vista storica dei muri diversa dalle tracce congelate: " + differences[0]);
            }
        }
        File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {scenarios.Count} scenari, {lines} righe della vista storica"
            + (check ? " uguali alle tracce congelate" : " catturate"), new UTF8Encoding(false));
    }

    static async Task<List<string>> Trace(string root, RetainingWallScenario scenario)
    {
        var lines = new List<string>();
        var engine = new HeldModuleService(RetainingWallScenarios.Module);
        var ids = new RetainingWallIds();
        var side = new HistoricSide(RetainingWallScenarios.FixtureData(root, scenario.Fixture), engine, ids);
        try
        {
            var previous = side.State();
            lines.AddRange(RetainingWallTrace.Lines("apertura", null, previous));
            foreach (var step in scenario.Steps)
            {
                await side.Apply(step);
                var state = side.State();
                lines.AddRange(RetainingWallTrace.Lines(step.ToString(), previous, state));
                previous = state;
            }
        }
        finally { engine.Release(); side.Dispose(); }
        return lines;
    }

    /// <summary>The historic sheet driven through its controls, as the user does.</summary>
    private sealed class HistoricSide : IDisposable
    {
        private readonly HeldModuleService engine;
        private readonly RetainingWallIds ids;
        private RetainingWallWorkspace view = null!;
        private readonly RetainingWallWorkspace.TraceCounters counters = new();
        private Action? releaseInspection;

        internal HistoricSide(JsonObject data, HeldModuleService engine, RetainingWallIds ids) { this.engine = engine; this.ids = ids; Open(data); }

        // As SheetEditor: the view on a copy of the data, its events forwarded to the window (here counted).
        private void Open(JsonObject data)
        {
            view = new RetainingWallWorkspace((JsonObject)data.DeepClone(), engine);
            counters.Reset();
            view.Modified += () => counters.Modified++;
            view.ReportRequested += () => counters.Reports++;
            view.SoilTransferRequested += () => counters.Soils++;
            view.SectionRequested += (section, name) => counters.Sections.Add((name, section));
            Layout();
        }

        private void Layout() { view.Measure(new Size(1600, 1000)); view.Arrange(new Rect(0, 0, 1600, 1000)); view.UpdateLayout(); }

        internal RetainingWallTraceState State() => view.TraceForChecks(new RetainingWallTraceState(ids), counters, releaseInspection is not null);

        internal async Task Apply(RetainingWallStep step)
        {
            switch (step.Kind)
            {
                case RetainingWallStep.Commit: view.Commit(); break;
                // «Ricalcola» (inspection allowed): the click runs CalculateAsync, here awaited.
                case RetainingWallStep.Calculate: await view.CalculateAsync(); break;
                case RetainingWallStep.Start: _ = view.CalculateAsync(); break;
                case RetainingWallStep.Wait: await view.PendingForChecks; break;
                case RetainingWallStep.ReadOnly: releaseInspection = RevisionInspection.Protect(view); break;
                case RetainingWallStep.Reopen:
                    var data = view.Data; view.CloseParametersForChecks(); releaseInspection?.Invoke(); releaseInspection = null; view.Dispose(); Open(data); break;
                case RetainingWallStep.Hold: engine.Hold(); break;
                case RetainingWallStep.Entered: await engine.EnteredAsync(); break;
                case RetainingWallStep.Release: engine.Release(); break;
                case RetainingWallStep.ParametersWait: await view.ParametersPendingForChecks; break;
                case RetainingWallStep.Parameters: await view.OpenParametersForChecks(); break;
                default: view.ApplyForChecks(step); break;
            }
            Layout();
        }

        public void Dispose() { view.CloseParametersForChecks(); releaseInspection?.Invoke(); view.Dispose(); }
    }
}

internal sealed partial class RetainingWallWorkspace
{
    /// <summary>UiTests (F5.9 traces): the events of the view raised since its opening.</summary>
    internal sealed class TraceCounters
    {
        public int Modified, Reports, Soils;
        public List<(string Name, JsonObject Data)> Sections = [];
        public void Reset() { Modified = 0; Reports = 0; Soils = 0; Sections = []; }
    }

    private static readonly FieldInfo FormRows = typeof(InputForm).GetField("rows", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(nameof(InputForm), "rows");
    private static readonly FieldInfo FormTable = typeof(InputForm).GetField("table", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(nameof(InputForm), "table");
    private static readonly IReadOnlyDictionary<string, string> PointTitles = new Dictionary<string, string>
    { ["punti-valle"] = "Superficie a valle · punti x, y", ["punti-monte"] = "Superficie a monte · punti x, y", ["punti-falda"] = "Linea di falda · quote y, non profondità" };
    private Window? parametersWindow;

    /// <summary>UiTests: the last work requested (automatic recalculation, «Ricalcola», global search, search of the reinforcement).</summary>
    internal Task PendingForChecks => scheduledCalculation;

    /// <summary>UiTests: the last «Applica e ricalcola» of the window of the values.</summary>
    internal Task ParametersPendingForChecks => parameterApplyTask;

    /// <summary>UiTests: closes the window of the values, if open.</summary>
    internal void CloseParametersForChecks() { parametersWindow?.Close(); parametersWindow = null; }

    private static bool Visible(DependencyObject element, DependencyObject root)
    {
        for (DependencyObject? node = element; node is not null && !ReferenceEquals(node, root); node = LogicalTreeHelper.GetParent(node))
            if (node is UIElement { Visibility: not Visibility.Visible }) return false;
        return true;
    }

    private bool Usable(UIElement element) => element.IsEnabled && Visible(element, this);

    private static List<T> Controls<T>(DependencyObject root) where T : DependencyObject
    {
        var found = new List<T>(); var pending = new Stack<object>(); pending.Push(root);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (node is T match) found.Add(match);
            if (node is DependencyObject element) foreach (var child in LogicalTreeHelper.GetChildren(element).Cast<object>().Reverse()) pending.Push(child);
        }
        return found;
    }

    private void Click(ButtonBase? button) { if (button is not null && Usable(button)) button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button)); }

    private Button? Named(string caption) => Controls<Button>(this).FirstOrDefault(b => Equals(b.Content, caption));

    private Expander Group(string title) => Controls<Expander>(this).Single(e => Equals(e.Header, title));

    /// <summary>The tables of the inputs by the name of the trace; null when the table does not exist (yet).</summary>
    private JsonGrid? Table(string name) => name switch
    {
        "valle" => ValleyLayerGrid, "monte" => LayerGrid, "azioni" => ActionGrid, "matrice" => MatrixGrid,
        "globale-monte" => GlobalLayers, "globale-valle" => GlobalValleyLayers, "matrice-globale" => GlobalMatrix,
        "cedimenti" => Controls<JsonGrid>(Group("Cedimenti e spostamenti in esercizio")).Single(),
        _ when PointTitles.TryGetValue(name, out var title) => Controls<JsonGrid>(Group(title)).Single(),
        _ => throw new ArgumentException("Tabella sconosciuta: " + name)
    };

    /// <summary>The buttons under a table: the bar that follows it in its panel.</summary>
    private Button? TableButton(JsonGrid grid, string caption)
    {
        var panel = (Panel)LogicalTreeHelper.GetParent(grid);
        return panel.Children.OfType<WrapPanel>().SelectMany(Controls<Button>).FirstOrDefault(b => Equals(b.Content, caption));
    }

    private (InputForm Form, FrameworkElement Editor) Editor(string path)
    {
        var parts = path.Split('/');
        var form = Forms[parts[0]];
        return (form, form.Editors[parts[1]]);
    }

    /// <summary>UiTests (F5.9 traces): a step of the user on the controls of the view; a control the user cannot use does nothing.</summary>
    internal void ApplyForChecks(RetainingWallStep step)
    {
        // The user opens the page of the control first; the layout lets the read-only revision protect the controls now shown.
        if (RetainingWallScenarios.PageOf(step) is int page && Pages.SelectedIndex != page)
        {
            Pages.SelectedIndex = page;
            Measure(new Size(1600, 1000)); Arrange(new Rect(0, 0, 1600, 1000)); UpdateLayout();
        }
        switch (step.Kind)
        {
            case RetainingWallStep.Field:
                // The user focuses the field (the raw text replaces the formatted one), types and leaves: InputForm.Set does the same writes.
                if (Forms.ContainsKey(step.Key!.Split('/')[0]) && Editor(step.Key!) is ({ } form, TextBox text) && Usable(text) && !text.IsReadOnly)
                    form.Set(step.Key!.Split('/')[1], step.Text!);
                break;
            case RetainingWallStep.Choice:
                if (Forms.ContainsKey(step.Key!.Split('/')[0]) && Editor(step.Key!).Editor is ComboBox choice && Usable(choice) && !Equals(choice.SelectedItem, step.Text))
                {
                    choice.SelectedItem = step.Text;
                    // A new type of an action rebuilds its form on the dispatcher (EditAction, queued by the edit, which has just cancelled every
                    // pending calculation): the user sees the new form at once.
                    if (step.Key == "action/type") Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
                }
                break;
            case RetainingWallStep.Option:
                if (Forms.ContainsKey(step.Key!.Split('/')[0]) && Editor(step.Key!).Editor is CheckBox box && Usable(box)) box.IsChecked = box.IsChecked != true;
                break;
            case RetainingWallStep.Family:
                if (Usable(Family) && Family.Items.Cast<ComboBoxItem>().SingleOrDefault(i => (string)i.Tag == step.Text) is { IsEnabled: true } item) Family.SelectedItem = item;
                break;
            case RetainingWallStep.Example: Click(Named(step.Text == "gravity" ? "Gravità esempio" : "Mensola esempio")); break;
            case RetainingWallStep.Cell:
            {
                var parts = step.Key!.Split('/');
                var grid = Table(parts[0]);
                if (grid is null || grid.IsReadOnly || !Usable(grid) || step.Row >= grid.Rows.Count) break;
                if (grid.Columns.Single(c => c.SortMemberPath == parts[1]).IsReadOnly) break;
                // The bindings of the cells write the row at every keystroke (UpdateSourceTrigger=PropertyChanged).
                var row = grid.Rows[step.Row];
                if (step.Text is null) row[parts[1]] = !(row[parts[1]] is true);
                else row[parts[1]] = step.Text;
                break;
            }
            case RetainingWallStep.Select:
            {
                var grid = Table(step.Key!);
                if (grid is not null && Visible(grid, this)) grid.SelectedIndex = step.Row;
                break;
            }
            case RetainingWallStep.Add:
            {
                var grid = Table(step.Key!);
                if (grid is null) break;
                if (step.Key == "azioni")
                {
                    var type = ((Panel)LogicalTreeHelper.GetParent(grid)).Children.OfType<WrapPanel>().SelectMany(Controls<ComboBox>).Single();
                    if (Usable(type)) type.SelectedItem = step.Text;
                    Click(TableButton(grid, "+ Azione"));
                }
                else Click(TableButton(grid, step.Key!.StartsWith("punti-", StringComparison.Ordinal) ? "+ Punto" : "+ Strato"));
                break;
            }
            case RetainingWallStep.Remove:
            {
                var grid = Table(step.Key!);
                if (grid is null) break;
                Click(TableButton(grid, step.Key switch
                {
                    "azioni" => "− Azione", "valle" or "monte" => "−", _ when step.Key!.StartsWith("punti-", StringComparison.Ordinal) => "− Punto", _ => "− Strato"
                }));
                break;
            }
            case RetainingWallStep.Move:
            {
                var grid = Table(step.Key!);
                if (grid is not null) Click(TableButton(grid, step.Index < 0 ? "↑" : "↓"));
                break;
            }
            case RetainingWallStep.Button:
                Click(step.Text switch
                {
                    "Sisma" => SeismicButton, "Stabilità globale" => GlobalSetupButton, "Calcola armature" => DesignButton,
                    "Invia / carica terreno…" => SoilButton, "Apri sezione in c.a." => SendSectionButton,
                    "Applica proposta" => designPreview.Content is null ? null : Controls<Button>((DependencyObject)designPreview.Content).SingleOrDefault(b => Equals(b.Content, step.Text)),
                    "Modifica profilo, strati e ricerca" => checks.Content is null ? null : Controls<Button>((DependencyObject)checks.Content).SingleOrDefault(b => Equals(b.Content, step.Text)),
                    _ => Named(step.Text!) ?? throw new ArgumentException("Pulsante sconosciuto: " + step.Text)
                });
                break;
            case RetainingWallStep.RemoveHistory:
            {
                var host = (Panel)((StackPanel)Group("Spostamenti permanenti · Newmark SLD / SLV").Content).Children[^1];
                if (step.Index < host.Children.Count) Click(Controls<Button>(host.Children[step.Index]).Single(b => Equals(b.Content, "Elimina accelerogramma")));
                break;
            }
            case RetainingWallStep.Combination: Choose(Combination, step.Text!); break;
            case RetainingWallStep.View: Choose(ViewMode, step.Text!); break;
            case RetainingWallStep.Member: Choose(Member, step.Text!); break;
            case RetainingWallStep.Filter: Choose(CheckFilter, step.Text!); break;
            case RetainingWallStep.GlobalCombination: Choose(GlobalCombination, step.Text!); break;
            case RetainingWallStep.ResultRow:
                if (checks.Content is DependencyObject panel && Controls<DataGrid>(panel).FirstOrDefault() is { } results && step.Row < results.Items.Count) results.SelectedIndex = step.Row;
                break;
            case RetainingWallStep.Toggle:
                if (Controls<CheckBox>(this).SingleOrDefault(c => Equals(c.Content, step.Key)) is { } toggle && Usable(toggle)) toggle.IsChecked = toggle.IsChecked != true;
                break;
            case RetainingWallStep.Parameter:
            {
                if (parametersWindow is null) break;
                var grid = Controls<JsonGrid>(parametersWindow).Single();
                if (grid.Rows.SingleOrDefault(r => r.Values.S("path") == step.Key) is { } row && !grid.IsReadOnly && parametersWindow.IsEnabled) row["value"] = step.Text;
                break;
            }
            case RetainingWallStep.ParameterCombination:
                if (parametersWindow is not null && parametersWindow.IsEnabled) Controls<ComboBox>(parametersWindow).Single().SelectedItem = step.Text;
                break;
            case RetainingWallStep.ParametersApply:
                if (parametersWindow is not null) Click(Controls<Button>(parametersWindow).Single(b => Equals(b.Content, "Applica e ricalcola")));
                break;
            case RetainingWallStep.ParametersClose: parametersWindow?.Close(); break;
            default: throw new ArgumentException("Passo sconosciuto: " + step.Kind);
        }
    }

    private void Choose(ComboBox combo, string item)
    {
        if (!Usable(combo) || combo.ItemsSource is not IEnumerable items || !items.Cast<object>().Any(i => Equals(i, item))) return;
        combo.SelectedItem = item;
    }

    /// <summary>
    /// UiTests: «Valori di calcolo…» (inspection allowed): the calculation at once, then the window of the values. The historic button
    /// shows it as a dialog; the trace shows it off screen and modeless, so that the steps can drive it.
    /// </summary>
    internal async Task OpenParametersForChecks()
    {
        if (Named("Valori di calcolo…") is not { } open || !Usable(open)) return;
        await CalculateAsync();
        if (disposed) return;
        var window = CalculationParametersWindow();
        window.WindowStartupLocation = WindowStartupLocation.Manual; window.Left = -32000; window.Top = -32000;
        window.ShowActivated = false; window.ShowInTaskbar = false;
        window.Closed += (_, _) => { if (ReferenceEquals(parametersWindow, window)) parametersWindow = null; };
        parametersWindow = window;
        window.Show();
    }

    // ---------------------------------------------------------------- state

    /// <summary>The state of the view for the trace of F5.9: what the controls show, read without changing them.</summary>
    internal RetainingWallTraceState TraceForChecks(RetainingWallTraceState t, TraceCounters counters, bool readOnly)
    {
        string Q(string? text) => RetainingWallTrace.Quote(text);
        string N(int value) => RetainingWallTrace.Number(value);
        string YN(bool value) => RetainingWallTrace.YesNo(value);
        t.Data(Data);
        t.Add("foglio", "modificato " + N(counters.Modified) + " · sola lettura " + YN(readOnly) + " · occupato " + YN(Busy) + " · report " + N(counters.Reports)
            + " · terreno " + N(counters.Soils));
        t.Add("sezioni", string.Join(" | ", counters.Sections.Select(s => Q(s.Name) + " " + t.Hash(s.Data))));
        t.Add("risultato", Calculation is null ? "-" : t.Hash(Calculation.Json()));
        t.Add("globale", GlobalResult is null ? "-" : (independentGlobal is not null ? "indipendente" : "dal calcolo") + " · casi " + N(GlobalResult.Cases.Length));
        t.Add("stato", Q(status.Text));
        var family = (ComboBoxItem)Family.SelectedItem;
        t.Add("tipologia", (string)family.Tag + " · voci " + Q(string.Join("|", Family.Items.Cast<ComboBoxItem>().Select(i => (string)i.Content + (i.IsEnabled ? "" : " (spenta)")))));
        t.Add("pulsante.sisma", Q((string)SeismicButton.Content) + " · " + Q(SeismicButton.ToolTip as string));
        t.Add("pulsante.globale", Q((string)GlobalSetupButton.Content) + " · " + Q(GlobalSetupButton.ToolTip as string));
        t.Add("pulsante.armature", YN(DesignButton.IsEnabled));
        t.Add("pagina", N(Pages.SelectedIndex));
        t.Add("combinazioni", Q(string.Join("|", (Combination.ItemsSource as IEnumerable<string>) ?? [])) + " · scelta " + Q(Combination.SelectedItem as string));
        t.Add("vista", Q(ViewMode.SelectedItem as string) + " · elemento " + Q(Member.SelectedItem as string) + (Member.Visibility == Visibility.Visible ? "" : " nascosto")
            + " · filtro " + Q(CheckFilter.SelectedItem as string));
        t.Add("combinazioni-globali", Q(string.Join("|", (GlobalCombination.ItemsSource as IEnumerable<string>) ?? [])) + " · scelta " + Q(GlobalCombination.SelectedItem as string)
            + (GlobalCombination.Visibility == Visibility.Visible ? "" : " · nascoste"));
        t.Add("interruttori", string.Join(" ", RetainingWallScenarios.Toggles.Select(name => name + "=" + YN(Controls<CheckBox>(this).Single(c => Equals(c.Content, name)).IsChecked == true)))
            + (globalFullProfile.Visibility == Visibility.Visible ? "" : " · profilo nascosto"));
        t.Add("riepilogo", Q(summary.Text));
        foreach (string key in RetainingWallScenarios.FormKeys.Concat(Forms.Keys.Where(k => k.StartsWith("history_", StringComparison.Ordinal))))
        {
            if (!Forms.TryGetValue(key, out var form)) continue;
            t.Add("form." + key, Visible(form, this) ? "visibile" : "nascosto");
            foreach (string field in form.Editors.Keys) t.Field(FieldState(key, form, field));
        }
        foreach (var (name, card) in Cards.OrderBy(c => c.Key, StringComparer.Ordinal))
            t.Add("scheda." + name, (card.Visibility == Visibility.Visible ? "visibile" : "nascosta") + " · " + (card.IsExpanded ? "aperta" : "chiusa"));
        foreach (string name in RetainingWallScenarios.TableKeys)
        {
            var grid = Table(name);
            if (grid is null) { t.Add("tabella." + name, "-"); continue; }
            t.Table(name, grid.Columns.Select(c => c.Header as string ?? "").ToArray(), grid.Rows.Select(r => r.Values).ToArray(), grid.SelectedIndex);
        }
        t.Add("testo.strati", Q(layerStatus.Text));
        t.Add("testo.matrice", Q(matrixStatus.Text));
        t.Add("testo.matrice-globale", Q(globalMessage.Text));
        t.Add("testo.globale", Q(globalReadiness.Text) + " · " + Q(globalReadiness.ToolTip as string));
        t.Add("testo.ricerca", Q(globalSearchSummary.Text));
        t.Add("testo.sisma", Q(seismicAudit.Text));
        t.Add("testo.copriferro", Q(wallCoverStatus.Text));
        var histories = (Panel)((StackPanel)Group("Spostamenti permanenti · Newmark SLD / SLV").Content).Children[^1];
        t.Add("accelerogrammi", string.Join(" | ", histories.Children.OfType<Expander>().Select(e => Q(e.Header as string) + " "
            + Q(Controls<TextBlock>((DependencyObject)e.Content).First(b => b.Text.Contains("campioni salvati", StringComparison.Ordinal)).Text))));
        t.Panel("spinte", Blocks(pressures.Content));
        t.Panel("verifiche", Blocks(checks.Content));
        t.Add("sezione", Q(sectionSelection.Text) + " · invio " + YN(SendSectionButton.IsEnabled));
        t.Panel("proposta", Blocks(designPreview.Content));
        t.Add("disegno", "caso " + Q(Drawing.Case?.Name) + " · risultato " + YN(Drawing.Calculation is not null && ReferenceEquals(Drawing.Calculation, Calculation))
            + " · azione " + (Drawing.SelectedAction ?? "-") + " · strato " + N(Drawing.SelectedLayer) + (Drawing.SelectedValley ? " valle" : "")
            + " · spinta " + N(Drawing.SelectedPressure) + " · carichi " + YN(Drawing.ShowLoads) + " · dati " + YN(Drawing.ShowLabels)
            + " · armature " + YN(Drawing.ShowRebar) + " · spinte " + YN(Drawing.ShowPressures));
        t.Add("diagrammi", (Diagrams.Visibility == Visibility.Visible ? "visibili" : "nascosti") + " · modo " + Q(Diagrams.Mode) + " · diagrammi " + YN(Diagrams.Diagrams)
            + " · elemento " + Q(Diagrams.Member) + " · caso " + Q(Diagrams.Case?.Name) + " · armature " + YN(Diagrams.ShowRebar) + " · carichi " + YN(Diagrams.ShowLoads)
            + " · larghezza " + (double.IsPositiveInfinity(Diagrams.MaxWidth) ? "libera" : "1050"));
        t.Add("disegno-globale", (GlobalDrawing.Visibility == Visibility.Visible ? "visibile" : "nascosto") + " · caso " + Q(GlobalDrawing.Case?.Factors.Name)
            + " · risultato " + YN(GlobalDrawing.Result is not null && ReferenceEquals(GlobalDrawing.Result, GlobalResult)) + " · critica " + YN(GlobalDrawing.FocusCritical)
            + " · concio " + N(GlobalDrawing.SelectedSlice));
        t.Add("anteprima-globale", globalPreview is null ? "-" : "caso " + Q(globalPreview.Case?.Factors.Name) + " · risultato "
            + YN(globalPreview.Result is not null && ReferenceEquals(globalPreview.Result, GlobalResult)) + " · strato " + N(globalPreview.SelectedSoil) + " " + globalPreview.SelectedColumn);
        Parameters(t);
        return t;
    }

    private void Parameters(RetainingWallTraceState t)
    {
        if (parametersWindow is null) { t.Add("parametri", "-"); return; }
        var body = (StackPanel)((ScrollViewer)((Border)parametersWindow.Content).Child).Content;
        var message = (TextBlock)body.Children[1];
        var combo = Controls<ComboBox>(parametersWindow).Single();
        var grid = Controls<JsonGrid>(parametersWindow).Single();
        var apply = Controls<Button>(parametersWindow).Single(b => Equals(b.Content, "Applica e ricalcola"));
        t.Add("parametri", "attiva " + RetainingWallTrace.YesNo(parametersWindow.IsEnabled) + " · messaggio " + RetainingWallTrace.Quote(message.Text)
            + " · combinazioni " + RetainingWallTrace.Quote(string.Join("|", ((IEnumerable)combo.ItemsSource).Cast<string>())) + " · scelta " + RetainingWallTrace.Quote(combo.SelectedItem as string));
        t.Table("parametri", grid.Columns.Select(c => c.Header as string ?? "").ToArray(), grid.Rows.Select(r => r.Values).ToArray(), grid.SelectedIndex);
        t.Panel("parametri.derivati", Blocks(((ContentControl)body.Children[4]).Content));
    }

    private RetainingWallFieldState FieldState(string formKey, InputForm form, string key)
    {
        var editor = form.Editors[key];
        var elements = ((IDictionary)FormRows.GetValue(form)!)[key] as List<FrameworkElement> ?? [];
        bool symbols = ((Grid)FormTable.GetValue(form)!).ColumnDefinitions.Count == 4;
        string Column(int column) => elements.OfType<TextBlock>().FirstOrDefault(b => Grid.GetColumn(b) == column)?.Text ?? "";
        string? note = elements.OfType<Border>().SingleOrDefault() is { Visibility: Visibility.Visible, Child: TextBlock text } ? text.Text : null;
        bool active = editor.IsEnabled && editor is not TextBox { IsReadOnly: true };
        bool dimmed = editor.Opacity < 1, visible = editor.Visibility == Visibility.Visible;
        string label = AutomationProperties.GetName(editor), symbol = symbols ? Column(1) : "", unit = Column(symbols ? 3 : 2);
        return editor switch
        {
            TextBox => new(formKey, key, "campo", label, symbol, unit, form.Get(key), null, active, dimmed, visible, note),
            ComboBox choice => new(formKey, key, "scelta", label, symbol, unit, choice.SelectedItem as string ?? "", ((IEnumerable)choice.ItemsSource).Cast<string>().ToArray(), active, dimmed, visible, note),
            CheckBox box => new(formKey, key, "opzione", label, symbol, unit, RetainingWallTrace.YesNo(box.IsChecked == true), null, active, dimmed, visible, note),
            _ => throw new InvalidOperationException("Campo non previsto: " + formKey + "/" + key)
        };
    }

    /// <summary>A panel of the view as the user reads it (texts, tables, buttons and groups, in order); null without a panel.</summary>
    private static IReadOnlyList<RetainingWallBlock>? Blocks(object? content)
    {
        if (content is null) return null;
        var blocks = new List<RetainingWallBlock>();
        void Visit(object node)
        {
            switch (node)
            {
                case TextBlock text: blocks.Add(new(RetainingWallBlock.TextKind, text.Text)); return;
                case InputForm: return;
                case DataGrid grid:
                    blocks.Add(new(RetainingWallBlock.TableKind, "", grid.Columns.Select(c => c.Header as string ?? "").ToArray(),
                        ((IEnumerable)grid.ItemsSource).Cast<string[]>().ToArray()));
                    return;
                case Button button: blocks.Add(new(RetainingWallBlock.ButtonKind, button.Content as string ?? "", Enabled: button.IsEnabled)); return;
                case Expander group: blocks.Add(new(RetainingWallBlock.GroupKind, group.Header as string ?? "", Children: Blocks(group.Content) ?? [])); return;
                case Panel panel: foreach (var child in panel.Children) Visit(child); return;
                case Decorator decorator: if (decorator.Child is not null) Visit(decorator.Child); return;
                case ContentControl control: if (control.Content is not null) Visit(control.Content); return;
                default: throw new InvalidOperationException("Elemento del pannello non previsto: " + node.GetType().Name);
            }
        }
        Visit(content);
        return blocks;
    }
}
