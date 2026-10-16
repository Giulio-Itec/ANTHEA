using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules;

namespace Anthea.Presentation.Tests;

/// <summary>
/// The two additions of F5.5 to the common contract of the sheets: <see cref="ISheetViewModel.PreviewData"/> (the data by default,
/// the state not committed yet for a sheet that writes at Commit) and <see cref="SheetCalculationMode.None"/> (nothing calculated
/// or published, no automatic recalculation). The engine is counted: it must never run.
/// </summary>
internal static class SheetPreviewChecks
{
    internal static int Run() => SingleThreadContext.Run(_ => RunAsync());

    private static async Task<int> RunAsync()
    {
        var expect = new Expect();
        var engine = new CountingEngine();
        SheetContext Context() => new(new JsonObject { ["valore"] = 1 }, false, new NoHost(), Scripted.Services(), engine, new CalculationLimiter(1));

        expect.Throws<ArgumentException>(() => new DraftSheet(Context(), TimeSpan.FromMilliseconds(10)), "Modalità senza calcolo con ritardo accettata");
        using (var sheet = new DraftSheet(Context(), null))
        {
            ISheetViewModel contract = sheet;
            expect.That(sheet.CalculationMode == SheetCalculationMode.None && ReferenceEquals(contract.PreviewData, sheet.Data), "Anteprima predefinita diversa dai dati");
            var calculated = sheet.CalculateAsync();
            expect.That(calculated.IsCompleted && !await calculated && !sheet.HasResults && sheet.Result is null && sheet.CalculationState == CalculationState.Idle
                && !sheet.IsBusy && engine.Calls == 0, "Modalità senza calcolo: calcolo eseguito o pubblicato");
            await Expect.Within(contract.CalculateAsync());
            expect.That(engine.Calls == 0 && !sheet.HasResults, "Modalità senza calcolo: Calcola del contratto");
            expect.Throws<InvalidOperationException>(() => sheet.Schedule(), "Modalità senza calcolo: ricalcolo automatico accettato");
            int modified = 0;
            sheet.Modified += (_, _) => modified++;
            sheet.Draft = 7;
            expect.That(modified == 1 && contract.PreviewData["valore"]!.GetValue<int>() == 7 && sheet.Data["valore"]!.GetValue<int>() == 1
                && !ReferenceEquals(contract.PreviewData, sheet.Data), "Anteprima di un foglio che scrive alla conferma");
            sheet.Commit();
            expect.That(sheet.Data["valore"]!.GetValue<int>() == 7 && JsonNode.DeepEquals(contract.PreviewData, sheet.Data) && sheet.PendingCalculation.IsCompleted
                && engine.Calls == 0 && !sheet.HasResults, "Conferma della bozza");
            expect.That(engine.Calls == 0 && sheet.Revision == 1, "Nessun ricalcolo dopo la modifica");
        }
        return expect.Count;
    }

    /// <summary>A sheet without published calculation that keeps one value until Commit, as the concrete material; without draft its preview is the default one.</summary>
    private sealed class DraftSheet(SheetContext context, TimeSpan? delay) : SheetViewModelBase(context.Calculation.ModuleId, context, delay, SheetCalculationMode.None)
    {
        private int? draft;

        public override IModuleReportSource Report => throw new NotSupportedException();

        public override JsonObject PreviewData
        {
            get
            {
                if (draft is not int value) return base.PreviewData;
                var preview = (JsonObject)Data.DeepClone();
                preview["valore"] = value;
                return preview;
            }
        }

        internal int Draft { set { draft = value; Changed(); } }

        internal Task<bool> Schedule() => ScheduleCalculationAsync();

        protected override void OnCommit()
        {
            if (draft is int value) Data["valore"] = value;
            draft = null;
        }
    }

    private sealed class CountingEngine : IModuleService
    {
        internal int Calls { get; private set; }
        public string ModuleId => "mat_calcestruzzo";
        public JsonObject Prepare(JsonObject source) => (JsonObject)source.DeepClone();
        public ModuleCalculation Calculate(JsonObject source, CancellationToken cancellation = default)
        {
            Calls++;
            throw new InvalidOperationException("Il motore non deve essere chiamato.");
        }
    }

    private sealed class NoHost : ISheetHost
    {
        public void OpenModuleCopy(string module, JsonObject data, string name) => throw new InvalidOperationException();
        public void ReplaceSoil(JsonObject data) => throw new InvalidOperationException();
        public IReadOnlyList<SoilSource> SoilSources() => [];
        public void RequestReport() => throw new InvalidOperationException();
    }
}
