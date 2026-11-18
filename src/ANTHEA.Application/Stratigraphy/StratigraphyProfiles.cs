using System.Text.Json.Nodes;

namespace Anthea.Application.Stratigraphy;

/// <summary>The original profile stays at the document root; additional independent profiles are an optional extension.</summary>
public static class StratigraphyProfiles
{
    public const string Key = "additional_profiles";
    public static IReadOnlyList<JsonObject> All(JsonObject document)
    {
        if (document[Key] is not null and not JsonArray) throw new ArgumentException("Elenco delle stratigrafie non valido.");
        return [document, .. document[Key] is JsonArray extra ? extra.Select(p => p?.AsObject() ?? throw new ArgumentException("Stratigrafia non valida.")) : []];
    }

    public static JsonObject CopyProfile(JsonObject profile)
    {
        var copy = profile.DeepClone().AsObject();
        copy.Remove(Key);
        return copy;
    }

    public static int Add(JsonObject document, int? copyIndex = null)
    {
        var profiles = All(document);
        if (profiles.Count >= 100) throw new ArgumentException("Massimo 100 stratigrafie.");
        var copy = copyIndex is int index ? CopyProfile(profiles[index]) : StratigraphyDocument.Create();
        copy["id"] = Guid.NewGuid().ToString("N");
        foreach (var layer in StratigraphyDocument.Layers(copy).OfType<JsonObject>()) layer["id"] = Guid.NewGuid().ToString("N");
        string stem = copyIndex is null ? "Stratigrafia" : copy["name"] + " · copia";
        int suffix = profiles.Count + 1;
        string name = copyIndex is null ? stem + " " + suffix : stem;
        while (profiles.Any(p => p["name"]?.ToString() == name)) name = stem + " " + ++suffix;
        copy["name"] = name;
        if (document[Key] is not JsonArray) document[Key] = new JsonArray();
        document[Key]!.AsArray().Add(copy);
        return profiles.Count;
    }

    public static void Replace(JsonObject document, int index, JsonObject replacement)
    {
        var profiles = All(document);
        if (index < 0 || index >= profiles.Count) throw new ArgumentException("Stratigrafia non presente.");
        var copy = CopyProfile(replacement);
        if (index > 0) { document[Key]!.AsArray()[index - 1] = copy; return; }
        var extra = document[Key]?.DeepClone();
        document.Clear();
        foreach (var item in copy) document[item.Key] = item.Value?.DeepClone();
        if (extra is not null) document[Key] = extra;
    }

    /// <summary>
    /// «Copia in…» / «Copia da…»: the profile <paramref name="destination"/> becomes an independent copy of <paramref name="source"/>
    /// (general data and layers); it keeps its identifier and its name, the copied layers get new identifiers.
    /// </summary>
    public static void CopyInto(JsonObject document, int source, int destination)
    {
        var profiles = All(document);
        if (source < 0 || source >= profiles.Count || destination < 0 || destination >= profiles.Count) throw new ArgumentException("Stratigrafia non presente.");
        if (source == destination) return;
        var copy = CopyProfile(profiles[source]);
        copy["id"] = profiles[destination]["id"]?.DeepClone();
        copy["name"] = profiles[destination]["name"]?.DeepClone();
        foreach (var layer in StratigraphyDocument.Layers(copy).OfType<JsonObject>()) layer["id"] = Guid.NewGuid().ToString("N");
        Replace(document, destination, copy);
    }

    public static void Remove(JsonObject document, int index)
    {
        var profiles = All(document);
        if (profiles.Count == 1) throw new ArgumentException("Conservare almeno una stratigrafia.");
        if (index < 0 || index >= profiles.Count) throw new ArgumentException("Stratigrafia non presente.");
        if (index == 0)
        {
            var next = CopyProfile(profiles[1]);
            document[Key]!.AsArray().RemoveAt(0);
            Replace(document, 0, next);
        }
        else document[Key]!.AsArray().RemoveAt(index - 1);
        if (document[Key] is JsonArray { Count: 0 }) document.Remove(Key);
    }

    public static void ValidateAdditional(JsonObject document)
    {
        if (document[Key] is null) return;
        if (document[Key] is not JsonArray extra || extra.Count > 99) throw new ArgumentException("Elenco delle stratigrafie non valido.");
        var ids = new HashSet<string?> { document["id"]?.ToString() };
        foreach (var item in extra)
        {
            if (item is not JsonObject profile || profile[Key] is not null || !ids.Add(profile["id"]?.ToString()))
                throw new ArgumentException("Stratigrafia aggiuntiva non valida o identificativo duplicato.");
            StratigraphyDocument.Validate(profile);
        }
    }
}
