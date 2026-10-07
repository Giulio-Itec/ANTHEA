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
        RetainingWall.CompleteSoilInput(Data);
        var valley = Data["valley"]!.AsObject();
        var mode = Form("valley", [new("height_mode", "Tratto libero a valle", Choices: ["Interamente libero", "Assegnato"]),
            new("free_height", "Dalla sommità al terreno di valle", "m", Symbol: "Hlib"), new("linked", "Modifica insieme le due colonne", Bool: true),
            new("passive", "Considera la resistenza passiva a valle", Bool: true), new("mobilization", "Frazione mobilitata (0–1)", "−", Symbol: "ηp")], valley,
            key => { if (key == "linked" && valley.B("linked")) SyncSoils(false); });
        FrameworkElement Column(bool front)
        {
            var array = front ? valley.Array("layers") : Data.Array("layers");
            var grid = GridFor([new("name", "Terreno"), new("thickness", "Δz\n[m]"), new("gamma", "γ\n[kN/m³]"), new("gamma_sat", "γsat\n[kN/m³]"), new("phi", "φ′\n[°]"), new("__to", "Fondo z\n[m]", ReadOnly: true)], array,
                _ => { SyncSoils(front); UpdateColumnDepths(); }, 150);
            if (front) ValleyLayerGrid = grid; else LayerGrid = grid;
            for (int i = 0; i < grid.Columns.Count; i++) { grid.Columns[i].Width = new DataGridLength(i == 0 ? 2 : 1, DataGridLengthUnitType.Star); grid.Columns[i].MinWidth = i == 0 ? 80 : 40; }
            grid.LoadingRow += (_, e) => { e.Row.BorderBrush = Appearance.Outline(RetainingWallDrawing.LayerColors[e.Row.GetIndex() % RetainingWallDrawing.LayerColors.Length]); e.Row.BorderThickness = new Thickness(5, 0, 0, 0); };
            grid.SelectionChanged += (_, _) => { Drawing.SelectedLayer = grid.SelectedIndex; Drawing.SelectedValley = front; Drawing.InvalidateVisual(); };
            return Ui.Stack(Ui.Text(front ? "VALLE · z dalla superficie a valle" : "MONTE · z dalla sommità", 13, true), grid,
                Ui.Bar(Ui.Button("+ Strato", () => AddSoilLayer(front)), Ui.Button("−", () => RemoveSoilLayer(front)), Ui.Button("↑", () => MoveSoilLayer(front, -1)), Ui.Button("↓", () => MoveSoilLayer(front, 1))));
        }
        var columns = new Grid(); columns.ColumnDefinitions.Add(new()); columns.ColumnDefinitions.Add(new());
        var left = Column(true); var right = Column(false); left.Margin = new Thickness(0, 0, 8, 0); Grid.SetColumn(right, 1); columns.Children.Add(left); columns.Children.Add(right);
        SoilButton = Ui.Button("Invia / carica terreno…", () => SoilTransferRequested?.Invoke());
        var interfaces = Form("interfaces", [new("wall_mode", "Attrito muro–terreno", Choices: RetainingWall.FrictionModes), new("wall_delta", "δ muro caratteristico assegnato", "°"),
            new("wall_phi_cv", "φcv,k terreno a contatto col muro", "°"), new("base_mode", "Attrito terreno–fondazione", Choices: RetainingWall.FrictionModes), new("base_phi_cv", "φcv,k terreno di fondazione", "°")]);
        var water = Form("water", [new("enabled", "Presenza falda", Bool: true), new("depth", "Profondità da sommità monte", "m", Symbol: "zf"), new("front_head", "Battente a valle dal piano di posa", "m", Symbol: "hw,v")]);
        return Ui.Stack(mode, columns, layerStatus, Ui.Bar(SoilButton), Ui.Text(RetainingWall.SoilHelp, 11, color: Ui.Muted),
            Group("Attriti e terreno di fondazione", Ui.Stack(interfaces, Form("foundation", Fields(RetainingWall.FoundationFields)), Ui.Text(RetainingWall.FrictionHelp, 11, color: Ui.Muted)), false),
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
        var layer = RetainingWall.Layer(); layer["thickness"] = 1;
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
        double ht = Data["geometry"].D("height") + Data["geometry"].D("slab"), dv = RetainingWall.ValleyHeight(Data); var labels = new List<string>(); bool valid = true;
        foreach (var (grid, name, need) in new[] { (ValleyLayerGrid, "Valle", dv), (LayerGrid, "Monte", ht) })
        {
            double z = 0; int i = 0;
            foreach (var row in grid.Rows) { row.Output("__index", ++i); row.Output("__from", F(z)); z += row.Values.D("thickness"); row.Output("__to", F(z)); }
            grid.Height = Math.Min(200, 72 + 31 * grid.Rows.Count); valid &= z + 1e-9 >= need; labels.Add($"{name}: {F(z)} m / {F(need)} m richiesti");
        }
        layerStatus.Text = string.Join(" · ", labels) + $"\nHlib={F(ht - dv)} m · terreno di valle Dv={F(dv)} m sopra il piano di posa"; layerStatus.Foreground = Appearance.Foreground(valid ? Ui.Muted : Brushes.Firebrick);
    }
    private void UpdateSoilFields()
    {
        if (Forms.TryGetValue("valley", out var v)) { v.Enable("free_height", Data["valley"].S("height_mode") == "Assegnato"); v.Enable("mobilization", Data["valley"].B("passive")); }
        if (Forms.TryGetValue("interfaces", out var f))
        {
            f.ShowField("wall_delta", Data["interfaces"].S("wall_mode") == "Assegnato");
            f.ShowField("wall_phi_cv", Data["interfaces"].S("wall_mode") is "Gettato in opera" or "Prefabbricato liscio");
            f.ShowField("base_phi_cv", Data["interfaces"].S("base_mode") is "Gettato in opera" or "Prefabbricato liscio");
            if (Forms.TryGetValue("foundation", out var foundation)) foundation.Enable("delta", Data["interfaces"].S("base_mode") == "Assegnato");
        }
    }
}
