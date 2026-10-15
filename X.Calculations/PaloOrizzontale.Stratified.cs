using System.Text.Json.Nodes;
using GPC.Checkers.Geotechnics.Piles;

namespace Anthea.Calculations;

public static partial class PaloOrizzontale
{
    public const string BromsMethod = "Broms";
    public const string StratifiedMethod = "Stratificato";
    private const string LegacyStratifiedMethod = "Stratificato (PileChecker)";
    public static string NormalizeMethod(string method) => method == LegacyStratifiedMethod ? StratifiedMethod : method;
    public const string StratifiedSource = LateralPileCapacity.StratifiedSource;

    private static bool DistributedMethod(JsonNode general) => NormalizeMethod(general.S("metodo_calcolo", BromsMethod)) switch
    {
        BromsMethod => false,
        StratifiedMethod => true,
        _ => throw new ArgumentException("Metodo di calcolo orizzontale non riconosciuto.")
    };
}
