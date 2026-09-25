using System.Text.Json.Nodes;
using GPC.Checkers.Steel.CompositeBridges;

namespace X.Core;

public sealed record BridgeDetailValue(string Name, double Value, string Unit);

public static partial class BridgeSection
{
    public static readonly string[] StiffenerSides = ["Bilaterali simmetrici", "Solo sinistra", "Solo destra", "Bilaterali diversi"];
    public static readonly string[] SupportLocations = ["Appoggio interno", "Estremità sinistra", "Estremità destra"];
    public static JsonObject DetailDefaults() => J.Obj(
        ("lati_irr", StiffenerSides[0]), ("pannelli_uguali", true), ("a_irr_dx", 3000),
        ("b_irr_dx", 150), ("t_irr_dx", 15), ("x_irr", 0), ("betaL_irr", 1), ("saldature_irr", false), ("aw_irr", 6),
        ("appoggio", false), ("pos_app", SupportLocations[0]), ("lati_app", StiffenerSides[0]),
        ("b_app", 180), ("t_app", 25), ("b_app_dx", 180), ("t_app_dx", 25),
        ("a_app_sx", 3000), ("a_app_dx", 3000), ("c_app", 150), ("R_app", ""), ("x_app", 0), ("z_app", 0),
        ("betaL_app", 1), ("s_app", 300), ("B_app", 400), ("terminale_rigido", false), ("e_term", 300),
        ("saldature_app", true), ("aw_app", 8), ("gamma_m2", 1.25),
        ("armatura_trasv", false), ("d_trasv_sup", 16), ("s_trasv_sup", 150), ("d_trasv_inf", 16), ("s_trasv_inf", 150),
        ("cot_trasv", 1), ("quota_q_sx", .5), ("As_m_trasv", 0), ("l_anc_trasv", 700), ("buona_aderenza_trasv", true),
        ("bordo_cls_sx", 500), ("bordo_cls_dx", 500), ("forcine_bordo", false), ("d_forcine", 12),
        ("fatica_pioli", false), ("q_fat_min", ""), ("q_fat_max", ""), ("lambda_v", 1), ("phi_fat", 1),
        ("flangia_fat_tesa", false), ("dsigma_fat", ""), ("gamma_ff", 1), ("gamma_mf_pioli", 1.25), ("gamma_mf_flangia", 1.35));

    public static (double LeftWidth, double LeftThickness, double RightWidth, double RightThickness) StiffenerPlates(JsonObject d, string suffix)
    {
        string mode = d.S("lati_" + suffix, StiffenerSides[0]);
        if (!StiffenerSides.Contains(mode)) throw new ArgumentException("Disposizione irrigidimenti non riconosciuta.");
        double b = d.Required("b_" + suffix, strict: true), t = d.Required("t_" + suffix, strict: true);
        return (mode == StiffenerSides[2] ? 0 : b, mode == StiffenerSides[2] ? 0 : t,
            mode == StiffenerSides[1] ? 0 : mode == StiffenerSides[3] ? d.Required("b_" + suffix + "_dx", strict: true) : b,
            mode == StiffenerSides[1] ? 0 : mode == StiffenerSides[3] ? d.Required("t_" + suffix + "_dx", strict: true) : t);
    }
    private static WebShearResistance CheckStiffenerDetails(JsonObject d, BridgeGeometry g, BridgeMaterialValues m,
        double v, double webCompression, List<BridgeLocalCheck> checks, List<BridgeDetailValue> details, List<string> warnings,
        out bool usesIntermediate, out bool usesRigidEnd)
    {
        double gm0 = d.Required("gamma_m0", strict: true), gm1 = d.Required("gamma_m1", strict: true), eta = d.Required("eta_taglio", strict: true);
        double span = g.WebHeight + (g.TopThickness + g.BottomEquivalentThickness) / 2;
        var web = BridgeShearConnection.Web(g.WebHeight, g.WebThickness, m.Fy, m.Ea, gm0, gm1, eta);
        bool ultimate = d.S("stato") == "SLU";
        usesIntermediate = false; usesRigidEnd = false;
        bool Element(string suffix, string name, double aL, double aR, double availL, double availR, double extra,
            double x, double z, bool stiffness, out StiffenerDetail result)
        {
            var p = StiffenerPlates(d, suffix);
            var candidate = BridgeShearConnection.Web(g.WebHeight, g.WebThickness, m.Fy, m.Ea, gm0, gm1, eta, Math.Max(aL, aR));
            double nst = Math.Max(0, Math.Abs(v) * 1000 - candidate.TauCritical * g.WebHeight * g.WebThickness / gm1) + extra;
            result = BridgeLocalDetails.Stiffener(g.WebHeight, g.WebThickness, span, aL, aR, availL, availR,
                p.LeftWidth, p.LeftThickness, p.RightWidth, p.RightThickness, m.Fy, m.Ea, gm1, nst, webCompression,
                nst > 0 ? extra * x / nst : 0, nst > 0 ? extra * z / nst : 0, d.Required("betaL_" + suffix, strict: true));
            var st = result; int start = checks.Count;
            void Check(string label, double demand, double resistance, string unit, double? ratio, string note = "") =>
                checks.Add(new(name + " · " + label, demand, resistance, unit, ultimate ? ratio : null, ultimate ? note : "Esito richiede azioni SLU; " + note));
            if (stiffness) Check("rigidezza pannelli", st.RigidityRatio, 1, "—", st.RigidityRatio, "EN 1993-1-5 §9.3.3; controllo di entrambi i pannelli");
            Check("stabilità e pressoflessione II ordine", st.Stress, m.Fy / gm1, "MPa", st.StressRatio,
                "EN 1993-1-1 §§5.2.2(7)a, 5.3.4: imperfezione equivalente Lcr/200, eccentricità effettive, due piani; nessuna riserva plastica");
            Check("freccia fuori piano", st.Deflection, span / 300, "mm", st.DeflectionRatio, "EN 1993-1-5 §9.2.1");
            Check("rigidezza torsionale", st.TorsionRatio, 1, "—", st.TorsionRatio);
            Check("piatti entro classe 3", st.LocalRatio, 1, "—", st.LocalRatio, "Sbalzi uniformemente compressi c/t ≤ 14ε; nessuna riduzione automatica dei piatti snelli");
            double available = (Math.Min(g.TopWidth, g.Bottom1Width) - g.WebThickness) / 2;
            Check("ingombro tra piattabande", Math.Max(p.LeftWidth, p.RightWidth), available, "mm", Math.Max(p.LeftWidth, p.RightWidth) / available);
            details.AddRange(new[] { new BridgeDetailValue(name + " · A con anima", st.Area, "mm²"), new(name + " · striscia anima", st.WebStrip, "mm"),
                new(name + " · xG", st.CentroidX, "mm"), new(name + " · eccentricità x−xG", st.Eccentricity, "mm"),
                new(name + " · I fuori piano", st.InertiaOut, "mm⁴"), new(name + " · I nel piano", st.InertiaIn, "mm⁴"),
                new(name + " · Nst", st.Compression / 1000, "kN"), new(name + " · Ncr fuori piano", st.CriticalOut / 1000, "kN"),
                new(name + " · M II ordine fuori piano", st.MomentOut / 1e6, "kNm"), new(name + " · M II ordine nel piano", st.MomentIn / 1e6, "kNm") });
            if (d.B("saldature_" + suffix))
            {
                double throat = d.Required("aw_" + suffix, strict: true);
                // betaW=1 covers all offered grades; fu is the parent steel from Model.
                double fw = BridgeLocalDetails.WeldStrength(Materials(d).Steel.Fu, 1, d.Required("gamma_m2", strict: true));
                double length = g.WebHeight - 2 * throat;
                fw *= Math.Clamp(1.2 - .2 * length / (150 * throat), .6, 1); // conservative long-joint reduction
                double direct = nst / (2 * st.PlateCount * throat * Math.Max(1, length));
                double bending = 6 * (st.MomentOut + st.MomentIn) / (2 * st.PlateCount * throat * Math.Pow(Math.Max(1, length), 2));
                Check("saldature verticali all’anima", direct + bending, fw, "MPa", (direct + bending) / fw,
                    "Cordoni continui su entrambi i bordi di ogni piatto; risultante cautelativa, EN 1993-1-8 §4.5.3.3; βw=1");
                // Transfer the complete extreme normal stress of each plate through both end fillets.
                double endStress = st.Stress * st.MaxThickness / (2 * throat);
                Check("saldature alle flange", endStress, fw, "MPa", st.Stable ? endStress / fw : null);
                Check("gola minima", 3, throat, "mm", 3 / throat);
                double shortest = new[] { p.LeftWidth, p.RightWidth }.Where(b => b > 0).Min() - 2 * throat;
                Check("lunghezza efficace cordone", Math.Max(30, 6 * throat), shortest, "mm", shortest > 0 ? Math.Max(30, 6 * throat) / shortest : null);
            }
            else checks.Add(new(name + " · collegamenti", 0, 0, "—", null, "Saldature non verificate: il beneficio geometrico richiede un collegamento continuo idoneo"));
            bool memberOk = st.Stable && st.StressRatio <= 1 && st.DeflectionRatio <= 1 && st.LocalRatio <= 1 && st.TorsionRatio <= 1
                && (!stiffness || st.RigidityRatio <= 1) && Math.Max(p.LeftWidth, p.RightWidth) <= available;
            if (d.B("saldature_" + suffix)) memberOk &= checks.Skip(start).Where(c => c.Name.Contains("saldatur") || c.Name.Contains("gola") || c.Name.Contains("cordone")).All(c => c.Ratio <= 1);
            return memberOk;
        }
        if (d.B("irrigidimenti"))
        {
            double aL = d.Required("a_irr", strict: true), aR = d.B("pannelli_uguali") ? aL : d.Required("a_irr_dx", strict: true);
            usesIntermediate = Element("irr", "Intermedio", aL, aR, aL / 2, aR / 2, d.Required("N_irr") * 1000,
                d.Required("x_irr", double.NegativeInfinity), 0, true, out _);
            if (usesIntermediate) web = BridgeShearConnection.Web(g.WebHeight, g.WebThickness, m.Fy, m.Ea, gm0, gm1, eta, Math.Max(aL, aR));
            else warnings.Add("Irrigidimento intermedio non idoneo: escluso il beneficio dei pannelli a taglio.");
        }
        if (d.B("appoggio"))
        {
            string location = d.S("pos_app");
            if (!SupportLocations.Contains(location)) throw new ArgumentException("Posizione appoggio non riconosciuta.");
            bool end = location != SupportLocations[0];
            double aL = location == SupportLocations[1] ? d.Required("a_app_dx", strict: true) : d.Required("a_app_sx", strict: true);
            double aR = location == SupportLocations[2] ? aL : d.Required("a_app_dx", strict: true);
            double c = end ? d.Required("c_app") : 0, reaction = d.Required("R_app") * 1000;
            double left = location == SupportLocations[1] ? c : aL / 2, right = location == SupportLocations[2] ? c : aR / 2;
            if (end && d.B("terminale_rigido"))
            {
                double halfE = d.Required("e_term", strict: true) / 2;
                if (location == SupportLocations[1]) right = Math.Min(right, halfE); else left = Math.Min(left, halfE);
            }
            double x = d.Required("x_app", double.NegativeInfinity), z = d.Required("z_app", double.NegativeInfinity);
            double panel = location == SupportLocations[1] ? aR : location == SupportLocations[2] ? aL : Math.Max(aL, aR);
            bool member = Element("app", "Appoggio", panel, panel, left, right, reaction, x, z, false, out var st);
            double width = d.Required("B_app", strict: true), length = d.Required("s_app", strict: true);
            var p = StiffenerPlates(d, "app");
            double footprint = 2 * Math.Max(Math.Abs(-g.WebThickness / 2 - p.LeftWidth - x), Math.Abs(g.WebThickness / 2 + p.RightWidth - x));
            void Bearing(string name, double demand, double resistance) => checks.Add(new("Appoggio · " + name, demand, resistance, "mm", ultimate ? demand / resistance : null));
            Bearing("impronta trasversale sotto i piatti", footprint, width);
            Bearing("impronta longitudinale", st.MaxThickness + 2 * Math.Abs(z), length);
            if (end) Bearing("impronta entro l’estremità", length / 2 + Math.Abs(z), Math.Max(c, 1e-12));
            double bearingStress = st.Compression / st.PlateArea + Math.Max(0, st.Stress - st.Compression / st.Area);
            checks.Add(new("Appoggio · contatto piatti/flangia", bearingStress, m.Fy / gm0, "MPa", ultimate && st.Stable ? bearingStress / (m.Fy / gm0) : null,
                "Piatti a contatto con la flangia inferiore; impronta interamente sottostante. Non comprende piastra di ripartizione o apparecchio d’appoggio."));
            member &= footprint <= width && st.MaxThickness + 2 * Math.Abs(z) <= length && bearingStress <= m.Fy / gm0
                && (!end || length / 2 + Math.Abs(z) <= c);
            if (end && d.B("terminale_rigido"))
            {
                double e = d.Required("e_term", strict: true), requiredArea = 4 * g.WebHeight * g.WebThickness * g.WebThickness / e;
                bool symmetric = d.S("lati_app") == StiffenerSides[0];
                checks.Add(new("Terminale · due coppie bilaterali simmetriche", symmetric ? 1 : 0, 1, "—", symmetric ? 0 : 2, "Seconda coppia identica verso l’interno, distanza e; EN 1993-1-5 §9.3.1"));
                checks.Add(new("Terminale · area di ciascuna coppia", requiredArea, st.PlateArea, "mm²", ultimate ? requiredArea / st.PlateArea : null, "A ≥ 4 hw tw²/e"));
                checks.Add(new("Terminale · distanza e > 0,1 hw", .1 * g.WebHeight, e, "mm", e > .1 * g.WebHeight ? .1 * g.WebHeight / e : 2));
                checks.Add(new("Terminale · e entro il pannello", e, panel, "mm", e < panel ? e / panel : 2));
                bool second = Element("app", "Seconda coppia terminale", e, panel, e / 2, panel / 2, reaction, x, 0, false, out var secondSt);
                double? combined = st.StressRatio is {} sr && secondSt.StressRatio is {} sr2 ? requiredArea / st.PlateArea + Math.Max(sr, sr2) : null;
                checks.Add(new("Terminale · reazione e ancoraggio combinati", combined ?? 0, 1, "—", ultimate ? combined : null,
                    "Inviluppo cautelativo: utilizzo elastico della coppia + area richiesta dal §9.3.1 / area disponibile; R intera su ciascuna coppia"));
                usesRigidEnd = member && second && symmetric && combined <= 1 && e > .1 * g.WebHeight && e < panel && d.B("saldature_app");
                if (usesRigidEnd)
                {
                    // No credit for a shorter main panel unless intermediate supports are independently suitable.
                    double adoptedPanel = usesIntermediate ? Math.Max(d.D("a_irr"), d.B("pannelli_uguali") ? d.D("a_irr") : d.D("a_irr_dx")) : 0;
                    web = BridgeShearConnection.Web(g.WebHeight, g.WebThickness, m.Fy, m.Ea, gm0, gm1, eta, adoptedPanel, true);
                }
                else warnings.Add("Montante terminale non qualificato come rigido: resta adottata la curva non rigida a taglio.");
            }
            warnings.Add("Appoggio: R è l’inviluppo SLU assegnato, indipendente dalle fasi. Piatti continui senza intagli, vincolati lateralmente alle flange. Apparecchio d’appoggio e flessione della piastra di ripartizione esclusi.");
        }
        return web;
    }

    private static void CheckSlabAndFatigue(JsonObject d, BridgeGeometry g, BridgeMaterialValues m, double flow,
        List<BridgeLocalCheck> checks, List<BridgeDetailValue> details, List<string> warnings)
    {
        bool ultimate = d.S("stato") == "SLU";
        double fyd = m.Fys / d.Required("gamma_s", strict: true), fcd = d.Required("alpha_cc", strict: true) * m.Fck / d.Required("gamma_c", strict: true);
        if (d.B("armatura_trasv"))
        {
            double Bar(string side)
            { double dia = d.Required("d_trasv_" + side); return dia == 0 ? 0 : Math.PI * dia * dia / 4 / d.Required("s_trasv_" + side, strict: true); }
            double at = Bar("sup"), ab = Bar("inf"), fraction = d.Required("quota_q_sx"), cot = d.Required("cot_trasv", strict: true);
            if (fraction > 1 || cot < 1 || cot > 1.25) throw new ArgumentException("Soletta: quota q sinistra tra 0 e 1; cot θ tra 1 e 1,25 (valido anche in trazione).");
            double bending = d.Required("As_m_trasv") / 1000;
            void Surface(string name, double q, double length, double reinforcement)
            {
                var r = BridgeLocalDetails.SlabSurface(q, length, reinforcement, m.Fck, fcd, m.Fys, fyd, cot, bending);
                checks.Add(new(name + " · armatura trasversale", r.RequiredSteel * 1000, reinforcement * 1000, "mm²/m", ultimate ? r.SteelRatio : null,
                    "EN 1994-2 §6.6.6 / EN 1992-1-1 §6.2.4; inclusi minimo e combinazione con flessione trasversale"));
                checks.Add(new(name + " · puntone CLS", q, r.StrutResistance, "kN/m", ultimate ? r.StrutRatio : null));
                details.AddRange(new[] { new BridgeDetailValue(name + " · superficie", length, "mm"), new(name + " · qEd", q, "kN/m"), new(name + " · As minimo", r.MinimumSteel * 1000, "mm²/m") });
            }
            Surface("Soletta a–a sinistra", Math.Abs(flow) * fraction, g.SlabHeight, at + ab);
            Surface("Soletta a–a destra", Math.Abs(flow) * (1 - fraction), g.SlabHeight, at + ab);
            int studs = (int)d.D("n_pioli");
            for (int n = 1; n <= studs; n++) Surface($"Soletta b–b · gruppo {n}", Math.Abs(flow) * n / studs,
                2 * d.D("h_pioli") + (n - 1) * d.D("passo_trasv_pioli") + d.D("d_testa_pioli"), 2 * ab);
            double fctm = m.Fck <= 50 ? .3 * Math.Pow(m.Fck, 2d / 3) : 2.12 * Math.Log(1 + (m.Fck + 8) / 10);
            foreach (string side in new[] { "sup", "inf" })
            {
                double diameter = d.D("d_trasv_" + side); if (diameter == 0) continue;
                double required = BridgeLocalDetails.AnchorageLength(diameter, fyd, .7 * fctm, d.D("gamma_c"), d.B("buona_aderenza_trasv"), d.S("normativa").StartsWith("NTC"));
                double provided = d.Required("l_anc_trasv", strict: true);
                checks.Add(new("Ancoraggio trasversale " + side, required, provided, "mm", required / provided,
                    "EN 1992-1-1 §8.4, minimo aggiuntivo NTC 20Ø/150 mm quando selezionate; barra a fyd, α1…α5=1; lunghezza disponibile minima oltre ogni superficie, entrambi i lati"));
            }
            double edge = Math.Min(d.Required("bordo_cls_sx", strict: true), d.Required("bordo_cls_dx", strict: true)), dia = d.D("d_pioli");
            if (edge < 300)
            {
                checks.Add(new("Splitting al bordo · distanza piolo", 6 * dia, edge, "mm", 6 * dia / edge));
                checks.Add(new("Splitting al bordo · forcine a U", d.B("forcine_bordo") ? 1 : 0, 1, "—", d.B("forcine_bordo") ? 0 : 2,
                    "EN 1994-2 §6.6.5.3; forcine attorno ai pioli, in basso e ancorate oltre la superficie"));
                if (d.B("forcine_bordo")) checks.Add(new("Splitting al bordo · diametro forcine", .5 * dia, d.Required("d_forcine", strict: true), "mm", .5 * dia / d.D("d_forcine")));
            }
        }
        else checks.Add(new("Soletta · taglio longitudinale e splitting", 0, 0, "—", null, "Attivare l’armatura trasversale dedicata; le barre longitudinali non sono conteggiate"));
        if (d.B("fatica_pioli"))
        {
            double qmin = d.Required("q_fat_min", double.NegativeInfinity), qmax = d.Required("q_fat_max", double.NegativeInfinity);
            if (qmax < qmin) throw new ArgumentException("Fatica: q massimo deve essere ≥ q minimo.");
            double range = (qmax - qmin) * d.Required("lambda_v", strict: true) * d.Required("phi_fat", strict: true);
            double dsigma = d.B("flangia_fat_tesa") ? d.Required("dsigma_fat") : 0;
            var r = BridgeLocalDetails.StudFatigue(range, d.D("passo_pioli"), (int)d.D("n_pioli"), d.D("d_pioli"), dsigma,
                d.B("flangia_fat_tesa"), d.Required("gamma_ff", strict: true), d.Required("gamma_mf_pioli", strict: true), d.Required("gamma_mf_flangia", strict: true));
            checks.Add(new("Fatica pioli · ΔτE,2", r.StressRange * d.D("gamma_ff"), 90 / d.D("gamma_mf_pioli"), "MPa", r.StudRatio, "EN 1994-2 §§6.8.3, 6.8.7.2; inviluppo a 2 milioni di cicli indipendente dalle fasi"));
            if (d.B("flangia_fat_tesa"))
            {
                checks.Add(new("Fatica flangia · categoria 80", dsigma * d.D("gamma_ff"), 80 / d.D("gamma_mf_flangia"), "MPa", r.FlangeRatio));
                checks.Add(new("Fatica · interazione flangia/piolo", r.StudRatio + r.FlangeRatio, 1.3, "—", r.Interaction, "Intervalli assegnati come inviluppo dei casi fessurati/non fessurati e delle coppie concomitanti"));
            }
            details.Add(new("Fatica · ΔqE,2 = λv φ Δq", range, "kN/m"));
        }
        else checks.Add(new("Pioli · resistenza a fatica", 0, 0, "—", null, "Non attivata: i dettagli per azioni ripetute non sostituiscono la verifica a fatica"));
        warnings.Add("Connessione: soletta piena, pioli verticali standard. I controlli di armatura assumono barre effettivamente ancorate e superfici a–a/b–b. Sollevamento imposto, torsione e splitting attraverso lo spessore richiedono un modello dedicato.");
    }
}
