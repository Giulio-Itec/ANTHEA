using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Application.Materials;
using Anthea.Application.Walls;
using Anthea.Calculations;
using Anthea.Presentation.Forms;

namespace Anthea.Presentation.Modules.RetainingWall;

/// <summary>A group of fields of a form that the view shows in an expander (InputForm.GroupFields).</summary>
public sealed record RetainingWallFieldGroup(string Title, IReadOnlyList<string> Keys, bool Expanded);

/// <summary>A zone of the reinforcement: key of «reinforcement» and title of its card.</summary>
public sealed record RetainingWallRebarZone(string Key, string Title);

/// <summary>
/// The fields of the retaining wall sheet (F5.9): the only definition of labels, symbols, units, items, groups and columns, read by
/// the historic view RetainingWallWorkspace and by the ViewModel of the sheet, in the order of the view. The forms of the sheet have
/// the symbol column (<see cref="SymbolForms"/>), except the seismic form (wide choices).
/// </summary>
public static class RetainingWallFields
{
    /// <summary>Title of the sheet.</summary>
    public const string Title = "Muri di sostegno";

    /// <summary>Views of the results, in the order of the choice.</summary>
    public static IReadOnlyList<string> ViewModes { get; } = ["Geometria e carichi", "Sollecitazioni", "Forze resistenti", "Tassi di lavoro", "Armature", "Stabilità globale"];

    /// <summary>Filters of «Riepilogo e dettagli», in the order of the choice.</summary>
    public static IReadOnlyList<string> CheckFilters { get; } = ["Riepilogo completo", "Combinazione selezionata", "Tutti i controlli", "Sollecitazioni numeriche",
        "Calcolo delle spinte", "Stabilità globale", "Portanza sismica", "Cedimenti e spostamenti", "Dettagli armature"];

    /// <summary>Members of the diagrams.</summary>
    public static IReadOnlyList<string> Members { get; } = ["Fusto", "Valle", "Monte"];

    /// <summary>Switches of the drawing of the inputs («Carichi», «Dati», «Armature», «Spinte»).</summary>
    public static IReadOnlyList<string> DrawingSwitches { get; } = ["Carichi", "Dati", "Armature", "Spinte"];

    /// <summary>The fields of a group of parameters of the model (geometry, foundation, the strengths of a gravity wall).</summary>
    public static FieldSpec[] Parameters(IEnumerable<WallModel.Parameter> parameters) => parameters.Select(p => new FieldSpec(p.Key, p.Label, p.Unit, Symbol: p.Symbol)).ToArray();

    // ---------------------------------------------------------------- soils

    public static IReadOnlyList<FieldSpec> Valley { get; } = [new("height_mode", "Tratto libero a valle", Choices: ["Interamente libero", "Assegnato"]),
        new("free_height", "Dalla sommità al terreno di valle", "m", Symbol: "Hlib"), new("linked", "Modifica insieme le due colonne", Bool: true),
        new("passive", "Considera la resistenza passiva a valle", Bool: true), new("mobilization", "Frazione mobilitata (0–0,5)", "−", Symbol: "ηp")];

    /// <summary>Columns of the two soil columns (valley and uphill).</summary>
    public static IReadOnlyList<FieldSpec> SoilColumns { get; } = [new("name", "Terreno"), new("thickness", "Δz\n[m]"), new("gamma", "γ\n[kN/m³]"),
        new("gamma_sat", "γsat\n[kN/m³]"), new("phi", "φ′\n[°]"), new("__to", "Fondo z\n[m]", ReadOnly: true)];

    public static IReadOnlyList<FieldSpec> Interfaces { get; } = [new("wall_mode", "Attrito muro–terreno", Choices: WallModel.FrictionModes),
        new("wall_delta", "δ muro caratteristico assegnato", "°"), new("wall_phi_cv", "φcv,k terreno a contatto col muro", "°"),
        new("base_mode", "Attrito terreno–fondazione", Choices: WallModel.FrictionModes), new("base_phi_cv", "φcv,k terreno di fondazione", "°")];

    public static IReadOnlyList<FieldSpec> Water { get; } = [new("enabled", "Presenza falda", Bool: true), new("depth", "Profondità da sommità monte", "m", Symbol: "zf"),
        new("front_head", "Battente a valle dal piano di posa", "m", Symbol: "hw,v")];

    public static IReadOnlyList<FieldSpec> Foundation { get; } = Parameters(WallModel.FoundationFields);

    public static IReadOnlyList<FieldSpec> BearingSeismic { get; } = [new("source", "Accelerazione fondazione", Choices: ["Da sito", "Assegnata"]),
        new("ground_kh", "Accelerazione orizzontale / g", "−"), new("ground_kv", "Accelerazione verticale / g (modulo)", "−"), new("model_factor", "Fattore modello γRD", "−")];

    public static IReadOnlyList<FieldSpec> Serviceability { get; } = [new("settlement", "Calcola cedimenti finali", Bool: true),
        new("displacement", "Calcola spostamenti in testa", Bool: true), new("removed_pressure", "Pressione del terreno rimosso", "kPa"),
        new("rigid_base", "Substrato rigido documentato al fondo degli strati", Bool: true), new("settlement_limit", "Limite cedimento", "mm"),
        new("rotation_limit", "Limite rotazione", "rad"), new("head_limit", "Limite spostamento in testa", "mm"),
        new("horizontal_stiffness", "Rigidezza orizzontale fondazione per metro", "kN/m²")];

    /// <summary>Columns of the deformable layers under the foundation.</summary>
    public static IReadOnlyList<FieldSpec> ServiceLayerColumns { get; } = [new("name", "Strato sotto posa"), new("thickness", "Spessore [m]"), new("modulus", "M edometrico [kPa]")];

    // ---------------------------------------------------------------- global stability

    public static IReadOnlyList<FieldSpec> Global { get; } = [new("enabled", "Attiva la verifica globale", Bool: true),
        new("condition", "Condizione del terreno", Choices: ["Drenata", "Non drenata"]),
        new("profile_confirmed", "Ho controllato profilo, strati e falda del sito", Bool: true),
        new("water_enabled", "Considera la falda", Bool: true), new("seismic", "Includi il sisma SLV", Bool: true),
        new("seismic_source", "Coefficienti sismici", Choices: ["Da sito · βs=0,38", "kh e kv assegnati"]),
        new("kh", "Coefficiente orizzontale", "−"), new("kv", "Modulo coefficiente verticale", "−"),
        new("soil_mode", "Stratigrafia globale", Choices: ["Profilo unico", "Due colonne"]), new("soil_split_x", "Confine valle / monte", "m")];

    /// <summary>The groups of the global form; the first one is open when groundwater or the seismic action are considered.</summary>
    public static IReadOnlyList<RetainingWallFieldGroup> GlobalGroups(JsonNode? global) => [
        new("Falda e sisma · ripresi dal muro, modificabili", ["water_enabled", "seismic", "seismic_source", "kh", "kv"], global.B("water_enabled") || global.B("seismic")),
        new("Modello del terreno · dettagli", ["soil_mode", "soil_split_x"], false)];

    public static IReadOnlyList<FieldSpec> GlobalSearchMode { get; } = [new("search_mode", "Area di ricerca", Choices: ["Automatica", "Assegnata"])];

    public static IReadOnlyList<FieldSpec> GlobalSearch { get; } = [new("exit_min", "Uscita a valle · x minima", "m"), new("exit_max", "Uscita a valle · x massima", "m"),
        new("entry_min", "Ingresso a monte · x minima", "m"), new("entry_max", "Ingresso a monte · x massima", "m"),
        new("depth_min", "Profondità minima sotto la fondazione", "m"), new("depth_max", "Profondità massima sotto la fondazione", "m"),
        new("grid", "Nodi per direzione"), new("slices", "Conci iniziali"), new("refinements", "Raffinamenti locali")];

    /// <summary>Limits of the search, written by the automatic proposal and disabled in the automatic mode.</summary>
    public static IReadOnlyList<string> GlobalSearchLimits { get; } = ["exit_min", "exit_max", "entry_min", "entry_max", "depth_min", "depth_max"];

    /// <summary>Columns of the points of the profile (valley, uphill, groundwater).</summary>
    public static IReadOnlyList<FieldSpec> PointColumns { get; } = [new("x", "x [m]"), new("y", "y [m]")];

    /// <summary>Columns of the deep layers of the global stability (the thickness is a column of the display copy).</summary>
    public static IReadOnlyList<FieldSpec> GlobalLayerColumns { get; } = [new("name", "Terreno"), new("__thickness", "Spessore [m]"), new("bottom", "Fondo y [m]", ReadOnly: true)];

    /// <summary>The properties of the selected deep layer.</summary>
    public static IReadOnlyList<FieldSpec> GlobalSoil { get; } = [new("gamma", "Peso naturale", "kN/m³", Symbol: "γ"), new("gamma_sat", "Peso saturo", "kN/m³", Symbol: "γsat"),
        new("phi", "Angolo di attrito caratteristico", "°", Symbol: "φ′k"), new("c", "Coesione efficace caratteristica", "kPa", Symbol: "c′k"),
        new("cu", "Resistenza non drenata caratteristica", "kPa", Symbol: "cu,k")];

    /// <summary>Columns of the matrix of the global combinations: the factors, then one column for each action.</summary>
    public static FieldSpec[] GlobalMatrix(JsonObject data)
    {
        var fields = new List<FieldSpec> { new("enabled", "Usa", Bool: true), new("name", "Combinazione"), new("state", "Stato", Choices: ["SLU", "SISMA", "ECCEZIONALE"]),
            new("soil", "γ terra"), new("wall", "γ muro"), new("mphi", "γMφ"), new("mc", "γMc′"), new("mcu", "γMcu"), new("r", "γR"), new("kh", "kh"), new("kv", "kv") };
        foreach (var action in data.Array("actions")) fields.Add(new("a_" + action.S("id"), action.S("name")));
        return [.. fields];
    }

    // ---------------------------------------------------------------- materials

    /// <summary>The material form: concrete and steel of the catalogues (the saved name also when it is not an item), the
    /// coefficients, cover, exposure and creep, then the strengths of a gravity wall.</summary>
    public static FieldSpec[] Materials(JsonObject materials)
    {
        string[] Choices(JsonObject[] catalog, string key) => catalog.Select(x => x.S("nome")).Append(materials.S(key)).Append("Personalizzato").Distinct().ToArray();
        return new FieldSpec[] {
            new("gamma", "Peso specifico muro", "kN/m³"),
            new("classe_cls", "Calcestruzzo da normativa", Choices: Choices(MaterialInputCatalog.Concrete("NTC 2018"), "classe_cls")),
            new("materiale_cls_nome", "Nome CLS personalizzato"), new("fck", "fck", "MPa"),
            new("cls_diagramma", "Diagramma CLS", Choices: MaterialInputCatalog.ConcreteDiagrams, Note: FieldNotes.StressBlock),
            new("__fcd", "fcd", "MPa", ReadOnly: true), new("__ecm", "Ecm", "MPa", ReadOnly: true),
            // DET-3: steel saved with the name of the catalogue and other values (B450A with εu = 30 ‰), declared as in the c.a. section and in the pile.
            new("classe_acciaio", "Acciaio da normativa", Choices: Choices(MaterialInputCatalog.Steel(false, "NTC 2018"), "classe_acciaio"), Note: WallModel.SteelDifference),
            new("materiale_acciaio_nome", "Nome acciaio personalizzato"), new("fyk", "fyk", "MPa"), new("steel_modulus_mpa", "Es", "MPa"),
            new("steel_fu_mpa", "fu", "MPa"), new("steel_eps_u", "εu", "‰"), new("steel_diagramma", "Diagramma acciaio", Choices: ["Elastoplastico", "Incrudente"]),
            new("__fyd", "fyd", "MPa", ReadOnly: true), new("alpha_cc", "αcc"), new("gamma_c", "γc"), new("gamma_s", "γs"),
            new("cover", "Copriferro netto adottato", "mm"), new("exposure", "Esposizione", Choices: ConcreteEditor.ExposureChoices.Skip(1).ToArray()),
            new("creep", "Viscosità SLE", "−")
        }.Concat(Parameters(WallModel.MaterialFields.Where(f => f.Key.EndsWith("_rd", StringComparison.Ordinal)))).ToArray();
    }

    /// <summary>The groups of the material form.</summary>
    public static IReadOnlyList<RetainingWallFieldGroup> MaterialGroups { get; } = [
        new("Calcestruzzo · verificatore sezioni", ["classe_cls", "materiale_cls_nome", "fck", "cls_diagramma", "__fcd", "__ecm"], true),
        new("Acciaio per armature · verificatore sezioni", ["classe_acciaio", "materiale_acciaio_nome", "fyk", "steel_modulus_mpa", "steel_fu_mpa", "steel_eps_u", "steel_diagramma", "__fyd"], true),
        new("Coefficienti materiali NTC 2018", ["alpha_cc", "gamma_c", "gamma_s"], false)];

    /// <summary>Durability and cover, saved in «detailing».</summary>
    public static IReadOnlyList<FieldSpec> Cover { get; } = [new("aggregate", "Diametro massimo aggregato", "mm"), new("life", "Vita utile", Choices: ["50", "100"]),
        new("cover_deviation", "Tolleranza copriferro", "mm"), new("cover_method", "Metodo copriferro", Choices: ["NTC + Circ. 2019", "EC2 2004"]),
        new("cover_element", "Elemento NTC", Choices: ["Trave / pilastro", "Piastra / soletta / parete"]), new("cover_quality", "Controllo qualità NTC", Bool: true),
        new("cover_ground", "Superficie di getto", Choices: ["Casseratura", "Terreno preparato", "Direttamente su terra"]),
        new("cover_abrasion", "Abrasione", Choices: ["Nessuno", "XM1", "XM2", "XM3"]),
        new("cover_high_strength", "Riduzione EC2 per resistenza", Bool: true), new("cover_slab", "Riduzione EC2 per piastra", Bool: true),
        new("cover_ec_quality", "Controllo qualità EC2", Bool: true), new("cover_rough", "Superficie irregolare", Bool: true)];

    public static IReadOnlyList<FieldSpec> GravityDesign { get; } = [new("type", "Modello materiale", Choices: ["Resistenze assegnate", "Calcestruzzo non armato", "Muratura"]),
        new("fk", "Resistenza caratteristica muratura", "MPa"), new("fvk0", "Resistenza a taglio senza compressione", "MPa"),
        new("fvk_limit", "Limite caratteristico a taglio", "MPa"), new("gamma_m", "Coefficiente γM", "−"),
        new("confidence", "Fattore di confidenza", "−"), new("elastic_modulus", "Modulo elastico muratura / assegnato", "MPa"),
        new("eccentricity", "Imperfezione aggiuntiva (minimo H/200)", "mm"), new("effective_height", "Lunghezza efficace (vuoto = 2H)", "m")];

    // ---------------------------------------------------------------- geometry and reinforcement

    public static IReadOnlyList<FieldSpec> Geometry { get; } = Parameters(WallModel.GeometryFields);

    public static IReadOnlyList<FieldSpec> Zones { get; } = [new("two_zones", "Due zone verticali di armatura", Bool: true),
        new("lower_height", "Altezza zona inferiore dal piede del fusto", "m", Symbol: "h₁")];

    public static IReadOnlyList<RetainingWallRebarZone> RebarZones { get; } = [new("stem", "Fusto · zona inferiore / intera altezza"), new("stem_upper", "Fusto · zona superiore"),
        new("toe", "Mensola a valle"), new("heel", "Mensola a monte")];

    public static IReadOnlyList<FieldSpec> Rebar { get; } = [
        new("diameter", "Ø monte (fusto) / inferiore (soletta)", "mm"), new("count", "Barre principali per metro", "−"),
        new("symmetric", "Stesse barre sulla faccia opposta", Bool: true),
        new("opposite_diameter", "Ø valle (fusto) / superiore (soletta)", "mm"), new("opposite_count", "Barre opposte per metro", "−"),
        new("secondary_diameter", "Ø rete secondaria su entrambe le facce", "mm"), new("secondary_spacing", "Passo rete secondaria", "mm"),
        new("anchor_length", "Ancoraggio (0 = automatico)", "mm"), new("lap_length", "Sovrapposizione (0 = automatica)", "mm"),
        new("bend_diameter", "Diametro mandrino (0 = automatico)", "mm")];

    public static IReadOnlyList<FieldSpec> BarSchedule { get; } = [new("panel_length", "Lunghezza del tratto da armare", "m"),
        new("end_cover", "Copriferro alle estremità del tratto", "mm"), new("stock_length", "Lunghezza barra commerciale", "m"),
        new("ties_per_m", "Collegamenti per fila e per metro di muro", "−"), new("tie_cut_length", "Sviluppo collegamento (0 = da definire)", "mm")];

    public static IReadOnlyList<FieldSpec> Detailing { get; } = [new("enabled", "Verifica i dettagli delle armature", Bool: true),
        new("good_bond", "Condizioni di buona aderenza documentate", Bool: true), new("lap_percent", "Percentuale barre giuntate (schema: 100%)", "%"),
        new("lap_clear", "Distanza libera fra barre giuntate", "mm"), new("tie_diameter", "Ø collegamenti della giunzione", "mm"),
        new("tie_spacing", "Passo collegamenti della giunzione", "mm"), new("max_diameter", "Ø massimo nel predimensionamento", "mm"),
        new("max_count", "Numero massimo barre per metro", "−"), new("target_ratio", "Tasso massimo ricercato", "−")];

    // ---------------------------------------------------------------- actions and seismic action

    /// <summary>Columns of the table of the actions.</summary>
    public static IReadOnlyList<FieldSpec> ActionColumns { get; } = [new("enabled", "Calcola", Bool: true), new("visible", "Disegna", Bool: true), new("name", "Azione"),
        new("type", "Tipo", ReadOnly: true), new("category", "Natura", ReadOnly: true), new("value", "Valore", ReadOnly: true)];

    /// <summary>The form of the selected action; the unit of the intensity follows its type.</summary>
    public static FieldSpec[] Action(string type) => [new("name", "Nome"), new("type", "Tipo", Choices: WallModel.ActionTypes),
        new("category", "Natura", Choices: WallModel.ActionCategories), new("value", "Intensità caratteristica", WallModel.ActionUnit(type)),
        new("z", "Quota di applicazione / estremo superiore", "m", Symbol: "z₁"), new("z0", "Quota inferiore della pressione", "m", Symbol: "z₀"),
        new("x", "Ascissa dal bordo a valle", "m", Symbol: "x"), new("psi0", "Accompagnamento SLU / rara", "−", Symbol: "ψ₀"),
        new("psi1", "Frequente", "−", Symbol: "ψ₁"), new("psi2", "Quasi permanente", "−", Symbol: "ψ₂"), new("group", "Gruppo correlato (vuoto = indipendente)")];

    public static IReadOnlyList<FieldSpec> Seismic { get; } = [new("enabled", "Considera il sisma", Bool: true),
        new("method", "Comportamento del muro / modello di spinta", Choices: ["Mononobe–Okabe", "Wood semplificato"]),
        new("source", "Come definire l’azione sismica", Choices: [WallModel.SeismicSite, WallModel.SeismicManual]),
        new("ag_g", "Accelerazione su suolo rigido allo SLV · ag/g", "−"),
        new("ss_mode", "Amplificazione del sottosuolo · Ss", Choices: [WallModel.AmplificationCalculated, WallModel.AmplificationAssigned]),
        new("soil_class", "Categoria dalla relazione geotecnica", Choices: ["Da scegliere", "A", "B", "C", "D", "E"]),
        new("f0", "Fattore spettrale del sito allo SLV · F₀", "−"), new("ss", "Coefficiente Ss assegnato", "−"),
        new("st_mode", "Amplificazione topografica · St", Choices: [WallModel.AmplificationCalculated, WallModel.AmplificationAssigned]),
        new("topography", "Forma del terreno circostante", Choices: [WallModel.TopographyFlat, WallModel.TopographySlope, WallModel.TopographyRidge]),
        new("slope", "Inclinazione media del pendio / rilievo", "°"),
        new("relief_height", "Altezza del pendio / rilievo (non del muro)", "m"), new("site_height", "Quota del muro sopra la base del pendio / rilievo", "m"),
        new("st", "Coefficiente St assegnato", "−"), new("kh", "Coefficiente orizzontale kh assegnato", "−"), new("kv", "Coefficiente verticale |kv| assegnato", "−")];

    /// <summary>The form of an accelerogram of the serviceability (Newmark).</summary>
    public static IReadOnlyList<FieldSpec> History { get; } = [new("enabled", "Calcola questo accelerogramma", Bool: true), new("name", "Nome"),
        new("state", "Stato limite", Choices: ["SLD", "SLV"]), new("yield_g", "Accelerazione critica di scorrimento ky/g", "−"),
        new("scale", "Fattore di scala", "−"), new("limit_mm", "Limite spostamento permanente", "mm"),
        new("compatible", "Accelerogramma e scala compatibili con il sito e lo stato limite", Bool: true)];

    /// <summary>Columns of the combination matrix: the factors, one column for each action (γ × ψ), the resistances, the surcharge on the
    /// heel, kh and kv, and the seismic purpose when a row has one.</summary>
    public static FieldSpec[] Matrix(JsonObject data)
    {
        var fields = new List<FieldSpec> { new("enabled", "Usa", Bool: true), new("name", "Combinazione"), new("state", "Stato", Choices: WallModel.States),
            new("approach", "A / M / R", ReadOnly: true), new("wall", "γ muro"), new("soil", "γ monte"), new("valley_soil", "γ valle"), new("water", "γ acqua") };
        foreach (var (a, i) in data.Array("actions").Select((a, i) => (a, i))) fields.Add(new("a_" + a.S("id"), $"{i + 1} · {a.S("name")}\nγ × ψ"));
        fields.AddRange([new("mphi", "γMφ"), new("rslide", "γR\nscorr."), new("rover", "γR\nribalt."), new("rbearing", "γR\nport."), new("rpassive", "γR,e\npassiva"),
            new("heel_surcharge", "q var.\nmensola", Bool: true), new("kh", "kh"), new("kv", "kv")]);
        if (data.Array("combinations").Any(c => c.S("purpose") != "")) fields.Add(new("purpose", "Uso sisma", Choices: ["", "Generale", "Ribaltamento"]));
        return [.. fields];
    }

    /// <summary>Columns of the window of the values used in the calculation.</summary>
    public static IReadOnlyList<FieldSpec> ParameterColumns { get; } = [new("group", "Gruppo", ReadOnly: true), new("meaning", "Parametro", ReadOnly: true),
        new("value", "Valore assegnato"), new("unit", "Unità", ReadOnly: true)];
}
