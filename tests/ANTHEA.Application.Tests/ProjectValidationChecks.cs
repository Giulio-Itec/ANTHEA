using System.Text.Json.Nodes;
using Anthea.Application.Documents;
using Anthea.Calculations;
using X.Core;

internal static class ProjectValidationChecks
{
    internal static int Run(string root)
    {
        int checks = 0;
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            checks++;
        }

        // The captured file has anonymized data references: opening with diagnosis leaves its sheets
        // quarantined, without dati. The overview must still be readable, without inventing valid data.
        string fixture = Path.Combine(root, "tests", "fixtures", "f5-shell", "cattura-mainwindow", "13-file-salvato.json");
        var problems = new List<DocumentProblem>();
        var document = DocumentService.Open(fixture, problems);
        var project = document.Array("progetti").OfType<JsonObject>().Single();
        Check(problems.Count > 0, "La fixture deve aprirsi con diagnosi dei riferimenti non risolti");
        Check(ProjectSharedData.SubtreeSheets(project).Any(s => s.S("modulo_id") == "mat_calcestruzzo" && s["dati"] is null),
            "La fixture deve riprodurre Materiali senza dati");
        string before = document.ToJsonString();
        var cache = new ProjectCoherenceCache();
        cache.NewReading();
        foreach (var section in ProjectSharedData.Sections(project))
        {
            var cover = ProjectValidation.CoverChecks(section);
            Check(cover.Count > 0 && cover.All(c => c.Passed is null && c.Text.Contains("non verificabile", StringComparison.Ordinal)),
                "Materiali senza dati: il copriferro resta non verificabile in " + section.S("nome"));
            var warnings = ProjectValidation.Warnings(section);
            Check(cover.All(c => warnings.Contains(c.Text)), "Gli avvisi del copriferro devono arrivare alla copertina e al report");
            Check(cache.Of(section).Warnings.SequenceEqual(warnings), "La cache deve restituire gli stessi avvisi della validazione diretta");
        }
        cache.NewReading();
        foreach (var section in ProjectSharedData.Sections(project))
            Check(cache.Of(section).Warnings.SequenceEqual(ProjectValidation.Warnings(section)), "La seconda lettura della cache conserva gli avvisi");
        Check(document.ToJsonString() == before, "Validare la fixture non deve riempire o cambiare i fogli in quarantena");

        // Missing and explicit-null data, including an inherited material. An empty material must not
        // stop the other material's check, nor turn a previously computable check into a positive one.
        foreach (string module in new[] { "str_palo", PaloOrizzontale.Module, RetainingWall.Module })
        foreach (bool explicitNull in new[] { false, true })
        {
            var archive = ProjectDocuments.CreateArchive();
            var owner = ProjectDocuments.AddProject(archive, "Prova copriferro");
            var valid = ProjectDocuments.AddSheet(owner, "mat_calcestruzzo", "CLS valido");
            var empty = ProjectDocuments.AddSheet(owner, "mat_calcestruzzo", "CLS senza dati");
            var section = ProjectDocuments.AddSection(owner, "Elemento");
            var sheet = ProjectDocuments.AddSheet(section, module);
            var expected = ProjectValidation.CoverChecks(section, sheet).First();
            if (explicitNull) empty["dati"] = null;
            else empty.Remove("dati");
            string input = archive.ToJsonString();

            var cover = ProjectValidation.CoverChecks(section, sheet);
            Check(cover.Count == 2 && cover[0] == expected, module + ": la scheda valida mantiene il proprio esito");
            Check(cover[1].Passed is null && cover[1].Text.Contains("non verificabile", StringComparison.Ordinal),
                module + ": Materiali senza dati deve produrre un avviso");
            Check(ProjectValidation.Warnings(owner).Contains(cover[1].Text), module + ": avviso anche nel contenitore superiore");
            var localCache = new ProjectCoherenceCache();
            localCache.NewReading();
            Check(localCache.Of(section).Warnings.SequenceEqual(ProjectValidation.Warnings(section)),
                module + ": coerenza diretta e in cache con Materiali vuoto");
            Check(archive.ToJsonString() == input, module + ": validazione senza mutazioni");

            owner.Array("fogli").Remove(empty);
            owner.Array("fogli").Insert(0, empty);
            var reordered = ProjectValidation.CoverChecks(section, sheet);
            Check(reordered.Count == 2 && reordered[0].Passed is null && reordered[1] == expected,
                module + ": il materiale vuoto non interrompe i controlli dei materiali successivi");

            // Filling the same sheet must invalidate the cached warning.
            empty["dati"] = valid["dati"]!.DeepClone();
            localCache.NewReading();
            Check(localCache.Of(section).Warnings.SequenceEqual(ProjectValidation.Warnings(section))
                && !localCache.Of(section).Warnings.Contains(cover[1].Text),
                module + ": completare Materiali rimuove l'avviso anche dalla cache");
        }
        return checks;
    }
}
