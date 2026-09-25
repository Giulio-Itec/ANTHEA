using System.Text.Json.Nodes;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    private const string ModuleDragFormat = "ANTHEA.ProjectModule";
    private const string SheetDragFormat = "ANTHEA.ProjectSheet";
    private const string SectionDragFormat = "ANTHEA.ProjectSection";
    private DispatcherTimer? projectSectionClick;
    private Action<bool>? finishProjectRename;

    [DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    private void CancelProjectSectionClick()
    {
        projectSectionClick?.Stop(); projectSectionClick = null;
    }

    private void ScheduleProjectSectionClick(JsonObject section, FrameworkElement label)
    {
        CancelProjectSectionClick();
        var timer = projectSectionClick = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(GetDoubleClickTime()) };
        timer.Tick += (_, _) =>
        {
            CancelProjectSectionClick();
            if (label.IsLoaded && label.IsVisible && ReferenceEquals(section.Root, document))
                Safe(() => { Commit(); ShowProjectOverview(section); });
        };
        timer.Start();
    }

    private static string NextProjectName(JsonArray siblings, string prefix)
    {
        var names = siblings.Select(n => n.S("nome")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int index = 1; while (names.Contains(prefix + " " + index)) index++;
        return prefix + " " + index;
    }

    private void BeginProjectRename(TreeViewItem item)
    {
        if (projectReadOnly) return;
        if (item.Tag is not JsonObject value || item.Header is not Panel header) return;
        if (header.Children.OfType<TextBox>().FirstOrDefault() is TextBox existing) { existing.Focus(); return; }
        CancelProjectSectionClick(); finishProjectRename?.Invoke(true);
        var label = header.Children.OfType<TextBlock>().First();
        int index = header.Children.IndexOf(label);
        var input = new TextBox { Text = value.S("nome", label.Text), MinWidth = 180,
            FontSize = label.FontSize, Margin = label.Margin, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(input, Grid.GetColumn(label));
        bool finished = false;
        void OutsideClick(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject target && (ReferenceEquals(target, input) || input.IsAncestorOf(target))) return;
            Finish(true);
        }
        void Deactivate(object? sender, EventArgs e) => Finish(true);
        void Finish(bool save)
        {
            if (finished) return; finished = true;
            finishProjectRename = null;
            RemoveHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(OutsideClick));
            Deactivated -= Deactivate;
            string name = input.Text.Trim();
            if (save && name.Length > 0 && name != value.S("nome"))
            {
                value["nome"] = name; label.Text = name; MarkDirty();
                if (ReferenceEquals(value, currentSheet)) heading.Text = SheetHeading(value);
                if (ReferenceEquals(value, overviewSection) && overviewName is not null) overviewName.Text = name;
            }
            header.Children.Remove(input); header.Children.Insert(index, label);
        }
        input.KeyDown += (_, e) =>
        {
            if (e.Key is Key.Enter or Key.Escape) { e.Handled = true; Finish(e.Key == Key.Enter); item.Focus(); }
        };
        input.LostKeyboardFocus += (_, _) => Finish(true);
        input.Unloaded += (_, _) => Finish(true);
        input.Loaded += (_, _) => { input.Focus(); input.SelectAll(); };
        finishProjectRename = Finish;
        AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(OutsideClick), true);
        Deactivated += Deactivate;
        header.Children.RemoveAt(index); header.Children.Insert(index, input);
    }

    private void EnableProjectDrag(FrameworkElement source, string format, object value)
    {
        if (projectReadOnly) return;
        Point? start = null;
        source.PreviewMouseLeftButtonDown += (_, e) => { projectDragged = false; start = source is Panel panel && panel.Children.OfType<TextBox>().Any() ? null : e.GetPosition(source); };
        source.PreviewMouseLeftButtonUp += (_, _) => start = null;
        source.MouseMove += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed) { start = null; return; }
            if (start is not Point origin) return;
            var position = e.GetPosition(source);
            if (Math.Abs(position.X - origin.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(position.Y - origin.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            start = null; projectDragged = true; e.Handled = true;
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
        var header = new Grid { MinWidth = 155 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition());
        for (int i = 0; i < 3; i++) header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var label = Ui.Text(value.S("nome", isSheet ? ModuleName(value.S("modulo_id")) : "Sezione"),
            isSheet ? 14 : depth == 0 ? 20 : depth == 1 ? 17 : depth == 2 ? 15 : 14, !isSheet);
        Grid.SetColumn(label, 1); label.TextWrapping = TextWrapping.NoWrap; label.TextTrimming = TextTrimming.CharacterEllipsis;
        label.ToolTip = "Doppio clic per rinominare · F2";
        label.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount != 2) return;
            e.Handled = true; item.IsSelected = true; BeginProjectRename(item);
        };
        label.MouseLeftButtonUp += (_, e) =>
        {
            if (!isSheet && !projectDragged && !header.Children.OfType<TextBox>().Any())
            { e.Handled = true; ScheduleProjectSectionClick(value, label); }
        };
        item.Header = header;
        item.KeyDown += (_, e) =>
        {
            if (e.OriginalSource is TextBox) return;
            if (e.Key == Key.F2) { e.Handled = true; BeginProjectRename(item); }
            else if (isSheet && e.Key == Key.Enter) { e.Handled = true; Safe(() => { Commit(); ShowSheet(value); }); }
            else if (!isSheet && e.Key == Key.Enter) { e.Handled = true; Safe(() => { Commit(); ShowProjectOverview(value); }); }
        };
        if (isSheet)
        {
            var icon = new Viewbox { Child = Ui.ModuleIcon(value.S("modulo_id")), Width = 36, Height = 36, Stretch = System.Windows.Media.Stretch.Uniform, ClipToBounds = true };
            var open = ProjectButton("", () => Safe(() => { Commit(); ShowSheet(value); }));
            open.Content = icon; open.Padding = new Thickness(3); open.Margin = new Thickness(0, 0, 10, 0);
            open.BorderBrush = Ui.Brush("#B6CBE0"); open.Background = Ui.Brush("#F5F9FD");
            open.ToolTip = "Apri scheda · " + value.S("nome", ModuleName(value.S("modulo_id")));
            System.Windows.Automation.AutomationProperties.SetName(open, "Apri scheda " + value.S("nome"));
            header.Children.Add(open); header.Children.Add(label);
            EnableProjectDrag(header, SheetDragFormat, value);
            if (!projectReadOnly) EnableSheetReorder(item, header, value);
            return;
        }
        item.Background = Ui.Brush(depth == 0 ? "#F0F6FC" : "#FFFFFF");
        var folder = new ProjectGlyph(depth == 0 ? "project" : "folder") { Width = 22, Height = 22, Margin = new Thickness(3, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        header.Children.Add(folder);
        label.Margin = new Thickness(0, 3, 10, 3); header.Children.Add(label);
        label.Cursor = Cursors.SizeAll;
        label.ToolTip = projectReadOnly ? "Apri il riepilogo" : (depth == 0 ? "Trascina per riordinare i progetti" : "Trascina sul bordo per riordinare, al centro per inserire in una sezione") + " · Doppio clic per rinominare · F2";
        EnableProjectDrag(label, SectionDragFormat, value);
        var add = ProjectIconButton("+", () => Safe(() => AddStructureTo(value)));
        add.ToolTip = "Aggiungi sottosezione"; add.FontSize = 18;
        Grid.SetColumn(add, 3); if (!projectReadOnly) header.Children.Add(add);
        AddCoherenceBadge(header, value);
        var report = ProjectIconButton("", () => Safe(() => ExportSectionReport(value)));
        report.Content = new ProjectGlyph("report") { Width = 18, Height = 18 }; Grid.SetColumn(report, 4);
        report.IsEnabled = ProjectSharedData.SubtreeSheets(value).Any();
        report.ToolTip = report.IsEnabled ? "Report Word della sezione, comprese tutte le sottosezioni" : "Aggiungi una scheda alla sezione o alle sue sottosezioni";
        ToolTipService.SetShowOnDisabled(report, true);
        System.Windows.Automation.AutomationProperties.SetName(report, "Genera report " + value.S("nome"));
        header.Children.Add(report);
        if (projectReadOnly) return;
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
            if (e.Effects != DragDropEffects.None) { item.IsExpanded = true; projectDropHint.Text = "Inserisci in: " + value.S("nome"); header.Background = Ui.Brush("#D4E6F7"); }
        };
        item.DragLeave += (_, e) => { ClearProjectSectionDropIndicator(); header.Background = null; projectDropHint.Text = ""; e.Handled = true; };
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
        item.DragOver += (_, e) =>
        {
            e.Handled = true; e.Effects = Source(e.Data) is null ? DragDropEffects.None : DragDropEffects.Move;
            if (e.Effects == DragDropEffects.None) { ClearProjectSectionDropIndicator(); return; }
            bool after = e.GetPosition(header).Y >= header.ActualHeight / 2;
            // Draw above the row instead of changing its border and measured height.
            ShowProjectSectionDropIndicator(header, after);
            projectDropHint.Text = (after ? "Dopo " : "Prima di ") + anchor.S("nome") + " · " + anchor.Parent?.Parent?.S("nome");
        };
        item.DragLeave += (_, e) => { ClearProjectSectionDropIndicator(); e.Handled = true; };
        item.Drop += (_, e) =>
        {
            e.Handled = true; ClearProjectSectionDropIndicator();
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
        Commit();
        JsonObject? proposal = null;
        if (!ReferenceEquals(source, target))
        {
            proposal = PreviewProjectMove(sheet, destination);
            if (proposal is null) return;
        }
        source.Remove(sheet); target.Insert(index, sheet);
        if (proposal is not null) sheet["dati"] = proposal["dati"]!.DeepClone();
        MarkDirty(); ReloadMovedEditor(sheet); RefreshTree(sheet); RefreshSharedStatus();
        if (!ReferenceEquals(projectContent.Content, moduleView)) ShowProjectOverview(destination);
    }
}
