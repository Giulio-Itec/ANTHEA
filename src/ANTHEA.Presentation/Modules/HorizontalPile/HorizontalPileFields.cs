using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Application.Materials;
using Anthea.Application.Modules;
using Anthea.Application.Presentation;
using Anthea.Calculations;
using Anthea.Presentation.Forms;

namespace Anthea.Presentation.Modules.HorizontalPile;

/// <summary>A group of fields of a form (InputForm.GroupFields): title, keys in their order, open or closed at the start.</summary>
public sealed record HorizontalPileFieldGroup(string Title, IReadOnlyList<string> Keys, bool Expanded);

/// <summary>
/// The fields of the horizontal pile and micropile sheet (F5.7), one definition for the historic view (HorizontalWorkspace, until it
/// is archived) and for <see cref="HorizontalPileSheetViewModel"/>: the seven forms in their order with their groups, hidden fields and
/// tool tips, the columns of the stratigraphies, the headings of the soil editor and the fixed texts of the cards. Moved without changes
/// from HorizontalWorkspace and HorizontalSoilEditor. Every field keeps the rule of InputForm: the raw text goes into the document.
/// </summary>
public static class HorizontalPileFields
{
    private static string[] BarDiameters => ["8", "10", "12", "14", "16", "18", "20", "22", "24", "25", "26", "28", "30", "32", "36", "40"];

    /// <summary>«Dati generali».</summary>
    public static FieldSpec[] General(bool micro) => [new("diametro", micro ? "Diametro geotecnico micropalo" : "Diametro palo", "m", Symbol: "D"), new("lunghezza", "Lunghezza infissa", "m", Symbol: "L"),
        new("eccentricita", "Eccentricità forza sopra il piano campagna", "m", Symbol: "e"), new("tratto_libero", "Tratto libero sopra il terreno", "m"), new("vincolo", "Rotazione in testa", Choices: ["Libera", "Impedita"]),
        new("azione_orizzontale", "Forza orizzontale", "kN", Symbol: "HEd"), new("azione_assiale", "Forza assiale (+ compressione)", "kN", Symbol: "N"),
        new("presenza_falda", "Presenza falda", Bool: true), new("profondita_falda", "Profondità falda", "m"), new("gamma_acqua", "Peso unitario acqua", "kN/m³")];

    /// <summary>The groups of «Dati generali».</summary>
    public static HorizontalPileFieldGroup[] GeneralGroups() => [new("Dati opzionali · peso acqua", ["gamma_acqua"], false)];

    /// <summary>«Opzioni avanzate» of «Dati generali»: the method, the step of the diagrams and the tolerance.</summary>
    public static FieldSpec[] Model() => [new("metodo_calcolo", "Metodo di calcolo", Choices: [HorizontalPileEditor.BromsMethod, HorizontalPileEditor.StratifiedMethod]),
        new("passo", "Passo diagrammi", "m", Symbol: "Δz"), new("tolleranza", "Tolleranza radici", Symbol: "ε")];

    /// <summary>«Coefficienti normativa»: the investigated verticals and the coefficients they give.</summary>
    public static FieldSpec[] Factors() => [new("verticali_indagate", "Verticali indagate", Choices: PileInputCatalog.InvestigatedProfiles),
        new("__xi3", "Correlazione", Symbol: "ξ3", ReadOnly: true), new("__xi4", "Correlazione", Symbol: "ξ4", ReadOnly: true),
        new("__gamma_r", "Sicurezza resistenza", Symbol: "γR", ReadOnly: true)];

    /// <summary>«Efficienza».</summary>
    public static FieldSpec[] Efficiency() => [
        new("efficienza_metodo", "Efficienza", Choices: ["Manuale", "Reese & Van Impe (foglio)"]),
        new("efficienza_eta", "Efficienza manuale", Symbol: "η"),
        new("interasse_anteriore", "Interasse anteriore", "m"), new("interasse_posteriore", "Interasse posteriore", "m"),
        new("interasse_sinistro", "Interasse sinistro", "m"), new("interasse_destro", "Interasse destro", "m"),
        new("__eta", "Efficienza adottata", Symbol: "η", ReadOnly: true)];

    /// <summary>«Opzioni avanzate · resistenza manuale ed EJ»: where the moment comes from.</summary>
    public static FieldSpec[] Moment(bool micro) => [new("origine_momento", "Momento adottato da", Choices: [micro ? "Sezione CHS" : "Sezione c.a.", "Manuale"]),
        new("momento_resistente", "Momento manuale", "kNm", Symbol: "My"), new("provenienza_momento", "Natura / fonte My manuale")];

    /// <summary>The assigned EJ.</summary>
    public static FieldSpec[] Ej() => [new("ej_override", "EJ manuale motivato", Bool: true), new("ej_assegnato", "EJ assegnato", "kN m²"), new("ej_motivo", "Motivazione EJ", Wide: true)];

    /// <summary>The fields of the assigned EJ shown only with the override.</summary>
    public static string[] EjValues() => ["ej_assegnato", "ej_motivo"];

    /// <summary>
    /// «Momento plastico / resistente» of the pile: the c.a. section. The choices of the materials are those of the catalogue, the class
    /// of <paramref name="section"/> and «Personalizzato».
    /// </summary>
    public static FieldSpec[] ConcreteSection(JsonObject section) => [
        new("classe_cls", "Calcestruzzo", Choices: MaterialInputCatalog.Concrete().Select(m => m.S("nome")).Append(section.S("classe_cls")).Append("Personalizzato").Distinct().ToArray()),
        new("fck_mpa", "fck del materiale", "MPa", ReadOnly: true),
        new("alpha_cc", "αcc archivio", ReadOnly: true), new("gamma_c", "γc archivio", ReadOnly: true),
        new("__fcd", "Resistenza di progetto a compressione", "MPa", Symbol: "fcd", ReadOnly: true),
        new("classe_acciaio", "Acciaio", Choices: MaterialInputCatalog.Steel(false, "NTC 2018").Select(m => m.S("nome")).Append(section.S("classe_acciaio")).Append("Personalizzato").Distinct().ToArray(), Note: i => MaterialInputCatalog.SteelDifference(i, "NTC 2018")),
        new("fyk_mpa", "fyk del materiale", "MPa", ReadOnly: true),
        new("gamma_s", "γs archivio", ReadOnly: true),
        new("coefficienti_unitari", "Coefficienti unitari", Bool: true),
        new("__fyd", "Resistenza a snervamento di progetto", "MPa", Symbol: "fyd", ReadOnly: true),
        new("longitudinal_bar_diameter_mm", "Diametro barre", "mm", Symbol: "φL", Choices: BarDiameters),
        new("longitudinal_bar_count", "Numero barre", Symbol: "n"),
        new("second_inner_enabled", "Secondo anello interno", Bool: true),
        new("second_inner_count", "Numero barre dell'anello interno", Symbol: "n₂"),
        new("second_inner_diameter", "Diametro barre dell'anello interno", "mm", Symbol: "φL,2", Choices: BarDiameters),
        new("second_inner_gap", "Distanza libera dalla prima fila", "mm"),
        new("transverse_bar_diameter_mm", "Diametro staffa", "mm", Symbol: "φst", Choices: ["6", "8", "10", "12", "14", "16", "18", "20"]),
        new("transverse_spacing_mm", "Passo staffe", "mm"),
        new("gamma_ca", "Peso unitario c.a. adottato (acciaio incluso)", "kN/m³"),
        new("cover_mm", "Copriferro netto", "mm", Symbol: "c"),
        new("esposizione", "Esposizione", Choices: ConcreteEditor.ExposureChoices.ToArray()),
        new("vita_durabilita", "Vita utile per durabilità", "anni", Choices: ["50", "100"]),
        new("qualita_copriferro", "Controllo qualità copriferro confermato", Bool: true),
        new("steel_modulus_mpa", "Modulo elastico acciaio", "MPa", Symbol: "Es"),
        new("cls_diagramma", "Legame calcestruzzo", Choices: MaterialInputCatalog.ConcreteDiagrams, Note: FieldNotes.StressBlock),
        new("steel_diagramma", "Legame acciaio", Choices: ["Elastoplastico", "Incrudente"]),
        new("steel_fu_mpa", "Resistenza ultima", "MPa", Symbol: "fu"),
        new("steel_eps_u", "Deformazione ultima", "‰", Symbol: "εu"),
        new("circular_sides", "Lati del contorno", Choices: ["16", "32", "64", "96", "128", "256"])];

    /// <summary>The groups of the c.a. section.</summary>
    public static HorizontalPileFieldGroup[] ConcreteSectionGroups() => [new("Dati opzionali e proprietà materiali", ["fck_mpa", "fyk_mpa", "__fcd", "__fyd", "gamma_ca", "esposizione",
        "vita_durabilita", "qualita_copriferro", "steel_modulus_mpa", "steel_fu_mpa", "steel_eps_u", "cls_diagramma", "steel_diagramma", "circular_sides"], false)];

    /// <summary>The fields of the c.a. section always hidden (coefficients of older archives).</summary>
    public static string[] ConcreteSectionHidden() => ["alpha_cc", "gamma_c", "gamma_s"];

    /// <summary>The tool tips of the fields of the c.a. section.</summary>
    public static IReadOnlyDictionary<string, string> ConcreteSectionToolTips() => new Dictionary<string, string>
    {
        ["coefficienti_unitari"] = "Disattivato: coefficienti NTC/archivio. Attivato: normativa custom di sezione con tutti i gamma e alpha_cc pari a 1; non modifica i coefficienti geotecnici di Broms. I valori ordinari restano conservati.",
        ["cover_mm"] = "Distanza netta dal bordo del calcestruzzo alla superficie esterna della staffa, in mm.",
        ["second_inner_gap"] = "Distanza libera tra le superfici delle barre dei due anelli, in mm.",
    };

    /// <summary>«Sezione resistente CHS» of the micropile.</summary>
    public static FieldSpec[] Chs() => [
        new("modo_chs", "Inserimento CHS", Choices: ["Catalogo", "Manuale"]),
        new("profilo_chs", "Catalogo ANTHEA", Choices: PileInputCatalog.ChsProfiles),
        new("diametro_chs_mm", "Diametro esterno manuale", "mm", Symbol: "De"),
        new("spessore_chs_mm", "Spessore manuale", "mm", Symbol: "t"),
        new("fy_chs_mpa", "Snervamento acciaio", "MPa", Symbol: "fy"),
        new("modulo_chs_mpa", "Modulo elastico acciaio", "MPa", Symbol: "Es"),
        new("gamma_acciaio", "Peso unitario acciaio adottato", "kN/m³"),
        new("gamma_iniezione", "Peso unitario iniezione (0 = esclusa)", "kN/m³"),
        new("gamma_m0", "Sicurezza resistenza", Symbol: "γM0"),
        new("__fyd", "Resistenza di progetto", "MPa", Symbol: "fyd", ReadOnly: true),
        new("__diametro_mm", "Diametro adottato", "mm", Symbol: "De", ReadOnly: true),
        new("__spessore_mm", "Spessore adottato", "mm", Symbol: "t", ReadOnly: true),
        new("__area_mm2", "Area acciaio", "mm²", Symbol: "A", ReadOnly: true),
        new("__inerzia_mm4", "Momento d’inerzia", "mm⁴", Symbol: "I", ReadOnly: true),
        new("__wel_mm3", "Modulo elastico", "mm³", Symbol: "Wel", ReadOnly: true),
        new("__wpl_mm3", "Modulo plastico", "mm³", Symbol: "Wpl", ReadOnly: true),
        new("__massa_kg_m", "Massa lineare acciaio", "kg/m", ReadOnly: true),
        new("__classe", "Classe sezione", ReadOnly: true),
        new("__npl_kn", "Resistenza assiale sezione", "kN", Symbol: "Npl", ReadOnly: true),
        new("__mpl_knm", "Momento plastico N=0", "kNm", Symbol: "Mpl", ReadOnly: true)];

    /// <summary>The groups of the CHS section, all open.</summary>
    public static HorizontalPileFieldGroup[] ChsGroups() => [
        new("Tubolare CHS", ["modo_chs", "profilo_chs", "diametro_chs_mm", "spessore_chs_mm"], true),
        new("Acciaio", ["fy_chs_mpa", "modulo_chs_mpa", "gamma_m0", "__fyd"], true),
        new("Proprietà della sezione", ["__diametro_mm", "__spessore_mm", "__area_mm2", "__inerzia_mm4", "__wel_mm3", "__wpl_mm3", "__massa_kg_m", "__classe", "__npl_kn", "__mpl_knm"], true)];

    /// <summary>The columns of the table of the layers of a stratigraphy (the soil editor is a column of its own, after the second).</summary>
    public static FieldSpec[] Layers() => [new("tipologia", "Terreno", Choices: ["Granulare", "Coesivo"]),
        new("__kp", "Kp [−]", ReadOnly: true),
        new("spessore", "Spessore [m]"), new("peso_specifico", "γ [kN/m³]"), new("peso_specifico_saturo", "γsat [kN/m³]"),
        new("angolo_attrito", "φ′ [°]"), new("coesione_non_drenata", "Cu [kPa]"), new("coesione_efficace", "c′ [kPa]")];

    /// <summary>The headings of the columns of the soil editor (horizontal soil reaction).</summary>
    public static string[] SoilHeadings() => ["Addensamento", "Determinazione · autore / teoria", "Parametro\nunità", "Minimo", "Massimo", "Adottato\nmodificabile", "Origine / Info"];

    /// <summary>Under «Efficienza».</summary>
    public static string EfficiencyNote => HorizontalSheetPresentation.ReeseVanImpeRule + "\nMetodo del foglio: schema a 8 vicini.";

    /// <summary>Under «Coefficienti normativa».</summary>
    public const string FactorsNote = "Rd = η · min(Hu,media / ξ3; Hu,min / ξ4) / 1,3\nRestano escluse le altre verifiche NTC/EC2.";

    /// <summary>Title of the card of the section.</summary>
    public static string SectionTitle(bool micro) => micro ? "Sezione resistente CHS" : "Momento plastico / resistente";

    /// <summary>«Sintesi del calcolo» under the verification.</summary>
    public const string CalculationDescription =
        "Per ogni sondaggio si confrontano i meccanismi applicabili e si assume la capacità ultima Hu più bassa.\n\n" +
        "• Corto: raggiungimento della resistenza del terreno, senza cerniere plastiche.\n" +
        "• Intermedio: cerniera in testa; previsto con rotazione impedita.\n" +
        "• Lungo: cerniera interna e, con testa impedita, anche in testa.\n\n" +
        "Testa libera: confronto corto/lungo. Testa impedita: corto/intermedio/lungo. La scelta non dipende dal solo rapporto L/D.\n\n" +
        "Rk = min(Hu,media/ξ3; Hu,min/ξ4).\nRd = η · Rk/1,3. Verifica soddisfatta se HEd ≤ Rd.\n\n" +
        "In Opzioni avanzate, Stratificato abilita anche alternanze coesivo/granulare con equilibrio globale e reazioni distribuite. Nei granulari corti/intermedi differisce dalla chiusura concentrata di Broms.\n\n" +
        "Diagrammi e dettagli mostra tensioni verticali, pressioni laterali, reazioni, taglio, momento e risultante limite su tutta la profondità. Il tratto sotto la cerniera interna è tratteggiato perché idealizzato; l'anteprima compatta mostra solo il ramo superiore. Riferimenti: Viggiani, Broms, Wood/NZGS (Broms Modified) e FHWA GEC 9, consultabili nei risultati. I grafici sono alla capacità Hu, senza spostamenti o rotazioni; l’estensione stratificata resta sperimentale.";
}
