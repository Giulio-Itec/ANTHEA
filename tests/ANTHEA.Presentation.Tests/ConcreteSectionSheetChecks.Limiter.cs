using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules.ConcreteSection;

namespace Anthea.Presentation.Tests;

/// <summary>
/// 1.16.0 (D-5, C2/S5-c): the RC section passes through the limiter of the application, as the calculations of the modules, and calculates the
/// shear on a worker thread. With the only place of a limiter taken by another calculation:
/// <list type="bullet">
/// <item>the batch of the opening is busy and waits without a place (<see cref="ConcreteCalculation.IsEngineRunning"/> false): no result, the status
/// of the request unchanged, the quick resistances waiting too; with the place freed the sheet gives the same result, data, status, errors and quick
/// resistances as a sheet calculated with a free limiter;</item>
/// <item>the shear alone (a row of the shear changed and set back) waits for its place with the rows invalidated and the UI thread free («GPC Engine
/// · in corso: Taglio»), then gives the same rows and summaries as before;</item>
/// <item>closing the sheet while its batch waits publishes nothing, gives no error and leaves the place to the others.</item>
/// </list>
/// </summary>
internal static partial class ConcreteSectionSheetChecks
{
    private static async Task LimiterAsync(string root, Expect expect)
    {
        var fixture = ConcreteSectionScenarios.FixtureData(root, "taglio-senza-staffe-manuali");
        JsonObject data;
        string reference;
        // The reference: a free limiter. The data after its opening (with the ids of the rows) open the sheet of the check.
        using (var free = OpenSheet(fixture))
        {
            await IdleAsync(free);
            data = (JsonObject)free.Data.DeepClone();
        }
        using (var free = OpenSheet(data))
        {
            await IdleAsync(free);
            reference = Fingerprint(free);
        }

        var limiter = new CalculationLimiter(1);
        var other = await limiter.EnterAsync(CancellationToken.None);
        using (var sheet = OpenSheet(data, limiter: limiter))
        {
            string opening = sheet.Status;
            await EnteredAsync(sheet);
            for (int turn = 0; turn < 5; turn++) { await Task.Yield(); await Task.Delay(20); }
            expect.That(sheet.IsBusy && !sheet.Calculation.IsEngineRunning && !sheet.HasResults && sheet.Status == opening && sheet.Inputs.IsQuickResistanceRunning
                && sheet.Calculation.Pending.Count == ConcreteCalculation.AllCalculations.Length,
                "Limitatore occupato: lotto dell'apertura in attesa del posto, stato della richiesta, resistenze rapide in attesa · " + sheet.Status);
            other.Dispose();
            await IdleAsync(sheet);
            expect.That(Fingerprint(sheet) == reference && sheet.HasResults && !sheet.Calculation.IsEngineRunning,
                "Limitatore liberato: stessi risultati, dati, stato, errori e resistenze rapide del foglio con il limitatore libero");

            // The shear alone: a row changed and set back while the place is taken.
            string rows = ShearRows(sheet), dashboard = sheet.ShearDashboard.Text, summary = sheet.Shear.Summary;
            other = await limiter.EnterAsync(CancellationToken.None);
            var row = sheet.ShearRowCollection[0];
            string vx = row.Values.S("Vx");
            row["Vx"] = "123"; row["Vx"] = vx;
            await EnteredAsync(sheet);
            for (int turn = 0; turn < 5; turn++) { await Task.Yield(); await Task.Delay(20); }
            expect.That(sheet.IsBusy && !sheet.Calculation.IsEngineRunning && sheet.Status == "GPC Engine · in corso: Taglio"
                && sheet.Calculation.Pending.SetEquals(["Taglio"]) && sheet.ShearRowCollection.All(r => r.Values.S("esito") == "—"),
                "Taglio in attesa del posto del limitatore, righe invalidate, thread dell'interfaccia libero · " + sheet.Status);
            other.Dispose();
            await IdleAsync(sheet);
            expect.That(ShearRows(sheet) == rows && sheet.ShearDashboard.Text == dashboard && sheet.Shear.Summary == summary && sheet.Calculation.Pending.Count == 0,
                "Taglio sullo sfondo: stesse righe e stessi riepiloghi · " + sheet.Shear.Summary);
        }

        // Closing while the batch waits: nothing published, no error, the place to the others.
        other = await limiter.EnterAsync(CancellationToken.None);
        var closed = OpenSheet(data, limiter: limiter);
        await EnteredAsync(closed);
        closed.Dispose();
        other.Dispose();
        using (var place = await limiter.EnterAsync(CancellationToken.None).WaitAsync(Expect.Deadline))
            expect.That(!closed.HasResults && !closed.Calculation.IsEngineRunning, "Foglio chiuso durante l'attesa del posto: nessun risultato, posto restituito");
    }

    // What the sheet shows of its calculation: the result (the export of the historic contract), the data, the status, the errors and the quick
    // resistances.
    private static string Fingerprint(ConcreteSheetViewModel sheet) => string.Join("\n", sheet.Result?.ToJsonString() ?? "null", sheet.Data.ToJsonString(), sheet.Status,
        string.Join(" | ", sheet.Calculation.Errors.Select(e => e.Key + ": " + e.Value)), sheet.Inputs.QuickResistanceText);

    private static string ShearRows(ConcreteSheetViewModel sheet) => string.Join("\n", sheet.ShearRowCollection.Select(r => r.Values.ToJsonString()));
}
