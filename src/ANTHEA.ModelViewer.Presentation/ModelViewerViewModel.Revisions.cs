using ANTHEA.ModelWorkspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using System.Text;

namespace ANTHEA.ModelViewer.Presentation;

public sealed record RevisionDeltaReading(int Element,int Node,double? Station,double Previous,double Current,double Delta,bool GeometryChanged);

public sealed partial class ModelViewerViewModel
{
    public ModelRevisionReview? RevisionReview {get;private set;}
    public event EventHandler<int>? FocusRemovedRevisionRequested;
    public ModelRevisionOverlay? ActiveRevisionOverlay=>ShowRevisionHighlights&&revisionOverlay is {} overlay?overlay with {ShowRemoved=ShowRemovedRevisionElements}:null;
    public bool HasRevision=>revisionOverlay!=null;
    public bool RevisionLegendVisible=>ActiveRevisionOverlay!=null;
    public bool CanComparePrevious=>Snapshot!=null && !IsBusy && !IsRevisionBusy && !IsReportRunning && !disposed;
    public bool CanComputeRevisionDelta=>RevisionReview!=null && IsForceResult && !IsBusy && !IsRevisionBusy && !IsReportRunning && !disposed && SelectedCase!=null;
    public IReadOnlyList<string> RevisionFilters {get;}=["Tutte le modifiche","Nuovi","Geometria modificata","Attributi modificati","Cancellati"];
    [ObservableProperty] private bool isRevisionBusy;
    [ObservableProperty] private bool showRevisionDetails;
    [ObservableProperty] private bool showRevisionHighlights;
    [ObservableProperty] private bool showRemovedRevisionElements=true;
    [ObservableProperty] private bool includeChangedGeometryInDelta;
    [ObservableProperty] private int revisionFilter;
    [ObservableProperty] private int revisionTab;
    [ObservableProperty] private IReadOnlyList<ModelRevisionRow> revisionRows=[];
    [ObservableProperty] private ModelRevisionRow? selectedRevisionRow;
    [ObservableProperty] private IReadOnlyList<RevisionDeltaReading> revisionDeltaRows=[];
    [ObservableProperty] private RevisionDeltaReading? selectedRevisionDeltaRow;
    [ObservableProperty] private string revisionSummary="Nessun confronto disponibile.";
    [ObservableProperty] private string revisionMessage="Confronta la copia corrente con una copia precedente. I riferimenti vengono abbinati per ID, senza cercare equivalenze geometriche.";
    [ObservableProperty] private string revisionDeltaSummary="Il delta viene calcolato solo su richiesta, per il campo scelto nei Risultati.";
    public string RevisionDeltaHeading=>$"{SelectedCase} · {Family} · {SelectedComponent} · {ResultUnit} · {(IsDisplacementResult?"Globali":AxesLabels[SelectedAxes])}";
    private ModelRevisionOverlay? revisionOverlay;
    private ModelResultDelta? revisionDelta;
    private string revisionDeltaKey="",revisionDeltaDisplayKey="";
    partial void OnIsRevisionBusyChanged(bool value){ImportCommand.NotifyCanExecuteChanged();ComparePreviousCommand.NotifyCanExecuteChanged();ComputeRevisionDeltaCommand.NotifyCanExecuteChanged();ClearRevisionCommand.NotifyCanExecuteChanged();OnPropertyChanged(nameof(CanComparePrevious));OnPropertyChanged(nameof(CanComputeRevisionDelta));}
    partial void OnShowRevisionDetailsChanged(bool value){if(value){ShowTable=false;ShowMemberDiagram=false;PauseAnimation();}}
    partial void OnShowRevisionHighlightsChanged(bool value)=>RefreshScene();
    partial void OnShowRemovedRevisionElementsChanged(bool value)=>RefreshScene();
    partial void OnIncludeChangedGeometryInDeltaChanged(bool value){revisionDeltaDisplayKey="";UpdateRevisionState();}
    partial void OnRevisionFilterChanged(int value)=>RefreshRevisionRows();
    partial void OnSelectedRevisionRowChanged(ModelRevisionRow? value)
    {
        if(value==null)return;
        SelectElements(value.Elements);
        if(value.Category=="Nodo" && int.TryParse(value.Id,out int node) && Snapshot?.Nodes.Any(n=>n.Id==node)==true)SelectNodes([node]);
        if(value.Change=="Cancellato")
        {
            ShowRevisionHighlights=true;ShowRemovedRevisionElements=true;
            if(value.Category=="Elemento"&&int.TryParse(value.Id,out int id))FocusRemovedRevisionRequested?.Invoke(this,id);
        }
    }
    partial void OnSelectedRevisionDeltaRowChanged(RevisionDeltaReading? value){if(value!=null)SelectElements([value.Element]);}
    private static Task<(ModelRevisionReview Review,ModelRevisionOverlay Overlay)> CreateRevisionAsync(ModelSnapshot previous,ModelSnapshot current,CancellationToken token)
        =>Task.Run(()=>
        {
            token.ThrowIfCancellationRequested();var review=ModelRevisionComparison.Compare(previous,current,cancellationToken:token);
            var overlay=ModelRevisionOverlay.Create(review,previous.Fingerprint(),current.Fingerprint());token.ThrowIfCancellationRequested();return(review,overlay);
        },token);
    [RelayCommand(CanExecute=nameof(CanComparePrevious),IncludeCancelCommand=true)]
    private async Task ComparePreviousAsync(CancellationToken cancellationToken)
    {
        IsRevisionBusy=true;RevisionMessage="Lettura della copia precedente…";
        using var linkedToken=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token,cancellationToken);
        try
        {
            var current=Snapshot!;var previous=await services.OpenPreviousModelAsync(linkedToken.Token);
            if(previous==null){RevisionMessage="Confronto annullato.";return;}
            previous.Validate();RevisionMessage="Confronto di geometria e attributi…";
            var review=await CreateRevisionAsync(previous,current,linkedToken.Token);
            if(disposed || !ReferenceEquals(current,Snapshot))return;
            ApplyRevision(review.Review,review.Overlay);
        }
        catch(OperationCanceledException){if(!disposed)RevisionMessage="Confronto annullato. Il modello e l'ultimo confronto valido sono conservati.";}
        catch(Exception ex){if(!disposed)RevisionMessage="Confronto non completato: "+ex.Message;}
        finally{IsRevisionBusy=false;}
    }
    private void ApplyRevision(ModelRevisionReview review,ModelRevisionOverlay overlay)
    {
        RevisionReview=review;revisionOverlay=overlay;revisionDelta=null;revisionDeltaKey="";revisionDeltaDisplayKey="";
        RevisionFilter=0;RefreshRevisionRows();ShowRevisionHighlights=true;ShowRevisionDetails=true;
        RevisionMessage=$"Confronto con «{review.Previous.Name}» · {review.Milliseconds:N0} ms per geometria e attributi. ID corrispondenti; tolleranza coordinate 0,001 mm. Il delta delle forze resta su richiesta.";
        RefreshScene();
    }
    private void ResetRevision()
    {
        RevisionReview=null;revisionOverlay=null;revisionDelta=null;revisionDeltaKey="";revisionDeltaDisplayKey="";RevisionRows=[];RevisionDeltaRows=[];SelectedRevisionRow=null;
        ShowRevisionDetails=false;ShowRevisionHighlights=false;
    }
    private void ApplyRevisionOverlay(ModelRevisionOverlay? overlay)
    {
        if(overlay==null){ShowRevisionHighlights=false;return;}
        if(overlay.CurrentRevision!=viewRevision){ShowRevisionHighlights=false;RevisionMessage="Le annotazioni appartengono a una revisione diversa: ricalcola il confronto.";return;}
        if(revisionOverlay?.PreviousRevision!=overlay.PreviousRevision){RevisionReview=null;RevisionRows=[];revisionDelta=null;revisionDeltaKey="";}
        revisionOverlay=overlay;ShowRemovedRevisionElements=overlay.ShowRemoved;ShowRevisionHighlights=true;
    }
    [RelayCommand] private void OpenRevisionDetails(){ShowRevisionDetails=true;}
    [RelayCommand] private void CloseRevisionDetails()=>ShowRevisionDetails=false;
    [RelayCommand(CanExecute=nameof(CanComparePrevious))] private void ClearRevision(){ResetRevision();RevisionMessage="Confronto rimosso dalla sessione. La copia del modello resta invariata.";RefreshScene();}
    [RelayCommand] private void IsolateRevisionChanges()
    {
        var ids=RevisionRows.SelectMany(r=>r.Elements).Distinct().ToArray();
        if(ids.Length==0){RevisionMessage="Il filtro non contiene elementi della revisione corrente. I cancellati sono mostrati come sagoma rossa.";return;}
        SelectElements(ids);IsolateCommand.Execute(null);ClearSelectionCommand.Execute(null);ShowRevisionHighlights=true;
    }
    private void RefreshRevisionRows()
    {
        var rows=RevisionReview?.Rows??[];
        var geometry=RevisionReview?.Elements.Where(e=>e.Geometry).Select(e=>e.Id).ToHashSet()??[];
        var attributes=RevisionReview?.Elements.Where(e=>e.Attributes).Select(e=>e.Id).ToHashSet()??[];
        RevisionRows=rows.Where(r=>RevisionFilter switch {1=>r.Change=="Nuovo",2=>r.Category=="Nodo"&&r.Change=="Modificato"||r.Category=="Elemento"&&r.Elements.Any(geometry.Contains),3=>r.Category is not("Nodo" or "Elemento")||r.Elements.Any(attributes.Contains),4=>r.Change=="Cancellato",_=>true}).ToArray();
    }
    private string DeltaKey()=>string.Join("|",SelectedResultType,SelectedCase,Family,Axes,SelectedComponent);
    [RelayCommand(CanExecute=nameof(CanComputeRevisionDelta),IncludeCancelCommand=true)]
    private async Task ComputeRevisionDeltaAsync(CancellationToken cancellationToken)
    {
        var review=RevisionReview!;string key=DeltaKey(),caseName=SelectedCase!,family=Family,axes=Axes,component=SelectedComponent;
        IsRevisionBusy=true;RevisionTab=1;ShowRevisionDetails=true;RevisionDeltaSummary="Confronto dei campioni importati…";
        using var linkedToken=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token,cancellationToken);
        try
        {
            var delta=await Task.Run(()=>ModelRevisionComparison.CompareResults(review,caseName,family,axes,component,linkedToken.Token),linkedToken.Token);
            if(disposed || RevisionReview!=review || DeltaKey()!=key){if(!disposed)RevisionDeltaSummary="Il campo è cambiato durante il confronto. Ricalcola il delta.";return;}
            revisionDelta=delta;revisionDeltaKey=key;revisionDeltaDisplayKey="";UpdateRevisionState();
        }
        catch(OperationCanceledException){if(!disposed)RevisionDeltaSummary="Delta annullato. Nessun risultato del modello modificato.";}
        catch(Exception ex){if(!disposed)RevisionDeltaSummary="Delta non disponibile: "+ex.Message;}
        finally{IsRevisionBusy=false;}
    }
    private void UpdateRevisionState()
    {
        RevisionSummary=revisionOverlay==null?"Nessun confronto disponibile.":$"{revisionOverlay.Markers.Count(m=>m.Change=="Nuovo")} nuovi · {revisionOverlay.Markers.Count(m=>m.Change=="Geometria")} geometria · {revisionOverlay.Markers.Count(m=>m.Change=="Attributi")} attributi · {revisionOverlay.Removed.Length} cancellati";
        if(revisionDelta!=null && DeltaKey()!=revisionDeltaKey){revisionDelta=null;RevisionDeltaRows=[];RevisionDeltaSummary="Campo cambiato: calcola il delta per il nuovo caso e componente.";}
        if(IsDisplacementResult)RevisionDeltaSummary="Il confronto attuale riguarda le sollecitazioni. Per calcolare il delta scegli Sollecitazioni nella scheda Risultati.";
        if(revisionDelta!=null)
        {
            string displayKey=string.Join("|",revisionDeltaKey,ForceUnit,LengthUnit,IncludeChangedGeometryInDelta);
            if(displayKey!=revisionDeltaDisplayKey)
            {
                revisionDeltaDisplayKey=displayKey;
                RevisionDeltaRows=revisionDelta.Rows.Where(r=>IncludeChangedGeometryInDelta||!r.GeometryChanged).Select(r=>new RevisionDeltaReading(r.Element,r.Node,r.Station,r.Previous*currentDisplayFactor,r.Current*currentDisplayFactor,r.Delta*currentDisplayFactor,r.GeometryChanged)).ToArray();
                int moved=revisionDelta.Rows.Count(r=>r.GeometryChanged);
                RevisionDeltaSummary=$"{RevisionDeltaRows.Count:N0} campioni confrontati · Δ = corrente − precedente · {revisionDelta.Milliseconds:N0} ms. Non abbinati: {revisionDelta.UnmatchedPrevious:N0} precedenti, {revisionDelta.UnmatchedCurrent:N0} correnti. {moved:N0} campioni su geometrie modificate {(IncludeChangedGeometryInDelta?"inclusi":"esclusi")}.";
                if(RevisionReview?.Current.Results.FirstOrDefault(r=>r.Name==SelectedCase)?.IsEnvelope==true)RevisionDeltaSummary+=" Differenza fra estremi, non inviluppo della differenza.";
            }
        }
        OnPropertyChanged(nameof(ActiveRevisionOverlay));OnPropertyChanged(nameof(HasRevision));OnPropertyChanged(nameof(RevisionLegendVisible));OnPropertyChanged(nameof(CanComputeRevisionDelta));OnPropertyChanged(nameof(RevisionDeltaHeading));
        ComparePreviousCommand.NotifyCanExecuteChanged();ComputeRevisionDeltaCommand.NotifyCanExecuteChanged();
    }
    [RelayCommand] private void ExportRevisionDelta()
    {
        if(RevisionDeltaRows.Count==0){RevisionDeltaSummary="Calcola un delta con campioni confrontabili prima di esportare.";return;}
        var csv=new StringBuilder("Elemento;Nodo;Stazione;Precedente;Corrente;Delta;Geometria modificata;Unita\r\n");
        foreach(var row in RevisionDeltaRows)csv.AppendLine(string.Join(";",row.Element,row.Node,row.Station?.ToString("R",CultureInfo.InvariantCulture)??"",row.Previous.ToString("R",CultureInfo.InvariantCulture),row.Current.ToString("R",CultureInfo.InvariantCulture),row.Delta.ToString("R",CultureInfo.InvariantCulture),row.GeometryChanged?"Si":"No",ResultUnit));
        try{services.ExportTable("delta-revisione",csv.ToString());}catch(Exception ex){RevisionDeltaSummary="Esportazione non completata: "+ex.Message;}
    }
}
