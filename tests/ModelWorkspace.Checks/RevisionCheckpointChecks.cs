using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;

/// <summary>
/// Rilievo R1 della prova dell'utente del 16/11/2026 (registro MOD-RIL-R1): the fingerprint of a copy resumed from the result sets already
/// hashed by the copy it comes from, against the fingerprint of its complete serialization (ModelSnapshot.Fingerprint).
/// </summary>
static partial class Program
{
    static object? Checkpoint(ModelSnapshot snapshot) =>
        typeof(ModelSnapshot).GetField("RevisionCheckpoint", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(snapshot);

    static async Task RevisionCheckpointChecks()
    {
        var linear = new ModelCombinationDefinition("ANTHEA/sum", "Somma", ModelCombinationKind.Linear, "ANTHEA", "", [new("G", false, 2), new("Q", false, -.5)], true);
        var envelope = new ModelCombinationDefinition("ANTHEA/env", "Inviluppo", ModelCombinationKind.Envelope, "ANTHEA", "", [new("G", false, 1), new("Q", false, 1)], true);
        var model = ModelCombinations.Save(ModelCombinations.Save(CombinationModel(), linear), envelope);
        var service = new GpcModelCombinationService();
        // A limit of one byte: the copies go to the results archive as the large ones do.
        var first = ModelDocumentEntry.Prepare(ModelCombinations.Attach(model, linear.Id, await service.CalculateAsync(model, linear.Id)), 1);
        Check(first.IsArchived && first.Revision == first.Snapshot.Fingerprint() && Checkpoint(first.Snapshot) is not null,
            "R1: the first copy prepared for the archive has the fingerprint of its whole serialization and keeps the state of the hash");
        var appended = ModelCombinations.Attach(first.Snapshot, envelope.Id, await service.CalculateAsync(first.Snapshot, envelope.Id));
        Check(ReferenceEquals(Checkpoint(appended), Checkpoint(first.Snapshot)), "R1: the copy with one more result keeps the state of the copy it comes from");
        var second = ModelDocumentEntry.Prepare(appended, 1);
        Check(second.Revision == second.Snapshot.Fingerprint() && !ReferenceEquals(Checkpoint(second.Snapshot), Checkpoint(first.Snapshot)),
            "R1: the fingerprint resumed from the sets already hashed equals the complete one (results appended at the end, as in a series)");
        var freed = ModelDocumentEntry.Prepare(ModelCombinations.DiscardResults(second.Snapshot, [linear.Id]), 1);
        Check(freed.Revision == freed.Snapshot.Fingerprint(), "R1: a copy without a set in the middle («Libera») is hashed again from the start, with the same fingerprint");
        var renamed = ModelCombinations.Save(second.Snapshot, linear with { Id = "ANTHEA/altra", Name = "Altra" });
        var defined = ModelDocumentEntry.Prepare(renamed, 1);
        Check(defined.Revision == defined.Snapshot.Fingerprint(), "R1: a new definition with the same results resumes the hash, the definitions after the results included");
    }
}
