using System.Text.Json.Nodes;
using System.Globalization;
using GPC.Model.Standards;
namespace Anthea.Calculations;
public static partial class BridgeSection
{
    public static BridgeStandard RequiredStandard(JsonObject data) => data.S("normativa") switch
    {
        "NTC 2018 / EN 1994-2:2005" => BridgeStandard.Ntc2018,
        "EN 1994-2:2005" => BridgeStandard.Eurocode4,
        _ => throw new ArgumentException("Selezionare una normativa valida per la sezione composta.")
    };

    // Reuse material profiles; gamma M1 retains the supported bridge model's default.
    public static void ApplyStandardDefaults(JsonObject data)
    {
        bool ntc = RequiredStandard(data) == BridgeStandard.Ntc2018;
        var steel = ntc ? new StandardNTC2018Steel() : new StandardEN1993p11();
        var concrete = ntc ? new StandardNTC2018Concrete() : new StandardEN1992p11();
        data["gamma_m0"] = steel.GammaM0.ToString("0.0#", CultureInfo.InvariantCulture);
        data["gamma_m1"] = new BridgeAnalysisOptions().GammaM1.ToString("0.0#", CultureInfo.InvariantCulture);
        data["alpha_cc"] = concrete.AlphaCC.ToString("0.0#", CultureInfo.InvariantCulture);
    }
}
