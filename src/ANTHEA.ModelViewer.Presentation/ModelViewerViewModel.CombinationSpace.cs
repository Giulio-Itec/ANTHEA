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

    void NotifyCombinationSpaceCommands(){FreeSelectedCombinationResultsCommand.NotifyCanExecuteChanged();FreeAllCombinationResultsCommand.NotifyCanExecuteChanged();}

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
        try
        {
            if(!await PublishCombinationChangeAsync(s=>ModelCombinations.DiscardResults(s,ids),"Liberazione dei risultati salvati…"))return;
        }
        catch(Exception ex) when(ex is InvalidDataException or InvalidOperationException){CombinationMessage="Risultati non liberati: "+ex.Message;return;}
        RefreshCombinations(selected);
        CombinationMessage=$"Risultati di {ids.Count} combinazioni liberati: le definizioni restano e si ricalcolano quando servono.";
    }
}
