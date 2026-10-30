using System.Text.Json.Nodes;

namespace Anthea.Testing.Comparison;

/// <summary>
/// An admitted transition of a reference comparison (decision D2 of 15/10/2026): one exact difference, identified by file, path, class,
/// value before and value after, with the register entry that explains it. It admits only that difference: no tolerance, no pattern.
/// </summary>
public sealed record Transition(string File, string Path, string Class, string? A, string? B, string Entry)
{
    public (string, string, string, string?, string?) Key => (File, Path, Class, A, B);

    /// <summary>
    /// Articles of the Wiki in the F3 capture declared by the editorial record: 78 in Rev62, 82 in Rev63 with the
    /// four practical/theoretical chapters about climate actions, loads, combinations and traffic (AZIONI-01).
    /// </summary>
    public const string EditorialArticleCount = "82";

    /// <summary>Keep F4 engineering transitions immutable. The separate F5 editorial record replaces only the article count
    /// and adds exact Wiki differences; it cannot admit any engineering result, missing article, or numerical body change.</summary>
    public static IReadOnlyList<Transition> WithEditorialUpdate(IReadOnlyList<Transition> historical, IReadOnlyList<Transition> editorial)
    {
        static bool Count(Transition t) => t.File == "esito.txt" && t.Path == "riga 1#2" && t.Class == "numero" && t.A == "71";
        var oldCount = historical.Where(t => t.File == "esito.txt").ToArray();
        var newCount = editorial.Where(t => t.File == "esito.txt").ToArray();
        if (oldCount.Length != 1 || !Count(oldCount[0]) || oldCount[0].B != "75"
            || newCount.Length != 1 || !Count(newCount[0]) || newCount[0].B != EditorialArticleCount)
            throw new InvalidDataException("Censimento editoriale F3/F5 diverso dalla revisione dichiarata (71 → 75 → " + EditorialArticleCount + ").");
        foreach (var t in editorial.Where(t => t.File != "esito.txt"))
            if (!t.File.StartsWith("wiki/", StringComparison.Ordinal) || t.Class is not ("testo" or "elemento-aggiunto" or "file-aggiunto" or "riga-aggiunta"))
                throw new InvalidDataException("Transizione non editoriale nel registro Wiki F5: " + t.File + " " + t.Path);
        var result = historical.Where(t => t.File != "esito.txt").Concat(editorial).ToArray();
        if (result.Select(t => t.Key).Distinct().Count() != result.Length)
            throw new InvalidDataException("Transizione editoriale duplicata.");
        return result;
    }

    /// <summary>Entries of a transitions file: {"voci":[{"file","percorso","classe","a","b","voce"}]}. Every entry needs a register entry.</summary>
    public static IReadOnlyList<Transition> Load(string path)
    {
        var root = JsonNode.Parse(System.IO.File.ReadAllText(path))!.AsObject();
        var list = new List<Transition>();
        foreach (var item in root["voci"]!.AsArray().OfType<JsonObject>())
        {
            string entry = item["voce"]?.GetValue<string>() ?? "";
            if (entry.Length == 0) throw new InvalidDataException("Transizione senza voce del registro: " + item.ToJsonString());
            list.Add(new(item["file"]!.GetValue<string>(), item["percorso"]!.GetValue<string>(), item["classe"]!.GetValue<string>(),
                item["a"]?.GetValue<string>(), item["b"]?.GetValue<string>(), entry));
        }
        return list;
    }
}
