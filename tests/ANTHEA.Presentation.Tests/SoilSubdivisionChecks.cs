using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Presentation.Projects;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// §1.5 point 8 of the note of step 6 (gpc-f6-claude/INT/rapporti/passo6-progetto-20261115.md, 16/11/2026). The layers are shared and
/// written by position («Strato · 1/2/angolo_attrito»). Until 3120a64d, removing the first layer in a sheet of a section with subsections
/// updated the sheets below without a question (SharedDataCoordinator, «controlsDescendants»): they received the values moved up by one
/// layer, thicknesses included, and kept their last layer (2 m Coesivo · 5 m · 10 m became 5 m · 10 m · 10 m). Now a survey whose number
/// of layers changes is not written into the other sheets, which keep their layers; the difference shows in the comparison. The same
/// number of layers is shared as before. The neighbouring cases: a layer added at the top, the last one removed, a thickness changed,
/// two surveys, a survey removed, the section without subsections, and undo and redo through the shell.
/// </summary>
internal static class SoilSubdivisionChecks
{
    internal static int Run()
    {
        var expect = new Expect();
        Subdivisions(expect);
        SameSubdivision(expect);
        WithoutSubsection(expect);
        SingleThreadContext.Run(_ => UndoRedo(expect));
        return expect.Count;
    }

    /// <summary>Another number of layers (or of surveys): the sheet below keeps everything, without a question, and the comparison shows it.</summary>
    private static void Subdivisions(Expect expect)
    {
        foreach (var (name, edit) in new (string, Action<JsonArray>)[]
        {
            ("primo strato tolto (riproduzione)", surveys => surveys[0]!.AsArray().RemoveAt(0)),
            ("strato aggiunto in testa", surveys => surveys[0]!.AsArray().Insert(0, Layer("1", "Coesivo", ("coesione_non_drenata", "20")))),
            ("ultimo strato tolto", surveys => surveys[0]!.AsArray().RemoveAt(2)),
            ("strato in mezzo tolto e spessore cambiato", surveys => { surveys[0]!.AsArray().RemoveAt(1); surveys[0]![0]!["spessore"] = "3"; }),
        })
        {
            var (document, project, top, below) = Scenario(withSubsection: true);
            var before = below["dati"]!.DeepClone();
            var confirmations = new ProjectPromptRecorder(new ScriptedConfirmations());
            bool updated = Commit(new SharedDataCoordinator(confirmations), document, top, edit);
            expect.That(!updated && confirmations.SharedPrompts.Count == 0 && JsonNode.DeepEquals(before, below["dati"]),
                name + ": «Palo P1» cambiato. Strati: " + Rows(below, 0));
            expect.That(Rows(top, 0) != Rows(below, 0), name + ": il foglio confermato non ha i suoi strati");
            if (name != "ultimo strato tolto")
                expect.That(ProjectSharedData.Differences(project).Any(d => d.Key.StartsWith("Strato · 1/", StringComparison.Ordinal)), name + ": differenza non visibile nel confronto");
        }

        // A survey removed when the two surveys have the same number of layers: survey 2 would become survey 1.
        {
            var (document, _, top, below) = Scenario(withSubsection: true, surveys: 2);
            var before = below["dati"]!.DeepClone();
            var confirmations = new ProjectPromptRecorder(new ScriptedConfirmations());
            bool updated = Commit(new SharedDataCoordinator(confirmations), document, top, surveys => surveys.RemoveAt(0));
            expect.That(!updated && confirmations.SharedPrompts.Count == 0 && JsonNode.DeepEquals(before, below["dati"]), "Sondaggio tolto: «Palo P1» cambiato");
        }
        // Two surveys: the first with a layer removed stays, the second with the same layers is shared.
        {
            var (document, _, top, below) = Scenario(withSubsection: true, surveys: 2);
            string first = Rows(below, 0);
            var confirmations = new ProjectPromptRecorder(new ScriptedConfirmations());
            bool updated = Commit(new SharedDataCoordinator(confirmations), document, top, surveys => { surveys[0]!.AsArray().RemoveAt(0); surveys[1]![1]!["spessore"] = "7"; });
            expect.That(updated && confirmations.SharedPrompts.Count == 0 && Rows(below, 0) == first && Rows(below, 1) == Rows(top, 1)
                && below["dati"]!["stratigrafie"]![1]![1].S("spessore") == "7", "Due sondaggi: " + Rows(below, 0) + " | " + Rows(below, 1));
        }
    }

    /// <summary>The same number of layers: a thickness reaches the sheet below as before, the other layers stay.</summary>
    private static void SameSubdivision(Expect expect)
    {
        var (document, _, top, below) = Scenario(withSubsection: true);
        var confirmations = new ProjectPromptRecorder(new ScriptedConfirmations());
        bool updated = Commit(new SharedDataCoordinator(confirmations), document, top, surveys => surveys[0]![1]!["spessore"] = "6");
        expect.That(updated && confirmations.SharedPrompts.Count == 0 && Rows(below, 0) == "2 m Coesivo φ=0 cu=40 · 6 m Granulare φ=30 cu=0 · 10 m Granulare φ=36 cu=0",
            "Spessore cambiato: " + Rows(below, 0));
    }

    /// <summary>Without subsections: no question for a new subdivision (nothing to share); the question stays for a thickness.</summary>
    private static void WithoutSubsection(Expect expect)
    {
        {
            var (document, _, top, below) = Scenario(withSubsection: false);
            var before = below["dati"]!.DeepClone();
            var confirmations = new ProjectPromptRecorder(new ScriptedConfirmations());
            bool updated = Commit(new SharedDataCoordinator(confirmations), document, top, surveys => surveys[0]!.AsArray().RemoveAt(0));
            expect.That(!updated && confirmations.SharedPrompts.Count == 0 && JsonNode.DeepEquals(before, below["dati"]), "Senza sottosezioni, primo strato tolto");
        }
        {
            var (document, _, top, below) = Scenario(withSubsection: false);
            var confirmations = new ProjectPromptRecorder(new ScriptedConfirmations().Then(true));
            bool updated = Commit(new SharedDataCoordinator(confirmations), document, top, surveys => surveys[0]![0]!["spessore"] = "4");
            expect.That(updated && confirmations.SharedPrompts.Count == 1 && below["dati"]!["stratigrafie"]![0]![0].S("spessore") == "4"
                && confirmations.SharedPrompts[0].Targets.Single().Changes.Any(c => c.Contains("Strato 1", StringComparison.Ordinal)), "Senza sottosezioni, spessore con la domanda");
        }
    }

    /// <summary>Through the shell: the commit of the open sheet, undo and redo of the project (the nodes are made again: found by id).</summary>
    private static Task<int> UndoRedo(Expect expect)
    {
        var bench = new ShellBench();
        var workspace = bench.Workspace;
        var project = workspace.NewProject()!;
        var root = project.AddProject()!;
        var top = project.AddSheet("geo_palo_verticale", root)!;
        var pila = project.AddSection(root)!;
        var below = project.AddSheet("geo_palo_verticale", pila)!;
        // Through the session, so that the history records the layers before the edits that are undone.
        expect.That(project.ChangeData(() => { top["dati"]!["stratigrafie"] = Layers(); return true; }, top)
            && project.ChangeData(() => { below["dati"]!["stratigrafie"] = Layers(); return true; }, below), "Shell: strati iniziali");
        string topId = top.S("id"), belowId = below.S("id");
        string Top() => Rows(project.Find(topId)!, 0);
        string Below() => Rows(project.Find(belowId)!, 0);
        string original = Below();

        // The same number of layers: the thickness reaches «Palo P1»; undo and redo take it back and again.
        var tab = workspace.OpenSheet(top);
        tab.ViewModel.Data["stratigrafie"]![0]![1]!["spessore"] = "6";
        workspace.CommitActive();
        string thicker = Below();
        expect.That(thicker == Top() && thicker != original, "Shell, spessore cambiato: " + Top() + " | " + thicker);
        expect.That(project.Restore(false) && Top() == original && Below() == original, "Shell, annulla dello spessore: " + Top() + " | " + Below());
        expect.That(project.Restore(true) && Top() == thicker && Below() == thicker, "Shell, ripristina dello spessore: " + Top() + " | " + Below());

        // The first layer removed: «Palo P1» keeps its layers, through undo and redo too.
        tab = workspace.OpenSheet(project.Find(topId)!);
        tab.ViewModel.Data["stratigrafie"]![0]!.AsArray().RemoveAt(0);
        workspace.CommitActive();
        string removed = Top();
        expect.That(removed != thicker && Below() == thicker, "Shell, primo strato tolto: «Palo P1» " + Below());
        expect.That(project.Restore(false) && Top() == thicker && Below() == thicker, "Shell, annulla del primo strato: " + Top() + " | " + Below());
        expect.That(project.Restore(true) && Top() == removed && Below() == thicker, "Shell, ripristina del primo strato: " + Top() + " | " + Below());

        // Now the two sheets have another number of layers: a thickness of the first layer does not go into the first layer of «Palo P1».
        tab = workspace.OpenSheet(project.Find(topId)!);
        tab.ViewModel.Data["stratigrafie"]![0]![0]!["spessore"] = "8";
        workspace.CommitActive();
        expect.That(Below() == thicker && Top().StartsWith("8 m Granulare", StringComparison.Ordinal), "Shell, suddivisioni già diverse: " + Top() + " | " + Below());
        return Task.FromResult(expect.Count);
    }

    /// <summary>Project «Viadotto» with «Palo di progetto» and «Palo P1» with the same layers, in «Pila 1» under the project
    /// (<paramref name="withSubsection"/>) or beside the first sheet; <paramref name="surveys"/> equal surveys.</summary>
    private static (JsonObject Document, JsonObject Project, JsonObject Top, JsonObject Below) Scenario(bool withSubsection, int surveys = 1)
    {
        var document = ProjectDocuments.CreateArchive();
        var project = ProjectDocuments.AddProject(document, "Viadotto");
        var top = ProjectDocuments.AddSheet(project, "geo_palo_verticale", "Palo di progetto");
        var all = new JsonArray();
        for (int i = 0; i < surveys; i++) all.Add(Layers()[0]!.DeepClone());
        top["dati"]!["stratigrafie"] = all;
        var owner = withSubsection ? ProjectDocuments.AddSection(project, "Pila 1") : project;
        var below = ProjectDocuments.AddSheet(owner, "geo_palo_verticale", "Palo P1");
        below["dati"]!["stratigrafie"] = all.DeepClone();
        if (ProjectSharedData.Differences(project).Any(d => d.Key.StartsWith("Strato · ", StringComparison.Ordinal)))
            throw new InvalidOperationException("Scenario: le stratigrafie dei due fogli devono partire uguali");
        return (document, project, top, below);
    }

    /// <summary>One survey: 2 m Coesivo (cu 40), 5 m Granulare (φ 30), 10 m Granulare (φ 36).</summary>
    private static JsonArray Layers() => new(new JsonArray(
        Layer("2", "Coesivo", ("coesione_non_drenata", "40")), Layer("5", "Granulare", ("angolo_attrito", "30")), Layer("10", "Granulare", ("angolo_attrito", "36"))));

    private static JsonObject Layer(string thickness, string type, (string Key, string Value) parameter)
    {
        var layer = Archivio.NuovoStratoPalo();
        layer["spessore"] = thickness; layer["tipologia"] = type; layer[parameter.Key] = parameter.Value;
        return layer;
    }

    /// <summary>The commit of the open sheet as SheetSession does it: proposal with <paramref name="edit"/>, confirmation, data written back.</summary>
    private static bool Commit(SharedDataCoordinator coordinator, JsonObject document, JsonObject sheet, Action<JsonArray> edit)
    {
        var baseline = (JsonObject)sheet.DeepClone();
        var data = (JsonObject)sheet["dati"]!.DeepClone();
        edit(data["stratigrafie"]!.AsArray());
        var proposed = (JsonObject)sheet.DeepClone(); proposed["dati"] = data.DeepClone();
        bool shared = coordinator.ConfirmSharedChanges(document, sheet, baseline, proposed, () => { });
        sheet["dati"] = data.DeepClone();
        return shared;
    }

    /// <summary>The common data of the layers of a survey: «spessore tipologia φ cu» for each layer.</summary>
    private static string Rows(JsonObject sheet, int survey) => string.Join(" · ", sheet["dati"]!["stratigrafie"]![survey]!.AsArray().OfType<JsonObject>()
        .Select(r => $"{r.S("spessore")} m {r.S("tipologia")} φ={r.S("angolo_attrito")} cu={r.S("coesione_non_drenata")}"));
}
