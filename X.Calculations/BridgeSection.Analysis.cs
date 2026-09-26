using System.Text.Json.Nodes;
using GPC.Checkers.CompositeBridge.History;
namespace Anthea.Calculations;

public static partial class BridgeSection
{
    // Archive boundary only: all numerical work is performed in GPCChecker.CompositeBridge.
    public static BridgeResult Calculate(JsonObject data, CancellationToken cancellation = default)
    {
        var input = ToCheckerInput(data);
        string method = data.S("metodo_analisi", CalculationMethods[0]);
        if (!CalculationMethods.Contains(method)) throw new ArgumentException("Metodo di analisi sconosciuto.");
        var result = method == CalculationMethods[0] ? HBridgeSection.Calculate(input, cancellation) :
            HBridgeHistoryResults.Calculate(method == CalculationMethods[2] ? input with { Options = input.Options with { Class4 = false } } : input,
                HistoryOptions(data), cancellation);
        return new(result.Method, result.Scope, (JsonObject)data.DeepClone(), result.Geometry, result.Materials, result.Stages);
    }
    public static readonly string[] CalculationMethods = ["Cumulativo · metodo precedente", "Storico lineare", "Storico non lineare"];
    public static readonly string[] NonlinearCreepModes = ["Istantaneo · φ = 0 nel calcolo", "Richiedi φ = 0 negli ingressi"];
    public static bool IsNonlinear(JsonObject data) => data.S("metodo_analisi") == CalculationMethods[2];
    public static bool IsHistory(JsonObject data) => data.S("metodo_analisi", CalculationMethods[0]) != CalculationMethods[0];
    public static HBridgeHistoryOptions HistoryOptions(JsonObject data)
    {
        int Integer(string key, int value, int maximum)
        {
            double number = data.ContainsKey(key) ? data.Required(key, strict: true) : value;
            if (number != Math.Truncate(number) || number > maximum) throw new ArgumentException(key + ": inserire un intero da 1 a " + maximum + ".");
            return (int)number;
        }
        string creep = data.S("viscosita_nl", NonlinearCreepModes[0]);
        if (!NonlinearCreepModes.Contains(creep)) throw new ArgumentException("Opzione viscosità non lineare sconosciuta.");
        return new() { MaterialMode = IsNonlinear(data) ? HistoryMaterialMode.Nonlinear : HistoryMaterialMode.Linear,
            InstantaneousConcrete = IsNonlinear(data) && creep == NonlinearCreepModes[0],
            WebLayers = Integer("fibre_anima", 160, 10000), FlangeLayers = Integer("fibre_flange", 8, 1000),
            ConcreteLayers = Integer("fibre_cls", 64, 10000), SubstepsPerPhase = Integer("sottopassi", 8, 1000) };
    }
    // Geometry queries never depend on how many load increments have been entered.
    private static HBridgeInput GeometryInput(JsonObject data) => ToCheckerInput(data) with { Phases = [new BridgePhase()] };
}
