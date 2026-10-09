using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace ANTHEA.ModelWorkspace;

public sealed record ModelNode(int Id, double X, double Y, double Z);
public sealed record ModelElement(int Id, string Type, int Property, int[] Nodes, double Angle = 0);
public sealed record PlateProperty(int Id, string Name, double Thickness, double Offset);
public sealed record SectionProperty(int Id, string Name, string Shape, double Width, double Height, bool Centered);
public sealed record CornerResult(int Element, int Node, double[] Values);
public sealed record ModelDirection(double X, double Y, double Z);
public sealed record ModelLocalFrame(int Element, ModelDirection X, ModelDirection Y, ModelDirection Z);
public sealed record ModelBoundary(string SourceRecord, string Kind, int[] Nodes, int? Element, string Dofs);
public sealed record ModelImportMessage(string Code, string Severity, string Record, string Message);
public sealed record ModelResultSet(string Name, bool IsEnvelope, CornerResult[] Values)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResultField[]? Fields { get; init; }
}

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
    // Optional additions are omitted from old snapshots, preserving their revision fingerprints.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, JsonElement>? SourceTables { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ModelLocalFrame[]? LocalFrames { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ModelBoundary[]? Boundaries { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ModelImportMessage[]? ImportMessages { get; set; }
    public static readonly string[] Components = ["Mxx", "Myy", "Mxy", "Fxx", "Fyy", "Fxy", "Vxx", "Vyy"];
    public static string Unit(string component) => component.StartsWith('M') ? "kN·m/m" : "kN/m";
    public void Validate()
    {
        if (Nodes is null || Elements is null || Plates is null || Sections is null || Results is null || Nodes.Any(n => n is null) || Elements.Any(e => e is null || e.Nodes is null) || Plates.Any(p => p is null) || Sections.Any(s => s is null) || Results.Any(r => r is null || r.Values is null || r.Values.Any(v => v is null || v.Values is null))) throw new InvalidDataException("Il modello contiene dati mancanti.");
        if (Version != 1) throw new InvalidDataException("Versione del modello non supportata.");
        if (Nodes.Length == 0 || Nodes.Length > 500000 || Elements.Length > 500000) throw new InvalidDataException("Dimensione del modello non supportata dalla bozza.");
        if (Nodes.Any(n => n.Id <= 0 || !double.IsFinite(n.X) || !double.IsFinite(n.Y) || !double.IsFinite(n.Z)) || Nodes.Select(n => n.Id).Distinct().Count() != Nodes.Length) throw new InvalidDataException("Nodi non validi o duplicati.");
        var ids = Nodes.Select(n => n.Id).ToHashSet();
        if (Elements.Select(e => e.Id).Distinct().Count() != Elements.Length || Elements.Any(e => e.Id <= 0 || !double.IsFinite(e.Angle) || e.Nodes.Distinct().Count() != e.Nodes.Length || e.Nodes.Any(n => !ids.Contains(n)) || !(e.Type == "PLATE" && e.Nodes.Length is 3 or 4 || ElementFamilies.IsLine(e.Type) && e.Nodes.Length == 2))) throw new InvalidDataException("Connettività o tipo di elemento non supportato.");
        if (Plates.Select(p => p.Id).Distinct().Count() != Plates.Length || Sections.Select(p => p.Id).Distinct().Count() != Sections.Length || Plates.Any(p => p.Thickness <= 0 || !double.IsFinite(p.Thickness) || !double.IsFinite(p.Offset)) || Sections.Any(p => !double.IsFinite(p.Width) || !double.IsFinite(p.Height) || (p.Shape == "SB" ? p.Width <= 0 || p.Height <= 0 : p.Width < 0 || p.Height < 0))) throw new InvalidDataException("Proprietà geometriche non valide.");
        if (Elements.Any(e => e.Type == "PLATE" ? !Plates.Any(p => p.Id == e.Property) : !Sections.Any(p => p.Id == e.Property))) throw new InvalidDataException("Proprietà di un elemento mancante.");
        var elements = Elements.ToDictionary(e => e.Id);
        if (Boundaries?.Any(b => b == null || b.Nodes == null || b.Nodes.Length is < 1 or > 2 || b.Nodes.Any(n => !ids.Contains(n)) ||
            b.Element.HasValue && !elements.ContainsKey(b.Element.Value) || b.Kind is not ("RESTRAIN" or "CONSTRAINT" or "RELEASE" or "LINK")) == true)
            throw new InvalidDataException("Assegnazioni di vincolo non associabili al modello.");
        if (LocalFrames != null)
        {
            if (LocalFrames.Any(f => f == null || !elements.ContainsKey(f.Element)) || LocalFrames.Select(f => f.Element).Distinct().Count() != LocalFrames.Length)
                throw new InvalidDataException("Terne locali non associabili agli elementi.");
            static double Dot(ModelDirection a, ModelDirection b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
            foreach (var frame in LocalFrames)
            {
                var directions = new[] { frame.X, frame.Y, frame.Z };
                if (directions.Any(v => v == null || !double.IsFinite(v.X) || !double.IsFinite(v.Y) || !double.IsFinite(v.Z) || Math.Abs(Dot(v, v) - 1) > 1e-6) ||
                    Math.Abs(Dot(frame.X, frame.Y)) > 1e-6 || Math.Abs(Dot(frame.X, frame.Z)) > 1e-6 || Math.Abs(Dot(frame.Y, frame.Z)) > 1e-6 ||
                    frame.X.X * (frame.Y.Y * frame.Z.Z - frame.Y.Z * frame.Z.Y) + frame.X.Y * (frame.Y.Z * frame.Z.X - frame.Y.X * frame.Z.Z) + frame.X.Z * (frame.Y.X * frame.Z.Y - frame.Y.Y * frame.Z.X) < .999999)
                    throw new InvalidDataException("Terna locale non ortonormale e destrorsa.");
            }
        }
        if (Results.Select(r => r.Name).Distinct().Count() != Results.Length) throw new InvalidDataException("Casi di risultato duplicati.");
        foreach (var result in Results)
        {
            if (string.IsNullOrWhiteSpace(result.Name) || result.Values.Select(v => (v.Element, v.Node)).Distinct().Count() != result.Values.Length || result.Values.Any(v => !elements.TryGetValue(v.Element, out var e) || e.Type != "PLATE" || !e.Nodes.Contains(v.Node) || v.Values.Length != 8 || v.Values.Any(x => !double.IsFinite(x)))) throw new InvalidDataException("Risultati non validi o non associabili agli elementi.");
            ResultFields.Validate(result, elements);
        }
        if (SourceTables?.Any(t => string.IsNullOrWhiteSpace(t.Key) || t.Value.ValueKind != JsonValueKind.Object) == true)
            throw new InvalidDataException("Tabelle degli attributi non valide.");
        if (ImportMessages?.Any(m => m == null || string.IsNullOrWhiteSpace(m.Code)) == true) throw new InvalidDataException("Rapporto di importazione non valido.");
    }
    public string Fingerprint() => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(this)));
    public static ModelSnapshot FromJson(string json)
    {
        if (json.Length > 64000000) throw new InvalidDataException("La bozza accetta modelli fino a 64 MB.");
        var value = JsonSerializer.Deserialize<ModelSnapshot>(json) ?? throw new InvalidDataException("Modello vuoto."); value.Validate(); return value;
    }
}

