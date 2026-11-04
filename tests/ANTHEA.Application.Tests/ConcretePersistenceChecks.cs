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
        // ANTHEA 1.19: the compact archive of GPC (ModelArchive version 6, «GPCMOD») in base64 in the contract of version 2.
        bool CompactArchive(JsonObject input) => input[ConcreteSectionArchive.Key] is JsonObject saved && saved.D("versione") == ConcreteSectionArchive.CompactVersion &&
            Convert.FromBase64String(saved.S("archivio")).AsSpan(0, 6).SequenceEqual("GPCMOD"u8);
        // ANTHEA up to 1.18 removes the layers 4, 3 and 2 only (each looks for its own version), then Validate refuses every version but 1: the message of a
        // newer file, never a failure on a section (the real 1.18.0 is checked outside the suite with the payload of its installer, correzioni-F7a.md).
        void RefusedAsNewer(JsonObject wire, string what)
        {
            try { DocumentService.Validate(wire.DeepClone().AsObject()); }
            catch (ArgumentException error)
            {
                Check(error.Message == "Formato o versione del file non supportati.", what + ": ANTHEA 1.18 non rifiuta il file con il messaggio di un file più recente");
                return;
            }
            throw new InvalidOperationException(what + ": file con sezioni compatte accettato come documento di versione 1");
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
            // ANTHEA 1.19 (4/11/2026, «Compatto dalla prossima»): the section in the compact contract, the wire version 5 over the 3.
            Check(wire.D("versione") == 5 && wire.D("versione_senza_compatto") == 3 && wire.D("versione_base") == 1, "Versione file nuova esplicita");
            Check(CompactArchive(wire["dati"]!["input"]!.AsObject()), shape + ": sezione non salvata nel formato compatto");
            RefusedAsNewer(wire, shape);
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
        var rawProject = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        Check(rawProject.D("versione") == 5 && rawProject.D("versione_senza_compatto") == 3, "Progetto con sezioni compatte senza la versione 5");
        RefusedAsNewer(rawProject, "progetto");

        // ANTHEA 1.19 (4/11/2026, «Compatto dalla prossima»): the files of 1.18 (sections in XML) open, are saved in the compact form and read back
        // with the same sections. The distributed examples (documents of a sheet) and a project written by the real ANTHEA 1.18.0 (revision,
        // nested section, sheet of the project, sheet of another module: tests/fixtures/f7a-sezioni-compatte/README.md).
        var roundTrips = new JsonArray();
        var oldFiles = Directory.GetFiles(Path.Combine(root, "supporto", "esempi", "distribuzione", "str_palo"), "*.anthea").OrderBy(f => f, StringComparer.Ordinal)
            .Append(Path.Combine(root, "tests", "fixtures", "f7a-sezioni-compatte", "progetto-1.18.0.programma")).ToArray();
        Check(oldFiles.Length == 16, "File storici con sezioni in XML mancanti");
        foreach (string oldFile in oldFiles)
        {
            string name = Path.GetFileName(oldFile);
            var oldWire = JsonNode.Parse(File.ReadAllText(oldFile))!.AsObject();
            Check(oldWire.D("versione") == 3 && Sections(oldWire).All(s => s.Input[ConcreteSectionArchive.Key].D("versione") == 1), name + ": file storico non di 1.18 (versione 3, sezioni in XML)");
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var old = DocumentService.Open(oldFile);
            double openXml = watch.Elapsed.TotalMilliseconds;
            var before = Sections(old).ToDictionary(s => s.Where, s => (Section: ConcreteSectionArchive.Restore(s.Input, s.Workspace, requireCurrent: true)!, Document: GpcDocument(s.Input)));
            string target = Path.Combine(output, "compatto-" + name);
            watch.Restart(); DocumentService.Save(target, old); double save = watch.Elapsed.TotalMilliseconds;
            var wire = JsonNode.Parse(File.ReadAllText(target))!.AsObject();
            Check(wire.D("versione") == 5 && wire.D("versione_senza_compatto") == 3 && wire.D("versione_base") == oldWire.D("versione_base") &&
                Sections(wire).Count() == Sections(oldWire).Count() && Sections(wire).All(s => CompactArchive(s.Input)), name + ": salvataggio non passato al formato compatto");
            RefusedAsNewer(wire, name);
            watch.Restart(); var reopened = DocumentService.Open(target); double openCompact = watch.Elapsed.TotalMilliseconds;
            if (!JsonNode.DeepEquals(Editor(reopened), Editor(old)))
            {
                File.WriteAllText(Path.Combine(output, "editor-xml-" + name + ".json"), Editor(old)!.ToJsonString(J.Options));
                File.WriteAllText(Path.Combine(output, "editor-compatto-" + name + ".json"), Editor(reopened)!.ToJsonString(J.Options));
            }
            Check(JsonNode.DeepEquals(Editor(reopened), Editor(old)), name + ": dati dell'editor cambiati dal passaggio al compatto");
            var after = Sections(reopened).ToList();
            Check(after.Count == before.Count && after.All(s => before.ContainsKey(s.Where)), name + ": sezioni perse o spostate");
            foreach (var s in after)
            {
                var (xml, document) = before[s.Where]; var compact = ConcreteSectionArchive.Restore(s.Input, s.Workspace, requireCurrent: true)!;
                // The same GPC model: the document of ModelArchive written from the section read from the XML and from the compact archive.
                Check(GpcDocument(s.Input) == document, name + " " + s.Where + ": modello GPC riletto dal compatto diverso da quello dell'XML");
                Check(compact.Guid == xml.Guid && compact.Area == xml.Area && compact.Jxx == xml.Jxx && compact.Jyy == xml.Jyy && compact.Jxy == xml.Jxy &&
                    compact.Centroid.X == xml.Centroid.X && compact.Centroid.Y == xml.Centroid.Y && compact.AreaRebars == xml.AreaRebars &&
                    compact.Rebars.Select(b => (b.Area, b.Position.X, b.Position.Y, b.EpsilonP)).SequenceEqual(xml.Rebars.Select(b => (b.Area, b.Position.X, b.Position.Y, b.EpsilonP))),
                    name + " " + s.Where + ": sezione diversa dopo il passaggio al compatto");
                // The same solver on the section read from the XML and from the compact archive, where the sheet has the SLE options.
                if (s.Workspace["sle"]?["SLE"] is JsonObject options)
                {
                    var source = Sections(old).Single(o => o.Where == s.Where);
                    var force = new ActionPoint(-500, 10, 5);
                    var stressXml = new CheckerSection(CheckerSection.PrepareModel(source.Input, source.Workspace), source.Input, source.Workspace, options).Stress(force, "SLE");
                    var stressCompact = new CheckerSection(CheckerSection.PrepareModel(s.Input, s.Workspace), s.Input, s.Workspace, options).Stress(force, "SLE");
                    Check(stressXml.tensioni_barre.SequenceEqual(stressCompact.tensioni_barre) && stressXml.FiberStresses.SequenceEqual(stressCompact.FiberStresses),
                        name + " " + s.Where + ": tensioni diverse dopo il passaggio al compatto");
                }
            }
            string again = Path.Combine(output, "compatto-di-nuovo-" + name);
            DocumentService.Save(again, reopened);
            Check(File.ReadAllBytes(again).AsSpan().SequenceEqual(File.ReadAllBytes(target)), name + ": il secondo salvataggio cambia il file compatto");
            roundTrips.Add(J.Obj(("file", name), ("sezioni", after.Count), ("byte_xml", new FileInfo(oldFile).Length), ("byte_compatto", new FileInfo(target).Length),
                ("apertura_xml_ms", Math.Round(openXml, 1)), ("salvataggio_ms", Math.Round(save, 1)), ("apertura_compatto_ms", Math.Round(openCompact, 1))));
        }
        File.WriteAllText(Path.Combine(output, "sezioni-compatte.json"), roundTrips.ToJsonString(J.Options));
        File.WriteAllText(Path.Combine(output, "persistenza-sezioni.json"), evidence.ToJsonString(J.Options));
        return checks;
    }

    /// <summary>The editing contract of a document (<see cref="EditorData"/>) with the references to the pool of the revisions resolved: the keys of
    /// the pool are hashes of the data, so of the native archives, and change with them; the data they point to are compared.</summary>
    static JsonNode? Editor(JsonObject document)
    {
        var pool = document["archivio_revisioni"] as JsonObject;
        JsonNode? Resolve(JsonNode? node)
        {
            if (node is JsonObject obj)
            {
                var copy = new JsonObject();
                foreach (var (key, value) in obj)
                {
                    if (key == "archivio_revisioni") continue;
                    if (key == "dati_ref" && pool?[obj.S("dati_ref")] is JsonObject data) copy["dati"] = Resolve(data);
                    else copy[key] = Resolve(value);
                }
                return copy;
            }
            if (node is JsonArray array) return new JsonArray(array.Select(Resolve).ToArray());
            return node?.DeepClone();
        }
        return EditorData(Resolve(document));
    }

    /// <summary>The concrete sections of a document, opened or as written, by their path: the data of every sheet with a native section (live sheets
    /// and contexts of the revisions), a reference to the pool of the revisions (a written project, a context of a revision) read in the pool.</summary>
    static IEnumerable<(string Where, JsonObject Input, JsonObject Workspace)> Sections(JsonObject document) => Sections(document, document["archivio_revisioni"] as JsonObject, "");

    static IEnumerable<(string Where, JsonObject Input, JsonObject Workspace)> Sections(JsonNode? node, JsonObject? pool, string where)
    {
        if (node is JsonObject obj)
        {
            if (obj["input"] is JsonObject input && input.ContainsKey(ConcreteSectionArchive.Key))
            {
                yield return (where, input, obj["workspace_ca"] as JsonObject ?? new JsonObject());
                yield break;
            }
            foreach (var (key, value) in obj)
            {
                if (key == "archivio_revisioni") continue;
                var next = key == "dati_ref" && pool?[obj.S("dati_ref")] is JsonObject data ? data : value;
                foreach (var s in Sections(next, pool, where + "/" + (key == "dati_ref" ? "dati" : key))) yield return s;
            }
        }
        else if (node is JsonArray array)
            for (int i = 0; i < array.Count; i++)
                foreach (var s in Sections(array[i], pool, where + "[" + i + "]")) yield return s;
    }

    /// <summary>The GPC model of a section (XML of version 1 or compact archive of version 2) as the document of ModelArchive.SaveDocument, with
    /// the GUIDs numbered by first appearance (the geometry gets new ones at every reading).</summary>
    static string GpcDocument(JsonObject input)
    {
        var saved = input[ConcreteSectionArchive.Key]!.AsObject();
        string text = saved.S("archivio");
        byte[] bytes = saved.D("versione") == ConcreteSectionArchive.CompactVersion ? Convert.FromBase64String(text) : System.Text.Encoding.UTF8.GetBytes(text);
        GPC.Model.Models.Model model;
        using (var stream = new MemoryStream(bytes, writable: false)) model = GPC.Model.Persistence.ModelArchive.Load(stream);
        using var written = new MemoryStream();
        GPC.Model.Persistence.ModelArchive.SaveDocument(model, written);
        var ids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        return System.Text.RegularExpressions.Regex.Replace(System.Text.Encoding.UTF8.GetString(written.ToArray()),
            "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", m => ids.TryGetValue(m.Value, out var id) ? id : ids[m.Value] = "<guid " + (ids.Count + 1) + ">");
    }
}
