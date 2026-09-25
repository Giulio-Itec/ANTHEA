using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;

internal sealed partial class BridgeWorkspace
{
    private readonly List<UIElement> pageInputs = [];
    private readonly ContentControl inputHost = new();
    private readonly TextBlock inputTitle = Ui.Text("", 15, true), inputSubtitle = Ui.Text("", 11, color: Ui.Muted);
    private readonly VerificationCards summaryCards = new();
    private readonly ProgressBar progress = new() { Height = 3, IsIndeterminate = true, Visibility = Visibility.Collapsed };
    private readonly TextBlock resultNotice = Ui.Text("", 12, true, Ui.Brush("#8B5916"));
    private UIElement stageControls = null!;
    internal readonly CheckBox GeometryLabels = new() { Content = "Quote sezione", Margin = new Thickness(8, 6, 4, 4) }, RebarLabels = new() { Content = "Info armature", Margin = new Thickness(8, 6, 4, 4) };
    private JsonObject viewSettings = null!;
    internal readonly Dictionary<string, Grid> Splits = new();
    private int currentPage = -1;
    private bool selectingPage;
    internal ViewportFrame Viewport { get; private set; } = null!;

    private static Expander Group(string title, UIElement body, bool expanded = false) => new()
    {
        Header = Ui.Text(title, 14, true), Content = body, IsExpanded = expanded,
        Padding = new Thickness(0, 8, 0, 10), Margin = new Thickness(0, 4, 0, 4)
    };
    private static Border Panel(string title, UIElement body, string? subtitle = null)
    {
        var heading = Ui.Stack(Ui.Text(title, 15, true)); heading.Margin = new Thickness(0, 0, 0, 10);
        if (subtitle is not null) { var text = Ui.Text(subtitle, 11, color: Ui.Muted); text.Margin = new Thickness(0, 5, 0, 0); heading.Children.Add(text); }
        return Ui.Paper(Ui.Dock(body, heading), 14);
    }
    private static Border Notice(string text)
    {
        var border = Ui.Paper(Ui.Text(text, 11, color: Ui.Brush("#865D16")), 10);
        border.Background = Ui.Brush("#FFF6DD"); border.BorderBrush = Ui.Brush("#EEDCAF"); border.Margin = new Thickness(0, 8, 0, 8); return border;
    }
    private void BuildLayout()
    {
        if (Data["ui_mista"] is not JsonObject) Data["ui_mista"] = new JsonObject();
        viewSettings = Data["ui_mista"]!.AsObject();
        followLatestStage = viewSettings.B("segui_ultima_fase", !viewSettings.ContainsKey("fase"));
        MigrateViewSettings();
        if (viewSettings.D("layout_version") < 4)
        {
            foreach (string key in new[] { "risultato_0", "risultato_1" })
                if (viewSettings.ContainsKey(key)) viewSettings[key] = new[] { 3, 1, 2 }[Math.Clamp((int)viewSettings.D(key), 0, 2)];
            viewSettings["layout_version"] = 4;
        }
        BuildSectionProperties();
        foreach (var (number, title) in new[] { ("01", "Pannello di controllo"), ("02", "Fasi e tensioni") })
            Pages.Items.Add(new TabItem { Header = Ui.Bar(Ui.Text(number, 11, true, Ui.Muted), Ui.Text("  " + title, 14, true)), Padding = new Thickness(14, 9, 14, 9) });

        inputSubtitle.Margin = new Thickness(0, 5, 0, 10);
        var selectionLabel = Ui.Text("Risultati cumulati fino alla fase", 12, true); selectionLabel.Margin = new Thickness(0, 3, 0, 3);
        StageChoice.HorizontalAlignment = HorizontalAlignment.Stretch; StageChoice.MaxWidth = double.PositiveInfinity;
        stageControls = Ui.Stack(selectionLabel, StageChoice, Ui.Button("Mostra tutte le fasi → ultima situazione", () =>
        { followLatestStage = true; StageChoice.SelectedIndex = StageChoice.Items.Count - 1; ViewChanged(); }),
            Ui.Text("Grafico e risultati includono le fasi attive fino alla situazione scelta.", 11, color: Ui.Muted));
        var inputHeading = Ui.Stack(inputTitle, inputSubtitle, stageControls); inputHeading.Margin = new Thickness(0, 0, 0, 10);
        var left = Ui.Paper(Ui.Dock(inputHost, inputHeading), 14);

        Drawing.MinHeight = 160;
        Viewport = new ViewportFrame("Sezione composta · geometria e tensioni", Drawing, Drawing.ResetView);
        DisplayChoice.Width = 170; Viewport.Toolbar.Children.Insert(0, DisplayChoice);
        Viewport.Toolbar.Children.Add(Ui.Button("−", () => Drawing.ZoomBy(.85), inspection: true));
        Viewport.Toolbar.Children.Add(Ui.Button("+", () => Drawing.ZoomBy(1.15), inspection: true));
        Viewport.Toolbar.Children.Add(GeometryLabels); Viewport.Toolbar.Children.Add(RebarLabels);
        BuildStressScaleControls(); BuildContourControls();
        foreach (var toggle in new[] { GeometryLabels, RebarLabels })
        {
            toggle.Checked += (_, _) => LabelOptionsChanged(); toggle.Unchecked += (_, _) => LabelOptionsChanged();
        }
        resultNotice.Margin = new Thickness(10, 6, 10, 6);
        overview.Margin = new Thickness(0, 12, 0, 10);
        warnings.Margin = new Thickness(0, 8, 0, 0);
        analysisSidebar = Panel("Riepilogo della situazione", Scroll(Ui.Stack(summaryCards, overview,
            Ui.Text("Rapporti locali σ/limite · non rappresentano la verifica completa del ponte.", 11, color: Ui.Muted), warnings,
            Ui.Button("Dettagli sezione efficace ↓", () => Results.SelectedIndex = 2, inspection: true))), "Compressione − · trazione +");
        sidebarHost.Content = analysisSidebar;
        var sectionWithElevation = Ui.Dock(Viewport, bottom: BuildDetailSketch());
        var upper = Split(sectionWithElevation, sidebarHost, "risultati", false, .7, 460, 250);
        var resultActions = Ui.Bar(Ui.Button("Esporta CSV…", ExportCsv, inspection: true));
        var table = Panel("Sollecitazioni e risultati", Ui.Dock(Results, bottom: resultActions), "Sollecitazioni: tutte le fasi · risultati: situazione selezionata · mm, MPa, kN, kNm");
        var right = Split(upper, table, "righe", true, .56, 290, 250);
        var layout = Split(left, right, "ingressi", false, .29, 285, 710);
        layout.Margin = new Thickness(12, 10, 12, 0);
        var footer = new DockPanel { Margin = new Thickness(16, 4, 16, 8) }; footer.Children.Add(status);
        var header = new DockPanel();
        var information = Ui.Button("Info modello…", ShowModelInformation, inspection: true); information.Margin = new Thickness(8, 8, 16, 0);
        information.VerticalAlignment = VerticalAlignment.Center; DockPanel.SetDock(information, Dock.Right);
        header.Children.Add(information); header.Children.Add(Pages);
        var body = Ui.Dock(layout, Ui.Stack(header, resultNotice), Ui.Stack(progress, footer));
        var scroll = new ChainedScrollViewer { Content = body, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Content = scroll;
        void Resize()
        {
            body.Width = Math.Max(1120, double.IsFinite(scroll.ViewportWidth) ? scroll.ViewportWidth : 0);
            body.Height = Math.Max(700, double.IsFinite(scroll.ViewportHeight) ? scroll.ViewportHeight : 0);
        }
        scroll.SizeChanged += (_, _) => Resize();
        scroll.ScrollChanged += (_, e) => { if (e.Source == scroll && (e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0)) Resize(); };
        Pages.SelectedIndex = Math.Clamp((int)viewSettings.D("tab"), 0, 1);
        SelectPage();
        Pages.SelectionChanged += (_, e) => { if (ReferenceEquals(e.Source, Pages) && !building) { Commit(); SelectPage(); Modified?.Invoke(); } };
    }
    private void MigrateViewSettings()
    {
        if (viewSettings.D("layout_version") >= 3) return;
        if (!viewSettings.ContainsKey("tab") && !viewSettings.ContainsKey("layout_version"))
        { viewSettings["layout_version"] = 3; return; }
        if (viewSettings.D("layout_version") < 2)
        {
            var previous = (JsonObject)viewSettings.DeepClone();
            int selected = Math.Clamp((int)previous.D("tab"), 0, 3);
            // The two former phase pages share one page now; retain the active one's view.
            int[] sources = [0, selected == 2 ? 2 : 1, 3];
            int[] resultMap = [0, 1, 2, 1, 2, 2];
            for (int page = 0; page < sources.Length; page++)
            {
                int oldPage = sources[page];
                if (previous["vista_" + oldPage] is { } view) viewSettings["vista_" + page] = view.DeepClone();
                else viewSettings.Remove("vista_" + page);
                int result = Math.Clamp((int)previous.D("risultato_" + oldPage, new[] { 4, 3, 1, 0 }[oldPage]), 0, 5);
                viewSettings["risultato_" + page] = resultMap[result];
            }
            viewSettings.Remove("vista_3"); viewSettings.Remove("risultato_3");
            viewSettings["tab"] = new[] { 0, 1, 1, 2 }[selected];
            viewSettings["layout_version"] = 2;
        }
        // Merge the former phase and analysis pages, retaining the active page's view.
        int active = Math.Clamp((int)viewSettings.D("tab"), 0, 2);
        if (active == 2)
        {
            viewSettings["vista_1"] = (int)viewSettings.D("vista_2", 0);
            viewSettings["risultato_1"] = (int)viewSettings.D("risultato_2", 0);
        }
        viewSettings.Remove("vista_2"); viewSettings.Remove("risultato_2");
        viewSettings["tab"] = Math.Min(active, 1);
        viewSettings["layout_version"] = 3;
    }
    private void SelectPage()
    {
        selectingPage = true;
        int page = Math.Clamp(Pages.SelectedIndex, 0, 1);
        if (currentPage >= 0)
        {
            viewSettings["vista_" + currentPage] = DisplayChoice.SelectedIndex;
            viewSettings["risultato_" + currentPage] = Results.SelectedIndex;
        }
        if (page == 1) BuildPhases();
        inputHost.Content = pageInputs[page]; currentPage = page; viewSettings["tab"] = page;
        inputTitle.Text = new[] { "Definizione della sezione", "Fasi, omogeneizzazione e limiti" }[page];
        inputSubtitle.Text = new[] { "Geometria, materiali e armature · dimensioni in mm", "Carichi incrementali · φ, ψL e n sincronizzati" }[page];
        stageControls.Visibility = DisplayChoice.Visibility = page == 0 ? Visibility.Collapsed : Visibility.Visible;
        DisplayChoice.SelectedIndex = page == 0 ? 2 : Math.Clamp((int)viewSettings.D("vista_1", 0), 0, 2);
        GeometryLabels.IsChecked = viewSettings.B("quote_" + page, page == 0);
        RebarLabels.IsChecked = viewSettings.B("armature_" + page, page == 0);
        SetAnalysisPanels(page == 1);
        detailExpander.Visibility = page == 0 && (Data.B("irrigidimenti") || Data.B("appoggio")) ? Visibility.Visible : Visibility.Collapsed;
        Results.SelectedIndex = Math.Clamp((int)viewSettings.D("risultato_" + page, new[] { 2, 0 }[page]), 0, 4);
        selectingPage = false;
        RefreshDrawing();
    }
    private void SaveView()
    {
        if (currentPage < 0) return;
        viewSettings["vista_" + currentPage] = DisplayChoice.SelectedIndex;
        viewSettings["risultato_" + currentPage] = Results.SelectedIndex;
        viewSettings["quote_" + currentPage] = GeometryLabels.IsChecked == true;
        viewSettings["armature_" + currentPage] = RebarLabels.IsChecked == true;
        if (StageChoice.SelectedIndex >= 0) viewSettings["fase"] = StageChoice.SelectedIndex;
        viewSettings["segui_ultima_fase"] = followLatestStage;
    }
    private void ViewChanged()
    {
        if (building || selectingPage || Busy) return;
        SaveView(); Modified?.Invoke();
    }
    private void LabelOptionsChanged()
    {
        Drawing.ShowGeometryLabels = GeometryLabels.IsChecked == true;
        Drawing.ShowRebarLabels = RebarLabels.IsChecked == true;
        Drawing.InvalidateVisual(); ViewChanged();
    }
    private void SetAnalysisPanels(bool visible)
    {
        var upper = Splits["risultati"]; var right = Splits["righe"];
        sidebarHost.Content = visible ? analysisSidebar : propertiesSidebar;
        right.Children[1].Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        right.Children.OfType<GridSplitter>().Single().Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        upper.ColumnDefinitions[2].MinWidth = 250;
        upper.ColumnDefinitions[1].Width = new GridLength(8);
        SetSplit(upper, false, viewSettings.D(visible ? "split_risultati" : "split_proprieta", .7));
        right.RowDefinitions[2].MinHeight = visible ? 250 : 0;
        right.RowDefinitions[1].Height = new GridLength(visible ? 8 : 0);
        if (visible) { SetSplit(upper, false, viewSettings.D("split_risultati", .7)); SetSplit(right, true, viewSettings.D("split_righe", .56)); }
        else { right.RowDefinitions[0].Height = new(1, GridUnitType.Star); right.RowDefinitions[2].Height = new(0); }
    }
    private Grid Split(UIElement first, UIElement second, string key, bool rows, double initial, double firstMin, double secondMin)
    {
        var grid = new Grid(); Splits[key] = grid;
        if (rows)
        {
            grid.RowDefinitions.Add(new RowDefinition { MinHeight = firstMin }); grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) }); grid.RowDefinitions.Add(new RowDefinition { MinHeight = secondMin });
            Grid.SetRow(second, 2);
        }
        else
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { MinWidth = firstMin }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) }); grid.ColumnDefinitions.Add(new ColumnDefinition { MinWidth = secondMin });
            Grid.SetColumn(second, 2);
        }
        grid.Children.Add(first); grid.Children.Add(second);
        var splitter = new GridSplitter { Background = Ui.Bg, ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            HorizontalAlignment = rows ? HorizontalAlignment.Stretch : HorizontalAlignment.Center,
            VerticalAlignment = rows ? VerticalAlignment.Center : VerticalAlignment.Stretch };
        if (rows) { splitter.Height = 6; Grid.SetRow(splitter, 1); } else { splitter.Width = 6; Grid.SetColumn(splitter, 1); }
        grid.Children.Add(splitter);
        double ratio = viewSettings.D("split_" + key, initial);
        if (!double.IsFinite(ratio) || ratio < .15 || ratio > .85) ratio = initial;
        SetSplit(grid, rows, ratio);
        splitter.DragCompleted += (_, _) =>
        {
            double a = rows ? grid.RowDefinitions[0].ActualHeight : grid.ColumnDefinitions[0].ActualWidth;
            double b = rows ? grid.RowDefinitions[2].ActualHeight : grid.ColumnDefinitions[2].ActualWidth;
            if (a + b <= 0) return;
            double value = Math.Clamp(a / (a + b), .15, .85); SetSplit(grid, rows, value); viewSettings["split_" + (key == "risultati" && currentPage == 0 ? "proprieta" : key)] = value; Modified?.Invoke();
        };
        return grid;
    }
    private static void SetSplit(Grid grid, bool rows, double ratio)
    {
        if (rows) { grid.RowDefinitions[0].Height = new(ratio, GridUnitType.Star); grid.RowDefinitions[2].Height = new(1 - ratio, GridUnitType.Star); }
        else { grid.ColumnDefinitions[0].Width = new(ratio, GridUnitType.Star); grid.ColumnDefinitions[2].Width = new(1 - ratio, GridUnitType.Star); }
    }
}
