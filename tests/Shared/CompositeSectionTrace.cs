using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Anthea.Presentation.Tests;

/// <summary>
/// One step of a scenario of the composite bridge section sheet (str_mista_ponte, F5.11), applied in the same way to the historic view
/// (BridgeWorkspace, tests/Desktop/CompositeSectionHistoricTrace.cs) and to its ViewModel (tests/ANTHEA.Presentation.Tests). A step is what
/// the user does with a control: a control that the user cannot use (hidden, disabled, read-only revision) does nothing.
/// <see cref="Form"/> names the form (<see cref="CompositeSectionScenarios.Forms"/>, or «fase», «omogeneizzazione», «proprieta-soletta» and
/// «curva»); <see cref="Key"/> the field, the column of the table of the actions or the switch; <see cref="Index"/> the phase, the page, the
/// situation, the tab or the choice; <see cref="Row"/> the row of the table (or the direction of a move); <see cref="Text"/> what the user
/// types or chooses.
/// </summary>
internal sealed record CompositeSectionStep(string Kind, string? Form = null, string? Key = null, string? Text = null, int Index = -1, int Row = -1)
{
    /// <summary>Commit of the sheet (navigation, save).</summary>
    internal const string Commit = "conferma";
    /// <summary>The «Calcola» command of the window (commit, then the calculation at once), awaited.</summary>
    internal const string Calculate = "calcola";
    /// <summary>Starts the «Calcola» command of the window without awaiting it.</summary>
    internal const string Start = "avvia-calcolo";
    /// <summary>Waits for the last calculation requested: the automatic one (500 ms after the last edit) or the started one.</summary>
    internal const string Wait = "attendi";
    /// <summary>Closes the sheet and opens its data again (a new sheet on a copy, as the window does).</summary>
    internal const string Reopen = "riapri";
    /// <summary>The sheet becomes a read-only revision.</summary>
    internal const string ReadOnly = "sola-lettura";
    /// <summary>The visit of the F3 capture: every expander opened and every page of every tab control chosen, then the first again.</summary>
    internal const string Visit = "visita";
    /// <summary>Chooses the page <see cref="Index"/> (0 control panel, 1 phases and stresses, 2 curves of the section).</summary>
    internal const string Page = "pagina";
    /// <summary>Types <see cref="Text"/> in the text field <see cref="Key"/> of the form <see cref="Form"/> (and of the phase <see cref="Index"/>), then leaves it.</summary>
    internal const string Field = "campo";
    /// <summary>Chooses <see cref="Text"/> in the choice <see cref="Key"/> of the form <see cref="Form"/>.</summary>
    internal const string Choice = "scelta";
    /// <summary>Clicks the check box <see cref="Key"/> of the form <see cref="Form"/>.</summary>
    internal const string Option = "opzione";
    /// <summary>«+ Aggiungi fase» of the page of the phases.</summary>
    internal const string AddPhase = "aggiungi-fase";
    /// <summary>«+ Ritiro» of the page of the phases.</summary>
    internal const string AddShrinkage = "aggiungi-ritiro";
    /// <summary>«↑» (<see cref="Row"/> −1) or «↓» (+1) of the card of the phase <see cref="Index"/>.</summary>
    internal const string MovePhase = "sposta-fase";
    /// <summary>«Elimina» of the card of the phase <see cref="Index"/>.</summary>
    internal const string DeletePhase = "elimina-fase";
    /// <summary>Edits the cell <see cref="Key"/> of the row <see cref="Row"/> of «Sollecitazioni»: types or chooses <see cref="Text"/>, or clicks the check box when null.</summary>
    internal const string Cell = "cella";
    /// <summary>Selects the row <see cref="Row"/> of «Sollecitazioni».</summary>
    internal const string SelectRow = "riga";
    /// <summary>«+ Carico» under «Sollecitazioni».</summary>
    internal const string TableLoad = "tabella-carico";
    /// <summary>«+ Ritiro» under «Sollecitazioni».</summary>
    internal const string TableShrinkage = "tabella-ritiro";
    /// <summary>«↑» under «Sollecitazioni».</summary>
    internal const string TableUp = "tabella-su";
    /// <summary>«↓» under «Sollecitazioni».</summary>
    internal const string TableDown = "tabella-giu";
    /// <summary>«Elimina» under «Sollecitazioni».</summary>
    internal const string TableDelete = "tabella-elimina";
    /// <summary>Chooses the situation <see cref="Index"/> («Risultati cumulati fino alla fase»).</summary>
    internal const string Situation = "situazione";
    /// <summary>«Mostra tutte le fasi → ultima situazione».</summary>
    internal const string Latest = "ultima-situazione";
    /// <summary>Chooses the display <see cref="Index"/> of the drawing (total stresses, contributions, geometry).</summary>
    internal const string Display = "vista";
    /// <summary>Chooses the tab <see cref="Index"/> of the results.</summary>
    internal const string Results = "risultati";
    /// <summary>Clicks the switch <see cref="Key"/> (<see cref="CompositeSectionScenarios.Switches"/>).</summary>
    internal const string Switch = "interruttore";
    /// <summary>Types <see cref="Text"/> in the scale of the concrete diagram («CLS ×») and presses Enter.</summary>
    internal const string Scale = "scala-cls";
    /// <summary>Chooses the section <see cref="Index"/> of the local sketch («Intermedio», «Appoggio»).</summary>
    internal const string DetailSection = "prospetto-sezione";
    /// <summary>Chooses the tab <see cref="Index"/> of the properties of the section (steel, slab, phases).</summary>
    internal const string PropertyTab = "proprieta";
    /// <summary>«Dettagli sezione efficace ↓».</summary>
    internal const string EffectiveDetails = "dettagli-efficace";
    /// <summary>Chooses the phase <see cref="Index"/> of the starting state of the curves.</summary>
    internal const string CurvePhase = "curva-fase";
    /// <summary>«Calcola curva», awaited.</summary>
    internal const string CurveCalculate = "calcola-curva";
    /// <summary>Chooses the fibre <see cref="Index"/> of the curves.</summary>
    internal const string CurveFiber = "fibra";
    /// <summary>Moves the slider of the points of the curve to <see cref="Index"/>.</summary>
    internal const string CurvePoint = "punto";
    /// <summary>«Stacca vista»: the drawing goes into a window of its own (off screen).</summary>
    internal const string Detach = "stacca-vista";
    /// <summary>«Riaggancia» of the detached window.</summary>
    internal const string Reattach = "riaggancia-vista";
    /// <summary>The Word report of the sheet with the sections <see cref="Text"/> (keys separated by «,»; null: all of them).</summary>
    internal const string Report = "report";
    /// <summary>The engine holds every calculation that enters it until <see cref="Release"/>.</summary>
    internal const string Hold = "trattieni";
    /// <summary>Waits until one more calculation is inside the held engine.</summary>
    internal const string Entered = "attendi-motore";
    /// <summary>Releases the held calculations; the engine stops holding.</summary>
    internal const string Release = "rilascia";

    public override string ToString() => Kind + (Form is null ? "" : " " + Form) + (Key is null ? "" : " " + Key)
        + (Index < 0 ? "" : " [" + Index.ToString(CultureInfo.InvariantCulture) + "]") + (Row == -1 ? "" : " (" + Row.ToString(CultureInfo.InvariantCulture) + ")")
        + (Text is null ? "" : " «" + Text + "»");
}

/// <summary>A scenario: the data of an F3 fixture of the module, then the steps.</summary>
internal sealed record CompositeSectionScenario(string Name, string Fixture, IReadOnlyList<CompositeSectionStep> Steps);

/// <summary>
/// The scenarios of the composite bridge section sheet (F5.11): for every F3 fixture of the module the path of the F3 capture (opening,
/// visit, calculation, reopening); then edits of the definition of the section, of the accessories and of the phases (forms, cards and the
/// table of the actions), the views of the results, the curves of the section, the detached view, the report, a read-only revision and
/// the scheduling of the background calculation with a held engine.
/// </summary>
internal static class CompositeSectionScenarios
{
    internal const string Module = "str_mista_ponte";

    /// <summary>Folder of the traces of the historic view, frozen before the migration (proposal 7.2, capture first).</summary>
    internal static string Reference(string root) => Path.Combine(root, "tests", "fixtures", "f5-sezione-mista", "storica");

    /// <summary>Folder of the frozen drawing lists of the historic view.</summary>
    internal static string Drawings(string root) => Path.Combine(root, "tests", "fixtures", "f5-sezione-mista", "disegni");

    internal static string FixturePath(string root, string id) => Path.Combine(root, "tests", "fixtures", "f3", "inputs", Module + "--" + id + ".json");

    /// <summary>The data of an F3 fixture, as the F3 capture reads them.</summary>
    internal static JsonObject FixtureData(string root, string id) => JsonNode.Parse(File.ReadAllText(FixturePath(root, id)))!["data"]!.AsObject();

    /// <summary>Identifiers of the F3 fixtures of the module, in ordinal order.</summary>
    internal static IReadOnlyList<string> Fixtures(string root) => Directory.EnumerateFiles(Path.Combine(root, "tests", "fixtures", "f3", "inputs"), Module + "--*.json")
        .Select(f => Path.GetFileNameWithoutExtension(f)[(Module.Length + 2)..]).Order(StringComparer.Ordinal).ToArray();

    /// <summary>
    /// The forms of the definition of the section, of the accessories and of the options, in the order of the trace, with a key that only
    /// that form has. The phases have one form each («fase») and, when the phase homogenizes the concrete, a second one
    /// («omogeneizzazione»); the slab properties («proprieta-soletta») and the curves («curva») have theirs.
    /// </summary>
    internal static IReadOnlyList<(string Name, string Key)> Forms { get; } = [("normativa", "normativa"), ("coefficienti", "gamma_v"), ("materiali", "classe_cls"),
        ("soletta", "b_cls"), ("predalle", "predalle"), ("carpenteria", "h_trave"), ("sezione", "sezione"), ("piastra", "plate2"), ("armatura-sup", "rebars_top"),
        ("armatura-inf", "rebars_bottom"), ("trasversale", "armatura_trasv"), ("fatica", "fatica_pioli"), ("pioli", "pioli"), ("irrigidimenti", "irrigidimenti"),
        ("appoggio", "appoggio"), ("cassoncino", "torsione_cassoncino"), ("metodo", "metodo_analisi"), ("discretizzazione", "fibre_anima"), ("limiti", "stato")];

    /// <summary>The form of the phases.</summary>
    internal const string PhaseForm = "fase";
    /// <summary>The form of the homogenization of a phase (φ, ψL, n).</summary>
    internal const string HomogenizationForm = "omogeneizzazione";
    /// <summary>The form of the slab properties (φ, ψL, n of the exploration).</summary>
    internal const string SlabPropertiesForm = "proprieta-soletta";
    /// <summary>The form of the curves of the section.</summary>
    internal const string CurveForm = "curva";

    /// <summary>The switches of the viewport and of the local sketch.</summary>
    internal static IReadOnlyList<string> Switches { get; } = ["quote", "info-armature", "asse-neutro", "contorno-sezione", "contorno-tensioni", "limiti-tensioni", "scala-auto", "prospetto"];

    static CompositeSectionStep Step(string kind) => new(kind);
    static CompositeSectionStep Field(string form, string key, string text, int phase = -1) => new(CompositeSectionStep.Field, form, key, text, phase);
    static CompositeSectionStep Choice(string form, string key, string text, int phase = -1) => new(CompositeSectionStep.Choice, form, key, text, phase);
    static CompositeSectionStep Option(string form, string key, int phase = -1) => new(CompositeSectionStep.Option, form, key, Index: phase);
    static CompositeSectionStep Indexed(string kind, int index) => new(kind, Index: index);
    static CompositeSectionStep Cell(int row, string key, string? text) => new(CompositeSectionStep.Cell, Key: key, Text: text, Row: row);
    static CompositeSectionStep Switch(string key) => new(CompositeSectionStep.Switch, Key: key);

    internal static IReadOnlyList<CompositeSectionScenario> All(string root)
    {
        CompositeSectionStep commit = Step(CompositeSectionStep.Commit), calculate = Step(CompositeSectionStep.Calculate), wait = Step(CompositeSectionStep.Wait),
            reopen = Step(CompositeSectionStep.Reopen), visit = Step(CompositeSectionStep.Visit), hold = Step(CompositeSectionStep.Hold),
            entered = Step(CompositeSectionStep.Entered), release = Step(CompositeSectionStep.Release);
        var list = new List<CompositeSectionScenario>();
        list.AddRange(Fixtures(root).Select(id => new CompositeSectionScenario("percorso-" + id, id, [wait, commit, visit, commit, calculate, commit, reopen, commit, wait])));
        var page0 = Indexed(CompositeSectionStep.Page, 0); var page1 = Indexed(CompositeSectionStep.Page, 1); var page2 = Indexed(CompositeSectionStep.Page, 2);
        // The definition of the section: standard, coefficients, materials, slab and predalle, the three types of section, the second plate, the bars.
        list.Add(new("ingressi-sezione", "default", [
            wait, page0,
            Field("normativa", "nome", "Trave di riva"), wait,
            Choice("normativa", "normativa", "EN 1994-2:2005"), wait,
            Field("coefficienti", "gamma_c", "1,4"), wait,
            Choice("materiali", "classe_cls", "C45/55"), Choice("materiali", "acciaio", "S275"), wait,
            Option("materiali", "fy_override"), Field("materiali", "fy", "265"), wait,
            Field("materiali", "fy", "abc"), wait,
            Field("materiali", "fy", "265"), wait,
            Field("soletta", "h_cls", "260"), wait,
            Option("predalle", "predalle"), Field("predalle", "h_predalle", "55"), wait,
            Choice("predalle", "rif_ferri_inf", "Intradosso soletta"), wait,
            Option("armatura-inf", "rebars_bottom"), wait,
            Option("armatura-inf", "rebars_bottom"), Field("armatura-inf", "cover_bottom", "50"), wait,
            Option("piastra", "plate2"), Field("piastra", "t_bottom2", "22"), wait,
            Field("carpenteria", "h_trave", "1900"), Field("carpenteria", "t_web", "15"), wait,
            Choice("sezione", "sezione", "H con anima inclinata"), Field("sezione", "offset_anima", "250"), wait,
            Choice("sezione", "sezione", "Cassoncino"), Field("sezione", "interasse_anime", "1700"), Field("carpenteria", "b_bottom", "1400"), wait,
            Option("cassoncino", "torsione_cassoncino"), Field("cassoncino", "t_controvento", "4"), Field("cassoncino", "L_campata", "36000"), wait,
            Field("cassoncino", "passo_diaframmi", "6000"), Choice("cassoncino", "tipo_diaframma", "Controvento a X"), Field("cassoncino", "T_app", "500"), wait,
            Field("carpenteria", "t_web", ""), wait,
            Field("carpenteria", "t_web", "15"), wait,
            Choice("sezione", "sezione", "H saldato"), wait,
            Indexed(CompositeSectionStep.PropertyTab, 1), Field(SlabPropertiesForm, "n", "8"), Field(SlabPropertiesForm, "psi", "1,1"),
            Indexed(CompositeSectionStep.PropertyTab, 3), Indexed(CompositeSectionStep.PropertyTab, 0),
            calculate, commit, reopen, commit, wait]));
        // The accessories: stiffeners, support, studs, transverse reinforcement, fatigue, with the local sketch.
        list.Add(new("ingressi-accessori", "default", [
            wait, page0,
            Option("irrigidimenti", "irrigidimenti"), wait,
            Choice("irrigidimenti", "lati_irr", "Bilaterali diversi"), Field("irrigidimenti", "b_irr_dx", "120"), Option("irrigidimenti", "pannelli_uguali"), wait,
            Option("irrigidimenti", "saldature_irr"), Field("irrigidimenti", "aw_irr", "7"), wait,
            Option("appoggio", "appoggio"), wait,
            Choice("appoggio", "pos_app", "Estremità destra"), Option("appoggio", "terminale_rigido"), Field("appoggio", "R_app", "1800"), wait,
            Indexed(CompositeSectionStep.DetailSection, 1), Switch("prospetto"), Switch("prospetto"),
            Option("pioli", "pioli"), wait,
            Field("pioli", "n_pioli", "1"), wait,
            Field("pioli", "n_pioli", "3"), Option("trasversale", "armatura_trasv"), Field("trasversale", "d_trasv_inf", "0"), wait,
            Option("trasversale", "forcine_bordo"), wait,
            Option("fatica", "fatica_pioli"), Field("fatica", "q_fat_min", "30"), Field("fatica", "q_fat_max", "200"), Option("fatica", "flangia_fat_tesa"), wait,
            Option("irrigidimenti", "irrigidimenti"), wait,
            page1, Field(PhaseForm, "V", "450", 1), Field(PhaseForm, "q_conn", "10", 2), wait,
            calculate, Indexed(CompositeSectionStep.Results, 4), commit, reopen, commit, wait]));
        // The phases on their page: kind, homogenization with φ and with n, references of N, the common level, cards added, moved and
        // deleted, the methods with history, the limits.
        list.Add(new("fasi-schede", "default", [
            wait, page1,
            Field(PhaseForm, "nome", "G2 · portati", 1), wait,
            Field(HomogenizationForm, "phi", "1,5", 1), wait,
            Field(HomogenizationForm, "n", "15", 1), wait,
            Field(HomogenizationForm, "psi", "1,2", 1), wait,
            Choice(PhaseForm, "riferimento_N", "Quota comune", 2), Field(PhaseForm, "N", "-300", 2), wait,
            Field("limiti", "y_ref", "-150"), wait,
            Choice(PhaseForm, "tipo", "Soletta esclusa", 2), wait,
            Choice(PhaseForm, "tipo", "Ritiro", 2), Field(PhaseForm, "epsilon_cs", "-200", 2), wait,
            Option(PhaseForm, "attiva", 0), wait,
            Option(PhaseForm, "attiva", 0), wait,
            Step(CompositeSectionStep.AddPhase), wait,
            Step(CompositeSectionStep.AddShrinkage), wait,
            new(CompositeSectionStep.MovePhase, Index: 4, Row: -1), wait,
            new(CompositeSectionStep.MovePhase, Index: 0, Row: -1), wait,
            Indexed(CompositeSectionStep.DeletePhase, 3), wait,
            Choice("limiti", "stato", "SLE quasi permanente"), Option("limiti", "classe4"), wait,
            Choice("metodo", "metodo_analisi", "Storico lineare"), Field("discretizzazione", "sottopassi", "4"), Field("discretizzazione", "fibre_anima", "60"), wait,
            Indexed(CompositeSectionStep.Situation, 1), Indexed(CompositeSectionStep.Results, 1), Indexed(CompositeSectionStep.Results, 3),
            Choice("metodo", "metodo_analisi", "Storico non lineare"), wait,
            Choice("metodo", "cls_teso_nl", "Tension stiffening"), Choice("metodo", "viscosita_nl", "Richiedi φ = 0 negli ingressi"), wait,
            Choice("metodo", "viscosita_nl", "Istantaneo · φ = 0 nel calcolo"), wait,
            Choice("metodo", "metodo_analisi", "Cumulativo lineare"), wait,
            calculate, commit, reopen, commit, wait]));
        // The table of the actions: cells of every kind, rows added, moved and deleted with the buttons under the table.
        list.Add(new("tabella-azioni", "anima-inclinata-ec4-sle", [
            wait, page1, Indexed(CompositeSectionStep.Results, 0),
            Cell(1, "Mx", "1900"), wait,
            Cell(4, "attiva", null), wait,
            Cell(1, "n", "14"), wait,
            Cell(0, "tipo", "Composta"), wait,
            Cell(0, "phi", "1"), wait,
            Cell(3, "riferimento_N", "Baricentro efficace · iterativo"), wait,
            Cell(2, "epsilon_cs", "-300"), Cell(2, "Mx", "50"), wait,
            Cell(3, "tipo", "Ritiro"), wait,
            new(CompositeSectionStep.SelectRow, Row: 1), Step(CompositeSectionStep.TableDown), wait,
            Step(CompositeSectionStep.TableUp), wait,
            Step(CompositeSectionStep.TableLoad), wait,
            Step(CompositeSectionStep.TableShrinkage), wait,
            new(CompositeSectionStep.SelectRow, Row: 5), Step(CompositeSectionStep.TableDelete), wait,
            new(CompositeSectionStep.SelectRow, Row: 0), Step(CompositeSectionStep.TableDelete), wait,
            calculate, commit, reopen, commit, wait]));
        // The views of the results: situations, displays, tabs, labels, neutral axis, contours, limits, scale of the concrete, details.
        list.Add(new("risultati-viste", "cassoncino-torsione-pioli", [
            wait, page1,
            Indexed(CompositeSectionStep.Situation, 0), Indexed(CompositeSectionStep.Display, 1), Indexed(CompositeSectionStep.Display, 0),
            Indexed(CompositeSectionStep.Results, 0), Indexed(CompositeSectionStep.Results, 1), Indexed(CompositeSectionStep.Results, 2), Indexed(CompositeSectionStep.Results, 3),
            Switch("asse-neutro"), Switch("contorno-sezione"), Switch("contorno-tensioni"), Switch("limiti-tensioni"),
            new(CompositeSectionStep.Scale, Text: "abc"), new(CompositeSectionStep.Scale, Text: "12,5"), Switch("scala-auto"), new(CompositeSectionStep.Scale, Text: "0,5"),
            Indexed(CompositeSectionStep.Display, 2), Switch("quote"), Switch("info-armature"),
            Step(CompositeSectionStep.Latest), Step(CompositeSectionStep.EffectiveDetails),
            Field(PhaseForm, "Mx", "3200", 2), wait,
            Indexed(CompositeSectionStep.Situation, 1), Field(PhaseForm, "T", "900", 2), wait,
            page0, Switch("quote"), Indexed(CompositeSectionStep.PropertyTab, 2), page1,
            Step(CompositeSectionStep.Commit), reopen, commit, wait]));
        // The results of the history and their tables.
        list.Add(new("risultati-storico", "storico-lineare-ritiro", [
            wait, page1,
            Indexed(CompositeSectionStep.Situation, 0), Indexed(CompositeSectionStep.Results, 1), Indexed(CompositeSectionStep.Results, 2), Indexed(CompositeSectionStep.Results, 3),
            Indexed(CompositeSectionStep.Results, 4), Indexed(CompositeSectionStep.Situation, 3), Indexed(CompositeSectionStep.Display, 1),
            Option(PhaseForm, "attiva", 4), wait,
            Step(CompositeSectionStep.Latest), commit, reopen, commit, wait]));
        // The curves of the section: the form, the starting phase, the calculation, the fibre and the point, the data changed after a curve.
        list.Add(new("curve-sezione", "storico-non-lineare-curve", [
            wait, page2,
            Field(CurveForm, "punti", "8"), Field(CurveForm, "sottopassi", "2"),
            Step(CompositeSectionStep.CurveCalculate),
            Indexed(CompositeSectionStep.CurveFiber, 0), Indexed(CompositeSectionStep.CurvePoint, 3),
            Indexed(CompositeSectionStep.CurvePhase, 0),
            Choice(CurveForm, "tipo", "Momento–curvatura · N costante"), Option(CurveForm, "n_storico"), Field(CurveForm, "N", "-500"), Field(CurveForm, "incremento_k", "0,002"),
            Step(CompositeSectionStep.CurveCalculate), Indexed(CompositeSectionStep.CurvePoint, 2),
            page1, Field(PhaseForm, "Mx", "2500", 2), wait, page2,
            Choice(CurveForm, "origine", "Sezione composta vergine"), Step(CompositeSectionStep.CurveCalculate),
            commit, reopen, commit, wait]));
        // The detached view of the drawing: page, display and drawing while detached, then reattached.
        list.Add(new("vista-staccata", "default", [
            wait, page0,
            Step(CompositeSectionStep.Detach), Indexed(CompositeSectionStep.Display, 2), page0, Indexed(CompositeSectionStep.Display, 1), Indexed(CompositeSectionStep.Situation, 0),
            Field("soletta", "b_cls", "3100"), wait,
            Step(CompositeSectionStep.Reattach), page0, page1, commit]));
        // The report: the sections chosen are saved in the sheet; without a result it is refused.
        list.Add(new("report", "irrigidimenti-appoggio-piastra2", [
            wait, new(CompositeSectionStep.Report, Text: "normativa,taglio,grafici"), new(CompositeSectionStep.Report),
            Field("soletta", "b_cls", "2900"), new(CompositeSectionStep.Report, Text: "geometria"), wait, commit]));
        // A read-only revision: the forms, the table and the buttons refuse; pages, situations, displays, neutral axis and details stay.
        list.Add(new("sola-lettura", "anima-inclinata-ec4-sle", [
            wait, Step(CompositeSectionStep.ReadOnly),
            page0, Field("soletta", "b_cls", "2000"), Choice("sezione", "sezione", "H saldato"), Option("predalle", "predalle"), Indexed(CompositeSectionStep.DetailSection, 1),
            Switch("quote"), Indexed(CompositeSectionStep.PropertyTab, 1), Field(SlabPropertiesForm, "n", "9"),
            page1, Field(PhaseForm, "Mx", "100", 0), Cell(0, "Mx", "100"), Step(CompositeSectionStep.AddPhase), Step(CompositeSectionStep.TableLoad),
            Indexed(CompositeSectionStep.DeletePhase, 0), Indexed(CompositeSectionStep.Situation, 0), Indexed(CompositeSectionStep.Display, 0), Switch("asse-neutro"),
            Switch("contorno-sezione"), new(CompositeSectionStep.Scale, Text: "3"), Step(CompositeSectionStep.Latest), Step(CompositeSectionStep.EffectiveDetails),
            Indexed(CompositeSectionStep.Results, 2), page2, Field(CurveForm, "punti", "5"), Step(CompositeSectionStep.CurveCalculate),
            page1, calculate, commit]));
        // The background calculation with the engine held: a calculation overtaken by an edit, «Calcola» during a calculation, closing.
        list.Add(new("calcolo-sullo-sfondo", "default", [
            wait, page1,
            hold, Field(PhaseForm, "Mx", "2100", 1), entered, Indexed(CompositeSectionStep.Display, 1), Field(PhaseForm, "Mx", "2200", 1), release, wait,
            hold, Step(CompositeSectionStep.Start), entered, Indexed(CompositeSectionStep.Situation, 0), release, wait,
            Field(PhaseForm, "Mx", "2300", 1), hold, Step(CompositeSectionStep.Start), entered, Step(CompositeSectionStep.Start), release, wait,
            hold, Field(PhaseForm, "Mx", "2400", 1), entered, reopen, release, wait, commit]));
        return list;
    }
}

/// <summary>A field of a form as the user sees it: kind, label, value, choices and the state of its row and of its editor.</summary>
internal sealed record CompositeSectionFieldState(string Key, string Kind, string Label, string Value, IReadOnlyList<string>? Choices, bool Enabled, bool Dimmed,
    bool Visible, bool Editable)
{
    internal string Line() => Kind + " " + CompositeSectionTrace.Quote(Label) + " = " + CompositeSectionTrace.Quote(Value)
        + (Choices is null ? "" : " voci " + CompositeSectionTrace.Quote(string.Join("|", Choices)))
        + (Enabled ? "" : " · spento") + (Dimmed ? " · attenuato" : "") + (Visible ? "" : " · nascosto") + (Editable ? "" : " · bloccato");
}

/// <summary>
/// One block of a panel of results, as the user reads it: a text, a table (columns and rows already formatted), or a group (expander) with
/// its blocks. The two sides describe their panels with these blocks; the trace writes them in one format.
/// </summary>
internal sealed record CompositeSectionBlock(string Kind, string Text, IReadOnlyList<string>? Columns = null, IReadOnlyList<string[]>? Rows = null,
    IReadOnlyList<CompositeSectionBlock>? Children = null)
{
    internal const string TextKind = "testo", TableKind = "tabella", GroupKind = "gruppo";
}

/// <summary>
/// The state of the sheet seen by the user after a step, as keyed entries in a fixed order: data (one entry per top-level key, in memory
/// order), Modified, result and status texts, page and selections, the fields of every form, the cards of the phases, the table of the
/// actions, the panels of the results, the properties of the section, the drawing and the local sketch as data, the page of the curves.
/// Each side fills it from its own objects; <see cref="CompositeSectionTrace.Lines"/> writes the first state whole and then, after every
/// step, only the entries that changed.
/// </summary>
internal sealed class CompositeSectionTraceState
{
    private readonly List<(string Key, string Text)> entries = [];
    private readonly HashSet<string> keys = new(StringComparer.Ordinal);

    internal IReadOnlyList<(string Key, string Text)> Entries => entries;

    internal void Add(string key, string text)
    {
        if (!keys.Add(key)) throw new InvalidOperationException("Voce della traccia ripetuta: " + key);
        entries.Add((key, text.Replace("\r", "\\r").Replace("\n", "\\n")));
    }

    internal void Add(string key, bool value) => Add(key, CompositeSectionTrace.YesNo(value));

    /// <summary>The data: the order of the top-level keys and one entry for each key, in memory order.</summary>
    internal void Data(JsonObject data)
    {
        Add("dati", string.Join("|", data.Select(p => p.Key)));
        foreach (var (key, value) in data) Add("dati." + key, CompositeSectionTrace.Compact(value));
    }

    /// <summary>The fields of a form, «-» when the sheet has no such form.</summary>
    internal void Form(string name, IReadOnlyList<CompositeSectionFieldState>? fields)
    {
        if (fields is null) { Add("form." + name, "-"); return; }
        Add("form." + name, string.Join("|", fields.Select(f => f.Key)));
        foreach (var field in fields) Add("form." + name + "." + field.Key, field.Line());
    }

    /// <summary>A panel: one entry for each block (tables by their size, hash and first three rows), «-» when there is no panel.</summary>
    internal void Panel(string name, IReadOnlyList<CompositeSectionBlock>? blocks)
    {
        if (blocks is null) { Add(name, "-"); return; }
        Add(name, CompositeSectionTrace.Number(blocks.Count) + " blocchi");
        Blocks(name, blocks);
    }

    private void Blocks(string prefix, IReadOnlyList<CompositeSectionBlock> blocks)
    {
        for (int i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            string key = prefix + "." + CompositeSectionTrace.Number(i);
            switch (block.Kind)
            {
                case CompositeSectionBlock.TableKind:
                    Add(key, "tabella " + CompositeSectionTrace.Quote(string.Join("|", block.Columns!)) + " · righe " + CompositeSectionTrace.Number(block.Rows!.Count)
                        + " " + CompositeSectionTrace.Hash(block.Rows));
                    for (int r = 0; r < Math.Min(3, block.Rows.Count); r++) Add(key + "." + CompositeSectionTrace.Number(r), string.Join(" | ", block.Rows[r].Select(CompositeSectionTrace.Quote)));
                    break;
                case CompositeSectionBlock.GroupKind:
                    Add(key, "gruppo " + CompositeSectionTrace.Quote(block.Text));
                    Blocks(key, block.Children!);
                    break;
                default:
                    Add(key, "testo " + CompositeSectionTrace.Quote(block.Text));
                    break;
            }
        }
    }
}

/// <summary>Formats of the trace, shared by the two sides.</summary>
internal static class CompositeSectionTrace
{
    internal static string Compact(JsonNode? node) => PileGroupTrace.Compact(node);

    internal static string Hash(JsonNode node) => PileGroupTrace.Hash(node);

    internal static string HashText(string text) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)))[..16];

    internal static string Hash(IReadOnlyList<string[]> rows) => PileGroupTrace.Hash(new JsonArray(rows.Select(r => (JsonNode)new JsonArray(r.Select(c => (JsonNode?)JsonValue.Create(c)).ToArray())).ToArray()));

    /// <summary>Hash of a plain object (a geometry, points), serialized as JSON with the invariant culture.</summary>
    internal static string HashObject(object? value) => value is null ? "-" : PileGroupTrace.Hash(JsonSerializer.SerializeToNode(value, PileGroupTrace.Json)!);

    internal static string Quote(string? text) => PileGroupTrace.Quote(text);

    internal static string YesNo(bool value) => PileGroupTrace.YesNo(value);

    internal static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    internal static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>
    /// The trace of a step: the whole state after the opening (<paramref name="previous"/> null), then only what changed: «~» an entry with a
    /// new text, «+» a new entry, «-» an entry that no longer exists.
    /// </summary>
    internal static IEnumerable<string> Lines(string step, CompositeSectionTraceState? previous, CompositeSectionTraceState current)
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
