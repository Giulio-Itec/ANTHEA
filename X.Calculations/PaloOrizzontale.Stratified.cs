using System.Globalization;
using System.Text.Json.Nodes;

namespace Anthea.Calculations;

public static partial class PaloOrizzontale
{
    public const string BromsMethod = "Broms";
    public const string StratifiedMethod = "Stratificato";
    private const string LegacyStratifiedMethod = "Stratificato (PileChecker)";
    public static string NormalizeMethod(string method) => method == LegacyStratifiedMethod ? StratifiedMethod : method;
    public const string StratifiedSource = "Estensione stratificata di G. Pacini: diagrammi per strato e profondità a taglio nullo; equilibrio globale con reazioni distribuite. Modello sperimentale, non formula originale di Broms.";

    /// <summary>Uses a copy of the input and selects the distributed, possibly mixed-soil model.</summary>
    public static JsonObject CalculateStratified(JsonObject data)
    {
        var copy = (JsonObject)data.DeepClone();
        if (copy["generali"] is not JsonObject general) return J.Error("Dati generali mancanti.");
        general["metodo_calcolo"] = StratifiedMethod;
        return Calculate(copy);
    }

    private static bool DistributedMethod(JsonNode general) => NormalizeMethod(general.S("metodo_calcolo", BromsMethod)) switch
    {
        BromsMethod => false,
        StratifiedMethod => true,
        _ => throw new ArgumentException("Metodo di calcolo orizzontale non riconosciuto.")
    };

    // p is a force per unit length [kN/m], not a pressure. Each segment belongs
    // to one layer and lies entirely above/below the water table and the 1.5D cut.
    private sealed record Segment(double Top, double Bottom, double P0, double Slope, int LayerIndex,
        string Kind, double EffectiveStressAtTop)
    {
        public double Force(double z) { double t = Math.Clamp(z - Top, 0, Bottom - Top); return P0 * t + Slope * t * t / 2; }
        public double First(double z) { double t = Math.Clamp(z - Top, 0, Bottom - Top); return Top * Force(z) + P0 * t * t / 2 + Slope * t * t * t / 3; }
    }

    private sealed class Ground
    {
        public List<Segment> Segments { get; } = [];
        public bool Clay { get; }
        public bool Mixed { get; }
        public bool Distributed { get; }
        public bool Uniform { get; private set; } = true;
        public double L { get; }
        public double Tolerance { get; }
        public double Q(double z) => Segments.Sum(s => s.Force(z));
        public double S(double z) => Segments.Sum(s => s.First(z));
        public double A(double z) => z * Q(z) - S(z);
        public double P(double z, bool before = false)
        {
            var s = Segments.FirstOrDefault(s => before ? z > s.Top && z <= s.Bottom : z >= s.Top && z < s.Bottom);
            return s is null ? 0 : s.P0 + s.Slope * (z - s.Top);
        }

        public Ground(JsonArray rows, double l, double d, bool water, double zw, double tolerance, bool distributed = false)
        {
            if (!(l > 0) || !(d > 0) || !double.IsFinite(l + d + zw + tolerance))
                throw new ArgumentException("Geometria e parametri numerici non validi.");
            L = l; Tolerance = tolerance;
            if (rows.Count == 0) throw new ArgumentException("Inserire almeno uno strato.");
            // Clip actual layer intervals, without translating interfaces by 1.5D.
            var layers = new List<(JsonNode Row, double Top, double End, int Index)>();
            double top = 0;
            foreach (var row in rows)
            {
                if (top >= l) break;
                double bottom = top + row.Required("spessore", strict: true);
                if (!double.IsFinite(bottom)) throw new ArgumentException("Spessore complessivo non finito.");
                // Only absorb accumulated floating-point addition error, not a real gap.
                if (Math.Abs(bottom - l) <= 16 * 2.2204460492503131e-16 * rows.Count * Math.Max(1, l)) bottom = l;
                if (row.S("tipologia") is not ("Coesivo" or "Granulare")) throw new ArgumentException("Tipologia del terreno non valida.");
                if (!row.B("laterale_attiva", true)) throw new ArgumentException("Strati disattivati non supportati dal modello orizzontale.");
                layers.Add((row!, top, Math.Min(bottom, l), layers.Count + 1)); top = bottom;
            }
            if (top < l) throw new ArgumentException("La stratigrafia deve coprire tutta la lunghezza infissa.");
            Clay = layers.All(s => s.Row.S("tipologia") == "Coesivo");
            Mixed = !Clay && layers.Any(s => s.Row.S("tipologia") == "Coesivo");
            if (Mixed && !distributed) throw new ArgumentException("Sequenze miste: selezionare Stratificato per il modello a reazioni distribuite.");
            Distributed = distributed || Clay;
            double sigma = 0; string? signature = null;
            foreach (var (row, a0, end, index) in layers)
            {
                bool clay = row.S("tipologia") == "Coesivo";
                double cu = clay ? row.Required("coesione_non_drenata", strict: true) : 0;
                double phi = clay ? 0 : row.Required("angolo_attrito");
                if (phi >= 60) throw new ArgumentException("Angolo d'attrito fuori dal campo ammesso [0, 60°). Il limite è un controllo d'input, non una soglia di Broms.");
                if (!clay && row.Required("coesione_efficace") != 0) throw new ArgumentException("Terreno c–φ non supportato: c′ deve essere zero.");
                // Cohesive overburden also loads any granular layers below it.
                // In an entirely cohesive profile, weights do not enter the model.
                double gamma = Clay ? 0 : row.Required("peso_specifico", strict: true);
                double saturated = !Clay && water && zw < end ? row.Required("peso_specifico_saturo", strict: true) : gamma;
                if (!Clay && water && zw < end && saturated <= 9.81) throw new ArgumentException("γsat deve essere maggiore di γw = 9,81 kN/m³.");
                string sig = clay ? "C|" + cu.ToString("R", CultureInfo.InvariantCulture) : FormattableString.Invariant($"G|{phi:R}|{gamma:R}|{saturated:R}");
                if (signature is not null && signature != sig) Uniform = false;
                signature ??= sig;
                var breaks = new SortedSet<double> { a0, end };
                if (clay && 1.5 * d > a0 && 1.5 * d < end) breaks.Add(1.5 * d);
                if (!Clay && water && zw > a0 && zw < end) breaks.Add(zw);
                var nodes = breaks.ToArray(); double kp = PassivePressureCoefficient(phi);
                for (int i = 0; i < nodes.Length - 1; i++)
                {
                    double a = nodes[i], b = nodes[i + 1], effective = water && a >= zw ? saturated - (Clay ? 0 : 9.81) : gamma;
                    double p = clay ? a < 1.5 * d ? 0 : 9 * cu * d : 3 * kp * d * sigma;
                    double slope = clay ? 0 : 3 * kp * d * effective;
                    Segments.Add(new(a, b, p, slope, index, row.S("tipologia"), sigma));
                    sigma += effective * (b - a);
                }
            }
            if (!Clay && water && zw > 0 && zw < l) Uniform = false;
            if (!(Q(l) > 0) || !double.IsFinite(Q(l)) || !double.IsFinite(S(l)))
                throw new ArgumentException("Nessuna resistenza laterale finita mobilitabile (per coesivi occorre L > 1,5D).");
        }

        public double Root(Func<double, double> f, double lo, double hi)
        {
            double a = f(lo), b = f(hi);
            if (!double.IsFinite(a) || !double.IsFinite(b) || Math.Sign(a) == Math.Sign(b) && a != 0 && b != 0)
                throw new ArgumentException("Radice non delimitata: meccanismo non ammissibile.");
            if (a == 0) return lo; if (b == 0) return hi;
            // Keep equilibrium checks reliable even with a coarse requested tolerance.
            double epsilon = Math.Min(Tolerance, 1e-12) * Math.Max(1, hi - lo);
            for (int i = 0; i < 100; i++)
            {
                double mid = lo + (hi - lo) / 2, c = f(mid);
                if (!double.IsFinite(c)) throw new ArgumentException("Soluzione non finita.");
                if (c == 0 || hi - lo <= epsilon) return mid;
                if (Math.Sign(c) == Math.Sign(a)) { lo = mid; a = c; } else hi = mid;
            }
            throw new ArgumentException("Superato il limite di 100 iterazioni.");
        }

        // PileChecker's FindDepthForForce, with zero-area intervals and a stable
        // quadratic inverse: y = 2*area / (p0 + sqrt(p0² + 2*k*area)).
        public double Depth(double h)
        {
            double total = Q(L), cumulative = 0;
            if (!double.IsFinite(h) || h < 0 || h > total * (1 + 1e-12)) throw new ArgumentException("Risultante fuori dal diagramma limite.");
            if (h == 0) return 0;
            foreach (var s in Segments)
            {
                double area = s.Force(s.Bottom);
                if (area > 0 && h <= cumulative + area)
                {
                    double remaining = Math.Max(0, h - cumulative);
                    double dz = s.Slope == 0 ? remaining / s.P0 : 2 * remaining / (s.P0 + Math.Sqrt(s.P0 * s.P0 + 2 * s.Slope * remaining));
                    return Math.Clamp(s.Top + dz, s.Top, s.Bottom);
                }
                cumulative += area;
            }
            return L; // Only endpoint round-off remains after the range check.
        }

        public (double Couple, double Switch) Tail(double start, double end)
        {
            if (end <= start) return (0, start);
            double mid = Math.Clamp(Depth((Q(start) + Q(end)) / 2), start, end);
            return (S(end) + S(start) - 2 * S(mid), mid);
        }

        public JsonArray LimitDiagram() => new(Segments.Select(s => (JsonNode)J.Obj(
            ("strato", s.LayerIndex), ("tipologia", s.Kind), ("da_m", s.Top), ("a_m", s.Bottom),
            ("sigma_eff_iniziale_kpa", s.EffectiveStressAtTop), ("p_iniziale_kn_m", s.P0),
            ("pendenza_kn_m2", s.Slope), ("risultante_kn", s.Force(s.Bottom)),
            ("momento_primo_knm", s.First(s.Bottom)))).ToArray());
    }
}
