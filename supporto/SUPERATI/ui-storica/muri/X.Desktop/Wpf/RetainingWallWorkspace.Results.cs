using Anthea.Application.Walls;
using Anthea.Application.Presentation;
using Anthea.Presentation.Modules.RetainingWall;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;

internal sealed partial class RetainingWallWorkspace
{
    internal void GenerateMatrix()
    {
        Commit();
        try { RetainingWallPresentation.GenerateMatrix(Data); BuildMatrix(); Changed(); }
        catch (Exception ex) { matrixStatus.Text = ex.Message; }
    }
    private void ConfirmMatrix()
    {
        Commit();
        try { RetainingWallPresentation.ConfirmMatrix(Data); matrixStatus.Text = RetainingWallPresentation.MatrixConfirmed; Changed(); }
        catch (Exception ex) { matrixStatus.Text = ex.Message; }
    }
    private void CopyCombination()
    {
        if (MatrixGrid?.SelectedItem is not JsonRow row) return;
        RetainingWallPresentation.CopyCombination(Data, MatrixGrid.Rows.IndexOf(row)); BuildMatrix(); Changed();
    }
    private void RemoveCombination()
    {
        if (MatrixGrid?.SelectedItem is not JsonRow row) return; RetainingWallPresentation.RemoveCombination(Data, MatrixGrid.Rows.IndexOf(row)); BuildMatrix(); Changed();
    }
    private void BuildMatrix()
    {
        if (MatrixGrid is not null) grids.Remove(MatrixGrid);
        MatrixGrid = new JsonGrid(RetainingWallFields.Matrix(Data)) { Height = 205, RowHeight = 30, ColumnHeaderHeight = 43, AlternatingRowBackground = Ui.Brush("#F3F6FA") };
        CompactHeaders(MatrixGrid);
        grids.Add(MatrixGrid);
        for (int i = 0; i < MatrixGrid.Columns.Count; i++) MatrixGrid.Columns[i].Width = i switch { 0 => 55, 1 => 185, 2 => 140, 3 => 150, _ => 85 };
        foreach (var c in Data.Array("combinations").OfType<JsonObject>())
        {
            var flat = RetainingWallPresentation.MatrixRow(Data, c);
            var row = new JsonRow(flat, key =>
            {
                RetainingWallPresentation.MatrixEdit(Data, c, flat, key);
                matrixStatus.Text = RetainingWallPresentation.MatrixEdited; Changed();
            }); MatrixGrid.Rows.Add(row);
        }
        MatrixGrid.SelectionChanged += (_, _) => { if (MatrixGrid.SelectedItem is JsonRow row && Calculation is not null) Combination.SelectedItem = row.Values.S("name"); };
        matrixHost.Content = MatrixGrid;
        matrixStatus.Text = RetainingWallPresentation.MatrixStatus(Data);
    }
    private void ShowResults()
    {
        var r = Calculation!; string? name = Combination.SelectedItem as string; independentGlobal = null; independentGlobalInput = null; independentGlobalError = null;
        string? globalName = GlobalCombination.SelectedItem as string;
        var prepared = Anthea.Application.Presentation.WallSheetPresentation.ApplyResult(Data, r);
        if (prepared.GlobalUpdated) BuildGlobalMatrix();
        if (prepared.GlobalError is { } globalError) globalMessage.Text = globalError;
        GlobalCombination.ItemsSource = r.GlobalStability?.Cases.Select(c => c.Factors.Name).ToArray();
        GlobalCombination.SelectedItem = RetainingWallPresentation.GlobalCombination(r.GlobalStability, globalName);
        if (prepared.LocalUpdated || MatrixGrid is null) BuildMatrix();
        Combination.ItemsSource = r.Cases.Select(c => c.Name).ToArray(); Combination.SelectedItem = name is not null && r.Cases.Any(c => c.Name == name) ? name : r.Cases[0].Name;
        ShowCombination();
        UpdateSeismicStatus();
    }
    private void ShowCombination()
    {
        if (Calculation is not { } r || Combination.SelectedItem is not string name) return; var c = r.Cases.FirstOrDefault(c => c.Name == name); if (c is null) return;
        foreach (var draw in new[] { Drawing, Diagrams }) { draw.Data = Data; draw.Calculation = r; draw.Case = c; }
        summary.Text = RetainingWallPresentation.Summary(Data, r, c);
        pressures.Content = PressurePanel(c);
        ShowCheckTable(); RefreshViews();
    }
    private FrameworkElement PressurePanel(WallModel.LoadCase c)
    {
        var table = Table([.. RetainingWallPresentation.PressureColumns], RetainingWallPresentation.PressureRows(c));
        table.Height = 175;
        table.SelectionChanged += (_, _) => { Drawing.SelectedPressure = table.SelectedIndex; Drawing.InvalidateVisual(); };
        var stem = Table([.. RetainingWallPresentation.StemPressureColumns], RetainingWallPresentation.StemPressureRows(c)); stem.Height = 130;
        var valley = Table([.. RetainingWallPresentation.ValleyPressureColumns], RetainingWallPresentation.ValleyPressureRows(c)); valley.Height = 110;
        return Ui.Stack(Ui.Text(RetainingWallPresentation.PressureFormula(Data), 11), Ui.Text(RetainingWallPresentation.PressureHeader(c), 11), table,
            Group(RetainingWallPresentation.PressureStemTitle, stem, false), Group(RetainingWallPresentation.PressureValleyTitle, valley, false),
            Ui.Text(RetainingWallPresentation.PressureThrust(Data, c), 11));
    }
    private void RefreshViews()
    {
        bool global = (string?)ViewMode.SelectedItem == "Stabilità globale";
        GlobalDrawing.Visibility = GlobalCombination.Visibility = globalFullProfile.Visibility = global ? Visibility.Visible : Visibility.Collapsed; Diagrams.Visibility = global ? Visibility.Collapsed : Visibility.Visible;
        if (global) CheckFilter.SelectedItem = "Stabilità globale";
        RefreshGlobal();
        Diagrams.Mode = ViewMode.SelectedItem as string ?? "Geometria e carichi"; Diagrams.Diagrams = Diagrams.Mode == "Sollecitazioni"; Diagrams.Member = Member.SelectedItem as string ?? "Fusto";
        Diagrams.ShowRebar = Diagrams.Mode is "Armature" or "Geometria e carichi"; Member.Visibility = Diagrams.Diagrams ? Visibility.Visible : Visibility.Collapsed;
        Diagrams.ShowLoads = Diagrams.Mode != "Armature"; Diagrams.MaxWidth = Diagrams.Diagrams ? double.PositiveInfinity : 1050;
        if (Diagrams.Mode == "Armature") CheckFilter.SelectedItem = "Dettagli armature";
        Drawing.InvalidateVisual(); Diagrams.InvalidateVisual();
    }
    private void ShowCheckTable()
    {
        SelectSection(null); SelectedResultsGrid = null;
        if ((string?)CheckFilter.SelectedItem == "Stabilità globale") { checks.Content = GlobalResultsPanel(); return; }
        if (Calculation is not { } r || Combination.SelectedItem is not string name) return; var c = r.Cases.FirstOrDefault(c => c.Name == name); if (c is null) return;
        if ((string)CheckFilter.SelectedItem is "Portanza sismica" or "Cedimenti e spostamenti" or "Dettagli armature") { checks.Content = AdvancedResults((string)CheckFilter.SelectedItem, r, c); return; }
        if ((string)CheckFilter.SelectedItem == "Calcolo delle spinte") { checks.Content = PressurePanel(c); return; }
        if ((string)CheckFilter.SelectedItem == "Sollecitazioni numeriche")
        {
            var table = Table([.. RetainingWallPresentation.SectionColumns], RetainingWallPresentation.SectionRows(Data, c)); table.Height = 285;
            table.SelectionChanged += (_, _) => SelectSection(RetainingWallPresentation.ForceSection(c, table.SelectedIndex));
            SelectedResultsGrid = table; checks.Content = table; return;
        }
        var rows = RetainingWallPresentation.FilterChecks(r, (string)CheckFilter.SelectedItem, name); var grid = CheckTable(rows); grid.Height = 285;
        grid.SelectionChanged += (_, _) => SelectSection(grid.SelectedIndex >= 0 ? RetainingWallPresentation.CheckSection(rows[grid.SelectedIndex], c) : null);
        SelectedResultsGrid = grid;
        var text = Ui.Text(RetainingWallPresentation.CheckHeader(c), 12);
        checks.Content = Ui.Stack(text, grid, Ui.Text(RetainingWallPresentation.CheckFooter, 11, color: Ui.Muted));
    }
    internal static string RebarLabel(JsonObject d, string key) => WallModel.RebarLabel(d, key);

}
