using System.Text.Json.Nodes;
using Anthea.Application.Documents;
using Anthea.Calculations;
using Anthea.Presentation.Services;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Commands;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// F5.13 b6: the commands of the shell without WPF: the menu «File» (order, titles, shortcuts of MainWindow.BuildMenu), open, save, export
/// of the sheet, results and reports with the file names, conditions and messages of MainWindow for each module, the contents of the Word
/// report, the short report, undo and redo of the project.
/// </summary>
internal static class CommandsChecks
{
    internal static int Run(string output) => SingleThreadContext.Run(_ => RunAsync(output));

    /// <summary>The writing of the files of a sheet, recorded.</summary>
    private sealed class RecordingExports : ISheetExports
    {
        internal List<string> Calls { get; } = [];
        internal Exception? ShortFailure { get; set; }
        public void ExportResult(SheetSession sheet, string path) => Calls.Add("risultati " + sheet.Module + " " + Path.GetFileName(path));
        public void ExportReport(SheetSession sheet, string path, string title, IReadOnlySet<string> options) =>
            Calls.Add("report " + sheet.Module + " " + Path.GetFileName(path) + " «" + title + "» " + string.Join(",", options.Order(StringComparer.Ordinal)));
        public byte[] BuildShortReport(SheetSession sheet, string title) { Calls.Add("short " + title); return [1, 2]; }
        public Task WriteShortReportAsync(string path, byte[] report, string title)
        {
            Calls.Add("scrivi short " + Path.GetFileName(path) + " " + report.Length);
            return ShortFailure is null ? Task.CompletedTask : Task.FromException(ShortFailure);
        }
    }

    private static async Task<int> RunAsync(string output)
    {
        var expect = new Expect();
        string folder = Path.Combine(output, "shell-comandi");
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
        Directory.CreateDirectory(folder);
        var files = new ScriptedFileDialogs();
        var dialogs = new ScriptedDialogs();
        var bench = new ShellBench(dialogs: files, windows: dialogs);
        var workspace = bench.Workspace;
        var exports = new RecordingExports();
        using var commands = new ShellCommandsViewModel(workspace, bench.Services, exports, new DocumentFiles(Path.Combine(folder, "Esempi")));

        // The menu «File» of today.
        expect.That(commands.File.Select(i => i.Title).SequenceEqual(["Nuovo palo", "Nuovo micropalo", "Nuova sezione in c.a.", "Nuovo archivio progetti",
            "Nuova sezione composta da ponte", "Nuovo Bridge Design", "Nuovo muro di sostegno", "Nuovo palo orizzontale", "Nuova palificata orizzontale",
            "Nuovo micropalo orizzontale", "", "Apri…", "Apri esempio…", "Salva", "Salva con nome…", "Esporta foglio selezionato…", "", "Report Word…",
            "Report short CLS…", "Risultati JSON…", "Esci"]), "Voci del menu File");
        expect.That(commands.File.Where(i => i.Shortcut is not null).Select(i => (i.Title, i.Shortcut!.Key, i.Shortcut.Shift)).SequenceEqual([("Nuovo palo", "N", false),
            ("Apri…", "O", false), ("Salva", "S", false), ("Salva con nome…", "S", true)]) && commands.File.Count(i => i.IsSeparator) == 2, "Scorciatoie");
        expect.That(commands.Appearances.SequenceEqual(["Chiaro", "Scuro", "Molto scuro"]) && ShellCommandsViewModel.RheologyTitle == "Reologia del calcestruzzo…", "Aspetto e strumenti");
        int requests = 0;
        commands.ExamplesRequested += (_, _) => requests++;
        commands.ExitRequested += (_, _) => requests += 10;
        commands.RheologyRequested += (_, _) => requests += 100;
        commands.File.Single(i => i.Title == "Apri esempio…").Command!.Execute(null);
        commands.ExitCommand.Execute(null); commands.RheologyCommand.Execute(null);
        expect.That(requests == 111, "Richieste alla vista");

        // «Nuovo …»: the modules of the directory, a new tab each; «Report short» only for the RC section.
        commands.File[0].Command!.Execute(null);
        var pile = workspace.Active!;
        expect.That(pile.Module == "geo_palo_verticale" && !commands.ShortReportVisible, "Nuovo palo");
        foreach (var item in commands.File.Take(10).Skip(1).Where(i => i.Title != "Nuovo archivio progetti"))
            expect.That(item.Command is not null && Anthea.Application.Modules.ModuleDirectory.All.Count > 0, "Voce " + item.Title);
        commands.File[2].Command!.Execute(null);
        var concrete = workspace.Active!;
        expect.That(concrete.Module == "str_palo" && commands.ShortReportVisible, "Nuova sezione in c.a.");

        // Results (MainWindow.ExportJson): without results the message of the module; with results «Risultati.json».
        commands.ResultsCommand.Execute(null);
        expect.That(bench.Messages.Shown.Last() == ("", ShellCommandsViewModel.WaitConcrete, false), "Risultati della sezione senza calcolo");
        workspace.Activate(pile);
        commands.ResultsCommand.Execute(null);
        expect.That(bench.Messages.Shown.Last() == ("", ShellCommandsViewModel.CalculateFirst, false), "Risultati senza calcolo");
        await Expect.Within(pile.ViewModel.CalculateAsync());
        files.Then(Path.Combine(folder, "Risultati.json"));
        commands.ResultsCommand.Execute(null);
        var request = (SaveFileRequest)files.Requests.Last();
        expect.That(exports.Calls.Last() == "risultati geo_palo_verticale Risultati.json" && request.FileName == "Risultati.json" && FileFilter.Join(request.Filters) == "Risultati JSON|*.json", "Risultati del palo");
        var material = workspace.NewCalculation("mat_calcestruzzo");
        commands.ResultsCommand.Execute(null);
        expect.That(bench.Messages.Shown.Last() == ("", ShellCommandsViewModel.MaterialResults, false), "Risultati di un materiale");

        // Reports: a material with its name and no calculation; the others only after it; the contents of the Word report for the piles.
        files.Then(Path.Combine(folder, "Scheda.docx"));
        await commands.ReportCommand.ExecuteAsync(null);
        request = (SaveFileRequest)files.Requests.Last();
        expect.That(exports.Calls.Last() == "report mat_calcestruzzo Scheda.docx «" + material.Heading + "» " && request.FileName == "Scheda_calcestruzzo.docx"
            && FileFilter.Join(request.Filters) == "Word e PDF affiancati|*.docx", "Report del calcestruzzo");
        workspace.Activate(concrete);
        await commands.ReportCommand.ExecuteAsync(null);
        expect.That(bench.Messages.Shown.Last() == ("", ShellCommandsViewModel.WaitConcrete, false), "Report della sezione senza calcolo");
        workspace.Activate(pile);
        dialogs.Then(d =>
        {
            var choice = (ReportSectionsDialogViewModel)d;
            expect.That(choice.Title == "Contenuti del report Word" && choice.Note is null && choice.Sections.Select(s => s.Key).SequenceEqual(ReportWord.Sezioni.Select(s => s.Key))
                && choice.Sections.All(s => s.IsChecked == !s.Key.StartsWith("grafico", StringComparison.Ordinal)) && choice.CanExport, "Contenuti del report del palo");
            foreach (var section in choice.Sections) section.IsChecked = section.Key == "dettagli";
            expect.That(!choice.CanExport && !choice.ExportCommand.CanExecute(null), "Report con le sole opzioni");
            choice.Sections[0].IsChecked = true;
            choice.ExportCommand.Execute(null);
        });
        files.Then(Path.Combine(folder, "Relazione.docx"));
        await commands.ReportCommand.ExecuteAsync(null);
        expect.That(exports.Calls.Last() == "report geo_palo_verticale Relazione.docx «" + pile.Heading + "» dettagli,generali" && ((SaveFileRequest)files.Requests.Last()).FileName == "Relazione.docx", "Report del palo: " + exports.Calls.Last());
        dialogs.Then(d => ((ReportSectionsDialogViewModel)d).CancelCommand.Execute(null));
        int calls = exports.Calls.Count;
        await commands.ReportCommand.ExecuteAsync(null);
        expect.That(exports.Calls.Count == calls, "Report annullato");

        // The defaults of the RC section and of the composite section, and the contents saved in the sheet.
        var data = new JsonObject();
        var concreteChoice = new ReportSectionsDialogViewModel("str_palo", data);
        expect.That(concreteChoice.Note == ReportSectionsDialogViewModel.ConcreteNote && concreteChoice.Sections.Select(s => s.Key).SequenceEqual(ReportConcrete.Sections.Select(s => s.Key))
            && concreteChoice.Sections.All(s => s.IsChecked == s.Key is not ("dettagli" or "sle_tutte")), "Contenuti della sezione c.a.");
        var bridge = new ReportSectionsDialogViewModel("str_mista_ponte", data);
        expect.That(bridge.Note == ReportSectionsDialogViewModel.BridgeNote && bridge.Sections.All(s => s.IsChecked), "Contenuti della sezione composta");
        data["workspace_ca"] = new JsonObject { ["report_sezioni"] = new JsonArray("taglio", "SLE") };
        var saved = new ReportSectionsDialogViewModel("str_palo", data);
        expect.That(saved.Sections.Where(s => s.IsChecked).Select(s => s.Key).Order(StringComparer.Ordinal).SequenceEqual(["SLE", "taglio"]), "Contenuti salvati nel foglio");

        // Short report: only for the RC section; written away, with the notes of today.
        workspace.Activate(pile);
        await commands.ShortReportCommand.ExecuteAsync(null);
        expect.That(bench.Messages.Shown.Last() == (ShellCommandsViewModel.ShortTitle, "Selezionare una sezione in calcestruzzo armato.", true), "Report short di un palo");
        workspace.Activate(concrete);
        files.Then(Path.Combine(folder, "Relazione_short.docx"));
        await commands.ShortReportCommand.ExecuteAsync(null);
        expect.That(exports.Calls.TakeLast(2).SequenceEqual(["short " + concrete.Heading, "scrivi short Relazione_short.docx 2"])
            && bench.Messages.Shown.Last() == (ShellCommandsViewModel.ShortTitle, "Report short salvato in Word e PDF, entro due pagine.", false)
            && ((SaveFileRequest)files.Requests.Last()).FileName == "Relazione_short.docx" && commands.ShortReportCommand.CanExecute(null), "Report short");
        exports.ShortFailure = new IOException("File in uso");
        files.Then(Path.Combine(folder, "Relazione_short.docx"));
        await commands.ShortReportCommand.ExecuteAsync(null);
        expect.That(bench.Messages.Shown.Last() == (ShellCommandsViewModel.ShortTitle, "File in uso", true) && commands.ShortReportCommand.CanExecute(null), "Errore del report short");

        // Save and export of the sheet: the document of the sheet with the focus.
        files.Then(Path.Combine(folder, "Sezione.programma"));
        commands.SaveCommand.Execute(null);
        expect.That(concrete.Owner.Path == Path.Combine(folder, "Sezione.programma") && !concrete.Owner.IsDirty, "Salva");
        files.Then(Path.Combine(folder, "Foglio.programma"));
        commands.ExportSheetCommand.Execute(null);
        var exported = DocumentService.Open(Path.Combine(folder, "Foglio.programma"));
        expect.That(exported.S("tipo") == "calcolo" && exported.S("modulo_id") == "str_palo" && exported["dati"] is JsonObject && exported.Count == 5
            && ((SaveFileRequest)files.Requests.Last()).FileName == "Foglio.programma", "Esporta foglio");
        files.Then(Path.Combine(folder, "Sezione.programma"));
        commands.OpenCommand.Execute(null);
        expect.That(ReferenceEquals(workspace.Active, concrete) && FileFilter.Join(((OpenFileRequest)files.Requests.Last()).Filters) == "File ANTHEA|*.programma;*.anthea|Tutti i file|*.*", "Apri un file già aperto");

        // Undo and redo follow the project.
        expect.That(!commands.UndoCommand.CanExecute(null) && !commands.RedoCommand.CanExecute(null), "Annulla senza progetto");
        commands.NewProjectCommand.Execute(null);
        var project = workspace.Project!;
        int notified = 0;
        commands.UndoCommand.CanExecuteChanged += (_, _) => notified++;
        project.AddProject();
        expect.That(commands.UndoCommand.CanExecute(null) && notified > 0 && ShellCommandsViewModel.UndoToolTip == "Annulla l’ultima operazione · Ctrl+Z", "Annulla disponibile");
        commands.UndoCommand.Execute(null);
        expect.That(project.Projects.Count() == 0 && commands.RedoCommand.CanExecute(null), "Annulla");
        commands.RedoCommand.Execute(null);
        expect.That(project.Projects.Count() == 1, "Ripristina");
        return expect.Count;
    }
}
