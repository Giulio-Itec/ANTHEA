using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Anthea.Calculations;
using Materiali;

internal static class MaterialPresentationChecks
{
    internal static int Run(string root)
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("it-IT");
            int checks = 0;
            void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks++; }
            string Text(string value) => Regex.Replace(value, @"\s+", " ").Trim();
            // U5 (14/10/2026, register F4-47): own wording citing only standards. The frozen report keeps the previous texts:
            // each must still be there and is replaced by the new one before the comparison; nothing else may change.
            (string Before, string After)[] u5 =
            [
                ("Cl 0,40: proposta per armatura ordinaria dagli esempi ATECAP, pp. 43–50; non è una classe dedotta dall’esposizione e va confermata nella prescrizione.",
                    MaterialSheetPresentation.CompositionChlorideNote),
                ("UNI 11104:2016, prospetto 5 (riportato in ATECAP 2020, p. 19). Nella UNI 11104:2025 in vigore (prospetto 6) i dosaggi minimi di cemento sono inferiori e per XF1 l’A/C massimo è 0,55: i valori adottati sono più restrittivi. Per esposizioni concomitanti: il minore A/C massimo e il maggiore dosaggio minimo. Valori senza applicazione del concetto k per le aggiunte (nota d).",
                    MaterialSheetPresentation.CompositionReference),
            ];
            foreach (string id in new[] { "default", "legacy-esposizioni" })
            {
                string directory = Path.Combine(root, "tests/fixtures/f3/expected/sheets/mat_calcestruzzo", id);
                var state = JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "calculated-data.json")))!.AsObject();
                var fixture = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "tests/fixtures/f3/inputs", "mat_calcestruzzo--" + id + ".json")))!["data"]!.AsObject();
                string sourceState = fixture.ToJsonString();
                var normalized = MaterialSheetInput.NormalizeState(fixture);
                Check(JsonNode.DeepEquals(state, normalized), "Preparazione materiali diversa dal riferimento storico");
                Check(JsonNode.DeepEquals(normalized, MaterialSheetInput.NormalizeState(normalized)), "Preparazione materiali non idempotente");
                Check(fixture.ToJsonString() == sourceState, "Preparazione materiali ha mutato la sorgente");
                string before = state.ToJsonString();
                string report = File.ReadAllText(Path.Combine(directory, "report.txt"));
                var cells = Regex.Matches(report, @"^\[T2 r(\d+) c1\] (.*?)\r?\n\[T2 r\1 c2\] (.*?)(?=^\[|^#|\z)", RegexOptions.Multiline | RegexOptions.Singleline)
                    .ToDictionary(m => m.Groups[2].Value.Trim(), m => Text(m.Groups[3].Value));
                foreach (var (previous, current) in u5)
                {
                    string[] keys = cells.Keys.Where(k => cells[k].Contains(Text(previous), StringComparison.Ordinal)).ToArray();
                    Check(keys.Length == 1, "Testo U5 non unico nel report congelato " + id + ": " + previous[..40]);
                    cells[keys[0]] = cells[keys[0]].Replace(Text(previous), Text(current), StringComparison.Ordinal);
                }
                var calculated = MaterialSheetPresentation.Evaluate(state);
                foreach (var row in calculated.ReportProperties())
                {
                    string label = row.Nome + (row.Simbolo.Length > 0 ? " · " + row.Simbolo : "") + (row.Unita.Length > 0 ? " [" + row.Unita + "]" : "");
                    Check(cells.TryGetValue(label, out string? expected), "Voce assente dal report congelato: " + label);
                    double? actualNumber = J.Number(JsonValue.Create(row.Valore)), expectedNumber = J.Number(JsonValue.Create(expected));
                    Check(actualNumber is not null && expectedNumber is not null ? actualNumber == expectedNumber : Text(row.Valore) == expected,
                        "Materiale diverso dal report pre-refactoring: " + id + "/" + row.Nome);
                }
                Check(before == state.ToJsonString(), "Presentazione ha modificato l'archivio materiali");
                var chapter = MaterialReportChapter.Create("mat_calcestruzzo", state);
                Check(chapter.Error is null && chapter.Derived!.Count == calculated.ReportProperties().Count, "Capitolo materiali incompleto");
            }
            var source = ModuleCatalog.CreateData("mat_calcestruzzo");
            var first = MaterialSheetPresentation.Evaluate(source);
            Check(first.Strength == "30,00" && first.CoverHeadline == "cnom = 35 mm", "Valori iniziali modificati");
            source["numeri"] = new JsonObject { ["diameter"] = "NaN" };
            var invalid = MaterialSheetPresentation.Evaluate(source);
            Check(invalid.CoverError && invalid.CoverHeadline == "Da completare" && invalid.BondValue == "Da completare", "Dati non validi mostrano risultati vecchi");
            Check(invalid.Strength == "30,00" && invalid.Composition["ratio"] == "0,60", "Errore locale nasconde risultati indipendenti");
            Check(MaterialReportChapter.Create("mat_calcestruzzo", source).Error is not null, "Report non segnala il dato incompleto");
            source["numeri"]!["diameter"] = "16";
            source["scelte"] = new JsonObject { ["deviationValue"] = "5 mm" };
            try { MaterialSheetPresentation.Evaluate(source); throw new Exception("Tolleranza non consentita accettata"); }
            catch (ArgumentException) { checks++; }
            source["scelte"]!["deviationControl"] = "Misura copriferri";
            Check(MaterialSheetPresentation.Evaluate(source).CoverHeadline == "cnom = 30 mm", "Tolleranza ammessa non applicata");
            source["esposizione_principale"] = "XF2";
            source["numeri"]!["aggregate"] = "16";
            Check(MaterialSheetPresentation.Evaluate(source).Composition["air"] == "5,0", "Valore aria del caso documentato alterato");
            Check(first.CoverHeadline == "cnom = 35 mm", "Presentazione precedente alias del documento");
            return checks;
        }
        finally { CultureInfo.CurrentCulture = culture; }
    }
}
