using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Calculations;

/// <summary>
/// Prova 4c: integrazione in ANTHEA delle correzioni lato calcestruzzo delle librerie dell'analisi dei bug del 15/10/2026
/// (docs/refactoring/analisi-bug-20261015.md): SLU-1, SLU-2, SLU-4, FES-1, FES-2, ARM-3, DET-3 e i coefficienti unitari del foglio c.a. del
/// palo in ancoraggi e dettagli. Attesi indipendenti dallo script attesi/correzioni_ca_20261015.py (modello a fibre, formule di norma), citati
/// accanto a ogni valore; i confronti fra due percorsi della libreria (2D contro 3D) sono dichiarati come tali.
/// </summary>
internal static class CorrezioniCa20261015
{
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    static JsonObject Workspace() => new() { ["normativa"] = "NTC 2018", ["trefoli"] = new JsonArray() };

    /// <summary>Rettangolo 300×500 C25/30, B450C del catalogo, copriferro 30, staffe Ø8: barre superiori e inferiori della disposizione automatica.</summary>
    static JsonObject Rect(int top, double topDiameter, int bottom, double bottomDiameter)
    {
        var input = SezioneCA.DefaultCatalogInput();
        input["shape"] = "Rettangolare"; input["width_mm"] = "300"; input["height_mm"] = "500";
        input["top_bar_count"] = top.ToString(Invariant); input["top_bar_diameter_mm"] = topDiameter.ToString(Invariant);
        input["bottom_bar_count"] = bottom.ToString(Invariant); input["bottom_bar_diameter_mm"] = bottomDiameter.ToString(Invariant);
        input["side_bar_count_per_side"] = "0"; input["cover_mm"] = "30"; input["transverse_bar_diameter_mm"] = "8";
        input["fck_mpa"] = "25"; input["cls_diagramma"] = "Parabola-rettangolo";
        return input;
    }

    static JsonObject Options2D(double theta) => new()
    {
        ["tipo"] = "N–M", ["theta"] = theta.ToString(Invariant), ["N"] = "0", ["angoli"] = "64", ["assi"] = "Locali", ["proietta"] = "No", ["trazione_cls"] = "No"
    };

    static JsonObject Options3D(string criterion) => new()
    {
        ["criterio"] = criterion, ["assi"] = "Locali", ["strategia"] = "Iterativo", ["angoli"] = "64", ["trazione_cls"] = "No"
    };

    static CheckerDomain3D Domain3D(JsonObject input, string criterion, string state)
    {
        var options = Options3D(criterion);
        var domain = new CheckerSection(input, Workspace(), options, state).Domain3D();
        domain.ConfigureVerification(options);
        return domain;
    }

    public static int Run(Action<bool, string> check)
    {
        int count = 0;
        void Near(double? value, double expected, double relative, string message)
        {
            check(value is double v && double.IsFinite(v) && Math.Abs(v - expected) <= relative * Math.Max(1e-12, Math.Abs(expected)),
                $"{message}: {value?.ToString("G17", Invariant) ?? "null"} invece di {expected.ToString("G17", Invariant)}");
            count++;
        }
        void Is(bool condition, string message) { check(condition, message); count++; }

        // ------------------------------------------------------------ SLU-1: dominio 2D N–M con θ qualsiasi
        // CheckerSection.Domain2D usa sempre il meridiano della direzione del momento (CalculateDomainConstantMomentsRatio). Attesi: η del modello a
        // fibre indipendente (correzioni_ca_20261015.py, NTC 2018 §4.1.2.3.4) entro l'1%, e la verifica 3D a eccentricità costante della libreria
        // (stesso tasso di lavoro, percorso diverso) entro l'1%. Prima del 15/10: θ = 90° dava η 0,0127 «Entro il dominio» con R = 80 604 kN.
        var symmetric = Rect(2, 16, 2, 16);
        var plastic3D = Domain3D(symmetric, "Eccentricità costante", "SLU");
        foreach (var (theta, action, expected) in new (double, ActionPoint, double)[]
        {
            (90, new(-1000, 0, 200), 2.4778), (270, new(-1000, 0, -200), 2.4778), (90, new(-1000, 0, 60), 0.6775),
            (45, new(-1000, 120, 120), 1.5811), (30, new(-500, 100 * Math.Cos(Math.PI / 6), 100 * Math.Sin(Math.PI / 6)), 0.8266),
            (180, new(-1000, -250, 0), 1.3803), (0, new(-1000, 250, 0), 1.3803)
        })
        {
            string tag = $"SLU-1 θ = {theta}° N {action.N} Mx {action.Mx:0.###} My {action.My:0.###}";
            var two = new CheckerSection(symmetric, Workspace(), Options2D(theta), "SLU").Domain2D().Check(action);
            var three = plastic3D.Check(action);
            Near(two.Utilization, expected, .01, tag + ": η 2D contro il modello a fibre");
            Near(two.Utilization, three.Utilization ?? double.NaN, .01, tag + ": η 2D contro la verifica 3D a eccentricità costante");
            Is(two.Status == (expected <= 1 ? "Entro il dominio" : "Fuori dominio"), tag + ": stato «" + two.Status + "»");
            // Segni: NRd di compressione come NEd, momento resistente nel verso dell'azione e senza componente trasversale.
            var r = two.Resistance!.Value;
            double moment = Math.Sqrt(action.Mx * action.Mx + action.My * action.My), along = (r.Mx * action.Mx + r.My * action.My) / moment,
                transverse = (r.Mx * action.My - r.My * action.Mx) / moment;
            Is(r.N < 0 && along > 0 && Math.Abs(transverse) <= 1e-3 * Math.Abs(along), $"{tag}: segni o direzione di Rd ({r.N:0.##}; {r.Mx:0.###}; {r.My:0.###})");
        }
        // Sezione non simmetrica rispetto al piano di flessione (2Ø16 sopra, 3Ø20 sotto), θ = 90°: prima il meridiano dell'asse neutro fisso,
        // con Mx trasversale fino a 42 kNm e i rami rimescolati (η 0,0095); atteso a fibre con l'asse neutro ruotato e Mx = 0: η 1,9673.
        {
            var asymmetric = Rect(2, 16, 3, 20);
            var action = new ActionPoint(-1000, 0, 200);
            var two = new CheckerSection(asymmetric, Workspace(), Options2D(90), "SLU").Domain2D().Check(action);
            Near(two.Utilization, 1.9673, .01, "SLU-1 2Ø16/3Ø20 θ = 90°: η 2D contro il modello a fibre");
            Near(two.Utilization, Domain3D(asymmetric, "Eccentricità costante", "SLU").Check(action).Utilization ?? double.NaN, .01,
                "SLU-1 2Ø16/3Ø20 θ = 90°: η 2D contro la verifica 3D a eccentricità costante");
            Is(Math.Abs(two.Resistance!.Value.Mx) <= 1e-3 * Math.Abs(two.Resistance.Value.My), "SLU-1 2Ø16/3Ø20 θ = 90°: Mx trasversale in Rd");
        }
        // SLV (dominio elastico): il meridiano della direzione del momento ora c'è anche per θ obliqui (prima «sezione del dominio assente»).
        // Nessun modello indipendente del dominio elastico: confronto fra i due percorsi della libreria, entro l'1%.
        {
            var elastic3D = Domain3D(symmetric, "Eccentricità costante", "SLV");
            foreach (var (theta, action) in new (double, ActionPoint)[] { (90, new(-1000, 0, 60)), (45, new(-1000, 40, 40)), (0, new(-1000, 80, 0)) })
            {
                var two = new CheckerSection(symmetric, Workspace(), Options2D(theta), "SLV").Domain2D().Check(action);
                Near(two.Utilization, elastic3D.Check(action).Utilization ?? double.NaN, .01, $"SLU-1 SLV θ = {theta}°: η 2D contro la verifica 3D elastica");
            }
        }
        return count;
    }
}
