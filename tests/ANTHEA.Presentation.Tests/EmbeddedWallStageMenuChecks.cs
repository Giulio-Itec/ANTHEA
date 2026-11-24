using System.Text.Json.Nodes;
using Anthea.Application.EmbeddedWalls;
using Anthea.Application.Modules;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.EmbeddedWalls;
using Anthea.Presentation.Shell;

namespace Anthea.Presentation.Tests;

/// <summary>
/// Organizzazione del modulo paratie (progetto del 16/11), step 2: the phases of the case as tabs and the menu Fase in the sheet. Every
/// command is one change (one step of the history of the project), asks before renaming all or deleting, lists the rows that go with a
/// deleted phase, selects the phase it made or kept, and does nothing in a read-only sheet. The rules are checked in
/// ANTHEA.Application.Tests (EmbeddedWallStageEditingChecks).
/// </summary>
internal static partial class EmbeddedWallChecks
{
    private static async Task StageMenuChecks(Expect e)
    {
        var data = EmbeddedWallDocument.Create();
        var f2 = EmbeddedWallDocument.NewRow("stages"); f2["id"] = "F2"; f2["right_ground"] = -4; f2["left_water"] = -1.5; f2["right_water"] = -5;
        data["stages"]!.AsArray().Add(f2);
        var service = EmbeddedWallDocument.NewRow("cases"); service["id"] = "Servizio"; data["cases"]!.AsArray().Add(service);
        var s1 = EmbeddedWallDocument.NewRow("stages"); s1["id"] = "S1"; s1["case"] = "Servizio"; s1["right_ground"] = -2; data["stages"]!.AsArray().Add(s1);
        data["supports"]!.AsArray().Add(EmbeddedWallDocument.NewRow("supports"));
        var activation = EmbeddedWallDocument.NewRow("activations"); activation["stage"] = "F2"; data["activations"]!.AsArray().Add(activation);
        var load = EmbeddedWallDocument.NewRow("loads"); load["stage"] = "F2"; load["horizontal"] = 5; data["loads"]!.AsArray().Add(load);
        var confirmations = new ScriptedConfirmations(); var dialogs = new ScriptedDialogs(); var messages = new ScriptedMessages();
        using var sheet = new EmbeddedWallSheetViewModel(new(data.DeepClone().AsObject(), false, new Host(), Scripted.Services(messages, confirmations, null, dialogs),
            ModuleServices.Get(EmbeddedWallDocument.Module), new CalculationLimiter(1)));
        int changes = 0; sheet.Modified += (_, _) => changes++; long revision = sheet.Revision;
        string Ids() => string.Join(",", sheet.InputStages.Select(s => s["id"]));
        JsonObject[] Rows(string table) => sheet.Data[table]!.AsArray().Cast<JsonObject>().ToArray();
        e.That(Ids() == "F1,F2" && sheet.InputStage?["id"] == "F2" && sheet.AddStageCommand.CanExecute(null) && sheet.DeleteStageCommand.CanExecute(null),
            "Passo 2: schede delle fasi del caso o comandi non disponibili all'apertura.");

        sheet.AddStageCommand.Execute(null);
        e.That(Ids() == "F1,F2,F3" && sheet.InputStage?["id"] == "F3" && sheet.InputStage["right_ground"] == "-4" && changes == 1 && sheet.Revision == revision + 1,
            "Passo 2: «Aggiungi fase» non aggiunge in coda una fase con le quote della precedente, o non è una modifica sola.");
        sheet.InsertStageCommand.Execute(null);
        e.That(Ids() == "F1,F2,F4,F3" && sheet.InputStage?["id"] == "F4" && sheet.InputStage["right_ground"] == "-4" && sheet.InputStage["left_water"] == "-1.5"
            && sheet.Sketch?.RightGround == -4 && changes == 2, "Passo 2: «Inserisci fase» non inserisce prima della corrente con le quote della precedente (D9).");
        e.That(sheet.Tables.Single(t => t.Key == "stages").Rows.Count == 5 && ReferenceEquals(sheet.Tables.Single(t => t.Key == "stages").Selected, sheet.InputStage),
            "Passo 2: la tabella delle fasi non segue l'inserimento.");

        confirmations.Then(false); sheet.RenameAllStagesCommand.Execute(null);
        e.That(Ids() == "F1,F2,F4,F3" && changes == 2 && confirmations.Asked.Last().Contains("«Caso 1»"), "Passo 2: «Rinomina tutte» rifiutata ma eseguita, o domanda senza il caso.");
        var inserted = sheet.InputStage;
        confirmations.Then(true); sheet.RenameAllStagesCommand.Execute(null);
        e.That(Ids() == "F1,F2,F3,F4" && ReferenceEquals(sheet.InputStage, inserted) && sheet.InputStage?["id"] == "F3" && changes == 3,
            "Passo 2: «Rinomina tutte» non dà F1…Fn nell'ordine della storia o perde la fase scelta.");

        sheet.InputStage = sheet.InputStages[1];
        dialogs.Then(d =>
        {
            var dialog = (EmbeddedWallStageNameDialogViewModel)d;
            e.That(dialog.Name == "F2" && dialog.Prompt.Contains("«Caso 1»"), "Passo 2: il dialogo non propone il nome della fase corrente.");
            dialog.Name = "Scavo"; dialog.ConfirmCommand.Execute(null);
        });
        await sheet.RenameStageCommand.ExecuteAsync(null);
        e.That(Ids() == "F1,Scavo,F3,F4" && Rows("activations")[0]["stage"]!.ToString() == "Scavo" && Rows("loads")[0]["stage"]!.ToString() == "Scavo" && changes == 4,
            "Passo 2: «Rinomina» non rinomina la fase con i suoi riferimenti in una modifica sola.");
        e.That(sheet.Sketch?.Supports.Count == 1 && sheet.Sketch.Loads.Count == 1, "Passo 2: dopo la rinomina il disegno perde sostegni o carichi della fase.");
        dialogs.Then(d => { var dialog = (EmbeddedWallStageNameDialogViewModel)d; dialog.Name = "F1"; dialog.ConfirmCommand.Execute(null); });
        await sheet.RenameStageCommand.ExecuteAsync(null);
        e.That(messages.Failures.Count == 1 && Ids() == "F1,Scavo,F3,F4" && changes == 4, "Passo 2: accettato un nome già usato nel caso.");
        dialogs.Then(d => ((EmbeddedWallStageNameDialogViewModel)d).CancelCommand.Execute(null));
        await sheet.RenameStageCommand.ExecuteAsync(null);
        e.That(Ids() == "F1,Scavo,F3,F4" && changes == 4, "Passo 2: «Annulla» del dialogo modifica la fase.");

        confirmations.Then(false); sheet.DeleteStageCommand.Execute(null);
        string question = confirmations.Asked.Last();
        e.That(Ids() == "F1,Scavo,F3,F4" && changes == 4 && question.Contains("«Scavo»") && question.Contains(EmbeddedWallFields.Title("activations") + " 1")
            && question.Contains(EmbeddedWallFields.Title("loads") + " 1"), "Passo 2: «Elimina» senza le righe citate nella domanda, o eseguita dopo il rifiuto: " + question);
        confirmations.Then(true); sheet.DeleteStageCommand.Execute(null);
        e.That(Ids() == "F1,F3,F4" && sheet.InputStage?["id"] == "F1" && Rows("activations").Length == 0 && Rows("loads").Length == 0 && changes == 5,
            "Passo 2: «Elimina» non toglie la fase con le righe che la citano, o non seleziona la precedente.");
        e.That(Rows("stages").Count(r => r["case"]?.ToString() == "Servizio") == 1 && Rows("supports").Length == 1, "Passo 2: «Elimina» tocca un altro caso o i sostegni.");

        sheet.InputCase = "Servizio"; sheet.AddStageCommand.Execute(null);
        e.That(sheet.InputStage?["case"] == "Servizio" && sheet.InputStage["right_ground"] == "-2" && Ids() == "S1," + sheet.InputStage["id"]
            && !Rows("stages").Where(r => r["case"]?.ToString() != "Servizio").Any(r => r["id"]!.ToString() == sheet.InputStage["id"]),
            "Passo 2: la fase aggiunta in un altro caso non appartiene al caso scelto o ripete un identificativo.");
        e.That(messages.Failures.Count == 1, "Passo 2: un comando delle fasi ha segnalato un errore inatteso: " + messages.Failures.LastOrDefault().Error?.Message);

        using (var readOnly = Open(data, readOnly: true))
        {
            string before = readOnly.Data.ToJsonString();
            e.That(!readOnly.AddStageCommand.CanExecute(null) && !readOnly.InsertStageCommand.CanExecute(null) && !readOnly.RenameStageCommand.CanExecute(null)
                && !readOnly.RenameAllStagesCommand.CanExecute(null) && !readOnly.DeleteStageCommand.CanExecute(null), "Passo 2: comandi delle fasi attivi in sola lettura.");
            readOnly.AddStageCommand.Execute(null); readOnly.InsertStageCommand.Execute(null);
            e.That(readOnly.Data.ToJsonString() == before, "Passo 2: le fasi di un foglio in sola lettura cambiano.");
        }

        // R04 of the review G-208: an insertion that interrupts a support asks first, naming it and the prestress that changes.
        var chain = EmbeddedWallDocument.Create();
        var second = EmbeddedWallDocument.NewRow("stages"); second["id"] = "F2"; chain["stages"]!.AsArray().Add(second);
        chain["supports"]!.AsArray().Add(EmbeddedWallDocument.NewRow("supports"));
        var installed = EmbeddedWallDocument.NewRow("activations"); installed["prestress"] = 150; chain["activations"]!.AsArray().Add(installed);
        var kept = EmbeddedWallDocument.NewRow("activations"); kept["stage"] = "F2"; chain["activations"]!.AsArray().Add(kept);
        var asked = new ScriptedConfirmations();
        using (var interrupted = new EmbeddedWallSheetViewModel(new(chain.DeepClone().AsObject(), false, new Host(), Scripted.Services(confirmations: asked),
            ModuleServices.Get(EmbeddedWallDocument.Module), new CalculationLimiter(1))))
        {
            string untouched = interrupted.Data.ToJsonString();
            asked.Then(false); interrupted.InsertStageCommand.Execute(null);
            e.That(interrupted.Data.ToJsonString() == untouched && asked.Asked.Count == 1 && asked.Asked[0].Contains("«T1»") && asked.Asked[0].Contains("0 kN"),
                "R04: l'inserimento che interrompe un sostegno non lo dice prima o avviene dopo il rifiuto: " + string.Join(" | ", asked.Asked));
            asked.Then(true); interrupted.InsertStageCommand.Execute(null);
            e.That(string.Join(",", interrupted.InputStages.Select(s => s["id"])) == "F1,F3,F2" && interrupted.InputStage?["id"] == "F3",
                "R04: l'inserimento confermato non avviene prima della fase corrente.");
        }

        // R01 of the review G-208, in the project: every command is one step of the history by itself, with no other confirmation of the sheet;
        // what was typed before is a step of its own; a refused question records nothing; «Annulla» goes back one operation at a time.
        var answers = new ScriptedConfirmations();
        var services = Scripted.Services(confirmations: answers);
        using var workspace = new WorkspaceSession(services, new RegistrySheetFactory(new ModuleRegistry([new EmbeddedWallSheetModule()]), services, new CalculationLimiter(1)),
            new DocumentFiles(Path.Combine(Path.GetTempPath(), "anthea-esempi-assenti")));
        var project = workspace.NewProject()!; var section = project.AddSection(project.AddProject()!)!;
        var node = project.AddSheet(EmbeddedWallDocument.Module, section)!; string id = node["id"]!.ToString(), name = ShellTexts.SheetName(node);
        var opened = (EmbeddedWallSheetViewModel)workspace.OpenSheet(node).ViewModel;
        string Stored() => project.Find(id)!["dati"]!.ToJsonString();
        JsonArray StoredRows(string table) => project.Find(id)!["dati"]![table]!.AsArray();
        string initial = Stored();
        opened.AddStageCommand.Execute(null);
        string afterAdd = Stored();
        e.That(afterAdd != initial && StoredRows("stages").Count == 2 && project.UndoLabel == ProjectSession.EditLabel(name),
            "R01: «Aggiungi fase» non è da sola un passo della cronologia: " + project.UndoLabel);
        opened.Tables.Single(t => t.Key == "activations").AddCommand.Execute(null);
        e.That(Stored() == afterAdd, "R01: una riga digitata è entrata nel progetto prima della conferma.");
        opened.InsertStageCommand.Execute(null);
        string afterInsert = Stored();
        e.That(StoredRows("stages").Count == 3 && StoredRows("activations").Count == 1, "R01: inserimento o riga digitata prima non confermati.");
        answers.Then(false); opened.DeleteStageCommand.Execute(null);
        e.That(Stored() == afterInsert, "R01: una domanda rifiutata ha modificato il progetto.");
        answers.Then(true); opened.RenameAllStagesCommand.Execute(null);
        e.That(Stored() != afterInsert && project.UndoLabel == ProjectSession.EditLabel(name), "R01: «Rinomina tutte» non è un passo della cronologia.");
        project.Restore(false);
        e.That(Stored() == afterInsert, "R01: «Annulla» non toglie in un passo solo «Rinomina tutte».");
        project.Restore(false);
        e.That(StoredRows("stages").Count == 2 && StoredRows("activations").Count == 1, "R01: l'inserimento non è un passo distinto dalla riga digitata prima.");
        project.Restore(false);
        e.That(Stored() == afterAdd, "R01: la riga digitata prima dell'inserimento non è un passo distinto.");
        project.Restore(true); project.Restore(true); project.Restore(true);
        e.That(StoredRows("stages").Count == 3 && Stored() != afterInsert, "R01: «Ripristina» non riporta le tre operazioni.");    }
}
