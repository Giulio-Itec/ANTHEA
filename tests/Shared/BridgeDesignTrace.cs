using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Anthea.Application.BridgeDesign;

namespace Anthea.Presentation.Tests;

/// <summary>
/// One step of a scenario of the Bridge Design sheet (str_bridge_design, F5.12), applied in the same way to the historic view
/// (BridgeDesignWorkspace, tests/Desktop/BridgeDesignHistoricTrace.cs) and to its ViewModel (tests/ANTHEA.Presentation.Tests). A step is what
/// the user does with a control: a control that the user cannot use (on a page or a tab not shown, disabled, or protected by the read-only
/// revision) does nothing. Before the step the user opens the page and the tab that hold the control (<see cref="BridgeDesignScenarios.PlaceOf"/>),
/// as the trace does on both sides. <see cref="Key"/> names the parameter («gruppo/chiave»), the choice or the option (its key in «input»), the
/// lock or the limit of the search; <see cref="Text"/> what the user types or chooses, or the caption of a button; <see cref="Index"/> a page,
/// a tab, a seed, an alternative, a row or a rank.
/// </summary>
internal sealed record BridgeDesignStep(string Kind, string? Key = null, string? Text = null, int Index = int.MinValue)
{
    /// <summary>Commit of the sheet (navigation, save).</summary>
    internal const string Commit = "conferma";
    /// <summary>The «Calcola» command of the window, awaited.</summary>
    internal const string Calculate = "calcola";
    /// <summary>Starts the «Calcola» command of the window without awaiting it.</summary>
    internal const string Start = "avvia-calcolo";
    /// <summary>Waits for the last calculation requested: the one of the opening, the automatic one (450 ms after the last edit) or «Calcola».</summary>
    internal const string Wait = "attendi";
    /// <summary>Closes the sheet and opens its data again (a new sheet on a copy, as the window does).</summary>
    internal const string Reopen = "riapri";
    /// <summary>The sheet becomes a read-only revision.</summary>
    internal const string ReadOnly = "sola-lettura";
    /// <summary>The visit of the F3 capture: every expander opened and every page of every tab control chosen, then the previous one again.</summary>
    internal const string Visit = "visita";
    /// <summary>The engine holds every calculation that enters it until <see cref="Release"/>.</summary>
    internal const string Hold = "trattieni";
    /// <summary>Waits until one more calculation is inside the held engine.</summary>
    internal const string Entered = "attendi-motore";
    /// <summary>Releases the held calculations; the engine stops holding.</summary>
    internal const string Release = "rilascia";
    /// <summary>Chooses the page <see cref="Index"/> of the sheet (0 «Progetto», 1 «Ottimizzazione»).</summary>
    internal const string Page = "pagina";
    /// <summary>Chooses the tab <see cref="Index"/> of the inputs.</summary>
    internal const string InputTab = "ingressi";
    /// <summary>Chooses the tab <see cref="Index"/> of the results.</summary>
    internal const string OutputTab = "risultati";
    /// <summary>Types <see cref="Text"/> in the parameter <see cref="Key"/> («gruppo/chiave»).</summary>
    internal const string Field = "campo";
    /// <summary>Moves the slider of the parameter <see cref="Key"/> to <see cref="Text"/> (a number with the point).</summary>
    internal const string Slider = "cursore";
    /// <summary>Chooses <see cref="Text"/> in the choice <see cref="Key"/>.</summary>
    internal const string Choice = "scelta";
    /// <summary>Clicks the check box <see cref="Key"/> of the spans.</summary>
    internal const string Option = "opzione";
    /// <summary>Clicks the button of the typology <see cref="Text"/> (its identifier).</summary>
    internal const string Family = "tipologia";
    /// <summary>Clicks the button <see cref="Text"/>; <see cref="Key"/> «ricerca» for the buttons of the page of the search.</summary>
    internal const string Button = "pulsante";
    /// <summary>«Ponte casuale» with the generator of the seed <see cref="Index"/> (the button uses the shared generator).</summary>
    internal const string Random = "ponte-casuale";
    /// <summary>Clicks the alternative <see cref="Index"/> of «Alternative per ridurre le quantità e le emissioni».</summary>
    internal const string Alternative = "alternativa";
    /// <summary>«Gestisci prezzario…», then the script <see cref="Text"/> in the price editor (<see cref="BridgeDesignPriceScripts"/>).</summary>
    internal const string Prices = "prezzario";
    /// <summary>The Word report of the sheet.</summary>
    internal const string Report = "report";
    /// <summary>The page printed by «Stampa scheda» (its texts).</summary>
    internal const string Print = "stampa";
    /// <summary>Chooses the objective <see cref="Index"/> of the search.</summary>
    internal const string Objective = "obiettivo";
    /// <summary>Clicks the lock <see cref="Key"/> of «Mantieni costante».</summary>
    internal const string Lock = "blocca";
    /// <summary>Types <see cref="Text"/> in the limit <see cref="Key"/> of the search (<see cref="BridgeDesignScenarios.Limits"/>), then leaves it.</summary>
    internal const string Limit = "limite";
    /// <summary>Waits for the last search started.</summary>
    internal const string WaitSearch = "attendi-ricerca";
    /// <summary>Selects the row <see cref="Index"/> of the ranking.</summary>
    internal const string Rank = "graduatoria";
    /// <summary>Clicks the point of the solution of rank <see cref="Index"/> in the cloud of the solutions.</summary>
    internal const string Point = "punto";
    /// <summary>Chooses the variable <see cref="Index"/> of the chart of the explored variables.</summary>
    internal const string Variable = "variabile";
    /// <summary>Chooses the typology filter <see cref="Index"/> of the cloud (0 every typology).</summary>
    internal const string Filter = "filtro";
    /// <summary>Clicks «Solo frontiera Pareto».</summary>
    internal const string Pareto = "pareto";

    public override string ToString() => Kind + (Key is null ? "" : " " + Key) + (Index == int.MinValue ? "" : " [" + Index.ToString(CultureInfo.InvariantCulture) + "]")
        + (Text is null ? "" : " «" + Text + "»");
}

/// <summary>A scenario: the data of an F3 fixture of the module, then the steps.</summary>
internal sealed record BridgeDesignScenario(string Name, string Fixture, IReadOnlyList<BridgeDesignStep> Steps);

/// <summary>
/// The scenarios of the Bridge Design sheet (F5.12): for every F3 fixture of the module the path of the F3 capture (opening, visit,
/// calculation, report, reopening); then the inputs of every tab (parameters typed and moved with the sliders, choices, options, typologies,
/// automatic dimensions, traffic), the results and the comparison with the alternative A, the alternatives, undo, the random bridge, the
/// prices (rates, assumptions, the price editor), the search of the optimum with its views, a read-only revision and the scheduling of the
/// background calculation with a held engine.
/// </summary>
internal static class BridgeDesignScenarios
{
    internal const string Module = "str_bridge_design";

    /// <summary>Folder of the traces of the historic view, frozen before the migration (proposal 7.2, capture first).</summary>
    internal static string Reference(string root) => Path.Combine(root, "tests", "fixtures", "f5-bridge-design", "storica");

    /// <summary>Folder of the frozen drawing lists of the historic view.</summary>
    internal static string Drawings(string root) => Path.Combine(root, "tests", "fixtures", "f5-bridge-design", "disegni");

    internal static string FixturePath(string root, string id) => Path.Combine(root, "tests", "fixtures", "f3", "inputs", Module + "--" + id + ".json");

    /// <summary>The data of an F3 fixture, as the F3 capture reads them.</summary>
    internal static JsonObject FixtureData(string root, string id) => JsonNode.Parse(File.ReadAllText(FixturePath(root, id)))!["data"]!.AsObject();

    /// <summary>Identifiers of the F3 fixtures of the module, in ordinal order.</summary>
    internal static IReadOnlyList<string> Fixtures(string root) => Directory.EnumerateFiles(Path.Combine(root, "tests", "fixtures", "f3", "inputs"), Module + "--*.json")
        .Select(f => Path.GetFileNameWithoutExtension(f)[(Module.Length + 2)..]).Order(StringComparer.Ordinal).ToArray();

    /// <summary>The choices of the inputs: key and label (the label names the choice in the view).</summary>
    internal static IReadOnlyList<(string Key, string Label)> Choices { get; } = [("obstacle", "Ostacolo da attraversare"), ("soil", "Terreno convenzionale"),
        ("deck_type", "Piano carrabile"), ("pier", "Tipologia pila"), ("foundation", "Fondazioni"), ("traffic_model", "Modello di traffico")];

    /// <summary>The options of the spans: key and label.</summary>
    internal static IReadOnlyList<(string Key, string Label)> Options { get; } = [("continuous", "Impalcato continuo"),
        ("start_pier", "Inizio su pila · viadotto prosegue"), ("end_pier", "Fine su pila · viadotto prosegue")];

    /// <summary>The locks of «Mantieni costante», in the order of the view.</summary>
    internal static IReadOnlyList<string> Locks { get; } = ["family", "spans", "depth", "section", "continuous", "pier", "foundation"];

    /// <summary>The limits of the search, in the order of the view: spans, depth, the two grids, the first N of the ranking.</summary>
    internal static IReadOnlyList<string> Limits { get; } = ["campate-min", "campate-max", "altezza-min", "altezza-max", "altezza-da", "altezza-a", "altezza-passo",
        "pali-da", "pali-a", "pali-passo", "prime"];

    /// <summary>The tab of the inputs that holds a choice.</summary>
    private static int ChoiceTab(string key) => key switch { "obstacle" or "soil" => 0, "deck_type" => 2, "pier" or "foundation" => 3, "traffic_model" => 4, _ => -1 };

    /// <summary>The tab of the inputs that holds a parameter of «input».</summary>
    private static readonly Dictionary<string, int> InputTabs = new(StringComparer.Ordinal)
    {
        ["length"] = 0, ["height"] = 0, ["obstacle_width"] = 0, ["lanes"] = 0, ["lane_width"] = 0, ["shoulder"] = 0, ["median"] = 0, ["barrier_width"] = 0,
        ["spans"] = 1,
        ["pile_length"] = 3, ["pier_size"] = 3, ["pile_count"] = 3, ["footing_size"] = 3, ["fc_sub"] = 3
    };

    /// <summary>
    /// Where the control of a step is: the page of the sheet, the tab of the inputs and the tab of the results that the user opens before the
    /// step (null: nothing to open). The trace opens them on both sides.
    /// </summary>
    internal static (int? Page, int? Input, int? Output) PlaceOf(BridgeDesignStep step)
    {
        switch (step.Kind)
        {
            case BridgeDesignStep.Field or BridgeDesignStep.Slider:
            {
                var parts = step.Key!.Split('/');
                return parts[0] switch
                {
                    "input" => (0, InputTabs.TryGetValue(parts[1], out int tab) ? tab : 2, null),
                    "assumptions" when parts[1] is "gamma_q" or "traffic_load" => (0, 4, null),
                    "assumptions" => (0, null, 4),
                    "rates" => (0, null, 3),
                    _ => throw new ArgumentException("Gruppo sconosciuto: " + step.Key)
                };
            }
            case BridgeDesignStep.Choice: return (0, ChoiceTab(step.Key!), null);
            case BridgeDesignStep.Option: return (0, 1, null);
            case BridgeDesignStep.Family: return (0, 2, null);
            case BridgeDesignStep.Alternative or BridgeDesignStep.Random: return (0, null, null);
            case BridgeDesignStep.Prices: return (0, null, 3);
            case BridgeDesignStep.Objective or BridgeDesignStep.Lock or BridgeDesignStep.Limit or BridgeDesignStep.Rank or BridgeDesignStep.Point
                or BridgeDesignStep.Variable or BridgeDesignStep.Filter or BridgeDesignStep.Pareto: return (1, null, null);
            case BridgeDesignStep.Button when step.Key == "ricerca": return (1, null, null);
            case BridgeDesignStep.Button:
                return step.Text switch
                {
                    "Ripristina dimensioni automatiche" => (0, 1, null),
                    "Sezione automatica" => (0, 2, null),
                    "Applica γQ NTC consigliato" => (0, 4, null),
                    "Applica valori orientativi 2026" => (0, null, 3),
                    "Ripristina A" or "Rimuovi confronto" => (0, null, 2),
                    _ => (0, null, null)
                };
            default: return (null, null, null);
        }
    }

    static BridgeDesignStep Step(string kind) => new(kind);
    static BridgeDesignStep Field(string key, string text) => new(BridgeDesignStep.Field, key, text);
    static BridgeDesignStep Slider(double value, string key) => new(BridgeDesignStep.Slider, key, value.ToString("R", CultureInfo.InvariantCulture));
    static BridgeDesignStep Choice(string key, string text) => new(BridgeDesignStep.Choice, key, text);
    static BridgeDesignStep Option(string key) => new(BridgeDesignStep.Option, key);
    static BridgeDesignStep Family(string id) => new(BridgeDesignStep.Family, Text: id);
    static BridgeDesignStep Button(string caption) => new(BridgeDesignStep.Button, Text: caption);
    static BridgeDesignStep Search(string caption) => new(BridgeDesignStep.Button, "ricerca", caption);
    static BridgeDesignStep Indexed(string kind, int index) => new(kind, Index: index);
    static BridgeDesignStep Prices(string script) => new(BridgeDesignStep.Prices, Text: script);
    static BridgeDesignStep Limit(string key, string text) => new(BridgeDesignStep.Limit, key, text);
    static BridgeDesignStep Lock(string key) => new(BridgeDesignStep.Lock, key);

    internal static IReadOnlyList<BridgeDesignScenario> All(string root)
    {
        BridgeDesignStep commit = Step(BridgeDesignStep.Commit), calculate = Step(BridgeDesignStep.Calculate), wait = Step(BridgeDesignStep.Wait),
            reopen = Step(BridgeDesignStep.Reopen), visit = Step(BridgeDesignStep.Visit), report = Step(BridgeDesignStep.Report), hold = Step(BridgeDesignStep.Hold),
            entered = Step(BridgeDesignStep.Entered), release = Step(BridgeDesignStep.Release), start = Step(BridgeDesignStep.Start),
            searched = Step(BridgeDesignStep.WaitSearch), print = Step(BridgeDesignStep.Print);
        var list = new List<BridgeDesignScenario>();
        // The path of the F3 capture: opened, visited while the opening calculation runs, calculated, report, reopened.
        list.AddRange(Fixtures(root).Select(id => new BridgeDesignScenario("percorso-" + id, id, [commit, visit, commit, calculate, commit, report, print, reopen, commit, wait])));
        // The site and the spans: choices, parameters typed (also invalid and out of range) and moved with the sliders, the options of the
        // spans, the automatic dimensions.
        list.Add(new("ingressi-sito-campate", "default", [
            wait,
            Choice("obstacle", "Strada / ferrovia"), Choice("soil", "Roccia"), wait,
            Field("input/length", "abc"), wait,
            Field("input/length", "5000"), wait,
            Field("input/length", "130,5"), wait,
            Slider(26, "input/height"), Slider(3, "input/lanes"), wait,
            Field("input/lane_width", "3,5"), Field("input/shoulder", ""), wait,
            Field("input/shoulder", "1,2"), Choice("obstacle", "Nessuno"), wait,
            Indexed(BridgeDesignStep.InputTab, 1), Slider(4, "input/spans"), wait,
            Option("continuous"), Option("start_pier"), wait,
            Option("end_pier"), Option("start_pier"), wait,
            Field("input/spans", "0"), wait,
            Button("Ripristina dimensioni automatiche"), wait,
            calculate, commit, reopen, commit, wait]));
        // The section: every kind of typology (the rules of each button), the deck of the upper structures, the fields shown by the typology,
        // the automatic section.
        list.Add(new("sezione-tipologie", "default", [
            wait,
            Family("steel_box"), wait,
            Field("input/boxes", "2"), Slider(1.5, "input/web_slope"), wait,
            Family("tied_arch"), wait,
            Choice("deck_type", "Piastra ortotropa"), wait,
            Field("input/rib_spacing", "0,7"), wait,
            Family("cable_stayed"), wait,
            Field("input/tower_height", "80"), wait,
            Family("suspension"), wait,
            Choice("deck_type", "Soletta in cls"), wait,
            Family("truss"), Field("input/length", "120"), wait,
            Family("filler_beam"), Field("input/length", "60"), Field("input/spans", "3"), wait,
            Family("orthotropic"), wait,
            Family("fcm"), Field("input/cells", "1"), Field("input/bottom_ratio", "0,6"), wait,
            Family("psc_u"), Field("input/u_top", "2,6"), wait,
            Family("slab"), Field("input/length", "40"), wait,
            Field("input/depth", "0,9"), Field("input/fc", "50"), wait,
            Button("Sezione automatica"), wait,
            Family("psc_i"), Field("input/haunch", "0,08"), wait,
            calculate, commit, reopen, commit, wait]));
        // Piers, foundations and traffic: the choices, the imposed dimensions, the towers, the two traffic models with their coefficients.
        list.Add(new("pile-traffico", "default", [
            wait,
            Choice("pier", "Telaio a colonne"), Choice("foundation", "Pali Ø 1,0 m"), wait,
            Field("input/pile_length", "25"), Field("input/pile_count", "6"), wait,
            Field("input/pier_size", "1,8"), Field("input/footing_size", "7,5"), Field("input/fc_sub", "40"), wait,
            Choice("foundation", "Plinto diretto"), wait,
            Choice("traffic_model", "Uniforme equivalente (legacy)"), wait,
            Field("assumptions/traffic_load", "10,5"), Field("assumptions/gamma_q", "1,4"), wait,
            Choice("traffic_model", "Schema 1 NTC 2018"), wait,
            Button("Applica γQ NTC consigliato"), wait,
            Field("assumptions/gamma_q", "abc"), wait,
            Family("cable_stayed"), wait,
            Field("input/pier_size", "4"), wait,
            calculate, commit, reopen, commit, wait]));
        // The results: the comparison with the alternative A (pinned, restored, removed), the alternatives, the drawing, the scenery, undo
        // and the random bridge.
        list.Add(new("risultati-confronto", "default", [
            wait,
            Indexed(BridgeDesignStep.OutputTab, 1), Indexed(BridgeDesignStep.OutputTab, 2),
            Button("Fissa A"), wait,
            Field("input/length", "140"), wait,
            Button("Sezioni e quote"), Button("Sezione"), Button("Prospetto"),
            Button("Ripristina A"), wait,
            Family("steel_i"), wait,
            Button("Rimuovi confronto"), wait,
            Button("Fissa A"), wait,
            Indexed(BridgeDesignStep.Alternative, 0), wait,
            Indexed(BridgeDesignStep.Alternative, 0), wait,
            Button("Ripristina fattori ambientali ordinari"), wait,
            Button("Nuovo paesaggio"), wait,
            Indexed(BridgeDesignStep.Random, 7), wait,
            Indexed(BridgeDesignStep.Random, 21), wait,
            Button("Annulla"), wait,
            Button("Annulla"), Button("Annulla"), wait,
            Button("Ottimizza"), Indexed(BridgeDesignStep.Page, 0),
            calculate, commit, reopen, commit, wait]));
        // The prices without a price document: rates and assumptions typed, the indicative rates of 2026, undo of an invalid assumption, the
        // environmental factors; then the price editor turns the rates into a price document and changes it.
        list.Add(new("prezzi-ipotesi", "default", [
            wait,
            Field("rates/concrete_deck", "480"), wait,
            Field("rates/rebar", "1500,5"), Field("rates/steel", "abc"), wait,
            Button("Applica valori orientativi 2026"), wait,
            Field("assumptions/prelims", "abc"), wait,
            Button("Annulla"), wait,
            Field("assumptions/co2_concrete", "300"), wait,
            Field("assumptions/contingency", "20"), wait,
            Prices("annulla"), wait,
            Prices("applica"), wait,
            Prices("personalizza"), wait,
            Prices("prezzo-mancante"), wait,
            Button("Annulla"), wait,
            calculate, commit, reopen, commit, wait]));
        // The price document of a fixture: the editor cancelled, a catalogue changed, the alternative A repriced.
        list.Add(new("prezzario-documento", "cassone-acciaio-prezzario", [
            wait,
            Prices("annulla"), wait,
            Prices("catalogo"), wait,
            Button("Fissa A"), wait,
            Prices("rivaluta-a"), wait,
            Indexed(BridgeDesignStep.OutputTab, 2),
            calculate, commit, reopen, commit, wait]));
        // The search of the optimum: locks, limits, the suggested spans, the search, the ranking, the selection in the table and in the cloud, the
        // views, the first N, the preview, the solution applied and undone; invalid limits, the search interrupted, the data changed during it.
        list.Add(new("ottimizzazione", "default", [
            wait,
            Button("Ottimizza"),
            Lock("pier"), Lock("foundation"),
            Search("Suggerisci campate dalla lunghezza"),
            Limit("campate-min", "3"), Limit("campate-max", "6"),
            Search("Avvia ottimizzazione"), searched,
            Indexed(BridgeDesignStep.Rank, 2), Indexed(BridgeDesignStep.Point, 1),
            Indexed(BridgeDesignStep.Variable, 0), Indexed(BridgeDesignStep.Variable, 6), Indexed(BridgeDesignStep.Filter, 3), Step(BridgeDesignStep.Pareto),
            Step(BridgeDesignStep.Pareto), Indexed(BridgeDesignStep.Filter, 0),
            Limit("prime", "3"), Search("Aggiorna elenco"),
            Limit("prime", "abc"), Search("Aggiorna elenco"), Limit("prime", "10"),
            Search("Sezione"), Search("Prospetto"),
            Indexed(BridgeDesignStep.Objective, 1), Search("Avvia ottimizzazione"), searched,
            Search("Applica soluzione selezionata"), wait,
            Button("Annulla"), wait,
            Button("Ottimizza"),
            Lock("section"), Lock("depth"), Limit("altezza-max", "0,5"),
            Search("Avvia ottimizzazione"), searched,
            Lock("depth"), Lock("family"), Lock("spans"),
            Search("Suggerisci campate dalla lunghezza"),
            Limit("campate-min", "x"), Search("Avvia ottimizzazione"), searched,
            Limit("campate-min", "2"), Limit("pali-passo", "50"),
            Search("Avvia ottimizzazione"), Search("Interrompi"), searched,
            Indexed(BridgeDesignStep.Objective, 2), Search("Avvia ottimizzazione"), Field("input/lanes", "3"), searched, wait,
            Search("Avvia ottimizzazione"), searched,
            Search("Applica soluzione selezionata"), wait,
            commit, reopen, commit, wait]));
        // A read-only revision: every edit is refused; pages, tabs, views of the drawing, the search with its views, the report and the
        // calculation stay available.
        list.Add(new("sola-lettura", "confronto-alternativa-a", [
            wait, Step(BridgeDesignStep.ReadOnly),
            Field("input/length", "150"), Slider(30, "input/height"), Choice("soil", "Roccia"), Option("continuous"), Family("slab"),
            Button("Ripristina dimensioni automatiche"), Button("Sezione automatica"), Field("assumptions/gamma_q", "1,2"), Button("Applica γQ NTC consigliato"),
            Field("rates/rebar", "1000"), Button("Applica valori orientativi 2026"), Field("assumptions/prelims", "5"),
            Button("Ripristina A"), Button("Rimuovi confronto"), Indexed(BridgeDesignStep.Alternative, 0), Button("Ripristina fattori ambientali ordinari"),
            Button("Fissa A"), Button("Annulla"), Button("Nuovo paesaggio"), Indexed(BridgeDesignStep.Random, 3),
            Button("Sezione"), Button("Prospetto"), Button("Sezioni e quote"),
            Button("Ottimizza"), Lock("family"), Limit("campate-min", "2"), Indexed(BridgeDesignStep.Objective, 1), Search("Suggerisci campate dalla lunghezza"),
            Search("Avvia ottimizzazione"), Search("Interrompi"), Search("Aggiorna elenco"), Indexed(BridgeDesignStep.Variable, 3),
            Indexed(BridgeDesignStep.Page, 0), report, print, calculate, commit]));
        // Scheduling of the background calculation with a held engine: a calculation overtaken by an edit is never published, only the current
        // revision is published, «Calcola» during a calculation, the search waiting for the calculation, closing during a calculation.
        list.Add(new("calcolo-sullo-sfondo", "default", [
            wait,
            hold, Field("input/length", "120"), entered, Field("input/length", "125"), release, wait,
            hold, start, entered, start, release, wait,
            hold, Field("input/length", "115"), Button("Ottimizza"), Lock("family"), Lock("section"), Lock("pier"), Lock("foundation"),
            Search("Avvia ottimizzazione"), entered, release, searched, wait,
            hold, Field("input/length", "110"), entered, reopen, release, wait, commit]));
        return list;
    }
}

/// <summary>
/// The scripts of the price editor used by the trace: what the user does in the window opened by «Gestisci prezzario…». They act on the
/// ViewModel of the window (the historic window binds its controls to it, two-way), then «Applica prezzario» when the script asks for it and
/// the command can run, otherwise «Annulla». <see cref="Describe"/> records what the window showed when it closed (message of the step).
/// </summary>
internal static class BridgeDesignPriceScripts
{
    /// <summary>Runs <paramref name="script"/> on the editor; true when the user then presses «Applica prezzario».</summary>
    internal static bool Run(string script, PriceCatalogueViewModel editor)
    {
        WorkPriceRow Mapping(string work) => editor.Mappings.Single(m => m.WorkCode == work);
        PriceEntryRow Entry(string code) => editor.Entries.Single(e => e.Code == code);
        switch (script)
        {
            case "annulla": return false;
            case "applica": return true;
            case "personalizza":
                Mapping("rebar").Reason = "Offerta aggiornata"; Mapping("rebar").OverridePrice = "1750";
                return true;
            case "catalogo":
                editor.CatalogueName = "Listino di prova"; editor.Edition = "2026";
                Entry("concrete_deck").Price = "300";
                Mapping("pile_15").Reason = "Pali a corpo"; Mapping("pile_15").OverridePrice = "700";
                return true;
            case "rivaluta-a":
                editor.RepricePinnedAlternative = true;
                Entry("rebar").Price = "1800";
                return true;
            case "prezzo-mancante":
                Mapping("concrete_deck").EntryCode = "manca";
                return true;
            default: throw new ArgumentException("Script del prezzario sconosciuto: " + script);
        }
    }

    /// <summary>The window when it closes: status, preview, «Applica», the associations and the analyses.</summary>
    internal static string Describe(PriceCatalogueViewModel editor) => "prezzario " + BridgeDesignTrace.Quote(editor.CatalogueName) + " " + BridgeDesignTrace.Quote(editor.Edition)
        + " · stato " + BridgeDesignTrace.Quote(editor.Status) + " · anteprima " + BridgeDesignTrace.Quote(editor.CostPreview) + " · applica " + BridgeDesignTrace.YesNo(editor.CanApply)
        + " · A " + BridgeDesignTrace.YesNo(editor.HasPinnedAlternative) + " · associazioni "
        + string.Join(" ", editor.Mappings.Select(m => m.WorkCode + "=" + m.EntryCode + "/" + m.OverridePrice + "/" + m.PreviousPrice + "/" + m.ResolvedPrice + "/" + m.Status))
        + " · analisi " + string.Join(" ", editor.Analyses.Select(a => a.Code + "=" + a.UnitPrice)) + " · voci " + BridgeDesignTrace.Number(editor.Entries.Count)
        + " · revisioni " + BridgeDesignTrace.Number(editor.History.Count) + " · risultato " + (editor.Result is { } result ? BridgeDesignTrace.Hash(result) : "-");
}

/// <summary>
/// One block of a panel as the user reads it: a text with its role (titolo, evidenza, nota, avviso, errore, accento), a table (columns, rows
/// already formatted, the row selected), a button, a group (an expander) with its blocks, a parameter (key, label, the raw text, unit, the red
/// border of an invalid value, the slider), a choice (key, label, value, items), an option (key, label, checked), the typologies (their names,
/// the one selected marked «*») or a chart (its data). The two sides describe their panels with these blocks.
/// </summary>
internal sealed record BridgeDesignBlock(string Kind, string Text = "", string Role = "", IReadOnlyList<string>? Columns = null, IReadOnlyList<string[]>? Rows = null,
    IReadOnlyList<BridgeDesignBlock>? Children = null, int Selected = -1, string Key = "", string Label = "", string Unit = "", bool Invalid = false,
    double? Slider = null, IReadOnlyList<string>? Choices = null)
{
    internal const string TextKind = "testo", TableKind = "tabella", ButtonKind = "pulsante", GroupKind = "gruppo", FieldKind = "campo", ChoiceKind = "scelta",
        OptionKind = "opzione", FamiliesKind = "tipologie", ChartKind = "grafico";
}

/// <summary>
/// The state of the sheet seen by the user after a step, as keyed entries in a fixed order: the data (one entry per top-level key, in memory
/// order), Modified, the message of the step, the calculation, the status, the metrics, the configuration, pages and tabs, the undo history,
/// the drawing as data, the tabs of the inputs and the panels of the results as blocks, the alternatives, and the search of the optimum.
/// Each side fills it from its own objects; <see cref="BridgeDesignTrace.Lines"/> writes the first state whole and then, after every step,
/// only the entries that changed.
/// </summary>
internal sealed class BridgeDesignTraceState
{
    private readonly List<(string Key, string Text)> entries = [];
    private readonly HashSet<string> keys = new(StringComparer.Ordinal);

    internal IReadOnlyList<(string Key, string Text)> Entries => entries;

    internal void Add(string key, string text)
    {
        if (!keys.Add(key)) throw new InvalidOperationException("Voce della traccia ripetuta: " + key);
        entries.Add((key, BridgeDesignTrace.Normalize(text).Replace("\r", "\\r").Replace("\n", "\\n")));
    }

    /// <summary>The data: the order of the top-level keys and one entry for each key, in memory order.</summary>
    internal void Data(JsonObject data)
    {
        Add("dati", string.Join("|", data.Select(p => p.Key)));
        foreach (var (key, value) in data) Add("dati." + key, BridgeDesignTrace.Compact(value));
    }

    /// <summary>A panel: one entry for each block (tables by their size, hash and first three rows), «-» when there is no panel.</summary>
    internal void Panel(string name, IReadOnlyList<BridgeDesignBlock>? blocks)
    {
        if (blocks is null) { Add(name, "-"); return; }
        Add(name, BridgeDesignTrace.Number(blocks.Count) + " blocchi");
        Blocks(name, blocks);
    }

    private void Blocks(string prefix, IReadOnlyList<BridgeDesignBlock> blocks)
    {
        for (int i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            string key = prefix + "." + BridgeDesignTrace.Number(i);
            switch (block.Kind)
            {
                case BridgeDesignBlock.TableKind:
                    var rows = block.Rows!;
                    Add(key, "tabella " + BridgeDesignTrace.Quote(string.Join("|", block.Columns!)) + " · righe " + BridgeDesignTrace.Number(rows.Count)
                        + " " + BridgeDesignTrace.Hash(rows) + (block.Selected < 0 ? "" : " · selezione " + BridgeDesignTrace.Number(block.Selected)));
                    for (int r = 0; r < Math.Min(3, rows.Count); r++) Add(key + "." + BridgeDesignTrace.Number(r), string.Join(" | ", rows[r].Select(BridgeDesignTrace.Quote)));
                    break;
                case BridgeDesignBlock.GroupKind:
                    Add(key, "gruppo " + BridgeDesignTrace.Quote(block.Text));
                    Blocks(key, block.Children!);
                    break;
                case BridgeDesignBlock.ButtonKind:
                    Add(key, "pulsante " + BridgeDesignTrace.Quote(block.Text));
                    break;
                case BridgeDesignBlock.FieldKind:
                    Add(key, "campo " + block.Key + " " + BridgeDesignTrace.Quote(block.Label) + " = " + BridgeDesignTrace.Quote(block.Text)
                        + (block.Unit.Length == 0 ? "" : " [" + block.Unit + "]") + (block.Invalid ? " · errore" : "")
                        + (block.Slider is double slider ? " · cursore " + BridgeDesignTrace.Number(slider) : ""));
                    break;
                case BridgeDesignBlock.ChoiceKind:
                    Add(key, "scelta " + block.Key + " " + BridgeDesignTrace.Quote(block.Label) + " = " + BridgeDesignTrace.Quote(block.Text)
                        + " voci " + BridgeDesignTrace.Quote(string.Join("|", block.Choices!)));
                    break;
                case BridgeDesignBlock.OptionKind:
                    Add(key, "opzione " + block.Key + " " + BridgeDesignTrace.Quote(block.Label) + " = " + block.Text);
                    break;
                case BridgeDesignBlock.FamiliesKind:
                    Add(key, "tipologie " + BridgeDesignTrace.Quote(block.Text));
                    break;
                case BridgeDesignBlock.ChartKind:
                    Add(key, "grafico " + BridgeDesignTrace.Quote(block.Text));
                    break;
                default:
                    Add(key, "testo" + (block.Role.Length == 0 ? "" : " [" + block.Role + "]") + " " + BridgeDesignTrace.Quote(block.Text));
                    break;
            }
        }
    }
}

/// <summary>Formats of the trace, shared by the two sides.</summary>
internal static class BridgeDesignTrace
{
    // The dates written by the sheet (environmental factors, price history) and by the price editor change at every run.
    private static readonly Regex IsoDate = new(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})", RegexOptions.CultureInvariant);

    internal static string Normalize(string text) => IsoDate.Replace(text, "<DATAORA>");

    internal static string Compact(JsonNode? node) => PileGroupTrace.Compact(node);

    internal static string Hash(JsonNode node) => HashText(Normalize(Compact(node)));

    internal static string HashText(string text) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)))[..16];

    internal static string Hash(IReadOnlyList<string[]> rows) => PileGroupTrace.Hash(new JsonArray(rows.Select(r => (JsonNode)new JsonArray(r.Select(c => (JsonNode?)JsonValue.Create(c)).ToArray())).ToArray()));

    internal static string Quote(string? text) => PileGroupTrace.Quote(text);

    internal static string YesNo(bool value) => PileGroupTrace.YesNo(value);

    internal static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    internal static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>
    /// The trace of a step: the whole state after the opening (<paramref name="previous"/> null), then only what changed: «~» an entry with
    /// a new text, «+» a new entry, «-» an entry that no longer exists. The texts of the entries are the same on both sides, so equal traces
    /// mean equal states after every step.
    /// </summary>
    internal static IEnumerable<string> Lines(string step, BridgeDesignTraceState? previous, BridgeDesignTraceState current)
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
