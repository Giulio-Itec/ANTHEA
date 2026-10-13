using System.Text.Json.Nodes;
using X.Core;

namespace Anthea.Application.Reporting;

/// <summary>Prepares one chapter on a sheet owned by the report snapshot. Implementations may replace
/// its data with the migrated/calculated copy; the open project must never be supplied here.</summary>
public interface IProjectReportChapterBuilder
{
    Task<ReportProject.SheetContent> BuildAsync(JsonObject snapshotSheet, CancellationToken cancellation);
}

public static class ProjectReportService
{
    public static JsonObject Snapshot(JsonObject section)
    {
        var top = ProjectSharedData.Ancestors(section).LastOrDefault() ?? section;
        int index = ProjectSharedData.Sections(top).ToList().IndexOf(section);
        return ProjectSharedData.Sections((JsonObject)top.DeepClone()).ElementAt(index);
    }

    /// <summary>Writes an owned snapshot (created by Snapshot), preserving its ancestry for shared
    /// inputs. Updated per-sheet data remains in that snapshot for diagnostics; the original is untouched.</summary>
    public static async Task<int> WriteSnapshotAsync(JsonObject snapshot, string filename,
        IProjectReportChapterBuilder chapters, Action<string>? progress = null, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(chapters);
        var sheets = ProjectSharedData.SubtreeSheets(snapshot).ToArray();
        if (sheets.Length == 0) throw new ArgumentException("La sezione non contiene schede.");
        var reports = new Dictionary<JsonObject, ReportProject.SheetContent>();
        for (int i = 0; i < sheets.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            var sheet = sheets[i];
            progress?.Invoke($"Scheda {i + 1} di {sheets.Length}: {sheet.S("nome")}\nAggiornamento dei calcoli e preparazione del report…");
            try
            {
                reports[sheet] = await chapters.BuildAsync(sheet, cancellation);
                cancellation.ThrowIfCancellationRequested();
                progress?.Invoke($"Scheda {i + 1} di {sheets.Length}: capitolo pronto.");
            }
            catch (Exception error) when (error is not OperationCanceledException && error is not IOException && error is not UnauthorizedAccessException)
            { reports[sheet] = new(Error: error.Message); }
        }
        cancellation.ThrowIfCancellationRequested();
        progress?.Invoke("Unione dei dati comuni e impaginazione…");
        var plan = new ProjectReportPlan(snapshot);
        var warnings = ProjectValidation.Warnings(snapshot);
        await Task.Run(() => { cancellation.ThrowIfCancellationRequested(); ReportProject.Write(filename, plan, reports, warnings, cancellation); }, cancellation);
        return reports.Values.Count(r => r.Error is not null);
    }
}
