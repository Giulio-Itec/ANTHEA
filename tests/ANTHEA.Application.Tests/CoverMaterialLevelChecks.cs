using System.IO.Compression;
using System.Text.Json.Nodes;
using Anthea.Application.Reporting;
using Anthea.Calculations;
using X.Core;

/// <summary>
/// P10 of the note of step 6 (gpc-f6-claude/INT/rapporti/passo6-progetto-20261115.md, defect R1; 16/11/2026): the check of the cover uses
/// the Materiali of the highest level that has any, as the guide says, and a notice names the Materiali of a nearer level that it ignores
/// when that one gives another outcome or another minimum. The outcome of the check does not change. Scenario of the note: project
/// «Viadotto» with «CLS generale» (casseratura), section «Pila 1» with «CLS pali» (getto direttamente su terra), «Palo P1» with 60 mm.
/// </summary>
internal static class CoverMaterialLevelChecks
{
    internal static async Task<int> Run(string output)
    {
        int checks = 0;
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            checks++;
        }

        // The scenario: the check says RISPETTATO with «CLS generale», and the notice says what «CLS pali» gives.
        var (archive, project, general, pila, piles, pile) = Scenario("Direttamente su terra");
        double usedMinimum = Materiali.MaterialCover.Required(general["dati"]!.AsObject(), 30), nearMinimum = Materiali.MaterialCover.Required(piles["dati"]!.AsObject(), 30);
        Check(nearMinimum >= 75 && usedMinimum < 60, $"Scenario: minimi {usedMinimum} e {nearMinimum} mm non come nella nota");
        string before = archive.ToJsonString();
        var cover = ProjectValidation.CoverChecks(pila, pile);
        string notice = $"Palo P1: il controllo del copriferro usa Materiali «CLS generale» del progetto «Viadotto», il livello più alto (minimo da Materiali {usedMinimum:0.##} mm — RISPETTATO), " +
            $"e non «CLS pali» della sezione «Pila 1», più vicino al foglio. Con «CLS pali»: minimo da Materiali {nearMinimum:0.##} mm — NON RISPETTATO.";
        Check(cover.Count == 2 && cover[0].Passed == true && cover[0].Text.Contains($"minimo da Materiali {usedMinimum:0.##} mm — RISPETTATO (CLS generale", StringComparison.Ordinal),
            "Scenario: il controllo deve restare RISPETTATO con «CLS generale»: " + string.Join(" | ", cover.Select(c => c.Text)));
        Check(cover[1] == new ProjectValidation.CoverStatus(notice, null), "Scenario: avviso diverso dall'atteso: " + cover[1].Text);
        var alone = (JsonObject)project.DeepClone();
        ProjectSharedData.Sections(alone).ElementAt(1).Array("fogli").RemoveAt(0);
        var aloneCover = ProjectValidation.CoverChecks(ProjectSharedData.Sections(alone).ElementAt(1));
        Check(aloneCover.Count == 1 && aloneCover[0] == cover[0], "Scenario: l'esito del controllo cambia per la sola presenza di «CLS pali»");
        Check(ProjectValidation.Warnings(pila).Contains(notice) && ProjectValidation.Warnings(project).Contains(notice) && !ProjectValidation.Warnings(pila).Contains(cover[0].Text),
            "Scenario: l'avviso deve arrivare agli avvisi della sezione e del progetto (riepilogo, badge, report), il controllo rispettato no");
        var cache = new ProjectCoherenceCache();
        cache.NewReading();
        Check(cache.Of(pila).Warnings.SequenceEqual(ProjectValidation.Warnings(pila)) && cache.Of(project).Warnings.SequenceEqual(ProjectValidation.Warnings(project)),
            "Scenario: coerenza in cache diversa dalla diretta");
        Check(archive.ToJsonString() == before, "Scenario: il controllo modifica il progetto");

        // The notice reaches the report («Avvisi»).
        string path = Path.Combine(output, "report-avviso-materiali.docx");
        await ProjectReportService.WriteSnapshotAsync(ProjectReportService.Snapshot(pila), path, new NoChapters());
        using (var package = ZipFile.OpenRead(path))
        using (var reader = new StreamReader(package.GetEntry("word/document.xml")!.Open()))
            Check(reader.ReadToEnd().Contains(notice, StringComparison.Ordinal), "Scenario: l'avviso deve comparire nel report");

        // The names of the levels are in the notice: renaming a section changes it, in the cache too.
        pila["nome"] = "Pila 1 bis";
        cache.NewReading();
        Check(cache.Of(project).Warnings.SequenceEqual(ProjectValidation.Warnings(project)) && cache.Of(project).Warnings.Any(w => w.Contains("della sezione «Pila 1 bis»", StringComparison.Ordinal)),
            "Scenario: la cache non segue la rinomina della sezione");
        pila["nome"] = "Pila 1";

        // A deeper sheet through an empty section: one notice for each nearer Materiali that differs.
        var empty = ProjectDocuments.AddSection(pila, "Fondazione");
        var deep = ProjectDocuments.AddSheet(ProjectDocuments.AddSection(empty, "Plinto"), "str_palo", "Palo P2");
        deep["dati"]!["input"]!["cover_mm"] = 60;
        var deepCover = ProjectValidation.CoverChecks(project, deep);
        Check(deepCover.Count == 2 && deepCover[0].Passed == true && deepCover[1].Passed is null && deepCover[1].Text.StartsWith("Palo P2: ", StringComparison.Ordinal)
            && deepCover[1].Text.Contains("della sezione «Pila 1»", StringComparison.Ordinal), "Foglio più in basso: avviso atteso attraverso la sezione vuota");

        // Without conflict: no nearer Materiali, or a nearer Materiali that differs only in data that do not change outcome or minimum.
        {
            var (_, _, _, section, nearer, sheet) = Scenario("Casseratura");
            section.Array("fogli").Remove(nearer);
            var only = ProjectValidation.CoverChecks(section, sheet);
            Check(only.Count == 1 && only[0].Passed == true, "Senza Materiali più vicini: un solo controllo, nessun avviso");
        }
        {
            var (_, _, top, section, nearer, sheet) = Scenario("Casseratura");
            nearer["dati"]!["scelte"]!["consistency"] = "S4";
            Check(!JsonNode.DeepEquals(top["dati"], nearer["dati"]), "Senza conflitto: i due Materiali devono essere diversi");
            var same = ProjectValidation.CoverChecks(section, sheet);
            Check(same.Count == 1 && same[0].Passed == true && ProjectValidation.Warnings(section).Length == ProjectSharedData.Limitations(section).Distinct().Count(),
                "Materiali più vicino con lo stesso esito e lo stesso minimo: nessun avviso: " + string.Join(" | ", same.Select(c => c.Text)));
        }
        // Equal Materiali on two levels: no notice.
        {
            var (_, _, top, section, nearer, sheet) = Scenario("Casseratura");
            Check(JsonNode.DeepEquals(top["dati"], nearer["dati"]), "Materiali uguali: i due livelli devono avere gli stessi dati");
            var equal = ProjectValidation.CoverChecks(section, sheet);
            Check(equal.Count == 1 && equal[0].Passed == true && !ProjectValidation.Warnings(section).Any(w => w.Contains("più vicino al foglio", StringComparison.Ordinal)),
                "Materiali uguali su due livelli: nessun avviso");
        }
        // Another minimum with the same outcome: the notice says so (the rule is «esito o minimo diverso»).
        {
            var (_, _, _, section, nearer, sheet) = Scenario("Terreno preparato");
            double minimum = Materiali.MaterialCover.Required(nearer["dati"]!.AsObject(), 30);
            var other = ProjectValidation.CoverChecks(section, sheet);
            Check(minimum >= 40 && minimum != usedMinimum && minimum < 60, $"Minimo diverso: {minimum} mm non come atteso");
            Check(other.Count == 2 && other[0].Passed == true && other[1].Passed is null
                && other[1].Text.EndsWith($"Con «CLS pali»: minimo da Materiali {minimum:0.##} mm — RISPETTATO.", StringComparison.Ordinal),
                "Minimo diverso con lo stesso esito: avviso atteso: " + string.Join(" | ", other.Select(c => c.Text)));
        }
        return checks;
    }

    /// <summary>Project «Viadotto» with «CLS generale» (C30/37, XC2, 50 anni, casseratura) and section «Pila 1» with «CLS pali» (the same,
    /// with the surface of the cast <paramref name="ground"/>) and «Palo P1» (Sezione in c.a., 60 mm, data inherited from the project).</summary>
    private static (JsonObject Archive, JsonObject Project, JsonObject General, JsonObject Pila, JsonObject Piles, JsonObject Pile) Scenario(string ground)
    {
        var archive = ProjectDocuments.CreateArchive();
        var project = ProjectDocuments.AddProject(archive, "Viadotto");
        var general = ProjectDocuments.AddSheet(project, "mat_calcestruzzo", "CLS generale");
        var data = general["dati"]!.AsObject();
        data["classe"] = "C30/37"; data["esposizione_principale"] = "XC2"; data["esposizioni"] = new JsonArray("XC2");
        data["scelte"]!["life"] = "50 anni"; data["scelte"]!["ground"] = "Casseratura";
        var pila = ProjectDocuments.AddSection(project, "Pila 1");
        var piles = ProjectDocuments.AddSheet(pila, "mat_calcestruzzo", "CLS pali");
        piles["dati"] = data.DeepClone();
        piles["dati"]!["scelte"]!["ground"] = ground;
        var pile = ProjectDocuments.AddSheet(pila, "str_palo", "Palo P1");
        pile["dati"]!["input"]!["cover_mm"] = 60;
        return (archive, project, general, pila, piles, pile);
    }

    /// <summary>The chapters are not needed: every sheet is recorded as incomplete, the warnings remain.</summary>
    private sealed class NoChapters : IProjectReportChapterBuilder
    {
        public Task<ReportProject.SheetContent> BuildAsync(JsonObject sheet, CancellationToken cancellation) => throw new ArgumentException("Capitolo non necessario alla prova");
    }
}
