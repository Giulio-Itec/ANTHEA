using System.Text.Json.Nodes;

namespace Anthea.Calculations;

/// <summary>Normal-weight concrete section checks. Forces kN/kNm, dimensions mm, stresses MPa.
/// Compression is negative, consistently with Checker. No favourable near-support reduction.</summary>
public static class ConcreteCodeChecks
{
    public static double EffectiveCrackDepth(string standard, SezioneCA section, double qx, double qy,
        double top, double height, double coverToCenter, double tensileDepth, bool entirelyTensile)
    {
        double hc = Math.Min(2.5*coverToCenter, Math.Min(entirelyTensile?height: tensileDepth/3, height/2));
        if (standard.StartsWith("DIN"))
        {
            double coefficient=Math.Clamp(2+.1*height/coverToCenter,2.5,5);
            hc=Math.Min(coefficient*coverToCenter,height/2);
            double cover=section.Input.D("cover_mm")+(section.Input.S("staffe_presenti","Sì")=="Sì"?section.Input.D("transverse_bar_diameter_mm"):0);
            if(!entirelyTensile&&tensileDepth/3>=cover+20)hc=Math.Min(hc,tensileDepth/3);
        }
        if (standard.StartsWith("DS"))
        {
            // DK NA Fig.7.100: largest concrete band with centroid at the tensile reinforcement.
            // Polygon area and centroid come from GPC.Geometry, including subtraction of cavities.
            double target=top-coverToCenter,lo=0,hi=entirelyTensile?height/2:Math.Min(height,tensileDepth);
            for(int i=0;i<65;i++)
            {
                double h=(lo+hi)/2,area=0,moment=0;
                void Accumulate(IReadOnlyList<double[]> points,double sign)
                {
                    var clip=SectionRegions.Clip(points,qx,qy,top-h);if(clip.Length<3)return;
                    var poly=SectionGeometry.Polygon(clip);double a=Math.Abs(poly.GetSignedArea());if(a<1e-12)return;
                    var c=poly.GetCentroid();area+=sign*a;moment+=sign*a*(qx*c.X+qy*c.Y);
                }
                Accumulate(section.Outline,1);foreach(var hole in section.Holes)Accumulate(hole,-1);
                if(area<=1e-12||moment/area>target)lo=h;else hi=h;
            }
            hc=(lo+hi)/2;
        }
        return hc;
    }
    public static bool IsEurocode(string name) => name is "EN 1992-1-1" or "UNI EN 1992-1-1" or "DIN EN 1992-1-1" or "DS EN 1992-1-1" or "NS EN 1992-1-1";
    public static void RequireOrdinary(string name)
    {
        if (name != "NTC 2018" && name != "Model Code 2010" && !IsEurocode(name))
            throw new ArgumentException(name + ": modello FRC/pavimentazioni escluso; scegliere una norma per calcestruzzo ordinario.");
    }

    public sealed record ShearInput(string Standard, double N, double V, double M, double Area, double Bw,
        double D, double Asl, double Fck, double Fcd, double Fyd, double GammaC, double Es,
        double Asw, double Spacing, double Alpha = 90, double? CotTheta = null, double LeverFactor = .9,
        double Aggregate = 20, double AxialEccentricity = 0);

    public static Ntc2018Checks.ShearResult Shear(ShearInput p)
    {
        RequireOrdinary(p.Standard);
        if (new[] { p.Area, p.Bw, p.D, p.Fck, p.Fcd, p.Fyd, p.GammaC, p.Es, p.Spacing }.Any(v => !double.IsFinite(v) || v <= 0)
            || new[] { p.N, p.V, p.M, p.AxialEccentricity }.Any(v => !double.IsFinite(v))
            || new[] { p.Asl, p.Asw, p.Aggregate }.Any(v => !double.IsFinite(v) || v < 0)
            || !double.IsFinite(p.Alpha) || p.Alpha < 45 || p.Alpha > 90 || !double.IsFinite(p.LeverFactor) || p.LeverFactor <= 0 || p.LeverFactor > .9)
            throw new ArgumentException("Taglio: geometria, materiali, carichi o inclinazione delle staffe non validi.");
        if (p.Fck > 90) throw new ArgumentException("Taglio: calcestruzzo oltre C90/105 fuori dal campo implementato.");
        if (p.Standard.StartsWith("NS") && p.Fck > 60) p = p with { Fck = 60, Fcd = p.Fcd * 60 / p.Fck };
        if (p.Standard == "NTC 2018") return Ntc2018Checks.Shear(p.N, p.V, p.Area, p.Bw, p.D, p.Asl, p.Fck, p.Fcd, p.Fyd, p.GammaC, p.Asw, p.Spacing, p.Alpha, p.CotTheta, p.LeverFactor)
            with { Reference = "NTC 2018 §§4.1.2.3.5.1–2", Model = "NTC · traliccio a inclinazione variabile" };

        double z = p.LeverFactor * p.D, sigma = -p.N * 1000 / p.Area;
        var details = new List<CrackCalculationDetail>();
        void Add(string key, double value, string unit, string formula) => details.Add(new(key, value, unit, formula));
        Add("z", z, "mm", "z/d · d"); Add("σcp", sigma, "MPa", "−N / Ac; compressione positiva");
        bool mc = p.Standard == "Model Code 2010";
        double epsilon = 0;
        if (mc)
        {
            if (p.Asl <= 0) throw new ArgumentException("Model Code: occorre Asl efficace anche in presenza di staffe.");
            epsilon = Math.Max(0, (Math.Abs(p.M) * 1e6 / z + Math.Abs(p.V) * 1000 + p.N * 1000 * (.5 + p.AxialEccentricity / z)) / (2 * p.Es * p.Asl));
            Add("εx", epsilon, "−", "max[0; (|M|/z + |V| + N(1/2 + Δe/z))/(2 Es Asl)]");
        }
        string reference = mc ? "fib MC2010 · livello II · §§7.3.3–7.3.4" : p.Standard + " · §§6.2.2–6.2.3";
        Ntc2018Checks.ShearResult Result(double rs, double rc, double rd, double cot) => new(rs, rc, rd,
            rd > 0 ? Math.Abs(p.V) / rd : p.V == 0 ? 0 : null, cot,
            rd <= 0 ? "Resistenza nulla" : Math.Abs(p.V) <= rd ? "Resistenza sufficiente · dettagli da verificare" : "Resistenza insufficiente")
            { Reference = reference, Model = mc ? "Model Code 2010 · livello II" : "Eurocodice 2 · prima generazione", Details = details.ToArray() };
        if (p.Asw == 0)
        {
            if (mc)
            {
                double dg = p.Fck > 70 ? 0 : p.Aggregate, kd = Math.Max(.75, 32 / (16 + dg));
                double kv = .4 / (1 + 1500 * epsilon) * 1300 / (1000 + kd * z);
                double resistance = kv * Math.Min(8, Math.Sqrt(p.Fck)) * z * p.Bw / p.GammaC / 1000;
                Add("dg efficace", dg, "mm", "0 per fck > 70 MPa"); Add("kdg", kd, "−", "max[0,75; 32/(16+dg)]");
                Add("kv", kv, "−", "0,4/(1+1500 εx) · 1300/(1000+kdg z)");
                return Result(0, resistance, resistance, 0);
            }
            double k = Math.Min(2, 1 + Math.Sqrt(200 / p.D)), rho = Math.Min(.02, p.Asl / (p.Bw * p.D));
            double cr = .18 / p.GammaC, k1 = .15, vmin = .035 * Math.Pow(k, 1.5) * Math.Sqrt(p.Fck);
            if (p.Standard.StartsWith("NS")) { cr = (p.Aggregate < 16 ? .15 : .18) / p.GammaC; k1 = sigma < 0 ? .3 : .15; }
            if (p.Standard.StartsWith("DIN")) { cr = .15 / p.GammaC; k1 = .12; vmin = (p.D <= 600 ? .0525 : p.D >= 800 ? .0375 : .0525 - (p.D - 600) * .015 / 200) / p.GammaC * Math.Pow(k, 1.5) * Math.Sqrt(p.Fck); }
            if (p.Standard.StartsWith("DS")) vmin = .051 / p.GammaC * Math.Pow(k, 1.5) * Math.Sqrt(p.Fck);
            double sc = Math.Min(sigma, .2 * p.Fcd);
            double empirical = (cr * k * Math.Cbrt(100 * rho * p.Fck) + k1 * sc) * p.Bw * p.D / 1000;
            double floor = (vmin + k1 * sc) * p.Bw * p.D / 1000;
            Add("k", k, "−", "min[2;1+√(200/d)]"); Add("ρl", rho, "−", "min[0,02;Asl/(bw d)]");
            Add("CRd,c", cr, "−", "Coefficiente della norma"); Add("k1", k1, "−", "Coefficiente di σcp"); Add("vmin", vmin, "MPa", "Minimo della norma");
            return Result(empirical, floor, Math.Max(0, Math.Max(empirical, floor)), 0);
        }
        double alpha = p.Alpha * Math.PI / 180, ca = 1 / Math.Tan(alpha);
        double minCot = 1, maxCot = mc ? 1 / Math.Tan(20 * Math.PI / 180) : 2.5;
        if (p.Standard.StartsWith("NS") && -sigma >= .7 * (p.Fck <= 50 ? .3 * Math.Pow(p.Fck, 2d/3) : 2.12 * Math.Log(1 + (p.Fck + 8)/10))) maxCot = 1.25;
        double acw = 1; // EC2 6.2.3: recommended value for non-prestressed structures.
        double nu = p.Standard.StartsWith("DS") ? Math.Max(.45, .7 - p.Fck / 200) : .6 * (1 - p.Fck / 250);
        if (p.Standard.StartsWith("UNI"))
        {
            nu = p.Fck <= 70 ? .5 : .6 * (1 - p.Fck / 250);
            // DM 31/07/2012 §6.2.3(3): alpha_cw = 1 for non-prestressed structures.
        }
        if (p.Standard.StartsWith("DIN"))
        {
            nu = .75 * Math.Min(1, 1.1 - p.Fck / 500);
            double vcc = .24 * Math.Cbrt(p.Fck) * Math.Max(0, 1 - 1.2 * sigma / p.Fcd) * p.Bw * z / 1000;
            maxCot = Math.Abs(p.V) <= vcc ? 3 : Math.Min(3, (1.2 + 1.4 * sigma / p.Fcd) / (1 - vcc / Math.Abs(p.V)));
            if (maxCot < 1) throw new ArgumentException("DIN: inclinazione del puntone fuori campo con questa trazione assiale.");
            Add("VRd,cc", vcc, "kN", "0,24 fck^(1/3) (1−1,2 σcp/fcd) bw z");
        }
        if (p.Standard.StartsWith("DS")) { minCot = Math.Tan(alpha / 2); maxCot = 2; }
        Add("cot θ massimo", maxCot, "−", p.Standard.StartsWith("DS") ? "Limite cautelativo 2, anche con armatura interrotta; acciaio B/C" : "Limite della norma");
        (double Steel, double Concrete) Resistance(double cot)
        {
            double rs = z * p.Asw / p.Spacing * p.Fyd * (cot + ca) * Math.Sin(alpha) / 1000;
            double strength = acw * nu * p.Fcd;
            if (mc)
            {
                double e1 = epsilon + (epsilon + .002) * cot * cot;
                strength = Math.Min(.65, 1 / (1.2 + 55 * e1)) * Math.Min(1, Math.Cbrt(30 / p.Fck)) * p.Fck / p.GammaC;
            }
            return (rs, z * p.Bw * strength * (cot + ca) / (1 + cot * cot) / 1000);
        }
        double cot = p.CotTheta ?? minCot;
        if (p.CotTheta is null)
        {
            // Bounded scalar maximisation also covers inclined stirrups and MC strain-dependent struts.
            double lo = minCot, hi = maxCot;
            for (int i = 0; i < 80; i++)
            {
                double a = lo + (hi-lo)/3, b = hi - (hi-lo)/3;
                var ra = Resistance(a); var rb = Resistance(b);
                if (Math.Min(ra.Steel, ra.Concrete) < Math.Min(rb.Steel, rb.Concrete)) lo = a; else hi = b;
            }
            cot = (lo + hi) / 2;
        }
        if (!double.IsFinite(cot) || cot < minCot - 1e-10 || cot > maxCot + 1e-10) throw new ArgumentException($"{p.Standard}: cot θ fuori intervallo [{minCot:0.###}; {maxCot:0.###}].");
        var capacity = Resistance(cot);
        Add("cot θ", cot, "−", p.CotTheta is null ? "Massimizza min(VRd,s;VRd,max) nell’intervallo ammesso" : "Assegnato");
        if (!mc) { Add("ν1", nu, "−", "Riduzione del puntone"); Add("αcw", acw, "−", "Sezione non precompressa"); }
        return Result(capacity.Steel, capacity.Concrete, Math.Min(capacity.Steel, capacity.Concrete), cot);
    }

    public static (string Kind, double? Limit) CrackRequirement(string standard, string set, JsonObject options)
    {
        RequireOrdinary(standard);
        if (standard is "NTC 2018" or "UNI EN 1992-1-1") return Ntc2018Checks.CrackRequirement(set, options.S("esposizione"), options.S("sensibilita") == "Sensibile");
        string e = options.S("esposizione");
        string requiredSet = standard.StartsWith("NS") && e is "XD3" or "XS3" ? "SLE_FREQ" : "SLE_QP";
        if (set != requiredSet) return ("Non richiesta: verificare " + SectionWorkspace.Label(requiredSet), null);
        if (options.S("limite_fessure").Trim() != "") return ("Apertura fessure", options.Required("limite_fessure", strict: true));
        if (standard == "Model Code 2010") return ("Selezionare wlim di progetto per Model Code 2010", null);
        if (standard.StartsWith("DS")) return e switch
        { "XD2" or "XD3" or "XS3" => ("Apertura fessure", .2), "XD1" or "XS1" or "XS2" => ("Apertura fessure", .3), "XC2" or "XC3" or "XC4" => ("Apertura fessure", .4), _ => ("Selezionare wlim di progetto per questa esposizione", null) };
        if (standard.StartsWith("NS")) return e switch
        { "X0" => ("Apertura fessure", .4), "XC1" or "XC2" or "XC3" or "XC4" or "XD1" or "XD2" or "XD3" or "XS1" or "XS2" or "XS3" => ("Apertura fessure", .3), _ => ("Selezionare esposizione XC/XD/XS oppure wlim di progetto", null) };
        return e switch
        { "X0" or "XC1" => ("Apertura fessure", .4), "XC2" or "XC3" or "XC4" or "XD1" or "XD2" or "XD3" or "XS1" or "XS2" or "XS3" => ("Apertura fessure", .3), _ => ("Selezionare esposizione XC/XD/XS oppure wlim di progetto", null) };
    }

    public static double CrackWidth(string standard, double sigma, double es, double ecm, double fct,
        double rho, double phi, double cover, double spacing, double tensileDepth, bool shortTerm,
        bool ribbed, double k2, List<CrackCalculationDetail>? details = null)
    {
        RequireOrdinary(standard);
        if (standard == "NTC 2018")
        {
            var result = Ntc2018Checks.CrackWidthWithDetails(sigma, es, ecm, fct, rho, phi, cover, spacing, tensileDepth, shortTerm, ribbed, k2);
            details?.AddRange(result.Details); return result.Width;
        }
        if (new[] { es, ecm, fct, rho, phi, spacing, tensileDepth }.Any(v => !double.IsFinite(v) || v <= 0)
            || !double.IsFinite(sigma + cover + k2) || sigma < 0 || cover < 0 || k2 < .5 || k2 > 1)
            throw new ArgumentException("Parametri di fessurazione non validi.");
        bool mc = standard == "Model Code 2010", din = standard.StartsWith("DIN");
        if ((mc || din) && !ribbed) throw new ArgumentException("Modello di fessurazione MC/DIN implementato per barre ad aderenza migliorata.");
        double kt = din ? .4 : shortTerm ? .6 : .4;
        double lower = mc ? 1 - kt : .6;
        double strain = Math.Max(lower * sigma / es, (sigma - kt * fct / rho * (1 + es / ecm * rho)) / es);
        double k3 = standard.StartsWith("DS") && cover > 0 ? 3.4 * Math.Pow(25 / cover, 2d/3) : 3.4;
        double sr = k3 * cover + (ribbed ? .8 : 1.6) * k2 * .425 * phi / rho;
        string formula = "k3 c + k1 k2 k4 Ø/ρeff";
        if (mc) { sr = 2 * (cover + phi / (4 * (shortTerm ? 1.8 : 1.35) * rho)); formula = "2[c + fctm/(4 τbm) · Ø/ρeff]"; }
        else if (din)
        {
            double freeSpacing = spacing > 5 * (cover + phi / 2) ? 1.3 * tensileDepth : phi / (3.6 * rho);
            sr = Math.Min(freeSpacing, sigma * phi / (3.6 * fct));
            formula = "min[sr secondo (7.11)/(7.14); σs Ø/(3,6 fctm)] · DIN";
        }
        else if (spacing > 5 * (cover + phi / 2)) { sr = 1.3 * tensileDepth; formula = "1,3(h−x), barre distanziate · EC2 (7.14)"; }
        void Add(string key, double value, string unit, string expression) => details?.Add(new(key, value, unit, expression));
        // The legacy rule of Ntc2018Checks.NtcK2FromCompressedBars keeps its trace text (local copy: no unreachable branch).
        bool legacyK2 = Ntc2018Checks.NtcK2FromCompressedBars;
        Add("ρp,eff", rho, "−", "As,eff / Ac,eff"); Add("αe", es/ecm, "−", "Es/Ecm");
        Add("kt", kt, "−", "Coefficiente della durata / normativa"); Add("k₂", k2, "−", (mc || din) && !legacyK2 ? "Distribuzione delle deformazioni; non entra in sr,max di " + standard : "Distribuzione delle deformazioni");
        Add("σs", sigma, "MPa", "Massima tensione nelle barre efficaci"); Add("c", cover, "mm", "Copriferro delle barre efficaci");
        Add("Øeq", phi, "mm", "Diametro equivalente"); Add("s", spacing, "mm", "Interasse massimo adottato");
        Add("sr,max", sr, "mm", formula); Add("εsm − εcm", strain, "−", "max[(σs−kt fctm/ρeff (1+αe ρeff))/Es; βmin σs/Es]");
        Add("β minimo deformazione", lower, "−", "Limite inferiore"); Add("wk", sr * strain, "mm", "sr,max (εsm−εcm)");
        return sr * strain;
    }

    /// <summary>Limite superiore di wk senza barre aderenti in Ac,eff (EC2 7.3.4(3), eq. (7.14); per NTC analogia con la Circolare C4.1.2.2.4.5 [C4.1.10], che non tratta questo caso): limite ρ → 0 di <see cref="CrackWidth"/>.
    /// εsm − εcm = βmin σs/Es; sr,max = 1,3 (h − x), NTC 1,7 · 0,75 (h − x) come per barre distanziate, DIN anche ≤ σs Ø/(3,6 fct).</summary>
    public static double UnbondedCrackWidthBound(string standard, double sigma, double es, double fct, double phi, double tensileDepth, bool shortTerm,
        bool ribbed, List<CrackCalculationDetail>? details = null)
    {
        RequireOrdinary(standard);
        if (new[] { es, fct, phi, tensileDepth }.Any(v => !double.IsFinite(v) || v <= 0) || !double.IsFinite(sigma) || sigma < 0)
            throw new ArgumentException("Parametri di fessurazione non validi.");
        bool ntc = standard == "NTC 2018", mc = standard == "Model Code 2010", din = standard.StartsWith("DIN");
        if ((mc || din) && !ribbed) throw new ArgumentException("Modello di fessurazione MC/DIN implementato per barre ad aderenza migliorata.");
        double lower = mc ? 1 - (shortTerm ? .6 : .4) : .6, strain = lower * sigma / es;
        double sr = ntc ? 1.7 * .75 * tensileDepth : 1.3 * tensileDepth;
        string formula = ntc ? "1,7 · 0,75 (h − x), nessuna barra aderente in Ac,eff" : "1,3 (h − x), nessuna barra aderente in Ac,eff · EC2 7.3.4(3), eq. (7.14)";
        if (din) { sr = Math.Min(sr, sigma * phi / (3.6 * fct)); formula = "min[1,3 (h − x); σs Ø/(3,6 fctm)], nessuna barra aderente in Ac,eff · DIN"; }
        void Add(string key, double value, string unit, string expression) => details?.Add(new(key, value, unit, expression));
        Add("εsm − εcm", strain, "−", "βmin σs/Es (ρp,eff → 0)"); Add("β minimo deformazione", lower, "−", mc ? "1 − kt" : "0,6");
        Add("sr,max", sr, "mm", formula); Add("wk", sr * strain, "mm", "sr,max (εsm−εcm), limite superiore");
        return sr * strain;
    }
}
