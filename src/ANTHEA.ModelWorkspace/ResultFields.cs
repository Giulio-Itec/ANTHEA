namespace ANTHEA.ModelWorkspace;

/// <summary>Imported scalars only. No principal-axis transformation, averaging or envelope calculation.</summary>
public sealed record ResultValue(int Element, int Node, double Value,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] double? Station = null);
public sealed record ResultField(string Family, string Axes, string Component, ResultValue[] Values);

public static class ResultFields
{
    public static IReadOnlyList<string> Components(string family, string axes) => (family, axes) switch
    {
        ("PLATE", "LOCAL") => ModelSnapshot.Components,
        ("PLATE", "PRINCIPAL") => ["Mmax", "Mmin", "Fmax", "Fmin"],
        ("BEAM", "LOCAL") => ["Fx", "Fy", "Fz", "Mx", "My", "Mz"],
        ("TRUSS" or "TENSTR" or "COMPTR" or "CABLE", "LOCAL") => ["Fx"],
        _ => []
    };

    public static IReadOnlyList<ResultValue> Read(ModelResultSet? result, string family, string axes, string component)
    {
        if (result == null) return [];
        var field = result.Fields?.SingleOrDefault(f => f.Family == family && f.Axes == axes && f.Component == component);
        if (field != null) return field.Values;
        int column = Array.IndexOf(ModelSnapshot.Components, component);
        return family == "PLATE" && axes == "LOCAL" && column >= 0
            ? result.Values.Select(v => new ResultValue(v.Element, v.Node, v.Values[column])).ToArray() : [];
    }

    internal static void Validate(ModelResultSet result, IReadOnlyDictionary<int, ModelElement> elements)
    {
        if (result.Fields == null) return;
        if (result.Fields.Any(f => f == null || f.Values == null || !Components(f.Family, f.Axes).Contains(f.Component)) ||
            result.Fields.Select(f => (f.Family, f.Axes, f.Component)).Distinct().Count() != result.Fields.Length)
            throw new InvalidDataException("Campi di risultato non supportati o duplicati.");
        foreach (var field in result.Fields)
        {
            if (field.Family == "PLATE" && field.Axes == "LOCAL" && result.Values.Length > 0)
                throw new InvalidDataException("Due sorgenti per lo stesso campo locale delle piastre.");
            if (field.Values.Any(v => v == null) || field.Values.Select(v => (v.Element, v.Node, v.Station)).Distinct().Count() != field.Values.Length ||
                field.Values.Any(v => v == null || !double.IsFinite(v.Value) || !elements.TryGetValue(v.Element, out var e) || e.Type != field.Family ||
                    (ElementFamilies.IsLine(field.Family) && v.Station.HasValue ? !double.IsFinite(v.Station.Value) || v.Station is < 0 or > 1 ||
                        (v.Station == 0 ? v.Node != e.Nodes[0] : v.Station == 1 ? v.Node != e.Nodes[1] : v.Node != 0) : !e.Nodes.Contains(v.Node))))
                throw new InvalidDataException("Valori del campo non associabili agli elementi.");
        }
    }
}
