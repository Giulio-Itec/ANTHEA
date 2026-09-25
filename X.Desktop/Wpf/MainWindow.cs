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
    private bool dirty, testing;
    private SheetEditor? editor;
    private readonly ContentControl body = new();
    private readonly Grid dashboard = new();
    private readonly ScrollViewer dashboardViewport;
    private readonly DockPanel moduleView = new();
    private readonly ContentControl sheetProjectTreeControl = new() { VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
    private readonly ContentControl sheetProjectActions = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly ContentControl dashboardBody = new() { Margin = new Thickness(42, 24, 42, 36) };
    private readonly ContentControl sheetContent = new();
    private readonly TextBlock heading = Ui.Text("", 17, true, Brushes.White);
    private Button backToOverview = null!;
    private readonly TreeView tree = new() { BorderThickness = new Thickness(0), Background = Brushes.White };
    private readonly Dictionary<string, Button> navigation = new();

    public MainWindow()
    {
        Style = (Style)Application.Current.FindResource(typeof(Window));
        Title = "ANTHEA"; Width = 1600; Height = 990; MinWidth = 760; MinHeight = 480; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        dashboardViewport = DisplayAdaptation.Viewport(dashboard, 1120, 680);
        var root = Ui.Dock(body, BuildMenu()); root.Background = Ui.Bg; Content = root; BuildShell(); ShowHome();
        DisplayAdaptation.Attach(this);
        PreviewMouseDown += (_, _) => CancelProjectSectionClick();
        Deactivated += (_, _) => CancelProjectSectionClick();
        Closing += (_, e) => { if (testing) return; e.Cancel = !ConfirmDiscard(); };
        Closed += (_, _) => { CancelProjectSectionClick(); editor?.Dispose(); };

    }
    internal void Safe(Action action) { try { action(); } catch (Exception ex) { if (testing) throw; MessageBox.Show(this, ex.Message, "Operazione non completata", MessageBoxButton.OK, MessageBoxImage.Error); } }
    internal static string ModuleName(string module) => module switch { "geo_palo_verticale" => "Palo · capacità portante", "geo_palo_orizzontale" => "Palo · capacità portante orizzontale", "geo_micropalo_verticale" => "Micropalo · Bustamante–Doix", MicropaloOrizzontale.Module => "Micropalo · capacità portante orizzontale", "str_palo" => "Sezione in c.a. · SLU / SLV / SLE", BridgeSection.Module => "Sezione composta · ponte / classe 4", "mat_calcestruzzo" => "Calcestruzzo · Materiali", RebarMaterial.Module => "Acciaio per armature · Materiali", _ => module };
    private Menu BuildMenu()
    {
        var menu = new Menu { Background = Brushes.White }; var file = new MenuItem { Header = "_File" }; menu.Items.Add(file);
        void Add(string title, Action action, Key? key = null, ModifierKeys modifiers = ModifierKeys.Control)
        {
            var item = new MenuItem { Header = title }; item.Click += (_, _) => Safe(action); file.Items.Add(item);
            if (key is Key k) { var command = new RoutedCommand(); CommandBindings.Add(new CommandBinding(command, (_, _) => Safe(action))); InputBindings.Add(new KeyBinding(command, k, modifiers)); item.InputGestureText = new KeyGesture(k, modifiers).GetDisplayStringForCulture(System.Globalization.CultureInfo.CurrentCulture); }
        }
        Add("Nuovo palo", () => NewCalculation("geo_palo_verticale"), Key.N); Add("Nuovo micropalo", () => NewCalculation("geo_micropalo_verticale")); Add("Nuova sezione in c.a.", () => NewCalculation("str_palo")); Add("Nuovo archivio progetti", NewProjects);
        Add("Nuova sezione composta da ponte", () => NewCalculation(BridgeSection.Module));
        Add("Nuovo palo orizzontale", () => NewCalculation(PaloOrizzontale.Module));
        Add("Nuovo micropalo orizzontale", () => NewCalculation(MicropaloOrizzontale.Module));
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
            var b = Ui.Button(title, () => Safe(() => { Commit(); action(); })); b.Height = 45; b.HorizontalContentAlignment = HorizontalAlignment.Left; b.FontWeight = FontWeights.SemiBold; nav.Children.Add(b); navigation[title] = b;
        }
        nav.Children.Add(FileCommands(false)); var footer = Ui.Text($"Moduli disponibili: {Archivio.Moduli.Length} di {Archivio.Moduli.Length + 1}", color: Ui.Muted); footer.Margin = new Thickness(20);
        var sidebar = Ui.Dock(nav, bottom: footer); sidebar.Background = Brushes.White; dashboard.Children.Add(sidebar); Grid.SetColumn(dashboardBody, 1); dashboard.Children.Add(dashboardBody);
        var top = new DockPanel { Background = Ui.Navy, MinHeight = 68, LastChildFill = true };
        top.Children.Add(sheetProjectTreeControl);
        var back = backToOverview = CommandButton("← Torna ad ANTHEA", () => Safe(() => { Commit(); if (document.S("tipo") == "progetti") ShowProjects(); else ShowHome(); })); back.Width = 200; top.Children.Add(back); top.Children.Add(FileCommands(true));
        confirmShared = CommandButton("Conferma modifiche", () => Safe(Commit)); confirmShared.Visibility = Visibility.Collapsed; top.Children.Add(confirmShared);
        top.Children.Add(sheetProjectActions);
        var titles = Ui.Stack(heading, Ui.Text("Scheda di calcolo · input, profilo e risultati", 12, color: Ui.Brush("#B9C8D8"))); titles.Margin = new Thickness(15, 10, 0, 0); top.Children.Add(titles);
        DockPanel.SetDock(top, System.Windows.Controls.Dock.Top); moduleView.Children.Add(top); DockPanel.SetDock(sharedStatus, Dock.Top); moduleView.Children.Add(sharedStatus); moduleView.Children.Add(sheetContent);
    }
    private static Button CommandButton(string title, Action action, bool dark = true)
    {
        var button = Ui.Button(title, action, dark);
        button.Style = (Style)Application.Current.FindResource(dark ? "ProjectCommandButton" : "ProjectButton");
        button.Height = 36; button.Padding = new Thickness(10, 2, 10, 2);
        button.VerticalAlignment = VerticalAlignment.Center;
        return button;
    }
    private WrapPanel FileCommands(bool dark, bool includeReport = true)
    {
        var bar = new WrapPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 6, 0, 6) };
        foreach (var (label, icon, action) in new (string, string, Action)[] { ("Apri", "📂", Open), ("Salva", "▣", () => Save(false)), ("Salva con nome", "▤", () => Save(true)), ("Report Word", "W", ExportReport) })
        {
            if ((!dark || !includeReport) && label == "Report Word") continue;
            bool save = label is "Salva" or "Salva con nome";
            var b = CommandButton(save ? label : icon, () => Safe(action), dark); b.ToolTip = label; b.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, label);
            if (!save) { b.Width = 37; b.Padding = new Thickness(2); } bar.Children.Add(b);
        }
        return bar;
    }
    private void SelectNavigation(string name)
    {
        ExitRevisionPreview();
        projectContent.Content = null;
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
        Card(0, "Moduli singoli", "Usa un modulo direttamente, senza creare un progetto.", $"{Archivio.Moduli.Length} moduli disponibili", "Apri i moduli", () => ShowModules());
        Card(1, "Progetti", "Raggruppa i fogli per opera, spalla, pila o altra struttura.", "Struttura gerarchica libera", "Gestisci i progetti", ShowProjects); dashboardBody.Content = layout;
    }
    private void ShowModules(string discipline = "Tutti")
    {
        SelectNavigation("Moduli singoli"); var root = new DockPanel(); var intro = Ui.Stack(Ui.Text("Moduli singoli", 30, true), Ui.Text("Scegli disciplina, elemento e verifica. I dati restano indipendenti da un progetto.", color: Ui.Muted)); intro.Margin = new Thickness(0, 0, 0, 24); DockPanel.SetDock(intro, System.Windows.Controls.Dock.Top); root.Children.Add(intro);
        var filters = Ui.Stack(Ui.Text("DISCIPLINE", 13, true)); filters.Width = 185; foreach (string name in new[] { "Tutti", "Geotecnica", "Strutture", "Materiali" }) filters.Children.Add(Ui.Button(name, () => ShowModules(name), discipline == name));
        var fp = Ui.Paper(filters, 16); fp.Margin = new Thickness(0, 0, 16, 0); DockPanel.SetDock(fp, System.Windows.Controls.Dock.Left); root.Children.Add(fp);
        var list = new StackPanel();
        var modules = new[] { ("Geotecnica", "Palo", "Capacità portante verticale", "geo_palo_verticale"), ("Geotecnica", "Palo", "Capacità portante orizzontale", "geo_palo_orizzontale"), ("Geotecnica", "Micropalo", "Capacità portante verticale", "geo_micropalo_verticale"), ("Geotecnica", "Micropalo", "Capacità portante orizzontale", MicropaloOrizzontale.Module), ("Strutture", "Sezione in c.a.", "Verifiche SLU · SLV · SLE", "str_palo"), ("Strutture", "Sezione composta", "Ponte · fasi e classe 4", BridgeSection.Module), ("Materiali", "Calcestruzzo", "Proprietà, copriferro e composizione", "mat_calcestruzzo"), ("Materiali", "Acciaio per armature", "Proprietà meccaniche e diagramma", RebarMaterial.Module) };
        foreach (string area in new[] { "Geotecnica", "Strutture", "Materiali" }.Where(a => discipline == "Tutti" || a == discipline))
        {
            var label = Ui.Text(area, 21, true); label.Margin = new Thickness(0, 10, 0, 14); list.Children.Add(label);
            var groups = new Grid(); groups.ColumnDefinitions.Add(new ColumnDefinition()); groups.ColumnDefinitions.Add(new ColumnDefinition()); int col = 0;
            foreach (var group in modules.Where(m => m.Item1 == area).GroupBy(m => m.Item2))
            {
                var pane = new StackPanel(); var head = Ui.Text(group.Key.ToUpperInvariant(), 13, true, Brushes.White); head.Padding = new Thickness(8); pane.Children.Add(new Border { Child = head, Background = Ui.Navy });
                foreach (var (_, title, description, id) in group)
                {
                    string icon = id != "" ? id : area == "Strutture" ? "str_micropalo" : title == "Palo" ? "geo_palo_orizzontale" : "geo_micropalo_orizzontale";
                    var copy = Ui.Stack(Ui.Text(id == "" ? "In preparazione" : "Disponibile", 11, true, id == "" ? Ui.Muted : Brushes.ForestGreen), Ui.Text(description, 17, true), Ui.Text(area + " · " + title, 12, color: Ui.Muted), Ui.Text("Referente: " + (area == "Strutture" ? "GPC" : "GSC"), 12, true, Ui.Muted)); copy.Margin = new Thickness(12);
                    var row = new DockPanel(); row.Children.Add(Ui.ModuleIcon(icon)); row.Children.Add(copy);
                    var open = Ui.Button(id == "" ? "Dettagli" : editor?.Module == id ? "Riprendi" : "Apri", () => { if (id != "") Safe(() => OpenModule(id)); else MessageBox.Show(this, "Modulo in preparazione, come nella versione originale."); }, id != ""); open.Tag = id; open.HorizontalAlignment = HorizontalAlignment.Right;
                    var card = Ui.Paper(Ui.Dock(row, bottom: open)); card.Height = 200; card.Margin = new Thickness(0, 0, 0, 8); pane.Children.Add(card);
                }
                pane.Margin = new Thickness(0, 0, 14, 0); if (col % 2 == 0) groups.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); Grid.SetColumn(pane, col % 2); Grid.SetRow(pane, col / 2); col++; groups.Children.Add(pane);
            }
            list.Children.Add(groups);
        }
        root.Children.Add(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }); dashboardBody.Content = root;
    }
    private void ShowProjects()
    {
        Commit(); ShowProjectOverview();
    }
    private void MarkDirty() { if (projectReadOnly) return; dirty = true; RecordProjectHistory(); UpdateTitle(); }
    private void UpdateTitle() => Title = "ANTHEA — " + (path is null ? "Nuovo documento" : Path.GetFileName(path)) + (dirty ? " *" : "") +
        (displayedRevision is int rev ? $" · Rev. {rev} · sola lettura" : "");
    private void Commit()
    {
        if (projectReadOnly || editor is null || currentSheet is null) return;
        editor.Commit();
        var proposed = (JsonObject)currentSheet.DeepClone(); proposed["dati"] = editor.Data.DeepClone();
        ConfirmSharedChanges(proposed);
        currentSheet["dati"] = editor.Data.DeepClone();
        sharedBaseline = (JsonObject)proposed.DeepClone();
        RecordProjectHistory(); RefreshSharedStatus();
    }
    private void ShowSheet(JsonObject sheet)
    {
        if (editor is not null && ReferenceEquals(sheet, currentSheet)) { ResumeCalculation(); return; }
        LoadSheetEditor(sheet);
        if (document.S("tipo") == "progetti") ShowProjectSheet(); else { projectContent.Content = null; body.Content = moduleView; sheetContent.IsEnabled = true; }
        RefreshSharedStatus();
    }
    private void LoadSheetEditor(JsonObject sheet)
    {
        editor?.Dispose(); currentSheet = sheet; string module = sheet.S("modulo_id");
        editor = new SheetEditor(module, sheet["dati"] as JsonObject ?? Archivio.NuovoFoglio(module)); editor.Modified += MarkDirty; editor.Modified += RefreshSharedStatus;
        if (projectReadOnly) editor.InspectRevision();
        sharedBaseline = (JsonObject)sheet.DeepClone(); sharedBaseline["dati"] = editor.Data.DeepClone();
        sheetContent.Content = module is "geo_palo_verticale" or "geo_micropalo_verticale" or PaloOrizzontale.Module or MicropaloOrizzontale.Module or "mat_calcestruzzo" or RebarMaterial.Module or BridgeSection.Module
            ? editor : DisplayAdaptation.Viewport(editor, 1120, 600);
        heading.Text = SheetHeading(sheet); UpdateBackButton();
    }
    private void RefreshTree(JsonObject? selected = null)
    {
        CancelProjectSectionClick(); finishProjectRename?.Invoke(true);
        refreshingProjectTree = true;
        foreach (var root in tree.Items.OfType<TreeViewItem>()) RememberExpansion(root);
        var selection = selected ?? currentSheet;
        for (var parent = selection?.Parent?.Parent as JsonObject; parent is not null; parent = parent.Parent?.Parent as JsonObject) collapsedProjectNodes.Remove(parent.S("id"));
        tree.Items.Clear();
        TreeViewItem? selectedItem = null;
        TreeViewItem Node(JsonObject value, string fallback)
        {
            var item = new TreeViewItem { Header = value.S("nome", fallback), Tag = value, IsExpanded = !collapsedProjectNodes.Contains(value.S("id")),
                Style = (Style)FindResource("ProjectTreeItem"), BorderThickness = new Thickness(0, 0, 0, 1), BorderBrush = Ui.Brush("#EDF2F7") };
            ConfigureProjectNode(item, value);
            item.MouseRightButtonDown += (_, e) => { item.IsSelected = true; e.Handled = true; };
            var menu = new ContextMenu(); void Action(string name, Action action) { var m = new MenuItem { Header = name }; m.Click += (_, _) => Safe(action); menu.Items.Add(m); }
            if (!projectReadOnly)
            {
                Action("Rinomina", () => BeginProjectRename(item));
                if (!value.ContainsKey("modulo_id")) Action("Duplica", () => DuplicateProjectSection(value));
                Action("Elimina", Delete); item.ContextMenu = menu;
            }
            foreach (var sheet in value.Array("fogli").OfType<JsonObject>()) item.Items.Add(Node(sheet, ModuleName(sheet.S("modulo_id"))));
            foreach (var child in value.Array("strutture").OfType<JsonObject>()) item.Items.Add(Node(child, "Sezione"));
            if (ReferenceEquals(value, selection)) selectedItem = item; return item;
        }
        foreach (var p in document.Array("progetti").OfType<JsonObject>())
        {
            var pn = Node(p, "Progetto"); tree.Items.Add(pn);
        }
        tree.UpdateLayout();
        if (selectedItem is not null) { selectedItem.IsSelected = true; selectedItem.BringIntoView(); }
        if (projectEmptyState is not null)
        {
            bool empty = tree.Items.Count == 0; projectEmptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
            var roots = document.Array("progetti").OfType<JsonObject>().ToArray();
            int sections = roots.Sum(p => ProjectSharedData.Sections(p).Count() - 1), sheets = roots.Sum(p => ProjectSharedData.SubtreeSheets(p).Count());
            projectTreeSummary.Text = $"{roots.Length} {(roots.Length == 1 ? "progetto" : "progetti")} · {sections} {(sections == 1 ? "sezione" : "sezioni")} · {sheets} {(sheets == 1 ? "foglio" : "fogli")}";
            projectTreeSummary.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        }
        refreshingProjectTree = false;
    }
    private void RememberExpansion(TreeViewItem item)
    {
        if (item.Tag is JsonObject node) { if (item.IsExpanded) collapsedProjectNodes.Remove(node.S("id")); else collapsedProjectNodes.Add(node.S("id")); }
        foreach (var child in item.Items.OfType<TreeViewItem>()) RememberExpansion(child);
    }
    private bool ConfirmDiscard()
    {
        Commit(); if (!dirty) return true;
        var answer = MessageBox.Show(this, "Salvare le modifiche al documento corrente?", "ANTHEA", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        return answer != MessageBoxResult.Cancel && (answer != MessageBoxResult.Yes || Save(false));
    }
    private void NewCalculation(string module)
    { if (!ConfirmDiscard()) return; ExitRevisionPreview(); document = Archivio.Documento(module); path = null; dirty = false; currentSheet = null; ShowSheet(document); RefreshTree(); UpdateTitle(); }
    private void OpenModule(string module)
    {
        if (editor?.Module == module && currentSheet is not null) ResumeCalculation();
        else NewCalculation(module);
    }
    private void ResumeCalculation()
    {
        if (editor is null || currentSheet is null) return;
        heading.Text = SheetHeading(currentSheet);
        UpdateBackButton(); if (document.S("tipo") == "progetti") ShowProjectSheet(); else { projectContent.Content = null; body.Content = moduleView; }
    }
    private void UpdateBackButton()
    {
        bool project = document.S("tipo") == "progetti";
        sheetProjectTreeControl.Visibility = project ? Visibility.Visible : Visibility.Collapsed;
        backToOverview.Content = project ? "← Torna al progetto" : "← Torna ad ANTHEA";
        confirmShared.Visibility = project && !projectReadOnly ? Visibility.Visible : Visibility.Collapsed;
        sheetProjectActions.Visibility = project && !projectReadOnly ? Visibility.Visible : Visibility.Collapsed;
    }
    private void NewProjects()
    {
        if (!ConfirmDiscard()) return;
        ExitRevisionPreview();
        editor?.Dispose(); editor = null; currentSheet = null; sheetContent.Content = null;
        document = J.Obj(("formato", "X"), ("versione", 1), ("tipo", "progetti"), ("progetti", new JsonArray())); path = null; dirty = false; ShowProjects(); UpdateTitle();
    }
    private void AddProject()
    {
        if (projectReadOnly) return;
        if (document.S("tipo") != "progetti") { NewProjects(); if (document.S("tipo") != "progetti") return; }
        string name = NextProjectName(document.Array("progetti"), "Progetto");
        Commit(); var p = J.Obj(("id", Guid.NewGuid().ToString("N")), ("nome", name), ("strutture", new JsonArray())); document.Array("progetti").Add(p); MarkDirty(); ShowProjectOverview(p);
    }
    private void AddStructure()
    {
        if (document.S("tipo") != "progetti") { MessageBox.Show(this, "Creare o aprire un archivio progetti dal menu File."); return; }
        var target = SelectedProjectContainer();
        if (target is null) { AddProject(); return; }
        AddStructureTo(target);
    }
    private JsonObject? SelectedProjectContainer()
    {
        var value = (tree.SelectedItem as TreeViewItem)?.Tag as JsonObject;
        return value?.ContainsKey("modulo_id") == true ? value.Parent?.Parent as JsonObject : value;
    }
    private void AddStructureTo(JsonObject parent)
    {
        if (projectReadOnly) return;
        string name = NextProjectName(parent.Array("strutture"), "Sezione");
        Commit(); var section = CreateProjectSection(parent, name); MarkDirty(); ShowProjectOverview(section);
    }
    private static JsonObject CreateProjectSection(JsonObject parent, string name)
    {
        if (parent["strutture"] is not JsonArray) parent["strutture"] = new JsonArray();
        var section = J.Obj(("id", Guid.NewGuid().ToString("N")), ("nome", name), ("strutture", new JsonArray()), ("fogli", new JsonArray()));
        parent.Array("strutture").Add(section); return section;
    }
    private void AddSheet(string module) => AddSheetTo(module, SelectedProjectContainer(), true);
    private void AddSheetTo(string module, JsonObject? section, bool open)
    {
        if (projectReadOnly) return;
        if (document.S("tipo") != "progetti") { NewCalculation(module); return; }
        if (section is null || !(section.ContainsKey("fogli") || section.ContainsKey("strutture"))) { MessageBox.Show(this, "Selezionare una sezione nell'albero dei progetti."); return; }
        if (section["fogli"] is not JsonArray) section["fogli"] = new JsonArray();
        string name = NextProjectName(section.Array("fogli"), ModuleName(module));
        Commit(); var sheet = J.Obj(("id", Guid.NewGuid().ToString("N")), ("nome", name), ("modulo_id", module), ("dati", Archivio.NuovoFoglio(module)));
        section.Array("fogli").Add(sheet); InheritSectionData(sheet, section); MarkDirty(); RefreshTree(sheet); if (open) ShowSheet(sheet); else if (!ReferenceEquals(projectContent.Content, moduleView)) ShowProjectOverview(section);
    }

    private void Rename()
    {
        if (tree.SelectedItem is TreeViewItem node) BeginProjectRename(node);
    }
    private void Delete()
    {
        if (projectReadOnly || document.S("tipo") != "progetti" || tree.SelectedItem is not TreeViewItem { Tag: JsonObject target }) return;
        if (MessageBox.Show(this, "Eliminare l'elemento selezionato e i suoi contenuti dal documento? Il file su disco resta invariato fino al salvataggio.", "Elimina", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        DeleteProjectNode(target);
    }
    private void DeleteProjectNode(JsonObject target)
    {
        if (projectReadOnly) return;
        Commit(); var parent = target.Parent?.Parent as JsonObject; if (target.Parent is JsonArray list) list.Remove(target); editor?.Dispose(); editor = null; currentSheet = null; sheetContent.Content = null; MarkDirty(); ShowProjectOverview(parent?.ContainsKey("strutture") == true ? parent : null);
    }
    private void Open() { var d = new OpenFileDialog { Filter = "File ANTHEA|*.programma;*.anthea|Tutti i file|*.*" }; if (d.ShowDialog(this) == true) LoadFile(d.FileName); }
    internal void LoadFile(string filename)
    {
        var loaded = Archivio.Leggi(filename); if (!ConfirmDiscard()) return;
        ExitRevisionPreview();
        editor?.Dispose(); editor = null; currentSheet = null; document = loaded; path = filename; dirty = false; sheetContent.Content = null;
        if (document.S("tipo") == "calcolo") ShowSheet(document); else ShowProjects(); RefreshTree(); UpdateTitle();
    }
    private bool Save(bool asNew)
    {
        Commit(); string? target = path;
        if (asNew || target is null) { var d = new SaveFileDialog { Filter = "File ANTHEA|*.programma", FileName = path is null ? "Calcolo.programma" : Path.GetFileName(path) }; if (d.ShowDialog(this) != true) return false; target = d.FileName; }
        try { Archivio.Scrivi(target, WorkingProjectDocument); path = target; dirty = false; UpdateTitle(); return true; }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Salvataggio non riuscito", MessageBoxButton.OK, MessageBoxImage.Error); return false; }
    }
    private void ExportSheet()
    {
        Commit(); if (currentSheet is null) return; var d = new SaveFileDialog { Filter = "File ANTHEA|*.programma", FileName = "Foglio.programma" };
        if (d.ShowDialog(this) == true) Archivio.Scrivi(d.FileName, J.Obj(("formato", "X"), ("versione", 1), ("tipo", "calcolo"), ("modulo_id", currentSheet["modulo_id"]), ("dati", currentSheet["dati"])));
    }
    private void ExportJson()
    {
        if (editor?.Module is "mat_calcestruzzo" or RebarMaterial.Module) { MessageBox.Show(this, "Per conservare la scheda materiali usa Salva o Esporta foglio selezionato."); return; }
        Commit(); if (editor?.HasResults != true) { MessageBox.Show(this, editor?.Module == "str_palo" ? "Attendere l’aggiornamento automatico e correggere gli eventuali dati non validi." : "Premere Calcola prima di esportare i risultati."); return; }
        var d = new SaveFileDialog { Filter = "Risultati JSON|*.json", FileName = "Risultati.json" }; if (d.ShowDialog(this) == true) editor.ExportResult(d.FileName);
    }
    private void ExportReport()
    {
        if (editor?.Module is "mat_calcestruzzo" or RebarMaterial.Module)
        {
            Commit();
            var save = new SaveFileDialog { Filter = "Documento Word|*.docx", FileName = editor.Module == "mat_calcestruzzo" ? "Scheda_calcestruzzo.docx" : "Scheda_acciaio_armature.docx" };
            if (save.ShowDialog(this) == true) editor.ExportReport(save.FileName, heading.Text, []);
            return;
        }
        Commit(); if (editor?.HasResults != true || editor.Busy) { MessageBox.Show(this, editor?.Module == "str_palo" ? "Attendere l’aggiornamento automatico e correggere gli eventuali dati non validi." : "Completare il calcolo prima di esportare il report."); return; }
        if (editor.Module is PaloOrizzontale.Module or MicropaloOrizzontale.Module)
        {
            var save = new SaveFileDialog {
                Filter = "Documento Word|*.docx",
                FileName = editor.Module == MicropaloOrizzontale.Module
                    ? "Relazione_micropalo_orizzontale.docx" : "Relazione_palo_orizzontale.docx"
            };
            if (save.ShowDialog(this) == true) editor.ExportReport(save.FileName, heading.Text, []);
            return;
        }
        var list = new StackPanel { Margin = new Thickness(16) }; var checks = new Dictionary<string, CheckBox>();
        var saved = editor.Data[editor.Module == BridgeSection.Module ? "ui_mista" : "workspace_ca"]?["report_sezioni"] as JsonArray;
        var sections = editor.Module == BridgeSection.Module ? ReportBridge.Sections : editor.Module == "str_palo" ? ReportConcrete.Sections : ReportWord.Sezioni;
        foreach (var (key, label) in sections) { var check = new CheckBox { Content = label, IsChecked = saved is not null ? saved.Any(v => v?.ToString() == key) : editor.Module == BridgeSection.Module || (editor.Module == "str_palo" ? key is not ("dettagli" or "sle_tutte") : !key.StartsWith("grafico")), Margin = new Thickness(4) }; checks[key] = check; list.Children.Add(check); }
        if (editor.Module == BridgeSection.Module) list.Children.Add(Ui.Text("Stampa tutte le situazioni e i rispettivi contributi, indipendentemente dalla fase visualizzata. Ambito, riepilogo e avvisi sono sempre inclusi. I grafici riportano la geometria e la somma dei contributi di ogni situazione.", 11));
        if (editor.Module == "str_palo") list.Children.Add(Ui.Text("SLE: di default inviluppo degli estremi con combinazione di origine e casi governanti distinti per tensioni e fessurazione. Nessun esito non determinato viene escluso. In modalità completa si stampano tutte le combinazioni; i grafici SLE restano riferiti ai casi governanti. Ambito, avvisi ed errori sono sempre inclusi.", 11));
        var window = Ui.Dialog(this, "Contenuti del report Word", new ChainedScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, 590, 650); var ok = Ui.Button("Esporta", () => { if (checks.Any(c => c.Value.IsChecked == true && c.Key is not ("sle_tutte" or "dettagli" or "grafici"))) window.DialogResult = true; }, true); list.Children.Add(ok); if (window.ShowDialog() != true) return;
        var d = new SaveFileDialog { Filter = "Documento Word|*.docx", FileName = "Relazione.docx" }; if (d.ShowDialog(this) == true) editor.ExportReport(d.FileName, heading.Text, checks.Where(p => p.Value.IsChecked == true).Select(p => p.Key).ToHashSet());
    }
}
