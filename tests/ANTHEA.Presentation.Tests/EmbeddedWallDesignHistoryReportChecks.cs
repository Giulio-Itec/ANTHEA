using System.Text.Json.Nodes;
using Anthea.Application.EmbeddedWalls;
using Anthea.Application.Reporting;
using Anthea.Reports;

namespace Anthea.Presentation.Tests;

/// <summary>
/// A2, review of 15/11/2026 (M1): the project report reads the current result of the migrated data, as the sheet does. A result saved by the
/// 1.19.0 on the same libraries (schema 12, same fingerprint) was computed before the confirmation existed: it is no longer current.
/// </summary>
internal static partial class EmbeddedWallChecks
{
    private static async Task DesignHistoryProjectReportAsync(Expect e)
    {
        var service = new EmbeddedWallModule(); var builder = new ModuleReportService(new NoFigures());
        var saved = RcPileExample(); saved.Remove(DesignKey); saved["schema"] = 12;
        var computed = service.Calculate(RcPileExample()).NativeAs<EmbeddedWallReportData>();
        EmbeddedWallDocument.StoreResult(saved, computed with { Fingerprint = EmbeddedWallDocument.Fingerprint(saved) });
        e.That(EmbeddedWallDocument.ReadCurrentResult(saved) is not null && EmbeddedWallDocument.ReadCurrentResult(EmbeddedWallDocument.Snapshot(saved)) is null,
            "A2 (M1): premessa della prova, il risultato della versione 12 sembra corrente solo sui dati non migrati.");
        try
        {
            await builder.BuildAsync(new JsonObject { ["modulo_id"] = EmbeddedWallDocument.Module, ["nome"] = "Paratia della 1.19.0", ["dati"] = saved.DeepClone() }, default);
            e.That(false, "A2 (M1): la relazione di progetto usa un risultato SLU salvato prima della conferma.");
        }
        catch (ArgumentException error) { e.That(error.Message.Contains("assenti o superati", StringComparison.Ordinal), "A2 (M1): messaggio della relazione: " + error.Message); }
        var current = RcPileExample(); EmbeddedWallDocument.StoreResult(current, service.Calculate(current).NativeAs<EmbeddedWallReportData>());
        var chapter = await builder.BuildAsync(new JsonObject { ["modulo_id"] = EmbeddedWallDocument.Module, ["nome"] = "Paratia", ["dati"] = current.DeepClone() }, default);
        e.That(chapter.Document is not null, "A2 (M1): un risultato della versione 13 non è più accettato dalla relazione di progetto.");
    }
}
