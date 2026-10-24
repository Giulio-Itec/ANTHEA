using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Anthea.Presentation.Modules.CompositeSection;

namespace X.Desktop.Views.CompositeSection;

/// <summary>
/// View of <see cref="CompositeSectionSheetViewModel"/> (F5.11). The code-behind does only what decision Q3 allows and what is state of the view:
/// <list type="bullet">
/// <item>the technical drawings: the historic <see cref="BridgeDrawing"/> receives <see cref="CompositeSectionSheetViewModel.Drawing"/> at every
/// change, the <see cref="BridgeDetailSketch"/> the data and the section of the local sketch, the two <see cref="BridgeResponsePlot"/> the charts of
/// the curves; «Adatta», «−», «+», «PNG» (written by the sheet through its file dialog) and «Espandi» (a dialog of the view) act on the drawing;</item>
/// <item>the page shown: only the page of the inputs and results or the page of the curves is in the tree, as the historic view, which swaps them
/// (the visit of the F3 capture sees only the page shown);</item>
/// <item>the splitters, with the ratios saved in the sheet (<see cref="CompositeSectionSheetViewModel.SplitRatio"/>,
/// <see cref="CompositeSectionSheetViewModel.SetSplit"/>), the side panel and the table hidden on the control panel (SetAnalysisPanels of the
/// historic view), the size of the page (at least 1120 × 700, with scroll bars below);</item>
/// <item>the scale of the concrete diagram typed and confirmed (Enter, Esc, focus lost), never overwritten while the user types in it;</item>
/// <item>the detached view (SM3): the drawing with its toolbar in a window of its own, with the situation, «Schermo intero · F11» and «Riaggancia»,
/// opened when the sheet is detached and closed when it is attached again or disposed; closing the window attaches the sheet again;</item>
/// <item>«Info modello…» (a dialog of the view, with the sketches of the method) and the menu «Verifiche ▾»;</item>
/// <item>the edit of a cell of the table committed before the buttons under the table, as the historic view.</item>
/// </list>
/// No calculation and no write of the document.
/// </summary>
public partial class CompositeSectionSheetView : UserControl
{
    /// <summary>The note under «Tipo di sezione».</summary>
    public const string SectionTypeNote = "Anima inclinata: scostamento orizzontale del piede dell'anima rispetto alla sommità (positivo verso destra). Cassoncino: due anime simmetriche, larghezza superiore di " +
        "ciascuna piattabanda, larghezza inferiore dell'intero fondo; lo scostamento è il rientro di ciascuna anima al piede (interasse al piede = interasse − 2 × scostamento). " +
        "Altezza totale H misurata fuori tutto. Il programma ricava h anima = H − t sup − t inf; con la seconda piattabanda inferiore attiva sottrae anche il relativo spessore. Per l'anima inclinata h anima è la distanza verticale libera. Lo spessore anima è normale alla lamiera. Flessione retta: N–Mx con anima verticale equivalente tw/cos α, verifiche locali sulle lamiere reali.";

    /// <summary>The note of the group of the box girder.</summary>
    public const string BoxNote = "Torsione: cella chiusa di Bredt, q = T/(2A0), con la soletta (fasi composte) o il controvento (fasi di solo acciaio). q si somma al taglio delle anime, al fondo, ai pioli e alla soletta. " +
        "Distorsione: trave su suolo elastico sulla campata appoggiata, diaframmi intermedi come molle; σdw oltre il 10% della flessione entra nelle verifiche del fondo (EN 1993-2 §6.2.7). " +
        "Diaframmi a piastra (taglio e imbozzamento) o controventi a X (diagonale compressa); diaframma d'appoggio a taglio e coppia degli apparecchi sugli irrigidimenti d'appoggio.";

    /// <summary>The note above the table of the actions.</summary>
    public const string ActionsNote = "Ritiro: Δεcs < 0 accorciamento, in µε (−250 = −0,25‰). φ, ψL e n sono propri della fase. Il risultato somma gli incrementi ΔV. Δq aggiunge scorrimento ai pioli (effetti locali/estremità), nullo nelle fasi di solo acciaio. " +
        "ΔT: momento torcente del cassoncino, attivo con le verifiche a torsione.";

    /// <summary>«Ipotesi e limiti» of the page of the curves.</summary>
    public const string CurveHypotheses = "Legami non lineari caratteristici, sezione lorda e perfetta aderenza. Lo storico, se richiesto, viene ricalcolato con questi legami: non si converte uno stato cumulativo o efficace in stato plastico. Usa discretizzazione, opzione di viscosità e «CLS teso (non lineare)» del modulo. Non è una verifica SLU di classe 4. L'arresto può indicare un limite costitutivo o un problema di equilibrio: non certifica il collasso.\n\nPer N–ε la curvatura resta fissata: il momento risultante è una reazione. Per M–κ resta fissato N. Le curve σ–ε usano la deformazione meccanica della fibra, con memoria plastica.";

    /// <summary>The title of the window of the detached view.</summary>
    public const string DetachedTitle = "ANTHEA · Sezione da ponte · sollecitazioni e tensioni";

    /// <summary>The title of the dialog «Info modello…».</summary>
    public const string InformationTitle = "Sezione composta · informazioni sul modello";

    /// <summary>The tool tips of the editors of the form of a phase: those of the sheet and ΔT (SM7).</summary>
    public static IReadOnlyDictionary<string, string> PhaseToolTips { get; } =
        new Dictionary<string, string>(CompositeSectionFields.EditorToolTips, StringComparer.Ordinal) { ["T"] = CompositeSectionFields.TorqueToolTip };

    private CompositeSectionSheetViewModel? sheet;
    private bool updatingScale;

    /// <summary>The drawing of the section (Drawing of the historic view).</summary>
    internal BridgeDrawing Drawing { get; } = new() { MinHeight = 160 };

    /// <summary>The local sketch (DetailSketch of the historic view).</summary>
    internal BridgeDetailSketch DetailSketch { get; } = new();

    /// <summary>«Risposta della sezione».</summary>
    internal BridgeResponsePlot SectionPlot { get; } = new();

    /// <summary>«Tensione e deformazione della fibra», with the colour of the historic fibre plot.</summary>
    internal BridgeResponsePlot FiberPlot { get; } = new() { CurveBrush = BridgeResponsePlot.FiberBrush };

    /// <summary>Shows a dialog of the view («Info modello…», «Espandi»): modal, as the historic ones; the checks show it off screen without waiting.</summary>
    internal Func<Window, bool?> ShowWindow { get; set; } = window => window.ShowDialog();

    /// <summary>Shows the window of the detached view: modeless, as the historic one; the checks keep it off screen and never activated.</summary>
    internal Action<Window> ShowDetached { get; set; } = window => window.Show();

    /// <summary>The window of the detached view, null when the drawing is in the sheet.</summary>
    internal Window? DetachedWindow { get; private set; }

    /// <summary>The view inside <see cref="DetachedWindow"/>.</summary>
    internal CompositeSectionDetachedView? DetachedView { get; private set; }

    public CompositeSectionSheetView()
    {
        InitializeComponent();
        // As the historic view: the numbers of the forms and of the table with the numeric presentation.
        SetValue(NumericPresentation.EnabledProperty, true);
        DrawingHost.Content = Drawing; DetailSketchHost.Content = DetailSketch; SectionPlotHost.Content = SectionPlot; FiberPlotHost.Content = FiberPlot;
        // Only the page, the inputs and the side panel shown stay in the tree, as in the historic view.
        WorkspaceHost.Children.Clear(); InputHost.Children.Clear(); SidebarHost.Children.Clear();
        foreach (var splitter in new[] { MainWorkspace, ResponsePage }.SelectMany(Logical<GridSplitter>)) splitter.DragCompleted += Splitter_DragCompleted;
        Scroll.SizeChanged += (_, _) => Resize();
        Scroll.ScrollChanged += (_, e) => { if (ReferenceEquals(e.Source, Scroll) && (e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0)) Resize(); };
        DataContextChanged += (_, _) => Attach(DataContext as CompositeSectionSheetViewModel);
        Loaded += (_, _) => Attach(DataContext as CompositeSectionSheetViewModel);
    }

    /// <summary>The elements of type <typeparamref name="T"/> in the logical tree under <paramref name="root"/> (the splitters of the pages not shown).</summary>
    internal static IEnumerable<T> Logical<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T found) yield return found;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var item in Logical<T>(child)) yield return item;
    }

    private void Attach(CompositeSectionSheetViewModel? next)
    {
        if (ReferenceEquals(sheet, next)) return;
        if (sheet is not null) { sheet.PropertyChanged -= OnSheetChanged; CloseDetached(); }
        sheet = next;
        if (sheet is null) return;
        // The pages and the drawing keep the sheet also while they are out of the tree (the page not shown, the detached window).
        foreach (var part in new FrameworkElement[] { MainWorkspace, ResponsePage, ControlPanelInputs, PhaseInputs, SummaryPanel, PropertiesPanel, Viewport })
            part.DataContext = sheet;
        // The menu «Verifiche ▾» is outside the tree until it opens: its switches follow the sheet from the start.
        ChecksMenuButton.ContextMenu.DataContext = sheet;
        sheet.PropertyChanged += OnSheetChanged;
        ShowPage(); ShowInputs();
        foreach (var (grid, key, rows, initial) in new[] { (MainWorkspace, "ingressi", false, .29), (ResponsePage, "curve_colonne", false, .27), (CurveRows, "curve_righe", true, .65) })
            SetSplit(grid, rows, sheet.SplitRatio("split_" + key, initial));
        SetSplit(UpperPanels, false, sheet.SplitRatio("split_risultati", .7));
        SetSplit(RightPanels, true, sheet.SplitRatio("split_righe", .56));
        ApplyPanels();
        Draw(); DrawSketch(); DrawCurves(); RefreshScale();
        if (sheet.IsDetached) OpenDetached();
    }

    private void OnSheetChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sheet is null) return;
        switch (e.PropertyName)
        {
            case nameof(CompositeSectionSheetViewModel.Page): ShowPage(); break;
            case nameof(CompositeSectionSheetViewModel.AnalysisPanelsShown): ApplyPanels(); break;
            case nameof(CompositeSectionSheetViewModel.InputPage): ShowInputs(); ApplyPanels(); break;
            case nameof(CompositeSectionSheetViewModel.Drawing): Draw(); DrawSketch(); break;
            case nameof(CompositeSectionSheetViewModel.DetailAtSupport):
            case nameof(CompositeSectionSheetViewModel.Status):
            case nameof(CompositeSectionSheetViewModel.MaterialText):
            case nameof(CompositeSectionSheetViewModel.SteelHeightText):
            case nameof(CompositeSectionSheetViewModel.SlabLayoutText): DrawSketch(); break;
            case nameof(CompositeSectionSheetViewModel.SectionChart):
            case nameof(CompositeSectionSheetViewModel.FiberChart): DrawCurves(); break;
            case nameof(CompositeSectionSheetViewModel.ScaleText):
            case nameof(CompositeSectionSheetViewModel.ScaleError): RefreshScale(); break;
            case nameof(CompositeSectionSheetViewModel.IsDetached): if (sheet.IsDetached) OpenDetached(); else CloseDetached(); break;
            case nameof(CompositeSectionSheetViewModel.IsDisposed): CloseDetached(); break;
            case nameof(CompositeSectionSheetViewModel.SelectedActionIndex): if (ActionsGrid.SelectedItem is { } row) ActionsGrid.ScrollIntoView(row); break;
        }
    }

    // ---------------------------------------------------------------- layout

    /// <summary>The page shown in the tree: the inputs and results (pages 01 and 02) or the curves (03).</summary>
    private void ShowPage()
    {
        if (sheet is null) return;
        Show(WorkspaceHost, sheet.Page == 2 ? ResponsePage : MainWorkspace);
    }

    /// <summary>The inputs of the page of inputs shown (none before the first one, when the sheet opens on the curves).</summary>
    private void ShowInputs()
    {
        if (sheet is null) return;
        Show(InputHost, sheet.InputPage switch { 0 => ControlPanelInputs, 1 => PhaseInputs, _ => null });
    }

    /// <summary><paramref name="shown"/> the only child of <paramref name="host"/> (none when null).</summary>
    private static void Show(Panel host, UIElement? shown)
    {
        if (shown is null ? host.Children.Count == 0 : host.Children.Count == 1 && ReferenceEquals(host.Children[0], shown)) return;
        host.Children.Clear();
        if (shown is not null) host.Children.Add(shown);
    }

    /// <summary>The page of the phases shows the summary and the table; the control panel the properties, over the whole height (SetAnalysisPanels).</summary>
    private void ApplyPanels()
    {
        if (sheet is null) return;
        bool visible = sheet.AnalysisPanelsShown;
        Show(SidebarHost, visible ? SummaryPanel : PropertiesPanel);
        ResultsPanel.Visibility = RowsSplitter.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        UpperPanels.ColumnDefinitions[2].MinWidth = 250;
        UpperPanels.ColumnDefinitions[1].Width = new GridLength(8);
        SetSplit(UpperPanels, false, sheet.SplitRatio(visible ? "split_risultati" : "split_proprieta", .7));
        RightPanels.RowDefinitions[2].MinHeight = visible ? 250 : 0;
        RightPanels.RowDefinitions[1].Height = new GridLength(visible ? 8 : 0);
        if (visible) SetSplit(RightPanels, true, sheet.SplitRatio("split_righe", .56));
        else { RightPanels.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star); RightPanels.RowDefinitions[2].Height = new GridLength(0); }
    }

    private static void SetSplit(Grid grid, bool rows, double ratio)
    {
        if (rows) { grid.RowDefinitions[0].Height = new(ratio, GridUnitType.Star); grid.RowDefinitions[2].Height = new(1 - ratio, GridUnitType.Star); }
        else { grid.ColumnDefinitions[0].Width = new(ratio, GridUnitType.Star); grid.ColumnDefinitions[2].Width = new(1 - ratio, GridUnitType.Star); }
    }

    /// <summary>A splitter released: the ratio of the two panels (0,15–0,85) applied and saved in the sheet, with Modified.</summary>
    private void Splitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (sheet is null || sender is not GridSplitter { Tag: string key, Parent: Grid grid }) return;
        bool rows = grid.RowDefinitions.Count == 3;
        double a = rows ? grid.RowDefinitions[0].ActualHeight : grid.ColumnDefinitions[0].ActualWidth;
        double b = rows ? grid.RowDefinitions[2].ActualHeight : grid.ColumnDefinitions[2].ActualWidth;
        if (a + b <= 0) return;
        double value = Math.Clamp(a / (a + b), .15, .85);
        SetSplit(grid, rows, value);
        sheet.SetSplit(key, value);
    }

    /// <summary>The page fills the sheet, at least 1120 × 700 (scroll bars below), as the historic view.</summary>
    private void Resize()
    {
        Body.Width = Math.Max(1120, double.IsFinite(Scroll.ViewportWidth) ? Scroll.ViewportWidth : 0);
        Body.Height = Math.Max(700, double.IsFinite(Scroll.ViewportHeight) ? Scroll.ViewportHeight : 0);
    }

    // ---------------------------------------------------------------- drawings

    /// <summary>The drawing of the section as the historic RefreshDrawing sets it.</summary>
    private void Draw()
    {
        if (sheet is null) return;
        var d = sheet.Drawing;
        Drawing.Geometry = d.Geometry; Drawing.Input = d.Input; Drawing.Stage = d.Stage; Drawing.Mode = d.Mode; Drawing.IsStale = d.Stale;
        Drawing.ShowGeometryLabels = d.GeometryLabels; Drawing.ShowRebarLabels = d.RebarLabels; Drawing.ShowNeutralAxis = d.NeutralAxis;
        Drawing.ContourSection = d.ContourSection; Drawing.ContourDiagram = d.ContourDiagram; Drawing.ShowStressLimits = d.StressLimits;
        Drawing.ConcreteAmplification = d.ConcreteAmplification; Drawing.DetailAtSupport = d.DetailAtSupport;
        Drawing.LoadY = null; Drawing.LoadPoints = [.. d.LoadPoints];
        Drawing.InvalidateVisual();
    }

    /// <summary>The local sketch on the data of the sheet (it reads them when it draws).</summary>
    private void DrawSketch()
    {
        if (sheet is null) return;
        DetailSketch.Input = sheet.Data; DetailSketch.AtSupport = sheet.DetailAtSupport; DetailSketch.InvalidateVisual();
    }

    private void DrawCurves()
    {
        if (sheet is null) return;
        Plot(SectionPlot, sheet.SectionChart); Plot(FiberPlot, sheet.FiberChart);
        static void Plot(BridgeResponsePlot plot, CompositeSectionChart chart)
        {
            plot.XLabel = chart.XLabel; plot.YLabel = chart.YLabel; plot.Points = chart.Points.Select(p => new Point(p.X, p.Y)).ToArray(); plot.Selected = chart.Selected;
            plot.InvalidateVisual();
        }
    }

    private void Fit_Click(object sender, RoutedEventArgs e) => Drawing.ResetView();

    private void ZoomOut_Click(object sender, RoutedEventArgs e) => Drawing.ZoomBy(.85);

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => Drawing.ZoomBy(1.15);

    /// <summary>«PNG»: the drawing with the colours of a document, written by the sheet in the file chosen by the user.</summary>
    private void Png_Click(object sender, RoutedEventArgs e) => sheet?.SaveDrawingImage(() => Ui.DocumentSnapshot(Drawing));

    /// <summary>«Espandi»: the drawing in a dialog of 1200 × 800 until it is closed, then back in its frame.</summary>
    private void Expand_Click(object sender, RoutedEventArgs e)
    {
        DrawingHost.Content = null;
        var window = Ui.Dialog(this, CompositeSectionSheetViewModel.DrawingTitle, Drawing, 1200, 800);
        window.Closed += (_, _) => { window.Content = null; DrawingHost.Content = Drawing; };
        ShowWindow(window);
    }

    /// <summary>«Verifiche ▾»: the menu of the contours and of the limits under its button.</summary>
    private void ChecksMenu_Click(object sender, RoutedEventArgs e)
    {
        if (ChecksMenuButton.ContextMenu is not { } menu) return;
        menu.PlacementTarget = ChecksMenuButton;
        menu.IsOpen = true;
    }

    // ---------------------------------------------------------------- scale of the concrete diagram

    /// <summary>The factor of the sheet in «CLS ×», unless the user is typing in it.</summary>
    private void RefreshScale()
    {
        if (sheet is null || ScaleEditor.IsKeyboardFocusWithin) return;
        updatingScale = true;
        try { ScaleEditor.Text = sheet.ScaleText; }
        finally { updatingScale = false; }
    }

    /// <summary>Enter confirms the factor, Esc shows the factor of the drawing again; both give the focus back to the drawing.</summary>
    private void Scale_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sheet is null) return;
        if (e.Key == Key.Enter) { sheet.CommitScale(ScaleEditor.Text); Drawing.Focus(); e.Handled = true; }
        else if (e.Key == Key.Escape) { sheet.RestoreScale(); ScaleEditor.Text = sheet.ScaleText; Drawing.Focus(); e.Handled = true; }
    }

    /// <summary>Leaving «CLS ×» confirms what was typed (the factor shown, unchanged, does nothing).</summary>
    private void Scale_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sheet is null || updatingScale) return;
        sheet.CommitScale(ScaleEditor.Text);
        RefreshScale();
    }

    // ---------------------------------------------------------------- table of the actions

    /// <summary>The buttons under the table commit the cell being edited first, as the historic view (ActionsTable.Commit).</summary>
    private void TableCommand_Click(object sender, RoutedEventArgs e)
    {
        ActionsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        ActionsGrid.CommitEdit(DataGridEditingUnit.Row, true);
    }

    // ---------------------------------------------------------------- detached view (SM3)

    /// <summary>«Stacca vista»: the sheet detaches its drawing (the window opens when it is detached); a window already open comes to the front.</summary>
    private void Detach_Click(object sender, RoutedEventArgs e)
    {
        if (DetachedWindow is { } window) { window.Activate(); return; }
        if (sheet?.DetachCommand.CanExecute(null) == true) sheet.DetachCommand.Execute(null);
    }

    private void BringToFront_Click(object sender, RoutedEventArgs e) => DetachedWindow?.Activate();

    private void Reattach_Click(object sender, RoutedEventArgs e) => DetachedWindow?.Close();

    /// <summary>The window of the detached view (BridgeDetachedView of the historic view): the drawing and its toolbar moved into it.</summary>
    private void OpenDetached()
    {
        if (DetachedWindow is not null || sheet is null) return;
        var view = new CompositeSectionDetachedView { DataContext = sheet };
        ViewportHost.Content = null;
        view.Host.Content = Viewport;
        var window = new Window
        {
            Title = DetachedTitle, Owner = Window.GetWindow(this), Width = 1400, Height = 900, MinWidth = 880, MinHeight = 550,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = view
        };
        window.SetResourceReference(BackgroundProperty, "Token.Surface");
        Appearance.Watch(window);
        WindowState previous = WindowState.Normal;
        view.FullScreenRequested += () => FullScreen(window);
        view.ReattachRequested += window.Close;
        void FullScreen(Window target)
        {
            if (target.WindowStyle == WindowStyle.None)
            { target.WindowStyle = WindowStyle.SingleBorderWindow; target.ResizeMode = ResizeMode.CanResize; target.WindowState = previous; }
            else { previous = target.WindowState; target.WindowState = WindowState.Normal; target.WindowStyle = WindowStyle.None; target.ResizeMode = ResizeMode.NoResize; target.WindowState = WindowState.Maximized; }
        }
        window.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.F11 || e.Key == Key.Escape && window.WindowStyle == WindowStyle.None) { FullScreen(window); e.Handled = true; }
        };
        window.Closed += (_, _) =>
        {
            view.Host.Content = null; ViewportHost.Content = Viewport;
            DetachedWindow = null; DetachedView = null;
            if (sheet is { IsDisposed: false } open) { open.Reattach(); Drawing.ResetView(); }
        };
        DetachedWindow = window; DetachedView = view;
        ShowDetached(window);
        Drawing.ResetView();
    }

    private void CloseDetached() => DetachedWindow?.Close();

    // ---------------------------------------------------------------- dialogs

    /// <summary>«Info modello…»: the model and its sketches in a dialog of the view (1020 × 800), as the historic window.</summary>
    private void Information_Click(object sender, RoutedEventArgs e)
    {
        var dialog = Ui.Dialog(this, InformationTitle, new CompositeSectionInformationView(), 1020, 800);
        ShowWindow(dialog);
    }
}
