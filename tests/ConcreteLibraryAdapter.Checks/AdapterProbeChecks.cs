using Anthea.Calculations;

/// <summary>F2.11: diagnostic scopes follow the execution flow without mixing independent calculations.</summary>
internal static class AdapterProbeChecks
{
    const string Adapter = nameof(ConcreteDurabilityAdapter);

    public static void Run(Action<bool, string> check)
    {
        var outcomes = new List<(bool Passed, string Name)>();
        void Expect(bool condition, string name) => outcomes.Add((condition, name));

        using (var common = ServiceabilityProbe.Start())
        {
            ConcreteDurabilityAdapter.Severity("XC1", DurabilityEngine.Legacy);
            ConcreteDurabilityAdapter.Severity("XC1", DurabilityEngine.Library);
            ServiceabilityProbe.Record("other", "operation", "Library");
            Expect(common.Entries.SequenceEqual(new[] {
                new ServiceabilityProbeEntry(Adapter, "Severity", "Legacy"),
                new ServiceabilityProbeEntry(Adapter, "Severity", "Library"),
                new ServiceabilityProbeEntry("other", "operation", "Library") }), "sonda comune: ordine e motori della durabilità");
            var snapshot = common.Entries;
            ConcreteDurabilityAdapter.MinimumFck("XC1", DurabilityEngine.Library);
            Expect(snapshot.Count == 3 && common.Entries.Count == 4, "letture diagnostiche come istantanee indipendenti");
        }

        using (var outer = ServiceabilityProbe.Start())
        {
            var specific = ConcreteDurabilityAdapter.StartProbe();
            try
            {
                ConcreteDurabilityAdapter.Severity("XC1", DurabilityEngine.Library);
                ServiceabilityProbe.Record("other", "operation", "Library");
                Expect(specific.Entries.SequenceEqual(new[] { new ConcreteDurabilityAdapter.ProbeEntry(Adapter, "Severity", DurabilityEngine.Library) }),
                    "facciata tipizzata: filtra gli altri adattatori");
                Expect(outer.Entries.Count == 0, "sonda annidata: non mescola le chiamate con la precedente");
            }
            finally { ConcreteDurabilityAdapter.StopProbe(); }
            ConcreteDurabilityAdapter.Severity("XC1", DurabilityEngine.Legacy);
            Expect(outer.Entries.Count == 1 && outer.Entries[0].Engine == "Legacy", "arresto: ripristina la sonda comune precedente");
            Expect(specific.Entries.Count == 1, "sonda arrestata: nessuna nuova voce");
            ConcreteDurabilityAdapter.StopProbe();
            ServiceabilityProbe.Record("other", "after-stop", "Library");
            Expect(outer.Entries.Count == 2, "arresto ripetuto: non chiude una sonda di un altro proprietario");
        }

        var first = ConcreteDurabilityAdapter.StartProbe();
        try
        {
            ConcreteDurabilityAdapter.Severity("XC1", DurabilityEngine.Legacy);
            var second = ConcreteDurabilityAdapter.StartProbe();
            try { ConcreteDurabilityAdapter.MinimumFck("XC1", DurabilityEngine.Library); }
            finally { ConcreteDurabilityAdapter.StopProbe(); }
            ConcreteDurabilityAdapter.Severity("XC1", DurabilityEngine.Library);
            Expect(first.Entries.Count == 2 && first.Entries.All(e => e.Operation == "Severity") && second.Entries.Count == 1,
                "due sonde durabilità annidate: ripristino e separazione");
            using (var general = ServiceabilityProbe.Start())
            {
                ConcreteDurabilityAdapter.StopProbe();
                ConcreteDurabilityAdapter.Severity("XC1", DurabilityEngine.Library);
                Expect(general.Entries.Count == 1 && first.Entries.Count == 2, "StopProbe non interrompe la sonda generale annidata");
            }
            ConcreteDurabilityAdapter.Severity("XC1", DurabilityEngine.Library);
            Expect(first.Entries.Count == 3, "Dispose ripristina la sonda tipizzata esterna");
        }
        finally { ConcreteDurabilityAdapter.StopProbe(); }

        using (var shared = ServiceabilityProbe.Start())
        {
            Task.Run(() => ConcreteDurabilityAdapter.Severity("XC1", DurabilityEngine.Library)).GetAwaiter().GetResult();
            Parallel.For(0, 16, _ => ConcreteDurabilityAdapter.MinimumFck("XC1", DurabilityEngine.Library));
            Expect(shared.Entries.Count == 17, "contesto ereditato: Task.Run e Parallel registrano tutte le chiamate");
            var tasks = Enumerable.Range(0, 4).Select(index => Task.Run(async () =>
            {
                var own = ConcreteDurabilityAdapter.StartProbe();
                try
                {
                    await Task.Yield();
                    var engine = index % 2 == 0 ? DurabilityEngine.Legacy : DurabilityEngine.Library;
                    for (int i = 0; i < 5; i++) ConcreteDurabilityAdapter.Severity("XC1", engine);
                    return own.Entries.Count == 5 && own.Entries.All(e => e.Engine == engine);
                }
                finally { ConcreteDurabilityAdapter.StopProbe(); }
            })).ToArray();
            Expect(Task.WhenAll(tasks).GetAwaiter().GetResult().All(result => result), "sonde concorrenti: ciascun calcolo conserva solo le proprie voci");
            ConcreteDurabilityAdapter.Severity("XC1", DurabilityEngine.Legacy);
            Expect(shared.Entries.Count == 18, "le sonde dei Task non sostituiscono o contaminano quella del chiamante");
        }

        using (var outer = ServiceabilityProbe.Start())
        {
            bool rejected = false;
            try
            {
                ConcreteDurabilityAdapter.StartProbe();
                try { ConcreteDurabilityAdapter.Severity("invalid", DurabilityEngine.Library); }
                finally { ConcreteDurabilityAdapter.StopProbe(); }
            }
            catch (ArgumentException) { rejected = true; }
            ConcreteDurabilityAdapter.Severity("XC1", DurabilityEngine.Library);
            Expect(rejected && outer.Entries.Count == 1, "rifiuto di un ingresso: il finally ripristina la sonda precedente");
        }

        var ended = ServiceabilityProbe.Start();
        ended.Dispose();
        ConcreteDurabilityAdapter.Severity("XC1", DurabilityEngine.Library);
        ServiceabilityProbe.Record("other", "disabled", "Library");
        Expect(ended.Entries.Count == 0, "diagnostica disattivata: nessuna registrazione");
        Console.WriteLine($"Sonda comune: {outcomes.Count(x => x.Passed)} PASS, {outcomes.Count(x => !x.Passed)} FAIL");
        foreach (var result in outcomes.Where(x => !x.Passed)) Console.WriteLine("FAIL sonda: " + result.Name);
        foreach (var result in outcomes) check(result.Passed, "sonda comune: " + result.Name);
    }
}
