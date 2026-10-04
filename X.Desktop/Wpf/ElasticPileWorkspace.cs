using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using X.Core;
using GPC.Checkers.Geotechnics.Piles;
namespace X.Desktop;
internal sealed class ElasticPileWorkspace : UserControl, IDisposable
{
    readonly JsonObject parent,data;
    readonly InputForm form;
    readonly JsonGrid layers;
    readonly ContentControl soilEditor=new();
    readonly TextBlock soilHelp=Ui.Text("Selezionare uno strato per assegnare i parametri.",12);
    readonly TextBlock adopted=Ui.Text("",12);
    InputForm? soilForm;
    readonly TextBlock status=Ui.Text("Completare EI e rigidezze del terreno",13), summary=Ui.Text("",14,true);
    readonly ElasticPileDrawing drawing=new(){MinWidth=1060,Height=560};
    readonly DataGrid table=new(){AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,Height=240};
    readonly Button csv;
    readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(300)};
    bool loading=true,disposed,busy;int revision;
    internal JsonObject? Result{get;private set;}
    internal event Action? Modified;
    internal ElasticPileWorkspace(JsonObject root)
    {
        parent=root;if(root["elastico"] is not JsonObject)root["elastico"]=ElasticHorizontalPile.Defaults(root);data=root["elastico"]!.AsObject();
        ElasticHorizontalPile.Upgrade(data);
        SetValue(InputForm.CommitOnFocusLossProperty,false);
        form=new InputForm(data,[new("diametro","Diametro geotecnico","m"),new("lunghezza","Lunghezza totale testa–punta","m"),new("libero","Tratto libero sopra il terreno","m"),
            new("EI","Rigidezza flessionale assegnata","kN m²"),new("fonte_EI","Origine EI (E, I, fessurazione)",Wide:true),new("H","Forza in testa (+ destra)","kN"),new("C","Momento in testa (+ con θ)","kNm"),new("e","Eccentricità alternativa: C=H·e","m"),
            new("vincolo","Rotazione in testa",Choices:["Libera","Impedita"]),new("punta","Vincolo alla punta",Choices:["Libera","Cerniera","Incastro"]),new("passo","Passo iniziale (confronto h e h/2)","m"),new("falda","Presenza falda",Bool:true),new("z_falda","Profondità falda dal piano campagna","m"),new("gamma_w","Peso unitario acqua","kN/m³")],key=>{Changed();WaterFields();if(key=="falda")BuildSoilEditor();},compact:true);
        WaterFields();
        layers=new JsonGrid([new("nome","Strato"),new("spessore","Spessore [m]"),new("legge","Determinazione del parametro",Choices:ElasticHorizontalPile.Laws)]){Height=150};
        layers.SelectionChanged+=(_,e)=>{if(e.Source==layers)BuildSoilEditor();};
        Rebind();
        layers.Columns[2].Width=340;
        csv=Ui.Button("Esporta tabella CSV",Export,inspection:true);csv.IsEnabled=false;
        var input=Ui.Stack(Ui.Text("Risposta elastica · dati dell’analisi",18,true),Ui.Text("Parametri separati dalla verifica di capacità. Nessun fattore di efficienza applicato. EI deve essere assegnato con la propria origine; non è il momento resistente My.",12),form);
        var info=Ui.Stack(Ui.Text("Terreno e condizioni del modello",18,true),Ui.Text(ElasticHorizontalPile.Limits,12),Ui.Text("kh: pressione/spostamento [kN/m³]; k=kh·D [kN/m²]. Reese–Matlock: kh=nh·z/D, k=nh·z; z dal piano campagna, senza azzeramento alle interfacce. 1 N/cm³ = 1000 kN/m³. Nel tratto libero k=0. Matrice di fondazione integrata, nessuna molla nodale indipendente.",12),Ui.Text(ViggianiHorizontalSoil.Source,11),Ui.Text(ViggianiHorizontalSoil.StratificationConvention,11),Ui.Text(ElasticHorizontalPile.Signs,12),
            Ui.Button("Copia geometria, H e stratigrafia dalla capacità",CopyCapacity),Ui.Text("La copia usa la prima stratigrafia. I valori di rigidezza restano da assegnare: il nome del terreno non determina kh. Lunghezza totale = lunghezza infissa copiata; aggiungendo un tratto libero, adeguare anche la lunghezza totale.",11,color:Ui.Muted));
        var top=new Grid();top.ColumnDefinitions.Add(new ColumnDefinition());top.ColumnDefinitions.Add(new ColumnDefinition());top.Children.Add(Ui.Paper(input,14));var right=Ui.Paper(info,14);Grid.SetColumn(right,1);top.Children.Add(right);
        var layerPanel=Ui.Stack(Ui.Text("Strati dal piano campagna verso il basso",16,true),layers,Ui.Bar(Ui.Button("+ Strato",()=>{Commit();data.Array("strati").Add(ElasticHorizontalPile.Layer());Rebind(data.Array("strati").Count-1);Changed();}),Ui.Button("− Strato",()=>{Commit();int i=layers.SelectedIndex;if(i>=0&&i<data.Array("strati").Count){data.Array("strati").RemoveAt(i);Rebind();Changed();}})),soilEditor,soilHelp,adopted);
        string[] headers=["x [m]","z [m]","Strato","Lato","kh [kN/m³]","nh [kN/m³]","k [kN/m²]","y [m]","θ [rad]","V [kN]","M [kNm]","q [kN/m]"];
        for(int i=0;i<headers.Length;i++)table.Columns.Add(new DataGridTextColumn{Header=headers[i],Binding=new Binding($"[{i}]"),Width=new DataGridLength(1,DataGridLengthUnitType.Star),MinWidth=75});
        var stack=Ui.Stack(top,Ui.Paper(layerPanel,14),Ui.Paper(Ui.Stack(status,summary,new ScrollViewer{Content=drawing,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled},csv,table),14));
        Content=new ScrollViewer{Content=stack,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};timer.Tick+=Tick;loading=false;Changed();
    }
    void WaterFields(){form.ShowField("z_falda",data.B("falda"));form.ShowField("gamma_w",data.B("falda"));}
    void Rebind(int selected=0){layers.Rows.Clear();foreach(var row in data.Array("strati").OfType<JsonObject>())layers.Rows.Add(new JsonRow(row,key=>{Changed();if(key=="legge")BuildSoilEditor();}));layers.SelectedIndex=Math.Min(selected,layers.Rows.Count-1);BuildSoilEditor();}
    void BuildSoilEditor()
    {
        soilForm=null;soilEditor.Content=null;if(layers.SelectedItem is not JsonRow selected)return;
        var row=selected.Values;int mode=Array.IndexOf(ElasticHorizontalPile.Laws,row.S("legge"));bool assisted=mode>=3;
        var fields=new List<Field>();
        if(mode<=2)fields.Add(new("valore",mode==0?"kh assegnato":mode==1?"nh assegnato":"k distribuito assegnato",mode==2?"kN/m²":"kN/m³"));
        if(mode is 3 or 5)fields.Add(new("densita","Stato di addensamento",Choices:ViggianiHorizontalSoil.SandTable.Select(r=>r.Density).ToArray()));
        if(mode==4){fields.Add(new("riga_146","Riga e autore della tabella 14.6",Choices:ViggianiHorizontalSoil.CohesiveTable.Select(r=>r.Label).ToArray()));fields.Add(new("nh_tabella","nh scelto esplicitamente nell’intervallo","N/cm³"));}
        if(mode==5){fields.Add(new("A","A scelto esplicitamente","−"));fields.Add(new("gamma","γ sopra falda","kN/m³"));if(data.B("falda"))fields.Add(new("gamma_sat","γsat sotto falda (γ′=γsat−γw)","kN/m³"));}
        if(assisted){fields.Add(new("override","Override manuale motivato di nh",Bool:true));fields.Add(new("nh_override","nh adottato con override","kN/m³"));fields.Add(new("motivo_override","Motivazione override",Wide:true));}
        fields.Add(new("fonte","Fonte del parametro e condizioni di impiego / drenaggio",Wide:true));
        soilForm=new InputForm(row,fields,key=>{Changed();SoilHelp();if(key=="override"){soilForm?.ShowField("nh_override",row.B("override"));soilForm?.ShowField("motivo_override",row.B("override"));}},compact:true,wideChoices:true){MaxWidth=900,HorizontalAlignment=HorizontalAlignment.Left};
        if(assisted){soilForm.ShowField("nh_override",row.B("override"));soilForm.ShowField("motivo_override",row.B("override"));}
        soilEditor.Content=soilForm;SoilHelp();
    }
    void SoilHelp()
    {
        if(layers.SelectedItem is not JsonRow selected)return;var row=selected.Values;int mode=Array.IndexOf(ElasticHorizontalPile.Laws,row.S("legge"));
        string text=mode==0?"kh costante: schematizzazione per argille sovraconsolidate. Il valore resta assegnato, senza correlazione automatica con cu.":mode==2?"k è già distribuito: il diametro non viene moltiplicato di nuovo.":"Legge Reese–Matlock: nh costante in ciascun tratto; kh=nh·z/D con z globale dal piano campagna.";
        var sand=ViggianiHorizontalSoil.SandTable.FirstOrDefault(r=>r.Density==row.S("densita"));
        if(mode is 3 or 5 && sand is not null)text+=mode==3?$" Tabella 14.5: nh non immerso {sand.DryNhNPerCm3:G4}, immerso {sand.SubmergedNhNPerCm3:G4} N/cm³. La presenza e profondità della falda selezionano i tratti.":$" Eq. 14.25: A tra {sand.MinimumA} e {sand.MaximumA}; consigliato {sand.RecommendedA}, da inserire esplicitamente. γ′=γsat−γw sotto falda. Questi valori non sono i nh direttamente tabellati.";
        var clay=ViggianiHorizontalSoil.CohesiveTable.FirstOrDefault(r=>r.Label==row.S("riga_146"));
        if(mode==4&&clay is not null)text+=$" Tabella 14.6: nh {clay.MinimumNPerCm3:G4}–{clay.MaximumNPerCm3:G4} N/cm³; {clay.Author}. Scelta esplicita obbligatoria, anche per la riga a valore unico. Valori orientativi.";
        soilHelp.Text=text;
    }
    void CopyCapacity()
    {
        layers.Commit();var g=parent["generali"]!;
        foreach(var pair in new[]{("diametro","diametro"),("lunghezza","lunghezza"),("H","azione_orizzontale"),("vincolo","vincolo")})form.Set(pair.Item1,g.S(pair.Item2));
        form.Set("libero","0");form.Set("C","0");form.Set("e","0");
        data["strati"]=new JsonArray(parent.Array("stratigrafie").FirstOrDefault()?.AsArray().Select(s=>(JsonNode)J.Obj(("nome",s.S("tipologia")),("spessore",s!["spessore"]?.DeepClone()),("legge",ElasticHorizontalPile.Laws[0]),("valore",""),("fonte","Assegnazione manuale"))).ToArray()??[]);Rebind();Changed();
    }
    void Changed(){if(loading||disposed)return;revision++;Result=null;table.ItemsSource=null;drawing.Set(null);summary.Text=adopted.Text="";csv.IsEnabled=false;status.Text="Dati modificati · ricalcolo automatico";timer.Stop();timer.Start();Modified?.Invoke();}
    async void Tick(object? sender,EventArgs args){if(busy)return;timer.Stop();await CalculateAsync();}
    internal void Commit(){form.Commit();layers.Commit();soilForm?.Commit();}
    internal async Task CalculateAsync()
    {
        if(disposed||busy)return;Commit();timer.Stop();int request=revision;busy=true;var snapshot=(JsonObject)data.DeepClone();
        try{var result=await Task.Run(()=>ElasticHorizontalPile.Calculate(snapshot));if(disposed||request!=revision)return;Result=result;Present();}
        catch(Exception ex){if(!disposed&&request==revision){Result=null;csv.IsEnabled=false;table.ItemsSource=null;drawing.Set(null);summary.Text=adopted.Text="";status.Text="Calcolo non disponibile: "+ex.Message;}}
        finally{busy=false;if(!disposed&&request!=revision)timer.Start();}
    }
    void Present()
    {
        var r=Result!["risposta"]!;csv.IsEnabled=true;drawing.Set(Result);
        adopted.Text=string.Join("\n",Result.Array("parametri_terreno").Select(p=>$"Strato {p.D("Layer")} · z {p.D("Top"):G5}–{p.D("Bottom"):G5} m · {ElasticHorizontalPile.MethodLabel(p.S("Method"))}: {ElasticHorizontalPile.ParameterLabel(p.S("Law"))} = {p.D("AdoptedValue"):G6} {p.S("Unit")}"+(p.B("Overridden")?$" · OVERRIDE da {p.D("BaseValue"):G6}: {p.S("OverrideReason")}":"")+" · "+p.S("Applicability")));
        status.Text=$"{r.D("Elements"):0} elementi · confronto con {r.D("ComparisonElements"):0} · "+(r.B("MeshConverged")?"variazioni ≤ 0,1%":"RAFFINARE LA MESH: variazioni > 0,1%")+$" · Δy {r["RelativeMeshChanges"].D("y"):P3}, ΔM {r["RelativeMeshChanges"].D("M"):P3}, ΔV {r["RelativeMeshChanges"].D("V"):P3}";
        var lines=new List<string>{$"Testa: y={r.D("HeadDisplacement"):G6} m; θ={r.D("HeadRotation"):G6} rad. Residui: ΣF={r.D("ForceResidual"):G3} kN; ΣMtesta={r.D("MomentResidual"):G3} kNm.",$"Reazioni: Ctesta={r.D("HeadReactionMoment"):G6} kNm; Hpunta={r.D("TipReactionForce"):G6} kN; Cpunta={r.D("TipReactionMoment"):G6} kNm."};
        foreach(var pair in new[]{("y","m"),("theta","rad"),("V","kN"),("M","kNm"),("q","kN/m")}){var e=r["Extrema"]![pair.Item1]!;lines.Add($"{pair.Item1} [{pair.Item2}]: min {e.D("Minimum"):G5} a x={e.D("MinimumDepth"):0.###} m; max {e.D("Maximum"):G5} a x={e.D("MaximumDepth"):0.###} m; |max| {e.D("AbsoluteMaximum"):G5} a x={e.D("AbsoluteMaximumDepth"):0.###} m.");}summary.Text=string.Join("\n",lines);
        table.ItemsSource=r.Array("Points").Select(p=>new[]{"Depth","GroundDepth","Layer","Side","Kh","Nh","DistributedStiffness","Displacement","Rotation","Shear","Moment","SoilReaction"}.Select(k=>J.Number(p![k]) is double n?n.ToString("G6"):p[k]?.ToString()??"—").ToArray()).ToArray();
    }
    void Export(){Commit();if(Result is null)return;var save=new SaveFileDialog{Filter="CSV|*.csv",FileName="Palo_risposta_elastica.csv"};if(save.ShowDialog(Window.GetWindow(this))==true)Archivio.ScriviAtomico(save.FileName,Encoding.UTF8.GetBytes(ElasticHorizontalPile.Csv(Result)));}
    public void Dispose(){disposed=true;timer.Stop();timer.Tick-=Tick;}
}
