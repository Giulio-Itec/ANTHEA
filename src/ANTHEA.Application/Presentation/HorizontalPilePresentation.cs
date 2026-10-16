using System.Text.Json.Nodes;
using Anthea.Application.Materials;
using Anthea.Application.Modules;
namespace Anthea.Application.Presentation;

/// <summary>How the outcome of the verification is shown: muted (to calculate, or Rd not determined), satisfied or not satisfied.</summary>
public enum HorizontalVerificationOutcome { Muted, Satisfied, NotSatisfied }

/// <summary>«Verifica» of the horizontal pile: rows (quantity, value in kN), outcome and how it is shown.</summary>
public sealed record HorizontalPileVerification(IReadOnlyList<string[]> Rows, string Outcome, HorizontalVerificationOutcome Kind);

/// <summary>The text of the moment under the verification and its note; a null note leaves the previous one (the historic view sets it only for the c.a. section).</summary>
public sealed record HorizontalPileMoment(string Text, string? ToolTip);

/// <summary>A table of «Diagrammi e dettagli»: the title of its tab, the columns and the rows already formatted.</summary>
public sealed record HorizontalPileResultTable(string Title, IReadOnlyList<string> Columns, IReadOnlyList<string[]> Rows);

/// <summary>«Diagrammi e dettagli» of one survey: the three tables and the line of the mechanism, of the residuals and of the hinges.</summary>
public sealed record HorizontalPileResultSurvey(IReadOnlyList<HorizontalPileResultTable> Tables, string Info);

/// <summary>A figure of the report of the horizontal pile: survey (from 1), soil view or pile view, caption and size of the image.</summary>
public sealed record HorizontalPileFigurePlan(int Survey, bool SoilView, string Caption, int Width, int Height)
{
    public double AspectRatio => (double)Width / Height;
}

/// <summary>
/// Presentation of the horizontal pile and micropile sheet (geo_palo_orizzontale, geo_micropalo_orizzontale), moved without changes from
/// the historic view HorizontalWorkspace (F5.7): status, summary and moment texts, the verification, the tables of the results, the
/// fields enabled or shown by the options, the materials of the c.a. section and the plan of the figures of the report. The historic
/// view and the ViewModel of Anthea.Presentation call the same functions.
/// </summary>
public static class HorizontalPilePresentation
{
    public const string StatusToVerify = "Dati da verificare";
    public const string StatusPending = "Aggiornamento automatico in attesa…";
    public const string StatusRunning = "Calcolo di sezione ed equilibri in corso…";
    public const string StatusCompleted = "Calcolo completato · diagrammi alla capacità ultima";
    public const string StatusIncomplete = "Correggere i dati indicati";
    public const string SummaryInitial = "Completare i dati · calcolo automatico";
    public const string SummaryChanged = "Dati modificati · aggiornamento automatico";
    public const string SummaryFailed = "Calcolo fallito";
    public const string MomentInitial = "Momento da calcolare";
    public const string MomentChanged = "Aggiornamento automatico del momento…";
    public const string MomentUnavailable = "Momento non disponibile";
    public const string ModelInitial = "Modello automatico · dati da completare";
    public static IReadOnlyList<string> VerificationColumns { get; } = ["Grandezza", "Valore [kN]"];

    public static string SummaryIncomplete(string error) => "Calcolo non disponibile: " + error;

    /// <summary>The name of the layer <paramref name="index"/> of a stratigraphy: A, B, … (column «Strato»).</summary>
    public static string LayerName(int index) => ((char)('A' + index % 26)).ToString();

    /// <summary>A new layer of a stratigraphy («+ Stratigrafia», «Aggiungi strato»): granular, every value zero.</summary>
    public static JsonObject NewLayer() => J.Obj(("tipologia", "Granulare"), ("spessore", "0"), ("peso_specifico", "0"),
        ("peso_specifico_saturo", "0"), ("angolo_attrito", "0"), ("coesione_non_drenata", "0"), ("coesione_efficace", "0"));

    /// <summary>The summary of a complete result: mechanism, governing survey, moment, ratio and the experimental model.</summary>
    public static string Summary(JsonObject result) =>
        $"{result.S("meccanismo")} · stratigrafia {result.D("sondaggio_governante")}\nMy = {result.D("momento_resistente_knm"):N1} kNm\nHEd / Hu = {100 * result.D("rapporto_meccanico"):0.0}%\nVerifica normativa incompleta." +
        (result.B("sperimentale") ? "\nMODELLO SPERIMENTALE" : "");

    /// <summary>The warnings of a result, one per line.</summary>
    public static string Warnings(JsonObject result) => string.Join("\n", result.Array("avvisi").Select(v => v!.ToString()));

    /// <summary>The moment of a section (DisplayMoment): CHS (no note) or c.a. with the standard and the model as note.</summary>
    public static HorizontalPileMoment SectionMoment(JsonObject section, bool micro)
    {
        if (micro) return new($"My(N) = {section.D("momento_knm"):N1} kNm · N = {section.D("n_kn"):N1} kN\nCHS classe {section.D("classe")} · solo acciaio · interazione N–M lineare", null);
        string text = $"MRd = {section.D("momento_knm"):N1} kNm · N = {section.D("n_kn"):N1} kN\nChecker · {section.D("lati_contorno"):0} lati · residuo N = {section.D("residuo_n_kn"):G3} kN (tol. {section.D("tolleranza_n_kn"):G3})";
        return new(text + "\n" + section.S("normativa", "Normativa del risultato non disponibile"), section.S("modello"));
    }

    /// <summary>
    /// The moment shown when a calculation is published, before its result is read: of the section, the manual one of the calculated
    /// data, or not available with the error of the section.
    /// </summary>
    public static HorizontalPileMoment CalculatedMoment(HorizontalPileAnalysis analysis, JsonObject calculated, bool micro)
    {
        if (analysis.Section is not null) return SectionMoment(analysis.Section, micro);
        if (calculated["generali"].S("origine_momento") == "Manuale" && J.Number(calculated["generali"]!["momento_resistente"]) is double manual && manual > 0)
            return new($"My inserito manualmente = {manual:N1} kNm", null);
        return new(MomentUnavailable + (analysis.SectionError == "" ? "" : ": " + analysis.SectionError), null);
    }

    /// <summary>The moment of a complete result: of the section of the result, or the manual one adopted.</summary>
    public static HorizontalPileMoment ResultMoment(JsonObject result, bool micro) => result["sezione"] is JsonObject section
        ? SectionMoment(section, micro) : new($"My adottato manualmente = {result.D("momento_resistente_knm"):N1} kNm", null);

    /// <summary>«Verifica»: HEd, Hu, Rk and Rd («—» when missing) and the outcome; to calculate without result.</summary>
    public static HorizontalPileVerification Verification(JsonObject? result)
    {
        string Number(string key) => result?[key] is null ? "—" : result.D(key).ToString("N1");
        string[][] rows = [
            ["Azione orizzontale HEd", Number("azione_kn")], ["Capacità ultima Hu", Number("capacita_kn")],
            ["Resistenza caratteristica Rk", Number("resistenza_caratteristica_manuale_kn")],
            ["Resistenza di progetto Rd", Number("resistenza_progetto_manuale_kn")]];
        bool available = result?["resistenza_progetto_manuale_kn"] is not null;
        bool satisfied = available && result!.D("azione_kn") <= result.D("resistenza_progetto_manuale_kn");
        string outcome = result is null ? "Da calcolare" : !available ? "Verifica non eseguita: Rd non determinata" :
            satisfied ? "Verifica soddisfatta" : "Verifica non soddisfatta";
        return new(rows, outcome, !available ? HorizontalVerificationOutcome.Muted : satisfied ? HorizontalVerificationOutcome.Satisfied : HorizontalVerificationOutcome.NotSatisfied);
    }

    /// <summary>«Diagrammi e dettagli», one item for every survey of the result (ResultsTabs of the historic view).</summary>
    public static IReadOnlyList<HorizontalPileResultSurvey> Results(JsonObject result) => result.Array("sondaggi").Select(survey =>
    {
        var s = survey!;
        bool truncated = HorizontalPileDiagrams.ReactionLimit(s) is not null;
        var terrainRows = s.Array("diagramma_terreno").Select(r => new[] { r.D("z").ToString("0.000"), r.S("lato"), r.D("strato").ToString("0"),
            r!["sigma_v_kpa"] is null ? "—" : r.D("sigma_v_kpa").ToString("0.00"), r.D("u_kpa").ToString("0.00"),
            r["sigma_eff_kpa"] is null ? "—" : r.D("sigma_eff_kpa").ToString("0.00"), r.D("q_lim_kpa").ToString("0.00"), r.D("p_lim_kn_m").ToString("0.00"), r.D("q_integrale_kn").ToString("0.00") }).ToArray();
        var rows = s.Array("diagrammi").Select(r => new[] { r.D("z").ToString("0.0"), r.S("lato"), r.D("p_kn_m").ToString("0.0"), r.D("v_kn").ToString("0.0"), r.D("m_knm").ToString("0.0") }).ToArray();
        var candidates = s.Array("candidati").Select(r => new[] { r.S("meccanismo"), r!["capacita_kn"] is null ? "—" : r.D("capacita_kn").ToString("N1"), r.S("stato") }).ToArray();
        var hinges = string.Join("; ", s.Array("cerniere_m").Select(v => (J.Number(v) ?? 0).ToString("0.0")));
        string info = $"{s.S("meccanismo")} · Hu {s.D("capacita_kn"):N1} kN · |M|max {s.D("momento_massimo_knm"):N1} kNm a z={s.D("quota_momento_massimo_m"):0.0} m\n" +
            $"Residui: H {s.D("residuo_forza_kn"):0.0} kN · M {s.D("residuo_momento_knm"):0.0} kNm · {s.S("convergenza")}\n" +
            $"Cerniere z [m]: {hinges} · " + (truncated ? "Tratto sotto cerniera mostrato tratteggiato: completamento idealizzato dell'equilibrio." : $"F concentrata {s.D("risultante_concentrata_kn"):N1} kN a z={s.D("quota_risultante_m"):0.0} m");
        return new HorizontalPileResultSurvey([
            new("Tabella terreno", ["z [m]", "Lato", "Strato", "σv [kPa]", "u [kPa]", "σ′v [kPa]", "q_lim [kPa]", "p_lim [kN/m]", "Q [kN]"], terrainRows),
            new("Tabella", ["z [m]", "Lato", "p [kN/m]", "V [kN]", "M [kNm]"], rows),
            new("Meccanismi", ["Meccanismo", "H [kN]", "Stato"], candidates)], info);
    }).ToArray();

    /// <summary>The figures of the report: for every survey of the result, the soil view and the pile view, 1000 × 660.</summary>
    public static IReadOnlyList<HorizontalPileFigurePlan> Figures(JsonObject result) =>
        Enumerable.Range(0, result.Array("sondaggi").Count).SelectMany(i => new[] { true, false }.Select(soil =>
            new HorizontalPileFigurePlan(i + 1, soil, soil ? "Tensioni e resistenze del terreno" : "Equilibrio e sollecitazioni alla capacità ultima", 1000, 660))).ToArray();

    /// <summary>
    /// The fields of the general, moment and efficiency forms enabled by the options (Preview of the historic view): the key, whether it
    /// is enabled and whether a disabled field is also faded. The eccentricity stays editable even when legacy (never overwritten).
    /// </summary>
    public static IReadOnlyList<(string Key, bool Enabled, bool Dim)> FieldStates(JsonObject data)
    {
        var g = data["generali"]!;
        bool manual = g.S("origine_momento") == "Manuale";
        bool manualEfficiency = data["verifica"].S("efficienza_metodo") == "Manuale";
        var states = new List<(string, bool, bool)> { ("profondita_falda", g.B("presenza_falda"), true), ("eccentricita", true, false),
            ("momento_resistente", manual, true), ("provenienza_momento", manual, true), ("efficienza_eta", manualEfficiency, true) };
        foreach (string key in new[] { "interasse_anteriore", "interasse_posteriore", "interasse_sinistro", "interasse_destro" }) states.Add((key, !manualEfficiency, true));
        return states;
    }

    /// <summary>The CHS fields enabled by the mode of insertion: the profile of the catalogue or the manual diameter and thickness.</summary>
    public static IReadOnlyList<(string Key, bool Enabled)> ChsFieldStates(JsonObject section)
    {
        bool catalog = section.S("modo_chs") == "Catalogo";
        return [("profilo_chs", catalog), ("diametro_chs_mm", !catalog), ("spessore_chs_mm", !catalog)];
    }

    /// <summary>The fields of the assigned EJ shown only when the override is active.</summary>
    public static bool EjShown(JsonObject section) => section.B("ej_override");

    /// <summary>The fields of the second inner ring of bars, shown only when it is active.</summary>
    public static IReadOnlyList<string> InnerRingKeys { get; } = ["second_inner_count", "second_inner_diameter", "second_inner_gap"];

    /// <summary>
    /// Second ring of bars, as «Secondo anello interno» of the c.a. section: when it is active its empty fields start from the bars of the
    /// first ring and a clear distance of 30 mm. Writes into <paramref name="section"/> and returns the keys written, in order.
    /// </summary>
    public static IReadOnlyList<string> FillInnerRing(JsonObject section)
    {
        var written = new List<string>();
        if (!section.B("second_inner_enabled")) return written;
        foreach (var (key, value) in new[] { ("second_inner_count", section.S("longitudinal_bar_count")), ("second_inner_diameter", section.S("longitudinal_bar_diameter_mm")), ("second_inner_gap", "30") })
            if (string.IsNullOrWhiteSpace(section.S(key))) { section[key] = value; written.Add(key); }
        return written;
    }

    /// <summary>
    /// A material of the c.a. section chosen or changed (SectionMaterialChanged of the historic view): a class of the catalogue writes its
    /// values, the strength of the concrete gives the class, a value of the steel makes it custom. Writes into <paramref name="section"/>
    /// and returns the keys whose shown value follows the data, in order.
    /// </summary>
    public static IReadOnlyList<string> ApplySectionMaterial(JsonObject section, string key)
    {
        var shown = new List<string>();
        if (key is "classe_cls" or "classe_acciaio")
        {
            var catalog = key == "classe_cls" ? MaterialInputCatalog.Concrete() : MaterialInputCatalog.Steel(false, "NTC 2018");
            var material = catalog.FirstOrDefault(m => m.S("nome") == section.S(key));
            if (material != null) foreach (var (field, value) in material.Where(v => v.Key != "nome")) { section[field] = value?.DeepClone(); shown.Add(field); }
        }
        else if (key == "fck_mpa")
        {
            section["classe_cls"] = HorizontalSheetPresentation.ConcreteClass(section.D("fck_mpa")); shown.Add("classe_cls");
        }
        else if (key is "fyk_mpa" or "steel_modulus_mpa" or "steel_fu_mpa" or "steel_eps_u" or "steel_diagramma")
        {
            section["classe_acciaio"] = "Personalizzato";
            section["materiale_acciaio_nome"] = "Acciaio personalizzato";
        }
        return shown;
    }
}
