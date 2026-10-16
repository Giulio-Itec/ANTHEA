using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Anthea.Presentation.Tests;

/// <summary>
/// One step of a scenario of the retaining wall sheet (F5.9), applied in the same way to the historic view (RetainingWallWorkspace,
/// tests/Desktop/RetainingWallHistoricTrace.cs) and to its ViewModel (tests/ANTHEA.Presentation.Tests/RetainingWallSheetChecks.cs). A
/// step is what the user does with a control: a control that the user cannot use (hidden, disabled, read-only revision) does
/// nothing. <see cref="Key"/> names the field («form/campo»), the table or the column («tabella/colonna»), the parameter (its path)
/// or the switch of the drawing; <see cref="Row"/> the row of a table; <see cref="Index"/> a direction or an accelerogram;
/// <see cref="Text"/> what the user types or chooses.
/// </summary>
internal sealed record RetainingWallStep(string Kind, string? Key = null, string? Text = null, int Index = int.MinValue, int Row = -1)
{
    /// <summary>Commit of the sheet (navigation, save).</summary>
    internal const string Commit = "conferma";
    /// <summary>The «Ricalcola» button (commit, then the calculation at once), awaited.</summary>
    internal const string Calculate = "calcola";
    /// <summary>The «Ricalcola» button without awaiting the calculation.</summary>
    internal const string Start = "avvia-calcolo";
    /// <summary>Waits for the last work requested: the automatic recalculation (450 ms after the last edit), «Ricalcola», the
    /// global search or the search of the reinforcement.</summary>
    internal const string Wait = "attendi";
    /// <summary>Closes the sheet and opens its data again (a new sheet on a copy, as the window does).</summary>
    internal const string Reopen = "riapri";
    /// <summary>The sheet becomes a read-only revision.</summary>
    internal const string ReadOnly = "sola-lettura";
    /// <summary>The engine holds every calculation that enters it until <see cref="Release"/>.</summary>
    internal const string Hold = "trattieni";
    /// <summary>Waits until one more calculation is inside the held engine.</summary>
    internal const string Entered = "attendi-motore";
    /// <summary>Releases the held calculations; the engine stops holding.</summary>
    internal const string Release = "rilascia";
    /// <summary>Types <see cref="Text"/> in the text field <see cref="Key"/> («form/campo»).</summary>
    internal const string Field = "campo";
    /// <summary>Chooses <see cref="Text"/> in the choice <see cref="Key"/> («form/campo»).</summary>
    internal const string Choice = "scelta";
    /// <summary>Clicks the check box <see cref="Key"/> («form/campo»).</summary>
    internal const string Option = "opzione";
    /// <summary>Chooses the typology <see cref="Text"/> (its identifier) in the header.</summary>
    internal const string Family = "tipologia";
    /// <summary>The «Mensola esempio» (<see cref="Text"/> cantilever) or «Gravità esempio» (gravity) button.</summary>
    internal const string Example = "esempio";
    /// <summary>Edits the cell <see cref="Key"/> («tabella/colonna») of the row <see cref="Row"/>: types or chooses <see cref="Text"/>,
    /// or clicks the check box when <see cref="Text"/> is null.</summary>
    internal const string Cell = "cella";
    /// <summary>Selects the row <see cref="Row"/> of the table <see cref="Key"/>.</summary>
    internal const string Select = "seleziona";
    /// <summary>The «+» button of the table <see cref="Key"/>; for the actions <see cref="Text"/> is the type of the new action.</summary>
    internal const string Add = "aggiungi";
    /// <summary>The «−» button of the table <see cref="Key"/> (removes the selected row).</summary>
    internal const string Remove = "togli";
    /// <summary>The «↑» (<see cref="Index"/> −1) or «↓» (+1) button of the soil column <see cref="Key"/>.</summary>
    internal const string Move = "sposta";
    /// <summary>A button of the sheet, by its caption <see cref="Text"/> (see <see cref="RetainingWallScenarios.Buttons"/>).</summary>
    internal const string Button = "pulsante";
    /// <summary>«Elimina accelerogramma» of the accelerogram <see cref="Index"/>.</summary>
    internal const string RemoveHistory = "togli-accelerogramma";
    /// <summary>Chooses the combination <see cref="Text"/> shown in the header.</summary>
    internal const string Combination = "combinazione";
    /// <summary>Chooses the view <see cref="Text"/> of the results.</summary>
    internal const string View = "vista";
    /// <summary>Chooses the member <see cref="Text"/> of the diagrams.</summary>
    internal const string Member = "elemento";
    /// <summary>Chooses the filter <see cref="Text"/> of «Riepilogo e dettagli».</summary>
    internal const string Filter = "filtro";
    /// <summary>Chooses the combination <see cref="Text"/> of the global stability.</summary>
    internal const string GlobalCombination = "combinazione-globale";
    /// <summary>Selects the row <see cref="Row"/> of the table of «Riepilogo e dettagli» (−1 clears the selection).</summary>
    internal const string ResultRow = "riga-risultati";
    /// <summary>Clicks the switch <see cref="Key"/> of the drawings (Carichi, Dati, Armature, Spinte, Intero profilo).</summary>
    internal const string Toggle = "mostra";
    /// <summary>«Valori di calcolo…»: the calculation at once, then the window of the values used in the calculation.</summary>
    internal const string Parameters = "parametri";
    /// <summary>Types <see cref="Text"/> in the column «Valore assegnato» of the parameter <see cref="Key"/> (its path).</summary>
    internal const string Parameter = "parametro";
    /// <summary>Chooses the combination <see cref="Text"/> of the window of the values.</summary>
    internal const string ParameterCombination = "parametri-combinazione";
    /// <summary>«Applica e ricalcola» of the window of the values, without awaiting it.</summary>
    internal const string ParametersApply = "parametri-applica";
    /// <summary>Waits for «Applica e ricalcola» (validation, then the calculation of the new data).</summary>
    internal const string ParametersWait = "parametri-attendi";
    /// <summary>Closes the window of the values (its title bar, available also while it validates).</summary>
    internal const string ParametersClose = "parametri-chiudi";

    public override string ToString() => Kind + (Key is null ? "" : " " + Key)
        + (Index == int.MinValue && Row < 0 ? "" : " [" + (Index != int.MinValue ? Index.ToString(CultureInfo.InvariantCulture) : Row.ToString(CultureInfo.InvariantCulture)) + "]")
        + (Text is null ? "" : " «" + Text + "»");
}

/// <summary>A scenario: the data of an F3 fixture of the module, then the steps.</summary>
internal sealed record RetainingWallScenario(string Name, string Fixture, IReadOnlyList<RetainingWallStep> Steps);

/// <summary>
/// The scenarios of the retaining wall sheet (F5.9): for every F3 fixture of the module the path of the F3 capture (opening, commit,
/// calculation, reopening); then edits of every kind on the inputs (geometry, soils, materials, reinforcement, actions, seismic,
/// advanced options), the combination matrix, the results (combinations, views, filters, the selection of a section and the request of
/// a concrete section), the global stability, the window of the values used in the calculation with its scheduler, the search of the
/// reinforcement, a read-only revision and the scheduling of the background calculation with a held engine (cancellation,
/// publication of the current revision only, closing during a calculation).
/// </summary>
internal static class RetainingWallScenarios
{
    internal const string Module = "geo_muri_sostegno";

    /// <summary>Folder of the traces of the historic view, frozen before the migration (proposal 7.2, capture first).</summary>
    internal static string Reference(string root) => Path.Combine(root, "tests", "fixtures", "f5-muri", "storica");

    internal static string FixturePath(string root, string id) => Path.Combine(root, "tests", "fixtures", "f3", "inputs", Module + "--" + id + ".json");

    /// <summary>The data of an F3 fixture of the module, as the F3 capture reads them.</summary>
    internal static JsonObject FixtureData(string root, string id) => JsonNode.Parse(File.ReadAllText(FixturePath(root, id)))!["data"]!.AsObject();

    /// <summary>Identifiers of the F3 fixtures of the module, in ordinal order.</summary>
    internal static IReadOnlyList<string> Fixtures(string root) => Directory.EnumerateFiles(Path.Combine(root, "tests", "fixtures", "f3", "inputs"), Module + "--*.json")
        .Select(f => Path.GetFileNameWithoutExtension(f)[(Module.Length + 2)..]).Order(StringComparer.Ordinal).ToArray();

    /// <summary>The forms of the sheet in the order of the trace (a form that does not exist is skipped).</summary>
    internal static IReadOnlyList<string> FormKeys { get; } = ["valley", "interfaces", "foundation", "water", "global", "global_soil_rear", "global_soil_valley",
        "global_search_mode", "global_search", "bearing_seismic", "serviceability", "materials", "material_cover", "gravity_design", "zones", "rebar_stem",
        "rebar_stem_upper", "rebar_toe", "rebar_heel", "geometry", "bar_schedule", "detailing", "seismic", "action"];

    /// <summary>The tables of the inputs, in the order of the trace.</summary>
    internal static IReadOnlyList<string> TableKeys { get; } = ["valle", "monte", "cedimenti", "globale-monte", "globale-valle", "punti-valle", "punti-monte",
        "punti-falda", "matrice-globale", "azioni", "matrice"];

    /// <summary>The switches of the drawings, in the order of the view.</summary>
    internal static IReadOnlyList<string> Toggles { get; } = ["Carichi", "Dati", "Armature", "Spinte", "Intero profilo"];

    /// <summary>The buttons that a step can click by their caption.</summary>
    internal static IReadOnlyList<string> Buttons { get; } = ["Genera / ripristina automatiche", "Copia riga", "Elimina riga", "Conferma matrice",
        "Genera globali", "Conferma matrice globale", "Prepara dal muro", "Calcola globale", "Sisma", "Stabilità globale", "Adotta copriferro minimo",
        "Calcola armature", "Interrompi ricerca", "Applica proposta", "Relazione Word", "Invia / carica terreno…", "Apri sezione in c.a.", "+ Accelerogramma",
        "Modifica profilo, strati e ricerca"];

    /// <summary>
    /// The page of the sheet that holds the control of a step (0 «Input», 1 «Verifiche»), null for the header and the commands of the
    /// window: before the step the user opens that page, as the trace does on both sides. In a read-only revision this matters: the
    /// historic view protects only the controls of the page shown (RevisionInspection on the visual tree).
    /// </summary>
    internal static int? PageOf(RetainingWallStep step) => step.Kind switch
    {
        RetainingWallStep.Field or RetainingWallStep.Choice or RetainingWallStep.Option or RetainingWallStep.Cell or RetainingWallStep.Select
            or RetainingWallStep.Add or RetainingWallStep.Remove or RetainingWallStep.Move or RetainingWallStep.RemoveHistory => 0,
        RetainingWallStep.Toggle => step.Key == "Intero profilo" ? 1 : 0,
        RetainingWallStep.View or RetainingWallStep.Member or RetainingWallStep.GlobalCombination or RetainingWallStep.Filter or RetainingWallStep.ResultRow => 1,
        RetainingWallStep.Button => step.Text switch
        {
            "Apri sezione in c.a." or "Modifica profilo, strati e ricerca" => 1,
            "Sisma" or "Stabilità globale" or "Relazione Word" => null,
            _ => 0
        },
        _ => null
    };

    static RetainingWallStep Step(string kind) => new(kind);
    static RetainingWallStep Field(string key, string text) => new(RetainingWallStep.Field, key, text);
    static RetainingWallStep Choice(string key, string text) => new(RetainingWallStep.Choice, key, text);
    static RetainingWallStep Option(string key) => new(RetainingWallStep.Option, key);
    static RetainingWallStep Cell(string table, int row, string column, string? text) => new(RetainingWallStep.Cell, table + "/" + column, text, Row: row);
    static RetainingWallStep Select(string table, int row) => new(RetainingWallStep.Select, table, Row: row);
    static RetainingWallStep Add(string table, string? type = null) => new(RetainingWallStep.Add, table, type);
    static RetainingWallStep Remove(string table) => new(RetainingWallStep.Remove, table);
    static RetainingWallStep Button(string caption) => new(RetainingWallStep.Button, Text: caption);
    static RetainingWallStep Named(string kind, string text) => new(kind, Text: text);

    internal static IReadOnlyList<RetainingWallScenario> All(string root)
    {
        RetainingWallStep commit = Step(RetainingWallStep.Commit), calculate = Step(RetainingWallStep.Calculate), wait = Step(RetainingWallStep.Wait),
            reopen = Step(RetainingWallStep.Reopen), hold = Step(RetainingWallStep.Hold), entered = Step(RetainingWallStep.Entered),
            release = Step(RetainingWallStep.Release), start = Step(RetainingWallStep.Start);
        var list = Fixtures(root).Select(id => new RetainingWallScenario("fixture-" + id, id, [commit, calculate, commit, reopen, commit, wait])).ToList();
        // Geometry, soils, interfaces, groundwater, materials, cover, reinforcement and the bar schedule of the cantilever; the typology.
        list.Add(new("modifiche-ingressi", "default", [
            wait,
            Field("geometry/height", "3,5"), wait,
            Field("geometry/heel", "abc"), wait,
            Field("geometry/heel", "1,9"),
            Cell("monte", 0, "phi", "32"),
            Add("monte"), Cell("monte", 1, "thickness", "2"), Select("monte", 1), new(RetainingWallStep.Move, "monte", Index: -1),
            Select("monte", 0), Remove("monte"), wait,
            Choice("valley/height_mode", "Assegnato"), Field("valley/free_height", "2,8"),
            Option("valley/passive"), Field("valley/mobilization", "0,3"),
            Option("valley/linked"), Cell("valle", 0, "gamma", "18,5"), Cell("monte", 0, "name", "Sabbia"), wait,
            Option("valley/linked"),
            Choice("interfaces/wall_mode", "Gettato in opera"), Field("interfaces/wall_phi_cv", "28"),
            Choice("interfaces/base_mode", "Prefabbricato liscio"), Field("interfaces/base_phi_cv", "30"), wait,
            Field("foundation/delta", "40"), Choice("interfaces/base_mode", "Assegnato"), Field("foundation/delta", "24"),
            Option("water/enabled"), Field("water/depth", "2,5"), Field("water/front_head", "0,2"), wait,
            Choice("materials/classe_cls", "C35/45"), Choice("materials/classe_acciaio", "Personalizzato"), Field("materials/fyk", "430"),
            Choice("materials/classe_cls", "Personalizzato"), Field("materials/fck", "28"), Choice("materials/exposure", "XC4"),
            Choice("material_cover/cover_method", "EC2 2004"), Option("material_cover/cover_slab"), Button("Adotta copriferro minimo"), wait,
            Option("zones/two_zones"), Field("zones/lower_height", "1,2"), Option("rebar_stem/symmetric"), Field("rebar_stem/opposite_diameter", "12"),
            Field("rebar_stem_upper/count", "5"), Field("rebar_heel/diameter", "14"), wait,
            Field("bar_schedule/panel_length", "5"), Option("detailing/enabled"), Field("detailing/lap_percent", "50"), wait,
            Named(RetainingWallStep.Family, "gravity"),
            Choice("gravity_design/type", "Muratura"), Field("gravity_design/fk", "5"), Field("gravity_design/elastic_modulus", "5000"),
            Choice("gravity_design/type", "Calcestruzzo non armato"), wait,
            Named(RetainingWallStep.Family, "cantilever"), Named(RetainingWallStep.Example, "gravity"), wait,
            Named(RetainingWallStep.Example, "cantilever"), wait,
            calculate, commit, reopen, commit, wait]));
        // Actions and the seismic action; the combination matrix generated, edited, copied, confirmed and regenerated.
        list.Add(new("azioni-sisma-matrice", "default", [
            wait,
            Add("azioni", "Forza orizzontale"), Field("action/value", "15"), Field("action/z", "3"), Field("action/name", "Urto parapetto"),
            Choice("action/category", "G2"), wait,
            Choice("action/type", "Pressione laterale"), Field("action/z0", "1,5"), wait,
            Add("azioni", "Urto"), Field("action/z", "2,5"),
            Cell("azioni", 1, "visible", null), Cell("azioni", 2, "enabled", null), Cell("azioni", 2, "enabled", null), wait,
            Select("azioni", 0), Field("action/psi0", "0,8"), Select("azioni", 2), Remove("azioni"), wait,
            Button("Sisma"), Option("seismic/enabled"), Field("seismic/ag_g", "0,25"), Choice("seismic/soil_class", "B"), Field("seismic/f0", "2,4"),
            Choice("seismic/topography", "Rilievo a cresta stretta"), Field("seismic/slope", "25"), Field("seismic/relief_height", "40"),
            Field("seismic/site_height", "35"), wait,
            Choice("seismic/ss_mode", "Assegnato"), Field("seismic/ss", "1,3"), Choice("seismic/st_mode", "Assegnato"), Field("seismic/st", "1,1"), wait,
            Choice("seismic/source", "kh e kv assegnati"), Field("seismic/kh", "0,15"), Choice("seismic/method", "Wood semplificato"), wait,
            Button("Genera / ripristina automatiche"), wait,
            Cell("matrice", 0, "wall", "1,2"), Cell("matrice", 1, "heel_surcharge", null), Select("matrice", 2), Button("Copia riga"), wait,
            Select("matrice", 0), Button("Elimina riga"), Button("Conferma matrice"), wait,
            Field("action/value", "12"), Button("Conferma matrice"), wait,
            Option("seismic/enabled"), wait,
            Button("Genera / ripristina automatiche"), wait,
            calculate, commit, reopen, commit, wait]));
        // Results: combinations, views, members, filters, switches, the selection of a section and the request of a concrete section.
        list.Add(new("risultati-sezione", "mensola-sisma-sito-pendio", [
            wait,
            Named(RetainingWallStep.Combination, "SLU 5"), Named(RetainingWallStep.View, "Sollecitazioni"), Named(RetainingWallStep.Member, "Valle"),
            Named(RetainingWallStep.Member, "Monte"), Named(RetainingWallStep.View, "Forze resistenti"), Named(RetainingWallStep.View, "Tassi di lavoro"),
            Named(RetainingWallStep.View, "Armature"), Named(RetainingWallStep.View, "Geometria e carichi"),
            new(RetainingWallStep.Toggle, "Carichi"), new(RetainingWallStep.Toggle, "Spinte"), new(RetainingWallStep.Toggle, "Dati"), new(RetainingWallStep.Toggle, "Armature"),
            Named(RetainingWallStep.Filter, "Combinazione selezionata"), new(RetainingWallStep.ResultRow, Row: 20), Button("Apri sezione in c.a."),
            Named(RetainingWallStep.Filter, "Tutti i controlli"), Named(RetainingWallStep.Filter, "Sollecitazioni numeriche"), new(RetainingWallStep.ResultRow, Row: 4),
            Button("Apri sezione in c.a."), Named(RetainingWallStep.Filter, "Calcolo delle spinte"), Named(RetainingWallStep.Filter, "Portanza sismica"),
            Named(RetainingWallStep.Combination, "SLV Generale kv− 12"), Named(RetainingWallStep.Filter, "Cedimenti e spostamenti"),
            Named(RetainingWallStep.Filter, "Dettagli armature"), Named(RetainingWallStep.Filter, "Stabilità globale"),
            Named(RetainingWallStep.Filter, "Riepilogo completo"), new(RetainingWallStep.ResultRow, Row: 0), Button("Apri sezione in c.a."),
            Button("Relazione Word"), Button("Invia / carica terreno…"),
            Field("geometry/toe", "0,9"), Button("Apri sezione in c.a."), wait,
            Named(RetainingWallStep.Family, "gravity"), wait, Named(RetainingWallStep.Filter, "Sollecitazioni numeriche"), new(RetainingWallStep.ResultRow, Row: 1),
            Button("Apri sezione in c.a."),
            calculate, commit]));
        // Advanced options: settlements and displacements, seismic bearing, accelerograms, details and the search of the reinforcement.
        list.Add(new("opzioni-armature", "opzioni-avanzate", [
            wait,
            Named(RetainingWallStep.Filter, "Cedimenti e spostamenti"), Named(RetainingWallStep.Filter, "Portanza sismica"),
            Named(RetainingWallStep.Combination, "Sisma kv− 12"), Named(RetainingWallStep.Filter, "Dettagli armature"),
            Option("serviceability/rigid_base"), Add("cedimenti"), Cell("cedimenti", 2, "modulus", "60000"), Select("cedimenti", 0), Remove("cedimenti"), wait,
            Choice("bearing_seismic/source", "Da sito"), wait,
            Button("+ Accelerogramma"), new(RetainingWallStep.RemoveHistory, Index: 0), wait,
            Button("Calcola armature"), wait,
            Button("Applica proposta"), wait,
            Button("Calcola armature"), wait, Field("geometry/height", "3,2"), Button("Applica proposta"), wait,
            Button("Calcola armature"), Button("Interrompi ricerca"), wait,
            Button("Calcola armature"), Field("geometry/height", "3,3"), wait,
            calculate, commit, reopen, commit, wait]));
        // Global stability: setup from the wall, edits of profile, layers, groundwater and search, matrix, the independent calculation.
        list.Add(new("stabilita-globale", "default", [
            wait,
            Cell("monte", 0, "thickness", "25"), Button("Stabilità globale"), wait,
            Option("global/profile_confirmed"), Choice("global_search_mode/search_mode", "Assegnata"), Field("global_search/depth_min", "0,15"), wait,
            Named(RetainingWallStep.View, "Stabilità globale"), Named(RetainingWallStep.GlobalCombination, "Globale A2–M2–R2 1"),
            new(RetainingWallStep.Toggle, "Intero profilo"), new(RetainingWallStep.ResultRow, Row: 1),
            Select("globale-monte", 0), Field("global_soil_rear/c", "5"), Cell("globale-monte", 0, "__thickness", "20"), wait,
            Choice("global/condition", "Non drenata"), Field("global_soil_rear/cu", "80"), wait,
            Choice("global/soil_mode", "Profilo unico"), Choice("global/soil_mode", "Due colonne"), Field("global/soil_split_x", "1,2"),
            Select("globale-valle", 0), Field("global_soil_valley/gamma", "19"), Add("globale-valle"), Remove("globale-valle"), wait,
            Add("punti-valle"), Cell("punti-valle", 2, "x", "-20"), Cell("punti-valle", 2, "y", "0"), Select("punti-valle", 2), Remove("punti-valle"),
            Option("global/water_enabled"), Option("global/seismic"), wait,
            Option("global/seismic"), Option("global/water_enabled"), Choice("global/condition", "Drenata"), Option("global/profile_confirmed"), Button("Genera globali"),
            Cell("matrice-globale", 0, "r", "1,2"), Button("Conferma matrice globale"), wait,
            Button("Calcola globale"), wait,
            Named(RetainingWallStep.Filter, "Stabilità globale"), new(RetainingWallStep.ResultRow, Row: 0), Button("Modifica profilo, strati e ricerca"),
            Field("foundation/delta", "40"), wait, Button("Calcola globale"), wait,
            Field("global_search/depth_max", "100"), Button("Calcola globale"), wait,
            Button("Prepara dal muro"), wait,
            calculate, commit, reopen, commit, wait]));
        // The window of the values used in the calculation: values applied, an invalid value, a revision changed while validating, closing.
        list.Add(new("valori-di-calcolo", "gravita-cls-passiva", [
            wait,
            Step(RetainingWallStep.Parameters), Named(RetainingWallStep.ParameterCombination, "SLU 5"),
            new(RetainingWallStep.Parameter, "valley/free_height", "2,4"), Step(RetainingWallStep.ParametersApply), Step(RetainingWallStep.ParametersWait),
            Step(RetainingWallStep.Parameters), new(RetainingWallStep.Parameter, "valley/linked", "forse"), Step(RetainingWallStep.ParametersApply),
            Step(RetainingWallStep.ParametersWait), new(RetainingWallStep.Parameter, "valley/linked", "false"),
            new(RetainingWallStep.Parameter, "geometry/height", "3,1"), hold, Step(RetainingWallStep.ParametersApply), entered,
            Field("geometry/toe", "0,35"), release, Step(RetainingWallStep.ParametersWait), Step(RetainingWallStep.ParametersClose), wait,
            Step(RetainingWallStep.Parameters), new(RetainingWallStep.Parameter, "geometry/slab", "0,5"), hold, Step(RetainingWallStep.ParametersApply), entered,
            Step(RetainingWallStep.ParametersClose), release, Step(RetainingWallStep.ParametersWait), wait,
            calculate, commit]));
        list.Add(new("sola-lettura", "globale-drenata-due-colonne", [
            wait, Step(RetainingWallStep.ReadOnly),
            Field("geometry/height", "4"), Choice("valley/height_mode", "Assegnato"), Option("water/enabled"), Cell("monte", 0, "phi", "20"),
            Add("monte"), Remove("monte"), Named(RetainingWallStep.Family, "gravity"), Named(RetainingWallStep.Example, "gravity"),
            Add("azioni", "Momento"), Button("Genera / ripristina automatiche"), Cell("matrice", 0, "wall", "2"), Button("Prepara dal muro"),
            Button("Genera globali"), Button("Adotta copriferro minimo"), Button("Calcola armature"), Button("+ Accelerogramma"),
            Button("Sisma"), Button("Stabilità globale"),
            Named(RetainingWallStep.Combination, "SLU 4"), Named(RetainingWallStep.View, "Sollecitazioni"), Named(RetainingWallStep.Member, "Monte"),
            Named(RetainingWallStep.Filter, "Sollecitazioni numeriche"), new(RetainingWallStep.ResultRow, Row: 3), Button("Apri sezione in c.a."),
            new(RetainingWallStep.Toggle, "Carichi"), Named(RetainingWallStep.View, "Stabilità globale"),
            Named(RetainingWallStep.GlobalCombination, "Globale A2–M2–R2 1"), Button("Calcola globale"), wait,
            Button("Relazione Word"), calculate, commit]));
        // Scheduling of the background calculation with a held engine: a calculation overtaken by an edit is never published, only the
        // current revision is published, «Ricalcola» during a calculation, the global search after an edit, closing during a calculation.
        list.Add(new("calcolo-sullo-sfondo", "default", [
            wait,
            hold, Field("geometry/height", "3,2"), entered, Field("geometry/height", "3,3"), release, wait,
            hold, start, entered, start, release, wait,
            hold, Field("geometry/toe", "0,7"), entered, Button("Calcola globale"), release, wait,
            hold, Field("geometry/toe", "0,75"), entered, reopen, release, wait, commit]));
        return list;
    }
}

/// <summary>
/// A field of a form as the user sees it: kind (campo, scelta, opzione), label, symbol, unit, value (the raw text of a text field), items,
/// whether the user can change it (a disabled row, a calculated field or a read-only revision: «spento»), faded, hidden, and its notice.
/// </summary>
internal sealed record RetainingWallFieldState(string Form, string Key, string Kind, string Label, string Symbol, string Unit, string Value,
    IReadOnlyList<string>? Choices, bool Active, bool Dimmed, bool Visible, string? Note)
{
    internal string Line() => Kind + " " + RetainingWallTrace.Quote(Label) + (Symbol.Length == 0 ? "" : " " + Symbol) + (Unit.Length == 0 ? "" : " [" + Unit + "]")
        + " = " + RetainingWallTrace.Quote(Value) + (Choices is null ? "" : " voci " + RetainingWallTrace.Quote(string.Join("|", Choices)))
        + (Active ? "" : " · spento") + (Dimmed ? " · attenuato" : "") + (Visible ? "" : " · nascosto")
        + (Note is null ? "" : " · nota " + RetainingWallTrace.Quote(Note));
}

/// <summary>
/// One block of a panel of results or of a window, as the user reads it: a text, a table (columns and rows already formatted), a
/// button or a group with its blocks. The two sides describe their panels with these blocks; the trace writes them in one format.
/// </summary>
internal sealed record RetainingWallBlock(string Kind, string Text, IReadOnlyList<string>? Columns = null, IReadOnlyList<string[]>? Rows = null,
    IReadOnlyList<RetainingWallBlock>? Children = null, bool Enabled = true)
{
    internal const string TextKind = "testo", TableKind = "tabella", ButtonKind = "pulsante", GroupKind = "gruppo";
}

/// <summary>
/// The state of the sheet seen by the user after a step, as keyed entries in a fixed order: data (one entry per top-level key, in
/// memory order), Modified and the requests to the window, result, status, header, combinations and views, every field of every form,
/// cards, tables, the texts of the inputs, the panels of the results, the drawings as data, the proposal of the reinforcement and the
/// window of the values. Each side fills it from its own objects; <see cref="RetainingWallTrace.Lines"/> writes the first state whole and
/// then, after every step, only the entries that changed.
/// </summary>
internal sealed class RetainingWallTraceState(RetainingWallIds ids)
{
    private readonly List<(string Key, string Text)> entries = [];
    private readonly HashSet<string> keys = new(StringComparer.Ordinal);

    internal IReadOnlyList<(string Key, string Text)> Entries => entries;

    /// <summary>Adds an entry; the identifiers of the actions are written by their order of appearance (<see cref="RetainingWallIds"/>).</summary>
    internal void Add(string key, string text)
    {
        if (!keys.Add(key)) throw new InvalidOperationException("Voce della traccia ripetuta: " + key);
        entries.Add((key, ids.Normalize(text).Replace("\r", "\\r").Replace("\n", "\\n")));
    }

    /// <summary>Hash of a JSON value with the identifiers of the actions written by their order of appearance.</summary>
    internal string Hash(JsonNode node) => RetainingWallTrace.HashText(ids.Normalize(RetainingWallTrace.Compact(node)));

    /// <summary>The data: the order of the top-level keys and one entry for each key, in memory order.</summary>
    internal void Data(JsonObject data)
    {
        Add("dati", string.Join("|", data.Select(p => p.Key)));
        foreach (var (key, value) in data) Add("dati." + key, RetainingWallTrace.Compact(value));
    }

    internal void Field(RetainingWallFieldState field) => Add("campo." + field.Form + "." + field.Key, field.Line());

    /// <summary>A table: columns, selection and every row (the object the row edits, in memory order).</summary>
    internal void Table(string name, IReadOnlyList<string> columns, IReadOnlyList<JsonObject> rows, int selected)
    {
        Add("tabella." + name, "colonne " + RetainingWallTrace.Quote(string.Join("|", columns)) + " · righe " + RetainingWallTrace.Number(rows.Count)
            + " · selezione " + RetainingWallTrace.Number(selected));
        for (int i = 0; i < rows.Count; i++) Add("tabella." + name + "." + RetainingWallTrace.Number(i), RetainingWallTrace.Compact(rows[i]));
    }

    /// <summary>A panel: one entry for each block (tables by their size, hash and first three rows), «-» when there is no panel.</summary>
    internal void Panel(string name, IReadOnlyList<RetainingWallBlock>? blocks)
    {
        if (blocks is null) { Add(name, "-"); return; }
        Add(name, RetainingWallTrace.Number(blocks.Count) + " blocchi");
        Blocks(name, blocks);
    }

    private void Blocks(string prefix, IReadOnlyList<RetainingWallBlock> blocks)
    {
        for (int i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            string key = prefix + "." + RetainingWallTrace.Number(i);
            switch (block.Kind)
            {
                case RetainingWallBlock.TableKind:
                    var rows = block.Rows!.Select(r => r.Select(ids.Normalize).ToArray()).ToArray();
                    Add(key, "tabella " + RetainingWallTrace.Quote(string.Join("|", block.Columns!)) + " · righe " + RetainingWallTrace.Number(rows.Length)
                        + " " + RetainingWallTrace.Hash(rows));
                    for (int r = 0; r < Math.Min(3, rows.Length); r++) Add(key + "." + RetainingWallTrace.Number(r), string.Join(" | ", rows[r].Select(RetainingWallTrace.Quote)));
                    break;
                case RetainingWallBlock.GroupKind:
                    Add(key, "gruppo " + RetainingWallTrace.Quote(block.Text));
                    Blocks(key, block.Children!);
                    break;
                case RetainingWallBlock.ButtonKind:
                    Add(key, "pulsante " + RetainingWallTrace.Quote(block.Text) + (block.Enabled ? "" : " · spento"));
                    break;
                default:
                    Add(key, "testo " + RetainingWallTrace.Quote(block.Text));
                    break;
            }
        }
    }
}

/// <summary>
/// The identifiers of the actions (32 hexadecimal digits) in the order of their first appearance in a scenario: an action added by
/// a step has a new random identifier, written «&lt;ID-n&gt;» on both sides.
/// </summary>
internal sealed class RetainingWallIds
{
    private static readonly Regex Identifier = new("(?<![0-9a-f])[0-9a-f]{32}(?![0-9a-f])", RegexOptions.CultureInvariant);
    private readonly Dictionary<string, string> names = new(StringComparer.Ordinal);

    internal string Normalize(string text) => Identifier.Replace(text, m =>
    {
        if (!names.TryGetValue(m.Value, out var name)) names[m.Value] = name = "<ID-" + (names.Count + 1).ToString(CultureInfo.InvariantCulture) + ">";
        return name;
    });
}

/// <summary>Formats of the trace, shared by the two sides.</summary>
internal static class RetainingWallTrace
{
    internal static string Compact(JsonNode? node) => PileGroupTrace.Compact(node);

    internal static string Hash(JsonNode node) => PileGroupTrace.Hash(node);

    internal static string HashText(string text) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)))[..16];

    internal static string Hash(IReadOnlyList<string[]> rows) => PileGroupTrace.Hash(new JsonArray(rows.Select(r => (JsonNode)new JsonArray(r.Select(c => (JsonNode?)JsonValue.Create(c)).ToArray())).ToArray()));

    internal static string Quote(string? text) => PileGroupTrace.Quote(text);

    internal static string YesNo(bool value) => PileGroupTrace.YesNo(value);

    internal static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// The trace of a step: the whole state after the opening (<paramref name="previous"/> null), then only what changed: «~» an entry
    /// with a new text, «+» a new entry, «-» an entry that no longer exists. The texts of the entries are the same on both sides, so
    /// equal traces mean equal states after every step.
    /// </summary>
    internal static IEnumerable<string> Lines(string step, RetainingWallTraceState? previous, RetainingWallTraceState current)
    {
        yield return "== " + step;
        if (previous is null)
        {
            foreach (var (key, text) in current.Entries) yield return key + ": " + text;
            yield break;
        }
        var before = previous.Entries.ToDictionary(e => e.Key, e => e.Text, StringComparer.Ordinal);
        var now = current.Entries.Select(e => e.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var (key, text) in current.Entries)
        {
            if (!before.TryGetValue(key, out var old)) yield return "+ " + key + ": " + text;
            else if (old != text) yield return "~ " + key + ": " + text;
        }
        foreach (var (key, _) in previous.Entries) if (!now.Contains(key)) yield return "- " + key;
    }

    internal static void Write(string path, IEnumerable<string> lines) => PileGroupTrace.Write(path, lines);

    internal static string[] Read(string path) => PileGroupTrace.Read(path);

    internal static string? FirstDifference(IReadOnlyList<string> expected, IReadOnlyList<string> actual) => PileGroupTrace.FirstDifference(expected, actual);
}
