using System.Text.Json.Nodes;

namespace Anthea.Calculations;

public static partial class PaloOrizzontale
{
    public static JsonArray References() => new(
        Reference("Pacini", "G. Pacini — approccio al terreno stratificato", "Checker/PileCalculator.cs; diagrammi per strato e FindDepthForForce", "", "Origine dell'estensione stratificata. Equilibrio globale e chiusura distribuita implementati in ANTHEA."),
        Reference("Viggiani", "C. Viggiani, Fondazioni", "§§13.2.2–13.2.5, pp. 400–415 del testo fornito", "", "Leggi locali e meccanismi classici; non attribuisce al testo l'estensione mista."),
        Reference("Broms-C", "B. B. Broms (1964), Lateral Resistance of Piles in Cohesive Soils", "ASCE, Journal of the Soil Mechanics and Foundations Division, 90(SM2)", "https://doi.org/10.1061/JSFEAQ.0000611", "Riferimento originale per i terreni coesivi, ripreso attraverso Viggiani."),
        Reference("Broms-G", "B. B. Broms (1964), Lateral Resistance of Piles in Cohesionless Soils", "ASCE, Journal of the Soil Mechanics and Foundations Division, 90(SM3)", "https://doi.org/10.1061/JSFEAQ.0000614", "Riferimento originale per i granulari e la chiusura concentrata, ripreso attraverso Viggiani e Wood."),
        Reference("NZGS", "J. Wood (2021), Cantilever Pole Retaining Walls", "New Zealand Geotechnical Society, Geomechanics News 101, 22 giugno 2021; §§2.2–2.3, 2.5, 2.8", "https://www.nzgs.org/libraries/cantilever-pole-retaining-walls/", "Broms Modified: reazioni opposte distribuite per pali corti rigidi in terreno granulare. Supporta questa ipotesi, non valida l'estensione mista o la chiusura sotto la cerniera dei pali lunghi. Il salto di segno è un'idealizzazione limite, non una legge pressione-spostamento."),
        Reference("FHWA", "FHWA (2018), Geotechnical Engineering Circular No. 9", "Design, Analysis, and Testing of Laterally Loaded Deep Foundations that Support Transportation Facilities; FHWA-HIF-18-031, §§6.3 e 6.5", "https://www.fhwa.dot.gov/engineering/geotech/pubs/hif18031.pdf", "Inquadramento dei metodi p-y, della deformabilità e dei limiti di Broms. Le curve p-y non sono implementate qui e richiedono calibrazione appropriata; non sono una validazione del presente modello.")
    );

    private static JsonObject Reference(string id, string title, string detail, string url, string use)
        => J.Obj(("id", id), ("titolo", title), ("dettaglio", detail), ("url", url), ("uso", use));

    // Diagnostics only: no local design factors and no changes to the capacity solver.
    // Pure clay can be calculated without unit weights; do not invent zero stresses.
    private static void AddGroundDiagnostics(JsonObject result, JsonArray layers, JsonNode general, Ground ground, double d)
    {
        bool water = general.B("presenza_falda"); double zw = general.D("profondita_falda");
        bool weightsAvailable = true; double top = 0;
        var intervals = new List<(double Top, double Bottom, double Gamma, double Sat)>();
        foreach (var layer in layers)
        {
            if (top >= ground.L) break;
            double end = Math.Min(ground.L, top + layer.D("spessore"));
            double gamma = J.Number(layer!["peso_specifico"]) ?? double.NaN;
            double sat = water && zw < end ? J.Number(layer["peso_specifico_saturo"]) ?? double.NaN : gamma;
            weightsAvailable &= double.IsFinite(gamma) && gamma > 0 && double.IsFinite(sat) && (!water || zw >= end || sat > 9.81);
            intervals.Add((top, end, gamma, sat)); top = end;
        }
        (double Total, double U, double Effective) Stress(double z)
        {
            double total = 0;
            foreach (var a in intervals)
            {
                double t = Math.Clamp(z - a.Top, 0, a.Bottom - a.Top);
                double dry = water ? Math.Clamp(zw - a.Top, 0, t) : t;
                total += a.Gamma * dry + a.Sat * (t - dry);
            }
            double u = water ? 9.81 * Math.Max(0, z - zw) : 0;
            return (total, u, total - u);
        }
        var points = new JsonArray();
        var nodes = new SortedSet<double>(ground.Segments.SelectMany(s => new[] { s.Top, s.Bottom }));
        foreach (var row in result.Array("diagrammi")) nodes.Add(row.D("z"));
        if (water && zw > 0 && zw < ground.L) nodes.Add(zw);
        foreach (double z in nodes)
        {
            // Paired endpoints retain jumps in the local law at every interface.
            foreach (bool before in z == 0 ? new[] { false } : z == ground.L ? new[] { true } : new[] { true, false })
            {
                var s = ground.Segments.First(v => before ? z > v.Top && z <= v.Bottom : z >= v.Top && z < v.Bottom);
                var stress = Stress(z); double p = ground.P(z, before);
                points.Add(J.Obj(("z", z), ("lato", before ? "prima" : "dopo"), ("strato", s.LayerIndex),
                    ("sigma_v_kpa", weightsAvailable ? stress.Total : null), ("u_kpa", stress.U),
                    ("sigma_eff_kpa", weightsAvailable ? stress.Effective : null),
                    ("q_lim_kpa", p / d), ("p_lim_kn_m", p), ("q_integrale_kn", ground.Q(z))));
            }
        }
        foreach (var row in result.Array("diagrammi")) row!["q_kpa"] = row.D("p_kn_m") / d;
        foreach (var segment in result.Array("diagramma_limite"))
            segment!["sigma_eff_iniziale_kpa"] = weightsAvailable ? Stress(segment.D("da_m")).Effective : null;
        result["tensioni_disponibili"] = weightsAvailable;
        result["nota_tensioni"] = weightsAvailable
            ? "σv e σ′v da peso proprio; u idrostatica, senza suzione. q_lim=p_lim/D; q=p/D è una pressione laterale equivalente. Nessuna riduzione locale con ξ o γR."
            : "σv e σ′v non disponibili: completare i pesi di volume. Nel profilo interamente coesivo la capacità dipende da Cu, non da questi pesi.";
        result["diagramma_terreno"] = points;
    }
}
