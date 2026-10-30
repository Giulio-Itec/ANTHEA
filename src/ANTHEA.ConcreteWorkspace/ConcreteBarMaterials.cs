using System.Text.Json.Nodes;
using Anthea.Calculations;

namespace Anthea.ConcreteWorkspace;

/// <summary>Editor data for the material of an ordinary bar. No material law or calculation is implemented here.</summary>
public static class ConcreteBarMaterials
{
    public const string Key = "acciaio";

    /// <summary>An absent override follows the sheet material; a present override is a complete material snapshot.</summary>
    public static JsonObject InputForBar(JsonObject input, int index)
    {
        if (input["barre_manuali"] is not JsonArray bars) return input;
        if (index < 0 || index >= bars.Count || bars[index] is not JsonObject row)
            throw new ArgumentException("Barra ordinaria non presente nella disposizione.");
        if (!row.ContainsKey(Key)) return input;
        return row[Key] as JsonObject ?? throw new ArgumentException("Materiale della barra non valido.");
    }

    public static bool HasOverrides(JsonObject input) => input["barre_manuali"] is JsonArray bars && bars.OfType<JsonObject>().Any(b => b.ContainsKey(Key));

    /// <summary>Adapters with one material parameter must not silently substitute the sheet value for a bar override.</summary>
    public static void RequireSheetProperties(JsonObject input, string operation, params string[] keys)
    {
        if (input["barre_manuali"] is not JsonArray bars) return;
        for (int i = 0; i < bars.Count; i++)
        {
            var material = InputForBar(input, i);
            if (keys.Any(key => material.Required(key, strict: true) != input.Required(key, strict: true)))
                throw new ArgumentException(operation + ": il modello automatico richiede " + string.Join(", ", keys.Select(Label))
                    + " uguali al materiale generale per tutte le barre. B" + (i + 1).ToString("D2")
                    + " ha proprietà diverse; questa verifica resta da completare. Le analisi N–M e tensionali usano il materiale di ogni barra.");
        }
    }

    private static string Label(string key) => key switch
    {
        "fyk_mpa" => "fyk", "steel_modulus_mpa" => "Es", "steel_fu_mpa" => "ftk", "steel_eps_u" => "εuk", _ => key
    };
}
