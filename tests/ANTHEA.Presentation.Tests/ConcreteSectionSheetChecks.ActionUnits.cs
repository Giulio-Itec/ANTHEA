using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Calculations;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules.ConcreteSection;
using Anthea.Presentation.Services;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// The actions of the RC section (F5.10 S2) beyond the traces: the tables of the panels with their columns and commands (historic and unified
/// actions), copy, a refused paste with its warning, the Excel commands and the file of the actions, the window of the actions on the dialog
/// service (cancel, apply with the new opening, its Excel commands), the read-only revision, the limit state of a domain panel and the selection
/// of the shear table after a calculation.
/// </summary>
internal static partial class ConcreteSectionSheetChecks
{
    private static async Task ActionsAsync(string root, Expect expect)
    {
        string folder = Path.Combine(Path.GetTempPath(), "anthea-f5-10-s2-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            await TablesAsync(root, expect);
            await CommandsAsync(root, folder, expect);
            await EditorAsync(root, folder, expect);
            await ActionsReadOnlyAsync(root, expect);
        }
        finally { Directory.Delete(folder, true); }
    }

    private static string Keys(ConcreteActionTableViewModel table) => string.Join("|", table.Table.Columns.Select(c => c.Key + (c.IsReadOnly ? "*" : "")));

    private static async Task TablesAsync(string root, Expect expect)
    {
        using (var sheet = OpenSheet(ConcreteSectionScenarios.FixtureData(root, "default")))
        {
            await IdleAsync(sheet);
            var actions = sheet.Actions;
            expect.That(!actions.Unified && actions.Domain3D.IsDomain && actions.Domain3D.Family == "SLU" && actions.Domain3D.State == "SLU"
                && ReferenceEquals(actions.Domain3D.Table.Rows, sheet.ActionRowSets["SLU"]) && ReferenceEquals(actions.Shear.Table.Rows, sheet.ShearRowCollection)
                && actions.Stress.Keys.SequenceEqual(["SLE", "SLE_FREQ", "SLE_QP"]) && ReferenceEquals(actions.Table("SLE_FREQ"), actions.Stress["SLE_FREQ"])
                && ReferenceEquals(actions.Table("3D"), actions.Domain3D) && ReferenceEquals(actions.Table("taglio"), actions.Shear), "Tabelle delle azioni dei pannelli");
            expect.Throws<ArgumentException>(() => actions.Table("Taglio"), "Tabella sconosciuta rifiutata");
            expect.That(Keys(actions.Domain3D) == "visible|nome|N|Mx|My|eta3d*|esito3d*" && Keys(actions.Domain2D) == "visible|nome|N|Mx|My|eta2d*|esito2d*"
                && Keys(actions.Stress["SLE"]) == "nome|N|Mx|My|sigma_c*|sigma_s*|eta_sigma*|stress_status*|wk*"
                && Keys(actions.Shear) == "nome|N|Mx|My|Vx|Vy|T|VRdx*|VRdy*|eta_x*|eta_y*|eta_t*|eta_vt*|esito*", "Colonne delle tabelle storiche");
            expect.That(actions.Domain3D.AddCommand.CanExecute(null) && actions.Domain3D.DuplicateCommand.CanExecute(null) && actions.Shear.AddCommand.CanExecute(null)
                && !actions.Shear.DuplicateCommand.CanExecute(null) && actions.Shear.IsShear && actions.Domain3D.PasteCommand.CanExecute(null)
                && actions.Domain3D.CopyCommand.CanExecute(null) && actions.EditCommand.CanExecute(null) && actions.Domain3D.Table.Filter.Key == "nome",
                "Comandi delle tabelle storiche");
            // The limit state of a domain panel: the rows of SLV, the first selected; an unknown state or the same state change nothing.
            int modified = 0; sheet.Modified += (_, _) => modified++;
            actions.Domain3D.State = "SLV";
            expect.That(actions.Domain3D.Family == "SLV" && ReferenceEquals(actions.Domain3D.Table.Rows, sheet.ActionRowSets["SLV"])
                && actions.Domain3D.Table.SelectedIndex == 0 && sheet.Settings["dominio3d"]!["stato"]!.ToString() == "SLV" && modified == 1
                && sheet.ActionRowSets["SLV"][0].Values["esito3d"] is not null && actions.Domain2D.Family == "SLU", "Stato del dominio 3D: azioni SLV, uscite, Modified");
            actions.Domain3D.State = "SLV"; actions.Domain3D.State = "SLE";
            expect.That(modified == 1 && actions.Domain3D.Family == "SLV" && actions.Stress["SLE"].State is null, "Stato invariato o non ammesso: nessun effetto");
        }
        using (var sheet = OpenSheet(ConcreteSectionScenarios.FixtureData(root, "sezione-a-t-sollecitazioni-uniche")))
        {
            await IdleAsync(sheet);
            var actions = sheet.Actions;
            expect.That(actions.Unified && Keys(actions.Domain3D) == "elemento*|visible|nome*|N*|Mx*|My*|eta3d*|esito3d*"
                && Keys(actions.Stress["SLE"]) == "elemento*|nome*|N*|Mx*|My*|sigma_c*|sigma_s*|eta_sigma*|stress_status*|wk*"
                && Keys(actions.Shear).StartsWith("elemento*|stato*|nome|", StringComparison.Ordinal) && actions.Shear.Table.IsReadOnly && !actions.Domain3D.Table.IsReadOnly,
                "Sollecitazioni uniche: colonne in sola lettura tranne «Mostra», «Elemento» e «Stato limite»");
            var all = new[] { actions.Domain3D, actions.Domain2D, actions.Shear }.Concat(actions.Stress.Values).ToArray();
            expect.That(all.All(t => !t.AddCommand.CanExecute(null) && !t.RemoveCommand.CanExecute(null) && !t.PasteCommand.CanExecute(null) && !t.CopyCommand.CanExecute(null)
                && !t.TemplateCommand.CanExecute(null) && !t.ExportCommand.CanExecute(null) && !t.BrowseCommand.CanExecute(null) && t.ImportCommand.CanExecute(null)),
                "Sollecitazioni uniche: solo «Gestisci sollecitazioni…»");
        }
    }

    private static async Task CommandsAsync(string root, string folder, Expect expect)
    {
        var messages = new ScriptedMessages(); var files = new ScriptedFileDialogs(); var clipboard = new RecordingClipboard();
        var services = new PresentationServices(messages, new ScriptedConfirmations(), files, new ScriptedDialogs(), clipboard, new RecordingLinks(), new RecordingWiki());
        using var sheet = OpenSheet(ConcreteSectionScenarios.FixtureData(root, "taglio-senza-staffe-manuali"), services: services);
        await IdleAsync(sheet);
        int modified = 0; sheet.Modified += (_, _) => modified++;
        var actions = sheet.Actions; var sle = actions.Stress["SLE"];
        // Rows of a limit state.
        sle.AddCommand.Execute(null);
        expect.That(sheet.ActionRowSets["SLE"].Count == 2 && ReferenceEquals(sle.Table.Selected, sheet.ActionRowSets["SLE"][1])
            && sheet.Data["combinazioni"]!["SLE"]!.AsArray().Count == 2 && modified == 1 && sheet.IsCalculationQueued, "«+ Riga»: riga nuova selezionata, dati, ricalcolo");
        sle.DuplicateCommand.Execute(null);
        var copy = sheet.ActionRowSets["SLE"][2].Values;
        expect.That(sheet.ActionRowSets["SLE"].Count == 3 && copy.S("N") == "0" && copy.S("nome") != sheet.ActionRowSets["SLE"][1].Values.S("nome"), "«Duplica»: forze della riga selezionata, nome nuovo");
        sle.Table.Selected = sheet.ActionRowSets["SLE"][0];
        sle.RemoveCommand.Execute(null);
        expect.That(sheet.ActionRowSets["SLE"].Count == 2 && ReferenceEquals(sle.Table.Selected, sheet.ActionRowSets["SLE"][0]), "«− Riga»: la selezione passa alla riga al suo posto");
        // Rows of the shear: stored in the options of the shear.
        actions.Shear.AddCommand.Execute(null);
        expect.That(sheet.ShearRowCollection.Count == 4 && sheet.Settings["taglio"]!["azioni"]!.AsArray().Count == 4 && ReferenceEquals(actions.Shear.Table.Selected, sheet.ShearRowCollection[3]),
            "«+ Combinazione» del taglio");
        actions.Shear.RemoveCommand.Execute(null);
        expect.That(sheet.ShearRowCollection.Count == 3 && sheet.Settings["taglio"]!["azioni"]!.AsArray().Count == 3, "«−» del taglio");
        await IdleAsync(sheet);
        // The shear table selects its first row after a calculation, when none is selected.
        actions.Shear.Table.Selected = null;
        sheet.ShearRowCollection[0]["Vx"] = "120";
        await IdleAsync(sheet);
        expect.That(actions.Shear.Table.SelectedIndex == 0, "Taglio calcolato: prima riga selezionata");
        // Copy: the selected rows in the order shown.
        sle.SetSelectedRows(sheet.ActionRowSets["SLE"].Reverse());
        sle.CopyCommand.Execute(null);
        expect.That(clipboard.GetText() == ConcreteActionPresentation.CopyText(sle.Table.Shown.Select(r => r.Values), "SLE")
            && sheet.Status == ConcreteActionPresentation.CopyStatus(2), "«Copia»: righe selezionate nell'ordine mostrato, stato");
        // A refused paste: the warning of the historic window, no row changed.
        string before = sheet.Data.ToJsonString(); int beforeModified = modified;
        clipboard.SetText("1\t2");
        sle.PasteCommand.Execute(null);
        expect.That(messages.Shown.Count == 1 && messages.Shown[0].Title == "Incolla sollecitazioni" && messages.Shown[0].Warning
            && messages.Shown[0].Message.EndsWith("\nNessun dato incollato.", StringComparison.Ordinal) && sheet.Data.ToJsonString() == before && modified == beforeModified,
            "Incolla rifiutato: avviso, nessuna modifica");
        clipboard.SetText("Taglio Z\t-100\t0\t0\t50\t20\t0");
        actions.Shear.AppendCommand.Execute(null);
        expect.That(sheet.ShearRowCollection.Count == 4 && sheet.ShearRowCollection[3].Values.S("Vx") == "50" && sheet.Status == ConcreteActionPresentation.PasteStatus(1)
            && sheet.Settings["taglio"]!["azioni"]!.AsArray().Count == 4, "«Aggiungi dagli appunti» del taglio");
        // Excel: «Template Excel» writes the workbook and remembers the file; «Sfoglia…» chooses another file; a cancelled dialog changes nothing.
        string template = Path.Combine(folder, "modello.xlsx"), other = Path.Combine(folder, "altro.xlsx");
        files.Then(template).Then(other).Then(null);
        beforeModified = modified;
        sle.TemplateCommand.Execute(null);
        expect.That(File.ReadAllBytes(template).SequenceEqual(SectionActionsExcel.Template()) && actions.ExcelPath == template && sle.ExcelPath == template
            && sheet.Settings.S("file_sollecitazioni") == template && modified == beforeModified + 1 && sheet.Status.StartsWith("Template salvato.", StringComparison.Ordinal),
            "«Template Excel»: cartella scritta, file delle azioni, Modified");
        actions.Domain2D.BrowseCommand.Execute(null);
        expect.That(actions.ExcelPath == other && actions.Shear.ExcelPath == other && modified == beforeModified + 2, "«Sfoglia…»: file delle azioni scelto");
        actions.Domain2D.BrowseCommand.Execute(null);
        expect.That(actions.ExcelPath == other && modified == beforeModified + 2, "«Sfoglia…» annullato");
        var opened = files.Requests.OfType<OpenFileRequest>().ToArray();
        expect.That(opened.Length == 2 && opened[0].FileName == template && opened[1].FileName == other && opened.All(r => r.Filters.Single().Patterns.Single() == "*.xlsx"),
            "«Sfoglia…» propone il file delle azioni");
    }

    private static async Task EditorAsync(string root, string folder, Expect expect)
    {
        var dialogs = new ScriptedDialogs(); var files = new ScriptedFileDialogs();
        var services = new PresentationServices(new ScriptedMessages(), new ScriptedConfirmations(), files, dialogs, new RecordingClipboard(), new RecordingLinks(), new RecordingWiki());
        using var sheet = OpenSheet(ConcreteSectionScenarios.FixtureData(root, "default"), services: services);
        await IdleAsync(sheet);
        int modified = 0; sheet.Modified += (_, _) => modified++;
        string committed = "";
        // «Annulla»: nothing applied (only the commit before the window).
        dialogs.Then(dialog => { committed = sheet.Data.ToJsonString(); ((SectionActionsViewModel)dialog).CancelCommand.Execute(null); });
        await sheet.Actions.EditCommand.ExecuteAsync(null);
        expect.That(sheet.Data.ToJsonString() == committed && modified == 0 && sheet.HasResults && dialogs.Shown[^1] is SectionActionsViewModel { Title: "Sollecitazioni della sezione" }, "Finestra annullata");
        // «Esporta Excel» with the historic actions opens the window.
        dialogs.Then(dialog => ((SectionActionsViewModel)dialog).CancelCommand.Execute(null));
        await sheet.Actions.Domain3D.ExportCommand.ExecuteAsync(null);
        expect.That(dialogs.Shown.Count == 2 && modified == 0, "«Esporta Excel» delle azioni storiche: la finestra");
        // The Excel commands of the window and «Applica»: the actions become unified, the sheet opens again.
        string template = Path.Combine(folder, "finestra.xlsx");
        files.Then(template).Then(template).Then(Path.Combine(folder, "mancante.xlsx"));
        var oldActions = sheet.Actions;
        dialogs.Then(dialog =>
        {
            var editor = (SectionActionsViewModel)dialog;
            expect.That(editor.SaveTemplateCommand.CanExecute(null) && editor.Legacy && editor.HasOrigins, "Finestra con i comandi Excel e le azioni precedenti");
            editor.SaveTemplateCommand.Execute(null);
            expect.That(File.Exists(template) && editor.Status == "Excel salvato.", "Finestra: «Template Excel»");
            editor.ImportReplaceCommand.Execute(null);
            expect.That(editor.Status == "Il foglio Azioni è vuoto: nessuna modifica." && editor.Rows.Count == 3, "Finestra: cartella vuota rifiutata");
            editor.ImportAppendCommand.Execute(null);
            expect.That(editor.Status.Length > 0 && editor.Status != "Il foglio Azioni è vuoto: nessuna modifica." && editor.Rows.Count == 3, "Finestra: file mancante nello stato");
            editor.ApplyAndCloseCommand.Execute(null);
            expect.That(editor.Result is null && editor.Status == "Scegliere lo stato limite per Combo 1.", "Finestra: stato limite mancante, nessuna chiusura");
            foreach (var (row, state) in editor.Rows.Zip(new[] { "SLU", "SLV", "Rara" }))
            { row.State = state; editor.Selected = row; editor.FillMissingWithZeroCommand.Execute(null); }
            editor.ApplyAndCloseCommand.Execute(null);
        });
        await sheet.Actions.EditCommand.ExecuteAsync(null);
        expect.That(modified == 1 && SectionActionInputs.Unified(sheet.Data) && !ReferenceEquals(oldActions, sheet.Actions) && sheet.Actions.Unified
            && sheet.Status == ConcreteSheetViewModel.OpeningStatus && !sheet.HasResults && !sheet.IsCalculationQueued, "«Applica»: azioni uniche, foglio aperto di nuovo, calcolo non ancora in coda");
        await Task.Yield(); await Task.Delay(20);
        expect.That(sheet.IsCalculationQueued || sheet.IsBusy || sheet.HasResults, "Calcolo dell'apertura un giro dopo");
        await IdleAsync(sheet);
        expect.That(sheet.HasResults && sheet.ActionRowSets["SLU"].Count == 1 && sheet.ActionRowSets["SLE"].Count == 1, "Foglio calcolato sulle azioni applicate");
    }

    private static async Task ActionsReadOnlyAsync(string root, Expect expect)
    {
        using var sheet = OpenSheet(ConcreteSectionScenarios.FixtureData(root, "circolare-cava"), readOnly: true);
        await IdleAsync(sheet);
        var actions = sheet.Actions; var table = actions.Domain3D;
        expect.That(table.Table.IsReadOnly && !table.AddCommand.CanExecute(null) && !table.DuplicateCommand.CanExecute(null) && !table.RemoveCommand.CanExecute(null)
            && !table.PasteCommand.CanExecute(null) && !table.AppendCommand.CanExecute(null) && !table.BrowseCommand.CanExecute(null) && !table.ImportCommand.CanExecute(null)
            && !actions.EditCommand.CanExecute(null) && table.CopyCommand.CanExecute(null) && table.TemplateCommand.CanExecute(null) && table.ExportCommand.CanExecute(null),
            "Sola lettura: comandi delle tabelle protetti, «Copia», «Template Excel» ed «Esporta Excel» ammessi");
        int modified = 0; sheet.Modified += (_, _) => modified++;
        table.State = "SLV";
        expect.That(sheet.Settings["dominio3d"]!["stato"]!.ToString() == "SLV" && modified == 0, "Sola lettura: lo stato del dominio resta in memoria, senza Modified (T6)");
        sheet.IsReadOnly = false;
        expect.That(!table.Table.IsReadOnly && table.AddCommand.CanExecute(null) && actions.EditCommand.CanExecute(null), "Revisione modificabile: comandi delle tabelle di nuovo");
    }
}
