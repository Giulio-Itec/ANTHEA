using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    private TextBox projectCatalogSearch = null!;
    private readonly List<(string Module, Border Card)> projectCatalogCards = [];
    private Border projectEmptyState = null!;
    private TextBlock projectTreeSummary = null!;
    private JsonObject? overviewSection;
    private TextBlock? overviewName;

    private static Border ProjectPanel(UIElement content, double padding = 14) => new()
    {
        Child = content, Background = Brushes.White, BorderBrush = Ui.Brush("#DCE5EF"), BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(6), Padding = new Thickness(padding)
    };
    private static Button ProjectButton(string title, Action action, bool primary = false)
    {
        var button = Ui.Button(title, action, primary); button.Style = (Style)Application.Current.FindResource("ProjectButton");
        return button;
    }
    private static Button ProjectIconButton(string title, Action action)
    {
        var button = Ui.Button(title, action); button.Style = (Style)Application.Current.FindResource("ProjectIconButton");
        button.Background = Brushes.Transparent;
        return button;
    }
    private static Expander ProjectGroup(string title, UIElement content, bool expanded = true) => new()
    {
        Header = Ui.Text(title, 14, true), Content = content, IsExpanded = expanded,
        Style = (Style)Application.Current.FindResource("ProjectExpander")
    };
    private static Border ProjectCard(string title, params UIElement[] content)
    {
        var panel = Ui.Stack(Ui.Text(title, 15, true)); panel.Children[0].SetValue(MarginProperty, new Thickness(0, 0, 0, 10));
        foreach (var item in content) panel.Children.Add(item);
        var card = ProjectPanel(panel, 13); card.Margin = new Thickness(0, 0, 0, 12); return card;
    }
    private static UIElement ProjectStatus(string text, bool warning)
    {
        var row = Ui.Stack(Ui.Text((warning ? "!  " : "✓  ") + text, 12, true, Ui.Brush(warning ? "#A7600A" : "#267245")));
        return new Border { Child = row, Background = Ui.Brush(warning ? "#FFF3DF" : "#EAF5ED"),
            Padding = new Thickness(10, 7, 10, 7), CornerRadius = new CornerRadius(4), Margin = new Thickness(0, 0, 7, 10) };
    }
    private static (string Title, string Subtitle) ProjectModuleLabel(string module)
    {
        var definition = ModuleCatalog.Get(module);
        return (definition.ProjectTitle, definition.ProjectSubtitle);
    }
    private UIElement BuildProjectCatalog()
    {
        var title = Ui.Text("Schede da aggiungere", 21, true); title.Margin = new Thickness(0, 0, 0, 12);
        projectCatalogSearch = new TextBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Padding = new Thickness(0, 7, 4, 7), FontSize = 13, VerticalContentAlignment = VerticalAlignment.Center };
        System.Windows.Automation.AutomationProperties.SetName(projectCatalogSearch, "Cerca una scheda");
        var search = new Grid(); search.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) }); search.ColumnDefinitions.Add(new ColumnDefinition());
        search.Children.Add(new ProjectGlyph("search") { Width = 17, Height = 17, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(projectCatalogSearch, 1); search.Children.Add(projectCatalogSearch);
        var placeholder = Ui.Text("Cerca una scheda…", 13, color: Ui.Muted); placeholder.IsHitTestVisible = false;
        Grid.SetColumn(placeholder, 1); search.Children.Add(placeholder);
        var searchFrame = ProjectPanel(search, 6); searchFrame.Padding = new Thickness(9, 0, 8, 0); searchFrame.Margin = new Thickness(0, 0, 0, 10);
        var header = Ui.Stack(title, searchFrame, Ui.Text("Trascina una scheda nella struttura.", 12, color: Ui.Muted));
        header.Margin = new Thickness(0, 0, 0, 8);
        var catalog = new StackPanel(); var groups = new List<(Expander View, List<(Border Card, string Search)> Rows)>();
        foreach (var group in Archivio.Moduli.GroupBy(id => ModuleCatalog.Get(id).Area.ToUpperInvariant()))
        {
            var cards = new StackPanel(); var rows = new List<(Border Card, string Search)>();
            foreach (string id in group)
            {
                var (name, subtitle) = ProjectModuleLabel(id);
                var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) }); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(15) });
                row.Children.Add(new Viewbox { Child = Ui.ModuleIcon(id), Width = 36, Height = 36, Margin = new Thickness(0, 0, 10, 0) });
                var labels = Ui.Stack(Ui.Text(name, 14, true), Ui.Text(subtitle, 12, color: Ui.Muted)); labels.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(labels, 1); row.Children.Add(labels);
                var grip = new ProjectGlyph("grip") { Width = 10, Height = 20, VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(grip, 2); row.Children.Add(grip);
                var card = ProjectPanel(row, 10); card.MinHeight = 59; card.Margin = new Thickness(0, 0, 0, 6); card.Cursor = Cursors.Hand;
                card.ToolTip = "Trascina nella sezione oppure fai doppio clic per aggiungere.";
                card.MouseEnter += (_, _) => { card.Background = Ui.Brush("#F0F6FC"); card.BorderBrush = Ui.Brush("#A8C6E3"); };
                card.MouseLeave += (_, _) => { card.Background = Brushes.White; card.BorderBrush = Ui.Brush("#DCE5EF"); };
                card.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) { e.Handled = true; Safe(() => AddSheet(id)); } };
                EnableProjectDrag(card, ModuleDragFormat, id); cards.Children.Add(card); projectCatalogCards.Add((id, card));
                rows.Add((card, group.Key + " " + name + " " + subtitle + " " + ModuleName(id)));
            }
            var expander = ProjectGroup(group.Key, cards); expander.Margin = new Thickness(0, 0, 0, 5); catalog.Children.Add(expander); groups.Add((expander, rows));
        }
        var noResults = Ui.Text("Nessuna scheda corrispondente.", 13, color: Ui.Muted); noResults.Margin = new Thickness(0, 16, 0, 0); noResults.Visibility = Visibility.Collapsed; catalog.Children.Add(noResults);
        projectCatalogSearch.TextChanged += (_, _) =>
        {
            string filter = projectCatalogSearch.Text.Trim(); placeholder.Visibility = filter.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            int matches = 0;
            foreach (var group in groups)
            {
                int visible = 0;
                foreach (var row in group.Rows)
                {
                    bool match = CultureInfo.CurrentCulture.CompareInfo.IndexOf(row.Search, filter, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
                    row.Card.Visibility = match ? Visibility.Visible : Visibility.Collapsed; if (match) visible++;
                }
                group.View.Visibility = visible > 0 ? Visibility.Visible : Visibility.Collapsed;
                if (filter.Length > 0 && visible > 0) group.View.IsExpanded = true;
                matches += visible;
            }
            noResults.Visibility = matches == 0 ? Visibility.Visible : Visibility.Collapsed;
        };
        return Ui.Dock(new ScrollViewer { Content = catalog, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }, header);
    }
    private UIElement BuildEmptyProjectTree()
    {
        var icon = new ProjectGlyph("project") { Width = 42, Height = 42, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 16) };
        var title = Ui.Text("Il tuo progetto parte da qui", 22, true); title.TextAlignment = TextAlignment.Center;
        var text = Ui.Text("Crea un progetto, aggiungi le sezioni e trascina i fogli dal catalogo.", 14, color: Ui.Muted); text.TextAlignment = TextAlignment.Center; text.Margin = new Thickness(0, 10, 0, 20);
        var create = ProjectButton("+  Crea progetto", () => Safe(AddProject), true); create.HorizontalAlignment = HorizontalAlignment.Center; create.Padding = new Thickness(22, 10, 22, 10);
        var content = Ui.Stack(icon, title, text); if (!projectReadOnly) content.Children.Add(create);
        content.MaxWidth = 340; content.Margin = new Thickness(24); content.VerticalAlignment = VerticalAlignment.Center;
        return content;
    }
    private void BuildEmptyProjectOverview(StackPanel panel)
    {
        overviewSection = null; overviewName = null;
        panel.Children.Add(Ui.Text("INFORMAZIONI", 11, true, Ui.Muted));
        var title = Ui.Text("Tutto sotto controllo", 22, true); title.Margin = new Thickness(0, 8, 0, 16); panel.Children.Add(title);
        panel.Children.Add(ProjectCard("La sezione selezionata", Ui.Text("Qui trovi i dati comuni, i controlli e le revisioni della sezione su cui stai lavorando.", 13, color: Ui.Muted)));
        panel.Children.Add(ProjectCard("Come iniziare", Ui.Text("1. Crea il progetto\n\n2. Organizza le sezioni\n\n3. Aggiungi le schede", 13, color: Ui.Muted)));
    }
    private void BuildSectionOverview(StackPanel panel, JsonObject section)
    {
        overviewSection = section;
        panel.Children.Add(Ui.Text("SEZIONE SELEZIONATA", 11, true, Ui.Muted));
        overviewName = Ui.Text(section.S("nome"), 26, true); overviewName.Margin = new Thickness(0, 5, 0, 16); panel.Children.Add(overviewName);
        int conflicts = ProjectSharedData.Differences(section).Select(d => d.Key).Distinct().Count() + ProjectSharedData.MissingSoilLayers(section).Count;
        var notices = SectionReportWarnings(section);
        var statuses = Ui.Bar(ProjectStatus($"{conflicts} {(conflicts == 1 ? "conflitto" : "conflitti")}", conflicts > 0), ProjectStatus($"{notices.Length} {(notices.Length == 1 ? "avviso" : "avvisi")}", notices.Length > 0));
        var compare = ProjectButton("Confronto e avvisi", () => Safe(() => ShowCoherence(section))); compare.HorizontalAlignment = HorizontalAlignment.Stretch;
        panel.Children.Add(ProjectCard("Controlli", statuses, compare));
        var fields = ProjectSharedData.ContextSheets(section).SelectMany(s => ProjectSharedData.Fields(s).Values)
            .Where(f => f.Group is "Materiali" or "Geometria" or "Armatura" or "Normativa" or "Coefficienti").GroupBy(f => f.Key)
            .Select(g => (Key: g.Key, Label: ProjectReportPlan.Label(g.Key), Value: string.Join(" / ", g.Select(f => ProjectSharedData.Text(f.Value)).Distinct()))).ToArray();
        string[] priority = ["CLS · fck [MPa]", "classe_acciaio", "esposizione", "diameter_mm", "cover_mm", "width_mm", "height_mm", "fyk_mpa"];
        var ordered = fields.OrderBy(f => Array.IndexOf(priority, f.Key) is int i && i >= 0 ? i : priority.Length).ToArray();
        if (ordered.Length > 0)
        {
            StackPanel Rows(IEnumerable<(string Key, string Label, string Value)> values)
            {
                var list = new StackPanel();
                foreach (var entry in values)
                {
                    var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    var label = Ui.Text(entry.Label, 12, color: Ui.Muted); label.Margin = new Thickness(0, 0, 10, 0); row.Children.Add(label);
                    var value = Ui.Text(entry.Value, 13); value.ToolTip = entry.Value; Grid.SetColumn(value, 1); row.Children.Add(value);
                    list.Children.Add(new Border { Child = row, Padding = new Thickness(0, 8, 0, 8), BorderBrush = Ui.Brush("#EDF1F6"), BorderThickness = new Thickness(0, 0, 0, 1) });
                }
                return list;
            }
            var data = Rows(ordered.Take(5));
            if (ordered.Length > 5) data.Children.Add(ProjectGroup("Tutti i dati comuni", new ScrollViewer { Content = Rows(ordered.Skip(5)), MaxHeight = 240, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, false));
            panel.Children.Add(ProjectCard("Dati comuni", data));
        }
        else panel.Children.Add(ProjectCard("Dati comuni", Ui.Text("Aggiungi le schede per visualizzare materiali, geometria e armature.", 13, color: Ui.Muted)));
        var revisionScope = RevisionScope(section) ?? section;
        var revision = displayedRevision is int revisionNumber
            ? revisionScope.Array("revisioni").OfType<JsonObject>().FirstOrDefault(r => (int)r.D("numero") == revisionNumber)
            : revisionScope["revisione"] as JsonObject;
        var revisions = Ui.Stack(Ui.Text(revision is null ? "Versione corrente" : $"Revisione {(int)revision.D("numero"):00}", 14, true));
        if (revision is not null)
        {
            var date = Ui.Text(RevisionDate(revision), 11, color: Ui.Muted); date.Margin = new Thickness(0, 4, 0, 0); revisions.Children.Add(date);
            if (revision.S("nota").Length > 0) revisions.Children.Add(Ui.Text(revision.S("nota"), 12, color: Ui.Muted));
        }
        var revisionActions = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
        if (revisionScope.Array("revisioni").Count > 0) { var history = ProjectButton("Riepilogo modifiche", () => Safe(() => ShowProjectRevisions(revisionScope))); history.Margin = new Thickness(0, 0, 0, 6); revisionActions.Children.Add(history); }
        revisions.Children.Add(revisionActions); panel.Children.Add(ProjectCard("Revisioni", revisions));
        var actions = new WrapPanel();
        if (!projectReadOnly) { var duplicate = ProjectButton("Duplica sezione", () => Safe(() => DuplicateProjectSection(section))); duplicate.FontSize = 12; duplicate.Margin = new Thickness(0, 0, 7, 7); actions.Children.Add(duplicate); }
        var report = ProjectButton("Genera report", () => Safe(() => ExportSectionReport(section)), true); report.FontSize = 12; report.IsEnabled = ProjectSharedData.SubtreeSheets(section).Any(); report.Margin = new Thickness(0, 0, 0, 7); actions.Children.Add(report); panel.Children.Add(actions);
        var links = new StackPanel();
        foreach (var node in section.Array("fogli").Concat(section.Array("strutture")).OfType<JsonObject>())
        {
            var link = ProjectButton(node.S("nome"), () => Safe(() => { Commit(); if (node.ContainsKey("modulo_id")) ShowSheet(node); else ShowProjectOverview(node); }));
            link.HorizontalContentAlignment = HorizontalAlignment.Left; link.Margin = new Thickness(0, 0, 0, 5); links.Children.Add(link);
        }
        if (links.Children.Count > 0) panel.Children.Add(ProjectGroup("Fogli e sottosezioni", links, false));
        if (notices.Length > 0)
        {
            var warnings = new StackPanel();
            foreach (string notice in notices) { var warning = Ui.Text("• " + notice, 12, color: Ui.Muted); warning.Margin = new Thickness(0, 4, 0, 8); warnings.Children.Add(warning); }
            panel.Children.Add(ProjectGroup("Dettaglio avvisi", warnings, false));
        }
    }
}

internal sealed class ProjectGlyph(string kind) : FrameworkElement
{
    protected override void OnRender(DrawingContext dc)
    {
        dc.PushTransform(new ScaleTransform(ActualWidth / 24, ActualHeight / 24));
        var pen = new Pen(Ui.Navy, 1.5) { LineJoin = PenLineJoin.Round };
        if (kind == "folder")
        {
            dc.DrawGeometry(Ui.Brush("#7590A6"), null, Geometry.Parse("M2,6 L9,6 11,9 22,9 22,21 2,21 Z"));
            dc.DrawRectangle(Ui.Brush("#58758C"), null, new Rect(2,10,20,11));
        }
        else if (kind == "project")
        {
            dc.DrawRoundedRectangle(null, pen, new Rect(5,2,14,20), 1, 1);
            foreach (int x in new[] { 8, 14 }) foreach (int y in new[] { 6, 11, 16 }) dc.DrawRectangle(Ui.Navy, null, new Rect(x,y,2,2));
        }
        else if (kind == "report")
        {
            dc.DrawGeometry(null, pen, Geometry.Parse("M5,2 L14,2 20,8 20,22 5,22 Z M14,2 L14,8 20,8 M8,12 L16,12 M8,16 L16,16"));
        }
        else if (kind == "search")
        {
            dc.DrawEllipse(null, pen, new Point(9,9), 6, 6); dc.DrawLine(pen, new Point(14,14), new Point(21,21));
        }
        else if (kind == "grip")
        {
            foreach (int x in new[] { 7, 17 }) foreach (int y in new[] { 5, 12, 19 }) dc.DrawEllipse(Ui.Brush("#A3B4C7"), null, new Point(x,y), 2, 1.5);
        }
        dc.Pop();
    }
}
