using System.Text.Json.Nodes;
using ANTHEA.ModelWorkspace;
using Anthea.Application.Documents;
using Anthea.Application.Modules;
using Anthea.Calculations;
using Anthea.Presentation.Services;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Commands;
using Anthea.Presentation.Shell.Navigation;
using Anthea.Presentation.Shell.Projects;
using Anthea.Presentation.Shell.Tabs;
using Anthea.Wiki;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// F5.13 c: the pieces of the shell that do not depend on the layout, without WPF: the command line (App.OnStartup), the menu «Aspetto», the
/// examples of the Wiki that open a module and the Wiki tab (MainWindow.WikiExamples, ShowWiki, OpenWikiModule), the centre of the main window,
/// the bar above the sheet with the menu «Approfondimenti», the page «Modelli», the model tabs and the notice of the linked model
/// (MainWindow.ShowModelsHub, ShowContainerModel, RefreshModelReference). The expected texts and conditions come from the historic code.
/// </summary>
internal static class ShellPartCChecks
{
    internal static int Run(string output) => SingleThreadContext.Run(_ => Task.FromResult(RunAll(output)));

    private sealed class NoWindows : IWindowService
    {
        public void Open(TabHostViewModel host) { }
        public void Close(TabHostViewModel host) { }
        public void Activate(TabHostViewModel host) { }
    }

    private sealed class RecordingAppearance : IAppearanceService
    {
        internal List<AppearanceChoice> Applied { get; } = [];
        internal Exception? Failure { get; set; }
        public AppearanceChoice Current { get; set; } = AppearanceChoice.Dark;
        public void Apply(AppearanceChoice choice)
        {
            if (Failure is not null) throw Failure;
            Applied.Add(choice);
            Current = choice;
        }
    }

    private static int RunAll(string output)
    {
        var expect = new Expect();
        string folder = Path.Combine(output, "shell-parte-c");
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
        Directory.CreateDirectory(folder);
        StartupArguments(expect);
        Appearance(expect);
        Examples(expect);
        Menu(expect);
        Shell(expect, folder);
        Models(expect);
        return expect.Count;
    }

    /// <summary>App.OnStartup: «--wiki» with exactly one argument, otherwise the first argument without «--» opens, «--models» anywhere after it.</summary>
    private static void StartupArguments(Expect expect)
    {
        static StartupRequest P(params string[] args) => StartupRequest.Parse(args);
        expect.That(P() == StartupRequest.None && P("--models") == StartupRequest.None && P("--wiki") == StartupRequest.None, "Nessuna richiesta");
        expect.That(P("--wiki", "pali#portata") == new StartupRequest(StartupAction.Wiki, "pali#portata", false), "Wiki su un articolo");
        expect.That(P("--wiki", "--altro") == new StartupRequest(StartupAction.Wiki, "--altro", false), "Wiki: il secondo argomento così com'è");
        expect.That(P("--wiki", "a", "b") == new StartupRequest(StartupAction.OpenFile, "a", false), "Wiki con tre argomenti: il primo percorso si apre, come oggi");
        expect.That(P(@"C:\lavori\palo.programma") == new StartupRequest(StartupAction.OpenFile, @"C:\lavori\palo.programma", false), "Percorso");
        expect.That(P("--models", "progetto.programma") == new StartupRequest(StartupAction.OpenFile, "progetto.programma", true)
            && P("progetto.programma", "--models") == P("--models", "progetto.programma"), "Percorso e pagina Modelli, in qualsiasi ordine");
        expect.That(P("--altro", "uno", "due") == new StartupRequest(StartupAction.OpenFile, "uno", false), "Primo argomento senza trattini");
        expect.Throws<ArgumentNullException>(() => StartupRequest.Parse(null!), "Argomenti assenti");
    }

    /// <summary>MainWindow.BuildMenu, menu «Aspetto».</summary>
    private static void Appearance(Expect expect)
    {
        var service = new RecordingAppearance();
        var messages = new ScriptedMessages();
        using var menu = new AppearanceMenuViewModel(service, messages);
        expect.That(AppearanceMenuViewModel.Title == "_Aspetto" && menu.Items.Select(i => i.Title).SequenceEqual(["Chiaro", "Scuro", "Molto scuro"])
            && menu.Items.Select(i => i.IsChecked).SequenceEqual([false, true, false]), "Voci e segno dell'aspetto corrente");
        menu.Items[2].SelectCommand.Execute(null);
        expect.That(service.Applied.SequenceEqual([AppearanceChoice.VeryDark]) && menu.Items.Select(i => i.IsChecked).SequenceEqual([false, false, true]), "Scelta di «Molto scuro»");
        service.Current = AppearanceChoice.Light;
        menu.OpenedCommand.Execute(null);
        expect.That(menu.Items.Select(i => i.IsChecked).SequenceEqual([true, false, false]), "All'apertura il segno segue l'aspetto corrente");
        service.Failure = new InvalidOperationException("tema non disponibile");
        menu.Items[1].SelectCommand.Execute(null);
        expect.That(messages.Failures.Single().Error.Message == "tema non disponibile" && menu.Items.Select(i => i.IsChecked).SequenceEqual([true, false, false]),
            "Errore dell'aspetto mostrato, segni invariati");
    }

    /// <summary>MainWindow.WikiExamples: identifiers, module, merge of the overrides with the combination families replaced as a whole.</summary>
    private static void Examples(Expect expect)
    {
        var definition = WikiExamples.Definition("beam-ca");
        expect.That(definition.S("moduleId") == "str_palo", "Definizione dell'esempio");
        var data = WikiExamples.Create("str_palo", "beam-ca");
        var overrides = definition["overrides"]!.AsObject();
        expect.That(JsonNode.DeepEquals(data["combinazioni"], overrides["combinazioni"]), "Famiglie di combinazioni sostituite per intero");
        expect.That(data["input"]!["height_mm"]!.ToString() == "800" && data["input"]!["shape"]!.ToString() == "Rettangolare", "Valori dell'esempio sui default");
        var defaults = ModuleDirectory.CreateData("str_palo");
        expect.That(defaults["input"]!.AsObject().Where(p => !overrides["input"]!.AsObject().ContainsKey(p.Key)).All(p => JsonNode.DeepEquals(p.Value, data["input"]![p.Key])),
            "Gli altri valori restano quelli di default");
        expect.That(WikiCatalog.Articles.Where(a => a.Example is not null).All(a => WikiExamples.Create(WikiExamples.Definition(a.Example!).S("moduleId"), a.Example!) is not null),
            "Ogni esempio della Wiki si applica al suo modulo");
        string? Error(Action action) { try { action(); return null; } catch (ArgumentException e) { return e.Message; } }
        expect.That(Error(() => WikiExamples.Definition("Beam CA")) == WikiExamples.InvalidExample && Error(() => WikiExamples.Create("geo_palo_verticale", "beam-ca")) == WikiExamples.OtherModule,
            "Identificativo non valido, modulo diverso");
    }

    /// <summary>MainWindow.ShowModuleWikiTopics: topics of the fields, a separator, the articles with their headings.</summary>
    private static void Menu(Expect expect)
    {
        var items = WikiMenu.For("str_palo");
        int separator = items.ToList().FindIndex(i => i.IsSeparator);
        expect.That(items.Count > 0 && separator > 0 && items.Take(separator).All(i => i.Uri is not null && i.Children.Count == 0)
            && items.Take(separator).Select(i => i.Uri).Distinct().Count() == separator, "Argomenti dei campi, uno per collegamento, poi il separatore");
        var articles = items.Skip(separator + 1).ToArray();
        expect.That(articles.Length == WikiMenu.ArticlesFor("str_palo").Length && articles.All(a => (a.Title.StartsWith("Guida · ", StringComparison.Ordinal) || a.Title.StartsWith("Teoria · ", StringComparison.Ordinal))
            && a.Uri is null && a.Children[0].Title == "Apri l'articolo" && a.Children.Skip(1).All(c => c.Uri!.Contains('#'))), "Articoli con «Apri l'articolo» e i titoli delle sezioni");
        expect.That(WikiMenu.ArticlesFor("str_palo").First().Type == "guide", "Prima le guide");
    }

    /// <summary>The command line, the Wiki tab, the modules asked by the Wiki, the centre, the bar of the sheet.</summary>
    private static void Shell(Expect expect, string folder)
    {
        var bench = new ShellBench(new ScriptedConfirmations().Then(SaveChangesAnswer.Discard).Then(SaveChangesAnswer.Discard));
        var appearance = new RecordingAppearance();
        using var shell = new ShellViewModel(bench.Services, bench.Factory, new DocumentFiles(Path.Combine(folder, "Esempi")), new NoWindows(), new SheetFileExports(),
            new ShellOptions(appearance));
        var workspace = shell.Workspace;
        expect.That(ReferenceEquals(shell.Center, shell.Navigation) && shell.Appearance is not null && !shell.SheetBar.IsVisible && !shell.ModelReference.IsVisible && !shell.HasProject,
            "All'avvio: la Home al centro, nessuna barra del foglio");
        shell.Start(StartupRequest.None);
        expect.That(workspace.Sheets.Count == 0 && ReferenceEquals(shell.Center, shell.Navigation), "Nessuna richiesta all'avvio");

        // --wiki: the Wiki tab on the article, in the centre; the page of the navigation leaves the screen.
        shell.Start(StartupRequest.Parse(["--wiki", "pali"]));
        var wikiTab = shell.Windows.Pages.Single();
        var wiki = (WikiTabViewModel)wikiTab.Content!;
        expect.That(wikiTab.Kind == ShellTabKind.Wiki && wiki.Uri == "pali" && ReferenceEquals(shell.Center, wikiTab) && shell.Navigation.Page is null, "Avvio sulla Wiki");
        string? asked = null;
        wiki.NavigationRequested += (_, uri) => asked = uri;
        shell.ShowWiki("muri");
        expect.That(shell.Windows.Pages.Count() == 1 && asked == "muri" && wiki.Uri == "muri", "Wiki già aperta: stessa scheda, nuovo articolo");

        // A module asked by the Wiki: «Apri» of the catalogue; the same module again resumes its sheet.
        wiki.OpenModule("str_palo", null);
        var first = workspace.Active!;
        expect.That(first.Module == "str_palo" && workspace.Sheets.Count == 1 && ReferenceEquals(shell.Center, shell.Windows.TabOf(first)), "Modulo dalla Wiki in una scheda nuova");
        shell.Navigation.Select(ShellPage.Wiki);
        expect.That(ReferenceEquals(shell.Center, wikiTab) && workspace.Active is null, "Pagina Wiki: la sua scheda");
        wiki.OpenModule("str_palo", null);
        expect.That(ReferenceEquals(workspace.Active, first) && workspace.Sheets.Count == 1, "Stesso modulo: si riprende il suo foglio");

        // An example of the Wiki: a new unsaved single file with the data of the example.
        wiki.OpenModule("str_palo", "beam-ca");
        var example = workspace.Active!;
        expect.That(!ReferenceEquals(example, first) && example.Owner.IsDirty && example.Owner.Path is null
            && JsonNode.DeepEquals(example.Sheet["dati"], WikiExamples.Create("str_palo", "beam-ca")), "Esempio della Wiki in una scheda nuova non salvata");
        expect.Throws<ArgumentException>(() => shell.OpenWikiModule("geo_palo_verticale", "beam-ca"), "Esempio di un altro modulo rifiutato");

        // «Torna al lavoro»: the last sheet; without sheets the home page.
        shell.Navigation.Select(ShellPage.Wiki);
        wiki.ReturnToWork();
        expect.That(ReferenceEquals(workspace.Active, example) && ReferenceEquals(shell.Center, shell.Windows.TabOf(example)), "Ritorno al lavoro sull'ultimo foglio");
        shell.Windows.Close(shell.Windows.TabOf(example)!);
        shell.Windows.Close(shell.Windows.TabOf(first)!);
        shell.Navigation.Select(ShellPage.Wiki);
        wiki.ReturnToWork();
        expect.That(shell.Navigation.Page == ShellPage.Home && ReferenceEquals(shell.Center, shell.Navigation), "Ritorno al lavoro senza fogli: la Home");
        shell.Windows.Close(wikiTab);
        expect.That(wiki.IsDisposed && !shell.Windows.Pages.Any(), "Chiusa la scheda della Wiki, il suo ViewModel è rilasciato");

        // The centre follows the page and the tabs.
        shell.Navigation.Select(ShellPage.Modules);
        expect.That(ReferenceEquals(shell.Center, shell.Navigation.Modules), "Moduli singoli al centro");
        shell.Navigation.Select(ShellPage.Models);
        expect.That(ReferenceEquals(shell.Center, shell.Models) && !shell.Models.HasProject && shell.Models.Cards.Count == 0, "Modelli senza progetto");
        shell.Navigation.Select(ShellPage.Examples);
        expect.That(ReferenceEquals(shell.Center, shell.Navigation) && shell.Examples is null, "Esempi senza catalogo: la Home");

        // The command line: a file opens; a file that does not exist gives the message, without the page «Modelli».
        string single = Path.Combine(folder, "palo.programma");
        DocumentService.Save(single, Archivio.Documento("geo_palo_verticale"));
        shell.Start(StartupRequest.Parse([single]));
        var opened = workspace.Active!;
        expect.That(opened.Module == "geo_palo_verticale" && opened.Owner.IsFile(single) && ReferenceEquals(shell.Center, shell.Windows.TabOf(opened)), "Avvio con un file");
        int failures = bench.Messages.Failures.Count;
        shell.Start(StartupRequest.Parse([Path.Combine(folder, "assente.programma"), "--models"]));
        expect.That(bench.Messages.Failures.Count == failures + 1 && shell.Navigation.Page != ShellPage.Models, "File assente: messaggio, nessuna pagina Modelli");

        // The bar of a single file: back to ANTHEA, no confirmation; the guide of the module when it has one.
        workspace.Activate(opened);
        expect.That(shell.SheetBar.IsVisible && shell.SheetBar.BackLabel == SheetBarViewModel.BackToHome && !shell.SheetBar.CanConfirm
            && shell.SheetBar.Heading == opened.Heading && shell.SheetBar.Module == "geo_palo_verticale"
            && shell.SheetBar.GuideId == WikiCatalog.ForModule("geo_palo_verticale")?.Id, "Barra di un file singolo");
        shell.SheetBar.BackCommand.Execute(null);
        expect.That(shell.Navigation.Page == ShellPage.Home && !shell.SheetBar.IsVisible && ReferenceEquals(shell.Center, shell.Navigation), "Torna ad ANTHEA");

        // A project archive on the command line with «--models»: the archive opens, then the page «Modelli».
        string archive = Path.Combine(folder, "progetto.programma");
        var document = ProjectDocuments.CreateArchive();
        var root = ProjectDocuments.AddProject(document, "Viadotto");
        var section = ProjectDocuments.AddSection(root, "Spalla A");
        var sheet = ProjectDocuments.AddSheet(section, "str_palo");
        DocumentService.Save(archive, document);
        shell.Start(StartupRequest.Parse(["--models", archive]));
        var project = workspace.Project!;
        expect.That(project.IsFile(archive) && shell.HasProject && shell.Navigation.Page == ShellPage.Models && ReferenceEquals(shell.Center, shell.Models)
            && shell.Models.Cards.Select(c => (c.Path, c.Summary)).SequenceEqual([("Viadotto", "0 modelli · 0 fogli"), ("Viadotto / Spalla A", "0 modelli · 1 fogli")]),
            "Avvio con un archivio e --models: pagina Modelli con le sezioni");

        // The bar of a sheet of the project: back to the project, «Conferma modifiche» confirms the sheet.
        var tab = workspace.OpenSheet(project.Find(sheet.S("id"))!);
        expect.That(shell.SheetBar.BackLabel == SheetBarViewModel.BackToProject && shell.SheetBar.CanConfirm && shell.SheetBar.ConfirmCommand.CanExecute(null), "Barra di un foglio del progetto");
        ShellBench.Probe(tab).Note = "da confermare";
        shell.SheetBar.ConfirmCommand.Execute(null);
        expect.That(tab.Sheet["dati"].S("nota") == "da confermare" && ReferenceEquals(workspace.Active, tab), "«Conferma modifiche»");
        shell.SheetBar.BackCommand.Execute(null);
        expect.That(shell.Navigation.Page == ShellPage.Projects && ReferenceEquals(shell.Overview?.Section, tab.Sheet.Parent?.Parent) && ReferenceEquals(shell.Center, shell.Overview)
            && workspace.Active is null, "Torna al progetto: il riepilogo della sezione del foglio");
        shell.SheetBar.OpenTopicCommand.Execute("sezione-in-calcestruzzo-armato");
        expect.That(shell.Windows.Pages.Single().Content is WikiTabViewModel { Uri: "sezione-in-calcestruzzo-armato" }, "Approfondimenti: la Wiki sull'argomento");
    }

    /// <summary>MainWindow.ShowModelsHub, ShowContainerModel, RefreshModelReference.</summary>
    private static void Models(Expect expect)
    {
        var bench = new ShellBench(dialogs: new ScriptedFileDialogs().Then(null));
        using var shell = new ShellViewModel(bench.Services, bench.Factory, new DocumentFiles(Path.Combine(Path.GetTempPath(), "anthea-esempi-assenti")), new NoWindows(), new SheetFileExports());
        var workspace = shell.Workspace;
        shell.Models.OpenProjectCommand.Execute(null);
        expect.That(bench.Dialogs.Requests.Single() is OpenFileRequest && workspace.Project is null && bench.Messages.Failures.Count == 0, "«Apri progetto…»: il dialogo di «Apri…»");
        shell.Models.NewProjectCommand.Execute(null);
        var project = workspace.Project!;
        expect.That(shell.Navigation.Page == ShellPage.Projects && shell.Models.HasProject, "«Nuovo archivio progetti»: la pagina Progetti");
        var root = shell.Tree.AddProject()!;
        var section = project.AddSection(root)!;
        var sheet = project.AddSheet("str_palo", section)!;
        var card = shell.Models.Cards.Single(c => ReferenceEquals(c.Container, section));
        expect.That(card.Models.Count == 0 && card.NewModel.Id is null && card.NewModel.Name == ModelsPageViewModel.NewModelLabel, "Scheda della sezione con «+ Modello»");

        // «+ Modello»: the tab of a new model, the tree on the section; renaming before the import is refused.
        shell.Models.OpenModelCommand.Execute(card.NewModel);
        var tab = shell.Windows.Pages.Single();
        var model = (ModelTabViewModel)tab.Content!;
        expect.That(tab.Kind == ShellTabKind.Model && tab.Title == ModelTabViewModel.NewModelTitle && model.Snapshot is null && model.ModelId is null
            && model.PathText == "Progetto 1 / " + section.S("nome") && model.Targets.Single().Id == sheet.S("id") && model.Targets.Single().Status == "Non collegato"
            && ReferenceEquals(shell.Center, tab) && ReferenceEquals(shell.Tree.Selected?.Node, section), "Scheda di un modello nuovo");
        model.RenameCommand.Execute(null);
        expect.That(bench.Messages.Failures.Last().Error.Message == ModelTabViewModel.ImportFirst, "Rinomina prima dell'importazione rifiutata");
        shell.Models.OpenModelCommand.Execute(card.NewModel);
        expect.That(shell.Windows.Pages.Count() == 1, "Stesso modello nuovo: stessa scheda");

        // The import: the model of the section, the title of the tab, the document modified, the card and the tree updated.
        bool dirty = project.IsDirty;
        model.Imported(new ModelSnapshot { Name = "Telaio di prova", Nodes = [new ModelNode(1, 0, 0, 0)] });
        expect.That(model.ModelId is not null && tab.Title == "Telaio di prova" && project.IsDirty && ProjectModelStore.Models(section).Single().S("nome") == "Telaio di prova"
            && shell.Models.Cards.Single(c => ReferenceEquals(c.Container, section)).Summary == "1 modelli · 1 fogli", "Importazione del modello");
        model.RenameText = "  Telaio  ";
        model.RenameCommand.Execute(null);
        expect.That(tab.Title == "Telaio" && ProjectModelStore.Models(section).Single().S("nome") == "Telaio", "Rinomina del modello");

        // The tree asks the model: its tab is selected, no other opens.
        shell.Navigation.Select(ShellPage.Home);
        shell.ShowModel(section, model.ModelId);
        expect.That(shell.Windows.Pages.Count() == 1 && ReferenceEquals(shell.Center, tab), "Modello già aperto: la sua scheda");

        // The notice of the linked model above the sheet: state, element, case, component, «Apri modello».
        var entry = ProjectModelStore.Models(section).Single();
        sheet[ProjectModelStore.Binding] = new JsonObject { ["modello_id"] = model.ModelId, ["revisione"] = entry["revisione"]!.DeepClone(), ["elemento"] = 7, ["caso"] = "SLU 1", ["componente"] = "Mxx" };
        workspace.OpenSheet(sheet);
        expect.That(shell.ModelReference.IsVisible && shell.ModelReference.CanOpen && shell.ModelReference.Text == "Riferimento aggiornato · elemento 7 · SLU 1 · Mxx", "Avviso del modello collegato");
        shell.ModelReference.OpenCommand.Execute(null);
        expect.That(ReferenceEquals(shell.Center, tab) && workspace.Active is null, "«Apri modello»");
        ((JsonObject)sheet[ProjectModelStore.Binding]!)["modello_id"] = "altro";
        workspace.OpenSheet(sheet);
        expect.That(shell.ModelReference.Text.StartsWith(ModelReferenceViewModel.Missing, StringComparison.Ordinal) && !shell.ModelReference.CanOpen, "Modello non disponibile nella sezione");

        // The bar of the tab: back to the models, the owner section, save; undo removes the model and closes the tab.
        model.BackCommand.Execute(null);
        expect.That(shell.Navigation.Page == ShellPage.Models && ReferenceEquals(shell.Center, shell.Models), "«← Modelli»");
        shell.Windows.Select(tab);
        model.OwnerCommand.Execute(null);
        expect.That(shell.Navigation.Page == ShellPage.Projects && ReferenceEquals(shell.Overview?.Section, section), "«Progetto di appartenenza»");
        while (project.CanUndo && ProjectModelStore.Models(project.Find(section.S("id")) ?? new JsonObject()).Any()) project.Restore(false);
        expect.That(!shell.Windows.Pages.Any() && model.IsDisposed, "Annulla dell'importazione: la scheda del modello si chiude");
        expect.That(!dirty || project.IsDirty, "Documento ancora da salvare");
    }
}
