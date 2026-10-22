using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules.ConcreteSection;
using Anthea.Presentation.Tests;
using X.Core;
using X.Desktop.Services;
using X.Desktop.Views.ConcreteSection;

namespace X.Desktop;

/// <summary>
/// The parts «nomi» and «finestre» of --check-sezione-view (F5.10, section 22 of docs/refactoring/f5-10-sezione.md): the automation names of the view
/// against those of the historic view, and the two windows that the frozen traces do not open («Scala…» of the domains, «Proprietà / report…»), driven
/// through their controls beside the historic windows on the same data (the historic windows kept off screen by the hooks of the historic trace).
/// </summary>
internal static partial class ConcreteSectionViewChecks
{
    // ---------------------------------------------------------------- automation names

    /// <summary>The controls the user reaches: not a part of the template of another control, not inside a table (its cells).</summary>
    private static bool Reachable(FrameworkElement element)
    {
        if (!element.IsVisible || element is not (ButtonBase or ComboBox or TextBox or Slider or TabItem or DataGrid or Expander or ProgressBar)) return false;
        if (element.TemplatedParent is not (null or ContentPresenter)) return false;
        for (DependencyObject? node = VisualTreeHelper.GetParent(element); node is not null; node = VisualTreeHelper.GetParent(node))
            if (node is DataGrid or ComboBox) return false;
        return true;
    }

    /// <summary>
    /// In the eight tabs of the two views (every group opened, three passes), on four fixtures: every name the historic view gives to a control
    /// (AutomationProperties.Name: the editors of its forms, the «Wiki: …» labels, the notices) is a name of the XAML view, and every control of the
    /// XAML view that the user reaches (buttons, check boxes, choices, text boxes, sliders, tabs, tables, groups, the progress) has a name.
    /// </summary>
    private static async Task AutomationNames(string root)
    {
        static HashSet<string> Explicit(DependencyObject root) => CompositeSectionViewChecks.Walk(root).OfType<UIElement>().Where(e => e.IsVisible)
            .Select(e => AutomationProperties.GetName(e)).Where(n => !string.IsNullOrEmpty(n)).ToHashSet(StringComparer.Ordinal);
        var editors = Editors(new Services());
        foreach (string id in new[] { "default", "circolare-cava", "sle-fessurazione-assi", "soletta-dettagli-ricerca" })
        {
            var data = ConcreteSectionScenarios.FixtureData(root, id);
            using var historic = new Hosted(new SheetEditor(Module, data));
            using var host = new Hosted(editors.Open(Module, data));
            await historic.Settle(); await host.Settle();
            var before = new HashSet<string>(StringComparer.Ordinal); var after = new HashSet<string>(StringComparer.Ordinal);
            var unnamed = new SortedSet<string>(StringComparer.Ordinal);
            int controls = 0;
            foreach (var hosted in new[] { historic, host })
            {
                var tabs = CompositeSectionViewChecks.Walk(hosted.Root).OfType<TabControl>().First();
                for (int tab = 0; tab < tabs.Items.Count; tab++)
                {
                    tabs.SelectedIndex = tab; await hosted.Settle();
                    for (int pass = 0; pass < 3; pass++) { foreach (var e in CompositeSectionViewChecks.Walk(hosted.Root).OfType<Expander>().ToArray()) e.IsExpanded = true; await hosted.Settle(); }
                    if (ReferenceEquals(hosted, historic)) { before.UnionWith(Explicit(hosted.Root)); continue; }
                    foreach (var element in CompositeSectionViewChecks.Walk(hosted.Root, popups: false).OfType<FrameworkElement>().Where(Reachable))
                    {
                        controls++;
                        string name = AutomationName(element);
                        if (name.Length > 0) after.Add(name);
                        else unnamed.Add((tab + 1).ToString("D2", CultureInfo.InvariantCulture) + " " + element.GetType().Name + (element is ContentControl { Content: string text } ? " «" + text + "»" : ""));
                    }
                    after.UnionWith(Explicit(hosted.Root));
                }
            }
            var missing = before.Except(after).Order(StringComparer.Ordinal).ToArray();
            Log.Add($"   nomi ({id}): {controls} controlli della vista, nomi storici {before.Count}, nomi nuovi: " + string.Join(", ", after.Except(before).Order(StringComparer.Ordinal).Take(60)));
            Check(missing.Length == 0, $"{id}: nomi di automazione della vista storica presenti nella vista XAML ({before.Count})" + (missing.Length == 0 ? "" : "; mancanti: " + string.Join(", ", missing)));
            Check(unnamed.Count == 0 && controls > 100, $"{id}: ogni controllo raggiungibile delle otto schede ha un nome di automazione ({controls} letture)"
                + (unnamed.Count == 0 ? "" : "; senza nome: " + string.Join(", ", unnamed)));
        }
    }

    // ---------------------------------------------------------------- windows outside the traces

    /// <summary>
    /// «Scala…» of the 3D and 2D domains and «Proprietà / report…», on two fixtures, in the XAML view (the dialog shown by the service of the dialogs
    /// from its DataTemplate, the window of the properties by the view) and in the historic view (its windows built in code, kept off screen and never
    /// activated by the hooks of the historic trace, the modal one driven inside its loop), each through its controls: a value refused (the same
    /// message), then values accepted (the same data, the same Modified, the same zoom of the drawing); the properties for φ and then for n (the same
    /// text, data and Modified).
    /// </summary>
    private static async Task Windows(string root)
    {
        using var offscreen = new ConcreteSectionHistoricTrace.OffscreenWindows();
        foreach (string id in new[] { "default", "circolare-cava" })
        {
            var data = ConcreteSectionScenarios.FixtureData(root, id);
            var services = new TraceServices();
            var editors = new SheetEditorFactory(ModuleHostBench.Implementations(BenchArguments), DesktopComposition.CreateModules(), services.Create(),
                new CalculationLimiter(CalculationLimiter.DefaultMaximum));
            using var host = new Hosted(editors.Open(Module, data));
            using var historic = new Hosted(new SheetEditor(Module, data));
            await host.Settle(); await historic.Settle();
            var historicTabs = CompositeSectionViewChecks.Walk(historic.Root).OfType<TabControl>().First();
            var view = host.View;
            // The data of each view after a commit, the identifiers created by each numbered in their own order (as the exports).
            var guids = new Dictionary<Hosted, ConcreteGuids> { [host] = new(data), [historic] = new(data) };
            string Data(Hosted hosted) { hosted.Editor.Commit(); return guids[hosted].Normalize(hosted.Editor.Data.ToJsonString()); }
            Check(Data(host) == Data(historic), $"{id} · finestre: dati delle due viste uguali all'apertura");
            foreach (int tab in new[] { 1, 2 })
            {
                bool threeD = tab == 1;
                string name = "Scala del dominio " + (threeD ? "3D" : "2D");
                view.Tabs.Items.OfType<TabItem>().Single(t => AutomationName(t) == ConcreteSheetViewModel.TabTitles[tab]).IsSelected = true;
                historicTabs.SelectedIndex = tab;
                await host.Settle(); await historic.Settle();
                var panelView = threeD ? view.Domain3DView : view.Domain2DView;
                var historicPanel = (FrameworkElement)((TabItem)historicTabs.Items[tab]).Content;
                string first = threeD ? "scala_n" : "scala_x", second = threeD ? "scala_mx" : "scala_y";
                (string Key, string Value)[] refused = [(first, "abc")];
                (string Key, string Value)[] accepted = [(first, "2"), (second, "0.5"), ("zoom", "150"), ("fit_azioni", "Sì")];
                foreach (var (values, label) in new[] { (refused, "valore rifiutato"), (accepted, "valori accettati") })
                {
                    int modified = host.Modified, historicModified = historic.Modified;
                    string viewMessage = ScaleInView(panelView, services, values);
                    string historicMessage = ScaleInHistoric(historicPanel, name, values);
                    await host.Settle(); await historic.Settle();
                    Check(viewMessage == historicMessage, $"{id} · «Scala…» {(threeD ? "3D" : "2D")}, {label}: stessa finestra all'uscita («{viewMessage}» / «{historicMessage}»)");
                    Check(Data(host) == Data(historic) && host.Modified - modified == historic.Modified - historicModified,
                        $"{id} · «Scala…» {(threeD ? "3D" : "2D")}, {label}: stessi dati e stessi Modified della vista storica ({host.Modified - modified})");
                    string drawing = threeD ? Viewport(panelView.Drawing) : Chart(panelView.Drawing);
                    string historicDrawing = threeD ? Viewport(CompositeSectionViewChecks.Walk(historicPanel).OfType<DomainViewport3D>().Single())
                        : Chart(CompositeSectionViewChecks.Walk(historicPanel).OfType<Plot>().First());
                    Check(drawing == historicDrawing, $"{id} · «Scala…» {(threeD ? "3D" : "2D")}, {label}: disegno con lo stesso zoom e gli stessi fattori ({drawing})");
                }
            }
            await Properties(id, host, historic, historicTabs, Data);
        }

        static string Viewport(FrameworkElement? drawing) => drawing is DomainViewport3D v
            ? "zoom " + v.Zoom.ToString("R", CultureInfo.InvariantCulture) + " · azioni nell'adattamento " + v.FitIncludesActions : "-";
        static string Chart(FrameworkElement? drawing) => drawing is Plot p
            ? "zoom " + p.Zoom.ToString("R", CultureInfo.InvariantCulture) + " · fattori " + p.ScaleX.ToString("R", CultureInfo.InvariantCulture) + "/" + p.ScaleY.ToString("R", CultureInfo.InvariantCulture)
              + " · punti nell'adattamento " + p.FitIncludesMarkers : "-";
    }

    /// <summary>The message under the form of a window of the scale (the text block between the form and «Applica»).</summary>
    private static string ScaleMessage(DependencyObject window)
    {
        var apply = CompositeSectionViewChecks.Walk(window, popups: false).OfType<Button>().Single(b => AutomationName(b) == "Applica");
        var stack = (Panel)VisualTreeHelper.GetParent(apply);
        return stack.Children.OfType<TextBlock>().Single().Text;
    }

    /// <summary>«Scala…» of the XAML view: the values typed and chosen in the fields found by their automation names, «Applica»; the message when the
    /// window stays open (a value refused), otherwise «chiusa».</summary>
    private static string ScaleInView(ConcreteDomainPanelView panel, TraceServices services, (string Key, string Value)[] values)
    {
        string message = "";
        Exception? failure = null;
        services.Script = (dialog, window) =>
        {
            try
            {
                var scale = (ConcreteDomainScaleDialogViewModel)dialog;
                foreach (var (key, value) in values)
                {
                    var field = scale.Form[key];
                    var editor = CompositeSectionViewChecks.Walk(window, popups: false).OfType<Control>()
                        .Single(e => ReferenceEquals(e.DataContext, field) && e.TemplatedParent is not ComboBox && AutomationName(e) == field.Label);
                    if (editor is TextBox box) box.Text = value;
                    else ((ComboBox)editor).SelectedItem = value;
                }
                Click(CompositeSectionViewChecks.Walk(window, popups: false).OfType<Button>().Single(b => AutomationName(b) == "Applica"));
                window.UpdateLayout();
                message = window.IsVisible ? ScaleMessage(window) : "chiusa";
            }
            catch (Exception error) { failure = error; }
        };
        try { Click(CompositeSectionViewChecks.Walk(panel, popups: false).OfType<Button>().Single(b => AutomationName(b) == "Scala…")); }
        finally { services.Script = null; }
        if (failure is not null) throw new InvalidOperationException("Finestra «Scala…» della vista: " + failure.Message, failure);
        return message;
    }

    /// <summary>«Scala…» of the historic view, driven inside the loop of its modal window: the editors of its form by key, «Applica»; the message
    /// when the window stays open, then closed as the user closes it.</summary>
    private static string ScaleInHistoric(FrameworkElement panel, string title, (string Key, string Value)[] values)
    {
        string? message = null;
        Exception? failure = null;
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            var window = System.Windows.Application.Current.Windows.OfType<Window>().SingleOrDefault(w => w.Title == title && w.IsVisible);
            if (window is null) return;
            try
            {
                var form = CompositeSectionViewChecks.Walk(window, popups: false).OfType<InputForm>().Single();
                foreach (var (key, value) in values)
                {
                    if (form.Editors[key] is TextBox box) box.Text = value;
                    else ((ComboBox)form.Editors[key]).SelectedItem = value;
                }
                var apply = CompositeSectionViewChecks.Walk(window, popups: false).OfType<Button>().Single(b => b.Content as string == "Applica");
                apply.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, apply));
                message = window.IsVisible ? ScaleMessage(window) : "chiusa";
            }
            catch (Exception error) { failure = error; }
            finally { if (window.IsVisible) window.Close(); }
        }));
        var button = CompositeSectionViewChecks.Walk(panel, popups: false).OfType<Button>().Single(b => b.Content as string == "Scala…");
        button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
        if (failure is not null) throw new InvalidOperationException("Finestra «Scala…» storica: " + failure.Message, failure);
        return message ?? throw new InvalidOperationException("Finestra storica «" + title + "» non aperta.");
    }

    /// <summary>
    /// «Proprietà / report…» of the two views on the same data: the window opened with its button, the properties for φ, then «Omogeneizzazione» «Da n»
    /// chosen in the window: the same text, the same data, the same Modified; then the windows closed.
    /// </summary>
    private static async Task Properties(string id, Hosted host, Hosted historic, TabControl historicTabs, Func<Hosted, string> data)
    {
        const string Title = "Proprietà / report della sezione", Waiting = "Calcolo proprietà della sezione…";
        host.View.Tabs.Items.OfType<TabItem>().Single(t => AutomationName(t) == ConcreteSheetViewModel.TabTitles[0]).IsSelected = true;
        historicTabs.SelectedIndex = 0;
        await host.Settle(); await historic.Settle();
        int modified = host.Modified, historicModified = historic.Modified;
        Click(CompositeSectionViewChecks.Walk(host.View.PreviewToolbar, popups: false).OfType<Button>().Single(b => AutomationName(b) == "Proprietà / report…"));
        var historicButton = CompositeSectionViewChecks.Walk((DependencyObject)((TabItem)historicTabs.Items[0]).Content, popups: false).OfType<Button>()
            .Single(b => b.Content as string == "Proprietà / report…");
        historicButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, historicButton));
        var window = host.Dialogs.Single(w => w.Title == Title);
        var historicWindow = System.Windows.Application.Current.Windows.OfType<Window>().Single(w => w.Title == Title && !host.Dialogs.Contains(w));
        try
        {
            TextBox Text(Window w) => CompositeSectionViewChecks.Walk(w, popups: false).OfType<TextBox>().Single(t => t.IsReadOnly && t.AcceptsReturn);
            async Task Ready()
            {
                for (int i = 0; i < 3000 && (Text(window).Text == Waiting || Text(historicWindow).Text == Waiting); i++)
                { await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); await Task.Delay(10); }
                await host.Settle(); await historic.Settle();
            }
            await Ready();
            Check(Text(window).Text == Text(historicWindow).Text && Text(window).Text != Waiting && AutomationName(Text(window)) == "Proprietà della sezione",
                $"{id} · «Proprietà / report…»: stesso testo della finestra storica per φ ({Text(window).Text.Length} caratteri)");
            Check(data(host) == data(historic) && host.Modified - modified == historic.Modified - historicModified,
                $"{id} · «Proprietà / report…» per φ: stessi dati e stessi Modified ({host.Modified - modified})");
            // «Da n»: the choice of the window found by its automation name, the choice of the historic form by its key.
            var method = CompositeSectionViewChecks.Walk(window, popups: false).OfType<ComboBox>().Single(c => AutomationName(c) == "Omogeneizzazione");
            method.SelectedItem = "Da n";
            var historicForm = CompositeSectionViewChecks.Walk(historicWindow, popups: false).OfType<InputForm>().Single();
            ((ComboBox)historicForm.Editors["metodo"]).SelectedItem = "Da n";
            await Ready();
            Check(Text(window).Text == Text(historicWindow).Text && Text(window).Text != Waiting, $"{id} · «Proprietà / report…» per n: stesso testo della finestra storica");
            Check(data(host) == data(historic) && host.Modified - modified == historic.Modified - historicModified,
                $"{id} · «Proprietà / report…» per n: stessi dati e stessi Modified ({host.Modified - modified})");
        }
        finally { host.Dialogs.Remove(window); window.Close(); historicWindow.Close(); }
        await host.Settle(); await historic.Settle();
    }
}
