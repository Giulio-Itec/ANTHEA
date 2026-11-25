using System.Text.Json.Nodes;
using Anthea.Calculations;

namespace Anthea.Application.Concrete;

/// <summary>Document actions for the drawing only; no resistance calculation or change of the document.</summary>
public static class ConcreteQuickActions
{
    public static IReadOnlyList<ConcreteActionChoice> Choices(JsonObject data, bool shear) => Rows(data, shear)
        .Select(r => new ConcreteActionChoice(r.S("id"), r.S("nome"))).ToArray();

    private static IReadOnlyList<JsonObject> Rows(JsonObject data, bool shear) => shear
        ? ConcreteQuickShear.Combinations(data) : ConcreteQuickSection.Combinations(data);

    public static ConcreteActionScene Read(JsonObject data, bool shear, string? id)
    {
        var row = Rows(data, shear).FirstOrDefault(r => r.S("id") == id);
        if (row is null) return new("", [], shear ? "Nessuna combinazione a taglio" : "Nessuna combinazione selezionata");
        var values = new List<ConcreteActionVector>();
        foreach (string key in shear ? new[] { "N", "Vx", "Vy" } : new[] { "N", "Mx", "My" })
        {
            // My is not used by the straight-bending calculation, even if an older value remains in the document.
            double? value = !shear && key == "My" && data[ConcreteQuickSection.ViewKey].S("flessione", "Retta") == "Retta"
                ? 0 : J.Number(JsonValue.Create(row.S(key, "0")));
            if (value is null) return new(row.S("nome"), [], "Valore non valido: " + key);
            values.Add(new(key, key == "N" ? 'z' : key[^1], key.StartsWith('M'), value.Value));
        }
        return new(row.S("nome"), values, "");
    }
}

public sealed record ConcreteActionChoice(string Id, string Name);
public sealed record ConcreteActionScene(string Combination, IReadOnlyList<ConcreteActionVector> Actions, string Message);
public sealed record ConcreteActionVector(string Name, char Axis, bool IsMoment, double Value)
{
    public string Label => Name + " " + EngineeringFormat.Number(Math.Abs(Value)) + (IsMoment ? " kNm" : " kN");
}
