using System.Collections.Concurrent;
using System.Text.Json.Nodes;

namespace Anthea.Calculations;

public sealed record StressOutcome(CheckerStressState? State, double? Ratio, string Status,
    string Cracking = "Da calcolare", Ntc2018Checks.CrackResult? CrackResult = null);
public sealed record ConcreteDomainResult(CheckerDomain3D? Three, CheckerDomain2D? Two, Dictionary<string, DomainCheck> Checks);

/// <summary>Numerical work and caches shared by WPF, diagnostics and project report calculations.
/// One session per sheet. Call a given domain/set once at a time, on input snapshots.</summary>
public sealed class ConcreteAnalysisSession
{
    public static JsonObject ExportStress(IReadOnlyDictionary<string, StressOutcome> rows)
    {
        var result = new JsonObject();
        foreach (var (id, outcome) in rows)
        {
            var s = outcome.State;
            result[id] = J.Node(new
            {
                State = s is null ? null : new
                {
                    s.sigma_cls,
                    s.sigma_acciaio,
                    s.tensioni_barre,
                    s.Response,
                    s.ConcreteStressLimit,
                    s.SteelStressLimit,
                    s.ConcreteVertices,
                    s.BarStrains
                },
                outcome.Ratio,
                outcome.Status,
                outcome.Cracking,
                outcome.CrackResult
            });
        }
        return result;
    }
    private readonly ConcurrentDictionary<string, (string Signature, CheckerDomain3D? Three, CheckerDomain2D? Two)> domainCache = new();
    private readonly ConcurrentDictionary<string, (string Signature, CheckerSection Engine, CheckerStressState State)> stressCache = new();
    public static readonly string[] CrackFields = ["esposizione", "sensibilita", "durata", "aderenza", "copriferro_fessure", "spaziatura_fessure"];
    public static ActionPoint ReadAction(JsonObject row) => new(
        SectionWorkspace.Number(row.S("N"), "N"), SectionWorkspace.Number(row.S("Mx"), "Mx"),
        SectionWorkspace.Number(row.S("My"), "My"));

    public ConcreteDomainResult Domain(JsonObject input, JsonObject workspace, JsonObject options, string key,
        bool threeD, JsonObject[] snapshots, CancellationToken token = default, CheckerSectionModel? prepared = null)
    {
        token.ThrowIfCancellationRequested();
        var computationOptions = (JsonObject)options.DeepClone();
        foreach (string visual in new[] { "stato", "filtro", "solo_selezionata", "trasparenza", "mostra_ed", "mostra_rd", "tutte_rd", "mostra_linee", "colora_eta", "criterio", "strategia", "proietta", "scala_x", "scala_y", "scala_n", "scala_mx", "scala_my", "fit_azioni", "dimensione_ed", "dimensione_rd", "interpolazione", "suddivisioni_n" }) computationOptions.Remove(visual);
        string signature = input.ToJsonString() + workspace.S("normativa") + workspace["coefficienti"]?.ToJsonString() + workspace["trefoli"]?.ToJsonString() + computationOptions.ToJsonString();
        var cached = domainCache.GetValueOrDefault((threeD ? "3D:" : "2D:") + key);

        CheckerDomain3D? three = cached.Signature == signature ? cached.Three : null;
        CheckerDomain2D? two = cached.Signature == signature ? cached.Two : null;
        if (three is null && two is null)
        {
            var engine = prepared is null ? new CheckerSection(input, workspace, options, key) : new CheckerSection(prepared, input, workspace, options, key);
            three = threeD ? engine.Domain3D(token) : null;
            two = threeD ? null : engine.Domain2D(token);
        }
        three?.ConfigureVerification(options);
        if (two is not null) two.Section.Options["proietta"] = options["proietta"]?.DeepClone();
        var results = new Dictionary<string, DomainCheck>();
        var valid = new List<(string Id, ActionPoint Force)>();
        foreach (var snapshot in snapshots)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var force = ReadAction(snapshot);
                if (three is not null) valid.Add((snapshot.S("id"), force));
                else results[snapshot.S("id")] = two!.Check(force);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { results[snapshot.S("id")] = new(null, null, "Checker: " + ex.Message); }
        }
        if (three is not null)
        {
            var checks = three.CheckMany(valid.Select(row => row.Force).ToArray(), token);
            for (int i = 0; i < valid.Count; i++) results[valid[i].Id] = checks[i];
        }
        token.ThrowIfCancellationRequested();
        domainCache[(threeD ? "3D:" : "2D:") + key] = (signature, three, two);
        return new(three, two, results);

    }

    public Dictionary<string, StressOutcome> Stress(JsonObject input, JsonObject workspace, JsonObject options, string key,
        JsonObject[] rows, CancellationToken token = default, CheckerSectionModel? prepared = null)
    {
        token.ThrowIfCancellationRequested();
        var requests = rows.Select(row => (Id: row.S("id"), Values: row)).ToArray();
        var activeIds = requests.Select(r => key + ":" + r.Id).ToHashSet();
        foreach (var id in stressCache.Keys.Where(id => id.StartsWith(key + ":") && !activeIds.Contains(id))) stressCache.TryRemove(id, out _);
        var analysisOptions = (JsonObject)options.DeepClone();
        foreach (var field in CrackFields.Concat(new[] { "contour", "testi_barre", "testi_trefoli", "testi_cls" })) analysisOptions.Remove(field);
        string analysisSignature = input.ToJsonString() + workspace.S("normativa") + workspace["coefficienti"]?.ToJsonString() + workspace["trefoli"]?.ToJsonString() + analysisOptions.ToJsonString();

        var results = new ConcurrentDictionary<string, StressOutcome>(); if (requests.Length == 0) return new Dictionary<string, StressOutcome>();
        Parallel.ForEach(requests, new ParallelOptions { CancellationToken = token, MaxDegreeOfParallelism = Math.Max(1, Math.Min(2, Environment.ProcessorCount / 3)) },
            () => (CheckerSection?)null, (request, loop, workerEngine) =>
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var force = ReadAction(request.Values);
                string cacheKey = key + ":" + request.Id, signature = analysisSignature + J.Node(force)?.ToJsonString();
                if (!stressCache.TryGetValue(cacheKey, out var cached) || cached.Signature != signature)
                {
                    workerEngine ??= prepared is null ? new CheckerSection(input, workspace, options) : new CheckerSection(prepared, input, workspace, options);
                    var calculated = workerEngine.Stress(force, key); token.ThrowIfCancellationRequested();
                    cached = (signature, workerEngine, calculated); stressCache[cacheKey] = cached;
                }
                var engine = cached.Engine; var state = cached.State;
                Ntc2018Checks.CrackResult crack;
                try { crack = workspace.S("normativa") == "NTC 2018" ? Ntc2018Checks.Cracking(engine, state, force, input, workspace, options, key) : new(null, null, null, null, "Fessurazione specifica " + workspace.S("normativa") + ": da implementare"); }
                catch (Exception ex) { crack = new(null, null, null, null, "Fessurazione non calcolata: " + ex.Message); }
                results[request.Id] = new(state, state.Ratio, state.Status, crack.Status, crack);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { results[request.Id] = new(null, null, "Checker: " + ex.Message); }
            return workerEngine;
        }, _ => { });
        return results.ToDictionary(p => p.Key, p => p.Value);

    }
}
