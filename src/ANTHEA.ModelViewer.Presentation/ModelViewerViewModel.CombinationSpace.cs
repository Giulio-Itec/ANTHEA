using ANTHEA.ModelWorkspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ANTHEA.ModelViewer.Presentation;

/// <summary>Request of the user of 3/11/2026 («riesci a mettere un pannello di gestione limite? o qualcosa del genere»), after «Calcola tutte» of
/// 1.17.6 reached the 2 GiB of a MIDAS model of 3447 plates and 49 cases: the space the copy takes with its combinations, the limit chosen by
/// the user among those the memory of the computer allows (ModelResultArchive.ArchivedCopyLimits, kept by the services) and the release of the
/// saved results of chosen combinations, whose definitions stay and are calculated again when needed. Only with the results archive of the
/// project (<see cref="CombinationArchive"/>); without it the whole copy is in the document, within ModelSnapshotFiles.MaximumPackageBytes.</summary>
public sealed partial class ModelViewerViewModel
{
    int spaceOperation;
    static long ComputerMemory()=>GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

    /// <summary>True when the project of the host has a results archive (the shell): the copy may take the limit chosen by the user.</summary>
    public bool CombinationArchive {get;init;}
    /// <summary>The limits of the copy offered on this computer, in bytes of JSON.</summary>
    public IReadOnlyList<long> CombinationLimits {get;}=ModelResultArchive.ArchivedCopyLimits(ComputerMemory());
    public IReadOnlyList<string> CombinationLimitLabels=>[..CombinationLimits.Select(ModelCombinations.CopyLimit)];
    [ObservableProperty] int selectedCombinationLimit;
    [ObservableProperty] long combinationSpaceBytes;
    [ObservableProperty] string combinationSpaceText="";
    /// <summary>The JSON bytes the copy may take with its combinations (ModelCombinations.Attach).</summary>
    public long CombinationCopyBytes=>CombinationArchive?CombinationLimits[SelectedCombinationLimit]:ModelSnapshotFiles.MaximumPackageBytes;
    public string CombinationMemoryText=>$"Spazio massimo della copia con i risultati delle combinazioni; oltre, escono dalla cache quelle calcolate per prime. Memoria del computer {ModelCombinations.CopyLimit(ComputerMemory())}: limiti fino a {ModelCombinations.CopyLimit(CombinationLimits.Last())}.";

    /// <summary>The index of the limit the user chose before, or the first one.</summary>
    int SavedCombinationLimit()=>services.LoadCombinationLimit() is {} saved&&CombinationLimits.Contains(saved)?CombinationLimits.ToList().IndexOf(saved):0;

    partial void OnSelectedCombinationLimitChanged(int value)
    {
        services.SaveCombinationLimit(CombinationLimits[value]);
        OnPropertyChanged(nameof(CombinationCopyBytes));RefreshCombinationSpace();
    }

    /// <summary>The space of the copy, measured off the thread of the interface.</summary>
    void RefreshCombinationSpace(){if(CombinationArchive)_=RefreshCombinationSpaceAsync();}
    async Task RefreshCombinationSpaceAsync()
    {
        if(Snapshot is not {} snapshot)return;
        int operation=++spaceOperation;
        int saved=snapshot.Results.Select(r=>r.Combination?.DefinitionId).OfType<string>().Distinct(StringComparer.Ordinal).Count();
        long bytes;
        try {bytes=await Task.Run(()=>ModelCombinations.CopyBytes(snapshot),lifetime.Token);}
        catch(Exception ex) when(ex is OperationCanceledException or InvalidDataException or InvalidOperationException){return;}
        if(disposed||operation!=spaceOperation)return;
        CombinationSpaceBytes=bytes;
        CombinationSpaceText=$"{ModelCombinations.CopyLimit(bytes)} di {ModelCombinations.CopyLimit(CombinationCopyBytes)} · {saved} combinazioni con risultati salvati";
        NotifyCombinationSpaceCommands();
    }

    void NotifyCombinationSpaceCommands(){FreeSelectedCombinationResultsCommand.NotifyCanExecuteChanged();FreeAllCombinationResultsCommand.NotifyCanExecuteChanged();NotifyUndoFree();}

    IEnumerable<string> SavedDefinitions(IEnumerable<string> ids)=>Snapshot==null?[]:
        ids.Where(id=>Snapshot.Results.Any(r=>r.Combination?.DefinitionId==id)).Distinct(StringComparer.Ordinal);
    bool CanFreeSelected()=>CanStartCombination()&&!IsCombinationBusy&&SavedDefinitions(SelectedCombinationIds).Any();
    bool CanFreeAll()=>CanStartCombination()&&!IsCombinationBusy&&Snapshot?.Results.Any(r=>r.Combination!=null)==true;
    /// <summary>«Libera selezionate»: the saved results of the combinations selected in the list leave the copy.</summary>
    [RelayCommand(CanExecute=nameof(CanFreeSelected))]
    Task FreeSelectedCombinationResultsAsync()=>FreeCombinationResultsAsync([..SavedDefinitions(SelectedCombinationIds)]);
    /// <summary>«Libera tutte»: every saved result of a combination leaves the copy.</summary>
    [RelayCommand(CanExecute=nameof(CanFreeAll))]
    Task FreeAllCombinationResultsAsync()=>FreeCombinationResultsAsync([..Snapshot!.Results.Select(r=>r.Combination?.DefinitionId).OfType<string>().Distinct(StringComparer.Ordinal)]);

    async Task FreeCombinationResultsAsync(IReadOnlyCollection<string> ids)
    {
        string? selected=SelectedCombination?.Definition.Id;
        string label=FreeLabel(ids.Count);
        try
        {
            if(!await PublishCombinationChangeAsync(s=>ModelCombinations.DiscardResults(s,ids),"Liberazione dei risultati salvati…",label))return;
        }
        catch(Exception ex) when(ex is InvalidDataException or InvalidOperationException){CombinationMessage="Risultati non liberati: "+ex.Message;return;}
        // R6 (16/11/2026): the memory of the results freed goes back to Windows (18 GB stayed with the process after «Libera tutte»).
        if(!disposed)await SystemMemory.ReleaseAsync();
        if(disposed)return;
        RefreshCombinations(selected);
        freedLabel=UndoProjectChange is null?null:label;NotifyUndoFree();
        // R4 (16/11/2026): where the undo is, right after «Libera» (the user did not find it in the main window).
        CombinationMessage=$"Risultati di {ids.Count} combinazioni liberati: le definizioni restano e si ricalcolano quando servono.\n"+
            (UndoProjectChange is null?"Per riaverli usa «↶ Annulla» del progetto (Ctrl+Z), nella barra in alto della finestra principale."
                :"Per riaverli usa «↶ Annulla Libera» qui sotto, o «↶ Annulla» del progetto (Ctrl+Z) nella barra in alto della finestra principale, finché non fai altre modifiche.");
    }

    /// <summary>The name of «Libera» in the history of the project.</summary>
    public static string FreeLabel(int count)=>count==1?"Libera i risultati di una combinazione":$"Libera i risultati di {count} combinazioni";

    /// <summary>
    /// Rilievo R4 (16/11/2026): the undo of the last change of the project when it is still the one named <c>label</c> (the host, the tab of
    /// the model: ModelTabViewModel.UndoChange); false when another change came after it. Null outside a project: no «↶ Annulla Libera».
    /// </summary>
    public Func<string,bool>? UndoProjectChange {get;init;}
    string? freedLabel;
    /// <summary>«↶ Annulla Libera» is offered: right after «Libera», until another change of the copy.</summary>
    public bool CanUndoFree=>UndoProjectChange!=null&&freedLabel!=null&&!IsCombinationBusy&&!IsBusy&&!disposed;
    void NotifyUndoFree(){OnPropertyChanged(nameof(CanUndoFree));UndoFreeCommand.NotifyCanExecuteChanged();}
    /// <summary>«↶ Annulla Libera»: the project goes back to before «Libera»; the tab reads the copy with its results again.</summary>
    [RelayCommand(CanExecute=nameof(CanUndoFree))]
    void UndoFree()
    {
        string label=freedLabel!;freedLabel=null;NotifyUndoFree();
        if(!UndoProjectChange!(label)&&!disposed)
            CombinationMessage="Non annullato: dopo «Libera» il progetto è cambiato. Usa «↶ Annulla» della finestra principale (Ctrl+Z) per tornare indietro una modifica alla volta.";
    }
}
