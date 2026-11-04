using System.Text.Json;
using System.Text.Json.Nodes;

namespace Anthea.Projects;

/// <summary>
/// A document frozen on the thread of the interface, to be written on another (docs/sviluppo/progetto-sistema.md, steps 2c and 2e): the
/// document as UTF-8 JSON with a placeholder for each copy of a model, and those copies as UTF-8 JSON, shared with the history of the undo
/// (the copies are never changed in place, so the same bytes stay valid). Nothing in it refers to the live document: the interface goes on
/// changing it while the copy is written.
/// </summary>
public sealed class FrozenDocument
{
    internal const string Placeholder = "#copia-del-modello";
    private readonly byte[] skeleton;
    private readonly IReadOnlyDictionary<string, byte[]> models;

    internal FrozenDocument(byte[] skeleton, IReadOnlyDictionary<string, byte[]> models)
    {
        this.skeleton = skeleton;
        this.models = models;
    }

    /// <summary>A document without copies of models kept apart (a single calculation file): its JSON, now.</summary>
    public static FrozenDocument Of(JsonObject document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new(JsonSerializer.SerializeToUtf8Bytes(document), new Dictionary<string, byte[]>(StringComparer.Ordinal));
    }

    /// <summary>Bytes held: the document and the copies of its models.</summary>
    public long Bytes => skeleton.LongLength + models.Values.Sum(m => m.LongLength);

    /// <summary>A new live document equal to the frozen one; any thread.</summary>
    public JsonObject Thaw() => (JsonObject)Thaw(JsonNode.Parse(skeleton))!;

    private JsonNode? Thaw(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject { Count: 1 } value when value[Placeholder] is JsonValue key && key.TryGetValue(out string? text) && models.TryGetValue(text, out var json):
                return JsonNode.Parse(json);
            case JsonObject value:
                foreach (string name in value.Select(p => p.Key).ToArray())
                {
                    var child = value[name];
                    var thawed = Thaw(child);
                    if (!ReferenceEquals(thawed, child)) value[name] = thawed;
                }
                return value;
            case JsonArray items:
                for (int i = 0; i < items.Count; i++)
                {
                    var child = items[i];
                    var thawed = Thaw(child);
                    if (!ReferenceEquals(thawed, child)) items[i] = thawed;
                }
                return items;
            default:
                return node;
        }
    }
}
