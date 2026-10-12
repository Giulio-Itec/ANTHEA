using System.Collections.ObjectModel;
using System.Globalization;
using ANTHEA.ModelWorkspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ANTHEA.ModelViewer.Presentation;

public sealed record CombinationSource(string Id,string Name,bool IsCombination,string Analysis="ST")
{
    public string Label=>(IsCombination?"Σ  ":"↓  ")+Name;
    public override string ToString()=>Label;
}

public sealed partial class CombinationTermEditor:ObservableObject
{
    [ObservableProperty] CombinationSource? source;
    [ObservableProperty] string factor="1";
}

public sealed partial class ModelViewerViewModel
{
    readonly GpcModelCombinationService combinationService=new();
    bool loadingCombination;
    int combinationOperation;
    string? editingCombinationId;
    string editingCombinationSource="ANTHEA";
    string editingCombinationStatus="";
    [ObservableProperty] bool showCombinationManager;
    [ObservableProperty] IReadOnlyList<ModelCombinationAvailability> combinations=[];
    [ObservableProperty] ModelCombinationAvailability? selectedCombination;
    [ObservableProperty] IReadOnlyList<CombinationSource> combinationSources=[];
    [ObservableProperty] string combinationName="";
    [ObservableProperty] int selectedCombinationKind;
    [ObservableProperty] bool isCombinationDirty;
    [ObservableProperty] bool isCombinationBusy;
    [ObservableProperty] string combinationMessage="Seleziona una combinazione oppure creane una nuova.";
    [ObservableProperty] CombinationTermEditor? selectedCombinationTerm;
    public ObservableCollection<CombinationTermEditor> CombinationTerms {get;}=[];
    public IReadOnlyList<string> CombinationKindLabels {get;}=["Somma lineare","Inviluppo min / max","Valori assoluti · sola consultazione","SRSS · sola consultazione"];
    public bool CanManageCombinations=>Snapshot!=null&&!IsReadOnly&&!IsBusy&&!IsVerificationRunning&&!IsReportRunning&&!IsRevisionBusy&&!disposed;
    public bool CanEditCombination=>CanManageCombinations&&editingCombinationId!=null;
    public bool CanChooseCombination=>!IsCombinationDirty&&!IsCombinationBusy;
    public string CombinationSummary=>$"{Combinations.Count} combinazioni · {Combinations.Count(c=>c.State=="Calcolata")} calcolate";
    public string CombinationOrigin=>SelectedCombination is {} selected&&!IsCombinationDirty
        ?$"{selected.Definition.Source} · {selected.State}. {selected.Message}"
        :"Definizione locale: salva le modifiche prima di calcolare.";
    public string CombinationActionLabel=>SelectedCombination?.State=="Calcolata"?"Visualizza risultati":"Calcola e visualizza";
    public string CombinationRules=>SelectedCombinationKind==1
        ?"Ogni termine è un’alternativa. Gli estremi sono calcolati per componente; il governante conserva il proprio stato concomitante."
        :"I termini vengono sommati con i coefficienti indicati. Un inviluppo annidato genera più alternative.";

    partial void OnSelectedCombinationChanged(ModelCombinationAvailability? value)
    {
        if(loadingCombination)return;
        LoadCombination(value?.Definition);
        SyncCombinationGridSelection();
    }
    partial void OnCombinationNameChanged(string value)=>CombinationEdited();
    partial void OnSelectedCombinationKindChanged(int value){CombinationEdited();OnPropertyChanged(nameof(CombinationRules));}
    partial void OnIsCombinationDirtyChanged(bool value){NotifyCombinationCommands();ImportCommand.NotifyCanExecuteChanged();}
    partial void OnSelectedCombinationTermChanged(CombinationTermEditor? value)=>RemoveCombinationTermCommand.NotifyCanExecuteChanged();

    void CombinationEdited()
    {
        if(loadingCombination)return;
        IsCombinationDirty=true;
        CombinationMessage="Modifiche da salvare. I risultati già calcolati verranno invalidati al salvataggio.";
    }
    void AddTermEditor(CombinationSource? source,string factor)
    {
        var term=new CombinationTermEditor{Source=source,Factor=factor};
        term.PropertyChanged+=(_,_)=>CombinationEdited();CombinationTerms.Add(term);
    }
    void LoadCombination(ModelCombinationDefinition? definition)
    {
        loadingCombination=true;
        editingCombinationId=definition?.Id;editingCombinationSource=definition?.Source??"ANTHEA";
        editingCombinationStatus=definition?.Status??"";
        CombinationName=definition?.Name??"";SelectedCombinationKind=(int)(definition?.Kind??ModelCombinationKind.Linear);
        CombinationSources=Snapshot==null?[]:ModelCombinations.StaticCaseNames(Snapshot).Select(c=>new CombinationSource(c,c,false))
            .Concat(ModelCombinations.Read(Snapshot).Where(d=>d.Id!=definition?.Id).Select(d=>new CombinationSource(d.Id,d.Name+" ["+d.Source+"]",true))).ToArray();
        CombinationTerms.Clear();SelectedCombinationTerm=null;
        foreach(var term in definition?.Terms??[])
        {
            var source=CombinationSources.FirstOrDefault(s=>s.Id==term.Reference&&s.IsCombination==term.IsCombination&&(term.IsCombination||s.Analysis==term.Analysis))
                ??new(term.Reference,term.Reference+" · "+term.Analysis,term.IsCombination,term.Analysis);
            if(!CombinationSources.Contains(source))CombinationSources=[..CombinationSources,source];
            AddTermEditor(source,term.Factor.ToString("R",CultureInfo.CurrentCulture));
        }
        IsCombinationDirty=false;loadingCombination=false;
        NotifyCombinationCommands();
    }
    void RefreshCombinations(string? selectId=null)
    {
        loadingCombination=true;
        Combinations=Snapshot==null?[]:ModelCombinations.Inspect(Snapshot);
        SelectedCombination=Combinations.FirstOrDefault(c=>c.Definition.Id==selectId)??Combinations.FirstOrDefault();
        loadingCombination=false;LoadCombination(SelectedCombination?.Definition);
        OnPropertyChanged(nameof(CombinationSummary));
        if(ShowCombinationGrid)BuildCombinationGrid();
    }
    void NotifyCombinationCommands()
    {
        foreach(string property in new[]{nameof(CanManageCombinations),nameof(CanEditCombination),nameof(CanChooseCombination),nameof(CombinationOrigin),nameof(CombinationActionLabel)})OnPropertyChanged(property);
        NewCombinationCommand.NotifyCanExecuteChanged();DuplicateCombinationCommand.NotifyCanExecuteChanged();SaveCombinationCommand.NotifyCanExecuteChanged();
        DeleteCombinationCommand.NotifyCanExecuteChanged();RestoreImportedCombinationsCommand.NotifyCanExecuteChanged();
        AddCombinationTermCommand.NotifyCanExecuteChanged();RemoveCombinationTermCommand.NotifyCanExecuteChanged();CalculateCombinationCommand.NotifyCanExecuteChanged();
        DiscardCombinationEditsCommand.NotifyCanExecuteChanged();
    }
    [RelayCommand] void OpenCombinationManager()
    {
        if(Snapshot==null)return;
        if(SelectedPanel!=ModelViewerPanels.Combinations){SelectedPanel=ModelViewerPanels.Combinations;return;}
        ShowImportOptions=false;IsNavigationCollapsed=false;ShowCombinationManager=!IsCombinationDetached;PauseAnimation();
        if(IsCombinationDetached)CombinationWindowChanged?.Invoke(this,EventArgs.Empty);
        if(!IsCombinationDirty&&!IsCombinationBusy)RefreshCombinations(SelectedTreeItem?.Table=="COMBINATION"?SelectedTreeItem.Key[6..]:Snapshot.Results.FirstOrDefault(r=>r.Name==SelectedCase)?.Combination?.DefinitionId??SelectedCombination?.Definition.Id);
    }
    [RelayCommand] void CloseCombinationManager(){IsNavigationCollapsed=true;ShowCombinationManager=false;IsCombinationDetached=false;}
    bool CanStartCombination()=>CanManageCombinations&&!IsCombinationDirty;
    [RelayCommand(CanExecute=nameof(CanStartCombination))] void NewCombination()
    {
        ShowCombinationEditor=true;
        LoadCombination(new("ANTHEA/"+Guid.NewGuid().ToString("N"),"Nuova combinazione",ModelCombinationKind.Linear,"ANTHEA","",[],true));
        loadingCombination=true;SelectedCombination=null;loadingCombination=false;IsCombinationDirty=true;AddCombinationTerm();
    }
    bool CanDuplicateCombination()=>CanStartCombination()&&SelectedCombination!=null;
    [RelayCommand(CanExecute=nameof(CanDuplicateCombination))] void DuplicateCombination()
    {
        ShowCombinationEditor=true;
        var definition=SelectedCombination!.Definition;
        LoadCombination(definition with {Id="ANTHEA/"+Guid.NewGuid().ToString("N"),Name=definition.Name+" · copia",Source="ANTHEA",Editable=true});
        loadingCombination=true;SelectedCombination=null;loadingCombination=false;IsCombinationDirty=true;
    }
    [RelayCommand(CanExecute=nameof(CanEditCombination))] void AddCombinationTerm()
    {AddTermEditor(CombinationSources.FirstOrDefault(s=>!s.IsCombination),"1");CombinationEdited();}
    bool CanRemoveCombinationTerm()=>CanEditCombination&&SelectedCombinationTerm!=null;
    [RelayCommand(CanExecute=nameof(CanRemoveCombinationTerm))] void RemoveCombinationTerm()
    {CombinationTerms.Remove(SelectedCombinationTerm!);SelectedCombinationTerm=null;CombinationEdited();}
    bool CanSaveCombination()=>CanEditCombination&&IsCombinationDirty;
    [RelayCommand(CanExecute=nameof(CanSaveCombination))] void SaveCombination()
    {
        try
        {
            var terms=CombinationTerms.Select(t=>
            {
                if(t.Source==null)throw new InvalidDataException("Scegli un caso o una combinazione per ogni termine.");
                if(!(double.TryParse(t.Factor,NumberStyles.Float,CultureInfo.CurrentCulture,out double factor)||double.TryParse(t.Factor,NumberStyles.Float,CultureInfo.InvariantCulture,out factor))||!double.IsFinite(factor))
                    throw new InvalidDataException("Coefficiente non valido. Usa un numero finito, anche negativo.");
                return new ModelCombinationTerm(t.Source.Id,t.Source.IsCombination,factor,t.Source.IsCombination?"CB":t.Source.Analysis);
            }).ToArray();
            var definition=new ModelCombinationDefinition(editingCombinationId!,CombinationName.Trim(),(ModelCombinationKind)SelectedCombinationKind,editingCombinationSource,editingCombinationStatus,terms,true);
            PublishCombinationSnapshot(ModelCombinations.Save(Snapshot!,definition));RefreshCombinations(definition.Id);
            CombinationMessage="Combinazione salvata nel modello. I risultati derivati precedenti sono stati invalidati. Salva il progetto per conservarla.";
        }
        catch(Exception ex){CombinationMessage="Salvataggio non completato: "+ex.Message;}
    }
    bool CanDiscardCombination()=>IsCombinationDirty&&!IsCombinationBusy;
    [RelayCommand(CanExecute=nameof(CanDiscardCombination))] void DiscardCombinationEdits()
    {RefreshCombinations(SelectedCombination?.Definition.Id);CombinationMessage="Modifiche non salvate annullate.";}
    bool CanDeleteCombination()=>CanStartCombination()&&SelectedCombination!=null;
    [RelayCommand(CanExecute=nameof(CanDeleteCombination))] void DeleteCombination()
    {
        try {PublishCombinationSnapshot(ModelCombinations.Remove(Snapshot!,SelectedCombination!.Definition.Id));RefreshCombinations();CombinationMessage="Combinazione rimossa dalla copia ANTHEA. Le definizioni importate originali restano conservate nella sorgente.";}
        catch(Exception ex){CombinationMessage="Eliminazione non completata: "+ex.Message;}
    }
    bool CanRestoreImported()=>CanStartCombination()&&Snapshot?.ExcludedCombinationIds is {Length:>0};
    [RelayCommand(CanExecute=nameof(CanRestoreImported))] void RestoreImportedCombinations()
    {PublishCombinationSnapshot(ModelCombinations.RestoreImported(Snapshot!));RefreshCombinations();CombinationMessage="Combinazioni importate eliminate ripristinate.";}
    bool CanCalculateCombination()=>CanStartCombination()&&SelectedCombination?.CanCalculate==true;
    [RelayCommand(CanExecute=nameof(CanCalculateCombination),IncludeCancelCommand=true)]
    async Task CalculateCombinationAsync(CancellationToken cancellationToken)
    {
        var snapshot=Snapshot!;string id=SelectedCombination!.Definition.Id;
        var cached=snapshot.Results.FirstOrDefault(r=>r.Combination?.DefinitionId==id);
        if(SelectedCombination.State=="Calcolata"&&cached!=null)
        {ShowCombinationResult(cached.Name);CombinationMessage="Risultati già calcolati: visualizzazione dalla copia salvata, senza nuovo calcolo.";return;}
        using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token,cancellationToken);
        IsCombinationBusy=true;IsBusy=true;NotifyCombinationCommands();
        int operation=++combinationOperation;
        CombinationMessage="Preparazione del calcolo…";
        var progress=new Progress<ModelCombinationProgress>(p=>{if(!disposed&&IsCombinationBusy&&operation==combinationOperation)CombinationMessage=p.Message;});
        try
        {
            var calculated=await combinationService.CalculateAsync(snapshot,id,progress,cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();if(disposed)return;
            var next=await Task.Run(()=>ModelCombinations.Attach(snapshot,id,calculated),cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();if(disposed)return;
            int evicted=snapshot.Results.Count(r=>r.Combination!=null&&r.Combination.DefinitionId!=id&&!next.Results.Any(n=>n.Name==r.Name));
            PublishCombinationSnapshot(next);RefreshCombinations(id);
            ShowCombinationResult(calculated.Results[0].Name);
            CombinationMessage=$"{(calculated.PreparationReused?"Casi base riutilizzati":"Preparazione iniziale: "+(calculated.PreparationMilliseconds/1000).ToString("F2")+" s")} · calcolo {calculated.RebuildMilliseconds/1000:F2} s · campi {calculated.DisplayMilliseconds/1000:F2} s.\n"+string.Join("\n",calculated.Warnings.Take(6));
            if(evicted>0)CombinationMessage+=$"\n{evicted} campi derivati precedenti rimossi dalla cache per rispettare il limite del pacchetto. Le definizioni restano disponibili per il ricalcolo.";
        }
        catch(OperationCanceledException){if(!disposed)CombinationMessage="Calcolo annullato. Il modello precedente è stato conservato.";}
        catch(Exception ex){if(!disposed)CombinationMessage="Calcolo non completato: "+ex.Message;}
        finally {IsCombinationBusy=false;IsBusy=false;NotifyCombinationCommands();}
    }
    void ShowCombinationResult(string name)
    {SelectedCase=name;SelectedPanel=1;SelectedAxes=0;ShowResults=true;Status="Risultato della combinazione disponibile nella vista e nelle tabelle.";}
    void PublishCombinationSnapshot(ModelSnapshot snapshot)
    {
        snapshot.Validate();imported(snapshot);Snapshot=snapshot;fields.Clear();tableState=null;
        bool previous=updating;updating=true;
        Cases=snapshot.Results.Select(r=>r.Name).ToArray();if(!Cases.Contains(SelectedCase))SelectedCase=Cases.FirstOrDefault();
        allTree=ModelTreeBuilder.Build(snapshot);OnSearchTextChanged(SearchText);
        updating=previous;OnPropertyChanged(nameof(Snapshot));RefreshReportChoices();RefreshScene();
    }
}
