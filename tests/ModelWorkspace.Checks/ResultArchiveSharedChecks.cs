using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;
using ANTHEA.ModelWorkspace;
using Anthea.Application.Documents;
using LegacyArchive;

/// <summary>
/// 3/11/2026, review D1/D2 («per il salvataggio usa GPC.Archive. però vediamo di ottimizzarlo a dovere»): the results archive of ANTHEA is
/// GPC.Archive. Equivalence with ANTHEA 1.18.0 (LegacyResultArchive118, the released code kept in the checks): the same saves give the same
/// archive file byte for byte, and each version reads the files of the other. Then the per-result failures: a damaged reference is one
/// unavailable result, not a project that does not open or save; a damaged archive is set aside instead of stopping the save.
/// </summary>
static partial class Program
{
    static async Task ResultArchiveSharedChecks(string output)
    {
        string folder = Path.Combine(output, "archivio-gpc"); if (Directory.Exists(folder)) Directory.Delete(folder, true); Directory.CreateDirectory(folder);
        var small = SyntheticMidasModel(3, 2, 24, 12);
        var model = AddUserCombinations(ModelCombinationGenerator.Apply(small.Snapshot, ModelCombinationGenerator.Generate(small.Snapshot, small.Natures)), small);
        var service = new GpcModelCombinationService();
        foreach (var definition in new[] { "ANTHEA/utente-somma", "ANTHEA/utente-inviluppo-slu" }) model = ModelCombinations.Attach(model, definition, await service.CalculateAsync(model, definition));

        // The same document saved by GPC.Archive and by 1.18.0: the same archive, byte for byte, also along a history of revisions.
        var (doc, section) = ArchiveDocument(); var archive = new ModelResultArchive(null); var legacy = new LegacyResultArchive118(null);
        string shared = Path.Combine(folder, "gpc.programma"), released = Path.Combine(folder, "1.18.0.programma");
        string id = ProjectModelStore.Set(section, ModelDocumentEntry.Prepare(model, 1), null, archive);
        ProjectModelStore.Set(section, ModelDocumentEntry.Prepare(model, 1), null, archive);
        bool identical = true;
        for (int k = 0; k <= ModelResultArchive.RetainedCopies; k++)
        {
            if (k > 0) { var next = ModelCombinations.RestoreImported(model); next.Name = "Revisione " + k; ProjectModelStore.Set(section, ModelDocumentEntry.Prepare(next, 1), id, archive); }
            archive.Save(shared, doc, () => DocumentService.Save(shared, doc));
            legacy.Add(LegacyBlocks(ModelResultArchive.PathFor(shared)));
            legacy.Save(released, doc, () => DocumentService.Save(released, doc));
            identical &= File.ReadAllBytes(ModelResultArchive.PathFor(shared)).AsSpan().SequenceEqual(File.ReadAllBytes(ModelResultArchive.PathFor(released)));
        }
        Check(identical, "GPC.Archive: every save of a history of revisions writes the archive of ANTHEA 1.18.0 byte for byte");
        var references = ModelResultArchive.References(doc).ToArray();
        var releasedReads = new LegacyResultArchive118(shared, DocumentService.Open(shared)).Read(references);
        Check(releasedReads.All(c => c.Failure is null), "GPC.Archive: ANTHEA 1.18.0 reads every block of an archive written by GPC.Archive");
        var (opened, openedSection) = Reopen(released);
        var back = ProjectModelStore.Read(openedSection, id, opened)!;
        Check(back.UnavailableResults.Count == 0 && back.Fingerprint() == ProjectModelStore.Read(section, id, archive)!.Fingerprint(),
            "GPC.Archive: an archive written by ANTHEA 1.18.0 opens with every result set and the same fingerprint");

        // A damaged reference in the document: one unavailable result set; the project opens, the model opens, the project saves.
        var text = DocumentService.Open(shared);
        var sets = ArchiveSection(text)[ProjectModelStore.Property]!.AsArray().OfType<JsonObject>().Single(m => m["id"]!.GetValue<string>() == id)["risultati"]!.AsArray();
        sets[0]!["blocco"] = 12345; sets[1]!["blocco"] = "non è un indirizzo";
        string damagedReference = Path.Combine(folder, "riferimento danneggiato.programma");
        File.Copy(ModelResultArchive.PathFor(shared), ModelResultArchive.PathFor(damagedReference)); DocumentService.Save(damagedReference, text);
        var reopened = DocumentService.Open(damagedReference);
        Check(Fails(() => new LegacyResultArchive118(damagedReference, reopened)), "GPC.Archive: ANTHEA 1.18.0 could not open the archive of a project with a damaged reference (the defect fixed here)");
        var withDamage = new ModelResultArchive(damagedReference, reopened);
        var partial = ProjectModelStore.Read(ArchiveSection(reopened), id, withDamage)!;
        Check(partial.UnavailableResults.Count == 2 && partial.Results.Length == back.Results.Length - 2,
            "GPC.Archive: two damaged references in the document are two unavailable result sets, the model opens with the others");
        bool written = false;
        withDamage.Save(damagedReference, reopened, () => { written = true; DocumentService.Save(damagedReference, reopened); });
        Check(written, "GPC.Archive: a project with damaged references is saved");

        // A damaged archive (index): the project is saved, the damaged file is set aside byte for byte, never overwritten.
        string damagedArchive = Path.Combine(folder, "archivio danneggiato.programma");
        File.Copy(shared, damagedArchive); var bytes = File.ReadAllBytes(ModelResultArchive.PathFor(shared));
        long index = BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(24)); bytes[index + 3] ^= 0xFF; File.WriteAllBytes(ModelResultArchive.PathFor(damagedArchive), bytes);
        reopened = DocumentService.Open(damagedArchive); var broken = new ModelResultArchive(damagedArchive, reopened);
        Check(ProjectModelStore.Read(ArchiveSection(reopened), id, broken)!.UnavailableResults.All(u => u.Reason.Contains("danneggiato", StringComparison.Ordinal)),
            "GPC.Archive: with a damaged index every result set is unavailable with the reason, the model opens");
        Check(Fails(() => new LegacyResultArchive118(damagedArchive, reopened).Save(damagedArchive, reopened, () => DocumentService.Save(damagedArchive, reopened))),
            "GPC.Archive: ANTHEA 1.18.0 refused to save a project whose archive is damaged (the defect fixed here)");
        written = false;
        broken.Save(damagedArchive, reopened, () => { written = true; DocumentService.Save(damagedArchive, reopened); });
        Check(written && broken.LastSave?.SetAside is { } aside && File.ReadAllBytes(aside).AsSpan().SequenceEqual(bytes),
            "GPC.Archive: a damaged archive does not stop the save; it is kept aside, byte for byte, with another name");
    }

    static bool Fails(Action action) { try { action(); return false; } catch (Exception error) when (error is InvalidOperationException or InvalidDataException or IOException) { return true; } }

    /// <summary>The blocks of an archive file as 1.18.0 holds them in memory (kind, JSON length, compressed bytes), by address.</summary>
    static IEnumerable<KeyValuePair<string, LegacyResultArchive118.Block>> LegacyBlocks(string path)
    {
        var bytes = File.ReadAllBytes(path);
        int count = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(ArchiveCountAt)); long index = BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(ArchiveIndexAt));
        for (int i = 0; i < count; i++)
        {
            var row = bytes.AsSpan((int)index + i * ArchiveEntryBytes, ArchiveEntryBytes);
            long offset = BinaryPrimitives.ReadInt64LittleEndian(row[ArchiveOffsetAt..]), stored = BinaryPrimitives.ReadInt64LittleEndian(row[ArchiveStoredAt..]);
            yield return new(Convert.ToHexString(row[..32]), new(row[ArchiveKindAt], BinaryPrimitives.ReadInt64LittleEndian(row[49..]), bytes.AsSpan((int)offset, (int)stored).ToArray()));
        }
    }
}
