using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Anthea.Calculations;

/// <summary>
/// F2.11c (decisione E5 del 15/10/2026): l'aggancio dei ripieghi di J è per contesto. Senza osservatore non si registra nulla; un
/// osservatore registrato vale per il flusso che lo registra e per i thread e le attività avviati da lì, non per gli altri; due contesti in
/// parallelo non si mescolano; i valori restituiti non cambiano. Con l'aggancio globale di prima la prova dei contesti paralleli fallisce.
/// </summary>
static class FallbackHookChecks
{
    public static int Run()
    {
        int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException("Aggancio dei ripieghi: " + message); checks++; }
        var empty = new JsonObject();
        Check(JsonFallbackHook.Observer is null && !JsonFallbackHook.Enabled, "osservatore presente all'avvio");
        Check(empty.S("assente", "x") == "x" && empty.D("assente", 2.5) == 2.5 && empty.B("assente", true), "ripieghi senza osservatore");

        // Due contesti in parallelo, ognuno con il suo osservatore registrato prima che l'altro calcoli: ogni osservatore vede solo le chiavi
        // del suo contesto, anche dai thread del Parallel.For avviato dentro il contesto.
        using var ready = new Barrier(2);
        var seen = new ConcurrentDictionary<string, ConcurrentBag<string>>();
        Task Context(string name) => Task.Run(() =>
        {
            var bag = seen.GetOrAdd(name, _ => new ConcurrentBag<string>());
            JsonFallbackHook.Observer = (kind, parent, key, fallback) => bag.Add(kind + ":" + key);
            ready.SignalAndWait();
            Parallel.For(0, 64, i =>
            {
                var node = new JsonObject();
                if (node.S(name + "-s" + i, name) != name || node.D(name + "-d" + i, i) != i || node.B(name + "-b" + i, i % 2 == 0) != (i % 2 == 0))
                    throw new InvalidOperationException("ripiego cambiato");
            });
            ready.SignalAndWait();
        });
        Task.WaitAll(Context("a"), Context("b"));
        foreach (string name in new[] { "a", "b" })
        {
            var keys = seen[name].ToArray();
            Check(keys.Length == 3 * 64, $"contesto {name}: {keys.Length} ripieghi registrati invece di {3 * 64}");
            Check(keys.All(k => k.Split(':')[1].StartsWith(name + "-", StringComparison.Ordinal)), $"contesto {name}: ripieghi di un altro contesto");
            Check(keys.Distinct().Count() == keys.Length, $"contesto {name}: ripieghi duplicati");
        }
        Check(JsonFallbackHook.Observer is null && !JsonFallbackHook.Enabled, "l'osservatore di un contesto è uscito dal contesto");

        // Il chiamante che registra l'osservatore lo vede nei calcoli avviati dopo (come JsonFallbackTrace.Start prima della cattura);
        // tolto l'osservatore, nulla si registra più.
        var outer = new ConcurrentBag<string>();
        JsonFallbackHook.Observer = (kind, parent, key, fallback) => outer.Add(key);
        Task.Run(() => empty.S("figlio")).Wait();
        empty.D("chiamante");
        JsonFallbackHook.Observer = null;
        empty.B("dopo");
        Check(outer.OrderBy(k => k, StringComparer.Ordinal).SequenceEqual(new[] { "chiamante", "figlio" }), "osservatore del chiamante: " + string.Join(", ", outer));
        return checks;
    }
}
