using System.Text.Json.Nodes;

namespace Anthea.Calculations;

public static partial class RetainingWall
{
    public static string ReinforcementKey(JsonObject d, string member, double? position = null) => member == "Fusto" ? d["reinforcement"].B("two_zones") && position is double z && z < d["geometry"].D("height") - d["reinforcement"].D("lower_height") - 1e-9 ? "stem_upper" : "stem" : member == "Valle" ? "toe" : "heel";
    public static JsonObject SectionInput(JsonObject d, string member, double thickness, double? position = null)
    {
        string key = ReinforcementKey(d, member, position);
        var r = d["reinforcement"]![key]!; var m = d["materials"]!;
        double count = r.Required("count", 2), dia = r.Required("diameter", 6);
        if (count % 1 != 0 || count > 30 || dia > 40) throw new ArgumentException("Armature: da 2 a 30 barre per metro e per faccia; diametro da 6 a 40 mm.");
        double cover = m.D("cover");
        if (2 * (cover + dia) >= thickness * 1000) throw new ArgumentException(member + ": spessore insufficiente per due facce di armatura e copriferro.");
        var input = MaterialSectionInput(d);
        input["width_mm"] = 1000; input["height_mm"] = thickness * 1000; input["cover_mm"] = cover;
        input["staffe_presenti"] = "No"; input["transverse_bar_diameter_mm"] = 0; input["side_bar_count_per_side"] = 0;
        double otherCount = r.B("symmetric", true) ? count : r.Required("opposite_count", 2), otherDia = r.B("symmetric", true) ? dia : r.Required("opposite_diameter", 6);
        if (otherCount % 1 != 0 || otherCount > 30 || otherDia > 40 || 2 * cover + dia + otherDia >= thickness * 1000) throw new ArgumentException("Armatura della faccia opposta incompatibile con la sezione.");
        input["top_bar_count"] = otherCount; input["bottom_bar_count"] = count; input["top_bar_diameter_mm"] = otherDia; input["bottom_bar_diameter_mm"] = dia;
        input["fck_mpa"] = m.D("fck"); input["fyk_mpa"] = m.D("fyk"); input["axial_force_kn"] = 0; input["moment_x_knm"] = 0; input["moment_y_knm"] = 0;
        return input;
    }
    private static List<Check> StructuralChecks(JsonObject d, List<LoadCase> cases, CancellationToken token, out double steelKg)
    {
        var result = new List<Check>(); steelKg = 0; var m = d["materials"]!;
        void Stamp(int start, SectionForce force) { for (int i = start; i < result.Count; i++) result[i] = result[i] with { Member = force.Name, Position = force.Position }; }
        if (d.S("family") == "gravity")
        {
            if (d["gravity_design"].S("type") != "Resistenze assegnate") return GravityChecks(d, cases);
            foreach (var c in cases.Where(c => c.State is "SLU" or "SISMA" or "ECCEZIONALE")) foreach (var f in c.Sections)
            {
                int firstCheck = result.Count;
                if (f.Name != "Fusto" && !c.Contact.Valid) { result.Add(CheckValue(f.Name + " · resistenza", c.Name, 0, null, "MPa", "Reazioni non disponibili: perdita di equilibrio")); continue; }
                if (f.Position <= 0 && f.N == 0 && f.V == 0 && f.M == 0) continue;
                string label = f.Name + " z=" + f.Position.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " m";
                double max = f.N / f.Thickness / 1000 + 6 * Math.Abs(f.M) / (f.Thickness * f.Thickness) / 1000;
                result.Add(CheckValue(label + " · compressione", c.Name, max, m.D("compression_rd"), "MPa"));
                if (f.Name == "Fusto") result.Add(CheckValue(label + " · assenza trazione", c.Name, Math.Abs(f.M), f.N * f.Thickness / 6, "kNm/m"));
                else result.Add(CheckValue(label + " · flessione fondazione", c.Name, max, m.D("tension_rd"), "MPa"));
                result.Add(CheckValue(label + " · taglio", c.Name, 1.5 * Math.Abs(f.V) / f.Thickness / 1000, m.D("shear_rd"), "MPa"));
                Stamp(firstCheck, f);
            }
            return result;
        }
        var geometry = d["geometry"]!;
        bool two = d["reinforcement"].B("two_zones"); double lower = two ? d["reinforcement"].D("lower_height") : geometry.D("height");
        foreach (var (key, length) in new[] { ("stem", lower), ("stem_upper", two ? geometry.D("height") - lower : 0), ("toe", geometry.D("toe") + geometry.D("stem_base") / 2), ("heel", geometry.D("heel") + geometry.D("stem_base") / 2) })
        {
            if (length == 0) continue;
            var arm = d["reinforcement"]![key]!;
            double other = arm.B("symmetric", true) ? arm.D("count") * Math.Pow(arm.D("diameter"), 2) : arm.D("opposite_count") * Math.Pow(arm.D("opposite_diameter"), 2);
            steelKg += (arm.D("count") * Math.Pow(arm.D("diameter"), 2) + other) * Math.PI / 4 * 1e-6 * length * 7850;
        }
        var cache = new Dictionary<(string, double), (JsonObject Input, JsonObject Ws, CheckerSection Ultimate, CheckerSection Service, JsonObject Options)>();
        var moments = new Dictionary<(string, double, double, int), double?>();
        var uncracked = new Dictionary<(string, double), CheckerSection>();
        foreach (var c in cases) foreach (var f in c.Sections.Where(s => s.Position > 0 || s.N != 0 || s.V != 0 || s.M != 0))
        {
            int firstCheck = result.Count;
            token.ThrowIfCancellationRequested();
            string label = f.Name + " z=" + f.Position.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " m";
            if (f.Name != "Fusto" && !c.Contact.Valid) { result.Add(CheckValue(label + " · resistenza", c.Name, 0, null, "kNm/m", "Reazioni non disponibili: perdita di equilibrio")); continue; }
            // Input errors propagate. Native convergence failures produce an explicit missing verification.
            var input = SectionInput(d, f.Name, f.Thickness, f.Position); string memberKey = ReinforcementKey(d, f.Name, f.Position);
            string tension = f.M >= 0 ? "bottom" : "top";
            double dia = input.D(tension + "_bar_diameter_mm"), asFace = input.D(tension + "_bar_count") * Math.PI * dia * dia / 4;
            double eff = f.Thickness * 1000 - m.D("cover") - dia / 2;
            if (!cache.TryGetValue((memberKey, f.Thickness), out var engines))
            {
                var def = SezioneCA.DefaultData(); def["input"] = input.DeepClone();
                var ws = SectionWorkspace.Prepare(def);
                var opt = ws["sle"]!["SLE_QP"]!.AsObject(); opt["modello"] = "Lineare"; opt["trazione_cls"] = "No"; opt["phi"] = m.D("creep");
                opt["esposizione"] = m.S("exposure"); opt["sensibilita"] = "Non sensibile"; opt["aderenza"] = "Migliorata"; opt["durata"] = "Lunga";
                opt["spaziatura_fessure"] = new[] { "top", "bottom" }.Max(face => (1000 - 2 * m.D("cover") - input.D(face + "_bar_diameter_mm")) / (input.D(face + "_bar_count") - 1)); opt["copriferro_fessure"] = m.D("cover");
                engines = (input, ws, new CheckerSection(input, ws, J.Obj(("criterio", "N costante"), ("modello", "Non lineare"))), new CheckerSection(input, ws, opt), opt);
                cache[(memberKey, f.Thickness)] = engines;
                double minimumDepth = f.Thickness * 1000 - m.D("cover") - Math.Min(input.D("top_bar_diameter_mm"), input.D("bottom_bar_diameter_mm")) / 2;
                double minimum = Math.Max(.26 * ConcreteMaterials.Concrete(input).Fctm / m.D("fyk"), .0013) * 1000 * minimumDepth;
                if (f.Name == "Fusto") minimum = Math.Max(minimum, .001 * 1000 * f.Thickness * 1000);
                double topAs = input.D("top_bar_count") * Math.PI * Math.Pow(input.D("top_bar_diameter_mm"), 2) / 4;
                double bottomAs = input.D("bottom_bar_count") * Math.PI * Math.Pow(input.D("bottom_bar_diameter_mm"), 2) / 4;
                result.Add(CheckValue(label + " · armatura minima per faccia", "Dettagli", minimum, Math.Min(topAs, bottomAs), "mm²/m"));
                result.Add(CheckValue(label + " · armatura massima", "Dettagli", topAs + bottomAs, .04 * 1000 * f.Thickness * 1000, "mm²/m"));
            }
            try
            {
                if (c.State is "SLU" or "SISMA" or "ECCEZIONALE")
                {
                    int sign = f.M >= 0 ? 1 : -1; var key = (memberKey, f.Thickness, f.N, sign);
                    if (!moments.TryGetValue(key, out double? resistance))
                    {
                        var value = SectionMomentResistance.Calculate(engines.Ultimate, -f.N, [("Mx", sign, 0d)], token)[0];
                        resistance = value.Moment is double mv ? Math.Abs(mv) : null; moments[key] = resistance;
                    }
                    result.Add(CheckValue(label + " · N–M GPC", c.Name, Math.Abs(f.M), resistance, "kNm/m", "GPC: resistenza non convergente o N fuori dominio"));
                    double k = Math.Min(2, 1 + Math.Sqrt(200 / eff)), rho = Math.Min(.02, asFace / (1000 * eff));
                    double vrd = Math.Max(.18 / input.D("gamma_c") * k * Math.Pow(100 * rho * m.D("fck"), 1d / 3), .035 * Math.Pow(k, 1.5) * Math.Sqrt(m.D("fck"))) * 1000 * eff / 1000;
                    result.Add(CheckValue(label + " · taglio senza staffe", c.Name, Math.Abs(f.V), vrd, "kN/m"));
                }
                else
                {
                    var action = new ActionPoint(-f.N, f.M, 0); var state = engines.Service.Stress(action, c.State);
                    // GPC stores strain gradients: Mx bends across Y, hence ChiY (not ChiX).
                    if (f.Name == "Fusto") c.Curvatures.Add(new(d["geometry"].D("height") - f.Position, Math.Sign(f.M) * Math.Abs(state.Native.StrainPlane.ChiY) * 1000));
                    if (state.ConcreteStressLimit is double limit) result.Add(CheckValue(label + " · tensione CLS", c.Name, Math.Max(0, -state.sigma_cls), limit, "MPa"));
                    if (c.State == "SLE") result.Add(CheckValue(label + " · tensione acciaio", c.Name, state.sigma_acciaio, state.SteelStressLimit, "MPa"));
                    if (c.State != "SLE")
                    {
                        var crack = Ntc2018Checks.Cracking(engines.Service, state, action, input, engines.Ws, engines.Options, c.State);
                        // In weakly stressed sections the no-tension solution may have no tensile
                        // reinforcement in the effective zone. Establish absence of cracking with
                        // a separate GPC uncracked analysis, never turn arbitrary missing checks into passes.
                        if (crack.Width is null && crack.Status is "Nessuna armatura tesa" or "Armatura/area efficace assente")
                        {
                            if (!uncracked.TryGetValue((memberKey, f.Thickness), out var elastic))
                            {
                                var elasticOptions = (JsonObject)engines.Options.DeepClone(); elasticOptions["trazione_cls"] = "Sì";
                                elastic = new CheckerSection(input, engines.Ws, elasticOptions); uncracked[(memberKey, f.Thickness)] = elastic;
                            }
                            var elasticState = elastic.Stress(action, c.State);
                            double tensile = elasticState.Native.GetConcreteVerticesTension(m.D("creep")).Max(x => x.tension);
                            double fctk = ConcreteMaterials.Concrete(input).Fctk05;
                            if (tensile <= fctk)
                            {
                                result.Add(CheckValue(label + " · fessurazione", c.Name, 0, crack.Limit, "mm") with { Status = $"Non fessurata: σt,el={tensile:0.###} ≤ fctk,0.05={fctk:0.###} MPa (GPC)" });
                                Stamp(firstCheck, f); continue;
                            }
                        }
                        result.Add(crack.Width is double wk ? CheckValue(label + " · fessurazione", c.Name, wk, crack.Limit, "mm", crack.Status) : CheckValue(label + " · fessurazione", c.Name, 0, null, "mm", crack.Status));
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { result.Add(CheckValue(label + " · " + c.State, c.Name, Math.Abs(f.M), null, "kNm/m", "Non calcolata: " + ex.Message)); }
            Stamp(firstCheck, f);
        }
        return result;
    }
}
