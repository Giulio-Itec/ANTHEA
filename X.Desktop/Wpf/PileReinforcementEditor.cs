using Anthea.Application.Concrete;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X.Core;
using Anthea.Application.Modules;
using Anthea.Application.Presentation;
using Anthea.Presentation.Modules.HorizontalPile.Elastic;
namespace X.Desktop;
internal sealed partial class PileReinforcementEditor : ContentControl
{
    readonly JsonObject root;readonly Action changed;readonly Action? sharedSectionChanged;readonly Func<JsonObject?> result;InputForm? details;readonly TextBlock message=Ui.Text("",12);
    readonly TextBlock phase=Ui.Text("",12);readonly ProgressBar progress=new(){Height=7,Maximum=100,Visibility=Visibility.Collapsed,Margin=new Thickness(0,4,0,8)};readonly List<Button> resultCommands=new();
    string calculationState=ElasticPilePresentation.ReinforcementInitial;
    internal void SetCalculationState(string text,bool running,int completed=0,int total=0){calculationState=text;phase.Text=text;progress.Visibility=running?Visibility.Visible:Visibility.Collapsed;progress.IsIndeterminate=running&&total==0;progress.Value=total>0?100d*completed/total:0;foreach(var b in resultCommands)b.IsEnabled=result()!=null;if(!running&&result()==null)UpdateResults();}
    Button ResultButton(string title,Action action){var button=Ui.Button(title,action);button.IsEnabled=result()!=null;button.ToolTip=ElasticPilePresentation.ResultCommandToolTip;resultCommands.Add(button);return button;}
    int selected;string checkMode="Riepilogo";readonly ContentControl summary=new();readonly Dictionary<string,TextBlock> bills=new();readonly Dictionary<string,bool> expandedBills=new();
    internal static Brush SegmentColor(int index)=>Ui.Brush(new[]{"#DBEAFE","#DCFCE7","#F3E8FF","#FFEDD5","#CFFAFE","#FCE7F3"}[index%6]);
    internal PileReinforcementEditor(JsonObject root,Action changed,Func<JsonObject?> result,Action? sharedSectionChanged=null){this.root=root;this.changed=changed;this.result=result;this.sharedSectionChanged=sharedSectionChanged;Build();}
    FrameworkElement SeismicPanel(JsonObject settings)
    {
        InputForm? form=null;Button? approval=null;
        var conditional=ElasticPileFields.SeismicConditional;
        void Visibility(){if(approval!=null)approval.IsEnabled=settings.B("sisma_testa");if(form!=null)foreach(var key in conditional)form.ShowField(key,settings.B("sisma_testa"));}
        form=new InputForm(settings,ElasticPileFields.Seismic(),_=>{Visibility();changed();},compact:true);

        approval=ApproveButton(true);Visibility();
        return new Expander{Header=ElasticPileFields.SeismicHeader,IsExpanded=settings.B("sisma_testa"),Content=Ui.Stack(Ui.Text(ElasticPileFields.SeismicIntro,12),form,Ui.Text(ElasticPileFields.SeismicNote,12),Ui.Bar(approval,Ui.Button("NTC §7.2.5 · finestra interna",()=>CreateNtcWindow().ShowDialog())))};
    }
    internal void Commit(){foreach(var form in Ui.Descendants<InputForm>(this))form.Commit();}
    internal void Refresh()=>Build();
    internal void InvalidateResults(){foreach(var bill in bills.Values)bill.Text=ElasticPilePresentation.BillPending;UpdateSummary();}
    // Update output labels even while an input retains focus; rebuilding would interrupt editing.
    internal void UpdateResults(){foreach(var (id,bill) in bills)bill.Text=BillFor(id);UpdateSummary();}
    void Build()
    {
        if(message.Parent is Panel old)old.Children.Remove(message);if(phase.Parent is Panel owner)owner.Children.Remove(phase);if(progress.Parent is Panel pp)pp.Children.Remove(progress);resultCommands.Clear();
        if(ElasticPileEditor.NeedsMigration(root))return;ElasticPileEditor.PrepareReinforcement(root);
        var e=root["elastico"]!.AsObject();var rows=e.Array("tratti");bills.Clear();selected=Math.Clamp(selected,0,Math.Max(0,rows.Count-1));var stack=Ui.Stack(Ui.Text(ElasticPileFields.SegmentsTitle,16,true),Ui.Text(ElasticPileFields.SegmentsNote,12));
        phase.Text=calculationState;stack.Children.Add(phase);stack.Children.Add(progress);
        stack.Children.Add(Ui.Bar(ApproveButton(false),Ui.Button("NTC §7.2.5",()=>CreateNtcWindow().ShowDialog())));
        var transverse=Ui.Choice(ElasticPilePresentation.TransverseKinds,e["dettagli"].S("sisma_staffe"));transverse.Width=160;transverse.SelectionChanged+=(_,_)=>{e["dettagli"]!["sisma_staffe"]=transverse.SelectedItem?.ToString();changed();Build();};stack.Children.Add(Ui.Bar(Ui.Text(ElasticPileFields.MainTransverseLabel,12),transverse));
        stack.Children.Add(Ui.Bar(ResultButton("Proponi suddivisione…",Propose),Ui.Button("Info sezioni",()=>Ui.Dialog(this,"Perimetro delle verifiche",Ui.Text("NTC 2018 §7.2.5 · G.U. 20/02/2018, PDF 217, pagina stampata 213: per i pali As≥0,3% Ac, φst≥8 mm e s≤8φL per tutta la lunghezza. Se non si esclude il raggiungimento della capacità, il testo richiede zone dissipative (10D presso testa / 5D ai contatti profondi), As≥1%, staffe singole s≤6φL e condizioni ulteriori su duttilità e azioni. I controlli di testa sono attivabili nelle ipotesi Sisma e leggibili nel riepilogo. Le azioni sismiche devono essere assegnate e confermate; le zone profonde restano da valutare. Regole Pilastro: default modificabile, aggiuntivo ai minimi pali.\n\nN–M tramite il dominio resistente Checker; taglio tramite lo stesso motore del modulo c.a. Staffe chiuse a 90°, due bracci. La proposta dei tratti conserva inizialmente l'armatura principale; personalizzare i tratti e controllare gli esiti. Ancoraggi rettilinei a fyd e sovrapposizioni sono proposte da completare con disposizione, confinamento e requisiti esecutivi/sismici pertinenti. MRd tratteggiato = capacità nominale, senza certificare i dettagli costruttivi.",14),900,520).ShowDialog())));
        var headers=ElasticPilePresentation.SegmentHeaders(root);
        for(int i=0;i<rows.Count;i++)
        {
            int index=i;var row=rows[i]!.AsObject();
            var card=Ui.Stack(Ui.Text(headers[i],14,true));
            var line=new WrapPanel();
            FrameworkElement Field(string title,FrameworkElement input,double width=115){input.Width=width;var field=Ui.Stack(Ui.Text(title,11,true),input);field.Margin=new Thickness(0,4,10,4);return field;}
            var end=new TextBox{Text=row.S("fine"),ToolTip=ElasticPilePresentation.SegmentEndToolTip};end.TextChanged+=(_,_)=>{row["fine"]=string.IsNullOrWhiteSpace(end.Text)?null:JsonValue.Create(end.Text);changed();};line.Children.Add(Field("Fine tratto / barre [m]",end));
            var inherit=new CheckBox{Content="Usa principale",IsChecked=row.B("collegato"),IsEnabled=i>0,VerticalAlignment=VerticalAlignment.Center};inherit.Click+=(_,_)=>{if(inherit.IsChecked!=true){foreach(var key in ElasticPileEditor.ReinforcementKeys)if(row[key]==null)row[key]=root["sezione"]![key]?.DeepClone();foreach(var key in ElasticPileEditor.InnerRingKeys)if(row[key]==null&&root["sezione"]![key] is JsonNode ring)row[key]=ring.DeepClone();}row["collegato"]=inherit.IsChecked==true;changed();Build();};line.Children.Add(Field("Origine armatura",inherit,140));
            var adoptedSection=ElasticPileEditor.SegmentSection(root,row);
            foreach(var (key,label) in ElasticPileFields.SegmentFields.Take(4)){var box=new TextBox{Text=adoptedSection.S(key),IsReadOnly=row.B("collegato"),Background=row.B("collegato")?Ui.Bg:Brushes.White,ToolTip=ElasticPilePresentation.SegmentFieldToolTip(row,key,label)};box.TextChanged+=(_,_)=>{row[key]=box.Text;changed();};line.Children.Add(Field(label,box,95));}
            // Second ring of bars, as in the c.a. section: active from this card, the main section or the c.a. verifier of the segment.
            bool linked=row.B("collegato"),ring=adoptedSection.B("second_inner_enabled");
            var ringBox=new CheckBox{Content="Secondo anello interno",IsChecked=ring,IsEnabled=!linked,VerticalAlignment=VerticalAlignment.Center,ToolTip=linked?ElasticPilePresentation.SharedValue:ElasticPilePresentation.InnerRingToolTip};
            ringBox.Click+=(_,_)=>{row["second_inner_enabled"]=ringBox.IsChecked==true;if(ringBox.IsChecked==true)foreach(var (key,value) in new[]{("second_inner_count",adoptedSection.S("longitudinal_bar_count")),("second_inner_diameter",adoptedSection.S("longitudinal_bar_diameter_mm")),("second_inner_gap","30")})if(row[key]==null&&string.IsNullOrWhiteSpace(adoptedSection.S(key)))row[key]=value;changed();Build();};
            line.Children.Add(Field("Anello interno",ringBox,170));
            if(ring)foreach(var (key,label) in ElasticPileFields.SegmentFields.Skip(4))
            {var box=new TextBox{Text=adoptedSection.S(key),IsReadOnly=linked,Background=linked?Ui.Bg:Brushes.White,ToolTip=linked?ElasticPilePresentation.SharedValue:label};box.TextChanged+=(_,_)=>{row[key]=box.Text;changed();};line.Children.Add(Field(label,box,95));}
            var kind=Ui.Choice(ElasticPilePresentation.TransverseKinds,ElasticPileEditor.TransverseKind(root,row));kind.IsEnabled=!row.B("collegato");kind.SelectionChanged+=(_,_)=>{row["tipo_trasversale"]=kind.SelectedItem?.ToString();changed();};line.Children.Add(Field("Tipo trasversale",kind,145));
            card.Children.Add(line);var commands=Ui.Bar(Ui.Button("Verifiche →",()=>{selected=index;UpdateSummary();}),Ui.Button("Apri verificatore c.a.",()=>OpenConcrete(index)),ResultButton("Dimensiona…",()=>Design(index)),Ui.Button("Dividi…",()=>Split(index)));
            if(i>0)commands.Children.Add(Ui.Button("Unisci ←",()=>{rows[index-1]!["fine"]=row["fine"]?.DeepClone();rows.RemoveAt(index);changed();Build();}));card.Children.Add(commands);
            string id=row.S("id");var bill=Ui.Text(BillFor(id),11);bills.Add(id,bill);
            var billPanel=new Expander{Header="Distinta ferri · barre e staffe",Content=bill,IsExpanded=!expandedBills.TryGetValue(id,out bool open)||open};
            billPanel.Expanded+=(_,_)=>expandedBills[id]=true;billPanel.Collapsed+=(_,_)=>expandedBills[id]=false;
            commands.Children.Add(Ui.Button("Distinta ferri",()=>{selected=index;checkMode="Distinta ferri";billPanel.IsExpanded=true;UpdateResults();billPanel.BringIntoView();}));card.Children.Add(billPanel);
            stack.Children.Add(new Border{Background=SegmentColor(i),CornerRadius=new CornerRadius(5),Padding=new Thickness(10),Margin=new Thickness(0,7,0,0),Child=card});
        }
        details=new InputForm(e["dettagli"]!.AsObject(),ElasticPileFields.Details(),_=>changed(),compact:true);
        stack.Children.Add(new Expander{Header=ElasticPileFields.DetailsHeader,Content=details});
        stack.Children.Add(SeismicPanel(e["dettagli"]!.AsObject()));
        var extra=new InputForm(e["dettagli"]!.AsObject(),ElasticPileFields.Extra(),_=>{changed();},compact:true);
        extra.ShowField("cmin_dur",e["dettagli"].B("copriferro_override"));
        ((CheckBox)extra.Editors["copriferro_override"]).Click+=(_,_)=>extra.ShowField("cmin_dur",e["dettagli"].B("copriferro_override"));
        stack.Children.Add(new Expander{Header=ElasticPileFields.ExtraHeader,Content=Ui.Stack(Ui.Text(ElasticPileFields.ExtraNote(root["sezione"].S("esposizione")),12),extra)});stack.Children.Add(message);
        var layout=new Grid();layout.ColumnDefinitions.Add(new ColumnDefinition());layout.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(325)});layout.Children.Add(stack);Grid.SetColumn(summary,1);if(summary.Parent is Panel previous)previous.Children.Remove(summary);summary.Margin=new Thickness(16,0,0,0);summary.VerticalAlignment=VerticalAlignment.Top;layout.Children.Add(summary);Content=layout;UpdateSummary();
        var minimum=new CheckBox{Content=ElasticPileFields.MinimumLabel,IsChecked=e["dettagli"].B("minimi_pali"),Margin=new Thickness(4)};minimum.Click+=(_,_)=>{e["dettagli"]!["minimi_pali"]=minimum.IsChecked==true;changed();};stack.Children.Insert(stack.Children.Count-1,minimum);
    }
    string BillFor(string id)=>ElasticPilePresentation.BillFor(result(),calculationState,id);
    internal void UpdateSummary()
    {
        if(ElasticPilePresentation.Summary(root,result(),selected,checkMode,calculationState) is not { } model)return;selected=model.Selected;
        var choose=new ComboBox{ItemsSource=model.Segments,SelectedIndex=selected,Margin=new Thickness(0,6,0,6)};
        choose.SelectionChanged+=(_,_)=>{selected=choose.SelectedIndex;UpdateSummary();};
        var mode=Ui.Choice(ElasticPilePresentation.SummaryModes,checkMode);mode.SelectionChanged+=(_,_)=>{checkMode=mode.SelectedItem?.ToString()??"N–M";UpdateSummary();};
        var content=Ui.Stack(Ui.Text("Sezione e verifiche",15,true));
        if(model.Header is { } header)content.Children.Add(Text(header));
        content.Children.Add(choose);content.Children.Add(mode);
        if(model.Segment is not { } record)content.Children.Add(Ui.Text(ElasticPilePresentation.SegmentPending,12));
        else
        {
            var s=record.Section;var preview=new SectionDrawing{Width=205,Height=175,Caption=s.Caption,CircularLinkRadius=s.LinkRadius,LinkDiameter=s.LinkDiameter,LinkCaption=s.LinkCaption,ShowAxes=s.ShowAxes,Outline=[..s.Outline],Bars=[..s.Bars]};content.Children.Add(preview);
            content.Children.Add(Ui.Text(record.Details,11));
            AddVerdict(content,record.Verdict);
            foreach(var line in record.Lines)content.Children.Add(Text(line));
            content.Children.Add(Ui.Text(record.Error,12,color:Brushes.Firebrick));
        }
        content.Children.Add(Ui.Button("Apri verificatore c.a.",()=>OpenConcrete(selected)));content.Children.Add(Ui.Text(ElasticPilePresentation.SummaryNote,11,color:Ui.Muted));summary.Content=Ui.Paper(content,12);
    }
    void OpenConcrete(int index)
    {
        try{CreateConcreteWindow(index)?.ShowDialog();}
        catch(ArgumentException ex){message.Text=ex.Message;}
    }
    /// <summary>The c.a. verifier of the segment in its dialog, not shown yet (F5.8 traces); an invalid segment writes the message and gives null.</summary>
    internal Window? CreateConcreteWindow(int index)
    {
        try
        {
            Commit();var sheet=ElasticPileEditor.CreateConcreteSheet(root,result(),index);var workspace=new ConcreteWorkspace(sheet);var row=root["elastico"]!.Array("tratti")[index]!.AsObject();
            var note=Ui.Text(ElasticPileFields.ConcreteNote,12);
            if(ElasticPileEditor.TransverseKind(root,row)=="Spirale")note.Text+=ElasticPileFields.ConcreteSpiralNote;
            var apply=Ui.Button(index==0?"Applica armatura alla sezione principale":"Applica armatura al tratto",()=>{try{workspace.Commit();ElasticPileEditor.ApplyConcreteSheetReinforcement(root,sheet,index);if(index==0)sharedSectionChanged?.Invoke();changed();note.Text=ElasticPileFields.ConcreteApplied;}catch(ArgumentException ex){note.Text=ex.Message;}});
            var host = new ContentControl { Content = workspace };
            void Reload() { workspace.Dispose(); workspace = new ConcreteWorkspace(sheet); workspace.ReloadRequested += Reload; host.Content = workspace; }
            workspace.ReloadRequested += Reload;
            var dialog=Ui.Dialog(this,$"Verificatore c.a. · {row.S("id")}",Ui.Dock(host,Ui.Stack(note,apply)),1280,900);
            dialog.Closed+=(_,_)=>{workspace.Commit();workspace.Dispose();row["foglio_cls"]=sheet.DeepClone();changed();Build();};return dialog;
        }
        catch(ArgumentException ex){message.Text=ex.Message;return null;}
    }
    void Split(int index)=>CreateSplitWindow(index).ShowDialog();
    /// <summary>The dialog «Dividi il tratto», not shown yet (F5.8 traces).</summary>
    internal Window CreateSplitWindow(int index)
    {
        var edit=new TextBox{Text="",MinWidth=170};var dialog=Ui.Dialog(this,"Dividi il tratto",Ui.Stack(Ui.Text("Quota dalla testa [m]",13),edit),420,220);
        ((StackPanel)dialog.Content).Children.Add(Ui.Button("Dividi",()=>{try{var cut=J.Number(JsonValue.Create(edit.Text))??throw new ArgumentException("Quota numerica richiesta.");root["elastico"]!["tratti"]=PileReinforcementOperations.Split(root,index,cut);changed();Build();dialog.Close();}catch(ArgumentException ex){edit.ToolTip=ex.Message;message.Text=ex.Message;}}));return dialog;
    }
    void Propose()
    {
        try{CreateProposalWindow()?.ShowDialog();}
        catch(ArgumentException ex){message.Text=ex.Message;}
    }
    /// <summary>The dialog «Proposta di suddivisione», not shown yet (F5.8 traces); without result, or when the proposal fails, the message and null.</summary>
    internal Window? CreateProposalWindow()
    {
        if(result() is not JsonObject r){message.Text=""+calculationState+"";return null;}
        try{var proposal=PileReinforcementOperations.Propose(root,r);var dialog=Ui.Dialog(this,"Proposta di suddivisione",Ui.Stack(Ui.Text(ElasticPileFields.ProposalNote,13),Ui.Text(proposal.Segments.ToJsonString(J.Options),12)),700,500);
            ((StackPanel)dialog.Content).Children.Add(Ui.Button("Applica e archivia i tratti precedenti",()=>{try{PileReinforcementOperations.ApplyProposal(root,proposal);changed();Build();dialog.Close();}catch(ArgumentException ex){message.Text=ex.Message;MessageBox.Show(dialog,ex.Message,"Proposta da aggiornare");}}));return dialog;}
        catch(ArgumentException ex){message.Text=ex.Message;return null;}
    }
    private Task segmentDesignTask = Task.CompletedTask;
    void Design(int index) => CreateSegmentDesignWindow(index)?.ShowDialog();
    private Window? CreateSegmentDesignWindow(int index)
    {
        if (result() is not JsonObject r) { message.Text = calculationState; return null; }
        var counts = new TextBox { Text = "6;8;10;12;14;16;20;24", Margin = new Thickness(5) };
        var spacings = new TextBox { Text = "100;150;200", Margin = new Thickness(5) };
        var diameters = new TextBox { Text = "16;20;24;28;32", Margin = new Thickness(5) };
        var links = new TextBox { Text = "8;10;12", Margin = new Thickness(5) };
        var status = Ui.Text(ElasticPileFields.DesignIntro, 12);
        JsonObject? proposal = null; bool closed = false; int generation = 0;
        var session = new PileSegmentDesignSession(root, r, index);
        var designProgress = new ProgressBar { Height = 8, Maximum = 100, Visibility = Visibility.Collapsed };
        var apply = Ui.Button(index == 0 ? "Applica alla sezione principale" : "Applica al tratto", () =>
        {
            if (closed || proposal is null) return;
            try
            {
                session.Apply(root, proposal);
                if (index == 0) sharedSectionChanged?.Invoke(); changed(); Build();
            }
            catch (ArgumentException ex) { status.Text = ex.Message; }
        });
        apply.IsEnabled = false;
        var run = Ui.Button("Trova proposta", () => { });
        async Task RunAsync()
        {
            if (closed || !run.IsEnabled) return;
            run.IsEnabled = false; apply.IsEnabled = false; proposal = null; int requested = ++generation;
            try
            {
                double[] Values(TextBox box) => box.Text.Split(';').Select(s => J.Number(JsonValue.Create(s)) ?? throw new ArgumentException("Catalogo non numerico.")).ToArray();
                var ns = counts.Text.Split(';').Select(int.Parse).ToArray(); var ss = Values(spacings); var ds = Values(diameters); var ts = Values(links);
                status.Text = ElasticPileFields.DesignSearching; designProgress.Visibility = Visibility.Visible; designProgress.IsIndeterminate = true;
                var updates = new Progress<PileProgress>(p =>
                {
                    if (closed || run.IsEnabled || requested != generation) return;
                    status.Text = p.Phase; designProgress.IsIndeterminate = p.Total == 0;
                    designProgress.Value = p.Total > 0 ? 100d * p.Completed / p.Total : 0;
                });
                var calculation = await session.CalculateAsync(ns, ds, ts, ss, updates);
                calculation.TryPublish(value =>
                {
                    if (closed) return;
                    if (!session.Matches(root)) { status.Text = PileReinforcementOperations.RevisionChangedMessage; return; }
                    if (value.Error is not null) { status.Text = value.Error.Message; return; }
                    proposal = value.Value; status.Text = proposal!.ToJsonString(J.Options); apply.IsEnabled = true;
                });
            }
            catch (Exception ex) { if (!closed) status.Text = ex.Message; }
            finally { if (!closed) { run.IsEnabled = true; designProgress.Visibility = Visibility.Collapsed; } }
        }
        run.Click += (_, _) => segmentDesignTask = RunAsync();
        var dialog = Ui.Dialog(this, "Dimensionamento del tratto · proposta da applicare", Ui.Stack(
            Ui.Text("Quantità barre candidate (separate da ;)", 12), counts, Ui.Text("Diametri longitudinali [mm]", 12), diameters,
            Ui.Text("Diametri staffe [mm]", 12), links, Ui.Text("Passi staffe [mm]", 12), spacings, Ui.Bar(run, apply), designProgress, status), 740, 650);
        dialog.Closed += (_, _) => { closed = true; session.Dispose(); };
        return dialog;
    }
}
