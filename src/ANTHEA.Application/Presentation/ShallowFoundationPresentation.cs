using System.Text.Json.Nodes;
using Anthea.FoundationWorkspace;

namespace Anthea.Application.Presentation;

/// <summary>
/// Presentation of the shallow foundation sheet (geo_fondazioni_superficiali), shared by the historic view FoundationWorkspace and
/// by the ViewModel of ANTHEA.Presentation (refactoring F5.9b): the texts, the rules of the fields and the derivations that the view
/// computed itself, moved here unchanged. No engineering formula: the values come from the document and from the result of the
/// GPC library; numbers and units are formatted by <see cref="FoundationPresentation"/>.
/// </summary>
public static class ShallowFoundationPresentation
{
    /// <summary>Status line at opening.</summary>
    public const string StateToVerify = "Dati da verificare · premere Calcola";

    /// <summary>Status line after an edit: the results are cleared and the sheet waits for «Calcola».</summary>
    public const string StateChanged = "Dati modificati · premere Calcola";

    /// <summary>Status line when the user removes the only survey.</summary>
    public const string KeepOneSurvey = "Conservare almeno un sondaggio.";

    /// <summary>Title and question of the removal of a survey.</summary>
    public const string RemoveSurveyTitle = "Stratigrafia";
    public const string RemoveSurveyQuestion = "Eliminare il sondaggio selezionato e i suoi strati?";

    /// <summary>The four graphic tabs of the results before a result.</summary>
    public const string ViewToCalculate = "Calcolare per aggiornare questa vista.";

    /// <summary>The four graphic tabs of the results when the result has no graphics (actions beyond the resistant domain).</summary>
    public const string ViewNotAvailable = "Vista non disponibile per questo stato di carico. Consultare gli avvisi della verifica.";

    /// <summary>Title of the table of the additive corrections of Brinch Hansen (undrained).</summary>
    public const string AdditiveTitle = "Hansen non drenato · correzioni additive";

    /// <summary>Limits of the verification, after the tables of the details.</summary>
    public const string Limits = "Verifica statica di portanza; scorrimento, cedimenti, stabilità globale, sisma e struttura esclusi.";

    /// <summary>Note under the inputs.</summary>
    public const string ActionsNote = "Azioni al piano di posa. Drenato: N efficace; non drenato: N totale. Nessun peso aggiunto automaticamente. Striscia: azioni per metro.";

    /// <summary>Note under the shared concrete.</summary>
    public const string ConcreteNote = "Materiale condiviso nei Progetti. Non interviene nella portanza geotecnica; resistenza strutturale e peso proprio automatico non calcolati.";

    /// <summary>Label of the confirmation of the A1 actions (data key «azioni_confermate»).</summary>
    public const string ConfirmationLabel = "Azioni A1 già combinate, inclusi pesi e azioni idrauliche";

    /// <summary>Label of the option of the half-space (generali/strato_portante_semiinfinito).</summary>
    public const string HalfSpaceLabel = "Strato portante omogeneo sotto la base, prolungato come semispazio";

    /// <summary>Columns of the table of the coefficients and of the contributions.</summary>
    public static IReadOnlyList<string> CoefficientColumns { get; } = ["Contributo", "N", "s", "d", "i", "kPa"];

    /// <summary>Columns of the table of the additive corrections of Brinch Hansen.</summary>
    public static IReadOnlyList<string> AdditiveColumns { get; } = ["Contributo", "s′ (+)", "d′ (+)", "i′ (−)", "1+s′+d′−i′"];

    /// <summary>The sections of the failure mechanism, in the order of the choice.</summary>
    public static IReadOnlyList<string> MechanismAxes { get; } = ["Sezione X · larghezza B", "Sezione Y · lunghezza L"];

    /// <summary>Key of the graphics of the failure mechanism for the section <paramref name="axis"/> (0 X, 1 Y).</summary>
    public static string MechanismKey(int axis) => axis == 1 ? "MechanismY" : "MechanismX";

    /// <summary>Text of the results without a result: the sheet can be prepared and saved also without the approved library.</summary>
    public static string ResultsPlaceholder(bool libraryAvailable) => libraryAvailable
        ? "Verifica da calcolare · selezionare il sondaggio e confermare le azioni."
        : "Calcolo in attesa del rilascio approvato GPC · puoi preparare e salvare il foglio.";

    /// <summary>
    /// The input read by the rules of visibility: a copy of «input» where WaterEnabled is the groundwater of the common data
    /// (generali/presenza_falda), as the sheet of version 2 shows it.
    /// </summary>
    public static JsonObject VisibilityInput(JsonObject data)
    {
        var visibility = data["input"]!.DeepClone().AsObject(); visibility["WaterEnabled"] = data["generali"]!["presenza_falda"]!.DeepClone();
        return visibility;
    }

    /// <summary>
    /// Whether the field <paramref name="key"/> (a key of «input», or «azioni_confermate») is shown: the confirmation only for the
    /// NTC 2018 profile; the others by <see cref="FoundationPresentation.Visible"/> on <paramref name="visibility"/>
    /// (<see cref="VisibilityInput"/>); Mx, My and Hy stay visible while they are not «0», and the compressibility while it is not
    /// the general failure, so that a value hidden by the shape or the condition can still be seen and cleared.
    /// </summary>
    public static bool FieldVisible(JsonObject input, JsonObject visibility, string key)
    {
        bool visible = key == "azioni_confermate" ? input["Profile"]!.GetValue<int>() == 0 : FoundationPresentation.Visible(key, visibility);
        if (key is "MomentX" or "MomentY" or "HorizontalY" && input[key]?.ToString() is string s && s != "0") visible = true;
        if (key == "Compression" && input[key]!.GetValue<int>() != 0) visible = true;
        return visible;
    }

    /// <summary>Saved state of a group of the view (vista_fondazioni/<paramref name="key"/>); <paramref name="fallback"/> when missing.</summary>
    public static bool ViewFlag(JsonObject data, string key, bool fallback) =>
        data["vista_fondazioni"]?[key] is JsonValue v && v.TryGetValue<bool>(out bool b) ? b : fallback;

    /// <summary>Writes the state of a group of the view into vista_fondazioni, created when missing.</summary>
    public static void SaveViewFlag(JsonObject data, string key, bool value)
    {
        data["vista_fondazioni"] ??= new JsonObject(); data["vista_fondazioni"]![key] = value;
    }

    /// <summary>Names of the surveys of the sheet: «Sondaggio 1» … «Sondaggio n».</summary>
    public static string[] SurveyNames(JsonObject data) => Enumerable.Range(1, data["stratigrafie"]!.AsArray().Count).Select(i => "Sondaggio " + i).ToArray();

    /// <summary>The survey examined (generali/sondaggio_fondazione), checked against the number of surveys.</summary>
    public static int SelectedSurvey(JsonObject data) => FoundationDocument.Choice(data["generali"]!.AsObject(), "sondaggio_fondazione", data["stratigrafie"]!.AsArray().Count);

    /// <summary>Letter of the layer <paramref name="index"/> in the table and in the drawing of the stratigraphy: A, B, … Z, then A again.</summary>
    public static string LayerLetter(int index) => ((char)('A' + index % 26)).ToString();

    /// <summary>Headline of the result: outcome, N, R, N/R and A′.</summary>
    public static string Headline(JsonObject input, JsonObject result) => result["Status"] + "     N = " + FoundationPresentation.Value(input["Normal"]) + " kN     R = "
        + FoundationPresentation.Value(result["Resistance"], .001) + " kN     N/R = " + FoundationPresentation.Value(result["Utilization"], 1, "0.000") + "     A′ = "
        + FoundationPresentation.Value(result["EffectiveArea"], 1e-6) + " m²";

    /// <summary>A line of the summary of the details: label and value.</summary>
    public static string SummaryLine((string Label, string Value) row) => row.Label + "   " + row.Value;

    /// <summary>Status line after a calculation: the survey examined (from 0) and the only combination.</summary>
    public static string CalculatedState(int survey, JsonObject data) => "Calcolato · sondaggio " + (survey + 1) + " · unica combinazione: " + data["nome_combinazione"];

    /// <summary>Text under the map of the pressures: effective or total pressures, minimum, maximum and compressed area.</summary>
    public static string PressureSummary(JsonObject input, JsonObject graphics) => (input["Condition"]!.GetValue<int>() == 0 ? "Efficaci · " : "Totali · ")
        + "pmin = " + FoundationPresentation.Value(graphics["MinimumPressure"], 1000) + " kPa · pmax = " + FoundationPresentation.Value(graphics["MaximumPressure"], 1000)
        + " kPa · area compressa = " + FoundationPresentation.Value(graphics["CompressedArea"], 1e-6) + " m²";

    /// <summary>
    /// Items of the choice of the shared concrete: the classes of <paramref name="catalog"/>, the class of the sheet when it is not
    /// one of them, and «Personalizzato», without repetitions.
    /// </summary>
    public static string[] ConcreteChoices(JsonObject section, IEnumerable<JsonObject> catalog) =>
        catalog.Select(m => m["nome"]!.ToString()).Append(section["classe_cls"]!.ToString()).Append("Personalizzato").Distinct().ToArray();

    /// <summary>
    /// After the user wrote the field <paramref name="key"/> of the shared concrete: a class of the catalogue copies its values into
    /// the section; a typed fck names the class of the catalogue with that fck, otherwise «Personalizzato». <paramref name="show"/>
    /// receives every value to show in the fields without writing it again (key, text).
    /// </summary>
    public static void ConcreteChanged(JsonObject section, string key, IReadOnlyList<JsonObject> catalog, Action<string, string> show)
    {
        if (key == "classe_cls" && catalog.FirstOrDefault(m => m["nome"]?.ToString() == section[key]?.ToString()) is { } selected)
            foreach (var (k, v) in selected.Where(p => p.Key != "nome")) { section[k] = v?.DeepClone(); show(k, v?.ToString() ?? ""); }
        if (key == "fck_mpa")
        {
            string name = catalog.FirstOrDefault(m => m["fck_mpa"]?.ToString() == section[key]?.ToString())?["nome"]?.ToString() ?? "Personalizzato";
            section["classe_cls"] = name; section["materiale_cls_nome"] = name; show("classe_cls", name);
        }
    }
}
