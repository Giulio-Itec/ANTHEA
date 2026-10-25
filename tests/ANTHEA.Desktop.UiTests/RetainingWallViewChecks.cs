using System.Collections;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Anthea.Application.Presentation;
using Anthea.Application.Walls;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.RetainingWall;
using Anthea.Presentation.Modules.Shared;
using Anthea.Presentation.Services;
using Anthea.Presentation.Tests;
using Anthea.Testing.Normalization;
using X.Core;
using X.Desktop.Services;
using X.Desktop.Views.RetainingWall;

namespace X.Desktop;

/// <summary>
/// --check-muri-view &lt;radice&gt; &lt;cartella&gt; (UiTests, F5.9, proposal F5 section 7.3): the XAML view of the retaining walls
/// (RetainingWallSheetView) hosted by <see cref="MvvmSheetEditor"/>, as the Release opens it since the switch of 23/10/2026 (f5-pass-muri; the
/// historic view RetainingWallWorkspace is archived in supporto/SUPERATI/ui-storica/muri), in windows outside the screen (never activated, no
/// mouse). Parts:
/// <list type="number">
/// <item>Traces through the view: the 24 scenarios of tests/Shared/RetainingWallTrace.cs driven through the controls of the view (fields,
/// choices and check boxes of the FieldForms, cells and rows of the tables, buttons, combo boxes, switches, the rows of the panels of results,
/// the dialog of the values shown off screen and modeless as the historic trace shows its window) with the held engine of the traces, and read
/// back from the controls (the drawings from the RetainingWallDrawing and GlobalStabilityDrawing of the view), equal line by line to the traces
/// of the historic view frozen in tests/fixtures/f5-muri/storica.</item>
/// <item>For each of the 16 F3 fixtures: the visit (both pages, every expander, three appearances) writes nothing and raises no Modified;
/// after the calculation data and result are those of the historic view (recorded); the exports are those of the historic view on the same
/// result: the Word report (dates normalized) and the PDF beside it by its recipe (WallExports.CreateDocument with the figures of
/// ModuleFigureRenderer.WallFigures for the combinations it showed, then ReportFiles as SheetEditor.ExportReport), the CSV of the checks and of
/// the slices (recorded texts), «Word globale» by its recipe (WallExports.CreateGlobal), the bar schedule (PDF, Word and PNG of
/// «Distinta ferri…» by its recipe on WallModel.CalculateBarSchedule and ModuleFigureRenderer.BarFigures, pages and CSV recorded).</item>
/// <item>Screenshots of the view in Chiara, Scuro and Molto scuro (folder schermate; 144 DPI on this machine).</item>
/// <item>Help: the «?» labels shown are those of the historic view (recorded); every «?» and every F1 on a field of every form reaches the
/// IWikiNavigator of the services (lesson 1 of 17/10), F1 elsewhere the guide of the module.</item>
/// <item>Automation names: those of the historic view kept (recorded).</item>
/// <item>Read-only revision: the controls that accept the user are those of the historic view after InspectRevision (recorded).</item>
/// <item>The dialog of the values (MU10): the view of RetainingWallParametersViewModel, applied, closed and read-only (MU3).</item>
/// <item>The shared soil transfer dialog (T5, MU9): its view, a copy and a replacement sent to the window.</item>
/// <item>Bench and Release: with --module-impl and in the Release the walls open the MVVM view.</item>
/// </list>
/// What the check compared with the historic view opened beside the new one is compared with the values it gave, recorded before its archiving in
/// tests/fixtures/f5-muri/vista-storica.json (<see cref="Recorded"/>), or with the recipe of its exports on the same result.
/// Writes dettaglio.txt and esito.txt («PASS n controlli …»).
/// </summary>
internal static class RetainingWallViewChecks
{
    private const string Module = RetainingWallScenarios.Module;

    /// <summary>
    /// The values of the historic view recorded by this check before its archiving (passaggio della Release, 23/10/2026, f5-pass-muri):
    /// tests/fixtures/f5-muri/vista-storica.json.
    /// </summary>
    private static JsonObject Recorded(string root) => recorded ??=
        JsonNode.Parse(File.ReadAllText(Path.Combine(root, "tests", "fixtures", "f5-muri", "vista-storica.json")))!.AsObject();

    private static JsonObject? recorded;

    /// <summary>SHA-256 of the UTF-8 text, as the values of <see cref="Recorded"/>.</summary>
    private static string Sha(string text) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    /// <summary>The bytes of an export with the BOM, the text being the recorded one.</summary>
    private static bool WithBom(byte[] bytes, string sha)
    {
        byte[] bom = Encoding.UTF8.GetPreamble();
        return bytes.AsSpan().StartsWith(bom) && Sha(Encoding.UTF8.GetString(bytes, bom.Length, bytes.Length - bom.Length)) == sha;
    }

    private static int checks;
    private static readonly List<string> Log = [];

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
        checks++;
        Log.Add("OK " + message);
    }

    internal static async Task Run(string root, string output)
    {
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("La cartella della prova deve essere vuota.");
        Directory.CreateDirectory(output);
        checks = 0; Log.Clear();
        var culture = CultureInfo.GetCultureInfo("it-IT");
        await Dispatcher.CurrentDispatcher.InvokeAsync(() => { CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = culture; });
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
        try
        {
            recorded = null;
            await Traces(root, output);
            await FixturesAndExports(root, output);
            await Screenshots(root, Path.Combine(output, "schermate"));
            await Help(root);
            await AutomationNames(root);
            await ReadOnly(root);
            await ValuesDialog(root);
            await SoilDialog(root);
            Bench(root);
        }
        finally
        {
            Appearance.Set(AppAppearance.Light, false);
            File.WriteAllLines(Path.Combine(output, "dettaglio.txt"), Log, new UTF8Encoding(false));
        }
        Check(Application.Current.Windows.Count == 0, "Finestre della prova chiuse");
        File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {checks} controlli della vista XAML dei muri di sostegno (MvvmSheetEditor, fuori schermo)", new UTF8Encoding(false));
    }

    // ---------------------------------------------------------------- services and hosting

    /// <summary>The bench choice of the check: the walls with the MVVM view.</summary>
    private static readonly string[] BenchArguments = [ModuleHostBench.Argument, Module + "=mvvm"];

    /// <summary>
    /// Services of the sheets in the check: errors rethrown, messages recorded, every question answered yes, files from <see cref="SavePath"/>,
    /// recorded Wiki. The dialog of the values is shown off screen and modeless (as the historic trace shows its window) until it closes; the
    /// soil transfer dialogs are counted and answered by <see cref="SoilTransfer"/> (none: closed without a choice).
    /// </summary>
    private sealed class Services : IConfirmationService, IDialogService, IFileDialogService, IWikiNavigator, IMessageService
    {
        internal List<string?> Wiki { get; } = [];
        internal List<(string Title, string Message)> Messages { get; } = [];
        internal string? SavePath { get; set; }
        internal int Soils { get; private set; }
        internal Func<SoilTransferDialogViewModel, SoilTransferChoice?>? SoilTransfer { get; set; }
        internal Window? ParametersWindow { get; private set; }
        internal RetainingWallParametersViewModel? Parameters { get; private set; }
        private TaskCompletionSource shown = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal PresentationServices Create()
        {
            var bench = ModuleHostBench.Services();
            return new PresentationServices(this, this, this, this, bench.Clipboard, bench.Links, this);
        }

        /// <summary>Completed when the next dialog of the values is shown.</summary>
        internal Task NextParameters() { shown = new(TaskCreationOptions.RunContinuationsAsynchronously); return shown.Task; }

        public bool ConfirmSharedUpdate(SharedUpdatePrompt prompt) => throw new InvalidOperationException("Domanda non prevista: dati condivisi.");
        public bool ConfirmProjectMove(string destination, IReadOnlyList<string> changes) => throw new InvalidOperationException("Domanda non prevista: spostamento.");
        public bool ConfirmClose(Func<bool> saveOrDiscard) => throw new InvalidOperationException("Domanda non prevista: chiusura.");
        public SaveChangesAnswer AskSaveChanges() => throw new InvalidOperationException("Domanda non prevista: salvataggio.");
        public bool ConfirmDelete(string title, string question) => true;

        public void ReportFailure(string title, Exception error) => throw new InvalidOperationException("Errore di un comando: " + title, error);
        public void Inform(string title, string message) => Messages.Add((title, message));
        public void Warn(string title, string message) => Messages.Add((title, message));

        public Task<TResult?> ShowDialogAsync<TResult>(IDialogViewModel<TResult> dialog)
        {
            if (dialog is SoilTransferDialogViewModel transfer)
            {
                Soils++;
                return Task.FromResult(SoilTransfer is { } answer ? (TResult?)(object?)answer(transfer) : default);
            }
            if (dialog is not RetainingWallParametersViewModel parameters) throw new InvalidOperationException("Finestra non prevista: " + dialog.Title);
            // As WpfDialogService (window on the DataTemplate, the result when it closes), but outside the screen and without waiting.
            var window = new Window
            {
                Title = dialog.Title, Content = dialog, SizeToContent = SizeToContent.WidthAndHeight, Left = -32000, Top = -32000,
                WindowStartupLocation = WindowStartupLocation.Manual, ShowActivated = false, ShowInTaskbar = false
            };
            window.SetValue(NumericPresentation.EnabledProperty, true);
            Appearance.Watch(window);
            var done = new TaskCompletionSource<TResult?>(TaskCreationOptions.RunContinuationsAsynchronously);
            void Close(object? sender, EventArgs e) => window.Close();
            dialog.CloseRequested += Close;
            window.Closed += (_, _) =>
            {
                dialog.CloseRequested -= Close;
                if (ReferenceEquals(ParametersWindow, window)) { ParametersWindow = null; Parameters = null; }
                done.TrySetResult(dialog.Result);
            };
            ParametersWindow = window; Parameters = parameters;
            window.Show();
            window.UpdateLayout();
            shown.TrySetResult();
            return done.Task;
        }

        public string? OpenFile(OpenFileRequest request) => throw new InvalidOperationException("Dialogo di apertura non previsto.");
        public string? SaveFile(SaveFileRequest request) => SavePath ?? throw new InvalidOperationException("Dialogo di salvataggio non previsto.");
        public string? ChooseFolder(string? title = null) => throw new InvalidOperationException("Cartella non prevista.");
        public void Show(string? uri = null) => Wiki.Add(uri);
    }

    private static SheetEditorFactory Editors(Services services) =>
        new(ModuleHostBench.Implementations(BenchArguments), DesktopComposition.CreateModules(), services.Create(), new CalculationLimiter(CalculationLimiter.DefaultMaximum));

    /// <summary>A sheet in a window outside the screen, 1600×1000 as the historic traces lay out the view.</summary>
    private sealed class Hosted : IDisposable
    {
        private readonly Window window;
        internal ISheetEditor Editor { get; }
        internal int Modified, Reports;
        internal List<(string Name, JsonObject Data)> Sections { get; } = [];

        internal Hosted(ISheetEditor editor, bool dark = false, double width = 1600, double height = 1000)
        {
            Editor = editor;
            editor.Modified += () => Modified++;
            editor.ReportRequested += () => Reports++;
            editor.ModuleCopyRequested += (module, data, name) => { if (module == "str_palo") Sections.Add((name, data)); };
            window = new Window
            {
                Content = editor.View, Width = width, Height = height, Left = -32000, Top = -32000, WindowStartupLocation = WindowStartupLocation.Manual,
                ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, Title = "Muri " + editor.GetType().Name
            };
            Appearance.Watch(window);
            Appearance.SetDark(window, dark);
            window.Show();
            Layout();
        }

        internal FrameworkElement Root => Editor.View;

        internal RetainingWallSheetViewModel Sheet => (RetainingWallSheetViewModel)((MvvmSheetEditor)Editor).Sheet;

        internal RetainingWallSheetView View => Walk(Root).OfType<RetainingWallSheetView>().Single();

        internal void Layout() { window.UpdateLayout(); Root.UpdateLayout(); }

        internal async Task Settle()
        {
            Layout();
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            Layout();
        }

        /// <summary>Opens every expander and shows both pages, as the visit of the F3 capture.</summary>
        internal async Task ExpandAll()
        {
            for (int pass = 0; pass < 4; pass++)
            {
                foreach (var expander in Walk(Root).OfType<Expander>().ToArray()) expander.IsExpanded = true;
                foreach (var tabs in Walk(Root).OfType<TabControl>().ToArray())
                {
                    int selected = tabs.SelectedIndex;
                    for (int i = 0; i < tabs.Items.Count; i++) { tabs.SelectedIndex = i; await Settle(); }
                    tabs.SelectedIndex = selected;
                }
                await Settle();
            }
        }

        internal byte[] Png()
        {
            Layout();
            var element = (FrameworkElement)window.Content;
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(element);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = new MemoryStream(); encoder.Save(stream); return stream.ToArray();
        }

        public void Dispose()
        {
            window.Content = null;
            window.Close();
            Editor.Dispose();
        }
    }

    /// <summary>The logical and the visual tree, as the visit of the F3 capture walks it; without <paramref name="popups"/> the lists of the combo boxes are left out.</summary>
    internal static IEnumerable<DependencyObject> Walk(DependencyObject root, bool popups = true)
    {
        var pending = new Stack<DependencyObject>(); var seen = new HashSet<DependencyObject>(); pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop(); if (!seen.Add(current)) continue;
            yield return current;
            if (!popups && current is Popup) continue;
            var children = LogicalTreeHelper.GetChildren(current).OfType<DependencyObject>().ToList();
            if (current is Visual) for (int i = 0; i < VisualTreeHelper.GetChildrenCount(current); i++) children.Add(VisualTreeHelper.GetChild(current, i));
            foreach (var child in children.AsEnumerable().Reverse()) pending.Push(child);
        }
    }

    /// <summary>
    /// The historic view builds every control in code, also where the user does not see it; the XAML view builds the content that WPF has not
    /// laid out yet (closed expanders, the page not shown, the containers of the panels) only when it is shown. Here that content is measured
    /// outside the visual tree and its templates are applied, so that it can be read and driven as in the historic trace (a control the user
    /// cannot use still does nothing). Nothing is written into the sheet (as ShallowFoundationViewChecks.Realize).
    /// </summary>
    internal static void Realize(FrameworkElement root)
    {
        int known = -1;
        for (int pass = 0; pass < 20; pass++)
        {
            var elements = Walk(root, popups: false).OfType<FrameworkElement>().ToArray();
            foreach (var element in elements)
            {
                if (!ReferenceEquals(element, root) && element is not Window && VisualTreeHelper.GetParent(element) is null && !(element.IsMeasureValid && element.IsArrangeValid))
                {
                    element.Measure(new Size(760, 700));
                    element.Arrange(new Rect(0, 0, 760, 700));
                }
                element.ApplyTemplate();
            }
            if (elements.Length == known) return;
            known = elements.Length;
        }
    }

    private static string? BoundPath(DependencyObject element, DependencyProperty property) => BindingOperations.GetBinding(element, property)?.Path?.Path;

    /// <summary>Shown as the historic trace checks it: no element up to the view is collapsed or hidden (closed expanders do not count).</summary>
    private static bool Shown(DependencyObject element, DependencyObject root)
    {
        for (DependencyObject? node = element; node is not null && !ReferenceEquals(node, root); node = LogicalTreeHelper.GetParent(node) ?? VisualTreeHelper.GetParent(node))
            if (node is UIElement { Visibility: not Visibility.Visible }) return false;
        return true;
    }

    /// <summary>A click as the user makes it (ButtonBase.OnClick: the Click event, then the command), only when the button is usable.</summary>
    private static void Press(ButtonBase? button, DependencyObject root)
    {
        if (button is null || !button.IsEnabled || !Shown(button, root)) return;
        typeof(ButtonBase).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(button, null);
    }

    private static void F1(UIElement target)
    {
        var source = PresentationSource.FromVisual(target) ?? throw new InvalidOperationException("Elemento senza finestra: " + target);
        target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.F1) { RoutedEvent = Keyboard.KeyDownEvent });
    }

    // ---------------------------------------------------------------- the view, read and driven through its controls

    private sealed class ViewSide
    {
        private readonly HeldModuleService engine;
        private readonly RetainingWallIds ids;
        private readonly Services services;
        private readonly Func<JsonObject, Hosted> open;
        private Hosted host;
        private bool readOnly;

        internal ViewSide(Hosted host, HeldModuleService engine, RetainingWallIds ids, Services services, Func<JsonObject, Hosted> open)
        {
            this.host = host; this.engine = engine; this.ids = ids; this.services = services; this.open = open;
        }

        internal Hosted Host => host;
        private RetainingWallSheetViewModel Sheet => host.Sheet;
        private RetainingWallSheetView View => host.View;
        private FrameworkElement Root => host.Root;
        private IEnumerable<DependencyObject> All => Walk(Root, popups: false);

        private void Layout() => host.Layout();

        private T Named<T>(string name) where T : FrameworkElement => (T)(View.FindName(name) ?? throw new InvalidOperationException("Controllo della vista assente: " + name));

        /// <summary>The editor of a field of a form («form/campo»): the control of FieldForm bound to the FieldViewModel of the sheet; null when not realized.</summary>
        private T? Editor<T>(string path) where T : FrameworkElement
        {
            var parts = path.Split('/');
            if (!Sheet.Forms.TryGetValue(parts[0], out var form) || !form.Contains(parts[1])) return null;
            var field = form[parts[1]];
            T? Find() => All.OfType<T>().FirstOrDefault(e => ReferenceEquals(e.DataContext, field) && e.TemplatedParent is not ComboBox);
            // A field in a closed group or on the page not shown: its editor is built as the historic one exists.
            if (Find() is { } editor) return editor;
            // The content of a closed group is built in two passes (the group, then the form it hosts).
            for (int pass = 0; pass < 3; pass++)
            {
                Realize(Root); host.Layout();
                if (Find() is { } built) return built;
            }
            return null;
        }

        /// <summary>The grid of a table of the inputs by the name of the trace (the grid bound to that table).</summary>
        private WallInputGrid? Grid(string name)
        {
            var table = name switch
            {
                "valle" => Sheet.ValleyLayers, "monte" => Sheet.MountainLayers, "cedimenti" => Sheet.ServiceLayers, "azioni" => Sheet.Actions, "matrice" => Sheet.Matrix,
                "globale-monte" => Sheet.GlobalRearLayers, "globale-valle" => Sheet.GlobalValleyLayers, "matrice-globale" => Sheet.GlobalMatrix,
                _ => Sheet.ProfilePoints.TryGetValue(name, out var points) ? points : throw new ArgumentException("Tabella sconosciuta: " + name)
            };
            string field = name switch
            {
                "valle" => "ValleyGrid", "monte" => "MountainGrid", "cedimenti" => "ServiceGrid", "azioni" => "ActionGrid", "matrice" => "MatrixGrid",
                "globale-monte" => "GlobalRearGrid", "globale-valle" => "GlobalValleyGrid", "matrice-globale" => "GlobalMatrixGrid", _ => ""
            };
            var grid = field.Length > 0 ? Named<WallInputGrid>(field) : All.OfType<WallInputGrid>().SingleOrDefault(g => table is not null && ReferenceEquals(g.Table, table));
            return grid?.Table is null ? null : grid;
        }

        /// <summary>The button of a table bound to <paramref name="command"/> with <paramref name="parameter"/>.</summary>
        private Button? TableButton(string command, string? parameter) =>
            All.OfType<Button>().FirstOrDefault(b => BoundPath(b, ButtonBase.CommandProperty) == command && Equals(b.CommandParameter, parameter));

        private Button? Caption(string caption) => All.OfType<Button>().FirstOrDefault(b => Equals(b.Content, caption));

        private bool Usable(UIElement element) => element.IsEnabled && Shown(element, Root);

        /// <summary>
        /// F5.15, MU14: the buttons of a work are off until their command ends, a turn of the dispatcher after the work of the sheet; a step
        /// that waits for the work waits for them too, as a user who looks at the screen after it is updated.
        /// </summary>
        private async Task CommandsEnded()
        {
            foreach (var command in new[] { Sheet.RecalculateCommand, Sheet.DesignRebarCommand, Sheet.CalculateGlobalCommand })
                if (command is CommunityToolkit.Mvvm.Input.IAsyncRelayCommand { ExecutionTask: { } running }) await running.WaitAsync(TimeSpan.FromSeconds(60));
        }

        private void Choose(ComboBox combo, string item)
        {
            if (!Usable(combo) || combo.ItemsSource is not IEnumerable items || !items.Cast<object>().Any(i => Equals(i, item))) return;
            combo.SelectedItem = item;
        }

        internal async Task Apply(RetainingWallStep step)
        {
            // The user opens the page of the control first.
            if (RetainingWallScenarios.PageOf(step) is int page && View.Pages.SelectedIndex != page) { View.Pages.SelectedIndex = page; Layout(); }
            switch (step.Kind)
            {
                case RetainingWallStep.Commit: host.Editor.Commit(); break;
                case RetainingWallStep.Calculate:
                    Press(Caption("Ricalcola"), Root);
                    await Sheet.PendingWork.WaitAsync(TimeSpan.FromSeconds(60));
                    await CommandsEnded();
                    break;
                case RetainingWallStep.Start: Press(Caption("Ricalcola"), Root); break;
                case RetainingWallStep.Wait: await Sheet.PendingWork.WaitAsync(TimeSpan.FromSeconds(60)); await CommandsEnded(); break;
                case RetainingWallStep.ReadOnly: host.Editor.InspectRevision(); readOnly = true; break;
                case RetainingWallStep.Reopen:
                {
                    var data = host.Editor.Data;
                    services.ParametersWindow?.Close();
                    host.Dispose();
                    host = open(data); readOnly = false;
                    break;
                }
                case RetainingWallStep.Hold: engine.Hold(); break;
                case RetainingWallStep.Entered: await engine.EnteredAsync(); break;
                case RetainingWallStep.Release: engine.Release(); break;
                case RetainingWallStep.Parameters:
                {
                    var shown = services.NextParameters();
                    var parameters = Caption("Valori di calcolo…");
                    if (parameters is null || !Usable(parameters)) break;
                    Press(parameters, Root);
                    await shown.WaitAsync(TimeSpan.FromSeconds(60));
                    break;
                }
                case RetainingWallStep.ParametersWait:
                    if (services.Parameters is { } applying) await applying.Applying.WaitAsync(TimeSpan.FromSeconds(60));
                    else await LastApplying.WaitAsync(TimeSpan.FromSeconds(60));
                    break;
                case RetainingWallStep.Field:
                    if (Editor<TextBox>(step.Key!) is { } text && Usable(text) && !text.IsReadOnly && text.DataContext is FieldViewModel typed)
                    {
                        // The user focuses the field (the raw text replaces the formatted one), types and leaves, as FieldForm reports the focus.
                        typed.IsFocused = true; text.Text = step.Text; typed.IsFocused = false;
                    }
                    break;
                case RetainingWallStep.Choice:
                    if (Editor<ComboBox>(step.Key!) is { } choice && Usable(choice) && !Equals(choice.SelectedItem, step.Text)) choice.SelectedItem = step.Text;
                    break;
                case RetainingWallStep.Option:
                    if (Editor<CheckBox>(step.Key!) is { } box && Usable(box)) box.IsChecked = box.IsChecked != true;
                    break;
                case RetainingWallStep.Family:
                {
                    var family = Named<ComboBox>("FamilyChoice");
                    if (Usable(family) && family.Items.Cast<WallFamily>().SingleOrDefault(f => f.Id == step.Text) is { Available: true } item) family.SelectedItem = item;
                    break;
                }
                case RetainingWallStep.Example: Press(Caption(step.Text == "gravity" ? "Gravità esempio" : "Mensola esempio"), Root); break;
                case RetainingWallStep.Cell:
                {
                    var parts = step.Key!.Split('/');
                    var grid = Grid(parts[0]);
                    if (grid is null || grid.IsReadOnly || !Usable(grid) || step.Row >= grid.Items.Count) break;
                    if (grid.Columns.Single(c => c.SortMemberPath == parts[1]).IsReadOnly) break;
                    // The bindings of the cells write the row at every keystroke (UpdateSourceTrigger=PropertyChanged).
                    var row = (JsonRowViewModel)grid.Items[step.Row];
                    if (step.Text is null) row[parts[1]] = !(row[parts[1]] is true);
                    else row[parts[1]] = step.Text;
                    break;
                }
                case RetainingWallStep.Select:
                {
                    var grid = Grid(step.Key!);
                    if (grid is not null && Shown(grid, Root)) grid.SelectedIndex = step.Row;
                    break;
                }
                case RetainingWallStep.Add:
                    switch (step.Key)
                    {
                        case "azioni":
                            var type = Named<ComboBox>("ActionType");
                            if (Usable(type)) type.SelectedItem = step.Text;
                            Press(Caption("+ Azione"), Root);
                            break;
                        case "valle" or "monte": Press(TableButton("AddSoilLayerCommand", step.Key), Root); break;
                        case "cedimenti": Press(TableButton("AddServiceLayerCommand", null), Root); break;
                        case "globale-monte" or "globale-valle": Press(TableButton("AddGlobalLayerCommand", step.Key == "globale-valle" ? "valle" : "monte"), Root); break;
                        case "matrice" or "matrice-globale": break;
                        default: Press(TableButton("AddProfilePointCommand", step.Key), Root); break;
                    }
                    break;
                case RetainingWallStep.Remove:
                    switch (step.Key)
                    {
                        case "azioni": Press(Caption("− Azione"), Root); break;
                        case "valle" or "monte": Press(TableButton("RemoveSoilLayerCommand", step.Key), Root); break;
                        case "cedimenti": Press(TableButton("RemoveServiceLayerCommand", null), Root); break;
                        case "globale-monte" or "globale-valle": Press(TableButton("RemoveGlobalLayerCommand", step.Key == "globale-valle" ? "valle" : "monte"), Root); break;
                        case "matrice" or "matrice-globale": break;
                        default: Press(TableButton("RemoveProfilePointCommand", step.Key), Root); break;
                    }
                    break;
                case RetainingWallStep.Move:
                    if (step.Key is "valle" or "monte") Press(TableButton(step.Index < 0 ? "MoveSoilLayerUpCommand" : "MoveSoilLayerDownCommand", step.Key), Root);
                    break;
                case RetainingWallStep.Button:
                    if (step.Text is "Applica proposta" or "Modifica profilo, strati e ricerca") Realize(Root);
                    Press(step.Text switch
                    {
                        "Sisma" => Named<Button>("SeismicButton"), "Stabilità globale" => Named<Button>("GlobalSetupButton"), "Calcola armature" => Named<Button>("DesignButton"),
                        "Apri sezione in c.a." => Named<Button>("SendSectionButton"),
                        "Applica proposta" => Walk(Named<ItemsControl>("ProposalPanel")).OfType<Button>().FirstOrDefault(b => Equals(b.Content, step.Text)),
                        "Modifica profilo, strati e ricerca" => Walk(Named<ItemsControl>("ChecksPanel")).OfType<Button>().FirstOrDefault(b => Equals(b.Content, step.Text)),
                        _ => Caption(step.Text!) ?? throw new ArgumentException("Pulsante sconosciuto: " + step.Text)
                    }, Root);
                    break;
                case RetainingWallStep.RemoveHistory:
                {
                    Realize(Root);
                    var list = Named<ItemsControl>("HistoryList");
                    if (step.Index < list.Items.Count && list.ItemContainerGenerator.ContainerFromIndex(step.Index) is DependencyObject item)
                        Press(Walk(item).OfType<Button>().Single(b => Equals(b.Content, "Elimina accelerogramma")), Root);
                    break;
                }
                case RetainingWallStep.Combination: Choose(Named<ComboBox>("CombinationChoice"), step.Text!); break;
                case RetainingWallStep.View: Choose(Named<ComboBox>("ViewChoice"), step.Text!); break;
                case RetainingWallStep.Member: Choose(Named<ComboBox>("MemberChoice"), step.Text!); break;
                case RetainingWallStep.Filter: Choose(Named<ComboBox>("FilterChoice"), step.Text!); break;
                case RetainingWallStep.GlobalCombination: Choose(Named<ComboBox>("GlobalCombinationChoice"), step.Text!); break;
                case RetainingWallStep.ResultRow:
                    Realize(Root);
                    if (Walk(Named<ItemsControl>("ChecksPanel")).OfType<WallBlockGrid>().FirstOrDefault() is { } results && step.Row < results.Items.Count) results.SelectedIndex = step.Row;
                    break;
                case RetainingWallStep.Toggle:
                    if (All.OfType<CheckBox>().SingleOrDefault(c => Equals(c.Content, step.Key)) is { } toggle && Usable(toggle)) toggle.IsChecked = toggle.IsChecked != true;
                    break;
                case RetainingWallStep.Parameter:
                {
                    if (ParametersView is not { } dialog) break;
                    var grid = (WallInputGrid)dialog.FindName("RowsGrid");
                    var body = (FrameworkElement)dialog.FindName("Body");
                    if (grid.Items.Cast<JsonRowViewModel>().SingleOrDefault(r => r.Values.S("path") == step.Key) is { } row && !grid.IsReadOnly && body.IsEnabled) row["value"] = step.Text;
                    break;
                }
                case RetainingWallStep.ParameterCombination:
                    if (ParametersView is { } values && ((FrameworkElement)values.FindName("Body")).IsEnabled) ((ComboBox)values.FindName("CombinationChoice")).SelectedItem = step.Text;
                    break;
                case RetainingWallStep.ParametersApply:
                    if (ParametersView is { } apply)
                    {
                        var parameters = services.Parameters!;
                        var button = Walk(apply).OfType<Button>().Single(b => Equals(b.Content, "Applica e ricalcola"));
                        if (button.IsEnabled && ((FrameworkElement)apply.FindName("Body")).IsEnabled) { Press(button, apply); LastApplying = parameters.Applying; }
                    }
                    break;
                case RetainingWallStep.ParametersClose: services.ParametersWindow?.Close(); break;
                default: throw new ArgumentException("Passo sconosciuto: " + step.Kind);
            }
            Layout();
        }

        /// <summary>The last «Applica e ricalcola» pressed: awaited after its window closed.</summary>
        private Task LastApplying { get; set; } = Task.CompletedTask;

        /// <summary>The view of the dialog of the values, when it is open.</summary>
        private RetainingWallParametersView? ParametersView =>
            services.ParametersWindow is { } window ? Walk(window).OfType<RetainingWallParametersView>().SingleOrDefault() : null;

        // ---------------------------------------------------------------- state

        /// <summary>The state of the sheet read from the controls of the view (the data, the result and the requests to the window from the sheet and the host).</summary>
        internal RetainingWallTraceState State()
        {
            Layout();
            var index = new Index(Root);
            var t = new RetainingWallTraceState(ids);
            var s = Sheet; var view = View;
            string Q(string? text) => RetainingWallTrace.Quote(text);
            string N(int value) => RetainingWallTrace.Number(value);
            string YN(bool value) => RetainingWallTrace.YesNo(value);
            t.Data(host.Editor.Data);
            t.Add("foglio", "modificato " + N(host.Modified) + " · sola lettura " + YN(readOnly) + " · occupato " + YN(host.Editor.Busy) + " · report " + N(host.Reports)
                + " · terreno " + N(services.Soils));
            t.Add("sezioni", string.Join(" | ", host.Sections.Select(c => Q(c.Name) + " " + t.Hash(c.Data))));
            t.Add("risultato", s.Calculation is null ? "-" : t.Hash(s.Calculation.Json()));
            t.Add("globale", s.GlobalResult is null ? "-" : (s.HasIndependentGlobal ? "indipendente" : "dal calcolo") + " · casi " + N(s.GlobalResult.Cases.Length));
            t.Add("stato", Q(view.StatusText.Text));
            var family = view.FamilyChoice;
            t.Add("tipologia", (string)family.SelectedValue + " · voci " + Q(string.Join("|", family.Items.Cast<WallFamily>().Select(f => f.Name + (f.Available ? "" : " (spenta)")))));
            t.Add("pulsante.sisma", Q(view.SeismicButton.Content as string) + " · " + Q(view.SeismicButton.ToolTip as string));
            t.Add("pulsante.globale", Q(view.GlobalSetupButton.Content as string) + " · " + Q(view.GlobalSetupButton.ToolTip as string));
            t.Add("pulsante.armature", YN(view.DesignButton.IsEnabled));
            t.Add("pagina", N(view.Pages.SelectedIndex));
            t.Add("combinazioni", Q(string.Join("|", (view.CombinationChoice.ItemsSource as IEnumerable<string>) ?? [])) + " · scelta " + Q(view.CombinationChoice.SelectedItem as string));
            t.Add("vista", Q(view.ViewChoice.SelectedItem as string) + " · elemento " + Q(view.MemberChoice.SelectedItem as string) + (view.MemberChoice.Visibility == Visibility.Visible ? "" : " nascosto")
                + " · filtro " + Q(view.FilterChoice.SelectedItem as string));
            t.Add("combinazioni-globali", Q(string.Join("|", (view.GlobalCombinationChoice.ItemsSource as IEnumerable<string>) ?? [])) + " · scelta " + Q(view.GlobalCombinationChoice.SelectedItem as string)
                + (view.GlobalCombinationChoice.Visibility == Visibility.Visible ? "" : " · nascoste"));
            t.Add("interruttori", string.Join(" ", RetainingWallScenarios.Toggles.Select(name => name + "=" + YN(index.CheckBoxes.Single(c => Equals(c.Content, name)).IsChecked == true)))
                + (view.FullProfileSwitch.Visibility == Visibility.Visible ? "" : " · profilo nascosto"));
            t.Add("riepilogo", Q(view.SummaryText.Text));
            foreach (string key in RetainingWallScenarios.FormKeys.Concat(s.Forms.Keys.Where(k => k.StartsWith("history_", StringComparison.Ordinal))))
            {
                if (!s.Forms.TryGetValue(key, out var form)) continue;
                // The host of the form; a form in a list not laid out yet (an accelerogram in a closed group) is shown as the sheet shows it.
                var hostControl = index.Hosts.GetValueOrDefault(form);
                t.Add("form." + key, (hostControl is null ? s.IsFormShown(key) : Shown(hostControl, Root)) ? "visibile" : "nascosto");
                foreach (var field in form.Fields) t.Field(FieldState(key, form, field, index));
            }
            foreach (var (name, card) in s.Cards.OrderBy(c => c.Key, StringComparer.Ordinal))
            {
                var expander = index.Cards[card];
                t.Add("scheda." + name, (expander.Visibility == Visibility.Visible ? "visibile" : "nascosta") + " · " + (expander.IsExpanded ? "aperta" : "chiusa"));
            }
            foreach (string name in RetainingWallScenarios.TableKeys)
            {
                var grid = Grid(name);
                if (grid is null) { t.Add("tabella." + name, "-"); continue; }
                t.Table(name, grid.Columns.Select(c => c.Header as string ?? "").ToArray(), grid.Items.Cast<JsonRowViewModel>().Select(r => r.Values).ToArray(), grid.SelectedIndex);
            }
            t.Add("testo.strati", Q(view.LayerStatusText.Text));
            t.Add("testo.matrice", Q(view.MatrixStatusText.Text));
            t.Add("testo.matrice-globale", Q(view.GlobalMessageText.Text));
            t.Add("testo.globale", Q(view.GlobalReadinessText.Text) + " · " + Q(view.GlobalReadinessText.ToolTip as string));
            t.Add("testo.ricerca", Q(view.GlobalSearchText.Text));
            t.Add("testo.sisma", Q(view.SeismicAuditText.Text));
            t.Add("testo.copriferro", Q(view.CoverStatusText.Text));
            t.Add("accelerogrammi", string.Join(" | ", Histories(view.HistoryList)));
            t.Panel("spinte", Blocks(view.PressurePanel, s));
            t.Panel("verifiche", Blocks(view.ChecksPanel, s));
            t.Add("sezione", Q(view.SectionText.Text) + " · invio " + YN(view.SendSectionButton.IsEnabled));
            t.Panel("proposta", Blocks(view.ProposalPanel, s));
            var d = view.InputDrawing;
            t.Add("disegno", "caso " + Q(d.Case?.Name) + " · risultato " + YN(d.Calculation is not null && ReferenceEquals(d.Calculation, s.Calculation))
                + " · azione " + (d.SelectedAction ?? "-") + " · strato " + N(d.SelectedLayer) + (d.SelectedValley ? " valle" : "")
                + " · spinta " + N(d.SelectedPressure) + " · carichi " + YN(d.ShowLoads) + " · dati " + YN(d.ShowLabels)
                + " · armature " + YN(d.ShowRebar) + " · spinte " + YN(d.ShowPressures));
            var g = view.DiagramsDrawing;
            t.Add("diagrammi", (g.Visibility == Visibility.Visible ? "visibili" : "nascosti") + " · modo " + Q(g.Mode) + " · diagrammi " + YN(g.Diagrams)
                + " · elemento " + Q(g.Member) + " · caso " + Q(g.Case?.Name) + " · armature " + YN(g.ShowRebar) + " · carichi " + YN(g.ShowLoads)
                + " · larghezza " + (double.IsPositiveInfinity(g.MaxWidth) ? "libera" : "1050"));
            var gd = view.GlobalDrawing;
            t.Add("disegno-globale", (gd.Visibility == Visibility.Visible ? "visibile" : "nascosto") + " · caso " + Q(gd.Case?.Factors.Name)
                + " · risultato " + YN(gd.Result is not null && ReferenceEquals(gd.Result, s.GlobalResult)) + " · critica " + YN(gd.FocusCritical)
                + " · concio " + N(gd.SelectedSlice));
            var p = view.GlobalPreview;
            t.Add("anteprima-globale", "caso " + Q(p.Case?.Factors.Name) + " · risultato " + YN(p.Result is not null && ReferenceEquals(p.Result, s.GlobalResult))
                + " · strato " + N(p.SelectedSoil) + " " + p.SelectedColumn);
            Parameters(t);
            return t;
        }

        private void Parameters(RetainingWallTraceState t)
        {
            if (ParametersView is not { } dialog) { t.Add("parametri", "-"); return; }
            Realize(dialog);
            var body = (FrameworkElement)dialog.FindName("Body");
            var message = (TextBlock)dialog.FindName("MessageText");
            var combo = (ComboBox)dialog.FindName("CombinationChoice");
            var grid = (WallInputGrid)dialog.FindName("RowsGrid");
            t.Add("parametri", "attiva " + RetainingWallTrace.YesNo(body.IsEnabled) + " · messaggio " + RetainingWallTrace.Quote(message.Text)
                + " · combinazioni " + RetainingWallTrace.Quote(string.Join("|", ((IEnumerable)combo.ItemsSource).Cast<string>())) + " · scelta " + RetainingWallTrace.Quote(combo.SelectedItem as string));
            t.Table("parametri", grid.Columns.Select(c => c.Header as string ?? "").ToArray(), grid.Items.Cast<JsonRowViewModel>().Select(r => r.Values).ToArray(), grid.SelectedIndex);
            t.Panel("parametri.derivati", Blocks((ItemsControl)dialog.FindName("DerivedPanel"), null));
        }

        /// <summary>A field as its FieldForm shows it; a row never laid out has no editor yet: its state is the one of its field.</summary>
        private static RetainingWallFieldState FieldState(string formKey, FormViewModel form, FieldViewModel f, Index index)
        {
            var editor = index.Editors.GetValueOrDefault(f);
            var row = index.Rows.GetValueOrDefault(f);
            if (editor is null || row is null)
            {
                bool active0 = f.IsEnabled && (f.Kind == FieldKind.Text ? !f.IsInputReadOnly : f.CanChange);
                string symbol0 = form.SymbolColumns ? f.Symbol : "";
                string? note0 = f.IsNoteVisible ? f.Note : null;
                return f.Kind switch
                {
                    FieldKind.Text => new(formKey, f.Key, "campo", f.Label, symbol0, f.Unit, f.Text, null, active0, f.IsDimmed, f.IsVisible, note0),
                    FieldKind.Choice => new(formKey, f.Key, "scelta", f.Label, symbol0, f.Unit, f.SelectedChoice ?? "", f.Choices, active0, f.IsDimmed, f.IsVisible, note0),
                    _ => new(formKey, f.Key, "opzione", f.Label, symbol0, f.Unit, RetainingWallTrace.YesNo(f.IsChecked), null, active0, f.IsDimmed, f.IsVisible, note0)
                };
            }
            var texts = Walk(row).OfType<TextBlock>().ToArray();
            string Text(string name) => texts.FirstOrDefault(b => b.Name == name) is { } block ? block.Text : "";
            var symbolText = texts.FirstOrDefault(b => b.Name == "SymbolText");
            string symbol = symbolText is { Visibility: Visibility.Visible } ? symbolText.Text : "";
            var noteBox = Walk(row).OfType<Border>().FirstOrDefault(b => b.Name == "NoteBox");
            string? note = noteBox is { Visibility: Visibility.Visible } ? ((TextBlock)noteBox.Child).Text : null;
            bool enabled = row.IsEnabled && editor.IsEnabled && editor is not TextBox { IsReadOnly: true };
            bool dimmed = row.Opacity < 1, visible = row.Visibility == Visibility.Visible;
            string label = AutomationProperties.GetName(editor), unit = Text("UnitText");
            // The value of a text field is its raw text (what the historic trace reads with InputForm.Get): the editor shows it formatted without focus.
            return editor switch
            {
                TextBox => new(formKey, f.Key, "campo", label, symbol, unit, f.Text, null, enabled, dimmed, visible, note),
                ComboBox choice => new(formKey, f.Key, "scelta", label, symbol, unit, choice.SelectedItem as string ?? "", ((IEnumerable)choice.ItemsSource).Cast<string>().ToArray(), enabled, dimmed, visible, note),
                CheckBox box => new(formKey, f.Key, "opzione", label, symbol, unit, RetainingWallTrace.YesNo(box.IsChecked == true), null, enabled, dimmed, visible, note),
                _ => throw new InvalidOperationException("Campo non previsto: " + formKey + "/" + f.Key)
            };
        }

        /// <summary>The accelerograms as the view shows them: the header of the group and the text of the samples.</summary>
        private static IEnumerable<string> Histories(ItemsControl list)
        {
            for (int i = 0; i < list.Items.Count; i++)
            {
                var history = (RetainingWallHistory)list.Items[i];
                if (list.ItemContainerGenerator.ContainerFromIndex(i) is not DependencyObject container) { yield return RetainingWallTrace.Quote(history.Name) + " " + RetainingWallTrace.Quote(history.Samples); continue; }
                var group = Walk(container).OfType<Expander>().First();
                var samples = Walk(container).OfType<TextBlock>().First(b => BoundPath(b, TextBlock.TextProperty) == "Samples");
                yield return RetainingWallTrace.Quote(group.Header as string) + " " + RetainingWallTrace.Quote(samples.Text);
            }
        }
    }

    /// <summary>
    /// A panel of results as the user reads it (texts, tables, buttons and groups, in order); null without a panel. A block that WPF has not
    /// laid out yet (a closed group, the page not shown) is read from the block shown by the panel, a button with the state of its command.
    /// </summary>
    private static IReadOnlyList<RetainingWallBlock>? Blocks(ItemsControl panel, RetainingWallSheetViewModel? sheet)
    {
        if (panel.ItemsSource is null) return null;
        var blocks = new List<RetainingWallBlock>();
        for (int i = 0; i < panel.Items.Count; i++)
        {
            var block = (WallPanelBlock)panel.Items[i];
            if (panel.ItemContainerGenerator.ContainerFromIndex(i) is not ContentPresenter container || VisualTreeHelper.GetChildrenCount(container) == 0)
            {
                blocks.Add(Unshown(block, sheet));
                continue;
            }
            switch (VisualTreeHelper.GetChild(container, 0))
            {
                case TextBlock text: blocks.Add(new(RetainingWallBlock.TextKind, text.Text)); break;
                case WallBlockGrid grid:
                    blocks.Add(new(RetainingWallBlock.TableKind, "", grid.Columns.Select(c => c.Header as string ?? "").ToArray(), ((IEnumerable)grid.ItemsSource).Cast<string[]>().ToArray()));
                    break;
                case Button button: blocks.Add(new(RetainingWallBlock.ButtonKind, button.Content as string ?? "", Enabled: button.IsEnabled)); break;
                case Expander group: blocks.Add(new(RetainingWallBlock.GroupKind, group.Header as string ?? "", Children: Blocks((ItemsControl)group.Content, sheet) ?? [])); break;
                case var other: throw new InvalidOperationException("Elemento del pannello non previsto: " + other.GetType().Name);
            }
        }
        return blocks;
    }

    private static RetainingWallBlock Unshown(WallPanelBlock block, RetainingWallSheetViewModel? sheet) => block.Kind switch
    {
        WallBlockKind.Table => new(RetainingWallBlock.TableKind, "", block.Columns, block.Rows),
        WallBlockKind.Group => new(RetainingWallBlock.GroupKind, block.Text, Children: block.Children!.Select(c => Unshown(c, sheet)).ToArray()),
        WallBlockKind.Button => new(RetainingWallBlock.ButtonKind, block.Text, Enabled: sheet?.PanelCommand.CanExecute(block.Command) ?? true),
        _ => new(RetainingWallBlock.TextKind, block.Text)
    };

    /// <summary>The controls of the view, found in one walk: the editors and the rows of the fields, the hosts of the forms, the cards, the check boxes.</summary>
    private sealed class Index
    {
        internal Dictionary<FieldViewModel, Control> Editors { get; } = new(ReferenceEqualityComparer.Instance);
        internal Dictionary<FieldViewModel, Grid> Rows { get; } = new(ReferenceEqualityComparer.Instance);
        internal Dictionary<object, ContentControl> Hosts { get; } = new(ReferenceEqualityComparer.Instance);
        internal Dictionary<WallCardViewModel, Expander> Cards { get; } = new(ReferenceEqualityComparer.Instance);
        internal List<CheckBox> CheckBoxes { get; } = [];

        internal Index(DependencyObject root)
        {
            foreach (var node in Walk(root, popups: false))
            {
                if (node is Control { DataContext: FieldViewModel field } editor && editor is TextBox or ComboBox or CheckBox && editor.TemplatedParent is not ComboBox) Editors.TryAdd(field, editor);
                if (node is Grid { Name: "Row", DataContext: FieldViewModel row } grid) Rows.TryAdd(row, grid);
                if (node is ContentControl { Content: { } content } host) Hosts.TryAdd(content, host);
                if (node is Expander { DataContext: WallCardViewModel card } expander) Cards.TryAdd(card, expander);
                if (node is CheckBox box) CheckBoxes.Add(box);
            }
        }
    }

    // ---------------------------------------------------------------- 1. traces

    private static async Task Traces(string root, string output)
    {
        string folder = Path.Combine(output, "tracce");
        Directory.CreateDirectory(folder);
        var differences = new List<string>();
        int scenarios = 0, lines = 0;
        foreach (var scenario in RetainingWallScenarios.All(root))
        {
            var trace = new List<string>();
            var services = new Services();
            var presentation = services.Create();
            var engine = new HeldModuleService(Module);
            var modules = DesktopComposition.CreateModules();
            Hosted Open(JsonObject data) => new(new MvvmSheetEditor(modules.SheetModule(Module), data, presentation, engine, new CalculationLimiter(CalculationLimiter.DefaultMaximum)));
            var side = new ViewSide(Open(RetainingWallScenarios.FixtureData(root, scenario.Fixture)), engine, new RetainingWallIds(), services, Open);
            try
            {
                var previous = side.State();
                trace.AddRange(RetainingWallTrace.Lines("apertura", null, previous));
                foreach (var step in scenario.Steps)
                {
                    await side.Apply(step);
                    var state = side.State();
                    trace.AddRange(RetainingWallTrace.Lines(step.ToString(), previous, state));
                    previous = state;
                }
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or TimeoutException)
            { trace.Add("errore " + error.GetType().Name + ": " + error.Message); Log.Add("   " + scenario.Name + ": " + error); }
            finally { engine.Release(); services.ParametersWindow?.Close(); side.Host.Dispose(); }
            RetainingWallTrace.Write(Path.Combine(folder, scenario.Name + ".txt"), trace);
            string stored = Path.Combine(RetainingWallScenarios.Reference(root), scenario.Name + ".txt");
            string? difference = File.Exists(stored) ? RetainingWallTrace.FirstDifference(RetainingWallTrace.Read(stored), trace) : "traccia di riferimento assente";
            if (difference is not null) differences.Add(scenario.Name + ": " + difference);
            scenarios++; lines += trace.Count;
        }
        if (differences.Count > 0) File.WriteAllLines(Path.Combine(folder, "differenze.txt"), differences, new UTF8Encoding(false));
        Check(differences.Count == 0 && scenarios == 24, $"Tracce della vista XAML uguali a quelle della vista storica: {scenarios} scenari, {lines} righe" + (differences.Count == 0 ? "" : " — " + differences[0]));
    }

    // ---------------------------------------------------------------- 2. visit and exports

    private static async Task FixturesAndExports(string root, string output)
    {
        string folder = Path.Combine(output, "esportazioni");
        Directory.CreateDirectory(folder);
        var normalizer = new TextNormalizer();
        var services = new Services();
        var editors = Editors(services);
        var fixtures = RetainingWallScenarios.Fixtures(root);
        var values = Recorded(root)["esportazioni"]!.AsObject();
        Check(fixtures.Count == 16 && fixtures.Order(StringComparer.Ordinal).SequenceEqual(values.Select(p => p.Key).Order(StringComparer.Ordinal)),
            "Fixture F3 dei muri: " + fixtures.Count + ", tutte con i valori registrati della vista storica");
        int schedules = 0, globals = 0;
        foreach (string id in fixtures)
        {
            string name = Module + "--" + id;
            var historic = values[id]!.AsObject();
            var data = RetainingWallScenarios.FixtureData(root, id);
            using var mvvm = new Hosted(editors.Open(Module, data));
            Check(mvvm.Editor is MvvmSheetEditor { Sheet: RetainingWallSheetViewModel }, name + ": editor MVVM con la vista XAML");
            // The automatic calculation of the opening first: its publication writes the automatic combinations, as today.
            await mvvm.Sheet.PendingWork.WaitAsync(TimeSpan.FromSeconds(60));
            await mvvm.Settle();
            mvvm.Editor.Commit();
            string opened = mvvm.Editor.Data.ToJsonString(J.Options);
            // The visit: both pages, every expander, the three appearances.
            await mvvm.ExpandAll();
            foreach (var mode in new[] { AppAppearance.Dark, AppAppearance.VeryDark, AppAppearance.Light }) { Appearance.Set(mode, false); await mvvm.Settle(); }
            mvvm.Editor.Commit();
            Check(mvvm.Editor.Data.ToJsonString(J.Options) == opened && mvvm.Modified == 0, name + ": la visita (pagine, gruppi, tre aspetti) non scrive e non solleva Modified");
            await mvvm.Editor.CalculateAsync(); mvvm.Editor.Commit();
            await mvvm.Settle();
            // The historic view after the same sequence (its automatic calculation of the opening, then the calculation): recorded values.
            string? result = historic["risultato"]?.GetValue<string>();
            Check(Sha(mvvm.Editor.Data.ToJsonString(J.Options)) == historic["dati"]!.GetValue<string>() && mvvm.Editor.HasResults == historic["risultati"]!.GetValue<bool>()
                && (result is null || mvvm.Editor.Result is { } r && Sha(r.ToJsonString(J.Options)) == result),
                name + ": dati e risultato dopo il calcolo uguali a quelli della vista storica (registrati)");
            var calculation = mvvm.Sheet.Calculation;
            Check((calculation is not null) == historic["calcolo"]!.GetValue<bool>(), name + ": calcolo mostrato come nella vista storica");
            if (calculation is null)
            {
                services.Messages.Clear();
                Press(mvvm.View.Pages.FindName("CsvButton") as Button ?? Walk(mvvm.Root).OfType<Button>().First(b => Equals(b.Content, "CSV")), mvvm.Root);
                Check(services.Messages.SequenceEqual([("", RetainingWallPresentation.WaitBeforeExport)]), name + ": senza risultato il CSV è rifiutato con il messaggio della vista storica");
                continue;
            }
            // Report Word and the PDF beside it.
            string title = "Fixture F3 · " + Module + " · " + id;
            var sections = ReportWord.Sezioni.Where(s => s.Key != "dettagli").Select(s => s.Key).ToHashSet();
            // The recipe of the historic view (RetainingWallWorkspace.BuildReportDocument) on the result, with the combinations it showed.
            string? drawn = historic["caso"]?.GetValue<string>(), drawnGlobal = historic["combinazione_globale_report"]?.GetValue<string>();
            var context = Anthea.Application.Reporting.WallReportPresentation.Prepare(calculation, drawn is null ? calculation.Cases[0] : calculation.Cases.First(c => c.Name == drawn),
                mvvm.Sheet.GlobalResult?.Cases.FirstOrDefault(c => c.Factors.Name == drawnGlobal));
            void ExportReport()
            {
                mvvm.Editor.ExportReport(Path.Combine(folder, name + "-mvvm.docx"), title, sections);
                var figures = ModuleFigureRenderer.WallFigures(calculation, context).Select(f => new ReportRetainingWall.Figure(f.Caption, f.Png, f.AspectRatio)).ToList();
                Anthea.Reports.ReportFiles.Write(Path.Combine(folder, name + "-storica.docx"), Anthea.Reports.DocxWriter.Write(WallExports.CreateDocument(title, calculation, figures)), title);
            }
            ExportReport();
            string? part = DifferentPart(File.ReadAllBytes(Path.Combine(folder, name + "-mvvm.docx")), File.ReadAllBytes(Path.Combine(folder, name + "-storica.docx")), normalizer);
            Check(part is null, name + ": report Word uguale in ogni parte a quello della ricetta della vista storica, figure comprese (date normalizzate)" + (part is null ? "" : ", diversa " + part));
            int attempts = 1;
            while (!SamePdf(File.ReadAllBytes(Path.Combine(folder, name + "-mvvm.pdf")), File.ReadAllBytes(Path.Combine(folder, name + "-storica.pdf"))) && attempts < 3)
            {
                attempts++;
                ExportReport();
            }
            Check(SamePdf(File.ReadAllBytes(Path.Combine(folder, name + "-mvvm.pdf")), File.ReadAllBytes(Path.Combine(folder, name + "-storica.pdf"))),
                name + ": PDF affiancato uguale a quello della ricetta della vista storica (date normalizzate" + (attempts > 1 ? $", {attempts} esportazioni" : "") + ")");
            // «CSV» of the checks: the BOM and the recorded text of the historic export (WallExports.Csv).
            string csv = Path.Combine(folder, name + "-verifiche.csv");
            services.SavePath = csv;
            Press(Walk(mvvm.Root).OfType<Button>().First(b => Equals(b.Content, "CSV")), mvvm.Root);
            Check(WithBom(File.ReadAllBytes(csv), historic["csv"]!.GetValue<string>()), name + ": CSV delle verifiche uguale a quello della vista storica (testo registrato)");
            Check((mvvm.Sheet.GlobalResult is not null) == historic.ContainsKey("conci"), name + ": stabilità globale calcolata come nella vista storica");
            if (mvvm.Sheet.GlobalResult is { } global)
            {
                // The view of the global stability: «CSV» of the slices and «Word globale» of the combination shown.
                var view = mvvm.View;
                view.Pages.SelectedIndex = 1; await mvvm.Settle();
                view.ViewChoice.SelectedItem = "Stabilità globale"; await mvvm.Settle();
                string slices = Path.Combine(folder, name + "-conci.csv");
                services.SavePath = slices;
                Press(Walk(mvvm.Root).OfType<Button>().First(b => Equals(b.Content, "CSV")), mvvm.Root);
                Check(WithBom(File.ReadAllBytes(slices), historic["conci"]!.GetValue<string>()), name + ": CSV dei conci uguale a quello della vista storica (testo registrato)");
                string word = Path.Combine(folder, name + "-globale-mvvm.docx"), expected = Path.Combine(folder, name + "-globale-storica.docx");
                services.SavePath = word;
                Press(Walk(mvvm.Root).OfType<Button>().First(b => Equals(b.Content, "Word globale")), mvvm.Root);
                string? shown = historic["combinazione_globale"]?.GetValue<string>();
                Check(mvvm.Sheet.SelectedGlobalCombination == shown, name + ": combinazione globale mostrata uguale alla vista storica (" + shown + ")");
                // The recipe of «Word globale» of the historic view (RetainingWallWorkspace.GlobalStability) on the same result.
                var drawing = new GlobalStabilityDrawing { Data = calculation.Input, Result = global, Case = global.Cases.FirstOrDefault(c => c.Factors.Name == shown), Width = 1100, Height = 460 };
                drawing.Measure(new Size(1100, 460)); drawing.Arrange(new Rect(0, 0, 1100, 460)); drawing.UpdateLayout();
                Anthea.Reports.ReportFiles.Write(expected, WallExports.CreateGlobal(calculation.Input, global, new("Stabilità globale · superficie visualizzata", Ui.DocumentSnapshot(drawing), 1100d / 460)), "Stabilità globale");
                part = DifferentPart(File.ReadAllBytes(word), File.ReadAllBytes(expected), normalizer);
                Check(part is null && SamePdf(File.ReadAllBytes(Path.ChangeExtension(word, ".pdf")), File.ReadAllBytes(Path.ChangeExtension(expected, ".pdf"))),
                    name + ": «Word globale» (Word e PDF) uguale a quello della vista storica" + (part is null ? "" : ", diversa " + part));
                globals++;
            }
            if (data.S("family") == "cantilever")
            {
                // «Distinta ferri…»: the dialog of the view and its four exports against the historic dialog on the same result.
                Window? dialog = null;
                mvvm.View.ShowWindow = window =>
                {
                    window.WindowStartupLocation = WindowStartupLocation.Manual; window.Left = -32000; window.Top = -32000; window.ShowActivated = false; window.ShowInTaskbar = false;
                    dialog = window; window.Show(); return null;
                };
                mvvm.View.Pages.SelectedIndex = 1; await mvvm.Settle();
                Press(Walk(mvvm.Root).OfType<Button>().First(b => Equals(b.Content, "Distinta ferri…")), mvvm.Root);
                Check(dialog is { Content: RetainingWallBarScheduleView } && dialog.Title == "Armature del muro · sezione e distinta ferri" && dialog.Width == 1270 && dialog.Height == 920,
                    name + ": «Distinta ferri…» apre il dialogo della vista come la finestra storica");
                var schedule = (RetainingWallBarScheduleView)dialog!.Content;
                // The drawing of the historic window (RetainingWallWorkspace.CreateBarDrawing) on the same result; its pages recorded.
                var sheet = new RetainingWallBarDrawing(calculation.Input, WallModel.CalculateBarSchedule(calculation));
                int pages = historic["pagine_distinta"]!.GetValue<int>();
                Check(((IEnumerable)((ComboBox)schedule.FindName("PageChoice")).ItemsSource).Cast<string>().Count() == pages && schedule.Drawing.PageCount == pages && sheet.PageCount == pages,
                    name + ": pagine della distinta come la finestra storica (" + pages + ", registrate)");
                byte[] Export(string caption, string extension)
                {
                    string path = Path.Combine(folder, name + "-distinta." + extension);
                    services.SavePath = path;
                    Press(Walk(schedule).OfType<Button>().Single(b => Equals(b.Content, caption)), schedule);
                    services.SavePath = null;
                    return File.ReadAllBytes(path);
                }
                Check(SamePdf(Export("PDF completo", "pdf"), DrawingPdf.Create(ModuleFigureRenderer.BarFigures(sheet).Select(f => f.Png))), name + ": distinta, PDF completo uguale alla finestra storica");
                string expectedWord = Path.Combine(folder, name + "-distinta-storica.docx");
                File.WriteAllBytes(expectedWord, WallExports.CreateBarSchedule(sheet.Data, sheet.Schedule, ModuleFigureRenderer.BarFigures(sheet)));
                part = DifferentPart(Export("Word completo", "docx"), File.ReadAllBytes(expectedWord), normalizer);
                Check(part is null, name + ": distinta, Word completo uguale alla finestra storica" + (part is null ? "" : ", diversa " + part));
                Check(WithBom(Export("CSV distinta", "csv"), historic["distinta_csv"]!.GetValue<string>()), name + ": distinta, CSV uguale alla finestra storica (testo registrato)");
                Check(Export("PNG pagina", "png").SequenceEqual(sheet.Png(3360, 2370)), name + ": distinta, PNG della pagina uguale alla finestra storica");
                dialog.Close();
                schedules++;
            }
            services.SavePath = null;
        }
        Log.Add($"   esportazioni: {schedules} distinte, {globals} stabilità globali");
        Check(schedules > 0 && globals > 0, $"Esportazioni della distinta ({schedules}) e della stabilità globale ({globals}) confrontate");
    }

    /// <summary>First part of two Word documents that differs, with the dates of core.xml and of the text normalized as in F3; null when equal.</summary>
    private static string? DifferentPart(byte[] a, byte[] b, TextNormalizer normalizer)
    {
        using var first = new ZipArchive(new MemoryStream(a)); using var second = new ZipArchive(new MemoryStream(b));
        var names = first.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal).ToArray();
        if (!names.SequenceEqual(second.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal))) return "elenco delle parti";
        foreach (string name in names)
        {
            byte[] x = Read(first.GetEntry(name)!), y = Read(second.GetEntry(name)!);
            bool equal = name is "docProps/core.xml" or "word/document.xml"
                ? Normalize(name, x, normalizer) == Normalize(name, y, normalizer) : x.SequenceEqual(y);
            if (!equal) return name;
        }
        return null;
        static byte[] Read(ZipArchiveEntry entry) { using var stream = entry.Open(); using var copy = new MemoryStream(); stream.CopyTo(copy); return copy.ToArray(); }
        static string Normalize(string name, byte[] bytes, TextNormalizer normalizer) => name == "docProps/core.xml"
            ? normalizer.Normalize(Encoding.UTF8.GetString(bytes)) : normalizer.NormalizeReportTimestamps(Encoding.UTF8.GetString(bytes));
    }

    /// <summary>Two PDF files equal but for what changes at every rendering (dates, identifiers, tags of the font subsets).</summary>
    private static bool SamePdf(byte[] a, byte[] b)
    {
        static string Text(byte[] bytes)
        {
            string text = Encoding.Latin1.GetString(bytes);
            text = Regex.Replace(text, @"\(D:[0-9+\-Z']+\)", "(D:)");
            text = Regex.Replace(text, @"/ID\s*\[[^\]]*\]", "/ID[]");
            text = Regex.Replace(text, @"(?<=/(?:FontName|BaseFont)/)[A-Z]{6}\+", "SUBSET+");
            text = Regex.Replace(text, @"(<xmp:\w*Date>)[^<]*(<)", "$1$2");
            return Regex.Replace(text, @"uuid:[0-9a-fA-F-]+", "uuid:");
        }
        return a.SequenceEqual(b) || Text(a) == Text(b);
    }

    // ---------------------------------------------------------------- 3. screenshots

    private static async Task Screenshots(string root, string folder)
    {
        Directory.CreateDirectory(folder);
        var editors = Editors(new Services());
        var data = RetainingWallScenarios.FixtureData(root, "globale-drenata-due-colonne");
        int written = 0;
        foreach (var mode in new[] { AppAppearance.Light, AppAppearance.Dark, AppAppearance.VeryDark })
        {
            Appearance.Set(mode, false);
            string suffix = mode switch { AppAppearance.Light => "chiara", AppAppearance.Dark => "scuro", _ => "molto-scuro" };
            using var host = new Hosted(editors.Open(Module, data), mode != AppAppearance.Light);
            await host.Sheet.PendingWork.WaitAsync(TimeSpan.FromSeconds(60));
            await host.Settle();
            File.WriteAllBytes(Path.Combine(folder, $"vista-input-{suffix}.png"), host.Png());
            host.View.Pages.SelectedIndex = 1; await host.Settle();
            File.WriteAllBytes(Path.Combine(folder, $"vista-verifiche-{suffix}.png"), host.Png());
            host.Sheet.SelectedView = "Stabilità globale"; await host.Settle();
            File.WriteAllBytes(Path.Combine(folder, $"vista-globale-{suffix}.png"), host.Png());
            written += 3;
        }
        Appearance.Set(AppAppearance.Light, false);
        // The narrow layout (below 1450 px the drawing goes under the cards) and the cards of the global stability opened.
        using (var narrow = new Hosted(editors.Open(Module, data), width: 1300, height: 1000))
        {
            await narrow.Sheet.PendingWork.WaitAsync(TimeSpan.FromSeconds(60));
            await narrow.Settle();
            Check(Grid.GetRow(narrow.View.InputView) == 1 && Grid.GetColumn(narrow.View.InputView) == 0, "Sotto i 1450 px il disegno degli input va sotto le schede");
            File.WriteAllBytes(Path.Combine(folder, "vista-input-stretta.png"), narrow.Png());
            narrow.Sheet.ShowGlobalSetupCommand.Execute(null); await narrow.Settle();
            File.WriteAllBytes(Path.Combine(folder, "vista-stabilita-globale-imposta.png"), narrow.Png());
            written += 2;
        }
        Check(written == 11, "Schermate della vista nei tre aspetti, stretta e con la stabilità globale: " + written + " PNG in " + folder + " (144 DPI su questa macchina)");
    }

    // ---------------------------------------------------------------- 4. help

    private static async Task Help(string root)
    {
        var services = new Services();
        var data = RetainingWallScenarios.FixtureData(root, "opzioni-avanzate");
        using var host = new Hosted(Editors(services).Open(Module, data));
        await host.Sheet.PendingWork.WaitAsync(TimeSpan.FromSeconds(60));
        await host.Settle();
        string[] before = Recorded(root)["aiuto"]!.AsArray().Select(n => n!.GetValue<string>()).ToArray(), after = HelpLabels(host.Root);
        Check(after.SequenceEqual(before), "Etichette «?» visibili uguali a quelle della vista storica (" + before.Length + ", registrate)");
        // Every expander opened on both pages: every «?» and every F1 on a field reaches the navigator of the services (lesson 1 of 17/10).
        await host.ExpandAll();
        host.View.Pages.SelectedIndex = 0; await host.Settle();
        int presses = 0;
        services.Wiki.Clear();
        foreach (var button in Walk(host.Root).OfType<Button>().Where(b => b.IsVisible && b.Content as string == "?").ToArray())
        {
            button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button)); presses++;
        }
        var editors = Walk(host.Root).OfType<Control>().Where(e => e.IsVisible && e.DataContext is FieldViewModel { HelpUri: not null } && e is TextBox or ComboBox or CheckBox && e.TemplatedParent is not ComboBox).ToArray();
        foreach (var editor in editors) { F1(editor); presses++; }
        var forms = editors.Select(e => ((FieldViewModel)e.DataContext).Form).Distinct().Count();
        Check(presses > 50 && services.Wiki.Count == presses && services.Wiki.All(u => u is not null),
            $"«?» e F1 su {editors.Length} campi di {forms} form: ogni pressione arriva al navigatore della Wiki dei servizi ({services.Wiki.Count}/{presses})");
        string guide = Anthea.Wiki.WikiCatalog.ForModule(Module)?.Id ?? throw new InvalidOperationException("Guida assente nella Wiki: " + Module);
        services.Wiki.Clear();
        F1(host.View.Pages); F1(host.View.StatusText);
        Check(services.Wiki.SequenceEqual([guide, guide]), "F1 fuori dai campi: la guida del modulo dal navigatore (" + guide + ")");
        host.Editor.InspectRevision(); await host.Settle();
        services.Wiki.Clear();
        var help = Walk(host.Root).OfType<Button>().First(b => b.IsVisible && b.Content as string == "?");
        Check(help.IsEnabled, "«?» utilizzabile in sola lettura");
        help.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, help));
        F1(editors[0]);
        Check(services.Wiki.Count == 2 && host.Modified == 0, "«?» e F1 anche in sola lettura, senza modificare il foglio");
    }

    // ---------------------------------------------------------------- 5. automation names

    /// <summary>The fixtures of the automation names and of the read-only revision.</summary>
    private static readonly string[] NameFixtures = ["default", "gravita-cls-passiva", "globale-drenata-due-colonne", "opzioni-avanzate"], ReadOnlyFixtures = ["default", "globale-drenata-due-colonne"];

    /// <summary>The «?» labels shown.</summary>
    private static string[] HelpLabels(DependencyObject root) => Walk(root).OfType<Button>().Where(b => b.IsVisible && b.Content as string == "?")
        .Select(b => AutomationProperties.GetName(b)).Order(StringComparer.Ordinal).ToArray();

    /// <summary>The automation names shown on both pages with every group opened.</summary>
    private static async Task<HashSet<string>> AutomationNameSet(Hosted hosted)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (int page = 0; page < 2; page++)
        {
            foreach (var tabs in Walk(hosted.Root).OfType<TabControl>().Take(1)) tabs.SelectedIndex = page;
            for (int pass = 0; pass < 3; pass++) { foreach (var e in Walk(hosted.Root).OfType<Expander>().ToArray()) e.IsExpanded = true; await hosted.Settle(); }
            names.UnionWith(Walk(hosted.Root).OfType<UIElement>().Where(e => e.IsVisible).Select(e => AutomationProperties.GetName(e)).Where(n => !string.IsNullOrEmpty(n)));
        }
        return names;
    }

    /// <summary>The state of the input controls on both pages with every group opened (<see cref="Controls"/>).</summary>
    private static async Task<Dictionary<string, string>> ControlStates(Hosted hosted)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int page = 0; page < 2; page++)
        {
            foreach (var tabs in Walk(hosted.Root).OfType<TabControl>().Take(1)) tabs.SelectedIndex = page;
            for (int pass = 0; pass < 3; pass++) { foreach (var e in Walk(hosted.Root).OfType<Expander>().ToArray()) e.IsExpanded = true; await hosted.Settle(); }
            foreach (var (key, value) in Controls(hosted.Root)) map[key] = map.TryGetValue(key, out var previous) && previous != value ? "misto" : value;
        }
        return map;
    }

    private static async Task AutomationNames(string root)
    {
        var editors = Editors(new Services());
        var values = Recorded(root)["nomi"]!.AsObject();
        foreach (string id in NameFixtures)
        {
            var data = RetainingWallScenarios.FixtureData(root, id);
            using var host = new Hosted(editors.Open(Module, data));
            await host.Settle();
            var before = values[id]!.AsArray().Select(n => n!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
            var after = await AutomationNameSet(host);
            var missing = before.Except(after).Order(StringComparer.Ordinal).ToArray();
            Log.Add($"   nomi nuovi ({id}): " + string.Join(", ", after.Except(before).Order(StringComparer.Ordinal)));
            Check(missing.Length == 0, $"{id}: nomi di automazione della vista storica conservati ({before.Count}, registrati)" + (missing.Length == 0 ? "" : "; mancanti: " + string.Join(", ", missing)));
        }
    }

    // ---------------------------------------------------------------- 6. read-only

    /// <summary>The controls that accept the user after InspectRevision, in the new view and in the historic view on the same data (recorded).</summary>
    private static async Task ReadOnly(string root)
    {
        var values = Recorded(root)["sola_lettura"]!.AsObject();
        foreach (string id in ReadOnlyFixtures)
        {
            var services = new Services();
            var data = RetainingWallScenarios.FixtureData(root, id);
            using var host = new Hosted(Editors(services).Open(Module, data));
            await host.Sheet.PendingWork.WaitAsync(TimeSpan.FromSeconds(60));
            host.Editor.InspectRevision();
            var before = values[id]!.AsObject().ToDictionary(p => p.Key, p => p.Value!.GetValue<string>(), StringComparer.Ordinal);
            var after = await ControlStates(host);
            var common = before.Keys.Intersect(after.Keys).Order(StringComparer.Ordinal).ToArray();
            var different = common.Where(k => before[k] != after[k]).Select(k => k + ": storica " + before[k] + ", nuova " + after[k]).ToArray();
            Log.Add($"   sola lettura ({id}), solo nella storica: " + string.Join(", ", before.Keys.Except(after.Keys).Order(StringComparer.Ordinal)));
            Log.Add($"   sola lettura ({id}), solo nella nuova: " + string.Join(", ", after.Keys.Except(before.Keys).Order(StringComparer.Ordinal)));
            Log.Add($"   sola lettura ({id}), comuni: " + string.Join(", ", common.Select(k => k + " " + after[k])));
            Check(different.Length == 0 && common.Length >= 60, $"{id}: sola lettura, stessi controlli bloccati e liberi della vista storica ({common.Length} controlli comuni, registrati)"
                + (different.Length == 0 ? "" : ": " + string.Join("; ", different)));
            Check(host.Modified == 0 && host.Editor.HasResults, id + ": sola lettura, la visita non modifica il foglio");
        }
    }

    /// <summary>The state of the visible input controls, keyed by kind and name (automation name or content).</summary>
    private static Dictionary<string, string> Controls(DependencyObject root)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var element in Walk(root).OfType<Control>().Where(e => e.IsVisible))
        {
            string? key = element switch
            {
                TextBox t when t.TemplatedParent is not Control && AutomationProperties.GetName(t) is { Length: > 0 } name => "testo " + name,
                ComboBox c when AutomationProperties.GetName(c) is { Length: > 0 } name => "scelta " + name,
                CheckBox c when AutomationProperties.GetName(c) is { Length: > 0 } name => "spunta " + name,
                CheckBox c when c.Content is string text => "spunta " + text,
                Button b when b.Content is string text && text != "?" && b.TemplatedParent is not Control => "pulsante " + text,
                _ => null
            };
            if (key is null) continue;
            string value = element switch
            {
                TextBox t => t.IsReadOnly || !t.IsEnabled ? "bloccato" : "libero",
                _ => element.IsEnabled ? "libero" : "bloccato"
            };
            map[key] = map.TryGetValue(key, out var previous) && previous != value ? "misto" : value;
        }
        return map;
    }

    // ---------------------------------------------------------------- 7. the dialog of the values (MU10)

    private static async Task ValuesDialog(string root)
    {
        var services = new Services();
        using var host = new Hosted(Editors(services).Open(Module, RetainingWallScenarios.FixtureData(root, "gravita-cls-passiva")));
        await host.Sheet.PendingWork.WaitAsync(TimeSpan.FromSeconds(60));
        await host.Settle();
        async Task<RetainingWallParametersView> Open()
        {
            var shown = services.NextParameters();
            Press(Walk(host.Root).OfType<Button>().First(b => Equals(b.Content, "Valori di calcolo…")), host.Root);
            await shown.WaitAsync(TimeSpan.FromSeconds(60));
            var view = Walk(services.ParametersWindow!).OfType<RetainingWallParametersView>().Single();
            Realize(view);
            return view;
        }
        var dialog = await Open();
        var window = services.ParametersWindow!;
        Check(window.Title == RetainingWallPresentation.ParametersTitle && dialog.Width == 1134 && dialog.Height == 741, "Dialogo dei valori: titolo e dimensioni della finestra storica (1150×780 con i bordi)");
        var grid = (WallInputGrid)dialog.FindName("RowsGrid");
        Check(grid.Columns.Select(c => c.Header as string).SequenceEqual(["Gruppo", "Parametro", "Valore assegnato", "Unità"]) && grid.Columns.Take(3).Select(c => c.Width.Value).SequenceEqual([270d, 340, 190])
            && grid.Items.Count > 20 && !grid.IsReadOnly, "Dialogo dei valori: righe con le colonne e le larghezze della finestra storica (" + grid.Items.Count + " righe)");
        var row = grid.Items.Cast<JsonRowViewModel>().Single(r => r.Values.S("path") == "geometry/height");
        row["value"] = "3,4";
        var parameters = services.Parameters!;
        Press(Walk(dialog).OfType<Button>().Single(b => Equals(b.Content, "Applica e ricalcola")), dialog);
        await parameters.Applying.WaitAsync(TimeSpan.FromSeconds(60));
        Check(services.ParametersWindow is null && parameters.Result && host.Editor.Data["geometry"].D("height") == 3.4 && host.Modified == 1, "Dialogo dei valori: «Applica e ricalcola» applica, chiude e ricalcola");
        dialog = await Open();
        Press(Walk(dialog).OfType<Button>().Single(b => Equals(b.Content, "Chiudi")), dialog);
        await host.Settle();
        Check(services.ParametersWindow is null && host.Modified == 1, "Dialogo dei valori: «Chiudi» senza applicare");
        // MU3: in a read-only revision the rows and «Applica e ricalcola» are locked (the historic window applies them: declared difference).
        host.Editor.InspectRevision(); await host.Settle();
        dialog = await Open();
        grid = (WallInputGrid)dialog.FindName("RowsGrid");
        var apply = Walk(dialog).OfType<Button>().Single(b => Equals(b.Content, "Applica e ricalcola"));
        Check(grid.IsReadOnly && !apply.IsEnabled && Walk(dialog).OfType<Button>().Single(b => Equals(b.Content, "Chiudi")).IsEnabled,
            "Sola lettura (MU3): righe bloccate e «Applica e ricalcola» spento, «Chiudi» disponibile");
        services.ParametersWindow!.Close();
        await host.Settle();
        Check(host.Modified == 1 && services.ParametersWindow is null, "Dialogo dei valori chiuso dalla barra del titolo");
    }

    // ---------------------------------------------------------------- 8. the shared soil transfer dialog (T5, MU9)

    private static async Task SoilDialog(string root)
    {
        var services = new Services();
        using var host = new Hosted(Editors(services).Open(Module, RetainingWallScenarios.FixtureData(root, "default")));
        await host.Settle();
        (string Module, JsonObject Data, string Name)? copied = null; JsonObject? replaced = null;
        host.Editor.ModuleCopyRequested += (module, data, name) => copied = (module, data, name);
        host.Editor.SoilReplacementRequested += data => replaced = data;
        SoilTransferDialogViewModel? shown = null;
        services.SoilTransfer = dialog =>
        {
            shown = dialog;
            // The view of the shared dialog (Views/Shared/SoilTransferTemplates.xaml) on this ViewModel, off screen.
            var window = new Window { Content = dialog, SizeToContent = SizeToContent.WidthAndHeight, Left = -32000, Top = -32000, ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual };
            window.Show(); window.UpdateLayout();
            bool templated = Walk(window).OfType<ContentPresenter>().Any(p => ReferenceEquals(p.Content, dialog) && VisualTreeHelper.GetChildrenCount(p) > 0);
            window.Close();
            Check(templated && dialog.Surveys.SequenceEqual(["Monte", "Valle"]), "Dialogo del terreno condiviso: vista del DataTemplate, profili Monte e Valle del muro");
            dialog.SelectedDestination = "geo_palo_verticale";
            dialog.SendCommand.Execute(null);
            return dialog.Result;
        };
        var button = Walk(host.Root).OfType<Button>().First(b => Equals(b.Content, "Invia / carica terreno…"));
        Press(button, host.Root);
        await host.Settle();
        Check(shown is not null && copied is { Module: "geo_palo_verticale" } && copied.Value.Name.StartsWith("Terreno da ", StringComparison.Ordinal) && host.Modified == 0,
            "«Invia / carica terreno…»: dialogo condiviso, copia inviata alla finestra (" + copied?.Name + ")");
        services.SoilTransfer = dialog => { dialog.ApplyCommand.Execute(null); return dialog.Result; };
        Press(button, host.Root);
        await host.Settle();
        Check(replaced is not null && replaced.S("family") == "cantilever" && services.Soils == 2, "«Invia / carica terreno…»: sostituzione del terreno inviata alla finestra");
        host.Editor.InspectRevision(); await host.Settle();
        Check(!button.IsEnabled, "«Invia / carica terreno…» spento in sola lettura, come il pulsante storico protetto");
    }

    // ---------------------------------------------------------------- 9. bench and Release

    private static void Bench(string root)
    {
        var editors = Editors(new Services());
        using (var mvvm = editors.Open(Module, RetainingWallScenarios.FixtureData(root, "default")))
            Check(mvvm is MvvmSheetEditor { Sheet: RetainingWallSheetViewModel sheet } && sheet.Forms.Values.All(f => f.WikiNavigator is not null), "Con --module-impl il banco apre la vista MVVM dei muri");
        using (var release = ModuleHostBench.SheetEditors([], DesktopComposition.CreateModules()).Open(Module, RetainingWallScenarios.FixtureData(root, "default")))
            Check(release is MvvmSheetEditor { Sheet: RetainingWallSheetViewModel } && ModuleImplementations.Release.For(Module) == SheetImplementation.Mvvm,
                "La Release apre i muri con la vista MVVM (passaggio del 23/10/2026)");
    }
}
