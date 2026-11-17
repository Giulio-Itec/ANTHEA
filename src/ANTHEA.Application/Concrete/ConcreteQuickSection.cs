using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Anthea.Application.Materials;
using Anthea.Application.Modules;

namespace Anthea.Application.Concrete;

/// <summary>A value shown by the quick RC section: label, value already formatted, unit.</summary>
public sealed record ConcreteQuickValue(string Label, string Value, string Unit = "");

/// <summary>
/// The outcome of a quick check of an RC section (module <see cref="ConcreteQuickSection.Module"/>): what the engine of the RC section returned,
/// unchanged. No value is computed here.
/// </summary>
public sealed record ConcreteQuickResult(
    string Method,
    bool Straight,
    ConcretePreview Preview,
    ConcreteResults.Action? UltimateAction,
    ConcreteResults.Check? UltimateCheck,
    ConcreteResults.StressState? UltimateState,
    IReadOnlyList<ConcreteResults.PrincipalResistance> Resistances,
    ConcreteResults.Action? ServiceAction,
    ConcreteResults.StressState? ServiceState,
    double? ServiceRatio,
    string ServiceStatus,
    DomainChartGeometry DomainGeometry,
    DomainChartActions DomainActions,
    ConcreteResults.CurvatureResult? Curvature,
    IReadOnlyList<string> Errors,
    string Model,
    double? YieldStrain,
    IReadOnlyList<ConcreteQuickCombination> Combinations,
    string? SelectedId);

/// <summary>
/// The outcome of a combination of the quick section: η of the ultimate check (or the ratio of the stresses with the method n) and the status of
/// the engine; <see cref="Passed"/> true when the ratio is within 1, null without a ratio.
/// </summary>
public sealed record ConcreteQuickCombination(string Id, string Name, double? Ratio, string Status, bool? Passed);

/// <summary>
/// The main outcome of the quick RC section, shown in evidence: the design resisting moment (or the stresses of the method n), the ratio with the
/// outcome, and the behaviour at the resistant point (ductile when the tension steel yields, brittle otherwise; null for the method n).
/// </summary>
public sealed record ConcreteQuickSummary(string Title, string Value, string Detail, string Behaviour, bool? Ductile, double? Ratio, bool? Passed);

/// <summary>
/// The quick RC section (17/11/2026): a simplified interface on the engine of the RC section (<c>str_palo</c>). The document has the format of the
/// RC section: the same <c>input</c>, <c>combinazioni</c> and <c>workspace_ca</c>, prepared and validated by the same code, plus the choices of the
/// simplified view in <see cref="ViewKey"/>. The ultimate action is the first row of the SLU actions, the action of the method n the first row of the
/// SLE (rare) actions. The checks are the calls of the RC section, unchanged: resistances at constant N, 2D domain with its check, stresses of the
/// serviceability limit state, moment–curvature. Nothing is computed here: the values shown come from the engine.
/// </summary>
public static class ConcreteQuickSection
{
    /// <summary>The identifier of the module.</summary>
    public const string Module = "str_sezione_ca_rapida";

    /// <summary>The module whose engine and format the quick section uses.</summary>
    public const string SourceModule = "str_palo";

    /// <summary>The choices of the simplified view in the document: title, method, bending, chart.</summary>
    public const string ViewKey = "verifica_rapida";

    /// <summary>The quick section has no report of its own in this version.</summary>
    public const string NoReport = "La verifica rapida della sezione in c.a. non ha ancora una relazione: usare il modulo «Sezione in c.a.» per la relazione completa.";

    /// <summary>The names of the two actions of the sheet.</summary>
    public const string UltimateName = "Sollecitazione SLU", ServiceName = "Metodo n";

    public static ImmutableArray<string> Shapes { get; } = ["Rettangolare", "A T", "Circolare"];
    public static ImmutableArray<string> Methods { get; } = ["SLU+", "SLU−", "Metodo n"];
    public static ImmutableArray<string> Bendings { get; } = ["Retta", "Deviata"];
    public static ImmutableArray<string> Charts { get; } = ["Dominio", "Momento–curvatura"];

    /// <summary>The names on screen of <see cref="Methods"/>, in the same order.</summary>
    public static ImmutableArray<string> MethodLabels { get; } = ["Momento positivo", "Momento negativo", "Metodo n"];

    /// <summary>
    /// The checks on screen with the biaxial bending: the sign of the moment comes from Mx and My of each combination, so the ultimate check is one
    /// («SLU», the codes SLU+ and SLU− give the same check), then the method n.
    /// </summary>
    public static ImmutableArray<string> BiaxialMethodLabels { get; } = ["SLU", "Metodo n"];

    /// <summary>The checks on screen for the bending: <see cref="MethodLabels"/> with the straight one, <see cref="BiaxialMethodLabels"/> with the biaxial one.</summary>
    public static ImmutableArray<string> MethodChoices(bool straight) => straight ? MethodLabels : BiaxialMethodLabels;

    /// <summary>The label on screen of a method for the bending: with the biaxial one SLU+ and SLU− are «SLU».</summary>
    public static string MethodLabel(string method, bool straight) => !straight && method != Methods[2] && Methods.Contains(method) ? BiaxialMethodLabels[0] : MethodLabel(method);

    /// <summary>
    /// The code of a method chosen on screen: «SLU» (biaxial bending) keeps SLU+ or SLU− when one of them is the current code, otherwise SLU+.
    /// </summary>
    public static string? MethodCode(string? label, string current) => label == BiaxialMethodLabels[0]
        ? (current == Methods[2] ? Methods[0] : current)
        : Methods.ElementAtOrDefault(MethodLabels.IndexOf(label ?? ""));

    /// <summary>The resistance models of the RC section for the ultimate check (<c>workspace_ca.modello_resistente.SLU</c>).</summary>
    public static ImmutableArray<string> Models { get; } = ["Plastico", "Elastico"];

    /// <summary>The names on screen of <see cref="Models"/>, in the same order.</summary>
    public static ImmutableArray<string> ModelLabels { get; } = ["Plastica", "Elastica"];

    /// <summary>
    /// Where a layer of bars stands: under the top cover, over the bottom cover, at a distance from the top edge, at a distance from the bottom edge.
    /// </summary>
    public static ImmutableArray<string> LayerPositions { get; } = ["Superiore", "Inferiore", "Da sopra", "Da sotto"];

    /// <summary>The position of a layer, with the names of the first version («Sopra», «Sotto»: 17/11/2026) read as «Superiore» and «Inferiore».</summary>
    public static string LayerPosition(string text) => text switch { "Sopra" => LayerPositions[0], "Sotto" => LayerPositions[1], _ => text };

    /// <summary>The key of the layers of bars in the choices of the view.</summary>
    public const string LayersKey = "strati";

    /// <summary>The explanation of the cover, for its «?».</summary>
    public const string CoverHelpTitle = "Copriferro c";
    public const string CoverHelp = "c è il copriferro netto della staffa: la distanza dal bordo della sezione alla faccia esterna della staffa.\n\n"
        + "Gli strati «Superiore» e «Inferiore» hanno l'asse delle barre a c + Ø staffa + Ø barra / 2 dal bordo; gli strati «Da sopra» e «Da sotto» hanno "
        + "l'asse alla distanza d indicata, misurata dal bordo superiore o da quello inferiore. In larghezza le barre di uno strato sono distribuite in modo "
        + "uniforme fra le facce interne delle staffe.\n\n"
        + "Nella sezione circolare l'anello delle barre ha l'asse a c + Ø staffa + Ø barra / 2 dal bordo.";


    /// <summary>The components of an action that the sheet edits.</summary>
    public static ImmutableArray<string> ActionComponents { get; } = ["N", "Mx", "My"];

    /// <summary>The metadata of the module for the navigation.</summary>
    public static ModuleInfo Info { get; } = new(Module, "Strutture", "Sezione in c.a. · verifica rapida",
        "SLU, metodo n, dominio M–N e M–χ con il motore della sezione in c.a.", "Sezione c.a. rapida", "Sezione in c.a. · verifica rapida", "Verifica rapida della sezione");

    /// <summary>
    /// A new sheet: the default document of the RC section (its catalogue, prepared with unified actions), the second ring off, the ultimate action
    /// and the action of the method n at zero, two layers of bars (top and bottom, as the default bars of the RC section), the plastic model, the
    /// choices of the view.
    /// </summary>
    public static JsonObject Create()
    {
        var data = ModuleDirectory.CreateData(SourceModule);
        var input = data["input"]!.AsObject();
        input["second_inner_enabled"] = false; input["second_inner_count"] = "8"; input["second_inner_diameter"] = "20"; input["second_inner_gap"] = "25";
        SectionActionInputs.Replace(data, [Row("SLU", "Combo 1")]);
        CopyToService(data);
        WriteModel(data, Models[0]);
        data[ViewKey] = new JsonObject
        {
            ["titolo"] = "", ["metodo"] = Methods[0], ["flessione"] = Bendings[0], ["grafico"] = Charts[0],
            [LayersKey] = new JsonArray(Layer(LayerPositions[0], input.S("top_bar_count"), input.S("top_bar_diameter_mm")),
                Layer(LayerPositions[1], input.S("bottom_bar_count"), input.S("bottom_bar_diameter_mm")))
        };
        View(data)[SelectedKey] = Combinations(data)[0].S("id");
        ApplyLayers(data);
        return data;
    }

    private static JsonObject Row(string state, string name) => new()
    {
        ["stato"] = state, ["nome"] = name, ["elemento"] = "", ["N"] = "0", ["Mx"] = "0", ["My"] = "0", ["Vx"] = "0", ["Vy"] = "0", ["T"] = "0"
    };

    private static JsonObject Layer(string position, string count, string diameter, string distance = "") => new()
    {
        ["posizione"] = position, ["d"] = distance, ["n"] = count, ["phi"] = diameter
    };

    /// <summary>The checks of the RC section, then the choices of the view.</summary>
    public static void Validate(JsonObject data)
    {
        ArgumentNullException.ThrowIfNull(data);
        ModuleCatalog.ValidateData(SourceModule, data);
        if (data[ViewKey] is not JsonObject view) throw new ArgumentException("Scelte della verifica rapida mancanti.");
        void Choice(string key, ImmutableArray<string> choices)
        {
            if (!choices.Contains(view.S(key))) throw new ArgumentException("Scelta della verifica rapida non valida: " + key + ".");
        }
        Choice("metodo", Methods); Choice("flessione", Bendings); Choice("grafico", Charts);
        if (view["titolo"] is not null && (view["titolo"] is not JsonValue title || !title.TryGetValue<string>(out _)))
            throw new ArgumentException("Titolo della verifica rapida non valido.");
        if (view[LayersKey] is { } layers && (layers is not JsonArray list || list.Any(l => l is not JsonObject layer || !LayerPositions.Contains(LayerPosition(layer.S("posizione"))))))
            throw new ArgumentException("Strati di armatura della verifica rapida non validi.");
    }

    /// <summary>The choices of the view of <paramref name="data"/>.</summary>
    public static JsonObject View(JsonObject data) => data[ViewKey] as JsonObject ?? throw new ArgumentException("Scelte della verifica rapida mancanti.");

    // ---------------------------------------------------------------- actions

    /// <summary>The first action of <paramref name="state"/> (SLU or SLE) as the RC section reads it, or null.</summary>
    public static JsonObject? Action(JsonObject data, string state) => ConcreteSheetPreparation.ReadActions(data)[state].FirstOrDefault();

    // ---------------------------------------------------------------- combinations

    /// <summary>The key of the selected combination in the choices of the view.</summary>
    public const string SelectedKey = "combinazione";

    /// <summary>
    /// The combinations of the sheet (17/11/2026): one table for every method, the SLU rows of the RC section (id, nome, N, Mx, My as the RC section
    /// reads them). The SLE rows are their copy (<see cref="CopyToService"/>), so that the document says the same to the RC section.
    /// </summary>
    public static IReadOnlyList<JsonObject> Combinations(JsonObject data) => ConcreteSheetPreparation.ReadActions(data)["SLU"];

    /// <summary>The selected combination: the saved one when it exists, otherwise the first; null without combinations.</summary>
    public static string? SelectedCombination(JsonObject data)
    {
        var rows = Combinations(data);
        string id = data[ViewKey].S(SelectedKey);
        return rows.FirstOrDefault(r => r.S("id") == id)?.S("id") ?? rows.FirstOrDefault()?.S("id");
    }

    /// <summary>Selects the combination <paramref name="id"/>.</summary>
    public static void SelectCombination(JsonObject data, string id) => View(data)[SelectedKey] = id;

    private static JsonArray Stored(JsonObject data)
    {
        if (data["combinazioni"] is not JsonObject sets) data["combinazioni"] = sets = new JsonObject();
        if (sets["SLU"] is not JsonArray rows) sets["SLU"] = rows = new JsonArray();
        return rows;
    }

    /// <summary>A new combination at zero, «Combo k» with the first free k, selected; returns its identifier.</summary>
    public static string AddCombination(JsonObject data)
    {
        var rows = Stored(data);
        var names = rows.Select(r => r.S("nome")).ToHashSet();
        int k = rows.Count + 1;
        while (names.Contains("Combo " + k)) k++;
        string id = Guid.NewGuid().ToString("N");
        rows.Add(new JsonObject
        {
            ["id"] = id, ["nome"] = "Combo " + k,
            ["azioni"] = new JsonArray(SectionActionInputs.Components.Select(_ => (JsonNode?)JsonValue.Create("0")).ToArray()), ["visible"] = true, ["elemento"] = ""
        });
        CopyToService(data);
        SelectCombination(data, id);
        return id;
    }

    /// <summary>Removes the combination <paramref name="id"/>; the last one stays. The selection moves to the first combination when it was removed.</summary>
    public static void RemoveCombination(JsonObject data, string id)
    {
        var rows = Stored(data);
        if (rows.Count <= 1) return;
        var row = rows.FirstOrDefault(r => r.S("id") == id);
        if (row is null) return;
        rows.Remove(row);
        CopyToService(data);
        if (data[ViewKey].S(SelectedKey) == id) SelectCombination(data, rows[0].S("id"));
    }

    /// <summary>Writes the name or a component (N, Mx, My) of the combination <paramref name="id"/>.</summary>
    public static void WriteCombination(JsonObject data, string id, string key, string value)
    {
        var row = Stored(data).OfType<JsonObject>().FirstOrDefault(r => r.S("id") == id) ?? throw new ArgumentException("Combinazione inesistente.");
        if (key == "nome") row["nome"] = value;
        else
        {
            int index = SectionActionInputs.Components.IndexOf(key);
            if (index < 0) throw new ArgumentException("Componente della sollecitazione non valida: " + key);
            if (row["azioni"] is not JsonArray components) row["azioni"] = components = new JsonArray();
            while (components.Count < SectionActionInputs.Components.Length) components.Add(JsonValue.Create("0"));
            components[index] = value;
        }
        CopyToService(data);
    }

    /// <summary>The SLE rows as a copy of the combinations (identifiers with «-n», distinct from those of the SLU rows).</summary>
    public static void CopyToService(JsonObject data)
    {
        var copy = new JsonArray(Stored(data).Select(r =>
        {
            var row = (JsonObject)r!.DeepClone();
            row["id"] = row.S("id") + "-n";
            return (JsonNode?)row;
        }).ToArray());
        data["combinazioni"]!["SLE"] = copy;
    }

    /// <summary>Writes a component of the first action of <paramref name="state"/>; the action is created when missing.</summary>
    public static void WriteAction(JsonObject data, string state, string component, string value)
    {
        int index = SectionActionInputs.Components.IndexOf(component);
        if (index < 0) throw new ArgumentException("Componente della sollecitazione non valida: " + component);
        if (data["combinazioni"] is not JsonObject sets) data["combinazioni"] = sets = new JsonObject();
        if (sets[state] is not JsonArray rows) sets[state] = rows = new JsonArray();
        if (rows.FirstOrDefault() is not JsonObject row)
        {
            rows.Add(row = new JsonObject
            {
                ["id"] = Guid.NewGuid().ToString("N"), ["nome"] = state == "SLU" ? UltimateName : ServiceName,
                ["azioni"] = new JsonArray(SectionActionInputs.Components.Select(_ => (JsonNode?)JsonValue.Create("0")).ToArray()), ["visible"] = true, ["elemento"] = ""
            });
        }
        if (row["azioni"] is not JsonArray components) row["azioni"] = components = new JsonArray();
        while (components.Count < SectionActionInputs.Components.Length) components.Add(JsonValue.Create("0"));
        components[index] = value;
    }

    // ---------------------------------------------------------------- resistance model

    /// <summary>The resistance model of the ultimate check: <c>workspace_ca.modello_resistente.SLU</c>, plastic by default as in the RC section.</summary>
    public static string Model(JsonObject data) => data["workspace_ca"]?["modello_resistente"] is JsonObject models && Models.Contains(models.S("SLU")) ? models.S("SLU") : Models[0];

    /// <summary>Writes the resistance model of the ultimate check, where the RC section reads it.</summary>
    public static void WriteModel(JsonObject data, string model)
    {
        if (!Models.Contains(model)) throw new ArgumentException("Modello resistente non valido: " + model);
        if (data["workspace_ca"] is not JsonObject settings) data["workspace_ca"] = settings = new JsonObject();
        if (settings["modello_resistente"] is not JsonObject models) settings["modello_resistente"] = models = new JsonObject { ["SLV"] = "Elastico" };
        models["SLU"] = model;
    }

    // ---------------------------------------------------------------- layers of bars

    /// <summary>The layers of bars of the sheet (posizione, d, n, phi as texts); created empty when missing.</summary>
    public static JsonArray Layers(JsonObject data)
    {
        var view = View(data);
        if (view[LayersKey] is not JsonArray layers) view[LayersKey] = layers = new JsonArray();
        return layers;
    }

    /// <summary>Adds a layer at the bottom of the list: at a distance from the top, with the diameter of the last layer.</summary>
    public static void AddLayer(JsonObject data)
    {
        var layers = Layers(data);
        string diameter = layers.LastOrDefault()?.S("phi") is { Length: > 0 } last ? last : "16";
        layers.Add(Layer(LayerPositions[2], "2", diameter, ""));
    }

    /// <summary>Removes the layer at <paramref name="index"/>.</summary>
    public static void RemoveLayer(JsonObject data, int index)
    {
        var layers = Layers(data);
        if (index >= 0 && index < layers.Count) layers.RemoveAt(index);
    }

    /// <summary>Writes a field (posizione, d, n, phi) of the layer at <paramref name="index"/>.</summary>
    public static void WriteLayer(JsonObject data, int index, string key, string value)
    {
        if (key is not ("posizione" or "d" or "n" or "phi")) throw new ArgumentException("Campo dello strato non valido: " + key);
        if (key == "posizione" && !LayerPositions.Contains(value)) throw new ArgumentException("Posizione dello strato non valida: " + value);
        var layers = Layers(data);
        if (index < 0 || index >= layers.Count || layers[index] is not JsonObject layer) throw new ArgumentException("Strato inesistente.");
        layer[key] = value;
    }

    /// <summary>
    /// The bars of the layers written where the RC section reads the bars placed by hand (<c>input.barre_manuali</c>: x, y, Ø in the coordinates of
    /// its outline); for the circular section the ring of the RC section, without bars by hand. Each layer is a row of bars spread evenly between the
    /// inner faces of the links: «Superiore» and «Inferiore» with the axis at c + Ø of the link + Ø / 2 from the edge, «Da sopra» and «Da sotto» at
    /// the distance d from the top or bottom edge; in the section with a flange the width is that of the flange or of the web at the height of the layer. A geometric arrangement of
    /// the data: no material law or check. Invalid layers leave no bars, so that the engine refuses the section, and the reason is returned.
    /// </summary>
    public static string? ApplyLayers(JsonObject data)
    {
        var input = data["input"]!.AsObject();
        if (input.S("shape") == Shapes[2]) { input.Remove("barre_manuali"); return null; }
        try { input["barre_manuali"] = LayerBars(input, Layers(data)); return null; }
        catch (ArgumentException failure) { input["barre_manuali"] = new JsonArray(); return failure.Message; }
    }

    private static JsonArray LayerBars(JsonObject input, JsonArray layers)
    {
        if (layers.Count == 0) throw new ArgumentException("Aggiungere almeno uno strato di barre.");
        double Positive(string text, string label)
        {
            double value = SectionWorkspace.Number(text, label);
            if (value <= 0) throw new ArgumentException(label + ": inserire un numero maggiore di zero.");
            return value;
        }
        bool tee = input.S("shape") == Shapes[1];
        double height = Positive(input.S("height_mm"), "h"), cover = SectionWorkspace.Number(input.S("cover_mm"), "Copriferro c");
        double link = input.S("staffe_presenti", "Sì") == "No" ? 0 : SectionWorkspace.Number(input.S("transverse_bar_diameter_mm"), "Ø staffe");
        double top = tee ? Positive(input.S("flange_width_mm"), "B ala") : Positive(input.S("width_mm"), "b");
        double web = tee ? Positive(input.S("web_width_mm"), "b anima") : top, flange = tee ? Positive(input.S("flange_thickness_mm"), "s ala") : height;
        if (cover < 0 || link < 0) throw new ArgumentException("Copriferro e Ø staffe non possono essere negativi.");
        // The edges in the coordinates of the outline of the RC section (with a flange the origin is the centroid): the outline of the engine.
        var probe = (JsonObject)input.DeepClone();
        probe["barre_manuali"] = new JsonArray(new JsonObject { ["x"] = 0, ["y"] = 0, ["phi"] = 1 });
        var outline = ConcretePreview.PrepareGeometry(probe).Outline;
        double upper = outline.Max(p => p[1]);
        var bars = new JsonArray();
        for (int i = 0; i < layers.Count; i++)
        {
            var layer = layers[i]!.AsObject();
            string name = "Strato " + (i + 1);
            double count = Positive(layer.S("n"), name + " · n"), diameter = Positive(layer.S("phi"), name + " · Ø");
            if (count != (int)count) throw new ArgumentException(name + " · n: inserire un numero intero.");
            double edge = cover + link + diameter / 2;
            string position = LayerPosition(layer.S("posizione"));
            double depth = position == LayerPositions[0] ? edge : position == LayerPositions[1] ? height - edge
                : position == LayerPositions[3] ? height - Positive(layer.S("d"), name + " · d") : Positive(layer.S("d"), name + " · d");
            if (depth < edge || depth > height - edge) throw new ArgumentException(name + ": la distanza d deve lasciare copriferro e staffe dal bordo.");
            double width = depth <= flange ? top : web, half = width / 2 - edge;
            if (half < 0 || count > 1 && half <= 0) throw new ArgumentException(name + ": le barre non entrano nella larghezza della sezione.");
            double y = upper - depth;
            for (int k = 0; k < (int)count; k++)
            {
                double x = count == 1 ? 0 : -half + 2 * half * k / (count - 1);
                bars.Add(new JsonObject { ["x"] = x, ["y"] = y, ["phi"] = diameter });
            }
        }
        return bars;
    }

    // ---------------------------------------------------------------- materials

    private static string Standard(JsonObject data) => data["workspace_ca"].S("normativa");

    private static JsonObject[] Catalog(JsonObject data, string key) => key == "classe_cls"
        ? MaterialInputCatalog.Concrete(Standard(data)) : MaterialInputCatalog.Steel(false, Standard(data));

    /// <summary>The classes of the concrete (<c>classe_cls</c>) or of the steel (<c>classe_acciaio</c>) of the standard of the sheet.</summary>
    public static string[] MaterialChoices(JsonObject data, string key) =>
        ConcreteInputPresentation.MaterialChoices(Catalog(data, key), data["input"].S(key));

    /// <summary>
    /// The class of <paramref name="key"/> chosen in the input: writes the values of the material of the catalogue, as the RC section does; nothing
    /// for «Personalizzato».
    /// </summary>
    public static void ApplyMaterial(JsonObject data, string key)
    {
        var input = data["input"]!.AsObject();
        var selected = Catalog(data, key).FirstOrDefault(m => m.S("nome") == input.S(key));
        if (selected is null) return;
        foreach (var (name, value) in ConcreteInputPresentation.StandardMaterialValues(selected)) input[name] = value?.DeepClone();
    }

    // ---------------------------------------------------------------- geometry

    /// <summary>The geometry of the section for the drawing, or the reason why it cannot be drawn.</summary>
    public static (ConcreteGeometry? Geometry, string Message) Geometry(JsonObject data)
    {
        try
        {
            var copy = (JsonObject)data.DeepClone();
            if (ApplyLayers(copy) is { } error) return (null, error);
            return (ConcretePreview.PrepareGeometry(copy["input"]!.AsObject()), "");
        }
        catch (ArgumentException failure) { return (null, failure.Message); }
    }

    // ---------------------------------------------------------------- calculation

    /// <summary>
    /// The checks of the sheet with the engine of the RC section, on a prepared copy of the data:
    /// <list type="bullet">
    /// <item>SLU+ and SLU−: the resistances at the N of the ultimate action (as «Resistenze rapide»), the check of the action on the section Mx–My
    /// of the domain at that N (the check at constant N of the 2D panel of the RC section) with the state at the resistant point;</item>
    /// <item>the chart of the domain: the section N–M at θ = 0 for the straight bending, the section Mx–My for the biaxial one;</item>
    /// <item>method n: the stresses of the rare serviceability limit state of the RC section for the action of the method n;</item>
    /// <item>the moment–curvature at the N of the ultimate action, about x, when the chart asks for it.</item>
    /// </list>
    /// With the straight bending the moments My are ignored. A failed step is listed in <see cref="ConcreteQuickResult.Errors"/>; invalid data of the
    /// section stop the calculation.
    /// </summary>
    public static ConcreteQuickResult Calculate(JsonObject source, CancellationToken token = default)
    {
        Validate(source);
        var data = (JsonObject)source.DeepClone();
        if (ApplyLayers(data) is { } layerError) throw new ArgumentException(layerError);
        string model = Model(data);
        var settings = SectionWorkspace.Prepare(data);
        // The method n of the quick section is at short term (n = Es / Ecm): no creep, whatever φ the document has from the RC section (on this copy).
        foreach (string set in SectionWorkspace.Sets.Skip(2)) if (settings["sle"]?[set] is JsonObject sle) sle["phi"] = "0";
        var input = data["input"]!.AsObject();
        var view = View(data);
        string method = view.S("metodo");
        bool straight = view.S("flessione") == Bendings[0], ultimate = method != Methods[2], curvatureChart = view.S("grafico") == Charts[1];
        bool elastic = model == Models[1];
        var preview = ConcretePreview.Read(input, settings);
        double? yieldStrain = null;
        try { yieldStrain = RebarMaterial.Evaluate(input).EpsilonYd; } catch (ArgumentException) { }
        token.ThrowIfCancellationRequested();
        var errors = new List<string>();
        var session = new ConcreteAnalysisSession();

        // The combinations (17/11/2026): one table for every method. With the straight bending the moments My are ignored.
        var rows = ConcreteSheetPreparation.ReadActions(data)["SLU"].Select((row, i) =>
        {
            if (straight) row["My"] = "0";
            if (row.S("nome").Length == 0) row["nome"] = "Combo " + (i + 1);
            return row;
        }).ToArray();
        if (rows.Length == 0) errors.Add("Nessuna combinazione: aggiungerne una.");
        string? selectedId = SelectedCombination(data);
        var selectedRow = rows.FirstOrDefault(r => r.S("id") == selectedId) ?? rows.FirstOrDefault();
        selectedId = selectedRow?.S("id");
        var forces = new Dictionary<string, ConcreteResults.Action>();
        var statuses = new Dictionary<string, string>();
        foreach (var row in rows)
        {
            try { forces[row.S("id")] = ConcreteDomainChart.ReadNamedAction(row); }
            catch (ArgumentException failure) { statuses[row.S("id")] = failure.Message; errors.Add(row.S("nome") + ": " + failure.Message); }
        }
        // A null action has no line of action: no check (the engine has no resistance to give), only the resistances and the domain.
        static bool Null(ConcreteResults.Action a) => a.N == 0 && a.Mx == 0 && a.My == 0;
        var valid = rows.Where(r => forces.ContainsKey(r.S("id"))).ToArray();
        var active = valid.Where(r => !Null(forces[r.S("id")])).ToArray();
        foreach (var row in valid.Except(active)) statuses[row.S("id")] = "Azione nulla";
        ConcreteResults.Action? selectedForce = selectedId is not null && forces.TryGetValue(selectedId, out var chosen) ? chosen : null;

        ConcreteResults.Check? check = null; ConcreteResults.StressState? limit = null;
        IReadOnlyDictionary<string, ConcreteResults.Check>? checks = null;
        IReadOnlyList<ConcreteResults.PrincipalResistance> resistances = [];
        ConcreteResults.CurvatureResult? curvature = null;
        // The checks of all the combinations: the 3D domain of the RC section at constant N (as its 3D panel); for each combination the same η as
        // the section Mx–My of the domain at its N.
        if (ultimate && active.Length > 0)
        {
            var options3D = (JsonObject)settings["dominio3d"]!.DeepClone();
            options3D["stato"] = "SLU"; options3D["criterio"] = "N costante";
            try
            {
                checks = session.Domain(input, settings, options3D, "SLU", true, active, token).Checks;
                foreach (var row in active)
                    if (checks.GetValueOrDefault(row.S("id")) is { Utilization: null } failed) errors.Add(row.S("nome") + ": " + failed.Status);
                check = selectedId is null ? null : checks.GetValueOrDefault(selectedId);
                limit = check?.LimitState?.Value;
            }
            catch (Exception failure) when (failure is not OperationCanceledException) { errors.Add("Verifica SLU: " + failure.Message); }
        }
        token.ThrowIfCancellationRequested();
        // The chart: the section N–M at θ = 0 with the straight bending, the section Mx–My at the N of the selected combination with the biaxial one.
        var options = (JsonObject)settings["dominio2d"]!.DeepClone();
        options["stato"] = "SLU"; options["tipo"] = straight ? "N–M" : "Mx–My"; options["theta"] = "0"; options["filtro"] = "Tutte";
        options["solo_selezionata"] = false; options["tutte_rd"] = true;
        options["N"] = selectedRow?.S("N") ?? "0";
        ConcreteResults.Domain2D? domain = null; string? domainError = null;
        if (selectedForce is not null)
        {
            try { domain = session.Domain(input, settings, options, "SLU", false, [], token).Two; }
            catch (Exception failure) when (failure is not OperationCanceledException) { domainError = failure.Message; errors.Add("Dominio: " + failure.Message); }
        }
        token.ThrowIfCancellationRequested();
        if (selectedForce is { } force && selectedRow is not null)
        {
            if (ultimate)
            {
                try { resistances = StructuralQueries.ConcreteQuickResistance(input, settings, force.N, elastic, token); }
                catch (Exception failure) when (failure is not OperationCanceledException) { errors.Add("Resistenze a N costante: " + failure.Message); }
            }
            token.ThrowIfCancellationRequested();
            if (curvatureChart)
            {
                var curvatureOptions = (JsonObject)settings["momento_curvatura"]!.DeepClone();
                curvatureOptions["N"] = selectedRow.S("N"); curvatureOptions["theta"] = "0";
                try { curvature = StructuralQueries.ConcreteCurvature(input, settings, curvatureOptions, token); }
                catch (Exception failure) when (failure is not OperationCanceledException) { errors.Add("Momento–curvatura: " + failure.Message); }
            }
        }

        // The method n: the stresses of all the combinations, the details of the selected one.
        ConcreteResults.StressState? service = null; double? ratio = null; string serviceStatus = "";
        IReadOnlyDictionary<string, StressOutcome>? stresses = null;
        if (!ultimate && valid.Length > 0)
        {
            try
            {
                stresses = session.Stress(input, settings, settings["sle"]!["SLE"]!.AsObject(), "SLE", valid, token);
                foreach (var row in valid)
                    if (stresses.GetValueOrDefault(row.S("id")) is { State: null } failed) errors.Add(row.S("nome") + ": " + failed.Status);
                var outcome = selectedId is null ? null : stresses.GetValueOrDefault(selectedId);
                service = outcome?.State; ratio = outcome?.Ratio; serviceStatus = outcome?.Status ?? "";
            }
            catch (Exception failure) when (failure is not OperationCanceledException) { errors.Add(ServiceName + ": " + failure.Message); }
        }
        token.ThrowIfCancellationRequested();

        var combinations = rows.Select(row =>
        {
            string id = row.S("id");
            if (statuses.TryGetValue(id, out var status)) return new ConcreteQuickCombination(id, row.S("nome"), null, status, null);
            if (ultimate)
                return checks?.GetValueOrDefault(id) is { } c ? new ConcreteQuickCombination(id, row.S("nome"), c.Utilization, c.Status, c.Utilization is double u ? u <= 1 : null)
                    : new ConcreteQuickCombination(id, row.S("nome"), null, "Da calcolare", null);
            return stresses?.GetValueOrDefault(id) is { } s ? new ConcreteQuickCombination(id, row.S("nome"), s.Ratio, s.Status, s.Ratio is double r ? r <= 1 : null)
                : new ConcreteQuickCombination(id, row.S("nome"), null, "Da calcolare", null);
        }).ToArray();
        return new(method, straight, preview, selectedForce, check, limit, resistances, ultimate ? null : selectedForce, service, ratio, serviceStatus,
            ConcreteDomainChart.Geometry("SLU", options, domain, domainError), ConcreteDomainChart.Actions(options, domain, valid, selectedId, checks),
            curvature, errors, model, yieldStrain, combinations, selectedId);
    }

    /// <summary>
    /// The JSON values of a calculation for the sheet and the project: the error when the check of the method has no result (the state of the
    /// calculation becomes incomplete), the failed steps as warnings, the main outcome.
    /// </summary>
    public static JsonObject Values(ConcreteQuickResult result)
    {
        bool ultimate = result.Method != Methods[2];
        bool missing = ultimate ? result.UltimateCheck is null && result.Resistances.All(r => r.Moment is null) : result.ServiceState is null;
        var summary = Summary(result);
        return new JsonObject
        {
            ["errore"] = missing ? (result.Errors.FirstOrDefault() ?? "Verifica non disponibile.") : "",
            ["avvisi"] = new JsonArray(result.Errors.Select(e => (JsonNode?)JsonValue.Create(e)).ToArray()),
            ["motore"] = "GPC Engine · sezione in c.a.",
            ["metodo"] = result.Method,
            ["modello"] = result.Model,
            ["flessione"] = result.Straight ? Bendings[0] : Bendings[1],
            ["sfruttamento"] = ultimate ? result.UltimateCheck?.Utilization : result.ServiceRatio,
            ["esito"] = ultimate ? result.UltimateCheck?.Status ?? "" : result.ServiceStatus,
            ["risultato"] = summary?.Value ?? "",
            ["comportamento"] = summary?.Behaviour ?? "",
            ["combinazione"] = result.SelectedId ?? "",
            ["combinazioni"] = new JsonArray(result.Combinations.Select(c => (JsonNode?)new JsonObject
            {
                ["id"] = c.Id, ["nome"] = c.Name, ["rapporto"] = c.Ratio, ["esito"] = c.Status
            }).ToArray())
        };
    }

    /// <summary>The label on screen of the code of a method, or of a resistance model.</summary>
    public static string MethodLabel(string method) => Methods.IndexOf(method) is int i and >= 0 ? MethodLabels[i] : method;
    public static string ModelLabel(string model) => Models.IndexOf(model) is int i and >= 0 ? ModelLabels[i] : model;

    /// <summary>
    /// The main outcome in evidence. For the ultimate check: the design resisting moment at the N of the action (the moment of the chosen sign with
    /// the straight bending, Mx,Rd and My,Rd of the resistant point with the biaxial one), η with the outcome, and the behaviour at the resistant point:
    /// ductile when the strain of the tension steel reaches εyd of the steel of the engine, brittle otherwise. For the method n: the stresses.
    /// </summary>
    public static ConcreteQuickSummary? Summary(ConcreteQuickResult? result)
    {
        if (result is null) return null;
        // The name of the selected combination, first in the heading.
        string combination = result.Combinations.FirstOrDefault(c => c.Id == result.SelectedId)?.Name is { Length: > 0 } name ? name + " · " : "";
        if (result.Method == Methods[2])
        {
            string title = combination + "tensioni con il metodo n";
            return result.ServiceState is { } s
                ? new(title, $"σc = {Number(s.sigma_cls)} MPa · σs = {Number(s.sigma_acciaio)} MPa", $"Rapporto {Number(result.ServiceRatio)} · {result.ServiceStatus}", "", null,
                    result.ServiceRatio, result.ServiceRatio is double r ? r <= 1 : null)
                : new(title, "—", result.ServiceStatus, "", null, null, null);
        }
        string direction = result.Method == Methods[0] ? "Mx+" : "Mx−";
        string kind = result.Straight ? MethodLabel(result.Method).ToLowerInvariant() : "flessione deviata";
        string heading = $"{combination}momento resistente di calcolo · {kind} · verifica {ModelLabel(result.Model).ToLowerInvariant()}";
        string value; ConcreteResults.SectionState? state;
        if (result.Straight)
        {
            var resistance = result.Resistances.FirstOrDefault(r => r.Direction == direction);
            value = resistance?.Moment is double moment ? $"MRd = {Number(moment)} kNm" : resistance?.Status ?? "—";
            state = resistance?.Section;
        }
        else if (result.UltimateCheck?.Resistance is { } point)
        {
            value = $"Mx,Rd = {Number(point.Mx)} kNm" + "\n" + $"My,Rd = {Number(point.My)} kNm";
            state = result.UltimateCheck.Response;
        }
        else
        {
            // No line of action (a null action): the principal resistances at the N of the combination, with the sign of the chosen check.
            string sign = result.Method == Methods[1] ? "−" : "+";
            var mx = result.Resistances.FirstOrDefault(r => r.Direction == "Mx" + sign);
            var my = result.Resistances.FirstOrDefault(r => r.Direction == "My" + sign);
            value = mx?.Moment is double x && my?.Moment is double y ? $"Mx,Rd = {Number(x)} kNm" + "\n" + $"My,Rd = {Number(y)} kNm" : "—";
            state = mx?.Section;
        }
        string detail = result.UltimateCheck is { } check ? $"η = {Number(check.Utilization)} · {check.Status}"
            : result.UltimateAction is { N: 0, Mx: 0, My: 0 } ? (result.Straight ? "Sollecitazioni nulle: inserire NEd e MEd per la verifica."
                : "Sollecitazioni nulle: resistenze principali a N = 0; inserire NEd, Mx,Ed e My,Ed per la verifica.") : "";
        var (behaviour, ductile) = Behaviour(state?.EsMax, result.YieldStrain);
        double? utilization = result.UltimateCheck?.Utilization;
        return new(heading, value, detail, behaviour, ductile, utilization, utilization is double u ? u <= 1 : null);
    }

    private static (string Text, bool? Ductile) Behaviour(double? steel, double? yield)
    {
        if (steel is not double strain || yield is not double limit) return ("Comportamento non determinato: deformazione dell'acciaio non disponibile.", null);
        return strain >= limit
            ? ($"Comportamento duttile · εs = {Number(strain)} ‰ ≥ εyd = {Number(limit)} ‰", true)
            : ($"Comportamento fragile · εs = {Number(strain)} ‰ < εyd = {Number(limit)} ‰", false);
    }

    // ---------------------------------------------------------------- presentation

    private static string Number(double? value) => EngineeringFormat.Number(value);

    /// <summary>The properties of the concrete typed by hand: the class becomes «Personalizzato», as in the RC section.</summary>
    public static ImmutableArray<string> ConcreteInputs { get; } = ["fck_mpa"];

    /// <summary>The properties of the steel typed by hand (those of the RC section that make the steel «Personalizzato»).</summary>
    public static ImmutableArray<string> SteelInputs => ConcreteInputPresentation.SteelProperties;

    /// <summary>A property of a material typed by hand: its class becomes «Personalizzato», as in the RC section; returns the key of the class, or null.</summary>
    public static string? MarkCustomMaterial(JsonObject data, string key)
    {
        var input = data["input"]!.AsObject();
        if (ConcreteInputs.Contains(key)) { ConcreteInputPresentation.CustomConcrete(input); return "classe_cls"; }
        if (SteelInputs.Contains(key)) { ConcreteInputPresentation.CustomSteel(input); return "classe_acciaio"; }
        return null;
    }

    /// <summary>The keys of the input written by the class <paramref name="key"/> of the catalogue (to show them again after the choice).</summary>
    public static IReadOnlyList<string> MaterialKeys(JsonObject data, string key) =>
        Catalog(data, key).FirstOrDefault(m => m.S("nome") == data["input"].S(key)) is { } material
            ? ConcreteInputPresentation.StandardMaterialValues(material).Select(p => p.Key).ToArray() : [];

    /// <summary>The values of the concrete that the engine derives from the input.</summary>
    public static IReadOnlyList<ConcreteQuickValue> ConcreteValues(ConcreteQuickResult? result)
    {
        var preview = result?.Preview;
        return [new("fcd", Number(preview?.Fcd), "MPa"), new("Ecm", Number(preview?.Ecm), "MPa"), new("εc2", Number(preview?.Ec2), "‰"), new("εcu", Number(preview?.Ecu), "‰")];
    }

    /// <summary>The values of the steel that the engine derives from the input.</summary>
    public static IReadOnlyList<ConcreteQuickValue> SteelValues(ConcreteQuickResult? result)
    {
        var preview = result?.Preview;
        return [new("fyd", Number(preview?.Fyd), "MPa"), new("εyd", Number(result?.YieldStrain), "‰"), new("n", Number(preview?.N), "Es/Ecm")];
    }

    /// <summary>All the values of the materials that the engine derives from the input.</summary>
    public static IReadOnlyList<ConcreteQuickValue> MaterialValues(JsonObject data, ConcreteQuickResult? result) => [.. ConcreteValues(result), .. SteelValues(result)];

    private static IEnumerable<ConcreteQuickValue> State(ConcreteResults.SectionState? state)
    {
        if (state is null) yield break;
        yield return new("εc min", Number(state.EcMin), "‰");
        yield return new("εs max", Number(state.EsMax), "‰");
        yield return new("d utile", Number(state.UsefulDepth), "mm");
        yield return new("x asse neutro", Number(state.NeutralDistance), "mm");
    }

    /// <summary>The results of the method of the sheet, as the engine gave them.</summary>
    public static IReadOnlyList<ConcreteQuickValue> ResultValues(ConcreteQuickResult? result)
    {
        if (result is null) return [];
        var values = new List<ConcreteQuickValue>();
        if (result.Method != Methods[2])
        {
            if (result.UltimateAction is { } a)
            {
                values.Add(new("NEd", Number(a.N), "kN")); values.Add(new("Mx,Ed", Number(a.Mx), "kNm"));
                if (!result.Straight) values.Add(new("My,Ed", Number(a.My), "kNm"));
            }
            string direction = result.Method == Methods[0] ? "Mx+" : "Mx−";
            // MRd, η and the outcome are in the summary; with the biaxial bending the four principal resistances at the N of the action.
            if (!result.Straight)
                foreach (var resistance in result.Resistances)
                    values.Add(resistance.Moment is double moment ? new(resistance.Direction + ",Rd", Number(moment), "kNm") : new(resistance.Direction + ",Rd", resistance.Status));
            var state = result.Straight ? result.Resistances.FirstOrDefault(r => r.Direction == direction)?.Section : result.UltimateCheck?.Response;
            values.AddRange(State(state));
            return values;
        }
        if (result.ServiceAction is { } s)
        {
            values.Add(new("N", Number(s.N), "kN")); values.Add(new("Mx", Number(s.Mx), "kNm"));
            if (!result.Straight) values.Add(new("My", Number(s.My), "kNm"));
        }
        if (result.ServiceState is { } stress)
        {
            values.Add(new("σc", Number(stress.sigma_cls), "MPa")); values.Add(new("σc limite", Number(stress.ConcreteStressLimit), "MPa"));
            values.Add(new("σs", Number(stress.sigma_acciaio), "MPa")); values.Add(new("σs limite", Number(stress.SteelStressLimit), "MPa"));
            values.Add(new("Rapporto", Number(result.ServiceRatio)));
            values.Add(new("Esito", result.ServiceStatus));
            values.AddRange(State(stress.Response));
        }
        return values;
    }

    /// <summary>The points of the moment–curvature, as the RC section draws them.</summary>
    public static List<double[]> CurvaturePoints(ConcreteQuickResult? result) =>
        result?.Curvature is { } curve ? ConcreteCurvaturePresentation.Points(curve) : [];
}

/// <summary>The calculation service of the quick RC section: snapshots and the calls of <see cref="ConcreteQuickSection.Calculate"/>.</summary>
public sealed class ConcreteQuickSectionService : IModuleService
{
    public string ModuleId => ConcreteQuickSection.Module;

    public JsonObject Prepare(JsonObject source)
    {
        ConcreteQuickSection.Validate(source);
        return (JsonObject)source.DeepClone();
    }

    public ModuleCalculation Calculate(JsonObject source, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        var data = Prepare(source);
        var result = ConcreteQuickSection.Calculate(data, cancellation);
        return new(ModuleId, data, ConcreteQuickSection.Values(result), result);
    }
}
