using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Calculations;
using Anthea.Presentation.Modules.QuickSection;

namespace Anthea.Presentation.Tests;

internal static class QuickShearCombinationChecks
{
    internal static async Task Run(Expect expect, string folder, Func<JsonObject, QuickSectionSheetViewModel> open)
    {
        using var sheet = open(ConcreteQuickSection.Create());
        sheet.ToggleShearCommand.Execute(null);
        string first = sheet.ShearCombinationRows.Single().Id;
        expect.That(!sheet.ShearCombinationRows[0].RemoveCommand.CanExecute(null), "Taglio: l'ultima combinazione resta disponibile");
        sheet.ShearCombinationRows[0].N = "-200"; sheet.ShearCombinationRows[0].Vx = "20";
        sheet.ToggleShearCommand.Execute(null); sheet.ToggleShearCommand.Execute(null);
        expect.That(sheet.ShearCombinationRows.Count == 1 && sheet.ShearCombinationRows[0].Id == first && sheet.ShearCombinationRows[0].Vx == "20",
            "Taglio: riapertura senza duplicazioni o perdita dei valori");
        sheet.ShearCombinationRows[0].Condition = ConcreteQuickShear.WithoutLinks;
        sheet.AddShearCombinationCommand.Execute(null);
        sheet.ShearCombinationRows[0].IsSelected = true;
        expect.That(sheet.ShearWithoutLinks && sheet.ShearModel == "Verifica sezione non armata", "Taglio: modello blu associato alla tipologia della riga selezionata");
        sheet.ShearModel = "Verifica con armatura a taglio";
        expect.That(sheet.ResetShearConditionsCommand.CanExecute(null) && sheet.ShearCombinationRows[0].Condition == ConcreteQuickShear.WithoutLinks,
            "Taglio: modello modificabile senza cambiare la tipologia assegnata");
        sheet.ShearForm.Set("alpha_x", "75"); sheet.ShearForm.Set("cot_x", "1.5"); sheet.ShearForm.Set("rami_x", "3");
        var expected = (JsonObject)sheet.Data.DeepClone();
        foreach (var row in expected[ConcreteQuickSection.ViewKey]![ConcreteQuickShear.Key]!["azioni"]!.AsArray().OfType<JsonObject>()) row.Remove("modello");
        sheet.ResetShearConditionsCommand.Execute(null);
        expect.That(JsonNode.DeepEquals(sheet.Data, expected) && !sheet.ResetShearConditionsCommand.CanExecute(null) && sheet.ShearWithoutLinks,
            "Taglio: ripristino cambia soltanto il modello armato/non armato, conservando integralmente ogni altro dato");
        sheet.ShearAnchorageConfirmed = true;
        sheet.EffectiveStirrup = false;
        await sheet.PendingCalculation;
        expect.That(sheet.Current!.Shear!.Single(r => r.Id == first).Directions.Count == 2
            && sheet.Current.Shear.Single(r => r.Id != first).Error.Contains("diametro"),
            "Taglio: senza armatura calcolabile senza diametro trasversale; errore della riga armata isolato");
        sheet.ShearCombinationRows[1].IsSelected = true;
        sheet.ShearForm.Set(ConcreteQuickShear.Diameter, "8"); await sheet.PendingCalculation;
        foreach (var row in ConcreteQuickShear.Combinations(sheet.Data))
        {
            var (input, settings) = ConcreteQuickShear.Prepare(sheet.Data, row);
            var native = ConcreteShearAnalysis.Calculate(input, settings, settings["taglio"]!.AsObject(), row);
            var actual = sheet.Current!.Shear!.Single(r => r.Id == row.S("id"));
            expect.That(actual.Directions.Select(r => (r.VRd, r.Ratio)).SequenceEqual(native.Shear.Select(r => (r.VRd, r.Ratio))),
                "Taglio: modello per combinazione identico al motore " + actual.Condition);
        }
        var report = ConcreteQuickReportAnalysis.Create(sheet.Data).Report;
        expect.That(report.Shear!.Rows.Select(r => r.Condition).Distinct().Count() == 2, "Taglio: report distingue le due tipologie nella stessa sezione");
        string dataBefore = sheet.Data.ToJsonString();
        var legacy = (JsonObject)sheet.Data.DeepClone(); var legacyOptions = legacy[ConcreteQuickSection.ViewKey]![ConcreteQuickShear.Key]!.AsObject();
        legacyOptions["modello"] = ConcreteQuickShear.WithoutLinks;
        foreach (var row in legacyOptions["azioni"]!.AsArray().OfType<JsonObject>()) { row.Remove(ConcreteQuickShear.ConditionField); row["stato"] = "SLE"; }
        string legacyBefore = legacy.ToJsonString();
        expect.That(ConcreteQuickShear.Combinations(legacy).All(r => ConcreteQuickShear.Model(legacy, r) == ConcreteQuickShear.WithoutLinks)
            && ConcreteQuickShear.Calculate(legacy).Count == 2 && legacy.ToJsonString() == legacyBefore, "Taglio: vecchie righe ereditano il modello salvato, senza filtro SL né modifica in lettura");
        ConcreteQuickShearExcel.Row[] rows = [new("Armata α", -300.25, 85.5, -42), new("Non armata β", -120, -15, 8, ConcreteQuickShear.WithoutLinks)];
        var workbook = ConcreteQuickShearExcel.Write(rows);
        expect.That(ConcreteQuickShearExcel.Read(workbook).SequenceEqual(rows), "Excel taglio: tipologie, nomi e azioni esatti all'andata e ritorno");
        expect.Throws<ArgumentException>(() => ConcreteQuickShearExcel.Write([new("Errata", double.NaN, 1, 2)]), "Excel taglio: valore non finito rifiutato");
        expect.Throws<ArgumentException>(() => ConcreteQuickShear.ReplaceCombinations(sheet.Data, [new("Errata", 0, 1, 2, "SLU")]), "Excel taglio: tipologia non valida rifiutata prima di modificare i dati");
        expect.That(sheet.Data.ToJsonString() == dataBefore, "Excel taglio: errore non modifica il documento");
        sheet.ExcelFolder = Path.Combine(folder, "excel-taglio");
        sheet.ExportExcelCommand.Execute(null); string bendingPath = sheet.ExcelPath!; byte[] bendingBook = File.ReadAllBytes(bendingPath);
        sheet.ExportShearExcelCommand.Execute(null); string shearPath = sheet.ShearExcelPath!;
        expect.That(shearPath != bendingPath && File.Exists(shearPath), "Excel taglio: file indipendente da quello del momento");
        string bending = sheet.Data["combinazioni"]!.ToJsonString();
        File.WriteAllBytes(shearPath, workbook); sheet.ImportShearExcelCommand.Execute(null);
        expect.That(sheet.ShearCombinationRows.Select(r => (r.Name, r.Condition)).SequenceEqual(rows.Select(r => (r.Name, r.Condition)))
            && sheet.Data["combinazioni"]!.ToJsonString() == bending && File.ReadAllBytes(bendingPath).SequenceEqual(bendingBook), "Excel taglio: import sostituisce soltanto la tabella autonoma");
        sheet.ExportShearExcelCommand.Execute(null);
        expect.That(sheet.ShearExcelPath == shearPath && ConcreteQuickShearExcel.Read(File.ReadAllBytes(shearPath)).SequenceEqual(rows), "Excel taglio: successiva esportazione aggiorna lo stesso file");
        string beforeInvalid = sheet.Data.ToJsonString(); File.WriteAllText(shearPath, "file non valido"); sheet.ImportShearExcelCommand.Execute(null);
        expect.That(sheet.Data.ToJsonString() == beforeInvalid, "Excel taglio: file non valido lascia le combinazioni inalterate");
        sheet.IsReadOnly = true; string readOnly = sheet.Data.ToJsonString();
        sheet.ShearModel = "Verifica sezione non armata"; sheet.ShearCombinationRows[0].Condition = ConcreteQuickShear.WithoutLinks;
        expect.That(!sheet.ImportShearExcelCommand.CanExecute(null) && !sheet.ResetShearConditionsCommand.CanExecute(null) && sheet.Data.ToJsonString() == readOnly,
            "Taglio: modello, tipologia, importazione e ripristino protetti in sola lettura");
    }
}
