using Anthea.Calculations;
using System.Text.Json.Nodes;

internal static class ExplorationChecks
{
    private sealed class Reporter(Action<BridgeConcept.OptimizationProgress> receive) : IProgress<BridgeConcept.OptimizationProgress>
    { public void Report(BridgeConcept.OptimizationProgress value) => receive(value); }
    internal static int Run(string output)
    {
        int count = 0;
        void Check(bool pass, string message) { count++; if (!pass) throw new Exception(message); }
        void Invalid(Action run) { try { run(); throw new Exception("Griglia invalida accettata"); } catch (ArgumentException) { count++; } }
        Check(BridgeConcept.OptimizationRange(100, 115, 10).SequenceEqual(new[] { 1d, 1.1, 1.15 }), "Endpoint griglia perso");
        Check(BridgeConcept.OptimizationRange(112.5, 112.5, 1).SequenceEqual(new[] { 1.125 }), "Intervallo puntuale errato");
        Check(BridgeConcept.OptimizationRange(100, 200, 10).Length == 11, "Numero valori griglia errato");
        Invalid(() => BridgeConcept.OptimizationRange(100, 200, 1));
        Invalid(() => BridgeConcept.OptimizationRange(115, 100, 5)); Invalid(() => BridgeConcept.OptimizationRange(99, 150, 25));
        Invalid(() => BridgeConcept.OptimizationRange(100, 150, 0)); Invalid(() => BridgeConcept.OptimizationRange(100, double.NaN, 5));
        var data = BridgeConcept.Defaults(); var input = data["input"]!;
        input["length"] = 120; input["spans"] = 3; input["height"] = 12; input["soil"] = "Roccia"; input["obstacle"] = "Nessuno";
        string before = data.ToJsonString();
        var options = new BridgeConcept.OptimizationOptions { KeepPier = true, KeepFoundation = true,
            DepthPercentMax = 120, DepthPercentStep = 10, Alternatives = 2 };
        var updates = new List<BridgeConcept.OptimizationProgress>();
        var r = BridgeConcept.Optimize(data, options, progress: new Reporter(updates.Add));
        Check(data.ToJsonString() == before, "Progress o ricerca mutano il progetto");
        Check(r.Trials.Length == r.Evaluated && r.Trials.Count(t => t.Admissible) == r.Admissible, "Traccia incompleta");
        Check(r.Trials.Select(t => t.Iteration).SequenceEqual(Enumerable.Range(1, r.Evaluated)), "Iterazioni duplicate o fuori sequenza");
        Check(updates.SelectMany(p => p.NewTrials).SequenceEqual(r.Trials), "Progress perde o duplica tentativi");
        Check(updates.Last().Evaluated == r.Evaluated && updates.Last().Admissible == r.Admissible, "Progress finale non aggiornato");
        Check(updates.All(p => p.Planned >= p.Evaluated), "Conteggio griglia sottostimato");
        Check(r.Solutions.Length > r.Candidates.Length && r.Candidates.Length == 2, "Top N limita la famiglia completa");
        Check(r.Solutions.Select(s => s.Rank).SequenceEqual(Enumerable.Range(1, r.Solutions.Length)), "Ranghi errati");
        Check(r.Solutions.Zip(r.Solutions.Skip(1)).All(p => p.First.Score <= p.Second.Score), "Famiglia non ordinata");
        for (int k = 0; k < r.Solutions.Length; k++)
        {
            var s = r.Solutions[k]; var t = s.Trial;
            Check(t.Admissible && r.Trials[t.Iteration - 1] == t, "Soluzione senza un tentativo ammesso");
            var calculated = BridgeConcept.Calculate(s.Data);
            Check(calculated.TotalCost == t.Cost && calculated.Carbon == t.Carbon && calculated.Depth == t.Depth, "Punto del grafico non riproducibile");
            Check(t.Continuous == s.Data["input"]!["continuous"]!.GetValue<bool>(), "Continuità nella traccia non coerente");
            Check(s.Pareto == !r.Solutions.Any(o => o.Trial.Cost <= t.Cost && o.Trial.Carbon <= t.Carbon && (o.Trial.Cost < t.Cost || o.Trial.Carbon < t.Carbon)), "Frontiera Pareto errata");
            if (k < r.Candidates.Length) Check(JsonNode.DeepEquals(s.Data, r.Candidates[k].Data), "Top N diverso dal prefisso della graduatoria completa");
        }
        foreach (var objective in Enum.GetValues<BridgeConcept.OptimizationObjective>())
        {
            var test = BridgeConcept.Optimize(data, options with { Objective = objective });
            double minC = test.Solutions.Min(s => s.Trial.Cost!.Value), minE = test.Solutions.Min(s => s.Trial.Carbon!.Value);
            double Score(BridgeConcept.OptimizationSolution s) => objective switch { BridgeConcept.OptimizationObjective.Cost => s.Trial.Cost!.Value,
                BridgeConcept.OptimizationObjective.Carbon => s.Trial.Carbon!.Value, _ => .5 * (s.Trial.Cost!.Value / Math.Max(1, minC) + s.Trial.Carbon!.Value / Math.Max(1e-9, minE)) };
            Check(test.Solutions.All(s => s.Score == Score(s)) && test.Solutions[0].Score == test.Solutions.Min(Score), "Ottimo o normalizzazione errati");
        }
        var narrow = options with { KeepFamily = true, KeepSpans = true, MinDepth = 3, MaxDepth = 4 };
        var restricted = BridgeConcept.Optimize(data, narrow);
        Check(restricted.Trials.Any(t => !t.Admissible), "Tentativi esclusi non tracciati");
        Check(restricted.Solutions.All(s => s.Trial.Depth >= 3 && BridgeConcept.Calculate(s.Data).PierDepth <= 4), "Range assoluto non rispettato");
        Invalid(() => BridgeConcept.Optimize(data, options with { MinDepth = 4, MaxDepth = 3 }));
        Invalid(() => BridgeConcept.Optimize(data, options with { KeepPier = false, KeepFoundation = false, KeepContinuity = false,
            MaxSpans = 30, DepthPercentMax = 200, DepthPercentStep = 10, PilePercentMax = 200, PilePercentStep = 10 }));
        using var cts = new CancellationTokenSource();
        try { BridgeConcept.Optimize(data, options, cts.Token, new Reporter(_ => cts.Cancel())); throw new Exception("Interruzione durante ricerca ignorata"); }
        catch (OperationCanceledException) { count++; }
        var empty = BridgeConcept.Optimize(data, options with { MaxDepth = .1 });
        Check(empty.Solutions.Length == 0 && empty.Trials.Length == empty.Evaluated && empty.Trials.All(t => !t.Admissible), "Traccia ricerca senza risultati errata");
        // Exact geometry grid: one family / layout / substructure, depth varied independently.
        var grid = BridgeConcept.Optimize(data, options with { KeepFamily = true, KeepSpans = true });
        var adopted = BridgeConcept.Calculate(data);
        var expected = new[] { adopted.Depth, Math.Ceiling(adopted.Depth * 1.1 * 20) / 20, Math.Ceiling(adopted.Depth * 1.2 * 20) / 20 }.Distinct().Order().ToArray();
        Check(grid.Trials.Select(t => t.Depth!.Value).Distinct().Order().SequenceEqual(expected), "Griglia delle altezze non corrisponde alle quote attese");
        File.WriteAllText(Path.Combine(output, "exploration.txt"), $"PASS: {count} controlli su griglie, traccia integrale, progress, ordinamento, Pareto, riproducibilità di ogni punto, N indipendente, range e cancellazione. {r.Evaluated} tentativi, {r.Solutions.Length} geometrie distinte.");
        return count;
    }
}
