namespace Anthea.Presentation.Help;

/// <summary>
/// Section of the Wiki that explains some fields of some modules: title shown in the tooltips, URI of the section (also the
/// identifier of the topic in the field specifications), modules and field keys it covers. Moved without changes
/// from X.Desktop WikiContextHelp in F5.2 (proposal F5, section 2.1); the «?» label and the opening of the Wiki stay WPF.
/// </summary>
public sealed record Topic(string Title, string Uri, IReadOnlyList<string> Modules, IReadOnlyList<string> Keys);

/// <summary>Table of the help topics of the fields and its lookups, without WPF (moved from WikiContextHelp in F5.2).</summary>
public static class HelpTopics
{
    static readonly IReadOnlyList<string> Concrete = ["str_palo", "mat_calcestruzzo", "geo_palo_orizzontale", "geo_muri_sostegno", "str_mista_ponte"];
    static readonly IReadOnlyList<string> Horizontal = ["geo_palo_orizzontale", "geo_micropalo_orizzontale"];
    public static IReadOnlyList<Topic> Topics { get; } =
    [
        new("Copriferro per la fessurazione", "sezione-in-calcestruzzo-armato#apertura-delle-fessure", ["str_palo"], ["copriferro_fessure", "spaziatura_fessure", "limite_fessure"]),
        new("Copriferro minimo, nominale e tolleranza", "calcestruzzo-armature-e-copriferro#copriferro-minimo-e-nominale", Concrete, ["cover_mm", "copriferro", "cover", "cmin_dur", "delta_c", "copriferro_override", "delta_cdev", "coverMethod", "diameter", "aggregate", "deviationControl", "deviation", "life", "ntcElement", "vita_durabilita", "qualita_copriferro"]),
        new("Esposizione e durabilità", "calcestruzzo-armature-e-copriferro#esposizioni-e-requisiti-del-materiale", Concrete, ["esposizione", "classe_strutturale", "vita_nominale"]),
        new("Materiali e diagrammi", "calcestruzzo-armature-e-copriferro#resistenze-e-diagrammi", Concrete, ["fck_mpa", "fyk_mpa", "gamma_c", "gamma_s", "alpha_cc", "fck", "fyk", "Ecm", "Es", "cls", "acciaio", "calcestruzzo", "classe_cls", "classe_acciaio", "cls_diagramma", "steel_diagramma", "steel_modulus_mpa"]),
        new("Acciaio: proprietà e unità", "acciaio-armature#grandezze-e-unita", ["mat_acciaio_armatura"], ["fyk_mpa", "steel_fu_mpa", "steel_modulus_mpa", "gamma_s", "steel_ultimate_strain", "steel_yield_strain", "steel_eps_u"]),
        new("Diagramma e classe dell'acciaio", "acciaio-armature#diagramma-e-classe-storica", ["mat_acciaio_armatura"], ["diagramma", "steel_diagram", "steel_diagramma", "classe", "materiale_acciaio_nome"]),
        new("Azioni, segni e unità della sezione", "guida-sezione-ca#input-azioni-e-convenzioni", ["str_palo"], ["axial_force_kn", "moment_x_knm", "moment_y_knm", "N", "theta", "assi", "origine_x", "origine_y", "rotazione"]),
        new("Geometria della sezione", "guida-sezione-ca#input-geometria-e-unita", ["str_palo"], ["shape", "diameter_mm", "width_mm", "height_mm", "flange_width_mm", "web_width_mm", "flange_thickness_mm", "inner_diameter_mm", "inner_width_mm", "inner_height_mm", "circular_sides"]),
        new("Profili normativi e limiti", "profili-calcestruzzo#implementazioni", ["str_palo"], ["normativa"]),
        new("Taglio e staffe", "sezione-in-calcestruzzo-armato#taglio", ["str_palo"], ["staffe_presenti", "transverse_bar_diameter_mm", "transverse_spacing_mm", "tipo_staffa", "rami_x", "rami_y", "schema_interno", "rami_interni", "rotazione_staffa"]),
        new("Traliccio, puntone e inclinazione delle staffe", "taglio-traliccio", ["str_palo"], ["bw_x", "bw_y", "d_x", "d_y", "asl_x", "asl_y", "alpha_x", "alpha_y", "cot_x", "cot_y"]),
        new("Torsione e interazione", "sezione-in-calcestruzzo-armato#torsione-e-interazione", ["str_palo"], ["cot_torsione", "as_torsione", "chiusura_torsione", "modello_circolare", "z_d"]),
        new("Dettagli, interferro e ancoraggi", "guida-sezione-ca#taglio-torsione-e-dettagli", ["str_palo"], ["aggregato", "interferro", "aderenza", "lunghezza", "percentuale", "confinamento", "posizione", "cautele", "diametro", "sigma"]),
        new("Tensioni, viscosità e fessurazione", "guida-sezione-ca#tensioni-e-fessurazione", ["str_palo"], ["phi", "n", "n_armature", "n_trefoli", "phi_trefoli", "trazione_cls", "sensibilita", "durata"]),
        new("Coefficienti e gruppo di pali", "guida-palo-verticale#coefficienti-e-gruppo-di-pali", ["geo_palo_verticale"], ["gamma_b", "gamma_s", "gamma_t", "xi", "verticali_indagate", "__xi3", "__xi4", "sicurezza_laterale_compressione", "sicurezza_laterale_trazione", "sicurezza_base", "peso_palo_sfavorevole", "peso_palo_favorevole", "numero_pali_x", "numero_pali_y", "interasse_x", "interasse_y", "eta_compressione", "eta_trazione", "metodo"]),
        new("Geometria, falda e azioni", "guida-palo-verticale#compilazione-ordinata", ["geo_palo_verticale"], ["diametro", "lunghezza", "profondita_testa", "tipo_palo", "sottotipo_palo_battuto", "presenza_falda", "profondita_falda", "considera_sottospinta", "azione_compressione", "azione_trazione"]),
        new("Tubo, inclinazione e peso", "guida-micropalo-verticale#tubo-e-peso", ["geo_micropalo_verticale"], ["profilo_chs", "inclinazione", "__peso_lineare", "considera_punta", "percentuale_punta"]),
        new("Bulbo e iniezione del micropalo", "guida-micropalo-verticale#dati-del-bulbo-e-dell-iniezione", ["geo_micropalo_verticale"], ["diametro", "diametro_bulbo", "lunghezza", "profondita_testa", "tipo_iniezione", "pressione_iniezione", "inizio_aderenza", "metodo_micropalo"]),
        new("Rigidezza EJ e sezione condivisa", "palo-elastico#rigidezza-della-sezione-e-dati-condivisi", Horizontal, ["ej_override", "ej_assegnato", "ej_motivo"]),
        new("Discretizzazione e vincoli del palo", "palo-elastico#modello-e-condizioni-al-contorno", Horizontal, ["passo", "punta"]),
        new("Modulo di reazione del terreno", "palo-elastico#modulo-di-reazione-e-conversioni", Horizontal, ["kh", "k_h", "modulo_reazione"]),
        new("Dettagli del palo e sovrapposizioni", "palo-elastico#dettagli-dei-pali-e-comportamento-adottato", ["geo_palo_orizzontale"], ["elemento", "barre_trattenute", "zone_estremita", "azioni_progetto", "taglio_confermato", "z_d", "aderenza_buona", "percentuale_sovrapposta", "distanza_barre", "lunghezza_barra", "lunghezza_gabbia", "lunghezza_minima", "fattore_sovrapposizione", "aggregato"]),
        new("Controlli sismici della testa", "guida-palo-elastico#controlli-sismici-della-testa-del-palo", ["geo_palo_orizzontale"], ["sisma_testa", "sisma_lunghezza", "sisma_staffe", "sisma_azioni", "sisma_elastico"]),
        new("Sezione CHS del micropalo", "guida-pali-e-micropali-caricati-orizzontalmente#sezione-chs-del-micropalo", ["geo_micropalo_orizzontale"], ["modo_chs", "profilo_chs", "diametro_chs_mm", "spessore_chs_mm", "fy_chs_mpa", "modulo_chs_mpa", "gamma_m0", "__classe", "__mpl_knm", "__npl_kn"]),
        new("Effetto di gruppo e applicabilità", "guida-palificata-orizzontale#metodo-e-applicabilita", ["geo_efficienza_orizzontale"], ["metodo", "interasse", "diametro"]),
        new("Omogeneizzazione e viscosità", "sezione-composta-da-ponte#geometria-e-omogeneizzazione", ["str_mista_ponte"], ["phi", "psi", "n"]),
        new("Fasi costruttive", "guida-sezione-composta-da-ponte#costruire-le-fasi", ["str_mista_ponte"], ["metodo", "ritiro", "tempo", "eta"]),
        new("Terreni, falda e sisma", "guida-muri-di-sostegno-con-stratigrafie-di-monte-e-valle#controllare-rilievo-falda-sisma-e-ricerca", ["geo_muri_sostegno"], ["kh", "kv", "ag", "ag_g", "f0", "ss", "st", "beta", "falda", "water_level"]),
        new("Muri: metodi, geometrie e limiti", "muri-metodi-perimetro", ["geo_muri_sostegno"], ["height", "stem_base", "stem_top", "toe", "heel", "slab"]),
        new("Ricerca, obiettivi e limiti", "guida-bridge-design#scegliere-obiettivo-e-costanti", ["str_bridge_design"], ["obiettivo", "metodo", "objective"]),
        new("Paratia: quote, fascia e unità", "guida-paratie#definire-il-modello-e-le-unita", ["geo_paratie"], ["top", "bottom", "width", "ei", "ei_mode", "spacing", "representation_source"]),
        new("Paratia: terreni, pressioni e memoria plastica", "analisi-paratie#stato-iniziale-e-pressioni-limite", ["geo_paratie"], ["gamma", "gamma_sat", "phi", "c", "cu", "k0", "kh", "drainage", "stiffness_source"]),
        new("Paratia: storia delle fasi e sostegni", "guida-paratie#costruire-la-storia-delle-fasi", ["geo_paratie"], ["stage", "prestress", "left_ground", "right_ground", "left_water", "right_water", "stiffness_factor", "capacity", "stiffness", "angle"]),
        new("Paratia: discretizzazione ed equilibrio", "analisi-paratie#discretizzazione-incrementi-ed-equilibrio", ["geo_paratie"], ["mesh", "substeps", "fix_top_u", "fix_top_rotation", "fix_bottom_u", "fix_bottom_rotation"]),
        new("Paratia: carichi lineari sulla struttura", "analisi-paratie#carichi-distribuiti-per-intervalli", ["geo_paratie"], ["q_top", "q_bottom", "distributed_load"]),
        new("Paratia: falda e filtrazione FEM", "guida-paratie#impostare-falda-e-filtrazione", ["geo_paratie"], ["hydraulic_mode", "hydraulic_left_extent", "hydraulic_right_extent", "hydraulic_bottom", "hydraulic_thickness", "hydraulic_mesh", "hydraulic_iterations", "hydraulic_weight_mode", "ignore_suction"]),
        new("Paratia: verifiche e applicabilità", "guida-paratie#richiedere-le-verifiche", ["geo_paratie"], ["case_id", "action_basis", "check_kind", "steel_standard", "steel_check_kind", "anchor_standard", "anchor_situation", "anchor_a1m1", "tube_standard", "tube_check_kind", "rc_pile_standard", "rc_pile_check_kind", "hydraulic_standard", "hydraulic_situation", "shear_confirmed", "detailing_mode"]),
        new("Paratia: ricerca dell'infissione", "guida-paratie#ricercare-l-infissione-sulla-griglia-assegnata", ["geo_paratie"],
            ["embedment_reference", "embedment_minimum", "embedment_maximum", "embedment_increment", "embedment_head_limit", "embedment_source"])
    ];
    /// <summary>Type of stress analysis of the c.a. section («modello» of the SLE tab; the same key names the shear model).</summary>
    public static Topic StressAnalysis { get; } = new("Analisi tensionale di esercizio: lineare o non lineare", "sezione-in-calcestruzzo-armato#materiali-tendini-e-analisi-di-esercizio", ["str_palo"], []);

    /// <summary>Topic of a field key in a module: the first topic of the table that lists both.</summary>
    public static Topic? ForField(string key, string? module) => Topics.FirstOrDefault(t => module is not null && t.Modules.Contains(module) && t.Keys.Contains(key));
    public static string? Description(string key, string? module) => ForField(key, module)?.Title;
    /// <summary>Topic with the given identifier (its URI): the table first, then <see cref="StressAnalysis"/>; null when unknown.</summary>
    public static Topic? Find(string? id) => id is null ? null : Topics.FirstOrDefault(t => t.Uri == id) ?? (StressAnalysis.Uri == id ? StressAnalysis : null);
}
