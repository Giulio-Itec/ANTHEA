using System.Text.Json.Nodes;
using Anthea.Application.Materials;
using Anthea.ConcreteWorkspace;

namespace Anthea.Application.Concrete;

/// <summary>Choices and detached editor snapshots for ordinary reinforcement; material validation stays in the existing GPC adapter.</summary>
public static class ConcreteBarMaterialPresentation
{
    public static string DefaultLabel(JsonObject input) => "Del foglio · " + input.S("materiale_acciaio_nome", input.S("classe_acciaio", "Acciaio"));

    public static string Label(JsonObject input, JsonObject row) => row[ConcreteBarMaterials.Key] is JsonObject material
        ? material.S("materiale_acciaio_nome", material.S("classe_acciaio")) : DefaultLabel(input);

    public static JsonObject[] Catalog(JsonObject settings) => MaterialInputCatalog.Steel(false, settings.S("normativa"))
        .Concat(settings.Array("materiali_custom").OfType<JsonObject>().Where(m => m.S("tipo") == "Acciaio"))
        .Select(m => (JsonObject)m.DeepClone()).ToArray();

    public static void Select(JsonObject row, string selection, JsonObject input, JsonObject settings)
    {
        if (selection == DefaultLabel(input)) { row.Remove(ConcreteBarMaterials.Key); return; }
        var material = Catalog(settings).FirstOrDefault(m => m.S("nome") == selection);
        if (material is null)
        {
            if (row[ConcreteBarMaterials.Key] is JsonObject saved && Label(input, row) == selection) return;
            throw new ArgumentException("Scegliere un materiale acciaio del catalogo o salvato nel foglio.");
        }
        var snapshot = new JsonObject();
        foreach (string key in ConcreteInputPresentation.SavedSteelKeys) snapshot[key] = material[key]?.DeepClone();
        snapshot["classe_acciaio"] = material.S("classe_acciaio", "Personalizzato");
        snapshot["materiale_acciaio_nome"] = material.S("nome");
        _ = ConcreteMaterials.Rebar(snapshot);
        row[ConcreteBarMaterials.Key] = snapshot;
    }

    /// <summary>The visible label is not persisted: default rows retain their historical wire representation.</summary>
    public static JsonObject StoredRow(JsonObject row)
    {
        var stored = (JsonObject)row.DeepClone();
        stored.Remove("materiale");
        return stored;
    }
}
