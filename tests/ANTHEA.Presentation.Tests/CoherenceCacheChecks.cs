using System.Text.Json.Nodes;
using Anthea.Application.Documents;
using Anthea.Calculations;
using Anthea.Presentation.Projects;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// Step 3 of docs/sviluppo/progetto-sistema.md (4/11/2026): the coherence badge of every section from <see cref="ProjectCoherenceCache"/>
/// is the one SharedDataCoordinator.Coherence computes from scratch, on the project fixtures and on a project with differences, through a
/// series of edits read with the same cache, and after an undo (new objects with the same content: found in the cache).
/// </summary>
internal static class CoherenceCacheChecks
{
    internal static int Run()
    {
        var expect = new Expect();
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "ANTHEA.sln"))) root = Path.GetDirectoryName(root)!;
        int sections = 0, fixtures = 0;
        var failures = new List<string>();
        foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "tests", "fixtures"), "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".programma", StringComparison.Ordinal) || f.EndsWith(".json", StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            string text = File.ReadAllText(file);
            if (!text.Contains("\"progetti\"", StringComparison.Ordinal) || !text.Contains("\"tipo\"", StringComparison.Ordinal)) continue;
            JsonObject document;
            try { document = DocumentService.Open(file, []); }
            catch (Exception error) when (error is ArgumentException or System.Text.Json.JsonException) { continue; }
            if (document.S("tipo") != "progetti") continue;
            fixtures++;
            var cache = new ProjectCoherenceCache();
            cache.NewReading();
            foreach (var section in document.Array("progetti").OfType<JsonObject>().SelectMany(ProjectSharedData.Sections))
            {
                sections++;
                var (direct, failure) = Outcome(() => SharedDataCoordinator.Coherence(section));
                var (cached, cachedFailure) = Outcome(() => SharedDataCoordinator.Coherence(section, cache));
                if (failure is not null) failures.Add(Path.GetFileName(file) + " › " + section.S("nome") + ": " + failure);
                expect.That(direct == cached && failure == cachedFailure, "Coerenza dalla cache diversa: " + Path.GetFileName(file) + " › " + section.S("nome"));
            }
        }
        Console.WriteLine("Coerenza che fallisce anche senza cache (stesso errore con la cache): " + (failures.Count == 0 ? "nessuna" : string.Join("; ", failures)));
        expect.That(fixtures >= 2 && sections > fixtures, $"Progetti di prova letti: {fixtures}, sezioni {sections}");

        // A project with differences and warnings, edited step by step and read again with the same cache.
        var document2 = ProjectDocuments.CreateArchive();
        var project = ProjectDocuments.AddProject(document2, "Ponte");
        var materials = ProjectDocuments.AddSheet(project, "mat_calcestruzzo", "CLS di progetto");
        var pila = ProjectDocuments.AddSection(project, "Pila P1");
        var cls = ProjectDocuments.AddSheet(pila, "mat_calcestruzzo", "CLS pila");
        var sezione = ProjectDocuments.AddSheet(pila, "str_palo", "Sezione pila");
        var fusto = ProjectDocuments.AddSection(pila, "Fusto");
        ProjectDocuments.AddSheet(fusto, "str_palo", "Sezione fusto");
        ProjectDocuments.AddSheet(fusto, "geo_palo_verticale", "Palo");
        var spalla = ProjectDocuments.AddSection(project, "Spalla");
        ProjectDocuments.AddSheet(spalla, "geo_stratigrafia", "Stratigrafia");
        ProjectDocuments.AddSheet(spalla, "geo_palo_verticale", "Palo spalla");
        var shared = new ProjectCoherenceCache();
        void Same(string step)
        {
            shared.NewReading();
            foreach (var section in ProjectSharedData.Sections(project))
                expect.That(Equal(SharedDataCoordinator.Coherence(section), SharedDataCoordinator.Coherence(section, shared)), "Coerenza dalla cache diversa dopo «" + step + "»: " + section.S("nome"));
        }
        Same("progetto nuovo");
        materials["dati"] = DocumentService.CreateSheet("mat_calcestruzzo");
        cls["dati"] = DocumentService.CreateSheet("mat_calcestruzzo");
        cls["dati"]!["classe"] = "C40/50";
        Same("classe diversa");
        int before = shared.Misses;
        Same("nessuna modifica");
        expect.That(shared.Misses == before && shared.Hits > 0, "Coerenza senza modifiche: tutto dalla cache");
        sezione["nome"] = "Sezione pila rinominata";
        Same("rinomina");
        ProjectDocuments.AddSheet(fusto, "mat_calcestruzzo", "CLS fusto");
        Same("nuovo foglio");
        var moved = (JsonObject)spalla.Array("fogli")[1]!;
        spalla.Array("fogli").Remove(moved);
        fusto.Array("fogli").Add(moved);
        Same("spostamento");
        var copy = (JsonObject)project.DeepClone();
        before = shared.Misses;
        shared.NewReading();
        foreach (var section in ProjectSharedData.Sections(copy))
            expect.That(Equal(SharedDataCoordinator.Coherence(section), SharedDataCoordinator.Coherence(section, shared)), "Coerenza dalla cache diversa sulla copia: " + section.S("nome"));
        expect.That(shared.Misses == before, "Copia dello stesso contenuto (un annulla): tutto dalla cache");

        // A larger project with the data of the modules and different classes: badges with conflicts, warnings and cover checks.
        var large = ProjectDocuments.CreateArchive();
        string[] modules = ["mat_calcestruzzo", "str_palo", "geo_palo_verticale", "geo_stratigrafia"];
        string[] classes = ["C25/30", "C30/37", "C32/40"];
        int index = 0;
        foreach (string projectName in new[] { "Viadotto", "Sottopasso" })
        {
            var root2 = ProjectDocuments.AddProject(large, projectName);
            var top = ProjectDocuments.AddSheet(root2, "mat_calcestruzzo", "CLS generale");
            top["dati"] = DocumentService.CreateSheet("mat_calcestruzzo");
            for (int s = 0; s < 4; s++)
            {
                var section = ProjectDocuments.AddSection(root2, "Sezione " + s);
                var sub = ProjectDocuments.AddSection(section, "Sottosezione " + s);
                foreach (var container in new[] { section, sub })
                    foreach (string module in modules)
                    {
                        var sheet = ProjectDocuments.AddSheet(container, module, module + " " + index);
                        sheet["dati"] = DocumentService.CreateSheet(module);
                        if (module == "mat_calcestruzzo") sheet["dati"]!["classe"] = classes[index % classes.Length];
                        index++;
                    }
            }
        }
        var largeCache = new ProjectCoherenceCache();
        largeCache.NewReading();
        int badges = 0, warned = 0;
        foreach (var section in large.Array("progetti").OfType<JsonObject>().SelectMany(ProjectSharedData.Sections))
        {
            var direct = SharedDataCoordinator.Coherence(section);
            badges++;
            if (direct?.Warning == true) warned++;
            expect.That(direct == SharedDataCoordinator.Coherence(section, largeCache), "Coerenza dalla cache diversa nel progetto grande: " + section.S("nome"));
        }
        expect.That(badges > 10 && warned > 0, $"Progetto grande: {badges} sezioni, {warned} con differenze o avvisi");
        return expect.Count;
    }

    private static bool Equal(CoherenceBadge? a, CoherenceBadge? b) => a == b;

    /// <summary>The badge, or the type of the error that stopped it (the same with and without the cache).</summary>
    private static (CoherenceBadge? Badge, string? Failure) Outcome(Func<CoherenceBadge?> badge)
    {
        try { return (badge(), null); }
        catch (Exception error) when (error is NullReferenceException or ArgumentException or InvalidOperationException) { return (null, error.GetType().Name); }
    }
}
