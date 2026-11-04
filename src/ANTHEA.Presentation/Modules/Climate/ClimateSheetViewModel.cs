using System.ComponentModel;
using System.Text.Json.Nodes;
using Anthea.Application.Actions;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Mvvm;
using Anthea.Reports;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.Climate;

public sealed record ClimateFormPanel(string Name,FormViewModel Form);
public sealed record ClimateQuantityRow(string Symbol,string Description,double Value,string Unit,string Formula,string Reference)
{public string DisplayValue=>Value.ToString("G9",System.Globalization.CultureInfo.CurrentCulture);}
public sealed record ClimateCaseItem(string Name,string Description,IReadOnlyList<ClimateQuantityRow> Values);

public sealed partial class ClimateSheetViewModel : SheetViewModelBase
{
    private readonly string module;
    private bool rebuilding;
    private int selectedCase;
    private JsonObject? casesSource;
    public ClimateSheetViewModel(string module,SheetContext context):base(module,context,TimeSpan.FromMilliseconds(500))
    {
        this.module=module; Report=new ReportSource(this);
        ExportCommand=GuardedCommands.Create(Guard,Export,()=>HasResults);
        CopyCommand=GuardedCommands.Create(Guard,()=>Context.Services.Clipboard.SetText(ClimateDocument.Csv(Result!)),()=>HasResults);
        InitializePrestress();BuildForms();
        // As the vertical piles: the sheet calculates at opening (also a revision, a reopened file or a copy restored by «Annulla»); the
        // shell has no «Calcola» of its own and the delay after an edit is the only other trigger.
        _=ScheduleCalculationAsync();
    }
    public override IModuleReportSource Report{get;}
    public IRelayCommand ExportCommand{get;}
    public IRelayCommand CopyCommand{get;}
    public IReadOnlyList<ClimateFormPanel> Forms{get;private set;}=[];
    public IReadOnlyList<ActionTableViewModel> Tables{get;private set;}=[];
    public bool IsFire=>module==ClimateDocument.Fire;
    public FireDrawing? FireDrawing=>IsFire?ClimateDocument.FireDiagram(Result,SelectedCase):null;
    public IReadOnlyList<ActionPlotDrawing> FirePlots=>IsFire?ClimateDocument.FirePlots(Result):[];
    public bool IsCranes=>module==ClimateDocument.Cranes;
    public CraneDrawing? CraneDrawing=>IsCranes?ClimateDocument.CraneDiagram(Result,SelectedCase):null;
    public bool IsTimeEffects=>module==ClimateDocument.TimeEffects;
    public IReadOnlyList<ActionPlotDrawing> TimePlots=>IsTimeEffects?ClimateDocument.TimePlots(Result,SelectedCase):[];
    public bool IsConstruction=>module==ClimateDocument.Construction;
    public ConstructionDrawing? ConstructionDrawing=>IsConstruction?ClimateDocument.ConstructionDiagram(Result,SelectedCase):null;
    public bool IsHydraulics=>module==ClimateDocument.Hydraulics;
    public HydraulicDrawing? HydraulicDrawing=>IsHydraulics?ClimateDocument.HydraulicDiagram(Result,SelectedCase):null;
    public bool IsDistribution=>module==ClimateDocument.Distribution;
    public TrafficDrawing? TrafficDrawing=>IsDistribution?ClimateDocument.Drawing(Data,Result):null;
    public IReadOnlyList<string> Schematics=>ClimateFigures.Names(Data).Select(n=>"pack://application:,,,/ANTHEA;component/Assets/Wiki/clima-"+n+".png").ToArray();
    // The quantities with a unit (loads, pressures, temperatures); coefficients such as μ and cpe only when the case has nothing else (review of
    // 2/11/2026, finding 13: the en dash of Snow and Wind on buildings opened the diagram on the coefficients instead of the loads).
    public IReadOnlyList<string> ChartUnits
    {
        get
        {
            var units=CaseValues.Select(v=>v.Unit).Where(u=>u.Length>0).Distinct().ToArray();
            var dimensional=units.Where(u=>!Dimensionless(u)).ToArray();
            return dimensional.Length>0?dimensional:units;
        }
    }
    private static bool Dimensionless(string unit)=>unit.Trim() is "" or "–" or "−" or "-" or "—";
    private string? chartUnit;
    public string? ChartUnit{get=>ChartUnits.Contains(chartUnit??"")?chartUnit:ChartUnits.FirstOrDefault();set{if(SetProperty(ref chartUnit,value))OnPropertyChanged(nameof(ChartValues));}}
    public IReadOnlyList<ClimateQuantityRow> ChartValues=>CaseValues.Where(v=>v.Unit==ChartUnit).ToArray();
    public string Title=>Anthea.Application.Modules.ModuleDirectory.Get(module).Name;
    public string Reference=>Result?["Standard"]?.ToString()??CalculationState switch
    {
        CalculationState.Pending or CalculationState.Running=>"Calcolo in corso…",
        CalculationState.Incomplete or CalculationState.Failed=>"Calcolo non eseguito: completare o correggere i dati indicati qui sotto.",
        _=>"Il calcolo si aggiorna da solo all’apertura e dopo ogni modifica dei dati."
    };
    /// <summary>Why there are no results (the message of the engine: data to complete, a confirmation, a value out of range) and the warnings of the result; empty otherwise.</summary>
    public string CalculationNotice=>string.Join("\n",new[]{HasResults?"":CalculationMessage}.Concat(Warnings).Where(s=>!string.IsNullOrWhiteSpace(s)).Distinct());
    public bool HasCalculationNotice=>CalculationNotice.Length>0;
    public string Scope=>Result?["Scope"]?.ToString()??Anthea.Application.Modules.ModuleDirectory.Get(module).Description;
    public string Notes=>Result is null?"Unità dei singoli campi indicate accanto ai valori. I parametri delle appendici nazionali EN devono essere documentati. Le condizioni di applicabilità sono verificate dal motore al calcolo.":string.Join("\n\n",Result["Notes"]!.AsArray().Select(n=>n!.ToString()));
    public IReadOnlyList<ClimateQuantityRow> Parameters=>Rows(Result?["Parameters"]);
    public IReadOnlyList<ClimateCaseItem> Cases{get;private set;}=[];
    public int SelectedCase{get=>selectedCase;set{if(value>=0&&value<Cases.Count&&SetProperty(ref selectedCase,value)){OnPropertyChanged(nameof(SelectedCaseItem));OnPropertyChanged(nameof(CaseValues));OnPropertyChanged(nameof(CaseDescription));OnPropertyChanged(nameof(HydraulicDrawing));OnPropertyChanged(nameof(ConstructionDrawing));OnPropertyChanged(nameof(TimePlots));OnPropertyChanged(nameof(CraneDrawing));OnPropertyChanged(nameof(FireDrawing));RefreshPrestress();RefreshChart();SynchronizeCaseInput();}}}
    public ClimateCaseItem? SelectedCaseItem{get=>Cases.ElementAtOrDefault(SelectedCase);set{if(value is not null){int index=Cases.ToList().IndexOf(value);if(index>=0)SelectedCase=index;}}}
    public IReadOnlyList<ClimateQuantityRow> CaseValues=>Cases.ElementAtOrDefault(SelectedCase)?.Values??[];
    public string CaseDescription=>Cases.ElementAtOrDefault(SelectedCase)?.Description??(IsBusy?"Calcolo in corso…":"Nessun caso di carico finché il calcolo non è completato.");
    // State of the view of this sheet, in memory and never in the document (rule T6): the tab of the results, the assistance and the diagram
    // open or closed. The shell shows every Actions sheet with the same view (one ViewModel type), so the view keeps none of it.
    /// <summary>Header of the tab of the module (pressures, phases, faces…); null when the module has only the common tabs.</summary>
    public string? ModuleTabTitle=>module switch
    {
        ClimateDocument.Fire=>"Incendio e facce",ClimateDocument.Cranes=>"Vie di corsa",ClimateDocument.Prestressing=>"Profilo e perdite",ClimateDocument.TimeEffects=>"Evoluzione nel tempo",
        ClimateDocument.Distribution=>"Impalcato e influenza",ClimateDocument.Construction=>"Fasi e pianta",ClimateDocument.Hydraulics=>"Schema e pressioni",_=>null
    };
    public bool HasModuleTab=>ModuleTabTitle is not null;
    private int resultTab=-1;
    private bool assistanceExpanded=true,chartExpanded=true;
    /// <summary>Tab of the results: 0 the tab of the module (the first one shown, when there is one), 1 «Casi di carico», 2 «Parametri e formule», 3 «Schemi», 4 «Ipotesi e applicabilità».</summary>
    public int ResultTabIndex{get=>resultTab>=0?resultTab:HasModuleTab?0:1;set{if(value is >= 0 and <= 4&&(value>0||HasModuleTab))SetProperty(ref resultTab,value);}}
    public bool IsAssistanceExpanded{get=>assistanceExpanded;set=>SetProperty(ref assistanceExpanded,value);}
    public bool IsChartExpanded{get=>chartExpanded;set=>SetProperty(ref chartExpanded,value);}
    private static IReadOnlyList<ClimateQuantityRow> Rows(JsonNode? array)=>array?.AsArray().Select(n=>new ClimateQuantityRow(n!["Symbol"]!.ToString(),n["Description"]!.ToString(),n["Value"]!.GetValue<double>(),n["Unit"]!.ToString(),n["Formula"]!.ToString(),n["Reference"]!.ToString())).ToArray()??[];
    /// <param name="focusKey">Field whose editor takes the focus again in the rebuilt forms (the choice that rebuilt them).</param>
    private void BuildForms(string? focusKey=null)
    {
        if(module is null)return;
        var selection=Tables.ToDictionary(t=>t.Key,t=>t.Selected?.Data);
        rebuilding=true;DetachAssistance();
        Forms=ClimateDocument.Forms(module,Data).Select(g=>
        {
            var fields=g.Fields.Select(f=>ActionFieldPresentation.Spec(f,module)).ToArray();
            var form=new FormViewModel(ActionFieldPresentation.Localize(g.Values,g.Fields),fields,key=>Edited(g,key),compact:true,validator:new ActionFieldPresentation.NumberValidator(g.Fields))
                {IsReadOnly=!CanEdit,WikiNavigator=Context.Services.Wiki};
            if(focusKey is not null&&g.Fields.Any(f=>f.Key==focusKey))form.PendingFocusKey=focusKey;
            return new ClimateFormPanel(g.Name,form);
        }).ToArray();
        Tables=ClimateDocument.Tables(Data).Select(t=>new ActionTableViewModel(module,t,TableEdited,()=>CanEdit,Guard,Context.Services.Wiki)).ToArray();
        foreach(var table in Tables)
            if(selection.TryGetValue(table.Key,out var selected)&&table.Rows.FirstOrDefault(r=>ReferenceEquals(r.Data,selected)) is { } row)table.Selected=row;
        rebuilding=false;ObserveAssistance();OnPropertyChanged(nameof(Forms));OnPropertyChanged(nameof(Tables));OnPropertyChanged(nameof(Schematics));OnPropertyChanged(nameof(TrafficDrawing));
    }
    private void TableEdited(){if(rebuilding||IsDisposed)return;Changed();RefreshAssistance();OnPropertyChanged(nameof(TrafficDrawing));}
    private void Edited(ClimateFormGroup group,string key)
    {
        if(rebuilding||IsDisposed)return;
        ClimateDocument.ApplyField(Data,group,key);Changed();RefreshAssistance();OnPropertyChanged(nameof(TrafficDrawing));
        var f=group.Fields.First(f=>f.Key==key);if(f.Boolean||f.Choices is not null)BuildForms(key);
    }
    protected override void OnCommit(){foreach(var panel in Forms)panel.Form.Commit();foreach(var table in Tables)table.Commit();ClimateDocument.Validate(module,Data);}
    protected override void OnReloaded()=>BuildForms();
    protected override void OnCalculationPublished()=>RefreshResults();
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if(e.PropertyName==nameof(CanEdit)){foreach(var panel in Forms)panel.Form.IsReadOnly=!CanEdit;foreach(var table in Tables)table.RefreshAccess();}
        if(e.PropertyName==nameof(Result))RefreshResults();
        if(e.PropertyName is nameof(CalculationMessage) or nameof(Warnings) or nameof(HasResults) or nameof(CalculationState))
            foreach(string p in new[]{nameof(CalculationNotice),nameof(HasCalculationNotice),nameof(Reference),nameof(CaseDescription)})OnPropertyChanged(p);
    }
    private void RefreshResults()
    {
        if(!ReferenceEquals(casesSource,Result))
        {
            casesSource=Result;
            Cases=Result?["Cases"]?.AsArray().Select(c=>new ClimateCaseItem(c!["Name"]!.ToString(),c["Description"]!.ToString(),Rows(c["Values"]))).ToArray()??[];
        }
        if(Cases.Count>0&&ActionAssistanceDocument.CaseTable(module) is not null)selectedCase=Math.Clamp(ActionAssistanceDocument.CaseForRow(module,Result,linkedRow,selectedCase),0,Cases.Count-1);
        if(selectedCase<0||selectedCase>=Cases.Count)selectedCase=0;
        foreach(string p in new[]{nameof(Reference),nameof(Scope),nameof(Notes),nameof(Parameters),nameof(Cases),nameof(SelectedCase),nameof(SelectedCaseItem),nameof(CaseValues),nameof(CaseDescription),nameof(TrafficDrawing),nameof(HydraulicDrawing),nameof(ConstructionDrawing),nameof(TimePlots),nameof(CraneDrawing),nameof(FireDrawing),nameof(FirePlots)})OnPropertyChanged(p);
        RefreshPrestress();ExportCommand?.NotifyCanExecuteChanged();CopyCommand?.NotifyCanExecuteChanged();
        RefreshChart();RefreshAssistance();
        if(Cases.Count>0)SynchronizeCaseInput();
    }
    private void RefreshChart(){if(IsDistribution&&ChartUnits.Contains("%")&&chartUnit is null)chartUnit="%";OnPropertyChanged(nameof(ChartUnits));OnPropertyChanged(nameof(ChartUnit));OnPropertyChanged(nameof(ChartValues));}
    private void Export(){if(Result is null)return;var path=Context.Services.Files.SaveFile(new([new("Azioni CSV","*.csv")],Title+".csv"));if(path is not null)File.WriteAllText(path,ClimateDocument.Csv(Result),new System.Text.UTF8Encoding(true));}
    private sealed class ReportSource(ClimateSheetViewModel sheet):IModuleReportSource
    {public byte[] BuildReport(string title,IReadOnlySet<string> options,bool projectReport=false){sheet.Commit();var result=sheet.CalculateModule(sheet.Data);return DocxWriter.Write(ClimateReport.Create(title,result.Data,result.Values,ClimateDocument.ReportInputs(sheet.module,result.Data)));}}
}
