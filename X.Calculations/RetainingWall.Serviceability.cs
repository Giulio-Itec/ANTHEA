using System.Text.Json.Nodes;
using Anthea.Calculations.Geotechnics;

namespace Anthea.Calculations;

public static partial class RetainingWall
{
    public sealed record CurvaturePoint(double Y, double Curvature);
    public sealed record DisplacementPoint(double Y, double Curvature, double Rotation, double DisplacementMm);
    public sealed record ServiceCase(string Combination, double? ToeSettlement, double? CentreSettlement, double? HeelSettlement,
        double? FoundationRotation, double? StemDisplacement, double? HeadDisplacement, string Status,
        IReadOnlyList<FoundationSettlement.Slice> Slices, IReadOnlyList<DisplacementPoint> Shape);
    public sealed record SeismicDisplacementCase(string Name, string State, double YieldG, double Scale, double LimitMm,
        NewmarkSliding.Result? History, string Status);
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

    private static ShallowFoundationSeismic.Result? SeismicBearing(JsonObject d, double width, double gamma,
        double phi, double n, double v, double eccentricity, double kv, double r, out string error)
    {
        error = "";
        try
        {
            var b = d["bearing_seismic"]!;
            double kh, vertical;
            if (b.S("source") == "Da sito")
            {
                var site = DeriveSeismic(d) ?? throw new ArgumentException("Portanza sismica: inserire l’accelerazione del terreno (non il kh ridotto del muro), oppure completare i parametri del sito.");
                kh = site.AmaxG; vertical = .5 * kh;
            }
            else if (b.S("source") == "Assegnata") { kh = AdvancedNumber(b, "ground_kh", 0, 1); vertical = AdvancedNumber(b, "ground_kv", 0, .9); }
            else throw new ArgumentException("Selezionare l’origine dell’accelerazione per la portanza sismica.");
            return ShallowFoundationSeismic.Calculate(width, gamma, phi, n, v, n * eccentricity, kh,
                vertical * (kv < 0 ? -1 : 1), AdvancedNumber(b, "model_factor", 1, 2), r);
        }
        catch (ArgumentException ex) { error = ex.Message; return null; }
    }

    public static IReadOnlyList<DisplacementPoint> IntegrateCurvature(IEnumerable<CurvaturePoint> values, double height)
    {
        var points = values.OrderBy(p => p.Y).ToList();
        if (points.Count < 2 || points.Any(p => !double.IsFinite(p.Y + p.Curvature))) throw new ArgumentException("Curvature insufficienti o non convergenti.");
        if (points[0].Y > 1e-6) throw new ArgumentException("Curvatura al piede non disponibile.");
        if (points[^1].Y < height - 1e-6) points.Add(new(height, 0));
        var result = new List<DisplacementPoint> { new(0, points[0].Curvature, 0, 0) }; double rotation = 0, displacement = 0;
        for (int i = 1; i < points.Count; i++)
        {
            double length = points[i].Y - points[i - 1].Y, a = points[i - 1].Curvature, b = points[i].Curvature;
            displacement += rotation * length + length * length * (2 * a + b) / 6;
            rotation += length * (a + b) / 2; result.Add(new(points[i].Y, b, rotation, displacement * 1000));
        }
        return result;
    }

    private static ServiceResult CalculateService(JsonObject d, List<LoadCase> cases, List<Check> checks, CancellationToken token)
    {
        var opt = d["serviceability"]!; var result = new List<ServiceCase>(); var histories = new List<SeismicDisplacementCase>();
        double width = d["geometry"].D("toe") + d["geometry"].D("stem_base") + d["geometry"].D("heel"), height = d["geometry"].D("height");
        foreach (var c in cases.Where(c => c.State.StartsWith("SLE")))
        {
            token.ThrowIfCancellationRequested();
            double? toe = null, centre = null, heel = null, rotation = null, stem = null, head = null;
            IReadOnlyList<FoundationSettlement.Slice> slices = []; IReadOnlyList<DisplacementPoint> shape = []; var messages = new List<string>();
            if (opt.B("settlement")) try
            {
                if (!c.Contact.Valid) throw new ArgumentException("Contatto fondazione non disponibile.");
                var layers = opt.Array("layers").Select(l => new FoundationSettlement.Layer(l.S("name"), AdvancedNumber(l!, "thickness", .01, 100), AdvancedNumber(l!, "modulus", 1, 1e8))).ToArray();
                double removed = AdvancedNumber(opt, "removed_pressure", 0, 5000);
                FoundationSettlement.Result At(double x, int steps = 40) => FoundationSettlement.Calculate(layers, x, c.Contact.Start, c.Contact.End, c.Contact.Toe, c.Contact.Heel, removed, width, steps);
                var low = At(0); var mid = At(width / 2); var high = At(width); var fine = At(width / 2, 80);
                toe = low.SettlementMm; centre = mid.SettlementMm; heel = high.SettlementMm; rotation = (heel - toe) / (1000 * width); slices = mid.Slices;
                bool converged = Math.Abs(fine.SettlementMm - mid.SettlementMm) <= Math.Max(.01, .01 * Math.Abs(fine.SettlementMm));
                bool depth = opt.B("rigid_base") || Math.Max(low.BottomStress, Math.Max(mid.BottomStress, high.BottomStress)) <= .1 * Math.Max(1e-9, c.Contact.Peak - removed);
                string incomplete = !converged ? "Ricerca incompleta: integrare più finemente gli strati" : !depth ? "Profilo insufficiente: tensione residua al fondo >10% del carico netto; approfondire o documentare il substrato rigido" : "";
                double maximum = Math.Max(toe.Value, Math.Max(centre.Value, heel.Value));
                var check = CheckValue("Cedimento edometrico finale", c.Name, maximum, AdvancedNumber(opt, "settlement_limit", .01, 1000), "mm");
                checks.Add(incomplete == "" || check.Ratio > 1 ? check : check with { Ratio = null, Status = incomplete });
                var rotationCheck = CheckValue("Rotazione da cedimenti differenziali", c.Name, Math.Abs(rotation.Value), AdvancedNumber(opt, "rotation_limit", 1e-6, .1), "rad");
                checks.Add(incomplete == "" || rotationCheck.Ratio > 1 ? rotationCheck : rotationCheck with { Ratio = null, Status = incomplete });
                if (incomplete != "") messages.Add(incomplete);
            }
            catch (ArgumentException ex) { checks.Add(CheckValue("Cedimenti", c.Name, 0, null, "mm", ex.Message)); messages.Add(ex.Message); }
            if (opt.B("displacement")) try
            {
                if (d.S("family") == "gravity" && d["gravity_design"].S("type") == "Resistenze assegnate") throw new ArgumentException("Selezionare il materiale della gravità e il modulo elastico per gli spostamenti.");
                int required = c.Sections.Count(f => f.Name == "Fusto" && (f.Position > 0 || f.N != 0 || f.V != 0 || f.M != 0));
                if (c.Curvatures.Count != required) throw new ArgumentException("Curvature mancanti in una o più sezioni: spostamento non disponibile.");
                shape = IntegrateCurvature(c.Curvatures, height); stem = shape[^1].DisplacementMm;
                checks.Add(CheckValue("Spostamento elastico fusto · base fissa", c.Name, Math.Abs(stem.Value), AdvancedNumber(opt, "head_limit", .01, 1000), "mm"));
                double stiffness = AdvancedNumber(opt, "horizontal_stiffness", .01, 1e10);
                if (rotation is null || messages.Count > 0) throw new ArgumentException("Spostamento totale: completare il calcolo convergente dei cedimenti/rotazione.");
                double translation = c.Horizontal / stiffness * 1000;
                head = stem + translation - rotation * height * 1000;
                checks.Add(CheckValue("Spostamento totale in testa · stima disaccoppiata", c.Name, Math.Abs(head.Value), opt.D("head_limit"), "mm"));
            }
            catch (ArgumentException ex) { checks.Add(CheckValue("Spostamento totale in testa", c.Name, 0, null, "mm", ex.Message)); messages.Add(ex.Message); }
            if (opt.B("settlement") || opt.B("displacement")) result.Add(new(c.Name, toe, centre, heel, rotation, stem, head, messages.Count == 0 ? "Calcolato" : string.Join("; ", messages), slices, shape));
        }
        foreach (var record in opt.Array("histories").Where(r => r.B("enabled")))
        {
            token.ThrowIfCancellationRequested(); string name = record.S("name"), state = record.S("state");
            try
            {
                if (state is not ("SLD" or "SLV") || !record.B("compatible")) throw new ArgumentException("Confermare compatibilità dell’accelerogramma con il sito e lo stato limite SLD/SLV.");
                if (d["seismic"].S("method") == "Wood semplificato") throw new ArgumentException("Newmark richiede un muro capace di scorrere; Wood descrive un muro vincolato.");
                double ky = AdvancedNumber(record!, "yield_g", .0001, 2), scale = AdvancedNumber(record!, "scale", .0001, 100), limit = AdvancedNumber(record!, "limit_mm", .01, 1000);
                var samples = record.Array("samples").Select(s => new NewmarkSliding.Sample(AdvancedNumber(s!, "t", 0, 10000), AdvancedNumber(s!, "a_g", -10, 10))).ToArray();
                var history = NewmarkSliding.Calculate(samples, ky, scale);
                var check = CheckValue("Scorrimento permanente Newmark · " + state, name, history.DisplacementMm, limit, "mm"); checks.Add(check);
                histories.Add(new(name, state, ky, scale, limit, history, check.Status));
            }
            catch (ArgumentException ex) { checks.Add(CheckValue("Spostamento Newmark", name, 0, null, "mm", ex.Message)); histories.Add(new(name, state, 0, 0, 0, null, ex.Message)); }
        }
        return new(result, histories);
    }
}
