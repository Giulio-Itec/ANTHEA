using Anthea.Presentation.Mvvm;
using Anthea.Reports;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.EmbeddedWalls;

/// <summary>A line of the tab «Analisi» of the left panel: a case or one of its phases, with what the results say about it.</summary>
public sealed record EmbeddedWallAnalysisEntry(string CaseId, string? Stage, string Title, string Detail, bool IsCase);

/// <summary>
/// The left panel (organizzazione del modulo paratie, progetto del 16/11, step 4).
/// <para>Contract C2: the selection is the pair (case, phase). The box «Caso» (one choice of the case for input and results, D3) and the tabs of the phases
/// choose input and results together; the results are those of the same case and of the phase with the same identifier, never of another case
/// or of the last phase that succeeded: without them «risultato non disponibile» (<see cref="StageResultStatus"/>). Choosing the results of a
/// case or a phase in a document chooses the same pair.</para>
/// <para>Contract C5: three states kept apart. The request (<see cref="RequestStatus"/>: running, ended, cancelled or superseded, with the
/// revision that started it), the snapshot of the results (<see cref="SnapshotStatus"/>: print, complete cases), and the state of the sheet in
/// the project, which the tree of the project shows (not this sheet). The tab «Analisi» lists the cases and their phases with what the results
/// contain: iterations, increments and residuals as GPC published them; a phase without results says so, with the diagnostics of its case,
/// never a cause of its own. «Calcolato» is not «verificato». Nothing is computed here.</para>
/// </summary>
public sealed partial class EmbeddedWallSheetViewModel
{
    private EmbeddedWallAnalysisEntry? selectedAnalysisEntry;
    private string requestStatus = "Nessun calcolo avviato da questa apertura del foglio.";
    public IReadOnlyList<EmbeddedWallAnalysisEntry> AnalysisEntries { get; private set; } = [];

    /// <summary>The line chosen in the tab «Analisi»: choosing it brings the drawing and the results to its case and phase.</summary>
    public EmbeddedWallAnalysisEntry? SelectedAnalysisEntry
    {
        get => selectedAnalysisEntry;
        set { if (SetProperty(ref selectedAnalysisEntry, value) && value is not null) Guard.Run(() => ShowAnalysisEntry(value)); }
    }

    /// <summary>C5, first level: the last request of calculation or search of this sheet and the revision it started from.</summary>
    public string RequestStatus { get => requestStatus; private set => SetProperty(ref requestStatus, value); }

    /// <summary>C5, second level: the results on screen, their print and how many cases are complete.</summary>
    public string SnapshotStatus => current is null ? "Nessun risultato corrente: dati mai calcolati o cambiati dopo il calcolo."
        : $"Risultati dell'impronta {current.Fingerprint[..Math.Min(8, current.Fingerprint.Length)]}: {ResultCases.Count(c => c.Complete)} casi completi su {ResultCases.Count}"
            + (current.EmbedmentSearch is null ? "." : ", della ricerca dell'infissione.");

    /// <summary>C2: why the documents show no results for the case and phase on screen; empty when they show them or nothing was calculated.</summary>
    public string StageResultStatus => current is null ? ""
        : DisplayedCase is null ? $"Risultato non disponibile per il caso «{InputCase}»."
        : inputStage is not null && selectedStage is null ? $"Risultato non disponibile per la fase «{inputStage["id"]}» del caso «{InputCase}». {DisplayedCase.Diagnostic}"
        : "";

    /// <summary>C2: the results of the case on screen, or none.</summary>
    private void SelectResultCase()
    {
        var match = ResultCases.FirstOrDefault(r => r.CaseId == inputCase);
        if (!ReferenceEquals(selectedResultCase, match)) SelectedResultCase = match;
    }

    /// <summary>C2: the results of the phase on screen in the results of its case, or none.</summary>
    private EmbeddedWallStageData? KeyedResultStage() => inputStage is null ? null : Stages.FirstOrDefault(s => s.Id == inputStage["id"]);

    private void SelectResultStage()
    {
        var match = KeyedResultStage();
        if (!ReferenceEquals(selectedStage, match)) SelectedStage = match;
        OnPropertyChanged(nameof(StageResultStatus));
    }

    /// <summary>A phase chosen among the results chooses the same phase of the input (C2).</summary>
    private void FollowResultStage(EmbeddedWallStageData? stage)
    {
        if (stage is not null && InputStages.FirstOrDefault(s => s["id"] == stage.Id) is { } row && !ReferenceEquals(row, inputStage)) InputStage = row;
        OnPropertyChanged(nameof(StageResultStatus));
    }

    private void ShowAnalysisEntry(EmbeddedWallAnalysisEntry entry)
    {
        if (InputCases.Contains(entry.CaseId)) InputCase = entry.CaseId;
        if (entry.Stage is not null && InputStages.FirstOrDefault(s => s["id"] == entry.Stage) is { } input) InputStage = input;
        Documents.Show(EmbeddedWallDocuments.Section);
    }

    private void RefreshAnalysis()
    {
        var entries = new List<EmbeddedWallAnalysisEntry>();
        if (current is not null && Tables.Count > 0)
        {
            string principal = Data["case_id"]?.ToString() ?? "";
            foreach (var result in ResultCases)
            {
                entries.Add(new(result.CaseId, null, result.CaseId + (result.Complete ? " · storia completa" : " · storia incompleta"), result.Diagnostic, true));
                foreach (var stage in result.Stages)
                    entries.Add(new(result.CaseId, stage.Id, $"{stage.Id} · {stage.Iterations} iterazioni · {stage.Increments} incrementi",
                        $"Residui finali pubblicati dal calcolo: forza {EmbeddedWallDisplay.Number(stage.ForceResidualN, "N")} N, momento {EmbeddedWallDisplay.Number(stage.MomentResidualNmm, "Nmm")} Nmm.", false));
                var calculated = result.Stages.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
                foreach (var missing in Tables.Single(t => t.Key == "stages").Rows
                    .Where(r => (string.IsNullOrWhiteSpace(r["case"]) ? principal : r["case"]) == result.CaseId && !calculated.Contains(r["id"])))
                    entries.Add(new(result.CaseId, missing["id"], missing["id"] + " · risultato non disponibile", "Diagnostica del caso: " + result.Diagnostic, false));
            }
        }
        selectedAnalysisEntry = null;
        AnalysisEntries = entries;
        OnPropertyChanged(nameof(AnalysisEntries)); OnPropertyChanged(nameof(SelectedAnalysisEntry)); OnPropertyChanged(nameof(SnapshotStatus));
        OnPropertyChanged(nameof(StageResultStatus));
    }
}
