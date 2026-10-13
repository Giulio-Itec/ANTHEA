using System.Globalization;
using System.Text.Json.Nodes;
namespace Anthea.Application.Presentation;

public static class WallSheetPresentation
{
    public static string Number(double value) => value.ToString("0.00", CultureInfo.GetCultureInfo("it-IT"));
    public static void PrepareGlobalSettings(JsonObject data)
    {
        var g = data["global_stability"]!.AsObject();
        g["valley_layers"] ??= new JsonArray(); g["soil_mode"] ??= "Profilo unico";
        g["soil_split_x"] ??= data["geometry"].D("toe") + data["geometry"].D("stem_base");
        g["search_mode"] ??= "Assegnata";
    }
    public static double UpdateColumnDepths(IEnumerable<JsonObject> layers)
    {
        double z = 0; int i = 0;
        foreach (var row in layers)
        {
            row["__index"] = ++i; row["__from"] = z.ToString("0.00", CultureInfo.GetCultureInfo("it-IT"));
            z += row.D("thickness"); row["__to"] = z.ToString("0.00", CultureInfo.GetCultureInfo("it-IT"));
        }
        return z;
    }
    public static void Prepare(JsonObject data)
    {
        RetainingWall.Upgrade(data); RetainingWall.CompleteSoilInput(data); PrepareGlobalSettings(data);
        if (data["global_stability"].S("search_mode") == "Automatica") RetainingWall.ProposeGlobalSearch(data);
        UpdateColumnDepths(data.Array("layers").OfType<JsonObject>());
        UpdateColumnDepths(data["valley"]!.Array("layers").OfType<JsonObject>());
    }
    public static (bool GlobalUpdated, string? GlobalError, bool LocalUpdated) ApplyResult(JsonObject data, RetainingWall.Result result)
    {
        bool globalUpdated = false; string? globalError = null;
        if (data["global_stability"].B("enabled") && data["global_stability"].S("combination_mode") == "Automatiche")
        {
            try { data["global_stability"]!["combinations"] = RetainingWall.GenerateGlobalCombinations(data); globalUpdated = true; }
            catch (ArgumentException ex) { globalError = ex.Message; }
        }
        bool localUpdated = data.S("combination_mode") == "Automatiche";
        if (localUpdated)
        { data["combinations"] = result.Input["combinations"]!.DeepClone(); data["combination_signature"] = RetainingWall.CombinationSignature(data); }
        return (globalUpdated, globalError, localUpdated);
    }
}
