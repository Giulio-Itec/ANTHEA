using System.Text.Json;

namespace ANTHEA.ModelWorkspace;

/// <summary>Preserves previously normalized data only when every source table and geometric value is unchanged.</summary>
public static class MidasNormalizationReuse
{
    public static bool TryApply(ModelSnapshot? previous, ModelSnapshot incoming, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (previous?.SourceTables is not { Count: > 0 } before || incoming.SourceTables is not { Count: > 0 } after ||
            previous.LocalFrames == null && previous.NodeFrames == null && previous.Boundaries == null ||
            incoming.LocalFrames != null || incoming.NodeFrames != null || incoming.Boundaries != null) return false;
        string[] required = ["UNIT", "NODE", "ELEM", "SECT", "THIK", "MATL", "SKEW", "CONS", "OFFS", "FRLS", "PRLS", "ELNK", "RIGD", "MCON", "NSPR", "NLNK", "NLLP"];
        if (required.Any(t => !before.ContainsKey(t) || !after.ContainsKey(t))) return false;
        if (MidasReadOnlyClient.Signature(before) != MidasReadOnlyClient.Signature(after)) return false;
        static Dictionary<string, JsonElement> Geometry(ModelSnapshot s) => new()
        {
            ["Nodes"] = JsonSerializer.SerializeToElement(s.Nodes.OrderBy(n => n.Id)),
            ["Elements"] = JsonSerializer.SerializeToElement(s.Elements.OrderBy(e => e.Id)),
            ["Sections"] = JsonSerializer.SerializeToElement(s.Sections.OrderBy(s => s.Id)),
            ["Plates"] = JsonSerializer.SerializeToElement(s.Plates.OrderBy(p => p.Id))
        };
        if (MidasReadOnlyClient.Signature(Geometry(previous)) != MidasReadOnlyClient.Signature(Geometry(incoming))) return false;
        token.ThrowIfCancellationRequested();
        incoming.LocalFrames = previous.LocalFrames?.ToArray(); incoming.NodeFrames = previous.NodeFrames?.ToArray();
        incoming.Boundaries = previous.Boundaries?.Select(b => b with { Nodes = b.Nodes.ToArray() }).ToArray();
        incoming.ImportMessages = (incoming.ImportMessages ?? []).Where(m => m.Code != "MIDAS-CONVERTER-PENDING").Append(new(
            "MIDAS-NORMALIZATION-REUSED", "info", "Geometria",
            "Tutte le tabelle sorgente e la geometria coincidono con la copia precedente: conservate le terne e le assegnazioni normalizzate già disponibili. Nessun orientamento o vincolo ricalcolato.")).ToArray();
        incoming.Validate(); return true;
    }
}
