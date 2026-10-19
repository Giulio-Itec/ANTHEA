using Anthea.Reports;

namespace Anthea.Presentation.Modules.EmbeddedWalls;

public sealed partial class EmbeddedWallSheetViewModel
{
    private EmbeddedWallReportData? selectedResultCase;
    private EmbeddedWallReportData? DisplayedCase => selectedResultCase ?? current;
    public IReadOnlyList<EmbeddedWallReportData> ResultCases => current?.Cases ?? (current is null ? [] : new[] { current });
    public EmbeddedWallReportData? SelectedResultCase
    {
        get => selectedResultCase;
        set
        {
            if (value is not null && !ResultCases.Any(r => ReferenceEquals(r, value))) return;
            if (ReferenceEquals(selectedResultCase, value)) return;
            selectedResultCase = value; OnPropertyChanged(nameof(SelectedResultCase));
            SelectedStage = Stages.LastOrDefault(); ComparisonStage = null;
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
        }
    }
    public string ResultCaseStatus => DisplayedCase is null ? ""
        : DisplayedCase.CaseId + " · " + (DisplayedCase.Complete ? "storia convergente" : "storia incompleta") + " · " + DisplayedCase.ActionBasis;
}
