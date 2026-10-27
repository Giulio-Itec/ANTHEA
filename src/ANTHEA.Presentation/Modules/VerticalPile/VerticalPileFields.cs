using System.Text.Json.Nodes;
using Anthea.Application.Presentation;
using Anthea.Calculations;
using Anthea.Presentation.Forms;

namespace Anthea.Presentation.Modules.VerticalPile;

/// <summary>
/// The fields of the vertical pile and micropile sheet (F5.6), one definition for the historic view (GeoEditor, until it is archived)
/// and for <see cref="VerticalPileSheetViewModel"/>: the three forms in their order, the columns of the stratigraphies (with the values of
/// older sheets added to the choices) and the views of the capacity chart. Moved without changes from GeoEditor.BuildGeo and
/// RebuildSondages. Every field keeps the rule of InputForm: the raw text goes into the document.
/// </summary>
public static class VerticalPileFields
{
    /// <summary>Fields of «Dati generali», in the order of the card (the method field is hidden by the sheet).</summary>
    public static FieldSpec[] General(bool micro)
    {
        var fields = new List<FieldSpec>();
        if (!micro) fields.AddRange([new("tipo_palo", "Tipo di palo", Choices: ["Trivellato", "Elica continua", "Battuto"]), new("sottotipo_palo_battuto", "Tipo battuto", Choices: PileInputCatalog.DrivenPileTypes)]);
        fields.AddRange([new("diametro", micro ? "Diametro perforazione Db" : "Diametro palo D", "m"), new("lunghezza", "Lunghezza palo L", "m"), new("profondita_testa", "Profondità della testa sotto il piano campagna d", "m", Default: "0"), new("peso_specifico_palo", "Peso specifico CLS / palo", "kN/m³"), new("azione_compressione", "Azione assiale di progetto — Compressione", "kN"), new("azione_trazione", "Azione assiale di progetto — Trazione", "kN")]);
        if (micro) fields.AddRange([new("metodo_micropalo", "Metodo (richiesto)", Choices: [PileInputCatalog.MicropileMethod]), new("tipo_iniezione", "Iniezione", Choices: ["IGU", "IRS"]), new("profilo_chs", "Profilo CHS", Choices: PileInputCatalog.ChsProfiles), new("pressione_iniezione", "Pressione p_i = p_l", "MPa"), new("inclinazione", "Inclinazione dalla verticale θ", "°"), new("inizio_aderenza", "Inizio aderenza sb lungo asse", "m"), new("considera_punta", "Considera punta", Bool: true), new("percentuale_punta", "Punta rispetto alla laterale", "%")]);
        else fields.AddRange([new("presenza_falda", "Presenza falda", Bool: true), new("profondita_falda", "Profondità falda zf", "m"), new("considera_sottospinta", "Considera sottospinta", Bool: true), new("metodo_nq", "Metodo Nq", Choices: ["Parametrizzata"])]);
        string[] order = micro ? ["tipo_iniezione", "diametro", "lunghezza", "profondita_testa", "inizio_aderenza", "considera_punta", "percentuale_punta", "pressione_iniezione", "profilo_chs", "peso_specifico_palo", "azione_compressione", "azione_trazione", "inclinazione", "metodo_micropalo"] : ["tipo_palo", "sottotipo_palo_battuto", "diametro", "lunghezza", "profondita_testa", "peso_specifico_palo", "presenza_falda", "profondita_falda", "considera_sottospinta", "azione_compressione", "azione_trazione", "metodo_nq"];
        if (micro)
        {
            fields[fields.FindIndex(f => f.Key == "diametro")] = new("diametro", "Diametro perforazione", "m", Symbol: "Db");
            fields[fields.FindIndex(f => f.Key == "peso_specifico_palo")] = new("__peso_lineare", "Peso al metro del micropalo", "kN/m", ReadOnly: true, Symbol: "qk");
            order[Array.IndexOf(order, "peso_specifico_palo")] = "__peso_lineare";
        }
        return fields.OrderBy(f => Array.IndexOf(order, f.Key)).ToArray();
    }

    /// <summary>The method field of «Dati generali» that the sheet hides.</summary>
    public static string HiddenMethod(bool micro) => micro ? "metodo_micropalo" : "metodo_nq";

    /// <summary>Fields of «Efficienza».</summary>
    public static FieldSpec[] Efficiency() => [new("metodo", "Metodo", Choices: ["Nessuna riduzione", "Converse-Labarre", "Feld", "Definita dall'utente"]), new("numero_pali_x", "Pali in X"), new("numero_pali_y", "Pali in Y"), new("interasse_x", "Interasse X", "m"), new("interasse_y", "Interasse Y", "m"), new("eta_compressione", "ηg,c manuale"), new("eta_trazione", "ηg,t manuale")];

    /// <summary>Fields of «Coefficienti normativa»; the editable ones are those of «Reset».</summary>
    public static FieldSpec[] Normative() => [new("verticali_indagate", "Verticali indagate", Choices: PileInputCatalog.InvestigatedProfiles), new("__xi3", "Correlazione ξ3", ReadOnly: true), new("__xi4", "Correlazione ξ4", ReadOnly: true), new("sicurezza_laterale_compressione", "Sicurezza laterale — Compressione γs"), new("sicurezza_laterale_trazione", "Sicurezza laterale — Trazione γt"), new("sicurezza_base", "Sicurezza di base γb"), new("peso_palo_sfavorevole", "Peso proprio palo — Sfavorevole γG"), new("peso_palo_favorevole", "Peso proprio palo — Favorevole γG")];

    /// <summary>
    /// Columns of the table of a stratigraphy: the calculated ones are read-only; the values of older sheets that are not choices are
    /// added to the choices of their column, so that the data of the user are shown and never replaced silently.
    /// </summary>
    public static FieldSpec[] Layers(bool micro, JsonArray rows)
    {
        var fields = micro ? new List<FieldSpec> { new("laterale_attiva", "Laterale", Bool: true), new("terreno", "Terreno", Choices: PileInputCatalog.MicropileSoils), new("spessore", "Spessore verticale [m]"), new("alpha", "α adottato"), new("__tau", "Aderenza τ [kPa]", ReadOnly: true) } :
            [new("__strato", "Strato", ReadOnly: true), new("laterale_attiva", "Laterale attiva", Bool: true), new("tipologia", "Tipologia", Choices: ["Granulare", "Coesivo"]), new("addensamento", "Addensamento", Choices: ["Sciolto", "Denso"]), new("spessore", "Spessore S [m]"), new("peso_specifico", "γ [kN/m³]"), new("peso_specifico_saturo", "γsat [kN/m³]"), new("angolo_attrito", "φ′ [°]"), new("coesione_efficace", "c′ [kPa]"), new("coesione_non_drenata", "Cu [kPa]"), new("__k", "Coeff. k [-]", ReadOnly: true), new("__mu", "Coeff. μ [-]", ReadOnly: true), new("__alfa", "Coeff. α [-]", ReadOnly: true), new("__nq", "Fattore Nq [-]", ReadOnly: true), new("nc", "Fattore Nc [-]")];
        for (int i = 0; i < fields.Count; i++) if (fields[i].Choices is not null) fields[i] = fields[i] with { Choices = fields[i].Choices!.Concat(rows.Select(r => r.S(fields[i].Key))).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().ToArray() };
        return [.. fields];
    }

    /// <summary>Views of the capacity chart: every design curve, then one condition at a time.</summary>
    public static string[] CapacityViews(bool micro) => micro ? ["Tutte - progetto", "Compressione", "Trazione"]
        : ["Tutte - progetto", "Drenante · Compressione", "Drenante · Trazione", "Non drenante · Compressione", "Non drenante · Trazione"];
}
