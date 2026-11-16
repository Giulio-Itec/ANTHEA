using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;

/// <summary>
/// Rilievi R8 e R5 della prova dell'utente del 16/11/2026 (registro MOD-RIL-R8): the copy taken from the document and read in windows on any
/// thread, with its progress and the state of its fingerprint; an altered copy is still refused.
/// </summary>
static partial class Program
{
    static void ArchivedCopyReadingChecks(ModelDocumentEntry second)
    {
        string revision = second.Revision;
        var linear = new ModelCombinationDefinition("ANTHEA/sum", "Somma", ModelCombinationKind.Linear, "ANTHEA", "", [new("G", false, 2), new("Q", false, -.5)], true);
        // The copy read again from the project and its archive keeps the state too: its first publication does not inflate its sets again.
        var container = new JsonObject(); var archive = new ModelResultArchive(null);
        string id = ProjectModelStore.Set(container, second, null, archive);
        var read = ProjectModelStore.Read(container, id, archive)!;
        Check(read.Fingerprint() == revision && Checkpoint(read) is not null, "R1/R8: the copy read from the archive keeps the state of its fingerprint");
        // R5/R8: the viewer keys its saved views with the fingerprint known from the entry, the same value, without serializing the copy again.
        var inline = new JsonObject(); string small = ProjectModelStore.Set(inline, second.Snapshot);
        Check(read.KnownFingerprint() == read.Fingerprint() && second.Snapshot.KnownFingerprint() == second.Revision &&
              ProjectModelStore.Read(inline, small)!.KnownFingerprint() == second.Revision && CombinationModel().KnownFingerprint() == CombinationModel().Fingerprint(),
            "R5/R8: the known fingerprint of a copy prepared or read back equals its fingerprint, and a copy without it computes it");
        // R8: the governing records read back share their texts and equal vectors, as the calculation made them (same values, same fingerprint).
        foreach (var set in read.Results.Where(r => r.Combination is { Governing.Length: > 1 }))
        {
            var governing = set.Combination!.Governing;
            Check(governing.GroupBy(g => g.Family).All(f => f.All(g => ReferenceEquals(g.Family, f.First().Family))) &&
                  governing.GroupBy(g => g.Alternative).All(a => a.All(g => ReferenceEquals(g.Alternative, a.First().Alternative))) &&
                  governing.Select(g => g.ConcomitantValues).Distinct(ReferenceEqualityComparer.Instance).Count() ==
                  governing.Select(g => string.Join(";", g.ConcomitantValues.Select(BitConverter.DoubleToInt64Bits))).Distinct().Count(),
                "R8: the governing records read back share texts and vectors: " + set.Name);
        }
        var reopened = ModelDocumentEntry.Prepare(ModelCombinations.Save(read, linear with { Id = "ANTHEA/terza", Name = "Terza" }), 1);
        Check(reopened.Revision == reopened.Snapshot.Fingerprint(), "R1/R8: the first publication of a copy read again has the complete fingerprint");
        // R8: the reading in windows reports its progress and refuses an altered copy as before.
        var shares = new List<double>();
        var reader = ProjectModelStore.Capture(container, id, archive)!;
        var again = reader.Read(new SynchronousShare(shares.Add));
        Check(again.Fingerprint() == revision && reader.ResultBytes > 0 && shares.Count > 0 && shares[^1] == 1, "R8: the copy taken from the document is read on any thread, with its progress");
        var entry = ProjectModelStore.Models(container).Single(); entry["revisione"] = new string('0', 64);
        Reject(() => ProjectModelStore.Read(container, id, archive), "R8: the reading in windows still refuses a copy whose fingerprint does not match");
    }

    sealed class SynchronousShare(Action<double> report) : IProgress<double> { public void Report(double value) => report(value); }
}
