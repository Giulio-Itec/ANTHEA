using Anthea.Application.Documents;
using Anthea.Calculations;
using Anthea.Presentation.Services;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Commands;
using Anthea.Presentation.Shell.Tabs;
using Anthea.Projects;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// Step 2e of docs/sviluppo/progetto-sistema.md (decision D2 of 3/11/2026): the recovery copies of the open documents. A changed project
/// gets a copy, written again only after a new change and deleted when the project is saved; a copy left over by a crash is offered at the
/// start: «Più tardi» keeps it, «Riprendi» opens its document on its file, not saved, «Scarta» deletes it.
/// </summary>
internal static class RecoveryChecks
{
    internal static int Run(string output) => SingleThreadContext.Run(_ => Task.FromResult(RunAll(output)));

    private sealed class NoWindows : IWindowService
    {
        public void Open(TabHostViewModel host) { }
        public void Close(TabHostViewModel host) { }
        public void Activate(TabHostViewModel host) { }
    }

    private static int RunAll(string output)
    {
        var expect = new Expect();
        string folder = Path.Combine(output, "shell-recupero");
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
        Directory.CreateDirectory(folder);
        var store = new RecoveryStore(Path.Combine(folder, "Recupero"));
        string path = Path.Combine(folder, "Ponte.programma");

        // The writer: a copy of the changed project, again only after a new change, deleted when the project is saved.
        var bench = new ShellBench(dialogs: new ScriptedFileDialogs().Then(path));
        var workspace = bench.Workspace;
        using (var writer = new RecoveryWriter(store, workspace, automatic: false))
        {
            var project = workspace.NewProject()!;
            Wait(writer.WriteNow());
            expect.That(writer.SlotOf(project) is null, "Copia di un progetto senza modifiche");
            project.AddProject();
            Wait(writer.WriteNow());
            var slot = writer.SlotOf(project);
            expect.That(slot is { Writes: 1 } && File.Exists(Path.Combine(slot.Folder, "documento.programma")) && store.LeftOver().Count == 0 && writer.LastFailure is null,
                "Copia del progetto modificato, in uso");
            Wait(writer.WriteNow());
            expect.That(slot!.Writes == 1, "Copia scritta di nuovo senza modifiche");
            project.AddProject();
            Wait(writer.WriteNow());
            expect.That(slot.Writes == 2, "Copia scritta di nuovo dopo una modifica");
            expect.That(workspace.Save(project, false) && writer.SlotOf(project) is null && !Directory.Exists(slot.Folder), "Copia cancellata al salvataggio");

            // A crash: the copy of a new change stays on disk with its lock released.
            project.Rename(project.Projects.First(), "Ponte modificato");
            Wait(writer.WriteNow());
            writer.SlotOf(project)!.Abandon();
        }
        workspace.Dispose();
        var crashed = store.LeftOver();
        expect.That(crashed is { Count: 1 } left && left[0].Info.File == path && left[0].Info.Title == "Ponte.programma" && left[0].Info.Project
            && left[0].Info.Stamp == FileStamp.Of(path), "Copia rimasta dopo un crash");
        foreach (var entry in crashed) entry.Dispose();

        // The next start: «Più tardi», then «Riprendi» on the file, then a copy discarded.
        var confirmations = new ScriptedConfirmations().Then(RecoveryAnswer.Later).Then(RecoveryAnswer.Resume);
        var next = new ShellBench(confirmations);
        using (var shell = new ShellViewModel(next.Services, next.Factory, new DocumentFiles(Path.Combine(Path.GetTempPath(), "anthea-esempi-assenti")), new NoWindows(),
            new SheetFileExports(), new ShellOptions(Recovery: store)))
        {
            shell.OfferRecovery();
            expect.That(shell.Workspace.Project is null && store.LeftOver() is { Count: 1 } kept && kept.All(e => { e.Dispose(); return true; })
                && confirmations.Asked.Single().StartsWith(WorkspaceSession.RecoveryTitle + ": ANTHEA non si è chiuso correttamente. Ci sono modifiche non salvate di «Ponte.programma»", StringComparison.Ordinal),
                "«Più tardi»: copia tenuta");
            shell.OfferRecovery();
            var resumed = shell.Workspace.Project;
            expect.That(resumed is not null && resumed.Path == path && resumed.IsDirty && resumed.Lock is not null && resumed.Projects.First().S("nome") == "Ponte modificato"
                && store.LeftOver().Count == 0 && DocumentService.Open(path).Array("progetti").First().S("nome") != "Ponte modificato",
                "«Riprendi»: il documento della copia sul suo file, da salvare");
        }

        var discard = new ShellBench(new ScriptedConfirmations().Then(RecoveryAnswer.Discard));
        using (var writer = new RecoveryWriter(store, discard.Workspace, automatic: false))
        {
            var project = discard.Workspace.NewProject()!;
            project.AddProject();
            Wait(writer.WriteNow());
            writer.SlotOf(project)!.Abandon();
        }
        discard.Workspace.Dispose();
        using (var shell = new ShellViewModel(discard.Services, discard.Factory, new DocumentFiles(Path.Combine(Path.GetTempPath(), "anthea-esempi-assenti")), new NoWindows(),
            new SheetFileExports(), new ShellOptions(Recovery: store)))
        {
            shell.OfferRecovery();
            expect.That(shell.Workspace.Project is null && store.LeftOver().Count == 0 && discard.Confirmations.Asked.Single().Contains("«Nuovo progetto»", StringComparison.Ordinal),
                "«Scarta»: copia cancellata");
        }
        return expect.Count;
    }

    private static void Wait(Task task) => task.GetAwaiter().GetResult();
}
