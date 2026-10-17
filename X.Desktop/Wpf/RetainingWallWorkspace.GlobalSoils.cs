using Anthea.Application.Walls;
using Anthea.Application.Presentation;
using Anthea.Presentation.Modules.RetainingWall;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;

namespace X.Desktop;

internal sealed partial class RetainingWallWorkspace
{
    internal JsonGrid GlobalValleyLayers = null!;
    private double GlobalSoilTop(bool valley) => RetainingWallPresentation.GlobalSoilTop(Data, valley);
    private FrameworkElement BuildGlobalSoilEditor(bool valley)
    {
        string key = valley ? "valley_layers" : "layers", formKey = valley ? "global_soil_valley" : "global_soil_rear";
        var layers = Data["global_stability"]!.Array(key);
        var grid = new JsonGrid(RetainingWallFields.GlobalLayerColumns, stretch: true)
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
            var form = Form(formKey, RetainingWallFields.GlobalSoil, layer, _ => InvalidateGlobalConfirmation());
            details.Content = Ui.Stack(Ui.Text("Proprietà dello strato selezionato", 11, true), form);
            // The properties of the deep layers for the condition (drained: φ′ and c′; undrained: cu), then the colours of the rows.
            foreach (var (key, field, visible) in RetainingWallPresentation.GlobalRules(Data).Fields.Where(f => f.Form.StartsWith("global_soil_", StringComparison.Ordinal)))
                if (Forms.TryGetValue(key, out var soil)) soil.ShowField(field, visible);
            RefreshGlobalSoilDetails();
            if (globalPreview is not null) { globalPreview.SelectedSoil = grid.SelectedIndex; globalPreview.SelectedColumn = valley ? "valle" : "monte"; globalPreview.InvalidateVisual(); }
        }
        void UpdateBottoms()
        {
            var bottoms = RetainingWallPresentation.GlobalBottoms(GlobalSoilTop(valley), grid.Rows.Select(r => r.Values["__thickness"]));
            for (int i = 0; i < grid.Rows.Count; i++)
            {
                double bottom = bottoms[i];
                layers[i]!["bottom"] = double.IsFinite(bottom) ? JsonValue.Create(bottom) : JsonValue.Create("");
                grid.Rows[i].Output("bottom", double.IsFinite(bottom) ? bottom : "");
            }
        }
        void AddView(JsonObject source)
        {
            var view = (JsonObject)source.DeepClone();
            int i = grid.Rows.Count; double upper = i == 0 ? GlobalSoilTop(valley) : J.Number(layers[i - 1]?["bottom"]) ?? double.NaN;
            view["__thickness"] = RetainingWallPresentation.GlobalThickness(upper, source["bottom"]);
            grid.Rows.Add(new JsonRow(view, changed =>
            {
                if (changed == "__thickness") UpdateBottoms(); else source[changed] = view[changed]?.DeepClone();
                InvalidateGlobalConfirmation(); Changed();
            }));
        }
        foreach (var layer in layers.OfType<JsonObject>()) AddView(layer);
        grid.LoadingRow += (_, e) => { e.Row.BorderBrush = Appearance.Swatch(RetainingWallDrawing.GlobalLayerColor(Data, ((JsonRow)e.Row.Item).Values)); e.Row.BorderThickness = new Thickness(5, 0, 0, 0); };
        grid.SelectionChanged += (_, _) => Selected(); grid.SelectedIndex = grid.Rows.Count > 0 ? 0 : -1;
        return Ui.Stack(grid, Ui.Bar(Ui.Button("+ Strato", () =>
        {
            grid.Commit(); var layer = RetainingWallPresentation.NewGlobalLayer();
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
        foreach (var grid in new[] { GlobalLayers, GlobalValleyLayers }) if (grid is not null)
            foreach (var row in grid.Rows) if (grid.ItemContainerGenerator.ContainerFromItem(row) is DataGridRow visual)
                visual.BorderBrush = Appearance.Swatch(RetainingWallDrawing.GlobalLayerColor(Data, row.Values));
    }
}
