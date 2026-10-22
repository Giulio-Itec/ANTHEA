using Anthea.Application.Modules;
using Anthea.Calculations;
using Anthea.Presentation.Services;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Navigation;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// F5.13 b1: navigation and catalogue of the modules without WPF, with the texts and orders of MainWindow.BuildShell, ShowHome and
/// ShowModules; the pages confirm the active sheet and end the preview of a revision (except «Progetti», SH7); «Riprendi» (SH13).
/// </summary>
internal static class NavigationChecks
{
    internal static int Run() => SingleThreadContext.Run(_ => Task.FromResult(RunAll()));

    private static int RunAll()
    {
        var expect = new Expect();
        Pages(expect);
        Catalogue(expect);
        return expect.Count;
    }

    private static void Pages(Expect expect)
    {
        var bench = new ShellBench();
        var workspace = bench.Workspace;
        using var navigation = new NavigationViewModel(workspace, bench.Messages);
        int count = Archivio.Moduli.Length;
        expect.That(navigation.Items.Select(i => i.Title).SequenceEqual(["Home", "Progetti", "Modelli", "Moduli singoli", "Esempi", "Wiki"])
            && navigation.Page == ShellPage.Home && navigation.Items.Single(i => i.IsSelected).Page == ShellPage.Home, "Barra di navigazione");
        expect.That(navigation.Footer == $"Moduli disponibili: {count} di {count + 1}" && NavigationViewModel.Subtitle == "Strumenti di calcolo", "Piè della barra");
        expect.That(navigation.HomeCards.Select(c => (c.Title, c.Button, c.Page)).SequenceEqual([("Moduli singoli", "Apri i moduli", ShellPage.Modules),
            ("Progetti", "Gestisci i progetti", ShellPage.Projects), ("Wiki", "Esplora la Wiki", ShellPage.Wiki)])
            && navigation.HomeCards[0].Badge == $"{count} moduli disponibili" && navigation.HomeCards[2].Badge == "Manuale di ingegneria · Guide Anthea", "Schede della Home");
        expect.That(navigation.ResumeText is null && !navigation.ResumeCommand.CanExecute(null), "Riprendi senza fogli");

        // A sheet takes the focus: no page; a page confirms it and takes the focus back; «Riprendi» returns to it.
        var sheet = workspace.NewCalculation("str_palo");
        expect.That(navigation.Page is null && navigation.Items.All(i => !i.IsSelected) && ReferenceEquals(navigation.Last, sheet), "Foglio attivo");
        ShellBench.Probe(sheet).Note = "bozza";
        navigation.Items[3].SelectCommand.Execute(null);
        expect.That(navigation.Page == ShellPage.Modules && workspace.Active is null && sheet.Sheet["dati"].S("nota") == "bozza"
            && navigation.ResumeText == "Riprendi · " + ShellTexts.ModuleName("str_palo") && navigation.ResumeCommand.CanExecute(null), "Pagina dei moduli");
        navigation.ResumeCommand.Execute(null);
        expect.That(ReferenceEquals(workspace.Active, sheet) && navigation.Page is null, "Riprendi");
        navigation.OpenCardCommand.Execute(navigation.HomeCards[2]);
        expect.That(navigation.Page == ShellPage.Wiki && workspace.Active is null, "Scheda della Home");

        // «Progetti» opens a new archive without a project, and keeps the preview of a revision; the other pages end it (SH7).
        navigation.Select(ShellPage.Projects);
        var project = workspace.Project!;
        expect.That(project is not null && navigation.Page == ShellPage.Projects && project.Projects.Count() == 0, "Progetti senza progetto");
        var section = project!.AddSection(project.AddProject()!)!;
        project.NewRevision(section, "Prima");
        project.SwitchRevision(section.S("id"), 0);
        navigation.Select(ShellPage.Projects);
        expect.That(project.IsReadOnly && ReferenceEquals(workspace.Project, project), "Progetti termina la consultazione");
        navigation.Select(ShellPage.Models);
        expect.That(!project.IsReadOnly && navigation.Page == ShellPage.Models, "Modelli non termina la consultazione");

        // The last sheet closed: «Riprendi» moves to the last open one, or disappears.
        var other = workspace.NewCalculation("mat_calcestruzzo");
        bench.Confirmations.Then(SaveChangesAnswer.Discard);
        ShellBench.Probe(other).Label = "x";
        workspace.CloseSheet(other);
        expect.That(ReferenceEquals(navigation.Last, sheet), "Riprendi dopo la chiusura dell'ultimo foglio");
        bench.Confirmations.Then(SaveChangesAnswer.Discard);
        workspace.CloseSheet(sheet);
        expect.That(navigation.Last is null && navigation.ResumeText is null, "Riprendi senza fogli aperti");
    }

    private static void Catalogue(Expect expect)
    {
        var bench = new ShellBench();
        var workspace = bench.Workspace;
        using var navigation = new NavigationViewModel(workspace, bench.Messages);
        var catalogue = navigation.Modules;
        expect.That(ModuleCatalogViewModel.Title == "Moduli singoli" && catalogue.Disciplines.SequenceEqual(["Tutti", "Geotecnica", "Strutture", "Materiali", "Azioni"])
            && catalogue.Discipline == "Tutti", "Discipline");
        var expected = new[] { "Geotecnica", "Strutture", "Materiali", "Azioni" };
        expect.That(catalogue.Areas.Select(a => a.Title).SequenceEqual(expected), "Aree: " + string.Join(",", catalogue.Areas.Select(a => a.Title)));
        expect.That(catalogue.Areas.SelectMany(a => a.Groups).SelectMany(g => g.Cards).Select(c => c.Id).Order().SequenceEqual(ModuleDirectory.All.Select(m => m.Id).Order()), "Tutti i moduli nel catalogo");
        var card = catalogue.Areas.Single(a => a.Title == "Strutture").Groups.SelectMany(g => g.Cards).Single(c => c.Id == "str_palo");
        var info = ModuleDirectory.Get("str_palo");
        expect.That(card.Status == "Disponibile" && card.Title == info.Description && card.Subtitle == "Strutture · " + info.Element && card.Owner == "Referente: GPC"
            && card.Icon == "str_palo" && card.IsAvailable, "Scheda del modulo");
        var group = catalogue.Areas.Single(a => a.Title == "Geotecnica").Groups.First();
        expect.That(group.Header == group.Header.ToUpperInvariant() && group.Cards.All(c => c.Owner == "Referente: GSC"), "Gruppi della geotecnica");
        catalogue.SelectDisciplineCommand.Execute("Strutture");
        expect.That(catalogue.Discipline == "Strutture" && catalogue.Areas.Select(a => a.Title).SequenceEqual(["Strutture"]), "Filtro della disciplina");
        expect.Throws<ArgumentException>(() => catalogue.Discipline = "Idraulica", "Disciplina sconosciuta");

        // «Apri» a new single file; «Riprendi» the last sheet of the same module.
        expect.That(catalogue.ButtonText(card) == "Apri", "Pulsante prima dell'apertura");
        catalogue.OpenCommand.Execute(card);
        var opened = workspace.Active!;
        expect.That(opened.Module == "str_palo" && workspace.Calculations.Count == 1 && catalogue.ButtonText(card) == "Riprendi", "Apri del catalogo");
        navigation.Select(ShellPage.Modules);
        catalogue.OpenCommand.Execute(card);
        expect.That(ReferenceEquals(workspace.Active, opened) && workspace.Calculations.Count == 1, "Riprendi del catalogo");
        var waiting = new ModuleCard("", "In preparazione", "Modulo futuro", "Strutture · Micropalo", "Referente: GPC", "str_micropalo");
        expect.That(catalogue.ButtonText(waiting) == "Dettagli" && !waiting.IsAvailable, "Modulo in preparazione");
        catalogue.OpenCommand.Execute(waiting);
        expect.That(bench.Messages.Shown.Single() == ("", ModuleCatalogViewModel.InPreparation, false) && workspace.Calculations.Count == 1, "Nota del modulo in preparazione");
    }
}
