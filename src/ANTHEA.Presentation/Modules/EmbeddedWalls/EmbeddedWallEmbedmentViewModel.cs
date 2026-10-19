using System.Text;
using Anthea.Application.EmbeddedWalls;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Services;
using Anthea.Reports;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.EmbeddedWalls;

public sealed partial class EmbeddedWallSheetViewModel
{
    private CancellationTokenSource? searchCancellation;
    private bool searching;
    private EmbeddedWallEmbedmentCandidate? selectedEmbedment;
    public override bool IsBusy => base.IsBusy || searching;
    public FormViewModel Embedment { get; private set; } = null!;
    public IAsyncRelayCommand SearchEmbedmentCommand { get; }
    public IRelayCommand EmbedmentCsvCommand { get; }
    public IReadOnlyList<EmbeddedWallEmbedmentCandidate> EmbedmentCandidates => current?.EmbedmentSearch?.Candidates ?? [];
    public EmbeddedWallEmbedmentCandidate? SelectedEmbedment
    {
        get => selectedEmbedment;
        set { if (SetProperty(ref selectedEmbedment, value)) { OnPropertyChanged(nameof(EmbedmentCriteria)); OnPropertyChanged(nameof(EmbedmentCandidateDiagnostic)); } }
    }
    public IReadOnlyList<EmbeddedWallEmbedmentCriterion> EmbedmentCriteria => selectedEmbedment?.Criteria ?? [];
    public string EmbedmentCandidateDiagnostic => selectedEmbedment is null ? "" : selectedEmbedment.Diagnostic + Environment.NewLine + selectedEmbedment.Result?.Diagnostic;
    public string EmbedmentStatus => current?.EmbedmentSearch is not { } s ? "Ricerca non eseguita sui dati correnti."
        : s.Diagnostic + (s.BestToe.HasValue ? $" Candidato accettabile: infissione {s.BestEmbedment:G6} m; quota piede {s.BestToe:G6} m. Diagrammi e verifiche principali riferiti a questa quota." : " Nessun candidato accettato.");

    public async Task<bool> SearchEmbedmentAsync(CancellationToken cancellation = default)
    {
        ThrowIfDisposed(); if (IsBusy) return false;
        Commit(); long started = Revision; var input = EmbeddedWallDocument.Snapshot(Data);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, Lifetime);
        searchCancellation = linked; searching = true; OnPropertyChanged(nameof(IsBusy)); Progress = "Preparazione della ricerca";
        var uiProgress = new Progress<EmbeddedWallEmbedmentProgress>(p =>
        {
            if (!IsDisposed && started == Revision && searching) Progress = $"Candidati valutati: {p.Evaluated}/{p.Planned}";
        });
        try
        {
            using var place = await Context.Limiter.EnterAsync(linked.Token);
            var result = await Task.Run(() => new EmbeddedWallModule().SearchEmbedment(input, linked.Token, uiProgress));
            // Explicit cancellation retains concluded candidates. A changed or closed document never receives them.
            if (IsDisposed || Revision != started) return false;
            current = result;
            if (CanEdit)
            {
                EmbeddedWallDocument.StoreResult(Data, result); storingResult = true;
                try { ChangedKeepingResults(); } finally { storingResult = false; }
            }
            SelectedStage = Stages.LastOrDefault(); ComparisonStage = null; SelectedHydraulicStage = HydraulicStages.LastOrDefault(); NotifyResult();
            return true;
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { return false; }
        finally
        {
            searchCancellation = null; searching = false;
            if (!IsDisposed) { Progress = ""; OnPropertyChanged(nameof(IsBusy)); }
        }
    }
    private void ExportEmbedmentCsv()
    {
        var result = RequireCurrent();
        var path = Context.Services.Files.SaveFile(new([new FileFilter("Ricerca infissione: criteri e risultati", "*.csv")], "ricerca-infissione.csv"));
        if (path is not null) File.WriteAllText(path, EmbeddedWallReport.EmbedmentCsv(result), new UTF8Encoding(true));
    }
}
