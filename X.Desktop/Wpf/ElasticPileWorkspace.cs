using Anthea.Application.Concrete;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Anthea.Application.Modules;
using Anthea.Application.Scheduling;
using Microsoft.Win32;
using System.Threading;
using X.Core;
using Anthea.Application.Presentation;
using Anthea.Presentation.Modules.HorizontalPile.Elastic;
namespace X.Desktop;
internal sealed partial class ElasticPileWorkspace : UserControl,IDisposable
{
    readonly JsonObject parent;readonly Action? editInputs,migrated;
    readonly ContentControl header=new(),migration=new();
    readonly TextBlock status=Ui.Text("",13),summary=Ui.Text("",13),section=Ui.Text("",13,true),selection=Ui.Text(ElasticPilePresentation.SelectionInitial,12);
    readonly ElasticPileDrawing drawing=new(){Height=650,MinWidth=1750};
    readonly PileReinforcementDrawing reinforcementDrawing=new(){HorizontalAlignment=HorizontalAlignment.Left};
    readonly PileReinforcementEditor reinforcementEditor;
    readonly TextBlock reinforcementSummary=Ui.Text("",12);
    readonly SectionDrawing sectionPreview=new(){Width=280,Height=280};
    readonly TextBlock sectionDetail=Ui.Text(ElasticPilePresentation.SectionDetailInitial,12);
    readonly DataGrid table=new(){IsReadOnly=true,AutoGenerateColumns=true,CanUserAddRows=false,Height=230};
    readonly ProgressBar calculationProgress=new(){Height=8,Maximum=100,Visibility=Visibility.Collapsed,Margin=new Thickness(0,5,0,5)};
    JsonNode? selectedPoint;
    void CalculationState(string text,bool running,int done=0,int total=0){status.Text=text;calculationProgress.Visibility=running?Visibility.Visible:Visibility.Collapsed;calculationProgress.IsIndeterminate=running&&total==0;calculationProgress.Value=total>0?100d*done/total:0;reinforcementEditor.SetCalculationState(text,running,done,total);}
    readonly Button csv;readonly RecalculationScheduler recalculation=new();
    Task scheduledCalculation=Task.CompletedTask;
    InputForm? options;bool busy,disposed;int surveyCount=-1;
    readonly IElasticPileAnalysis analysis;
    internal JsonObject? Response{get;private set;}
    internal int ResponseCalculations=>analysis.ResponseCalculations;
    internal JsonObject? Result{get;private set;}
    internal event Action? Modified;
    /// <param name="analysis">Calculation of the elastic response; null (production) uses a new ElasticPileAnalysisSession.</param>
    internal ElasticPileWorkspace(JsonObject root,Action? editInputs=null,Action? migrated=null,IElasticPileAnalysis? analysis=null)
    {
        this.analysis=analysis??new ElasticPileAnalysisSession();parent=root;this.editInputs=editInputs;this.migrated=migrated;Background = Appearance.Surface;ElasticPileEditor.PrepareShared(root);SetValue(InputForm.CommitOnFocusLossProperty,false);
        csv=Ui.Button("Esporta CSV",Export,inspection:true);csv.IsEnabled=false;
        reinforcementEditor=new(root,InvalidateShared,()=>Result,()=>migrated?.Invoke());
        drawing.Selected+=p=>{selectedPoint=p;if(p==null){selection.Text="";return;}selection.Text=ElasticPilePresentation.Selection(p);};
        Content=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=Ui.Stack(migration,Ui.Paper(Ui.Stack(header,section,status,calculationProgress),12),Ui.Paper(Ui.Stack(new ScrollViewer{Content=drawing,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled},selection),12),Ui.Paper(reinforcementEditor,12),new Expander{Header="Verifiche per tratto e sezioni critiche",IsExpanded=true,Content=reinforcementSummary},new Expander{Header="Tavola armature e distinta ferri",IsExpanded=true,Content=Ui.Stack(Ui.Bar(Ui.Button("Apri tavola armature…",ShowReinforcement,inspection:true)),new ScrollViewer{Content=reinforcementDrawing,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled})},new Expander{Header="Sezione trasversale alla quota selezionata",IsExpanded=true,Content=Ui.Bar(sectionPreview,sectionDetail)},new Expander{Header="Estremi, equilibrio e convergenza",Content=summary},new Expander{Header="Valori per quota · N–V–M concomitanti",Content=Ui.Stack(csv,table)})};
        BuildHeader();InvalidateShared();
        drawing.Selected+=ShowSection;
    }
    void BuildHeader()
    {
        surveyCount=parent.Array("stratigrafie").Count;header.Content=null;migration.Content=null;
        if(ElasticPileEditor.NeedsMigration(parent))
        {
            var text=Ui.Text(ElasticPilePresentation.MigrationText(parent),13);
            void Resolve(bool legacy){try{ElasticPileEditor.ResolveMigration(parent,legacy);BuildHeader();migrated?.Invoke();InvalidateShared();}catch(ArgumentException ex){status.Text=ex.Message;}}
            migration.Content=Ui.Paper(Ui.Stack(Ui.Text(ElasticPileFields.MigrationTitle,17,true),text,Ui.Bar(Ui.Button("Usa dati comuni e conserva legacy",()=>Resolve(false)),Ui.Button("Importa legacy nei dati comuni",()=>Resolve(true))),Ui.Button("Mostra confronto completo",()=>Info("Confronto completo",parent.ToJsonString(J.Options)))),14);return;
        }
        var e=parent["elastico"]!.AsObject();var view=Anthea.Application.Presentation.HorizontalSheetPresentation.PrepareElasticView(parent);drawing.Options=view;
        var choice=new ComboBox{ItemsSource=ElasticPilePresentation.SurveyChoices(surveyCount),SelectedIndex=(int)e.D("stratigrafia",-1),MinWidth=180,Margin=new Thickness(8,0,10,0)};
        choice.SelectionChanged+=(_,_)=>{e["stratigrafia"]=choice.SelectedIndex;InvalidateShared();};
        options=new InputForm(e,ElasticPileFields.Options(),_=>InvalidateShared(),compact:true);
        options.MaxWidth=540;options.HorizontalAlignment=HorizontalAlignment.Left;
        var controls=Ui.Stack(Ui.Bar(Ui.Text(ElasticPileFields.Title,18,true),choice,Ui.Button("Modifica dati e strati",()=>editInputs?.Invoke()),Ui.Button("Info",ShowInfo,inspection:true)),options);
        var toggles=new WrapPanel();foreach(var (key,label) in ElasticPilePresentation.Toggles)
        {var box=new CheckBox{Content=label,Tag=key,IsChecked=ElasticPilePresentation.Shown(view,key),Margin=new Thickness(0,5,14,5)};box.Click+=(_,_)=>{view[key]=box.IsChecked==true;drawing.InvalidateVisual();Modified?.Invoke();};toggles.Children.Add(box);}
        controls.Children.Add(toggles);controls.Children.Add(Ui.Text(ElasticPileFields.ToggleNote,11,color:Ui.Muted));header.Content=controls;
    }
    void Info(string title,string content){var text=new TextBox{IsReadOnly=true,TextWrapping=TextWrapping.Wrap,Padding=new Thickness(18),FontSize=14,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Text=content};Ui.Dialog(this,title,text,900,650).ShowDialog();}
    void ShowReinforcement()
    {
        if(Result==null){Info("Tavola armature",status.Text);return;}
        var sheet=new PileReinforcementDrawing{HorizontalAlignment=HorizontalAlignment.Left};sheet.Set(Result);
        var zoom=new Slider{Minimum=35,Maximum=150,Value=75,Width=180,TickFrequency=25,Margin=new Thickness(8)};
        var label=Ui.Text("75%",12);sheet.LayoutTransform=new System.Windows.Media.ScaleTransform(.75,.75);
        zoom.ValueChanged+=(_,_)=>{double factor=zoom.Value/100;sheet.LayoutTransform=new System.Windows.Media.ScaleTransform(factor,factor);label.Text=$"{zoom.Value:0}%";};
        var export=Ui.Button("Esporta PNG…",()=>{var save=new SaveFileDialog{Filter="Tavola PNG|*.png",FileName="armature-palo.png"};if(save.ShowDialog()==true)System.IO.File.WriteAllBytes(save.FileName,sheet.Png((int)Math.Ceiling(sheet.SheetSize.Width*2),(int)Math.Ceiling(sheet.SheetSize.Height*2)));},inspection:true);
        var bar=Ui.Bar(Ui.Text("Zoom",12),zoom,label,export);
        var view=new ScrollViewer{Content=sheet,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        Ui.Dialog(this,"Armature del palo · elevazione e distinta",Ui.Dock(view,top:bar),1250,900).ShowDialog();
    }
    void ShowInfo()=>Info("Info · risposta elastica del palo",ElasticPilePresentation.Info);
    internal void InvalidateShared()
    {
        if(disposed)return;recalculation.Invalidate();Result=null;reinforcementDrawing.Set(null);reinforcementSummary.Text=ElasticPilePresentation.ReinforcementPending;sectionPreview.Outline=[];sectionPreview.Bars=[];sectionPreview.InvalidateVisual();sectionDetail.Text=ElasticPilePresentation.SectionPending;csv.IsEnabled=false;if(surveyCount!=parent.Array("stratigrafie").Count){if(surveyCount>=0 && !ElasticPileEditor.NeedsMigration(parent))parent["elastico"]!["stratigrafia"]=-1;BuildHeader();}
        bool retain=ElasticPileAnalysisSession.Matches(Response,parent);
        reinforcementEditor.InvalidateResults();
        if(retain)drawing.Set(Response,preserveSelection:true);
        else{Response=null;drawing.Set(null);table.ItemsSource=null;selection.Text=summary.Text="";}
        section.Text=HorizontalPreview.StiffnessDescription(parent);
        CalculationState(retain?ElasticPilePresentation.StatusRetained:ElasticPilePresentation.StatusChanged,true);if(!ElasticPileEditor.NeedsMigration(parent))scheduledCalculation=CalculateScheduledAsync(TimeSpan.FromMilliseconds(300),false);Modified?.Invoke();
    }
    internal void Commit(){options?.Commit();reinforcementEditor.Commit();}
    internal Task CalculateAsync()=>scheduledCalculation=CalculateScheduledAsync(TimeSpan.Zero,true);
    async Task CalculateScheduledAsync(TimeSpan delay,bool commit)
    {
        if(disposed)return;if(commit)Commit();var snapshot=(JsonObject)parent.DeepClone();
        var completed=await recalculation.ScheduleAsync(async token=>
        {
            busy=true;
            if(Response==null)CalculationState(ElasticPilePresentation.StatusResponseRunning,true);
            var updates=new Progress<PileProgress>(p=>{if(!disposed&&!token.IsCancellationRequested&&busy&&Result==null)CalculationState(ElasticPilePresentation.Phase(p),true,p.Completed,p.Total);});
            try
            {
                return await analysis.CalculateAsync(snapshot,response=>
                {
                    if(disposed||token.IsCancellationRequested)return;
                    if(!ElasticPileAnalysisSession.Matches(Response,snapshot)){Response=response;PresentResponse();}
                    CalculationState(ElasticPilePresentation.StatusResponseReady,true);
                },updates,token);
            }
            finally{busy=false;}
        },delay);
        completed.TryPublish(current=>
        {
            if(current.Error is not null){Result=null;drawing.Set(Response,preserveSelection:true);reinforcementDrawing.Set(null,ElasticPilePresentation.SheetUnavailable(current.Error.Message));csv.IsEnabled=false;CalculationState(ElasticPilePresentation.Unavailable(current.Error.Message),false);return;}
            Result=current.Value;Present();
        });
    }
    void Present()
    {
        drawing.Set(Result,preserveSelection:true);csv.IsEnabled=true;status.Text=ElasticPilePresentation.Completed(Result!);
        reinforcementDrawing.Set(Result);if(!reinforcementEditor.IsKeyboardFocusWithin)reinforcementEditor.Refresh();
        CalculationState(status.Text,false);reinforcementEditor.UpdateResults();ShowSection(selectedPoint);
        reinforcementSummary.Text=ElasticPilePresentation.ReinforcementSummary(Result!);
    }
    void PresentResponse()
    {
        if(Response==null)return;drawing.Set(Response);
        summary.Text=ElasticPilePresentation.ResponseSummary(Response);
        table.ItemsSource=ElasticPilePresentation.Points(Response);
    }
    void Export(){Commit();if(Result==null)return;var save=new SaveFileDialog{Filter="CSV|*.csv",FileName="Palo_risposta_elastica.csv"};if(save.ShowDialog(Window.GetWindow(this))==true)Archivio.ScriviAtomico(save.FileName,Encoding.UTF8.GetBytes(ElasticPileEditor.Csv(Result)));}
    void ShowSection(JsonNode? p)
    {
        sectionPreview.Outline=[];sectionPreview.Bars=[];sectionPreview.CircularLinkRadius=0;sectionDetail.Text=ElasticPilePresentation.SectionNone;
        if(ElasticPilePresentation.SectionAt(Result,p) is { View: var shown } selected){sectionPreview.Outline=[..shown.Outline];sectionPreview.Bars=[..shown.Bars];sectionPreview.CircularLinkRadius=shown.LinkRadius;sectionPreview.LinkDiameter=shown.LinkDiameter;sectionPreview.LinkCaption=shown.LinkCaption;sectionPreview.ShowAxes=shown.ShowAxes;sectionPreview.Caption=shown.Caption;sectionDetail.Text=selected.Detail;}
        sectionPreview.InvalidateVisual();
    }
    public void Dispose(){disposed=true;recalculation.Dispose();analysis.Dispose();}
}
