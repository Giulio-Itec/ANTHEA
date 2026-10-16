using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Presentation.Projects;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// Project undo and redo without WPF (proposal F5, section 3.7): a copy only for a real change, at most 30 copies, redo cleared
/// by a new change, the open sheet committed before the restore (its pending input is kept by undo), nothing while read-only
/// or outside a project, and the states of the two buttons. The window is reproduced as MainWindow does it: the commit of the
/// sheet records the change, the restored copy becomes the document.
/// </summary>
internal static class ProjectHistoryChecks
{
    internal static int Run()
    {
        var expect = new Expect();
        Sequences(expect);
        Capacity(expect);
        ReadOnly(expect);
        Documents(expect);
        return expect.Count;
    }

    /// <summary>The window around the history: the current document and the commit of the open sheet (MainWindow.Commit → MarkDirty).</summary>
    private sealed class ProjectWindow
    {
        internal ProjectHistory History { get; } = new();
        internal JsonObject Document { get; private set; }
        internal int Commits { get; private set; }
        /// <summary>Input of the open sheet not committed yet: applied by the next commit.</summary>
        internal string? Pending { get; set; }

        internal ProjectWindow(string name)
        {
            Document = ProjectDocuments.CreateArchive();
            ProjectDocuments.AddProject(Document, name);
            History.Reset(Document);
        }

        internal string Name => Document.Array("progetti")[0].S("nome");

        /// <summary>An action of the project: the document changes, then MarkDirty records it.</summary>
        internal bool Change(string name) { Document.Array("progetti")[0]!["nome"] = name; return History.Record(Document, false); }

        private void Commit()
        {
            Commits++;
            if (Pending is null) return;
            Document.Array("progetti")[0]!["nome"] = Pending; Pending = null;
            History.Record(Document, false);
        }

        internal bool Undo() => Restore(false);
        internal bool Redo() => Restore(true);

        private bool Restore(bool redo)
        {
            var restored = History.Restore(redo, false, Commit, () => Document);
            if (restored is null) return false;
            Document = restored;
            return true;
        }
    }

    private static void Sequences(Expect expect)
    {
        var window = new ProjectWindow("Ponte");
        var history = window.History;
        expect.That(history.Tracks(window.Document) && history.UndoCount == 0 && history.RedoCount == 0 && !history.CanUndo(false) && !history.CanRedo(false), "Storia nuova");

        // A copy only when the document differs from the last confirmed state.
        expect.That(!history.Record(window.Document, false) && history.UndoCount == 0, "Copia senza modifiche");
        expect.That(window.Change("Ponte 1") && history.UndoCount == 1 && history.CanUndo(false) && !history.CanRedo(false), "Copia della modifica");
        expect.That(!history.Record(window.Document, false) && history.UndoCount == 1, "Seconda copia dello stesso stato");
        expect.That(window.Change("Ponte 2") && history.UndoCount == 2, "Copia della seconda modifica");

        // Undo: the sheet is committed first, the previous copy becomes the document, the current one goes to redo.
        var before = window.Document;
        expect.That(window.Undo() && window.Name == "Ponte 1" && window.Commits == 1, "Annulla");
        expect.That(!ReferenceEquals(before, window.Document) && history.Tracks(window.Document) && history.UndoCount == 1 && history.RedoCount == 1 && history.CanRedo(false), "Documento ripristinato non seguito");
        expect.That(window.Undo() && window.Name == "Ponte" && !history.CanUndo(false) && history.RedoCount == 2, "Secondo annulla");
        // Nothing left to undo: the commit still runs first, the document stays.
        var oldest = window.Document;
        expect.That(!window.Undo() && window.Commits == 3 && ReferenceEquals(oldest, window.Document), "Annulla senza copie");
        expect.That(window.Redo() && window.Name == "Ponte 1" && window.Redo() && window.Name == "Ponte 2" && !history.CanRedo(false) && history.UndoCount == 2, "Ripristina");
        expect.That(!window.Redo() && window.Name == "Ponte 2", "Ripristina senza copie");

        // A new change clears the redo copies.
        expect.That(window.Undo() && history.CanRedo(false), "Annulla prima della nuova modifica");
        expect.That(window.Change("Ponte 1b") && !history.CanRedo(false) && history.RedoCount == 0 && history.UndoCount == 2, "Ripristina conservato dopo una nuova modifica");

        // Pending input of the open sheet and redo: the commit records it, which clears redo, so nothing is restored.
        expect.That(window.Undo() && history.CanRedo(false), "Annulla prima dell'input in sospeso");
        window.Pending = "Ponte in sospeso";
        expect.That(!window.Redo() && window.Name == "Ponte in sospeso" && !history.CanRedo(false), "Ripristina sopra l'input in sospeso");

        // Pending input and undo: committed first, then undone; redo gives it back, never lost.
        window.Pending = "Ponte da annullare";
        expect.That(window.Undo() && window.Name == "Ponte in sospeso" && window.Redo() && window.Name == "Ponte da annullare", "Input in sospeso perso dall'annulla");
        expect.Throws<ArgumentNullException>(() => history.Restore(false, false, null!, () => window.Document), "Ripristino senza conferma del foglio");
        expect.Throws<ArgumentNullException>(() => history.Reset(null!), "Storia senza documento");
    }

    /// <summary>At most 30 copies: the oldest is dropped.</summary>
    private static void Capacity(Expect expect)
    {
        var window = new ProjectWindow("v0");
        for (int change = 1; change <= ProjectHistory.Capacity + 1; change++) window.Change("v" + change);
        expect.That(ProjectHistory.Capacity == 30 && window.History.UndoCount == 30, "Numero di copie: " + window.History.UndoCount);
        int undone = 0;
        while (window.Undo()) undone++;
        expect.That(undone == 30 && window.Name == "v1" && window.History.RedoCount == 30, "Copia più vecchia non scartata: " + window.Name);
    }

    /// <summary>Read-only (an archived revision on screen): nothing recorded, restored or committed; both buttons disabled.</summary>
    private static void ReadOnly(Expect expect)
    {
        var window = new ProjectWindow("Ponte");
        var history = window.History;
        window.Change("Ponte 1"); window.Change("Ponte 2"); window.Undo();
        expect.That(history.CanUndo(false) && history.CanRedo(false) && !history.CanUndo(true) && !history.CanRedo(true), "Pulsanti attivi in sola lettura");
        int commits = 0;
        var shown = window.Document;
        expect.That(history.Restore(false, true, () => commits++, () => shown) is null && history.Restore(true, true, () => commits++, () => shown) is null, "Ripristino in sola lettura");
        expect.That(commits == 0 && history.UndoCount == 1 && history.RedoCount == 1, "Conferma del foglio o copie in sola lettura");
        shown.Array("progetti")[0]!["nome"] = "Revisione consultata";
        expect.That(!history.Record(shown, true) && history.UndoCount == 1 && history.RedoCount == 1, "Copia in sola lettura");
    }

    /// <summary>Documents that are not projects, a new document (new history) and a document replaced with the same history.</summary>
    private static void Documents(Expect expect)
    {
        var window = new ProjectWindow("Ponte");
        var history = window.History;
        window.Change("Ponte 1");
        var single = J.Obj(("formato", "X"), ("versione", 1), ("tipo", "geo_palo_verticale"));
        expect.That(!history.Record(single, false) && history.Tracks(window.Document) && history.UndoCount == 1, "Storia di un documento che non è un progetto");

        // Another project document: a new history, ids completed, no copies.
        var opened = ProjectDocuments.CreateArchive();
        var legacy = J.Obj(("nome", "Senza id"), ("strutture", new JsonArray()), ("fogli", new JsonArray()));
        opened.Array("progetti").Add(legacy);
        expect.That(history.Record(opened, false) && history.Tracks(opened) && history.UndoCount == 0 && history.RedoCount == 0 && legacy.S("id").Length > 0, "Nuovo documento senza nuova storia");

        // A replaced document with the same history (archived revision deleted): undo returns to the state before it.
        var original = (JsonObject)opened.DeepClone();
        var updated = (JsonObject)opened.DeepClone(); updated.Array("progetti")[0]!["nome"] = "Revisione eliminata";
        history.Track(updated);
        expect.That(history.Tracks(updated) && history.Record(updated, false) && history.UndoCount == 1, "Documento sostituito senza copia");
        var restored = history.Restore(false, false, () => { }, () => updated);
        expect.That(restored is not null && JsonNode.DeepEquals(original, restored), "Annulla della sostituzione");
    }
}
