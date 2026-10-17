using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Anthea.Presentation.Modules.RetainingWall;
using X.Core;

namespace X.Desktop.Views.RetainingWall;

/// <summary>
/// View of <see cref="RetainingWallSheetViewModel"/> (F5.9). The code-behind does only what decision Q3 allows and what is state of the view:
/// <list type="bullet">
/// <item>the technical drawings: the historic <see cref="RetainingWallDrawing"/> of the inputs and of the results and the two
/// <see cref="GlobalStabilityDrawing"/> receive the data, the result, the case, the selections and the switches of the ViewModel at every
/// <see cref="RetainingWallSheetViewModel.DrawingsChanged"/>, as the historic view sets them; the text under the pointer (Inspected) stays in
/// the view; a click on a load selects its row of the actions and opens the card «Azioni» (ActionSelected);</item>
/// <item>the layout: the drawing of the inputs beside the cards, under them below 1450 px (SizeChanged of the historic view);</item>
/// <item>the scrolling to a card opened by a command («Sisma», «Stabilità globale»: <see cref="RetainingWallSheetViewModel.CardShown"/>), and,
/// when the sheet rebuilds its inputs (Cards notified), the inner groups of the cards back to their initial state and the column of the
/// cards back to the top, as the historic view that builds them again;</item>
/// <item>the selection of a row of a panel of results, sent to the sheet by the action of its table (the first table of «Riepilogo e
/// dettagli», the slices, the pressures);</item>
/// <item>the dialog of the bar schedule (decision MU10: a dialog of the view, <see cref="RetainingWallBarScheduleView"/>).</item>
/// </list>
/// No calculation and no write of the document.
/// </summary>
public partial class RetainingWallSheetView : UserControl
{
    private RetainingWallSheetViewModel? sheet;
    private readonly List<(Expander Group, bool Open)> groups = [];
    private long attachedRevision;
    private bool diagramData;

    /// <summary>The drawing of the inputs (Drawing of the historic view).</summary>
    internal RetainingWallDrawing InputDrawing { get; } = new() { Height = 440, MinHeight = 330 };

    /// <summary>The drawing of the results (Diagrams of the historic view).</summary>
    internal RetainingWallDrawing DiagramsDrawing { get; } = new() { Height = 390, Diagrams = true, CombinedLoads = true };

    /// <summary>The drawing of the global stability in the results.</summary>
    internal GlobalStabilityDrawing GlobalDrawing { get; } = new() { Height = 390 };

    /// <summary>The preview of the global stability in its card (globalPreview of the historic view).</summary>
    internal GlobalStabilityDrawing GlobalPreview { get; } = new() { Height = 320, FocusCritical = false, ShowSearch = true };

    /// <summary>Shows a window of the view (the bar schedule): modal, as the historic dialog; the checks show it off screen without waiting.</summary>
    internal Func<Window, bool?> ShowWindow { get; set; } = window => window.ShowDialog();

    public RetainingWallSheetView()
    {
        InitializeComponent();
        InputDrawingHost.Content = InputDrawing; DiagramsHost.Content = DiagramsDrawing; GlobalDrawingHost.Content = GlobalDrawing; GlobalPreviewHost.Content = GlobalPreview;
        InputDrawing.Inspected += text => Probe.Text = text;
        DiagramsDrawing.Inspected += text => Probe.Text = text;
        InputDrawing.ActionSelected += SelectAction;
        // The inner groups of the cards (not the cards, bound to the sheet) with their initial state.
        foreach (var group in Logical<Expander>(CardsPanel))
            if (BindingOperations.GetBinding(group, Expander.IsExpandedProperty) is null) groups.Add((group, group.IsExpanded));
        SizeChanged += (_, _) => Arrange();
        DataContextChanged += (_, _) => Attach(DataContext as RetainingWallSheetViewModel);
        Loaded += (_, _) => Attach(DataContext as RetainingWallSheetViewModel);
        Unloaded += (_, _) => Attach(null);
    }

    private void Attach(RetainingWallSheetViewModel? next)
    {
        if (ReferenceEquals(sheet, next)) return;
        if (sheet is not null)
        {
            sheet.PropertyChanged -= OnSheetChanged; sheet.DrawingsChanged -= OnDrawingsChanged; sheet.CardShown -= OnCardShown;
        }
        sheet = next;
        if (sheet is null) return;
        attachedRevision = sheet.Revision; diagramData = false;
        sheet.PropertyChanged += OnSheetChanged; sheet.DrawingsChanged += OnDrawingsChanged; sheet.CardShown += OnCardShown;
        Draw();
    }

    private void OnSheetChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RetainingWallSheetViewModel.Cards)) ResetInputs();
    }

    private void OnDrawingsChanged(object? sender, EventArgs e) => Draw();

    /// <summary>The inputs were built again by the sheet: the inner groups closed or opened as at the opening, the column of the cards at the top.</summary>
    private void ResetInputs()
    {
        foreach (var (group, open) in groups) group.IsExpanded = open;
        CardsScroll.ScrollToVerticalOffset(0);
    }

    /// <summary>The drawings of the historic view on the plain data of the sheet (Preview, ShowCombination, RefreshViews and RefreshGlobal).</summary>
    private void Draw()
    {
        if (sheet is not { } s) return;
        var d = s.Drawing;
        InputDrawing.Data = s.Data; InputDrawing.Calculation = s.DrawingCalculation; InputDrawing.Case = s.DrawingCase;
        InputDrawing.SelectedAction = d.SelectedAction; InputDrawing.SelectedLayer = d.SelectedLayer; InputDrawing.SelectedValley = d.SelectedValley;
        InputDrawing.SelectedPressure = d.SelectedPressure;
        InputDrawing.ShowLoads = d.ShowLoads; InputDrawing.ShowLabels = d.ShowLabels; InputDrawing.ShowRebar = d.ShowRebar; InputDrawing.ShowPressures = d.ShowPressures;
        InputDrawing.InvalidateVisual();
        // The diagrams receive the data at the first result or edit (Changed and ShowCombination of the historic view).
        var g = s.Diagrams;
        diagramData |= s.DiagramCalculation is not null || s.Revision != attachedRevision;
        DiagramsDrawing.Data = diagramData ? s.Data : null; DiagramsDrawing.Calculation = s.DiagramCalculation; DiagramsDrawing.Case = s.DiagramCase;
        DiagramsDrawing.Mode = g.Mode; DiagramsDrawing.Diagrams = g.Diagrams; DiagramsDrawing.Member = g.Member;
        DiagramsDrawing.ShowRebar = g.ShowRebar; DiagramsDrawing.ShowLoads = g.ShowLoads; DiagramsDrawing.MaxWidth = g.WideLimit ? 1050 : double.PositiveInfinity;
        DiagramsDrawing.Visibility = g.Visible ? Visibility.Visible : Visibility.Collapsed;
        DiagramsDrawing.InvalidateVisual();
        var global = s.GlobalDrawing;
        GlobalDrawing.Data = s.Data; GlobalDrawing.Result = s.GlobalDrawingResult; GlobalDrawing.Case = s.GlobalDrawingCase;
        GlobalDrawing.FocusCritical = global.FocusCritical; GlobalDrawing.SelectedSlice = global.SelectedSlice;
        GlobalDrawing.Visibility = global.Visible ? Visibility.Visible : Visibility.Collapsed;
        GlobalDrawing.InvalidateVisual();
        var preview = s.GlobalPreview;
        GlobalPreview.Data = s.Data; GlobalPreview.Result = s.GlobalPreviewResult; GlobalPreview.Case = s.GlobalPreviewCase;
        GlobalPreview.SelectedSoil = preview.SelectedSoil; GlobalPreview.SelectedColumn = preview.SelectedColumn;
        GlobalPreview.InvalidateVisual();
        // The colours of the deep layers follow their names (RefreshGlobalSoilDetails).
        GlobalRearGrid.RefreshMarks(); GlobalValleyGrid.RefreshMarks();
    }

    /// <summary>SizeChanged of the historic view: below 1450 px the drawing of the inputs goes under the cards.</summary>
    private void Arrange()
    {
        bool narrow = ActualWidth < 1450;
        DrawingColumn.Width = narrow ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        Grid.SetColumn(InputView, narrow ? 0 : 1); Grid.SetRow(InputView, narrow ? 1 : 0);
        InputView.Margin = narrow ? new Thickness(0, 10, 0, 0) : new Thickness(10, 0, 0, 0);
    }

    /// <summary>A click on a load of the drawing: its row of the actions selected, the card «Azioni» opened and brought into view.</summary>
    private void SelectAction(string id)
    {
        if (sheet is null) return;
        int index = sheet.Actions.Rows.ToList().FindIndex(r => r.Values.S("id") == id);
        if (index < 0) return;
        sheet.Actions.SelectedIndex = index;
        sheet.Cards["Azioni"].IsExpanded = true;
        UpdateLayout(); ActionsCard.BringIntoView();
    }

    /// <summary>A command opened a card: «Sisma» brought into view, «Stabilità globale» at the top of the column of the cards (ScrollGlobalSetup).</summary>
    private void OnCardShown(object? sender, string card)
    {
        UpdateLayout();
        if (card == "Sisma") { SeismicCard.BringIntoView(); return; }
        DependencyObject? parent = GlobalCard;
        while ((parent = VisualTreeHelper.GetParent(parent)) is not null)
            if (parent is ScrollViewer viewer)
            {
                double offset = GlobalCard.TransformToAncestor(viewer).Transform(new Point()).Y + viewer.VerticalOffset;
                viewer.ScrollToVerticalOffset(Math.Max(0, offset));
                break;
            }
    }

    /// <summary>A row of «Riepilogo e dettagli»: the first table is the selection of the sheet (checks, sections, pressures, global combinations), the slices are highlighted.</summary>
    private void ChecksPanel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.OriginalSource is not WallBlockGrid grid || sheet is null) return;
        e.Handled = true;
        if (ReferenceEquals(grid.Block, sheet.ChecksPanel?.FirstOrDefault(b => b.Kind == WallBlockKind.Table))) sheet.SelectResultRow(grid.SelectedIndex);
        else if (grid.Block?.RowAction == WallRowAction.Slice) sheet.SelectSlice(grid.SelectedIndex);
    }

    /// <summary>A row of the pressures of the inputs: the layer highlighted in the drawing of the inputs.</summary>
    private void PressurePanel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.OriginalSource is not WallBlockGrid grid || sheet is null) return;
        e.Handled = true;
        if (grid.Block?.RowAction == WallRowAction.Pressure) sheet.SelectPressure(grid.SelectedIndex);
    }

    /// <summary>«Distinta ferri…» and «Tavola armature e distinta ferri…»: the bar schedule of the result in its dialog (MU10).</summary>
    private void BarSchedule_Click(object sender, RoutedEventArgs e)
    {
        if (sheet?.OpenBarSchedule() is not { } schedule || sheet.Calculation is not { } result) return;
        var drawing = new RetainingWallBarDrawing(result.Input, schedule);
        ShowWindow(Ui.Dialog(this, RetainingWallBarScheduleView.Title, new RetainingWallBarScheduleView(sheet, drawing), 1270, 920));
    }

    private static IEnumerable<T> Logical<T>(DependencyObject root) where T : DependencyObject
    {
        var pending = new Stack<object>(); pending.Push(root);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (node is T match) yield return match;
            if (node is DependencyObject element) foreach (var child in LogicalTreeHelper.GetChildren(element)) pending.Push(child);
        }
    }
}
