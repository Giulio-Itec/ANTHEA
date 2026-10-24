using System.Text;
using System.Text.Json.Nodes;
using Anthea.Application.Documents;
using Anthea.Calculations;
using X.Core;

/// <summary>
/// F5.13 G8: the archive of ui/check-contrast, tests/fixtures/f5/contrasto-progetti.programma. The pages of the projects (tree, overview,
/// «Confronto e avvisi», revisions) are audited on this small project. It is built with the code of the product (ProjectDocuments for the nodes and
/// the sheets, which take over the shared data as the user adds them; DocumentService.Validate and DocumentFile.Write for the file), not written by hand.
/// «Pila P1» is consistent and without notices: the audit makes the conflict (another class for the second concrete). «Pila P2» has one conflict and
/// one notice from the start: the exposure class of its concrete was changed to XC3 after the section of the pile took XC1 over, so the cover of the
/// section is «DA VERIFICARE».
/// The file is a document of version 1, as the shell keeps it in memory (without the native model of the RC section that DocumentService.Save adds): it
/// stays readable when the model format of the libraries changes. This check only reads it; with <see cref="Regenerate"/> set it writes it again.
/// </summary>
internal static class ContrastArchiveChecks
{
    internal const string Fixture = "tests/fixtures/f5/contrasto-progetti.programma";
    internal const string Regenerate = "ANTHEA_RIGENERA_ARCHIVIO_CONTRASTO";

    internal static JsonObject Build()
    {
        var archive = ProjectDocuments.CreateArchive();
        var project = ProjectDocuments.AddProject(archive, "Viadotto di prova"); project["id"] = "contrasto-progetto";
        JsonObject Section(string id, string name) { var section = ProjectDocuments.AddSection(project, name); section["id"] = id; return section; }
        JsonObject Sheet(JsonObject section, string id, string module, string name) { var sheet = ProjectDocuments.AddSheet(section, module, name); sheet["id"] = id; return sheet; }

        var first = Section("contrasto-pila-1", "Pila P1");
        Sheet(first, "contrasto-p1-cls", "mat_calcestruzzo", "CLS di riferimento");
        Sheet(first, "contrasto-p1-cls-2", "mat_calcestruzzo", "CLS seconda verifica");
        Sheet(first, "contrasto-p1-sezione", "str_palo", "Verifica sezione");

        var second = Section("contrasto-pila-2", "Pila P2");
        var concrete = Sheet(second, "contrasto-p2-cls", "mat_calcestruzzo", "Calcestruzzo della pila");
        Sheet(second, "contrasto-p2-sezione", "str_palo", "Sezione della pila");
        // The exposure of the concrete is changed after the section of the pile took it over: a conflict that nobody has aligned yet.
        concrete["dati"]!["esposizione_principale"] = "XC3"; concrete["dati"]!["esposizioni"] = new JsonArray("XC3");
        return archive;
    }

    internal static int Run(string root, string output)
    {
        int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks++; }
        var built = Build();
        DocumentService.Validate(built);
        string file = Path.Combine(root, Fixture.Replace('/', Path.DirectorySeparatorChar));
        if (Environment.GetEnvironmentVariable(Regenerate) == "1")
            Anthea.Reports.DocumentFile.Write(file, Encoding.UTF8.GetBytes(built.ToJsonString(J.Options)));
        Check(File.Exists(file), "Archivio di prova del contrasto assente: " + Fixture + " (si scrive con " + Regenerate + "=1)");

        // The file is the one the code of the product builds: opened as the shell opens it, equal to the built archive.
        var opened = DocumentService.Open(file);
        Check(JsonNode.DeepEquals(built, opened), Fixture + " diverso dall'archivio costruito da ContrastArchiveChecks.Build (si riscrive con " + Regenerate + "=1)");
        Check(opened.S("versione_base") == "" && opened.D("versione") == 1, "L'archivio di prova deve restare un documento di versione 1, senza il modello nativo della sezione");

        // What ui/check-contrast needs from it (ShellContrastViews.AuditProjects): a project with two sections.
        var project = opened.Array("progetti").OfType<JsonObject>().Single();
        var sections = project.Array("strutture").OfType<JsonObject>().ToArray();
        Check(sections.Length == 2, "L'archivio di prova ha due sezioni");
        var first = sections[0]; var second = sections[1];
        var concretes = first.Array("fogli").OfType<JsonObject>().Where(s => s.S("modulo_id") == "mat_calcestruzzo").ToArray();
        Check(concretes.Length == 2 && concretes[1].S("nome") == "CLS seconda verifica", "La prima sezione ha due calcestruzzi: il secondo cambia classe nella prova");
        Check(first.Array("fogli").OfType<JsonObject>().Any(s => s.S("modulo_id") == "str_palo"), "La prima sezione ha la sezione in c.a. che la prova apre in un foglio");

        // The first section is consistent, the second has one conflict and one notice from the start.
        Check(ProjectSharedData.Differences(first).Count == 0 && ProjectValidation.Warnings(first).Length == 0, "La prima sezione deve essere coerente e senza avvisi");
        Check(ProjectValidation.CoverChecks(first).Count > 0 && ProjectValidation.CoverChecks(first).All(c => c.Passed == true), "La prima sezione verifica il copriferro");
        var differences = ProjectSharedData.Differences(second);
        Check(differences.Select(d => d.Key).Distinct().SequenceEqual(["esposizione"]), "La seconda sezione ha un solo conflitto: la classe di esposizione");
        var warnings = ProjectValidation.Warnings(second);
        Check(warnings.Length == 1 && warnings[0].Contains("DA VERIFICARE", StringComparison.Ordinal), "La seconda sezione ha un solo avviso: copriferro da verificare");

        // It can be saved by the product (with the native model of the section) and read again; the versioned file is never written here.
        string copy = Path.Combine(output, "contrasto-progetti.programma");
        DocumentService.Save(copy, opened);
        var saved = DocumentService.Open(copy);
        Check(ProjectSharedData.Differences(saved.Array("progetti").OfType<JsonObject>().Single().Array("strutture").OfType<JsonObject>().Last()).Count == 1, "Il salvataggio dell'archivio di prova perde il conflitto");
        return checks;
    }
}
