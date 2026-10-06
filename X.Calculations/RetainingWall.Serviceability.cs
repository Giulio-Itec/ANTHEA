using System.Text.Json.Nodes;
using GPC.Checkers.Geotechnics.Seismic;
using GPC.Checkers.Geotechnics.Walls;
using Anthea.Calculations.Geotechnics;
using GpcFoundations = GPC.Checkers.Geotechnics.Foundations;

namespace Anthea.Calculations;

public static partial class RetainingWall
{
    public sealed record CurvaturePoint(double Y, double Curvature);
    public sealed record DisplacementPoint(double Y, double Curvature, double Rotation, double DisplacementMm);
    public sealed record ServiceCase(string Combination, double? ToeSettlement, double? CentreSettlement, double? HeelSettlement,
        double? FoundationRotation, double? StemDisplacement, double? HeadDisplacement, string Status,
        IReadOnlyList<Geotechnics.SettlementSlice> Slices, IReadOnlyList<DisplacementPoint> Shape);
    public sealed record SeismicDisplacementCase(string Name, string State, double YieldG, double Scale, double LimitMm,
        SlidingHistory? History, string Status);
    public sealed record ServiceResult(IReadOnlyList<ServiceCase> Cases, IReadOnlyList<SeismicDisplacementCase> Earthquakes);

    public static void CompleteAdvancedInput(JsonObject d)
    {
        void Add(string key, JsonObject defaults)
        {
            if (d[key] is null) d[key] = defaults;
            else if (d[key] is not JsonObject) throw new ArgumentException("Formato non valido: " + key);
            else foreach (var p in defaults) if (d[key]![p.Key] is null) d[key]![p.Key] = p.Value?.DeepClone();
        }
        Add("bearing_seismic", J.Obj(("source", "Da sito"), ("ground_kh", ""), ("ground_kv", ""), ("model_factor", 1.15)));
        Add("bar_schedule", J.Obj(("panel_length", 1), ("end_cover", d["materials"].D("cover", 40)),
            ("stock_length", 12), ("ties_per_m", 2), ("tie_cut_length", 0)));
        Add("serviceability", J.Obj(("settlement", false), ("displacement", false), ("removed_pressure", ""),
            ("settlement_limit", 25), ("rotation_limit", .002), ("head_limit", 20), ("horizontal_stiffness", ""),
            ("rigid_base", false), ("layers", new JsonArray()), ("histories", new JsonArray())));
        Add("detailing", J.Obj(("enabled", false), ("aggregate", 20), ("lap_percent", 100), ("lap_clear", 0),
            ("good_bond", false), ("life", 50), ("cover_deviation", 10), ("tie_diameter", 8), ("tie_spacing", 200),
            ("max_diameter", 32), ("max_count", 12), ("target_ratio", .95)));
        Add("gravity_design", J.Obj(("type", "Resistenze assegnate"), ("fk", ""), ("fvk0", ""), ("fvk_limit", ""),
            ("gamma_m", 2.5), ("confidence", 1), ("eccentricity", 0), ("effective_height", ""), ("elastic_modulus", "")));
        var r = d["reinforcement"]!.AsObject();
        if (r["stem_upper"] is null) r["stem_upper"] = r["stem"]!.DeepClone();
        foreach (string key in new[] { "stem", "stem_upper", "toe", "heel" })
        {
            var a = r[key]!.AsObject();
            foreach (var p in J.Obj(("symmetric", true), ("opposite_diameter", a.D("diameter")), ("opposite_count", a.D("count")),
                ("secondary_diameter", 10), ("secondary_spacing", 200), ("anchor_length", 0), ("lap_length", 0),
                ("bend_diameter", 0))) if (a[p.Key] is null) a[p.Key] = p.Value?.DeepClone();
        }
    }

    internal static double AdvancedNumber(JsonNode node, string key, double min = 0, double max = double.MaxValue)
    {
        if (J.Number(node[key]) is not double value || !double.IsFinite(value) || value < min || value > max)
            throw new ArgumentException($"{key}: inserire un valore finito fra {min} e {max}.");
        return value;
    }

    /// <summary>
    /// Serviceability of the wall with GPCChecker.Geotechnics (WallServiceability): settlements, rotation and displacements of the SLS combinations
    /// with the curvatures of the stem found by the section checks, Newmark sliding of the accelerograms. The data of the document are read with
    /// their ranges; an invalid datum is reported on the checks of every combination, as an incomplete calculation.
    /// </summary>
    private static ServiceResult CalculateService(JsonObject d, WallInput input, WallResult wall, List<LoadCase> cases, List<Check> checks, CancellationToken token)
    {
        var opt = d["serviceability"]!; bool settlement = opt.B("settlement"), displacement = opt.B("displacement");
        WallSettlementOptions? settlementOptions = null; string? settlementError = null;
        if (settlement)
            try
            {
                var layers = opt.Array("layers").Select(l => new GpcFoundations.SettlementLayer(l.S("name"), AdvancedNumber(l!, "thickness", .01, 100) * Mm, AdvancedNumber(l!, "modulus", 1, 1e8) * KPa)).ToArray();
                settlementOptions = new WallSettlementOptions(layers, AdvancedNumber(opt, "removed_pressure", 0, 5000) * KPa, AdvancedNumber(opt, "settlement_limit", .01, 1000),
                    AdvancedNumber(opt, "rotation_limit", 1e-6, .1), opt.B("rigid_base"));
            }
            catch (ArgumentException ex) { settlementError = ex.Message; }
        // A gravity wall with assigned strengths has no stiffness: the library reports the missing curvatures.
        bool assigned = d.S("family") == "gravity" && d["gravity_design"].S("type") == "Resistenze assegnate";
        var byName = cases.ToDictionary(c => c.Name);
        IReadOnlyList<WallCurvaturePoint>? Curvatures(WallCaseResult c) => assigned ? null
            : byName[c.Combination.Name].Curvatures.Select(p => new WallCurvaturePoint(p.Y * Mm, p.Curvature / Mm)).ToArray();
        var records = new List<(WallAccelerogram? Record, JsonNode Source, string? Error)>();
        foreach (var h in opt.Array("histories").Where(r => r.B("enabled")))
            try
            {
                var samples = h.Array("samples").Select(s => new AccelerogramSample(AdvancedNumber(s!, "t", 0, 10000), AdvancedNumber(s!, "a_g", -10, 10))).ToArray();
                records.Add((new WallAccelerogram(h.S("name"), h.S("state"), h.B("compatible"), Value(h, "yield_g"), Value(h, "scale"), Value(h, "limit_mm"), samples), h!, null));
            }
            catch (ArgumentException ex) { records.Add((null, h!, ex.Message)); }
        token.ThrowIfCancellationRequested();
        var options = new WallServiceOptions(settlementOptions, displacement ? (Value(opt, "head_limit"), Value(opt, "horizontal_stiffness") * KPa) : null,
            records.Where(r => r.Record is not null).Select(r => r.Record!));
        var service = WallServiceability.Calculate(input, wall, options, displacement ? Curvatures : null, token);
        var result = new List<ServiceCase>();
        if (settlement || displacement)
            foreach (var c in wall.Cases.Where(c => c.Combination.Service))
            {
                var s = service.Cases.FirstOrDefault(x => x.Combination == c.Combination.Name);
                var own = service.Checks.Where(x => x.Combination == c.Combination.Name && x.Kind != WallCheckKind.Newmark).Select(x => ToCheck(x)).ToList();
                var messages = new List<string>();
                if (settlementError is not null)
                {
                    // As the settlement of the case: the contact first, then the data.
                    string message = c.Contact.Valid ? settlementError : "Contatto fondazione non disponibile.";
                    own.Insert(0, CheckValue("Cedimenti", c.Combination.Name, 0, null, "mm", message)); messages.Add(message);
                }
                if (s is not null && s.Status != "Calcolato") messages.Add(s.Status);
                checks.AddRange(own);
                result.Add(new(c.Combination.Name, s?.ToeSettlement, s?.CentreSettlement, s?.HeelSettlement, s?.Rotation, s?.StemDisplacement, s?.HeadDisplacement,
                    messages.Count == 0 ? "Calcolato" : string.Join("; ", messages), (s?.Slices ?? []).Select(Geotechnics.SettlementSlice.From).ToList(),
                    (s?.Shape ?? []).Select(p => new DisplacementPoint(p.Height / Mm, p.Curvature * Mm, p.Rotation, p.Displacement)).ToList()));
            }
        var histories = new List<SeismicDisplacementCase>(); int next = 0;
        foreach (var (record, source, error) in records)
        {
            string name = source.S("name"), state = source.S("state");
            if (error is not null)
            {
                checks.Add(CheckValue("Spostamento Newmark", name, 0, null, "mm", error)); histories.Add(new(name, state, 0, 0, 0, null, error)); continue;
            }
            var sliding = service.Sliding[next++]; checks.Add(ToCheck(sliding.Check, state));
            histories.Add(sliding.History is null ? new(name, state, 0, 0, 0, null, sliding.Check.Message)
                : new(name, state, record!.YieldAcceleration, record.Scale, record.Limit, SlidingHistory.From(sliding.History), sliding.Check.Message));
        }
        return new(result, histories);
    }
}
