using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Anthea.Application.Concrete;
using Anthea.Application.Documents;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules.ConcreteSection;

namespace Anthea.Presentation.Tests;

/// <summary>
/// One step of a scenario of the RC section sheet (str_palo, F5.10), applied to the historic view (ConcreteWorkspace inside SheetEditor,
/// tests/Desktop/ConcreteSectionHistoricTrace.cs) and, sub-step by sub-step, to the ViewModels that will replace it. A step is what the user
/// does with a control: a control that the user cannot use (disabled, hidden, or protected by the read-only revision) does nothing.
/// <see cref="Target"/> names the form, the table or the panel (see <see cref="ConcreteSectionScenarios"/>); <see cref="Key"/> the field, the
/// column or the control; <see cref="Text"/> what the user types or chooses; <see cref="Index"/> and <see cref="Row"/> a tab, a row or a
/// position. The steps of a modal dialog are in <see cref="Dialog"/>, run while the dialog is open.
/// </summary>
internal sealed record ConcreteSectionStep(string Kind, string? Target = null, string? Key = null, string? Text = null, int Index = -1, int Row = -1,
    IReadOnlyList<ConcreteSectionStep>? Dialog = null)
{
    /// <summary>Commit of the sheet (navigation, save).</summary>
    internal const string Commit = "conferma";
    /// <summary>The «Calcola» command of the window (the sheet calculates what is pending), awaited.</summary>
    internal const string Calculate = "calcola";
    /// <summary>Starts the «Calcola» command of the window without awaiting it (it waits for the calculation in progress).</summary>
    internal const string Start = "avvia-calcolo";
    /// <summary>Waits until the sheet is idle: automatic recalculation, quick resistances, inspections and meshes published.</summary>
    internal const string Wait = "attendi";
    /// <summary>Closes the sheet and opens its data again (a new sheet on a copy, as the window does).</summary>
    internal const string Reopen = "riapri";
    /// <summary>The sheet becomes a read-only revision.</summary>
    internal const string ReadOnly = "sola-lettura";
    /// <summary>The visit of the F3 capture: every expander opened, every tab of every tab control selected and restored.</summary>
    internal const string Visit = "visita";
    /// <summary>Selects the main tab <see cref="Index"/> (0 control panel … 7 model column, 8 reinforcement design since 1.15.0: the scenarios select
    /// «Calcola armature» with 8, the tab 7 of the historic traces).</summary>
    internal const string Tab = "scheda";
    /// <summary>Types <see cref="Text"/> in the text field <see cref="Key"/> of the form <see cref="Target"/>.</summary>
    internal const string Field = "campo";
    /// <summary>Chooses <see cref="Text"/> in the choice <see cref="Key"/> of the form <see cref="Target"/>.</summary>
    internal const string Choice = "scelta";
    /// <summary>Clicks the check box field <see cref="Key"/> of the form <see cref="Target"/>.</summary>
    internal const string Option = "opzione";
    /// <summary>Clicks the check box <see cref="Key"/> (its content) of the panel <see cref="Target"/>.</summary>
    internal const string Toggle = "spunta";
    /// <summary>Clicks the button <see cref="Key"/> (its content, or the name of a toggle of the layers) of the panel <see cref="Target"/>.</summary>
    internal const string Button = "pulsante";
    /// <summary>Chooses <see cref="Text"/> in the choice <see cref="Key"/> of the panel <see cref="Target"/> outside the forms (contour, state, view…).</summary>
    internal const string Pick = "menu";
    /// <summary>Moves the slider <see cref="Key"/> of the panel <see cref="Target"/> to <see cref="Text"/>.</summary>
    internal const string Slider = "cursore";
    /// <summary>Writes <see cref="Text"/> in the cell <see cref="Key"/> of the row <see cref="Row"/> of the table <see cref="Target"/> (as the binding of the cell does).</summary>
    internal const string Cell = "cella";
    /// <summary>Selects the row <see cref="Index"/> of the table <see cref="Target"/>, in the order shown.</summary>
    internal const string Select = "seleziona";
    /// <summary>Selects the row <see cref="Index"/> of the table <see cref="Target"/>, then clicks its command <see cref="Key"/> («+ Riga», «Duplica», «− Riga», …).</summary>
    internal const string RowCommand = "riga";
    /// <summary>Pastes <see cref="Text"/> (tab-separated rows) in the table <see cref="Target"/> from the row <see cref="Index"/>; <see cref="Key"/> «aggiungi» appends.</summary>
    internal const string Paste = "incolla";
    /// <summary>Filter of the table <see cref="Target"/>: column <see cref="Key"/>, mode <see cref="Index"/>, text <see cref="Text"/>, order <see cref="Row"/>.</summary>
    internal const string Filter = "filtro";
    /// <summary>Selects the tab <see cref="Index"/> of the inner tab control <see cref="Target"/> (SLE family, details, inspection…).</summary>
    internal const string InnerTab = "sottoscheda";
    /// <summary>Opens the modal dialog <see cref="Target"/> («sollecitazioni», «materiale», «scala») and runs the steps of <see cref="Dialog"/>.</summary>
    internal const string Open = "apri";
    /// <summary>Starts the report <see cref="Text"/> («completo» or «breve») of the window and records whether it is available.</summary>
    internal const string Report = "report";
    /// <summary>The engine of the sheet holds every batch of checks that enters it until <see cref="Release"/>.</summary>
    internal const string Hold = "trattieni";
    /// <summary>Waits until a batch of checks waits in the held engine.</summary>
    internal const string Entered = "attendi-motore";
    /// <summary>Releases the held engine.</summary>
    internal const string Release = "rilascia";

    public override string ToString() => Kind + (Target is null ? "" : " " + Target) + (Key is null ? "" : " " + Key)
        + (Index < 0 ? "" : " [" + Index.ToString(CultureInfo.InvariantCulture) + (Row < 0 ? "" : "," + Row.ToString(CultureInfo.InvariantCulture)) + "]")
        + (Index < 0 && Row >= 0 ? " [riga " + Row.ToString(CultureInfo.InvariantCulture) + "]" : "")
        + (Text is null ? "" : " «" + Text.Replace("\n", "⏎", StringComparison.Ordinal).Replace("\t", "⇥", StringComparison.Ordinal) + "»")
        + (Dialog is null ? "" : " { " + string.Join(" ; ", Dialog) + " }");
}

/// <summary>
/// A scenario: the data of an F3 fixture of str_palo, then the steps. <see cref="SubStep"/> is the sub-step of F5.10 that owns the
/// scenario (the folder of its trace); <see cref="Sections"/> the parts of the sheet written after every step (the common part always).
/// </summary>
internal sealed record ConcreteSectionScenario(string SubStep, string Name, string Fixture, IReadOnlyList<string> Sections, IReadOnlyList<ConcreteSectionStep> Steps)
{
    internal string File => Path.Combine(SubStep, Name + ".txt");
}

/// <summary>
/// The scenarios of the RC section sheet (F5.10), grouped by the sub-steps of the migration (proposal F5, row F5.10), so that each sub-step
/// has its own frozen traces: «percorso-f3» (the path of the F3 capture of every fixture: opening, visit, calculation, reopening, every part
/// of the sheet); «ingressi» (standard, coefficients, materials, geometry, reinforcement, links, tendons, quick resistances); «azioni» (the
/// tables of the actions, paste, and the modal editor of the actions, the MVVM island of SectionActionsViewModel); «domini» (3D and 2D
/// domains, filters, display options); «sle»; «taglio» (shear and torsion); «dettagli» (detailing and anchorages); «armature»
/// (reinforcement design); «ispezione» (section σ/ε, strain plane, M–χ); «report»; «calcolo» (recalculation, cancellation, current revision,
/// read-only revision).
/// </summary>
internal static class ConcreteSectionScenarios
{
    internal const string Module = "str_palo";

    /// <summary>The parts of the sheet in the trace, one per sub-step; «comune» is always written.</summary>
    internal static readonly string[] AllSections = ["ingressi", "azioni", "domini", "sle", "taglio", "dettagli", "armature", "ispezione"];

    /// <summary>Folder of the traces of the historic view, frozen before the migration (proposal 7.2, capture first).</summary>
    internal static string Reference(string root) => Path.Combine(root, "tests", "fixtures", "f5-sezione", "storica");

    /// <summary>Folder of the display lists of the 2D drawings and of the plain data of the 3D domain, frozen before the migration.</summary>
    internal static string Drawings(string root) => Path.Combine(root, "tests", "fixtures", "f5-sezione", "disegni");

    /// <summary>The file of a fixture: an F3 fixture of the module, («muri/…») a section sent by the retaining walls (F5.10 S1, section 5), or
    /// («esempi/…») an example of the distribution (F5.10 S1b).</summary>
    internal static string FixturePath(string root, string id) => id.StartsWith(WallPrefix, StringComparison.Ordinal)
        ? Path.Combine(WallSections(root), id[WallPrefix.Length..] + ".json")
        : id.StartsWith(ExamplePrefix, StringComparison.Ordinal) ? Path.Combine(ExampleSections(root), id[ExamplePrefix.Length..] + ".anthea")
        : Path.Combine(root, "tests", "fixtures", "f3", "inputs", Module + "--" + id + ".json");

    /// <summary>
    /// The data of a fixture, as the F3 capture reads them (the object «data» of the file); for an example, as the window opens it (the
    /// codec of version 3 through DocumentService.Open, then the data of the sheet, with the native section in input.sezione_gpc).
    /// </summary>
    internal static JsonObject FixtureData(string root, string id) => id.StartsWith(ExamplePrefix, StringComparison.Ordinal)
        ? DocumentService.Open(FixturePath(root, id))["dati"]!.AsObject()
        : JsonNode.Parse(System.IO.File.ReadAllText(FixturePath(root, id)))!["data"]!.AsObject();

    internal const string ExamplePrefix = "esempi/";

    /// <summary>
    /// Folder of the examples of the module in the distribution (F5.10 S1b, section 9.5 of docs/refactoring/f5-10-sezione.md): documents of
    /// version 3 with the native GPC section, twelve of them made by the wizard of the types.
    /// </summary>
    internal static string ExampleSections(string root) => Path.Combine(root, "supporto", "esempi", "distribuzione", Module);

    /// <summary>Identifiers («esempi/…») of the examples of the module, in ordinal order.</summary>
    internal static IReadOnlyList<string> ExampleFixtures(string root) => Directory.Exists(ExampleSections(root))
        ? Directory.EnumerateFiles(ExampleSections(root), "*.anthea").Select(f => ExamplePrefix + Path.GetFileNameWithoutExtension(f)).Order(StringComparer.Ordinal).ToArray() : [];

    /// <summary>
    /// The part of the sections made by the wizard (F5.10 S1b), written only by the scenarios that ask for it (the frozen scenarios of S1 do
    /// not): the command of the wizard, the geometry and the reinforcement hidden with their notes, the fields of the hole, the note of the
    /// shear parameters.
    /// </summary>
    internal const string WizardSection = "wizard";

    internal const string WallPrefix = "muri/";

    /// <summary>
    /// Folder of the sections that the retaining walls send to the module c.a. («Apri sezione in c.a.» of the scenario «risultati-sezione» of
    /// F5.9): the data that the request of the walls opens in a sheet of str_palo (section 5 of docs/refactoring/f5-10-sezione.md).
    /// </summary>
    internal static string WallSections(string root) => Path.Combine(root, "tests", "fixtures", "f5-sezione", "muri");

    /// <summary>Identifiers («muri/…») of the sections of the walls, in ordinal order.</summary>
    internal static IReadOnlyList<string> WallFixtures(string root) => Directory.Exists(WallSections(root))
        ? Directory.EnumerateFiles(WallSections(root), "*.json").Select(f => WallPrefix + Path.GetFileNameWithoutExtension(f)).Order(StringComparer.Ordinal).ToArray() : [];

    /// <summary>Identifiers of the F3 fixtures of the module, in ordinal order.</summary>
    internal static IReadOnlyList<string> Fixtures(string root) => Directory.EnumerateFiles(Path.Combine(root, "tests", "fixtures", "f3", "inputs"), Module + "--*.json")
        .Select(f => Path.GetFileNameWithoutExtension(f)[(Module.Length + 2)..]).Order(StringComparer.Ordinal).ToArray();

    static ConcreteSectionStep Step(string kind) => new(kind);
    static ConcreteSectionStep Tab(int index) => new(ConcreteSectionStep.Tab, Index: index);
    static ConcreteSectionStep Field(string form, string key, string text) => new(ConcreteSectionStep.Field, form, key, text);
    static ConcreteSectionStep Choice(string form, string key, string text) => new(ConcreteSectionStep.Choice, form, key, text);
    static ConcreteSectionStep Option(string form, string key) => new(ConcreteSectionStep.Option, form, key);
    static ConcreteSectionStep Toggle(string panel, string key) => new(ConcreteSectionStep.Toggle, panel, key);
    static ConcreteSectionStep Press(string panel, string key) => new(ConcreteSectionStep.Button, panel, key);
    static ConcreteSectionStep Pick(string panel, string key, string text) => new(ConcreteSectionStep.Pick, panel, key, text);
    static ConcreteSectionStep Cell(string table, int row, string key, string text) => new(ConcreteSectionStep.Cell, table, key, text, Row: row);
    static ConcreteSectionStep Select(string table, int index) => new(ConcreteSectionStep.Select, table, Index: index);
    static ConcreteSectionStep RowCommand(string table, int index, string command) => new(ConcreteSectionStep.RowCommand, table, command, Index: index);
    static ConcreteSectionStep Inner(string control, int index) => new(ConcreteSectionStep.InnerTab, control, Index: index);
    static ConcreteSectionStep Open(string dialog, params ConcreteSectionStep[] steps) => new(ConcreteSectionStep.Open, dialog, Dialog: steps);

    internal static IReadOnlyList<ConcreteSectionScenario> All(string root)
    {
        ConcreteSectionStep commit = Step(ConcreteSectionStep.Commit), calculate = Step(ConcreteSectionStep.Calculate), wait = Step(ConcreteSectionStep.Wait),
            reopen = Step(ConcreteSectionStep.Reopen), visit = Step(ConcreteSectionStep.Visit), hold = Step(ConcreteSectionStep.Hold),
            entered = Step(ConcreteSectionStep.Entered), release = Step(ConcreteSectionStep.Release);
        string[] all = AllSections;
        var list = new List<ConcreteSectionScenario>();
        // The path of the F3 capture, every part of the sheet: opening, the automatic calculation of the opening, the visit, «Calcola», reopening.
        foreach (string id in Fixtures(root))
            list.Add(new("percorso-f3", id, id, all, [wait, commit, visit, wait, commit, calculate, commit, reopen, wait, commit]));
        // The sections sent by the retaining walls, opened as the request of the walls opens them (F5.10 S1, section 5): the same path.
        foreach (string id in WallFixtures(root))
            list.Add(new("muri", id[WallPrefix.Length..], id, all, [wait, commit, visit, wait, commit, calculate, commit, reopen, wait, commit]));

        // ---------------------------------------------------------------- inputs and materials
        list.Add(new("ingressi", "geometria-forme", "default", ["ingressi", "taglio"], [
            wait,
            Choice("geometria", "shape", "Circolare"), wait,
            Field("geometria", "diameter_mm", "900"), Field("geometria", "circular_sides", "36"), wait,
            Toggle("foro", "Foro centrale"), wait,
            Field("foro", "inner_diameter_mm", "350"), wait,
            Press("strati", "inner"), wait,
            Field("strato-inner", "second_inner_count", "10"), Field("strato-inner", "second_inner_gap", "abc"), wait,
            Field("strato-inner", "second_inner_gap", "35"), wait,
            Choice("geometria", "shape", "A T"), wait,
            Field("geometria", "flange_width_mm", "1500"), Field("geometria", "flange_thickness_mm", "220"), wait,
            Field("armatura", "flange_bottom_count", "4"), Field("armatura", "flange_bottom_offset_mm", "75"), wait,
            Choice("geometria", "shape", "Rettangolare"), wait,
            Toggle("foro", "Foro centrale"), Field("foro", "inner_width_mm", "250"), wait,
            Press("strati", "top"), Press("strati", "bottom"), wait,
            Field("strato-top", "second_top_count", "3"), wait,
            Press("strati", "bottom"), wait,
            Field("geometria", "cover_mm", "45"), Field("geometria", "width_mm", "650"), wait,
            Field("armatura", "bottom_bar_count", "7"), Field("armatura", "side_bar_count_per_side", "1"), wait,
            Field("geometria", "height_mm", "0"), wait,
            Field("geometria", "height_mm", "800"), wait,
            Choice("geometria", "shape", "Generica (da definire)"), wait,
            Choice("geometria", "shape", "Rettangolare"), wait,
            Toggle("anteprima", "Assi"), Toggle("anteprima", "ID"), Toggle("anteprima", "Dimensioni"), Toggle("anteprima", "Copriferro"), Toggle("anteprima", "Interferro min."),
            commit, reopen, wait]));
        list.Add(new("ingressi", "barre-manuali", "default", ["ingressi"], [
            wait,
            Cell("barre", 0, "x", "-200"), wait,
            Cell("barre", 2, "phi", "26"), wait,
            Select("barre", 3),
            Field("armatura", "bottom_bar_count", "8"), wait,
            Press("armatura", "Ripristina barre da wizard"), wait,
            Field("armatura", "bottom_bar_count", "8"), wait,
            commit, reopen, wait]));
        list.Add(new("ingressi", "materiali-normativa", "default", ["ingressi", "sle"], [
            wait,
            Choice("materiali", "classe_cls", "C30/37"), wait,
            Choice("materiali", "cls_diagramma", "Stress block"), wait,
            Choice("materiali", "classe_acciaio", "B450A"), wait,
            Choice("materiali", "steel_diagramma", "Incrudente"), wait,
            Choice("materiali", "gettato_sottile", "Sì"), wait,
            Choice("normativa", "normativa", "EN 1992-1-1"), wait,
            Choice("materiali", "classe_acciaio", "B500B"), wait,
            Field("coefficienti", "GammaC", "1.4"), Field("coefficienti", "AlphaCC", "1"), wait,
            Field("coefficienti", "GammaS", "abc"), wait,
            Press("coefficienti", "Ripristina coefficienti della normativa"), wait,
            Choice("normativa", "normativa", "Model Code 2010"), wait,
            Choice("materiali", "classe_cls", "Personalizzato"), wait,
            Choice("normativa", "normativa", "NTC 2018"), wait,
            Field("normativa", "nota", "Pila P1 · sezione di base"), wait,
            commit, reopen, wait]));
        list.Add(new("ingressi", "materiali-utente", "rettangolare-materiali-utente", ["ingressi"], [
            wait,
            Open("materiale", Field("dialogo", "nome", "C35/45"), Press("dialogo", "Salva materiale e applica")),
            Open("materiale", Field("dialogo", "nome", "CLS secondo"), Field("dialogo", "fck_mpa", "abc"), Press("dialogo", "Salva materiale e applica")),
            Open("materiale", Field("dialogo", "nome", "CLS secondo"), Field("dialogo", "fck_mpa", "38"), Choice("dialogo", "cls_diagramma", "Stress block"),
                Press("dialogo", "Salva materiale e applica")), wait,
            Open("materiale-acciaio", Field("dialogo", "nome", "Acciaio secondo"), Field("dialogo", "fyk_mpa", "520"), Field("dialogo", "steel_fu_mpa", "600"),
                Press("dialogo", "Salva materiale e applica")), wait,
            Pick("materiali-utente", "elenco", "Calcestruzzo · CLS prova 32"), Press("materiali-utente", "Applica materiale salvato"), wait,
            Field("materiali", "fck_mpa", "33"), wait,
            commit, reopen, wait]));
        list.Add(new("ingressi", "staffe-trefoli", "default", ["ingressi", "sle", "taglio"], [
            wait,
            Field("staffe", "transverse_bar_diameter_mm", "12"), wait,
            Field("staffe", "transverse_spacing_mm", "150"), wait,
            Field("schema-staffe", "rami_x", "3"), Field("schema-staffe", "rami_y", "4"), wait,
            Choice("staffe", "staffe_presenti", "No"), wait,
            Choice("staffe", "staffe_presenti", "Sì"), wait,
            Field("cavo", "numero", "3"), Field("cavo", "x", "0"), Field("cavo", "y", "-250"), Field("cavo", "sigma0", "1250"),
            Press("cavo", "+ Cavo / trefolo"), wait,
            Field("cavo", "sigma0", "2000"), Press("cavo", "+ Cavo / trefolo"), wait,
            Field("cavo", "sigma0", "1100"), Field("cavo", "y", "250"), Press("cavo", "+ Cavo / trefolo"), wait,
            Cell("trefoli", 0, "area", "450"), wait,
            Cell("trefoli", 1, "diametro", "15.2"), wait,
            Cell("trefoli", 0, "materiale", "Y1770"), wait,
            Pick("trefoli", "standard", "Y1770"), wait,
            Choice("trefolo", "diagramma", "Incrudente"), wait,
            Select("trefoli", 1), Press("cavo", "−"), wait,
            commit, reopen, wait]));
        list.Add(new("ingressi", "resistenze-rapide", "default", ["ingressi"], [
            wait,
            Field("rapide", "N", "-2500"), wait,
            Choice("rapide", "tipo", "Elastico"), wait,
            Field("rapide", "N", "abc"), wait,
            Field("rapide", "N", "-500"), wait,
            commit, reopen, wait]));

        // ---------------------------------------------------------------- actions (tables, paste, modal editor: the MVVM island)
        list.Add(new("azioni", "tabelle-storiche", "default", ["azioni", "domini", "sle"], [
            wait, Tab(1), wait,
            Cell("3D", 0, "N", "-1800"), wait,
            RowCommand("3D", 0, "+ Riga"), wait,
            Cell("3D", 1, "Mx", "420"), Cell("3D", 1, "nome", "SLU nuova"), wait,
            RowCommand("3D", 1, "Duplica"), wait,
            Cell("3D", 2, "visible", "false"), wait,
            RowCommand("3D", 0, "− Riga"), wait,
            Pick("3D", "stato", "SLV"), wait,
            RowCommand("3D", 0, "+ Riga"), wait,
            Tab(3), wait,
            RowCommand("SLE", 0, "+ Riga"), Cell("SLE", 1, "N", "-900"), Cell("SLE", 1, "Mx", "150"), wait,
            commit, reopen, wait]));
        list.Add(new("azioni", "incolla", "default", ["azioni"], [
            wait, Tab(1),
            new(ConcreteSectionStep.Paste, "3D", Text: "Combo A\t-1000\t200\t30\nCombo B\t-1200\t250\t40", Index: 0), wait,
            new(ConcreteSectionStep.Paste, "3D", "aggiungi", "nome\tN\tMx\tMy\nCombo C\t-800\t100\t10", Index: 0), wait,
            new(ConcreteSectionStep.Paste, "3D", Text: "Combo D\t-800\tabc\t10", Index: 0), wait,
            new(ConcreteSectionStep.Paste, "3D", Text: "1\t2", Index: 0), wait,
            Tab(4),
            new(ConcreteSectionStep.Paste, "taglio", Text: "Taglio X\t-500\t100\t50", Index: 0), wait,
            new(ConcreteSectionStep.Paste, "taglio", "aggiungi", "Taglio Y\t-500\t0\t0\t90\t40\t0", Index: 0), wait,
            commit, reopen, wait]));
        list.Add(new("azioni", "editor-sollecitazioni", "default", ["azioni", "domini", "taglio"], [
            wait,
            Open("sollecitazioni", Press("dialogo", "Annulla")),
            Open("sollecitazioni", Press("dialogo", "Applica")), wait,
            Open("sollecitazioni", Cell("dialogo", 0, "State", "SLU"), Cell("dialogo", 1, "State", "SLV"), Cell("dialogo", 2, "State", "Rara"),
                Select("dialogo", 0), Press("dialogo", "Vuoti a zero · riga selezionata"),
                Select("dialogo", 1), Press("dialogo", "Vuoti a zero · riga selezionata"),
                Select("dialogo", 2), Press("dialogo", "Vuoti a zero · riga selezionata"),
                Press("dialogo", "+ Riga"), Press("dialogo", "Applica"),
                Cell("dialogo", 3, "State", "Frequente"), Cell("dialogo", 3, "N", "-700"), Cell("dialogo", 3, "Mx", "90"), Cell("dialogo", 3, "My", "0"),
                Cell("dialogo", 3, "Vx", "0"), Cell("dialogo", 3, "Vy", "0"), Cell("dialogo", 3, "T", "0"), Cell("dialogo", 3, "Element", "Pila"),
                Select("dialogo", 3), Press("dialogo", "Duplica"), Cell("dialogo", 4, "State", "Quasi permanente"),
                Press("dialogo", "Applica")), wait,
            Tab(1), wait, Tab(4), wait,
            Open("sollecitazioni", Select("dialogo", 1), Press("dialogo", "Elimina"), Cell("dialogo", 0, "Vy", "120"), Press("dialogo", "Applica")), wait,
            commit, reopen, wait]));
        list.Add(new("azioni", "sollecitazioni-uniche", "sezione-a-t-sollecitazioni-uniche", ["azioni", "domini", "taglio"], [
            wait,
            Cell("3D", 0, "visible", "false"), wait,
            Cell("3D", 1, "visible", "true"), wait,
            Open("sollecitazioni", Cell("dialogo", 0, "T", "5"), Cell("dialogo", 1, "Name", ""), Press("dialogo", "Applica"), Cell("dialogo", 1, "Name", "SLU 2 bis"),
                Press("dialogo", "Applica")), wait,
            commit, reopen, wait]));

        // ---------------------------------------------------------------- domains
        list.Add(new("domini", "opzioni-domini", "default", ["domini"], [
            wait, Tab(1), wait,
            Choice("3D", "criterio", "Eccentricità costante"), wait,
            Choice("3D", "strategia", "Intersezione"), wait,
            Choice("3D", "interpolazione", "Lineare"), wait,
            Field("3D", "suddivisioni_n", "30"), wait,
            Field("3D", "angoli", "16"), wait,
            Choice("3D", "assi", "Personalizzati"), Field("3D", "origine_x", "30"), Field("3D", "rotazione", "15"), wait,
            Choice("3D", "filtro", "Entro il dominio"), wait,
            Choice("3D", "filtro", "Tutte"),
            Press("3D", "Forze: tutte"), Toggle("3D", "Tutti i punti resistenti"), Toggle("3D", "Colori η"), Toggle("3D", "Linee di verifica"),
            new(ConcreteSectionStep.Slider, "3D", "dimensione_ed", "8"), new(ConcreteSectionStep.Slider, "3D", "trasparenza", "60"),
            Pick("3D", "vista", "N–Mx"), Press("3D", "Mesh"), wait,
            Tab(2), wait,
            Choice("2D", "tipo", "N–M"), wait,
            Field("2D", "theta", "45"), wait,
            Choice("2D", "proietta", "Sì"), wait,
            Press("2D", "Sezione sull’azione selezionata"), wait,
            Choice("2D", "tipo", "Mx–My"), Press("2D", "Sezione sull’azione selezionata"), wait,
            Choice("2D", "trazione_cls", "Sì"), wait,
            // Not here: directions refused by the engine. Both checks of the 2D domain (SLU and SLV) would stop, and the order of their
            // errors in the status follows the thread that ends first (docs/refactoring/f5-10-sezione.md, section 2).
            Pick("2D", "stato", "SLV"), wait,
            commit, reopen, wait]));
        list.Add(new("domini", "filtri-tabelle", "circolare-cava", ["domini"], [
            wait,
            new(ConcreteSectionStep.Filter, "2D", "Combinazione", "trazione", Index: 0, Row: 0), wait,
            new(ConcreteSectionStep.Filter, "2D", "Combinazione", "SLU", Index: 1, Row: 2), wait,
            new(ConcreteSectionStep.Filter, "2D", "N [kN]", "", Index: 0, Row: 1), wait,
            Select("2D", 1), wait,
            new(ConcreteSectionStep.Filter, "3D", "Combinazione", "pressoflessione", Index: 3, Row: 0), wait,
            commit]));

        // ---------------------------------------------------------------- serviceability
        list.Add(new("sle", "opzioni-sle", "default", ["sle", "dettagli"], [
            wait, Tab(3), wait,
            Choice("SLE", "esposizione", "XC3"), wait,
            Choice("SLE", "sensibilita", "Sensibile"), wait,
            Field("SLE", "phi", "2"), wait,
            Field("SLE", "n_armature", "15"), wait,
            Field("SLE", "phi", "abc"), wait,
            Choice("SLE", "modello", "Non lineare"), wait,
            Choice("SLE", "modello", "Lineare"), Choice("SLE", "assi", "Personalizzati"), Field("SLE", "origine_y", "-50"), wait,
            Field("SLE", "limite_fessure", "0.3"), Field("SLE", "copriferro_fessure", "60"), wait,
            Inner("sle", 2), wait,
            Pick("SLE_QP", "contorno", "CLS · deformazioni"), Toggle("SLE_QP", "σ/ε barre"), Toggle("SLE_QP", "Asse neutro"), wait,
            Inner("sle", 1), RowCommand("SLE_FREQ", 0, "+ Riga"), Cell("SLE_FREQ", 0, "N", "-1500"), Cell("SLE_FREQ", 0, "Mx", "300"), wait,
            commit, reopen, wait]));
        list.Add(new("sle", "fessurazione", "sle-fessurazione-assi", ["sle"], [
            wait, wait, Inner("sle", 0), Select("SLE", 1), wait,
            Inner("dettagli-SLE", 3), wait,
            Inner("sle", 1), Select("SLE_FREQ", 1), wait,
            Inner("sle", 2), Pick("SLE_QP", "zone", "1"), wait,
            Choice("SLE_QP", "esposizione", "XC1"), wait,
            Choice("SLE_QP", "durata", "Lunga"), wait,
            commit]));

        // ---------------------------------------------------------------- shear and torsion
        list.Add(new("taglio", "taglio-torsione", "default", ["taglio"], [
            wait, Tab(4), wait,
            RowCommand("taglio", 0, "+ Combinazione"), wait,
            Cell("taglio", 0, "Vx", "300"), Cell("taglio", 0, "Vy", "120"), Cell("taglio", 0, "N", "-800"), wait,
            RowCommand("taglio", 0, "+ Combinazione"), Cell("taglio", 1, "Vx", "200"), Cell("taglio", 1, "T", "40"), wait,
            Choice("torsione", "chiusura_torsione", "Confermato"), Field("torsione", "as_torsione", "500"), wait,
            Field("torsione", "cot_torsione", "2"), wait,
            Choice("taglio", "parametri", "Manuali"), Field("taglio", "bw_x", "500"), wait,
            Choice("taglio", "parametri", "Automatici da sezione"), wait,
            Field("taglio", "cot_x", "2.5"), Field("taglio", "alpha_y", "60"), wait,
            Choice("taglio", "modello", "Senza staffe"), wait,
            Choice("taglio", "ancoraggio", "Confermato"), wait,
            Choice("taglio", "modello", "Con staffe"), wait,
            Select("taglio", 1), wait,
            RowCommand("taglio", 0, "−"), wait,
            Choice("normativa", "normativa", "Model Code 2010"), Tab(4), wait,
            commit, reopen, wait]));
        list.Add(new("taglio", "circolare-senza-staffe", "circolare-cava", ["taglio"], [
            wait, Tab(4), wait,
            Choice("torsione", "modello_circolare", "Parametri assegnati"), Field("torsione", "z_d", "0.6"), wait,
            Choice("torsione", "chiusura_torsione", "Confermato"), wait,
            Choice("staffe", "staffe_presenti", "No"), Tab(4), wait,
            Choice("taglio", "modello", "Con staffe"), wait,
            commit, reopen, wait]));

        // ---------------------------------------------------------------- detailing and anchorages
        list.Add(new("dettagli", "dettagli-ancoraggi", "default", ["dettagli"], [
            wait, Tab(5), wait,
            Choice("dettagli", "elemento", "Trave"), wait,
            Choice("dettagli", "ancoraggio_appoggi", "Confermato"), Choice("dettagli", "barre_trattenute", "Confermato"), wait,
            Field("dettagli", "aggregato", "25"), wait,
            Choice("dettagli", "vita_durabilita", "100"), Choice("dettagli", "qualita_copriferro", "Sì"), Field("dettagli", "delta_c", "5"), wait,
            Choice("dettagli", "elemento", "Soletta piena"), Field("dettagli", "as_secondaria", "400"), Field("dettagli", "passo_secondaria", "250"), wait,
            Choice("dettagli", "zona_critica", "Sì"), wait,
            Choice("dettagli", "elemento", "Pilastro"), wait,
            Choice("ancoraggi", "tipo", "Sovrapposizione rettilinea"), Field("ancoraggi", "lunghezza", "800"), Field("ancoraggi", "percentuale", "50"), wait,
            Field("ancoraggi", "diametro", "24"), Field("ancoraggi", "sigma", "300"), Press("ancoraggi", "Aggiorna verifiche ancoraggi"), wait,
            Choice("ancoraggi", "confinamento", "Verificato sul disegno"), Choice("ancoraggi", "posizione", "Non conforme"), wait,
            Choice("ancoraggi", "tipo", "Ancoraggio rettilineo"), wait,
            commit, reopen, wait]));
        list.Add(new("dettagli", "soletta", "soletta-dettagli-ricerca", ["dettagli"], [
            wait, Tab(5), wait,
            Choice("dettagli", "zona_sovrapposizione", "No"), wait,
            Field("ancoraggi", "lunghezza", "400"), wait,
            commit]));

        // ---------------------------------------------------------------- reinforcement design
        list.Add(new("armature", "ricerca-armature", "soletta-dettagli-ricerca", ["armature", "ingressi"], [
            wait, Tab(8), wait,
            Choice("ricerca", "obiettivo", "Peso"), wait,
            Option("ricerca", "ricerca_completa"), wait,
            Field("ricerca", "diametri", "16;20"), Field("ricerca", "n_superiori", "10"), Field("ricerca", "n_inferiori", "6;7"), Field("ricerca", "diametri_ortogonali", "8;10"),
            Field("ricerca", "passi_ortogonali", "150;200"), Field("ricerca", "max_diametri", "3"), Choice("ricerca", "parallelismo", "1"), wait,
            // The fixture's 30 mm cover is below the nominal cover of its exposure and its lap length is short for Ø16 (detailing
            // tab, below): without the two changes every configuration would be refused.
            Field("geometria", "cover_mm", "60"), wait,
            // The fixture keeps a torsion reserve of 0 %, which the search refuses; then the default 25 %; then the search refuses the
            // section inside a lap zone (detailing), which the scenario removes.
            Press("ricerca", "Calcola armature"), wait,
            Field("ricerca", "riserva_torsione", "25"), Press("ricerca", "Calcola armature"), wait,
            Tab(5), Choice("dettagli", "zona_sovrapposizione", "No"), Field("ancoraggi", "lunghezza", "900"), Tab(8), wait,
            Press("ricerca", "Calcola armature"), wait,
            Choice("mostra", "mostra", "1"), wait,
            Select("alternative", 0), wait,
            Field("limiti", "slu", "0.9"), wait,
            Press("ricerca", "Calcola armature"), wait,
            Press("ricerca", "Applica configurazione"), wait,
            commit, reopen, wait]));
        list.Add(new("armature", "opzioni-ricerca", "default", ["armature"], [
            wait, Tab(8), wait,
            Choice("ricerca", "obiettivo", "Compromesso"), wait,
            Choice("ricerca", "staffe", "Mantieni"), wait,
            Option("ricerca", "simmetrica"), Option("ricerca", "ricerca_completa"), wait,
            Field("ricerca", "diametri", "abc"), Press("ricerca", "Calcola armature"), wait,
            Choice("geometria", "shape", "Circolare"), Tab(8), wait,
            commit]));

        // ---------------------------------------------------------------- inspection: section σ/ε, strain plane, M–χ
        list.Add(new("ispezione", "sezione-sigma-epsilon", "default", ["ispezione"], [
            wait, Tab(1), wait,
            Inner("ispezione-3D", 1), wait,
            Pick("ispezione-3D", "stato", "Azione inserita"), wait,
            Pick("ispezione-3D", "contorno", "CLS · deformazioni"), Toggle("ispezione-3D", "Valori σ/ε sulle barre"), wait,
            Tab(2), Inner("ispezione-2D", 1), wait,
            Field("geometria", "width_mm", "650"), Tab(2), wait,
            commit]));
        list.Add(new("ispezione", "momento-curvatura", "default", ["ispezione"], [
            wait, Tab(6), wait,
            Press("curvatura", "Calcola curva"), wait,
            Field("curvatura", "N", "-1500"), wait,
            Press("curvatura", "Calcola curva"), wait,
            Choice("curvatura", "cls_teso_risposte", "Tension stiffening"), Field("curvatura", "passi", "30"), Press("curvatura", "Calcola curva"), wait,
            Field("curvatura", "passi", "abc"), Press("curvatura", "Calcola curva"), wait,
            commit, reopen, wait]));

        // ---------------------------------------------------------------- reports
        list.Add(new("report", "disponibilita", "default", ["ingressi"], [
            new(ConcreteSectionStep.Report, Text: "completo"), new(ConcreteSectionStep.Report, Text: "breve"), wait,
            new(ConcreteSectionStep.Report, Text: "completo"), new(ConcreteSectionStep.Report, Text: "breve"),
            Field("geometria", "width_mm", "620"),
            new(ConcreteSectionStep.Report, Text: "completo"), wait,
            new(ConcreteSectionStep.Report, Text: "completo"), commit]));
        list.Add(new("report", "dati-incompleti", "dati-incompleti-avvisi", ["ingressi", "domini"], [
            wait, new(ConcreteSectionStep.Report, Text: "completo"), new(ConcreteSectionStep.Report, Text: "breve"),
            Field("geometria", "height_mm", "abc"), wait,
            new(ConcreteSectionStep.Report, Text: "completo"), commit]));

        // ---------------------------------------------------------------- scheduling and read-only revision
        list.Add(new("calcolo", "ricalcolo-revisione", "default", [], [
            wait,
            hold, Field("geometria", "width_mm", "620"), entered, Field("geometria", "width_mm", "640"), entered, release, wait,
            hold, Field("geometria", "height_mm", "820"), entered, Step(ConcreteSectionStep.Start), release, wait,
            hold, Field("geometria", "cover_mm", "60"), entered, reopen, release, wait,
            commit]));
        list.Add(new("calcolo", "sola-lettura", "circolare-cava", all, [
            wait, Step(ConcreteSectionStep.ReadOnly),
            Choice("geometria", "shape", "Rettangolare"), Field("geometria", "diameter_mm", "800"), Toggle("foro", "Foro centrale"), Choice("materiali", "classe_cls", "C30/37"),
            Field("armatura", "longitudinal_bar_count", "12"), Press("strati", "inner"), Field("rapide", "N", "-10"),
            Tab(1), Cell("3D", 0, "N", "-1"), RowCommand("3D", 0, "+ Riga"), Choice("3D", "criterio", "Eccentricità costante"), Choice("3D", "filtro", "Fuori dominio"),
            Toggle("3D", "Colori η"), Press("3D", "Forze: tutte"),
            Tab(3), Choice("SLE", "esposizione", "XC1"), Pick("SLE", "contorno", "Solo geometria"),
            Tab(4), Choice("taglio", "modello", "Senza staffe"),
            Tab(5), Choice("dettagli", "elemento", "Trave"),
            Tab(6), Press("curvatura", "Calcola curva"), wait,
            Tab(8), Field("ricerca", "diametri", "16"),
            Open("sollecitazioni", Press("dialogo", "Applica")),
            Tab(0), wait, commit]));

        // ---------------------------------------------------------------- sections made by the wizard (F5.10 S1b, section 9.5)
        // Added after the frozen scenarios of S1, which do not ask for the part «[wizard]» and keep their traces.
        string[] withWizard = [.. all, WizardSection];
        // The examples of the distribution, opened as the window opens an example: the path of «percorso-f3», every part and the wizard.
        foreach (string id in ExampleFixtures(root))
            list.Add(new("esempi", id[ExamplePrefix.Length..], id, withWizard, [wait, commit, visit, wait, commit, calculate, commit, reopen, wait, commit]));
        // The window of the wizard: cancelled, a type with a value refused and then accepted, the controls of the editor that the wizard
        // locks, «modifica disposizione» on the section applied, a hollow rectangle (double contour), then an ordinary circle again.
        list.Add(new("ingressi", "wizard-tipologie", "default", ["ingressi", WizardSection], [
            wait,
            Cell("barre", 0, "x", "-200"), wait,
            Open("wizard", Press("dialogo", "Annulla")), wait,
            Open("wizard", Choice("dialogo", "wizard_type", "A I"), Field("dialogo", "bottom_flange_width_mm", "abc"), Press("dialogo", "Applica sezione"),
                Field("dialogo", "bottom_flange_width_mm", "700"), Field("dialogo", "web_width_mm", "350"), Field("dialogo", "inner_diameter_mm", "300"),
                Press("dialogo", "Applica sezione")), wait,
            Field("geometria", "width_mm", "1200"), Toggle("foro", "Foro centrale"), Press("strati", "top"), Field("armatura", "top_bar_count", "6"),
            Choice("schema-staffe", "tipo_staffa", "Spirale"), wait,
            Field("staffe", "transverse_spacing_mm", "150"), wait,
            Open("wizard", Field("dialogo", "wizard_bar_spacing_mm", "150"), Press("dialogo", "Applica sezione")), wait,
            Open("wizard", Choice("dialogo", "wizard_type", "Rettangolare cava · due contorni"), Field("dialogo", "inner_width_mm", "350"), Press("dialogo", "Applica sezione")), wait,
            Field("foro", "inner_width_mm", "300"), Toggle("foro", "Foro centrale"), Press("strati", "bottom"), wait,
            Open("wizard", Choice("dialogo", "wizard_type", "Circolare · due anelli"), Press("dialogo", "Applica sezione")), wait,
            Field("geometria", "diameter_mm", "1100"), Field("strato-inner", "second_inner_count", "12"), wait,
            commit, reopen, wait]));
        // The capsule of the examples (old name of its type, radial layout): bars by coordinates on a section of the wizard, the two layouts
        // of the capsule with a count refused and then accepted, links absent and present again.
        list.Add(new("ingressi", "wizard-capsula", ExamplePrefix + "wizard-capsula", ["ingressi", WizardSection], [
            wait,
            Cell("barre", 0, "phi", "24"), wait,
            Press("armatura", "Ripristina barre da wizard"), wait,
            Open("wizard", Choice("dialogo", "wizard_stadium_layout", "Curve e rettilinei separati"), Field("dialogo", "wizard_stadium_curve_count", "1"),
                Press("dialogo", "Applica sezione"), Field("dialogo", "wizard_stadium_curve_count", "11"), Field("dialogo", "wizard_stadium_straight_diameter_mm", "16"),
                Press("dialogo", "Applica sezione")), wait,
            Open("wizard", Choice("dialogo", "wizard_stadium_layout", "Radiale dal centro (precedente)"), Field("dialogo", "wizard_radial_count", "28"),
                Press("dialogo", "Applica sezione")), wait,
            Choice("staffe", "staffe_presenti", "No"), wait,
            Choice("staffe", "staffe_presenti", "Sì"), wait,
            commit, reopen, wait]));
        // The hollow circle of the examples (double contour): the controls of the hole and of the circular links locked, the wizard applied
        // again, then the read-only revision.
        list.Add(new("ingressi", "wizard-sola-lettura", ExamplePrefix + "wizard-circolare-cava", ["ingressi", WizardSection], [
            wait,
            Toggle("foro", "Foro centrale"), Field("foro", "inner_diameter_mm", "300"), Choice("schema-staffe", "tipo_staffa", "Spirale"), Field("geometria", "diameter_mm", "900"), wait,
            Open("wizard", Field("dialogo", "wizard_inner_count", "12"), Press("dialogo", "Applica sezione")), wait,
            Step(ConcreteSectionStep.ReadOnly),
            Open("wizard", Press("dialogo", "Annulla")),
            Cell("barre", 0, "phi", "24"), Field("staffe", "transverse_spacing_mm", "150"),
            commit]));
        return list;
    }
}

/// <summary>A field of a form as the user sees it: kind, key, label, value, choices, enabled, dimmed, visible, read-only, note.</summary>
internal sealed record ConcreteFieldState(string Key, string Kind, string Label, string Value, IReadOnlyList<string>? Choices, bool Enabled, bool Dimmed, bool Visible,
    bool ReadOnly, string? Note)
{
    internal string Line() => Kind + " " + Key + " " + ConcreteSectionTrace.Quote(Label) + " = " + ConcreteSectionTrace.Quote(Value)
        + (Choices is null ? "" : " voci " + ConcreteSectionTrace.Quote(string.Join("|", Choices)))
        + (Enabled ? "" : " · spento") + (Dimmed ? " · attenuato" : "") + (Visible ? "" : " · nascosto") + (ReadOnly ? " · sola lettura" : "")
        + (string.IsNullOrEmpty(Note) ? "" : " · nota " + ConcreteSectionTrace.Quote(Note));
}

/// <summary>A form: its fields in order and its groups (title, keys, open).</summary>
internal sealed record ConcreteFormState(string Name, IReadOnlyList<ConcreteFieldState> Fields, IReadOnlyList<(string Title, IReadOnlyList<string> Keys, bool Open)> Groups)
{
    internal IEnumerable<string> Lines()
    {
        yield return "form " + Name;
        foreach (var field in Fields) yield return "  " + field.Line();
        foreach (var group in Groups) yield return "  gruppo " + ConcreteSectionTrace.Quote(group.Title) + " " + string.Join("|", group.Keys) + (group.Open ? " · aperto" : "");
    }
}

/// <summary>
/// A table as the user sees it: the visible columns (header and key), the rows shown in order (every value of the row, inputs and outputs),
/// the selected row, whether it is editable, the state of its filter.
/// </summary>
internal sealed record ConcreteTableState(string Name, IReadOnlyList<string> Columns, IReadOnlyList<JsonObject> Rows, int Selected, bool Editable, string? Filter, int Total)
{
    internal IEnumerable<string> Lines()
    {
        yield return "tabella " + Name + " · colonne " + ConcreteSectionTrace.Quote(string.Join("|", Columns)) + " · righe " + Rows.Count.ToString(CultureInfo.InvariantCulture)
            + (Total == Rows.Count ? "" : " di " + Total.ToString(CultureInfo.InvariantCulture)) + " · selezionata " + Selected.ToString(CultureInfo.InvariantCulture)
            + (Editable ? "" : " · sola lettura") + (Filter is null ? "" : " · filtro " + ConcreteSectionTrace.Quote(Filter));
        foreach (var row in Rows) yield return "  " + ConcreteSectionTrace.Canonical(row);
    }
}

/// <summary>The cards of the verifications (VerificationCards): their text and the colour of every card.</summary>
internal sealed record ConcreteCardsState(string Name, string Text, IReadOnlyList<string> Colors)
{
    internal string Line() => "riquadri " + Name + " " + ConcreteSectionTrace.Quote(Text) + (Colors.Count == 0 ? "" : " · colori " + string.Join(" ", Colors));
}

/// <summary>
/// A part of the state of the sheet (one per sub-step): its lines, already formatted with the records above. Each side fills the parts from
/// its own objects; the trace writes «[sottopasso] riga».
/// </summary>
internal sealed class ConcreteSectionBlock(string tag)
{
    internal string Tag { get; } = tag;
    internal List<string> Lines { get; } = [];
    internal void Add(string line) => Lines.Add(line);
    internal void Text(string name, string? text) => Lines.Add(name + " " + ConcreteSectionTrace.Quote(text));
    internal void Form(ConcreteFormState form) => Lines.AddRange(form.Lines());
}

/// <summary>
/// The state of the sheet seen by the user after a step: the common part (data in memory order, through the hash and the differences from
/// the previous state; Modified; calculation state; result with its names in order, because the parallel checks publish it in the order
/// in which they end; status; tab; whether it is editable), the message of the step, then the parts of the sub-steps written by the
/// scenario.
/// </summary>
internal sealed class ConcreteSectionTraceState
{
    public JsonObject Data = new();
    public int Modified;
    public bool Busy, Queued, HasResults, Editable, Progress;
    public JsonObject? Result;
    public string Status = "";
    public int Tab = -1;
    public string? Message;
    public List<ConcreteSectionBlock> Blocks = [];

    internal IEnumerable<string> Lines(string step, ConcreteGuids guids, ConcreteDataMemory memory)
    {
        yield return "== " + step;
        string data = guids.Normalize(ConcreteSectionTrace.Compact(Data));
        yield return "dati " + ConcreteSectionTrace.HashText(data) + " · byte " + Encoding.UTF8.GetByteCount(data).ToString(CultureInfo.InvariantCulture);
        foreach (string change in memory.Changes(JsonNode.Parse(data)!)) yield return "  " + change;
        yield return "modificato " + Modified.ToString(CultureInfo.InvariantCulture) + " · modificabile " + ConcreteSectionTrace.YesNo(Editable)
            + " · occupato " + ConcreteSectionTrace.YesNo(Busy) + " · in coda " + ConcreteSectionTrace.YesNo(Queued) + " · risultati " + ConcreteSectionTrace.YesNo(HasResults)
            + " · avanzamento " + ConcreteSectionTrace.YesNo(Progress) + " · scheda " + Tab.ToString(CultureInfo.InvariantCulture);
        // The identifiers of the rows are names of the result: they are replaced before the names are put in order.
        yield return "risultato " + (Result is null ? "-" : ConcreteSectionTrace.HashText(ConcreteSectionTrace.Canonical(JsonNode.Parse(guids.Normalize(ConcreteSectionTrace.Compact(Result))))));
        yield return "stato " + ConcreteSectionTrace.Quote(guids.Normalize(Status));
        if (Message is not null) yield return "messaggio " + ConcreteSectionTrace.Quote(guids.Normalize(Message));
        foreach (var block in Blocks)
        {
            var lines = block.Lines.Select(guids.Normalize).ToList();
            // A part equal to the one written after the previous step is written once: «[parte] invariata».
            if (memory.Unchanged(block.Tag, lines)) { yield return "[" + block.Tag + "] invariata"; continue; }
            foreach (string line in lines) yield return "[" + block.Tag + "] " + line;
        }
    }
}

/// <summary>
/// The previous state, to write only the differences: for the data «percorso = valore» for a changed or added value (compact JSON),
/// «percorso rimosso» for a removed one (the first state writes the whole data); a part of a sub-step equal to the previous one is
/// written as «[parte] invariata».
/// </summary>
internal sealed class ConcreteDataMemory
{
    private JsonNode? previous;
    private readonly Dictionary<string, List<string>> parts = new(StringComparer.Ordinal);

    /// <summary>Whether the part <paramref name="tag"/> has the same lines as after the previous step (and remembers them).</summary>
    internal bool Unchanged(string tag, List<string> lines)
    {
        bool same = parts.TryGetValue(tag, out var before) && before.SequenceEqual(lines, StringComparer.Ordinal);
        parts[tag] = lines;
        return same;
    }

    internal IEnumerable<string> Changes(JsonNode current)
    {
        current = Shown(current);
        var lines = new List<string>();
        if (previous is null) lines.Add("tutto = " + ConcreteSectionTrace.Compact(current));
        else Compare("", previous, current, lines);
        previous = current.DeepClone();
        return lines;
    }

    static void Compare(string path, JsonNode? before, JsonNode? after, List<string> lines)
    {
        if (before is JsonObject a && after is JsonObject b)
        {
            foreach (var (key, value) in b) if (!a.ContainsKey(key)) lines.Add(Join(path, key) + " = " + ConcreteSectionTrace.Compact(value)); else Compare(Join(path, key), a[key], value, lines);
            foreach (var (key, _) in a) if (!b.ContainsKey(key)) lines.Add(Join(path, key) + " rimosso");
            return;
        }
        if (before is JsonArray x && after is JsonArray y && x.Count == y.Count)
        {
            for (int i = 0; i < x.Count; i++) Compare(path + "[" + i.ToString(CultureInfo.InvariantCulture) + "]", x[i], y[i], lines);
            return;
        }
        if (ConcreteSectionTrace.Compact(before) != ConcreteSectionTrace.Compact(after)) lines.Add((path.Length == 0 ? "tutto" : path) + " = " + ConcreteSectionTrace.Compact(after));
    }

    static string Join(string path, string key) => path.Length == 0 ? key : path + "." + key;

    /// <summary>
    /// The data as the lines of the changes write them. The native section of a document of version 3 (<c>input.sezione_gpc</c>, F5.10 S1b)
    /// keeps its archive, the XML of ModelArchive of hundreds of kB, which is written as its size and hash; the hash of the data in the line
    /// «dati» covers every byte. The data of the frozen scenarios of S1 have no native section: their lines do not change.
    /// </summary>
    static JsonNode Shown(JsonNode data)
    {
        if (data is not JsonObject root || root["input"] is not JsonObject input || input["sezione_gpc"] is not JsonObject native
            || native["archivio"] is not JsonValue archive || !archive.TryGetValue(out string? text)) return data;
        var copy = root.DeepClone();
        copy["input"]!["sezione_gpc"]!["archivio"] = "‹archivio GPC · byte " + Encoding.UTF8.GetByteCount(text).ToString(CultureInfo.InvariantCulture)
            + " · " + ConcreteSectionTrace.HashText(text) + "›";
        return copy;
    }
}

/// <summary>
/// The identifiers created by the sheet (rows of the actions, materials): the GUIDs of the fixture stay, the new ones become «&lt;nuovo-n&gt;» in
/// the order in which the trace meets them, so that the traces of two runs are equal.
/// </summary>
internal sealed class ConcreteGuids
{
    private static readonly Regex Guid = new(@"\b[0-9a-fA-F]{8}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{12}\b", RegexOptions.Compiled);
    private readonly HashSet<string> known;
    private readonly Dictionary<string, string> created = new(StringComparer.Ordinal);

    internal ConcreteGuids(JsonNode? input) =>
        known = Guid.Matches(ConcreteSectionTrace.Compact(input)).Select(m => Key(m.Value)).ToHashSet(StringComparer.Ordinal);

    private static string Key(string guid) => guid.Replace("-", "", StringComparison.Ordinal).ToLowerInvariant();

    internal string Normalize(string line) => Guid.Replace(line, m =>
    {
        string key = Key(m.Value);
        if (known.Contains(key)) return m.Value;
        if (!created.TryGetValue(key, out var name)) created[key] = name = "<nuovo-" + (created.Count + 1).ToString(CultureInfo.InvariantCulture) + ">";
        return name;
    });
}

/// <summary>Formats of the trace, shared by the two sides (those of the pile group traces), and the masks of the measured times.</summary>
internal static class ConcreteSectionTrace
{
    private static readonly Regex Seconds = new(@"\d+(?:[.,]\d+)?\s?s\b", RegexOptions.Compiled);

    internal static string Compact(JsonNode? node) => PileGroupTrace.Compact(node);

    /// <summary>
    /// Compact JSON with the names of every object in ordinal order. For what the sheet only shows or exports and whose order follows the
    /// order in which the parallel checks end: the outputs of the rows of the tables (η and outcome of the 3D and 2D domains) and the
    /// exported result. The data keep their order in memory.
    /// </summary>
    internal static string Canonical(JsonNode? node) => Compact(Sorted(node));

    private static JsonNode? Sorted(JsonNode? node) => node switch
    {
        JsonObject o => new JsonObject(o.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => KeyValuePair.Create(p.Key, Sorted(p.Value)))),
        JsonArray a => new JsonArray(a.Select(Sorted).ToArray()),
        null => null,
        _ => node.DeepClone()
    };

    internal static string HashText(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];

    private static readonly Regex AnyGuid = new(@"\b[0-9a-fA-F]{8}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{12}\b", RegexOptions.Compiled);

    /// <summary>Hash of a JSON whose identifiers are replaced by their order of appearance: equal for two runs that create new identifiers.</summary>
    internal static string StableHash(JsonNode? node)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        return HashText(AnyGuid.Replace(Compact(node), m => names.TryGetValue(m.Value, out var name) ? name : names[m.Value] = "<id-" + names.Count.ToString(CultureInfo.InvariantCulture) + ">"));
    }

    private static readonly JsonSerializerOptions Values = new(PileGroupTrace.Json) { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals };

    /// <summary>Hash of texts, numbers or arrays, serialized as JSON with the invariant culture (infinite and undefined numbers as names).</summary>
    internal static string Hash(object value) => HashText(JsonSerializer.Serialize(value, Values));

    internal static string Quote(string? text) => PileGroupTrace.Quote(text);

    internal static string YesNo(bool value) => PileGroupTrace.YesNo(value);

    internal static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>A text with the measured times («1,25 s») replaced by «‹t› s»: the time of a search is not part of the trace.</summary>
    internal static string WithoutTimes(string text) => Seconds.Replace(text, "‹t› s");

    internal static void Write(string path, IEnumerable<string> lines)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        PileGroupTrace.Write(path, lines);
    }

    internal static string[] Read(string path) => PileGroupTrace.Read(path);

    internal static string? FirstDifference(IReadOnlyList<string> expected, IReadOnlyList<string> actual) => PileGroupTrace.FirstDifference(expected, actual);

    // ---------------------------------------------------------------- the window of the wizard (F5.10 S1b)

    /// <summary>The command of the control panel that opens the wizard, and the title of its window.</summary>
    internal const string WizardCommand = "Wizard tipologie / modifica disposizione…", WizardTitle = "Wizard · Sezione in cemento armato";

    /// <summary>A section of the engine as its plain data: shape, size, areas, outline, holes and bars.</summary>
    internal static string Section(ConcreteGeometry? section) => section is null ? "-" :
        section.Shape + " " + Number(section.Width) + "x" + Number(section.Height)
        + " · Ac " + Number(section.AreaCls) + " · As " + Number(section.AreaSteel)
        + " · contorno " + section.Outline.Count.ToString(CultureInfo.InvariantCulture) + " " + Hash(section.Outline)
        + " · fori " + section.Holes.Count.ToString(CultureInfo.InvariantCulture)
        + " · barre " + section.Bars.Count.ToString(CultureInfo.InvariantCulture) + " " + Hash(section.Bars.Select(b => new[] { b.X, b.Y, b.Diametro }).ToArray());

    /// <summary>
    /// What the window of the wizard shows, from its ViewModel (the window is a host of ConcreteTemplateWizardViewModel since 20/10, an MVVM
    /// island as the editor of the actions): type, «Applica sezione» available, error, note, every field (value, hidden, disabled), the
    /// preview (section and paths of the links) and the hash of the draft.
    /// </summary>
    internal static string WizardState(ConcreteTemplateWizardViewModel wizard) =>
        "tipologia " + Quote(wizard.Form.Get("wizard_type")) + " · applica " + YesNo(wizard.CanApply) + " · errore " + Quote(wizard.Error) + " · nota " + Quote(wizard.Note)
        + "\n  campi " + string.Join(" ", wizard.Form.Fields.Select(f => f.Key + "=" + Quote(wizard.Form.Get(f.Key)) + (f.IsVisible ? "" : "·nascosto") + (f.IsEnabled ? "" : "·spento")))
        + "\n  anteprima " + Section(wizard.Geometry) + " · staffe " + (wizard.Geometry is { } g ? g.TemplateLinks.Count.ToString(CultureInfo.InvariantCulture) + " " + Hash(g.TemplateLinks) : "-")
        + "\n  bozza " + StableHash(wizard.Draft);

    /// <summary>
    /// A field or a choice of the window of the wizard, as its FieldForm writes it: a hidden or disabled field does nothing. The buttons are
    /// pressed by each side (the window, or the commands of the ViewModel). False for a step that is not a field.
    /// </summary>
    internal static bool WizardStep(ConcreteTemplateWizardViewModel wizard, ConcreteSectionStep step)
    {
        if (step.Kind is not (ConcreteSectionStep.Field or ConcreteSectionStep.Choice)) return false;
        if (!wizard.Form.Contains(step.Key!) || wizard.Form[step.Key!] is not { IsVisible: true, IsEnabled: true } field) return true;
        if (step.Kind == ConcreteSectionStep.Field && field.Kind == FieldKind.Text && !field.IsInputReadOnly) field.EditText = step.Text!;
        else if (step.Kind == ConcreteSectionStep.Choice && field.Kind == FieldKind.Choice && field.CanChange) field.SelectedChoice = step.Text;
        return true;
    }
}
