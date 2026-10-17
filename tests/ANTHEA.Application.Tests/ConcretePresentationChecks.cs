using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Application.Concrete;

internal static class ConcretePresentationChecks
{
    internal static int Run(string root)
    {
        int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks++; }
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("it-IT");
            string folder = Path.Combine(root, "tests/fixtures/f3/expected/sheets/str_palo");
            foreach (string fixture in Directory.GetDirectories(folder))
            {
                var result = JsonNode.Parse(File.ReadAllText(Path.Combine(fixture, "result.json")))!.AsObject();
                var data = result["dati"]!.AsObject();
                var input = data["input"]!.AsObject();
                var settings = data["workspace_ca"]!.AsObject();
                // The rows of every limit state, as the sheet and the report pass them (ConcreteSheetPreparation.PrepareReport): with an
                // element chosen the summary names the row of the most severe compression (fixtures of F5.10). Before F5.10 every
                // fixture left the element to choose, and the N of the SLU and SLV rows was enough.
                var actions = ConcreteDetailingAnalysis.AllRows(ConcreteSheetPreparation.ReadActions(data)).ToArray();
                string before = data.ToJsonString();
                var presentation = ConcreteDetailingPresentation.Calculate(input, settings, actions);
                Check(before == data.ToJsonString(), "Presentazione dei dettagli modifica il documento");
                Check(presentation.DetailsText.ReplaceLineEndings("\n") == result.S("dettagli_esito").ReplaceLineEndings("\n"),
                    "Dettagli diversi dalla cattura precedente: " + fixture);
                Check(presentation.AnchorageDetails.Text.ReplaceLineEndings("\n") == result.S("ancoraggio_esito").ReplaceLineEndings("\n"),
                    "Ancoraggi diversi dalla cattura precedente: " + fixture);
                var copy = (JsonObject)settings.DeepClone();
                presentation.ApplyDerivedValues(copy);
                Check(JsonNode.DeepEquals(copy, settings), "Applicazione dei valori derivati modifica dati già aggiornati");
            }
            var dataValid = ModuleCatalog.CreateData("str_palo");
            var settingsValid = SectionWorkspace.Prepare(dataValid);
            settingsValid["dettagli_costruttivi"]!["elemento"] = "Trave";
            settingsValid["ancoraggi"]!["lunghezza"] = "1200";
            settingsValid["ancoraggi"]!["confinamento"] = "Verificato sul disegno";
            var valid = ConcreteDetailingPresentation.Calculate(dataValid["input"]!.AsObject(), settingsValid, [J.Obj(("N", "-120"))]);
            Check(valid.Checks.Count > 0 && valid.Anchorage is not null && valid.DurabilityCover is not null, "Dettagli completi non disponibili senza WPF");
            Check(valid.AnchorageDetails.Items.Any(x => x.Title.StartsWith("Riscontro manuale") && x.Passed == true), "Conferma manuale persa");
            settingsValid["dettagli_costruttivi"]!["aggregato"] = "non valido";
            var partial = ConcreteDetailingPresentation.Calculate(dataValid["input"]!.AsObject(), settingsValid, []);
            Check(partial.Checks.Count == 0 && partial.Anchorage is not null && partial.Summary.Items.Single().Passed is null,
                "Errore nei dettagli compromette la verifica indipendente degli ancoraggi");
        }
        finally { CultureInfo.CurrentCulture = previous; }
        return checks;
    }
}
