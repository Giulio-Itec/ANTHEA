using System.Diagnostics;
using ANTHEA.ModelWorkspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ANTHEA.ModelViewer.Presentation;

/// <summary>Request of the user of 2/11/2026: «Calcola tutte» and the calculation of the combinations selected in the list, one after the other
/// on the base cases prepared once by GPC. The results enter the copy in memory and are published to the document every
/// <see cref="BatchPublishSeconds"/> seconds and at the end, so that an interruption keeps the combinations already calculated.</summary>
public sealed partial class ModelViewerViewModel
{
    const double BatchPublishSeconds=20;
    /// <summary>3/11/2026 (copies of several GiB): the series calculates at least this many times as long as the last publication took before
    /// publishing again, so that the publications of a large copy do not take most of the time of the series.</summary>
    const double BatchPublishRatio=4;
    CancellationTokenSource? batchCancellation;
    /// <summary>Definitions selected in the list of the combinations (several rows), set by the view.</summary>
    [ObservableProperty] IReadOnlyList<string> selectedCombinationIds=[];
    [ObservableProperty] bool isCombinationBatchBusy;
    partial void OnSelectedCombinationIdsChanged(IReadOnlyList<string> value)=>NotifyCombinationBatch();

    /// <summary>Combinations to calculate: calculable and not calculated, or calculated before the principal components of the plates.</summary>
    IEnumerable<ModelCombinationAvailability> ToCalculate(IEnumerable<ModelCombinationAvailability> combinations)=>
        combinations.Where(c=>c.CanCalculate&&(c.State!="Calcolata"||RecalculatesPrincipals(c)));
    public int PendingCombinationCount=>ToCalculate(Combinations).Count();
    int SelectedPendingCount=>ToCalculate(Combinations.Where(c=>SelectedCombinationIds.Contains(c.Definition.Id))).Count();
    public string CalculateAllLabel=>$"Calcola tutte ({PendingCombinationCount})";
    public string CalculateSelectedLabel=>$"Calcola selezionate ({SelectedPendingCount})";
    bool CanCalculateAll()=>CanStartCombination()&&PendingCombinationCount>0;
    bool CanCalculateSelected()=>CanStartCombination()&&SelectedPendingCount>0;

    void NotifyCombinationBatch()
    {
        foreach(string property in new[]{nameof(PendingCombinationCount),nameof(CalculateAllLabel),nameof(CalculateSelectedLabel)})OnPropertyChanged(property);
        CalculateAllCombinationsCommand.NotifyCanExecuteChanged();CalculateSelectedCombinationsCommand.NotifyCanExecuteChanged();
        NotifyCombinationSpaceCommands();
    }

    [RelayCommand(CanExecute=nameof(CanCalculateAll))]
    Task CalculateAllCombinationsAsync()=>CalculateCombinationBatchAsync(ToCalculate(Combinations).Select(c=>c.Definition).ToArray());
    [RelayCommand(CanExecute=nameof(CanCalculateSelected))]
    Task CalculateSelectedCombinationsAsync()=>
        CalculateCombinationBatchAsync(ToCalculate(Combinations.Where(c=>SelectedCombinationIds.Contains(c.Definition.Id))).Select(c=>c.Definition).ToArray());
    /// <summary>Stops the calculation in progress, of one combination or of a series.</summary>
    [RelayCommand] void CancelCombinationCalculation(){CalculateCombinationCancelCommand.Execute(null);batchCancellation?.Cancel();}

    async Task CalculateCombinationBatchAsync(IReadOnlyList<ModelCombinationDefinition> definitions)
    {
        if(definitions.Count==0)return;
        using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);batchCancellation=cancellation;
        IsCombinationBusy=true;IsCombinationBatchBusy=true;IsBusy=true;NotifyCombinationCommands();
        int operation=++combinationOperation;
        var watch=Stopwatch.StartNew();var sincePublished=Stopwatch.StartNew();
        var working=Snapshot!;var calculated=new List<string>();var failed=new List<string>();
        int unpublished=0,evicted=0;string? stopped=null;double publishAfter=BatchPublishSeconds;
        async Task Publish()
        {
            var copy=working;var publishing=Stopwatch.StartNew();
            // Written off the thread of the interface, as a single combination; never cancelled, so the calculated results are kept.
            var entry=await Task.Run(()=>ModelDocumentEntry.Prepare(copy));
            if(disposed)return;
            PublishCombinationSnapshot(entry);unpublished=0;sincePublished.Restart();
            double spaced=BatchPublishRatio*publishing.Elapsed.TotalSeconds;publishAfter=spaced>BatchPublishSeconds?spaced:BatchPublishSeconds;
        }
        try
        {
            for(int i=0;i<definitions.Count;i++)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                var definition=definitions[i];
                string heading=$"Combinazione {i+1} di {definitions.Count} · {definition.Name}";
                CombinationMessage=heading;
                var progress=new Progress<ModelCombinationProgress>(p=>{if(!disposed&&IsCombinationBusy&&operation==combinationOperation)CombinationMessage=heading+"\n"+p.Message;});
                ModelSnapshot next;
                try
                {
                    var result=await combinationService.CalculateAsync(working,definition.Id,progress,cancellation.Token);
                    var before=working;
                    next=await Task.Run(()=>ModelCombinations.Attach(before,definition.Id,result,CombinationCopyBytes),cancellation.Token);
                }
                catch(Exception ex) when(ex is not OperationCanceledException){failed.Add(definition.Name+": "+ex.Message);continue;}
                var kept=next.Results.Select(r=>r.Combination?.DefinitionId).OfType<string>().ToHashSet(StringComparer.Ordinal);
                evicted+=working.Results.Where(r=>r.Combination!=null).Select(r=>r.Combination!.DefinitionId).Distinct().Count(id=>id!=definition.Id&&!kept.Contains(id));
                working=next;unpublished++;calculated.Add(definition.Id);
                // The limit of the package evicts the oldest derived results: when they are of this series, going on would only replace them.
                if(calculated.Any(id=>!kept.Contains(id)))
                {stopped=$"Limite dello spazio per il modello ({ModelCombinations.CopyLimit(CombinationCopyBytes)}) raggiunto: le prime combinazioni di questa serie sono state rimosse dalla cache. "+
                    (CombinationArchive?"Alza il limite in «Spazio delle combinazioni», libera i risultati che non servono o calcola meno combinazioni alla volta.":"Calcola meno combinazioni alla volta.");break;}
                if(sincePublished.Elapsed.TotalSeconds>=publishAfter)await Publish();
            }
        }
        catch(OperationCanceledException){stopped="Calcolo annullato: le combinazioni già calcolate sono conservate.";}
        finally
        {
            try {if(unpublished>0&&!disposed)await Publish();}
            catch(Exception ex){stopped=(stopped==null?"":stopped+"\n")+"Pubblicazione non completata: "+ex.Message;}
            batchCancellation=null;
            IsCombinationBusy=false;IsCombinationBatchBusy=false;IsBusy=false;NotifyCombinationCommands();
        }
        if(disposed)return;
        RefreshCombinations(SelectedCombination?.Definition.Id);
        CombinationMessage=$"{calculated.Count} di {definitions.Count} combinazioni calcolate in {watch.Elapsed.TotalSeconds:F1} s."+
            (failed.Count>0?$"\n{failed.Count} non calcolate:\n"+string.Join("\n",failed.Take(6)):"")+
            (evicted>0?$"\n{evicted} combinazioni calcolate in precedenza rimosse dalla cache per il limite del pacchetto; le definizioni restano.":"")+
            (stopped!=null?"\n"+stopped:"");
    }
}
