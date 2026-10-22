using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Projects;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// F5.13 b3: the revision bar, the dialog of the new revision and the summary of the changes without WPF, with the labels, the enabled
/// states, the tooltips and the details of MainWindow.UpdateProjectRevisionBar, CreateProjectRevision and ShowProjectRevisions.
/// </summary>
internal static class RevisionBarChecks
{
    internal static int Run() => SingleThreadContext.Run(RunAsync);

    private static async Task<int> RunAsync(SingleThreadContext ui)
    {
        var expect = new Expect();
        var dialogs = new ScriptedDialogs()
            .Then(d => { var dialog = (NewRevisionDialogViewModel)d; dialog.Note = "  Prima emissione  "; dialog.CreateCommand.Execute(null); })
            .Then(d => ((NewRevisionDialogViewModel)d).CancelCommand.Execute(null));
        var bench = new ShellBench(windows: dialogs);
        var project = bench.Workspace.NewProject()!;
        var root = project.AddProject()!;
        var section = project.AddSection(root)!;
        var child = project.AddSection(section)!;
        var sheet = project.AddSheet("str_palo", child)!;
        using var bar = new RevisionBarViewModel(project, bench.Services);
        int changes = 0;
        bar.RevisionsChanged += (_, _) => changes++;

        bar.Show(null);
        expect.That(!bar.IsVisible && bar.Tabs.Count == 0 && !bar.CanAdd, "Barra senza sezione");
        bar.Show(section);
        expect.That(bar.IsVisible && bar.Caption == "Revisioni · Sezione 1" && bar.ScopeName == "Sezione 1" && bar.Tabs.Single() == new RevisionTab(0, true, "Rev. 0 · attuale", true, "Revisione 0 attuale")
            && !bar.CanDelete && bar.DeleteToolTip == "L’unica versione rimasta non può essere eliminata." && bar.CanAdd
            && bar.AddToolTip == "Conserva la versione attuale e continua nella nuova revisione." && bar.Details == "Versione attuale · modificabile" && !bar.DetailsArchived, "Barra iniziale");
        expect.That(RevisionBarViewModel.AddLabel == "+ Nuova revisione" && RevisionBarViewModel.DeleteLabel == "Elimina revisione", "Etichette della barra");

        // «+ Nuova revisione»: the note of the dialog, trimmed; «Annulla» changes nothing.
        await Expect.Within(bar.AddCommand.ExecuteAsync(null));
        var dialog = (NewRevisionDialogViewModel)dialogs.Shown.Single();
        expect.That(dialog.Title == "Nuova revisione · Sezione 1" && NewRevisionDialogViewModel.Text == "La versione attuale verrà archiviata. Continuerai a lavorare nella nuova revisione."
            && NewRevisionDialogViewModel.NoteLabel == "Nota della nuova revisione (facoltativa)", "Dialogo della nuova revisione");
        var current = section["revisione"]!.AsObject();
        expect.That(ShellTexts.RevisionNumber(current) == 1 && current.S("nota") == "Prima emissione" && changes == 1, "Nuova revisione");
        expect.That(bar.Tabs.SequenceEqual([new RevisionTab(0, false, "Rev. 0", false, "Revisione 0 archiviata"), new RevisionTab(1, true, "Rev. 1 · attuale", true, "Revisione 1 attuale")])
            && bar.CanDelete && bar.DeleteToolTip == "Elimina Rev. 1 e rende modificabile la precedente. Puoi annullare l’operazione."
            && bar.Details == "Versione attuale · modificabile  ·  " + ShellTexts.RevisionDate(current) + "  ·  Prima emissione", "Barra dopo la nuova revisione: " + bar.Details);
        var before = section.DeepClone();
        await Expect.Within(bar.AddCommand.ExecuteAsync(null));
        expect.That(JsonNode.DeepEquals(before, section) && changes == 1 && dialogs.Shown.Count == 2, "Nuova revisione annullata");

        // The bar of a sheet: the nearest revised section above it.
        bar.Show(sheet);
        expect.That(ReferenceEquals(bar.Scope, section), "Ambito della barra di un foglio");

        // Rev. 0: read-only, tooltips of the archived revision; the bar stays on the section switched to.
        bar.SwitchCommand.Execute(bar.Tabs[0]);
        expect.That(project.IsReadOnly && project.DisplayedRevision == 0 && changes == 2 && ReferenceEquals(bar.Scope, section)
            && bar.Tabs[0].IsSelected && !bar.Tabs[1].IsSelected && !bar.CanAdd && bar.AddToolTip == "Torna alla revisione attuale per crearne una nuova."
            && bar.DeleteToolTip == "Elimina Rev. 0 dallo storico. La versione attuale resta invariata. Puoi annullare l’operazione."
            && bar.Details.StartsWith("Versione archiviata · sola lettura  ·  ", StringComparison.Ordinal) && bar.DetailsArchived && bar.SelectedNumber == 0, "Revisione archiviata");
        await Expect.Within(bar.AddCommand.ExecuteAsync(null));
        expect.That(dialogs.Shown.Count == 2, "Nuova revisione dalla revisione archiviata");

        // «Elimina revisione» on Rev. 0: the preview ends, only the current revision is left; the deletion can be undone.
        bar.DeleteCommand.Execute(null);
        var live = project.Find(section.S("id"))!;
        expect.That(!project.IsReadOnly && live.Array("revisioni").Count == 0 && bar.Tabs.Single().IsCurrent && !bar.CanDelete && changes == 3, "Eliminazione della revisione");
        expect.That(project.Restore(false) && project.Find(section.S("id"))!.Array("revisioni").Count == 1, "Annulla dell'eliminazione");

        // «Riepilogo modifiche»: the current revision, then the archived ones from the newest; «Mostra questa revisione».
        var restored = project.Find(section.S("id"))!;
        project.NewRevision(restored, "Seconda");
        using var summary = new RevisionSummaryViewModel(project, restored, bench.Messages);
        var currentRevision = restored["revisione"]!.AsObject();
        expect.That(summary.Title == "Revisioni · Sezione 1" && summary.CurrentHeader == $"Rev. 2 · corrente · {ShellTexts.RevisionDate(currentRevision)}"
            && summary.CurrentNote == "Seconda" && summary.CurrentChanges.All(c => c.StartsWith("• ", StringComparison.Ordinal)) && summary.CurrentChanges.Count > 0, "Riepilogo: revisione corrente");
        expect.That(summary.Entries.Select(e => e.Number).SequenceEqual([1, 0]) && summary.Entries[0].Note == "Prima emissione"
            && summary.Entries[0].Header == $"Rev. 1 · {ShellTexts.RevisionDate(restored.Array("revisioni")[1]!.AsObject())}"
            && summary.Entries.All(e => e.Changes.All(c => c.StartsWith("• ", StringComparison.Ordinal))) && summary.ShowLabel == "Mostra questa revisione", "Riepilogo: revisioni archiviate");
        bool shown = false;
        summary.Shown += (_, _) => shown = true;
        summary.ShowCommand.Execute(summary.Entries[1]);
        expect.That(shown && project.DisplayedRevision == 0 && project.IsReadOnly, "Mostra questa revisione");
        expect.Throws<ArgumentException>(() => _ = new RevisionSummaryViewModel(project, child, bench.Messages), "Riepilogo di una sezione senza revisione");
        return expect.Count;
    }
}
