using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Application.Documents;
using Anthea.Calculations;
using Anthea.Presentation.Modules.QuickSection;

namespace Anthea.Presentation.Tests;

internal static class QuickCombinationOrderChecks
{
    internal static async Task Run(Expect expect, string folder, Func<JsonObject, QuickSectionSheetViewModel> open)
    {
        var data = ConcreteQuickSection.Create();
        ConcreteQuickSection.ReplaceCombinations(data,
            [new("Stesso nome", -1000, 400, 0), new("Stesso nome", -300, 150, 0, "SLE"), new("Terza", -800, 300, 0, "SLV")]);
        var ids = ConcreteQuickSection.Combinations(data).Select(r => r.S("id")).ToArray();
        ConcreteQuickSection.WriteCombinationSetting(data, ids[0], ConcreteQuickSection.ModelField, "Elastico");
        ConcreteQuickSection.WriteCombinationSetting(data, ids[0], ConcreteQuickSection.SignField, "SLU−");
        ConcreteQuickSection.SelectCombination(data, ids[1]);
        var originalRows = ConcreteQuickSection.Combinations(data).ToDictionary(r => r.S("id"), r => r.ToJsonString());
        string originalActions = data["combinazioni"]!.ToJsonString(), originalInput = data["input"]!.ToJsonString();
        ConcreteQuickSection.MoveCombination(data, ids[1], -1);
        expect.That(ConcreteQuickSection.Combinations(data).Select(r => r.S("id")).SequenceEqual([ids[1], ids[0], ids[2]])
            && ConcreteQuickSection.SelectedCombination(data) == ids[1], "Ordine: scambio fra SL diversi e nomi uguali basato sugli identificatori");
        expect.That(data["combinazioni"]!.ToJsonString() == originalActions && data["input"]!.ToJsonString() == originalInput
            && ConcreteQuickSection.Combinations(data).All(r => originalRows[r.S("id")] == r.ToJsonString()),
            "Ordine: azioni, stati, modello, segno e geometria conservati esattamente");
        string moved = data.ToJsonString();
        ConcreteQuickSection.MoveCombination(data, ids[1], -1);
        ConcreteQuickSection.MoveCombination(data, ids[2], 1);
        ConcreteQuickSection.MoveCombination(data, "assente", 1);
        ConcreteQuickSection.MoveCombination(data, ids[0], 0);
        expect.That(data.ToJsonString() == moved, "Ordine: estremi, id inesistente e direzione non ammessa non modificano il documento");
        expect.That(!ConcreteQuickSection.CanMoveCombination(data, null, 1) && !ConcreteQuickSection.CanMoveCombination(data, ids[1], -1)
            && ConcreteQuickSection.CanMoveCombination(data, ids[0], -1) && ConcreteQuickSection.CanMoveCombination(data, ids[0], 1),
            "Ordine: disponibilità delle due direzioni secondo la posizione");
        var book = ConcreteQuickActionsExcel.Read(ConcreteQuickActionsExcel.Export(data));
        expect.That(book.Select(r => r.State).SequenceEqual(["SLE", "SLU", "SLV"]) && book.Select(r => r.Mx).SequenceEqual([150d, 400d, 300d]),
            "Ordine: Excel esporta righe, stati e azioni nell’ordine visualizzato");
        var imported = ConcreteQuickSection.Create(); ConcreteQuickSection.ReplaceCombinations(imported, book);
        expect.That(ConcreteQuickActionsExcel.Rows(imported).SequenceEqual(book), "Ordine: reimportazione Excel conserva l’ordine esportato");
        var document = DocumentService.CreateDocument(ConcreteQuickSection.Module); document["dati"] = data.DeepClone();
        string path = Path.Combine(folder, "ordine-combinazioni.anthea"); DocumentService.Save(path, document);
        var reopened = DocumentService.Open(path)["dati"]!.AsObject();
        expect.That(ConcreteQuickSection.Combinations(reopened).Select(r => r.S("id")).SequenceEqual([ids[1], ids[0], ids[2]])
            && ConcreteQuickSection.SelectedCombination(reopened) == ids[1], "Ordine e selezione conservati dopo salvataggio e riapertura");

        using var sheet = open(reopened);
        await sheet.PendingCalculation;
        sheet.Title = "  Pilastro P1 · base  "; await sheet.PendingCalculation;
        expect.That(sheet.Title == "Pilastro P1 · base" && sheet.DisplayName == sheet.Title,
            "Nome sezione: testo ripulito e nome esposto alla scheda");
        sheet.Commit(); document["dati"] = sheet.Data.DeepClone(); DocumentService.Save(path, document);
        using (var namedAgain = open(DocumentService.Open(path)["dati"]!.AsObject()))
        {
            expect.That(namedAgain.Title == sheet.Title && namedAgain.DisplayName == sheet.DisplayName, "Nome sezione conservato dopo salvataggio e riapertura");
            namedAgain.IsReadOnly = true; namedAgain.Title = "Non consentito";
            expect.That(namedAgain.Title == sheet.Title, "Nome sezione protetto in sola lettura");
            expect.That(Anthea.Presentation.Shell.ShellTexts.SheetHeading(document, 3, namedAgain.DisplayName) == namedAgain.DisplayName + " · Rev. 3 · sola lettura",
                "Nome sezione: il titolo conserva l’indicazione di revisione e sola lettura");
            int nameChanges = 0; namedAgain.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(namedAgain.DisplayName)) nameChanges++; };
            var renamed = (JsonObject)namedAgain.Data.DeepClone(); ConcreteQuickSection.View(renamed)["titolo"] = "Nome ricaricato";
            namedAgain.Reload(renamed);
            expect.That(namedAgain.DisplayName == "Nome ricaricato" && nameChanges > 0, "Nome sezione: ricaricamento notifica il titolo alla shell");
        }
        var ratios = sheet.Current!.Combinations.ToDictionary(r => r.Id, r => (r.Ratio, r.Passed));
        string selectedSummary = sheet.Summary!.Value;
        int edits = 0; sheet.Modified += (_, _) => edits++;
        expect.That(!sheet.MoveCombinationUpCommand.CanExecute(null) && sheet.MoveCombinationDownCommand.CanExecute(null),
            "Ordine ViewModel: selezione iniziale sulla prima riga");
        sheet.MoveCombinationUpCommand.Execute(null);
        expect.That(edits == 0, "Ordine ViewModel: comando al limite non crea una modifica");
        sheet.MoveCombinationDownCommand.Execute(null); await sheet.PendingCalculation;
        expect.That(edits == 1 && sheet.CombinationRows.Select(r => r.Id).SequenceEqual(ids) && sheet.SelectedCombinationId == ids[1]
            && sheet.CombinationRows[1].IsSelected && sheet.MoveCombinationUpCommand.CanExecute(null) && sheet.MoveCombinationDownCommand.CanExecute(null),
            "Ordine ViewModel: una modifica, stessa selezione nella posizione nuova, comandi aggiornati");
        expect.That(sheet.Summary!.Value == selectedSummary && sheet.Current!.Combinations.All(r => ratios[r.Id] == (r.Ratio, r.Passed)),
            "Ordine ViewModel: risultato selezionato e rapporti di tutte le combinazioni invariati");
        sheet.CombinationRows[2].IsSelected = true; await sheet.PendingCalculation;
        expect.That(sheet.MoveCombinationUpCommand.CanExecute(null) && !sheet.MoveCombinationDownCommand.CanExecute(null),
            "Ordine ViewModel: cambio selezione aggiorna i limiti");
        sheet.IsReadOnly = true;
        string readOnly = sheet.Data.ToJsonString(); int beforeReadOnly = edits;
        sheet.MoveCombinationUpCommand.Execute(null); sheet.MoveCombinationDownCommand.Execute(null);
        expect.That(!sheet.MoveCombinationUpCommand.CanExecute(null) && !sheet.MoveCombinationDownCommand.CanExecute(null)
            && sheet.Data.ToJsonString() == readOnly && edits == beforeReadOnly, "Ordine ViewModel: sola lettura protetta anche con esecuzione diretta");
        sheet.IsReadOnly = false;
        sheet.CombinationRows[2].RemoveCommand.Execute(null); sheet.CombinationRows[1].RemoveCommand.Execute(null); await sheet.PendingCalculation;
        expect.That(!sheet.MoveCombinationUpCommand.CanExecute(null) && !sheet.MoveCombinationDownCommand.CanExecute(null),
            "Ordine ViewModel: unica combinazione non spostabile");
        sheet.AddCombinationCommand.Execute(null); await sheet.PendingCalculation;
        expect.That(sheet.MoveCombinationUpCommand.CanExecute(null) && !sheet.MoveCombinationDownCommand.CanExecute(null),
            "Ordine ViewModel: nuova combinazione aggiunta in fondo e spostabile sopra");
        string beforeDispose = sheet.Data.ToJsonString(); sheet.Dispose(); sheet.MoveCombinationUpCommand.Execute(null);
        expect.That(!sheet.MoveCombinationUpCommand.CanExecute(null) && sheet.Data.ToJsonString() == beforeDispose,
            "Ordine ViewModel: foglio chiuso non modificabile");
    }
}
