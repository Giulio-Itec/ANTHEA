using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using Anthea.Reports;
using GPC.Checkers.Geotechnics.Seepage;
using GPC.Model.Checking.Contracts;
using GPC.Model.Geotechnics;
using GPC.Model.Geotechnics.EmbeddedWalls;
using GPC.Model.Geotechnics.Seepage;
using GPC.Model.Standards;
using static Anthea.Application.EmbeddedWalls.EmbeddedWallAdapter;

namespace Anthea.Application.EmbeddedWalls;

/// <summary>Mapping, provenance and orchestration only. Integrals, gradients, criteria and factors belong to GPC.</summary>
internal static class EmbeddedWallHydraulicChecks
{
    internal static IReadOnlyList<EmbeddedWallHydraulicCheckData> Calculate(JsonObject data, EmbeddedWallModel model,
        IReadOnlyDictionary<string, SeepageResult> fields, CancellationToken cancellation)
    {
        var upl = EmbeddedWallDocument.Rows(data, "hydraulic_upl").ToArray();
        var hyd = EmbeddedWallDocument.Rows(data, "hydraulic_hyd").ToArray();
        var actions = EmbeddedWallDocument.Rows(data, "hydraulic_actions").ToArray();
        var stages = model.Stages.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        var ids = new HashSet<(string Stage, string Id)>();
        foreach (var row in upl.Concat(hyd))
            if (!stages.Contains(Text(row, "stage")) || string.IsNullOrWhiteSpace(Text(row, "id")) || !ids.Add((Text(row, "stage"), Text(row, "id"))))
                throw new ArgumentException("Verifiche idrauliche: fase esistente e identificativo univoco nella fase richiesti.");
        foreach (var row in actions)
            if (!upl.Any(u => Text(u, "stage") == Text(row, "stage") && Text(u, "id") == Text(row, "check")))
                throw new ArgumentException("Azione idraulica riferita a una verifica UPL inesistente.");
        if (ids.Count == 0) return [];
        StandardGeotechnical standard = Text(data, "hydraulic_standard") switch
        {
            "NTC 2018" => new StandardNTC2018Geotechnics(),
            "EN 1997-1 — profilo non disponibile" => new StandardEN1997p1(),
            _ => throw new ArgumentException("Profilo normativo idraulico non riconosciuto.")
        };
        var situation = Choice<GeotechnicalSituation>(data, "hydraulic_situation");
        var reports = new List<EmbeddedWallHydraulicCheckData>();
        foreach (var row in upl.Concat(hyd))
        {
            cancellation.ThrowIfCancellationRequested();
            string stage = Text(row, "stage"), id = Text(row, "id"), method = upl.Contains(row) ? "UPL" : "HYD-" + Text(row, "method");
            var values = new List<EmbeddedWallHydraulicValue>();
            try
            {
                if (standard.HydraulicProfile(situation) is null) throw new MissingInput("Profilo idraulico non disponibile per " + standard.Name + " / " + situation + ". Nessun coefficiente sostitutivo applicato.");
                var result = method == "UPL" ? Uplift(row, actions, fields, model.CaseId, standard, situation, values)
                    : Gradient(row, fields, model.CaseId, standard, situation, values);
                reports.Add(Map(result, values));
            }
            catch (MissingInput e)
            {
                reports.Add(new(model.CaseId, stage, id, method, standard.HydraulicProfile(situation)?.Reference ?? "", Text(row, "source"),
                    "", null, null, null, EngineeringOutcome.NotEvaluated, e.Message,
                    new ReadOnlyDictionary<string, double>(new Dictionary<string, double>()), values.AsReadOnly(), []));
            }
        }
        return reports.AsReadOnly();
    }
    private static HydraulicSafetyResult Uplift(JsonObject row, JsonObject[] rows, IReadOnlyDictionary<string, SeepageResult> fields,
        string caseId, StandardGeotechnical standard, GeotechnicalSituation situation, List<EmbeddedWallHydraulicValue> values)
    {
        string stage = Text(row, "stage"), id = Text(row, "id");
        var actions = rows.Where(a => Text(a, "stage") == stage && Text(a, "check") == id).Select(a => new HydraulicForceAction
        {
            Id = Text(a, "id"), Category = Choice<HydraulicActionCategory>(a, "category"), UpwardForce = Required(a, "upward_force") * NewtonsPerKilonewton, Source = Text(a, "source")
        }).ToList();
        string origin = Text(row, "water_resultant");
        if (origin == "FEM 2D")
        {
            var field = Field(fields, stage);
            double x0 = Required(row, "x_start") * SoilUnits.Metre, x1 = Required(row, "x_end") * SoilUnits.Metre,
                z = Required(row, "z") * SoilUnits.Metre, width = Required(row, "body_width") * SoilUnits.Metre, topPressure = Required(row, "top_pressure") * SoilUnits.KiloPascal;
            double resultant = Sample(() => SeepageSampling.NetUpwardForce(field, x0, x1, z, width, topPressure, Flag(row, "ignore_suction")));
            values.Add(new("Risultante idraulica netta caratteristica", resultant / NewtonsPerKilonewton, "kN"));
            actions.Add(new() { Id = "Acqua FEM", Category = Choice<HydraulicActionCategory>(row, "water_category"), UpwardForce = resultant,
                Source = "Campo " + field.CaseId + "; pressione superiore uniforme; " + Text(row, "source") });
            if (string.IsNullOrWhiteSpace(Text(row, "source"))) throw new MissingInput("Documentare geometria del corpo, pressione superiore e classificazione dell'azione idraulica.");
        }
        else if (origin != "Assegnata nelle azioni") throw new ArgumentException("Origine della risultante UPL non riconosciuta.");
        double rd = Required(row, "rd") * NewtonsPerKilonewton;
        values.Add(new("Resistenza aggiuntiva già di progetto Rd", rd / NewtonsPerKilonewton, "kN"));
        return HydraulicSafety.Uplift(new() { CaseId = caseId, StageId = stage, ElementId = id, Situation = situation,
            CharacteristicActions = actions, AdditionalDesignResistance = rd, ResistanceSource = Text(row, "rd_source") }, standard);
    }
    private static HydraulicSafetyResult Gradient(JsonObject row, IReadOnlyDictionary<string, SeepageResult> fields, string caseId,
        StandardGeotechnical standard, GeotechnicalSituation situation, List<EmbeddedWallHydraulicValue> values)
    {
        string stage = Text(row, "stage"), id = Text(row, "id"), method = Text(row, "method"), origin = Text(row, "field_source");
        if (method is not ("Mean" or "Exit" or "Loaded")) throw new ArgumentException("Metodo HYD non riconosciuto.");
        bool fem = origin == "FEM 2D";
        if (!fem && origin != "Assegnato") throw new ArgumentException("Origine dei dati HYD non riconosciuta.");
        bool vertical = Flag(row, "vertical_flow"), outflow = Flag(row, "outflow_confirmed");
        string source = Text(row, "source");
        if (!vertical || !outflow || string.IsNullOrWhiteSpace(source))
            throw new MissingInput("Documentare terreno saturo, flusso prevalentemente verticale e frontiera di efflusso pertinente al criterio scelto. Il campo 2D non dimostra automaticamente queste condizioni.");
        SeepageResult? field = fem ? Field(fields, stage) : null;
        double x = fem ? Required(row, "x") * SoilUnits.Metre : 0, z = fem ? Required(row, "z") * SoilUnits.Metre : 0;
        if (fem) source = "Campo " + field!.CaseId + "; " + source;
        if (method == "Loaded")
        {
            double pressure = fem ? Sample(() => SeepageWallCoupling.PressureAt(field!, x, z)) : Required(row, "pressure") * SoilUnits.KiloPascal;
            double hydrostatic = Required(row, "hydrostatic_pressure") * SoilUnits.KiloPascal, stress = Required(row, "effective_stress") * SoilUnits.KiloPascal;
            values.Add(new("Pressione interstiziale caratteristica", pressure / SoilUnits.KiloPascal, "kPa"));
            values.Add(new("Pressione idrostatica di riferimento", hydrostatic / SoilUnits.KiloPascal, "kPa"));
            values.Add(new("Tensione efficace senza filtrazione", stress / SoilUnits.KiloPascal, "kPa"));
            if (pressure < 0) throw new MissingInput("Pressione negativa nel punto: il criterio di efflusso caricato saturo non è applicabile.");
            return HydraulicSafety.LoadedOutflow(new() { CaseId = caseId, StageId = stage, ElementId = id, Situation = situation,
                PorePressure = pressure, HydrostaticPressure = hydrostatic, EffectiveStressWithoutSeepage = stress,
                PredominantlyVerticalFlow = vertical, LoadedOutflow = outflow, ApplicabilitySource = source }, standard);
        }
        double gradient;
        if (!fem) gradient = Required(row, "gradient");
        else if (method == "Mean")
        {
            double bottom = Required(row, "z_bottom") * SoilUnits.Metre;
            var line = Sample(() => SeepageSampling.Line(field!, x, z, x, bottom));
            if (line.Any(p => p.Pressure < 0)) throw new MissingInput("La colonna attraversa pressioni negative: il criterio di gradiente critico per terreno saturo non è applicabile.");
            gradient = Sample(() => SeepageSampling.MeanUpwardGradient(field!, x, z, bottom));
        }
        else
        {
            if (Sample(() => SeepageWallCoupling.PressureAt(field!, x, z)) < 0) throw new MissingInput("Pressione negativa nel punto: il criterio di efflusso libero saturo non è applicabile.");
            var sample = Sample(() => SeepageSampling.GradientsAt(field!, x, z)); gradient = sample.MaximumUpwardGradient;
            values.Add(new("Massimo modulo gradiente orizzontale negli elementi incidenti", sample.MaximumAbsoluteHorizontalGradient, "1"));
            source += "; elementi incidenti: " + string.Join(", ", sample.Elements);
        }
        double gamma = Required(row, "gamma_sat") * SoilUnits.KiloNewtonPerCubicMetre;
        values.Add(new("Gradiente ascendente caratteristico", gradient, "1")); values.Add(new("Peso saturo rappresentativo", gamma / SoilUnits.KiloNewtonPerCubicMetre, "kN/m³"));
        return HydraulicSafety.FreeOutflow(new() { CaseId = caseId, StageId = stage, ElementId = id, Situation = situation,
            Measure = method == "Mean" ? HydraulicGradientMeasure.Mean : HydraulicGradientMeasure.Exit, UpwardGradient = gradient, SaturatedUnitWeight = gamma,
            PredominantlyVerticalFlow = vertical, FreeOutflow = outflow, ApplicabilitySource = source }, standard);
    }
    private static EmbeddedWallHydraulicCheckData Map(HydraulicSafetyResult result, List<EmbeddedWallHydraulicValue> values)
    {
        double unitScale = result.Unit switch { "N" => NewtonsPerKilonewton, "MPa" => SoilUnits.KiloPascal, _ => 1 };
        string unit = result.Unit switch { "N" => "kN", "MPa" => "kPa", _ => result.Unit };
        return new(result.CaseId, result.StageId, result.ElementId, result.Method, result.Reference, result.Source, unit,
            result.Demand / unitScale, result.Capacity / unitScale, result.Utilization, result.Outcome, result.Diagnostic,
            new ReadOnlyDictionary<string, double>(result.Coefficients.ToDictionary(p => p.Key, p => p.Value)), values.AsReadOnly(),
            Array.AsReadOnly(result.Actions.Select(a => new EmbeddedWallHydraulicActionData(a.Id, a.Category.ToString(), a.CharacteristicUpwardForce / NewtonsPerKilonewton,
                a.Factor, a.DesignUpwardForce / NewtonsPerKilonewton, a.Source)).ToArray()));
    }
    private static double Required(JsonObject row, string key) => Optional(row, key) ?? throw new MissingInput("Dato obbligatorio non assegnato: " + key);
    private static SeepageResult Field(IReadOnlyDictionary<string, SeepageResult> fields, string stage)
        => fields.TryGetValue(stage, out var field) && field.IsComplete ? field : throw new MissingInput("Campo FEM convergente non disponibile per la fase " + stage + ".");
    private static T Sample<T>(Func<T> sample)
    {
        try { return sample(); }
        catch (ArgumentException e) { throw new MissingInput("Campionamento idraulico non disponibile: " + e.Message); }
    }
    private sealed class MissingInput(string message) : Exception(message);
}
