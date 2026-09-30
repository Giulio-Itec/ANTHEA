using System.Text.Json.Nodes;

namespace Anthea.Calculations;

public static partial class RetainingWall
{
    private const double Rad = Math.PI / 180, Gw = 9.81;
    public static double Ka(double phi) => Math.Pow(Math.Tan(Math.PI / 4 - phi * Rad / 2), 2);
    public static double SeismicKa(double phi, double kh, double kv)
    {
        double p = phi * Rad, theta = Math.Atan2(kh, 1 - kv);
        if (theta >= p) throw new ArgumentException("Mononobe–Okabe non applicabile: atan(kh/(1−kv)) deve essere minore di φ′.");
        return Math.Pow(Math.Cos(p - theta), 2) / (Math.Pow(Math.Cos(theta), 2) * Math.Pow(1 + Math.Sqrt(Math.Sin(p) * Math.Sin(p - theta) / Math.Cos(theta)), 2));
    }
    public static double[][] Outline(JsonObject d)
    {
        var g = d["geometry"]!; double h = g.D("height"), t = g.D("slab"), a = g.D("toe"), s = g.D("stem_base"), top = g.D("stem_top"), b = a + s + g.D("heel");
        return [[0, 0], [b, 0], [b, t], [a + s, t], [a + s, t + h], [a + s - top, t + h], [a, t], [0, t]];
    }
    public static Contact ContactLaw(double width, double n, double x)
    {
        if (n <= 0 || x <= 0 || x >= width) return new(0, 0, 0, 0, 0, false);
        double e = width / 2 - x;
        if (Math.Abs(e) <= width / 6) return new(0, width, n / width * (1 + 6 * e / width), n / width * (1 - 6 * e / width), n / width * (1 + 6 * Math.Abs(e) / width), true);
        double length = 3 * Math.Min(x, width - x), peak = 2 * n / length;
        return x < width / 2 ? new(0, length, peak, 0, peak, true) : new(width - length, width, 0, peak, peak, true);
    }
    public static double Pressure(Contact c, double x)
    {
        if (!c.Valid || x < c.Start || x > c.End) return 0;
        return c.Toe + (c.Heel - c.Toe) * (x - c.Start) / (c.End - c.Start);
    }
    public static (double Force, double Moment) Integrate(IEnumerable<PressureSegment> pieces, double left, double right, double root)
    {
        double f = 0, m = 0;
        foreach (var p in pieces)
        {
            double a = Math.Max(left, p.Z0), b = Math.Min(right, p.Z1); if (b <= a) continue;
            double p0 = p.P0 + (p.P1 - p.P0) * (a - p.Z0) / (p.Z1 - p.Z0), p1 = p.P0 + (p.P1 - p.P0) * (b - p.Z0) / (p.Z1 - p.Z0), l = b - a;
            f += (p0 + p1) * l / 2;
            m += (a - root) * (p0 + p1) * l / 2 + l * l * (p0 + 2 * p1) / 6;
        }
        return (f, m);
    }
    private static List<PressureSegment> ContactPieces(Contact c) => c.Valid ? [new(c.Start, c.End, c.Toe, c.Heel)] : [];
    public static Result Calculate(JsonObject data, CancellationToken token = default)
    {
        ValidateShape(data); var d = (JsonObject)data.DeepClone(); CompleteSoilInput(d); CompleteAdvancedInput(d); ResolveSeismic(d); Validate(d); var g = d["geometry"]!; var mat = d["materials"]!; var loads = d["loads"]!;
        double h = g.D("height"), t = g.D("slab"), a = g.D("toe"), s = g.D("stem_base"), top = g.D("stem_top"), heel = g.D("heel"), width = a + s + heel, ht = h + t;
        double gc = mat.D("gamma"); bool modern = d.D("version") >= 2;
        JsonArray? definitions = modern ? d.S("combination_mode") == "Automatiche" ? GenerateCombinations(d) : d.Array("combinations") : null;
        if (modern) d["combinations"] = definitions!.DeepClone();
        bool wood = d["seismic"].B("enabled") && d["seismic"].S("method") == "Wood semplificato";
        bool water = d["water"].B("enabled"); double zw = water ? d["water"].D("depth") : double.PositiveInfinity, front = water ? d["water"].D("front_head") : 0;
        var stemShape = SectionGeometry.Polygon([[a, t], [a + s, t], [a + s, ht], [a + s - top, ht]]);
        double stemArea = Math.Abs(stemShape.GetSignedArea()); var center = stemShape.GetCentroid();
        double stemWeight = stemArea * gc, slabWeight = width * t * gc;
        // Piecewise total and submerged weights. Boundaries include the water table and the slab top.
        var weights = new List<PressureSegment>(); var soil = new List<(double Z0, double Z1, double Gamma, double Effective, double Phi)>();
        double start = 0;
        foreach (var layer in d.Array("layers"))
        {
            double end = Math.Min(ht, start + layer.D("thickness")); if (end <= start) break;
            var cuts = new[] { start, end, zw, h, ht - front }.Where(z => z >= start && z <= end).Distinct().Order().ToArray();
            for (int i = 0; i < cuts.Length - 1; i++)
            {
                double z0 = cuts[i], z1 = cuts[i + 1], gamma = (z0 + z1) / 2 >= zw ? layer.D("gamma_sat") : layer.D("gamma");
                soil.Add((z0, z1, gamma, gamma - ((z0 + z1) / 2 >= zw ? Gw : 0), layer.D("phi")));
                weights.Add(new(z0, z1, gamma, gamma));
            }
            start = end; if (start >= ht) break;
        }
        var soilIntegral = Integrate(weights, 0, h, ht); double soilWeight = soilIntegral.Force * heel;
        double soilHeight = soilWeight > 0 ? -soilIntegral.Moment * heel / soilWeight : 0;
        double gammaF = water ? d["foundation"].D("gamma_sat") - Gw : d["foundation"].D("gamma");
        var valleyBands = ValleyBands(d); var frontMass = FrontSoilMass(d, valleyBands);
        double frontColumn = valleyBands.Sum(b => Math.Max(0, b.Top - Math.Max(t, b.Bottom)) * b.Gamma);
        double overburden = valleyBands.LastOrDefault()?.SigmaBottom ?? 0;
        LoadCase Case(string name, string state, double fc, double fs, double fq, double fw = 1, double kh = 0, double kv = 0, JsonObject? definition = null)
        {
            token.ThrowIfCancellationRequested(); double fp = definition?.D("valley_soil", fs) ?? fs;
            double vf = 1 - kv, sigma = 0, mf = definition?.D("mphi", 1) ?? 1;
            double Phi(double phi) => Math.Atan(Math.Tan(phi * Rad) / mf) / Rad;
            double phiF = Phi(d["foundation"].D("phi")) * Rad;
            double factorNq = Math.Exp(Math.PI * Math.Tan(phiF)) * Math.Pow(Math.Tan(Math.PI / 4 + phiF / 2), 2), ng = 2 * (factorNq - 1) * Math.Tan(phiF);
            var actions = modern ? d.Array("actions").Where(a => a.B("enabled")).Select(a => new AppliedAction(a.S("id"), a.S("name"), a.S("type"), definition!["coefficients"].D(a.S("id")), a.D("value"), a.D("z"), a.D("z0"), a.D("x"))).ToList() : [];
            double q = modern ? actions.Where(a => a.Type == ActionTypes[0]).Sum(a => a.Value * a.Factor) : loads.D("surcharge"), hq = modern ? 0 : loads.D("horizontal"), nq = modern ? 0 : loads.D("vertical");
            var horizontalActions = actions.Where(a => a.Type is "Forza orizzontale" or "Urto").ToArray();
            var verticalActions = actions.Where(a => a.Type == "Forza verticale").ToArray();
            var momentActions = actions.Where(a => a.Type == "Momento").ToArray();
            var pressures = new List<PressureSegment>();
            var stemPressures = new List<PressureSegment>(); var verticalFriction = new List<PressureSegment>();
            var stemDetails = new List<PressureDetail>();
            double deltaWall = wood ? 0 : InterfaceDelta(d, true, mf), deltaBase = InterfaceDelta(d, false, mf);
            var details = new List<PressureDetail>();
            foreach (var z in soil)
            {
                double phi = Phi(z.Phi), virtualDelta = heel > 0 ? 0 : deltaWall, k = wood ? 1 - Math.Sin(phi * Rad) : ActiveHorizontal(phi, virtualDelta), ke = k;
                double sig0 = sigma * fs * vf, sig1 = (sigma + z.Effective * (z.Z1 - z.Z0)) * fs * vf, dynamic = 0, surcharge = k * q * fq;
                if (state == "SISMA")
                {
                    ke = wood ? k : ActiveHorizontal(phi, virtualDelta, kh, kv);
                    dynamic = fs * (wood ? kh * z.Gamma * ht : (ke - k) * vf * z.Gamma * ht / 2);
                    surcharge = (wood ? k : ke * vf) * q * fq;
                }
                double w0 = fw * Gw * (Math.Max(0, z.Z0 - zw) - Math.Max(0, z.Z0 - (ht - front))), w1 = fw * Gw * (Math.Max(0, z.Z1 - zw) - Math.Max(0, z.Z1 - (ht - front)));
                double p0 = k * sig0 + surcharge + dynamic + w0, p1 = k * sig1 + surcharge + dynamic + w1;
                details.Add(new(z.Z0, z.Z1, z.Phi, phi, k, ke, sig0, sig1, k * sig0, k * sig1, surcharge, w0, w1, dynamic, p0, p1));
                pressures.Add(new(z.Z0, z.Z1, p0, p1));
                double ks = wood ? k : ActiveHorizontal(phi, deltaWall), kes = state == "SISMA" && !wood ? ActiveHorizontal(phi, deltaWall, kh, kv) : ks;
                double ds = state == "SISMA" ? fs * (wood ? kh * z.Gamma * ht : (kes - ks) * vf * z.Gamma * ht / 2) : 0;
                double qs = (state == "SISMA" && !wood ? kes * vf : ks) * q * fq;
                double sp0 = ks * sig0 + qs + ds, sp1 = ks * sig1 + qs + ds;
                stemPressures.Add(new(z.Z0, z.Z1, sp0 + w0, sp1 + w1));
                stemDetails.Add(new(z.Z0, z.Z1, z.Phi, phi, ks, kes, sig0, sig1, ks * sig0, ks * sig1, qs, w0, w1, ds, sp0 + w0, sp1 + w1));
                verticalFriction.Add(new(z.Z0, z.Z1, sp0 * Math.Tan(deltaWall * Rad), sp1 * Math.Tan(deltaWall * Rad)));
                sigma += z.Effective * (z.Z1 - z.Z0);
            }
            foreach (var act in actions.Where(a => a.Type == "Pressione laterale")) { var p = new PressureSegment(ht - act.Z, ht - act.Z0, act.Value * act.Factor, act.Value * act.Factor); pressures.Add(p); stemPressures.Add(p); }
            var valleyPressures = new List<PressureSegment>();
            double eta = d["valley"].B("passive") && state != "SISMA" ? d["valley"].D("mobilization") : 0;
            foreach (var b in valleyBands)
            {
                double kp = 1 / Ka(Phi(b.Phi));
                // A resistance is not amplified by an unfavourable permanent-action factor.
                valleyPressures.Add(new(ht - b.Top, ht - b.Bottom, eta * kp * b.SigmaTop * Math.Min(1, fp) * vf, eta * kp * b.SigmaBottom * Math.Min(1, fp) * vf));
            }
            double availablePassive = Integrate(valleyPressures, 0, ht, ht).Force;
            double activeDrive = Integrate(pressures, 0, ht, ht).Force + hq * fq + horizontalActions.Sum(a => a.Value * a.Factor);
            double passiveScale = availablePassive > 0 ? Math.Clamp(activeDrive / availablePassive, 0, 1) : 0;
            valleyPressures = valleyPressures.Select(p => p with { P0 = p.P0 * passiveScale, P1 = p.P1 * passiveScale }).ToList();
            foreach (var p in valleyPressures) { pressures.Add(p with { P0 = -p.P0, P1 = -p.P1 }); stemPressures.Add(p with { P0 = -p.P0, P1 = -p.P1 }); }
            var thrust = Integrate(pressures, 0, ht, ht);
            double rearHead = water ? ht - zw : 0, u0 = front * Gw * fw, u1 = rearHead * Gw * fw;
            double uplift = (u0 + u1) * width / 2, upliftMoment = width * width * (u0 + 2 * u1) / 6;
            double pointN = verticalActions.Sum(a => a.Value * a.Factor);
            double wStem = stemWeight * fc, wSlab = slabWeight * fc, wSoil = soilWeight * fs, live = (heel * q + nq) * fq + pointN;
            double backFriction = heel == 0 ? Integrate(verticalFriction, 0, ht, ht).Force : 0;
            double normal = (wStem + wSlab + wSoil + frontMass.Weight * fp + live) * vf - uplift + backFriction;
            double inertial = kh * (wStem + wSlab + wSoil + frontMass.Weight * fp + live);
            double horizontal = thrust.Force + hq * fq + horizontalActions.Sum(a => a.Value * a.Factor) + inertial;
            double stabilizing = (wStem * center.X + wSlab * width / 2 + (wSoil + heel * q * fq) * (a + s + heel / 2) + nq * fq * (a + s - top / 2) + verticalActions.Sum(a => a.Value * a.Factor * a.X)) * vf;
            double overturning = -thrust.Moment + hq * fq * ht + horizontalActions.Sum(a => a.Value * a.Factor * a.Z) + momentActions.Sum(a => a.Value * a.Factor) + upliftMoment + kh * (wStem * center.Y + wSlab * t / 2 + wSoil * soilHeight + heel * q * fq * ht + nq * fq * ht + verticalActions.Sum(a => a.Value * a.Factor * a.Z));
            stabilizing += frontMass.MomentX * fp * vf + backFriction * (a + s);
            overturning += kh * frontMass.MomentY * fp;
            // Uplift moment is included in overturning, and U is subtracted from the normal force.
            double x = normal > 0 ? (stabilizing - overturning) / normal : 0;
            double e = width / 2 - x, be = Math.Max(0, width - 2 * Math.Abs(e)); var contact = ContactLaw(width, normal, x);
            bool design = state is "SLU" or "SISMA";
            double sliding = Math.Max(0, normal) * Math.Tan(deltaBase * Rad) / (definition?.D("rslide") ?? (design ? 1.1 : 1));
            double inclination = normal > 0 ? Math.Max(0, 1 - Math.Abs(horizontal) / normal) : 0;
            bool roughBase = deltaBase + 1e-9 >= phiF / Rad / 2;
            double? bearing = state == "SISMA" || !roughBase || Math.Abs(e) > width / 3 || normal <= 0 ? null : (overburden * be * factorNq * inclination * inclination + .5 * gammaF * be * be * ng * Math.Pow(inclination, 3)) / (definition?.D("rbearing") ?? (design ? 1.4 : 1));
            Geotechnics.ShallowFoundationSeismic.Result? seismicBearing = null; string seismicBearingError = "";
            if (state == "SISMA")
            {
                if (!roughBase || !contact.Valid) seismicBearingError = "Fuori campo: base non ruvida o contatto non equilibrato";
                else seismicBearing = SeismicBearing(d, width, gammaF, phiF / Rad, normal, horizontal, e, kv, definition?.D("rbearing", 1.2) ?? 1.2, out seismicBearingError);
                bearing = seismicBearing?.Capacity;
            }
            var sections = new List<SectionForce>();
            // Stem cuts: exact trapezoid weight and lever arm at each station; water above slab included.
            var stations = Enumerable.Range(0, 21).Select(i => h * i / 20).ToList();
            foreach (var act in actions.Where(a => a.Type != ActionTypes[0])) { double zz = ht - act.Z; stations.Add(zz); if (zz > 0) stations.Add(Math.Max(0, zz - 1e-7)); }
            if (d["reinforcement"].B("two_zones")) { double zz = h - d["reinforcement"].D("lower_height"); stations.Add(zz); stations.Add(Math.Max(0, zz - 1e-7)); }
            stations.AddRange(valleyBands.SelectMany(b => new[] { ht - b.Top, ht - b.Bottom }).Where(z => z > 0 && z < h));
            foreach (double z in stations.Distinct().Order())
            {
                double thick = top + (s - top) * z / h;
                var f = Integrate(stemPressures, 0, z, z);
                double weight = gc * fc * z * (top + thick) / 2;
                double xg = z == 0 ? a + s - top / 2 : a + s - (top * top + top * thick + thick * thick) / (3 * (top + thick));
                double root = a + s - thick / 2;
                var localN = verticalActions.Where(a => ht - a.Z <= z).ToArray(); double localPointN = localN.Sum(a => a.Value * a.Factor);
                double verticalMoment = vf * (weight * (xg - root) + nq * fq * (a + s - top / 2 - root) + localN.Sum(a => a.Value * a.Factor * (a.X - root)));
                double yi = z == 0 ? 0 : z * (2 * top + thick) / (3 * (top + thick));
                double shear = f.Force + hq * fq + horizontalActions.Where(a => ht - a.Z <= z).Sum(a => a.Value * a.Factor) + kh * (weight + nq * fq + localPointN);
                double moment = -f.Moment + hq * fq * z + horizontalActions.Where(a => ht - a.Z <= z).Sum(a => a.Value * a.Factor * (z - ht + a.Z)) + momentActions.Where(a => ht - a.Z <= z).Sum(a => a.Value * a.Factor) + kh * (weight * yi + nq * fq * z + localN.Sum(a => a.Value * a.Factor * (z - ht + a.Z))) - verticalMoment;
                var wedge = FrontSoilMass(d, valleyBands, ht - z, true);
                double friction = Integrate(verticalFriction, 0, z, z).Force;
                moment -= friction * (a + s - root) + (wedge.MomentX - wedge.Weight * root) * fp * vf;
                moment += kh * fp * (wedge.MomentY - wedge.Weight * (ht - z)); shear += kh * fp * wedge.Weight;
                sections.Add(new("Fusto", z, thick, (weight + nq * fq + localPointN + wedge.Weight * fp) * vf + friction, moment, shear));
            }
            // Net upward reaction on the slab includes groundwater separately from effective soil contact.
            var upward = ContactPieces(contact); upward.Add(new(0, width, u0, u1));
            foreach (var (label, l, r, root, downward) in new[] { ("Valle", 0d, a, a, (gc * t * fc + frontColumn * fp) * vf),
                ("Monte", a + s, width, a + s, (gc * t * fc + soilIntegral.Force * fs + q * fq) * vf) })
            {
                if (r <= l) continue;
                var load = new List<PressureSegment>(upward) { new(l, r, -downward, -downward) };
                for (int i = modern ? 0 : 20; i <= 20; i++)
                {
                    double length = (r - l) * i / 20, cut = label == "Valle" ? l + length : r - length;
                    var force = label == "Valle" ? Integrate(load, l, cut, cut) : Integrate(load, cut, r, cut);
                    sections.Add(new(label, length, t, 0, label == "Valle" ? -force.Moment : force.Moment, force.Force));
                }
            }
            return new(name, state, fc, fs, fq, fw, kh, kv, horizontal, normal, uplift, stabilizing, overturning, x, e, be, contact,
                sliding, stabilizing / (definition?.D("rover") ?? (design ? 1.15 : 1)), bearing, pressures, sections) { SeismicBearing = seismicBearing, SeismicBearingError = seismicBearingError, Factors = definition is null ? null : (JsonObject)definition.DeepClone(), PressureDetails = details, Actions = actions, StemPressures = stemPressures, ValleyPressures = valleyPressures, StemPressureDetails = stemDetails,
                SoilAudit = J.Obj(("Dv_m", ValleyHeight(d)), ("Hlib_m", ht - ValleyHeight(d)), ("peso_valle_kN_m", frontMass.Weight * fp * vf), ("momento_peso_valle_kNm_m", frontMass.MomentX * fp * vf), ("q_ricoprimento_kPa", overburden), ("delta_muro_d_gradi", deltaWall), ("delta_base_d_gradi", deltaBase), ("mu_base", Math.Tan(deltaBase * Rad)), ("delta_piano_equilibrio_gradi", heel > 0 ? 0 : deltaWall), ("passiva_disponibile_kN_m", availablePassive), ("passiva_usata_kN_m", availablePassive * passiveScale), ("passiva_frazione", eta), ("passiva_limite_equilibrio", passiveScale), ("Nq", factorNq), ("Ngamma", ng), ("iq", inclination * inclination), ("igamma", Math.Pow(inclination, 3)), ("base_ruvida", roughBase)) };
        }
        var cases = new List<LoadCase>();
        if (modern) foreach (var c in definitions!.OfType<JsonObject>().Where(c => c.B("enabled"))) cases.Add(Case(c.S("name"), c.S("state"), c.D("wall"), c.D("soil"), 1, c.D("water"), c.D("kh"), c.D("kv"), c));
        else
        {
        cases.AddRange([Case("Caratteristica", "SLE", 1, 1, 1), Case("Frequente", "SLE_FREQ", 1, 1, loads.D("psi1")), Case("Quasi permanente", "SLE_QP", 1, 1, loads.D("psi2"))]);
        int index = 0;
        foreach (double fc in new[] { 1d, 1.3 }) foreach (double fs in new[] { 1d, 1.3 }) foreach (double fq in new[] { 0d, 1.5 }) foreach (double fw in water ? new[] { 1d, 1.3 } : new[] { 1d })
            cases.Add(Case("SLU " + ++index, "SLU", fc, fs, fq, fw));
        if (d["seismic"].B("enabled")) foreach (double kv in new[] { -d["seismic"].D("kv"), d["seismic"].D("kv") }.Distinct())
            cases.Add(Case(kv < 0 ? "Sisma kv−" : "Sisma kv+", "SISMA", 1, 1, loads.D("psi2"), 1, d["seismic"].D("kh"), kv));
        }
        var checks = new List<Check>();
        foreach (var c in cases.Where(c => c.State is "SLU" or "SISMA" or "ECCEZIONALE"))
        {
            if (c.Factors.S("purpose") == "Ribaltamento") { checks.Add(CheckValue("Ribaltamento", c.Name, c.Overturning, c.OverturningResistance, "kNm/m")); continue; }
            checks.Add(CheckValue("Scorrimento", c.Name, Math.Abs(c.Horizontal), c.SlidingResistance, "kN/m"));
            if (c.Factors.S("purpose") != "Generale") checks.Add(CheckValue("Ribaltamento", c.Name, c.Overturning, c.OverturningResistance, "kNm/m"));
            checks.Add(CheckValue("Capacità portante", c.Name, Math.Max(0, c.Vertical), c.BearingResistance, "kN/m", c.State == "SISMA" ? c.SeismicBearingError ?? "Portanza sismica non disponibile" : !c.SoilAudit.B("base_ruvida") ? "Fuori campo: base liscia, δd < φd/2" : "Fuori campo: e > B/3 o risultante verticale non positiva"));
            checks.Add(CheckValue("Contatto fondazione", c.Name, Math.Abs(c.Eccentricity), width / 2, "m") with { Status = c.Contact.Valid ? "Contatto in compressione" : "Perdita di equilibrio", Ratio = c.Contact.Valid ? 2 * Math.Abs(c.Eccentricity) / width : null });
        }
        var notes = new List<string> { Scope, Limits, Method, "GPC.Geometry: area e baricentro; GPC.Model e GPCChecker.Concrete: materiali, equilibrio N–M e tensioni delle sezioni armate. Pressioni geotecniche ed equilibrio del muro: ANTHEA." };
        notes.Add(SoilHelp); notes.Add(FrictionHelp);
        notes.Add("Passiva applicata al piano verticale esterno a valle e limitata alla spinta motrice; nessuna maggiorazione favorevole γG>1. Diagrammi del fusto: equilibrio della sezione con terreno sul paramento inclinato e attrito di monte. Ricoprimento q′ di valle usato con iq=(1−|H|/V)²; nessuna maggiorazione di profondità.");
        if (modern) { notes.Add(ApproachHelp); notes.Add("Azioni indipendenti con ψ propri e gruppi correlati. Matrice " + d.S("combination_mode") + ": fanno fede i coefficienti salvati in ogni riga. Urto: forza statica equivalente assegnata per metro; eventi eccezionali separati, ψ₂ delle variabili e γR=1 nel preset. Resistenze del materiale conservate cautelativamente ai valori ordinari."); }
        if (wood) notes.Add(SeismicHelp);
        if (water) notes.Add("Sottospinta lineare fra i battenti a valle e a monte, senza riduzioni da drenaggi. Peso saturo totale sopra la mensola; pressione efficace nel terreno. Per la portanza si adotta cautelativamente γ′ su tutta la zona di rottura.");
        if (d["seismic"].B("enabled"))
        {
            notes.Add("Pseudostatica locale " + (wood ? "Wood semplificato" : "Mononobe–Okabe") + ": incremento dinamico a metà altezza, inerzia di muro e terreno sulla mensola, ±kv. Direzione verso valle. Portanza sismica Annex F con accelerazione del terreno indipendente dal kh ridotto del muro: interazione N–V–M, γRD e γR espliciti; ricoprimento favorevole escluso. Spostamenti Newmark e stabilità globale Bishop richiedono l’attivazione dei rispettivi calcoli separati.");
            if (DeriveSeismic(d) is { } derivation) notes.Add(derivation.Description + " Casi generali e ribaltamento separati; γR del preset SLV: scorrimento 1, ribaltamento 1, portanza 1,2. Matrici personalizzate: fanno fede kh, kv e coefficienti di ogni riga; l’accelerazione della fondazione è definita separatamente.");
            else notes.Add("kh e kv assegnati: il preset manuale mantiene γR statici e non maggiora kh per il ribaltamento. Il progettista deve predisporre le combinazioni appropriate o usare Da parametri del sito (SLV).");
        }
        if (d.Array("layers").Count > 1) notes.Add("Strati: Rankine locale con σ′v integrata; discontinuità di pressione alle interfacce. Non è una ricerca del cuneo di rottura multistrato.");
        if (cases.Any(c => !c.Contact.Valid)) notes.Add("Perdita di equilibrio in almeno una combinazione: le verifiche strutturali che dipendono dalle reazioni di fondazione non sono disponibili.");
        if (cases.Any(c => c.State != "SISMA" && c.BearingResistance is null)) notes.Add("Portanza fuori campo in almeno una combinazione: non sostituire questo stato con un esito favorevole.");
        var structural = StructuralChecks(d, cases.Where(c => c.Factors.S("purpose") != "Ribaltamento").ToList(), token, out double steel);
        var service = CalculateService(d, cases, checks, token);
        var detailing = d.S("family") == "cantilever" ? CalculateReinforcementDetails(d) : null;
        if (d["detailing"].B("enabled") && detailing is not null) { structural.AddRange(detailing.Checks); steel = detailing.SteelKg; }
        double area = SectionGeometry.Area(Outline(d));
        Geotechnics.SlopeResult? global = null; string? globalError = null;
        if (d["global_stability"].B("enabled"))
        {
            try { global = CalculateGlobal(d, token); checks.AddRange(GlobalChecks(global)); }
            catch (ArgumentException ex) { globalError = ex.Message; checks.Add(new("Stabilità globale · Bishop", "Globale", 0, null, "−", null, "Dati da correggere: " + ex.Message)); }
        }
        return new(d, width, area, area, steel, cases, checks, structural, notes) { GlobalStability = global, GlobalError = globalError, Serviceability = service, Detailing = detailing };
    }
    public static Check CheckValue(string name, string combo, double demand, double? resistance, string unit, string unavailable = "Non disponibile")
    {
        double? ratio = resistance is > 0 ? Math.Abs(demand) / resistance : null;
        if (resistance == 0) return new(name, combo, Math.Abs(demand), 0, unit, demand == 0 ? 0 : null, demand == 0 ? "Soddisfatta" : "Non soddisfatta: resistenza nulla");
        return new(name, combo, Math.Abs(demand), resistance, unit, ratio, ratio is null ? unavailable : ratio <= 1 ? "Soddisfatta" : "Non soddisfatta");
    }
}
