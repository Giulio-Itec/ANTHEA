using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Application.Presentation;
using Anthea.Reports;

namespace Anthea.Application.Bridge;

public sealed record BridgeQuickCheck(string Key, string Heading, string Value, string Detail, double? Ratio)
{
    public bool? Passed => Ratio is double value ? value <= 1 : null;
}

public sealed record BridgeQuickCase(string Id, string Name, string State, BridgeResultView? Calculation,
    IReadOnlyList<BridgeQuickCheck> Checks, IReadOnlyList<QuickReportValue> Values, IReadOnlyList<string> Warnings, string Error)
{
    public double? Ratio => Error.Length == 0 ? Checks.Select(c => c.Ratio).Max() : null;
    public bool? Passed => Ratio is double value ? value <= 1 : null;
    public string Status => Error.Length > 0 ? "Non determinata: " + Error : Passed is true ? "Entro i limiti locali" : Passed is false ? "Limiti superati" : "Non determinata";
    public BridgeStage? Stage => Calculation?.Stages.LastOrDefault();
}

public sealed record BridgeQuickResult(IReadOnlyList<BridgeQuickCase> Cases);

/// <summary>
/// A compact view of the bridge document. Each combination owns the original phase objects; the selected one's phases and state
/// are also written at the root, where the complete module reads them. All other combinations remain in the additional view key.
/// No resistance, stress, coefficient or phase distribution is calculated here.
/// </summary>
public static class BridgeQuickSection
{
    public const string Module = "str_sezione_composta_rapida", SourceModule = BridgeEditor.Module, ViewKey = "verifica_rapida_mista";
    public const int Casting = 0, LongTerm = 1, ShortTerm = 2, LoadPhaseCount = 3;
    public const string Scope = "Verifica tensionale per fasi e taglio con instabilità dell’anima. Classe della sezione, MRd e SLE frequente non sono disponibili nel motore attuale.";
    public const string ActionHelp = "Momenti già combinati e coefficientati. N e V sono applicati nella fase breve; il ritiro, se presente, resta una fase autonoma. Nessun coefficiente aggiunto ai carichi.";
    public static ImmutableArray<string> States { get; } = ["SLU", "SLE RA", "SLE QP"];
    public static ImmutableArray<string> ActionKeys { get; } = ["M_getto", "M_lungo", "M_breve", "V", "N"];
    public static ModuleInfo Info { get; } = new(Module, "Strutture", "Sezione composta · verifica rapida",
        "Trave saldata a I e soletta: tensioni per fasi e taglio", "Sezione composta", "Sezione composta · verifica rapida", "Verifica compatta con il motore della sezione da ponte");

    public static JsonObject View(JsonObject data) => data[ViewKey] as JsonObject ?? throw new ArgumentException("Vista della sezione composta rapida assente.");
    public static JsonObject[] Combinations(JsonObject data) => View(data)["combinazioni"]!.AsArray().OfType<JsonObject>().ToArray();
    public static string SelectedId(JsonObject data) => View(data).S("selezionata");
    public static JsonObject Selected(JsonObject data) => Combinations(data).Single(r => r.S("id") == SelectedId(data));
    public static JsonObject Create()
    {
        var data = ModuleDirectory.CreateData(SourceModule);
        Initialize(data);
        return data;
    }

    /// <summary>Import an existing bridge document, or pick up edits made in the complete module to the selected combination.</summary>
    public static void Initialize(JsonObject data)
    {
        BridgeEditor.ValidateShape(data);
        BridgeEditor.EnsureAccessoryDefaults(data);
        if (data[ViewKey] is null)
        {
            var row = NewRow("Combo 1", data.S("stato"), data["fasi"]!.AsArray());
            data[ViewKey] = new JsonObject { ["versione"] = 1, ["selezionata"] = row.S("id"), ["combinazioni"] = new JsonArray(row) };
        }
        Validate(data);
        var selected = Selected(data);
        selected["fasi"] = data["fasi"]!.DeepClone();
        selected["stato"] = State(data.S("stato"));
    }

    public static void Validate(JsonObject data)
    {
        BridgeEditor.ValidateShape(data);
        var view = View(data);
        if (view["versione"]?.ToString() != "1" || view["combinazioni"] is not JsonArray { Count: > 0 } rows
            || rows.Any(r => r is not JsonObject)) throw new ArgumentException("Formato delle combinazioni rapide non valido.");
        var combinations = Combinations(data);
        if (combinations.Any(r => r.S("id").Length == 0 || r["fasi"] is not JsonArray { Count: > 0 })
            || combinations.Select(r => r.S("id")).Distinct().Count() != combinations.Length
            || !combinations.Any(r => r.S("id") == SelectedId(data))) throw new ArgumentException("Identificatori o fasi delle combinazioni non validi.");
        foreach (var row in combinations) _ = State(row.S("stato"));
    }

    public static string State(string value) => value.Trim().ToUpperInvariant() switch
    {
        "" or "SLU" => "SLU",
        "SLE" or "SLE RA" or "SLE RARA" or "RARA" => "SLE RA",
        "SLE_QP" or "SLE QP" or "SLE QUASI PERMANENTE" or "QUASI PERMANENTE" => "SLE QP",
        _ => throw new ArgumentException("Stato limite non disponibile: " + value + ". Scegliere SLU, SLE RA o SLE QP; SLE frequente non è supportato dal motore.")
    };
    private static string NativeState(string state) => State(state) switch { "SLE RA" => "SLE rara", "SLE QP" => "SLE quasi permanente", _ => "SLU" };
    private static JsonObject NewRow(string name, string state, JsonArray phases) => new()
    {
        ["id"] = Guid.NewGuid().ToString("N"), ["nome"] = name, ["stato"] = State(state), ["fasi"] = phases.DeepClone()
    };
    private static (int Phase, string Field) Column(string key) => key switch
    {
        "M_getto" => (Casting, "Mx"), "M_lungo" => (LongTerm, "Mx"), "M_breve" => (ShortTerm, "Mx"),
        "V" or "N" => (ShortTerm, key), _ => throw new ArgumentException("Colonna sconosciuta: " + key)
    };
    public static string Action(JsonObject row, string key)
    {
        var column = Column(key);
        return row["fasi"] is JsonArray phases && phases.ElementAtOrDefault(column.Phase) is JsonObject phase ? phase.S(column.Field) : "";
    }
    public static double Number(string value, string label) => Anthea.Application.Concrete.ConcreteEditor.Number(value.Length == 0 ? "0" : value, label);

    /// <summary>Unsupported complete documents remain intact and can be opened in their full editor.</summary>
    public static string ScopeError(JsonObject data)
    {
        if (data.S("sezione") != BridgeEditor.SectionTypes[0] || data.B("plate2") || data.B("predalle") || data.B("irrigidimenti")
            || data.B("appoggio") || data.B("pioli") || data.B("armatura_trasv") || data.B("fatica_pioli") || BridgeEditor.TorsionEnabled(data))
            return "Questo documento contiene opzioni fuori dalla verifica rapida. Aprirlo nel modulo completo; i dati sono conservati.";
        foreach (var row in Combinations(data))
        {
            var phases = row["fasi"]!.AsArray().OfType<JsonObject>().ToArray();
            if (phases.Length < LoadPhaseCount || phases.Length != row["fasi"]!.AsArray().Count
                || phases[Casting].S("tipo") != "Solo acciaio" || phases[LongTerm].S("tipo") != "Composta" || phases[ShortTerm].S("tipo") != "Composta"
                || phases.Take(LoadPhaseCount).Any(p => !p.B("attiva")) || phases.Skip(LoadPhaseCount).Any(p => p.S("tipo") != BridgeEditor.ShrinkageKind))
                return "Le fasi di «" + row.S("nome") + "» richiedono il modulo completo. Sono conservate senza ripartizioni automatiche.";
            if (phases.Take(ShortTerm).Any(p => Number(p.S("N"), "N") != 0 || Number(p.S("V"), "V") != 0))
                return "N o V sono presenti in fasi diverse da quella breve. Aprire il modulo completo per conservarne la distribuzione.";
        }
        return "";
    }

    public static void Write(JsonObject data, string id, string key, string value)
    {
        var row = Combinations(data).Single(r => r.S("id") == id);
        if (key == "nome") row[key] = value;
        else if (key == "stato") row[key] = State(value);
        else { var column = Column(key); row["fasi"]![column.Phase]![column.Field] = value; }
        if (id == SelectedId(data)) Synchronize(data);
    }
    public static void Select(JsonObject data, string id)
    {
        if (!Combinations(data).Any(r => r.S("id") == id)) throw new ArgumentException("Combinazione assente.");
        View(data)["selezionata"] = id; Synchronize(data);
    }
    public static void Synchronize(JsonObject data)
    {
        var row = Selected(data);
        data["fasi"] = row["fasi"]!.DeepClone(); data["stato"] = NativeState(row.S("stato"));
    }
    public static string Add(JsonObject data)
    {
        var phases = (JsonArray)Selected(data)["fasi"]!.DeepClone();
        foreach (var phase in phases.OfType<JsonObject>())
            foreach (string key in new[] { "Mx", "N", "V", "epsilon_cs" }) phase[key] = "0";
        var row = NewRow("Combo " + (Combinations(data).Length + 1), "SLU", phases);
        View(data)["combinazioni"]!.AsArray().Add(row); Select(data, row.S("id")); return row.S("id");
    }
    public static void Remove(JsonObject data, string id)
    {
        var rows = View(data)["combinazioni"]!.AsArray();
        if (rows.Count <= 1) return;
        var row = rows.OfType<JsonObject>().Single(r => r.S("id") == id); rows.Remove(row);
        if (id == SelectedId(data)) Select(data, Combinations(data)[0].S("id"));
    }
    public static JsonObject ForCombination(JsonObject data, string id)
    {
        var copy = (JsonObject)data.DeepClone(); Select(copy, id);
        return copy;
    }
    public static void Replace(JsonObject data, IReadOnlyList<BridgeQuickActionsExcel.Row> rows)
    {
        if (rows.Count == 0) throw new ArgumentException("Inserire almeno una combinazione.");
        // Retain phase options and hidden shrinkage for the corresponding existing row. New rows start from the selected phase template.
        var previous = Combinations(data);
        var items = new JsonArray();
        for (int index = 0; index < rows.Count; index++)
        {
            var input = rows[index];
            var template = previous.ElementAtOrDefault(index) ?? Selected(data);
            var row = NewRow(input.Name, input.State, template["fasi"]!.AsArray());
            if (index < previous.Length) row["id"] = template.S("id");
            var values = new[] { input.Casting, input.LongTerm, input.ShortTerm, input.V, input.N };
            for (int column = 0; column < ActionKeys.Length; column++)
            {
                var slot = Column(ActionKeys[column]); row["fasi"]![slot.Phase]![slot.Field] = values[column];
            }
            items.Add(row);
        }
        View(data)["combinazioni"] = items;
        Select(data, items.OfType<JsonObject>().Any(r => r.S("id") == SelectedId(data)) ? SelectedId(data) : items[0]!.AsObject().S("id"));
    }

    public static BridgeQuickResult Calculate(JsonObject data, CancellationToken cancellation = default)
    {
        Validate(data);
        string scopeError = ScopeError(data);
        var cases = new List<BridgeQuickCase>();
        foreach (var row in Combinations(data))
        {
            cancellation.ThrowIfCancellationRequested();
            string id = row.S("id"), name = row.S("nome"), state = State(row.S("stato"));
            try
            {
                if (scopeError.Length > 0) throw new ArgumentException(scopeError);
                if (name.Trim().Length == 0) throw new ArgumentException("Nome della combinazione mancante.");
                foreach (var key in ActionKeys) _ = Number(Action(row, key), key);
                var input = ForCombination(data, id);
                var calculation = ModuleServices.Get(SourceModule).Calculate(input, cancellation);
                if (calculation.HasError) throw new ArgumentException(calculation.Values.S("errore"));
                var result = BridgeResultView.Read(calculation);
                var checks = Checks(result, state);
                cases.Add(new(id, name, state, result, checks, ResultValues(result), result.Stages.SelectMany(s => s.Warnings).Distinct().ToArray(), ""));
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or ArithmeticException)
            { cases.Add(new(id, name, state, null, [], [], [], error.Message)); }
        }
        return new(cases);
    }
    private static string N(double? value) => EngineeringFormat.Number(value);
    public static (BridgeGeometry? Geometry, string Error) Preview(JsonObject data)
    {
        try { return (new BridgeProjection().Read(Anthea.Calculations.BridgeSection.Geometry(data)), ""); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or ArithmeticException) { return (null, error.Message); }
    }
    private static BridgeQuickCheck Stress(string key, string heading, BridgeStage stage, BridgeStressPoint point) =>
        new(key, heading, "σ = " + N(point.Stress) + " MPa", "Limite " + N(point.Limit) + " MPa · η = " + N(point.Utilization)
            + " · " + stage.Name + " · " + point.Name, point.Utilization);
    private static IReadOnlyList<BridgeQuickCheck> Checks(BridgeResultView result, string state)
    {
        var checks = new List<BridgeQuickCheck>();
        var points = result.Stages.SelectMany(s => s.Points.Where(p => p.Active).Select(p => (Stage: s, Point: p))).ToArray();
        if (state == "SLU")
        {
            var governing = points.OrderByDescending(p => p.Point.Utilization).FirstOrDefault();
            if (governing.Point is not null) checks.Add(Stress("tensioni", "Tensioni · tutte le fasi", governing.Stage, governing.Point));
        }
        else
        {
            foreach (string label in new[] { "Soletta · estradosso", "Acciaio · estradosso", "Acciaio · intradosso" })
            {
                var governing = points.Where(p => p.Point.Name == label).OrderByDescending(p => p.Point.Utilization).FirstOrDefault();
                if (governing.Point is not null) checks.Add(Stress(label, label.Replace("Acciaio · estradosso", "Piattabanda superiore").Replace("Acciaio · intradosso", "Piattabanda inferiore"), governing.Stage, governing.Point));
            }
            var other = points.Where(p => p.Point.Material == "Armatura").OrderByDescending(p => p.Point.Utilization).FirstOrDefault();
            if (other.Point is not null) checks.Add(Stress("armatura", "Armatura della soletta", other.Stage, other.Point));
            // Include any remaining governing fibre (e.g. a reversed stress profile at the slab intrados).
            var maximum = points.OrderByDescending(p => p.Point.Utilization).FirstOrDefault();
            if (maximum.Point is not null && !checks.Any(c => c.Ratio == maximum.Point.Utilization))
                checks.Add(Stress("altre-fibre", "Altre fibre · tutte le fasi", maximum.Stage, maximum.Point));
        }
        foreach (var group in result.Stages.SelectMany(s => (s.Shear?.Checks ?? []).Select(c => (Stage: s, Check: c))).GroupBy(p => p.Check.Name))
        {
            var item = group.OrderByDescending(p => p.Check.Ratio).ThenByDescending(p => Math.Abs(p.Check.Demand)).First(); var check = item.Check;
            checks.Add(new(check.Name, check.Unit == "kN" ? "Taglio · instabilità dell’anima" : "Anima · tensione equivalente",
                (check.Unit == "kN" ? "VRd = " : "σeq = ") + N(check.Unit == "kN" ? check.Resistance : check.Demand) + " " + check.Unit,
                "Domanda " + N(check.Demand) + " / limite " + N(check.Resistance) + " " + check.Unit + (check.Ratio is null ? " · rapporto non previsto per questo SL" : " · η = " + N(check.Ratio)) + " · " + item.Stage.Name, check.Ratio));
        }
        return checks;
    }
    public static IReadOnlyList<QuickReportValue> ResultValues(BridgeResultView result)
    {
        var stage = result.Stages.Last(); var values = new List<QuickReportValue>
        {
            new("M totale", N(stage.Contributions.Sum(c => c.MomentAtInterface)), "kNm"), new("Area carpenteria", N(result.Geometry.SteelArea), "mm²"),
            new("N totale", N(stage.Contributions.Sum(c => c.N)), "kN"), new("As totale", N(result.Geometry.Bars.Sum(b => b.Area)), "mm²")
        };
        foreach (var phase in stage.Contributions.Where(c => c.HasConcrete && !c.IsShrinkage))
        { values.Add(new("n · " + phase.Name, N(phase.HomogenizationN))); values.Add(new("yG · " + phase.Name, N(phase.Centroid), "mm")); }
        if (stage.Contributions.LastOrDefault(c => c.HasConcrete && !c.IsShrinkage) is { } last)
        { values.Add(new("A* · fase breve", N(last.Area), "mm²")); values.Add(new("Ix* · fase breve", N(last.Inertia), "mm⁴")); }
        return values;
    }
    public static IReadOnlyList<QuickReportValue> MaterialValues(BridgeResultView? result, string material)
    {
        if (result is null) return [];
        var native = result.Json()["Materials"]!.AsObject();
        return material switch
        {
            "cls" => [new("fck", N(Number(native.S("Fck"), "fck")), "MPa"), new("Ecm", N(Number(native.S("Ec"), "Ecm")), "MPa")],
            "acciaio" => [new("fy", N(Number(native.S("Fy"), "fy")), "MPa"), new("Ea", N(Number(native.S("Ea"), "Ea")), "MPa")],
            _ => [new("fyk", N(Number(native.S("Fys"), "fyk")), "MPa"), new("Es", N(Number(native.S("Es"), "Es")), "MPa")]
        };
    }
    public static IReadOnlyList<string> BarAreas(JsonObject data, BridgeGeometry? geometry)
    {
        if (geometry is null) return ["—", "—"];
        var slab = BridgeEditor.SlabLayout(data);
        return new[] { slab.TopAxisY, slab.BottomAxisY }.Select(y => N(geometry.Bars.Where(b => b.Y == y).Sum(b => b.Area))).ToArray();
    }
    public static JsonObject Values(BridgeQuickResult result) => new()
    {
        ["errore"] = "", ["ambito"] = Scope,
        ["avvisi"] = new JsonArray(result.Cases.SelectMany(c => c.Warnings).Distinct().Select(w => (JsonNode?)JsonValue.Create(w)).ToArray()),
        ["combinazioni"] = new JsonArray(result.Cases.Select(c => (JsonNode)new JsonObject
        { ["id"] = c.Id, ["nome"] = c.Name, ["stato"] = c.State, ["rapporto"] = c.Ratio, ["esito"] = c.Status, ["errore"] = c.Error, ["risultato"] = c.Calculation?.Json() }).ToArray())
    };
}

public sealed class BridgeQuickSectionService : IModuleService
{
    public string ModuleId => BridgeQuickSection.Module;
    public JsonObject Prepare(JsonObject source) { BridgeQuickSection.Validate(source); return (JsonObject)source.DeepClone(); }
    public ModuleCalculation Calculate(JsonObject source, CancellationToken cancellation = default)
    {
        var data = Prepare(source); var result = BridgeQuickSection.Calculate(data, cancellation);
        return new(ModuleId, data, BridgeQuickSection.Values(result), result);
    }
}
