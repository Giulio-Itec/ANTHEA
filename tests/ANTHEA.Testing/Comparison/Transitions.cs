using System.Text.Json.Nodes;

namespace Anthea.Testing.Comparison;

/// <summary>
/// An admitted transition of a reference comparison (decision D2 of 15/10/2026): one exact difference, identified by file, path, class,
/// value before and value after, with the register entry that explains it. It admits only that difference: no tolerance, no pattern.
/// </summary>
public sealed record Transition(string File, string Path, string Class, string? A, string? B, string Entry)
{
    public (string, string, string, string?, string?) Key => (File, Path, Class, A, B);

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
