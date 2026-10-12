using System.Data;
using System.Globalization;
using ANTHEA.ModelWorkspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ANTHEA.ModelViewer.Presentation;

public sealed partial class ModelViewerViewModel
{
    [ObservableProperty] bool showCombinationGrid;
    [ObservableProperty] bool showCombinationEditor=true;
    [ObservableProperty] bool isCombinationDetached;
    [ObservableProperty] DataView? combinationGridRows;
    [ObservableProperty] DataRowView? selectedCombinationGridRow;
    public IReadOnlyDictionary<string,string> CombinationGridHeaders {get;private set;}=new Dictionary<string,string>();
    public string CombinationWindowLabel=>IsCombinationDetached?"Riaggancia":"Stacca finestra ↗";
    public bool ShowCombinationList=>!ShowCombinationGrid;
    public event EventHandler? CombinationWindowChanged;

    partial void OnShowCombinationGridChanged(bool value)
    {OnPropertyChanged(nameof(ShowCombinationList));if(value){BuildCombinationGrid();ShowCombinationEditor=false;}else ShowCombinationEditor=true;}
    partial void OnSelectedCombinationGridRowChanged(DataRowView? value)
    {
        if(value?.Row["DefinitionId"] is string id&&CanChooseCombination)
            SelectedCombination=Combinations.FirstOrDefault(c=>c.Definition.Id==id);
    }
    partial void OnIsCombinationDetachedChanged(bool value)
    {OnPropertyChanged(nameof(CombinationWindowLabel));CombinationWindowChanged?.Invoke(this,EventArgs.Empty);}
    [RelayCommand] void ToggleCombinationWindow()
    {ShowCombinationManager=IsCombinationDetached;IsCombinationDetached=!IsCombinationDetached;}

    void BuildCombinationGrid()
    {
        var table=new DataTable("Combinazioni"){Locale=CultureInfo.CurrentCulture};
        table.Columns.Add("DefinitionId",typeof(string));table.Columns.Add("Nome",typeof(string));table.Columns.Add("Operazione",typeof(string));table.Columns.Add("Sorgente",typeof(string));
        var refs=(Snapshot==null?[]:ModelCombinations.StaticCaseNames(Snapshot).Select(c=>(Id:c,Combo:false,Label:c)))
            .Concat(Combinations.Select(c=>(Id:c.Definition.Id,Combo:true,Label:c.Definition.Name+" ["+c.Definition.Source+"]")))
            .Concat(Combinations.SelectMany(c=>c.Definition.Terms).Select(t=>(Id:t.Reference,Combo:t.IsCombination,Label:t.Reference+" ["+t.Analysis+"]")))
            .DistinctBy(r=>(r.Id,r.Combo)).ToArray();
        var headers=new Dictionary<string,string>();
        for(int i=0;i<refs.Length;i++) {string key="Term"+i;table.Columns.Add(key,typeof(string));headers[key]=(refs[i].Combo?"Σ ":"↓ ")+refs[i].Label;}
        table.Columns.Add("Stato",typeof(string));
        foreach(var item in Combinations)
        {
            var d=item.Definition;
            table.Rows.Add(new object[]{d.Id,d.Name,CombinationKindLabels[(int)d.Kind],d.Source}
                .Concat(refs.Select(r=>(object)string.Join("; ",d.Terms.Where(t=>t.Reference==r.Id&&t.IsCombination==r.Combo).Select(t=>t.Factor.ToString("G9",CultureInfo.CurrentCulture)))))
                .Append(item.State).ToArray());
        }
        CombinationGridHeaders=headers;CombinationGridRows=table.DefaultView;SyncCombinationGridSelection();
    }
    void SyncCombinationGridSelection()
    {
        if(CombinationGridRows==null)return;
        var row=CombinationGridRows.Cast<DataRowView>().FirstOrDefault(r=>(string)r["DefinitionId"]==SelectedCombination?.Definition.Id);
        if(!ReferenceEquals(row,SelectedCombinationGridRow))SelectedCombinationGridRow=row;
    }
}
