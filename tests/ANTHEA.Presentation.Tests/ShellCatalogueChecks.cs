using Anthea.Calculations;
using Anthea.Presentation.Services;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Commands;
using Anthea.Presentation.Shell.Navigation;
using Anthea.Presentation.Shell.Projects;
using Anthea.Presentation.Shell.Tabs;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// F5.13 c, night of 22-23/10 (section 10.1, third item): the catalogues. «Moduli singoli» (MainWindow.ShowModules) labels the button of the module of
/// the last sheet «Riprendi», the others «Apri», those in preparation «Dettagli», and follows the last sheet; a card of «Schede da aggiungere»
/// (ProjectPresentation.BuildProjectCatalog) added with the double click goes into the selected section and opens, as MainWindow.AddSheet.
/// </summary>
internal static class ShellCatalogueChecks
{
    internal static int Run() => SingleThreadContext.Run(_ => Task.FromResult(RunAll()));

    private sealed class NoWindows : IWindowService
    {
        public void Open(TabHostViewModel host) { }
        public void Close(TabHostViewModel host) { }
        public void Activate(TabHostViewModel host) { }
    }

    private static int RunAll()
    {
        var expect = new Expect();
        var bench = new ShellBench(new ScriptedConfirmations().Then(SaveChangesAnswer.Discard));
        using var shell = new ShellViewModel(bench.Services, bench.Factory, new DocumentFiles(Path.Combine(Path.GetTempPath(), "anthea-esempi-assenti")), new NoWindows(), new SheetFileExports());
        var workspace = shell.Workspace;
        var catalogue = shell.Navigation.Modules;
        IEnumerable<ModuleCard> Cards() => catalogue.Areas.SelectMany(a => a.Groups).SelectMany(g => g.Cards);

        // No sheet: «Apri», «Dettagli» for the modules in preparation, as ButtonText.
        expect.That(Cards().All(c => c.Button == catalogue.ButtonText(c)) && Cards().Where(c => c.IsAvailable).All(c => c.Button == "Apri")
            && Cards().Where(c => !c.IsAvailable).All(c => c.Button == "Dettagli"), "Catalogo senza fogli: «Apri» e «Dettagli»");

        // A sheet of the RC section: its card says «Riprendi» and takes the focus back to it; the others still «Apri».
        var concrete = workspace.NewCalculation("str_palo");
        shell.Navigation.Select(ShellPage.Modules);
        var card = Cards().Single(c => c.Id == "str_palo");
        expect.That(card.Button == "Riprendi" && Cards().Where(c => c.IsAvailable && c.Id != "str_palo").All(c => c.Button == "Apri"), "«Riprendi» sulla scheda della sezione c.a.");
        catalogue.OpenCommand.Execute(card);
        expect.That(ReferenceEquals(workspace.Active, concrete) && workspace.Calculations.Count == 1, "«Riprendi»: lo stesso foglio");

        // Another sheet becomes the last one; the labels follow it, also after a change of discipline.
        workspace.NewCalculation("mat_calcestruzzo");
        expect.That(Cards().Single(c => c.Id == "mat_calcestruzzo").Button == "Riprendi" && Cards().Single(c => c.Id == "str_palo").Button == "Apri", "Il «Riprendi» segue l'ultimo foglio");
        catalogue.Discipline = "Materiali";
        expect.That(Cards().Single(c => c.Id == "mat_calcestruzzo").Button == "Riprendi" && catalogue.Areas.All(a => a.Title == "Materiali"), "«Riprendi» con il filtro della disciplina");
        catalogue.Discipline = "Tutti";

        // «Schede da aggiungere»: the double click adds a sheet of the module to the selected section and opens it.
        var project = workspace.NewProject()!;
        var root = project.AddProject()!;
        var section = project.AddSection(root)!;
        shell.Tree.Refresh(section);
        var add = shell.Catalogue.Groups.SelectMany(g => g.Cards).First(c => c.Module == "geo_stratigrafia");
        expect.That(shell.AddCatalogSheetCommand.CanExecute(add) && !shell.AddCatalogSheetCommand.CanExecute(null), "Doppio clic su una scheda del catalogo abilitato");
        shell.AddCatalogSheetCommand.Execute(add);
        var added = project.Find(section.S("id"))!.Array("fogli").OfType<System.Text.Json.Nodes.JsonObject>().SingleOrDefault();
        expect.That(added?.S("modulo_id") == "geo_stratigrafia" && workspace.Active?.Sheet is { } open && ReferenceEquals(open, added) && project.IsDirty,
            "Doppio clic: il foglio nella sezione selezionata, aperto");

        // The overview of another section selects it in the tree (MainWindow.ShowProjectOverview → RefreshTree): the double click goes there.
        var other = project.AddSection(root)!;
        shell.ShowOverview(other);
        expect.That(ReferenceEquals(shell.Tree.Selected?.Node, other) && ReferenceEquals(shell.Tree.SelectedContainer, other), "Riepilogo di una sezione: selezionata nell'albero");
        shell.AddCatalogSheetCommand.Execute(add);
        expect.That(other.Array("fogli").OfType<System.Text.Json.Nodes.JsonObject>().SingleOrDefault()?.S("modulo_id") == "geo_stratigrafia", "Doppio clic dopo il riepilogo: il foglio in quella sezione");

        // Without a section selected: the note of today, nothing added.
        shell.Tree.Selected = null;
        int shown = bench.Messages.Shown.Count;
        shell.AddCatalogSheetCommand.Execute(add);
        expect.That(bench.Messages.Shown.Count == shown + 1 && bench.Messages.Shown[^1].Message == ProjectTreeViewModel.SelectSection
            && project.Find(section.S("id"))!.Array("fogli").Count == 1, "Senza sezione selezionata: il messaggio di oggi");

        // Read-only: no sheet can be added.
        var live = project.Find(section.S("id"))!;
        project.NewRevision(live, "Prima");
        project.SwitchRevision(live.S("id"), 0);
        expect.That(!shell.AddCatalogSheetCommand.CanExecute(add), "Revisione in sola lettura: nessuna scheda da aggiungere");
        project.ExitPreview();
        return expect.Count;
    }
}
