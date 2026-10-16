using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules;

namespace Anthea.Presentation.Tests;

/// <summary>
/// The synchronous calculation of a sheet (decision Q8 a, F5.4): <see cref="SheetCalculation{TResult}.CalculateNow"/> and
/// <see cref="SheetCalculation{TResult}.ScheduleNowAsync"/>, and <see cref="SheetViewModelBase"/> with
/// <see cref="SheetCalculationMode.Synchronous"/>. The engine runs on the "UI" thread (SingleThreadContext), never on a
/// background thread; the result is published before «Calcola» returns; the automatic recalculation runs after the delay on the
/// same thread; latest request wins; closing during the delay publishes nothing.
/// </summary>
internal static class SynchronousSheetChecks
{
    internal static int Run() => SingleThreadContext.Run(RunAsync);

    private static JsonObject Data(double value) => new() { ["valore"] = value };

    private static double Echo(JsonObject? values) => values!["eco"]!.GetValue<double>();

    private static async Task<int> RunAsync(SingleThreadContext ui)
    {
        var expect = new Expect();
        var service = new BarrierModuleService();

        // CalculateNow: run here, published before returning, Running then Completed.
        var calculation = new SheetCalculation<ModuleCalculation>(service.Calculate, SheetCalculation.Review, TimeSpan.FromHours(1));
        var states = new List<CalculationState>();
        int published = 0;
        calculation.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(calculation.State)) states.Add(calculation.State); };
        calculation.Published += (_, _) => published++;
        var data = Data(3);
        expect.That(calculation.CalculateNow(() => service.Calculate(data)) && published == 1 && Echo(calculation.Result!.Values) == 3,
            "Calcolo sincrono non pubblicato al ritorno");
        expect.That(states.SequenceEqual([CalculationState.Running, CalculationState.Completed]) && service.Threads.Last() == ui.ThreadId,
            "Calcolo sincrono: stati o thread");
        expect.That(calculation.CalculateNow(() => throw new ArgumentException("dati da completare")) && calculation.State == CalculationState.Failed
            && calculation.Message == "dati da completare" && !calculation.HasResult && published == 2, "Errore del calcolo sincrono");

        // The pending automatic recalculation is cancelled by CalculateNow and never runs.
        int runs = 0;
        ModuleCalculation Counted(double value) { runs++; return service.Calculate(Data(value)); }
        var pending = calculation.ScheduleNowAsync(() => Counted(4));
        expect.That(calculation.State == CalculationState.Pending, "Ricalcolo automatico sincrono non in attesa");
        expect.That(calculation.CalculateNow(() => Counted(5)) && !await Expect.Within(pending) && runs == 1
            && Echo(calculation.Result!.Values) == 5 && calculation.State == CalculationState.Completed, "Ricalcolo superato eseguito o pubblicato");
        calculation.Dispose();

        // After the delay, on the UI thread; latest request wins; Invalidate and Dispose cancel the wait.
        var automatic = new SheetCalculation<ModuleCalculation>(service.Calculate, SheetCalculation.Review, TimeSpan.FromMilliseconds(20));
        int thread = -1;
        runs = 0;
        expect.That(await Expect.Within(automatic.ScheduleNowAsync(() => { thread = Environment.CurrentManagedThreadId; return Counted(6); }))
            && thread == ui.ThreadId && Echo(automatic.Result!.Values) == 6, "Ricalcolo automatico fuori dal thread dell'interfaccia");
        var first = automatic.ScheduleNowAsync(() => Counted(7));
        var second = automatic.ScheduleNowAsync(() => Counted(8));
        expect.That(!await Expect.Within(first) && await Expect.Within(second) && runs == 2 && Echo(automatic.Result!.Values) == 8, "Vince l'ultima richiesta");
        var invalidated = automatic.ScheduleNowAsync(() => Counted(9));
        automatic.Invalidate();
        expect.That(!await Expect.Within(invalidated) && runs == 2 && automatic.State == CalculationState.Idle && !automatic.HasResult, "Invalidate non annulla l'attesa");
        // The calculation itself changes the data (an edit during the run): the superseded request publishes nothing.
        expect.That(!automatic.CalculateNow(() => { automatic.Invalidate(); return Counted(10); }) && !automatic.HasResult, "Richiesta superata durante il calcolo pubblicata");
        var closing = automatic.ScheduleNowAsync(() => Counted(11));
        automatic.Dispose();
        expect.That(!await Expect.Within(closing) && runs == 3, "Chiusura durante il ritardo: calcolo eseguito");
        expect.Throws<ObjectDisposedException>(() => automatic.CalculateNow(() => Counted(12)), "Calcolo sincrono dopo la chiusura");
        expect.Throws<ObjectDisposedException>(() => _ = automatic.ScheduleNowAsync(() => Counted(13)), "Ricalcolo sincrono dopo la chiusura");

        // The sheet in synchronous mode.
        int calls = service.Calls;
        var sheet = new SyncSheet(Context(Data(1), service), TimeSpan.FromMilliseconds(20), SheetCalculationMode.Synchronous);
        int modified = 0;
        sheet.Modified += (_, _) => modified++;
        expect.That(sheet.CalculationMode == SheetCalculationMode.Synchronous && !sheet.HasResults, "Foglio sincrono: stato iniziale");
        var calculated = sheet.CalculateAsync();
        expect.That(calculated.IsCompleted && await calculated && sheet.HasResults && Echo(sheet.Result) == 1 && sheet.Published == 1
            && service.Calls == calls + 1 && service.Threads.Last() == ui.ThreadId, "Calcola del foglio sincrono");
        expect.That(sheet.Steps.SequenceEqual(["prima", "dopo"]) && sheet.Data["preparato"]!.GetValue<bool>() && sheet.Data["dopo"]!.GetValue<double>() == 1,
            "Preparazione e lettura del risultato nel calcolo sincrono");
        expect.That(await sheet.CalculateAsync() && service.Calls == calls + 2, "Dati cambiati dal calcolo stesso: cache non pertinente");
        expect.That(await sheet.CalculateAsync() && service.Calls == calls + 2, "Cache della sessione del foglio non usata");
        sheet.Set(2);
        expect.That(modified == 1 && !sheet.HasResults && sheet.CalculationState == CalculationState.Pending && !sheet.PendingCalculation.IsCompleted,
            "Modifica del foglio sincrono");
        expect.That(await Expect.Within(sheet.PendingCalculation) && Echo(sheet.Result) == 2 && service.Threads.Last() == ui.ThreadId && sheet.Published == 4,
            "Ricalcolo automatico del foglio sincrono");
        long revision = sheet.Revision;
        int before = service.Calls;
        var result = sheet.Result;
        sheet.Select();
        expect.That(modified == 2 && sheet.Revision == revision + 1 && ReferenceEquals(sheet.Result, result) && sheet.PendingCalculation.IsCompleted
            && service.Calls == before, "Stato dell'interfaccia salvato: risultati non conservati o ricalcolo");
        sheet.Set(3);
        var waiting = sheet.PendingCalculation;
        sheet.Dispose();
        expect.That(!await Expect.Within(waiting) && service.Calls == before && !sheet.HasResults, "Chiusura del foglio sincrono durante il ritardo");

        // A sheet that calculates on a background thread cannot ask for the synchronous calculation.
        using var background = new SyncSheet(Context(Data(1), service), null, SheetCalculationMode.Background);
        expect.Throws<InvalidOperationException>(() => background.Now(), "Calcolo sincrono di un foglio che calcola sullo sfondo");
        expect.Throws<ArgumentOutOfRangeException>(() => _ = new SyncSheet(Context(Data(1), service), null, (SheetCalculationMode)7), "Modalità di calcolo sconosciuta");
        return expect.Count;
    }

    private static SheetContext Context(JsonObject data, IModuleService service) =>
        new(data, false, new Host(), Scripted.Services(), service, new CalculationLimiter(1));

    /// <summary>A sheet of the test module in the given mode: it prepares the data before the calculation and copies the echo after.</summary>
    private sealed class SyncSheet(SheetContext context, TimeSpan? delay, SheetCalculationMode mode) : SheetViewModelBase("prova", context, delay, mode)
    {
        public override IModuleReportSource Report { get; } = new NoReport();
        internal int Published { get; private set; }
        internal List<string> Steps { get; } = [];
        internal void Set(double value) => Edit(d => d["valore"] = value);
        internal void Select() => ChangedKeepingResults();
        internal bool Now() => CalculateNow();

        protected override ModuleCalculation CalculateSynchronously()
        {
            Data["preparato"] = true; Steps.Add("prima");
            var calculation = CalculateModule(Data);
            Data["dopo"] = calculation.Values["eco"]!.DeepClone(); Steps.Add("dopo");
            return calculation;
        }

        protected override void OnCalculationPublished() => Published++;
    }

    private sealed class NoReport : IModuleReportSource
    {
        public byte[] BuildReport(string title, IReadOnlySet<string> options, bool projectReport = false) => [];
    }

    private sealed class Host : ISheetHost
    {
        public void OpenModuleCopy(string module, JsonObject data, string name) => throw new InvalidOperationException("Copia non prevista.");
        public void ReplaceSoil(JsonObject data) => throw new InvalidOperationException("Terreno non previsto.");
        public IReadOnlyList<SoilSource> SoilSources() => [];
        public void RequestReport() => throw new InvalidOperationException("Report non previsto.");
    }
}
