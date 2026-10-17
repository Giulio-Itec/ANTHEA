using Anthea.Presentation.Mvvm;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.RetainingWall;

/// <summary>
/// The commands of the buttons of the wall view (F5.9, X.Desktop/Views/RetainingWall), on the methods of the sheet. Every command goes
/// through the guard of the sheet (Q19: an error is shown in the screen). The commands that change the data are disabled when the sheet
/// is read-only, as the historic buttons protected by RevisionInspection; those the historic view allows in a revision (calculation,
/// report, CSV, values, «Sisma», «Stabilità globale», global calculation and its Word, «Modifica profilo, strati e ricerca») stay enabled.
/// The commands that start a background work (calculation, global calculation, search of the reinforcement, values) start it and return,
/// as the historic buttons: the schedulers of the sheet decide which work is published, and a second click is not refused.
/// </summary>
public sealed partial class RetainingWallSheetViewModel
{
    private readonly List<IRelayCommand> editCommands = [];

    private void CreateCommands()
    {
        LoadExampleCommand = Track(new RelayCommand<string>(example => Guard.Run(() => { if (example is not null) LoadExample(example); }), _ => CanEdit));
        RecalculateCommand = Start(RecalculateAsync);
        ReportCommand = GuardedCommands.Create(Guard, RequestReport);
        ExportCsvCommand = GuardedCommands.Create(Guard, ExportCsv);
        ShowSeismicCommand = GuardedCommands.Create(Guard, ShowSeismic);
        ShowGlobalSetupCommand = GuardedCommands.Create(Guard, ShowGlobalSetup);
        ParametersCommand = Start(ShowParametersAsync);
        GenerateMatrixCommand = Edit(GenerateMatrix);
        CopyCombinationCommand = Edit(CopyCombination);
        RemoveCombinationCommand = Edit(RemoveCombination);
        ConfirmMatrixCommand = Edit(ConfirmMatrix);
        AddSoilLayerCommand = Column(valley => AddSoilLayer(valley));
        RemoveSoilLayerCommand = Column(valley => RemoveSoilLayer(valley));
        MoveSoilLayerUpCommand = Column(valley => MoveSoilLayer(valley, -1));
        MoveSoilLayerDownCommand = Column(valley => MoveSoilLayer(valley, 1));
        TransferSoilCommand = Track(GuardedCommands.CreateAsync(Guard, _ => TransferSoilAsync(), Lifetime, () => CanEdit));
        AddServiceLayerCommand = Edit(AddServiceLayer);
        RemoveServiceLayerCommand = Edit(RemoveServiceLayer);
        AdoptMinimumCoverCommand = Edit(AdoptMinimumCover);
        DesignRebarCommand = Start(DesignRebarAsync, () => CanDesign);
        InterruptSearchCommand = Edit(InterruptSearch);
        AddActionCommand = Edit(AddAction);
        RemoveActionCommand = Edit(RemoveAction);
        AddHistoryCommand = Edit(AddHistory);
        ImportHistoryCommand = Track(new RelayCommand<RetainingWallHistory>(history => Guard.Run(() => ImportHistory(HistoryIndex(history))), _ => CanEdit));
        RemoveHistoryCommand = Track(new RelayCommand<RetainingWallHistory>(history => Guard.Run(() => RemoveHistory(HistoryIndex(history))), _ => CanEdit));
        PrepareGlobalCommand = Edit(PrepareGlobal);
        CalculateGlobalCommand = Start(CalculateGlobalAsync);
        GenerateGlobalMatrixCommand = Edit(GenerateGlobalMatrix);
        ConfirmGlobalMatrixCommand = Edit(ConfirmGlobalMatrix);
        ExportGlobalWordCommand = GuardedCommands.Create(Guard, ExportGlobalWord);
        AddGlobalLayerCommand = Column(valley => AddGlobalLayer(valley));
        RemoveGlobalLayerCommand = Column(valley => RemoveGlobalLayer(valley));
        AddProfilePointCommand = Track(new RelayCommand<string>(table => Guard.Run(() => { if (table is not null) AddProfilePoint(table); }), _ => CanEdit));
        RemoveProfilePointCommand = Track(new RelayCommand<string>(table => Guard.Run(() => { if (table is not null) RemoveProfilePoint(table); }), _ => CanEdit));
        SendSectionCommand = GuardedCommands.Create(Guard, SendSelectedSection, () => CanSendSection);
        PanelCommand = Track(new RelayCommand<string>(command => Guard.Run(() => RunPanelCommand(command)), command => command == EditGlobalSetupCommand || CanEdit));
    }

    /// <summary>«Mensola esempio» / «Gravità esempio»: the parameter is the typology of the example (cantilever, gravity).</summary>
    public IRelayCommand<string> LoadExampleCommand { get; private set; } = null!;

    /// <summary>«Ricalcola» (<see cref="RecalculateAsync"/>), also when read-only.</summary>
    public IRelayCommand RecalculateCommand { get; private set; } = null!;

    /// <summary>«Relazione Word» (<see cref="RequestReport"/>), also when read-only.</summary>
    public IRelayCommand ReportCommand { get; private set; } = null!;

    /// <summary>«CSV» (<see cref="ExportCsv"/>), also when read-only.</summary>
    public IRelayCommand ExportCsvCommand { get; private set; } = null!;

    /// <summary>The «Sisma» button (<see cref="ShowSeismic"/>), also when read-only.</summary>
    public IRelayCommand ShowSeismicCommand { get; private set; } = null!;

    /// <summary>The «Stabilità globale» button (<see cref="ShowGlobalSetup"/>), also when read-only.</summary>
    public IRelayCommand ShowGlobalSetupCommand { get; private set; } = null!;

    /// <summary>«Valori di calcolo…» (<see cref="ShowParametersAsync"/>), also when read-only.</summary>
    public IRelayCommand ParametersCommand { get; private set; } = null!;

    /// <summary>The buttons of the combination matrix.</summary>
    public IRelayCommand GenerateMatrixCommand { get; private set; } = null!;
    public IRelayCommand CopyCombinationCommand { get; private set; } = null!;
    public IRelayCommand RemoveCombinationCommand { get; private set; } = null!;
    public IRelayCommand ConfirmMatrixCommand { get; private set; } = null!;

    /// <summary>«+ Strato», «−», «↑», «↓» of a soil column: the parameter is the column, «valle» or «monte».</summary>
    public IRelayCommand<string> AddSoilLayerCommand { get; private set; } = null!;
    public IRelayCommand<string> RemoveSoilLayerCommand { get; private set; } = null!;
    public IRelayCommand<string> MoveSoilLayerUpCommand { get; private set; } = null!;
    public IRelayCommand<string> MoveSoilLayerDownCommand { get; private set; } = null!;

    /// <summary>«Invia / carica terreno…» (<see cref="TransferSoilAsync"/>), disabled when read-only.</summary>
    public IAsyncRelayCommand TransferSoilCommand { get; private set; } = null!;

    /// <summary>«+ Strato» and «− Strato» of the deformable layers.</summary>
    public IRelayCommand AddServiceLayerCommand { get; private set; } = null!;
    public IRelayCommand RemoveServiceLayerCommand { get; private set; } = null!;

    /// <summary>«Adotta copriferro minimo».</summary>
    public IRelayCommand AdoptMinimumCoverCommand { get; private set; } = null!;

    /// <summary>«Calcola armature» (<see cref="DesignRebarAsync"/>): enabled as <see cref="CanDesign"/>.</summary>
    public IRelayCommand DesignRebarCommand { get; private set; } = null!;

    /// <summary>«Interrompi ricerca».</summary>
    public IRelayCommand InterruptSearchCommand { get; private set; } = null!;

    /// <summary>«+ Azione» (of the type <see cref="NewActionType"/>) and «− Azione».</summary>
    public IRelayCommand AddActionCommand { get; private set; } = null!;
    public IRelayCommand RemoveActionCommand { get; private set; } = null!;

    /// <summary>«+ Accelerogramma»; «Importa CSV t; a/g» and «Elimina accelerogramma» of an accelerogram (the parameter, one of <see cref="Histories"/>).</summary>
    public IRelayCommand AddHistoryCommand { get; private set; } = null!;
    public IRelayCommand<RetainingWallHistory> ImportHistoryCommand { get; private set; } = null!;
    public IRelayCommand<RetainingWallHistory> RemoveHistoryCommand { get; private set; } = null!;

    /// <summary>«Prepara dal muro».</summary>
    public IRelayCommand PrepareGlobalCommand { get; private set; } = null!;

    /// <summary>«Calcola globale» (<see cref="CalculateGlobalAsync"/>), also when read-only.</summary>
    public IRelayCommand CalculateGlobalCommand { get; private set; } = null!;

    /// <summary>«Genera globali» and «Conferma matrice globale».</summary>
    public IRelayCommand GenerateGlobalMatrixCommand { get; private set; } = null!;
    public IRelayCommand ConfirmGlobalMatrixCommand { get; private set; } = null!;

    /// <summary>«Word globale» (<see cref="ExportGlobalWord"/>), also when read-only.</summary>
    public IRelayCommand ExportGlobalWordCommand { get; private set; } = null!;

    /// <summary>«+ Strato» and «− Strato» of a column of the deep layers: the parameter is the column, «valle» or «monte».</summary>
    public IRelayCommand<string> AddGlobalLayerCommand { get; private set; } = null!;
    public IRelayCommand<string> RemoveGlobalLayerCommand { get; private set; } = null!;

    /// <summary>«+ Punto» and «− Punto» of a table of the points: the parameter is the table («punti-valle», «punti-monte», «punti-falda»).</summary>
    public IRelayCommand<string> AddProfilePointCommand { get; private set; } = null!;
    public IRelayCommand<string> RemoveProfilePointCommand { get; private set; } = null!;

    /// <summary>«Apri sezione in c.a.» (<see cref="SendSelectedSection"/>): enabled as <see cref="CanSendSection"/>.</summary>
    public IRelayCommand SendSectionCommand { get; private set; } = null!;

    /// <summary>
    /// A button of a panel of results (<see cref="WallPanelBlock.Command"/>): «Applica proposta» (<see cref="ApplyProposalCommand"/>, disabled
    /// when read-only) or «Modifica profilo, strati e ricerca» (<see cref="EditGlobalSetupCommand"/>, also when read-only).
    /// </summary>
    public IRelayCommand<string> PanelCommand { get; private set; } = null!;

    private void RunPanelCommand(string? command)
    {
        switch (command)
        {
            case ApplyProposalCommand: ApplyProposal(); break;
            case EditGlobalSetupCommand: ShowGlobalSetup(); break;
            default: throw new ArgumentException("Comando del pannello sconosciuto: " + command, nameof(command));
        }
    }

    /// <summary>The index of an accelerogram from its form key («history_n»), −1 for an unknown one.</summary>
    private int HistoryIndex(RetainingWallHistory? history) => history is null ? -1 : Histories.ToList().FindIndex(h => h.FormKey == history.FormKey);

    private IRelayCommand Edit(Action action) => Track(GuardedCommands.Create(Guard, action, () => CanEdit));

    private IRelayCommand<string> Column(Action<bool> action) =>
        Track(new RelayCommand<string>(column => Guard.Run(() => action(Valley(column))), _ => CanEdit));

    private static bool Valley(string? column) => column switch
    {
        "valle" => true,
        "monte" => false,
        _ => throw new ArgumentException("Colonna sconosciuta: " + column, nameof(column))
    };

    /// <summary>A command that starts a background work and returns (the historic async click); errors are shown by the guard.</summary>
    private IRelayCommand Start(Func<Task> work, Func<bool>? canExecute = null)
    {
        void Run() => _ = Guard.RunAsync(_ => work());
        return canExecute is null ? new RelayCommand(Run) : new RelayCommand(Run, canExecute);
    }

    private T Track<T>(T command) where T : IRelayCommand { editCommands.Add(command); return command; }

    /// <summary>The sheet became read-only, editable again or closed: the buttons that change the data follow.</summary>
    private void NotifyCommands()
    {
        foreach (var command in editCommands) command.NotifyCanExecuteChanged();
        DesignRebarCommand?.NotifyCanExecuteChanged();
        SendSectionCommand?.NotifyCanExecuteChanged();
    }
}
