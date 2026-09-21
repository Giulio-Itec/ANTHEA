using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow : Window
{
    private JsonObject document = Archivio.Documento("geo_palo_verticale");
    private JsonObject? currentSheet;
    private string? path;
    private bool dirty, refreshing, testing;
    private SheetEditor? editor;
    private readonly ContentControl body = new();
    private readonly Grid dashboard = new();
    private readonly ScrollViewer dashboardViewport;
    private readonly DockPanel moduleView = new();
    private readonly ContentControl dashboardBody = new() { Margin = new Thickness(42, 24, 42, 36) };
    private readonly ContentControl sheetContent = new();
    private readonly TextBlock heading = Ui.Text("", 17, true, Brushes.White);
    private readonly TreeView tree = new() { BorderThickness = new Thickness(0), Background = Brushes.White };
    private readonly Dictionary<string, Button> navigation = new();

    public MainWindow()
    {
        Style = (Style)Application.Current.FindResource(typeof(Window));
        Title = "ANTHEA"; Width = 1600; Height = 990; MinWidth = 760; MinHeight = 480; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        dashboardViewport = DisplayAdaptation.Viewport(dashboard, 1120, 680);
        var root = Ui.Dock(body, BuildMenu()); root.Background = Ui.Bg; Content = root; BuildShell(); ShowHome();
        DisplayAdaptation.Attach(this);
        Closing += (_, e) => { if (testing) return; if (editor?.Busy == true) { e.Cancel = true; MessageBox.Show(this, "Attendere il completamento del calcolo."); return; } e.Cancel = !ConfirmDiscard(); };
        Closed += (_, _) => editor?.Dispose();
        tree.SelectedItemChanged += (_, e) =>
        {
            if (refreshing || editor?.Busy == true) return;
            if (e.NewValue is TreeViewItem { Tag: JsonObject sheet } && sheet.ContainsKey("modulo_id")) { Commit(); ShowSheet(sheet); }
        };
        tree.PreviewMouseDown += (_, e) => { if (editor?.Busy == true) e.Handled = true; };
        tree.PreviewKeyDown += (_, e) => { if (editor?.Busy == true) e.Handled = true; };
    }
    internal void Safe(Action action) { try { action(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Operazione non completata", MessageBoxButton.OK, MessageBoxImage.Error); } }
    internal static string ModuleName(string module) => module switch { "geo_palo_verticale" => "Palo · capacità portante", "geo_palo_orizzontale" => "Palo · capacità portante orizzontale", "geo_micropalo_verticale" => "Micropalo · Bustamante–Doix", "str_palo" => "Sezione in c.a. · SLU / SLV / SLE", _ => module };
    private Menu BuildMenu()
    {
        var menu = new Menu { Background = Brushes.White }; var file = new MenuItem { Header = "_File" }; menu.Items.Add(file);
        void Add(string title, Action action, Key? key = null, ModifierKeys modifiers = ModifierKeys.Control)
        {
            var item = new MenuItem { Header = title }; item.Click += (_, _) => Safe(action); file.Items.Add(item);
            if (key is Key k) { var command = new RoutedCommand(); CommandBindings.Add(new CommandBinding(command, (_, _) => Safe(action))); InputBindings.Add(new KeyBinding(command, k, modifiers)); item.InputGestureText = new KeyGesture(k, modifiers).GetDisplayStringForCulture(System.Globalization.CultureInfo.CurrentCulture); }
        }
        Add("Nuovo palo", () => NewCalculation("geo_palo_verticale"), Key.N); Add("Nuovo micropalo", () => NewCalculation("geo_micropalo_verticale")); Add("Nuova sezione in c.a.", () => NewCalculation("str_palo")); Add("Nuovo archivio progetti", NewProjects);
        Add("Nuovo palo orizzontale", () => NewCalculation(PaloOrizzontale.Module));
        file.Items.Add(new Separator()); Add("Apri…", Open, Key.O); Add("Salva", () => Save(false), Key.S); Add("Salva con nome…", () => Save(true), Key.S, ModifierKeys.Control | ModifierKeys.Shift);
        Add("Esporta foglio selezionato…", ExportSheet); file.Items.Add(new Separator()); Add("Report Word…", ExportReport); Add("Risultati JSON…", ExportJson); Add("Esci", Close);
        return menu;
    }
    private void BuildShell()
    {
        dashboard.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) }); dashboard.ColumnDefinitions.Add(new ColumnDefinition());
        var nav = new StackPanel { Margin = new Thickness(20, 22, 20, 22) }; var logo = Ui.Logo(150); logo.Margin = new Thickness(0, 0, 0, 16); nav.Children.Add(logo);
        nav.Children.Add(Ui.Text("Strumenti di calcolo", color: Ui.Muted));
        foreach (var (title, action) in new (string, Action)[] { ("Home", ShowHome), ("Moduli singoli", () => ShowModules()), ("Progetti", ShowProjects) })
        {
            var b = Ui.Button(title, () => Safe(() => { if (editor?.Busy == true) return; Commit(); action(); })); b.Height = 45; b.HorizontalContentAlignment = HorizontalAlignment.Left; b.FontWeight = FontWeights.SemiBold; nav.Children.Add(b); navigation[title] = b;
        }
        nav.Children.Add(FileCommands(false)); var footer = Ui.Text("Moduli disponibili: 3 di 6", color: Ui.Muted); footer.Margin = new Thickness(20);
        var sidebar = Ui.Dock(nav, bottom: footer); sidebar.Background = Brushes.White; dashboard.Children.Add(sidebar); Grid.SetColumn(dashboardBody, 1); dashboard.Children.Add(dashboardBody);
        var top = new DockPanel { Background = Ui.Navy, MinHeight = 68, LastChildFill = true };
        var back = Ui.Button("← Torna ad ANTHEA", () => { if (editor?.Busy == true) return; Commit(); ShowHome(); }, true); back.Width = 200; back.BorderThickness = new Thickness(0); top.Children.Add(back); top.Children.Add(FileCommands(true));
        var titles = Ui.Stack(heading, Ui.Text("Scheda di calcolo · input, profilo e risultati", 12, color: Ui.Brush("#B9C8D8"))); titles.Margin = new Thickness(15, 10, 0, 0); top.Children.Add(titles);
        DockPanel.SetDock(top, System.Windows.Controls.Dock.Top); moduleView.Children.Add(top); moduleView.Children.Add(sheetContent);
    }
    private WrapPanel FileCommands(bool dark)
    {
        var bar = new WrapPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 6, 0, 6) };
        foreach (var (label, icon, action) in new (string, string, Action)[] { ("Apri", "📂", Open), ("Salva", "▣", () => Save(false)), ("Salva con nome", "▤", () => Save(true)), ("Report Word", "W", ExportReport) })
        {
            if (!dark && label == "Report Word") continue;
            bool save = label is "Salva" or "Salva con nome";
            var b = Ui.Button(save ? label : icon, () => Safe(action), dark); b.ToolTip = label; b.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, label);
            if (!save) b.Width = 37; b.Height = 36; b.Padding = new Thickness(save ? 10 : 2, 2, save ? 10 : 2, 2); bar.Children.Add(b);
        }
        return bar;
    }
    private void SelectNavigation(string name)
    {
        if (tree.Parent is Panel p) p.Children.Remove(tree);
        dashboardBody.Content = null; body.Content = dashboardViewport;
        foreach (var (key, b) in navigation) { b.Background = key == name ? Ui.Navy : Brushes.White; b.Foreground = key == name ? Brushes.White : Ui.Navy; }
    }
    private void ShowHome()
    {
        SelectNavigation("Home"); var layout = new Grid(); layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(250) }); layout.RowDefinitions.Add(new RowDefinition());
        var hero = new DockPanel { Background = Ui.Navy, Margin = new Thickness(0, 0, 0, 10) }; var logo = Ui.Logo(220); logo.Margin = new Thickness(24, 8, 20, 8); hero.Children.Add(logo);
        var copy = Ui.Stack(Ui.Text("Strumenti di calcolo per l'ingegneria", 30, true, Brushes.White), Ui.Text("Apri un modulo indipendente oppure organizza più verifiche all'interno di un progetto.", 15, color: Ui.Brush("#B9C8D8"))); copy.VerticalAlignment = VerticalAlignment.Center; copy.Margin = new Thickness(20); hero.Children.Add(copy); layout.Children.Add(hero);
        if (editor is not null && currentSheet is not null)
            copy.Children.Add(Ui.Button("Riprendi · " + currentSheet.S("nome", ModuleName(editor.Module)), () => ResumeCalculation(), true));
        var cards = new Grid { Margin = new Thickness(0, 10, 0, 0) }; cards.ColumnDefinitions.Add(new ColumnDefinition()); cards.ColumnDefinitions.Add(new ColumnDefinition()); Grid.SetRow(cards, 1); layout.Children.Add(cards);
        void Card(int col, string title, string description, string badge, string button, Action action)
        {
            var t = Ui.Text(title, 30, true); t.Margin = new Thickness(0, 0, 0, 24); var d = Ui.Text(description, 16, color: Ui.Muted); d.Margin = new Thickness(0, 0, 0, 25);
            var b = Ui.Button(button, () => Safe(action), true); b.HorizontalAlignment = HorizontalAlignment.Left; b.MinWidth = 185;
            var pane = Ui.Paper(Ui.Dock(Ui.Stack(t, d, Ui.Text(badge, 13, true)), bottom: b), 34); pane.Margin = new Thickness(col == 0 ? 0 : 12, 0, col == 0 ? 12 : 0, 0); Grid.SetColumn(pane, col); cards.Children.Add(pane);
        }
        Card(0, "Moduli singoli", "Usa un modulo direttamente, senza creare un progetto.", "6 moduli predisposti", "Apri i moduli", () => ShowModules());
        Card(1, "Progetti", "Raggruppa i fogli per opera, spalla, pila o altra struttura.", "Struttura gerarchica libera", "Gestisci i progetti", ShowProjects); dashboardBody.Content = layout;
    }
    private void ShowModules(string discipline = "Tutti")
    {
        SelectNavigation("Moduli singoli"); var root = new DockPanel(); var intro = Ui.Stack(Ui.Text("Moduli singoli", 30, true), Ui.Text("Scegli disciplina, elemento e verifica. I dati restano indipendenti da un progetto.", color: Ui.Muted)); intro.Margin = new Thickness(0, 0, 0, 24); DockPanel.SetDock(intro, System.Windows.Controls.Dock.Top); root.Children.Add(intro);
        var filters = Ui.Stack(Ui.Text("DISCIPLINE", 13, true)); filters.Width = 185; foreach (string name in new[] { "Tutti", "Geotecnica", "Strutture" }) filters.Children.Add(Ui.Button(name, () => ShowModules(name), discipline == name));
        var fp = Ui.Paper(filters, 16); fp.Margin = new Thickness(0, 0, 16, 0); DockPanel.SetDock(fp, System.Windows.Controls.Dock.Left); root.Children.Add(fp);
        var list = new StackPanel();
        var modules = new[] { ("Geotecnica", "Palo", "Capacità portante verticale", "geo_palo_verticale"), ("Geotecnica", "Palo", "Capacità portante orizzontale", "geo_palo_orizzontale"), ("Geotecnica", "Micropalo", "Capacità portante verticale", "geo_micropalo_verticale"), ("Geotecnica", "Micropalo", "Capacità portante orizzontale", ""), ("Strutture", "Sezione in c.a.", "Verifiche SLU · SLV · SLE", "str_palo"), ("Strutture", "Micropalo", "Verifiche strutturali", "") };
        foreach (string area in new[] { "Geotecnica", "Strutture" }.Where(a => discipline == "Tutti" || a == discipline))
        {
            var label = Ui.Text(area, 21, true); label.Margin = new Thickness(0, 10, 0, 14); list.Children.Add(label);
            var groups = new Grid(); groups.ColumnDefinitions.Add(new ColumnDefinition()); groups.ColumnDefinitions.Add(new ColumnDefinition()); int col = 0;
            foreach (var group in modules.Where(m => m.Item1 == area).GroupBy(m => m.Item2))
            {
                var pane = new StackPanel(); var head = Ui.Text(group.Key.ToUpperInvariant(), 13, true, Brushes.White); head.Padding = new Thickness(8); pane.Children.Add(new Border { Child = head, Background = Ui.Navy });
                foreach (var (_, title, description, id) in group)
                {
                    string icon = id != "" ? id : area == "Strutture" ? "str_micropalo" : title == "Palo" ? "geo_palo_orizzontale" : "geo_micropalo_orizzontale";
                    var copy = Ui.Stack(Ui.Text(id == "" ? "In preparazione" : "Disponibile", 11, true, id == "" ? Ui.Muted : Brushes.ForestGreen), Ui.Text(description, 17, true), Ui.Text(area + " · " + title, 12, color: Ui.Muted)); copy.Margin = new Thickness(12);
                    var row = new DockPanel(); row.Children.Add(Ui.ModuleIcon(icon)); row.Children.Add(copy);
                    var open = Ui.Button(id == "" ? "Dettagli" : editor?.Module == id ? "Riprendi" : "Apri", () => { if (id != "") Safe(() => OpenModule(id)); else MessageBox.Show(this, "Modulo in preparazione, come nella versione originale."); }, id != ""); open.HorizontalAlignment = HorizontalAlignment.Right;
                    var card = Ui.Paper(Ui.Dock(row, bottom: open)); card.Height = 200; card.Margin = new Thickness(0, 0, 0, 8); pane.Children.Add(card);
                }
                pane.Margin = new Thickness(0, 0, 14, 0); Grid.SetColumn(pane, col++); groups.Children.Add(pane);
            }
            list.Children.Add(groups);
        }
        root.Children.Add(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }); dashboardBody.Content = root;
    }
    private void ShowProjects()
    {
        Commit(); SelectNavigation("Progetti"); var title = new DockPanel(); var add = Ui.Button("[+] Nuovo progetto", () => Safe(AddProject), true); DockPanel.SetDock(add, System.Windows.Controls.Dock.Right); title.Children.Add(add); title.Children.Add(Ui.Text("Progetti", 30, true));
        var columns = new Grid(); columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) }); columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        var hierarchy = Ui.Paper(Ui.Dock(tree, Ui.Text("Struttura del progetto", 16, true), Ui.Bar(Ui.Button("+ Struttura", () => Safe(AddStructure)), Ui.Button("Rinomina", () => Safe(Rename)), Ui.Button("Elimina", () => Safe(Delete)))), 18); hierarchy.Margin = new Thickness(0, 16, 12, 0); columns.Children.Add(hierarchy);
        var guide = Ui.Stack(Ui.Text("Schede di calcolo", 16, true), Ui.Text("Seleziona una struttura e fai doppio clic su una scheda.\n\nPer rinominare o eliminare: clic destro sul nodo.", color: Ui.Muted));
        foreach (string id in Archivio.Moduli)
        {
            var row = new DockPanel(); row.Children.Add(Ui.ModuleIcon(id)); row.Children.Add(Ui.Text(ModuleName(id), 15, true));
            var card = Ui.Paper(row); card.Margin = new Thickness(0, 14, 0, 0); card.Cursor = Cursors.Hand; card.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) Safe(() => AddSheet(id)); }; guide.Children.Add(card);
        }
        var guidePane = Ui.Paper(guide, 22); guidePane.Margin = new Thickness(12, 16, 0, 0); Grid.SetColumn(guidePane, 1); columns.Children.Add(guidePane);
        var intro = Ui.Stack(title, Ui.Text("Organizza i fogli di calcolo per struttura e conserva una gerarchia ordinata.", color: Ui.Muted)); dashboardBody.Content = Ui.Dock(columns, intro); RefreshTree();
    }
    private void MarkDirty() { dirty = true; UpdateTitle(); }
    private void UpdateTitle() => Title = "ANTHEA — " + (path is null ? "Nuovo documento" : Path.GetFileName(path)) + (dirty ? " *" : "");
    private void Commit() { if (editor is null || currentSheet is null) return; editor.Commit(); currentSheet["dati"] = editor.Data.DeepClone(); }
    private void ShowSheet(JsonObject sheet)
    {
        if (editor is not null && ReferenceEquals(sheet, currentSheet)) { ResumeCalculation(); return; }
        if (editor?.Busy == true) return; editor?.Dispose(); currentSheet = sheet; string module = sheet.S("modulo_id");
        editor = new SheetEditor(module, sheet["dati"] as JsonObject ?? Archivio.NuovoFoglio(module)); editor.Modified += MarkDirty;
        sheetContent.Content = module is "geo_palo_verticale" or "geo_micropalo_verticale"
            ? editor : DisplayAdaptation.Viewport(editor, 1120, 600);
        heading.Text = sheet.S("nome", ModuleName(module)); body.Content = moduleView;
    }
    private void RefreshTree(JsonObject? selected = null)
    {
        refreshing = true; tree.Items.Clear();
        TreeViewItem Node(JsonObject value, string fallback)
        {
            var item = new TreeViewItem { Header = value.S("nome", fallback), Tag = value, IsExpanded = true, Padding = new Thickness(3, 5, 3, 5) };
            item.PreviewMouseRightButtonDown += (_, e) => { item.IsSelected = true; e.Handled = true; };
            var menu = new ContextMenu(); void Action(string name, Action action) { var m = new MenuItem { Header = name }; m.Click += (_, _) => Safe(action); menu.Items.Add(m); }
            foreach (string module in Archivio.Moduli) Action("Aggiungi " + ModuleName(module), () => AddSheet(module)); Action("Rinomina", Rename); Action("Elimina", Delete); item.ContextMenu = menu;
            if (ReferenceEquals(value, selected ?? currentSheet)) item.IsSelected = true; return item;
        }
        if (document.S("tipo") == "calcolo") tree.Items.Add(Node(document, ModuleName(document.S("modulo_id"))));
        else foreach (var p in document.Array("progetti").OfType<JsonObject>())
        {
            var pn = Node(p, "Progetto"); tree.Items.Add(pn);
            foreach (var s in p.Array("strutture").OfType<JsonObject>()) { var sn = Node(s, "Struttura"); pn.Items.Add(sn); foreach (var f in s.Array("fogli").OfType<JsonObject>()) sn.Items.Add(Node(f, ModuleName(f.S("modulo_id")))); }
        }
        refreshing = false;
    }
    private bool ConfirmDiscard()
    {
        Commit(); if (!dirty) return true;
        var answer = MessageBox.Show(this, "Salvare le modifiche al documento corrente?", "ANTHEA", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        return answer != MessageBoxResult.Cancel && (answer != MessageBoxResult.Yes || Save(false));
    }
    private void NewCalculation(string module)
    { if (editor?.Busy == true || !ConfirmDiscard()) return; document = Archivio.Documento(module); path = null; dirty = false; currentSheet = null; ShowSheet(document); RefreshTree(); UpdateTitle(); }
    private void OpenModule(string module)
    {
        if (editor?.Module == module && currentSheet is not null) ResumeCalculation();
        else NewCalculation(module);
    }
    private void ResumeCalculation()
    {
        if (editor is null || currentSheet is null) return;
        heading.Text = currentSheet.S("nome", ModuleName(editor.Module));
        body.Content = moduleView;
    }
    private void NewProjects()
    {
        if (editor?.Busy == true || !ConfirmDiscard()) return;
        editor?.Dispose(); editor = null; currentSheet = null; sheetContent.Content = null;
        document = J.Obj(("formato", "X"), ("versione", 1), ("tipo", "progetti"), ("progetti", new JsonArray())); path = null; dirty = false; ShowProjects(); UpdateTitle(); AddProject();
    }
    private void AddProject()
    {
        if (editor?.Busy == true) return; if (document.S("tipo") != "progetti") { NewProjects(); return; }
        string? name = Ui.Ask(this, "Nome progetto", "Progetto " + (document.Array("progetti").Count + 1)); if (string.IsNullOrWhiteSpace(name)) return;
        Commit(); var p = J.Obj(("id", Guid.NewGuid().ToString("N")), ("nome", name), ("strutture", new JsonArray())); document.Array("progetti").Add(p); MarkDirty(); RefreshTree(p);
    }
    private void AddStructure()
    {
        if (editor?.Busy == true) return;
        if (document.S("tipo") != "progetti") { MessageBox.Show(this, "Creare o aprire un archivio progetti dal menu File."); return; }
        var node = tree.SelectedItem as TreeViewItem; while (node?.Parent is TreeViewItem parent) node = parent;
        if (node?.Tag is not JsonObject p) { AddProject(); return; }
        string? name = Ui.Ask(this, "Nome struttura", "Struttura " + (p.Array("strutture").Count + 1)); if (string.IsNullOrWhiteSpace(name)) return;
        Commit(); var s = J.Obj(("id", Guid.NewGuid().ToString("N")), ("nome", name), ("fogli", new JsonArray())); p.Array("strutture").Add(s); MarkDirty(); RefreshTree(s);
    }
    private void AddSheet(string module)
    {
        if (editor?.Busy == true) return; if (document.S("tipo") != "progetti") { NewCalculation(module); return; }
        var node = tree.SelectedItem as TreeViewItem; if (node?.Tag is JsonObject f && f.ContainsKey("modulo_id")) node = node.Parent as TreeViewItem;
        if (node?.Tag is not JsonObject s || !s.ContainsKey("fogli")) { MessageBox.Show(this, "Selezionare una struttura nell'albero dei progetti."); return; }
        string? name = Ui.Ask(this, "Nome foglio", ModuleName(module)); if (string.IsNullOrWhiteSpace(name)) return;
        Commit(); var sheet = J.Obj(("id", Guid.NewGuid().ToString("N")), ("nome", name), ("modulo_id", module), ("dati", Archivio.NuovoFoglio(module))); s.Array("fogli").Add(sheet); MarkDirty(); RefreshTree(sheet); ShowSheet(sheet);
    }
    private void Rename()
    {
        if (editor?.Busy == true || tree.SelectedItem is not TreeViewItem { Tag: JsonObject target } node) return;
        string? name = Ui.Ask(this, "Rinomina", target.S("nome", node.Header.ToString() ?? "")); if (string.IsNullOrWhiteSpace(name)) return;
        target["nome"] = name; MarkDirty(); RefreshTree(target); if (ReferenceEquals(target, currentSheet)) heading.Text = name;
    }
    private void Delete()
    {
        if (document.S("tipo") != "progetti" || tree.SelectedItem is not TreeViewItem { Tag: JsonObject target } || editor?.Busy == true) return;
        if (MessageBox.Show(this, "Eliminare l'elemento selezionato e i suoi contenuti dal documento? Il file su disco resta invariato fino al salvataggio.", "Elimina", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        Commit(); if (target.Parent is JsonArray list) list.Remove(target); editor?.Dispose(); editor = null; currentSheet = null; sheetContent.Content = null; MarkDirty(); RefreshTree();
    }
    private void Open() { var d = new OpenFileDialog { Filter = "File ANTHEA|*.programma;*.anthea|Tutti i file|*.*" }; if (d.ShowDialog(this) == true) LoadFile(d.FileName); }
    internal void LoadFile(string filename)
    {
        if (editor?.Busy == true) return; var loaded = Archivio.Leggi(filename); if (!ConfirmDiscard()) return;
        editor?.Dispose(); editor = null; currentSheet = null; document = loaded; path = filename; dirty = false; sheetContent.Content = null;
        if (document.S("tipo") == "calcolo") ShowSheet(document); else ShowProjects(); RefreshTree(); UpdateTitle();
    }
    private bool Save(bool asNew)
    {
        Commit(); string? target = path;
        if (asNew || target is null) { var d = new SaveFileDialog { Filter = "File ANTHEA|*.programma", FileName = path is null ? "Calcolo.programma" : Path.GetFileName(path) }; if (d.ShowDialog(this) != true) return false; target = d.FileName; }
        try { Archivio.Scrivi(target, document); path = target; dirty = false; UpdateTitle(); return true; }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Salvataggio non riuscito", MessageBoxButton.OK, MessageBoxImage.Error); return false; }
    }
    private void ExportSheet()
    {
        Commit(); if (currentSheet is null) return; var d = new SaveFileDialog { Filter = "File ANTHEA|*.programma", FileName = "Foglio.programma" };
        if (d.ShowDialog(this) == true) Archivio.Scrivi(d.FileName, J.Obj(("formato", "X"), ("versione", 1), ("tipo", "calcolo"), ("modulo_id", currentSheet["modulo_id"]), ("dati", currentSheet["dati"])));
    }
    private void ExportJson()
    {
        Commit(); if (editor?.Result is null) { MessageBox.Show(this, editor?.Module == "str_palo" ? "Attendere l’aggiornamento automatico e correggere gli eventuali dati non validi." : "Premere Calcola prima di esportare i risultati."); return; }
        var d = new SaveFileDialog { Filter = "Risultati JSON|*.json", FileName = "Risultati.json" }; if (d.ShowDialog(this) == true) editor.ExportResult(d.FileName);
    }
    private void ExportReport()
    {
        Commit(); if (editor?.Result is null || editor.Busy) { MessageBox.Show(this, editor?.Module == "str_palo" ? "Attendere l’aggiornamento automatico e correggere gli eventuali dati non validi." : "Completare il calcolo prima di esportare il report."); return; }
        if (editor.Module == PaloOrizzontale.Module)
        {
            var save = new SaveFileDialog { Filter = "Documento Word|*.docx", FileName = "Relazione_palo_orizzontale.docx" };
            if (save.ShowDialog(this) == true) editor.ExportReport(save.FileName, heading.Text, []); return;
        }
        var list = new StackPanel { Margin = new Thickness(16) }; var checks = new Dictionary<string, CheckBox>();
        foreach (var (key, label) in ReportWord.Sezioni) { var check = new CheckBox { Content = label, IsChecked = !key.StartsWith("grafico_"), Margin = new Thickness(4) }; checks[key] = check; list.Children.Add(check); }
        var window = Ui.Dialog(this, "Contenuti del report Word", list, 460, 500); var ok = Ui.Button("Esporta", () => window.DialogResult = true, true); list.Children.Add(ok); if (window.ShowDialog() != true) return;
        var d = new SaveFileDialog { Filter = "Documento Word|*.docx", FileName = "Relazione.docx" }; if (d.ShowDialog(this) == true) editor.ExportReport(d.FileName, heading.Text, checks.Where(p => p.Value.IsChecked == true).Select(p => p.Key).ToHashSet());
    }
}
