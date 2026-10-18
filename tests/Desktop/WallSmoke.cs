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
/// f5-suite-ui: the retaining wall as ui/smoke-retaining-wall and ui/smoke-global-stability drive it, with either view: the historic
/// RetainingWallWorkspace (<see cref="HistoricWallSmoke"/>, the members the suites used) or the XAML view hosted by MvvmSheetEditor
/// (<see cref="MvvmWallSmoke"/>: the controls of RetainingWallSheetView as the user uses them, read back from the view and its ViewModel, as
/// RetainingWallViewChecks). Forms and fields are named as in the historic view («form», «campo»); the tables of the inputs as in the traces
/// of F5.9 («valle», «monte», «azioni», «matrice», «matrice-globale»). At the Release switch of the walls the historic implementation goes away.
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

/// <summary>f5-suite-ui: the historic RetainingWallWorkspace, with the members the wall suites used before (unchanged behaviour).</summary>
internal sealed class HistoricWallSmoke(RetainingWallWorkspace w) : WallSmoke
{
    private JsonGrid Grid(string table) => table switch
    {
        "valle" => w.ValleyLayerGrid, "monte" => w.LayerGrid, "azioni" => w.ActionGrid, "matrice" => w.MatrixGrid, "matrice-globale" => w.GlobalMatrix,
        _ => throw new ArgumentException("Tabella sconosciuta: " + table)
    };

    internal override JsonObject Data => w.Data;
    internal override WallModel.Result? Calculation => w.Calculation;
    internal override SlopeResult? GlobalResult => w.GlobalResult;
    internal override int AvailableFamilies => w.Family.Items.Cast<ComboBoxItem>().Count(i => i.IsEnabled);
    internal override int Rows(string table) => Grid(table).Rows.Count;
    internal override JsonObject Row(string table, int row) => Grid(table).Rows[row].Values;
    internal override void SetCell(string table, int row, string key, object value) => Grid(table).Rows[row][key] = value;
    internal override void Set(string form, string key, string value) => w.Forms[form].Set(key, value);
    internal override string Get(string form, string key) => w.Forms[form].Get(key);
    internal override void Tick(string form, string key, bool value) => ((CheckBox)w.Forms[form].Editors[key]).IsChecked = value;
    internal override bool IsVisible(string form, string key) => w.Forms[form].Editors[key].IsVisible;
    internal override void BringIntoView(string form, string key) => w.Forms[form].Editors[key].BringIntoView();
    internal override void BringFormIntoView(string form) => w.Forms[form].BringIntoView();
    internal override Task CalculateAsync() => w.CalculateAsync();
    internal override Task StartCalculation() => w.CalculateAsync();
    internal override Task CalculateGlobalAsync() => w.CalculateGlobalAsync();
    internal override void Example(string family) => w.Example(family);
    internal override void AddAction(string type) => w.AddAction(type);
    internal override void GenerateMatrix() => w.GenerateMatrix();
    internal override void PrepareGlobal() => w.PrepareGlobal();
    internal override void GenerateGlobalMatrix() => w.GenerateGlobalMatrix();
    internal override void EnableTwoZones() { w.Forms["zones"].Set("two_zones", "true"); w.Data["reinforcement"]!["two_zones"] = true; }
    internal override void BringCardIntoView(string card) => w.Cards[card].BringIntoView();
    internal override bool IsExpanded(string card) => w.Cards[card].IsExpanded;
    internal override void Expand(string card) => w.Cards[card].IsExpanded = true;
    internal override void BringTextIntoView(string card, string prefix) => Ui.Descendants<TextBlock>(w.Cards[card]).Single(t => t.Text.StartsWith(prefix)).BringIntoView();
    internal override void BringGlobalLayersIntoView() => w.GlobalLayers.BringIntoView();
    internal override int Page { get => w.Pages.SelectedIndex; set => w.Pages.SelectedIndex = value; }
    internal override int PageCount => w.Pages.Items.Count;
    internal override void ShowSeismic() => w.SeismicButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    internal override string SeismicButtonText => w.SeismicButton.Content.ToString()!;
    internal override string? Combination { set => w.Combination.SelectedItem = value; }
    internal override string? ViewMode { set => w.ViewMode.SelectedItem = value; }
    internal override string? CheckFilter { get => w.CheckFilter.SelectedItem as string; set => w.CheckFilter.SelectedItem = value; }
    internal override int Inspections => w.Diagrams.InspectionCount;
    internal override void SelectResultRow(int row) => w.SelectedResultsGrid!.SelectedIndex = row;
    internal override bool CanSendSection => w.SendSectionButton.IsEnabled;
    internal override void SendSection() => w.SendSectionButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    internal override FrameworkElement Drawing => w.Drawing;
    internal override FrameworkElement GlobalDrawing => w.GlobalDrawing;
    internal override string? GlobalDrawingCase => w.GlobalDrawing.Case?.Factors.Name;
    internal override int GlobalCombinations => w.GlobalCombination.Items.Count;
    internal override int GlobalCombinationIndex { set => w.GlobalCombination.SelectedIndex = value; }
    internal override string? SelectedGlobalCombination => w.GlobalCombination.SelectedItem as string;

    internal override async Task<bool> ApplyParameterAsync(string directory, string path, string value)
    {
        var parameterWindow = w.CalculationParametersWindow(); parameterWindow.Show();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); parameterWindow.UpdateLayout();
        File.WriteAllBytes(Path.Combine(directory, "valori-di-calcolo.png"), Ui.Snapshot(parameterWindow));
        var parameterGrid = Ui.Descendants<JsonGrid>(parameterWindow).First(); parameterGrid.Rows.Single(row => row.Values.S("path") == path)["value"] = value;
        Ui.Descendants<Button>(parameterWindow).Single(b => b.Content?.ToString() == "Applica e ricalcola").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        for (int wait = 0; parameterWindow.IsVisible && wait < 150; wait++) await Task.Delay(100);
        return !parameterWindow.IsVisible;
    }

    internal override async Task SendSoilAsync(string directory)
    {
        Exception? transferError = null;
        _ = w.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            var dialog = Application.Current.Windows.OfType<SoilTransferDialog>().Single();
            try { File.WriteAllBytes(Path.Combine(directory, "trasferisci-terreno.png"), Ui.Snapshot(dialog)); dialog.SendButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
            catch (Exception ex) { transferError = ex; dialog.DialogResult = false; }
        }));
        w.SoilButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); if (transferError is not null) throw transferError;
        await Task.CompletedTask;
    }

    internal override string[] ReportFigureCaptions(bool global) => WallAdvancedChecks.ReportFigureCaptions(w, global);
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
        _ => throw new ArgumentException("Tabella sconosciuta: " + table)
    });

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
