using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Application.BridgeDesign;
using Anthea.Calculations;
using X.Core;

/// <summary>
/// Price analyses of the bridge price list (user decision of 14/10/2026): expected values by hand, general expenses and profit as
/// explicit terms, defaults of the list overridable per analysis, nesting, refusals in Italian, legacy catalogues unchanged.
/// </summary>
internal static class BridgePriceAnalysesChecks
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks++; }
        var source = BridgeConcept.Defaults();
        var vm = new PriceCatalogueViewModel(source);
        string legacy = Encoding.UTF8.GetString(vm.ExportCatalogue());
        Check(!legacy.Contains("Analyses") && !legacy.Contains("GeneralExpensesPercent") && !legacy.Contains("ProfitPercent"), "Catalogo senza analisi serializzato diversamente");
        void Entry(string code, string unit, string price)
        {
            vm.AddEntryCommand.Execute(null); var e = vm.SelectedEntry!; e.Code = code; e.Description = "Voce " + code; e.Unit = unit; e.Price = price;
        }
        Entry("cls-c25", "m³", "110"); Entry("operaio", "h", "30"); Entry("autopompa", "h", "90"); Entry("casseri", "m²", "25");
        vm.AddAnalysisCommand.Execute(null);
        Check(vm.GeneralExpensesPercent == "15" && vm.ProfitPercent == "10", "Percentuali del prezzario non rese esplicite");
        var cast = vm.SelectedAnalysis!; cast.Code = "cls-opera"; cast.Description = "Calcestruzzo in opera"; cast.Unit = "m³";
        cast.Components = "cls-c25 = 1,05; operaio = 0,8; autopompa = 0,1";
        // 115,50 + 24,00 + 9,00 = 148,50; SG 15 % = 22,275; utile 10 % di 170,775 = 17,0775; prezzo 187,8525
        Check(cast.UnitPrice == "187,85 €/m³", "Prezzo dell'analisi: " + cast.UnitPrice + " · " + vm.Status);
        foreach (string line in new[] { "Totale componenti: 148,50 €", "Spese generali 15 %: 22,28 €", "Utile d'impresa 10 % su componenti e spese generali: 17,08 €", "Prezzo unitario: 187,85 €/m³" })
            Check(vm.AnalysisBreakdown.Contains(line), "Voce del totale assente: " + line + "\n" + vm.AnalysisBreakdown);
        var deck = vm.Mappings.Single(m => m.WorkCode == "concrete_deck"); deck.EntryCode = "cls-opera";
        Check(deck.ResolvedPrice == "187,8525" && deck.Status == "Da analisi", "Associazione all'analisi: " + deck.ResolvedPrice + " · " + deck.Status);
        // Nested analysis with its own general expenses: 187,8525 + 8·25 = 387,8525; SG 13 % = 50,420825; utile 43,8273325; prezzo 482,1006575
        vm.AddAnalysisCommand.Execute(null); var column = vm.SelectedAnalysis!; column.Code = "pilastro"; column.Description = "Pilastro"; column.Unit = "m³";
        column.Components = "cls-opera = 1; casseri = 8"; column.GeneralExpenses = "13";
        Check(column.UnitPrice == "482,10 €/m³" && vm.AnalysisBreakdown.Contains("Spese generali 13 % (valore dell'analisi): 50,42 €"), "Analisi annidata: " + column.UnitPrice + "\n" + vm.AnalysisBreakdown);
        Check(vm.CanApply, vm.Status);
        vm.ApplyCommand.Execute(null);
        var rate = vm.Result!["snapshot"]!["Rates"]!.AsArray().Single(r => r!.S("WorkCode") == "concrete_deck")!;
        Check(rate["Price"]!.GetValue<decimal>() == 187.8525m, "Prezzo dell'analisi non passato al computo");
        // JSON keeps analyses and percentages; CSV and Excel keep the analyses of the editor
        var exported = vm.ExportCatalogue();
        var reopened = new PriceCatalogueViewModel(source); reopened.Import(exported, "catalogo.json");
        Check(reopened.Analyses.Count == 2 && reopened.GeneralExpensesPercent == "15" && reopened.Analyses.Single(a => a.Code == "pilastro").GeneralExpenses == "13", "Analisi perse nel catalogo JSON");
        reopened.Import(Encoding.UTF8.GetBytes(PriceCatalogueImport.Template()), "modello.csv");
        Check(reopened.Analyses.Count == 2 && !reopened.CanApply && reopened.Status.Contains("assente dal prezzario"), "Import CSV: " + reopened.Status);
        // Circular reference refused in Italian
        cast.Components = "pilastro = 1";
        Check(!vm.CanApply && vm.Status.Contains("riferimento circolare"), "Riferimento circolare: " + vm.Status);
        cast.Components = "cls-c25 = 1,05; operaio = 0,8; autopompa = 0,1";
        vm.GeneralExpensesPercent = "";
        Check(!vm.CanApply && vm.Status.Contains("spese generali e utile"), "Percentuali del prezzario mancanti accettate: " + vm.Status);
        return checks;
    }
}
