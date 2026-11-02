using System.Text.Json.Nodes;
using Anthea.Application.Actions;
using Anthea.Application.Documents;
using Anthea.Presentation.Mvvm;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.Climate;

public sealed partial class ClimateSheetViewModel
{
    private PrestressStation? selectedStation;
    public bool IsPrestressing=>module==ClimateDocument.Prestressing;
    public IReadOnlyList<ActionPlotDrawing> PrestressPlots=>IsPrestressing?ClimateDocument.PrestressPlots(Result,SelectedCase):[];
    public IReadOnlyList<PrestressStation> PrestressStations{get;private set;}=[];
    public PrestressStation? SelectedStation{get=>selectedStation;set{if(SetProperty(ref selectedStation,value)){OnPropertyChanged(nameof(PrestressState));TransferPrestressCommand?.NotifyCanExecuteChanged();}}}
    public string PrestressState=>SelectedStation?.Description??"Calcolare e selezionare lo stato temporale e l’ascissa.";
    public IAsyncRelayCommand TransferPrestressCommand{get;private set;}=null!;
    private void InitializePrestress()=>TransferPrestressCommand=GuardedCommands.CreateAsync(Guard,TransferPrestressAsync,Lifetime,()=>IsPrestressing&&HasResults&&SelectedStation is not null&&ClimateDocument.PrestressCanTransfer(Result));
    private void RefreshPrestress()
    {
        int index=selectedStation?.Index??0;PrestressStations=IsPrestressing?ClimateDocument.PrestressStations(Result,SelectedCase):[];
        OnPropertyChanged(nameof(PrestressStations));SelectedStation=PrestressStations.ElementAtOrDefault(index)??PrestressStations.FirstOrDefault();
        OnPropertyChanged(nameof(PrestressPlots));TransferPrestressCommand?.NotifyCanExecuteChanged();
    }
    public PrestressTransferDialogViewModel CreatePrestressTransferDialog(JsonObject section)=>new(section,Result??throw new InvalidOperationException("Calcolare prima del trasferimento."),SelectedCase,SelectedStation??throw new InvalidOperationException("Selezionare l’ascissa."));
    private async Task TransferPrestressAsync(CancellationToken cancellation)
    {
        if(Result is null||SelectedStation is null)return;
        string? path=Context.Services.Files.OpenFile(new([new("Sezione Concrete ANTHEA","*.anthea")],Title:"Sezione con il cavo da aggiornare"));if(path is null)return;
        var document=DocumentService.Open(path);
        if(document["tipo"]?.ToString()!="calcolo"||document["modulo_id"]?.ToString()!="str_palo")throw new ArgumentException("Selezionare un file del singolo modulo Concrete, con il cavo già definito.");
        string name="Sezione · "+SelectedCaseItem?.Name+" · "+SelectedStation.Label;
        var dialog=CreatePrestressTransferDialog(document["dati"]!.AsObject());
        var copy=await Context.Services.Dialogs.ShowDialogAsync(dialog);
        if(copy is not null&&!IsDisposed&&!cancellation.IsCancellationRequested)Context.Host.OpenModuleCopy("str_palo",copy,name);
    }
}
