using System.Text.Json.Nodes;
using GPC.Checkers.Concrete.Results;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Materials;

namespace X.Core;

/// <summary>Port of Rhino2Midas concrete checks, with documented NTC2018/Circolare2019 corrections.</summary>
public static class Ntc2018Checks
{
    public static readonly string[] Exposures = ["Da scegliere", "X0", "XC1", "XC2", "XC3", "XF1", "XC4", "XD1", "XS1", "XA1", "XA2", "XF2", "XF3", "XD2", "XD3", "XS2", "XS3", "XA3", "XF4"];
    public sealed record CrackResult(double? Width, double? Limit, double? Ratio, bool? Passed, string Status, double? EffectiveArea = null, double? EffectiveSteel = null);
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
    public static CrackResult Cracking(CheckerSection engine, CheckerStressState state, ActionPoint force, JsonObject input, JsonObject workspace, JsonObject options, string set)
    {
        var req = CrackRequirement(set, options.S("esposizione"), options.S("sensibilita", "Poco sensibile") == "Sensibile");
        if (set == "SLE" || req.Kind.StartsWith("Selezionare")) return new(null, null, null, null, req.Kind);
        if (req.Kind != "Apertura fessure")
        {
            // NTC 4.1.2.2.4.5: these checks use the homogenized UNCRACKED section, not wk / 0.
            var uncracked = (JsonObject)options.DeepClone(); uncracked["modello"] = "Lineare"; uncracked["trazione_cls"] = "Sì";
            var check = new CheckerSection(engine.Model, input, workspace, uncracked).Stress(force, "SLE_FREQ");
            var stresses = check.Native.GetConcreteVerticesTension(check.Native.PsiRebar ?? 0);
            double maximum = stresses.Max(p => p.tension);
            double limit = req.Kind == "Decompressione" ? 0 : ((ConcreteMaterialEuropeanCommon)engine.Section.ConcreteMaterial).Fctm / 1.2;
            bool ok = maximum <= limit;
            return new(null, null, null, ok, req.Kind + (ok ? ": soddisfatta" : ": non soddisfatta") + $" · σct,max={maximum:0.000} MPa; limite={limit:0.000}");
        }
        var native = state.Native; var section = engine.Section;
        if (workspace.Array("trefoli").Any()) return new(null, req.Limit, null, null, "Apertura CAP: modello aderenza/decompressione da definire");
        if (!native.LinearElasticAnalysis || options.S("trazione_cls") == "Sì")
            return new(null, req.Limit, null, null, "wk richiede analisi lineare con CLS teso escluso");
        var plane = native.StrainPlane;
        var points = section.Shape.GetPoints2d();
        var strains = points.Select(plane.GetStrain).ToArray();
        if (strains.Max() <= 1e-12) return new(0, req.Limit, 0, true, "Sezione interamente compressa");
        if (strains.Min() >= 0) return new(null, req.Limit, null, null, "Sezione interamente tesa: verificare separatamente le due aree efficaci");
        double gradient = double.Hypot(plane.ChiX, plane.ChiY);
        if (gradient <= 1e-15) return new(null, req.Limit, null, null, "Asse neutro non determinato");
        double qx = plane.ChiX / gradient, qy = plane.ChiY / gradient;
        double Q(Point2d p) => qx * p.X + qy * p.Y;
        double top = points.Max(Q), bottom = points.Min(Q), height = top - bottom;
        double tensileDepth = strains.Max() / gradient;
        var bars = section.GetRebars().Where(r => plane.GetStrain(r.Position) > 0).ToArray();
        if (bars.Length == 0) return new(null, req.Limit, null, null, "Nessuna armatura tesa");
        double centroid = bars.Sum(r => Q(r.Position) * r.Area) / bars.Sum(r => r.Area);
        double coverToCenter = top - centroid, hc = Math.Min(2.5 * coverToCenter, Math.Min(tensileDepth / 3, height / 2));
        if (hc <= 0) return new(null, req.Limit, null, null, "Area efficace nulla");
        double level = top - hc, half = 4 * Math.Max(height, engine.Geometry.Width);
        var start = new Point2d(qx * level - qy * half, qy * level + qx * half);
        var end = new Point2d(qx * level + qy * half, qy * level - qx * half);
        var mesh = (Mesh)section.Mesh.Clone(); mesh.Cut(new Line2d(start, end));
        double aceff = mesh.GetFaces().Where(f => Q(mesh.GetFaceCentroid(f)) >= level - 1e-8).Sum(mesh.FaceArea);
        var effective = bars.Where(r => Q(r.Position) >= level - 1e-8).ToArray();
        if (aceff <= 0 || effective.Length == 0) return new(null, req.Limit, null, null, "Armatura/area efficace assente");
        double steel = effective.Sum(r => r.Area), phi = effective.Sum(r => r.RebarSection.Diameter * r.RebarSection.Diameter) / effective.Sum(r => r.RebarSection.Diameter);
        double sigma = effective.Max(r => native.GetRebarTension(native.PsiRebar ?? 0, r));
        double c = options.S("copriferro_fessure").Trim() == "" ? input.Required("cover_mm") + input.Required("transverse_bar_diameter_mm") : options.Required("copriferro_fessure");
        // User-specified maximum spacing avoids treating nearest-neighbour spacing as the maximum in multilayer/curved reinforcement.
        if (options.S("spaziatura_fessure").Trim() == "") return new(null, req.Limit, null, null, "Inserire la spaziatura massima delle barre tese", aceff, steel);
        double spacing = options.Required("spaziatura_fessure", strict: true), es = effective[0].RebarMaterial.E;
        var concrete = (ConcreteMaterialEuropeanCommon)section.ConcreteMaterial;
        double width = CrackWidth(sigma, es, concrete.Ecm, concrete.Fctm, steel / aceff, phi, c, spacing, tensileDepth,
            options.S("durata", "Lunga") == "Breve", options.S("aderenza", "Migliorata") == "Migliorata", .5);
        return new(width, req.Limit, width / req.Limit, width <= req.Limit, width <= req.Limit ? "Apertura entro limite" : "Apertura oltre limite", aceff, steel);
    }
    public static double CrackWidth(double sigmaS, double es, double ecm, double fctm, double rho, double phi, double cover, double spacing, double tensileDepth, bool shortTerm, bool ribbed, double k2)
    {
        if (new[] { es, ecm, fctm, rho, phi, spacing, tensileDepth }.Any(v => !double.IsFinite(v) || v <= 0) || !double.IsFinite(sigmaS + cover + k2) || sigmaS < 0 || cover < 0 || k2 < .5 || k2 > 1) throw new ArgumentException("Parametri fessurazione non validi.");
        double kt = shortTerm ? .6 : .4, k1 = ribbed ? .8 : 1.6;
        double strain = Math.Max((sigmaS - kt * fctm / rho * (1 + es / ecm * rho)) / es, .6 * sigmaS / es);
        double near = (3.4 * cover + k1 * k2 * .425 * phi / rho) / 1.7;
        // C4.1.7 applies near bars; C4.1.10 in the remaining region. Check both, never take the smaller unconditionally.
        double distance = spacing <= 5 * (cover + phi / 2) ? near : Math.Max(near, .75 * tensileDepth);
        return Math.Max(0, 1.7 * distance * strain);
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
