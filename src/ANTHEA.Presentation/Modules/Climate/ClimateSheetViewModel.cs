using System.ComponentModel;
using System.Text.Json.Nodes;
using Anthea.Application.Actions;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Mvvm;
using Anthea.Reports;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.Climate;

public sealed record ClimateFormPanel(string Name,FormViewModel Form);
public sealed record ClimateQuantityRow(string Symbol,string Description,double Value,string Unit,string Formula,string Reference)
{public string DisplayValue=>Value.ToString("G9",System.Globalization.CultureInfo.CurrentCulture);}
public sealed record ClimateCaseItem(string Name,string Description,IReadOnlyList<ClimateQuantityRow> Values);

public sealed class ClimateSheetViewModel : SheetViewModelBase
{
    private readonly string module;
    private bool rebuilding;
    private int selectedCase;
    public ClimateSheetViewModel(string module,SheetContext context):base(module,context,TimeSpan.FromMilliseconds(500))
    {
        this.module=module; Report=new ReportSource(this);
        ExportCommand=GuardedCommands.Create(Guard,Export,()=>HasResults);
        CopyCommand=GuardedCommands.Create(Guard,()=>Context.Services.Clipboard.SetText(ClimateDocument.Csv(Result!)),()=>HasResults);
        BuildForms();
    }
    public override IModuleReportSource Report{get;}
    public IRelayCommand ExportCommand{get;}
    public IRelayCommand CopyCommand{get;}
    public IReadOnlyList<ClimateFormPanel> Forms{get;private set;}=[];
    public IReadOnlyList<ActionTableViewModel> Tables{get;private set;}=[];
    public bool IsDistribution=>module==ClimateDocument.Distribution;
    public TrafficDrawing? TrafficDrawing=>IsDistribution?ClimateDocument.Drawing(Data,Result):null;
    public IReadOnlyList<string> Schematics=>ClimateFigures.Names(Data).Select(n=>"pack://application:,,,/ANTHEA;component/Assets/Wiki/clima-"+n+".png").ToArray();
    public IReadOnlyList<string> ChartUnits=>CaseValues.Select(v=>v.Unit).Where(u=>u.Length>0&&u!="−"&&u!="-").Distinct().ToArray();
    private string? chartUnit;
    public string? ChartUnit{get=>ChartUnits.Contains(chartUnit??"")?chartUnit:ChartUnits.FirstOrDefault();set{if(SetProperty(ref chartUnit,value))OnPropertyChanged(nameof(ChartValues));}}
    public IReadOnlyList<ClimateQuantityRow> ChartValues=>CaseValues.Where(v=>v.Unit==ChartUnit).ToArray();
    public string Title=>Anthea.Application.Modules.ModuleDirectory.Get(module).Name;
    public string Reference=>Result?["Standard"]?.ToString()??"Selezionare norma e dati del progetto, quindi Calcola.";
    public string Scope=>Result?["Scope"]?.ToString()??Anthea.Application.Modules.ModuleDirectory.Get(module).Description;
    public string Notes=>Result is null?"Unità dei singoli campi indicate accanto ai valori. I parametri delle appendici nazionali EN devono essere documentati. Le condizioni di applicabilità sono verificate dal motore al calcolo.":string.Join("\n\n",Result["Notes"]!.AsArray().Select(n=>n!.ToString()));
    public IReadOnlyList<ClimateQuantityRow> Parameters=>Rows(Result?["Parameters"]);
    public IReadOnlyList<ClimateCaseItem> Cases=>Result?["Cases"]?.AsArray().Select(c=>new ClimateCaseItem(c!["Name"]!.ToString(),c["Description"]!.ToString(),Rows(c["Values"]))).ToArray()??[];
    public int SelectedCase{get=>selectedCase;set{if(value>=0&&value<Cases.Count&&SetProperty(ref selectedCase,value)){OnPropertyChanged(nameof(CaseValues));OnPropertyChanged(nameof(CaseDescription));RefreshChart();}}}
    public IReadOnlyList<ClimateQuantityRow> CaseValues=>Cases.ElementAtOrDefault(SelectedCase)?.Values??[];
    public string CaseDescription=>Cases.ElementAtOrDefault(SelectedCase)?.Description??"Calcolare per visualizzare i casi di carico.";
    private static IReadOnlyList<ClimateQuantityRow> Rows(JsonNode? array)=>array?.AsArray().Select(n=>new ClimateQuantityRow(n!["Symbol"]!.ToString(),n["Description"]!.ToString(),n["Value"]!.GetValue<double>(),n["Unit"]!.ToString(),n["Formula"]!.ToString(),n["Reference"]!.ToString())).ToArray()??[];
    private void BuildForms()
    {
        if(module is null)return;
        rebuilding=true;
        Forms=ClimateDocument.Forms(module,Data).Select(g=>
        {
            var fields=g.Fields.Select(Field).ToArray();
            var form=new FormViewModel(g.Values,fields,key=>Edited(g,key),compact:true){IsReadOnly=!CanEdit,WikiNavigator=Context.Services.Wiki};
            return new ClimateFormPanel(g.Name,form);
        }).ToArray();
        Tables=ClimateDocument.Tables(Data).Select(t=>new ActionTableViewModel(module,t,TableEdited,()=>CanEdit,Guard,Context.Services.Wiki)).ToArray();
        rebuilding=false;OnPropertyChanged(nameof(Forms));OnPropertyChanged(nameof(Tables));OnPropertyChanged(nameof(Schematics));OnPropertyChanged(nameof(TrafficDrawing));
    }
    internal static FieldSpec Field(ClimateField f)=>new(f.Key,f.Label,f.Unit,Choices:f.Choices,Bool:f.Boolean,Wide:f.Choices is not null||!f.Numeric&&!f.Boolean,Write:f.Numeric?FieldWriteRule.NumberOrText:FieldWriteRule.RawText,Multiline:!f.Numeric&&!f.Boolean&&f.Choices is null,ToolTip:f.Help);
    private void TableEdited(){if(rebuilding||IsDisposed)return;Changed();OnPropertyChanged(nameof(TrafficDrawing));}
    private void Edited(ClimateFormGroup group,string key)
    {
        if(rebuilding||IsDisposed)return;
        ClimateDocument.ApplyField(Data,group,key);Changed();OnPropertyChanged(nameof(TrafficDrawing));
        var f=group.Fields.First(f=>f.Key==key);if(f.Boolean||f.Choices is not null)BuildForms();
    }
    protected override void OnCommit(){foreach(var panel in Forms)panel.Form.Commit();foreach(var table in Tables)table.Commit();ClimateDocument.Validate(module,Data);}
    protected override void OnReloaded()=>BuildForms();
    protected override void OnCalculationPublished()=>RefreshResults();
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if(e.PropertyName==nameof(CanEdit)){foreach(var panel in Forms)panel.Form.IsReadOnly=!CanEdit;foreach(var table in Tables)table.RefreshAccess();}
        if(e.PropertyName==nameof(Result))RefreshResults();
    }
    private void RefreshResults()
    {
        if(selectedCase<0||selectedCase>=Cases.Count)selectedCase=0;
        foreach(string p in new[]{nameof(Reference),nameof(Scope),nameof(Notes),nameof(Parameters),nameof(Cases),nameof(SelectedCase),nameof(CaseValues),nameof(CaseDescription),nameof(TrafficDrawing)})OnPropertyChanged(p);
        ExportCommand?.NotifyCanExecuteChanged();CopyCommand?.NotifyCanExecuteChanged();
        RefreshChart();
    }
    private void RefreshChart(){if(IsDistribution&&ChartUnits.Contains("%")&&chartUnit is null)chartUnit="%";OnPropertyChanged(nameof(ChartUnits));OnPropertyChanged(nameof(ChartUnit));OnPropertyChanged(nameof(ChartValues));}
    private void Export(){if(Result is null)return;var path=Context.Services.Files.SaveFile(new([new("Azioni CSV","*.csv")],Title+".csv"));if(path is not null)File.WriteAllText(path,ClimateDocument.Csv(Result),new System.Text.UTF8Encoding(true));}
    private sealed class ReportSource(ClimateSheetViewModel sheet):IModuleReportSource
    {public byte[] BuildReport(string title,IReadOnlySet<string> options,bool projectReport=false){sheet.Commit();var result=sheet.CalculateModule(sheet.Data);return DocxWriter.Write(ClimateReport.Create(title,result.Data,result.Values,ClimateDocument.ReportInputs(sheet.module,result.Data)));}}
}
