using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Anthea.Application.Concrete;
using Anthea.Application.Scheduling;
using Anthea.Presentation.Tests;
using X.Core;

namespace X.Desktop;

/// <summary>
/// UiTests (F5.10 traces, tests/Desktop/ConcreteSectionHistoricTrace.cs): the steps of the user on the controls of the historic RC
/// section view and its state, read from its controls without changing them. Members prefixed with «Trace» to stay apart from the other
/// test partials of the view.
/// </summary>
internal sealed partial class ConcreteWorkspace
{
    private static readonly FieldInfo TraceFormRows = typeof(InputForm).GetField("rows", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(nameof(InputForm), "rows");
    private static readonly FieldInfo TraceFormNotes = typeof(InputForm).GetField("notes", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(nameof(InputForm), "notes");
    private static readonly FieldInfo TraceSchedulerActive = typeof(RecalculationScheduler).GetField("active", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(nameof(RecalculationScheduler), "active");
    private static readonly FieldInfo TraceBatches = typeof(ConcreteAnalysisSession).GetField("batches", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(nameof(ConcreteAnalysisSession), "batches");
    private static readonly FieldInfo TraceInspectionScheduler = typeof(ConcreteInspectionSession).GetField("scheduler", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(nameof(ConcreteInspectionSession), "scheduler");

    // ---------------------------------------------------------------- waiting

    /// <summary>UiTests: the gate of the batches of the analysis session (held by the trace to keep a calculation waiting).</summary>
    internal SemaphoreSlim BatchGateForChecks => (SemaphoreSlim)TraceBatches.GetValue(analysisSession)!;

    private static bool TraceActive(RecalculationScheduler scheduler) => TraceSchedulerActive.GetValue(scheduler) is not null;

    /// <summary>Whether the sheet has nothing left to do: no calculation queued or running, no scheduled quick resistance, curve, inspection or mesh.</summary>
    private bool TraceIdle() => !Busy && !calculationQueued && !curvatureRunning && !designRunning
        && !TraceActive(analysisScheduler) && !TraceActive(curvatureScheduler) && !TraceActive(quickResistanceScheduler)
        && domainPanels.All(p => !TraceActive((RecalculationScheduler)TraceInspectionScheduler.GetValue(p.Inspection.Session)!))
        && !status.Text.StartsWith("Aggiornamento della sola mesh", StringComparison.Ordinal);

    /// <summary>UiTests: runs the dispatcher until the sheet is idle for four consecutive turns (at most three minutes).</summary>
    internal async Task SettleForChecks()
    {
        var watch = Stopwatch.StartNew(); int quiet = 0;
        while (quiet < 4)
        {
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            await Task.Delay(20);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            quiet = TraceIdle() ? quiet + 1 : 0;
            if (watch.Elapsed > TimeSpan.FromMinutes(3)) throw new TimeoutException("La sezione c.a. non si ferma: " + status.Text);
        }
    }

    /// <summary>
    /// UiTests: waits until a calculation is running and none is queued: with the gate of the analysis session held, the batch of the
    /// last revision waits at the gate (a calculation superseded by an edit has ended and the new one has started).
    /// </summary>
    internal async Task EnteredForChecks()
    {
        var watch = Stopwatch.StartNew();
        while (!Busy || calculationQueued)
        {
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); await Task.Delay(10);
            if (watch.Elapsed > TimeSpan.FromMinutes(1)) throw new TimeoutException("Nessun calcolo avviato.");
        }
    }

    // ---------------------------------------------------------------- controls

    /// <summary>
    /// Whether the user can use a control: enabled, and neither it nor a container of it hidden (a field hidden by its form, the fields of
    /// the hole without the hole, the second layer of another shape). The tabs not shown and the closed expanders do not count: the user
    /// opens them.
    /// </summary>
    private static bool TraceUsable(UIElement element)
    {
        if (!element.IsEnabled) return false;
        for (DependencyObject? node = element; node is not null; node = LogicalTreeHelper.GetParent(node))
            if (node is UIElement ui && ui.Visibility != Visibility.Visible) return false;
        return true;
    }

    /// <summary>The logical tree under <paramref name="root"/> (the contents of the tabs not shown and of the closed expanders included).</summary>
    private static IEnumerable<DependencyObject> TraceTree(DependencyObject root)
    {
        var pending = new Stack<DependencyObject>(); pending.Push(root);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            yield return node;
            foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>().Reverse()) pending.Push(child);
        }
    }

    private static T TraceSingle<T>(DependencyObject root, Func<T, bool> match, string what) where T : DependencyObject
    {
        var found = TraceTree(root).OfType<T>().Where(match).ToList();
        return found.Count == 1 ? found[0] : throw new InvalidOperationException((found.Count == 0 ? "Controllo non trovato: " : "Controllo non univoco: ") + what);
    }

    private UIElement TraceTab(int index) => (UIElement)((TabItem)tabs.Items[index]).Content;

    private InputForm TraceFormWith(string key) => TraceSingle<InputForm>(this, f => f.Editors.ContainsKey(key), "form con " + key);

    private StressPanel TraceStress(string key) => stressPanels[key];

    private DomainPanel TraceDomain(string target) => domainPanels[target == "3D" || target == "ispezione-3D" ? 0 : 1];

    /// <summary>The form named in the steps.</summary>
    private InputForm TraceForm(string name, string key) => name switch
    {
        "normativa" => TraceFormWith("normativa"),
        "coefficienti" => coefficients!,
        "materiali" => materials,
        "geometria" => geometry,
        "foro" => TraceFormWith("inner_diameter_mm"),
        "armatura" => reinforcement,
        "strato-top" => additionalLayers["top"].Form,
        "strato-bottom" => additionalLayers["bottom"].Form,
        "strato-inner" => additionalLayers["inner"].Form,
        "staffe" => stirrupForms[0],
        "schema-staffe" => stirrupForms[1],
        "cavo" => TraceFormWith("sigma0"),
        "trefolo" => TraceSingle<InputForm>(this, f => f.Editors.ContainsKey("fpk") && f.Editors.ContainsKey("diagramma"), "proprietà del trefolo"),
        "rapide" => quickResistanceForm!,
        "3D" or "2D" => TraceDomain(name).Form,
        "SLE" or "SLE_FREQ" or "SLE_QP" => TraceStress(name).Options,
        "taglio" => shearForm,
        "torsione" => torsionForm,
        "dettagli" => detailingForms.Single(f => f.Editors.ContainsKey(key)),
        "ancoraggi" => anchorageForm,
        "curvatura" => curvatureForm,
        "ricerca" => designForm!,
        "limiti" => TraceFormWith("slu"),
        "mostra" => TraceFormWith("mostra"),
        _ => throw new ArgumentException("Form sconosciuto: " + name)
    };

    /// <summary>The table named in the steps.</summary>
    private DataGrid TraceTable(string name) => name switch
    {
        "barre" => barInventory,
        "trefoli" => tendons,
        "3D" or "2D" => TraceDomain(name).Grid,
        "SLE" or "SLE_FREQ" or "SLE_QP" => TraceStress(name).Grid,
        "taglio" => shearGrid!,
        "alternative" => designRanking,
        _ => throw new ArgumentException("Tabella sconosciuta: " + name)
    };

    /// <summary>Where the buttons, check boxes and choices of a panel are looked for.</summary>
    private DependencyObject TraceRoot(string target) => target switch
    {
        "3D" => TraceTab(1),
        "2D" => TraceTab(2),
        "SLE" or "SLE_FREQ" or "SLE_QP" => (DependencyObject)((TabItem)sleTabs.Items[Array.IndexOf(ConcreteEditor.Sets, target) - 2]).Content,
        "taglio" or "torsione" => TraceTab(4),
        "dettagli" or "ancoraggi" => TraceTab(5),
        "curvatura" => TraceTab(6),
        "ricerca" or "limiti" or "mostra" => TraceTab(7),
        "ispezione-3D" or "ispezione-2D" => TraceDomain(target).Inspection.Tabs,
        _ => TraceTab(0)
    };

    private static void TraceClick(ButtonBase button)
    {
        if (!TraceUsable(button)) return;
        if (button.Command is { } command) { if (command.CanExecute(button.CommandParameter)) command.Execute(button.CommandParameter); }
        else button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
    }

    // The click of the user on a check box: the box changes, then Click (ToggleButton.OnClick).
    private static void TraceToggle(CheckBox box)
    {
        if (!TraceUsable(box)) return;
        box.IsChecked = box.IsChecked != true;
        box.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, box));
    }

    private static string TraceContent(object? content) => content switch { string text => text, TextBlock block => block.Text, _ => content?.ToString() ?? "" };

    /// <summary>UiTests (F5.10 traces): a step of the user on the controls of the RC section; a control the user cannot use does nothing.
    /// Returns the message the view shows for the step, if any.</summary>
    internal string? ApplyForChecks(ConcreteSectionStep step)
    {
        switch (step.Kind)
        {
            case ConcreteSectionStep.Tab: tabs.SelectedIndex = step.Index; return null;
            case ConcreteSectionStep.Field:
            {
                var form = TraceForm(step.Target!, step.Key!);
                if (form.Editors.GetValueOrDefault(step.Key!) is TextBox text && TraceUsable(text) && !text.IsReadOnly) text.Text = step.Text;
                return null;
            }
            case ConcreteSectionStep.Choice:
            case ConcreteSectionStep.Pick when step.Target is "3D" or "2D" && step.Key == "stato":
            {
                var form = TraceForm(step.Target!, step.Key!);
                if (form.Editors.GetValueOrDefault(step.Key!) is ComboBox choice && TraceUsable(choice)) choice.SelectedItem = step.Text;
                return null;
            }
            case ConcreteSectionStep.Option:
            {
                var form = TraceForm(step.Target!, step.Key!);
                if (form.Editors.GetValueOrDefault(step.Key!) is CheckBox option && TraceUsable(option)) option.IsChecked = option.IsChecked != true;
                return null;
            }
            case ConcreteSectionStep.Toggle:
                TraceToggle(TraceSingle<CheckBox>(TraceRoot(step.Target!), b => TraceContent(b.Content) == step.Key, "casella " + step.Key));
                return null;
            case ConcreteSectionStep.Button: return TracePress(step);
            case ConcreteSectionStep.Pick: TracePick(step); return null;
            case ConcreteSectionStep.Slider:
            {
                var panel = TraceDomain(step.Target!);
                var slider = step.Key == "trasparenza" ? panel.Transparency!
                    : TraceSingle<Slider>(TraceRoot(step.Target!), s => s.ToolTip as string == (step.Key == "dimensione_ed" ? "Punti Ed" : "Punti Rd") + " · raggio grafico", "cursore " + step.Key);
                if (TraceUsable(slider)) slider.Value = double.Parse(step.Text!, CultureInfo.InvariantCulture);
                return null;
            }
            case ConcreteSectionStep.Cell: TraceCell(step); return null;
            case ConcreteSectionStep.Select:
            {
                var grid = TraceTable(step.Target!);
                if (step.Index < grid.Items.Count) grid.SelectedIndex = step.Index;
                return null;
            }
            case ConcreteSectionStep.RowCommand:
            {
                var grid = TraceTable(step.Target!);
                if (step.Index >= 0 && step.Index < grid.Items.Count) grid.SelectedIndex = step.Index;
                TraceClick(TraceNear(grid, step.Key!));
                return null;
            }
            case ConcreteSectionStep.Paste: return TracePaste(step);
            case ConcreteSectionStep.Filter: TraceFilter(step); return null;
            case ConcreteSectionStep.InnerTab: TraceInnerTab(step); return null;
            case ConcreteSectionStep.Open: return TraceOpen(step);
            default: throw new ArgumentException("Passo sconosciuto: " + step.Kind);
        }
    }

    /// <summary>The button <paramref name="content"/> next to a table: in the panel that holds the table and its command bar.</summary>
    private static Button TraceNear(DependencyObject grid, string content)
    {
        DependencyObject? node = grid;
        for (int level = 0; level < 6 && node is not null; level++)
        {
            var found = TraceTree(node).OfType<Button>().Where(b => TraceContent(b.Content) == content).ToList();
            if (found.Count == 1) return found[0];
            node = LogicalTreeHelper.GetParent(node);
        }
        throw new InvalidOperationException("Pulsante della tabella non trovato: " + content);
    }

    private string? TracePress(ConcreteSectionStep step)
    {
        if (step.Target == "strati") { TraceClick(additionalLayers[step.Key!].Toggle); return null; }
        if (step.Target is "3D" or "2D" && step.Key!.StartsWith("Forze", StringComparison.Ordinal)) { TraceClick(TraceDomain(step.Target).ForceToggle); return null; }
        var root = step.Target == "cavo" ? LogicalTreeHelper.GetParent(TraceForm("cavo", ""))! : TraceRoot(step.Target!);
        var button = TraceSingle<Button>(root, b => TraceContent(b.Content) == step.Key, "pulsante " + step.Key);
        if (step.Target == "ricerca" && step.Key == "Calcola armature")
        {
            // «Calcola armature» starts the search on the thread pool; the trace waits for its end (the button returns enabled).
            TraceClick(button);
            return null;
        }
        if (step.Target == "curvatura" && step.Key == "Calcola curva") { TraceClick(button); return null; }
        TraceClick(button);
        return null;
    }

    private void TracePick(ConcreteSectionStep step)
    {
        ComboBox? combo = (step.Target, step.Key) switch
        {
            ("3D" or "2D", "vista") => TraceSingle<ComboBox>(TraceRoot(step.Target), c => c.ItemsSource is IEnumerable<string> items && items.Contains("Isometrica"), "vista 3D"),
            ("SLE" or "SLE_FREQ" or "SLE_QP", "contorno") or ("ispezione-3D" or "ispezione-2D", "contorno") =>
                TraceSingle<ComboBox>(TraceRoot(step.Target!), c => c.ItemsSource is IEnumerable<string> items && items.SequenceEqual(ConcreteSectionViewport.Contours), "contorni"),
            ("SLE" or "SLE_FREQ" or "SLE_QP", "zone") => TraceStress(step.Target!).Regions,
            ("ispezione-3D" or "ispezione-2D", "stato") => TraceDomain(step.Target!).Inspection.State,
            ("trefoli", "standard") => ((Panel)LogicalTreeHelper.GetParent(TraceForm("trefolo", "")))!.Children.OfType<ComboBox>().Single(),
            ("materiali-utente", "elenco") => ((Panel)LogicalTreeHelper.GetParent(TraceSingle<Button>(TraceTab(0), b => TraceContent(b.Content) == "Applica materiale salvato", "applica materiale")))!
                .Children.OfType<ComboBox>().Single(),
            _ => throw new ArgumentException("Scelta sconosciuta: " + step.Target + " " + step.Key)
        };
        if (combo is null || !TraceUsable(combo)) return;
        if (step.Key == "zone") { if (int.TryParse(step.Text, out int index) && index < combo.Items.Count) combo.SelectedIndex = index; }
        else combo.SelectedItem = combo.Items.Cast<object>().FirstOrDefault(i => i.ToString() == step.Text);
    }

    /// <summary>A cell written as the binding of the cell writes it (the row of the table, in the order shown), then the refresh of the view
    /// that the table queues at the end of the edit of a row.</summary>
    private void TraceCell(ConcreteSectionStep step)
    {
        var grid = (JsonGrid)TraceTable(step.Target!);
        if (step.Row >= grid.Items.Count || grid.Items[step.Row] is not JsonRow row) return;
        var column = grid.Columns.FirstOrDefault(c => c.SortMemberPath == step.Key);
        if (grid.IsReadOnly || column is null || column.IsReadOnly || !TraceUsable(grid)) return;
        row[step.Key!] = column is DataGridCheckBoxColumn ? step.Text == "true" : step.Text;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (disposed) return;
            if (grid.ItemsSource is ListCollectionView view && !view.IsEditingItem && !view.IsAddingNew) view.Refresh();
        }));
    }

    /// <summary>«Incolla» of a table of the actions, with the text of the clipboard (the clipboard of the machine is not used): the paste of
    /// the view, or the message it shows when the text is refused.</summary>
    private string? TracePaste(ConcreteSectionStep step)
    {
        var grid = (JsonGrid)TraceTable(step.Target!);
        var paste = TraceNear(grid, "Incolla");
        if (!TraceUsable(paste)) return null;
        if (step.Index >= 0 && step.Index < grid.Items.Count) grid.SelectedIndex = step.Index;
        string family = step.Target == "taglio" ? "Taglio" : grid.Tag as string ?? step.Target!;
        try { PasteCells(grid, family, step.Text!, step.Key == "aggiungi"); return null; }
        catch (Exception ex) { return "Incolla sollecitazioni: " + ex.Message + "\nNessun dato incollato."; }
    }

    private void TraceFilter(ConcreteSectionStep step)
    {
        var grid = TraceTable(step.Target!);
        var bar = ((Panel)LogicalTreeHelper.GetParent(grid)).Children.OfType<WrapPanel>().Single();
        var combos = bar.Children.OfType<ComboBox>().ToList(); var query = bar.Children.OfType<TextBox>().Single();
        if (!TraceUsable(combos[0])) return;
        combos[0].SelectedItem = step.Key; combos[1].SelectedIndex = step.Index; combos[2].SelectedIndex = step.Row; query.Text = step.Text;
    }

    private void TraceInnerTab(ConcreteSectionStep step)
    {
        if (step.Target == "sle") { sleTabs.SelectedIndex = step.Index; return; }
        if (step.Target!.StartsWith("dettagli-", StringComparison.Ordinal)) { ((TabControl)TraceStress(step.Target[9..]).CrackTab.Parent).SelectedIndex = step.Index; return; }
        if (step.Target.StartsWith("ispezione-", StringComparison.Ordinal)) { TraceDomain(step.Target).Inspection.Tabs.SelectedIndex = step.Index; return; }
        throw new ArgumentException("Schede sconosciute: " + step.Target);
    }

    // ---------------------------------------------------------------- modal dialogs

    /// <summary>
    /// Opens a modal dialog with its button and runs the steps of the scenario inside its loop (an operation of the dispatcher): the editor of
    /// the actions (SectionActionsView on SectionActionsViewModel) or a new material. The state of the dialog after every step is the message
    /// of the step; a dialog still open at the end is cancelled, as the user closing it.
    /// </summary>
    private string? TraceOpen(ConcreteSectionStep step)
    {
        var (button, title) = step.Target switch
        {
            "sollecitazioni" => (TraceSingle<Button>(this, b => TraceContent(b.Content) == "Sollecitazioni…", "Sollecitazioni…"), "Sollecitazioni della sezione"),
            "materiale" => (TraceSingle<Button>(TraceTab(0), b => TraceContent(b.Content) == "Nuovo materiale CLS", "Nuovo materiale CLS"), "Nuovo materiale · Calcestruzzo"),
            "materiale-acciaio" => (TraceSingle<Button>(TraceTab(0), b => TraceContent(b.Content) == "Nuovo materiale acciaio", "Nuovo materiale acciaio"), "Nuovo materiale · Acciaio"),
            "materiale-trefoli" => (TraceSingle<Button>(TraceTab(0), b => TraceContent(b.Content) == "Nuovo materiale trefoli", "Nuovo materiale trefoli"), "Nuovo materiale · Trefoli"),
            _ => throw new ArgumentException("Finestra sconosciuta: " + step.Target)
        };
        if (!TraceUsable(button)) return "finestra non aperta: comando non disponibile";
        var log = new List<string>();
        bool done = false, opened = false;
        Exception? failure = null;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            if (done || System.Windows.Application.Current.Windows.OfType<Window>().SingleOrDefault(w => w.Title == title && w.IsVisible) is not { } dialog) return;
            opened = true;
            try
            {
                log.Add("aperta · " + TraceDialogState(dialog));
                foreach (var inner in step.Dialog ?? [])
                {
                    if (!dialog.IsVisible) { log.Add("chiusa: passo " + inner + " non eseguito"); continue; }
                    TraceDialogStep(dialog, inner);
                    log.Add(inner + " · " + (dialog.IsVisible ? TraceDialogState(dialog) : "chiusa"));
                }
                if (dialog.IsVisible) { dialog.Close(); log.Add("chiusa dalla traccia"); }
            }
            catch (Exception error) { failure = error; dialog.Close(); }
        }));
        try { TraceClick(button); }
        finally { done = true; }
        if (failure is not null) throw new InvalidOperationException("Finestra " + title + ": " + failure.Message, failure);
        if (!opened) throw new InvalidOperationException("Finestra " + title + " non aperta.");
        return string.Join("\n", log);
    }

    private static void TraceDialogStep(Window dialog, ConcreteSectionStep step)
    {
        switch (step.Kind)
        {
            case ConcreteSectionStep.Button:
                TraceClick(TraceSingle<Button>(dialog, b => TraceContent(b.Content) == step.Key, "pulsante " + step.Key)); break;
            case ConcreteSectionStep.Select:
                var grid = TraceSingle<DataGrid>(dialog, _ => true, "tabella");
                if (step.Index < grid.Items.Count) grid.SelectedIndex = step.Index;
                break;
            case ConcreteSectionStep.Cell when dialog.DataContext is SectionActionsViewModel vm:
                // The two-way binding of the cell writes the property of the row.
                if (step.Row < vm.Rows.Count) typeof(SectionActionRow).GetProperty(step.Key!)!.SetValue(vm.Rows[step.Row], step.Text);
                break;
            case ConcreteSectionStep.Field:
            {
                var form = TraceSingle<InputForm>(dialog, f => f.Editors.ContainsKey(step.Key!), "campo " + step.Key);
                if (form.Editors[step.Key!] is TextBox text && TraceUsable(text) && !text.IsReadOnly) text.Text = step.Text;
                break;
            }
            case ConcreteSectionStep.Choice:
            {
                var form = TraceSingle<InputForm>(dialog, f => f.Editors.ContainsKey(step.Key!), "scelta " + step.Key);
                if (form.Editors[step.Key!] is ComboBox choice && TraceUsable(choice)) choice.SelectedItem = step.Text;
                break;
            }
            default: throw new ArgumentException("Passo della finestra sconosciuto: " + step.Kind);
        }
    }

    /// <summary>What a dialog shows: the rows, selection, status and notice of the actions; the error, values and curves of a new material.</summary>
    private static string TraceDialogState(Window dialog)
    {
        if (dialog.DataContext is SectionActionsViewModel vm)
        {
            var notice = TraceTree(dialog).OfType<TextBlock>().FirstOrDefault(t => t.Text == vm.MigrationNotice);
            return "righe " + vm.Rows.Count.ToString(CultureInfo.InvariantCulture) + " · selezionata " + (vm.Selected is null ? -1 : vm.Rows.IndexOf(vm.Selected)).ToString(CultureInfo.InvariantCulture)
                + " · stato " + ConcreteSectionTrace.Quote(vm.Status) + " · origini " + ConcreteSectionTrace.YesNo(vm.HasOrigins)
                + " · avviso " + ConcreteSectionTrace.YesNo(notice?.Visibility == Visibility.Visible) + " · precedente " + ConcreteSectionTrace.YesNo(vm.Legacy)
                + " · risultato " + (vm.Result is null ? "-" : ConcreteSectionTrace.StableHash(vm.Result))
                + "\n" + string.Join("\n", vm.Rows.Select(r => "  " + string.Join(" | ", new[] { r.Id, r.Element, r.Name, r.State, r.N, r.Vx, r.Vy, r.T, r.Mx, r.My, r.Origin }
                    .Select(ConcreteSectionTrace.Quote)) + (r.Visible ? "" : " · nascosta")));
        }
        var form = TraceSingle<InputForm>(dialog, f => f.Editors.ContainsKey("nome"), "form del materiale");
        var plot = TraceTree(dialog).OfType<Plot>().Single();
        var texts = TraceTree(dialog).OfType<TextBlock>().Where(t => t.FontSize == 12 && ReferenceEquals(LogicalTreeHelper.GetParent(t), LogicalTreeHelper.GetParent(form)) || t.Text.Length > 0 && t.Foreground is not null && t.Parent is StackPanel stack && stack.Children.OfType<Button>().Any())
            .Select(t => t.Text).ToList();
        return string.Join(" · ", form.Editors.Keys.Select(k => k + "=" + ConcreteSectionTrace.Quote(form.Get(k))))
            + " · testi " + ConcreteSectionTrace.Quote(string.Join(" ⏎ ", texts))
            + " · curve " + string.Join(" ", plot.Series.Select(s => ConcreteSectionTrace.Quote(s.Name) + " " + s.Points.Count.ToString(CultureInfo.InvariantCulture) + " " + ConcreteSectionTrace.Hash(s.Points)));
    }

    // ---------------------------------------------------------------- state

    /// <summary>UiTests (F5.10 traces): the state of the sheet for the trace: the common part and the parts of <paramref name="sections"/>.</summary>
    internal ConcreteSectionTraceState TraceForChecks(IReadOnlyList<string> sections, int modifiedCount, string? message)
    {
        var state = new ConcreteSectionTraceState
        {
            Data = Data, Modified = modifiedCount, Busy = Busy, Queued = calculationQueued, HasResults = HasResults, Result = Result,
            Status = status.Text, Progress = progress.Visibility == Visibility.Visible, Tab = tabs.SelectedIndex,
            Editable = !((TextBox)geometry.Editors["width_mm"]).IsReadOnly, Message = message,
        };
        foreach (string section in sections)
        {
            var block = new ConcreteSectionBlock(section);
            switch (section)
            {
                case "ingressi": TraceInputs(block); break;
                case "azioni": TraceActions(block); break;
                case "domini": foreach (var panel in domainPanels) TraceDomainPanel(block, panel); break;
                case "sle": TraceServiceability(block); break;
                case "taglio": TraceShear(block); break;
                case "dettagli": TraceDetailing(block); break;
                case "armature": TraceDesign(block); break;
                case "ispezione": TraceInspection(block); break;
                default: throw new ArgumentException("Parte sconosciuta: " + section);
            }
            state.Blocks.Add(block);
        }
        return state;
    }

    private static ConcreteFormState TraceFormState(string name, InputForm form)
    {
        var rows = (Dictionary<string, List<FrameworkElement>>)TraceFormRows.GetValue(form)!;
        var notes = (System.Collections.IDictionary)TraceFormNotes.GetValue(form)!;
        string? Note(string key)
        {
            if (!notes.Contains(key)) return null;
            object tuple = notes[key]!;
            var text = (TextBlock)tuple.GetType().GetField("Item1")!.GetValue(tuple)!; var box = (Border)tuple.GetType().GetField("Item2")!.GetValue(tuple)!;
            return box.Visibility == Visibility.Visible ? text.Text : null;
        }
        var fields = form.Editors.Select(pair =>
        {
            var label = rows[pair.Key][0];
            string name = AutomationProperties.GetName(pair.Value);
            bool enabled = pair.Value.IsEnabled && label.IsEnabled, dimmed = label.Opacity < 1, visible = pair.Value.Visibility == Visibility.Visible;
            return pair.Value switch
            {
                TextBox box => new ConcreteFieldState(pair.Key, "campo", name, form.Get(pair.Key), null, enabled, dimmed, visible, box.IsReadOnly, Note(pair.Key)),
                ComboBox choice => new ConcreteFieldState(pair.Key, "scelta", name, form.Get(pair.Key), choice.Items.Cast<object>().Select(i => i.ToString()!).ToArray(),
                    enabled, dimmed, visible, false, Note(pair.Key)),
                CheckBox option => new ConcreteFieldState(pair.Key, "opzione", name, ConcreteSectionTrace.YesNo(option.IsChecked == true), null, enabled, dimmed, visible, false, Note(pair.Key)),
                _ => throw new InvalidOperationException("Editor non previsto: " + pair.Value.GetType().Name)
            };
        }).ToList();
        var groups = new List<(string, IReadOnlyList<string>, bool)>();
        if (form.Content is StackPanel panel)
            foreach (var expander in panel.Children.OfType<Expander>())
            {
                var keys = rows.Where(r => ReferenceEquals(r.Value[0].Parent, expander.Content)).OrderBy(r => Grid.GetRow(r.Value[0])).Select(r => r.Key).ToList();
                groups.Add((((TextBlock)expander.Header).Text, keys, expander.IsExpanded));
            }
        return new ConcreteFormState(name, fields, groups);
    }

    private ConcreteTableState TraceTableState(string name, DataGrid grid)
    {
        var columns = grid.Columns.Where(c => c.Visibility == Visibility.Visible).Select(c => TraceContent(c.Header) + "=" + c.SortMemberPath).ToList();
        var rows = grid.Items.OfType<JsonRow>().Select(r => r.Values).ToList();
        string? filter = null;
        if (grid is JsonGrid json && tableFilters.TryGetValue(json, out var f) && (f.Query.Length > 0 || f.SortKey != f.Key || f.Descending))
            filter = f.Key + " " + f.Match + " «" + f.Query + "» ordine " + (f.SortKey.Length == 0 ? "inserimento" : f.SortKey) + (f.Descending ? " decrescente" : "");
        int total = grid is JsonGrid source ? (source.ItemsSource is ListCollectionView view ? view.SourceCollection.Cast<object>().Count() : source.Rows.Count) : rows.Count;
        return new ConcreteTableState(name, columns, rows, grid.SelectedIndex, !grid.IsReadOnly && grid.IsEnabled, filter, total);
    }

    private static ConcreteCardsState TraceCards(string name, VerificationCards cards) =>
        new(name, cards.Text, cards.Children.OfType<Border>().Select(b => b.BorderBrush is SolidColorBrush s ? s.Color.ToString(CultureInfo.InvariantCulture) : "-").ToList());

    private static string TraceSection(ConcreteGeometry? section) => section is null ? "-" :
        section.Shape + " " + ConcreteSectionTrace.Number(section.Width) + "x" + ConcreteSectionTrace.Number(section.Height)
        + " · Ac " + ConcreteSectionTrace.Number(section.AreaCls) + " · As " + ConcreteSectionTrace.Number(section.AreaSteel)
        + " · contorno " + section.Outline.Count.ToString(CultureInfo.InvariantCulture) + " " + ConcreteSectionTrace.Hash(section.Outline)
        + " · fori " + section.Holes.Count.ToString(CultureInfo.InvariantCulture)
        + " · barre " + section.Bars.Count.ToString(CultureInfo.InvariantCulture) + " " + ConcreteSectionTrace.Hash(section.Bars.Select(b => new[] { b.X, b.Y, b.Diametro }).ToArray());

    private static string TraceStressState(ConcreteResults.StressState? stress) => stress is null ? "-" :
        ConcreteSectionTrace.Hash(new object?[] { stress.sigma_cls, stress.sigma_acciaio, stress.tensioni_barre, stress.BarStrains, stress.Status, stress.Ratio });

    /// <summary>A section drawn (ConcreteSectionViewport) as its properties: section, tendons, message, stress, contour and the options.</summary>
    private static string TraceViewport(ConcreteSectionViewport view) =>
        "sezione " + TraceSection(view.Section) + " · trefoli " + ConcreteSectionTrace.Hash(view.Tendons.Select(t => new object[] { t.Id, t.X, t.Y, t.Area }).ToArray())
        + " · messaggio " + ConcreteSectionTrace.Quote(view.Message) + " · tensioni " + TraceStressState(view.Stress) + " · contorno " + ConcreteSectionTrace.Quote(view.Contour)
        + " · opzioni " + string.Concat(new[] { view.Labels, view.Axes, view.ShowNeutralAxis, view.BarValues, view.TendonValues, view.ConcreteValues, view.Dimensions, view.CoverDimensions,
            view.SpacingDimensions, view.Stirrups is not null }.Select(b => b ? "1" : "0"))
        + " · barra " + ConcreteSectionTrace.Quote(view.SelectedBar) + " · zona " + ConcreteSectionTrace.Quote(view.EffectiveRegion?.Name);

    private static string TracePlot(Plot plot) =>
        ConcreteSectionTrace.Quote(plot.Title) + " · " + ConcreteSectionTrace.Quote(plot.XLabel) + " · " + ConcreteSectionTrace.Quote(plot.YLabel) + " · nota " + ConcreteSectionTrace.Quote(plot.Note)
        + " · vuoto " + ConcreteSectionTrace.Quote(plot.EmptyMessage) + " · serie " + string.Join(" ", plot.Series.Select(s => ConcreteSectionTrace.Quote(s.Name) + ":" + s.Points.Count.ToString(CultureInfo.InvariantCulture)))
        + " " + ConcreteSectionTrace.Hash(plot.Series.Select(s => s.Points).ToArray())
        + " · segmenti " + plot.Segments.Length.ToString(CultureInfo.InvariantCulture) + " " + ConcreteSectionTrace.Hash(plot.Segments.Select(s => new[] { s.A, s.B }).ToArray())
        + " · verifiche " + plot.VerificationSegments.Count.ToString(CultureInfo.InvariantCulture) + " " + ConcreteSectionTrace.Hash(plot.VerificationSegments.Select(s => new[] { s.A, s.B }).ToArray())
        + " · punti " + plot.Markers.Count.ToString(CultureInfo.InvariantCulture) + " " + ConcreteSectionTrace.Hash(plot.Markers.Select(m => new object?[] { m.X, m.Y, m.Label, m.Color.ToString(), m.Radius }).ToArray())
        + " · scala " + ConcreteSectionTrace.Number(plot.ScaleX) + "/" + ConcreteSectionTrace.Number(plot.ScaleY);

    private void TraceInputs(ConcreteSectionBlock block)
    {
        block.Text("nota-normativa", standardNote.Text);
        var migrated = TraceTree(TraceTab(0)).OfType<Expander>().FirstOrDefault(e => (e.Header as TextBlock)?.Text == "Valori integrati dal documento precedente");
        block.Text("valori-integrati", migrated is null ? null : ((TextBlock)migrated.Content).Text);
        foreach (var (name, form) in new[] { ("normativa", TraceFormWith("normativa")), ("coefficienti", coefficients!), ("materiali", materials), ("geometria", geometry) })
            block.Form(TraceFormState(name, form));
        var hole = TraceSingle<CheckBox>(TraceTab(0), b => TraceContent(b.Content) == "Foro centrale", "foro");
        var holeForm = TraceFormWith("inner_diameter_mm");
        block.Add("foro " + ConcreteSectionTrace.YesNo(hole.IsChecked == true) + (hole.IsEnabled ? "" : " · spento") + (holeForm.Visibility == Visibility.Visible ? "" : " · campi nascosti"));
        block.Form(TraceFormState("foro", holeForm));
        block.Add("armatura " + (reinforcement.IsEnabled ? "attiva" : "spenta") + " · nota " + ConcreteSectionTrace.Quote(reinforcement.ToolTip as string));
        block.Form(TraceFormState("armatura", reinforcement));
        foreach (var (key, controls) in additionalLayers)
        {
            block.Add("strato " + key + (controls.Body.Visibility == Visibility.Visible ? "" : " · nascosto") + " · " + ConcreteSectionTrace.Quote(TraceContent(controls.Toggle.Content))
                + (controls.Toggle.IsEnabled ? "" : " · spento") + " · nota " + ConcreteSectionTrace.Quote(controls.Toggle.ToolTip as string) + (controls.Form.Visibility == Visibility.Visible ? "" : " · campi nascosti"));
            block.Form(TraceFormState("strato-" + key, controls.Form));
        }
        block.Form(TraceFormState("staffe", stirrupForms[0])); block.Form(TraceFormState("schema-staffe", stirrupForms[1]));
        var cable = TraceFormWith("sigma0"); var cablePanel = (Panel)LogicalTreeHelper.GetParent(cable)!;
        var cableChoice = cablePanel.Children.OfType<ComboBox>().Single();
        block.Form(TraceFormState("cavo", cable));
        block.Add("cavo materiale " + ConcreteSectionTrace.Quote(cableChoice.SelectedItem as string) + " di " + ConcreteSectionTrace.Quote(string.Join("|", cableChoice.Items.Cast<object>()))
            + " · messaggio " + ConcreteSectionTrace.Quote(cablePanel.Children.OfType<TextBlock>().Last().Text));
        foreach (string line in TraceTableState("trefoli", tendons).Lines()) block.Add(line);
        var materialColumn = (DataGridComboBoxColumn)tendons.Columns.First(c => c.Header?.ToString() == "Materiale");
        block.Add("trefoli materiali " + ConcreteSectionTrace.Quote(string.Join("|", (materialColumn.ItemsSource as IEnumerable<string>) ?? [])));
        var tendonForm = TraceForm("trefolo", ""); var standard = ((Panel)LogicalTreeHelper.GetParent(tendonForm)!).Children.OfType<ComboBox>().Single();
        block.Add("trefolo standard " + ConcreteSectionTrace.Quote(standard.SelectedItem as string) + " di " + ConcreteSectionTrace.Quote(string.Join("|", standard.Items.Cast<object>())));
        block.Form(TraceFormState("trefolo", tendonForm));
        var saved = ((Panel)LogicalTreeHelper.GetParent(TraceSingle<Button>(TraceTab(0), b => TraceContent(b.Content) == "Applica materiale salvato", "applica materiale"))!).Children.OfType<ComboBox>().Single();
        block.Add("materiali salvati " + saved.SelectedIndex.ToString(CultureInfo.InvariantCulture) + " di " + ConcreteSectionTrace.Quote(string.Join("|", saved.Items.Cast<object>())));
        block.Form(TraceFormState("rapide", quickResistanceForm!));
        block.Add("rapide " + ConcreteSectionTrace.Quote(quickResistanceText.Text) + (quickResistanceText.Visibility == Visibility.Visible ? "" : " · nascosto") + " · riquadri "
            + ConcreteSectionTrace.Quote(string.Join(" | ", quickResistanceResults.Children.OfType<Border>().Select(b => string.Join(" ", TraceTree(b).OfType<TextBlock>().Select(t => t.Text))))));
        block.Add("anteprima " + TraceViewport(preview));
        foreach (string line in TraceTableState("barre", barInventory).Lines()) block.Add(line);
        foreach (var (key, cards) in summaries) block.Add(TraceCards("riepilogo-" + key, cards).Line());
        block.Add(TraceCards("riepilogo-taglio", shearDashboard).Line());
    }

    private void TraceActions(ConcreteSectionBlock block)
    {
        block.Add("sollecitazioni uniche " + ConcreteSectionTrace.YesNo(UnifiedActions) + " · file " + ConcreteSectionTrace.Quote(string.Join("|", excelPaths.Select(t => t.Text).Distinct())));
        foreach (var (key, rows) in actions)
        {
            block.Add("azioni " + key + " · " + rows.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var row in rows) block.Add("  " + ConcreteSectionTrace.Canonical(row.Values));
        }
        if (shearGrid is not null)
        {
            block.Add("azioni taglio · " + shearGrid.Rows.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var row in shearGrid.Rows) block.Add("  " + ConcreteSectionTrace.Canonical(row.Values));
        }
    }

    private void TraceDomainPanel(ConcreteSectionBlock block, DomainPanel panel)
    {
        string name = panel.ThreeD ? "3D" : "2D";
        block.Form(TraceFormState(name, panel.Form));
        foreach (string line in TraceTableState(name, panel.Grid).Lines()) block.Add(line);
        block.Text(name + " dettaglio", panel.Detail.Text);
        block.Add(TraceCards(name + "-riepilogo", panel.Summary).Line());
        var toolbar = TraceTree(TraceRoot(name)).OfType<CheckBox>().Where(c => c.Parent is WrapPanel).Select(c => TraceContent(c.Content) + "=" + ConcreteSectionTrace.YesNo(c.IsChecked == true)
            + (c.IsEnabled ? "" : "(spento)")).ToList();
        block.Add(name + " comandi " + ConcreteSectionTrace.Quote(TraceContent(panel.ForceToggle.Content)) + (panel.ForceToggle.IsEnabled ? "" : " · spento") + " · " + string.Join(" ", toolbar)
            + " · cursori " + string.Join(" ", TraceTree(TraceRoot(name)).OfType<Slider>().Select(s => ConcreteSectionTrace.Number(s.Value) + (s.IsEnabled ? "" : "(spento)"))));
        if (panel.ThreeD) foreach (string line in panel.View3D!.TraceDataForChecks()) block.Add("3D vista " + line);
        else block.Add("2D grafico " + TracePlot(panel.Plot));
    }

    private void TraceServiceability(ConcreteSectionBlock block)
    {
        block.Add("famiglia " + sleTabs.SelectedIndex.ToString(CultureInfo.InvariantCulture));
        foreach (var (key, panel) in stressPanels)
        {
            block.Form(TraceFormState(key, panel.Options));
            foreach (string line in TraceTableState(key, panel.Grid).Lines()) block.Add(line);
            var details = (TabControl)panel.CrackTab.Parent;
            block.Add(key + " schede " + details.SelectedIndex.ToString(CultureInfo.InvariantCulture) + " · fessurazione " + (panel.CrackTab.Visibility == Visibility.Visible ? "mostrata" : "nascosta"));
            block.Text(key + " dettaglio", panel.Detail.Text);
            block.Text(key + " passaggi", panel.CrackDetail.Text);
            foreach (string line in TraceTableState(key + "-barre", panel.Bars).Lines()) block.Add(line);
            foreach (string line in TraceTableState(key + "-cls", panel.Concrete).Lines()) block.Add(line);
            block.Add(key + " zone " + panel.Regions.SelectedIndex.ToString(CultureInfo.InvariantCulture) + " di " + panel.Regions.Items.Count.ToString(CultureInfo.InvariantCulture));
            block.Add(key + " mappa " + TraceViewport(panel.View) + " · legenda " + ConcreteSectionTrace.Quote(panel.View.ContourLegend));
            block.Add(TraceCards(key + "-riepilogo", panel.Summary).Line());
        }
    }

    private void TraceShear(ConcreteSectionBlock block)
    {
        block.Form(TraceFormState("taglio", shearForm)); block.Form(TraceFormState("torsione", torsionForm));
        for (int i = 2; i < stirrupForms.Count; i++) block.Form(TraceFormState("staffe-" + i.ToString(CultureInfo.InvariantCulture), stirrupForms[i]));
        block.Text("nota-automatici", shearAutomaticNote.Text);
        if (shearGrid is not null)
        {
            foreach (string line in TraceTableState("taglio", shearGrid).Lines()) block.Add(line);
            var buttons = TraceTree(LogicalTreeHelper.GetParent(LogicalTreeHelper.GetParent(shearGrid)!)!).OfType<Button>().Select(b => TraceContent(b.Content) + (b.IsEnabled ? "" : "(spento)"));
            block.Add("taglio comandi " + string.Join(" ", buttons));
        }
        block.Text("taglio dettaglio", shearDetail.Text);
        block.Text("taglio sintesi", shearSummary.Text);
        block.Add(TraceCards("taglio-peggiori", shearWorst).Line());
        block.Add("taglio sezione " + TraceViewport(shearView));
    }

    private void TraceDetailing(ConcreteSectionBlock block)
    {
        for (int i = 0; i < detailingForms.Count; i++) block.Form(TraceFormState("dettagli-" + i.ToString(CultureInfo.InvariantCulture), detailingForms[i]));
        block.Form(TraceFormState("copriferro", coverDetailingGeometry));
        block.Form(TraceFormState("ancoraggi", anchorageForm));
        block.Add(TraceCards("dettagli", detailingText).Line());
        foreach (var (key, cards) in detailingTopics) block.Add(TraceCards("dettagli-" + key, cards).Line());
        block.Add(TraceCards("ancoraggi", anchorageText).Line());
        block.Text("durabilita", durabilityNote.Text);
    }

    private void TraceDesign(ConcreteSectionBlock block)
    {
        block.Form(TraceFormState("ricerca", designForm!)); block.Form(TraceFormState("limiti", TraceFormWith("slu"))); block.Form(TraceFormState("mostra", TraceFormWith("mostra")));
        block.Add("ricerca comandi " + string.Join(" ", new[] { designRun, designStop, designApply }.Select(b => TraceContent(b.Content) + (b.IsEnabled ? "" : "(spento)"))));
        block.Text("ricerca stato", ConcreteSectionTrace.WithoutTimes(designStatus.Text));
        block.Text("ricerca proposta", designDescription.Text);
        block.Text("ricerca ambito", designScope.Text);
        block.Add(TraceCards("ricerca-metriche", designMetrics).Line());
        block.Add(TraceCards("ricerca-verifiche", designChecks).Line());
        block.Text("ricerca scarti", ConcreteSectionTrace.WithoutTimes(designDiagnostics?.Text ?? ""));
        foreach (string line in TraceTableState("alternative", designRanking).Lines()) block.Add(line);
        block.Add("ricerca anteprima " + TraceViewport(designPreview));
    }

    private void TraceInspection(ConcreteSectionBlock block)
    {
        foreach (var panel in domainPanels)
        {
            var s = panel.Inspection; string name = panel.ThreeD ? "3D" : "2D";
            block.Add(name + " ispezione scheda " + s.Tabs.SelectedIndex.ToString(CultureInfo.InvariantCulture) + " · " + ConcreteSectionTrace.Quote(s.State.SelectedItem as string)
                + " · messaggio " + ConcreteSectionTrace.Quote(s.Message.Text));
            block.Text(name + " ispezione valori", s.Values.Text);
            block.Add(name + " ispezione mappa " + TraceViewport(s.View));
            block.Add(name + " ispezione piano " + (s.Plane.Diagram is null ? "-" : ConcreteSectionTrace.Hash(new object[] { s.Plane.Diagram.StrainReferencePoint, s.Plane.Diagram.ChiX, s.Plane.Diagram.ChiY,
                s.Plane.Diagram.Outline.Select(v => new[] { v.X, v.Y, v.Strain }).ToArray() })));
        }
        block.Form(TraceFormState("curvatura", curvatureForm));
        block.Text("curvatura testo", curvatureText.Text);
        block.Add("curvatura grafico " + TracePlot(curvaturePlot));
    }

    // ---------------------------------------------------------------- drawings

    /// <summary>UiTests (F5.10 drawings): the display lists of the 2D views of this sheet and the plain data of its 3D domain.</summary>
    internal async Task DrawForChecks(string id, string output, List<string> list, List<string> three, bool allContours, Action layout)
    {
        async Task Show(int tab) { tabs.SelectedIndex = tab; layout(); await SettleForChecks(); layout(); }
        await Show(0);
        list.Add(ConcreteSectionHistoricTrace.Record(output, id + " sezione 900x700", preview, 900, 700));
        foreach (var (key, panel) in stressPanels)
        {
            await Show(3); sleTabs.SelectedIndex = Array.IndexOf(ConcreteEditor.Sets, key) - 2; layout(); await SettleForChecks(); layout();
            if (panel.View.Stress is null) continue;
            list.Add(ConcreteSectionHistoricTrace.Record(output, id + " " + key + " 900x700", panel.View, 900, 700));
            if (!allContours || key != "SLE") continue;
            string contour = panel.View.Contour;
            foreach (string other in ConcreteSectionViewport.Contours)
            {
                panel.View.Contour = other;
                list.Add(ConcreteSectionHistoricTrace.Record(output, id + " " + key + " " + other + " 900x700", panel.View, 900, 700));
            }
            panel.View.Contour = contour;
        }
        await Show(4);
        list.Add(ConcreteSectionHistoricTrace.Record(output, id + " taglio 700x600", shearView, 700, 600));
        await Show(1);
        var s = domainPanels[0].Inspection; s.Tabs.SelectedIndex = 1; layout();
        foreach (int state in new[] { 1, 0 })
        {
            s.State.SelectedIndex = state; layout(); await SettleForChecks(); layout();
            string label = state == 1 ? "limite" : "azione";
            list.Add(ConcreteSectionHistoricTrace.Record(output, id + " ispezione 3D " + label + " 800x650", s.View, 800, 650));
            list.Add(ConcreteSectionHistoricTrace.Record(output, id + " piano 3D " + label + " 800x650", s.Plane, 800, 650));
        }
        await Show(2);
        list.Add(ConcreteSectionHistoricTrace.Record(output, id + " dominio 2D 900x700", domainPanels[1].Plot, 900, 700));
        await Show(6);
        await CalculateCurvature(); await SettleForChecks();
        list.Add(ConcreteSectionHistoricTrace.Record(output, id + " momento-curvatura 900x650", curvaturePlot, 900, 650));
        foreach (string line in domainPanels[0].View3D!.TraceDataForChecks()) three.Add(id + " · " + line);
    }
}

internal sealed partial class DomainViewport3D
{
    /// <summary>UiTests (F5.10): the 3D domain as plain data: mesh, points of the actions, selection, resistances and options (not the camera).</summary>
    internal IEnumerable<string> TraceDataForChecks()
    {
        yield return "mesh " + (mesh is null ? "-" : mesh.Mode + " vertici " + mesh.Vertices.Count.ToString(CultureInfo.InvariantCulture) + " " + ConcreteSectionTrace.Hash(mesh.Vertices.Select(v => new[] { v.N, v.Mx, v.My }).ToArray())
            + " triangoli " + TriangleCount.ToString(CultureInfo.InvariantCulture) + " " + ConcreteSectionTrace.Hash(mesh.Triangles) + " scala " + ConcreteSectionTrace.Hash(new[] { mesh.Scale.N, mesh.Scale.Mx, mesh.Scale.My }));
        // The identifiers of the rows are created by the sheet: the points are written in order, the selection as the index of its point.
        yield return "azioni " + actionPoints.Count.ToString(CultureInfo.InvariantCulture) + " " + ConcreteSectionTrace.Hash(actionPoints.Select(p => new object[] { p.Force.N, p.Force.Mx, p.Force.My, p.Pass }).ToArray())
            + " · selezionata " + (selectedId is null ? "-" : actionPoints.FindIndex(p => p.Id == selectedId).ToString(CultureInfo.InvariantCulture))
            + " · Ed " + (SelectedAction is { } a ? ConcreteSectionTrace.Hash(new[] { a.N, a.Mx, a.My }) : "-") + " · Rd " + (SelectedResistance is { } r ? ConcreteSectionTrace.Hash(new[] { r.N, r.Mx, r.My }) : "-");
        yield return "opzioni " + string.Concat(new[] { ShowActions, OnlySelectedActions, ShowResistance, ShowVerificationLines, ColorByRatio, Wireframe, FitIncludesActions }.Select(b => b ? "1" : "0"))
            + " · criterio " + ConcreteSectionTrace.Quote(VerificationCriterion) + " · punti " + ConcreteSectionTrace.Number(ActionPointSize) + "/" + ConcreteSectionTrace.Number(ResistancePointSize)
            + " · opacità " + ConcreteSectionTrace.Number(SurfaceOpacity) + " · scale " + ConcreteSectionTrace.Number(scaleMx) + "/" + ConcreteSectionTrace.Number(scaleN) + "/" + ConcreteSectionTrace.Number(scaleMy)
            + " · tassi " + (Ratios is null ? "-" : Ratios.Count.ToString(CultureInfo.InvariantCulture)) + " · resistenze " + (Resistances is null ? "-" : Resistances.Count.ToString(CultureInfo.InvariantCulture))
            + " · nota " + ConcreteSectionTrace.Quote(note.Text);
    }
}
