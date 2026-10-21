using System.Globalization;
using System.Text.Json.Nodes;
using static Anthea.Application.EmbeddedWalls.EmbeddedWallAdapter;

namespace Anthea.Application.EmbeddedWalls;

/// <summary>Lock-off of one activation row, kN per element. Previous is the value of the same support in the preceding
/// stage of the history; null when the support is installed or reinstalled in this stage.</summary>
public sealed record EmbeddedWallLockOff(JsonObject Row, string Stage, string Support, double Prestress, double? Previous)
{
    public bool Reduced => Previous is double before && Prestress < before;
}

public static partial class EmbeddedWallDocument
{
    /// <summary>Lock-offs of one history in stage order. An empty cell keeps the value the support had in the preceding stage;
    /// a support installed without a value starts from 0. A number replaces the value and 0 releases it.
    /// Document semantics only: GPC receives the resulting value of every stage. Null case: all rows (single history).</summary>
    public static IReadOnlyList<EmbeddedWallLockOff> LockOffs(JsonObject data, string? caseId)
    {
        string principal = Text(data, "case_id");
        bool Owned(JsonObject row) => caseId is null || (string.IsNullOrWhiteSpace(Text(row, "case")) ? principal : Text(row, "case")) == caseId;
        var result = new List<EmbeddedWallLockOff>(); var previous = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var stage in Rows(data, "stages").Where(Owned))
        {
            string id = Text(stage, "id"); var current = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var row in Rows(data, "activations").Where(r => Owned(r) && Text(r, "stage") == id))
            {
                string support = Text(row, "support"); double? before = previous.TryGetValue(support, out var kept) ? kept : null;
                double value = Optional(row, "prestress") ?? before ?? 0;
                current[support] = value; result.Add(new(row, id, support, value, before));
            }
            previous = current;
        }
        return result.AsReadOnly();
    }

    /// <summary>Notice for lock-offs lower than in the preceding stage, or null. Lowering is accepted as an explicit choice.</summary>
    public static string? ReducedLockOffs(IEnumerable<EmbeddedWallLockOff> lockOffs)
    {
        static string F(double value) => value.ToString("0.###", CultureInfo.GetCultureInfo("it-IT"));
        var reduced = lockOffs.Where(l => l.Reduced).Select(l => $"{l.Support} da {F(l.Previous!.Value)} a {F(l.Prestress)} kN nella fase {l.Stage}").ToArray();
        return reduced.Length == 0 ? null : "Pretensione ridotta rispetto alla fase precedente: " + string.Join("; ", reduced)
            + ". Il valore assegnato sostituisce la pretensione e 0 la rilascia; la cella vuota la mantiene.";
    }
}
