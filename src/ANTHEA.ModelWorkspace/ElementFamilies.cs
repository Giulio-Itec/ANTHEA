namespace ANTHEA.ModelWorkspace;

/// <summary>Topology supported for presentation; no mechanical equivalence between different formulations.</summary>
public static class ElementFamilies
{
    public static bool IsLine(string type) => type is "BEAM" or "TRUSS" or "TENSTR" or "COMPTR" or "CABLE";
    public static string Label(string type) => type switch
    {
        "PLATE" => "Plate · superfici", "BEAM" => "Beam · aste", "TRUSS" => "Truss · aste assiali",
        "TENSTR" => "Tiranti · sola trazione", "COMPTR" => "Puntoni · sola compressione", "CABLE" => "Cavi · connettività", _ => type
    };
}
