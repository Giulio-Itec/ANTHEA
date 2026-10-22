using System.Text.Json.Nodes;
using Anthea.Application.Documents;
using Anthea.Calculations;
using Anthea.Presentation.Projects;
using Anthea.Presentation.Shell;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// F5.13 a2: the project session without WPF. Structure (names, conditions, questions and indices of MainWindow, ProjectHierarchy,
/// ProjectSectionReorder, ProjectMovePreview), history (undo and redo after a confirmation of the open sheet) and revisions (preview
/// read-only of a snapshot, return, deletion that can be undone), with the expectations of the historic code and of the WPF checks
/// ProjectRevisionNavigationSmokeChecks and ProjectRevisionDeletionSmokeChecks.
/// </summary>
internal static class ProjectSessionChecks
{
    internal static int Run()
    {
        var expect = new Expect();
        Structure(expect);
        Moves(expect);
        History(expect);
        Revisions(expect);
        return expect.Count;
    }

    /// <summary>A project session with a counter of the confirmations of the open sheet and a record of the changes.</summary>
    private sealed class Probe
    {
        internal Probe(ScriptedConfirmations? confirmations = null, JsonObject? document = null)
        {
            Confirmations = confirmations ?? new ScriptedConfirmations();
            Session = new ProjectSession(document ?? ProjectDocuments.CreateArchive(), null, false, Confirmations, () => Commits++);
            Session.Changed += (_, e) => Changes.Add((e.Change, e.Node));
        }

        internal ScriptedConfirmations Confirmations { get; }
        internal ProjectSession Session { get; }
        internal int Commits { get; private set; }
        internal List<(ProjectChange Change, JsonObject? Node)> Changes { get; } = [];
    }

    private static void Structure(Expect expect)
    {
        expect.Throws<ArgumentException>(() => _ = new ProjectSession(DocumentService.CreateDocument("str_palo"), null, false, new ScriptedConfirmations(), () => { }), "File singolo come progetto");
        var probe = new Probe(new ScriptedConfirmations().Then(false).Then(true).Then(true));
        var session = probe.Session;
        expect.That(session.Kind == DocumentKind.Project && !session.IsDirty && !session.IsReadOnly && session.History.Tracks(session.Document)
            && !session.CanUndo && !session.CanRedo && ReferenceEquals(session.WorkingDocument, session.Document), "Progetto nuovo");

        var first = session.AddProject()!;
        var second = session.AddProject()!;
        expect.That(first.S("nome") == "Progetto 1" && second.S("nome") == "Progetto 2" && probe.Commits == 2 && session.IsDirty && session.CanUndo
            && session.History.UndoCount == 2, "Nuovi progetti: nomi, conferme del foglio o storia");
        expect.That(probe.Changes.Count == 2 && probe.Changes.All(c => c.Change == ProjectChange.Structure) && ReferenceEquals(probe.Changes[1].Node, second), "Eventi dei nuovi progetti");
        var pier = session.AddSection(first)!;
        var abutment = session.AddSection(first)!;
        var nested = session.AddSection(pier)!;
        expect.That(pier.S("nome") == "Sezione 1" && abutment.S("nome") == "Sezione 2" && nested.S("nome") == "Sezione 1" && probe.Commits == 5, "Nomi delle sezioni");

        // Sheets only in a project or a section (MainWindow.AddSheetTo), with the name of the module.
        var material = session.AddSheet("mat_calcestruzzo", pier)!;
        expect.That(material.S("modulo_id") == "mat_calcestruzzo" && material.S("nome") == ShellTexts.ModuleName("mat_calcestruzzo") + " 1"
            && material["dati"] is JsonObject && ProjectSession.CanHoldSheets(pier) && ProjectSession.CanHoldSheets(first) && !ProjectSession.CanHoldSheets(material)
            && !ProjectSession.CanHoldSheets(null), "Nuovo foglio");
        expect.Throws<ArgumentException>(() => session.AddSheet("mat_calcestruzzo", material), "Foglio dentro un foglio");

        // Rename: trimmed; nothing when empty or unchanged; no confirmation of the open sheet.
        int commits = probe.Commits, undo = session.History.UndoCount;
        expect.That(session.Rename(pier, "  Pila P1  ") && pier.S("nome") == "Pila P1" && session.History.UndoCount == undo + 1 && probe.Commits == commits, "Rinomina");
        expect.That(!session.Rename(pier, "Pila P1") && !session.Rename(pier, "   ") && !session.Rename(pier, null!) && session.History.UndoCount == undo + 1, "Rinomina senza cambiamenti");

        // Duplicate: after the section, «nome copia n», new identifiers, no history.
        ProjectRevisions.NewRevision(session.Document, pier, "Prima");
        var copy = session.Duplicate(pier)!;
        var siblings = first.Array("strutture");
        expect.That(copy.S("nome") == "Pila P1 copia 1" && siblings.IndexOf(copy) == siblings.IndexOf(pier) + 1 && copy.S("id") != pier.S("id")
            && copy["revisioni"] is null && copy["revisione"] is null && ProjectSharedData.SubtreeSheets(copy).Single().S("id") != material.S("id"), "Duplica sezione");
        expect.That(session.Duplicate(session.Document) is null, "Duplica senza fratelli");

        // Delete: the question first («Elimina»), then the open sheet confirmed and the node removed; the overview goes to the section.
        commits = probe.Commits;
        expect.That(!session.Delete(material, out var overview) && overview is null && pier.Array("fogli").Contains(material) && probe.Commits == commits, "Eliminazione rifiutata");
        expect.That(session.Delete(material, out overview) && ReferenceEquals(overview, pier) && !pier.Array("fogli").Contains(material) && probe.Commits == commits + 1, "Eliminazione di un foglio");
        expect.That(session.Delete(second, out overview) && overview is null && !session.Document.Array("progetti").Contains(second), "Eliminazione di un progetto");
        expect.That(probe.Confirmations.Asked.Count == 3 && probe.Confirmations.Asked.All(q => q == ProjectSession.DeleteTitle + ": " + ProjectSession.DeleteQuestion)
            && ProjectSession.DeleteQuestion == "Eliminare l'elemento selezionato e i suoi contenuti dal documento? Il file su disco resta invariato fino al salvataggio.", "Domanda di eliminazione");

        // Drop rules (ProjectHierarchy): modules and sheets only on projects and sections of this document.
        var sheet = session.AddSheet("str_palo", abutment)!;
        expect.That(session.CanDropModule(pier, "str_palo") && session.CanDropModule(first, "mat_calcestruzzo") && !session.CanDropModule(sheet, "str_palo")
            && !session.CanDropModule(pier, "sconosciuto"), "Moduli trascinabili");
        expect.That(session.CanDropSheet(pier, sheet) && !session.CanDropSheet(abutment, sheet) && !session.CanDropSheet(sheet, sheet)
            && !session.CanDropSheet(pier, (JsonObject)sheet.DeepClone()), "Fogli trascinabili");
        var other = session.AddSheet("str_palo", abutment)!;
        expect.That(session.CanReorderSheet(other, sheet) && !session.CanReorderSheet(sheet, sheet) && !session.CanReorderSheet(other, abutment), "Fogli riordinabili");

        // CopySheet: the copy of a sheet in the section of the sheet that asked it (MainWindow.OpenModuleCopy).
        var data = DocumentService.CreateSheet("str_palo");
        var copied = session.CopySheet("str_palo", abutment, "Sezione del muro", data)!;
        expect.That(copied.S("nome") == "Sezione del muro 1" && JsonNode.DeepEquals(copied["dati"], data) && !ReferenceEquals(copied["dati"], data), "Copia nel progetto");
        expect.That(session.CopySheet("str_palo", abutment, "Sezione del muro", data)!.S("nome") == "Sezione del muro 2", "Seconda copia nel progetto");

        // ChangeData: the open sheet confirmed first; a change modifies the document and is announced.
        commits = probe.Commits; undo = session.History.UndoCount; probe.Changes.Clear();
        expect.That(!session.ChangeData(() => false) && probe.Commits == commits + 1 && probe.Changes.Count == 0 && session.History.UndoCount == undo, "Cambio di dati senza modifiche");
        expect.That(session.ChangeData(() => { sheet["dati"]!["nota_prova"] = "x"; return true; }, sheet) && probe.Changes.Single() == (ProjectChange.Data, sheet)
            && session.History.UndoCount == undo + 1, "Cambio di dati");
    }

    private static void Moves(Expect expect)
    {
        var probe = new Probe(new ScriptedConfirmations().Then(false).Then(true).Then(true));
        var session = probe.Session;
        var project = session.AddProject()!;
        var left = session.AddSection(project)!;
        var right = session.AddSection(project)!;
        var a = session.AddSheet("mat_calcestruzzo", left)!;
        var b = session.AddSheet("str_palo", left)!;
        var c = session.AddSheet("str_palo", left)!;
        var list = left.Array("fogli");
        string Order() => string.Join(",", list.Select(n => ReferenceEquals(n, a) ? "a" : ReferenceEquals(n, b) ? "b" : "c"));

        // Same list: the index of MainWindow.MoveProjectSheet, nothing (no confirmation) for a move in place.
        int commits = probe.Commits;
        expect.That(!session.MoveSheet(a, left) && !session.MoveSheet(a, left, a) && !session.MoveSheet(a, left, b, false) && !session.MoveSheet(b, left, a, true)
            && probe.Commits == commits && Order() == "a,b,c", "Spostamento senza effetto");
        expect.That(session.MoveSheet(a, left, c, true) && Order() == "b,c,a" && probe.Commits == commits + 1, "Spostamento dopo l'ultimo");
        expect.That(session.MoveSheet(a, left, b, false) && Order() == "a,b,c", "Spostamento prima del primo");
        expect.That(!session.MoveSheet(a, left, (JsonObject)b.DeepClone()), "Ancora fuori dalla lista");

        // Another section: the data of the destination inherited; a change of common data asks first («Annulla» keeps the sheet).
        var concrete = session.AddSheet("mat_calcestruzzo", right)!;
        concrete["dati"]!["classe"] = "C40/50";
        expect.That(!session.MoveSheet(a, right) && left.Array("fogli").Contains(a) && probe.Confirmations.Asked.Single() == "spostamento: " + right.S("nome"), "Spostamento annullato");
        expect.That(session.MoveSheet(a, right) && right.Array("fogli").Contains(a) && a["dati"].S("classe") == "C40/50", "Spostamento con eredità dei dati");

        // Sections: reorder among siblings (projects too), nest with the question when common data change.
        var third = session.AddSection(project)!;
        var sections = project.Array("strutture");
        string Sections() => string.Join(",", sections.Select(s => s!.S("nome")));
        expect.That(session.CanReorderSection(third, left) && !session.CanReorderSection(third, third) && !session.CanReorderSection(a, left)
            && !session.CanReorderSection(left, ProjectDocuments.AddSection(left, "Interna")), "Sezioni riordinabili");
        expect.That(!session.ReorderSection(left, right, false) && session.ReorderSection(third, left, false) && Sections() == "Sezione 3,Sezione 1,Sezione 2", "Riordino delle sezioni");
        var otherProject = session.AddProject()!;
        expect.That(session.CanReorderSection(otherProject, project) && session.ReorderSection(otherProject, project, false)
            && ReferenceEquals(session.Document.Array("progetti")[0], otherProject), "Riordino dei progetti");
        expect.That(session.CanNestSection(third, right) && !session.CanNestSection(project, right) && !session.CanNestSection(right, right)
            && !session.CanNestSection(third, project) && !session.CanNestSection(right, a), "Sezioni annidabili");
        expect.That(session.NestSection(third, right) && right.Array("strutture").Contains(third), "Sezione annidata senza domanda");
        var moved = session.AddSheet("mat_calcestruzzo", left)!;
        expect.That(session.NestSection(left, right) && right.Array("strutture").Contains(left) && moved["dati"].S("classe") == "C40/50"
            && probe.Confirmations.Asked.Count == 3, "Sezione annidata con eredità: " + string.Join(" | ", probe.Confirmations.Asked));
    }

    private static void History(Expect expect)
    {
        var probe = new Probe();
        var session = probe.Session;
        var project = session.AddProject()!;
        session.Rename(project, "Ponte");
        var shown = session.Document;
        probe.Changes.Clear();
        int commits = probe.Commits;
        expect.That(session.Restore(false) && !ReferenceEquals(shown, session.Document) && session.Projects.Single().S("nome") == "Progetto 1"
            && probe.Commits == commits + 1 && session.IsDirty && probe.Changes.Single().Change == ProjectChange.Restored, "Annulla");
        expect.That(session.CanRedo && session.Restore(true) && session.Projects.Single().S("nome") == "Ponte", "Ripristina");
        expect.That(!session.Restore(true) && probe.Commits == commits + 3, "Ripristina senza copie: il foglio è confermato comunque");
        session.MarkSaved(Path.Combine(Path.GetTempPath(), "Ponte.programma"));
        expect.That(session.Restore(false) && session.IsDirty, "Annulla dopo il salvataggio");
    }

    private static void Revisions(Expect expect)
    {
        var probe = new Probe();
        var session = probe.Session;
        var root = session.AddProject()!;
        var section = session.AddSection(root)!;
        var child = session.AddSection(section)!;
        var pile = session.AddSheet("geo_palo_verticale", child)!;
        string scope = section.S("id"), pileId = pile.S("id");

        // Scope of the bar (MainWindow.RevisionScope): a section is its own; a sheet takes the nearest revised section above it.
        expect.That(ReferenceEquals(session.RevisionScope(section), section) && ReferenceEquals(session.RevisionScope(pile), child) && session.RevisionScope(null) is null, "Ambito senza revisioni");
        expect.That(session.NewRevision(section, "Verifica della fondazione") && ShellTexts.RevisionNumber(section["revisione"]!.AsObject()) == 1
            && section.Array("revisioni").Count == 1, "Prima revisione");
        expect.That(ReferenceEquals(session.RevisionScope(pile), section), "Ambito: sezione revisionata più vicina");
        pile["dati"]!["generali"]!["diametro"] = 1.2; session.MarkDirty();
        var working = session.Document;
        var liveBefore = working.DeepClone();
        int undo = session.History.UndoCount, commits = probe.Commits;
        probe.Changes.Clear();

        // Revision 0 read-only: a snapshot of the project of the section, the working document untouched.
        expect.That(session.SwitchRevision(scope, 0) && session.IsReadOnly && session.IsPreviewingRevision && session.DisplayedRevision == 0
            && ReferenceEquals(session.WorkingDocument, working) && !ReferenceEquals(session.Document, working) && probe.Commits == commits + 1, "Revisione 0");
        expect.That(probe.Changes.Single().Change == ProjectChange.RevisionShown && session.Title.EndsWith(" · Rev. 0 · sola lettura", StringComparison.Ordinal)
            && session.Find(pileId)!["dati"]!["generali"].D("diametro") == 1, "Dati della revisione 0");
        expect.That(string.Join(",", session.Document.Select(p => p.Key)) == "formato,versione,tipo,progetti" && session.Projects.Count() == 1
            && ShellTexts.RevisionNumber(session.Find(scope)!["revisione"]!.AsObject()) == 0, "Istantanea della revisione");
        expect.That(ReferenceEquals(session.RevisionScope(pile), session.CurrentRevisionScope) && ReferenceEquals(session.CurrentRevisionScope, section), "Ambito durante la consultazione");
        var archived = session.Document.DeepClone();
        session.MarkDirty();
        expect.That(session.AddProject() is null && session.AddSection(root) is null && !session.Rename(root, "x") && !session.CanUndo && !session.CanRedo
            && !session.CanDropModule(session.Find(scope)!, "str_palo") && !session.ChangeData(() => true) && session.History.UndoCount == undo, "Modifiche in sola lettura");
        expect.That(JsonNode.DeepEquals(archived, session.Document) && JsonNode.DeepEquals(liveBefore, working), "La consultazione modifica i dati");
        expect.That(!session.SwitchRevision(scope, 0) && probe.Commits == commits + 1, "Stessa revisione due volte");

        // Back to the current version: the same working document, editable.
        expect.That(session.SwitchRevision(scope, null) && !session.IsReadOnly && ReferenceEquals(session.Document, working) && session.DisplayedRevision is null
            && session.Find(pileId)!["dati"]!["generali"].D("diametro") == 1.2 && session.History.UndoCount == undo, "Ritorno alla versione attuale");
        expect.Throws<ArgumentException>(() => session.SwitchRevision("assente", 0), "Revisione di una sezione assente");

        // ExitPreview (MainWindow.ExitRevisionPreview): back to the working document from any revision.
        session.NewRevision(section, "Diametro aggiornato");
        session.SwitchRevision(scope, 1);
        probe.Changes.Clear();
        expect.That(session.ExitPreview() && !session.IsReadOnly && ReferenceEquals(session.Document, working) && session.RevisionScopeId is null
            && probe.Changes.Single().Change == ProjectChange.PreviewExited && !session.ExitPreview(), "Uscita dalla consultazione");

        // Delete an archived revision while it is shown: preview ended, copy validated, the change can be undone.
        session.SwitchRevision(scope, 0);
        expect.That(session.DeleteRevision(scope, 0) && !session.IsReadOnly && !session.IsPreviewingRevision && !ReferenceEquals(session.Document, working)
            && session.History.Tracks(session.Document) && session.RevisionScopeId == scope, "Eliminazione della revisione consultata");
        var updated = session.Find(scope)!;
        expect.That(updated.Array("revisioni").Count == 1 && ShellTexts.RevisionNumber(updated.Array("revisioni")[0]!.AsObject()) == 1
            && ShellTexts.RevisionNumber(updated["revisione"]!.AsObject()) == 2 && session.CanUndo, "Revisione 0 eliminata");
        expect.That(session.Restore(false) && session.Find(scope)!.Array("revisioni").Count == 2, "Annulla dell'eliminazione");
        // The current revision: the previous one becomes current.
        expect.That(session.DeleteRevision(scope, 2) && ShellTexts.RevisionNumber(session.Find(scope)!["revisione"]!.AsObject()) == 1
            && session.Find(scope)!.Array("revisioni").Count == 1, "Eliminazione della revisione attuale");
        expect.That(!session.DeleteRevision(child.S("id"), 0) && !session.DeleteRevision("assente", 0), "Eliminazione senza revisioni archiviate");

        // Snapshot of MainWindow.SwitchProjectRevision, by itself.
        var entry = session.Find(scope)!.Array("revisioni")[0]!.AsObject();
        var snapshot = ProjectSession.Snapshot(session.WorkingDocument, entry, scope, 0);
        var marked = ProjectRevisions.Find(snapshot, scope)!["revisione"]!.AsObject();
        expect.That(ShellTexts.RevisionNumber(marked) == 0 && marked.S("data") == entry.S("data") && marked.S("nota") == entry.S("nota")
            && string.Join(",", marked.Select(p => p.Key)) == "numero,data,nota", "Istantanea statica");
    }
}
