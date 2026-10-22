using System.Text.Json.Nodes;
using Anthea.Application.Documents;
using Anthea.Calculations;
using Anthea.Presentation.Shell;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// F5.13 a1: texts of the shell (title of the window, heading of a sheet) with the values of MainWindow.UpdateTitle and
/// MainWindow.SheetHeading, the single calculation file and the files of the documents (save and open, examples folder).
/// </summary>
internal static class ShellDocumentChecks
{
    internal static int Run(string output)
    {
        var expect = new Expect();
        Texts(expect);
        Calculation(expect);
        Files(expect, output);
        return expect.Count;
    }

    private static void Texts(Expect expect)
    {
        string file = Path.Combine("C:\\Lavori", "Ponte.programma");
        expect.That(ShellTexts.WindowTitle(null, false, null) == "ANTHEA — Nuovo documento", "Titolo di un documento nuovo");
        expect.That(ShellTexts.WindowTitle(null, true, null) == "ANTHEA — Nuovo documento *", "Titolo di un documento nuovo modificato");
        expect.That(ShellTexts.WindowTitle(file, false, null) == "ANTHEA — Ponte.programma", "Titolo di un file");
        expect.That(ShellTexts.WindowTitle(file, true, 3) == "ANTHEA — Ponte.programma * · Rev. 3 · sola lettura", "Titolo di una revisione archiviata");

        // Heading: name or module name; nearest revised section above; the revision on screen first.
        var document = ProjectDocuments.CreateArchive();
        var project = ProjectDocuments.AddProject(document, "Ponte");
        var pier = ProjectDocuments.AddSection(project, "Pila P1");
        var foundation = ProjectDocuments.AddSection(pier, "Fondazione");
        var sheet = ProjectDocuments.AddSheet(foundation, "geo_palo_verticale", "Palo P1");
        expect.That(ShellTexts.SheetHeading(sheet, null) == "Palo P1", "Intestazione senza revisioni");
        ProjectRevisions.NewRevision(document, pier, "Prima emissione");
        expect.That(ShellTexts.SheetHeading(sheet, null) == "Palo P1 · Rev. 1", "Revisione della sezione superiore: " + ShellTexts.SheetHeading(sheet, null));
        ProjectRevisions.NewRevision(document, foundation, "Fondazione");
        ProjectRevisions.NewRevision(document, foundation, "Fondazione 2");
        expect.That(ShellTexts.SheetHeading(sheet, null) == "Palo P1 · Rev. 2", "Revisione della sezione più vicina: " + ShellTexts.SheetHeading(sheet, null));
        expect.That(ShellTexts.SheetHeading(sheet, 1) == "Palo P1 · Rev. 1 · sola lettura", "Intestazione di una revisione archiviata");
        sheet.Remove("nome");
        expect.That(ShellTexts.SheetHeading(sheet, null) == ShellTexts.ModuleName("geo_palo_verticale") + " · Rev. 2", "Intestazione con il nome del modulo");
        var single = DocumentService.CreateDocument("str_palo");
        expect.That(ShellTexts.SheetHeading(single, null) == ShellTexts.ModuleName("str_palo"), "Intestazione di un file singolo");
        expect.Throws<ArgumentException>(() => ShellTexts.SheetHeading(J.Obj(("modulo_id", "sconosciuto")), null), "Modulo sconosciuto accettato");

        // Revision numbers read as MainWindow reads them, (int)D("numero").
        expect.That(ShellTexts.RevisionNumber(J.Obj(("numero", 4))) == 4 && ShellTexts.RevisionNumber(J.Obj(("numero", 4.0))) == 4
            && ShellTexts.RevisionNumber(JsonNode.Parse("{\"numero\":7}")!.AsObject()) == 7 && ShellTexts.RevisionNumber(J.Obj(("numero", "5"))) == 5
            && ShellTexts.RevisionNumber(new JsonObject()) == 0 && ShellTexts.RevisionNumber(null) == 0, "Numero della revisione");
        expect.That(ShellTexts.RevisionDate(J.Obj(("data", "2026-10-22T09:05:00"))) == new DateTime(2026, 10, 22, 9, 5, 0).ToString("dd/MM/yyyy HH:mm")
            && ShellTexts.RevisionDate(J.Obj(("data", "ieri"))) == "ieri", "Data della revisione");
    }

    private static void Calculation(Expect expect)
    {
        var session = CalculationDocumentSession.New("geo_palo_verticale");
        expect.That(session.Kind == DocumentKind.Calculation && session.Module == "geo_palo_verticale" && ReferenceEquals(session.Sheet, session.Document)
            && ReferenceEquals(session.WorkingDocument, session.Document) && session.Document.S("tipo") == "calcolo", "File singolo nuovo");
        expect.That(session.Path is null && !session.IsDirty && !session.IsReadOnly && session.DisplayedRevision is null && session.Title == "ANTHEA — Nuovo documento", "Stato del file nuovo");
        int changes = 0;
        session.StateChanged += (_, _) => changes++;
        session.MarkDirty();
        expect.That(session.IsDirty && changes == 1 && session.Title == "ANTHEA — Nuovo documento *", "Modifica del file singolo");
        string path = Path.Combine(Path.GetTempPath(), "Cartella", "Palo.programma");
        session.MarkSaved(path);
        expect.That(!session.IsDirty && session.Path == path && changes == 2 && session.Title == "ANTHEA — Palo.programma", "File salvato");
        expect.That(session.IsFile(path.ToUpperInvariant()) && session.IsFile(Path.Combine(Path.GetTempPath(), "Cartella", ".", "Palo.programma"))
            && !session.IsFile(Path.Combine(Path.GetTempPath(), "Palo.programma")), "Stesso file");
        expect.That(!CalculationDocumentSession.New("str_palo").IsFile(path), "Documento nuovo con un file");
        expect.Throws<ArgumentException>(() => new CalculationDocumentSession(ProjectDocuments.CreateArchive()), "Archivio progetti come file singolo");

        // The copy of a sheet in another module outside a project: unsaved, named, with a copy of the data (MainWindow.OpenModuleCopy).
        var data = DocumentService.CreateSheet("str_palo");
        var copy = CalculationDocumentSession.Copy("str_palo", data, "Sezione del muro");
        expect.That(copy.IsDirty && copy.Path is null && copy.Document.S("nome") == "Sezione del muro" && copy.Module == "str_palo"
            && JsonNode.DeepEquals(copy.Document["dati"], data) && !ReferenceEquals(copy.Document["dati"], data), "Copia verso un altro modulo");
        expect.That(string.Join(",", copy.Document.Select(p => p.Key)) == "formato,versione,tipo,modulo_id,dati,nome", "Ordine delle chiavi della copia");
        expect.Throws<ArgumentException>(() => CalculationDocumentSession.Copy("sconosciuto", data, "x"), "Copia verso un modulo sconosciuto");
    }

    private static void Files(Expect expect, string output)
    {
        string root = Path.Combine(output, "shell-file");
        if (Directory.Exists(root)) Directory.Delete(root, true);
        string examples = Path.Combine(root, "Esempi");
        Directory.CreateDirectory(examples);
        var files = new DocumentFiles(examples);
        expect.That(files.IsInExamples(Path.Combine(examples, "str_palo", "a.anthea")) && files.IsInExamples(Path.Combine(examples.ToUpperInvariant(), "x.anthea"))
            && !files.IsInExamples(examples) && !files.IsInExamples(Path.Combine(root, "EsempiAltro", "x.anthea")) && !files.IsInExamples(Path.Combine(root, "x.anthea")),
            "Cartella degli esempi");
        expect.That(files.OpenExampleCopy(Path.Combine(root, "x.anthea")) is null, "Copia di un file fuori dagli esempi");
        expect.That(DocumentFiles.Installed().IsInExamples(Path.Combine(AppContext.BaseDirectory, "Esempi", "a.anthea")), "Esempi installati");

        // Save and open through DocumentService: the same document back.
        var document = DocumentService.CreateDocument("mat_calcestruzzo");
        string path = Path.Combine(root, "Calcestruzzo.programma");
        files.Save(path, document);
        var opened = files.Open(path);
        expect.That(JsonNode.DeepEquals(opened, document), "Documento salvato e riaperto diverso");
        expect.Throws<ArgumentException>(() => files.Save(Path.Combine(root, "Rotto.programma"), J.Obj(("formato", "X"))), "Documento non valido salvato");
    }
}
