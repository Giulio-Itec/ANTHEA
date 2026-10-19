using Anthea.Presentation.Forms;

namespace Anthea.Presentation.Modules.EmbeddedWalls;

public static class EmbeddedWallFields
{
    public static FieldSpec[] General() =>
    [
        new("name", "Nome", Wide: true), new("case_id", "Caso / combinazione", Wide: true), new("action_basis", "Provenienza e coefficienti delle azioni", Wide: true, Multiline: true),
        new("top", "Quota testa", "m"), new("bottom", "Quota piede", "m"), new("width", "Larghezza della fascia analizzata", "m"), new("mesh", "Lunghezza massima elemento", "m"),
        new("substeps", "Incrementi iniziali per fase"), new("check_kind", "Verifiche di sezione", Choices: ["Analisi", "Ultimate", "Characteristic", "Frequent", "QuasiPermanent"],
            ToolTip: "Analisi: sole sollecitazioni. Ultimate: SLU; Characteristic, Frequent, QuasiPermanent: SLE della combinazione esplicitamente assegnata. Non sono applicati coefficienti alle azioni."),
        new("fix_top_u", "Blocca traslazione in testa", Bool: true), new("fix_top_rotation", "Blocca rotazione in testa", Bool: true),
        new("fix_bottom_u", "Blocca traslazione al piede", Bool: true), new("fix_bottom_rotation", "Blocca rotazione al piede", Bool: true)
    ];
    public static string Title(string table) => table switch
    {
        "segments" => "Tratti e rigidezze", "left_soils" => "Terreno a sinistra", "right_soils" => "Terreno a destra", "supports" => "Tiranti e puntoni",
        "stages" => "Fasi costruttive", "activations" => "Sostegni presenti nelle fasi", "loads" => "Carichi sulla struttura", "rc_sections" => "Sezioni e armature", _ => table
    };
    public static FieldSpec[] Columns(string table) => table switch
    {
        "segments" => [new("section", "Sezione"), new("top", "Quota superiore", "m"), new("bottom", "Quota inferiore", "m"), new("ei", "Rigidezza EI", "kNm²"), new("weight", "Peso lineare della fascia", "kN/m"), new("source", "Fonte di EI")],
        "left_soils" or "right_soils" => [new("name", "Strato"), new("top", "Quota superiore", "m"), new("bottom", "Quota inferiore", "m"),
            new("gamma", "Peso naturale", "kN/m³"), new("gamma_sat", "Peso saturo", "kN/m³"), new("phi", "Attrito efficace", "°"), new("c", "Coesione efficace", "kPa"),
            new("cu", "Resistenza non drenata", "kPa"), new("k0", "Coefficiente a riposo K0"), new("kh", "Modulo orizzontale kh", "N/mm³"),
            new("drainage", "Condizione", Choices: ["Drained", "UndrainedTotalStress"], ToolTip: "Drained: tensioni efficaci. UndrainedTotalStress: analisi non drenata in tensioni totali, cu obbligatoria."),
            new("source", "Fonte dei parametri"), new("stiffness_source", "Fonte di kh")],
        "supports" => [new("id", "Identificativo"), new("elevation", "Quota", "m"), new("kind", "Tipo", Choices: ["Anchor", "Strut", "Bilateral"], ToolTip: "Anchor: tirante; Strut: puntone; Bilateral: vincolo bilaterale."),
            new("stiffness", "Rigidezza assiale EA/L", "kN/m"), new("angle", "Inclinazione (positiva in basso)", "°"), new("direction", "Verso resistente (+1 / −1)"),
            new("spacing", "Interasse", "m"), new("capacity", "Capacità plastica per elemento (facoltativa)", "kN")],
        "stages" => [new("id", "Fase"), new("left_ground", "Terreno sinistra", "m"), new("right_ground", "Terreno destra", "m"),
            new("left_water", "Falda sinistra (vuoto: assente)", "m"), new("right_water", "Falda destra (vuoto: assente)", "m"),
            new("left_surcharge", "Sovraccarico sinistra", "kPa"), new("right_surcharge", "Sovraccarico destra", "kPa"),
            new("distributed_load", "Carico orizzontale della fascia", "kN/m"), new("stiffness_factor", "Moltiplicatore EI")],
        "activations" => [new("stage", "Fase"), new("support", "Sostegno"), new("prestress", "Pretensione per elemento", "kN")],
        "loads" => [new("stage", "Fase"), new("elevation", "Quota", "m"), new("horizontal", "Forza verso destra", "kN"), new("moment", "Coppia coniugata alla rotazione", "kNm"), new("downward", "Forza verso il basso", "kN")],
        "rc_sections" => [new("id", "Sezione"), new("width", "Larghezza della fascia", "mm"), new("thickness", "Spessore", "mm"), new("cover", "Copriferro netto alle barre", "mm"),
            new("back_bars", "Numero barre retro (sinistra)"), new("back_diameter", "Diametro retro", "mm"), new("front_bars", "Numero barre fronte (destra)"), new("front_diameter", "Diametro fronte", "mm"),
            new("fck", "Resistenza cilindrica calcestruzzo", "MPa"), new("fyk", "Snervamento armatura", "MPa"), new("es", "Modulo armatura", "MPa"), new("eps_uk", "Deformazione ultima armatura"),
            new("creep", "Coefficiente di viscosità"), new("exposure", "Classe di esposizione")],
        _ => []
    };
}
