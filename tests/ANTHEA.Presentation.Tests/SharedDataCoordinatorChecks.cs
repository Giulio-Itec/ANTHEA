using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Presentation.Projects;
using Anthea.Presentation.Services;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// Shared data of the project sheets without WPF (proposal F5, sections 4.4 and 7.1; F1.4 scenarios): the question with
/// both answers, no question when nothing linked changed or when the section controls descendant sections, a lower sheet
/// that cannot rewrite its ancestor, and the rollback on error. The window commit is reproduced as MainWindow.Commit does
/// it: proposal from the editor data, confirmation, data written back, new baseline.
/// </summary>
internal static class SharedDataCoordinatorChecks
{
    internal static int Run()
    {
        var expect = new Expect();
        expect.Throws<ArgumentNullException>(() => _ = new SharedDataCoordinator(null!), "Coordinatore senza servizio delle conferme");
        Answers(expect);
        Unchanged(expect);
        Descendants(expect);
        Rollback(expect);
        Moves(expect);
        Badge(expect);
        Comparison(expect);
        return expect.Count;
    }

    /// <summary>A project with a section of three linked sheets and a parallel section that must never change.</summary>
    private sealed class Project
    {
        internal JsonObject Document { get; } = ProjectDocuments.CreateArchive();
        internal JsonObject Section { get; }
        internal JsonObject Material { get; }
        internal JsonObject Second { get; }
        internal JsonObject Concrete { get; }
        internal JsonObject Outside { get; }

        internal Project()
        {
            var project = ProjectDocuments.AddProject(Document, "Progetto condiviso");
            Section = ProjectDocuments.AddSection(project, "Pila P1");
            var independent = ProjectDocuments.AddSection(project, "Pila P2");
            Material = ProjectDocuments.AddSheet(Section, "mat_calcestruzzo", "CLS di riferimento");
            Second = ProjectDocuments.AddSheet(Section, "mat_calcestruzzo", "CLS seconda verifica");
            Concrete = ProjectDocuments.AddSheet(Section, "str_palo", "Verifica sezione");
            Outside = ProjectDocuments.AddSheet(independent, "mat_calcestruzzo", "Altro elemento");
        }
    }

    /// <summary>The open sheet of the window: its baseline (copy at the last confirmation) and the commit of MainWindow.</summary>
    private sealed class OpenSheet(SharedDataCoordinator coordinator, JsonObject document, JsonObject sheet)
    {
        private JsonObject baseline = (JsonObject)sheet.DeepClone();
        internal int Updates { get; private set; }
        internal Action? Updated { get; set; }

        internal bool Commit(JsonObject data)
        {
            var proposed = (JsonObject)sheet.DeepClone(); proposed["dati"] = data.DeepClone();
            bool shared = coordinator.ConfirmSharedChanges(document, sheet, baseline, proposed, () => { Updates++; Updated?.Invoke(); });
            sheet["dati"] = data.DeepClone();
            baseline = (JsonObject)proposed.DeepClone();
            return shared;
        }

        internal JsonObject Edit(Action<JsonObject> change) { var data = (JsonObject)sheet["dati"]!.DeepClone(); change(data); return data; }
    }

    private static double? Fck(JsonObject sheet) => J.Number(ProjectSharedData.Fields(sheet)["CLS · fck [MPa]"].Value);

    /// <summary>F1.4, «Aggiorna tutti i fogli collegati» and «Solo questo foglio»: same question, same texts, same order.</summary>
    private static void Answers(Expect expect)
    {
        var project = new Project();
        var confirmations = new ProjectPromptRecorder(new ScriptedConfirmations().Then(true).Then(false));
        var open = new OpenSheet(new SharedDataCoordinator(confirmations), project.Document, project.Material);
        var outsideBefore = project.Outside.DeepClone();

        expect.That(open.Commit(open.Edit(d => d["classe"] = "C40/50")), "Aggiornamento dei fogli collegati non applicato");
        var prompt = confirmations.SharedPrompts.Single();
        expect.That(prompt.SectionName == "Pila P1" && prompt.ChangedGroups.SequenceEqual(["Materiali"]), "Sezione o gruppi della domanda");
        expect.That(prompt.Targets.Select(t => t.SheetName).SequenceEqual(["CLS seconda verifica", "Verifica sezione"]), "Fogli collegati della domanda o loro ordine");
        expect.That(prompt.Targets.All(t => t.Changes.SequenceEqual(["CLS · fck [MPa]: 30 → 40"])), "Testo delle modifiche: " + string.Join(" | ", prompt.Targets.SelectMany(t => t.Changes)));
        expect.That(project.Second["dati"].S("classe") == "C40/50" && Fck(project.Concrete) == 40 && open.Updates == 1, "CLS non condiviso tra i fogli della sezione");
        expect.That(JsonNode.DeepEquals(outsideBefore, project.Outside), "Aggiornata una sezione parallela");

        // The same data again: no change of shared data, no question.
        expect.That(!open.Commit(open.Edit(_ => { })) && confirmations.SharedPrompts.Count == 1, "Conferma ripetuta senza modifiche");

        // «Solo questo foglio»: the change stays in the open sheet, the linked sheets keep their value.
        expect.That(!open.Commit(open.Edit(d => d["classe"] = "C25/30")) && confirmations.SharedPrompts.Count == 2, "Scelta locale senza domanda");
        expect.That(Fck(project.Concrete) == 40 && project.Second["dati"].S("classe") == "C40/50" && project.Material["dati"].S("classe") == "C25/30" && open.Updates == 1, "Scelta locale non rispettata");
        expect.That(ProjectSharedData.Differences(project.Section).Any(d => d.Group == "Materiali"), "Differenza locale non segnalata");

        // Back to the value of the linked sheets: shared keys changed, but no linked sheet differs, so no question.
        expect.That(!open.Commit(open.Edit(d => d["classe"] = "C40/50")) && confirmations.SharedPrompts.Count == 2, "Domanda senza fogli da aggiornare");
        expect.That(confirmations.Inner.Asked.SequenceEqual(["dati condivisi: Pila P1", "dati condivisi: Pila P1"]), "Domande registrate");
    }

    /// <summary>Nothing to confirm outside a project, without an open sheet in a section or without its baseline.</summary>
    private static void Unchanged(Expect expect)
    {
        var project = new Project();
        // No scripted answer: any question fails the check.
        var coordinator = new SharedDataCoordinator(new ScriptedConfirmations());
        var proposed = (JsonObject)project.Material.DeepClone(); proposed["dati"]!["classe"] = "C40/50";
        var baseline = (JsonObject)project.Material.DeepClone();
        var single = (JsonObject)project.Document.DeepClone(); single["tipo"] = "geo_palo_verticale";
        int updates = 0;
        expect.That(!coordinator.ConfirmSharedChanges(single, project.Material, baseline, proposed, () => updates++), "Dati condivisi fuori da un progetto");
        expect.That(!coordinator.ConfirmSharedChanges(project.Document, null, baseline, proposed, () => updates++), "Dati condivisi senza foglio aperto");
        expect.That(!coordinator.ConfirmSharedChanges(project.Document, project.Material, null, proposed, () => updates++), "Dati condivisi senza baseline del foglio");
        var detached = (JsonObject)project.Material.DeepClone();
        expect.That(!coordinator.ConfirmSharedChanges(project.Document, detached, baseline, proposed, () => updates++), "Dati condivisi di un foglio fuori dalle sezioni");
        expect.That(updates == 0 && Fck(project.Second) == 30 && Fck(project.Concrete) == 30, "Fogli collegati modificati senza conferma");
        expect.Throws<ArgumentNullException>(() => coordinator.ConfirmSharedChanges(project.Document, project.Material, baseline, proposed, null!), "Conferma senza azione di aggiornamento");
    }

    /// <summary>
    /// A sheet of a section that controls descendant sections propagates without asking (ProjectHierarchySmokeChecks), through
    /// every depth and an empty group, never to a parallel branch of another project; a lower sheet cannot rewrite its ancestor.
    /// </summary>
    private static void Descendants(Expect expect)
    {
        var document = ProjectDocuments.CreateArchive();
        var project = ProjectDocuments.AddProject(document, "Ponte");
        var upper = ProjectDocuments.AddSection(project, "Pali");
        var child = ProjectDocuments.AddSection(upper, "Spalla A");
        var deep = ProjectDocuments.AddSection(child, "Fondazioni");
        var other = ProjectDocuments.AddSection(project, "Travi");
        var unrelated = ProjectDocuments.AddSection(ProjectDocuments.AddProject(document, "Altro progetto"), "Sezione indipendente");
        var cls = ProjectDocuments.AddSheet(project, "mat_calcestruzzo", "CLS di progetto");
        var reference = ProjectDocuments.AddSheet(upper, "str_palo", "Sezione di riferimento");
        var ca = ProjectDocuments.AddSheet(deep, "str_palo", "Verifica fondazione");
        var beam = ProjectDocuments.AddSheet(other, "str_palo", "Verifica trave");
        var untouched = ProjectDocuments.AddSheet(unrelated, "str_palo", "Verifica indipendente");
        var untouchedBefore = untouched.DeepClone();

        // No scripted answer: a question would fail the check.
        var confirmations = new ProjectPromptRecorder(new ScriptedConfirmations());
        var coordinator = new SharedDataCoordinator(confirmations);
        var open = new OpenSheet(coordinator, document, cls);
        expect.That(open.Commit(open.Edit(d => d["classe"] = "C40/50")) && open.Updates == 1 && confirmations.SharedPrompts.Count == 0, "Propagazione del riferimento superiore con domanda");
        foreach (var target in new[] { reference, ca, beam }) expect.That(Fck(target) == 40, "CLS non propagato a " + target.S("nome"));
        expect.That(JsonNode.DeepEquals(untouchedBefore, untouched), "Aggiornamento oltre il progetto");

        // A lower sheet: its change of the common data driven from above is not shared, and the ancestor keeps its value.
        var lower = new OpenSheet(coordinator, document, ca);
        expect.That(!lower.Commit(lower.Edit(d => d["input"]!["fck_mpa"] = "25")) && confirmations.SharedPrompts.Count == 0, "Foglio inferiore ha comandato sui riferimenti");
        expect.That(cls["dati"].S("classe") == "C40/50" && Fck(reference) == 40 && Fck(ca) == 25, "Riferimento superiore modificato dal foglio inferiore");
    }

    /// <summary>An error while updating restores the data of the open sheet and reaches the caller (the window shows it).</summary>
    private static void Rollback(Expect expect)
    {
        var project = new Project();
        var coordinator = new SharedDataCoordinator(new ScriptedConfirmations().Then(true));
        var open = new OpenSheet(coordinator, project.Document, project.Material) { Updated = () => throw new InvalidOperationException("Aggiornamento fallito") };
        var before = project.Material["dati"]!.DeepClone();
        expect.Throws<InvalidOperationException>(() => open.Commit(open.Edit(d => d["classe"] = "C40/50")), "Errore dell'aggiornamento non propagato");
        expect.That(JsonNode.DeepEquals(before, project.Material["dati"]), "Dati del foglio aperto non ripristinati dopo l'errore");
        // As before F5.2 the rollback covers the open sheet only: the linked sheets keep what ApplyHierarchy wrote.
        expect.That(project.Second["dati"].S("classe") == "C40/50", "Ripristino diverso da quello della finestra");
    }

    /// <summary>
    /// F1.4, move into another section (ProjectWorkspaceSmokeChecks): the preview works on a copy, lists the inherited changes
    /// as "sheet · field: before → after", asks only when something changes, «Annulla» gives nothing, «Sposta e aggiorna» the
    /// moved node with the inherited data; a section moves with all its sheets.
    /// </summary>
    private static void Moves(Expect expect)
    {
        var document = ProjectDocuments.CreateArchive();
        var root = ProjectDocuments.AddProject(document, "Ponte di prova");
        var foundation = ProjectDocuments.AddSection(root, "Fondazioni");
        var pile = ProjectDocuments.AddSection(foundation, "Palo P1");
        var other = ProjectDocuments.AddSection(root, "Fondazioni B");
        var empty = ProjectDocuments.AddSection(root, "Vuota");
        JsonObject Steel(JsonObject owner, string name, double fy)
        {
            var sheet = ProjectDocuments.AddSheet(owner, RebarMaterial.Module, name);
            sheet["dati"]!["input"]!["fyk_mpa"] = fy; return sheet;
        }
        Steel(foundation, "Acciaio fondazioni", 450); var sheet = Steel(pile, "Acciaio palo", 450); Steel(other, "Acciaio destinazione", 400);
        var before = document.DeepClone();
        var confirmations = new ProjectPromptRecorder(new ScriptedConfirmations().Then(false).Then(true).Then(true));
        var coordinator = new SharedDataCoordinator(confirmations);
        string change = "Acciaio palo · " + SharedDataCoordinator.FieldLabel("fyk_mpa") + ": 450 → 400";

        expect.That(coordinator.PreviewMove(document, sheet, other) is null, "Spostamento annullato restituisce una proposta");
        var asked = confirmations.Moves.Single();
        expect.That(asked.Destination == "Fondazioni B" && asked.Changes.SequenceEqual([change]), "Anteprima dello spostamento: " + string.Join(" | ", asked.Changes));
        expect.That(JsonNode.DeepEquals(before, document), "Anteprima modifica il documento");

        var proposed = coordinator.PreviewMove(document, sheet, other);
        expect.That(proposed is not null && !ReferenceEquals(proposed, sheet) && proposed.S("id") == sheet.S("id") && J.Number(proposed["dati"]!["input"]!["fyk_mpa"]) == 400, "Proposta dello spostamento");
        expect.That(proposed!.Parent?.Parent is JsonObject destination && destination.S("id") == other.S("id") && JsonNode.DeepEquals(before, document), "Proposta fuori dalla destinazione o documento modificato");

        // Without changes of common data: no question (the scripted answers left are for the section).
        var quiet = coordinator.PreviewMove(document, sheet, empty);
        expect.That(quiet is not null && confirmations.Moves.Count == 2 && J.Number(quiet["dati"]!["input"]!["fyk_mpa"]) == 450, "Domanda senza dati comuni cambiati");

        // A section moves with its sheets, each listed with its changes.
        var section = coordinator.PreviewMove(document, pile, other);
        expect.That(section is not null && confirmations.Moves.Count == 3 && confirmations.Moves[^1].Changes.SequenceEqual([change]), "Anteprima dello spostamento della sezione");
        expect.That(ProjectSharedData.SubtreeSheets(section!).Single().S("id") == sheet.S("id") && section!.Parent?.Parent is JsonObject parent && parent.S("id") == other.S("id"), "Sezione proposta senza i suoi fogli");
        expect.That(JsonNode.DeepEquals(before, document), "Anteprima della sezione modifica il documento");
    }

    /// <summary>Badge of the section in the overview (formerly decided in MainWindow.AddCoherenceBadge): titles, number, tooltip.</summary>
    private static void Badge(Expect expect)
    {
        var document = ProjectDocuments.CreateArchive();
        var project = ProjectDocuments.AddProject(document, "Ponte");
        var empty = ProjectDocuments.AddSection(project, "Vuota");
        expect.That(SharedDataCoordinator.Coherence(empty) is null, "Contrassegno di una sezione senza fogli");

        var steel = ProjectDocuments.AddSection(project, "Acciai");
        var first = ProjectDocuments.AddSheet(steel, RebarMaterial.Module, "Acciaio A");
        var second = ProjectDocuments.AddSheet(steel, RebarMaterial.Module, "Acciaio B");
        var coherent = SharedDataCoordinator.Coherence(steel);
        expect.That(coherent == new CoherenceBadge("✓ Dati comuni coerenti", "✓", false, "Apri il confronto dei dati comuni della sezione."), "Contrassegno dei dati coerenti: " + coherent);
        second["dati"]!["input"]!["fyk_mpa"] = 400; second["dati"]!["input"]!["steel_fu_mpa"] = 480;
        var conflicts = SharedDataCoordinator.Coherence(steel);
        expect.That(conflicts == new CoherenceBadge("⚠ Differenze tra fogli", "2", true, "Apri il confronto dei dati comuni della sezione."), "Contrassegno delle differenze: " + conflicts);

        // A single c.a. sheet: nothing to compare, one warning (no Materiali sheet for the cover).
        var beam = ProjectDocuments.AddSection(project, "Travi");
        ProjectDocuments.AddSheet(beam, "str_palo", "Verifica trave");
        var warnings = ProjectValidation.Warnings(beam);
        var warned = SharedDataCoordinator.Coherence(beam);
        expect.That(warnings.Length == 1 && warned == new CoherenceBadge("⚠ 1 avviso", "1", true, "Apri il confronto e gli avvisi della sezione.\n\n" + warnings[0]), "Contrassegno dell'avviso: " + warned);
    }

    /// <summary>The actions of the comparison dialog: «Uniforma a questo», the missing layers, the link of the stratigraphies.</summary>
    private static void Comparison(Expect expect)
    {
        var document = ProjectDocuments.CreateArchive();
        var project = ProjectDocuments.AddProject(document, "Ponte");
        var steel = ProjectDocuments.AddSection(project, "Acciai");
        var first = ProjectDocuments.AddSheet(steel, RebarMaterial.Module, "Acciaio A");
        var second = ProjectDocuments.AddSheet(steel, RebarMaterial.Module, "Acciaio B");
        second["dati"]!["input"]!["fyk_mpa"] = 400;
        expect.That(SharedDataCoordinator.Align(first, "fyk_mpa", "Materiali", null) && J.Number(second["dati"]!["input"]!["fyk_mpa"]) == J.Number(first["dati"]!["input"]!["fyk_mpa"]), "Uniforma a questo non applicato");
        expect.That(!SharedDataCoordinator.Align(first, "fyk_mpa", "Materiali", null), "Uniforma a questo senza differenze segnalato come modifica");

        // Soils: a vertical pile with one layer, a horizontal pile without layers.
        var soils = ProjectDocuments.AddSection(project, "Terreni");
        var vertical = ProjectDocuments.AddSheet(soils, "geo_palo_verticale", "Palo verticale");
        var horizontal = ProjectDocuments.AddSheet(soils, PaloOrizzontale.Module, "Palo orizzontale");
        var soil = Archivio.NuovoStratoPalo(); soil["spessore"] = "5"; soil["tipologia"] = "Granulare"; soil["angolo_attrito"] = "30"; soil["nc"] = "12";
        vertical["dati"]!["stratigrafie"] = new JsonArray(new JsonArray(soil));
        horizontal["dati"]!["stratigrafie"] = new JsonArray(new JsonArray());
        expect.That(ProjectSharedData.MissingSoilLayers(soils).Count > 0, "Strato mancante non rilevato");
        string typeKey = "Strato · 1/1/tipologia";
        expect.That(SharedDataCoordinator.CopyMissingLayers(vertical, typeKey, 0, 0, "Coesivo"), "Strato mancante non copiato");
        expect.That(ProjectSharedData.MissingSoilLayers(soils).Count == 0 && vertical["dati"]!["stratigrafie"]![0]![0].S("tipologia") == "Coesivo"
            && horizontal["dati"]!["stratigrafie"]![0]![0].S("tipologia") == "Coesivo", "Tipologia scelta non condivisa");
        expect.That(horizontal["dati"]!["stratigrafie"]![0]![0]!["nc"] is null, "Copiato un dato esclusivo del palo verticale");
        expect.That(!SharedDataCoordinator.CopyMissingLayers(vertical, typeKey, 0, 0, null), "Copia senza strati mancanti segnalata come modifica");

        // «Uniforma a questo» with a chosen soil type: the source changes, then the linked sheets follow.
        expect.That(SharedDataCoordinator.Align(vertical, typeKey, "Terreno", "Granulare"), "Tipologia scelta non applicata");
        expect.That(vertical["dati"]!["stratigrafie"]![0]![0].S("tipologia") == "Granulare" && horizontal["dati"]!["stratigrafie"]![0]![0].S("tipologia") == "Granulare", "Tipologia scelta non uniformata");
        expect.That(!SharedDataCoordinator.Align(vertical, typeKey, "Terreno", "Granulare"), "Tipologia uguale segnalata come modifica");

        // Link of the stratigraphies: staged on copies, applied only on confirmation.
        var verticalBefore = vertical.DeepClone(); var horizontalBefore = horizontal.DeepClone();
        var link = SoilLink.Stage(vertical, horizontal);
        expect.That(JsonNode.DeepEquals(verticalBefore, vertical) && JsonNode.DeepEquals(horizontalBefore, horizontal), "Anteprima del collegamento modifica i fogli");
        link.Apply();
        expect.That(JsonNode.DeepEquals(link.StagedTarget["dati"], horizontal["dati"]) && JsonNode.DeepEquals(link.StagedSource["dati"], vertical["dati"])
            && !ReferenceEquals(link.StagedTarget["dati"], horizontal["dati"]), "Collegamento non applicato come in anteprima");
    }
}

/// <summary>Records the content of the project questions and answers them through the scripted confirmations.</summary>
internal sealed class ProjectPromptRecorder(ScriptedConfirmations inner) : IConfirmationService
{
    internal ScriptedConfirmations Inner { get; } = inner;
    internal List<SharedUpdatePrompt> SharedPrompts { get; } = [];
    internal List<(string Destination, IReadOnlyList<string> Changes)> Moves { get; } = [];

    public bool ConfirmSharedUpdate(SharedUpdatePrompt prompt) { SharedPrompts.Add(prompt); return Inner.ConfirmSharedUpdate(prompt); }
    public bool ConfirmProjectMove(string destination, IReadOnlyList<string> changes) { Moves.Add((destination, changes)); return Inner.ConfirmProjectMove(destination, changes); }
    public bool ConfirmClose(Func<bool> saveOrDiscard) => Inner.ConfirmClose(saveOrDiscard);
    public SaveChangesAnswer AskSaveChanges() => Inner.AskSaveChanges();
    public bool ConfirmDelete(string title, string question) => Inner.ConfirmDelete(title, question);
}
