using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace X.Core;

/// <summary>Broms: Viggiani, Fondazioni, pp. 400-415. Extensions and limits: docs/palo-orizzontale.md.</summary>
public static class PaloOrizzontale
{
    public const string Module = "geo_palo_orizzontale";
    public const string Source = "Viggiani, Fondazioni, pp. 400–415 (PDF 205–212), §§13.2.2–13.2.5";
    public static JsonObject Defaults() => J.Obj(("versione_orizzontale", 1),
        ("generali", J.Obj(("diametro", "1"), ("lunghezza", "10"), ("eccentricita", "0"),
            ("vincolo", "Libera"), ("modalita", "Omogeneo"), ("azione_orizzontale", "100"),
            ("presenza_falda", false), ("profondita_falda", "0"), ("origine_momento", "Sezione c.a."),
            ("momento_resistente", "1000"), ("provenienza_momento", ""), ("azione_assiale", "0"),
            ("passo", "0.10"), ("tolleranza", "1e-8"))),
        ("sezione", SezioneCA.DefaultInput()),
        ("verifica", J.Obj(("applica_fattori", false), ("xi", "1"), ("gamma_r", "1"), ("riferimento", ""))),
        ("stratigrafie", new JsonArray(new JsonArray())));

    public static JsonObject Layer() => J.Obj(("tipologia", "Granulare"), ("spessore", "10"),
        ("peso_specifico", "18"), ("peso_specifico_saturo", "20"), ("angolo_attrito", "30"),
        ("coesione_non_drenata", "50"), ("coesione_efficace", "0"));

    public static void ValidateShape(JsonObject data)
    {
        if (data.D("versione_orizzontale") != 1 || data["generali"] is not JsonObject ||
            data["sezione"] is not JsonObject || data["verifica"] is not JsonObject ||
            data["stratigrafie"] is not JsonArray surveys || surveys.Count == 0 ||
            surveys.Any(s => s is not JsonArray a || a.Any(r => r is not JsonObject)))
            throw new ArgumentException("Formato del foglio orizzontale non valido.");
        foreach (var key in new[] { "generali", "sezione", "verifica" })
            if (data[key]!.AsObject().Any(p => p.Value is null or JsonObject or JsonArray))
                throw new ArgumentException("Parametri del foglio orizzontale non validi.");
        if (data["generali"]!["presenza_falda"] is not JsonValue f || !f.TryGetValue<bool>(out _) ||
            data["verifica"]!["applica_fattori"] is not JsonValue v || !v.TryGetValue<bool>(out _))
            throw new ArgumentException("Falda e applicazione fattori richiedono valori vero/falso.");
    }

    public static JsonObject Section(JsonObject data)
    {
        var g = data["generali"]!;
        double d = g.Required("diametro", strict: true);
        double axial = Signed(g, "azione_assiale");
        var input = (JsonObject)data["sezione"]!.DeepClone();
        input["shape"] = "Circolare"; input["diameter_mm"] = d * 1000;
        double barCount = input.D("longitudinal_bar_count", 16);
        if (barCount < 4 || barCount > 512 || barCount % 2 != 0)
            throw new ArgumentException("Calcolo automatico: da 4 a 512 barre, in numero pari, per una sezione simmetrica nel piano di flessione.");
        double Solve(int nr, int na, out double depth, out SezioneCA engine)
        {
            engine = new SezioneCA(input, nr, na);
            // Restrict the strain domain to a neutral axis inside the section.
            // This avoids extending the existing engine into the all-compressed strain domain.
            double lo = d * .001, hi = d * 1000;
            if (axial <= engine.Profile(lo, 0).N || axial >= engine.Profile(hi, 0).N)
                throw new ArgumentException("N fuori dal campo del calcolo automatico (asse neutro interno). Inserire un momento resistente da analisi dedicata.");
            for (int i = 0; i < 65; i++) { double mid = (lo + hi) / 2; if (engine.Profile(mid, 0).N < axial) lo = mid; else hi = mid; }
            depth = (lo + hi) / 2;
            return engine.Profile(depth, 0).M;
        }
        double coarse = Solve(28, 96, out _, out _), moment = Solve(56, 192, out double x, out var section);
        double delta = Math.Abs(moment - coarse) / Math.Max(moment, 1e-9);
        if (!(moment > 0) || delta > .02) throw new ArgumentException("Momento della sezione non convergente al raffinamento (scarto > 2%). Usare un'analisi dedicata.");
        return J.Obj(("momento_knm", moment), ("n_kn", axial), ("asse_neutro_mm", x),
            ("residuo_n_kn", section.Profile(x, 0).N - axial), ("scarto_mesh", delta),
            ("fcd_mpa", section.Fcd), ("fyd_mpa", section.Fyd), ("area_acciaio_mm2", section.AreaSteel),
            ("outline", section.Outline), ("bars", section.Bars.Select(b => new[] { b.X, b.Y, b.Area, b.Diametro })),
            ("modello", "Sezione circolare: deformazioni piane, CLS parabola-rettangolo senza trazione, acciaio elastico-perfettamente plastico; asse neutro interno; N costante. Non verifica la duttilità della cerniera."));
    }

    private static double Signed(JsonNode n, string key) => J.Number(n[key]) ?? throw new ArgumentException(key + ": numero finito richiesto.");
    private sealed record Segment(double Top, double Bottom, double P0, double Slope)
    {
        public double Force(double z) { double t = Math.Clamp(z - Top, 0, Bottom - Top); return P0 * t + Slope * t * t / 2; }
        public double First(double z) { double t = Math.Clamp(z - Top, 0, Bottom - Top); return Top * Force(z) + P0 * t * t / 2 + Slope * t * t * t / 3; }
    }
    private sealed class Ground
    {
        public List<Segment> Segments { get; } = [];
        public bool Clay { get; private set; }
        public bool Uniform { get; private set; } = true;
        public double L { get; }
        public double Tolerance { get; }
        public double Q(double z) => Segments.Sum(s => s.Force(z));
        public double S(double z) => Segments.Sum(s => s.First(z));
        public double A(double z) => z * Q(z) - S(z);
        public double P(double z) { var s = Segments.FirstOrDefault(s => z >= s.Top && z < s.Bottom); return s is null ? 0 : s.P0 + s.Slope * (z - s.Top); }
        public Ground(JsonArray rows, double l, double d, bool water, double zw, double tolerance)
        {
            L = l; Tolerance = tolerance;
            if (rows.Count == 0) throw new ArgumentException("Inserire almeno uno strato.");
            string kind = rows[0].S("tipologia");
            if (kind is not ("Coesivo" or "Granulare")) throw new ArgumentException("Tipologia del terreno non valida.");
            Clay = kind == "Coesivo";
            double top = 0, sigma = 0; string? signature = null;
            foreach (var row in rows)
            {
                double thick = row.Required("spessore", strict: true), bottom = top + thick;
                if (top >= l) break;
                if (row.S("tipologia") != kind) throw new ArgumentException("Sequenze miste coesivo/granulare non supportate: manca un modello validato.");
                if (row.B("laterale_attiva", true) == false) throw new ArgumentException("Strati disattivati non supportati dal modello orizzontale.");
                double cu = Clay ? row.Required("coesione_non_drenata", strict: true) : 0;
                double phi = Clay ? 0 : row.Required("angolo_attrito");
                if (phi >= 60) throw new ArgumentException("Angolo d'attrito fuori dal campo ammesso [0, 60°). Il limite è un controllo d'input, non una soglia di Broms.");
                if (!Clay && row.Required("coesione_efficace") != 0) throw new ArgumentException("Terreno c–φ non supportato: c′ deve essere zero.");
                double gamma = Clay ? 0 : row.Required("peso_specifico", strict: true);
                double saturated = !Clay && water && zw < Math.Min(bottom, l) ? row.Required("peso_specifico_saturo", strict: true) : gamma;
                if (!Clay && water && zw < Math.Min(bottom, l) && saturated <= 9.81) throw new ArgumentException("γsat deve essere maggiore di γw = 9,81 kN/m³.");
                string sig = Clay ? cu.ToString("R", CultureInfo.InvariantCulture) : $"{phi:R}|{gamma:R}|{saturated:R}";
                if (signature is not null && signature != sig) Uniform = false; signature ??= sig;
                double end = Math.Min(bottom, l), kp = (1 + Math.Sin(phi * Math.PI / 180)) / (1 - Math.Sin(phi * Math.PI / 180));
                var breaks = new SortedSet<double> { top, end };
                if (Clay && 1.5 * d > top && 1.5 * d < end) breaks.Add(1.5 * d);
                if (!Clay && water && zw > top && zw < end) breaks.Add(zw);
                var nodes = breaks.ToArray();
                for (int i = 0; i < nodes.Length - 1; i++)
                {
                    double a = nodes[i], b = nodes[i + 1], effective = water && a >= zw ? saturated - 9.81 : gamma;
                    double p = Clay ? a < 1.5 * d ? 0 : 9 * cu * d : 3 * kp * d * sigma;
                    double slope = Clay ? 0 : 3 * kp * d * effective;
                    Segments.Add(new(a, b, p, slope)); sigma += effective * (b - a);
                }
                top = bottom;
            }
            if (top < l - 1e-9) throw new ArgumentException("La stratigrafia deve coprire tutta la lunghezza infissa.");
            if (!Clay && water && zw > 0 && zw < l) Uniform = false;
            if (Q(l) <= 0) throw new ArgumentException("Nessuna resistenza laterale mobilitabile (per coesivi occorre L > 1,5D).");
        }
        public double Root(Func<double, double> f, double lo, double hi)
        {
            double a = f(lo), b = f(hi);
            if (!double.IsFinite(a) || !double.IsFinite(b) || a * b > 0) throw new ArgumentException("Radice non delimitata: meccanismo non ammissibile.");
            if (Math.Abs(a) < 1e-12) return lo; if (Math.Abs(b) < 1e-12) return hi;
            for (int i = 0; i < 100; i++)
            {
                double mid = (lo + hi) / 2, c = f(mid);
                if (!double.IsFinite(c)) throw new ArgumentException("Soluzione non finita.");
                if (c == 0 || hi - lo <= Tolerance * Math.Max(1, L)) return mid;
                if (Math.Sign(c) == Math.Sign(a)) { lo = mid; a = c; } else hi = mid;
            }
            throw new ArgumentException("Superato il limite di 100 iterazioni.");
        }
        public double Depth(double h) => Root(z => Q(z) - h, 0, L);
        public (double Couple, double Switch) Tail(double start, double end)
        {
            double mid = Root(z => Q(z) - (Q(start) + Q(end)) / 2, start, end);
            return (S(end) + S(start) - 2 * S(mid), mid);
        }
    }

    public static JsonObject Calculate(JsonObject data)
    {
        try
        {
            ValidateShape(data); var g = data["generali"]!;
            double l = g.Required("lunghezza", strict: true), d = g.Required("diametro", strict: true), e = g.Required("eccentricita");
            double ed = g.Required("azione_orizzontale"), step = g.Required("passo", strict: true), tol = g.Required("tolleranza", strict: true);
            if (tol < 1e-12 || tol > 1e-5 || l / step > 20000) throw new ArgumentException("Tolleranza ammessa 1e-12…1e-5; massimo 20.000 intervalli di diagramma.");
            string restraint = g.S("vincolo"), mode = g.S("modalita");
            if (restraint is not ("Libera" or "Impedita") || mode is not ("Omogeneo" or "Multistrato sperimentale")) throw new ArgumentException("Vincolo o modalità non riconosciuti.");
            bool fixedHead = restraint == "Impedita", water = g.B("presenza_falda");
            if (fixedHead && e != 0) throw new ArgumentException("Testa impedita: il modello richiede vincolo e forza al piano campagna (e = 0).");
            if (g.AsObject().ContainsKey("momento_applicato") && Signed(g, "momento_applicato") != 0) throw new ArgumentException("Momento indipendente non supportato; il solo momento applicato è H·e.");
            double zw = water ? g.Required("profondita_falda") : l;
            _ = Signed(g, "azione_assiale");
            JsonObject? section = null; double my;
            if (g.S("origine_momento") == "Sezione c.a.") { section = Section(data); my = section.D("momento_knm"); }
            else if (g.S("origine_momento") == "Manuale")
            {
                my = g.Required("momento_resistente", strict: true);
                if (string.IsNullOrWhiteSpace(g.S("provenienza_momento"))) throw new ArgumentException("Indicare natura e provenienza del momento resistente manuale.");
            }
            else throw new ArgumentException("Origine del momento resistente non riconosciuta.");
            var results = new JsonArray();
            foreach (var survey in data.Array("stratigrafie"))
            {
                var ground = new Ground(survey!.AsArray(), l, d, water, zw, tol);
                if (mode == "Omogeneo" && !ground.Uniform) throw new ArgumentException("Parametri variabili o falda interna: selezionare Multistrato sperimentale.");
                results.Add(Solve(ground, e, my, fixedHead, step));
            }
            double hu = results.Min(r => r.D("capacita_kn")); int governing = results.Select(r => r.D("capacita_kn")).ToList().IndexOf(hu);
            var verification = data["verifica"]!; double? rk = null, rd = null;
            if (verification.B("applica_fattori"))
            {
                double xi = verification.Required("xi", 1), gamma = verification.Required("gamma_r", 1);
                if (string.IsNullOrWhiteSpace(verification.S("riferimento"))) throw new ArgumentException("Documentare il criterio dei fattori manuali e la natura delle azioni.");
                rk = hu / xi; rd = rk / gamma;
            }
            var warnings = new List<string> {
                "Palo singolo, capacità ultima al primo ordine; spostamenti, gruppo, ciclicità, taglio, secondo ordine e duttilità delle cerniere non verificati.",
                "Verifica normativa NTC/EC2 incompleta: nessuna conformità automatica. Il confronto con fattori manuali è un'elaborazione del progettista.",
                "I diagrammi si riferiscono alla capacità ultima, non all'azione inserita. N rimane costante; cresce H con momento H·e." };
            if (mode != "Omogeneo") warnings.Add("MULTISTRATO SPERIMENTALE: estensione integrale ANTHEA, non formula originale Broms né validazione indipendente per stratificazioni reali.");
            if (results.Any(r => !r.B("coesivo"))) warnings.Add("Terreno granulare: chiusura di equilibrio con risultante concentrata F indicata separatamente. Il completamento sotto la cerniera è idealizzato, non univoco.");
            if (section is not null) warnings.Add(section.S("modello"));
            return J.Obj(("errore", ""), ("versione_motore", "Broms-ANTHEA-1"), ("fonte", Source), ("input", data),
                ("capacita_kn", hu), ("sondaggio_governante", governing + 1), ("meccanismo", results[governing].S("meccanismo")),
                ("momento_resistente_knm", my), ("sezione", section), ("sondaggi", results), ("resistenza_caratteristica_manuale_kn", rk),
                ("resistenza_progetto_manuale_kn", rd), ("azione_kn", ed), ("rapporto_meccanico", ed / hu),
                ("utilizzo_manuale", rd is double resistance ? ed / resistance : null),
                ("esito_manuale", rd is double value ? ed <= value ? "Soddisfatto con fattori manuali" : "Non soddisfatto con fattori manuali" : "Non eseguito"),
                ("verifica_normativa", "Incompleta"), ("sperimentale", mode != "Omogeneo"),
                ("percorso", "Incremento di H con e costante; M applicato = H·e; N costante"), ("avvisi", warnings));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException or OverflowException)
        { return J.Error(ex.Message); }
    }

    private static JsonObject Solve(Ground soil, double e, double my, bool fixedHead, double step)
    {
        double l = soil.L, q = soil.Q(l), hShort, hIntermediate = double.PositiveInfinity;
        double Peak(double h) { double z = soil.Depth(h); return h * e + soil.S(z); }
        if (soil.Clay)
        {
            double Balance(double h, double m0) { double z = soil.Depth(h); return m0 + h * e + soil.S(z) - soil.Tail(z, l).Couple; }
            hShort = fixedHead ? q : soil.Root(h => Balance(h, 0), 0, q);
            if (fixedHead && soil.S(l) > my) hIntermediate = soil.Root(h => Balance(h, -my), 0, q);
        }
        else
        {
            hShort = fixedHead ? q : soil.A(l) / (l + e);
            if (fixedHead && soil.S(l) > my) hIntermediate = (my + soil.A(l)) / l;
        }
        double target = fixedHead ? 2 * my : my;
        double hLong = Peak(q) > target ? soil.Root(h => Peak(h) - target, 0, q) : double.PositiveInfinity;
        double h = Math.Min(hShort, Math.Min(hIntermediate, hLong));
        if (!(h > 0) || !double.IsFinite(h)) throw new ArgumentException("Capacità nulla o non finita.");
        string mechanism = h == hShort ? "Corto" : h == hIntermediate ? "Intermedio" : "Lungo";
        double m0 = fixedHead ? mechanism == "Corto" ? -soil.S(l) : -my : h * e;
        double zMax = soil.Depth(h), end = l, change = l, tip = 0;
        if (soil.Clay)
        {
            if (mechanism != "Corto" || !fixedHead)
            {
                double peak = m0 + soil.S(zMax);
                if (mechanism == "Lungo") end = soil.Root(t => soil.Tail(zMax, t).Couple - peak, zMax, l);
                change = soil.Tail(zMax, end).Switch;
            }
        }
        else
        {
            if (mechanism == "Lungo") end = soil.Root(z => m0 + h * z - soil.A(z), zMax, l);
            tip = soil.Q(end) - h;
        }
        (double P, double V, double M) State(double z, bool after = false)
        {
            if (z > end || (z == end && after)) return (0, 0, 0);
            if (!soil.Clay) return (soil.P(z), h - soil.Q(z), m0 + h * z - soil.A(z));
            double qr = z <= change ? soil.Q(z) : 2 * soil.Q(change) - soil.Q(z);
            double sr = z <= change ? soil.S(z) : 2 * soil.S(change) - soil.S(z);
            return ((z < change ? 1 : -1) * soil.P(z), h - qr, m0 + h * z - z * qr + sr);
        }
        var final = State(end); double residualForce = final.V + tip, residualMoment = final.M;
        double maxMoment = Math.Max(Math.Abs(m0), Math.Abs(State(zMax).M));
        double residualScale = Math.Max(1, Math.Max(my, q * l));
        if (Math.Abs(residualForce) > 1e-5 * Math.Max(1, q) || Math.Abs(residualMoment) > 1e-5 * residualScale || maxMoment > my * (1 + 1e-5))
            throw new ArgumentException("Equilibrio o limite di momento non soddisfatto: soluzione non ammissibile.");
        var depths = new SortedSet<double> { 0, l, zMax, end, change };
        foreach (var s in soil.Segments) { depths.Add(s.Top); depths.Add(s.Bottom); }
        for (int i = 1; i < Math.Ceiling(l / step); i++) depths.Add(i * step);
        var diagram = new JsonArray();
        foreach (double z in depths)
        {
            // Preserve two sides only at actual jumps, not at each plotting sample.
            var before = State(Math.Max(0, z - 1e-10 * Math.Max(1, l)));
            var current = State(z, z == end);
            bool jump = z > 0 && (Math.Abs(before.P - current.P) > 1e-7 * Math.Max(1, Math.Abs(before.P)) || Math.Abs(before.V - current.V) > 1e-7 * Math.Max(1, q));
            if (jump) diagram.Add(J.Obj(("z", z), ("lato", "prima"), ("p_kn_m", before.P), ("v_kn", before.V), ("m_knm", before.M)));
            diagram.Add(J.Obj(("z", z), ("lato", jump ? "dopo" : "nodo"), ("p_kn_m", current.P), ("v_kn", current.V), ("m_knm", current.M)));
        }
        var candidates = new JsonArray();
        void Candidate(string name, double value) => candidates.Add(J.Obj(("meccanismo", name),
            ("capacita_kn", double.IsFinite(value) ? value : null), ("governante", name == mechanism),
            ("stato", name == mechanism ? "Ammissibile, governante" : double.IsFinite(value) ? "Candidato non governante; limite precedente già raggiunto" : "Meccanismo non attivabile nel campo di lunghezza")));
        Candidate("Corto", hShort); if (fixedHead) Candidate("Intermedio", hIntermediate); Candidate("Lungo", hLong);
        var hinges = new JsonArray(); if (fixedHead && mechanism != "Corto") hinges.Add(0); if (mechanism == "Lungo") hinges.Add(zMax);
        return J.Obj(("capacita_kn", h), ("meccanismo", mechanism), ("coesivo", soil.Clay), ("candidati", candidates),
            ("momento_testa_knm", m0), ("momento_massimo_knm", maxMoment), ("quota_momento_massimo_m", Math.Abs(m0) >= Math.Abs(State(zMax).M) ? 0 : zMax),
            ("quota_taglio_nullo_m", zMax), ("cerniere_m", hinges), ("fine_reazioni_m", end),
            ("risultante_concentrata_kn", tip), ("quota_risultante_m", end), ("inversione_reazioni_m", soil.Clay ? change : null),
            ("residuo_forza_kn", residualForce), ("residuo_momento_knm", residualMoment), ("convergenza", "Raggiunta"), ("diagrammi", diagram));
    }

    public static string Csv(JsonObject result)
    {
        var text = new StringBuilder("sondaggio;z_m;lato;p_kN_m;V_kN;M_kNm\r\n"); int index = 0;
        foreach (var survey in result.Array("sondaggi"))
        {
            index++; foreach (var row in survey.Array("diagrammi")) text.AppendLine(string.Join(';', new[] { index.ToString(), row.D("z").ToString("R", CultureInfo.InvariantCulture), row.S("lato"), row.D("p_kn_m").ToString("R", CultureInfo.InvariantCulture), row.D("v_kn").ToString("R", CultureInfo.InvariantCulture), row.D("m_knm").ToString("R", CultureInfo.InvariantCulture) }));
        }
        return text.ToString();
    }
}
