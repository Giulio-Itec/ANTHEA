using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Threading;

namespace X.Desktop;

internal sealed class WikiView : UserControl
{
    private readonly Action<string, string?> openModule;
    private readonly WikiProgress progress;
    private readonly ContentControl content = new();
    private readonly StackPanel navigation = new(), toc = new();
    private readonly ScrollViewer reader = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private readonly Border navHost, tocHost;
    private readonly Grid columns = new();
    private readonly TextBox search = new() { MinHeight = 38, FontSize = 16, ToolTip = "Cerca titoli, concetti, formule e moduli (es. buckling)" };
    private readonly DispatcherTimer debounce = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private readonly DispatcherTimer save = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly List<(string Id, FrameworkElement Heading, Button Link)> sections = [];
    private WikiArticle? article;
    private bool restoring, navExpanded, tocExpanded;
    private string? activeSection;
    internal string? CurrentId => article?.Id;
    internal WikiView(Action<string, string?> openModule, WikiProgress? progress = null, Action? returnToWork = null)
    {
        this.openModule = openModule; this.progress = progress ?? new WikiProgress();
        Background = Ui.Bg;
        AutomationProperties.SetName(search, "Ricerca nella Wiki");
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
        columns.ColumnDefinitions.Add(new ColumnDefinition());
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(205) });
        navHost = Ui.Paper(new ScrollViewer { Content = navigation, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, 12);
        tocHost = Ui.Paper(new ScrollViewer { Content = toc, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, 12);
        navHost.Margin = new Thickness(0, 0, 14, 0); tocHost.Margin = new Thickness(14, 0, 0, 0);
        reader.Content = content; columns.Children.Add(navHost); columns.Children.Add(reader); columns.Children.Add(tocHost);
        reader.Background = Brushes.White;
        Grid.SetColumn(reader, 1); Grid.SetColumn(tocHost, 2);
        var searchBar = Ui.Dock(search, top: Ui.Text("Cerca nel manuale e nelle guide", 12, color: Ui.Muted));
        searchBar.Margin = new Thickness(0, 0, 0, 14);
        var commands = Ui.Bar(Ui.Button("Wiki", Home), Ui.Button("Indice", () => { navExpanded = !navExpanded; Adapt(); }),
            Ui.Button("In questa pagina", () => { tocExpanded = !tocExpanded; Adapt(); }),
            Ui.Button("Apri collegamento", () => { if (Ui.Ask(Window.GetWindow(this), "Apri collegamento Wiki", "/wiki/") is { } uri) Navigate(uri); }));
        if (returnToWork is not null) commands.Children.Insert(0, Ui.Button("← Torna al lavoro", returnToWork));
        Content = Ui.Dock(columns, top: Ui.Stack(commands, searchBar));
        BuildNavigation();
        search.TextChanged += (_, _) => { debounce.Stop(); debounce.Start(); };
        debounce.Tick += (_, _) => { debounce.Stop(); if (string.IsNullOrWhiteSpace(search.Text)) Home(); else Results(search.Text); };
        reader.ScrollChanged += (_, _) => Track();
        save.Tick += (_, _) => { save.Stop(); this.progress.Save(); };
        SizeChanged += (_, _) => Adapt();
        Unloaded += (_, _) => { debounce.Stop(); save.Stop(); this.progress.Save(); };
        Home();
    }
    private void Adapt()
    {
        bool small = ActualWidth < 1000;
        navHost.Visibility = !small || navExpanded ? Visibility.Visible : Visibility.Collapsed;
        tocHost.Visibility = (!small || tocExpanded) && article is not null ? Visibility.Visible : Visibility.Collapsed;
        columns.ColumnDefinitions[0].Width = new GridLength(navHost.Visibility == Visibility.Visible ? (small ? 170 : 210) : 0);
        columns.ColumnDefinitions[2].Width = new GridLength(tocHost.Visibility == Visibility.Visible ? (small ? 160 : 205) : 0);
        // On narrow windows the two menus are exclusive, leaving a readable central column.
        if (ActualWidth < 650 && navHost.Visibility == Visibility.Visible && tocHost.Visibility == Visibility.Visible)
        { tocHost.Visibility = Visibility.Collapsed; columns.ColumnDefinitions[2].Width = new GridLength(0); }
    }
    private void BuildNavigation()
    {
        using var stream = WikiCatalog.Resource("areas.json");
        var areas = JsonSerializer.Deserialize<Dictionary<string, string[]>>(stream)!;
        foreach (var type in new[] { "theory", "guide" })
        {
            navigation.Children.Add(Ui.Text(type == "theory" ? "MANUALE DI INGEGNERIA" : "GUIDE ANTHEA", 12, true));
            foreach (var area in areas[type])
            {
                var pages = Ui.Stack();
                var expander = new Expander { Header = Ui.Text(area, 13, true), Content = pages, Margin = new Thickness(0, 6, 0, 6) };
                expander.Expanded += (_, _) =>
                {
                    if (pages.Children.Count > 0) return;
                    foreach (var a in WikiCatalog.Articles.Where(a => a.Type == type && a.Area == area))
                    { var link = Link(a.Title, () => Navigate(a.Id)); link.ToolTip = a.Summary; pages.Children.Add(link); }
                    if (pages.Children.Count == 0) pages.Children.Add(Ui.Text("Area predisposta: capitoli in preparazione.", 12, color: Ui.Muted));
                };
                navigation.Children.Add(expander);
            }
        }
    }
    internal static Button Link(string title, Action action)
    {
        var b = Ui.Button(title, action); b.Content = Ui.Text(title, 13, color: Ui.Blue);
        b.Style = (Style)Application.Current.FindResource("ProjectButton");
        b.HorizontalContentAlignment = HorizontalAlignment.Left; b.BorderThickness = new Thickness(0); b.Padding = new Thickness(5, 6, 5, 6);
        AutomationProperties.SetName(b, title); return b;
    }
    private void Reset()
    {
        progress.Save(); article = null; restoring = false; activeSection = null; sections.Clear(); toc.Children.Clear(); reader.ScrollToTop();
    }
    internal void Home()
    {
        Reset(); var pane = new StackPanel();
        pane.Children.Add(PageTitle("Wiki", 34));
        pane.Children.Add(Ui.Text("Tutta la conoscenza tecnica e operativa di Anthea, organizzata per concetti.", 19));
        pane.Children.Add(Ui.Text("Dal comportamento strutturale al modello, dalla verifica all'esempio nel software. Cerca un concetto oppure scegli un percorso di lettura.", 15, color: Ui.Muted));
        var recent = progress.Entries.OrderByDescending(p => p.Value.Visited).Take(4).ToArray();
        if (recent.Length > 0)
        {
            pane.Children.Add(Title("Continua a leggere"));
            foreach (var (id, state) in recent)
                if (WikiCatalog.Resolve(id) is { } a)
                {
                    var read = a.Sections.Count(s => state.Read.Contains(s));
                    pane.Children.Add(Link($"{a.Title} · posizione {state.Fraction:P0} · lette {read}/{a.Sections.Length} sezioni", () => Navigate(id)));
                    pane.Children.Add(new ProgressBar { Minimum = 0, Maximum = Math.Max(1, a.Sections.Length), Value = read, Height = 5, Foreground = Ui.Blue, Margin = new Thickness(5, 0, 5, 10), ToolTip = "Avanzamento delle sezioni segnate come lette" });
                }
        }
        foreach (var (type, label) in new[] { ("theory", "Manuale di ingegneria"), ("guide", "Guide Anthea") })
        {
            pane.Children.Add(Title(label));
            var cards = new WrapPanel();
            using var stream = WikiCatalog.Resource("areas.json"); var areas = JsonSerializer.Deserialize<Dictionary<string, string[]>>(stream)!;
            foreach (var area in areas[type])
            {
                var matches = WikiCatalog.Articles.Where(a => a.Type == type && a.Area == area).ToArray();
                var stack = Ui.Stack(Ui.Text(area, 17, true), Ui.Text(matches.Length == 0 ? "Percorso in preparazione" : $"{matches.Length} capitoli · teoria, esempi e riferimenti", 12, color: Ui.Muted));
                if (matches.Length > 0) stack.Children.Add(Link("Esplora →", () => Browse(type, area)));
                var card = Ui.Paper(stack, 16); card.Width = 270; card.MinHeight = 135; card.Margin = new Thickness(0, 0, 12, 12); cards.Children.Add(card);
            }
            pane.Children.Add(cards);
        }
        pane.Children.Add(Title("Percorso consigliato · capisci e applica"));
        pane.Children.Add(Link("Elementi Beam → esempio di trave → verifica della Sezione in c.a.", () => Navigate("/wiki/manuale/fem/elementi-beam")));
        pane.Children.Add(Link("Glossario tecnico", Glossary));
        SetContent(pane); Adapt();
    }
    private static TextBlock Title(string text) { var t = Ui.Text(text, 22, true); t.Margin = new Thickness(0, 26, 0, 12); AutomationProperties.SetHeadingLevel(t, AutomationHeadingLevel.Level2); return t; }
    private static TextBlock PageTitle(string text, double size = 30) { var t = Ui.Text(text, size, true); AutomationProperties.SetHeadingLevel(t, AutomationHeadingLevel.Level1); return t; }
    private void SetContent(StackPanel pane)
    {
        pane.MaxWidth = article is null ? 960 : 760;
        pane.HorizontalAlignment = HorizontalAlignment.Center;
        pane.Margin = new Thickness(28, 24, 28, 40); content.Content = pane;
    }
    private void Browse(string type, string area)
    {
        Reset(); var pane = Ui.Stack(PageTitle(area == "" ? (type == "theory" ? "Manuale di ingegneria" : "Guide Anthea") : area));
        foreach (var a in WikiCatalog.Articles.Where(a => a.Type == type && (area == "" || a.Area == area))) pane.Children.Add(Card(a));
        SetContent(pane); Adapt();
    }
    private Border Card(WikiArticle a)
    {
        var p = Ui.Paper(Ui.Stack(Ui.Text(a.Type == "theory" ? "MANUALE · " + a.Area : "GUIDA ANTHEA · " + a.Area, 11, true, Ui.Muted),
            Link(a.Title, () => Navigate(a.Id)), Ui.Text(a.Summary, 14), Ui.Text($"Circa {a.ReadingTime} min", 12, color: Ui.Muted)), 18);
        p.Margin = new Thickness(0, 0, 0, 12); return p;
    }
    private void Results(string query)
    {
        Reset(); var results = WikiCatalog.Search(query); var pane = Ui.Stack(PageTitle($"Ricerca · {query}"), Ui.Text($"{results.Length} risultati (massimo 60) · manuale e guide", color: Ui.Muted));
        if (results.Length == 0) pane.Children.Add(Ui.Text("Nessun risultato. Prova un termine più breve o un sinonimo italiano/inglese."));
        foreach (var a in results) pane.Children.Add(Card(a)); SetContent(pane); Adapt();
    }
    internal void Navigate(string uri)
    {
        uri = uri.Trim();
        if (uri.TrimEnd('/') == "/wiki") { Home(); return; }
        var parts = uri.Trim('/').Split('/');
        if (parts.Length is 2 or 3 && parts[0] == "wiki" && parts[1] is "manuale" or "guide")
        {
            string type = parts[1] == "manuale" ? "theory" : "guide";
            if (parts.Length == 2) { Browse(type, ""); return; }
            var area = WikiCatalog.Articles.FirstOrDefault(a => a.Type == type && (WikiCatalog.Slug(a.Area) == parts[2] || parts[2] == "fem" && a.Area == "FEM e modellazione"))?.Area;
            if (area is not null) { Browse(type, area); return; }
        }
        var target = WikiCatalog.Resolve(uri);
        if (target is null) { MessageBox.Show("Pagina Wiki non disponibile: " + uri, "ANTHEA"); return; }
        Reset(); article = target; var pane = new StackPanel();
        progress.Update(target.Id, progress.Entries.GetValueOrDefault(target.Id)?.Section ?? "", progress.Entries.GetValueOrDefault(target.Id)?.Fraction ?? 0);
        pane.Children.Add(Link($"Wiki / {(target.Type == "theory" ? "Manuale" : "Guide Anthea")} / {target.Area}", () => Browse(target.Type, target.Area)));
        pane.Children.Add(PageTitle(target.Title)); pane.Children.Add(Ui.Text(target.Summary, 16, color: Ui.Muted));
        var metadata = Ui.Text($"{(target.Type == "theory" ? "Manuale di ingegneria" : "Guida Anthea")} · {target.ReadingTime} min di lettura", 12, color: Ui.Muted);
        metadata.Margin = new Thickness(0, 12, 0, 8); pane.Children.Add(metadata);
        toc.Children.Add(Ui.Text("IN QUESTA PAGINA", 11, true, Ui.Muted));
        WikiEditorial.Render(pane, WikiCatalog.Body(target), Navigate, (id, label, level) =>
        {
            var head = Title(label); head.FontSize = level == 3 ? 22 : 18;
            AutomationProperties.SetHeadingLevel(head, level == 3 ? AutomationHeadingLevel.Level2 : AutomationHeadingLevel.Level3);
            var link = Link(label, () => ScrollTo(id)); toc.Children.Add(link); sections.Add((id, head, link));
            var menu = new ContextMenu();
            var copy = new MenuItem { Header = "Copia collegamento alla sezione" };
            copy.Click += (_, _) => Clipboard.SetText(target.Id + "#" + id);
            var read = new MenuItem { Header = "Segna sezione letta" };
            read.Click += (_, _) => { progress.Update(target.Id, id, reader.ScrollableHeight <= 0 ? 1 : reader.VerticalOffset / reader.ScrollableHeight, true); progress.Save(); link.ToolTip = "Sezione letta"; };
            menu.Items.Add(copy); menu.Items.Add(read); head.ContextMenu = menu;
            var actions = Link("⋯", () => { menu.PlacementTarget = head; menu.IsOpen = true; });
            actions.ToolTip = "Azioni della sezione"; actions.VerticalAlignment = VerticalAlignment.Bottom;
            actions.Margin = new Thickness(8, 0, 0, 8);
            var row = new DockPanel(); DockPanel.SetDock(actions, Dock.Right);
            row.Children.Add(actions); row.Children.Add(head);
            return row;
        });
        if (target.Modules.Length > 0) pane.Children.Add(WikiEditorial.TryInAnthea(target, openModule));
        if (target.Related.Length > 0) pane.Children.Add(Title("Argomenti correlati"));
        foreach (var related in target.Related)
            if (WikiCatalog.Resolve(related) is { } a) pane.Children.Add(Link((a.Type == "theory" ? "Teoria · " : "Guida · ") + a.Title, () => Navigate(a.Id)));
        var siblings = WikiCatalog.Articles.Where(a => a.Type == target.Type && a.Area == target.Area).OrderBy(a => a.Order).ToArray();
        var index = Array.IndexOf(siblings, target);
        if (index > 0) pane.Children.Add(Link("← " + siblings[index - 1].Title, () => Navigate(siblings[index - 1].Id)));
        if (index < siblings.Length - 1) pane.Children.Add(Link(siblings[index + 1].Title + " →", () => Navigate(siblings[index + 1].Id)));
        SetContent(pane); Adapt();
        navExpanded = false; tocExpanded = false; Adapt();
        var requested = uri.Contains('#') ? uri.Split('#', 2)[1] : progress.Entries.GetValueOrDefault(target.Id)?.Section;
        restoring = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (article != target) return;
            reader.UpdateLayout(); if (!string.IsNullOrEmpty(requested)) ScrollTo(requested);
            restoring = false; Track();
        }));
    }
    private void ScrollTo(string id)
    {
        if (sections.FirstOrDefault(s => s.Id == id).Heading is { } h)
        { reader.UpdateLayout(); reader.ScrollToVerticalOffset(h.TranslatePoint(new Point(), content).Y); }
    }
    private void Track()
    {
        if (article is null || restoring || sections.Count == 0 || !reader.IsLoaded) return;
        var current = sections.LastOrDefault(s => s.Heading.TranslatePoint(new Point(), content).Y <= reader.VerticalOffset + 45);
        if (current.Heading is null) current = sections[0];
        activeSection = current.Id;
        foreach (var s in sections) { s.Link.Background = s.Id == activeSection ? Ui.Bg : Brushes.White; s.Link.FontWeight = s.Id == activeSection ? FontWeights.Bold : FontWeights.Normal; }
        progress.Update(article.Id, activeSection, reader.ScrollableHeight <= 0 ? 1 : reader.VerticalOffset / reader.ScrollableHeight);
        save.Stop(); save.Start();
    }
    private void Glossary()
    {
        Reset(); var pane = Ui.Stack(PageTitle("Glossario tecnico"));
        using var stream = WikiCatalog.Resource("glossary.json");
        var terms = JsonSerializer.Deserialize<Dictionary<string, string[]>>(stream)!;
        foreach (var (term, values) in terms.OrderBy(p => p.Key)) pane.Children.Add(Ui.Paper(Ui.Stack(Title(term), Ui.Text(values[0], 15), Link("Approfondisci →", () => Navigate(values[1]))), 18));
        SetContent(pane); Adapt();
    }
}
