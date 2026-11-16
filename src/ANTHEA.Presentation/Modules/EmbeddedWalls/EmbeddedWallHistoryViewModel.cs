using Anthea.Reports;

namespace Anthea.Presentation.Modules.EmbeddedWalls;

public sealed partial class EmbeddedWallSheetViewModel
{
    private EmbeddedWallReportData? selectedResultCase;
    /// <summary>The results of the case on screen (contract C2 of the project of 16/11): never those of another case.</summary>
    private EmbeddedWallReportData? DisplayedCase => selectedResultCase;
    public IReadOnlyList<EmbeddedWallReportData> ResultCases => current?.Cases ?? (current is null ? [] : new[] { current });
    public EmbeddedWallReportData? SelectedResultCase
    {
        get => selectedResultCase;
        set
        {
            if (value is not null && !ResultCases.Any(r => ReferenceEquals(r, value))) return;
            if (ReferenceEquals(selectedResultCase, value)) return;
            selectedResultCase = value; OnPropertyChanged(nameof(SelectedResultCase));
            SelectedStage = KeyedResultStage(); ComparisonStage = null;
            SelectedHydraulicStage = HydraulicStages.LastOrDefault();
            OnPropertyChanged(nameof(Stages)); OnPropertyChanged(nameof(Points)); OnPropertyChanged(nameof(Checks)); OnPropertyChanged(nameof(Supports));
            OnPropertyChanged(nameof(HydraulicStages)); OnPropertyChanged(nameof(HydraulicChecks)); SelectedHydraulicCheck = HydraulicChecks.FirstOrDefault();
            OnPropertyChanged(nameof(GlobalChecks)); SelectedGlobalCheck = GlobalChecks.FirstOrDefault();
            OnPropertyChanged(nameof(SteelSupports)); SelectedSteelSupport = SteelSupports.FirstOrDefault();
            OnPropertyChanged(nameof(AnchorChecks)); SelectedAnchorCheck = AnchorChecks.FirstOrDefault();
            OnPropertyChanged(nameof(SteelTubes)); SelectedTubeCheck = SteelTubes.FirstOrDefault();
            OnPropertyChanged(nameof(RcPiles)); SelectedRcPileCheck = RcPiles.FirstOrDefault();
            OnPropertyChanged(nameof(Scope)); OnPropertyChanged(nameof(ResultCaseStatus));
            HydraulicCsvCommand.NotifyCanExecuteChanged(); HydraulicChecksCsvCommand.NotifyCanExecuteChanged();
            GlobalCsvCommand.NotifyCanExecuteChanged(); SteelCsvCommand.NotifyCanExecuteChanged(); AnchorCsvCommand.NotifyCanExecuteChanged();
            TubeCsvCommand.NotifyCanExecuteChanged(); RcPileCsvCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(StageResultStatus));
            // Step 4 of the project of 16/11 (D3, C2): one case for input and results.
            if (value is not null && InputCases.Contains(value.CaseId) && inputCase != value.CaseId) InputCase = value.CaseId;
        }
    }
    public string ResultCaseStatus => DisplayedCase is null ? (current is null ? "" : "Risultato non disponibile per il caso «" + inputCase + "».")
        : DisplayedCase.CaseId + " · " + (DisplayedCase.Complete ? "storia convergente" : "storia incompleta") + " · " + DisplayedCase.ActionBasis;
}
