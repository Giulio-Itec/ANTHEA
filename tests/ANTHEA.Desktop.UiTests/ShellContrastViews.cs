using System.IO;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Anthea.Application.Documents;
using Anthea.Presentation.Services;
using Anthea.Presentation.Shell.Navigation;
using X.Core;
using X.Desktop.Services;

namespace X.Desktop;

/// <summary>
/// F5.13 g3 (G2): the views of --check-contrast on the new shell, in place of the partial of MainWindow (archived with it). The steps, their names and
/// their order are those of MainWindow (home, moduli, wiki, progetti, modulo-*, componenti-campi, cambio-*): the shell of the Release is opened on the bench
/// of part (e) (<see cref="ShellSmokeWindow"/>, the facade of the container suites), off screen through the hooks of <see cref="ContrastAudit"/>, with a
/// fixed client area of 1600 × 1000. Where MainWindow called its own methods, the shell gets the command the user presses (tree, overview, revision
/// bar, sheet bar, menu). The dialogs of the MVVM views open as in the product (a window around the ViewModel, as WpfDialogService shows it), so that the
/// hooks capture them; the file dialogs answer «Annulla», the questions «Non salvare» and «No».
/// </summary>
internal sealed class ShellContrastViews : IDisposable
{
    private readonly ContrastAudit a;

    private ShellContrastViews(ContrastAudit audit, ShellSmokeWindow window) { a = audit; W = window; }

    internal ShellSmokeWindow W { get; }
    internal ShellSmokeBench Bench => W.Bench;
    internal Window Window => W.Window;
    private FrameworkElement AuditRoot => ContrastAudit.WindowRoot(Window);
    private FrameworkElement Center => W.dashboardBody;

    /// <summary>The shell off screen (the hooks of the audit move and cloak it), without frame, client area 1600 × 1000; returns it with the DPI of its window.</summary>
    internal static async Task<(ShellContrastViews Views, double Dpi)> OpenAsync(ContrastAudit audit, string output)
    {
        var window = await ShellSmokeWindow.OpenAsync(Path.Combine(output, "banco"));
        var views = new ShellContrastViews(audit, window);
        var services = window.Bench.Services;
        services.DialogAsync = dialog => ShowDialog(window.Window, dialog);
        services.SaveFile = _ => null;
        services.OpenFileAnswer = _ => null;
        services.Clipboard = new ScriptedClipboard();
        services.Confirmations.SaveChanges = () => SaveChangesAnswer.Discard;
        services.Confirmations.Delete = (_, _) => false;
        var shell = window.Window;
        shell.WindowStyle = WindowStyle.None; shell.ResizeMode = ResizeMode.NoResize;
        shell.Width = ContrastAudit.CaptureWidth; shell.Height = ContrastAudit.CaptureHeight;
        double dpi = OffscreenWindows.ForceSize(new WindowInteropHelper(shell).Handle, ContrastAudit.CaptureWidth, ContrastAudit.CaptureHeight);
        await audit.Settle(100);
        return (views, dpi);
    }

    /// <summary>A dialog of an MVVM view shown as WpfDialogService shows it (modal, owned by the shell): the hooks capture it and close it (no result).</summary>
    private static Task<object?> ShowDialog(Window owner, object dialog)
    {
        var result = dialog.GetType().GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDialogViewModel<>)).GetGenericArguments()[0];
        var show = typeof(IDialogService).GetMethod(nameof(IDialogService.ShowDialogAsync))!.MakeGenericMethod(result);
        var task = (Task)show.Invoke(new WpfDialogService(() => owner), [dialog])!;
        return Task.FromResult(task.GetType().GetProperty("Result")!.GetValue(task));
    }

    public void Dispose() => W.Dispose();

    /// <summary>Before the next document: every tab closed without saving, the project closed (MainWindow.AuditReset).</summary>
    private void AuditReset()
    {
        var workspace = Bench.Workspace;
        Bench.Services.Confirmations.SaveChanges = () => SaveChangesAnswer.Discard;
        if (workspace.Project is { IsPreviewingRevision: true } preview) preview.ExitPreview();
        foreach (var sheet in workspace.Sheets.ToArray()) workspace.CloseSheet(sheet);
        workspace.CloseProject();
        // The page tabs (the Wiki): MainWindow showed one page at a time.
        foreach (var page in Bench.Shell.Windows.Pages.ToArray()) Bench.Shell.Windows.Close(page);
        W.ShowHome();
    }

    internal async Task AuditWaitEditor(bool calculate)
    {
        if (W.editor is not { } editor) return;
        // The sheet with the focus (MVVM view in its tab): «Calcola», then the end of the calculation.
        if (calculate) { try { await editor.CalculateAsync(); } catch (Exception ex) { a.Note(a.Context, "Calcolo: " + ex.Message); } }
        if (!await a.WaitUntil(() => W.editor is not { } current || !current.Busy, 120)) a.Note(a.Context, "calcolo ancora in corso dopo 120 s");
        await a.Settle(60);
    }

    /// <summary>Every view of the shell, in the order of the views of MainWindow; windows and popups are captured by the hooks of ContrastAudit.</summary>
    internal async Task AuditContrastViews()
    {
        await a.Step("home", async () =>
        {
            W.ShowHome(); await a.Settle();
            await a.SnapStable("home", AuditRoot, "vista");
            if (ContrastAudit.FirstWithToolTip(Center) is { } tipped) await a.SnapToolTip(tipped, "home--suggerimento");
            if (Ui.Descendants<Menu>(Window).FirstOrDefault() is { } menu)
                foreach (var item in menu.Items.OfType<object>().Select(i => menu.ItemContainerGenerator.ContainerFromItem(i) as MenuItem ?? i as MenuItem).OfType<MenuItem>())
                    await a.SnapSubmenu(item, "menu-" + ContrastAudit.Slug(MenuHeader(item), 20));
        });
        await a.Step("moduli", async () =>
        {
            W.ShowModules(); await a.Settle(); await a.SnapStable("moduli", AuditRoot, "vista");
            await a.SnapPages(Center, "moduli", AuditRoot);
            W.ShowModules("Strutture"); await a.Settle(); await a.SnapStable("moduli-strutture", AuditRoot, "vista");
            W.ShowModules(); await a.Settle();
        });
        await a.Step("wiki", AuditWiki);
        await a.Step("progetti", AuditProjects);
        foreach (var module in Anthea.Application.Modules.ModuleDirectory.All) await a.Step("modulo-" + module.Id, () => AuditModule(module.Id));
        // Common components of the MVVM views (F5.2): FieldForm in a dialog.
        await a.Step("componenti-campi", () => FieldFormContrast.Audit(a, Window));
        AuditReset();
    }

    private static string MenuHeader(MenuItem item) => (item.Header switch
    {
        string s => s,
        TextBlock t => t.Text,
        _ => item.Header?.GetType().GetProperty("Title")?.GetValue(item.Header) as string ?? item.Header?.ToString()
    } ?? "voce").Replace("_", "");

    private async Task AuditWiki()
    {
        W.ShowWiki(); await a.Settle(150); await a.SnapStable("wiki-indice", AuditRoot, "vista");
        W.ShowWiki("/wiki/manuale"); await a.Settle(150); await a.SnapStable("wiki-manuale", AuditRoot, "vista");
        if (WikiCatalog.ForModule("geo_palo_verticale") is { } guide) { W.ShowWiki(guide.Id); await a.Settle(200); await a.SnapStable("wiki-guida-palo", AuditRoot, "vista"); await a.SnapPages(W.wiki!, "wiki-guida-palo", AuditRoot); }
        var theory = WikiCatalog.Resolve("beam") ?? WikiCatalog.Articles.First(x => x.Type == "theory" && x.Status != "historical");
        W.ShowWiki(theory.Id); await a.Settle(200); await a.SnapStable("wiki-teoria", AuditRoot, "vista"); await a.SnapPages(W.wiki!, "wiki-teoria", AuditRoot);
        a.Context = "wiki-teoria";
        var wiki = W.wiki!;
        if (Ui.Descendants<Button>(wiki).FirstOrDefault(b => b.IsVisible && ContrastAudit.ButtonLabel(b) == "⋯") is { } dots)
            await a.Trigger("azioni della sezione", () => ContrastAudit.Click(dots));
        if (ContrastAudit.FindButton(wiki, "Apri collegamento") is { } ask) await a.Trigger("Apri collegamento", () => ContrastAudit.Click(ask));
        if (Ui.Descendants<ComboBox>(wiki).FirstOrDefault(c => c.IsVisible) is { } selector) await a.SnapComboBox(selector, "wiki--elenco-aspetto");
        if (Ui.Descendants<TextBox>(wiki).FirstOrDefault(t => AutomationProperties.GetName(t) == "Ricerca nella Wiki") is { } search)
        {
            search.Text = "palo"; await Task.Delay(500); await a.Settle(150); await a.SnapStable("wiki-ricerca", AuditRoot, "vista");
            search.Text = ""; await Task.Delay(500); await a.Settle();
        }
        W.ShowHome(); await a.Settle();
    }

    private static void AuditFixRevisionDates(JsonNode? node)
    {
        // Revision dates come from DateTime.Now: fixed, so that the reference captures stay reproducible.
        if (node is JsonObject o)
        {
            if (o.ContainsKey("numero") && o.ContainsKey("data")) o["data"] = "2026-01-15T10:30:00.0000000+01:00";
            foreach (var (_, child) in o.ToList()) AuditFixRevisionDates(child);
        }
        else if (node is JsonArray array) foreach (var child in array) AuditFixRevisionDates(child);
    }

    /// <summary>The overview of a node of the project, as the user selects it in the tree (MainWindow.ShowProjectOverview).</summary>
    private async Task Overview(JsonObject node)
    {
        W.ShowProjects();
        try { W.SelectInTree(node); } catch (InvalidOperationException) { }
        Bench.Shell.ShowOverview(node); W.UpdateLayout();
        await a.Settle();
    }

    private async Task AuditProjects()
    {
        AuditReset(); W.NewProjects(); await a.Settle(); await a.SnapStable("progetti-vuoto", AuditRoot, "vista");
        if (!File.Exists(a.TestArchive)) { a.Uncovered("Progetti con archivio di prova", "archivio di prova assente: " + a.TestArchive); return; }
        AuditReset(); W.LoadFile(a.TestArchive); W.ShowProjects(); await a.Settle();
        var shell = Bench.Shell;
        var document = Bench.Project.Document;
        var project = document.Array("progetti").OfType<JsonObject>().First();
        var sections = project.Array("strutture").OfType<JsonObject>().ToArray();
        var first = sections[0]; var second = sections.Length > 1 ? sections[1] : sections[0];
        string firstId = first.S("id");
        await Overview(project); await a.SnapStable("progetti-progetto", AuditRoot, "vista");
        await Overview(first); await a.SnapStable("progetti-sezione", AuditRoot, "vista");
        foreach (var expander in Ui.Descendants<Expander>(Center).Where(x => !x.IsExpanded)) expander.IsExpanded = true;
        await a.Settle(); await a.SnapStable("progetti-sezione-espansa", AuditRoot, "vista");
        if (Ui.Descendants<Border>(Center).FirstOrDefault(b => b.IsVisible && b.DataContext is Anthea.Presentation.Shell.Projects.ProjectCatalogCard) is { } card)
        {
            card.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
            await a.Settle(); await a.SnapStable("progetti-catalogo-hover", AuditRoot, "vista");
            card.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseLeaveEvent }); await a.Settle();
        }
        a.Context = "progetti-sezione";
        try
        {
            var item = Bench.Item(first);
            var menu = Bench.Menu(first);
            await a.Trigger("menu dell'albero", () => menu.IsOpen = true);
            if (ContrastAudit.FirstWithToolTip(item) is { } label) await a.SnapToolTip(label, "progetti--suggerimento-albero");
            var node = Bench.Node(first);
            node.BeginRenameCommand.Execute(null); await a.Settle(); await a.SnapStable("progetti-rinomina", AuditRoot, "vista");
            node.EndRename(false); await a.Settle();
        }
        catch (InvalidOperationException ex) { a.Note("progetti-sezione", "albero: " + ex.Message); }
        await Overview(first);
        await a.Trigger("Confronto e avvisi", () => _ = shell.ShowCoherenceAsync(first));
        // A common value that differs between two sheets of the section: conflicts in the overview, in the comparison and before the report.
        a.Context = "progetti-conflitto";
        if (first.Array("fogli").OfType<JsonObject>().Where(s => s.S("modulo_id") == "mat_calcestruzzo").Skip(1).FirstOrDefault()?["dati"] is JsonObject concrete)
        {
            concrete["classe"] = "C35/45";
            await Overview(second); await Overview(first); await a.SnapStable("progetti-conflitto", AuditRoot, "vista");
            await a.Trigger("Confronto e avvisi", () => _ = shell.ShowCoherenceAsync(first));
        }
        var reportSection = new[] { first, second, project }.FirstOrDefault(s => ProjectSharedData.SubtreeSheets(s).Any() && ProjectSharedData.Differences(s).Count > 0);
        if (reportSection is not null) await a.Trigger("Genera report", () => _ = shell.ExportSectionReportAsync(reportSection));
        a.Context = "progetti-sezione";
        var confirmations = new WpfConfirmationService(() => Window);
        await a.Trigger("Dati condivisi", () => confirmations.ConfirmSharedUpdate(new SharedUpdatePrompt(["Materiali"], first.S("nome"),
            [new SharedUpdateTarget("CLS seconda verifica", ["CLS · fck [MPa]: 30 → 35", "Classe di esposizione: XC1 → XC3"])])));
        string[] changes = [];
        if (first.Array("fogli").OfType<JsonObject>().FirstOrDefault(s => s.S("modulo_id") == "mat_calcestruzzo") is { } moving && !ReferenceEquals(first, second))
        {
            Bench.Services.Confirmations.ProjectMove = c => { changes = c; return false; };
            try { Bench.Project.MoveSheet(moving, second); } finally { Bench.Services.Confirmations.ProjectMove = null; }
        }
        if (changes.Length == 0) changes = ["CLS di riferimento · Classe calcestruzzo: C30/37 → C25/30"];
        await a.Trigger("Conferma spostamento", () => confirmations.ConfirmProjectMove(second.S("nome"), changes));

        a.Context = "progetti-foglio";
        var sheet = first.Array("fogli").OfType<JsonObject>().FirstOrDefault(s => s.S("modulo_id") == "str_palo") ?? first.Array("fogli").OfType<JsonObject>().First();
        bool tree = shell.IsTreeOpen;
        shell.IsTreeOpen = false;
        W.ShowSheet(sheet); await AuditWaitEditor(true); await a.SnapStable("progetti-foglio", AuditRoot, "vista");
        shell.IsTreeOpen = true; await a.Settle(); await a.SnapStable("progetti-foglio-struttura", AuditRoot, "vista");
        if (first.Array("fogli").OfType<JsonObject>().FirstOrDefault(s => s.S("modulo_id") == "mat_calcestruzzo") is { } material)
        { shell.IsTreeOpen = false; W.ShowSheet(material); await AuditWaitEditor(false); await a.SnapStable("progetti-foglio-materiale", AuditRoot, "vista"); }
        shell.IsTreeOpen = tree;
        foreach (var open in Bench.Workspace.Sheets.ToArray()) Bench.Workspace.CloseSheet(open);
        await a.Settle();

        a.Context = "progetti-revisioni";
        await Overview(first);
        Bench.Project.NewRevision(first, "Revisione di prova"); AuditFixRevisionDates(Bench.Project.Document);
        first = Bench.Project.Find(firstId)!;
        await Overview(first); await a.SnapStable("progetti-revisioni", AuditRoot, "vista");
        var scope = first;
        await a.Trigger("Riepilogo modifiche", () => _ = shell.ShowRevisionSummaryAsync(scope));
        if (shell.Revisions is { } revisions)
        {
            await a.Trigger("Nuova revisione", () => _ = revisions.AddCommand.ExecuteAsync(null));
            if (revisions.Tabs.FirstOrDefault(t => !t.IsCurrent) is { } archived)
            {
                revisions.Switch(archived); W.UpdateLayout(); await a.Settle(); await a.SnapStable("progetti-revisione-archiviata", AuditRoot, "vista");
                if (Bench.Project.Find(firstId) is { } snapshot && snapshot.Array("fogli").OfType<JsonObject>().FirstOrDefault() is { } archivedSheet)
                { Bench.Workspace.OpenSheet(archivedSheet); W.UpdateLayout(); await AuditWaitEditor(false); await a.SnapStable("progetti-revisione-archiviata-foglio", AuditRoot, "vista"); }
                if (shell.Revisions?.Tabs.FirstOrDefault(t => t.IsCurrent) is { } current) shell.Revisions.Switch(current);
                else Bench.Project.ExitPreview();
                W.UpdateLayout(); await a.Settle();
            }
        }
        else a.Note("progetti-revisioni", "barra delle revisioni non mostrata");

        a.Context = "progetti-terreni";
        project = Bench.Project.Document.Array("progetti").OfType<JsonObject>().First();
        var soils = Bench.Project.AddSection(project) ?? throw new InvalidOperationException("Sezione «Terreni» non aggiunta");
        Bench.Project.Rename(soils, "Terreni");
        Bench.Project.AddSheet("geo_palo_verticale", soils); Bench.Project.AddSheet("geo_palo_verticale", soils); W.UpdateLayout(); await a.Settle();
        a.DialogActions["Confronto · Terreni"] = async window =>
        {
            if (Ui.Descendants<Expander>(window).FirstOrDefault(x => x.Header as string == "Collegamento stratigrafie") is not { } expander) return;
            expander.IsExpanded = true; await a.Settle(80);
            if (ContrastAudit.FindButton(window, "Mostra collegamento stratigrafie…") is { } show) await a.Trigger("Collegamento dei sondaggi", () => ContrastAudit.Click(show));
        };
        try { await Overview(soils); await a.SnapStable("progetti-sezione-terreni", AuditRoot, "vista"); await a.Trigger("Confronto terreni", () => _ = shell.ShowCoherenceAsync(soils)); }
        finally { a.DialogActions.Remove("Confronto · Terreni"); }
        AuditReset();
    }

    /// <summary>Complete input for the modules whose defaults stop at "dati da completare", so that the results appear:
    /// cases and examples already in the repository (supporto/test/casi_confronto.json, supporto/esempi), or the smoke recipe.</summary>
    private JsonObject? AuditCompleteData(string module)
    {
        JsonObject? Case(string name) => File.Exists(Path.Combine(a.Root, "supporto", "test", "casi_confronto.json"))
            ? SmokeTestData.LoadCases().OfType<JsonObject>().FirstOrDefault(c => c.S("nome") == name)?["input"]?.DeepClone().AsObject() : null;
        JsonObject? Example(params string[] parts) { var file = Path.Combine([a.Root, "supporto", "esempi", .. parts]); return File.Exists(file) ? DocumentService.Open(file)["dati"]?.DeepClone().AsObject() : null; }
        JsonObject Layered(JsonObject data) { data.Array("stratigrafie")[0]!.AsArray().Add(PaloOrizzontale.Layer()); return data; }
        static JsonObject SpectraData()
        {
            var data = Anthea.Application.Actions.SpectraDocument.Create();
            data["isolation"] = true; data["pendulum"] = true;
            return data;
        }
        return module switch
        {
            "geo_palo_verticale" => Case("palo_storico_0"),
            "geo_micropalo_verticale" => Case("micropalo_IRS_45_Feld"),
            PaloOrizzontale.Module => Layered(PaloOrizzontale.Defaults()),
            MicropaloOrizzontale.Module => Layered(MicropaloOrizzontale.Defaults()),
            RetainingWall.Module => Example("muri-sostegno", "mensola.anthea"),
            BridgeConcept.Module => Example("bridge-design.anthea"),
            Anthea.Application.Actions.SpectraDocument.Module => SpectraData(),
            _ => null
        };
    }

    /// <summary>The module as opened from the catalogue; when its defaults are incomplete, the main view only, then every tab,
    /// popup and window on complete data ("-dati").</summary>
    private async Task AuditModule(string module, string prefix = "modulo-")
    {
        JsonObject? complete = null;
        try { complete = AuditCompleteData(module); } catch (Exception ex) { a.Note(prefix + module, "dati completi non disponibili: " + ex.Message); }
        if (complete is not null)
        {
            Anthea.Application.Modules.ModuleDirectory.ValidateData(module, complete);
            await AuditModuleState(module, null, prefix + module, full: false);
        }
        await AuditModuleState(module, complete, prefix + module + (complete is null ? "" : "-dati"), full: true);
    }

    private async Task AuditModuleState(string module, JsonObject? data, string name, bool full)
    {
        AuditReset();
        if (data is null) W.NewCalculation(module);
        else { var document = Archivio.Documento(module); document["dati"] = data.DeepClone(); W.document = document; W.ShowSheet(document); }
        await AuditWaitEditor(true);
        a.Context = name;
        var content = Center;
        if (!full) { await a.SnapStable(name, AuditRoot, "vista"); await a.SnapPages(content, name, AuditRoot); return; }
        // Spectra uses a phase selector instead of TabControl: inspect all four phases, including isolation and pendulum inputs.
        if (ShellSmokeWindow.SheetOf(W.editor) is Anthea.Presentation.Modules.Spectra.SpectraSheetViewModel spectra)
        {
            if (!W.editor!.HasResults) throw new InvalidOperationException("Contrasto Spettri: risultato del calcolo assente");
            int selected = spectra.Stage;
            for (int stage = 0; stage < 4; stage++)
            {
                spectra.Stage = stage; await a.Settle(60);
                await a.SnapStable(name + "-fase-" + (stage + 1), AuditRoot, "vista");
                await a.SnapPages(content, name + "-fase-" + (stage + 1), AuditRoot);
            }
            spectra.Stage = selected; await a.Settle(60);
        }
        a.TabDefaults.Clear();
        var routes = new Dictionary<string, List<(TabControl Tabs, int Index)>>();
        var routesEnabled = new HashSet<string>();
        bool horizontal = module is PaloOrizzontale.Module or MicropaloOrizzontale.Module;
        await a.ExploreTabs(content, name, 0, 3, [], new ContrastAudit.LeafBudget(70), async (leaf, route) =>
        {
            await AuditWaitEditor(false);
            await a.SnapStable(leaf, AuditRoot, "vista");
            await a.SnapPages(content, leaf, AuditRoot);
            // Route of the first tab where the command is enabled, otherwise where it is first seen.
            foreach (var (label, _) in ContrastAudit.DialogTriggers)
            {
                if (routesEnabled.Contains(label) || ContrastAudit.FindButton(content, label, enabledOnly: false) is not { } seen) continue;
                if (seen.IsEnabled) { routes[label] = route.ToList(); routesEnabled.Add(label); }
                else routes.TryAdd(label, route.ToList());
            }
        }, depth => AuditWaitEditor(horizontal && depth == 0));
        await a.Reselect([]);
        await AuditWaitEditor(false);
        if (Ui.Descendants<ComboBox>(content).FirstOrDefault(c => c.IsVisible && c.IsEnabled) is { } combo) await a.SnapComboBox(combo, name + "--elenco");
        if (ContrastAudit.FirstWithToolTip(content) is { } tipped) await a.SnapToolTip(tipped, name + "--suggerimento");
        if (ContrastAudit.FirstWithContextMenu(content) is { } owner && owner.ContextMenu is { } context)
            await a.Trigger("menu contestuale", () => { context.PlacementTarget = owner; context.IsOpen = true; });
        // «Approfondimenti ▾» of the bar of the sheet: its menu, built when pressed (MainWindow.ShowModuleWikiTopics).
        if (Ui.Descendants<Button>(Window).FirstOrDefault(b => b.Name == "Topics" && b.IsVisible && b.IsEnabled) is { } topics)
            await a.Trigger("Approfondimenti", () => ContrastAudit.Click(topics));
        if (module is "geo_palo_verticale" or "geo_micropalo_verticale" or "str_palo" or BridgeSection.Module && W.editor is { HasResults: true, Busy: false })
            await a.Trigger("Report Word", () => _ = Bench.Shell.Commands.ReportCommand.ExecuteAsync(null));
        foreach (var (label, seconds) in ContrastAudit.DialogTriggers)
        {
            if (!routes.TryGetValue(label, out var route)) continue;
            if (!await a.Reselect(route)) { a.Note(name, "scheda non più disponibile per " + label); continue; }
            await AuditWaitEditor(false);
            if (ContrastAudit.FindButton(content, label, enabledOnly: false) is not { } button) { a.Note(name, "comando non più visibile: " + label); continue; }
            // A command that needs a selected row (Apri sezione in c.a. of the wall): the rows are selected one at a time, as a user would.
            if (!button.IsEnabled) await AuditEnableBySelection(button);
            if (!button.IsEnabled) { a.Note(name, "comando non abilitato: " + label); continue; }
            if (!await a.Trigger(label, () => ContrastAudit.Click(button), seconds)) a.Note(name, "nessuna finestra da " + label);
        }
        await a.Reselect([]);
        a.RecordTriggers(name, routes.Keys);
    }

    private async Task AuditEnableBySelection(Button button)
    {
        foreach (var grid in Ui.Descendants<DataGrid>(Center).Where(g => g.IsVisible && g.Items.Count > 0).ToList())
            for (int i = 0; i < Math.Min(40, grid.Items.Count) && !button.IsEnabled; i++) { grid.SelectedIndex = i; await a.Settle(20); }
    }

    /// <summary>Appearance changed with the view open (Appearance.Set re-themes the open windows): home, a project section, the c.a. section.</summary>
    internal async Task AuditAppearanceSwitch(AppAppearance start, AppAppearance mode)
    {
        async Task Switch(string name, Func<Task> show)
        {
            await a.Step(name, async () =>
            {
                Appearance.Set(start, false); await show(); await a.Settle(80);
                Appearance.Set(mode, false); await a.Settle(80);
                await a.SnapStable(name, AuditRoot, "vista");
            });
        }
        await Switch("cambio-home", () => { W.ShowHome(); return Task.CompletedTask; });
        await Switch("cambio-progetti-sezione", async () =>
        {
            AuditReset();
            if (File.Exists(a.TestArchive)) { W.LoadFile(a.TestArchive); await Overview(Bench.Project.Document.Array("progetti")[0]!["strutture"]![0]!.AsObject()); }
            else W.ShowProjects();
            await a.Settle();
        });
        await Switch("cambio-modulo-str_palo", async () => { AuditReset(); W.NewCalculation("str_palo"); await AuditWaitEditor(true); });
        Appearance.Set(mode, false);
        AuditReset();
    }

    /// <summary>The click of a button as the user gives it (ButtonBase.OnClick: its Click handlers and its command), synchronous.</summary>
    internal static void Press(System.Windows.Controls.Primitives.ButtonBase button) =>
        typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(button, null);
}
