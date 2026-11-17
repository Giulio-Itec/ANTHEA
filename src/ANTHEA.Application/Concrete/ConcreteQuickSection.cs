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
    IReadOnlyList<string> Errors);

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

    /// <summary>The components of an action that the sheet edits.</summary>
    public static ImmutableArray<string> ActionComponents { get; } = ["N", "Mx", "My"];

    /// <summary>The metadata of the module for the navigation.</summary>
    public static ModuleInfo Info { get; } = new(Module, "Strutture", "Sezione in c.a. · verifica rapida",
        "SLU, metodo n, dominio M–N e M–χ con il motore della sezione in c.a.", "Sezione c.a. rapida", "Sezione in c.a. · verifica rapida", "Verifica rapida della sezione");

    /// <summary>
    /// A new sheet: the default document of the RC section (its catalogue, prepared with unified actions), the second layers off, one ultimate
    /// action and one action of the method n, the choices of the view.
    /// </summary>
    public static JsonObject Create()
    {
        var data = ModuleDirectory.CreateData(SourceModule);
        var input = data["input"]!.AsObject();
        foreach (string layer in new[] { "top", "bottom", "inner" })
        {
            input["second_" + layer + "_enabled"] = false;
            input["second_" + layer + "_count"] = layer == "inner" ? "8" : "2";
            input["second_" + layer + "_diameter"] = "20";
            input["second_" + layer + "_gap"] = "25";
        }
        SectionActionInputs.Replace(data, [Row("SLU", UltimateName, "-1000", "400"), Row("SLE", ServiceName, "-800", "250")]);
        data[ViewKey] = new JsonObject { ["titolo"] = "", ["metodo"] = Methods[0], ["flessione"] = Bendings[0], ["grafico"] = Charts[0] };
        return data;
    }

    private static JsonObject Row(string state, string name, string n, string mx) => new()
    {
        ["stato"] = state, ["nome"] = name, ["elemento"] = "", ["N"] = n, ["Mx"] = mx, ["My"] = "0", ["Vx"] = "0", ["Vy"] = "0", ["T"] = "0"
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
    }

    /// <summary>The choices of the view of <paramref name="data"/>.</summary>
    public static JsonObject View(JsonObject data) => data[ViewKey] as JsonObject ?? throw new ArgumentException("Scelte della verifica rapida mancanti.");

    // ---------------------------------------------------------------- actions

    /// <summary>The first action of <paramref name="state"/> (SLU or SLE) as the RC section reads it, or null.</summary>
    public static JsonObject? Action(JsonObject data, string state) => ConcreteSheetPreparation.ReadActions(data)[state].FirstOrDefault();

    /// <summary>N, Mx and My of the first action of <paramref name="state"/>, as texts (empty without an action).</summary>
    public static JsonObject ActionValues(JsonObject data, string state)
    {
        var row = Action(data, state);
        var values = new JsonObject();
        foreach (string key in ActionComponents) values[key] = row?.S(key) ?? "";
        return values;
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
        try { return (ConcretePreview.PrepareGeometry(data["input"]!.AsObject()), ""); }
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
        var settings = SectionWorkspace.Prepare(data);
        var input = data["input"]!.AsObject();
        var view = View(data);
        string method = view.S("metodo");
        bool straight = view.S("flessione") == Bendings[0], ultimate = method != Methods[2], curvatureChart = view.S("grafico") == Charts[1];
        var preview = ConcretePreview.Read(input, settings);
        token.ThrowIfCancellationRequested();
        var errors = new List<string>();
        var session = new ConcreteAnalysisSession();
        JsonObject? Read(string state, string label)
        {
            var row = Action(data, state);
            if (row is null) { errors.Add(label + ": sollecitazione mancante."); return null; }
            if (straight) row["My"] = "0";
            if (row.S("nome").Length == 0) row["nome"] = label;
            return row;
        }

        ConcreteResults.Action? ultimateAction = null; ConcreteResults.Check? check = null; ConcreteResults.StressState? limit = null;
        IReadOnlyList<ConcreteResults.PrincipalResistance> resistances = [];
        // The check: the section Mx–My of the domain at the N of the action (the check at constant N, as the 2D panel of the RC section); the
        // chart: the section N–M at θ = 0 with the straight bending, the same section Mx–My with the biaxial one.
        JsonObject Options(string type)
        {
            var options = (JsonObject)settings["dominio2d"]!.DeepClone();
            options["stato"] = "SLU"; options["tipo"] = type; options["theta"] = "0"; options["filtro"] = "Tutte";
            options["solo_selezionata"] = false; options["tutte_rd"] = true;
            return options;
        }
        var checkOptions = Options("Mx–My");
        var options = straight ? Options("N–M") : checkOptions;
        ConcreteResults.Domain2D? domain = null; string? domainError = null;
        ConcreteResults.CurvatureResult? curvature = null;
        var ultimateRow = Read("SLU", UltimateName);
        if (ultimateRow is not null)
        {
            try { ultimateAction = ConcreteDomainChart.ReadNamedAction(ultimateRow); }
            catch (ArgumentException failure) { errors.Add(UltimateName + ": " + failure.Message); }
        }
        if (ultimateAction is { } force && ultimateRow is not null)
        {
            checkOptions["N"] = ultimateRow.S("N");
            try
            {
                var result = session.Domain(input, settings, checkOptions, "SLU", false, [ultimateRow], token);
                domain = result.Two;
                check = result.Checks.GetValueOrDefault(ultimateRow.S("id"));
                if (check?.Utilization is null && check is not null) errors.Add("Verifica SLU: " + check.Status);
                limit = check?.LimitState?.Value;
            }
            catch (Exception failure) when (failure is not OperationCanceledException) { domainError = failure.Message; errors.Add("Verifica SLU: " + failure.Message); }
            token.ThrowIfCancellationRequested();
            if (straight)
            {
                domain = null;
                try { domain = session.Domain(input, settings, options, "SLU", false, [], token).Two; }
                catch (Exception failure) when (failure is not OperationCanceledException) { domainError = failure.Message; errors.Add("Dominio N–M: " + failure.Message); }
            }
            token.ThrowIfCancellationRequested();
            if (ultimate)
            {
                try { resistances = StructuralQueries.ConcreteQuickResistance(input, settings, force.N, false, token); }
                catch (Exception failure) when (failure is not OperationCanceledException) { errors.Add("Resistenze a N costante: " + failure.Message); }
            }
            token.ThrowIfCancellationRequested();
            if (curvatureChart)
            {
                var curvatureOptions = (JsonObject)settings["momento_curvatura"]!.DeepClone();
                curvatureOptions["N"] = ultimateRow.S("N"); curvatureOptions["theta"] = "0";
                try { curvature = StructuralQueries.ConcreteCurvature(input, settings, curvatureOptions, token); }
                catch (Exception failure) when (failure is not OperationCanceledException) { errors.Add("Momento–curvatura: " + failure.Message); }
            }
        }

        ConcreteResults.Action? serviceAction = null; ConcreteResults.StressState? service = null; double? ratio = null; string serviceStatus = "";
        if (!ultimate && Read("SLE", ServiceName) is { } serviceRow)
        {
            try
            {
                serviceAction = ConcreteDomainChart.ReadNamedAction(serviceRow);
                var outcome = session.Stress(input, settings, settings["sle"]!["SLE"]!.AsObject(), "SLE", [serviceRow], token).GetValueOrDefault(serviceRow.S("id"));
                service = outcome?.State; ratio = outcome?.Ratio; serviceStatus = outcome?.Status ?? "";
                if (service is null) errors.Add(ServiceName + ": " + serviceStatus);
            }
            catch (Exception failure) when (failure is not OperationCanceledException) { errors.Add(ServiceName + ": " + failure.Message); }
        }
        token.ThrowIfCancellationRequested();
        var rows = ultimateRow is null ? Array.Empty<JsonObject>() : [ultimateRow];
        var checks = check is null || ultimateRow is null ? null : new Dictionary<string, ConcreteResults.Check> { [ultimateRow.S("id")] = check };
        return new(method, straight, preview, ultimateAction, check, limit, resistances, serviceAction, service, ratio, serviceStatus,
            ConcreteDomainChart.Geometry("SLU", options, domain, domainError), ConcreteDomainChart.Actions(options, domain, rows, ultimateRow?.S("id"), checks),
            curvature, errors);
    }

    /// <summary>
    /// The JSON values of a calculation for the sheet and the project: the error when the check of the method has no result (the state of the
    /// calculation becomes incomplete), the failed steps as warnings, the main outcome.
    /// </summary>
    public static JsonObject Values(ConcreteQuickResult result)
    {
        bool ultimate = result.Method != Methods[2];
        bool missing = ultimate ? result.UltimateCheck?.Utilization is null : result.ServiceState is null;
        return new JsonObject
        {
            ["errore"] = missing ? (result.Errors.FirstOrDefault() ?? "Verifica non disponibile.") : "",
            ["avvisi"] = new JsonArray(result.Errors.Select(e => (JsonNode?)JsonValue.Create(e)).ToArray()),
            ["motore"] = "GPC Engine · sezione in c.a.",
            ["metodo"] = result.Method,
            ["flessione"] = result.Straight ? Bendings[0] : Bendings[1],
            ["sfruttamento"] = ultimate ? result.UltimateCheck?.Utilization : result.ServiceRatio,
            ["esito"] = ultimate ? result.UltimateCheck?.Status ?? "" : result.ServiceStatus
        };
    }

    // ---------------------------------------------------------------- presentation

    private static string Number(double? value) => EngineeringFormat.Number(value);

    /// <summary>The materials of the sheet: the values of the input and those the engine derives from them.</summary>
    public static IReadOnlyList<ConcreteQuickValue> MaterialValues(JsonObject data, ConcreteQuickResult? result)
    {
        var input = data["input"];
        var preview = result?.Preview;
        return
        [
            new("fck", input.S("fck_mpa"), "MPa"), new("fcd", Number(preview?.Fcd), "MPa"), new("Ecm", Number(preview?.Ecm), "MPa"),
            new("εc2", Number(preview?.Ec2), "‰"), new("εcu", Number(preview?.Ecu), "‰"),
            new("fyk", input.S("fyk_mpa"), "MPa"), new("fyd", Number(preview?.Fyd), "MPa"), new("Es", input.S("steel_modulus_mpa"), "MPa"),
            new("εsu", input.S("steel_eps_u"), "‰"), new("n = Es / Ecm", Number(preview?.N))
        ];
    }

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
            if (result.Straight)
            {
                var resistance = result.Resistances.FirstOrDefault(r => r.Direction == direction);
                if (resistance is not null)
                    values.Add(resistance.Moment is double moment ? new("Mx,Rd (" + direction + ")", Number(moment), "kNm") : new("Mx,Rd (" + direction + ")", resistance.Status));
            }
            else
                foreach (var resistance in result.Resistances)
                    values.Add(resistance.Moment is double moment ? new(resistance.Direction + ",Rd", Number(moment), "kNm") : new(resistance.Direction + ",Rd", resistance.Status));
            if (result.UltimateCheck is { } check)
            {
                if (!result.Straight && check.Resistance is { } r)
                {
                    values.Add(new("Mx,Rd punto resist.", Number(r.Mx), "kNm")); values.Add(new("My,Rd punto resist.", Number(r.My), "kNm"));
                }
                values.Add(new("η", Number(check.Utilization)));
                values.Add(new("Esito", check.Status));
            }
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
