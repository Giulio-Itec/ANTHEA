using System.Text.Json.Nodes;
using Anthea.Calculations;

namespace Anthea.Application.Concrete;

/// <summary>Actions of the complete RC sheet, already expressed in its displayed axes and units.</summary>
public static class ConcreteActions3D
{
    public static IReadOnlyList<ConcreteActionChoice> Choices(IEnumerable<JsonObject> rows) => rows
        .Select(r => new ConcreteActionChoice(r.S("id"), r.S("nome"))).ToArray();

    public static ConcreteActionScene Read(IEnumerable<JsonObject> rows, string? id, bool shear)
    {
        var row = rows.FirstOrDefault(r => r.S("id") == id);
        if (row is null) return new("", [], "Nessuna combinazione selezionata");
        var values = new List<ConcreteActionVector>();
        foreach (string key in shear ? new[] { "N", "Vx", "Vy" } : new[] { "N", "Mx", "My" })
        {
            double? value = J.Number(row[key]);
            if (value is null || !double.IsFinite(value.Value)) return new(row.S("nome"), [], "Valore non valido: " + key);
            values.Add(new(key, key == "N" ? 'z' : key[^1], key.StartsWith('M'), value.Value));
        }
        return new(row.S("nome"), values, "");
    }
}
