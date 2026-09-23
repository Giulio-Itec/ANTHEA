using System.Text.Json.Nodes;
using GPC.Checkers.Concrete.Results;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Materials;

namespace X.Core;

/// <summary>Port of Rhino2Midas concrete checks, with documented NTC2018/Circolare2019 corrections.</summary>
public static class Ntc2018Checks
{
    public static ITensionBarSpacing SpacingCalculator { get; set; } = new TensionBarSpacing();
    public static readonly string[] Exposures = ["Da scegliere", "X0", "XC1", "XC2", "XC3", "XF1", "XC4", "XD1", "XS1", "XA1", "XA2", "XF2", "XF3", "XD2", "XD3", "XS2", "XS3", "XA3", "XF4"];
    public sealed record CrackResult(double? Width, double? Limit, double? Ratio, bool? Passed, string Status, double? EffectiveArea = null, double? EffectiveSteel = null, double? BarSpacing = null, string? SpacingSource = null)
    {
        public CrackCalculationDetail[] Details { get; init; } = [];
    }
    public static (string Kind, double? Limit) CrackRequirement(string set, string exposure, bool sensitive)
    {
        if (set == "SLE") return ("Non richiesta nella rara", null);
        int index = Array.IndexOf(Exposures, exposure);
        if (index <= 0) return ("Selezionare la classe di esposizione", null);
        bool qp = set == "SLE_QP"; int environment = index <= 5 ? 0 : index <= 12 ? 1 : 2;
        if (sensitive && environment >= 1 && qp) return ("Decompressione", null);
        if (sensitive && environment == 2 && !qp) return ("Formazione fessure", null);
        return ("Apertura fessure", environment == 0 ? sensitive ? qp ? .2 : .3 : qp ? .3 : .4
            : environment == 1 ? sensitive ? .2 : qp ? .2 : .3 : .2);
    }
    /// <summary>Compression is negative. Inspect every ordinary bar, not only the effective tensile area.</summary>
    public static double CrackK2(IReadOnlyList<double> barStresses)
    {
        if (barStresses.Count == 0 || barStresses.Any(s => !double.IsFinite(s)))
            throw new ArgumentException("k₂: tensioni delle armature mancanti o non finite.");
        return barStresses.Any(s => s < 0) ? .5 : 1;
    }
    public static CrackResult Cracking(CheckerSection engine, CheckerStressState state, ActionPoint force, JsonObject input, JsonObject workspace, JsonObject options, string set)
    {
        var details = new List<CrackCalculationDetail>();
        void Add(string symbol, double? value, string unit, string expression, string note = "") => details.Add(new(symbol, value, unit, expression, note));
        Add("Verifica", null, "", SectionWorkspace.Label(set) + " · " + options.S("esposizione") + " · " + options.S("sensibilita") + " · durata " + options.S("durata") + " · aderenza " + options.S("aderenza"));
        Add("Modello", null, "", (state.Native.LinearElasticAnalysis ? "Lineare" : "Non lineare") + " · CLS teso: " + options.S("trazione_cls"), "Tensioni e piano di deformazione provenienti da Checker; compressione negativa.");
        Add("N", force.N, "kN", "Azione negli assi " + options.S("assi", "Locali"));
        Add("Mx", force.Mx, "kNm", "Azione della combinazione");
        Add("My", force.My, "kNm", "Azione della combinazione");
        Add("φ", state.Native.PsiRebar ?? 0, "−", "Coefficiente utilizzato nell'analisi tensionale");
        Add("γc (input)", input.D("gamma_c"), "−", "Coefficiente di materiale della sezione", "Non compare direttamente nella formula wk: qui si usa fctm, non fctd.");
        Add("γs (input)", input.D("gamma_s"), "−", "Coefficiente di materiale della sezione", "Non è applicato come divisore aggiuntivo di σs nella formula wk.");
        var req = CrackRequirement(set, options.S("esposizione"), options.S("sensibilita", "Poco sensibile") == "Sensibile");
        Add("Criterio", null, "", req.Kind, "Criterio selezionato dal codice in funzione di famiglia SLE, esposizione e sensibilità.");
        Add("wlim", req.Limit, "mm", "Limite di apertura selezionato", req.Limit is null ? "Nessun limite di apertura numerico per questo ramo." : "Confronto wk ≤ wlim");
        if (set == "SLE" || req.Kind.StartsWith("Selezionare")) return new(null, null, null, null, req.Kind) { Details = details.ToArray() };
        if (req.Kind != "Apertura fessure")
        {
            // NTC 4.1.2.2.4.5: these checks use the homogenized UNCRACKED section, not wk / 0.
            var uncracked = (JsonObject)options.DeepClone(); uncracked["modello"] = "Lineare"; uncracked["trazione_cls"] = "Sì";
            var check = new CheckerSection(engine.Model, input, workspace, uncracked).Stress(force, "SLE_FREQ");
            var stresses = check.Native.GetConcreteVerticesTension(check.Native.PsiRebar ?? 0);
            double maximum = stresses.Max(p => p.tension);
            double limit = req.Kind == "Decompressione" ? 0 : ((ConcreteMaterialEuropeanCommon)engine.Section.ConcreteMaterial).Fctm / 1.2;
            Add("Analisi ausiliaria", null, "", "Sezione omogeneizzata interamente reagente, lineare, CLS teso incluso");
            Add("fctm", ((ConcreteMaterialEuropeanCommon)engine.Section.ConcreteMaterial).Fctm, "MPa", "Resistenza media a trazione del materiale Checker");
            Add("σct,max", maximum, "MPa", "max delle tensioni ai vertici nella sezione interamente reagente");
            Add("σct,lim", limit, "MPa", req.Kind == "Decompressione" ? "0" : "fctm / 1,2", "σct,max ≤ σct,lim; non viene eseguita una divisione per limite nullo.");
            if (req.Kind != "Decompressione") Add("Divisore formazione", 1.2, "−", "Coefficiente usato per fctm / 1,2");
            bool ok = maximum <= limit;
            return new(null, null, null, ok, req.Kind + (ok ? ": soddisfatta" : ": non soddisfatta") + $" · σct,max={maximum:0.000} MPa; limite={limit:0.000}") { Details = details.ToArray() };
        }
        var native = state.Native; var section = engine.Section;
        if (workspace.Array("trefoli").Any()) return new(null, req.Limit, null, null, "Apertura CAP: modello aderenza/decompressione da definire") { Details = details.ToArray() };
        if (!native.LinearElasticAnalysis || options.S("trazione_cls") == "Sì")
            return new(null, req.Limit, null, null, "wk richiede analisi lineare con CLS teso escluso") { Details = details.ToArray() };
        var barStresses = state.tensioni_barre.Take(engine.Geometry.Bars.Count).ToArray();
        double k2 = CrackK2(barStresses);
        int compressedBars = barStresses.Count(s => s < 0), tensileBars = barStresses.Count(s => s > 0), zeroBars = barStresses.Count(s => s == 0);
        Add("Barre compresse per k₂", compressedBars, "−", "Conteggio σs < 0 su tutte le armature ordinarie, anche fuori dall'area efficace");
        Add("Barre tese per k₂", tensileBars, "−", "Conteggio σs > 0");
        Add("Barre a tensione nulla per k₂", zeroBars, "−", "Conteggio σs = 0");
        Add("Criterio k₂", k2, "−", compressedBars > 0 ? "Flessione: almeno una armatura compressa → k₂ = 0,50" : "Trazione: nessuna armatura compressa → k₂ = 1,00",
            zeroBars > 0 ? "Le barre a tensione esattamente nulla non sono considerate compresse." : "Selezione per la combinazione corrente, dalle tensioni Checker.");
        var plane = native.StrainPlane;
        var points = section.Shape.GetPoints2d();
        var strains = points.Select(plane.GetStrain).ToArray();
        Add("εc,min", strains.Min(), "−", "min ε ai vertici, prima dei criteri di applicabilità");
        Add("εc,max", strains.Max(), "−", "max ε ai vertici");
        Add("Tolleranza compressione", 1e-12, "−", "Se εc,max ≤ tolleranza, wk = 0");
        if (strains.Max() <= 1e-12) return new(0, req.Limit, 0, true, "Sezione interamente compressa") { Details = details.ToArray() };
        if (strains.Min() >= 0) return new(null, req.Limit, null, null, "Sezione interamente tesa: verificare separatamente le due aree efficaci") { Details = details.ToArray() };
        double gradient = double.Hypot(plane.ChiX, plane.ChiY);
        Add("χx", plane.ChiX, "1/mm", "Componente del gradiente di deformazione restituita da Checker");
        Add("χy", plane.ChiY, "1/mm", "Componente del gradiente di deformazione restituita da Checker");
        Add("|∇ε|", gradient, "1/mm", "sqrt(χx² + χy²)");
        if (gradient <= 1e-15) return new(null, req.Limit, null, null, "Asse neutro non determinato") { Details = details.ToArray() };
        double qx = plane.ChiX / gradient, qy = plane.ChiY / gradient;
        double Q(Point2d p) => qx * p.X + qy * p.Y;
        double top = points.Max(Q), bottom = points.Min(Q), height = top - bottom;
        double tensileDepth = strains.Max() / gradient;
        Add("qx", qx, "−", "χx / |∇ε|");
        Add("qy", qy, "−", "χy / |∇ε|");
        Add("Qmax", top, "mm", "max(qx·x + qy·y) sui vertici");
        Add("Qmin", bottom, "mm", "min(qx·x + qy·y) sui vertici");
        Add("h", height, "mm", "Qmax − Qmin; altezza proiettata lungo il gradiente");
        Add("h − x", tensileDepth, "mm", "εc,max / |∇ε|; profondità tesa");
        Add("x", height - tensileDepth, "mm", "h − profondità tesa; profondità compressa");
        var bars = section.GetRebars().Where(r => plane.GetStrain(r.Position) > 0).ToArray();
        if (bars.Length == 0) return new(null, req.Limit, null, null, "Nessuna armatura tesa") { Details = details.ToArray() };
        double centroid = bars.Sum(r => Q(r.Position) * r.Area) / bars.Sum(r => r.Area);
        double coverToCenter = top - centroid, hc = Math.Min(2.5 * coverToCenter, Math.Min(tensileDepth / 3, height / 2));
        Add("Numero barre tese", bars.Length, "−", "Barre con ε > 0");
        Add("QG,s", centroid, "mm", "Σ(Qi·As,i) / ΣAs,i sulle barre tese");
        Add("h − d", coverToCenter, "mm", "Qmax − QG,s");
        Add("d", height - coverToCenter, "mm", "h − (h − d)");
        Add("Candidato 1 hc,eff", 2.5 * coverToCenter, "mm", "2,5·(h − d)");
        Add("Candidato 2 hc,eff", tensileDepth / 3, "mm", "(h − x) / 3");
        Add("Candidato 3 hc,eff", height / 2, "mm", "h / 2");
        Add("hc,eff", hc, "mm", "min[2,5·(h − d); (h − x)/3; h/2]");
        if (hc <= 0) return new(null, req.Limit, null, null, "Area efficace nulla") { Details = details.ToArray() };
        double level = top - hc, half = 4 * Math.Max(height, engine.Geometry.Width);
        var start = new Point2d(qx * level - qy * half, qy * level + qx * half);
        var end = new Point2d(qx * level + qy * half, qy * level - qx * half);
        var mesh = (Mesh)section.Mesh.Clone(); mesh.Cut(new Line2d(start, end));
        double aceff = mesh.GetFaces().Where(f => Q(mesh.GetFaceCentroid(f)) >= level - 1e-8).Sum(mesh.FaceArea);
        var effective = bars.Where(r => Q(r.Position) >= level - 1e-8).ToArray();
        Add("Qtaglio", level, "mm", "Qmax − hc,eff; fascia efficace Q ≥ Qtaglio");
        Add("Ac,eff", aceff, "mm²", "Somma delle aree delle facce della mesh tagliata nella fascia efficace", "Integrazione geometrica sulla mesh Checker.");
        Add("Numero barre efficaci", effective.Length, "−", "Barre tese con Q ≥ Qtaglio − 10⁻⁸ mm");
        foreach (var (bar, index) in section.GetRebars().Select((bar, index) => (bar, index)))
        {
            var strain = plane.GetStrain(bar.Position);
            if (strain <= 0) continue;
            string id = "B" + (index + 1).ToString("D2");
            bool included = Q(bar.Position) >= level - 1e-8;
            Add(id + " · ε", strain, "−", "ε(x,y) dal piano Checker", included ? "Inclusa in As,eff" : "Tesa ma esclusa da As,eff");
            Add(id + " · x", bar.Position.X, "mm", "Coordinata della barra");
            Add(id + " · y", bar.Position.Y, "mm", "Coordinata della barra");
            Add(id + " · Q", Q(bar.Position), "mm", "qx·x + qy·y");
            Add(id + " · Ø", bar.RebarSection.Diameter, "mm", "Diametro della barra");
            Add(id + " · As", bar.Area, "mm²", "Area della barra");
            Add(id + " · σs", native.GetRebarTension(native.PsiRebar ?? 0, bar), "MPa", "Tensione nativa nella barra");
        }
        if (aceff <= 0 || effective.Length == 0) return new(null, req.Limit, null, null, "Armatura/area efficace assente") { Details = details.ToArray() };
        double steel = effective.Sum(r => r.Area), phi = effective.Sum(r => r.RebarSection.Diameter * r.RebarSection.Diameter) / effective.Sum(r => r.RebarSection.Diameter);
        double sigma = effective.Max(r => native.GetRebarTension(native.PsiRebar ?? 0, r));
        double c = options.S("copriferro_fessure").Trim() == "" ? input.Required("cover_mm") + input.Required("transverse_bar_diameter_mm") : options.Required("copriferro_fessure");
        Add("As,eff", steel, "mm²", "ΣAs,i delle barre tese incluse nella fascia efficace");
        Add("ΣØ²", effective.Sum(r => r.RebarSection.Diameter * r.RebarSection.Diameter), "mm²", "Somma sulle barre efficaci");
        Add("ΣØ", effective.Sum(r => r.RebarSection.Diameter), "mm", "Somma sulle barre efficaci");
        Add("Øeq", phi, "mm", "ΣØ² / ΣØ");
        Add("σs", sigma, "MPa", "max σs delle barre efficaci", "Si usa il massimo, non la tensione media pesata.");
        Add("c", c, "mm", options.S("copriferro_fessure").Trim() == "" ? "Copriferro netto + Ø staffa" : "Override manuale per fessurazione");
        bool automatic = options.S("spaziatura_fessure").Trim() == "";
        var tensileIndices = engine.Geometry.Bars.Select((b, i) => (b, i))
            .Where(v => plane.GetStrain(new Point2d(v.b.X, v.b.Y)) > 0 && Q(new Point2d(v.b.X, v.b.Y)) >= level - 1e-8).Select(v => v.i).ToArray();
        double? calculatedSpacing = automatic ? SpacingCalculator.Maximum(engine.Geometry, tensileIndices) : options.Required("spaziatura_fessure", strict: true);
        Add("s", calculatedSpacing, "mm", automatic ? "Interasse massimo geometrico delle barre efficaci tese" : "Interasse massimo manuale");
        if (calculatedSpacing is not double spacing || spacing <= 0)
            return new(null, req.Limit, null, null, "Interasse automatico non determinabile: inserire un valore manuale", aceff, steel) { Details = details.ToArray() };
        double es = effective[0].RebarMaterial.E;
        var concrete = (ConcreteMaterialEuropeanCommon)section.ConcreteMaterial;
        Add("Ecls analisi", concrete.E, "MPa", "Modulo del materiale nell'analisi Checker");
        Add("n analisi", es * (1 + (native.PsiRebar ?? 0)) / concrete.E, "−", "Es·(1 + φ) / Ecls; distinto da αe usato nella formula di fessurazione");
        double width = CalculateCrackWidth(sigma, es, concrete.Ecm, concrete.Fctm, steel / aceff, phi, c, spacing, tensileDepth,
            options.S("durata", "Lunga") == "Breve", options.S("aderenza", "Migliorata") == "Migliorata", k2, details);
        Add("ηw", width / req.Limit, "−", "wk / wlim");
        return new(width, req.Limit, width / req.Limit, width <= req.Limit, width <= req.Limit ? "Apertura entro limite" : "Apertura oltre limite", aceff, steel, spacing, automatic ? "Automatico geometrico" : "Manuale") { Details = details.ToArray() };
    }
    public static double CrackWidth(double sigmaS, double es, double ecm, double fctm, double rho, double phi, double cover, double spacing, double tensileDepth, bool shortTerm, bool ribbed, double k2)
        => CalculateCrackWidth(sigmaS, es, ecm, fctm, rho, phi, cover, spacing, tensileDepth, shortTerm, ribbed, k2, null);

    public static (double Width, CrackCalculationDetail[] Details) CrackWidthWithDetails(double sigmaS, double es, double ecm, double fctm, double rho, double phi, double cover, double spacing, double tensileDepth, bool shortTerm, bool ribbed, double k2)
    {
        var details = new List<CrackCalculationDetail>();
        double width = CalculateCrackWidth(sigmaS, es, ecm, fctm, rho, phi, cover, spacing, tensileDepth, shortTerm, ribbed, k2, details);
        return (width, details.ToArray());
    }

    private static double CalculateCrackWidth(double sigmaS, double es, double ecm, double fctm, double rho, double phi, double cover, double spacing, double tensileDepth, bool shortTerm, bool ribbed, double k2, List<CrackCalculationDetail>? details)
    {
        if (new[] { es, ecm, fctm, rho, phi, spacing, tensileDepth }.Any(v => !double.IsFinite(v) || v <= 0) || !double.IsFinite(sigmaS + cover + k2) || sigmaS < 0 || cover < 0 || k2 < .5 || k2 > 1) throw new ArgumentException("Parametri fessurazione non validi.");
        void Add(string symbol, double value, string unit, string expression, string note = "") => details?.Add(new(symbol, value, unit, expression, note));
        double kt = shortTerm ? .6 : .4, k1 = ribbed ? .8 : 1.6;
        double alphaE = es / ecm;
        double stiffening = kt * fctm / rho * (1 + alphaE * rho);
        double computedStrain = (sigmaS - stiffening) / es, minimumStrain = .6 * sigmaS / es;
        double strain = Math.Max(computedStrain, minimumStrain);
        double near = (3.4 * cover + k1 * k2 * .425 * phi / rho) / 1.7;
        double spacingLimit = 5 * (cover + phi / 2), far = .75 * tensileDepth;
        // Keep the existing branch: sparse bars compare the near-bar and remote regions.
        bool closeBars = spacing <= spacingLimit;
        double distance = closeBars ? near : Math.Max(near, far);
        double width = Math.Max(0, 1.7 * distance * strain);
        Add("Es", es, "MPa", "Modulo elastico della prima barra efficace");
        Add("Ecm", ecm, "MPa", "Modulo medio CLS usato per αe");
        Add("fct,eff = fctm", fctm, "MPa", "Resistenza media a trazione del materiale", "Nel codice attuale non è applicata una riduzione per l'età di fessurazione.");
        Add("ρp,eff", rho, "−", "As,eff / Ac,eff");
        Add("αe", alphaE, "−", "Es / Ecm", "Non coincide necessariamente con n dell'analisi con viscosità.");
        Add("kt", kt, "−", shortTerm ? "Breve durata → 0,60" : "Lunga durata → 0,40");
        Add("k₁", k1, "−", ribbed ? "Aderenza migliorata → 0,80" : "Barre lisce → 1,60");
        Add("k₂", k2, "−", "Coefficiente della distribuzione delle deformazioni passato dal chiamante");
        Add("k₃", 3.4, "−", "Coefficiente del termine di copriferro");
        Add("k₄", .425, "−", "Coefficiente del termine Øeq / ρp,eff");
        Add("β minimo deformazione", .6, "−", "Limite inferiore = 0,60·σs/Es");
        Add("β apertura", 1.7, "−", "wk = 1,70·Δsm·(εsm − εcm)");
        Add("Coefficiente regione distante", .75, "−", "Δsm,distante = 0,75·(h − x)");
        Add("Coefficiente soglia interasse", 5, "−", "s_lim = 5·(c + Øeq/2)");
        Add("σs (formula)", sigmaS, "MPa", "Tensione delle barre adottata nel calcolo");
        Add("Øeq (formula)", phi, "mm", "Diametro equivalente delle barre efficaci");
        Add("c (formula)", cover, "mm", "Copriferro alla superficie della barra longitudinale");
        Add("s (formula)", spacing, "mm", "Interasse massimo adottato");
        Add("h − x (formula)", tensileDepth, "mm", "Profondità della zona tesa");
        Add("1 + αe·ρp,eff", 1 + alphaE * rho, "−", "Fattore di interazione CLS/armatura");
        Add("Δσ tension stiffening", stiffening, "MPa", "kt·fct,eff/ρp,eff·(1 + αe·ρp,eff)");
        Add("Δε calcolata", computedStrain, "−", "(σs − Δσ) / Es", $"({sigmaS:G10} − {stiffening:G10}) / {es:G10}");
        Add("Δε minima", minimumStrain, "−", "0,60·σs / Es", $"0,60 × {sigmaS:G10} / {es:G10}");
        Add("εsm − εcm", strain, "−", "max(Δε calcolata; Δε minima)", computedStrain >= minimumStrain ? "Governa il valore calcolato." : "Governa il minimo 0,60·σs/Es.");
        Add("Termine copriferro", 3.4 * cover, "mm", "k₃·c");
        Add("Termine armatura", k1 * k2 * .425 * phi / rho, "mm", "k₁·k₂·k₄·Øeq / ρp,eff");
        Add("Δsm,vicino", near, "mm", "(k₃·c + k₁·k₂·k₄·Øeq/ρp,eff) / 1,70", "Il divisore 1,70 converte questo termine in distanza media nel percorso di calcolo attuale.");
        Add("s_lim", spacingLimit, "mm", "5·(c + Øeq/2)");
        Add("s − s_lim", spacing - spacingLimit, "mm", "Differenza usata per scegliere il ramo", closeBars ? "s ≤ s_lim: si usa Δsm,vicino." : "s > s_lim: si confrontano regione vicina e regione distante.");
        Add("Δsm,distante", far, "mm", "0,75·(h − x)", closeBars ? "Calcolato per confronto, non usato nel ramo attivo." : "Candidato nel ramo di barre distanziate.");
        Add("Δsm adottata", distance, "mm", closeBars ? "Δsm,vicino" : "max(Δsm,vicino; Δsm,distante)", closeBars || near >= far ? "Governa regione vicina alle barre." : "Governa regione distante dalle barre.");
        Add("wk", width, "mm", "max[0; 1,70·Δsm·(εsm − εcm)]", $"max[0; 1,70 × {distance:G10} × {strain:G10}]");
        return width;
    }

    public sealed record ShearResult(double VRsd, double VRcd, double VRd, double? Ratio, double CotTheta, string Status);
    public static ShearResult Shear(double nKn, double vKn, double area, double bw, double d, double asl, double fck, double fcd, double fyd, double gammaC,
        double asw, double spacing, double alphaDeg, double? cotTheta = null, double leverFactor = .9)
    {
        if (new[] { area, bw, d, fck, fcd, fyd, gammaC, spacing }.Any(v => !double.IsFinite(v) || v <= 0) || asl < 0 || asw < 0 || alphaDeg < 45 || alphaDeg > 90 || !double.IsFinite(nKn + vKn + asl + asw + alphaDeg + leverFactor) || leverFactor <= 0 || leverFactor > .9)
            throw new ArgumentException("Taglio: controllare aree, geometria, materiali e inclinazione delle staffe (45–90°).");
        double sigmaCp = -nKn * 1000 / area; // NTC compression stress positive; UI/Checker N compression negative.
        if (asw == 0)
        {
            if (nKn > 0) return new(0, 0, 0, null, 0, "Trazione: taglio senza staffe non verificato automaticamente");
            sigmaCp = Math.Min(sigmaCp, .2 * fcd);
            double k = Math.Min(2, 1 + Math.Sqrt(200 / d)), rho = Math.Min(.02, asl / (bw * d));
            double a = (.18 / gammaC * k * Math.Cbrt(100 * rho * fck) + .15 * sigmaCp) * bw * d / 1000;
            double b = (.035 * Math.Pow(k, 1.5) * Math.Sqrt(fck) + .15 * sigmaCp) * bw * d / 1000;
            double resistance = Math.Max(a, b), ratio = Math.Abs(vKn) / resistance;
            return new(a, b, resistance, ratio, 0, ratio <= 1 ? "Resistenza sufficiente · dettagli da verificare" : "Resistenza insufficiente");
        }
        double sc = Math.Max(0, sigmaCp), ac = sc <= .25 * fcd ? 1 + sc / fcd : sc <= .5 * fcd ? 1.25 : Math.Max(0, 2.5 * (1 - sc / fcd));
        double alpha = alphaDeg * Math.PI / 180;
        double autoCot = Math.Sqrt(Math.Max(0, .5 * fcd * bw * ac / (asw / spacing * fyd * Math.Sin(alpha)) - 1));
        double cot = cotTheta ?? Math.Clamp(autoCot, 1, 2.5);
        if (!double.IsFinite(cot) || cot < 1 || cot > 2.5) throw new ArgumentException("NTC: cot θ deve essere tra 1 e 2,5.");
        double common = 1 / Math.Tan(alpha) + cot;
        double rsd = leverFactor * d * asw / spacing * fyd * common * Math.Sin(alpha) / 1000;
        double rcd = leverFactor * d * bw * ac * .5 * fcd * common / (1 + cot * cot) / 1000;
        double rd = Math.Min(rsd, rcd);
        double? eta = rd > 0 ? Math.Abs(vKn) / rd : null;
        return new(rsd, rcd, rd, eta, cot, eta is null ? "Resistenza nulla / fuori campo" : eta <= 1 ? "Resistenza sufficiente · dettagli da verificare" : "Resistenza insufficiente");
    }
}
