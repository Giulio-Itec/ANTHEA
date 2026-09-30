using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace Anthea.Calculations;

/// <summary>Broms: Viggiani, Fondazioni, pp. 400-415. Extensions and limits: supporto/docs/palo-orizzontale.md.</summary>
public static partial class PaloOrizzontale
{
    public const string Module = "geo_palo_orizzontale";
    public static double PassivePressureCoefficient(double phiDegrees)
    {
        if (!double.IsFinite(phiDegrees) || phiDegrees < 0 || phiDegrees >= 90)
            throw new ArgumentException("Angolo di attrito: atteso un valore finito fra 0° incluso e 90° escluso.");
        double sine = Math.Sin(phiDegrees * Math.PI / 180);
        return (1 + sine) / (1 - sine);
    }
    public const string Source = "Viggiani, Fondazioni, pp. 400–415 (PDF 205–212), §§13.2.2–13.2.5";
    public static JsonObject Defaults() => J.Obj(("versione_orizzontale", 1),
        ("generali", J.Obj(("diametro", "1"), ("lunghezza", "10"), ("eccentricita", "0"),
            ("vincolo", "Libera"), ("modalita", "Automatica"), ("metodo_calcolo", BromsMethod), ("azione_orizzontale", "100"),
            ("presenza_falda", false), ("profondita_falda", "0"), ("origine_momento", "Sezione c.a."),
            ("momento_resistente", "1000"), ("provenienza_momento", ""), ("azione_assiale", "0"),
            ("passo", "0.10"), ("tolleranza", "1e-8"))),
        ("sezione", SezioneCA.DefaultInput()),
        ("verifica", J.Obj(("verticali_indagate", "1"), ("efficienza_metodo", "Manuale"), ("efficienza_eta", "1"))),
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
        if (data["generali"]!["presenza_falda"] is not JsonValue f || !f.TryGetValue<bool>(out _))
            throw new ArgumentException("Presenza falda richiede un valore vero/falso.");
    }

    public static JsonObject Section(JsonObject data)
    {
        if (data.S("tipo_sezione") == "CHS") return MicropaloOrizzontale.Section(data);
        return HorizontalConcreteSection.Calculate(data);
    }

    private static double Signed(JsonNode n, string key) => J.Number(n[key]) ?? throw new ArgumentException(key + ": numero finito richiesto.");
    /// <summary>Uses the same active-depth properties and groundwater rules as the solver.</summary>
    public static string ModelloAutomatico(JsonObject data)
    {
        ValidateShape(data); var g = data["generali"]!;
        double l = g.Required("lunghezza", strict: true), d = g.Required("diametro", strict: true), tol = g.Required("tolleranza", strict: true);
        bool water = g.B("presenza_falda"); double zw = water ? g.Required("profondita_falda") : l;
        bool uniform = true, distributed = DistributedMethod(g);
        foreach (var survey in data.Array("stratigrafie"))
            uniform &= new Ground(survey!.AsArray(), l, d, water, zw, tol, distributed).Uniform;
        return ModelName(uniform, distributed);
    }
    private static string ModelName(bool uniform, bool distributed = false) => distributed
        ? "Diagramma stratificato · " + (uniform ? "terreno omogeneo" : "terreno multistrato")
        : uniform ? "Omogeneo" : "Multistrato sperimentale";

    public static JsonObject Efficiency(JsonObject data)
    {
        var v = data["verifica"]!;
        string method = v.S("efficienza_metodo", "Manuale");
        if (method == "Manuale")
        {
            double eta = v.AsObject().ContainsKey("efficienza_eta") ? v.Required("efficienza_eta", strict: true) : 1;
            if (eta > 1) throw new ArgumentException("Efficienza manuale ammessa: 0 < η ≤ 1.");
            return J.Obj(("metodo", method), ("eta", eta));
        }
        if (method != "Reese & Van Impe (foglio)") throw new ArgumentException("Metodo di efficienza non riconosciuto.");
        double d = data["generali"]!.Required("diametro", strict: true);
        double Distance(string key)
        {
            double s = v.Required(key, strict: true);
            if (s < d) throw new ArgumentException("Gli interassi dei pali devono essere almeno pari al diametro.");
            return s;
        }
        double front = Distance("interasse_anteriore"), back = Distance("interasse_posteriore"),
            left = Distance("interasse_sinistro"), right = Distance("interasse_destro");
        double a = Math.Min(1, .7 * Math.Pow(front / d, .26)), p = Math.Min(1, .48 * Math.Pow(back / d, .38)),
            sx = Math.Min(1, .64 * Math.Pow(left / d, .34)), dx = Math.Min(1, .64 * Math.Pow(right / d, .34));
        static double Diagonal(double longitudinal, double transverse, double axialEta, double sideEta)
        {
            double angle = Math.Atan2(transverse, longitudinal);
            return Math.Sqrt(Math.Pow(axialEta * Math.Cos(angle), 2) + Math.Pow(sideEta * Math.Sin(angle), 2));
        }
        double als = Diagonal(front, left, a, sx), ald = Diagonal(front, right, a, dx),
            pls = Diagonal(back, left, p, sx), pld = Diagonal(back, right, p, dx);
        return J.Obj(("metodo", method), ("eta", a * p * sx * dx * als * ald * pls * pld),
            ("anteriore", a), ("posteriore", p), ("sinistro", sx), ("destro", dx),
            ("anteriore_sinistro", als), ("anteriore_destro", ald), ("posteriore_sinistro", pls), ("posteriore_destro", pld));
    }

    public static JsonObject Calculate(JsonObject data)
    {
        try
        {
            ValidateShape(data);
            if (data["generali"].S("metodo_calcolo") == LegacyStratifiedMethod)
            {
                data = (JsonObject)data.DeepClone();
                data["generali"]!["metodo_calcolo"] = StratifiedMethod;
            }
            var g = data["generali"]!;
            double l = g.Required("lunghezza", strict: true), d = g.Required("diametro", strict: true), e = g.Required("eccentricita");
            double ed = g.Required("azione_orizzontale"), step = g.Required("passo", strict: true), tol = g.Required("tolleranza", strict: true);
            if (tol < 1e-12 || tol > 1e-5 || l / step > 20000) throw new ArgumentException("Tolleranza ammessa 1e-12…1e-5; massimo 20.000 intervalli di diagramma.");
            string restraint = g.S("vincolo");
            if (restraint is not ("Libera" or "Impedita")) throw new ArgumentException("Vincolo non riconosciuto.");
            bool fixedHead = restraint == "Impedita", water = g.B("presenza_falda");
            if (fixedHead && e != 0) throw new ArgumentException("Testa impedita: il modello richiede vincolo e forza al piano campagna (e = 0).");
            if (g.AsObject().ContainsKey("momento_applicato") && Signed(g, "momento_applicato") != 0) throw new ArgumentException("Momento indipendente non supportato; il solo momento applicato è H·e.");
            double zw = water ? g.Required("profondita_falda") : l;
            _ = Signed(g, "azione_assiale");
            JsonObject? section = null; double my;
            if (g.S("origine_momento") == (data.S("tipo_sezione") == "CHS" ? "Sezione CHS" : "Sezione c.a.")) { section = Section(data); my = section.D("momento_knm"); }
            else if (g.S("origine_momento") == "Manuale")
            {
                my = g.Required("momento_resistente", strict: true);
                if (string.IsNullOrWhiteSpace(g.S("provenienza_momento"))) throw new ArgumentException("Indicare natura e provenienza del momento resistente manuale.");
            }
            else throw new ArgumentException("Origine del momento resistente non riconosciuta.");
            var results = new JsonArray(); bool uniform = true, distributed = DistributedMethod(g);
            foreach (var survey in data.Array("stratigrafie"))
            {
                var ground = new Ground(survey!.AsArray(), l, d, water, zw, tol, distributed);
                uniform &= ground.Uniform;
                var surveyResult = Solve(ground, e, my, fixedHead, step);
                AddGroundDiagnostics(surveyResult, survey.AsArray(), g, ground, d);
                surveyResult["modello_adottato"] = ModelName(ground.Uniform, distributed); results.Add(surveyResult);
            }
            string mode = ModelName(uniform, distributed); // Legacy manual selections never override the actual profile.
            double hu = results.Min(r => r.D("capacita_kn")); int governing = results.Select(r => r.D("capacita_kn")).ToList().IndexOf(hu);
            var verification = data["verifica"]!;
            if (!Calcolo.Verticali.TryGetValue(verification.S("verticali_indagate", "1"), out var xi))
                throw new ArgumentException("Numero di verticali indagate non riconosciuto.");
            double mean = results.Average(r => r.D("capacita_kn"));
            double meanBranch = mean / xi.Xi3, minBranch = hu / xi.Xi4;
            var efficiency = Efficiency(data);
            double rk = Math.Min(meanBranch, minBranch), rd = rk / 1.3 * efficiency.D("eta");
            var warnings = new List<string> {
                "Palo singolo, capacità ultima al primo ordine; spostamenti, gruppo, ciclicità, taglio, secondo ordine e duttilità delle cerniere non verificati.",
                "Resistenza ridotta con ξ3, ξ4 e γR = 1,3. HEd deve essere un'azione di progetto. Le altre verifiche NTC/EC2 restano escluse: nessuna conformità complessiva automatica.",
                "I diagrammi si riferiscono alla capacità ultima, non all'azione inserita. N rimane costante; cresce H con momento H·e." };
            if (distributed) warnings.Add("MODELLO STRATIFICATO SPERIMENTALE: estensione anche per alternanze coesivo/granulare. Equilibrio globale con reazioni distribuite limitate da p_lim; nessuna validazione sperimentale indipendente. Nei granulari corto/intermedio non coincide con la chiusura concentrata di Broms.");
            else if (!uniform) warnings.Add("MULTISTRATO SPERIMENTALE: estensione integrale ANTHEA, non formula originale Broms né validazione indipendente per stratificazioni reali.");
            if (results.Any(r => r.S("chiusura_equilibrio") == "Concentrata")) warnings.Add("Terreno granulare: chiusura di equilibrio con risultante concentrata F indicata separatamente. Il completamento sotto la cerniera è idealizzato, non univoco.");
            if (section is not null) warnings.Add(section.S("modello"));
            if (efficiency.S("metodo") != "Manuale") warnings.Add("Efficienza dal foglio SMath: schema di otto pali interferenti (quattro allineati e quattro diagonali). Verificare ulteriori interferenze nelle maglie fitte; lo schema non rappresenta una palificata arbitraria. Distanze riferite alla direzione di H.");
            return J.Obj(("errore", ""), ("versione_motore", distributed ? "Stratificato-ANTHEA-1" : "Broms-ANTHEA-1"),
                ("metodo_calcolo", distributed ? StratifiedMethod : BromsMethod), ("fonte", distributed ? StratifiedSource + " Leggi locali: " + Source : Source), ("riferimenti", References()), ("input", data),
                ("capacita_kn", hu), ("sondaggio_governante", governing + 1), ("meccanismo", results[governing].S("meccanismo")),
                ("momento_resistente_knm", my), ("sezione", section), ("sondaggi", results), ("resistenza_caratteristica_manuale_kn", rk),
                ("resistenza_progetto_manuale_kn", rd), ("azione_kn", ed), ("rapporto_meccanico", ed / hu),
                ("capacita_media_kn", mean), ("xi3", xi.Xi3), ("xi4", xi.Xi4), ("gamma_r", 1.3), ("efficienza", efficiency),
                ("verticali_indagate", verification.S("verticali_indagate", "1")),
                ("ramo_media_kn", meanBranch), ("ramo_minimo_kn", minBranch),
                ("criterio_governante", meanBranch <= minBranch ? "Media / ξ3" : "Minimo / ξ4"),
                ("utilizzo_manuale", ed / rd),
                ("esito_manuale", ed <= rd ? "Verifica soddisfatta" : "Verifica non soddisfatta"),
                ("verifica_normativa", "Incompleta"), ("modello_adottato", mode), ("selezione_modello", "Automatica"), ("sperimentale", distributed || !uniform),
                ("percorso", "Incremento di H con e costante; M applicato = H·e; N costante"), ("avvisi", warnings));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException or OverflowException)
        { return J.Error(ex.Message); }
    }

    private static JsonObject Solve(Ground soil, double e, double my, bool fixedHead, double step)
    {
        double l = soil.L, q = soil.Q(l), hShort, hIntermediate = double.PositiveInfinity;
        double Peak(double h) { double z = soil.Depth(h); return h * e + soil.S(z); }
        if (soil.Distributed)
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
        if (soil.Distributed)
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
        (double P, double V, double M) State(double z, bool after = false, bool before = false)
        {
            if (z > end || (z == end && after)) return (0, 0, 0);
            if (!soil.Distributed) return (soil.P(z, before), h - soil.Q(z), m0 + h * z - soil.A(z));
            double qr = z <= change ? soil.Q(z) : 2 * soil.Q(change) - soil.Q(z);
            double sr = z <= change ? soil.S(z) : 2 * soil.S(change) - soil.S(z);
            return ((z < change || before && z == change ? 1 : -1) * soil.P(z, before), h - qr, m0 + h * z - z * qr + sr);
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
            var before = State(z, before: true);
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
        return J.Obj(("capacita_kn", h), ("meccanismo", mechanism), ("coesivo", soil.Clay), ("misto", soil.Mixed),
            ("chiusura_equilibrio", soil.Distributed ? "Distribuita" : "Concentrata"), ("diagramma_limite", soil.LimitDiagram()), ("candidati", candidates),
            ("momento_testa_knm", m0), ("momento_massimo_knm", maxMoment), ("quota_momento_massimo_m", Math.Abs(m0) >= Math.Abs(State(zMax).M) ? 0 : zMax),
            ("quota_taglio_nullo_m", zMax), ("cerniere_m", hinges), ("fine_reazioni_m", end),
            ("risultante_concentrata_kn", tip), ("quota_risultante_m", end), ("inversione_reazioni_m", soil.Distributed ? change : null),
            ("residuo_forza_kn", residualForce), ("residuo_momento_knm", residualMoment), ("convergenza", "Raggiunta"), ("diagrammi", diagram));
    }

    public static string Csv(JsonObject result)
    {
        var text = new StringBuilder("sondaggio;z_m;lato;p_kN_m;V_kN;M_kNm;q_kPa;p_lim_kN_m;q_lim_kPa;sigma_v_kPa;u_kPa;sigma_eff_kPa;Q_kN\r\n"); int index = 0;
        foreach (var survey in result.Array("sondaggi"))
        {
            var terrain = survey.Array("diagramma_terreno").ToLookup(r => r.D("z"));
            string Number(JsonNode? node, string key) => J.Number(node?[key]) is double n ? n.ToString("R", CultureInfo.InvariantCulture) : "";
            index++; foreach (var row in survey.Array("diagrammi"))
            {
                var candidates = terrain[row.D("z")];
                var ground = candidates.FirstOrDefault(r => r.S("lato") == row.S("lato")) ?? candidates.FirstOrDefault();
                text.AppendLine(string.Join(';', new[] { index.ToString(), Number(row, "z"), row.S("lato"), Number(row, "p_kn_m"), Number(row, "v_kn"), Number(row, "m_knm"), Number(row, "q_kpa"), Number(ground, "p_lim_kn_m"), Number(ground, "q_lim_kpa"), Number(ground, "sigma_v_kpa"), Number(ground, "u_kpa"), Number(ground, "sigma_eff_kpa"), Number(ground, "q_integrale_kn") }));
            }
        }
        return text.ToString();
    }
}
