using System.IO;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Threading;
using Anthea.Application.Walls;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules.RetainingWall;
using Anthea.Presentation.Modules.Shared;
using X.Core;
using X.Desktop.Views.Common;
using X.Desktop.Views.RetainingWall;

namespace X.Desktop;

/// <summary>
/// f5-suite-ui: the retaining wall as ui/smoke-retaining-wall and ui/smoke-global-stability drive it: the XAML view hosted by MvvmSheetEditor
/// (<see cref="MvvmWallSmoke"/>: the controls of RetainingWallSheetView as the user uses them, read back from the view and its ViewModel, as
/// RetainingWallViewChecks). Forms and fields are named as in the historic view («form», «campo»); the tables of the inputs as in the traces
/// of F5.9 («valle», «monte», «azioni», «matrice», «matrice-globale»). The historic implementation (HistoricWallSmoke on RetainingWallWorkspace)
/// went away with the Release switch of the walls (f5-pass-muri, 23/10/2026; the view is archived in supporto/SUPERATI/ui-storica/muri).
/// </summary>
internal abstract class WallSmoke
{
    internal abstract JsonObject Data { get; }
    internal abstract WallModel.Result? Calculation { get; }
    internal abstract SlopeResult? GlobalResult { get; }

    /// <summary>The typologies that can be chosen.</summary>
    internal abstract int AvailableFamilies { get; }

    internal abstract int Rows(string table);
    internal abstract JsonObject Row(string table, int row);
    /// <summary>The user writes a cell (text) or ticks it (bool).</summary>
    internal abstract void SetCell(string table, int row, string key, object value);

    /// <summary>The user types a text field or chooses an item of a choice field (InputForm.Set of the historic view).</summary>
    internal abstract void Set(string form, string key, string value);
    /// <summary>The raw text of a text field (InputForm.Get).</summary>
    internal abstract string Get(string form, string key);
    /// <summary>The user ticks or clears a check box of a form.</summary>
    internal abstract void Tick(string form, string key, bool value);
    /// <summary>The editor of the field is visible.</summary>
    internal abstract bool IsVisible(string form, string key);
    internal abstract void BringIntoView(string form, string key);
    internal abstract void BringFormIntoView(string form);

    /// <summary>«Calcola»/«Ricalcola»: the calculation at once, awaited.</summary>
    internal abstract Task CalculateAsync();
    /// <summary>«Ricalcola» without waiting: the task of the calculation started.</summary>
    internal abstract Task StartCalculation();
    /// <summary>«Calcola globale», awaited.</summary>
    internal abstract Task CalculateGlobalAsync();

    /// <summary>«Mensola esempio» / «Gravità esempio».</summary>
    internal abstract void Example(string family);
    internal abstract void AddAction(string type);
    /// <summary>«Genera / ripristina automatiche».</summary>
    internal abstract void GenerateMatrix();
    internal abstract void PrepareGlobal();
    internal abstract void GenerateGlobalMatrix();
    /// <summary>Two zones of reinforcement in the stem.</summary>
    internal abstract void EnableTwoZones();

    internal abstract void BringCardIntoView(string card);
    internal abstract bool IsExpanded(string card);
    internal abstract void Expand(string card);
    /// <summary>The text of a card starting with <paramref name="prefix"/> brought into view.</summary>
    internal abstract void BringTextIntoView(string card, string prefix);
    internal abstract void BringGlobalLayersIntoView();

    internal abstract int Page { get; set; }
    internal abstract int PageCount { get; }
    /// <summary>The «Sisma» button of the results.</summary>
    internal abstract void ShowSeismic();
    internal abstract string SeismicButtonText { get; }

    internal abstract string? Combination { set; }
    internal abstract string? ViewMode { set; }
    internal abstract string? CheckFilter { get; set; }
    /// <summary>The points of the diagrams that answer the pointer.</summary>
    internal abstract int Inspections { get; }
    /// <summary>A row of the table of the selected results.</summary>
    internal abstract void SelectResultRow(int row);
    internal abstract bool CanSendSection { get; }
    /// <summary>«Apri sezione in c.a.».</summary>
    internal abstract void SendSection();

    internal abstract FrameworkElement Drawing { get; }
    internal abstract FrameworkElement GlobalDrawing { get; }
    internal abstract string? GlobalDrawingCase { get; }
    internal abstract int GlobalCombinations { get; }
    internal abstract int GlobalCombinationIndex { set; }
    internal abstract string? SelectedGlobalCombination { get; }

    /// <summary>
    /// «Valori di calcolo…»: the window of the values (screenshot <c>valori-di-calcolo.png</c>), the value of <paramref name="path"/> changed,
    /// «Applica e ricalcola»; true when the window closed within 15 s.
    /// </summary>
    internal abstract Task<bool> ApplyParameterAsync(string directory, string path, string value);

    /// <summary>«Invia / carica terreno…» → «Invia a un nuovo foglio» with the proposed destination (screenshot <c>trasferisci-terreno.png</c>).</summary>
    internal abstract Task SendSoilAsync(string directory);

    /// <summary>The captions of the figures of the Word report (WallAdvancedChecks.ReportFigureCaptions).</summary>
    internal abstract string[] ReportFigureCaptions(bool global);
}

/// <summary>f5-suite-ui: the XAML view of the walls (RetainingWallSheetView) driven through its controls, as RetainingWallViewChecks drives it.</summary>
internal sealed class MvvmWallSmoke : WallSmoke
{
    private readonly MvvmSheetEditor editor;
    private readonly BenchSheetServices services;

    internal MvvmWallSmoke(MvvmSheetEditor editor, BenchSheetServices services)
    {
        this.editor = editor; this.services = services;
        Sheet = (RetainingWallSheetViewModel)editor.Sheet;
    }

    /// <summary>The open sheet is a retaining wall with the XAML view.</summary>
    internal static bool Handles(ISheetEditor? editor) => editor is MvvmSheetEditor { Sheet: RetainingWallSheetViewModel };

    internal RetainingWallSheetViewModel Sheet { get; }

    /// <summary>The view of the sheet; laid out first when the window has not shown it yet (a sheet just opened).</summary>
    private RetainingWallSheetView View
    {
        get
        {
            RetainingWallSheetView? Find() => RetainingWallViewChecks.Walk(editor.View, popups: false).OfType<RetainingWallSheetView>().SingleOrDefault();
            if (Find() is { } view) return view;
            editor.View.UpdateLayout();
            return Find() ?? throw new InvalidOperationException("Vista XAML del muro non realizzata.");
        }
    }

    private IEnumerable<DependencyObject> All => RetainingWallViewChecks.Walk(View, popups: false);

    private void Layout() { editor.View.UpdateLayout(); }

    private T Named<T>(string name) where T : FrameworkElement => (T)(View.FindName(name) ?? throw new InvalidOperationException("Controllo della vista assente: " + name));

    private static string? BoundPath(DependencyObject element, DependencyProperty property) => BindingOperations.GetBinding(element, property)?.Path?.Path;

    /// <summary>A click of the user (ButtonBase.OnClick: the Click event, then the command), only on a usable button.</summary>
    private static void Press(ButtonBase? button)
    {
        if (button is null || !button.IsEnabled) throw new InvalidOperationException("Pulsante non utilizzabile: " + button?.Content);
        typeof(ButtonBase).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(button, null);
    }

    private Button Caption(string caption) => All.OfType<Button>().First(b => Equals(b.Content, caption));

    private Button CommandButton(string command) => All.OfType<Button>().First(b => BoundPath(b, ButtonBase.CommandProperty) == command);

    /// <summary>The editor of a field: the control of FieldForm bound to the FieldViewModel of the sheet, built also in a closed group.</summary>
    private T Editor<T>(string form, string key) where T : FrameworkElement
    {
        var field = Sheet.Forms[form][key];
        T? Find() => All.OfType<T>().FirstOrDefault(e => ReferenceEquals(e.DataContext, field) && e.TemplatedParent is not ComboBox);
        if (Find() is { } editor) return editor;
        for (int pass = 0; pass < 3; pass++)
        {
            RetainingWallViewChecks.Realize(View); Layout();
            if (Find() is { } built) return built;
        }
        throw new InvalidOperationException("Campo della vista assente: " + form + "/" + key);
    }

    private FrameworkElement EditorOf(string form, string key)
    {
        var field = Sheet.Forms[form][key];
        return field.Kind switch { FieldKind.Choice => Editor<ComboBox>(form, key), FieldKind.Bool => Editor<CheckBox>(form, key), _ => Editor<TextBox>(form, key) };
    }

    private WallInputGrid Grid(string table) => Named<WallInputGrid>(table switch
    {
        "valle" => "ValleyGrid", "monte" => "MountainGrid", "azioni" => "ActionGrid", "matrice" => "MatrixGrid", "matrice-globale" => "GlobalMatrixGrid",
        "globale-monte" => "GlobalRearGrid", "globale-valle" => "GlobalValleyGrid",
        _ => throw new ArgumentException("Tabella sconosciuta: " + table)
    });

    // ---- f5-pass-muri: what the offscreen checks of the archived RetainingWallWorkspace read and drove (OffscreenWall)

    /// <summary>The view of the sheet.</summary>
    internal RetainingWallSheetView SheetView => View;

    /// <summary>The user selects a row of a table of the inputs.</summary>
    internal void SelectRow(string table, int row) => Grid(table).SelectedIndex = row;

    /// <summary>A click on the button bound to <paramref name="command"/> (with <paramref name="parameter"/>), built also in a closed group.</summary>
    internal void Command(string command, string? parameter = null)
    {
        RetainingWallViewChecks.Realize(View); Layout();
        Press(All.OfType<Button>().First(b => BoundPath(b, ButtonBase.CommandProperty) == command && Equals(b.CommandParameter, parameter)));
    }

    /// <summary>A click on the button with the caption, built also in a closed group or in a panel of results.</summary>
    internal void Click(string caption) { RetainingWallViewChecks.Realize(View); Layout(); Press(Caption(caption)); }

    /// <summary>The caption of the button bound to <paramref name="command"/>.</summary>
    internal string? CommandCaption(string command) { RetainingWallViewChecks.Realize(View); return CommandButton(command).Content as string; }

    /// <summary>The state of a check box of a form.</summary>
    internal bool? IsChecked(string form, string key) => Editor<CheckBox>(form, key).IsChecked;

    /// <summary>The user can change the field (a text field neither read-only nor disabled).</summary>
    internal bool IsEditable(string form, string key) => EditorOf(form, key) is TextBox text ? !text.IsReadOnly && text.IsEnabled : EditorOf(form, key).IsEnabled;

    /// <summary>The FieldForm of a form, built also in a closed group.</summary>
    internal FieldForm FormView(string form)
    {
        var model = Sheet.Forms[form];
        RetainingWallViewChecks.Realize(View); Layout();
        return All.OfType<FieldForm>().First(f => ReferenceEquals(f.DataContext, model));
    }

    /// <summary>The calculations and the searches of the sheet finished (the work of a button that returns at once), within 120 s.</summary>
    internal async Task WaitIdle()
    {
        for (int i = 0; i < 2400; i++)
        {
            await Sheet.PendingWork.WaitAsync(TimeSpan.FromSeconds(120));
            if (!Sheet.IsBusy && Sheet.PendingWork.IsCompleted) return;
            await Task.Delay(50);
        }
        throw new TimeoutException("Foglio dei muri ancora occupato dopo 120 s.");
    }

    private JsonRowViewModel GridRow(string table, int row) => (JsonRowViewModel)Grid(table).Items[row];

    private Expander Card(string card) => All.OfType<Expander>().First(e => ReferenceEquals(e.DataContext, Sheet.Cards[card]));

    internal override JsonObject Data => editor.Data;
    internal override WallModel.Result? Calculation => Sheet.Calculation;
    internal override SlopeResult? GlobalResult => Sheet.GlobalResult;
    internal override int AvailableFamilies => View.FamilyChoice.Items.Cast<WallFamily>().Count(f => f.Available);
    internal override int Rows(string table) => Grid(table).Items.Count;
    internal override JsonObject Row(string table, int row) => GridRow(table, row).Values;

    /// <summary>The bindings of the cells write the row at every keystroke (UpdateSourceTrigger=PropertyChanged), as RetainingWallViewChecks writes them.</summary>
    internal override void SetCell(string table, int row, string key, object value)
    {
        var grid = Grid(table);
        if (grid.IsReadOnly || grid.Columns.Single(c => c.SortMemberPath == key).IsReadOnly) throw new InvalidOperationException("Cella non modificabile: " + table + "/" + key);
        GridRow(table, row)[key] = value;
    }

    internal override void Set(string form, string key, string value)
    {
        switch (EditorOf(form, key))
        {
            case ComboBox choice: choice.SelectedItem = value; break;
            case TextBox text when text.DataContext is FieldViewModel typed:
                // The user focuses the field (the raw text replaces the formatted one), types and leaves, as FieldForm reports the focus.
                typed.IsFocused = true; text.Text = value; typed.IsFocused = false; break;
            default: throw new InvalidOperationException("Campo non digitabile: " + form + "/" + key);
        }
    }

    internal override string Get(string form, string key) => Sheet.Forms[form].Get(key);
    internal override void Tick(string form, string key, bool value) => Editor<CheckBox>(form, key).IsChecked = value;
    internal override bool IsVisible(string form, string key) { Layout(); return EditorOf(form, key).IsVisible; }
    internal override void BringIntoView(string form, string key) => EditorOf(form, key).BringIntoView();
    internal override void BringFormIntoView(string form)
    {
        var model = Sheet.Forms[form];
        RetainingWallViewChecks.Realize(View); Layout();
        All.OfType<FieldForm>().First(f => ReferenceEquals(f.DataContext, model)).BringIntoView();
    }

    internal override async Task CalculateAsync()
    {
        Press(Caption("Ricalcola"));
        await Sheet.PendingWork.WaitAsync(TimeSpan.FromSeconds(120));
    }

    internal override Task StartCalculation() { Press(Caption("Ricalcola")); return Sheet.PendingWork; }

    internal override async Task CalculateGlobalAsync()
    {
        RetainingWallViewChecks.Realize(View);
        Press(CommandButton("CalculateGlobalCommand"));
        await Sheet.PendingWork.WaitAsync(TimeSpan.FromSeconds(120));
    }

    internal override void Example(string family) => Press(Caption(family == "gravity" ? "Gravità esempio" : "Mensola esempio"));

    internal override void AddAction(string type)
    {
        RetainingWallViewChecks.Realize(View);
        Named<ComboBox>("ActionType").SelectedItem = type;
        Press(Caption("+ Azione"));
    }

    internal override void GenerateMatrix() { RetainingWallViewChecks.Realize(View); Press(CommandButton("GenerateMatrixCommand")); }
    internal override void PrepareGlobal() { RetainingWallViewChecks.Realize(View); Press(CommandButton("PrepareGlobalCommand")); }
    internal override void GenerateGlobalMatrix() { RetainingWallViewChecks.Realize(View); Press(CommandButton("GenerateGlobalMatrixCommand")); }
    internal override void EnableTwoZones() => Tick("zones", "two_zones", true);
    internal override void BringCardIntoView(string card) => Card(card).BringIntoView();
    internal override bool IsExpanded(string card) => Card(card).IsExpanded;
    internal override void Expand(string card) => Card(card).IsExpanded = true;
    internal override void BringTextIntoView(string card, string prefix)
    {
        Layout();
        RetainingWallViewChecks.Walk(Card(card), popups: false).OfType<TextBlock>().Single(t => t.Text.StartsWith(prefix)).BringIntoView();
    }

    internal override void BringGlobalLayersIntoView() => View.GlobalRearGrid.BringIntoView();
    internal override int Page { get => View.Pages.SelectedIndex; set => View.Pages.SelectedIndex = value; }
    internal override int PageCount => View.Pages.Items.Count;
    internal override void ShowSeismic() => Press(View.SeismicButton);
    internal override string SeismicButtonText => View.SeismicButton.Content as string ?? "";
    internal override string? Combination { set => View.CombinationChoice.SelectedItem = value; }
    internal override string? ViewMode { set => View.ViewChoice.SelectedItem = value; }
    internal override string? CheckFilter { get => View.FilterChoice.SelectedItem as string; set => View.FilterChoice.SelectedItem = value; }
    internal override int Inspections => View.DiagramsDrawing.InspectionCount;

    internal override void SelectResultRow(int row)
    {
        RetainingWallViewChecks.Realize(View);
        RetainingWallViewChecks.Walk(View.ChecksPanel, popups: false).OfType<WallBlockGrid>().First().SelectedIndex = row;
    }

    internal override bool CanSendSection => View.SendSectionButton.IsEnabled;
    internal override void SendSection() => Press(View.SendSectionButton);
    internal override FrameworkElement Drawing => View.InputDrawing;
    internal override FrameworkElement GlobalDrawing => View.GlobalDrawing;
    internal override string? GlobalDrawingCase => View.GlobalDrawing.Case?.Factors.Name;
    internal override int GlobalCombinations => View.GlobalCombinationChoice.Items.Count;
    internal override int GlobalCombinationIndex { set => View.GlobalCombinationChoice.SelectedIndex = value; }
    internal override string? SelectedGlobalCombination => View.GlobalCombinationChoice.SelectedItem as string;

    /// <summary>The view of a dialog of the sheet in a window outside the screen, as WpfDialogService shows it (the DataTemplate of its ViewModel).</summary>
    private static Window DialogWindow(object dialog, string title)
    {
        var window = new Window
        {
            Title = title, Content = dialog, SizeToContent = SizeToContent.WidthAndHeight, Left = -32000, Top = -32000,
            WindowStartupLocation = WindowStartupLocation.Manual, ShowActivated = false, ShowInTaskbar = false
        };
        window.SetValue(NumericPresentation.EnabledProperty, true);
        Appearance.Watch(window);
        window.Show(); window.UpdateLayout();
        return window;
    }

    internal override async Task<bool> ApplyParameterAsync(string directory, string path, string value)
    {
        bool closed = false;
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        services.DialogAsync = async dialog =>
        {
            var parameters = (RetainingWallParametersViewModel)dialog;
            var window = DialogWindow(parameters, parameters.Title);
            try
            {
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout();
                File.WriteAllBytes(Path.Combine(directory, "valori-di-calcolo.png"), Ui.Snapshot(window));
                var view = RetainingWallViewChecks.Walk(window, popups: false).OfType<RetainingWallParametersView>().Single();
                var rows = (WallInputGrid)view.FindName("RowsGrid");
                ((JsonRowViewModel)rows.Items.Cast<JsonRowViewModel>().Single(r => r.Values.S("path") == path))["value"] = value;
                Press(RetainingWallViewChecks.Walk(view, popups: false).OfType<Button>().Single(b => Equals(b.Content, "Applica e ricalcola")));
                for (int wait = 0; !parameters.IsClosed && wait < 150; wait++) await Task.Delay(100);
                closed = parameters.IsClosed;
                return parameters.Result;
            }
            finally { window.Close(); opened.TrySetResult(); }
        };
        try
        {
            Press(Caption("Valori di calcolo…"));
            await opened.Task.WaitAsync(TimeSpan.FromSeconds(120));
        }
        finally { services.DialogAsync = null; }
        return closed;
    }

    internal override async Task SendSoilAsync(string directory)
    {
        services.Dialog = dialog =>
        {
            var transfer = (SoilTransferDialogViewModel)dialog;
            var window = DialogWindow(transfer, transfer.Title);
            try
            {
                File.WriteAllBytes(Path.Combine(directory, "trasferisci-terreno.png"), Ui.Snapshot(window));
                transfer.SendCommand.Execute(null);
                return transfer.Result;
            }
            finally { window.Close(); }
        };
        try { Press(Caption("Invia / carica terreno…")); await Task.Yield(); }
        finally { services.Dialog = null; }
    }

    internal override string[] ReportFigureCaptions(bool global) =>
        WallAdvancedChecks.ReportFigureCaptions(new RetainingWallBarDrawing(Sheet.Calculation!.Input, Sheet.CreateBarSchedule()).PageCount, global);
}

/// <summary>
/// f5-pass-muri: a sheet of the walls opened by the factory of the bench with the XAML view (as the Release opens it since the switch), in a
/// window outside the screen never activated (no mouse, no keyboard of the user), driven through <see cref="MvvmWallSmoke"/>: the offscreen
/// checks that drove the archived RetainingWallWorkspace (GlobalGuidanceChecks, WallAdvancedChecks, WallMaterialChecks, ReportAppearanceChecks).
/// </summary>
internal sealed class OffscreenWall : IDisposable
{
    private readonly Window window;
    internal MvvmSheetEditor Editor { get; }
    internal MvvmWallSmoke Wall { get; }
    internal RetainingWallSheetViewModel Sheet => Wall.Sheet;
    internal RetainingWallSheetView View => Wall.SheetView;

    private OffscreenWall(MvvmSheetEditor editor, BenchSheetServices services, double width, double height)
    {
        Editor = editor;
        Wall = new MvvmWallSmoke(editor, services);
        window = new Window
        {
            Content = editor.View, Width = width, Height = height, Left = -32000, Top = -32000, WindowStartupLocation = WindowStartupLocation.Manual,
            ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, Title = "Muro fuori schermo"
        };
        Appearance.Watch(window);
        window.Show();
        window.UpdateLayout();
    }

    /// <summary>Opens the sheet on <paramref name="data"/> (a copy) and waits for the automatic calculation of the opening.</summary>
    internal static async Task<OffscreenWall> OpenAsync(JsonObject data, double width = 1600, double height = 1000)
    {
        var factory = ModuleHostBench.SheetEditors([]);
        var editor = factory.Open(WallModel.Module, data) as MvvmSheetEditor ?? throw new InvalidOperationException("Il muro non si apre con la vista XAML.");
        var host = new OffscreenWall(editor, ModuleHostBench.ScriptedServices(factory)!, width, height);
        await host.Wall.WaitIdle();
        await host.Settle();
        return host;
    }

    /// <summary>Every window of the application outside the screen and never activated: nothing on the desktop of the user.</summary>
    internal static bool NothingOnScreen => Application.Current.Windows.Cast<Window>().All(w => !w.ShowActivated && !w.ShowInTaskbar
        && (w.Left + w.ActualWidth <= SystemParameters.VirtualScreenLeft || w.Top + w.ActualHeight <= SystemParameters.VirtualScreenTop));

    internal async Task Settle()
    {
        window.UpdateLayout();
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
        window.UpdateLayout();
    }

    /// <summary>The sheet laid out at <paramref name="width"/>×<paramref name="height"/>, as an image.</summary>
    internal async Task<byte[]> Png(double width, double height)
    {
        window.Width = width; window.Height = height;
        await Settle();
        return Ui.Snapshot((FrameworkElement)window.Content);
    }

    public void Dispose()
    {
        window.Content = null;
        window.Close();
        Editor.Dispose();
    }
}
