using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Anthea.Testing.Normalization;

/// <summary>
/// Canonical form of the captured JSON: property names sorted (ordinal), numbers written as double with the "R" format and the
/// invariant culture, strings and names passed through <see cref="TextNormalizer"/>. Two captures of the same behaviour give the
/// same bytes; the comparison never depends on the order in which the program filled its objects.
/// </summary>
public static class CanonicalJson
{
    static readonly JsonWriterOptions WriterOptions = new() { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Options for the typed results of the engines (records of X.Calculations and of the GPC libraries).</summary>
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        MaxDepth = 256
    };

    public static JsonNode? FromObject(object? value) => value switch
    {
        null => null,
        JsonNode node => node.DeepClone(),
        _ => JsonSerializer.SerializeToNode(value, value.GetType(), SerializerOptions)
    };

    /// <summary>Registers the GUIDs of a tree in document order: property names and string values, depth first.</summary>
    public static void Register(JsonNode? node, GuidMap guids)
    {
        switch (node)
        {
            case JsonObject o:
                foreach (var (key, value) in o) { TextNormalizer.Register(key, guids); Register(value, guids); }
                break;
            case JsonArray a:
                foreach (var item in a) Register(item, guids);
                break;
            case JsonValue v when v.GetValueKind() == JsonValueKind.String:
                TextNormalizer.Register(v.GetValue<string>(), guids);
                break;
        }
    }

    public static string Serialize(JsonNode? node, GuidMap guids, TextNormalizer text)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions)) Write(writer, node, guids, text);
        return Encoding.UTF8.GetString(buffer.ToArray()) + Environment.NewLine;
    }

    public static void WriteFile(string path, JsonNode? node, GuidMap guids, TextNormalizer text)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllText(path, Serialize(node, guids, text), new UTF8Encoding(false));
    }

    static void Write(Utf8JsonWriter writer, JsonNode? node, GuidMap guids, TextNormalizer text)
    {
        switch (node)
        {
            case null:
                writer.WriteNullValue();
                break;
            case JsonObject o:
                writer.WriteStartObject();
                // Names are normalised before sorting, so the order does not depend on the GUID values.
                foreach (var (key, value) in o.Select(p => (Key: TextNormalizer.NormalizeKey(p.Key, guids), p.Value)).OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(key);
                    Write(writer, value, guids, text);
                }
                writer.WriteEndObject();
                break;
            case JsonArray a:
                writer.WriteStartArray();
                foreach (var item in a) Write(writer, item, guids, text);
                writer.WriteEndArray();
                break;
            case JsonValue v:
                switch (v.GetValueKind())
                {
                    case JsonValueKind.Number:
                        // A CLR double that is not finite cannot be written as a JSON number: keep it as text.
                        if (v.TryGetValue<double>(out var d) && !double.IsFinite(d)) writer.WriteStringValue(d.ToString(CultureInfo.InvariantCulture));
                        else writer.WriteRawValue(Number(v.ToJsonString()));
                        break;
                    case JsonValueKind.String:
                        writer.WriteStringValue(text.Normalize(v.GetValue<string>(), guids));
                        break;
                    case JsonValueKind.True: writer.WriteBooleanValue(true); break;
                    case JsonValueKind.False: writer.WriteBooleanValue(false); break;
                    default: writer.WriteNullValue(); break;
                }
                break;
        }
    }

    /// <summary>A JSON number as the shortest double that round-trips ("R", invariant culture).</summary>
    public static string Number(string json)
    {
        double value = double.Parse(json, NumberStyles.Float, CultureInfo.InvariantCulture);
        return value.ToString("R", CultureInfo.InvariantCulture);
    }
}
