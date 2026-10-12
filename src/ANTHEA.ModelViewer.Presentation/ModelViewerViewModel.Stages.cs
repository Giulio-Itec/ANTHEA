using System.Collections.ObjectModel;
using ANTHEA.ModelWorkspace;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ANTHEA.ModelViewer.Presentation;

public sealed partial class ImportStageOption(ModelConstructionPhase phase, Action changed) : ObservableObject
{
    public ModelConstructionPhase Phase { get; } = phase;
    public string Label => Phase.Description;
    public bool CanRead => Phase.SavesResults != false;
    [ObservableProperty] bool isSelected;
    [ObservableProperty] string stepsText = "";
    partial void OnIsSelectedChanged(bool value) => changed();
    partial void OnStepsTextChanged(string value) => changed();
    public string[] Steps => StepsText.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}

public sealed partial class ModelViewerViewModel
{
    public ObservableCollection<ImportStageOption> ImportStages { get; } = [];
    public ObservableCollection<ImportCaseOption> ImportStageCases { get; } = [];
    [ObservableProperty] bool importConstructionStages;
    [ObservableProperty] string importDisplacementReference = "Accumulative";
    public IReadOnlyList<string> StageDisplacementReferences { get; } = ["Accumulative", "Current", "Real"];
    public bool HasImportStages => ImportStages.Count > 0;
    public string ImportStageSummary => $"{ImportStages.Count(s => s.IsSelected)} fasi · {ImportStages.Where(s => s.IsSelected).Sum(s => s.Steps.Length)} passi · {ImportStageCases.Count(c => c.IsSelected)} casi stage";
    public string ImportStageReferenceHelp => ImportDisplacementReference switch {
        "Current" => "Current: riferimento alla fase corrente restituito dalla sorgente. Non viene sommato agli spostamenti di altre fasi.",
        "Real" => "Real: riferimento reale restituito dalla sorgente. ANTHEA non ricostruisce o corregge gli spostamenti.",
        _ => "Accumulative: spostamenti accumulati restituiti dalla sorgente. La deformata mantiene esplicito questo riferimento." };
    bool HasValidStageSelection => ImportConstructionStages && ImportStageCases.Any(c => c.IsSelected) && ImportStages.Any(s => s.IsSelected) &&
        ImportStages.Where(s => s.IsSelected).All(s => s.Phase.SavesResults != false && s.Steps.Length > 0 && s.Steps.Distinct(StringComparer.Ordinal).Count() == s.Steps.Length &&
            s.Steps.All(p => p.Length < 200 && !p.Contains(':') && !p.Any(char.IsControl))) && StageDisplacementReferences.Contains(ImportDisplacementReference);
    partial void OnImportConstructionStagesChanged(bool value) => ImportStageSelectionChanged();
    partial void OnImportDisplacementReferenceChanged(string value) { ImportStageSelectionChanged(); OnPropertyChanged(nameof(ImportStageReferenceHelp)); }
    void ImportStageSelectionChanged() { OnPropertyChanged(nameof(ImportStageSummary)); ImportCommand.NotifyCanExecuteChanged(); }
    void ResetImportStages()
    {
        ImportStages.Clear(); ImportStageCases.Clear(); ImportConstructionStages = false;
        OnPropertyChanged(nameof(HasImportStages)); ImportStageSelectionChanged();
    }
    void PopulateImportStages(MidasModelCatalog catalog)
    {
        ResetImportStages();
        foreach (var phase in catalog.Phases) ImportStages.Add(new(phase, ImportStageSelectionChanged));
        foreach (var name in catalog.StageCases) ImportStageCases.Add(new(name, ImportStageSelectionChanged));
        OnPropertyChanged(nameof(HasImportStages)); ImportStageSelectionChanged();
    }
    MidasStageReadRequest? StageImportRequest() => !ImportConstructionStages ? null : new(
        ImportStages.Where(s => s.IsSelected).SelectMany(s => s.Steps.Select(step => new ModelStageStep(s.Phase.Name, step))).ToArray(),
        ImportStageCases.Where(c => c.IsSelected).Select(c => c.Name).ToArray(), ImportDisplacementReference);

    const string EveryStage = "Tutte le fasi", EveryStep = "Tutti i passi";
    bool filteringStages;
    public IReadOnlyList<string> ResultScopes { get; } = ["Tutti i risultati", "Casi statici e combinazioni", "Fasi costruttive"];
    [ObservableProperty] int selectedResultScope;
    [ObservableProperty] string selectedStagePhase = EveryStage;
    [ObservableProperty] string selectedStageStep = EveryStep;
    [ObservableProperty] IReadOnlyList<string> resultStagePhases = [EveryStage];
    [ObservableProperty] IReadOnlyList<string> resultStageSteps = [EveryStep];
    [ObservableProperty] IReadOnlyList<string> filteredCases = [];
    public bool HasStageResults => Snapshot?.Results.Any(r => r.Stage != null) == true;
    public bool ShowStageResultFilters => HasStageResults && SelectedResultScope != 1;
    public bool IsStageResult => Snapshot?.Results.FirstOrDefault(r => r.Name == SelectedCase)?.Stage != null;
    public string StageResultDescription
    {
        get
        {
            var r = Snapshot?.Results.FirstOrDefault(r => r.Name == SelectedCase); var s = r?.Stage;
            return s == null ? "" : $"Fase {s.Phase} · passo {s.Step}\n" +
                (s.DisplacementReference == null ? "Sollecitazioni importate." : $"Deformata: {s.DisplacementReference}.") +
                " Geometria completa in contesto; l’attivazione degli oggetti non è simulata. Gli oggetti senza risultati restano senza valore.";
        }
    }
    partial void OnCasesChanged(IReadOnlyList<string> value) => RefreshStageNavigation();
    partial void OnSelectedResultScopeChanged(int value) => FilterStageResults();
    partial void OnSelectedStagePhaseChanged(string value) { if (!filteringStages) { SelectedStageStep = EveryStep; FilterStageResults(); } }
    partial void OnSelectedStageStepChanged(string value) => FilterStageResults();
    void RefreshStageNavigation()
    {
        filteringStages = true;
        ResultStagePhases = [EveryStage, .. Snapshot?.Results.Where(r => r.Stage != null).Select(r => r.Stage!.Phase).Distinct(StringComparer.Ordinal) ?? []];
        SelectedStagePhase = EveryStage; SelectedStageStep = EveryStep; SelectedResultScope = 0;
        filteringStages = false;
        FilterStageResults(); OnPropertyChanged(nameof(HasStageResults));
    }
    void FilterStageResults()
    {
        if (filteringStages) return;
        filteringStages = true;
        var results = Snapshot?.Results ?? [];
        ResultStageSteps = [EveryStep, .. results.Where(r => r.Stage != null && (SelectedStagePhase == EveryStage || r.Stage.Phase == SelectedStagePhase))
            .Select(r => r.Stage!.Step).Distinct(StringComparer.Ordinal)];
        if (!ResultStageSteps.Contains(SelectedStageStep)) SelectedStageStep = EveryStep;
        FilteredCases = results.Where(r => SelectedResultScope switch {
            1 => r.Stage == null,
            2 => r.Stage != null && MatchStage(r.Stage),
            _ => r.Stage == null || MatchStage(r.Stage)
        }).Select(r => r.Name).ToArray();
        if (!FilteredCases.Contains(SelectedCase ?? "")) SelectedCase = FilteredCases.FirstOrDefault();
        filteringStages = false;
        OnPropertyChanged(nameof(ShowStageResultFilters)); NotifySelectedStage();
    }
    bool MatchStage(ModelStageResult s) => (SelectedStagePhase == EveryStage || s.Phase == SelectedStagePhase) && (SelectedStageStep == EveryStep || s.Step == SelectedStageStep);
    void NotifySelectedStage()
    {
        // Restoring a saved report/view may select a state outside the current browsing filter.
        if (!filteringStages && SelectedCase != null && !FilteredCases.Contains(SelectedCase)) RefreshStageNavigation();
        OnPropertyChanged(nameof(IsStageResult)); OnPropertyChanged(nameof(StageResultDescription));
    }
}
