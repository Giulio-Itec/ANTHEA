using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using GPC.Checkers.Geotechnics.Piles;
using GPC.Model.Geotechnics;
using GPC.Model.Standards;

namespace Anthea.Calculations;

/// <summary>
/// Foglio del palo sotto azione orizzontale (Broms, Viggiani pp. 400-415, ed estensione stratificata): il documento (m, kN, kPa, kN/m³, gradi) è
/// letto e controllato qui, la capacità è GPCChecker.Geotechnics (LateralPileCapacity, mm, N, MPa) con i terreni e i fattori di Model (NTC 2018
/// Tab. 6.4.IV e 6.4.VI); i risultati sono riportati nel formato del foglio. Limiti: supporto/docs/palo-orizzontale.md.
/// </summary>
public static partial class PaloOrizzontale
{
    public const string Module = "geo_palo_orizzontale";
    const double M = 1000, KPa = 1e-3, KN3 = 1e-6, Deg = Math.PI / 180, Gw = 9.81;
    static readonly StandardNTC2018Geotechnics Ntc = new();
    public static double PassivePressureCoefficient(double phiDegrees)
    {
        if (!double.IsFinite(phiDegrees) || phiDegrees < 0 || phiDegrees >= 90)
            throw new ArgumentException("Angolo di attrito: atteso un valore finito fra 0° incluso e 90° escluso.");
        return LateralPileCapacity.PassivePressureCoefficient(phiDegrees * Deg);
    }
    public const string Source = LateralPileCapacity.Source;
    public static JsonObject Defaults() => J.Obj(("versione_orizzontale", 1),
        ("generali", J.Obj(("diametro", "1"), ("lunghezza", "10"), ("eccentricita", "0"),
            ("vincolo", "Libera"), ("modalita", "Automatica"), ("metodo_calcolo", BromsMethod), ("azione_orizzontale", "100"),
            ("presenza_falda", false), ("profondita_falda", "0"), ("origine_momento", "Sezione c.a."),
            ("momento_resistente", "1000"), ("provenienza_momento", ""), ("azione_assiale", "0"),
            ("passo", "0.10"), ("tolleranza", "1e-8"))),
        ("sezione", SezioneCA.DefaultCatalogInput()),
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

    // Messaggi della libreria nelle parole del foglio.
    static readonly Dictionary<string, string> Messages = new()
    {
        ["Mixed sequences: select the stratified model with distributed reactions."] = "Sequenze miste: selezionare Stratificato per il modello a reazioni distribuite.",
        ["No finite lateral resistance can be mobilised (cohesive soils need L > 1.5D)."] = "Nessuna resistenza laterale finita mobilitabile (per coesivi occorre L > 1,5D).",
        ["Root not bracketed: inadmissible mechanism."] = "Radice non delimitata: meccanismo non ammissibile.",
        ["Non finite solution."] = "Soluzione non finita.",
        ["More than 100 iterations."] = "Superato il limite di 100 iterazioni.",
        ["Zero or infinite capacity."] = "Capacità nulla o non finita.",
        ["Equilibrium or moment limit not satisfied: inadmissible solution."] = "Equilibrio o limite di momento non soddisfatto: soluzione non ammissibile.",
        ["Resultant outside the limit diagram."] = "Risultante fuori dal diagramma limite.",
        ["The layers must cover the whole embedded length."] = "La stratigrafia deve coprire tutta la lunghezza infissa.",
        ["Resisting moment: a positive finite value is required."] = "Momento resistente: valore finito positivo richiesto."
    };
    static T Library<T>(Func<T> calculation)
    {
        try { return calculation(); }
        catch (ArgumentException ex) when (Messages.TryGetValue(ex.Message, out var message)) { throw new ArgumentException(message, ex); }
    }

    /// <summary>
    /// Una verticale del foglio come profilo di Model, con i controlli e i messaggi del foglio. Sono letti gli strati fino alla lunghezza infissa.
    /// Nel profilo interamente coesivo i pesi di volume non entrano nel modello e possono mancare; Weights dice se sono completi per le tensioni.
    /// </summary>
    private static (LateralPileSurvey Survey, bool Weights) Verticale(JsonArray rows, double l, bool water, double zw, bool distributed)
    {
        if (rows.Count == 0) throw new ArgumentException("Inserire almeno uno strato.");
        var layers = new List<(JsonNode Row, double Top, double Thickness, double End)>(); double top = 0;
        foreach (var row in rows)
        {
            if (top >= l) break;
            double thickness = row.Required("spessore", strict: true), bottom = top + thickness;
            if (!double.IsFinite(bottom)) throw new ArgumentException("Spessore complessivo non finito.");
            // Only absorb accumulated floating-point addition error, not a real gap.
            if (Math.Abs(bottom - l) <= 16 * 2.2204460492503131e-16 * rows.Count * Math.Max(1, l)) bottom = l;
            if (row.S("tipologia") is not ("Coesivo" or "Granulare")) throw new ArgumentException("Tipologia del terreno non valida.");
            if (!row.B("laterale_attiva", true)) throw new ArgumentException("Strati disattivati non supportati dal modello orizzontale.");
            layers.Add((row!, top, thickness, Math.Min(bottom, l))); top = bottom;
        }
        if (top < l) throw new ArgumentException("La stratigrafia deve coprire tutta la lunghezza infissa.");
        bool clay = layers.All(s => s.Row.S("tipologia") == "Coesivo"), mixed = !clay && layers.Any(s => s.Row.S("tipologia") == "Coesivo");
        if (mixed && !distributed) throw new ArgumentException("Sequenze miste: selezionare Stratificato per il modello a reazioni distribuite.");
        var list = new List<SoilLayer>(); var kinds = new List<SoilBehaviour>(); bool weights = true; double depth = 0;
        foreach (var (row, a0, thickness, end) in layers)
        {
            bool cohesive = row.S("tipologia") == "Coesivo";
            double? cu = cohesive ? row.Required("coesione_non_drenata", strict: true) : null;
            double phi = cohesive ? 0 : row.Required("angolo_attrito");
            if (phi >= 60) throw new ArgumentException("Angolo d'attrito fuori dal campo ammesso [0, 60°). Il limite è un controllo d'input, non una soglia di Broms.");
            if (!cohesive && row.Required("coesione_efficace") != 0) throw new ArgumentException("Terreno c–φ non supportato: c′ deve essere zero.");
            bool wet = water && zw < end;
            if (!clay)
            {
                row.Required("peso_specifico", strict: true);
                if (wet && row.Required("peso_specifico_saturo", strict: true) <= Gw) throw new ArgumentException("γsat deve essere maggiore di γw = 9,81 kN/m³.");
            }
            double gamma = J.Number(row["peso_specifico"]) ?? double.NaN, saturated = wet ? J.Number(row["peso_specifico_saturo"]) ?? double.NaN : gamma;
            if (!clay && wet && saturated < gamma) throw new ArgumentException("γsat deve essere almeno pari a γ (terreno di Model).");
            weights &= double.IsFinite(gamma) && gamma > 0 && double.IsFinite(saturated) && (!wet || saturated > Gw);
            // Profilo interamente coesivo senza pesi: pesi di riferimento, non usati dal modello; le tensioni non sono riportate.
            double g = gamma > 0 ? gamma : 18, gs = wet ? (saturated >= g ? saturated : g) : J.Number(row["peso_specifico_saturo"]) is double s && s >= g ? s : g;
            list.Add(new SoilLayer(new Soil("Strato " + (list.Count + 1), g * KN3, gs * KN3, phi * Deg, 0, "ANTHEA", undrainedShearStrength: cu * KPa), -depth * M, -(depth + thickness) * M));
            kinds.Add(cohesive ? SoilBehaviour.Cohesive : SoilBehaviour.Granular); depth += thickness;
        }
        return (new LateralPileSurvey(new SoilProfile("Verticale", list, "ANTHEA", water ? -zw * M : null), kinds), weights);
    }

    /// <summary>Uses the same active-depth properties and groundwater rules as the solver.</summary>
    public static string ModelloAutomatico(JsonObject data)
    {
        ValidateShape(data); var g = data["generali"]!;
        double l = g.Required("lunghezza", strict: true), d = g.Required("diametro", strict: true); g.Required("tolleranza", strict: true);
        bool water = g.B("presenza_falda"); double zw = water ? g.Required("profondita_falda") : l;
        bool distributed = DistributedMethod(g);
        var surveys = data.Array("stratigrafie").Select(s => Verticale(s!.AsArray(), l, water, zw, distributed).Survey).ToArray();
        return Library(() => LateralPileCapacity.ModelName(surveys, l * M, d * M, distributed ? LateralPileMethod.Stratified : LateralPileMethod.Broms));
    }

    /// <summary>Efficienza del gruppo dal foglio con la libreria (LateralGroupEfficiency): assegnata 0 &lt; η ≤ 1, o Reese e Van Impe (otto pali).</summary>
    private static (JsonObject Json, LateralGroupEfficiency Efficiency) Gruppo(JsonObject data)
    {
        var v = data["verifica"]!;
        string method = v.S("efficienza_metodo", "Manuale");
        if (method == "Manuale")
        {
            double eta = v.AsObject().ContainsKey("efficienza_eta") ? v.Required("efficienza_eta", strict: true) : 1;
            if (eta > 1) throw new ArgumentException("Efficienza manuale ammessa: 0 < η ≤ 1.");
            var manual = LateralGroupEfficiency.Manual(eta);
            return (J.Obj(("metodo", method), ("eta", manual.Eta)), manual);
        }
        if (method != "Reese & Van Impe (foglio)") throw new ArgumentException("Metodo di efficienza non riconosciuto.");
        double d = data["generali"]!.Required("diametro", strict: true);
        double Distance(string key)
        {
            double s = v.Required(key, strict: true);
            if (s < d) throw new ArgumentException("Gli interassi dei pali devono essere almeno pari al diametro.");
            return s;
        }
        double front = Distance("interasse_anteriore"), back = Distance("interasse_posteriore"), left = Distance("interasse_sinistro"), right = Distance("interasse_destro");
        var e = LateralGroupEfficiency.ReeseVanImpeEfficiency(d * M, front * M, back * M, left * M, right * M);
        return (J.Obj(("metodo", method), ("eta", e.Eta), ("anteriore", e.Front), ("posteriore", e.Back), ("sinistro", e.Left), ("destro", e.Right),
            ("anteriore_sinistro", e.FrontLeft), ("anteriore_destro", e.FrontRight), ("posteriore_sinistro", e.BackLeft), ("posteriore_destro", e.BackRight)), e);
    }
    public static JsonObject Efficiency(JsonObject data) => Gruppo(data).Json;

    static readonly string[] Mechanisms = ["Corto", "Intermedio", "Lungo"];
    static readonly string[] Sides = ["prima", "dopo", "nodo"];

    /// <summary>Una verticale del risultato nel formato del foglio: m, kN, kN/m, kN/m², kNm, kPa.</summary>
    private static JsonObject Verticale(LateralSurveyResult r, double d, bool weights)
    {
        string mechanism = Mechanisms[(int)r.Mechanism];
        var limit = new JsonArray(r.LimitDiagram.Select(s => (JsonNode)J.Obj(("strato", s.Layer), ("tipologia", s.Behaviour == SoilBehaviour.Cohesive ? "Coesivo" : "Granulare"),
            ("da_m", s.Top / M), ("a_m", s.Bottom / M), ("sigma_eff_iniziale_kpa", weights ? s.EffectiveStressAtTop / KPa : null), ("p_iniziale_kn_m", s.InitialReaction),
            ("pendenza_kn_m2", s.Slope * M), ("risultante_kn", s.Resultant / 1000), ("momento_primo_knm", s.FirstMoment / 1e6))).ToArray());
        var candidates = new JsonArray(r.Candidates.Select(c => (JsonNode)J.Obj(("meccanismo", Mechanisms[(int)c.Mechanism]), ("capacita_kn", c.Capacity / 1000), ("governante", c.Governing),
            ("stato", c.Governing ? "Ammissibile, governante" : c.Capacity.HasValue ? "Candidato non governante; limite precedente già raggiunto" : "Meccanismo non attivabile nel campo di lunghezza"))).ToArray());
        var diagram = new JsonArray(r.Diagram.Select(x => (JsonNode)J.Obj(("z", x.Depth / M), ("lato", Sides[(int)x.Side]), ("p_kn_m", x.Reaction), ("v_kn", x.Shear / 1000), ("m_knm", x.Moment / 1e6),
            ("q_kpa", x.Pressure / KPa))).ToArray());
        var ground = new JsonArray(r.Ground.Select(x => (JsonNode)J.Obj(("z", x.Depth / M), ("lato", Sides[(int)x.Side]), ("strato", x.Layer), ("sigma_v_kpa", weights ? x.TotalStress / KPa : null),
            ("u_kpa", x.PorePressure / KPa), ("sigma_eff_kpa", weights ? x.EffectiveStress / KPa : null), ("q_lim_kpa", x.LimitPressure / KPa), ("p_lim_kn_m", x.LimitReaction),
            ("q_integrale_kn", x.IntegratedReaction / 1000))).ToArray());
        bool available = weights && r.StressesAvailable;
        return J.Obj(("capacita_kn", r.Capacity / 1000), ("meccanismo", mechanism), ("coesivo", r.Cohesive), ("misto", r.Mixed),
            ("chiusura_equilibrio", r.DistributedClosure ? "Distribuita" : "Concentrata"), ("diagramma_limite", limit), ("candidati", candidates),
            ("momento_testa_knm", r.HeadMoment / 1e6), ("momento_massimo_knm", r.MaximumMoment / 1e6), ("quota_momento_massimo_m", r.MaximumMomentDepth / M),
            ("quota_taglio_nullo_m", r.ZeroShearDepth / M), ("cerniere_m", r.Hinges.Select(h => h / M).ToArray()), ("fine_reazioni_m", r.ReactionEnd / M),
            ("risultante_concentrata_kn", r.ConcentratedResultant / 1000), ("quota_risultante_m", r.ReactionEnd / M), ("inversione_reazioni_m", r.ReversalDepth / M),
            ("residuo_forza_kn", r.ResidualForce / 1000), ("residuo_momento_knm", r.ResidualMoment / 1e6), ("convergenza", "Raggiunta"), ("diagrammi", diagram),
            ("tensioni_disponibili", available), ("nota_tensioni", available
                ? "σv e σ′v da peso proprio; u idrostatica, senza suzione. q_lim=p_lim/D; q=p/D è una pressione laterale equivalente. Nessuna riduzione locale con ξ o γR."
                : "σv e σ′v non disponibili: completare i pesi di volume. Nel profilo interamente coesivo la capacità dipende da Cu, non da questi pesi."),
            ("diagramma_terreno", ground), ("modello_adottato", r.ModelName));
    }

    public static JsonObject Calculate(JsonObject data)
    {
        try
        {
            if(data.S("vista_orizzontale") == "elastico") return ElasticHorizontalPile.CalculateShared(data);
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
            JsonObject? section = null; double my; string source;
            if (g.S("origine_momento") == (data.S("tipo_sezione") == "CHS" ? "Sezione CHS" : "Sezione c.a.")) { section = Section(data); my = section.D("momento_knm"); source = g.S("origine_momento"); }
            else if (g.S("origine_momento") == "Manuale")
            {
                my = g.Required("momento_resistente", strict: true); source = g.S("provenienza_momento");
                if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("Indicare natura e provenienza del momento resistente manuale.");
            }
            else throw new ArgumentException("Origine del momento resistente non riconosciuta.");
            bool distributed = DistributedMethod(g);
            var verticals = data.Array("stratigrafie").Select(s => Verticale(s!.AsArray(), l, water, zw, distributed)).ToArray();
            var verification = data["verifica"]!;
            if (!Calcolo.Verticali.ContainsKey(verification.S("verticali_indagate", "1")))
                throw new ArgumentException("Numero di verticali indagate non riconosciuto.");
            var factors = LateralPileFactors.FromStandard(Ntc, verification.S("verticali_indagate", "1") is "≥10" ? 10 : int.Parse(verification.S("verticali_indagate", "1"), CultureInfo.InvariantCulture));
            var (efficiencyJson, efficiency) = Gruppo(data);
            var pile = new LateralPile(d * M, l * M, e * M, fixedHead, my * 1e6, source, distributed ? LateralPileMethod.Stratified : LateralPileMethod.Broms, ed * 1000, step * M, tol);
            var r = Library(() => LateralPileCapacity.Calculate(pile, verticals.Select(v => v.Survey).ToArray(), factors, efficiency));
            var results = new JsonArray(r.Surveys.Select((s, i) => (JsonNode)Verticale(s, d, verticals[i].Weights)).ToArray());
            var warnings = r.Warnings.ToList();
            // La sezione è del foglio: il suo modello precede l'avviso dell'efficienza.
            if (section is not null) warnings.Insert(efficiency.ReeseVanImpe ? warnings.Count - 1 : warnings.Count, section.S("modello"));
            return J.Obj(("errore", ""), ("versione_motore", r.EngineVersion),
                ("metodo_calcolo", distributed ? StratifiedMethod : BromsMethod), ("fonte", r.Source), ("riferimenti", References()), ("input", data),
                ("capacita_kn", r.Capacity / 1000), ("sondaggio_governante", r.GoverningSurvey), ("meccanismo", Mechanisms[(int)r.Mechanism]),
                ("momento_resistente_knm", my), ("sezione", section), ("sondaggi", results), ("resistenza_caratteristica_manuale_kn", r.CharacteristicResistance / 1000),
                ("resistenza_progetto_manuale_kn", r.DesignResistance / 1000), ("azione_kn", ed), ("rapporto_meccanico", r.MechanicalRatio),
                ("capacita_media_kn", r.MeanCapacity / 1000), ("xi3", r.Factors.Xi3), ("xi4", r.Factors.Xi4), ("gamma_r", r.Factors.ResistanceFactor), ("efficienza", efficiencyJson),
                ("verticali_indagate", verification.S("verticali_indagate", "1")),
                ("ramo_media_kn", r.MeanBranch / 1000), ("ramo_minimo_kn", r.MinimumBranch / 1000),
                ("criterio_governante", r.MeanGoverns ? "Media / ξ3" : "Minimo / ξ4"),
                ("utilizzo_manuale", r.Utilization),
                ("esito_manuale", r.Satisfied ? "Verifica soddisfatta" : "Verifica non soddisfatta"),
                ("verifica_normativa", "Incompleta"), ("modello_adottato", r.ModelName), ("selezione_modello", "Automatica"), ("sperimentale", r.Experimental),
                ("percorso", "Incremento di H con e costante; M applicato = H·e; N costante"), ("avvisi", warnings));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException or OverflowException)
        { return J.Error(ex.Message); }
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
