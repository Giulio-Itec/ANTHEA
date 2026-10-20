using System.ComponentModel;
using Anthea.Presentation.Mvvm;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.EmbeddedWalls;

public sealed partial class EmbeddedWallSheetViewModel
{
    private string inputCase = "";
    private EmbeddedWallRow? inputStage;
    private EmbeddedWallTable? inspectorTable;
    private bool refreshingSketch;
    public IReadOnlyList<string> InputCases { get; private set; } = [];
    public IReadOnlyList<EmbeddedWallRow> InputStages { get; private set; } = [];
    public EmbeddedWallSketch? Sketch { get; private set; }
    public IRelayCommand<string> EditModelCommand { get; private set; } = null!;
    public IRelayCommand<WallSketchPick> PickModelCommand { get; private set; } = null!;
    public IRelayCommand PreviousInputStageCommand { get; private set; } = null!;
    public IRelayCommand NextInputStageCommand { get; private set; } = null!;
    public IRelayCommand OpenDataCommand { get; private set; } = null!;
    public IRelayCommand OpenResultsCommand { get; private set; } = null!;
    private int workspaceTab;
    public int WorkspaceTab { get => workspaceTab; set => SetProperty(ref workspaceTab, value); }
    public string InputCase
    {
        get => inputCase;
        set { if (!refreshingSketch && value is not null && SetProperty(ref inputCase, value)) RefreshSketch(); }
    }
    public EmbeddedWallRow? InputStage
    {
        get => inputStage;
        set
        {
            if (refreshingSketch || value is null || !InputStages.Contains(value) || !SetProperty(ref inputStage, value)) return;
            Tables.Single(t => t.Key == "stages").Selected = value;
            RefreshSketch();
        }
    }
    public int InputStagePosition
    {
        get => InputStageIndex;
        set { if (!refreshingSketch && value >= 0 && value < InputStages.Count) InputStage = InputStages[value]; }
    }
    public EmbeddedWallTable? InspectorTable
    {
        get => inspectorTable;
        set
        {
            if (!SetProperty(ref inspectorTable, value)) return;
            if (value?.Key == "stages" && inputStage is not null) value.Selected = inputStage;
            OnPropertyChanged(nameof(ShowGeneral)); OnPropertyChanged(nameof(InspectorTitle)); OnPropertyChanged(nameof(InspectorHint));
            SyncWorkTreeSelection();
        }
    }
    public bool ShowGeneral => InspectorTable is null;
    public string InspectorTitle => InspectorTable?.Title ?? "Geometria e impostazioni";
    public string InspectorHint => InspectorTable?.Key switch
    {
        "segments" => "Assegna i tratti lungo la parete e la loro rigidezza EI, oppure ricavala dalla sezione.",
        "left_soils" or "right_soils" => "Definisci gli strati tramite quote assolute. Lo scavo rimuove il terreno sopra la quota della fase.",
        "stages" => "Le fasi si eseguono nell'ordine dell'elenco. Abbassa la quota del terreno sul lato scavato; assegna qui falda e sovraccarichi.",
        "supports" => "Definisci il sostegno, poi inseriscilo in «Sostegni presenti nelle fasi». Non è attivo per la sola presenza in questo elenco.",
        "activations" => "Elenco completo per ogni fase: un sostegno omesso viene rimosso. La pretensione appartiene al singolo elemento.",
        "loads" or "distributed_loads" => "Assegna caso e fase. Le forze si riferiscono alla fascia analizzata; le azioni devono essere già combinate.",
        null => "Inizia dalle quote di testa e piede. Poi definisci tratti, terreni e fasi con i pulsanti sopra il disegno.",
        _ => "Seleziona una riga per modificarne i dati. I valori sono condivisi con la vista tabellare."
    };
    public string SketchStatus => Sketch is null ? "" : string.Join(Environment.NewLine, Sketch.Notices);
    public string PhaseSummary => Sketch is null ? "" :
        $"Terreno sx {EmbeddedWallSketch.Format(Sketch.LeftGround)} m  ·  dx {EmbeddedWallSketch.Format(Sketch.RightGround)} m  ·  {Sketch.Supports.Count} sostegni presenti";

    private void InitializeSketch()
    {
        EditModelCommand = new RelayCommand<string>(key => Guard.Run(() =>
        {
            WorkspaceTab = 0;
            InspectorTable = Tables.FirstOrDefault(t => t.Key == key);
        }));
        PickModelCommand = new RelayCommand<WallSketchPick>(pick => Guard.Run(() =>
        {
            if (pick is null) return;
            InspectorTable = Tables.Single(t => t.Key == pick.Table);
            if (pick.Row is not null) InspectorTable.Selected = pick.Row;
        }));
        PreviousInputStageCommand = GuardedCommands.Create(Guard, () => MoveInputStage(-1), () => InputStageIndex > 0);
        NextInputStageCommand = GuardedCommands.Create(Guard, () => MoveInputStage(1), () => InputStageIndex >= 0 && InputStageIndex < InputStages.Count - 1);
        OpenDataCommand = GuardedCommands.Create(Guard, () => WorkspaceTab = 1);
        OpenResultsCommand = GuardedCommands.Create(Guard, () => WorkspaceTab = 3);
        RefreshSketch();
    }
    private int InputStageIndex => inputStage is null ? -1 : InputStages.ToList().IndexOf(inputStage);
    private void MoveInputStage(int delta) => InputStage = InputStages.ElementAtOrDefault(InputStageIndex + delta);
    private void OnSketchTableSelection(object? sender, PropertyChangedEventArgs e)
    {
        if (ReferenceEquals(sender, InspectorTable) && e.PropertyName == nameof(EmbeddedWallTable.Selected)) SyncWorkTreeSelection();
        if (refreshingSketch || sender is not EmbeddedWallTable { Key: "stages", Selected: { } row } || e.PropertyName != nameof(EmbeddedWallTable.Selected)) return;
        string principal = Data["case_id"]?.ToString() ?? "";
        inputCase = string.IsNullOrWhiteSpace(row["case"]) ? principal : row["case"];
        inputStage = row;
        RefreshSketch();
    }
    private void RefreshSketch()
    {
        if (refreshingSketch || Tables.Count == 0) return;
        refreshingSketch = true;
        try
        {
            string principal = Data["case_id"]?.ToString() ?? "";
            var cases = new[] { principal }.Concat(Tables.Single(t => t.Key == "cases").Rows.Select(r => r["id"])).Distinct().ToArray();
            bool casesChanged = !InputCases.SequenceEqual(cases);
            if (casesChanged) InputCases = cases;
            if (!InputCases.Contains(inputCase)) inputCase = principal;
            var stages = Tables.Single(t => t.Key == "stages").Rows
                .Where(r => (string.IsNullOrWhiteSpace(r["case"]) ? principal : r["case"]) == inputCase).ToArray();
            bool stagesChanged = !InputStages.SequenceEqual(stages);
            if (stagesChanged)
            {
                var nextStage = inputStage is not null && stages.Contains(inputStage) ? inputStage : stages.LastOrDefault();
                // Reset the selection before replacing the row instances (reload/case switch).
                // Otherwise WPF may retain the old index with a null SelectedItem.
                inputStage = null;
                OnPropertyChanged(nameof(InputStage)); OnPropertyChanged(nameof(InputStagePosition));
                InputStages = stages; OnPropertyChanged(nameof(InputStages));
                inputStage = nextStage;
            }
            if (inputStage is null || !InputStages.Contains(inputStage)) inputStage = InputStages.LastOrDefault();
            Sketch = EmbeddedWallSketch.Read(Data, Tables, inputCase, inputStage);
            if (casesChanged) OnPropertyChanged(nameof(InputCases));
            OnPropertyChanged(nameof(InputCase));
            OnPropertyChanged(nameof(InputStage)); OnPropertyChanged(nameof(InputStagePosition));
            OnPropertyChanged(nameof(Sketch)); OnPropertyChanged(nameof(SketchStatus)); OnPropertyChanged(nameof(PhaseSummary));
            PreviousInputStageCommand?.NotifyCanExecuteChanged(); NextInputStageCommand?.NotifyCanExecuteChanged();
        }
        finally { refreshingSketch = false; }
    }
}
