using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using GPC.Design.Actions;
using Anthea.Application.Modules;

namespace Anthea.Application.Actions;

public sealed record SpectraTown(string Region, string Province, string Name, double Longitude, double Latitude)
{ public string Label => $"{Name} · {Province}"; }

/// <summary>Versioned input document and adapter. Engineering calculations belong exclusively to GPC.Design.Actions.</summary>
public static class SpectraDocument
{
    public const string Module = "act_spettri_ntc";
    public static IReadOnlyList<SpectraTown> Towns { get; } = Cslp103Data.Towns.Select(t => new SpectraTown(t.Region, t.Province, t.Name, t.Longitude, t.Latitude)).ToArray();
    public static JsonObject Create() => new()
    {
        ["schema"] = 1,
        ["longitude"] = 12.4818,
        ["latitude"] = 41.9109,
        ["location"] = "Roma (coordinate)",
        ["method"] = "Superficie rigata",
        ["nominal"] = 50,
        ["use"] = 1,
        ["soil"] = "A",
        ["topography"] = "T1",
        ["height"] = 0,
        ["damping"] = 5,
        ["mode"] = "Elastico",
        ["q0"] = 3,
        ["regular"] = true,
        ["qv"] = 1.5,
        ["isolation"] = false,
        ["mass"] = 412.6,
        ["structure_damping"] = 5,
        ["isolation_damping"] = 10,
        ["factor_sle"] = 1.5,
        ["factor_slu"] = 1,
        ["group1_name"] = "Gruppo 1",
        ["group1_count"] = 8,
        ["group1_k"] = 1.01,
        ["group2_name"] = "Gruppo 2",
        ["group2_count"] = 0,
        ["group2_k"] = 1.81,
        ["pendulum"] = false,
        ["normal_force"] = 6414.017857142857,
        ["friction"] = .055,
        ["displacement"] = 200,
        ["radius"] = 3100,
        ["assigned_hazard"] = false,
        ["slo_ag"] = .08,
        ["slo_f0"] = 2.5,
        ["slo_tc"] = .27,
        ["sld_ag"] = .1,
        ["sld_f0"] = 2.5,
        ["sld_tc"] = .28,
        ["slv_ag"] = .22,
        ["slv_f0"] = 2.4,
        ["slv_tc"] = .31,
        ["slc_ag"] = .24,
        ["slc_f0"] = 2.4,
        ["slc_tc"] = .32
    };
    public static double Number(JsonObject d, string key)
    {
        var text = d[key]?.ToString();
        if (double.TryParse(text?.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n)) return n;
        throw new ArgumentException($"{key}: inserire un numero finito.");
    }
    public static bool Flag(JsonObject d, string key) => d[key] is JsonValue v && v.TryGetValue<bool>(out var b) && b;
    public static string Text(JsonObject d, string key) => d[key]?.ToString() ?? "";
    public static void Validate(JsonObject d)
    {
        if (d["schema"]?.GetValue<int>() != 1) throw new ArgumentException("Versione del documento Spettri non supportata.");
        NtcSpectra.Validate(Options(d)); Number(d, "longitude"); Number(d, "latitude");
        if (Text(d, "method") is not ("Superficie rigata" or "Distanza inversa")) throw new ArgumentException("Metodo di interpolazione non valido.");
        if (Flag(d, "isolation")) IsolationSpectra.Validate(Isolation(d));
    }
    internal static SpectrumOptions Options(JsonObject d) => new(Number(d, "nominal"), Number(d, "use"), Text(d, "soil"),
        Text(d, "topography") switch { "T1" => 1, "T2" => 2, "T3" => 3, "T4" => 4, _ => 0 }, Number(d, "height"), Number(d, "damping"), Text(d, "mode") switch { "Elastico" => false, "Progetto con q" => true, _ => throw new ArgumentException("Tipo di spettro non valido.") }, Number(d, "q0"), Flag(d, "regular"), Number(d, "qv"));
    internal static IsolationOptions Isolation(JsonObject d)
    {
        int Count(string key) { double x = Number(d, key); if (x != Math.Truncate(x) || x < 0 || x > 100000) throw new ArgumentException("Il numero di isolatori deve essere un intero non negativo."); return (int)x; }
        return new(Number(d, "mass"), Number(d, "structure_damping"), Number(d, "isolation_damping"), Number(d, "factor_sle"), Number(d, "factor_slu"),
            [new(Text(d, "group1_name"), Count("group1_count"), Number(d, "group1_k")), new(Text(d, "group2_name"), Count("group2_count"), Number(d, "group2_k"))]);
    }
    public static JsonObject Calculate(JsonObject d, CancellationToken cancellation = default)
    {
        Validate(d); cancellation.ThrowIfCancellationRequested();
        var site = NtcSpectra.Site(Number(d, "longitude"), Number(d, "latitude"), Text(d, "method") == "Distanza inversa" ? SiteInterpolation.InverseDistance : SiteInterpolation.RuledSurface);
        var assigned = Flag(d, "assigned_hazard") ? new[] { "slo", "sld", "slv", "slc" }.Select(s => new HazardParameters(Number(d, s + "_ag"), Number(d, s + "_f0"), Number(d, s + "_tc"))).ToArray() : null;
        var states = NtcSpectra.Calculate(site, Options(d), assigned); cancellation.ThrowIfCancellationRequested();
        var json = new JsonObject { ["reference"] = NtcSpectra.ReferenceVersion, ["hazard_source"] = assigned is null ? "Reticolo CSLP" : "Parametri assegnati dall’utente", ["site"] = JsonSerializer.SerializeToNode(site), ["states"] = JsonSerializer.SerializeToNode(states) };
        if (Flag(d, "isolation")) json["isolation"] = JsonSerializer.SerializeToNode(IsolationSpectra.Calculate(states, Isolation(d)), new JsonSerializerOptions { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals });
        if (Flag(d, "pendulum")) json["pendulum"] = JsonSerializer.SerializeToNode(IsolationSpectra.Pendulum(Number(d, "normal_force"), Number(d, "friction"), Number(d, "displacement"), Number(d, "radius")));
        return json;
    }
    public static string Csv(JsonObject result)
    {
        var b = new System.Text.StringBuilder("Fase;Stato/TR;Componente;T [s];Sa [g]\r\n");
        void Add(string phase, string state, string component, JsonArray? points) { if (points is null) return; foreach (var p in points.OfType<JsonObject>()) b.AppendLine($"{phase};{state};{component};{Number(p, "PeriodSeconds").ToString("G17", CultureInfo.InvariantCulture)};{Number(p, "AccelerationG").ToString("G17", CultureInfo.InvariantCulture)}"); }
        foreach (var p in result["site"]!["Periods"]!.AsArray().OfType<JsonObject>()) { Add("1", Text(p, "ReturnYears"), "Parametrico", p["ReferenceSpectrum"]?.AsArray()); Add("1", Text(p, "ReturnYears"), "Tabulato", p["TabulatedSpectrum"]?.AsArray()); }
        foreach (var s in result["states"]!.AsArray().OfType<JsonObject>()) { Add("2", Text(s, "State"), "Riferimento", s["Reference"]?.AsArray()); Add("3", Text(s, "State"), "Orizzontale", s["Horizontal"]?.AsArray()); Add("3", Text(s, "State"), "Verticale", s["Vertical"]?.AsArray()); }
        foreach (var s in result["isolation"]?.AsArray().OfType<JsonObject>() ?? []) Add("Isolamento", Text(s, "State"), "Orizzontale", s["Spectrum"]?.AsArray());
        return b.ToString();
    }
}

public sealed class SpectraModuleService : IModuleService
{
    public string ModuleId => SpectraDocument.Module;
    public JsonObject Prepare(JsonObject source) { SpectraDocument.Validate(source); return (JsonObject)source.DeepClone(); }
    public ModuleCalculation Calculate(JsonObject source, CancellationToken cancellation = default) { var data = Prepare(source); return new(ModuleId, data, SpectraDocument.Calculate(data, cancellation)); }
}
