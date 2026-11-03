using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace X.Core;

/// <summary>
/// The coherence of the sections of an open project, kept by the content of the sheets (docs/sviluppo/progetto-sistema.md, step 3,
/// 4/11/2026). Until then every edit computed it again for every section of the tree, and the same pair of sheets once for each level that
/// contains it: with 576 sheets 640 ms per edit. It depends only on the sheets that a section sees (its own, those of its subsections and
/// those of its ancestors, in their order and place): the fields of a sheet are kept by the SHA-256 of its JSON, the comparison of two
/// sheets by the two hashes, the checks of the cover of a sheet by its hash and those of its Materiali, the summary of a section by the
/// hashes of all the sheets it sees. The results are those of ProjectComparison, ProjectValidation.Warnings and
/// ProjectSharedData.MissingSoilLayers, computed by the same code on a miss; an undo, which builds new objects, finds them again.
/// </summary>
public sealed class ProjectCoherenceCache
{
    /// <summary>What the badge of a section needs.</summary>
    /// <param name="HasSheets">True when the section or a subsection has sheets.</param>
    /// <param name="Matches">Common fields compared between the sheets.</param>
    /// <param name="DifferenceKeys">Distinct common fields with different values.</param>
    /// <param name="Warnings">The warnings of ProjectValidation.Warnings, in their order.</param>
    /// <param name="MissingLayers">Soil layers present in one sheet and missing in another.</param>
    public sealed record Summary(bool HasSheets, int Matches, int DifferenceKeys, string[] Warnings, int MissingLayers);

    private sealed record Pair(int Matches, string[] Differences);

    private readonly Dictionary<JsonObject, string> prints = new(ReferenceEqualityComparer.Instance);
    private Dictionary<string, Dictionary<string, ProjectSharedData.Field>> fields = new(StringComparer.Ordinal), oldFields = new(StringComparer.Ordinal);
    private Dictionary<string, Pair> pairs = new(StringComparer.Ordinal), oldPairs = new(StringComparer.Ordinal);
    private Dictionary<string, ProjectValidation.CoverStatus[]> covers = new(StringComparer.Ordinal), oldCovers = new(StringComparer.Ordinal);
    private Dictionary<string, Summary> summaries = new(StringComparer.Ordinal), oldSummaries = new(StringComparer.Ordinal);

    /// <summary>Hits and misses of the summaries since the cache was made (for the checks and the bench).</summary>
    public int Hits { get; private set; }

    /// <inheritdoc cref="Hits"/>
    public int Misses { get; private set; }

    /// <summary>
    /// A new reading of the document (a refresh of the tree, an overview): the hashes of the sheets are taken again, and what the last two
    /// readings did not use leaves the cache. Nothing may change the document during a reading.
    /// </summary>
    public void NewReading()
    {
        prints.Clear();
        (oldFields, fields) = (fields, new(StringComparer.Ordinal));
        (oldPairs, pairs) = (pairs, new(StringComparer.Ordinal));
        (oldCovers, covers) = (covers, new(StringComparer.Ordinal));
        (oldSummaries, summaries) = (summaries, new(StringComparer.Ordinal));
    }

    /// <summary>The summary of <paramref name="section"/> for its badge, from the cache or computed.</summary>
    public Summary Of(JsonObject section)
    {
        ArgumentNullException.ThrowIfNull(section);
        string key = ScopeKey(section);
        if (Keep(summaries, oldSummaries, key) is { } known) { Hits++; return known; }
        Misses++;
        var summary = Compute(section);
        summaries[key] = summary;
        return summary;
    }

    private Summary Compute(JsonObject section)
    {
        bool hasSheets = ProjectSharedData.SubtreeSheets(section).Any();
        int matches = 0;
        var differences = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (first, second) in ProjectSharedData.ComparisonPairs(section))
        {
            var pair = PairOf(first, second);
            matches += pair.Matches;
            differences.UnionWith(pair.Differences);
        }
        var cover = new List<ProjectValidation.CoverStatus>();
        foreach (var sheet in ProjectValidation.CoverSheets(section)) cover.AddRange(CoverOf(sheet, ProjectValidation.CoverMaterials(sheet, section)));
        var warnings = ProjectSharedData.Limitations(section, FieldsOf).Concat(cover.Where(c => c.Passed != true).Select(c => c.Text)).Distinct().ToArray();
        return new(hasSheets, matches, differences.Count, warnings, ProjectSharedData.MissingSoilLayers(section).Count);
    }

    /// <summary>The comparison of two sheets as ProjectComparison makes it: the common fields and those with different values.</summary>
    private Pair PairOf(JsonObject first, JsonObject second)
    {
        string key = Print(first) + "|" + Print(second);
        if (Keep(pairs, oldPairs, key) is { } known) return known;
        int matches = 0;
        var differences = new List<string>();
        foreach (var (a, b) in ProjectSharedData.ComparableFields(first, second, FieldsOf(first), FieldsOf(second)))
        {
            matches++;
            if (!ProjectSharedData.Equal(a.Value, b.Value)) differences.Add(a.Key);
        }
        var pair = new Pair(matches, [.. differences]);
        pairs[key] = pair;
        return pair;
    }

    private ProjectValidation.CoverStatus[] CoverOf(JsonObject sheet, JsonObject[] materials)
    {
        string key = Print(sheet) + "|" + string.Join("|", materials.Select(Print));
        if (Keep(covers, oldCovers, key) is { } known) return known;
        var result = new List<ProjectValidation.CoverStatus>();
        ProjectValidation.CoverChecks(sheet, materials, result);
        var statuses = result.ToArray();
        covers[key] = statuses;
        return statuses;
    }

    private Dictionary<string, ProjectSharedData.Field> FieldsOf(JsonObject sheet)
    {
        string key = Print(sheet);
        if (Keep(fields, oldFields, key) is { } known) return known;
        var result = ProjectSharedData.Fields(sheet);
        fields[key] = result;
        return result;
    }

    /// <summary>
    /// The key of what <paramref name="section"/> sees: the sheets of its ancestors, level by level from the project, then its sections in
    /// order, each with its sheets and then its subsections inside it.
    /// </summary>
    private string ScopeKey(JsonObject section)
    {
        var text = new StringBuilder();
        foreach (var ancestor in ProjectSharedData.Ancestors(section).Reverse())
        {
            text.Append("A[");
            foreach (var sheet in ancestor.Array("fogli").OfType<JsonObject>()) text.Append(Print(sheet)).Append(',');
            text.Append(']');
        }
        void Walk(JsonObject node)
        {
            text.Append("S[");
            foreach (var sheet in node.Array("fogli").OfType<JsonObject>()) text.Append(Print(sheet)).Append(',');
            text.Append(']');
            foreach (var child in node.Array("strutture").OfType<JsonObject>()) Walk(child);
            text.Append("/S");
        }
        Walk(section);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    /// <summary>The SHA-256 of the JSON of a sheet, once per reading.</summary>
    private string Print(JsonObject sheet)
    {
        if (prints.TryGetValue(sheet, out var known)) return known;
        string print = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(sheet)));
        prints[sheet] = print;
        return print;
    }

    /// <summary>The value of <paramref name="key"/> in this reading, or brought from the previous one.</summary>
    private static T? Keep<T>(Dictionary<string, T> current, Dictionary<string, T> previous, string key) where T : class
    {
        if (current.TryGetValue(key, out var value)) return value;
        if (!previous.TryGetValue(key, out value)) return null;
        current[key] = value;
        return value;
    }
}
