using Anthea.Calculations;

/// <summary>F2.11: diagnostic scopes follow the execution flow without mixing independent calculations.
/// Decisione D3 del 15/10/2026: il motore legacy della durabilità si aggancia con LegacyDurabilityEngine.Use (tests/ANTHEA.Legacy) al
/// posto del parametro del motore dell'adattatore; senza aggancio l'adattatore usa la libreria.</summary>
internal static class AdapterProbeChecks
{
    const string Adapter = nameof(ConcreteDurabilityAdapter);

    public static void Run(Action<bool, string> check)
    {
        var outcomes = new List<(bool Passed, string Name)>();
        void Expect(bool condition, string name) => outcomes.Add((condition, name));

        using (var common = ServiceabilityProbe.Start())
        {
            using (LegacyDurabilityEngine.Use(DurabilityEngine.Legacy)) ConcreteDurabilityAdapter.Severity("XC1");
            ConcreteDurabilityAdapter.Severity("XC1");
            ServiceabilityProbe.Record("other", "operation", "Library");
            Expect(common.Entries.SequenceEqual(new[] {
                new ServiceabilityProbeEntry(Adapter, "Severity", "Legacy"),
                new ServiceabilityProbeEntry(Adapter, "Severity", "Library"),
                new ServiceabilityProbeEntry("other", "operation", "Library") }), "sonda comune: ordine e motori della durabilità");
            var snapshot = common.Entries;
            ConcreteDurabilityAdapter.MinimumFck("XC1");
            Expect(snapshot.Count == 3 && common.Entries.Count == 4, "letture diagnostiche come istantanee indipendenti");
        }

        using (var outer = ServiceabilityProbe.Start())
        {
            var specific = DurabilityProbe.Start();
            try
            {
                ConcreteDurabilityAdapter.Severity("XC1");
                ServiceabilityProbe.Record("other", "operation", "Library");
                Expect(specific.Entries.SequenceEqual(new[] { new DurabilityProbe.Entry(Adapter, "Severity", DurabilityEngine.Library) }),
                    "facciata tipizzata: filtra gli altri adattatori");
                Expect(outer.Entries.Count == 0, "sonda annidata: non mescola le chiamate con la precedente");
            }
            finally { DurabilityProbe.Stop(); }
            using (LegacyDurabilityEngine.Use(DurabilityEngine.Legacy)) ConcreteDurabilityAdapter.Severity("XC1");
            Expect(outer.Entries.Count == 1 && outer.Entries[0].Engine == "Legacy", "arresto: ripristina la sonda comune precedente");
            Expect(specific.Entries.Count == 1, "sonda arrestata: nessuna nuova voce");
            DurabilityProbe.Stop();
            ServiceabilityProbe.Record("other", "after-stop", "Library");
            Expect(outer.Entries.Count == 2, "arresto ripetuto: non chiude una sonda di un altro proprietario");
        }

        var first = DurabilityProbe.Start();
        try
        {
            using (LegacyDurabilityEngine.Use(DurabilityEngine.Legacy)) ConcreteDurabilityAdapter.Severity("XC1");
            var second = DurabilityProbe.Start();
            try { ConcreteDurabilityAdapter.MinimumFck("XC1"); }
            finally { DurabilityProbe.Stop(); }
            ConcreteDurabilityAdapter.Severity("XC1");
            Expect(first.Entries.Count == 2 && first.Entries.All(e => e.Operation == "Severity") && second.Entries.Count == 1,
                "due sonde durabilità annidate: ripristino e separazione");
            using (var general = ServiceabilityProbe.Start())
            {
                DurabilityProbe.Stop();
                ConcreteDurabilityAdapter.Severity("XC1");
                Expect(general.Entries.Count == 1 && first.Entries.Count == 2, "StopProbe non interrompe la sonda generale annidata");
            }
            ConcreteDurabilityAdapter.Severity("XC1");
            Expect(first.Entries.Count == 3, "Dispose ripristina la sonda tipizzata esterna");
        }
        finally { DurabilityProbe.Stop(); }

        using (var shared = ServiceabilityProbe.Start())
        {
            Task.Run(() => ConcreteDurabilityAdapter.Severity("XC1")).GetAwaiter().GetResult();
            Parallel.For(0, 16, _ => ConcreteDurabilityAdapter.MinimumFck("XC1"));
            Expect(shared.Entries.Count == 17, "contesto ereditato: Task.Run e Parallel registrano tutte le chiamate");
            var tasks = Enumerable.Range(0, 4).Select(index => Task.Run(async () =>
            {
                var own = DurabilityProbe.Start();
                try
                {
                    await Task.Yield();
                    var engine = index % 2 == 0 ? DurabilityEngine.Legacy : DurabilityEngine.Library;
                    using (LegacyDurabilityEngine.Use(engine)) for (int i = 0; i < 5; i++) ConcreteDurabilityAdapter.Severity("XC1");
                    return own.Entries.Count == 5 && own.Entries.All(e => e.Engine == engine);
                }
                finally { DurabilityProbe.Stop(); }
            })).ToArray();
            Expect(Task.WhenAll(tasks).GetAwaiter().GetResult().All(result => result), "sonde concorrenti: ciascun calcolo conserva solo le proprie voci");
            using (LegacyDurabilityEngine.Use(DurabilityEngine.Legacy)) ConcreteDurabilityAdapter.Severity("XC1");
            Expect(shared.Entries.Count == 18, "le sonde dei Task non sostituiscono o contaminano quella del chiamante");
        }

        using (var outer = ServiceabilityProbe.Start())
        {
            bool rejected = false;
            try
            {
                DurabilityProbe.Start();
                try { ConcreteDurabilityAdapter.Severity("invalid"); }
                finally { DurabilityProbe.Stop(); }
            }
            catch (ArgumentException) { rejected = true; }
            ConcreteDurabilityAdapter.Severity("XC1");
            Expect(rejected && outer.Entries.Count == 1, "rifiuto di un ingresso: il finally ripristina la sonda precedente");
        }

        var ended = ServiceabilityProbe.Start();
        ended.Dispose();
        ConcreteDurabilityAdapter.Severity("XC1");
        ServiceabilityProbe.Record("other", "disabled", "Library");
        Expect(ended.Entries.Count == 0, "diagnostica disattivata: nessuna registrazione");
        Console.WriteLine($"Sonda comune: {outcomes.Count(x => x.Passed)} PASS, {outcomes.Count(x => !x.Passed)} FAIL");
        foreach (var result in outcomes.Where(x => !x.Passed)) Console.WriteLine("FAIL sonda: " + result.Name);
        foreach (var result in outcomes) check(result.Passed, "sonda comune: " + result.Name);
    }
}
