using System.Text.Json.Nodes;
using Anthea.Application.Documents;
using Anthea.Application.Concrete;
using Anthea.Calculations;
using Anthea.ConcreteWorkspace;
using X.Core;

internal static class ConcretePersistenceChecks
{
    // Compare the editing contract in old fixtures without changing those fixtures. The native graph has its own tests below.
    internal static JsonNode? EditorData(JsonNode? node)
        => ConcretePersistenceComparison.EditorData(node);

    internal static int Run(string root, string output)
    {
        int checks = 0;
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks++; }
        void Reject(Action action, string message)
        {
            try { action(); }
            catch (ArgumentException) { checks++; return; }
            throw new InvalidOperationException(message);
        }
        string path = Path.Combine(output, "sezione-gpc.anth");
        var comparison = DocumentService.CreateDocument("str_palo");
        var extra = comparison.DeepClone().AsObject();
        extra["dati"]!["input"]![ConcreteSectionArchive.Key] = new JsonObject();
        Check(ConcretePersistenceComparison.Equivalent(comparison, extra), "Il confronto deve isolare il solo archivio nativo");
        extra["dati"]!["input"]!["width_mm"] = "999";
        Check(!ConcretePersistenceComparison.Equivalent(comparison, extra), "Il confronto nasconde una differenza geometrica");
        extra = comparison.DeepClone().AsObject(); extra[ConcreteSectionArchive.Key] = "campo estraneo";
        Check(!ConcretePersistenceComparison.Equivalent(comparison, extra), "Il confronto ignora omonimi fuori dagli input c.a.");
        var evidence = new JsonArray();
        foreach (string shape in new[] { "Rettangolare", "Circolare", "A T", "Foro rettangolare", "Foro circolare", "Manuale", "Trefolo" })
        {
            var data = DocumentService.CreateSheet("str_palo");
            var workspace = SectionWorkspace.Prepare(data);
            var input = data["input"]!.AsObject();
            input["shape"] = shape switch { "Foro circolare" => "Circolare", "A T" => "A T", "Circolare" => "Circolare", _ => "Rettangolare" };
            if (shape.StartsWith("Foro"))
            {
                input["foro_presente"] = true;
                input["inner_width_mm"] = "200"; input["inner_height_mm"] = "300"; input["inner_diameter_mm"] = "400";
            }
            if (shape == "Manuale")
                input["barre_manuali"] = new JsonArray(J.Obj(("x", "-180"), ("y", "-280"), ("phi", "24")),
                    J.Obj(("x", "180"), ("y", "-280"), ("phi", "24")), J.Obj(("x", "-180"), ("y", "280"), ("phi", "20")),
                    J.Obj(("x", "180"), ("y", "280"), ("phi", "20")));
            if (shape == "Trefolo")
                workspace["trefoli"] = new JsonArray(J.Obj(("id", "T1"), ("x", "0"), ("y", "-200"), ("area", "150"),
                    ("Ep", "195000"), ("fpyk", "1670"), ("fpk", "1860"), ("eps_u", "35"), ("sigma0", "900"), ("diagramma", "Incrudente")));
            var before = CheckerSection.PrepareModel(input, workspace);
            if (shape == "Rettangolare") Check(before.Section.Area == 600d * 800, "Area indipendente del rettangolo");
            if (shape == "Foro rettangolare") Check(before.Section.Area == 600d * 800 - 200d * 300, "Area indipendente del rettangolo forato");
            var document = DocumentService.CreateDocument("str_palo"); document["dati"] = data.DeepClone();
            string editorBefore = document.ToJsonString();
            DocumentService.Save(path, document);
            Check(document.ToJsonString() == editorBefore, shape + ": Save ha modificato il documento vivo");
            var wire = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            Check(wire.D("versione") == 3 && wire.D("versione_base") == 1, "Versione file nuova esplicita");
            var opened = DocumentService.Open(path);
            Check(JsonNode.DeepEquals(EditorData(opened), document), shape + ": metadati dell'editor modificati");
            var afterData = opened["dati"]!.AsObject();
            var afterInput = afterData["input"]!.AsObject(); var afterWorkspace = afterData["workspace_ca"]!.AsObject();
            Check(afterInput[ConcreteSectionArchive.Key].S("stato") == "modello", shape + ": sezione valida salvata come bozza");
            var native = ConcreteSectionArchive.Restore(afterInput, afterWorkspace, requireCurrent: true)!;
            var after = CheckerSection.PrepareModel(afterInput, afterWorkspace);
            Check(after.Section.Guid == native.Guid, shape + ": il calcolo ha ricostruito il modello anziché leggere GPC");
            Check(before.Section.Area == after.Section.Area && before.Section.Jxx == after.Section.Jxx &&
                before.Section.Jyy == after.Section.Jyy && before.Section.Jxy == after.Section.Jxy &&
                before.Section.Centroid.X == after.Section.Centroid.X && before.Section.Centroid.Y == after.Section.Centroid.Y,
                shape + ": proprietà geometriche alterate");
            var barsBefore = before.Section.Rebars.ToArray(); var barsAfter = after.Section.Rebars.ToArray();
            Check(barsBefore.Length == barsAfter.Length, shape + ": armature perse");
            for (int i = 0; i < barsBefore.Length; i++)
            {
                var a = barsBefore[i]; var b = barsAfter[i];
                Check(a.Area == b.Area && a.Position.X == b.Position.X && a.Position.Y == b.Position.Y &&
                    a.EpsilonP == b.EpsilonP && a.RebarMaterial.E == b.RebarMaterial.E &&
                    a.RebarMaterial.SteelType == b.RebarMaterial.SteelType, shape + ": barra/trefolo alterato");
            }
            // The same solver sees the original model and the restored model, with no expected value regenerated.
            var options = workspace["sle"]!["SLE"]!.AsObject();
            var force = new ActionPoint(-500, 10, 5);
            var stressBefore = new CheckerSection(before, input, workspace, options).Stress(force, "SLE");
            var stressAfter = new CheckerSection(after, afterInput, afterWorkspace, options).Stress(force, "SLE");
            Check(stressBefore.tensioni_barre.SequenceEqual(stressAfter.tensioni_barre), shape + ": tensioni barre dopo round trip");
            Check(stressBefore.FiberStresses.SequenceEqual(stressAfter.FiberStresses), shape + ": tensioni CLS dopo round trip");
            evidence.Add(J.Obj(("caso", shape), ("area_mm2", after.Section.Area), ("barre", barsAfter.Length),
                ("identita_gpc_conservata", true), ("tensioni_identiche", true)));
            DocumentService.Save(path, opened);
            Check(JsonNode.DeepEquals(wire, JsonNode.Parse(File.ReadAllText(path))), shape + ": il secondo salvataggio cambia l'archivio");
            var another = CheckerSection.PrepareModel(afterInput, afterWorkspace);
            Check(!ReferenceEquals(after.Section, another.Section), "Modelli mutabili condivisi fra sessioni");
            afterInput["width_mm"] = "650";
            Check(ConcreteSectionArchive.Restore(afterInput, afterWorkspace) is null, "Modifica editor non invalida il modello salvato");
            DocumentService.Save(path, opened);
            var changed = DocumentService.Open(path)["dati"]!.AsObject();
            Check(changed["input"]![ConcreteSectionArchive.Key].S("editor_sha256") != afterInput[ConcreteSectionArchive.Key].S("editor_sha256"),
                "Salvataggio dopo modifica conserva impronta superata");
        }

        // Actual editor/report preparation around save/open: lazily added fields must already be present at save.
        var preparedDocument = DocumentService.CreateDocument("str_palo");
        ConcreteSheetPreparation.PrepareReport(preparedDocument["dati"]!.AsObject());
        DocumentService.Save(path, preparedDocument);
        var editorDocument = DocumentService.Open(path);
        var editorData = editorDocument["dati"]!.AsObject();
        var editorSection = ConcreteSectionArchive.Restore(editorData["input"]!.AsObject(), editorData["workspace_ca"]!.AsObject(), true)!;
        ConcreteSheetPreparation.PrepareReport(editorData);
        Check(CheckerSection.PrepareModel(editorData["input"]!.AsObject(), editorData["workspace_ca"]!.AsObject()).Section.Guid == editorSection.Guid,
            "Preparazione del foglio riaperto perde il modello GPC");

        // Invalid drafts remain recoverable and do not retain a previously valid model.
        var draft = DocumentService.CreateDocument("str_palo");
        draft["dati"]!["input"]!["width_mm"] = "";
        DocumentService.Save(path, draft);
        var draftOpened = DocumentService.Open(path);
        Check(draftOpened["dati"]!["input"]![ConcreteSectionArchive.Key].S("stato") == "bozza", "Bozza non riconosciuta");
        Check(draftOpened["dati"]!["input"]![ConcreteSectionArchive.Key]!["archivio"] is null, "Bozza contiene un modello precedente");
        Check(JsonNode.DeepEquals(EditorData(draftOpened), draft), "Bozza non riapribile integralmente");
        draftOpened["dati"]!["input"]!["width_mm"] = "600";
        DocumentService.Save(path, draftOpened);
        var repaired = DocumentService.Open(path);
        Check(repaired["dati"]!["input"]![ConcreteSectionArchive.Key].S("stato") == "modello", "Bozza corretta resta senza modello");
        byte[] validBytes = File.ReadAllBytes(path);
        foreach (string corruption in new[] { "versione", "xml", "editor", "mancante" })
        {
            var bad = JsonNode.Parse(validBytes)!.AsObject();
            var input = bad["dati"]!["input"]!.AsObject();
            switch (corruption)
            {
                case "versione": input[ConcreteSectionArchive.Key]!["versione"] = 99; break;
                case "xml": input[ConcreteSectionArchive.Key]!["archivio"] = "<invalid/>"; break;
                case "editor": input["height_mm"] = "123"; break;
                case "mancante": input.Remove(ConcreteSectionArchive.Key); break;
            }
            File.WriteAllText(path, bad.ToJsonString());
            Reject(() => DocumentService.Open(path), "Archivio danneggiato accettato: " + corruption);
        }
        File.WriteAllBytes(path, validBytes);
        repaired["dati"]!["input"]![ConcreteSectionArchive.Key]!["versione"] = 99;
        Reject(() => DocumentService.Save(path, repaired), "Contratto futuro accettato in Save");
        Check(File.ReadAllBytes(path).SequenceEqual(validBytes), "Salvataggio fallito ha alterato il file valido");

        // Historic revisions, nested sections, project-level sheets and another module share the same archive.
        var projectDocument = ProjectDocuments.CreateArchive();
        var project = ProjectDocuments.AddProject(projectDocument, "Persistenza");
        var section = ProjectDocuments.AddSection(project, "Sezioni");
        var sheet = ProjectDocuments.AddSheet(section, "str_palo");
        sheet["dati"]!["input"]!["width_mm"] = "500";
        ProjectRevisions.NewRevision(projectDocument, section, "500 → 600");
        sheet["dati"]!["input"]!["width_mm"] = "600";
        var other = ProjectDocuments.AddSheet(section, "geo_efficienza_orizzontale");
        var otherData = other["dati"]!.DeepClone();
        var nested = ProjectDocuments.AddSection(section, "Figlia");
        var nestedSheet = ProjectDocuments.AddSheet(nested, "str_palo");
        nestedSheet["dati"]!["input"]!["width_mm"] = "450";
        var projectSheet = ProjectDocuments.AddSheet(project, "str_palo");
        projectSheet["dati"]!["input"]!["width_mm"] = "550";
        string beforeProject = projectDocument.ToJsonString();
        DocumentService.Save(path, projectDocument);
        Check(beforeProject == projectDocument.ToJsonString(), "Salvataggio modifica revisioni vive");
        var restoredProject = DocumentService.Open(path);
        var restoredSection = ProjectRevisions.Find(restoredProject, section.S("id"))!;
        var history = ProjectRevisions.Snapshot(restoredProject, restoredSection.Array("revisioni")[0]!.AsObject());
        var oldData = ProjectSharedData.SubtreeSheets(history).Single()["dati"]!.AsObject();
        Check(oldData["input"].D("width_mm") == 500, "Geometria della revisione storica persa");
        Check(ConcreteSectionArchive.Restore(oldData["input"]!.AsObject(), oldData["workspace_ca"]!.AsObject(), true)!.Area == 500d * 800,
            "La revisione contiene il modello corrente");
        Check(ProjectRevisions.Find(restoredProject, sheet.S("id"))!["dati"]!["input"].D("width_mm") == 600, "Sezione corrente persa");
        Check(JsonNode.DeepEquals(ProjectRevisions.Find(restoredProject, other.S("id"))!["dati"], otherData), "Modulo estraneo alterato");
        var nestedRestored = ProjectRevisions.Find(restoredProject, nestedSheet.S("id"))!["dati"]!.AsObject();
        Check(ConcreteSectionArchive.Restore(nestedRestored["input"]!.AsObject(), nestedRestored["workspace_ca"]!.AsObject(), true)!.Area == 450d * 800,
            "Sezione annidata non salvata come GPC");
        var projectRestored = restoredProject["progetti"]![0]!.Array("fogli").OfType<JsonObject>().Single()["dati"]!.AsObject();
        Check(ConcreteSectionArchive.Restore(projectRestored["input"]!.AsObject(), projectRestored["workspace_ca"]!.AsObject(), true)!.Area == 550d * 800,
            "Foglio del progetto non salvato come GPC");
        ProjectRevisions.Validate(restoredProject);
        checks++;
        DocumentService.Save(path, restoredProject);
        ProjectRevisions.Validate(DocumentService.Open(path));
        checks++;
        File.WriteAllText(Path.Combine(output, "persistenza-sezioni.json"), evidence.ToJsonString(J.Options));
        return checks;
    }
}
