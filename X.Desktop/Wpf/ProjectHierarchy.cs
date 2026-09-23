using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    private const string ModuleDragFormat = "ANTHEA.ProjectModule";
    private const string SheetDragFormat = "ANTHEA.ProjectSheet";

    private void EnableProjectDrag(FrameworkElement source, string format, object value)
    {
        Point? start = null;
        source.PreviewMouseLeftButtonDown += (_, e) => start = e.GetPosition(source);
        source.PreviewMouseLeftButtonUp += (_, _) => start = null;
        source.MouseMove += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed) { start = null; return; }
            if (start is not Point origin) return;
            var position = e.GetPosition(source);
            if (Math.Abs(position.X - origin.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(position.Y - origin.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            start = null; e.Handled = true;
            Safe(() => DragDrop.DoDragDrop(source, new DataObject(format, value),
                format == SheetDragFormat ? DragDropEffects.Move : DragDropEffects.Copy));
        };
    }

    private void ConfigureProjectNode(TreeViewItem item, JsonObject value)
    {
        if (value.ContainsKey("modulo_id"))
        {
            EnableProjectDrag(item, SheetDragFormat, value);
            item.MouseDoubleClick += (_, e) => { e.Handled = true; Safe(() => { Commit(); ShowSheet(value); }); };
            item.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; Safe(() => { Commit(); ShowSheet(value); }); } };
            return;
        }
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(Ui.Text(value.S("nome", "Sezione"), 14));
        var add = Ui.Button("+", () => Safe(() => AddStructureTo(value)));
        add.ToolTip = "Aggiungi sottosezione"; add.Padding = new Thickness(6, 0, 6, 0);
        add.Margin = new Thickness(10, 0, 0, 0); header.Children.Add(add); item.Header = header;
        item.AllowDrop = true;
        DragDropEffects Effect(IDataObject data)
        {
            if (!value.ContainsKey("fogli")) return DragDropEffects.None;
            if (data.GetData(ModuleDragFormat) is string module && Archivio.Moduli.Contains(module)) return DragDropEffects.Copy;
            if (data.GetData(SheetDragFormat) is JsonObject sheet && sheet.Parent is JsonArray list &&
                ReferenceEquals(sheet.Root, document) && !ReferenceEquals(list, value["fogli"])) return DragDropEffects.Move;
            return DragDropEffects.None;
        }
        item.DragOver += (_, e) => { e.Effects = Effect(e.Data); e.Handled = true; if (e.Effects != DragDropEffects.None) { item.IsExpanded = true; item.IsSelected = true; } };
        item.Drop += (_, e) =>
        {
            e.Handled = true; e.Effects = Effect(e.Data);
            if (e.Effects == DragDropEffects.None) return;
            Safe(() =>
            {
                if (e.Data.GetData(ModuleDragFormat) is string module) AddSheetTo(module, value, false);
                else if (e.Data.GetData(SheetDragFormat) is JsonObject sheet) MoveProjectSheet(sheet, value);
            });
        };
    }

    private void MoveProjectSheet(JsonObject sheet, JsonObject destination)
    {
        if (sheet.Parent is not JsonArray source || destination["fogli"] is not JsonArray target || ReferenceEquals(source, target)) return;
        Commit(); source.Remove(sheet); target.Add(sheet); MarkDirty(); RefreshTree(sheet);
    }
}
