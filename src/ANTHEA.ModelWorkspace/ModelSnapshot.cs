using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ANTHEA.ModelWorkspace;

public sealed record ModelNode(int Id, double X, double Y, double Z);
public sealed record ModelElement(int Id, string Type, int Property, int[] Nodes, double Angle = 0);
public sealed record PlateProperty(int Id, string Name, double Thickness, double Offset);
public sealed record SectionProperty(int Id, string Name, string Shape, double Width, double Height, bool Centered);
public sealed record CornerResult(int Element, int Node, double[] Values);
public sealed record ModelResultSet(string Name, bool IsEnvelope, CornerResult[] Values);

/// <summary>Offline presentation snapshot. Units: m, kN/m and kN m/m. It is not a calculation model or a solver.</summary>
public sealed class ModelSnapshot
{
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "Modello di calcolo";
    public string Source { get; set; } = "";
    public ModelNode[] Nodes { get; set; } = [];
    public ModelElement[] Elements { get; set; } = [];
    public PlateProperty[] Plates { get; set; } = [];
    public SectionProperty[] Sections { get; set; } = [];
    public ModelResultSet[] Results { get; set; } = [];
    public static readonly string[] Components = ["Mxx", "Myy", "Mxy", "Fxx", "Fyy", "Fxy", "Vxx", "Vyy"];
    public static string Unit(string component) => component.StartsWith('M') ? "kN·m/m" : "kN/m";
    public void Validate()
    {
        if (Nodes is null || Elements is null || Plates is null || Sections is null || Results is null || Nodes.Any(n => n is null) || Elements.Any(e => e is null || e.Nodes is null) || Plates.Any(p => p is null) || Sections.Any(s => s is null) || Results.Any(r => r is null || r.Values is null || r.Values.Any(v => v is null || v.Values is null))) throw new InvalidDataException("Il modello contiene dati mancanti.");
        if (Version != 1) throw new InvalidDataException("Versione del modello non supportata.");
        if (Nodes.Length == 0 || Nodes.Length > 500000 || Elements.Length > 500000) throw new InvalidDataException("Dimensione del modello non supportata dalla bozza.");
        if (Nodes.Any(n => n.Id <= 0 || !double.IsFinite(n.X) || !double.IsFinite(n.Y) || !double.IsFinite(n.Z)) || Nodes.Select(n => n.Id).Distinct().Count() != Nodes.Length) throw new InvalidDataException("Nodi non validi o duplicati.");
        var ids = Nodes.Select(n => n.Id).ToHashSet();
        if (Elements.Select(e => e.Id).Distinct().Count() != Elements.Length || Elements.Any(e => e.Id <= 0 || !double.IsFinite(e.Angle) || e.Nodes.Distinct().Count() != e.Nodes.Length || e.Nodes.Any(n => !ids.Contains(n)) || !(e.Type == "PLATE" && e.Nodes.Length is 3 or 4 || e.Type == "BEAM" && e.Nodes.Length == 2))) throw new InvalidDataException("Connettività o tipo di elemento non supportato.");
        if (Plates.Select(p => p.Id).Distinct().Count() != Plates.Length || Sections.Select(p => p.Id).Distinct().Count() != Sections.Length || Plates.Any(p => p.Thickness <= 0 || !double.IsFinite(p.Thickness) || !double.IsFinite(p.Offset)) || Sections.Any(p => !double.IsFinite(p.Width) || !double.IsFinite(p.Height) || p.Width <= 0 || p.Height <= 0)) throw new InvalidDataException("Proprietà geometriche non valide.");
        if (Elements.Any(e => e.Type == "PLATE" ? !Plates.Any(p => p.Id == e.Property) : !Sections.Any(p => p.Id == e.Property))) throw new InvalidDataException("Proprietà di un elemento mancante.");
        var elements = Elements.ToDictionary(e => e.Id);
        if (Results.Select(r => r.Name).Distinct().Count() != Results.Length) throw new InvalidDataException("Casi di risultato duplicati.");
        foreach (var result in Results)
            if (string.IsNullOrWhiteSpace(result.Name) || result.Values.Select(v => (v.Element, v.Node)).Distinct().Count() != result.Values.Length || result.Values.Any(v => !elements.TryGetValue(v.Element, out var e) || e.Type != "PLATE" || !e.Nodes.Contains(v.Node) || v.Values.Length != 8 || v.Values.Any(x => !double.IsFinite(x)))) throw new InvalidDataException("Risultati non validi o non associabili agli elementi.");
    }
    public string Fingerprint() => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(this)));
    public static ModelSnapshot FromJson(string json)
    {
        if (json.Length > 64000000) throw new InvalidDataException("La bozza accetta modelli fino a 64 MB.");
        var value = JsonSerializer.Deserialize<ModelSnapshot>(json) ?? throw new InvalidDataException("Modello vuoto."); value.Validate(); return value;
    }
}

