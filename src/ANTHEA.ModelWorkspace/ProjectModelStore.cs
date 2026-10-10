using System.Text.Json;
using System.Text.Json.Nodes;

namespace ANTHEA.ModelWorkspace;

public static class ProjectModelStore
{
    public const string Property = "modelli_calcolo";
    public const string Binding = "riferimento_modello";
    public static IEnumerable<JsonObject> Models(JsonObject container)
    {
        if (container[Property] is null) return [];
        if (container[Property] is not JsonArray items || items.Any(i => i is not JsonObject)) throw new InvalidDataException("Elenco dei modelli non valido.");
        return items.Cast<JsonObject>();
    }
    public static ModelSnapshot? Read(JsonObject project, string id)
    {
        var model = Models(project).SingleOrDefault(m => m["id"]?.GetValue<string>() == id); if (model == null) return null;
        if (model["schema"]?.GetValue<int>() != 1) throw new InvalidDataException("Versione del collegamento al modello non supportata.");
        var snapshot = ModelSnapshot.FromJson(model["snapshot"]?.ToJsonString() ?? throw new InvalidDataException("Modello mancante."));
        if (snapshot.Fingerprint() != model["revisione"]?.GetValue<string>()) throw new InvalidDataException("Impronta del modello non corrispondente ai dati.");
        return snapshot;
    }
    public static string Set(JsonObject project, ModelSnapshot snapshot, string? id = null)
    {
        snapshot.Validate();
        _ = Models(project); // Reject malformed metadata instead of losing an import silently.
        var models = project[Property] as JsonArray ?? new JsonArray();
        var old = id == null ? null : (Models(project).SingleOrDefault(m => m["id"]?.GetValue<string>() == id) ?? throw new ArgumentException("Modello non trovato nel contenitore."));
        id ??= Guid.NewGuid().ToString("N");
        var entry = new JsonObject { ["schema"] = 1, ["id"] = id, ["revisione"] = snapshot.Fingerprint(), ["nome"] = old?["nome"]?.GetValue<string>() ?? snapshot.Name, ["snapshot"] = JsonSerializer.SerializeToNode(snapshot) };
        if (old != null) models[models.IndexOf(old)] = entry; else models.Add(entry);
        if (project[Property] == null) project[Property] = models;
        return id;
    }
    public static void Rename(JsonObject container, string id, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Inserire un nome per il modello.");
        var model = Models(container).Single(m => m["id"]!.GetValue<string>() == id); model["nome"] = name.Trim();
    }
    public static void Link(JsonObject project, JsonObject sheet, string modelId, int element, string result, string component)
    {
        var snapshot = Read(project, modelId) ?? throw new InvalidOperationException("Importare prima il modello.");
        if (!DescendantSheets(project).Any(s => ReferenceEquals(s, sheet))) throw new InvalidOperationException("Il foglio appartiene a un altro progetto.");
        if (!snapshot.Elements.Any(e => e.Id == element) || !snapshot.Results.Any(r => r.Name == result && r.Values.Any(v => v.Element == element)) || !ModelSnapshot.Components.Contains(component)) throw new ArgumentException("Riferimento al risultato non valido o elemento senza risultati.");
        var model = Models(project).Single(m => m["id"]!.GetValue<string>() == modelId);
        sheet[Binding] = new JsonObject { ["modello_id"] = modelId, ["revisione"] = model["revisione"]!.DeepClone(), ["elemento"] = element, ["caso"] = result, ["componente"] = component };
    }
    public static IEnumerable<JsonObject> DescendantSheets(JsonObject project)
    {
        foreach (var s in (project["fogli"] as JsonArray ?? []).OfType<JsonObject>()) yield return s;
        foreach (var child in (project["strutture"] as JsonArray ?? []).OfType<JsonObject>()) foreach (var s in DescendantSheets(child)) yield return s;
    }
    public static string LinkStatus(JsonObject project, JsonObject sheet)
    {
        if (sheet[Binding] is not JsonObject link) return "Non collegato";
        var model = Models(project).SingleOrDefault(m => JsonNode.DeepEquals(link["modello_id"], m["id"]));
        return model != null && JsonNode.DeepEquals(link["revisione"], model["revisione"]) ? "Riferimento aggiornato" : "Riferimento da aggiornare";
    }
    public static IEnumerable<JsonObject> Containers(JsonObject container)
    {
        yield return container;
        foreach (var child in (container["strutture"] as JsonArray ?? []).OfType<JsonObject>()) foreach (var node in Containers(child)) yield return node;
    }
    public static JsonObject? FindOwner(JsonObject sheet)
    {
        if (sheet[Binding] is not JsonObject link) return null;
        for (var owner = sheet.Parent?.Parent as JsonObject; owner != null; owner = owner.Parent?.Parent as JsonObject)
            if (Models(owner).Any(m => JsonNode.DeepEquals(m["id"], link["modello_id"]))) return owner;
        return null;
    }
    /// <summary>Give copied models new identities; keep internal links and leave external references explicit.</summary>
    public static void ReidentifyCopy(JsonObject copy)
    {
        var map = new Dictionary<string, string>();
        foreach (var owner in Containers(copy)) foreach (var model in Models(owner))
            {
                string before = model["id"]!.GetValue<string>(), after = Guid.NewGuid().ToString("N"); map.Add(before, after); model["id"] = after;
            }
        foreach (var sheet in DescendantSheets(copy)) if (sheet[Binding] is JsonObject link && map.TryGetValue(link["modello_id"]!.GetValue<string>(), out var id)) link["modello_id"] = id;
    }
}
