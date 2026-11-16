using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Presentation.Projects;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// Reproduction of point 8 of §1.5 of the note of step 6 (gpc-f6-claude/INT/rapporti/passo6-progetto-20261115.md, 16/11/2026): the layers
/// are shared by position («Strato · 1/2/angolo_attrito», ProjectSharedSoils.AddSoilFields) and written by position (ApplySoilField). When
/// the first layer is removed in a sheet of a section with subsections, the confirmation does not ask (SharedDataCoordinator, «controlsDescendants»)
/// and the sheets below receive the values moved up by one layer, thicknesses included, while their last layer stays.
/// The check states the expected behaviour: the sheets below keep their layers, or the user is asked first. It FAILS on 3120a64d (known
/// defect, not corrected here: the fix is the user's decision). It stays out of the default run and of build/ci.ps1, because
/// build/known-failures.json admits a new failure only after it has been explained to the user (AGENTS.md, «Verifiche»): it runs only with
/// --riproduzione-strati. The scenario without subsections, where the question lists the changes, is checked as it is today.
/// </summary>
internal static class SoilLayerShiftReproduction
{
    internal static int Run()
    {
        var expect = new Expect();
        var failures = new List<string>();

        // With a subsection: «Palo di progetto» in the project, «Palo P1» in «Pila 1», the same three layers.
        {
            var (document, top, below) = Scenario(withSubsection: true);
            string before = Rows(below);
            var confirmations = new ProjectPromptRecorder(new ScriptedConfirmations().Then(false));
            bool updated = Commit(new SharedDataCoordinator(confirmations), document, top);
            string after = Rows(below);
            if (confirmations.SharedPrompts.Count == 0 && after != before)
                failures.Add("Primo strato tolto in «Palo di progetto»: «Palo P1» cambiato senza domanda (aggiornamento " + updated + "). Strati prima: " + before + ". Dopo: " + after + ".");
            else expect.That(true, "Con sottosezioni: strati del foglio sotto conservati o domanda posta");
        }

        // Without a subsection: the two sheets in the same section, the question lists the changes (answer «Solo questo foglio»).
        {
            var (document, top, below) = Scenario(withSubsection: false);
            string before = Rows(below);
            var confirmations = new ProjectPromptRecorder(new ScriptedConfirmations().Then(false));
            bool updated = Commit(new SharedDataCoordinator(confirmations), document, top);
            expect.That(!updated && confirmations.SharedPrompts.Count == 1 && Rows(below) == before
                && confirmations.SharedPrompts[0].Targets.Single().Changes.Any(c => c.Contains("Strato 1", StringComparison.Ordinal)),
                "Senza sottosezioni la domanda elenca i cambi e «Solo questo foglio» conserva gli strati");
        }
        if (failures.Count > 0) throw new InvalidOperationException("Riproduzione del §1.5 punto 8 confermata:\n" + string.Join("\n", failures));
        return expect.Count;
    }

    /// <summary>Project «Viadotto» with «Palo di progetto» (three layers) and «Palo P1» with the same layers, in «Pila 1» under the project
    /// (<paramref name="withSubsection"/>) or beside the first sheet.</summary>
    private static (JsonObject Document, JsonObject Top, JsonObject Below) Scenario(bool withSubsection)
    {
        var document = ProjectDocuments.CreateArchive();
        var project = ProjectDocuments.AddProject(document, "Viadotto");
        var top = ProjectDocuments.AddSheet(project, "geo_palo_verticale", "Palo di progetto");
        top["dati"]!["stratigrafie"] = new JsonArray(new JsonArray(
            Layer("2", "Coesivo", ("coesione_non_drenata", "40")), Layer("5", "Granulare", ("angolo_attrito", "30")), Layer("10", "Granulare", ("angolo_attrito", "36"))));
        var owner = withSubsection ? ProjectDocuments.AddSection(project, "Pila 1") : project;
        var below = ProjectDocuments.AddSheet(owner, "geo_palo_verticale", "Palo P1");
        below["dati"]!["stratigrafie"] = top["dati"]!["stratigrafie"]!.DeepClone();
        if (ProjectSharedData.Differences(project).Any(d => d.Key.StartsWith("Strato · ", StringComparison.Ordinal)))
            throw new InvalidOperationException("Scenario: le stratigrafie dei due fogli devono partire uguali");
        return (document, top, below);
    }

    private static JsonObject Layer(string thickness, string type, (string Key, string Value) parameter)
    {
        var layer = Archivio.NuovoStratoPalo();
        layer["spessore"] = thickness; layer["tipologia"] = type; layer[parameter.Key] = parameter.Value;
        return layer;
    }

    /// <summary>The commit of the open sheet as SheetSession does it, with the first layer removed: proposal, confirmation, data written back.</summary>
    private static bool Commit(SharedDataCoordinator coordinator, JsonObject document, JsonObject sheet)
    {
        var baseline = (JsonObject)sheet.DeepClone();
        var data = (JsonObject)sheet["dati"]!.DeepClone();
        data["stratigrafie"]![0]!.AsArray().RemoveAt(0);
        var proposed = (JsonObject)sheet.DeepClone(); proposed["dati"] = data.DeepClone();
        bool shared = coordinator.ConfirmSharedChanges(document, sheet, baseline, proposed, () => { });
        sheet["dati"] = data.DeepClone();
        return shared;
    }

    /// <summary>The common data of the layers of the first survey: «spessore tipologia φ cu» for each layer.</summary>
    private static string Rows(JsonObject sheet) => string.Join(" · ", sheet["dati"]!["stratigrafie"]![0]!.AsArray().OfType<JsonObject>()
        .Select(r => $"{r.S("spessore")} m {r.S("tipologia")} φ={r.S("angolo_attrito")} cu={r.S("coesione_non_drenata")}"));
}
