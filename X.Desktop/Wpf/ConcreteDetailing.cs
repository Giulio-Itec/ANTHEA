using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;
internal sealed partial class ConcreteWorkspace
{
    private readonly VerificationCards detailingText = new();
    private readonly VerificationCards anchorageText = new(2);
    private InputForm? detailingForm;
    private IReadOnlyList<DetailingCheck> detailingResults = [];
    private AnchorageResult? anchorageResult;
    private JsonObject DetailingOptions => settings["dettagli_costruttivi"]!.AsObject();
    private readonly Dictionary<string, VerificationCards> detailingTopics = new();
    private readonly List<InputForm> detailingForms = [];
    private InputForm coverDetailingForm = null!;
    private InputForm coverDetailingGeometry = null!;
    private InputForm anchorageForm = null!;
    private readonly TextBlock durabilityNote = Ui.Text("",11);
    private UIElement BuildDetailingPanel()
    {
        if(settings["dettagli_costruttivi"] is not JsonObject) settings["dettagli_costruttivi"]=new JsonObject();
        var o=DetailingOptions;
        foreach(var(k,v) in new[]{("vita_durabilita","50"),("qualita_copriferro","No"),("elemento","Da scegliere"),("aggregato","20"),("cmin_dur",""),("delta_c","10"),("zona_sovrapposizione","No"),("as_secondaria","0"),("passo_secondaria","0"),("zona_critica","No"),("barre_trattenute","Da confermare"),("ancoraggio_appoggi","Da confermare")})if(!o.ContainsKey(k))o[k]=v;
        InputForm Form(Field[] fields)
        {
            var form = new InputForm(o, fields, _ => { RefreshDetailing(); exportResult=null; Modified?.Invoke(); },true,true);
            detailingForms.Add(form); return form;
        }
        detailingForm = Form([new("elemento","Elemento",Choices:["Da scegliere","Trave","Pilastro","Soletta piena","Parete"])]);
        coverDetailingForm = Form([new("aggregato","Inerte massimo dg","mm"),new("esposizione_sle","Esposizione condivisa SLE",ReadOnly:true),new("vita_durabilita","Vita utile di progetto","anni",Choices:["50","100"]),new("qualita_copriferro","Controllo qualità dei copriferri",Choices:["No","Sì"]),new("cmin_dur","cmin,dur calcolato","mm",ReadOnly:true),new("delta_c","Tolleranza Δcdev","mm")]);
        var cover = new InputForm(Input,[new("cover_mm","Copriferro netto","mm")],_=>{geometry.Set("cover_mm",Input.S("cover_mm"));Invalidate();},true);
        coverDetailingGeometry = cover;
        var bars = Form([new("zona_sovrapposizione","Sezione dentro una giunzione",Choices:["No","Sì"]),new("as_secondaria","As ortogonale totale / m","mm²/m"),new("passo_secondaria","Interasse barre ortogonali","mm"),new("zona_critica","Zona di massimo momento / carico concentrato",Choices:["No","Sì"])]);
        var stirrups = Form([new("barre_trattenute","Barre compresse trattenute",Choices:["Da confermare","Confermato"])]);
        var supports = Form([new("ancoraggio_appoggi","Traslazione e ancoraggio agli appoggi",Choices:["Da confermare","Confermato"])]);
        if(settings["ancoraggi"] is not JsonObject)settings["ancoraggi"]=J.Obj(("schema_versione",1),("tipo","Ancoraggio rettilineo"),("diametro",""),("sigma",""),("aderenza","Buona"),("lunghezza",""),("percentuale","100"),("interferro","0"));
        var anchorOptions=settings["ancoraggi"]!.AsObject();
        foreach(string key in new[]{"confinamento","posizione","cautele"}) if(!anchorOptions.ContainsKey(key))anchorOptions[key]="Da verificare";
        var af = anchorageForm = new InputForm(anchorOptions,[new("tipo","Verifica",Choices:["Ancoraggio rettilineo","Sovrapposizione rettilinea"]),new("diametro","Ø barra (vuoto: max sezione)","mm"),new("sigma","|σsd| (vuoto: fyd)","MPa"),new("aderenza","Condizioni di aderenza",Choices:["Buona","Altre condizioni"]),new("lunghezza","Lunghezza disponibile","mm"),new("percentuale","Barre sovrapposte","%"),new("interferro","Interferro fra barre giuntate","mm"),new("confinamento","Confinamento / armatura trasversale",Choices:["Da verificare","Verificato sul disegno","Non conforme"]),new("posizione","Posizione e sfalsamento giunzioni",Choices:["Da verificare","Verificato sul disegno","Non conforme"]),new("cautele","Cautele per barre Ø > 32 mm",Choices:["Da verificare","Verificato sul disegno","Non conforme"])],_=>{RefreshDetailing();exportResult=null;Modified?.Invoke();},true,true);
        var rows = new StackPanel();
        rows.Children.Add(WorkspaceLayout(Panel("Tipo di elemento",detailingForm),Panel("Riepilogo dei dettagli",detailingText)));
        void Topic(string key,string title,UIElement inputs,UIElement? extra=null)
        {
            var checks = new VerificationCards(2); detailingTopics[key]=checks;
            rows.Children.Add(WorkspaceLayout(Panel(title,inputs),Panel("Verifiche · "+title,extra is null?checks:Ui.Stack(checks,extra))));
        }
        Topic("cover","Interferro / copriferro",Ui.Stack(cover,coverDetailingForm,durabilityNote));
        Topic("bars","Armatura",Ui.Stack(bars,Ui.Button("Modifica armature nel pannello di controllo →",()=>tabs.SelectedIndex=0),Ui.Text("La zona riguarda i limiti di armatura: includere anche le barre sovrapposte. Per la lunghezza scegliere sotto Sovrapposizione rettilinea. As ortogonale: totale delle due facce per metro.",11)));
        Topic("stirrups","Staffe",Ui.Stack(BuildStirrups(),stirrups));
        Topic("anchors","Ancoraggi / appoggi",Ui.Stack(supports,af,Ui.Button("Aggiorna verifiche ancoraggi",()=>{af.Commit();RefreshDetailing();}),Ui.Text("Riscontri sul disegno: le conferme registrano una verifica manuale della disposizione longitudinale.",11)),anchorageText);
        return Scroller(rows);
    }
    private static string DetailingTopic(string name)
    {
        if(name.Contains("staffe",StringComparison.OrdinalIgnoreCase)||name.Contains("Trattenimento"))return "stirrups";
        if(name.Contains("copriferro",StringComparison.OrdinalIgnoreCase)||name.StartsWith("Interferro"))return "cover";
        if(name.Contains("Ancoraggio")||name.StartsWith("Bordi"))return "anchors";
        return "bars";
    }
    private void RefreshDetailing()
    {
        if(detailingForm is null)return;
        coverDetailingGeometry.Set("cover_mm",Input.S("cover_mm"),true);
        var o=DetailingOptions;
        bool lap=settings["ancoraggi"].S("tipo").StartsWith("Sovrapposizione");
        foreach(string key in new[]{"percentuale","interferro","posizione"})anchorageForm.ShowField(key,lap);
        bool plate=o.S("elemento") is "Soletta piena" or "Parete";
        foreach(var form in detailingForms) foreach(string k in new[]{"as_secondaria","passo_secondaria"}) if(form.Editors.ContainsKey(k))form.ShowField(k,plate);
        foreach(var form in detailingForms){if(form.Editors.ContainsKey("zona_critica"))form.ShowField("zona_critica",o.S("elemento")=="Soletta piena");if(form.Editors.ContainsKey("ancoraggio_appoggi"))form.ShowField("ancoraggio_appoggi",o.S("elemento")=="Trave");}
        detailingResults=[];anchorageResult=null;exportResult=null; foreach(var cards in detailingTopics.Values)cards.Start();
        try
        {
            if(settings.S("normativa")!="NTC 2018")throw new ArgumentException("I dettagli implementati sono riferiti a NTC 2018 con integrazioni EC2.");
            var kind=o.S("elemento") switch {"Trave"=>ConcreteMemberKind.Beam,"Pilastro"=>ConcreteMemberKind.Column,"Soletta piena"=>ConcreteMemberKind.Slab,"Parete"=>ConcreteMemberKind.Wall,_=>throw new ArgumentException("Scegliere il tipo di elemento.")};
            var s=new SezioneCA(Input);
            try
            {
            string exposure=settings["sle_comuni"].S("esposizione");
            coverDetailingForm.Set("esposizione_sle",exposure,true);
            var exposures=new[]{Materiali.Durability.Exposures.FirstOrDefault(e=>e.Code==exposure) ?? throw new ArgumentException("Scegliere la classe di esposizione nella scheda Tensioni e fessurazione (SLE).")};
            var cp=new Materiali.CoverInput(int.Parse(o.S("vita_durabilita")),false,false,false,s.Bars.Max(b=>b.Diametro),SectionWorkspace.Number(o.S("aggregato"),"dg"),SectionWorkspace.Number(o.S("delta_c"),"Δcdev"),false,0,0);
            var dur=Materiali.NtcCover.Calculate(exposures,Input.D("fck_mpa"),cp,plate,o.S("qualita_copriferro")=="Sì",Materiali.MinimumConcrete.Required(exposures));
            o["cmin_dur"]=dur.Cover.Durability;
            coverDetailingForm.Set("cmin_dur",dur.Cover.Durability.ToString("0.###"),true);
            durabilityNote.Text=$"Circolare 2019, tab. C4.1.IV · {exposure}, {dur.Environment.ToLowerInvariant()}\nTabella {dur.TableCover:0.#} + vita {dur.LifeExtra:0.#} + classe bassa {dur.LowStrengthExtra:0.#} − qualità {dur.QualityReduction:0.#} = {dur.Cover.Durability:0.#} mm. Δcdev aggiunto nel copriferro nominale. Stesso motore del modulo Materiali.";
            }
            catch(Exception ex){o["cmin_dur"]="";coverDetailingForm.Set("cmin_dur","",true);durabilityNote.Text=ex.Message;}
            double n=actions["SLU"].Concat(actions["SLV"]).Select(r=>Math.Max(0,-SectionWorkspace.Number(r.Values.S("N"),"N"))).DefaultIfEmpty(0).Max();
            double V(string key)=>SectionWorkspace.Number(o.S(key),key);
            bool present=Input.S("staffe_presenti","Sì")=="Sì";
            var request=new ConcreteDetailingInput(kind,s,n,present,Input.D("transverse_bar_diameter_mm"),Input.D("transverse_spacing_mm"),(int)ShearOptions.D("rami_y",2),V("aggregato"),o.S("cmin_dur").Trim()==""?double.NaN:V("cmin_dur"),V("delta_c"),o.S("zona_sovrapposizione")=="Sì",plate?V("as_secondaria"):0,plate?V("passo_secondaria"):0,o.S("zona_critica")=="Sì",o.S("barre_trattenute")=="Confermato",o.S("ancoraggio_appoggi")=="Confermato");
            detailingResults=new ConcreteDetailingCalculator().Calculate(request);
            var b=new StringBuilder($"{o.S("elemento")} · NEd,max compressione = {n:0.###} kN\n");
            b.AppendLine($"{detailingResults.Count(r=>r.Passed==true)} soddisfatti · {detailingResults.Count(r=>r.Passed==false)} non soddisfatti · {detailingResults.Count(r=>r.Passed is null)} da completare\n");
            detailingText.Text=b.ToString();
            foreach(var r in detailingResults)detailingTopics[DetailingTopic(r.Name)].AddDetail(r.Name,r.Passed,$"Valore: {r.Actual?.ToString("0.###")??"—"} {r.Unit} · limite: {r.Limit?.ToString("0.###")??"—"} {r.Unit}",r.Explanation,r.Reference);
        }
        catch(Exception ex){o["cmin_dur"]="";coverDetailingForm.Set("cmin_dur","",true);durabilityNote.Text=ex.Message;detailingText.Start();detailingText.AddDetail("Controlli della sezione",null,"Dati da completare",ex.Message,"NTC capitolo 4 / EC2");}
        foreach(var topic in detailingTopics)if(topic.Key!="anchors"&&topic.Value.Children.Count==0)topic.Value.Text=detailingResults.Count==0?"In attesa dei dati dell’elemento.":"Nessuna verifica applicabile con i dati correnti.";
        try
        {
            var s=new SezioneCA(Input);var a=settings["ancoraggi"]!;
            double Number(string key)=>SectionWorkspace.Number(a.S(key),key);
            if(settings.S("normativa")!="NTC 2018")throw new ArgumentException("Ancoraggi disponibili per NTC 2018 con integrazioni EC2.");
            double phi=a.S("diametro").Trim()==""?s.Bars.Max(v=>v.Diametro):Number("diametro"),sigma=a.S("sigma").Trim()==""?s.Fyd:Number("sigma");
            if(sigma>s.Fyd)throw new ArgumentException("La tensione di ancoraggio non può superare fyd.");
            anchorageForm.ShowField("cautele",phi>32);
            if(a.S("lunghezza").Trim()=="")throw new ArgumentException("Inserire la lunghezza disponibile per eseguire la verifica.");
            var bondInput=(JsonObject)Input.DeepClone();bondInput["fck_mpa"]=Math.Min(Input.D("fck_mpa"),60);
            var concrete=ConcreteMaterials.Concrete(bondInput);
            var result=new ConcreteAnchorageCalculator().Calculate(new(phi,sigma,Math.Abs(concrete.Fctk05),Input.D("gamma_c"),a.S("aderenza")=="Buona",Number("lunghezza"),lap,lap?Number("percentuale"):100,lap?Number("interferro"):0));
            anchorageResult=result;
            anchorageText.Start();
            anchorageText.AddDetail(a.S("tipo")+$" · Ø{phi:0.###}",Number("lunghezza")>=result.RequiredLength,$"Disponibile: {a.S("lunghezza")} mm · richiesta: {result.RequiredLength:0.###} mm",$"σsd = {sigma:0.###} MPa · fbd = {result.Fbd:0.###} MPa · lb,rqd = {result.BasicLength:0.###} mm\n{result.Expression}\nη1 = {(a.S("aderenza")=="Buona"?1:.7):0.##}; η2 = {(phi<=32?1:(132-phi)/100):0.##}; fctk,0.05 = {Math.Abs(concrete.Fctk05):0.###} MPa (limite C60/75); γc = {Input.D("gamma_c"):0.###}. α1…α5 = 1; nessuna riduzione favorevole.","NTC §§4.1.2.1.1.4, 4.1.2.3.10, 4.1.6.1.4 / EC2 §§8.4 e 8.7.");
            if(lap)anchorageText.AddDetail("Interferro della giunzione",Number("interferro")<=4*phi,$"Interferro: {Number("interferro"):0.###} mm · massimo: {4*phi:0.###} mm",$"α6 = {Math.Clamp(Math.Sqrt(Number("percentuale")/25),1,1.5):0.###}; percentuale sovrapposta = {Number("percentuale"):0.###}%.","NTC §4.1.6.1.4 / EC2 §8.7");
            foreach(var (key,title) in new[]{("confinamento","Confinamento / armatura trasversale"),("posizione","Posizione e sfalsamento giunzioni"),("cautele","Cautele Ø > 32 mm")})
            {
                if(key=="posizione"&&!lap || key=="cautele"&&phi<=32)continue;
                string state=a.S(key);
                anchorageText.AddDetail("Riscontro manuale · "+title,state=="Verificato sul disegno"?true:state=="Non conforme"?false:null,state,"Esito dichiarato dal progettista tramite il campo a sinistra. Non incluso nell’esito numerico della lunghezza.","NTC §4.1.6.1.4 / EC2 §§8.4, 8.7 e 8.8");
            }
        }
        catch(Exception ex){anchorageText.Start();anchorageText.AddDetail("Ancoraggi e giunzioni",null,"Dati da completare",ex.Message,"NTC capitolo 4 / EC2 §§8.4 e 8.7");}
    }
}
