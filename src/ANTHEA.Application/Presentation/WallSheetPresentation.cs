using System.Globalization;
using System.Text.Json.Nodes;
namespace Anthea.Application.Presentation;

public static class WallSheetPresentation
{
    public static JsonArray ParseHistory(string text)
    {
        var samples = new JsonArray(); int line = 0;
        foreach (var raw in text.Split('\n'))
        {
            line++; string row = raw.Trim(); if (row == "" || row.StartsWith('#')) continue;
            var fields = row.Split([';', '\t'], StringSplitOptions.TrimEntries);
            if (fields.Length != 2 || !double.TryParse(fields[0].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double t)
                || !double.TryParse(fields[1].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double a))
            { if (samples.Count == 0 && row.StartsWith("t", StringComparison.OrdinalIgnoreCase)) continue; throw new ArgumentException($"Riga {line}: usare due colonne separate da ; (secondi; g)."); }
            if (!double.IsFinite(t + a) || t < 0 || Math.Abs(a) > 10 || samples.Count > 0 && t <= samples[^1].D("t")) throw new ArgumentException($"Riga {line}: tempi crescenti e accelerazioni finite richiesti.");
            if (samples.Count >= 200000) throw new ArgumentException("Massimo 200000 campioni.");
            samples.Add(J.Obj(("t", t), ("a_g", a)));
        }
        if (samples.Count < 2) throw new ArgumentException("Inserire almeno due campioni.");
        return samples;
    }

    public static double SeismicAngleDegrees(double kh, double kv)
        => GPC.Checkers.Geotechnics.Walls.EarthPressure.SeismicAngle(kh, kv) * 180 / Math.PI;
    public static (string Text, bool Valid) DescribeColumnDepths(JsonObject data, IEnumerable<JsonObject> valley, IEnumerable<JsonObject> mountain)
    {
        double ht = data["geometry"].D("height") + data["geometry"].D("slab"), dv = RetainingWall.ValleyHeight(data);
        var labels = new List<string>(); bool valid = true;
        foreach (var (rows, name, need) in new[] { (valley, "Valle", dv), (mountain, "Monte", ht) })
        {
            double z = UpdateColumnDepths(rows); valid &= z + 1e-9 >= need;
            labels.Add($"{name}: {Number(z)} m / {Number(need)} m richiesti");
        }
        return (string.Join(" · ", labels) + $"\nHlib={Number(ht - dv)} m · terreno di valle Dv={Number(dv)} m sopra il piano di posa", valid);
    }
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
    public static (bool GlobalUpdated, string? GlobalError, bool LocalUpdated) ApplyResult(JsonObject data, Anthea.Application.Walls.WallModel.Result result)
        => ApplyResult(data, result.Source);
    internal static (bool GlobalUpdated, string? GlobalError, bool LocalUpdated) ApplyResult(JsonObject data, RetainingWall.Result result)
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
