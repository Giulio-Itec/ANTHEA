using Anthea.Application.EmbeddedWalls;
using Anthea.Presentation.Modules.EmbeddedWalls;

namespace Anthea.Presentation.Tests;

/// <summary>
/// Organizzazione del modulo paratie (progetto del 16/11), step 11: the panel Messaggi lists only what ANTHEA and GPC produced (notices of the data
/// drawn, request, error of the engine, diagnostics of the results), with level, origin and provenance; a line opens its element through
/// structured data (rows, case, phase, the case and phase of the progress of the module); the lines of a request superseded by a change do not
/// enter (R06); filter and «Pulisci» are state of the view.
/// </summary>
internal static partial class EmbeddedWallChecks
{
    private static async Task MessagesChecks(Expect e)
    {
        // Structured progress of the module (Application): case and phase as fields, not inside the text.
        var progress = new List<EmbeddedWallProgress>();
        var module = new EmbeddedWallModule(); module.ProgressChanged += progress.Add;
        var example = Example(); example["case_id"] = "Caso A";
        module.Calculate(example);
        e.That(progress.Count > 0 && progress.All(p => p.CaseId == "Caso A") && progress.Any(p => p.StageId == "F1" && p.Stage == "F1"),
            "Passo 11: avanzamento senza caso e fase strutturati: " + string.Join("; ", progress.Take(3).Select(p => $"{p.Stage}|{p.CaseId}|{p.StageId}")));

        var data = EmbeddedWallDocument.Create();
        var excavation = EmbeddedWallDocument.NewRow("stages"); excavation["id"] = "F2"; excavation["right_ground"] = -4; excavation["right_water"] = "abc";
        excavation["distributed_load"] = 5; excavation["left_surcharge"] = null; data["stages"]!.AsArray().Add(excavation);
        using var sheet = Open(data);
        string original = sheet.Data.ToJsonString(); long revision = sheet.Revision;
        var messages = sheet.Messages;
        var water = messages.All.SingleOrDefault(m => m.Text.Contains("Falda a destra") && m.Text.Contains("non valido"));
        var stage = sheet.Tables.Single(t => t.Key == "stages").Rows[1];
        e.That(water is { Level: EmbeddedWallMessageLevel.Error, Origin: EmbeddedWallMessages.Data, LevelText: "Errore" } && water.Target?.Table == "stages"
            && ReferenceEquals(water.Target.Row, stage) && water.Target.CaseId == "Caso 1" && water.Target.StageId == "F2" && water.Place.Contains("fase F2")
            && water.Provenance.Contains("fase F2") && sheet.Sketch!.Notices.Contains(water.Text),
            "Passo 11: avviso di dato senza livello, origine, provenienza o riga.");
        var uniform = messages.All.SingleOrDefault(m => m.Text.StartsWith("Carico uniforme sulla parete"));
        e.That(uniform?.Level == EmbeddedWallMessageLevel.Information && messages.All.All(m => sheet.Sketch!.Notices.Contains(m.Text)),
            "Passo 11: messaggi diversi dagli avvisi già prodotti dal disegno.");
        var missing = messages.All.SingleOrDefault(m => m.Text == "Sovraccarico a sinistra: dato da completare.");
        e.That(missing?.Level == EmbeddedWallMessageLevel.Warning && ReferenceEquals(missing.Target?.Row, stage),
            "Passo 11: dato mancante non segnalato come avviso della sua riga.");
        // Filter and «Pulisci»: state of the view.
        messages.Filter = EmbeddedWallMessages.ErrorsOnly;
        e.That(messages.Items.Count > 0 && messages.Items.All(m => m.IsError) && messages.Summary.Contains("non mostrati"), "Passo 11: filtro «Solo errori» errato.");
        messages.Filter = EmbeddedWallMessages.WarningsAndErrors;
        e.That(messages.Items.All(m => m.Level != EmbeddedWallMessageLevel.Information) && !messages.Items.Contains(uniform!), "Passo 11: filtro «Avvisi ed errori» errato.");
        messages.Filter = EmbeddedWallMessages.AllLevels;
        // Another phase: its notices only; back to F2: the notice of F2 again (a new line, its own time).
        sheet.InputStage = sheet.InputStages[0];
        e.That(!messages.All.Any(m => m.Text == water!.Text), "Passo 11: avvisi di un'altra fase nel pannello.");
        sheet.InputStage = sheet.InputStages[1];
        var again = messages.All.Single(m => m.Text == water!.Text);
        e.That(again.Time >= water!.Time, "Passo 11: ora del messaggio non ordinata.");
        messages.ClearCommand.Execute(null);
        e.That(messages.Items.Count == 0 && messages.All.Count > 0 && messages.IsEmpty && !messages.ClearCommand.CanExecute(null), "Passo 11: «Pulisci» non pulisce la vista.");
        e.That(sheet.Data.ToJsonString() == original && sheet.Revision == revision, "Passo 11: filtro, navigazione o «Pulisci» modificano i dati.");
        // The line opens its row; a line cleared comes back when its notice is produced again.
        messages.OpenCommand.Execute(again);
        e.That(sheet.InspectorTable?.Key == "stages" && ReferenceEquals(sheet.InspectorTable.Selected, again.Target!.Row) && ReferenceEquals(sheet.InputStage, again.Target.Row)
            && sheet.Documents.Active == EmbeddedWallDocuments.Section, "Passo 11: il messaggio non apre la riga della fase.");
        stage["right_water"] = "";
        e.That(!messages.All.Any(m => m.Text.Contains("Falda a destra")), "Passo 11: avviso risolto ancora nel pannello.");
        var kept = messages.All.Single(m => m.Text == missing!.Text);
        stage["right_water"] = "xyz";
        e.That(messages.Items.Any(m => m.Text.Contains("Falda a destra") && m.IsError), "Passo 11: avviso prodotto di nuovo ma tenuto nascosto da «Pulisci».");
        e.That(ReferenceEquals(messages.All.Single(m => m.Text == missing!.Text), kept) && !messages.Items.Contains(kept),
            "Passo 11: una riga ancora prodotta perde la sua ora o torna visibile dopo «Pulisci».");
        var wrong = sheet.Data.DeepClone().AsObject(); wrong["bottom"] = 5; sheet.Reload(wrong);
        var wall = messages.All.SingleOrDefault(m => m.Text.StartsWith("Testa e piede non ordinati"));
        e.That(wall?.Target?.Table == EmbeddedWallMessages.GeneralTarget && wall.Place.Contains("Impostazioni"), "Passo 11: avviso della parete senza destinazione.");
        messages.OpenCommand.Execute(wall);
        e.That(sheet.ShowGeneral, "Passo 11: l'avviso della parete non apre le impostazioni.");

        // Calculation: request, results and the case without results; then a change supersedes them (R06).
        var cases = EmbedmentExample(); cases["case_id"] = "C1";
        var other = EmbeddedWallDocument.NewRow("cases"); other["id"] = "C2"; other["action_basis"] = "Seconda storia sintetica, carico eccessivo";
        cases["cases"]!.AsArray().Add(other);
        foreach (string table in new[] { "stages", "loads" })
        foreach (var row in cases[table]!.AsArray().ToArray())
        {
            var copy = row!.DeepClone().AsObject(); copy["case"] = "C2";
            if (table == "loads") copy["horizontal"] = 1e6;
            cases[table]!.AsArray().Add(copy);
        }
        using var calculated = Open(cases);
        e.That(!calculated.Messages.All.Any(m => m.Origin == EmbeddedWallMessages.Calculation), "Passo 11: messaggi di calcolo prima di ogni richiesta.");
        e.That(await Expect.Within(calculated.CalculateAsync()), "Passo 11: calcolo dei due casi non pubblicato.");
        var all = calculated.Messages.All;
        var request = all.SingleOrDefault(m => m.Text.StartsWith("Calcolo terminato"));
        var c2 = all.SingleOrDefault(m => m.Target?.CaseId == "C2" && m.Origin == EmbeddedWallMessages.Calculation);
        var c2Result = calculated.ResultCases.Single(r => r.CaseId == "C2");
        e.That(request is { Level: EmbeddedWallMessageLevel.Information } && request.Provenance.Contains("revisione")
            && c2 is { Level: EmbeddedWallMessageLevel.Warning } && c2.Text == c2Result.Diagnostic && c2.Provenance.Contains(calculated.Current!.Fingerprint[..8])
            && all.Any(m => m.Text == calculated.Current.Diagnostic) && all.Where(m => m.Origin == EmbeddedWallMessages.Calculation).All(m => m.Text == request.Text
                || m.Text == calculated.Current.Diagnostic || calculated.ResultCases.Any(r => r.Diagnostic == m.Text)),
            "Passo 11: messaggi del calcolo non tratti dalla richiesta e dai risultati: " + string.Join(" | ", all.Select(m => m.LevelText + " " + m.Text)));
        calculated.Messages.OpenCommand.Execute(c2);
        e.That(calculated.InputCase == "C2", "Passo 11: il messaggio del caso non porta al caso.");
        calculated.Tables.Single(t => t.Key == "stages").Rows[0]["left_surcharge"] = "5";
        e.That(calculated.Current is null && !calculated.Messages.All.Any(m => m.Origin == EmbeddedWallMessages.Calculation || m.Target?.CaseId == "C2" && m.Origin != EmbeddedWallMessages.Data),
            "Passo 11: dopo una modifica restano i messaggi della richiesta superata o dei risultati superati.");

        // Error of the engine: the missing basis of the actions is the text of the calculation, an error without target.
        var refused = Example(); refused["check_kind"] = "Ultimate"; refused["action_basis"] = "Da definire";
        using var failed = Open(refused);
        await Expect.Within(failed.CalculateAsync());
        var error = failed.Messages.All.SingleOrDefault(m => m.Level == EmbeddedWallMessageLevel.Error && m.Origin == EmbeddedWallMessages.Calculation);
        e.That(failed.CalculationMessage.Length > 0 && error?.Text == failed.CalculationMessage && error.Target is null,
            "Passo 11: errore del calcolo non riportato com'è: " + failed.CalculationMessage);
    }
}
