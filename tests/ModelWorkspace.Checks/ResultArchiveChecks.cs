using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelWorkspace;
using Anthea.Application.Documents;
using Anthea.Calculations;
using X.Core;

/// <summary>
/// Results archive beside the project (decision of the user of 2/11/2026, «Blocchi accanto»): round trip, the limit below and above, the
/// document of 1.17.0 written again byte for byte, damaged block and missing archive, equal blocks stored once and the pruning of the
/// history, «Salva con nome», the order archive → document. The flow of WorkspaceSession.Save: ModelResultArchive.Save with DocumentService.Save.
/// </summary>
static partial class Program
{
    /// <summary>Layout of the archive file (format 1, ModelResultArchive): read here to damage one result block on purpose.</summary>
    const int ArchiveCountAt = 20, ArchiveIndexAt = 24, ArchiveEntryBytes = 57, ArchiveKindAt = 32, ArchiveOffsetAt = 33, ArchiveStoredAt = 41;

    static async Task ResultArchiveChecks(string output)
    {
        string folder = Path.Combine(output, "archivio-risultati"); if (Directory.Exists(folder)) Directory.Delete(folder, true); Directory.CreateDirectory(folder);
        // A copy with imported cases and derived results (a linear combination and an envelope with its governing states).
        var small = SyntheticMidasModel(3, 2, 24, 12);
        var model = AddUserCombinations(ModelCombinationGenerator.Apply(small.Snapshot, ModelCombinationGenerator.Generate(small.Snapshot, small.Natures)), small);
        var service = new GpcModelCombinationService();
        foreach (var definition in new[] { "ANTHEA/utente-somma", "ANTHEA/utente-inviluppo-slu" }) model = ModelCombinations.Attach(model, definition, await service.CalculateAsync(model, definition));
        string fingerprint = model.Fingerprint();
        long resultBytes = model.Results.Sum(r => (long)JsonSerializer.SerializeToUtf8Bytes(r).Length);

        // Below the limit: the document of 1.17.0, byte for byte, and no archive.
        var (doc, sub) = ArchiveDocument();
        string id = ProjectModelStore.Set(sub, model);
        string inline17 = Path.Combine(folder, "in-linea-1.17.0.programma"); DocumentService.Save(inline17, doc);
        var archive = new ModelResultArchive(null);
        var below = ModelDocumentEntry.Prepare(model, resultBytes + 1);
        ProjectModelStore.Set(sub, below, id, archive);
        string inlineNew = Path.Combine(folder, "in-linea.programma");
        archive.Save(inlineNew, doc, () => DocumentService.Save(inlineNew, doc));
        Check(!below.IsArchived && File.ReadAllBytes(inlineNew).AsSpan().SequenceEqual(File.ReadAllBytes(inline17)) && !File.Exists(ModelResultArchive.PathFor(inlineNew)),
            "results archive: below the limit the project file is the one of 1.17.0, byte for byte, without an archive beside it");
        var reopened17 = DocumentService.Open(inline17); var rewritten = Path.Combine(folder, "riscritto.programma");
        new ModelResultArchive(inline17, reopened17).Save(rewritten, reopened17, () => DocumentService.Save(rewritten, reopened17));
        Check(File.ReadAllBytes(rewritten).AsSpan().SequenceEqual(File.ReadAllBytes(inline17)), "results archive: a document of 1.17.0 opened and saved again is the same file");

        // Above the limit: the results in the archive, the document of version 4, the same copy when read again.
        var above = ModelDocumentEntry.Prepare(model, resultBytes);
        Check(above.IsArchived && above.Revision == fingerprint, "results archive: above the limit the entry goes to the archive with the fingerprint of the whole copy");
        (doc, sub) = ArchiveDocument(); archive = new ModelResultArchive(null);
        id = ProjectModelStore.Set(sub, above, null, archive);
        string archived = Path.Combine(folder, "archivio.programma");
        Check(archive.Save(archived, doc, () => DocumentService.Save(archived, doc)) == 0 && File.Exists(ModelResultArchive.PathFor(archived)), "results archive: the save writes the archive beside the project");
        var raw = (JsonObject)JsonNode.Parse(File.ReadAllText(archived))!;
        Check(J.Number(raw["versione"]) == 4 && new FileInfo(archived).Length < new FileInfo(inline17).Length, "results archive: the project file has version 4 and is smaller");
        try { DocumentService.Validate(raw); throw new InvalidOperationException("Accepted: version 4 read as a project of 1.17.0"); }
        catch (ArgumentException error) { Check(error.Message == "Formato o versione del file non supportati.", "results archive: ANTHEA 1.17.0 refuses the file with «Formato o versione del file non supportati.»"); }
        var (read, readSub) = Reopen(archived);
        var back = ProjectModelStore.Read(readSub, id, read)!;
        Check(back.Fingerprint() == fingerprint && back.UnavailableResults.Count == 0 && JsonSerializer.Serialize(back.Results) == JsonSerializer.Serialize(model.Results),
            "results archive: round trip, every result set bit-identical and the same fingerprint");
        Check(ModelCombinations.Inspect(back).Single(c => c.Definition.Id == "ANTHEA/utente-somma").State == "Calcolata", "results archive: the combinations read from the archive stay «Calcolata»");

        // The archive is written before the document: a save that stops after it leaves the previous document readable.
        var changed = ModelCombinations.RestoreImported(model); changed.Name = "Copia cambiata";
        ProjectModelStore.Set(sub, ModelDocumentEntry.Prepare(changed, 1), id, archive);
        try { archive.Save(archived, doc, () => throw new IOException("interruzione simulata")); } catch (IOException) { }
        (read, readSub) = Reopen(archived);
        Check(ProjectModelStore.Read(readSub, id, read)!.Fingerprint() == fingerprint, "results archive: a save stopped between archive and document leaves the previous document whole");

        // «Salva con nome»: the archive goes with the copy, the old one does not change.
        var before = File.ReadAllBytes(ModelResultArchive.PathFor(archived));
        Directory.CreateDirectory(Path.Combine(folder, "altra cartella"));
        string saveAs = Path.Combine(folder, "altra cartella", "copia del progetto.programma");
        archive.Save(saveAs, doc, () => DocumentService.Save(saveAs, doc));
        (read, readSub) = Reopen(saveAs);
        Check(ProjectModelStore.Read(readSub, id, read)!.Fingerprint() == changed.Fingerprint() && File.ReadAllBytes(ModelResultArchive.PathFor(archived)).AsSpan().SequenceEqual(before),
            "results archive: «Salva con nome» writes the archive beside the new file and leaves the old one as it was");

        // A damaged block: the model opens without that result set, with the reason; the combination is «Da calcolare».
        string damaged = Path.Combine(folder, "danneggiato.programma"); File.Copy(archived, damaged);
        var bytes = File.ReadAllBytes(ModelResultArchive.PathFor(archived));
        var (offset, stored) = FirstResultBlock(bytes); bytes[offset + stored / 2] ^= 0xFF;
        File.WriteAllBytes(ModelResultArchive.PathFor(damaged), bytes);
        (read, readSub) = Reopen(damaged);
        var partial = ProjectModelStore.Read(readSub, id, read)!;
        Check(partial.UnavailableResults.Count == 1 && partial.UnavailableResults[0].Reason.Contains("danneggiato", StringComparison.Ordinal) &&
            partial.Results.Length == model.Results.Length - 1, "results archive: a damaged block leaves out its result set only, with the reason, without errors");
        // Missing archive: geometry, tables and definitions are in the document; every result set is listed, combinations «Da calcolare».
        string missing = Path.Combine(folder, "senza archivio.programma"); File.Copy(archived, missing);
        (read, readSub) = Reopen(missing);
        var bare = ProjectModelStore.Read(readSub, id, read)!;
        var catalogue = ModelCombinations.Inspect(bare);
        Check(bare.Results.Length == 0 && bare.UnavailableResults.Count == model.Results.Length && bare.UnavailableResults.All(u => u.Reason.StartsWith("archivio dei risultati mancante", StringComparison.Ordinal)) &&
            bare.Elements.Length == model.Elements.Length && catalogue.Length == ModelCombinations.Inspect(model).Length,
            "results archive: without the archive the model opens with geometry, tables and definitions and lists the missing results");
        Check(catalogue.Single(c => c.Definition.Id == "ANTHEA/utente-somma").Message.Contains("Non letti dall'archivio dei risultati", StringComparison.Ordinal),
            "results archive: the catalogue gives the reason of the missing cases");
        using (var vm = new ModelViewerViewModel(bare, [], false, new FakeServices(), _ => { }, (_, _, _, _) => { }))
            Check(vm.Status.Contains("non letti dall'archivio dei risultati", StringComparison.Ordinal), "results archive: the view says which results were not read and why");

        // Equal blocks once; the history keeps the current copy and four saved ones; the save drops the blocks no longer cited.
        (doc, sub) = ArchiveDocument(); archive = new ModelResultArchive(null);
        string history = Path.Combine(folder, "storico.programma");
        string first = ProjectModelStore.Set(sub, ModelDocumentEntry.Prepare(model, 1), null, archive);
        string second = ProjectModelStore.Set(sub, ModelDocumentEntry.Prepare(model, 1), null, archive);
        archive.Save(history, doc, () => DocumentService.Save(history, doc));
        Check(ArchiveBlocks(history) == ModelResultArchive.References(doc).Count && ArchiveBlocks(history) == model.Results.Length + 1,
            "results archive: two models with the same results store every block once");
        var revisions = new List<string>();
        for (int k = 1; k <= ModelResultArchive.RetainedCopies + 1; k++)
        {
            var next = ModelCombinations.RestoreImported(model); next.Name = "Revisione " + k;
            ProjectModelStore.Set(sub, ModelDocumentEntry.Prepare(next, 1), first, archive); revisions.Add(next.Fingerprint());
            archive.Save(history, doc, () => DocumentService.Save(history, doc));
        }
        archive.Save(history, doc, () => DocumentService.Save(history, doc));
        var kept = ProjectModelStore.History(sub, first);
        Check(kept.Count == ModelResultArchive.RetainedCopies - 1 && kept[0].Revision == revisions[^2] && ArchiveBlocks(history) == ModelResultArchive.References(doc).Count,
            "results archive: the history keeps four saved copies with the current one and the save drops the blocks no longer cited");
        (read, readSub) = Reopen(history);
        Check(ProjectModelStore.ReadRevision(readSub, first, kept[^1].Revision, read).Fingerprint() == kept[^1].Revision &&
            ProjectModelStore.Read(readSub, second, read)!.Fingerprint() == fingerprint, "results archive: a copy of the history and the other model read again from the archive");
        Reject(() => ProjectModelStore.ReadRevision(readSub, first, revisions[0], read), "results archive: a copy beyond the history is not kept");
    }

    static (JsonObject Document, JsonObject Section) ArchiveDocument()
    {
        var doc = ProjectDocuments.CreateArchive(); var project = ProjectDocuments.AddProject(doc, "Progetto con archivio");
        var section = ProjectDocuments.AddSection(project, "Fase");
        return (doc, section);
    }

    /// <summary>A saved project opened as the shell does: the document, then its archive (nothing read from it yet).</summary>
    static (ModelResultArchive Archive, JsonObject Section) Reopen(string path)
    {
        var document = DocumentService.Open(path);
        return (new ModelResultArchive(path, document), ArchiveSection(document));
    }

    /// <summary>The section «Fase» of a document of these checks.</summary>
    static JsonObject ArchiveSection(JsonObject document) => document.Array("progetti").OfType<JsonObject>().SelectMany(ProjectModelStore.Containers).Single(c => c.S("nome") == "Fase");

    static int ArchiveBlocks(string documentPath) => BinaryPrimitives.ReadInt32LittleEndian(File.ReadAllBytes(ModelResultArchive.PathFor(documentPath)).AsSpan(ArchiveCountAt));

    /// <summary>Position and compressed length of the first result block of an archive file.</summary>
    static (int Offset, int Stored) FirstResultBlock(byte[] archive)
    {
        int count = BinaryPrimitives.ReadInt32LittleEndian(archive.AsSpan(ArchiveCountAt)); long index = BinaryPrimitives.ReadInt64LittleEndian(archive.AsSpan(ArchiveIndexAt));
        for (int i = 0; i < count; i++)
        {
            var row = archive.AsSpan((int)index + i * ArchiveEntryBytes, ArchiveEntryBytes);
            if (row[ArchiveKindAt] == 0) return ((int)BinaryPrimitives.ReadInt64LittleEndian(row[ArchiveOffsetAt..]), (int)BinaryPrimitives.ReadInt64LittleEndian(row[ArchiveStoredAt..]));
        }
        throw new InvalidOperationException("Nessun blocco di risultati nell'archivio.");
    }
}
