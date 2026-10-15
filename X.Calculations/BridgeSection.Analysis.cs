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
        if (method == LegacyCumulativeMethod) method = CumulativeLinearMethod;
        if (!CalculationMethods.Contains(method)) throw new ArgumentException("Metodo di analisi sconosciuto.");
        var result = method == CalculationMethods[0] ? HBridgeSection.Calculate(input, cancellation) :
            HBridgeHistoryResults.Calculate(method == CalculationMethods[2] ? input with { Options = input.Options with { Class4 = false } } : input,
                HistoryOptions(data), cancellation);
        // The result carries the library method (model, standard and version) for reports and audits; the short label
        // chosen in the sheet stays in data["metodo_analisi"].
        return new(result.Method, result.Scope, (JsonObject)data.DeepClone(), result.Geometry, result.Materials, result.Stages);
    }
    public const string LegacyCumulativeMethod = "Cumulativo · metodo precedente", CumulativeLinearMethod = "Cumulativo lineare";
    public static readonly string[] CalculationMethods = [CumulativeLinearMethod, "Storico lineare", "Storico non lineare"];
    public static readonly string[] NonlinearCreepModes = ["Istantaneo · φ = 0 nel calcolo", "Richiedi φ = 0 negli ingressi"];
    /// <summary>
    /// PON-2: the concrete in tension of the nonlinear history and of the M–κ / N–ε curves (HBridgeHistoryOptions.ConcreteTension of
    /// GPCChecker.CompositeBridge): «Trascurato» (default, also for the sheets saved before 16/10/2026) or «Tension stiffening».
    /// </summary>
    public static readonly string[] ConcreteTensionModes = ["Trascurato", "Tension stiffening"];
    public const string ConcreteTensionHelp = "Legame del calcestruzzo teso nello «Storico non lineare» e nelle curve M–κ e N–ε. "
        + "Trascurato (predefinito): il CLS non resiste a trazione (EN 1992-1-1 §6.1(2)P, sezione fessurata di EN 1994-2), con tensione nulla per ogni deformazione di trazione, anche prima della fessurazione: a favore di sicurezza per acciaio e barre. "
        + "Tension stiffening: CLS elastico fino a fctm, poi tensione media del calcestruzzo fessurato σ = fctm/(1 + √(500 ε)) (Collins e Mitchell 1991, MCFT); alla fessurazione la tensione scende a circa 0,82 fctm. "
        + "Il contributo del CLS teso è sovrastimato oltre lo snervamento delle barre: il controllo della tensione alla fessura della MCFT non è applicato. Storico lineare e metodo cumulativo non usano l'opzione.";
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
        string tension = data.S("cls_teso_nl", ConcreteTensionModes[0]);
        if (!ConcreteTensionModes.Contains(tension)) throw new ArgumentException("CLS teso (non lineare): scegliere «Trascurato» o «Tension stiffening».");
        return new() { MaterialMode = IsNonlinear(data) ? HistoryMaterialMode.Nonlinear : HistoryMaterialMode.Linear,
            InstantaneousConcrete = IsNonlinear(data) && creep == NonlinearCreepModes[0],
            ConcreteTension = tension == ConcreteTensionModes[1] ? HistoryConcreteTension.TensionStiffening : HistoryConcreteTension.Neglected,
            WebLayers = Integer("fibre_anima", 160, 10000), FlangeLayers = Integer("fibre_flange", 8, 1000),
            ConcreteLayers = Integer("fibre_cls", 64, 10000), SubstepsPerPhase = Integer("sottopassi", 8, 1000) };
    }
    // Geometry queries never depend on how many load increments have been entered.
    internal static HBridgeInput GeometryInput(JsonObject data) => ToCheckerInput(data) with { Phases = [new BridgePhase()] };
}
