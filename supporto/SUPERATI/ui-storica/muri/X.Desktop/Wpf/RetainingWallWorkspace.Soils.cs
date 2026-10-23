using Anthea.Application.Walls;
using Anthea.Presentation.Modules.RetainingWall;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace X.Desktop;

internal sealed partial class RetainingWallWorkspace
{
    internal JsonGrid ValleyLayerGrid = null!;
    private bool syncingSoils;
    private FrameworkElement BuildSoilColumns()
    {
        WallModel.CompleteSoilInput(Data);
        var valley = Data["valley"]!.AsObject();
        var mode = Form("valley", RetainingWallFields.Valley, valley,
            key => { if (key == "linked" && valley.B("linked")) SyncSoils(false); });
        FrameworkElement Column(bool front)
        {
            var array = front ? valley.Array("layers") : Data.Array("layers");
            var grid = GridFor(RetainingWallFields.SoilColumns, array,
                _ => { SyncSoils(front); UpdateColumnDepths(); }, 150);
            if (front) ValleyLayerGrid = grid; else LayerGrid = grid;
            for (int i = 0; i < grid.Columns.Count; i++) { grid.Columns[i].Width = new DataGridLength(i == 0 ? 2 : 1, DataGridLengthUnitType.Star); grid.Columns[i].MinWidth = i == 0 ? 80 : 40; }
            grid.LoadingRow += (_, e) => { e.Row.BorderBrush = Appearance.Swatch(RetainingWallDrawing.LayerColors[e.Row.GetIndex() % RetainingWallDrawing.LayerColors.Length]); e.Row.BorderThickness = new Thickness(5, 0, 0, 0); };
            grid.SelectionChanged += (_, _) => { Drawing.SelectedLayer = grid.SelectedIndex; Drawing.SelectedValley = front; Drawing.InvalidateVisual(); };
            return Ui.Stack(Ui.Text(front ? "VALLE · z dalla superficie a valle" : "MONTE · z dalla sommità", 13, true), grid,
                Ui.Bar(Ui.Button("+ Strato", () => AddSoilLayer(front)), Ui.Button("−", () => RemoveSoilLayer(front)), Ui.Button("↑", () => MoveSoilLayer(front, -1)), Ui.Button("↓", () => MoveSoilLayer(front, 1))));
        }
        var columns = new Grid(); columns.ColumnDefinitions.Add(new()); columns.ColumnDefinitions.Add(new());
        var left = Column(true); var right = Column(false); left.Margin = new Thickness(0, 0, 8, 0); Grid.SetColumn(right, 1); columns.Children.Add(left); columns.Children.Add(right);
        SoilButton = Ui.Button("Invia / carica terreno…", () => SoilTransferRequested?.Invoke());
        var interfaces = Form("interfaces", RetainingWallFields.Interfaces);
        var water = Form("water", RetainingWallFields.Water);
        return Ui.Stack(mode, columns, layerStatus, Ui.Bar(SoilButton), Ui.Text(WallModel.SoilHelp, 11, color: Ui.Muted),
            Group("Attriti e terreno di fondazione", Ui.Stack(interfaces, Form("foundation", RetainingWallFields.Foundation), Ui.Text(WallModel.FrictionHelp, 11, color: Ui.Muted)), false),
            Group("Falda e sottospinta", water, false), GlobalCard());
    }
    internal void SyncSoils(bool fromValley)
    {
        if (syncingSoils || !Data["valley"].B("linked") || LayerGrid is null || ValleyLayerGrid is null) return;
        syncingSoils = true;
        try
        {
            var source = fromValley ? Data["valley"]!.Array("layers") : Data.Array("layers");
            var target = fromValley ? Data.Array("layers") : Data["valley"]!.Array("layers");
            var grid = fromValley ? LayerGrid : ValleyLayerGrid;
            target.Clear(); grid.Rows.Clear();
            foreach (var l in source) { var copy = (JsonObject)l!.DeepClone(); target.Add(copy); grid.Rows.Add(new JsonRow(copy, _ => { SyncSoils(!fromValley); UpdateColumnDepths(); Changed(); })); }
        }
        finally { syncingSoils = false; }
    }
    internal void AddSoilLayer(bool front)
    {
        var grid = front ? ValleyLayerGrid : LayerGrid; var array = front ? Data["valley"]!.Array("layers") : Data.Array("layers"); if (array.Count >= 50) return;
        var layer = WallModel.Layer(); layer["thickness"] = 1;
        AddRow(grid, array, layer, _ => { SyncSoils(front); UpdateColumnDepths(); }); SyncSoils(front); UpdateColumnDepths(); Changed();
    }
    private void RemoveSoilLayer(bool front)
    {
        var grid = front ? ValleyLayerGrid : LayerGrid; if (grid.Rows.Count <= 1 || grid.SelectedItem is not JsonRow row) return;
        (front ? Data["valley"]!.Array("layers") : Data.Array("layers")).Remove(row.Values); grid.Rows.Remove(row); SyncSoils(front); UpdateColumnDepths(); Changed();
    }
    private void MoveSoilLayer(bool front, int step)
    {
        var grid = front ? ValleyLayerGrid : LayerGrid; int i = grid.SelectedIndex, j = i + step; if (i < 0 || j < 0 || j >= grid.Rows.Count) return;
        var array = front ? Data["valley"]!.Array("layers") : Data.Array("layers"); var node = array[i]; array.RemoveAt(i); array.Insert(j, node); grid.Rows.Move(i, j); grid.SelectedIndex = j;
        SyncSoils(front); UpdateColumnDepths(); Changed();
    }
    private void UpdateColumnDepths()
    {
        if (LayerGrid is null || ValleyLayerGrid is null) return;
        var summary = Anthea.Application.Presentation.WallSheetPresentation.DescribeColumnDepths(Data,
            ValleyLayerGrid.Rows.Select(r => r.Values), LayerGrid.Rows.Select(r => r.Values));
        foreach (var grid in new[] { ValleyLayerGrid, LayerGrid })
        {
            foreach (var row in grid.Rows) row.Refresh();
            grid.Height = Math.Min(200, 72 + 31 * grid.Rows.Count);
        }
        layerStatus.Text = summary.Text; layerStatus.Foreground = Appearance.Foreground(summary.Valid ? Ui.Muted : Brushes.Firebrick);
    }
}
