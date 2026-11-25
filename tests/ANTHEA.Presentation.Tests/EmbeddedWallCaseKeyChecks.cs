using Anthea.Application.EmbeddedWalls;
using Anthea.Presentation.Modules.EmbeddedWalls;

namespace Anthea.Presentation.Tests;

/// <summary>
/// Organizzazione del modulo paratie (progetto del 16/11), step 4, contracts C2 and C5. One box Caso drives input and results: the selection
/// is the pair (case, phase), and a case or phase without results says so instead of showing another case or the last phase that
/// succeeded. Request and snapshot are two states; the tab Analisi shows what the results contain and nothing more.
/// </summary>
internal static partial class EmbeddedWallChecks
{
    private static async Task CaseKeyChecks(Expect e)
    {
        // C1 complete with Initial, Loaded and Reversed; C2 with the same phases and an excessive load: no mechanical phase accepted.
        var data = EmbedmentExample(); data["case_id"] = "C1";
        var other = EmbeddedWallDocument.NewRow("cases"); other["id"] = "C2"; other["action_basis"] = "Seconda storia sintetica, carico eccessivo";
        data["cases"]!.AsArray().Add(other);
        foreach (string table in new[] { "stages", "loads" })
        foreach (var original in data[table]!.AsArray().ToArray())
        {
            var row = original!.DeepClone().AsObject(); row["case"] = "C2";
            if (table == "loads") row["horizontal"] = 1e6;
            data[table]!.AsArray().Add(row);
        }
        using var sheet = Open(data);
        e.That(sheet.RequestStatus.Contains("Nessun calcolo") && sheet.SnapshotStatus.Contains("Nessun risultato") && sheet.StageResultStatus == "",
            "C5: stati iniziali della richiesta e dello snapshot errati.");
        e.That(await Expect.Within(sheet.CalculateAsync()), "C2: calcolo dei due casi non pubblicato.");
        var c1 = sheet.ResultCases.Single(r => r.CaseId == "C1"); var c2 = sheet.ResultCases.Single(r => r.CaseId == "C2");
        e.That(c1.Stages.Count == 3 && c2.Stages.Count == 0, "C2: preparazione dei due casi errata.");
        e.That(ReferenceEquals(sheet.SelectedResultCase, c1) && sheet.InputStage?["id"] == "Reversed" && ReferenceEquals(sheet.SelectedStage, c1.Stages[2])
            && sheet.StageResultStatus == "", "C2: dopo il calcolo la coppia (caso, fase) non è quella sullo schermo.");
        e.That(sheet.RequestStatus.Contains("terminato") && sheet.SnapshotStatus.Contains("1 casi completi su 2"), "C5: richiesta e snapshot non distinti: "
            + sheet.RequestStatus + " / " + sheet.SnapshotStatus);
        sheet.InputStage = sheet.InputStages[0];
        e.That(ReferenceEquals(sheet.SelectedStage, c1.Stages[0]) && sheet.Points.Count > 0, "C2: la scheda della fase non sceglie i risultati della stessa fase.");
        sheet.InputCase = "C2";
        e.That(ReferenceEquals(sheet.SelectedResultCase, c2) && sheet.SelectedStage is null && sheet.Points.Count == 0 && sheet.Checks.Count == 0
            && sheet.StageResultStatus.Contains("non disponibile") && sheet.StageResultStatus.Contains("«C2»"),
            "C2: per un caso senza la fase si vedono i risultati di un altro caso o di un'altra fase.");
        sheet.SelectedResultCase = c1;
        e.That(sheet.InputCase == "C1" && ReferenceEquals(sheet.SelectedStage, c1.Stages.Single(s => s.Id == sheet.InputStage?["id"])),
            "C2: i risultati di un caso scelti in un documento non scelgono il caso.");
        sheet.SelectedStage = c1.Stages[1];
        e.That(sheet.InputStage?["id"] == "Loaded" && sheet.Sketch?.Phase == "Loaded", "C2: la fase scelta tra i risultati non sceglie la stessa fase dell'anteprima.");
        var lines = sheet.AnalysisEntries;
        e.That(lines.Count(l => l.IsCase) == 2 && lines.Where(l => l.CaseId == "C1" && !l.IsCase).All(l => l.Title.Contains("iterazioni"))
            && lines.Where(l => l.CaseId == "C2" && !l.IsCase).Count() == 3
            && lines.Where(l => l.CaseId == "C2" && !l.IsCase).All(l => l.Title.EndsWith("risultato non disponibile") && l.Detail.Contains(c2.Diagnostic)),
            "C5: la scheda Analisi inventa uno stato per fase o perde la diagnostica del caso.");
        string before = sheet.Data.ToJsonString(); long revision = sheet.Revision;
        sheet.SelectedAnalysisEntry = lines.First(l => l.CaseId == "C2" && l.Stage == "Initial");
        e.That(sheet.InputCase == "C2" && sheet.InputStage?["id"] == "Initial" && sheet.SelectedStage is null && sheet.Documents.Active == EmbeddedWallDocuments.Section,
            "C2: la riga dell'analisi non porta alla coppia (caso, fase).");
        e.That(sheet.Data.ToJsonString() == before && sheet.Revision == revision && sheet.Current is not null, "C2: la navigazione modifica dati o risultati.");
        sheet.Tables.Single(t => t.Key == "stages").Rows[0]["left_surcharge"] = "5";
        e.That(sheet.Current is null && sheet.SnapshotStatus.Contains("Nessun risultato") && sheet.RequestStatus.Contains("terminato")
            && sheet.AnalysisEntries.Count == 0 && sheet.StageResultStatus == "", "C5: dopo una modifica lo snapshot superato resta o la richiesta cambia stato.");
    }
}
