using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Application.Documents;
using Anthea.Application.Modules;
using Anthea.Calculations;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.QuickSection;

namespace Anthea.Presentation.Tests;

internal static class QuickCombinationResetChecks
{
    internal static async Task Run(Expect expect, string folder)
    {
        var data = ConcreteQuickSection.Create();
        ConcreteQuickSection.ReplaceCombinations(data,
            [new("Positiva", -1000, 400, 0), new("Negativa", -600, -180, 0), new("Sisma", -800, 300, 0, "SLV"),
             new("Rara", -300, 100, 0, "SLE"), new("Frequente", -200, 80, 0, "SLE_FREQ"), new("Quasi permanente", -100, 60, 0, "SLE_QP")]);
        var ids = ConcreteQuickSection.Combinations(data).Select(r => r.S("id")).ToArray();
        ConcreteQuickSection.WriteCreep(data, "2"); ConcreteQuickSection.WriteServiceOption(data, ConcreteQuickSection.ExposureKey, "XC3");
        ConcreteQuickSection.SelectCombination(data, ids[1]); ConcreteQuickSection.MoveCombination(data, ids[1], -1);
        var baseline = ConcreteQuickSection.Calculate((JsonObject)data.DeepClone());
        ConcreteQuickSection.WriteCombinationSetting(data, ids[0], ConcreteQuickSection.ModelField, "Elastico");
        ConcreteQuickSection.WriteCombinationSetting(data, ids[1], ConcreteQuickSection.SignField, "SLU+");
        ConcreteQuickSection.WriteCombinationSetting(data, ids[2], ConcreteQuickSection.ModelField, "Plastico");
        ConcreteQuickSection.WriteCombinationSetting(data, ids[2], ConcreteQuickSection.SignField, "SLU−");
        string actions = data["combinazioni"]!.ToJsonString(), input = data["input"]!.ToJsonString(), general = data["workspace_ca"]!.ToJsonString();
        string order = ConcreteQuickSection.View(data)[ConcreteQuickSection.OrderKey]!.ToJsonString();
        var altered = (JsonObject)data.DeepClone();
        using var sheet = new QuickSectionSheetViewModel(new(data, false, new Host(), Scripted.Services(), ModuleServices.Get(ConcreteQuickSection.Module), new CalculationLimiter(1)));
        await sheet.PendingCalculation;
        int edits = 0, notifications = 0; sheet.Modified += (_, _) => edits++;
        sheet.ResetCombinationSettingsCommand.CanExecuteChanged += (_, _) => notifications++;
        expect.That(sheet.ResetCombinationSettingsCommand.CanExecute(null) && sheet.Method == "Momento positivo", "Ripristino: disponibile con modifiche al modello o al segno");
        sheet.ResetCombinationSettingsCommand.Execute(null);
        expect.That(edits == 1 && !sheet.HasResults && sheet.Method == "Momento negativo" && sheet.Model == "Plastica"
            && !sheet.ResetCombinationSettingsCommand.CanExecute(null) && notifications > 0, "Ripristino: una modifica, segno da Mx negativo, risultati invalidati e comandi aggiornati");
        var rows = ConcreteQuickSection.Combinations(sheet.Data).ToDictionary(r => r.S("id"));
        expect.That(rows[ids[0]].S("modello") == "Plastico" && rows[ids[1]].S("modello") == "Plastico" && rows[ids[2]].S("modello") == "Elastico"
            && ids.Skip(3).All(id => rows[id].S("modello") == "") && rows.Values.All(r => r.S("segno") == ""),
            "Ripristino: SLU plastico, SLV elastico, SLE senza modello resistente e nessun segno forzato");
        expect.That(sheet.Data["combinazioni"]!.ToJsonString() == actions && sheet.Data["input"]!.ToJsonString() == input
            && sheet.Data["workspace_ca"]!.ToJsonString() == general && ConcreteQuickSection.View(sheet.Data)[ConcreteQuickSection.OrderKey]!.ToJsonString() == order
            && sheet.SelectedCombinationId == ids[1], "Ripristino: nomi, stati limite, azioni, geometria, viscosità, esposizione, ordine e selezione invariati");
        await sheet.PendingCalculation;
        expect.That(sheet.HasResults && sheet.Current!.Combinations.Select(r => (r.Id, r.Ratio, r.Passed)).SequenceEqual(baseline.Combinations.Select(r => (r.Id, r.Ratio, r.Passed))),
            "Ripristino: tutti i risultati tornano a quelli della sezione con i predefiniti originali");
        sheet.ResetCombinationSettingsCommand.Execute(null); expect.That(edits == 1, "Ripristino già applicato: nessuna nuova modifica");
        var document = DocumentService.CreateDocument(ConcreteQuickSection.Module); document["dati"] = sheet.Data.DeepClone();
        string path = Path.Combine(folder, "impostazioni-ripristinate.anthea"); DocumentService.Save(path, document);
        expect.That(JsonNode.DeepEquals(DocumentService.Open(path)["dati"], sheet.Data), "Ripristino: impostazioni conservate dopo salvataggio e riapertura");
        sheet.Reload(altered); sheet.IsReadOnly = true; string before = sheet.Data.ToJsonString();
        sheet.ResetCombinationSettingsCommand.Execute(null);
        expect.That(!sheet.ResetCombinationSettingsCommand.CanExecute(null) && sheet.Data.ToJsonString() == before, "Ripristino: sola lettura protetta");
        sheet.IsReadOnly = false;
        expect.That(sheet.ResetCombinationSettingsCommand.CanExecute(null), "Ripristino: disponibilità aggiornata uscendo dalla sola lettura");
        sheet.Dispose(); sheet.ResetCombinationSettingsCommand.Execute(null);
        expect.That(!sheet.ResetCombinationSettingsCommand.CanExecute(null) && sheet.Data.ToJsonString() == before, "Ripristino: foglio chiuso protetto");
    }

    private sealed class Host : ISheetHost
    {
        public void OpenModuleCopy(string module, JsonObject data, string name) => throw new InvalidOperationException();
        public void ReplaceSoil(JsonObject data) => throw new InvalidOperationException();
        public IReadOnlyList<SoilSource> SoilSources() => [];
        public void RequestReport() => throw new InvalidOperationException();
    }
}
