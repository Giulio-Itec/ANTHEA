using System.Reflection;
using System.Text.Json.Nodes;
using Anthea.Calculations;
using GPC.Checkers.Concrete.Cracking;

/// <summary>
/// Copertura della prova 5c (refactoring F2.7b, ciclo di prototipo, giro 1): codici, varianti (codice e flag), argomenti della traccia, motivi, esiti,
/// esiti per regione e rifiuti con codice dei risultati della libreria che la griglia confronta con il legacy, letti dalla sonda
/// (<see cref="ServiceabilityProbe.Results"/>) prima della mappatura. Quello che la griglia non raggiunge è provato solo dalla 5a (voci sintetiche):
/// l'elenco va nel resoconto, così il corpus B4 (A6) può coprirlo.
/// </summary>
sealed class LibraryCoverage
{
    readonly HashSet<string> codes = new(StringComparer.Ordinal), variants = new(StringComparer.Ordinal), flags = new(StringComparer.Ordinal),
        arguments = new(StringComparer.Ordinal), regions = new(StringComparer.Ordinal), rejections = new(StringComparer.Ordinal);
    readonly HashSet<CrackReason> reasons = new();
    readonly HashSet<CrackOutcome> outcomes = new(), regionOutcomes = new();
    public int Results, Entries, Refusals;

    /// <summary>Aggiunge i risultati e i rifiuti registrati dalla sonda; restituisce i risultati (per i controlli del chiamante).</summary>
    public IEnumerable<SectionCrackResult> Add(ServiceabilityProbe probe)
    {
        var found = new List<SectionCrackResult>();
        foreach (var item in probe.Results)
        {
            if (item is SectionCrackResult r)
            {
                Results++; found.Add(r);
                reasons.Add(r.Reason); outcomes.Add(r.Outcome);
                foreach (var o in r.RegionOutcomes) { regionOutcomes.Add(o.Outcome); regions.Add(RegionKind(o.Key)); }
                foreach (var e in r.Trace)
                {
                    Entries++; codes.Add(e.Code);
                    foreach (var flag in e.Flags) { flags.Add(flag); variants.Add(e.Code + "·" + flag); }
                    if (e.Flags.Count == 0) variants.Add(e.Code);
                    foreach (var argument in e.Arguments) arguments.Add(argument.Key);
                    if (e.Region is not null) regions.Add(RegionKind(e.Region));
                }
            }
            else if (item is ArgumentException a)
            {
                Refusals++;
                rejections.Add(CrackRejection.CodeOf(a) ?? (a is ArgumentOutOfRangeException range ? "parametro " + range.ParamName : "senza codice"));
            }
        }
        return found;
    }

    static string RegionKind(string key) => key.StartsWith("Radial(", StringComparison.Ordinal) ? "Radial" : key;

    static string[] Constants(Type type) => type.GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!).ToArray();

    public string[] MissingCodes => Constants(typeof(CrackTraceCodes)).Where(c => !codes.Contains(c)).ToArray();
    public string[] MissingFlags => Constants(typeof(CrackTraceFlags)).Where(c => !flags.Contains(c)).ToArray();
    public string[] MissingArguments => Constants(typeof(CrackTraceArguments)).Where(c => !arguments.Contains(c)).ToArray();
    public string[] MissingReasons => Enum.GetValues<CrackReason>().Where(r => !reasons.Contains(r)).Select(r => r.ToString()).ToArray();
    public string[] MissingOutcomes => Enum.GetValues<CrackOutcome>().Where(o => !outcomes.Contains(o)).Select(o => o.ToString()).ToArray();

    public JsonObject Json() => new()
    {
        ["risultati"] = Results, ["voci"] = Entries, ["rifiuti"] = Refusals,
        ["codici"] = codes.Count, ["codici_totali"] = Constants(typeof(CrackTraceCodes)).Length, ["varianti_codice_flag"] = variants.Count,
        ["flag"] = flags.Count, ["flag_totali"] = Constants(typeof(CrackTraceFlags)).Length,
        ["argomenti"] = arguments.Count, ["argomenti_totali"] = Constants(typeof(CrackTraceArguments)).Length,
        ["motivi"] = new JsonArray(reasons.OrderBy(r => r).Select(r => (JsonNode)r.ToString()).ToArray()),
        ["esiti"] = new JsonArray(outcomes.OrderBy(o => o).Select(o => (JsonNode)o.ToString()).ToArray()),
        ["esiti_per_regione"] = new JsonArray(regionOutcomes.OrderBy(o => o).Select(o => (JsonNode)o.ToString()).ToArray()),
        ["regioni"] = new JsonArray(regions.OrderBy(r => r, StringComparer.Ordinal).Select(r => (JsonNode)r).ToArray()),
        ["rifiuti_per_codice"] = new JsonArray(rejections.OrderBy(r => r, StringComparer.Ordinal).Select(r => (JsonNode)r).ToArray()),
        ["codici_non_raggiunti"] = new JsonArray(MissingCodes.Select(c => (JsonNode)c).ToArray()),
        ["flag_non_raggiunti"] = new JsonArray(MissingFlags.Select(c => (JsonNode)c).ToArray()),
        ["argomenti_non_raggiunti"] = new JsonArray(MissingArguments.Select(c => (JsonNode)c).ToArray()),
        ["motivi_non_raggiunti"] = new JsonArray(MissingReasons.Select(c => (JsonNode)c).ToArray()),
        ["esiti_non_raggiunti"] = new JsonArray(MissingOutcomes.Select(c => (JsonNode)c).ToArray())
    };

    public string Line() => $"copertura della 5c: {Results} risultati della libreria ({Entries} voci) e {Refusals} rifiuti; "
        + $"{codes.Count}/{Constants(typeof(CrackTraceCodes)).Length} codici, {variants.Count} varianti codice·flag, {flags.Count}/{Constants(typeof(CrackTraceFlags)).Length} flag, "
        + $"{arguments.Count}/{Constants(typeof(CrackTraceArguments)).Length} argomenti, motivi {string.Join("/", reasons.OrderBy(r => r))}, esiti {string.Join("/", outcomes.OrderBy(o => o))}, "
        + $"esiti per regione {string.Join("/", regionOutcomes.OrderBy(o => o))}, rifiuti {string.Join("/", rejections.OrderBy(r => r, StringComparer.Ordinal))}; "
        + $"non raggiunti (solo 5a): codici {List(MissingCodes)}; flag {List(MissingFlags)}; argomenti {List(MissingArguments)}; motivi {List(MissingReasons)}; esiti {List(MissingOutcomes)}";

    static string List(string[] items) => items.Length == 0 ? "nessuno" : string.Join(", ", items);
}
