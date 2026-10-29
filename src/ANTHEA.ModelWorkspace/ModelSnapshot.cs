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
public sealed record ModelDisplacement(int Node,double X,double Y,double Z)
{
    // Optional global rotation vector in radians; absent in historical packages.
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public ModelDirection? Rotation {get;init;}
}
public sealed record ModelDirection(double X, double Y, double Z);
public sealed record ModelLocalFrame(int Element, ModelDirection X, ModelDirection Y, ModelDirection Z);
public sealed record ModelNodeFrame(int Node, ModelDirection X, ModelDirection Y, ModelDirection Z);
public sealed record ModelBoundary(string SourceRecord, string Kind, int[] Nodes, int? Element, string Dofs);
public sealed record ModelImportMessage(string Code, string Severity, string Record, string Message);
/// <summary>Static load cases and combination definitions of a source without MIDAS tables (Straus7 and later solvers): definitions only,
/// never results. Linear combinations and envelopes are rebuilt by GPC from the imported static cases (GpcModelCombinationService).</summary>
public sealed record ModelSourceCombinations(string[] StaticCases, ModelCombinationDefinition[] Definitions);
/// <summary>1.16.0: support reaction of a node, as reported by the solver: force and moment exerted by the support (restraint or spring to ground)
/// on the structure, global axes, kN and kN·m. The same sign for every source (MIDAS and Straus7 report the action on the structure).</summary>
public sealed record ModelReaction(int Node, double Fx, double Fy, double Fz, double Mx, double My, double Mz)
{
    [JsonIgnore] public double[] Values => [Fx, Fy, Fz, Mx, My, Mz];
}
/// <summary>1.16.0: global equilibrium of a static case checked by the source library (GPC): resultants of the applied loads and of the reactions,
/// global axes, kN and kN·m about the global origin. Balanced is null when the check was not evaluated; Note says what was or was not covered.</summary>
public sealed record ModelEquilibrium(double[] AppliedForce, double[] AppliedMoment, double[] ReactionForce, double[] ReactionMoment, bool? Balanced, string Note);
/// <summary>1.16.0: a mass of the source model, data only (no load is derived). Kind: NODE (Values = mass along global X, Y, Z, t), NODE-ROTATION
/// (rotational mass about Axes X, Y, Z, t·m²), NODE-NS (non-structural mass of a case, t), BEAM-NS (t/m at Stations, fractions of the length),
/// PLATE-NS (t/m²). Entity: document node or element. Offset: global, m.</summary>
public sealed record ModelMass(string Kind, int Entity, string? Case, double[] Values)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double[]? Stations { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ModelNodeFrame? Axes { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ModelDirection? Offset { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? DynamicFactor { get; init; }
    public string Record { get; init; } = "";
    public static readonly string[] Kinds = ["NODE", "NODE-ROTATION", "NODE-NS", "BEAM-NS", "PLATE-NS"];
}
/// <summary>1.16.0: a temperature of a load case, data only. Kind: REFERENCE (reference temperature of the case, Entity 0), NODE (°C; Type: Reference,
/// Fixed, Initial or Table), BEAM-GRADIENT (°C/m along the principal axes 1 and 2), PLATE-GRADIENT (°C/m along the local z axis).</summary>
public sealed record ModelTemperature(string Kind, int Entity, string Case, double[] Values)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Type { get; init; }
    public string Record { get; init; } = "";
    public static readonly string[] Kinds = ["REFERENCE", "NODE", "BEAM-GRADIENT", "PLATE-GRADIENT"];
}
/// <summary>1.16.0: a stage of the source model: number, name and label ("1: name") used by its stage results, type (Unmorphed, Morphed, Reset),
/// groups enabled in it and the document elements of those groups.</summary>
public sealed record ModelStageDefinition(int Number, string Name, string Label, string Type, string[] Groups, int[] Elements);
public sealed record ModelResultSet(string Name, bool IsEnvelope, CornerResult[] Values)
{
    // 1.16.0: optional and omitted when null.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ModelReaction[]? Reactions { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ModelEquilibrium? Equilibrium { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ModelStageResult? Stage { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ModelCombinationDerivation? Combination { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResultField[]? Fields { get; init; }
    // Optional imported global displacements in metres, never reconstructed from internal forces.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ModelDisplacement[]? Displacements { get; init; }
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
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ModelCombinationDefinition[]? CustomCombinations { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? ExcludedCombinationIds { get; set; }
    // Optional additions are omitted from old snapshots, preserving their revision fingerprints.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, JsonElement>? SourceTables { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ModelLocalFrame[]? LocalFrames { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ModelNodeFrame[]? NodeFrames { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ModelBoundary[]? Boundaries { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ModelImportMessage[]? ImportMessages { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ModelUpdateInfo? UpdateInfo { get; set; }
    // 1.15.0: optional and omitted when null, so the fingerprints of the existing documents do not change.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ModelSourceCombinations? SourceCombinations { get; set; }
    // 1.16.0: masses, temperatures and stages of the source model (Straus7), optional and omitted when null.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ModelMass[]? Masses { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ModelTemperature[]? Temperatures { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ModelStageDefinition[]? ConstructionStages { get; set; }
    public static readonly string[] Components = ["Mxx", "Myy", "Mxy", "Fxx", "Fyy", "Fxy", "Vxx", "Vyy"];
    public static string Unit(string component) => component.StartsWith('M') ? "kN·m/m" : "kN/m";
    public void Validate()
    {
        if(CustomCombinations!=null)
        {
            ModelCombinations.ValidateDefinitions(CustomCombinations);
            if(CustomCombinations.Any(d=>!d.Editable||!d.Id.StartsWith(d.Source+"/",StringComparison.Ordinal)))
                throw new InvalidDataException("Combinazioni personalizzate non valide.");
        }
        if(SourceCombinations is {} source)
        {
            if(source.StaticCases==null||source.Definitions==null||source.StaticCases.Length>10000||source.StaticCases.Any(string.IsNullOrWhiteSpace)||
                source.StaticCases.Distinct(StringComparer.Ordinal).Count()!=source.StaticCases.Length)
                throw new InvalidDataException("Casi di carico della sorgente non validi.");
            ModelCombinations.ValidateDefinitions(source.Definitions);
            if(source.Definitions.Any(d=>d.Editable||!d.Id.StartsWith(d.Source+"/",StringComparison.Ordinal)))
                throw new InvalidDataException("Combinazioni della sorgente non valide.");
        }
        if(ExcludedCombinationIds is {} excluded && (excluded.Length>10000||excluded.Any(string.IsNullOrWhiteSpace)||excluded.Distinct().Count()!=excluded.Length))
            throw new InvalidDataException("Elenco delle combinazioni escluse non valido.");
        if (Nodes is null || Elements is null || Plates is null || Sections is null || Results is null || Nodes.Any(n => n is null) || Elements.Any(e => e is null || e.Nodes is null) || Plates.Any(p => p is null) || Sections.Any(s => s is null) || Results.Any(r => r is null || r.Values is null || r.Values.Any(v => v is null || v.Values is null))) throw new InvalidDataException("Il modello contiene dati mancanti.");
        if (Version is not (1 or 2)) throw new InvalidDataException("Versione del modello non supportata.");
        if (Version < 2 && Results.Any(r => r.Stage != null)) throw new InvalidDataException("I risultati per fase richiedono il formato modello 2: non possono essere letti come casi statici da versioni precedenti.");
        if(UpdateInfo is {} update && (!Enum.IsDefined(update.Scope) || update.InputSource is null || update.InputSource.Length>4000 || update.DiscardedResultCases<0 || update.ResultsCompatibilityVerified!=(update.Scope==ModelUpdateScope.Results)))
            throw new InvalidDataException("Provenienza dell’aggiornamento non valida.");
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
        if (NodeFrames != null)
        {
            if (NodeFrames.Any(f => f == null || !ids.Contains(f.Node)) || NodeFrames.Select(f => f.Node).Distinct().Count() != NodeFrames.Length)
                throw new InvalidDataException("Terne locali non associabili ai nodi.");
            static double Dot(ModelDirection a, ModelDirection b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
            foreach (var frame in NodeFrames)
            {
                var directions = new[] { frame.X, frame.Y, frame.Z };
                if (directions.Any(v => v == null || !double.IsFinite(v.X) || !double.IsFinite(v.Y) || !double.IsFinite(v.Z) || Math.Abs(Dot(v, v) - 1) > 1e-6) ||
                    Math.Abs(Dot(frame.X, frame.Y)) > 1e-6 || Math.Abs(Dot(frame.X, frame.Z)) > 1e-6 || Math.Abs(Dot(frame.Y, frame.Z)) > 1e-6 ||
                    frame.X.X * (frame.Y.Y * frame.Z.Z - frame.Y.Z * frame.Z.Y) + frame.X.Y * (frame.Y.Z * frame.Z.X - frame.Y.X * frame.Z.Z) + frame.X.Z * (frame.Y.X * frame.Z.Y - frame.Y.Y * frame.Z.X) < .999999)
                    throw new InvalidDataException("Terna nodale non ortonormale e destrorsa.");
            }
        }
        static bool Finite(double[]? values, int? length = null) => values != null && (length == null || values.Length == length) && values.All(double.IsFinite);
        if (Masses?.Any(m => m == null || !ModelMass.Kinds.Contains(m.Kind) || !Finite(m.Values) || m.Values.Length == 0 ||
            (m.Kind.StartsWith("NODE", StringComparison.Ordinal) ? !ids.Contains(m.Entity) : !elements.TryGetValue(m.Entity, out var owner) || (owner.Type == "PLATE") != (m.Kind == "PLATE-NS")) ||
            (m.Kind is "NODE" or "NODE-ROTATION") != (m.Case == null) || m.Kind == "BEAM-NS" && (!Finite(m.Stations, m.Values.Length) || m.Stations!.Any(s => s is < 0 or > 1)) ||
            m.Offset is { } o && !(double.IsFinite(o.X) && double.IsFinite(o.Y) && double.IsFinite(o.Z))) == true)
            throw new InvalidDataException("Masse del modello non associabili o non valide.");
        if (Temperatures?.Any(t => t == null || !ModelTemperature.Kinds.Contains(t.Kind) || string.IsNullOrWhiteSpace(t.Case) || !Finite(t.Values) || t.Values.Length == 0 ||
            t.Kind switch { "REFERENCE" => t.Entity != 0, "NODE" => !ids.Contains(t.Entity), _ => !elements.TryGetValue(t.Entity, out var owner) || (owner.Type == "PLATE") != (t.Kind == "PLATE-GRADIENT") }) == true)
            throw new InvalidDataException("Temperature del modello non associabili o non valide.");
        if (ConstructionStages is { } stages && (stages.Any(s => s == null || s.Number < 1 || string.IsNullOrWhiteSpace(s.Label) || s.Name == null || s.Type == null || s.Groups == null ||
            s.Elements == null || s.Elements.Any(e => !elements.ContainsKey(e))) || stages.Select(s => s.Number).Distinct().Count() != stages.Length ||
            stages.Select(s => s.Label).Distinct(StringComparer.Ordinal).Count() != stages.Length))
            throw new InvalidDataException("Fasi del modello non valide o non associabili agli elementi.");
        if (Results.Select(r => r.Name).Distinct().Count() != Results.Length) throw new InvalidDataException("Casi di risultato duplicati.");
        ModelConstructionStages.ValidateResults(Results);
        foreach (var result in Results)
        {
            if(result.Combination is {} derivation && (string.IsNullOrWhiteSpace(derivation.DefinitionId)||derivation.InputRevision?.Length!=64||string.IsNullOrWhiteSpace(derivation.Engine)||
                derivation.RequiredCases==null||derivation.RequiredCases.Any(string.IsNullOrWhiteSpace)||derivation.Governing==null||derivation.Governing.Any(g=>g==null||
                string.IsNullOrWhiteSpace(g.Alternative)||g.ConcomitantValues==null||g.ConcomitantValues.Any(v=>!double.IsFinite(v))||
                !(g.Family=="PLATE"&&g.ConcomitantValues.Length==8&&elements.TryGetValue(g.Element,out var plate)&&plate.Type=="PLATE"&&plate.Nodes.Contains(g.Node)&&Components.Contains(g.Component)||
                  g.Family=="BEAM"&&g.ConcomitantValues.Length==6&&elements.TryGetValue(g.Element,out var beam)&&beam.Type=="BEAM"&&g.Station is >=0 and <=1&&ResultFields.Components("BEAM","LOCAL").Contains(g.Component)||
                  // 1.16.0: trusses and cables of the sources without MIDAS tables, axial force only.
                  g.Family!="BEAM"&&ElementFamilies.IsLine(g.Family)&&g.ConcomitantValues.Length==6&&elements.TryGetValue(g.Element,out var axial)&&axial.Type==g.Family&&g.Station is >=0 and <=1&&ResultFields.Components(g.Family,"LOCAL").Contains(g.Component)||
                  g.Family=="REACTION"&&g.ConcomitantValues.Length==6&&ids.Contains(g.Node)&&ModelReactions.Components.Contains(g.Component)||
                  g.Family=="NODE"&&g.ConcomitantValues.Length==6&&ids.Contains(g.Node)&&new[]{"ux","uy","uz"}.Contains(g.Component)))))
                throw new InvalidDataException("Provenienza o governanti della combinazione non validi.");
            if (string.IsNullOrWhiteSpace(result.Name) || result.Values.Select(v => (v.Element, v.Node)).Distinct().Count() != result.Values.Length || result.Values.Any(v => !elements.TryGetValue(v.Element, out var e) || e.Type != "PLATE" || !e.Nodes.Contains(v.Node) || v.Values.Length != 8 || v.Values.Any(x => !double.IsFinite(x)))) throw new InvalidDataException("Risultati non validi o non associabili agli elementi.");
            ResultFields.Validate(result, elements);
            if (result.Reactions is { } reactions && (reactions.Any(r => r == null || !ids.Contains(r.Node) || !r.Values.All(double.IsFinite)) || reactions.Select(r => r.Node).Distinct().Count() != reactions.Length))
                throw new InvalidDataException("Reazioni vincolari non valide o non associabili ai nodi.");
            if (result.Equilibrium is { } balance && (!Finite(balance.AppliedForce, 3) || !Finite(balance.AppliedMoment, 3) || !Finite(balance.ReactionForce, 3) || !Finite(balance.ReactionMoment, 3) || balance.Note == null))
                throw new InvalidDataException("Controllo di equilibrio non valido.");
            if(result.Displacements is {} displacements && (displacements.Any(d=>d==null||!ids.Contains(d.Node)||!double.IsFinite(d.X)||!double.IsFinite(d.Y)||!double.IsFinite(d.Z)||d.Rotation is {} rotation&&(!double.IsFinite(rotation.X)||!double.IsFinite(rotation.Y)||!double.IsFinite(rotation.Z)))||displacements.Select(d=>d.Node).Distinct().Count()!=displacements.Length))
                throw new InvalidDataException("Spostamenti importati non validi o non associabili ai nodi.");
        }
        if (SourceTables?.Any(t => string.IsNullOrWhiteSpace(t.Key) || t.Value.ValueKind != JsonValueKind.Object) == true)
            throw new InvalidDataException("Tabelle degli attributi non valide.");
        // Validate metadata consumed by the viewer before an import can replace the project document.
        try { _ = ModelCombinations.Read(this); _ = ModelCombinations.StaticCaseNames(this); }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        { throw new InvalidDataException("Tabelle dei casi di carico o delle combinazioni non valide. La copia precedente è stata conservata.", ex); }
        if (ImportMessages?.Any(m => m == null || string.IsNullOrWhiteSpace(m.Code)) == true) throw new InvalidDataException("Rapporto di importazione non valido.");
    }
    public string Fingerprint()
    {
        // Same canonical UTF-8 JSON as previous revisions, without allocating another complete copy.
        using var hash = SHA256.Create();
        using var stream = new CryptoStream(Stream.Null, hash, CryptoStreamMode.Write);
        JsonSerializer.Serialize(stream, this);
        stream.FlushFinalBlock();
        return Convert.ToHexString(hash.Hash!);
    }
    public static ModelSnapshot FromJson(string json)
    {
        if (json.Length > ModelSnapshotFiles.MaximumPackageBytes || System.Text.Encoding.UTF8.GetByteCount(json) > ModelSnapshotFiles.MaximumPackageBytes)
            throw new InvalidDataException(ModelSnapshotFiles.PackageSizeMessage);
        var value = JsonSerializer.Deserialize<ModelSnapshot>(json) ?? throw new InvalidDataException("Modello vuoto."); value.Validate(); return value;
    }
}

