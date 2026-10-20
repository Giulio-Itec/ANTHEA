using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Anthea.ConcreteWorkspace;

namespace Anthea.Application.Concrete;

/// <summary>Transactional editor recipes. The preview and acceptance use the same GPC geometry/model preparation.</summary>
public static class ConcreteTemplateWizard
{
    public static ImmutableArray<string> Choices { get; } =
    [
        "Rettangolare · doppia fila sopra e sotto", "Rettangolare cava · due contorni",
        "Circolare · due anelli", "Circolare cava · anello interno ed esterno",
        "A T", "A I", "Ellittica · armatura radiale", "Capsula · curve e rettilinei", "A L", "A C · canale aperto",
        "Trapezoidale", "Poligonale regolare"
    ];
    public static JsonObject Draft(JsonObject input)
    {
        var draft = input.DeepClone().AsObject();
        draft.Remove(ConcreteSectionArchive.Key);
        draft.Remove("barre_manuali");
        if (draft.S("wizard_type") == "Capsula · armatura radiale") draft["wizard_type"] = Choices[7];
        if (!Choices.Contains(draft.S("wizard_type"))) Select(draft, Choices[0]);
        if (draft.S("shape") == "Capsula" && !draft.ContainsKey("wizard_stadium_layout"))
            draft["wizard_stadium_layout"] = ConcreteTemplateAdapter.StadiumLegacy;
        StadiumDefaults(draft);
        return draft;
    }

    public static void Select(JsonObject input, string choice)
    {
        int index = Choices.IndexOf(choice);
        if (index < 0) throw new ArgumentException("Scegliere una tipologia del wizard.");
        input["wizard_type"] = choice;
        input["shape"] = index switch { 0 or 1 => "Rettangolare", 2 or 3 => "Circolare", 4 => "A T", 5 => "A I", 6 => "Ellittica", 7 => "Capsula", 8 => "A L", 9 => "A C", 10 => "Trapezoidale", _ => "Poligonale regolare" };
        input["foro_presente"] = index is 1 or 3;
        input["wizard_layout"] = index is 1 or 3 ? "Doppio contorno" : "Perimetro";
        input["second_top_enabled"] = index == 0; input["second_bottom_enabled"] = index == 0;
        input["second_inner_enabled"] = index == 2;
        if (index == 7) input["wizard_stadium_layout"] = ConcreteTemplateAdapter.StadiumSeparate;
        input.Remove("barre_manuali"); input.Remove(ConcreteSectionArchive.Key);
        foreach (var (key, value) in new[] {
            ("width_mm","1000"), ("height_mm", index is 6 or 7 ? "1600" : "1200"),
            ("diameter_mm","1000"), ("circular_sides","96"),
            ("flange_width_mm","1000"), ("web_width_mm","300"), ("flange_thickness_mm","300"),
            ("bottom_flange_width_mm","800"), ("bottom_flange_thickness_mm","250"),
            ("inner_width_mm","400"), ("inner_height_mm","500"), ("inner_diameter_mm","400"),
            ("wizard_bar_diameter_mm","20"), ("wizard_bar_spacing_mm","180"),
            ("wizard_radial_count","24"), ("wizard_inner_count","16"), ("wizard_polygon_sides","8"),
            ("top_bar_count","4"), ("top_bar_diameter_mm","20"), ("bottom_bar_count","4"), ("bottom_bar_diameter_mm","20"),
            ("side_bar_count_per_side",index == 4 ? "1" : "2"), ("side_bar_diameter_mm","16"),
            ("longitudinal_bar_count","24"), ("longitudinal_bar_diameter_mm","20"), ("flange_bottom_count","0"),
            ("second_top_count","4"), ("second_bottom_count","4"), ("second_inner_count","16"),
            ("second_top_diameter","20"), ("second_bottom_diameter","20"), ("second_inner_diameter","20"),
            ("second_top_gap","40"), ("second_bottom_gap","40"), ("second_inner_gap","60") })
            input[key] = value;
        StadiumDefaults(input, reset: true);
    }

    private static void StadiumDefaults(JsonObject input, bool reset = false)
    {
        foreach (var (key, value) in new[] { ("wizard_stadium_curve_count", "9"),
            ("wizard_stadium_curve_diameter_mm", "20"), ("wizard_stadium_straight_count", "4"),
            ("wizard_stadium_straight_diameter_mm", "20") })
            if (reset || !input.ContainsKey(key)) input[key] = value;
    }

    public static ConcreteGeometry Preview(JsonObject draft) => new(new SezioneCA(draft));
    public static string Note(JsonObject draft) => ConcreteTemplateAdapter.Uses(draft)
        ? (draft.S("shape") == "Capsula" ? (ConcreteTemplateAdapter.SeparateStadium(draft)
          ? "Capsula: conteggi per ciascuna semicirconferenza e per ciascun rettilineo; le barre di tangenza appartengono agli archi. "
          : "Capsula: disposizione radiale precedente, riferita al centro della sezione. ") +
          "Disponibili i dettagli generali del pilastro; il confinamento sismico della pila richiede una verifica separata. " : "") +
          "Staffe nei singoli elementi, con copriferro assegnato. Verificare piegature, ancoraggi e trattenimento delle barre. " +
          "Il taglio richiede bw, d, Asl e bracci efficaci assegnati per direzione; non viene ricavato automaticamente dallo schema. " +
          "L’applicazione azzera i precedenti parametri del taglio. Torsione: modello automatico limitato a rettangolo e cerchio."
        : "Disposizione geometrica iniziale. Confermare ancoraggi, bracci efficaci delle staffe e dettagli costruttivi nelle rispettive schede.";

    public static void Apply(JsonObject data, JsonObject draft)
    {
        var candidate = draft.DeepClone().AsObject();
        candidate.Remove(ConcreteSectionArchive.Key);
        var workspace = data["workspace_ca"] as JsonObject ?? new JsonObject();
        var candidateWorkspace = workspace.DeepClone().AsObject();
        ConcreteCalculationSettings.Prepare(candidate, candidateWorkspace);
        // Validate the complete native section, also against cables already in the sheet, before any live write.
        _ = CheckerSection.PrepareModel(candidate, candidateWorkspace);
        var input = data["input"]!.AsObject();
        input.Clear();
        foreach (var (key, value) in candidate) input[key] = value?.DeepClone();
        ConcreteCalculationSettings.Prepare(input, workspace);
        if (data["workspace_ca"] is not JsonObject) data["workspace_ca"] = workspace;
        var shear = workspace["taglio"]!.AsObject();
        shear["ancoraggio"] = "Da verificare"; shear["chiusura_torsione"] = "Da confermare";
        shear["tipo_staffa"] = "Staffa chiusa"; shear["rami_interni"] = "0";
        shear["schema_interno"] = "Bracci paralleli"; shear["rotazione_staffa"] = "0";
        shear["alpha_x"] = "90"; shear["alpha_y"] = "90";
        shear["modello_circolare"] = "Da scegliere";
        shear["modello"] = ConcreteCalculationSettings.HasStirrups(input) ? "Con staffe" : "Senza staffe";
        if (ConcreteTemplateAdapter.Uses(input))
        {
            shear["parametri"] = "Manuali";
            foreach (string key in new[] { "bw_x", "d_x", "asl_x", "bw_y", "d_y", "asl_y", "rami_x", "rami_y" }) shear[key] = "";
            shear["nota_wizard"] = Note(input);
        }
        else
        {
            shear["parametri"] = "Automatici da sezione";
            shear["rami_x"] = "2"; shear["rami_y"] = "2";
            shear.Remove("nota_wizard");
        }
    }
}
