using System.IO;
using System.Text.Json.Nodes;
using Anthea.Application.Modules;

namespace X.Desktop;

internal sealed partial class SheetEditor
{
    internal static async Task CheckF3Scheduling(string root, string output)
    {
        Directory.CreateDirectory(output); int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks++; }
        var fixture = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "tests/fixtures/f3/inputs/geo_palo_verticale--regressione-palo_storico_0.json")))!;
        var data = fixture["data"]!.AsObject();
        using var service = new GatedModuleService();
        using var editor = new SheetEditor(service.ModuleId, data, service);
        var first = editor.CalculateAsync(commitEdits: false);
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Check(editor.Busy, "Stato Busy non attivato nella vista");
        editor.generalForm.Set("diametro", "1.25");
        var last = editor.CalculateAsync(commitEdits: false);
        Check(editor.Result is null, "Risultato non invalidato dopo la modifica");
        Check(service.Calls == 1, "Motori verticali sovrapposti");
        service.Release();
        await Task.WhenAll(first, last).WaitAsync(TimeSpan.FromSeconds(20));
        Check(!editor.Busy && editor.Result is not null, "Ultimo calcolo non disponibile");
        Check(editor.Result!.D("test_diametro_snapshot") == 1.25, "Pubblicato il risultato di una geometria precedente");
        Check(service.Calls == 2, "Richiesta automatica duplicata dopo il calcolo esplicito");
        service.Arm();
        editor.generalForm.Set("diametro", "1.35");
        var closing = editor.CalculateAsync(commitEdits: false);
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(15));
        editor.Dispose(); service.Release();
        await closing.WaitAsync(TimeSpan.FromSeconds(20));
        Check(editor.Result is null, "Vista chiusa aggiornata da un calcolo completato tardi");
        Check(!editor.Busy && service.Calls == 3, "Chiusura lascia un lavoro in coda");
        Check(data["generali"].D("diametro") != 1.35, "Vista ha modificato il documento di ingresso");
        checks += await BridgeWorkspace.CheckF3Scheduling();
        checks += await RetainingWallWorkspace.CheckF3Scheduling();
        File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {checks} controlli WPF ricalcolo e chiusura");
    }
}

internal sealed class GatedModuleService : IModuleService, IDisposable
    {
        private readonly IModuleService inner;
        public GatedModuleService(string module = "geo_palo_verticale") => inner = ModuleServices.Get(module);
        private readonly ManualResetEventSlim release = new(false);
        private volatile bool holding = true;
        private int calls;
        public int Calls => Volatile.Read(ref calls);
        public TaskCompletionSource Started { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string ModuleId => inner.ModuleId;
        public JsonObject Prepare(JsonObject source) => inner.Prepare(source);
        public void Arm() { release.Reset(); Started = new(TaskCreationOptions.RunContinuationsAsynchronously); holding = true; }
        public void Release() { holding = false; release.Set(); }
        public ModuleCalculation Calculate(JsonObject source, CancellationToken cancellation = default)
        {
            Interlocked.Increment(ref calls);
            if (holding)
            {
                Started.TrySetResult();
                if (!release.Wait(TimeSpan.FromSeconds(20))) throw new TimeoutException("Barriera del test non rilasciata.");
            }
            // Deliberately emulate an engine which completes before observing cancellation.
            var result = inner.Calculate(source);
            if (ModuleId == "geo_palo_verticale") result.Values["test_diametro_snapshot"] = source["generali"].D("diametro");
            return result;
        }
        public void Dispose() => release.Dispose();
    }
