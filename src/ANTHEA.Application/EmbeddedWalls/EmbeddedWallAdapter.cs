using System.Text.Json.Nodes;
using Anthea.Application.Stratigraphy;
using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Design.EmbeddedWalls;
using GPC.Model.Geotechnics;
using GPC.Model.Geotechnics.EmbeddedWalls;

namespace Anthea.Application.EmbeddedWalls;

/// <summary>Unit conversion and contracts only. No constitutive, section or normative equations.</summary>
public static class EmbeddedWallAdapter
{
    internal static string Text(JsonObject row, string key) => row[key]?.ToString() ?? "";
    internal static double Number(JsonObject row, string key) => Optional(row, key) ?? throw new ArgumentException("Dato obbligatorio: " + key);
    internal static double? Optional(JsonObject row, string key) => StratigraphyDocument.Number(row[key], key);
    private static bool Flag(JsonObject row, string key) => row[key] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : throw new ArgumentException("Scelta non valida: " + key);
    private static T Choice<T>(JsonObject row, string key) where T : struct, Enum => Enum.TryParse<T>(Text(row, key), out var value) && Enum.IsDefined(value) ? value : throw new ArgumentException("Scelta non valida: " + key);
    private static int Integer(JsonObject row, string key)
    { double value = Number(row, key); return value == Math.Truncate(value) && value is >= int.MinValue and <= int.MaxValue ? (int)value : throw new ArgumentException("Numero intero richiesto: " + key); }
    public static IReadOnlyList<EmbeddedWallRcStrip> ConcreteSections(JsonObject data) => EmbeddedWallDocument.Rows(data, "rc_sections").Select(s => new EmbeddedWallRcStrip(
        Text(s, "id"), Number(s, "width"), Number(s, "thickness"), Number(s, "cover"), Integer(s, "back_bars"), Number(s, "back_diameter"),
        Integer(s, "front_bars"), Number(s, "front_diameter"), Number(s, "fck"), Number(s, "fyk"), Number(s, "es"), Number(s, "eps_uk"), Number(s, "creep"), Text(s, "exposure"))).ToArray();
    public static WallSolverOptions Options(JsonObject data) => new() { InitialSubsteps = Integer(data, "substeps") };
    public static EmbeddedWallModel ToModel(JsonObject data)
    {
        EmbeddedWallDocument.Validate(data);
        var stages = EmbeddedWallDocument.Rows(data, "stages").ToArray();
        var ids = stages.Select(s => Text(s, "id")).ToHashSet(StringComparer.Ordinal);
        foreach (var row in EmbeddedWallDocument.Rows(data, "activations").Concat(EmbeddedWallDocument.Rows(data, "loads")))
            if (!ids.Contains(Text(row, "stage"))) throw new ArgumentException("Carico o attivazione riferito a una fase inesistente.");
        var m = new EmbeddedWallModel
        {
            Name = Text(data, "name"), CaseId = Text(data, "case_id"), Top = Number(data, "top") * SoilUnits.Metre, Bottom = Number(data, "bottom") * SoilUnits.Metre,
            TributaryWidth = Number(data, "width") * SoilUnits.Metre, MaximumElementLength = Number(data, "mesh") * SoilUnits.Metre,
            FixTopDisplacement = Flag(data, "fix_top_u"), FixTopRotation = Flag(data, "fix_top_rotation"),
            FixBottomDisplacement = Flag(data, "fix_bottom_u"), FixBottomRotation = Flag(data, "fix_bottom_rotation"),
            Segments = EmbeddedWallDocument.Rows(data, "segments").Select(s => new EmbeddedWallSegment { SectionId = Text(s, "section"),
                Top = Number(s, "top") * SoilUnits.Metre, Bottom = Number(s, "bottom") * SoilUnits.Metre, FlexuralRigidity = Number(s, "ei") * 1e9,
                WeightPerLength = Number(s, "weight"), Source = Text(s, "source") }).ToArray(),
            LeftSoils = Soils(data, "left_soils"), RightSoils = Soils(data, "right_soils"),
            Supports = EmbeddedWallDocument.Rows(data, "supports").Select(s => new EmbeddedWallSupport { Id = Text(s, "id"),
                Elevation = Number(s, "elevation") * SoilUnits.Metre, Kind = Choice<EmbeddedWallSupportKind>(s, "kind"), AxialStiffness = Number(s, "stiffness"),
                Inclination = Number(s, "angle") * SoilUnits.Degree, ResistingDirection = Integer(s, "direction"), Spacing = Number(s, "spacing") * SoilUnits.Metre,
                Capacity = Optional(s, "capacity") * 1000 }).ToArray(),
            Stages = stages.Select(s => new EmbeddedWallStage { Id = Text(s, "id"), Left = Ground(s, "left"), Right = Ground(s, "right"),
                DistributedLoad = Number(s, "distributed_load"), StiffnessFactor = Number(s, "stiffness_factor"),
                ActiveSupports = EmbeddedWallDocument.Rows(data, "activations").Where(a => Text(a, "stage") == Text(s, "id")).Select(a =>
                    new EmbeddedWallSupportActivation { SupportId = Text(a, "support"), Prestress = Number(a, "prestress") * 1000 }).ToArray(),
                PointLoads = EmbeddedWallDocument.Rows(data, "loads").Where(p => Text(p, "stage") == Text(s, "id")).Select(p => new EmbeddedWallPointLoad {
                    Elevation = Number(p, "elevation") * SoilUnits.Metre, HorizontalForce = Number(p, "horizontal") * 1000, Moment = Number(p, "moment") * 1e6,
                    DownwardForce = Number(p, "downward") * 1000 }).ToArray() }).ToArray()
        };
        EmbeddedWallAnalysis.Validate(m); return m;
    }
    private static EmbeddedWallGround Ground(JsonObject s, string side) => new() { Elevation = Number(s, side + "_ground") * SoilUnits.Metre,
        WaterElevation = Optional(s, side + "_water") * SoilUnits.Metre, Surcharge = Number(s, side + "_surcharge") * SoilUnits.KiloPascal };
    private static EmbeddedWallSoil[] Soils(JsonObject data, string table) => EmbeddedWallDocument.Rows(data, table).Select(s => new EmbeddedWallSoil {
        Soil = new Soil(Text(s, "name"), Number(s, "gamma") * SoilUnits.KiloNewtonPerCubicMetre, Number(s, "gamma_sat") * SoilUnits.KiloNewtonPerCubicMetre,
            Number(s, "phi") * SoilUnits.Degree, Number(s, "c") * SoilUnits.KiloPascal, Text(s, "source"), Optional(s, "cu") * SoilUnits.KiloPascal),
        Top = Number(s, "top") * SoilUnits.Metre, Bottom = Number(s, "bottom") * SoilUnits.Metre, HorizontalModulus = Number(s, "kh"),
        AtRestCoefficient = Number(s, "k0"), Drainage = Choice<EmbeddedWallDrainage>(s, "drainage"), StiffnessSource = Text(s, "stiffness_source") }).ToArray();
}
