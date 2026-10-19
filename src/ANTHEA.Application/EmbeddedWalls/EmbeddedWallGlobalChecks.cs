using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using Anthea.Reports;
using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Checkers.Geotechnics.Seepage;
using GPC.Checkers.Geotechnics.Slopes;
using GPC.Checkers.Geotechnics.Walls;
using GPC.Model.Checking.Contracts;
using GPC.Model.Geotechnics;
using GPC.Model.Geotechnics.EmbeddedWalls;
using GPC.Model.Standards;
using static Anthea.Application.EmbeddedWalls.EmbeddedWallAdapter;

namespace Anthea.Application.EmbeddedWalls;

/// <summary>Document mapping and orchestration; geometry, factors, solution and verdict come from GPC.</summary>
internal static class EmbeddedWallGlobalChecks
{
    internal static IReadOnlyList<EmbeddedWallGlobalData> Calculate(JsonObject data, EmbeddedWallModel model,
        IReadOnlyDictionary<string, SeepageResult> fields, CancellationToken cancellation, Action<EmbeddedWallProgress> progress)
    {
        var rows = EmbeddedWallDocument.Rows(data, "global_checks").ToArray();
        var actions = EmbeddedWallDocument.Rows(data, "global_actions").ToArray();
        var ids = new HashSet<(string, string)>();
        foreach (var row in rows)
            if (string.IsNullOrWhiteSpace(Text(row, "id")) || !model.Stages.Any(s => s.Id == Text(row, "stage")) || !ids.Add((Text(row, "stage"), Text(row, "id"))))
                throw new ArgumentException("Stabilità globale: fase esistente e identificativo univoco nella fase richiesti.");
        foreach (var row in actions)
            if (!ids.Contains((Text(row, "stage"), Text(row, "check")))) throw new ArgumentException("Azione riferita a una verifica globale inesistente.");
        var output = new List<EmbeddedWallGlobalData>();
        foreach (var row in rows)
        {
            cancellation.ThrowIfCancellationRequested();
            string id = Text(row, "id"), stage = Text(row, "stage");
            progress(new("Stabilità globale · " + stage + " · " + id, null, 0));
            cancellation.ThrowIfCancellationRequested();
            try
            {
                if (Text(row, "standard") != "NTC 2018" || Text(row, "situation") != "PersistentTransient")
                    throw new NotSupportedException("Adattatore globale disponibile per NTC 2018, situazione persistente/transitoria. Profilo richiesto non implementato; nessun coefficiente sostitutivo.");
                double Metres(string key) => Number(row, key) * SoilUnits.Metre;
                var search = new SlopeSearch(Metres("exit_min"), Metres("exit_max"), Metres("entry_min"), Metres("entry_max"),
                    Metres("depth_min"), Metres("depth_max"), Integer(row, "grid"), Integer(row, "slices"), Integer(row, "refinements"));
                var profile = new EmbeddedWallGlobalProfile(Metres("global_thickness"), Metres("left_extent"), Metres("right_extent"),
                    Integer(row, "valley_direction"), Flag(row, "characteristic_confirmed"), Text(row, "source"), search);
                ISlopeHydraulicField? field = null;
                if (Text(data, "hydraulic_mode") == "FEM 2D")
                {
                    if (!fields.TryGetValue(stage, out var fem)) throw new NotSupportedException("Campo FEM convergente assente per questa fase.");
                    field = new SeepageSlopeField(fem, "Filtrazione della fase " + stage, originZ: model.Top,
                        xDirection: -profile.ValleyDirection, ignoreSuction: Flag(row, "ignore_suction"));
                }
                var prepared = EmbeddedWallGlobalStability.Prepare(model, stage, profile, field);
                var combination = new StandardNTC2018Geotechnics().Combinations(GeotechnicalCheck.GlobalStability, GeotechnicalSituation.PersistentTransient).Single();
                var selections = actions.Where(a => Text(a, "stage") == stage && Text(a, "check") == id).Select(a => new GeotechnicalActionSelection(
                    Text(a, "id"), Choice<GeotechnicalCharacteristicActionCategory>(a, "category"), Flag(a, "unfavourable"), Number(a, "psi"), Text(a, "source"))).ToArray();
                var globalCase = EmbeddedWallGlobalStability.CreateCase(prepared, combination, selections, Flag(row, "undrained"));
                var result = EmbeddedWallGlobalStability.Calculate(prepared, [globalCase.Factors], cancellation);
                // This existing GPC operation classifies the generic slope result; it does not introduce gravity-wall factors.
                var check = WallGlobalStability.Checks(result).Single(); var c = result.Cases.Single(); var critical = c.Critical;
                var factors = c.Factors;
                var coefficients = new ReadOnlyDictionary<string, double>(new Dictionary<string, double> {
                    ["gammaR"] = factors.ResistanceFactor, ["gammaM_phi"] = factors.TanFrictionAngle, ["gammaM_c"] = factors.EffectiveCohesion,
                    ["gammaM_cu"] = factors.UndrainedShearStrength, ["soilWeightFactor"] = factors.SoilWeight, ["bodyWeightFactor"] = factors.BodyWeight });
                EmbeddedWallGlobalPoint Point(SlopePoint p) => new(p.X / SoilUnits.Metre, p.Y / SoilUnits.Metre);
                output.Add(new(id, model.CaseId, stage, factors.Name, c.Status.ToString(), check.Outcome, globalCase.Reference, prepared.Source,
                    string.Join(" ", result.Notes) + " " + StatusText(c.Status), check.Demand, check.Resistance, check.Ratio, c.Ratio, c.RefinedFactor,
                    c.Tried, c.GeometricallyValid, c.Solved, c.NumericalFailures, c.Boundary, critical?.Residual, coefficients,
                    Array.AsReadOnly(globalCase.Actions.Select(a => new EmbeddedWallGlobalActionData(a.Selection.Id, a.Selection.Category.ToString(), a.Selection.Unfavourable,
                        a.PartialFactor, a.Selection.CombinationFactor, a.CombinedFactor, a.Selection.Source)).ToArray()),
                    Array.AsReadOnly(prepared.Section.Surface.Select(Point).ToArray()),
                    Array.AsReadOnly(prepared.Section.Bodies.Select(b => new EmbeddedWallGlobalBody(b.Name, Array.AsReadOnly(b.Polygon.Select(Point).ToArray()))).ToArray()),
                    critical is null ? null : new(critical.Circle.X / SoilUnits.Metre, critical.Circle.Y / SoilUnits.Metre, critical.Circle.Radius / SoilUnits.Metre,
                        critical.Circle.Left / SoilUnits.Metre, critical.Circle.Right / SoilUnits.Metre),
                    Array.AsReadOnly((critical?.Slices ?? []).Select(s => new EmbeddedWallGlobalSlice(s.Index, s.Left / SoilUnits.Metre, s.Right / SoilUnits.Metre,
                        s.BaseY / SoilUnits.Metre, s.TopY / SoilUnits.Metre, s.Soil, s.SoilWeight, s.BodyWeight, s.VerticalLoad, s.HorizontalLoad,
                        s.PorePressure / SoilUnits.KiloPascal, s.NormalEffective, s.Resistance, s.Mobilized)).ToArray())));
            }
            catch (Exception e) when (e is ArgumentException or NotSupportedException)
            {
                output.Add(new(id, model.CaseId, stage, "", "Unavailable", EngineeringOutcome.NotEvaluated, "", Text(row, "source"), e.Message,
                    null, null, null, null, null, 0, 0, 0, 0, false, null,
                    new ReadOnlyDictionary<string, double>(new Dictionary<string, double>()), [], [], [], null, []));
            }
        }
        return output.AsReadOnly();
    }
    private static string StatusText(SlopeSearchStatus status) => status switch
    {
        SlopeSearchStatus.Converged => "Ricerca convergente nel dominio assegnato.",
        SlopeSearchStatus.BoundaryMinimum => "Minimo al bordo: estendere il dominio. Un utilizzo inferiore a uno non conclude la verifica.",
        SlopeSearchStatus.NotConverged => "Discretizzazione non convergente: rivedere numero di conci e dominio di ricerca.",
        SlopeSearchStatus.Incomplete => "Ricerca incompleta: alcune superfici non hanno una soluzione di equilibrio.",
        _ => "Nessuna superficie risolta: rivedere geometria, stratigrafia e dominio."
    };
}
