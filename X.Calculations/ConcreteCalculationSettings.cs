using System.Text.Json.Nodes;

namespace Anthea.Calculations;

/// <summary>Defaults and authoritative section parameters, independent of which tab has been opened.</summary>
public static class ConcreteCalculationSettings
{
    public static readonly (string Input, string Standard)[] CommonCoefficients =
        [("alpha_cc", "AlphaCC"), ("gamma_c", "GammaC"), ("gamma_s", "GammaS")];
    public static void Prepare(JsonObject input, JsonObject settings)
    {
        if (settings["coefficienti"] is not JsonObject)
            settings["coefficienti"] = ConcreteStandards.Defaults(ConcreteStandards.RequiredName(settings));
        foreach (var (key, standard) in CommonCoefficients) settings["coefficienti"]![standard] = input[key]?.DeepClone();
        if (!input.ContainsKey("staffe_presenti")) input["staffe_presenti"] = "Sì";
        if (settings["taglio"] is not JsonObject) settings["taglio"] = new JsonObject();
        var o = settings["taglio"]!.AsObject();
        if (!o.ContainsKey("parametri"))
        {
            o["parametri_precedenti"] = J.Obj(new[] { "bw_x", "d_x", "asl_x", "bw_y", "d_y", "asl_y" }.Select(k => (k, (object?)o.S(k))).ToArray());
            o["parametri"] = "Automatici da sezione";
        }
        foreach (var (key, value) in new[] {
            ("tipo_staffa","Staffa chiusa"), ("rami_x","2"), ("rami_y","2"), ("rami_interni","0"),
            ("schema_interno","Bracci paralleli"), ("rotazione_staffa","0"),
            ("ancoraggio","Da verificare"), ("modello","Con staffe"),
            ("bw_x",""), ("d_x",""), ("asl_x",""), ("alpha_x","90"), ("cot_x",""),
            ("bw_y",""), ("d_y",""), ("asl_y",""), ("alpha_y","90"), ("cot_y",""),
            ("cot_torsione","1"), ("as_torsione","0"), ("chiusura_torsione","Da confermare"),
            ("modello_circolare","Da scegliere"), ("z_d","0.75"), ("eccentricita_mc_x","0"), ("eccentricita_mc_y","0") })
            if (!o.ContainsKey(key)) o[key] = value;
        if (o["azioni"] is not JsonArray) o["azioni"] = new JsonArray();
        void Defaults(string name, params (string Key, object Value)[] values)
        {
            if (settings[name] is not JsonObject) settings[name] = new JsonObject();
            foreach (var (key, value) in values)
                if (!settings[name]!.AsObject().ContainsKey(key)) settings[name]![key] = J.Node(value);
        }
        Defaults("momento_curvatura", ("N", "0"), ("theta", "0"), ("passi", "60"), ("frazione", "1"),
            ("campionamento", "Quadratico"), ("trazione_cls", "No"), ("angoli", "64"), ("tolleranza_n", "1"), ("raffina_snervamento", "12"));
        Defaults("dettagli_costruttivi", ("vita_durabilita", "50"), ("qualita_copriferro", "No"), ("elemento", "Da scegliere"),
            ("aggregato", "20"), ("cmin_dur", ""), ("delta_c", "10"), ("zona_sovrapposizione", "No"), ("as_secondaria", "0"),
            ("passo_secondaria", "0"), ("zona_critica", "No"), ("barre_trattenute", "Da confermare"), ("ancoraggio_appoggi", "Da confermare"));
        Defaults("ancoraggi", ("schema_versione", 1), ("tipo", "Ancoraggio rettilineo"), ("diametro", ""), ("sigma", ""),
            ("aderenza", "Buona"), ("lunghezza", ""), ("percentuale", "100"), ("interferro", "0"),
            ("confinamento", "Da verificare"), ("posizione", "Da verificare"), ("cautele", "Da verificare"));
    }

    public static bool HasStirrups(JsonObject input)
    {
        // Missing in old documents means the historical default; an explicit invalid value is not a default.
        if (!input.ContainsKey("staffe_presenti")) return true;
        if (input["staffe_presenti"] is JsonValue value && value.TryGetValue<string>(out var flag))
        {
            if (flag == "Sì") return true;
            if (flag == "No") return false;
        }
        throw new ArgumentException("Staffe presenti: scegliere Sì oppure No.");
    }

    public static int StirrupLegs(JsonNode? options, string key)
    {
        int minimum = key == "rami_interni" ? 0 : 2;
        string text = options is JsonObject fields && fields.ContainsKey(key) ? options.S(key) : minimum.ToString();
        return SectionWorkspace.Subdivisions(text, key, minimum, 100);
    }

    public static void ValidateStirrups(JsonObject input, JsonObject options)
    {
        if (!HasStirrups(input)) return;
        if (input.S("shape") == "Circolare") _ = SectionWorkspace.Number(options.S("rotazione_staffa", "0"), "Rotazione staffe");
        foreach (var key in input.S("shape") == "Circolare" ? new[] { "rami_interni" } : new[] { "rami_x", "rami_y" })
            _ = StirrupLegs(options, key);
    }

    public static SezioneCA UpdateAutomaticShear(JsonObject input, JsonObject options)
    {
        var geometry = new SezioneCA(input);
        if (options.S("parametri", "Automatici da sezione") != "Automatici da sezione") return geometry;
        bool changed = false;
        foreach (string axis in new[] { "x", "y" })
        {
            var values = SectionShearGeometry.Derive(geometry, axis == "x");
            foreach (var (field, value) in new[] { ("bw_", values.Bw), ("d_", values.Depth), ("asl_", values.SteelArea) })
            {
                changed |= J.Number(options[field + axis]) != value;
                options[field + axis] = value.ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        if (changed) options["ancoraggio"] = "Da verificare";
        return geometry;
    }

    /// <summary>Parametri del taglio diversi da «Automatici da sezione» (manuali).</summary>
    public static bool ManualShear(JsonObject options) => options.S("parametri", "Automatici da sezione") != "Automatici da sezione";

    /// <summary>
    /// Parametri manuali del taglio applicati a una disposizione diversa da quella del foglio (ricerca delle armature, analisi dei bug del
    /// 15/10/2026, ARM-1): per ogni asse bw, d e Asl = min(valore manuale; valore ricavato dalla disposizione, come
    /// <see cref="UpdateAutomaticShear"/>), per ogni forma; «parametri» resta manuale. Il minimo non supera mai la scelta dell'utente e
    /// segue la nuova disposizione quando le barre o le staffe riducono d o Asl.
    /// </summary>
    public static SezioneCA LimitManualShear(JsonObject input, JsonObject options)
    {
        var geometry = new SezioneCA(input);
        foreach (string axis in new[] { "x", "y" })
        {
            var values = SectionShearGeometry.Derive(geometry, axis == "x");
            foreach (var (field, value) in new[] { ("bw_", values.Bw), ("d_", values.Depth), ("asl_", values.SteelArea) })
            {
                double manual = SectionWorkspace.Number(options.S(field + axis), field + axis);
                options[field + axis] = Math.Min(manual, value).ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        return geometry;
    }
}
