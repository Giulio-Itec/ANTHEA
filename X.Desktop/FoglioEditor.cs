using System.Text.Json.Nodes;
using X.Core;

namespace X.Desktop;

public sealed partial class FoglioEditor:UserControl
{
    public string Module {get;}public JsonObject Data {get;}public JsonObject? Result {get;private set;}
    public event Action? Modified;public bool Busy {get;private set;}
    private bool building=true;
    private readonly TabControl inputs=new(){Dock=DockStyle.Fill},outputs=new(){Dock=DockStyle.Fill};
    private readonly Label status=new(){Dock=DockStyle.Fill,Text="Dati da verificare · premere Calcola",ForeColor=Ui.Muted,AutoEllipsis=true,TextAlign=ContentAlignment.MiddleLeft};
    private readonly TextBox warnings=new(){Multiline=true,ReadOnly=true,Dock=DockStyle.Bottom,Height=110,ScrollBars=ScrollBars.Vertical,BackColor=Color.FromArgb(255,250,235),BorderStyle=BorderStyle.FixedSingle};
    private readonly Button calculate;
    private readonly Plot plot=new();private readonly Plot domain=new(){InvertY=false};private readonly SectionDrawing sectionDrawing=new();
    private readonly CheckedListBox visible=new(){Dock=DockStyle.Left,Width=220,CheckOnClick=true,BorderStyle=BorderStyle.None};
    private readonly ComboBox tableSelect=new(){Dock=DockStyle.Top,DropDownStyle=ComboBoxStyle.DropDownList};private readonly DataGridView resultsGrid=Ui.Grid();
    private readonly ComboBox resultSelect=new(){Dock=DockStyle.Top,DropDownStyle=ComboBoxStyle.DropDownList};
    private readonly ComboBox domainType=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=110},domainMode=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=110};
    private readonly TextBox domainN=new(){Text="1000",Width=90};private readonly Label effLabel=new(){Text="ηc = 1 · ηt = 1",Dock=DockStyle.Bottom,Height=34,TextAlign=ContentAlignment.MiddleLeft};
    private readonly List<Serie> allSeries=[];private readonly List<string> seriesKeys=[];private List<Tabella> tables=[];
    private InputForm? generalForm,materialForm,normativeForm;private TabControl? sondages;private bool updatingLayers;
    private readonly Dictionary<string,DataGridView> comboGrids=new();
    private readonly Dictionary<string,JsonNode?> domainCache=new();
    public FoglioEditor(string module,JsonObject data)
    {
        Module=module;Data=(JsonObject)data.DeepClone();Dock=DockStyle.Fill;BackColor=Ui.Bg;
        calculate=Ui.Button("Calcola",async()=>await CalculateAsync(),true);
        var footer=new TableLayoutPanel{Dock=DockStyle.Bottom,Height=48,ColumnCount=4,Padding=new Padding(5)};footer.ColumnStyles.Add(new(SizeType.Percent,100));footer.ColumnStyles.Add(new(SizeType.Absolute,135));footer.ColumnStyles.Add(new(SizeType.Absolute,145));footer.ColumnStyles.Add(new(SizeType.Absolute,120));footer.Controls.Add(status);
        var split=new SplitContainer{Dock=DockStyle.Fill,SplitterWidth=7,FixedPanel=FixedPanel.Panel1};split.Size=new Size(1250,750);split.SplitterDistance=470;split.Panel1MinSize=350;split.Panel2MinSize=350;split.Panel1.Controls.Add(inputs);split.Panel2.Controls.Add(outputs);split.Panel2.Controls.Add(warnings);Controls.Add(split);Controls.Add(footer);
        footer.Controls.Add(Ui.Button("Estendi input",()=>{split.Panel1Collapsed=false;split.Panel2Collapsed=!split.Panel2Collapsed;}));footer.Controls.Add(Ui.Button("Estendi risultati",()=>{split.Panel2Collapsed=false;split.Panel1Collapsed=!split.Panel1Collapsed;}));footer.Controls.Add(calculate);
        var tablePanel=new Panel();tablePanel.Controls.Add(resultsGrid);tablePanel.Controls.Add(tableSelect);tableSelect.SelectedIndexChanged+=(_,_)=>ShowTable();
        if(module=="str_palo")BuildSection();else BuildGeo();
        Ui.Tab(outputs,"Tabelle e dettagli",tablePanel);
        var raw=new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,Font=new Font("Consolas",9)};Ui.Tab(outputs,"Risultati JSON",raw);outputs.SelectedIndexChanged+=(_,_)=>{if(outputs.SelectedTab?.Text=="Risultati JSON")raw.Text=Result?.ToJsonString(J.Options)??"Premere Calcola.";};
        RestoreOriginalLayout();if(PileCapacity)FitPileFonts();building=false;Preview();if(PileCapacity)InitializePileAutomatic();else Disposed+=(_,_)=>pileTimer.Dispose();
    }
    public void Commit(){Validate();foreach(var grid in GetAll(this).OfType<DataGridView>())grid.EndEdit();}
    private static IEnumerable<Control> GetAll(Control c){foreach(Control child in c.Controls){yield return child;foreach(var descendant in GetAll(child))yield return descendant;}}
    private void Changed()
    {
        if(!building){allSeries.Clear();seriesKeys.Clear();visible.Items.Clear();foreach(var control in curveChoices.Controls.Cast<Control>().ToArray())control.Dispose();curveChoices.Controls.Clear();}
        if(building)return;Result=null;UpdateVerification();tables=[];resultsGrid.Rows.Clear();tableSelect.Items.Clear();warnings.Text="";status.Text="Dati modificati · premere Calcola";plot.Series=[];plot.Title="Risultati da ricalcolare";plot.Invalidate();domain.Series=[];domain.Invalidate();domainCache.Clear();resultSelect.Items.Clear();sectionDrawing.Plane=null;Preview();QueuePileCalculation();Modified?.Invoke();
    }
    private bool Micro=>Module=="geo_micropalo_verticale";
    private void BuildGeo()
    {
        var defaults=Archivio.NuovoFoglio(Module);if(Data["generali"] is not JsonObject)Data["generali"]=new JsonObject();var g=Data["generali"]!.AsObject();
        string previous=Micro?"":Nq.MetodoPrecedente(g);
        foreach(var (k,v) in defaults["generali"]!.AsObject())if(!g.ContainsKey(k)&&k!="metodo_micropalo")g[k]=v?.DeepClone();
        if(!Micro){g["metodo_nq"]="Parametrizzata";if(previous!="")g["metodo_nq_precedente"]=previous;}
        if(Data["efficienza"] is not JsonObject)Data["efficienza"]=defaults["efficienza"]!.DeepClone();
        var fields=new List<Field>();
        if(!Micro){fields.Add(new("tipo_palo","Tipo di palo",Choices:["Trivellato","Elica continua","Battuto"]));fields.Add(new("sottotipo_palo_battuto","Tipo battuto",Choices:Calcolo.Parametri.Keys.Where(k=>k is not ("Trivellato" or "Elica continua")).ToArray()));}
        fields.AddRange([new("diametro",Micro?"Diametro perforazione Db":"Diametro D","m"),new("lunghezza","Lunghezza L","m"),new("peso_specifico_palo","Peso specifico CLS / palo","kN/m³"),new("azione_compressione","Azione di compressione","kN"),new("azione_trazione","Azione di trazione","kN")]);
        if(Micro)fields.AddRange([new("metodo_micropalo","Metodo (richiesto)",Choices:[BustamanteDoix.Versione]),new("tipo_iniezione","Iniezione",Choices:["IGU","IRS"]),new("profilo_chs","Profilo CHS",Choices:Chs.Catalogo.Keys.ToArray()),new("pressione_iniezione","Pressione p_i = p_l","MPa"),new("inclinazione","Inclinazione dalla verticale θ","°"),new("inizio_aderenza","Inizio aderenza sb lungo asse","m"),new("considera_punta","Considera punta",Bool:true),new("percentuale_punta","Punta rispetto alla laterale","%")]);
        else fields.AddRange([new("presenza_falda","Presenza falda",Bool:true),new("profondita_falda","Profondità falda","m"),new("considera_sottospinta","Considera sottospinta",Bool:true),new("metodo_nq","Metodo Nq",Choices:["Parametrizzata"])]);
        string[] fieldOrder=Micro?["tipo_iniezione","diametro","lunghezza","inizio_aderenza","considera_punta","percentuale_punta","pressione_iniezione","profilo_chs","peso_specifico_palo","azione_compressione","azione_trazione","inclinazione","metodo_micropalo"]:["tipo_palo","sottotipo_palo_battuto","diametro","lunghezza","peso_specifico_palo","presenza_falda","profondita_falda","considera_sottospinta","azione_compressione","azione_trazione","metodo_nq"];
        fields=fields.OrderBy(f=>Array.IndexOf(fieldOrder,f.Key)).ToList();
        generalForm=new(g,fields,key=>{if(Micro&&key=="tipo_iniezione")ResetAlphas();Changed();},capacityStyle:!Micro);generalForm.ShowField(Micro?"metodo_micropalo":"metodo_nq",false);Ui.Tab(inputs,"Dati generali",generalForm);
        var normativeFields=new[]{new Field("verticali_indagate","Verticali indagate",Choices:Calcolo.Verticali.Keys.ToArray()),new("__xi3","Correlazione ξ3",ReadOnly:true),new("__xi4","Correlazione ξ4",ReadOnly:true),new("sicurezza_laterale_compressione","γs,c · laterale compressione"),new("sicurezza_laterale_trazione","γs,t · laterale trazione"),new("sicurezza_base","γb · base"),new("peso_palo_sfavorevole","γG · peso sfavorevole"),new("peso_palo_favorevole","γG · peso favorevole")};
        var nf=new InputForm(g,normativeFields,_=>Changed(),capacityStyle:!Micro);normativeForm=nf;Ui.Tab(inputs,"Coefficienti",Ui.WithToolbar(nf,Ui.Button("Reset",()=>{foreach(var f in normativeFields.Where(f=>!f.ReadOnly))nf.Set(f.Key,defaults["generali"].S(f.Key));})));
        var efficiency=new InputForm(Data["efficienza"]!.AsObject(),[new("metodo","Metodo efficienza",Choices:["Nessuna riduzione","Converse-Labarre","Feld","Definita dall'utente"]),new("numero_pali_x","Numero pali X"),new("numero_pali_y","Numero pali Y"),new("interasse_x","Interasse X","m"),new("interasse_y","Interasse Y","m"),new("eta_compressione","ηg,c manuale"),new("eta_trazione","ηg,t manuale")],_=>Changed(),capacityStyle:!Micro);efficiency.Name="efficiency";
        var ep=new Panel();ep.Controls.Add(efficiency);ep.Controls.Add(effLabel);Ui.Tab(inputs,"Efficienza",ep);
        sondages=new TabControl{Dock=DockStyle.Fill};if(Data["stratigrafie"] is not JsonArray)Data["stratigrafie"]=new JsonArray();RebuildSondages();
        Ui.Tab(inputs,"Stratigrafie",Ui.WithToolbar(sondages,Ui.Button("+ Sondaggio",()=>{Data.Array("stratigrafie").Add(new JsonArray());RebuildSondages();sondages.SelectedIndex=sondages.TabCount-1;Changed();}),Ui.Button("− Sondaggio",()=>{if(sondages.SelectedIndex<0)return;if(MessageBox.Show(this,"Eliminare il sondaggio selezionato dal foglio?","Sondaggio",MessageBoxButtons.YesNo)==DialogResult.Yes){Data.Array("stratigrafie").RemoveAt(sondages.SelectedIndex);RebuildSondages();Changed();}})));
        var graph=new Panel();graph.Controls.Add(plot);graph.Controls.Add(visible);visible.ItemCheck+=(_,_)=>BeginInvoke((Action)UpdateVisible);Ui.Tab(outputs,"Capacità portante",Ui.WithToolbar(graph,Ui.Button("Tutte",()=>SetVisible(true)),Ui.Button("Nessuna",()=>SetVisible(false)),Ui.Button("Adatta",plot.ResetView),Ui.Button("Salva PNG",SavePlot)));
        var profile=new Plot{Title="Profilo stratigrafico",XLabel="Larghezza schematica",YLabel="Profondità verticale z [m]"};profile.Name="profile";Ui.Tab(outputs,"Profilo",profile);
        var nqPlot=new Plot{Title=Micro?"Abachi Bustamante–Doix":"Nq parametrizzato",InvertY=false,XLabel=Micro?"p_l [MPa]":"φ [°]",YLabel=Micro?"s [kPa]":"Nq / Nq*"};nqPlot.Name="nq";Ui.Tab(outputs,Micro?"Abachi":"Nq",nqPlot);BuildReferencePlot(nqPlot);
    }
    private void ResetAlphas()
    {
        if(updatingLayers)return;updatingLayers=true;
        try{foreach(var rows in Data.Array("stratigrafie"))foreach(var row in rows!.AsArray())if(BustamanteDoix.Terreni.ContainsKey(row.S("terreno")))row!["alpha"]=BustamanteDoix.IntervalloAlpha(row.S("terreno"),Data["generali"].S("tipo_iniezione"))[0].ToString(System.Globalization.CultureInfo.InvariantCulture);RebuildSondages();}
        finally{updatingLayers=false;}
    }
    private void RebuildSondages()
    {
        if(sondages is null)return;int selected=sondages.SelectedIndex;foreach(var tab in sondages.TabPages.Cast<TabPage>().ToArray())tab.Dispose();sondages.TabPages.Clear();int index=0;
        foreach(var rowsNode in Data.Array("stratigrafie"))
        {
            var rows=rowsNode!.AsArray();var grid=Ui.Grid(true);
            Field[] fields=Micro?[new("spessore","Spessore verticale [m]"),new("terreno","Terreno",Choices:BustamanteDoix.Terreni.Keys.ToArray()),new("alpha","α adottato"),new("laterale_attiva","Laterale",Bool:true)]:[new("spessore","Spessore [m]"),new("tipologia","Tipologia",Choices:["Granulare","Coesivo"]),new("addensamento","Addensamento",Choices:["Sciolto","Denso"]),new("peso_specifico","γ [kN/m³]"),new("peso_specifico_saturo","γsat [kN/m³]"),new("angolo_attrito","φ′ [°]"),new("coesione_efficace","c′ [kPa]"),new("coesione_non_drenata","Cu [kPa]"),new("nc","Nc"),new("laterale_attiva","Laterale",Bool:true)];
            foreach(var field in fields)
            {
                DataGridViewColumn col;if(field.Bool)col=new DataGridViewCheckBoxColumn();else if(field.Choices is not null){var cb=new DataGridViewComboBoxColumn{FlatStyle=FlatStyle.Flat};cb.Items.AddRange(field.Choices);foreach(var r in rows){var value=r.S(field.Key);if(value!=""&&!cb.Items.Contains(value))cb.Items.Add(value);}cb.Items.Add("");col=cb;}else col=new DataGridViewTextBoxColumn();col.Name=field.Key;col.HeaderText=field.Label;col.MinimumWidth=80;grid.Columns.Add(col);
            }
            foreach(var r in rows)grid.Rows.Add(fields.Select(f=>f.Bool?(object)r.B(f.Key,true):r.S(f.Key,f.Key=="nc"?"9":"")).ToArray());
            grid.ColumnHeadersDefaultCellStyle.BackColor=Ui.Navy;grid.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;grid.ColumnHeadersDefaultCellStyle.SelectionBackColor=Ui.Navy;grid.ColumnHeadersDefaultCellStyle.SelectionForeColor=Color.White;grid.ColumnHeadersDefaultCellStyle.WrapMode=DataGridViewTriState.True;grid.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;grid.ColumnHeadersHeight=50;grid.RowTemplate.Height=36;
            string[] columnOrder=Micro?["laterale_attiva","terreno","spessore","alpha"]:["laterale_attiva","tipologia","addensamento","spessore","peso_specifico","peso_specifico_saturo","angolo_attrito","coesione_efficace","coesione_non_drenata","nc"];
            for(int ci=0;ci<columnOrder.Length;ci++)grid.Columns[columnOrder[ci]].DisplayIndex=ci;
            foreach(DataGridViewColumn col in grid.Columns){col.AutoSizeMode=DataGridViewAutoSizeColumnMode.None;col.Width=Micro&&col.Name=="terreno"?260:110;}
            if(!Micro)StylePileLayers(grid);
            grid.CurrentCellDirtyStateChanged+=(_,_)=>{if(grid.IsCurrentCellDirty)grid.CommitEdit(DataGridViewDataErrorContexts.Commit);};
            grid.CellValueChanged+=(_,e)=>
            {
                if(e.RowIndex<0||e.RowIndex>=rows.Count||e.ColumnIndex<0)return;string key=grid.Columns[e.ColumnIndex].Name;if(key.StartsWith("__"))return;var value=grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;rows[e.RowIndex]![key]=key=="laterale_attiva"?JsonValue.Create(value is true):JsonValue.Create(value?.ToString()??"");
                if(Micro&&key=="terreno"&&BustamanteDoix.Terreni.ContainsKey(value?.ToString()??"")&&Data["generali"].S("tipo_iniezione") is "IGU" or "IRS")grid.Rows[e.RowIndex].Cells["alpha"].Value=BustamanteDoix.IntervalloAlpha(value!.ToString()!,Data["generali"].S("tipo_iniezione"))[0].ToString(System.Globalization.CultureInfo.InvariantCulture);
                Changed();
            };
            var panel=Ui.WithToolbar(grid,Ui.Button("+ Strato",()=>
            {
                var row=Micro?J.Obj(("spessore",""),("terreno",""),("alpha",""),("laterale_attiva",true)):J.Obj(("spessore",""),("tipologia","Granulare"),("addensamento","Sciolto"),("peso_specifico",""),("peso_specifico_saturo",""),("angolo_attrito",""),("coesione_efficace",""),("coesione_non_drenata",""),("nc","9"),("laterale_attiva",true));rows.Add(row);grid.Rows.Add(fields.Select(f=>f.Bool?(object)true:row.S(f.Key)).ToArray());Changed();
            }),Ui.Button(Micro?"− Strato":"Elimina ultimo strato",()=>{if(grid.Rows.Count==0)return;int i=Micro?grid.CurrentRow?.Index??-1:grid.Rows.Count-1;if(i<0)return;rows.RemoveAt(i);grid.Rows.RemoveAt(i);Changed();}));
            if(!Micro)ArrangePileLayerActions(panel,grid);
            Ui.Tab(sondages,Micro?$"Sondaggio {++index}":$"{++index}",panel);
        }
        if(selected>=0&&selected<sondages.TabCount)sondages.SelectedIndex=selected;
    }
    private void BuildSection()
    {
        var defaults=SezioneCA.DefaultInput();if(Data["input"] is not JsonObject)Data["input"]=defaults.DeepClone();var input=Data["input"]!.AsObject();foreach(var (k,v) in defaults)if(!input.ContainsKey(k))input[k]=v?.DeepClone();
        if(new[]{"apply_pile_requirements","apply_minimum_eccentricity","dissipative_zone"}.Any(k=>input.B(k)))warnings.Text="Archivio aggiornato: rimossi requisiti specifici ed eccentricità minima, come nella versione Python attuale.";
        foreach(var k in new[]{"apply_pile_requirements","apply_minimum_eccentricity","dissipative_zone"})input[k]=false;input["minimum_eccentricity_mm"]="0";Data["versione_sezione"]=2;
        generalForm=new(input,[new("shape","Sezione",Choices:["Circolare","Rettangolare","A T"]),new("diameter_mm","Diametro D","mm"),new("width_mm","Larghezza b","mm"),new("height_mm","Altezza h","mm"),new("flange_width_mm","Larghezza ala bf","mm"),new("web_width_mm","Larghezza anima bw","mm"),new("flange_thickness_mm","Spessore ala hf","mm"),new("cover_mm","Copriferro netto alla staffa","mm")],_=>Changed());Ui.Tab(inputs,"Geometria",generalForm);
        var material=new[]{new Field("classe_cls","Classe CLS",Choices:["C12/15","C16/20","C20/25","C25/30","C30/37","C35/45","C40/50","C45/55","C50/60","C55/67","C60/75","C70/85","C80/95","C90/105","Personalizzato"]),new("__ec2","Deformazione εc2","‰",ReadOnly:true),new("__ecu","Ultima εcu","‰",ReadOnly:true),new("fck_mpa","Resistenza fck","MPa"),new("__fcd","Progetto fcd","MPa",ReadOnly:true),new("__ecm","Modulo Ecm","MPa",ReadOnly:true),new("__esu","Ultima εsu (inform.)","‰",ReadOnly:true),new("__fyd","Progetto fyd","MPa",ReadOnly:true),new("steel_modulus_mpa","Modulo Es","MPa"),new("__esyd","Snervamento εsyd","‰",ReadOnly:true),new("n","Omogeneizzazione n")};
        materialForm=new(input,material,key=>{if(key=="classe_cls"&&input.S(key).StartsWith('C'))materialForm?.Set("fck_mpa",input.S(key)[1..].Split('/')[0]);if(key=="n"&&!building)Data["n_automatico"]=false;Changed();});
        materialForm.CompactRows();var matPanel=new Panel();matPanel.Controls.Add(materialForm);var resetN=Ui.Button("Ripristina n = Es/Ecm",()=>{Data["n_automatico"]=true;Changed();});resetN.Dock=DockStyle.Bottom;matPanel.Controls.Add(resetN);Ui.Tab(inputs,"Materiali",matPanel);
        var settings=Ui.Button("Coefficienti materiali…",()=>{using var dialog=new Form{Text="Coefficienti materiali",Size=new Size(440,275),StartPosition=FormStartPosition.CenterParent};dialog.Controls.Add(new InputForm(input,[new("fyk_mpa","Resistenza fyk","MPa"),new("alpha_cc","αcc"),new("gamma_c","γc"),new("gamma_s","γs")],_=>Changed()));dialog.ShowDialog(this);});settings.Dock=DockStyle.Bottom;generalForm.Controls.Add(settings);
        string[] diameters=["6","8","10","12","14","16","18","20","22","24","26","28","30","32","36","40"];
        var bars=new InputForm(input,[new("longitudinal_bar_count","Barre circolari: quantità"),new("longitudinal_bar_diameter_mm","Barre circolari: diametro","mm",diameters),new("top_bar_count","Barre superiori: quantità"),new("top_bar_diameter_mm","Barre superiori: diametro","mm",diameters),new("bottom_bar_count","Barre inferiori: quantità"),new("bottom_bar_diameter_mm","Barre inferiori: diametro","mm",diameters),new("side_bar_count_per_side","Barre laterali: per lato"),new("side_bar_diameter_mm","Barre laterali: diametro","mm",diameters),new("transverse_bar_diameter_mm","Diametro staffa","mm",diameters),new("transverse_spacing_mm","Passo staffe","mm")],_=>Changed());bars.Name="bars";Ui.Tab(inputs,"Armatura",bars);
        if(Data["combinazioni"] is not JsonObject)Data["combinazioni"]=J.Obj(("SLU",new JsonArray(J.Obj(("nome","Combo 1"),("azioni",new[]{input.S("axial_force_kn","0"),input.S("moment_x_knm","0"),input.S("moment_y_knm","0")})))));
        var comboTabs=new TabControl();foreach(var limit in new[]{"SLU","SLV","SLE"})
        {
            if(Data["combinazioni"]![limit] is not JsonArray)Data["combinazioni"]![limit]=new JsonArray();var rows=Data["combinazioni"]![limit]!.AsArray();var grid=Ui.Grid(true);comboGrids[limit]=grid;
            foreach(var h in new[]{"Nome","N [kN]","Mx [kNm]","My [kNm]"})grid.Columns.Add(h,h);
            foreach(var row in rows)grid.Rows.Add(new[]{row.S("nome")}.Concat(row!.Array("azioni").Select(v=>v?.ToString()??"")).Cast<object>().ToArray());
            void Sync(){rows.Clear();foreach(DataGridViewRow row in grid.Rows)rows.Add(J.Obj(("nome",row.Cells[0].Value?.ToString()??""),("azioni",Enumerable.Range(1,3).Select(i=>row.Cells[i].Value?.ToString()??"").ToArray())));Changed();}
            grid.CellValueChanged+=(_,e)=>{if(e.RowIndex>=0&&e.ColumnIndex<4)Sync();};
            foreach(var name in limit=="SLE"?new[]{"σs [MPa]","σc [MPa]"}:new[]{"R"}){int column=grid.Columns.Add(name,name);grid.Columns[column].ReadOnly=true;}
            grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
            Ui.Tab(comboTabs,limit,Ui.WithToolbar(grid,Ui.Button("+ Combinazione",()=>{grid.Rows.Add("Combo "+(grid.Rows.Count+1),"0","0","0");Sync();}),Ui.Button("− Combinazione",()=>{if(grid.CurrentRow is not null){grid.Rows.RemoveAt(grid.CurrentRow.Index);Sync();}})));
        }
        Ui.Tab(inputs,"Azioni SLU / SLV / SLE",comboTabs);
        var drawingPanel=new Panel();drawingPanel.Controls.Add(sectionDrawing);drawingPanel.Controls.Add(resultSelect);resultSelect.SelectedIndexChanged+=(_,_)=>ShowSectionState();Ui.Tab(outputs,"Sezione",drawingPanel);
        domainType.Items.AddRange(["N–Mx","Mx–My"]);domainType.SelectedIndex=0;domainMode.Items.AddRange(["Plastico","Elastico"]);domainMode.SelectedIndex=0;
        void DomainChanged(){domain.Series=[];domain.Invalidate();status.Text="Dominio selezionato da calcolare · premere Calcola dominio";}
        domainType.SelectedIndexChanged+=(_,_)=>DomainChanged();domainMode.SelectedIndexChanged+=(_,_)=>DomainChanged();domainN.TextChanged+=(_,_)=>DomainChanged();
        var domainPanel=new Panel();var domainBar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=105,WrapContents=true,BackColor=Color.White};
        domainBar.Controls.AddRange([domainType,domainMode,new Label{Text="N [kN]",AutoSize=true,Padding=new Padding(0,6,0,0)},domainN,Ui.Button("Calcola dominio",async()=>await CalculateDomainAsync()),Ui.Button("Adatta",domain.ResetView)]);
        domainPanel.Controls.Add(domain);domainPanel.Controls.Add(domainBar);Ui.Tab(outputs,"Domini",domainPanel);
    }
    private void Preview()
    {
        if(Module!="str_palo")
        {
            if(PileCapacity){curveChoices.Visible=allSeries.Count>0;plot.CapacityEmptyMessage=Data["generali"].D("lunghezza")>0?"Completare i dati · calcolo automatico":"Lunghezza: inserire un numero valido";}
            var r=Calcolo.Efficienza(Data);effLabel.Text=r.S("errore")!=""?r.S("errore"):$"ηg,c = {r.D("eta_compressione"):F3}     ηg,t = {r.D("eta_trazione"):F3}";
            if(Calcolo.Verticali.TryGetValue(Data["generali"].S("verticali_indagate"),out var xi)){normativeForm?.Set("__xi3",xi.Xi3.ToString("F2"));normativeForm?.Set("__xi4",xi.Xi4.ToString("F2"));}
            if(generalForm is not null&&!Micro){generalForm.Enable("sottotipo_palo_battuto",Data["generali"].S("tipo_palo")=="Battuto");generalForm.Enable("profondita_falda",Data["generali"].B("presenza_falda"));generalForm.Enable("considera_sottospinta",Data["generali"].B("presenza_falda"));}
            if(Micro)generalForm?.Enable("percentuale_punta",Data["generali"].B("considera_punta"));
            var efficiencyForm=GetAll(this).OfType<InputForm>().FirstOrDefault(f=>f.Name=="efficiency");string method=Data["efficienza"].S("metodo");
            if(efficiencyForm is not null)foreach(var key in efficiencyForm.Editors.Keys.Where(k=>k!="metodo"))efficiencyForm.ShowField(key,key.StartsWith("numero_")?method is "Feld" or "Converse-Labarre":key.StartsWith("interasse")?method=="Converse-Labarre":method=="Definita dall'utente");
            BuildProfile();var reference=GetAll(outputs).OfType<Plot>().FirstOrDefault(p=>p.Name=="nq");if(reference is not null)BuildReferencePlot(reference);return;
        }
        var input=Data["input"]!.AsObject();string shape=input.S("shape");foreach(var key in new[]{"diameter_mm","width_mm","height_mm","flange_width_mm","web_width_mm","flange_thickness_mm"})generalForm?.ShowField(key,key switch{"diameter_mm"=>shape=="Circolare","width_mm"=>shape=="Rettangolare","height_mm"=>shape!="Circolare",_=>shape=="A T"});
        var barForm=GetAll(this).OfType<InputForm>().FirstOrDefault(f=>f.Name=="bars");if(barForm is not null)foreach(var key in barForm.Editors.Keys)if(!key.StartsWith("transverse"))barForm.ShowField(key,key.StartsWith("longitudinal")?shape=="Circolare":shape!="Circolare");
        try
        {
            var preview=(JsonObject)input.DeepClone();foreach(var key in new[]{"fck_mpa","fyk_mpa","alpha_cc","gamma_c","gamma_s","steel_modulus_mpa","transverse_spacing_mm"})preview[key]=SezioneCA.DefaultInput()[key]!.DeepClone();
            var engine=new SezioneCA(preview,12,36);sectionDrawing.Outline=engine.Outline;sectionDrawing.Bars=engine.Bars;
        }
        catch(ArgumentException){sectionDrawing.Outline=[];sectionDrawing.Bars=[];}
        sectionDrawing.Invalidate();
        double? fck=J.Number(input["fck_mpa"]),es=J.Number(input["steel_modulus_mpa"]);
        if(fck>0&&es>0){double ec=22000*Math.Pow((fck.Value+8)/10,.3),n=es.Value/ec;effLabel.Text=$"Ecm = {ec:F1} MPa · n automatico = {n:G9}\r\nSLE: "+(Data.B("n_automatico",true)?"n automatico":"n manuale")+" · εsu = 67,5‰ informativo\r\nLegame SLU: acciaio elastico-perfettamente plastico.";}
        else effLabel.Text="Materiali incompleti";
        try
        {
            var materialEngine=new SezioneCA(input,12,36);
            if(Data.B("n_automatico",true))materialForm?.SetDisplay("n",materialEngine.NAutomatico.ToString("G6"));
            materialForm?.Set("__ec2",(materialEngine.EpsC2*1000).ToString("G5"));materialForm?.Set("__ecu",(materialEngine.EpsCu*1000).ToString("G5"));materialForm?.Set("__fcd",materialEngine.Fcd.ToString("F2"));materialForm?.Set("__fyd",materialEngine.Fyd.ToString("F2"));materialForm?.Set("__ecm",(materialEngine.Es/materialEngine.NAutomatico).ToString("F0"));materialForm?.Set("__esyd",(materialEngine.Fyd/materialEngine.Es*1000).ToString("F3"));materialForm?.Set("__esu","67,5");
        }
        catch(ArgumentException){if(materialForm is not null)foreach(var key in materialForm.Editors.Keys.Where(k=>k.StartsWith("__")))materialForm.Set(key,"—");}
    }
    public async Task CalculateAsync()
    {
        if(Busy)return;if(!PileCapacity)Commit();else pileTimer.Stop();Busy=true;calculate.Enabled=false;if(!PileCapacity)originalCanvas.Enabled=false;status.Text="Calcolo in corso…";var snapshot=(JsonObject)Data.DeepClone();int revision=pileRevision;
        try
        {
            var result=await Task.Run(()=>Module=="str_palo"?CalcoloSezione.Calcola(snapshot):Calcolo.Calcola(snapshot,Micro));
            if(IsDisposed||PileCapacity&&revision!=pileRevision)return;
            Result=result;
            if(Result.S("errore")!=""){status.Text=(PileCapacity?"Dati da completare: ":"")+Result.S("errore");if(PileCapacity){Result=null;warnings.Text="";}else warnings.Text=Result.S("errore");return;}
            warnings.Text=string.Join(Environment.NewLine,Result.Array("avvisi").Select(v=>v!.ToString()));
            if(Module=="str_palo")ShowSectionResults();else ShowGeoResults();
            status.Text="Calcolo completato · risultati riferiti ai dati correnti";
        }
        catch(Exception ex){status.Text="Errore: "+ex.Message;warnings.Text=ex.Message;Result=null;}
        finally{Busy=false;if(!IsDisposed){calculate.Enabled=true;originalCanvas.Enabled=true;UpdatePileRawResult();if(PileCapacity&&revision!=pileRevision)pileTimer.Start();}}
    }
    private void ShowGeoResults()
    {
        allSeries.Clear();seriesKeys.Clear();visible.Items.Clear();Color[] colors=[Ui.Navy,Ui.Navy,Color.FromArgb(17,24,39),Color.FromArgb(17,24,39)];int index=0;
        foreach(var (name,curve) in Result!["curve"]!.AsObject())foreach(var branch in new[]{"progetto","media","minima"})
        {
            string key=branch+"_"+name;string label=name.Replace("non_drenante","Non dren.").Replace("drenante","Dren.").Replace("compressione","C").Replace("trazione","T").Replace('_',' ')+" · "+branch;var points=curve!.Array(branch).Select(p=>new[]{p![1]!.GetValue<double>(),p[0]!.GetValue<double>()}).ToList();allSeries.Add(new(label,points,colors[index%4],branch!="progetto"));seriesKeys.Add(key);visible.Items.Add(label,Data["visibilita_grafici"]?.AsObject().ContainsKey(key)==true?Data["visibilita_grafici"].B(key):branch=="progetto");if(branch=="minima")index++;
        }
        foreach(var (name,points) in Result["azioni"]!.AsObject())if(points!.AsArray().Count>0){allSeries.Add(new("Ed "+name,points.AsArray().Select(p=>new[]{p![1]!.GetValue<double>(),p[0]!.GetValue<double>()}).ToList(),Color.Black,true));seriesKeys.Add("azione_"+name);visible.Items.Add("Ed "+name,Data["visibilita_grafici"].B("azione_"+name,true));}
        plot.Title=Micro?"Capacità portante del micropalo":"Capacità portante del palo";plot.YLabel=Micro?"Lungo asse s [m]":"Profondità z [m]";plot.CapacityDepth=Result.D("profondita_massima");plot.SondageCount=Data.Array("stratigrafie").Count;plot.ResetView();UpdateVisible();RebuildCurveChoices();
        tables=Tabelle.Crea(Result,Micro);var summary=new List<string[]>();foreach(var (key,c) in Result["curve"]!.AsObject())
        {
            var p=c!.Array("progetto")[^1]!;double rd=p[1]!.GetValue<double>();var a=Result["azioni"]!.Array(key.Contains("trazione")?"trazione":"compressione").LastOrDefault();double? ed=J.Number(a?[1]);summary.Add([key.Replace('_',' '),Tabelle.F(p[0]),Tabelle.F(rd),ed.HasValue?Tabelle.F(ed.Value):"—",ed.HasValue?(ed<=rd?"Verificato":"Non verificato"):"Azione non inserita"]);
        }
        tables.Insert(0,new("Riepilogo alla quota disponibile",["Verifica",Micro?"s [m]":"z [m]","Rd [kN]","Ed [kN]","Esito"],summary));PopulateTables();UpdateVerification();
        if(!Result.B("copertura_completa"))warnings.Text="ATTENZIONE: stratigrafia insufficiente; risultati limitati alla quota disponibile, non alla punta richiesta.\r\n"+warnings.Text;
    }
    private void SetVisible(bool show){for(int i=0;i<visible.Items.Count;i++)visible.SetItemChecked(i,show);}
    private void UpdateVisible()
    {
        foreach(var check in curveChoices.Controls.OfType<CheckBox>())if(check.Tag is int index&&index<visible.Items.Count&&check.Checked!=visible.GetItemChecked(index))check.Checked=visible.GetItemChecked(index);
        if(IsDisposed)return;plot.Series=allSeries.Where((_,i)=>i<visible.Items.Count&&visible.GetItemChecked(i)&&CapacityIncludes(seriesKeys[i])).ToList();if(PileCapacity&&Result is not null)plot.CapacityEmptyMessage="Nessuna curva selezionata";plot.Invalidate();
        if(Result is null)return;if(Data["visibilita_grafici"] is not JsonObject)Data["visibilita_grafici"]=new JsonObject();bool changed=false;
        for(int i=0;i<allSeries.Count&&i<visible.Items.Count;i++){string key=seriesKeys[i];bool value=visible.GetItemChecked(i);if(Data["visibilita_grafici"]![key]?.ToString()!=value.ToString().ToLowerInvariant())changed=true;Data["visibilita_grafici"]![key]=value;}
        if(changed)Modified?.Invoke();
    }
    private void PopulateTables(){tableSelect.Items.Clear();tableSelect.Items.AddRange(tables.Select(t=>t.Titolo).ToArray());if(tables.Count>0)tableSelect.SelectedIndex=0;}
    private void ShowTable(){resultsGrid.Columns.Clear();if(tableSelect.SelectedIndex<0)return;var t=tables[tableSelect.SelectedIndex];foreach(var h in t.Colonne)resultsGrid.Columns.Add(h,h);foreach(var row in t.Righe)resultsGrid.Rows.Add(row.Cast<object>().ToArray());}
    private void ShowSectionResults()
    {
        var rows=new List<string[]>();resultSelect.Items.Clear();
        foreach(var (name,result) in Result!["risultati"]!.AsObject())
        {
            resultSelect.Items.Add(name);rows.Add([name,result.S("errore"),Tabelle.F(result?["utilization"]),Tabelle.F(result?["resistance_moment_knm"]),Tabelle.F(result?["resistance_elastic_knm"]),Tabelle.F(result?["stato"]?["sigma_acciaio"]),Tabelle.F(result?["stato"]?["sigma_cls"])]);
            foreach(var (limit,grid) in comboGrids)foreach(DataGridViewRow row in grid.Rows)
                if(name==limit+" · "+row.Cells[0].Value){row.Cells[4].Value=result.S("errore")!=""?"Errore":Tabelle.F(limit=="SLE"?result?["stato"]?["sigma_acciaio"]:result?["utilization"]);if(limit=="SLE")row.Cells[5].Value=Tabelle.F(result?["stato"]?["sigma_cls"]);}
        }
        tables=[new("Combinazioni — risultati",["Combinazione","Errore","R [-]","MRd plastico [kNm]","MRd elastico [kNm]","σs [MPa]","σc [MPa]"],rows)];PopulateTables();if(resultSelect.Items.Count>0)resultSelect.SelectedIndex=0;
    }
    private void ShowSectionState()
    {
        if(Result is null||resultSelect.SelectedItem is not string key)return;var r=Result["risultati"]?[key];sectionDrawing.Plane=null;
        if(r?["direction_rad"] is not null)
        {
            var engine=new SezioneCA(Data["input"]!.AsObject(),12,36);double angle=r.D("direction_rad");double? depth=J.Number(r["neutral_axis_depth_mm"]);
            sectionDrawing.Plane=depth is null?[1,0,0]:[-(engine.Supports(angle).Max-depth.Value),Math.Cos(angle),Math.Sin(angle)];
        }
        else if(r?["stato"]?["piano"] is JsonArray plane){double length=r["stato"].D("lunghezza_mm");sectionDrawing.Plane=[plane[0]!.GetValue<double>(),plane[1]!.GetValue<double>()/length,plane[2]!.GetValue<double>()/length];}
        sectionDrawing.Invalidate();
    }
    private async Task CalculateDomainAsync()
    {
        if(Busy)return;Commit();var data=(JsonObject)Data.DeepClone();string type=domainType.Text,mode=domainMode.Text;double? axial=J.Number(JsonValue.Create(domainN.Text));if(axial is null){status.Text="N del dominio: inserire un numero finito.";return;}
        Busy=true;originalCanvas.Enabled=false;calculate.Enabled=false;status.Text="Calcolo del dominio in corso…";
        try
        {
            var points=await Task.Run(()=>{var engine=new SezioneCA(data["input"]!.AsObject());return type=="N–Mx"?Domini.NM(engine,mode,engine.NAutomatico):Domini.MM(engine,mode,engine.NAutomatico,axial.Value);});
            if(points.Count>0)points.Add(points[0]);domain.Title=mode+" · "+type+(type=="Mx–My"?$" · N={axial} kN":"");domain.XLabel="Mx [kNm]";domain.YLabel=type=="N–Mx"?"N [kN]":"My [kNm]";domain.Series=[new("Dominio",points,Ui.Blue)];domain.ResetView();status.Text="Dominio calcolato · interpolazione tra profili senza estrapolazione";
        }
        catch(Exception ex){status.Text=ex.Message;}
        finally{Busy=false;originalCanvas.Enabled=true;calculate.Enabled=true;}
    }
    private void BuildProfile()
    {
        UpdatePileProfile();stratigraphy.Invalidate();
        var profile=GetAll(outputs).OfType<Plot>().FirstOrDefault(p=>p.Name=="profile");if(profile is null)return;var series=new List<Serie>();Color[] colors=[Ui.Blue,Color.SeaGreen,Color.DarkOrange,Color.Purple];int i=0;
        foreach(var rows in Data.Array("stratigrafie"))
        {
            double z=0;foreach(var row in rows!.AsArray()){double h=row.D("spessore");if(h<=0)continue;series.Add(new($"Sondaggio {i+1} · "+row.S(Micro?"terreno":"tipologia"),[[i*2,z],[i*2+1,z],[i*2+1,z+h],[i*2,z+h],[i*2,z]],colors[i%4]));z+=h;}i++;
        }
        if(Data["generali"].B("presenza_falda")&&!Micro){double z=Data["generali"].D("profondita_falda");series.Add(new("Falda",[[0,z],[Math.Max(1,i*2-1),z]],Color.DeepSkyBlue,true));}
        profile.Series=series;profile.Invalidate();
    }
    private void BuildReferencePlot(Plot target)
    {
        var series=new List<Serie>();Color[] colors=[Ui.Blue,Color.SeaGreen,Color.DarkOrange,Color.Purple,Color.Brown,Color.Teal,Color.Crimson,Color.Gray];int i=0;
        if(Micro)foreach(var (name,pts) in BustamanteDoix.Curve)series.Add(new(name,pts.Select(p=>new[]{p.X,1000*p.Y}).ToList(),colors[i++%colors.Length]));
        else
        {
            bool big=Data["generali"].D("diametro",1)>.8;var ratios=big?Nq.RapportiGrande:Nq.RapportiMedio;
            target.XMinimum=25;
            foreach(double ratio in ratios){double low=big?26:25,high=big?42:ratio==5?38.8:ratio==10?40:ratio==20?41:41.6;series.Add(new("z/D="+ratio,Enumerable.Range(0,161).Select(k=>{double phi=low+(high-low)*k/160;return new[]{phi,Nq.Dettaglio(phi,ratio,big).D("nq")};}).ToList(),colors[i++]));}
        }
        if(PileCapacity)AddActualPileNq(target,series,Data["generali"].D("diametro",1)>.8);
        target.Series=series;target.Invalidate();
    }
    private void SavePlot(){using var save=new SaveFileDialog{Filter="Immagine PNG|*.png",FileName="capacita.png"};if(save.ShowDialog(this)==DialogResult.OK)Archivio.ScriviAtomico(save.FileName,plot.Png());}
    public void ExportResult(string path){if(Result is null)throw new InvalidOperationException("Premere Calcola prima di esportare.");Archivio.ScriviAtomico(path,System.Text.Encoding.UTF8.GetBytes(Result.ToJsonString(J.Options)));}
    public void ExportReport(string path,string title,HashSet<string> options)
    {
        if(Result is null)throw new InvalidOperationException("Premere Calcola prima di esportare.");var images=new List<ImmagineReport>{new(plot.Title,plot.Png(),"grafico_capacita")};foreach(var p in GetAll(outputs).OfType<Plot>())if(p.Name is "nq" or "profile")images.Add(new(p.Title,p.Png(),p.Name=="nq"?"grafico_nq":"grafico_profilo"));ReportWord.Esporta(path,title,Module,Data,Result,options,images);
    }
}
