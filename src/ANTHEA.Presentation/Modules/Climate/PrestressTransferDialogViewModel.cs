using System.Text.Json.Nodes;
using Anthea.Application.Actions;
using Anthea.Presentation.Services;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.Climate;

public sealed class PrestressTransferDialogViewModel:DialogViewModel<JsonObject>
{
    private readonly JsonObject section,result;
    private readonly int stage,station;
    private PrestressTarget? selected;
    private bool confirmed;
    private string error="";
    public PrestressTransferDialogViewModel(JsonObject section,JsonObject result,int stage,PrestressStation station):base("Stato efficace in una copia della sezione")
    {
        this.section=section;this.result=result;this.stage=stage;this.station=station.Index;
        Targets=ClimateDocument.PrestressTargets(section);selected=Targets.FirstOrDefault();
        State=station.Description+" · età = "+result["Prestress"]!["Stages"]![stage]!["AgeDays"]+" giorni";
        ConfirmCommand=new RelayCommand(Apply,()=>Confirmed&&Selected is not null);CancelCommand=new RelayCommand(Cancel);
    }
    public IReadOnlyList<PrestressTarget> Targets{get;}
    public string State{get;}
    public PrestressTarget? Selected{get=>selected;set{if(SetProperty(ref selected,value))ConfirmCommand.NotifyCanExecuteChanged();}}
    public bool Confirmed{get=>confirmed;set{if(SetProperty(ref confirmed,value))ConfirmCommand.NotifyCanExecuteChanged();}}
    public string Error{get=>error;private set=>SetProperty(ref error,value);}
    public IRelayCommand ConfirmCommand{get;}
    public IRelayCommand CancelCommand{get;}
    private void Apply()
    {
        if(!Confirmed||Selected is null)return;
        try{Close(ClimateDocument.ApplyPrestressToSection(section,result,stage,station,Selected.Id));}
        catch(ArgumentException e){Error=e.Message;}
    }
}
