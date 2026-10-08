using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;

namespace X.Desktop;

internal sealed partial class RetainingWallWorkspace
{
    internal JsonGrid GlobalValleyLayers = null!;
    private double GlobalSoilTop(bool valley)
    {
        var points = Data["global_stability"]!.Array(valley ? "valley" : "uphill");
        return points.Count > 0 ? (valley ? points[^1] : points[0]).D("y") : valley ? RetainingWall.ValleyHeight(Data) : Data["geometry"].D("height") + Data["geometry"].D("slab");
    }
    private FrameworkElement BuildGlobalSoilEditor(bool valley)
    {
        string key = valley ? "valley_layers" : "layers", formKey = valley ? "global_soil_valley" : "global_soil_rear";
        var layers = Data["global_stability"]!.Array(key);
        var grid = new JsonGrid([new("name", "Terreno"), new("__thickness", "Spessore [m]"), new("bottom", "Fondo y [m]", ReadOnly: true)], stretch: true)
            { Height = 135, RowHeight = 29, ColumnHeaderHeight = 32 };
        grid.Columns[0].Width = new DataGridLength(1.8, DataGridLengthUnitType.Star); grids.Add(grid); CompactHeaders(grid);
        if (valley) GlobalValleyLayers = grid; else GlobalLayers = grid;
        var details = new ContentControl();
        void Selected()
        {
            if (Forms.Remove(formKey, out var previous)) previous.Commit();
            details.Content = null;
            if (grid.SelectedIndex < 0 || grid.SelectedIndex >= layers.Count) return;
            var layer = layers[grid.SelectedIndex]!.AsObject();
            var form = Form(formKey, [new("gamma", "Peso naturale", "kN/m³", Symbol: "γ"), new("gamma_sat", "Peso saturo", "kN/m³", Symbol: "γsat"),
                new("phi", "Angolo di attrito caratteristico", "°", Symbol: "φ′k"), new("c", "Coesione efficace caratteristica", "kPa", Symbol: "c′k"), new("cu", "Resistenza non drenata caratteristica", "kPa", Symbol: "cu,k")], layer, _ => InvalidateGlobalConfirmation());
            details.Content = Ui.Stack(Ui.Text("Proprietà dello strato selezionato", 11, true), form);
            RefreshGlobalSoilDetails();
            if (globalPreview is not null) { globalPreview.SelectedSoil = grid.SelectedIndex; globalPreview.SelectedColumn = valley ? "valle" : "monte"; globalPreview.InvalidateVisual(); }
        }
        void UpdateBottoms()
        {
            double bottom = GlobalSoilTop(valley);
            for (int i = 0; i < grid.Rows.Count; i++)
            {
                double? h = J.Number(grid.Rows[i].Values["__thickness"]);
                bottom = h is > 0 && double.IsFinite(h.Value) && double.IsFinite(bottom) ? bottom - h.Value : double.NaN;
                layers[i]!["bottom"] = double.IsFinite(bottom) ? JsonValue.Create(bottom) : JsonValue.Create("");
                grid.Rows[i].Output("bottom", double.IsFinite(bottom) ? bottom : "");
            }
        }
        void AddView(JsonObject source)
        {
            var view = (JsonObject)source.DeepClone();
            int i = grid.Rows.Count; double upper = i == 0 ? GlobalSoilTop(valley) : J.Number(layers[i - 1]?["bottom"]) ?? double.NaN;
            double thickness = upper - (J.Number(source["bottom"]) ?? double.NaN);
            view["__thickness"] = double.IsFinite(thickness) ? JsonValue.Create(thickness) : JsonValue.Create("");
            grid.Rows.Add(new JsonRow(view, changed =>
            {
                if (changed == "__thickness") UpdateBottoms(); else source[changed] = view[changed]?.DeepClone();
                InvalidateGlobalConfirmation(); Changed();
            }));
        }
        foreach (var layer in layers.OfType<JsonObject>()) AddView(layer);
        grid.LoadingRow += (_, e) => { e.Row.BorderBrush = Appearance.Outline(RetainingWallDrawing.GlobalLayerColor(Data, ((JsonRow)e.Row.Item).Values)); e.Row.BorderThickness = new Thickness(5, 0, 0, 0); };
        grid.SelectionChanged += (_, _) => Selected(); grid.SelectedIndex = grid.Rows.Count > 0 ? 0 : -1;
        return Ui.Stack(grid, Ui.Bar(Ui.Button("+ Strato", () =>
        {
            grid.Commit(); var layer = J.Obj(("name", "Nuovo terreno"), ("bottom", ""), ("gamma", ""), ("gamma_sat", ""), ("phi", ""), ("c", ""), ("cu", ""));
            layers.Add(layer); AddView(layer); grid.SelectedIndex = grid.Rows.Count - 1; InvalidateGlobalConfirmation(); Changed();
        }), Ui.Button("− Strato", () =>
        {
            if (grid.SelectedIndex < 0) return; grid.Commit(); int i = grid.SelectedIndex;
            grid.SelectedIndex = -1; layers.RemoveAt(i); grid.Rows.RemoveAt(i); UpdateBottoms();
            grid.SelectedIndex = Math.Min(i, grid.Rows.Count - 1); InvalidateGlobalConfirmation(); Changed();
        })), details);
    }
    private void RefreshGlobalLayerDisplays()
    {
        foreach (bool valley in new[] { false, true })
        {
            var grid = valley ? GlobalValleyLayers : GlobalLayers; if (grid is null) continue;
            var layers = Data["global_stability"]!.Array(valley ? "valley_layers" : "layers");
            double upper = GlobalSoilTop(valley);
            for (int i = 0; i < grid.Rows.Count && i < layers.Count; i++)
            {
                double lower = J.Number(layers[i]?["bottom"]) ?? double.NaN;
                grid.Rows[i].Output("__thickness", double.IsFinite(upper - lower) ? upper - lower : ""); upper = lower;
            }
        }
    }
    private void RefreshGlobalSoilDetails()
    {
        bool drained = Data["global_stability"].S("condition") != "Non drenata";
        foreach (string key in new[] { "global_soil_rear", "global_soil_valley" }) if (Forms.TryGetValue(key, out var form))
        { form.ShowField("phi", drained); form.ShowField("c", drained); form.ShowField("cu", !drained); }
        foreach (var grid in new[] { GlobalLayers, GlobalValleyLayers }) if (grid is not null)
            foreach (var row in grid.Rows) if (grid.ItemContainerGenerator.ContainerFromItem(row) is DataGridRow visual)
                visual.BorderBrush = Appearance.Outline(RetainingWallDrawing.GlobalLayerColor(Data, row.Values));
    }
}
