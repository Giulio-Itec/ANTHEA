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
    private const string SectionDragFormat = "ANTHEA.ProjectSection";

    private static string NextProjectName(JsonArray siblings, string prefix)
    {
        var names = siblings.Select(n => n.S("nome")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int index = 1; while (names.Contains(prefix + " " + index)) index++;
        return prefix + " " + index;
    }

    private void BeginProjectRename(TreeViewItem item)
    {
        if (item.Tag is not JsonObject value || item.Header is not StackPanel header) return;
        if (header.Children.OfType<TextBox>().FirstOrDefault() is TextBox existing) { existing.Focus(); return; }
        var label = header.Children.OfType<TextBlock>().First();
        int index = header.Children.IndexOf(label);
        var input = new TextBox { Text = value.S("nome", label.Text), MinWidth = 180,
            FontSize = label.FontSize, Margin = label.Margin, VerticalAlignment = VerticalAlignment.Center };
        header.Children.RemoveAt(index); header.Children.Insert(index, input);
        bool finished = false;
        void Finish(bool save)
        {
            if (finished) return; finished = true;
            string name = input.Text.Trim();
            if (save && name.Length > 0 && name != value.S("nome"))
            {
                value["nome"] = name; label.Text = name; MarkDirty();
                if (ReferenceEquals(value, currentSheet)) heading.Text = name;
            }
            header.Children.Remove(input); header.Children.Insert(index, label);
        }
        input.KeyDown += (_, e) =>
        {
            if (e.Key is Key.Enter or Key.Escape) { e.Handled = true; Finish(e.Key == Key.Enter); item.Focus(); }
        };
        input.LostKeyboardFocus += (_, _) => Finish(true);
        input.Loaded += (_, _) => { input.Focus(); input.SelectAll(); };
    }

    private void EnableProjectDrag(FrameworkElement source, string format, object value)
    {
        Point? start = null;
        source.PreviewMouseLeftButtonDown += (_, e) => start = source is Panel panel && panel.Children.OfType<TextBox>().Any() ? null : e.GetPosition(source);
        source.PreviewMouseLeftButtonUp += (_, _) => start = null;
        source.MouseMove += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed) { start = null; return; }
            if (start is not Point origin) return;
            var position = e.GetPosition(source);
            if (Math.Abs(position.X - origin.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(position.Y - origin.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            start = null; e.Handled = true;
            Safe(() =>
            {
                try { DragDrop.DoDragDrop(source, new DataObject(format, value),
                    format == ModuleDragFormat ? DragDropEffects.Copy : DragDropEffects.Move); }
                finally { ClearProjectSectionDropIndicator(); }
            });
        };
    }

    private void ConfigureProjectNode(TreeViewItem item, JsonObject value)
    {
        bool isSheet = value.ContainsKey("modulo_id");
        int depth = 0;
        for (var ancestor = value.Parent?.Parent; ancestor?.Parent?.Parent is JsonObject; ancestor = ancestor.Parent.Parent) depth++;
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        var label = Ui.Text(value.S("nome", isSheet ? ModuleName(value.S("modulo_id")) : "Sezione"),
            isSheet ? 14 : depth == 0 ? 20 : depth == 1 ? 17 : depth == 2 ? 15 : 14, !isSheet);
        label.ToolTip = "Doppio clic per rinominare · F2";
        label.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount != 2) return;
            e.Handled = true; item.IsSelected = true; BeginProjectRename(item);
        };
        item.Header = header;
        item.KeyDown += (_, e) =>
        {
            if (e.OriginalSource is TextBox) return;
            if (e.Key == Key.F2) { e.Handled = true; BeginProjectRename(item); }
            else if (isSheet && e.Key == Key.Enter) { e.Handled = true; Safe(() => { Commit(); ShowSheet(value); }); }
        };
        if (isSheet)
        {
            var icon = new Viewbox { Child = Ui.ModuleIcon(value.S("modulo_id")), Width = 36, Height = 36, Stretch = System.Windows.Media.Stretch.Uniform, ClipToBounds = true };
            var open = Ui.Button("", () => Safe(() => { Commit(); ShowSheet(value); }));
            open.Content = icon; open.Padding = new Thickness(4); open.Margin = new Thickness(0, 0, 10, 0);
            open.BorderThickness = new Thickness(1); open.BorderBrush = Ui.Blue; open.Background = Ui.Brush("#EEF4FA");
            open.ToolTip = "Apri scheda · " + value.S("nome", ModuleName(value.S("modulo_id")));
            System.Windows.Automation.AutomationProperties.SetName(open, "Apri scheda " + value.S("nome"));
            header.Children.Add(open); header.Children.Add(label);
            if (value.Parent?.Parent is JsonObject owner && owner.Array("strutture").Count > 0)
            {
                bool hasOwnReference = ProjectSharedData.ReferenceKeys(value, ProjectSharedData.Fields(value).Keys).Count > 0;
                var role = Ui.Text(hasOwnReference ? "Riferimento per le sottosezioni" : "Dati dal livello superiore", 11, color: Ui.Muted); role.Margin = new Thickness(14, 0, 0, 0);
                role.ToolTip = "Guida i dati comuni delle sottosezioni, nel rispetto degli eventuali riferimenti dei livelli superiori.";
                header.Children.Add(role);
            }
            EnableProjectDrag(header, SheetDragFormat, value);
            EnableSheetReorder(item, header, value);
            return;
        }
        header.Background = Ui.Brush(depth == 0 ? "#E1EAF4" : depth == 1 ? "#EFF4F9" : "#F7F9FC");
        label.Margin = new Thickness(8, 3, 6, 3); header.Children.Add(label);
        label.Cursor = Cursors.SizeAll;
        label.ToolTip = (depth == 0 ? "Trascina per riordinare i progetti" : "Trascina sopra o sotto una sezione dello stesso gruppo") + " · Doppio clic per rinominare · F2";
        EnableProjectDrag(label, SectionDragFormat, value);
        var add = Ui.Button("+", () => Safe(() => AddStructureTo(value)));
        add.ToolTip = "Aggiungi sottosezione"; add.Padding = new Thickness(6, 0, 6, 0);
        add.Margin = new Thickness(10, 0, 4, 0); header.Children.Add(add);
        AddCoherenceBadge(header, value);
        var report = Ui.Button("Genera report", () => Safe(() => ExportSectionReport(value)));
        report.FontSize = 11; report.Margin = new Thickness(8, 0, 4, 0); report.Padding = new Thickness(8, 4, 8, 4);
        report.IsEnabled = ProjectSharedData.SubtreeSheets(value).Any();
        report.ToolTip = report.IsEnabled ? "Report Word della sezione, comprese tutte le sottosezioni" : "Aggiungi una scheda alla sezione o alle sue sottosezioni";
        ToolTipService.SetShowOnDisabled(report, true);
        System.Windows.Automation.AutomationProperties.SetName(report, "Genera report " + value.S("nome"));
        header.Children.Add(report);
        item.AllowDrop = true;
        DragDropEffects Effect(IDataObject data)
        {
            if (!(value.ContainsKey("fogli") || value.ContainsKey("strutture"))) return DragDropEffects.None;
            if (data.GetData(ModuleDragFormat) is string module && Archivio.Moduli.Contains(module)) return DragDropEffects.Copy;
            if (data.GetData(SheetDragFormat) is JsonObject sheet && sheet.Parent is JsonArray list &&
                ReferenceEquals(sheet.Root, document) && !ReferenceEquals(list, value["fogli"])) return DragDropEffects.Move;
            return DragDropEffects.None;
        }
        item.DragOver += (_, e) =>
        {
            if (HandleProjectSectionDrag(header, value, e, false)) return;
            ClearProjectSectionDropIndicator();
            e.Effects = Effect(e.Data); e.Handled = true;
            if (e.Effects != DragDropEffects.None) { item.IsExpanded = true; item.IsSelected = true; }
        };
        item.DragLeave += (_, e) => { ClearProjectSectionDropIndicator(); e.Handled = true; };
        item.Drop += (_, e) =>
        {
            if (HandleProjectSectionDrag(header, value, e, true)) return;
            ClearProjectSectionDropIndicator();
            e.Handled = true; e.Effects = Effect(e.Data);
            if (e.Effects == DragDropEffects.None) return;
            Safe(() =>
            {
                if (e.Data.GetData(ModuleDragFormat) is string module) AddSheetTo(module, value, false);
                else if (e.Data.GetData(SheetDragFormat) is JsonObject sheet) MoveProjectSheet(sheet, value);
            });
        };
    }

    private void EnableSheetReorder(TreeViewItem item, FrameworkElement header, JsonObject anchor)
    {
        item.AllowDrop = true;
        JsonObject? Source(IDataObject data) => data.GetData(SheetDragFormat) is JsonObject sheet &&
            sheet.ContainsKey("modulo_id") && sheet.Parent is JsonArray && ReferenceEquals(sheet.Root, document) &&
            !ReferenceEquals(sheet, anchor) ? sheet : null;
        void ClearIndicator() { item.ClearValue(Control.BorderBrushProperty); item.ClearValue(Control.BorderThicknessProperty); }
        item.DragOver += (_, e) =>
        {
            ClearProjectSectionDropIndicator();
            e.Handled = true; e.Effects = Source(e.Data) is null ? DragDropEffects.None : DragDropEffects.Move;
            ClearIndicator();
            if (e.Effects == DragDropEffects.None) return;
            bool after = e.GetPosition(header).Y >= header.ActualHeight / 2;
            item.BorderBrush = Ui.Blue; item.BorderThickness = after ? new Thickness(0, 0, 0, 2) : new Thickness(0, 2, 0, 0);
        };
        item.DragLeave += (_, e) => { ClearIndicator(); e.Handled = true; };
        item.Drop += (_, e) =>
        {
            e.Handled = true; ClearIndicator();
            var sheet = Source(e.Data); e.Effects = sheet is null ? DragDropEffects.None : DragDropEffects.Move;
            if (sheet is null || anchor.Parent?.Parent is not JsonObject destination) return;
            bool after = e.GetPosition(header).Y >= header.ActualHeight / 2;
            Safe(() => MoveProjectSheet(sheet, destination, anchor, after));
        };
    }

    private void MoveProjectSheet(JsonObject sheet, JsonObject destination, JsonObject? anchor = null, bool after = false)
    {
        if (destination["fogli"] is null && destination.ContainsKey("strutture")) destination["fogli"] = new JsonArray();
        if (sheet.Parent is not JsonArray source || destination["fogli"] is not JsonArray target) return;
        if (anchor is null && ReferenceEquals(source, target) || ReferenceEquals(sheet, anchor)) return;
        if (anchor is not null && !ReferenceEquals(anchor.Parent, target)) return;
        int index = anchor is null ? target.Count : target.IndexOf(anchor) + (after ? 1 : 0);
        if (ReferenceEquals(source, target))
        {
            int previous = source.IndexOf(sheet);
            if (previous < index) index--;
            if (previous == index) return;
        }
        Commit(); source.Remove(sheet); target.Insert(index, sheet); MarkDirty(); RefreshTree(sheet); RefreshSharedStatus();
    }
}
