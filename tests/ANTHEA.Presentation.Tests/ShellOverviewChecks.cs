using System.Text.Json.Nodes;
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
/// F5.13 c, after the checkpoint of 22/10 (section 10.1 of f5-13-shell.md, first item): the overview of a section with the content and the texts of
/// MainWindow (ProjectPresentation.BuildSectionOverview, AddModelsOverview): the card «Controlli» with its two statuses and «Confronto e avvisi», which
/// opens the comparison in its dialog (ProjectSharing.ShowCoherence; the coherence badge of the tree too); «Riepilogo modifiche» of the card «Revisioni»
/// in its dialog (ProjectWorkspace.ShowProjectRevisions), whose «Mostra questa revisione» closes it and shows the revision; «Dettaglio avvisi»; the
/// buttons of «Modelli di calcolo».
/// </summary>
internal static class ShellOverviewChecks
{
    internal static int Run() => SingleThreadContext.Run(RunAsync);

    private sealed class NoWindows : IWindowService
    {
        public void Open(TabHostViewModel host) { }
        public void Close(TabHostViewModel host) { }
        public void Activate(TabHostViewModel host) { }
    }

    private static async Task<int> RunAsync(SingleThreadContext ui)
    {
        var expect = new Expect();
        CoherenceViewModel? comparison = null;
        RevisionSummaryViewModel? summary = null;
        bool committedBefore = false;
        SheetSession? edited = null;
        var dialogs = new ScriptedDialogs()
            // 1. The comparison from the badge of the tree with a sheet active: the sheet was confirmed before; «Uniforma a questo» on the second
            //    sheet, then the window is closed by the user.
            .Then(d =>
            {
                comparison = (CoherenceViewModel)d;
                committedBefore = edited is not null && edited.Sheet["dati"].S("classe") == "C35/45";
                var item = comparison.Conflicts.Single().Sheets[1];
                item.AlignCommand.Execute(item);
            })
            // 2. The comparison from «Confronto e avvisi» of the overview, closed without changes.
            .Then(d => comparison = (CoherenceViewModel)d)
            // 3. «Riepilogo modifiche»: «Mostra questa revisione» on the first archived revision.
            .Then(d =>
            {
                summary = (RevisionSummaryViewModel)d;
                summary.ShowCommand.Execute(summary.Entries[^1]);
            })
            // 4. The comparison of an archived revision: read-only, nothing can be aligned.
            .Then(d => comparison = (CoherenceViewModel)d);
        // The confirmation of the edited sheet asks about the linked sheets: «Solo questo foglio», so that the conflict stays for the comparison.
        var bench = new ShellBench(new ScriptedConfirmations().Then(false), windows: dialogs);
        using var shell = new ShellViewModel(bench.Services, bench.Factory, new DocumentFiles(Path.Combine(Path.GetTempPath(), "anthea-esempi-assenti")), new NoWindows(), new SheetFileExports());
        var workspace = shell.Workspace;
        var project = workspace.NewProject()!;
        var root = project.AddProject()!;
        var section = project.AddSection(root)!;
        project.Rename(section, "Pila P1");
        var first = project.AddSheet("mat_calcestruzzo", section)!;
        var second = project.AddSheet("mat_calcestruzzo", section)!;
        string sectionId = section.S("id");

        // The card «Controlli»: «✓  » without conflicts or notices, «!  » with one; both always shown, as ProjectStatus.
        shell.ShowOverview(section);
        var overview = shell.Overview!;
        int notices = ProjectValidation.Warnings(section).Length;
        expect.That(SectionOverviewViewModel.ControlsTitle == "Controlli" && overview.ConflictsStatus == "✓  0 conflitti"
            && overview.NoticesStatus == (notices > 0 ? "!  " : "✓  ") + overview.NoticesText, "Controlli senza conflitti: " + overview.ConflictsStatus + " · " + overview.NoticesStatus);
        expect.That(overview.NoticeLines.SequenceEqual(ProjectValidation.Warnings(section).Select(n => "• " + n)) && SectionOverviewViewModel.NoticesTitle == "Dettaglio avvisi",
            "«Dettaglio avvisi»: una riga «• avviso» per avviso");
        // «Modelli di calcolo»: no model, «+ Importa un modello» outside read-only; it opens the tab of a new model of the section.
        expect.That(overview.ModelButtons.Count == 1 && overview.ModelButtons[0] is { ModelId: null, Label: "+ Importa un modello" } && ReferenceEquals(overview.ModelButtons[0].Container, section)
            && SectionOverviewViewModel.ModelsTitle == "Modelli di calcolo", "«Modelli di calcolo»: «+ Importa un modello»");
        shell.OpenOverviewModelCommand.Execute(overview.ModelButtons[0]);
        expect.That(shell.Center is PageTabViewModel { Kind: ShellTabKind.Model, Content: ModelTabViewModel { ModelId: null } }, "«+ Importa un modello»: la scheda del nuovo modello");
        foreach (var page in shell.Windows.Pages.ToArray()) shell.Windows.Close(page);

        // A conflict on the class of the concrete.
        second["dati"]!["classe"] = "C40/50";
        shell.ShowOverview(section);
        overview = shell.Overview!;
        expect.That(overview.HasConflicts && overview.ConflictsStatus == "!  1 conflitto" && overview.CompareLabel == "Confronto e avvisi", "Controlli con un conflitto: " + overview.ConflictsStatus);

        // The coherence badge of the section in the tree, with the first sheet open and its class changed, not confirmed: the comparison confirms it
        // first (MainWindow.ShowCoherence: Commit), then the dialog; «Uniforma a questo» changes the document and the overview is rebuilt.
        edited = workspace.OpenSheet(first);
        edited.ViewModel.Data["classe"] = "C35/45";
        var node = shell.Tree.Find(project.Find(sectionId)!)!;
        expect.That(node.Badge is { Warning: true } && node.CompareCommand.CanExecute(null) && shell.CompareCommand.CanExecute(node.Node) && !shell.CompareCommand.CanExecute(null)
            && node.Children.Where(c => c.IsSheet).All(c => !c.CompareCommand.CanExecute(null)), "Badge della sezione con il conflitto, confronto abilitato (non per i fogli)");
        node.CompareCommand.Execute(null);
        expect.That(comparison is { Title: "Confronto · Pila P1", Result: true } && committedBefore, "Confronto nel suo dialogo, dopo la conferma della scheda aperta " + string.Join(" | ", bench.Messages.Failures.Select(f => f.Error.Message)));
        expect.That(project.Find(first.S("id"))!["dati"].S("classe") == "C40/50" && project.IsDirty && edited.ViewModel.Data.S("classe") == "C40/50",
            "«Uniforma a questo»: documento cambiato, scheda aperta ricaricata");

        // «Confronto e avvisi» of the overview opens the same comparison; closed without changes it gives false.
        shell.ShowOverview(project.Find(sectionId));
        overview = shell.Overview!;
        expect.That(!overview.HasConflicts && overview.ConflictsStatus == "✓  0 conflitti", "Riepilogo dopo «Uniforma a questo»: " + overview.ConflictsStatus);
        await Expect.Within(shell.CompareCommand.ExecuteAsync(overview.Section));
        expect.That(comparison is { Result: false, HasNoConflicts: true } && ReferenceEquals(comparison.Section, overview.Section) && bench.Windows.Shown.Count == 2,
            "«Confronto e avvisi»: il confronto, chiuso senza modifiche");

        // «Riepilogo modifiche»: shown with archived revisions; «Mostra questa revisione» closes the dialog and shows the revision read-only, with its bar.
        var live = project.Find(sectionId)!;
        project.NewRevision(live, "Prima emissione");
        shell.ShowOverview(project.Find(sectionId));
        overview = shell.Overview!;
        expect.That(overview.HasRevisionSummary && SectionOverviewViewModel.RevisionSummaryLabel == "Riepilogo modifiche" && shell.RevisionSummaryCommand.CanExecute(overview.RevisionScope)
            && !shell.RevisionSummaryCommand.CanExecute(null), "«Riepilogo modifiche» con una revisione archiviata");
        await Expect.Within(shell.RevisionSummaryCommand.ExecuteAsync(overview.RevisionScope));
        expect.That(summary is { Title: "Revisioni · Pila P1", Result: true } && project.IsReadOnly && project.DisplayedRevision == 0, "«Mostra questa revisione»: dialogo chiuso, revisione 0 in sola lettura");
        expect.That(shell.Revisions is { DetailsArchived: true, SelectedNumber: 0 } && shell.Overview is { RevisionTitle: "Revisione 00" } && shell.Center is SectionOverviewViewModel,
            "Barra delle revisioni e riepilogo sulla revisione mostrata");
        // Read-only: «+ Importa un modello» hidden; the comparison opens with nothing to align.
        expect.That(shell.Overview!.ModelButtons.Count == 0, "Sola lettura: nessun «+ Importa un modello»");
        await Expect.Within(shell.CompareCommand.ExecuteAsync(shell.Overview.Section));
        expect.That(comparison is { Result: false } && comparison.Conflicts.Count == 0 && !comparison.CanLink, "Confronto della revisione in sola lettura");
        project.ExitPreview();
        expect.That(bench.Windows.Shown.Count == 4 && bench.Messages.Failures.Count == 0, "Quattro dialoghi, nessun errore: " + string.Join(" | ", bench.Messages.Failures));
        return expect.Count;
    }
}
