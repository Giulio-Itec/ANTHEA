using System.Globalization;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using GPC.Model.Materials;

namespace Anthea.Application.Materials;

public sealed record RheologyPoint(double AgeDays, double CreepCoefficient, double EcmMpa, double ModulusFactor);

/// <summary>Adapter to the existing GPC law. Sampling is copied from the support window; it introduces no material formula.</summary>
public sealed class RheologyEvaluation
{
    private readonly ConcreteTimeEffects native;
    public RheologyEvaluation(double fck, string cement, double humidity, double size, double loadingAge)
    {
        if (!Enum.TryParse<ConcreteMaterialEuropeanCommon.CementTypes>(cement, out var kind) || !Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(cement));
        native = new(fck, kind, humidity, size, loadingAge);
    }
    public double CreepInfinity => native.CreepInfinity;
    public double Ecm28Mpa => native.Ecm28Mpa;
    public double CreepReferenceModulusMpa => native.CreepReferenceModulusMpa;
    public double LoadingAgeDays => native.LoadingAgeDays;
    public RheologyPoint AtAge(double age)
    {
        var p = native.AtAge(age);
        return new(p.AgeDays, p.CreepCoefficient, p.EcmMpa, p.ModulusFactor);
    }
    public IReadOnlyList<RheologyPoint> Sample(double first, double last, double count)
    {
        if (!double.IsFinite(first) || first <= 0 || !double.IsFinite(last) || last <= first)
            throw new ArgumentException("Intervallo delle età non valido.");
        if (!double.IsFinite(count) || count != Math.Truncate(count) || count < 20 || count > 1000)
            throw new ArgumentException("Campionamento: numero intero fra 20 e 1000.");
        double t0 = LoadingAgeDays;
        var ages = new SortedSet<double> { first, last };
        for (int i = 0; i < (int)count; i++)
        {
            double p = i / (count - 1);
            ages.Add(Math.Clamp(Math.Exp(Math.Log(first) * (1 - p) + Math.Log(last) * p), first, last));
            if (last > t0)
            {
                double elapsed = Math.Exp(Math.Log(1e-4) * (1 - p) + Math.Log(Math.Max(1e-4, last - t0)) * p);
                double age = t0 + elapsed;
                if (age >= first && age <= last) ages.Add(age);
            }
        }
        foreach (double age in new[] { t0, 28d }) if (age >= first && age <= last) ages.Add(age);
        var rows = ages.Select(AtAge).ToArray();
        if (rows.Any(p => !double.IsFinite(p.CreepCoefficient) || !double.IsFinite(p.EcmMpa) || !double.IsFinite(p.ModulusFactor)))
            throw new ArgumentException("L'intervallo scelto non produce valori rappresentabili: ridurre le età estreme.");
        return rows;
    }
}

public static class RheologyDocument
{
    public const string Module = "mat_reologia";
    public const string Assumptions = "EN 1992-1-1:2004 · allegato B e §3.1.3. Calcestruzzo ordinario, 20 °C, umidità costante e viscosità lineare. Ecm(t) descrive la maturazione: non è il modulo efficace ridotto per viscosità. Ritiro e storia termica esclusi.";
    public static readonly ImmutableArray<string> TextKeys = ["Fck", "Humidity", "NotionalSize", "LoadingAge", "Cement", "StartAge", "EndAge", "Samples", "QueryAge", "SelectedCurve"];
    /// <summary>
    /// Options of the plot: consultation, as the tab or the case chosen in the Actions sheets (review of 2/11/2026, finding 12). The sheet reads
    /// them at opening and never writes them: changing them neither marks the document as modified nor adds a step to «Annulla». They are not
    /// in the results or in the report. The reading age (QueryAge) is a datum instead: the report prints the reading at that age.
    /// </summary>
    public static readonly ImmutableArray<string> ViewKeys = ["SelectedCurve", "LogX", "LogY"];
    public static JsonObject Create() => new()
    {
        ["rheology_schema"] = 1, ["Fck"] = "35", ["Humidity"] = "70", ["NotionalSize"] = "560", ["LoadingAge"] = "10", ["Cement"] = "ClassN",
        ["StartAge"] = "0,5", ["EndAge"] = "10000", ["Samples"] = "160", ["QueryAge"] = "365", ["SelectedCurve"] = "Creep", ["LogX"] = true, ["LogY"] = false
    };
    public static void Validate(JsonObject data)
    {
        if (data["rheology_schema"]?.GetValue<int>() != 1) throw new ArgumentException("Versione del foglio Reologia non supportata.");
        foreach (string key in TextKeys)
            if (data[key] is not JsonValue value || !value.TryGetValue<string>(out _)) throw new ArgumentException("Dato Reologia mancante o non testuale: " + key);
        foreach (string key in new[] { "LogX", "LogY" })
            if (data[key] is not JsonValue value || !value.TryGetValue<bool>(out _)) throw new ArgumentException("Opzione grafica mancante: " + key);
        if (data["Cement"]!.GetValue<string>() is not ("ClassS" or "ClassN" or "ClassR")) throw new ArgumentException("Classe di cemento non valida.");
        if (data["SelectedCurve"]!.GetValue<string>() is not ("Creep" or "Modulus" or "ModulusFactor")) throw new ArgumentException("Grandezza grafica non valida.");
    }
    private static double Number(JsonObject data, string key)
    {
        if (double.TryParse(data[key]!.GetValue<string>().Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && double.IsFinite(value)) return value;
        throw new ArgumentException(key + ": inserire un numero finito.");
    }
    public static JsonObject Calculate(JsonObject data)
    {
        Validate(data);
        var evaluation = new RheologyEvaluation(Number(data, "Fck"), data["Cement"]!.GetValue<string>(), Number(data, "Humidity"), Number(data, "NotionalSize"), Number(data, "LoadingAge"));
        var points = evaluation.Sample(Number(data, "StartAge"), Number(data, "EndAge"), Number(data, "Samples"));
        RheologyPoint? query = null;
        string queryError = "";
        if (double.TryParse(data["QueryAge"]!.GetValue<string>().Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double age) && double.IsFinite(age) && age > 0)
            query = evaluation.AtAge(age);
        else queryError = "Età di lettura: inserire un numero positivo e finito.";
        return new() { ["errore"] = "", ["CreepInfinity"] = evaluation.CreepInfinity, ["Ecm28Mpa"] = evaluation.Ecm28Mpa,
            ["CreepReferenceModulusMpa"] = evaluation.CreepReferenceModulusMpa, ["LoadingAgeDays"] = evaluation.LoadingAgeDays,
            ["Points"] = JsonSerializer.SerializeToNode(points), ["QueryPoint"] = JsonSerializer.SerializeToNode(query), ["QueryError"] = queryError };
    }
}

public sealed class RheologyModuleService : IModuleService
{
    public string ModuleId => RheologyDocument.Module;
    public JsonObject Prepare(JsonObject source) { RheologyDocument.Validate(source); return (JsonObject)source.DeepClone(); }
    public ModuleCalculation Calculate(JsonObject source, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested(); var data = Prepare(source);
        return new(ModuleId, data, RheologyDocument.Calculate(data));
    }
}
