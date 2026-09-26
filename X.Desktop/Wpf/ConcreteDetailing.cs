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
        var o=DetailingOptions;
        InputForm Form(Field[] fields)
        {
            var form = new InputForm(o, fields, _ => { RefreshDetailing(); exportResult=null; Modified?.Invoke(); },true,true);
            detailingForms.Add(form); return form;
        }
        detailingForm = Form([new("elemento","Elemento",Choices:["Da scegliere","Trave","Pilastro","Soletta piena","Parete"])]);
        coverDetailingForm = Form([new("aggregato","Inerte massimo dg","mm"),new("esposizione_sle","Esposizione condivisa SLE",ReadOnly:true),new("vita_durabilita","Vita utile di progetto","anni",Choices:["50","100"]),new("qualita_copriferro","Controllo qualità dei copriferri",Choices:["No","Sì"]),new("cmin_dur","cmin,dur calcolato","mm",ReadOnly:true),new("delta_c","Tolleranza Δcdev","mm")]);
        var cover = new InputForm(Input,[new("cover_mm","Copriferro netto adottato","mm",ReadOnly:true)],_=>{},true);
        coverDetailingGeometry = cover;
        var bars = Form([new("zona_sovrapposizione","Sezione dentro una giunzione",Choices:["No","Sì"]),new("as_secondaria","As ortogonale totale / m","mm²/m"),new("passo_secondaria","Interasse barre ortogonali","mm"),new("zona_critica","Zona di massimo momento / carico concentrato",Choices:["No","Sì"])]);
        var stirrups = Form([new("barre_trattenute","Barre compresse trattenute",Choices:["Da confermare","Confermato"])]);
        var supports = Form([new("ancoraggio_appoggi","Traslazione e ancoraggio agli appoggi",Choices:["Da confermare","Confermato"])]);
        var anchorOptions=settings["ancoraggi"]!.AsObject();
        var af = anchorageForm = new InputForm(anchorOptions,[new("tipo","Verifica",Choices:["Ancoraggio rettilineo","Sovrapposizione rettilinea"]),new("diametro","Ø barra (vuoto: max sezione)","mm"),new("sigma","|σsd| (vuoto: fyd)","MPa"),new("aderenza","Condizioni di aderenza",Choices:["Buona","Altre condizioni"]),new("lunghezza","Lunghezza disponibile","mm"),new("percentuale","Barre sovrapposte","%"),new("interferro","Interferro fra barre giuntate","mm"),new("confinamento","Confinamento / armatura trasversale",Choices:["Da verificare","Verificato sul disegno","Non conforme"]),new("posizione","Posizione e sfalsamento giunzioni",Choices:["Da verificare","Verificato sul disegno","Non conforme"]),new("cautele","Cautele per barre Ø > 32 mm",Choices:["Da verificare","Verificato sul disegno","Non conforme"])],_=>{RefreshDetailing();exportResult=null;Modified?.Invoke();},true,true);
        var rows = new StackPanel();
        rows.Children.Add(WorkspaceLayout(Panel("Tipo di elemento",detailingForm),Panel("Riepilogo dei dettagli",detailingText)));
        void Topic(string key,string title,UIElement inputs,UIElement? extra=null)
        {
            var checks = new VerificationCards(2); detailingTopics[key]=checks;
            rows.Children.Add(WorkspaceLayout(Panel(title,inputs),Panel("Verifiche · "+title,extra is null?checks:Ui.Stack(checks,extra))));
        }
        Topic("cover","Interferro / copriferro",Ui.Stack(cover,Ui.Button("Modifica geometria nel pannello di controllo →",()=>tabs.SelectedIndex=0),coverDetailingForm,durabilityNote));
        Topic("bars","Armatura",Ui.Stack(bars,Ui.Button("Modifica armature nel pannello di controllo →",()=>tabs.SelectedIndex=0),Ui.Text("La zona riguarda i limiti di armatura: includere anche le barre sovrapposte. Per la lunghezza scegliere sotto Sovrapposizione rettilinea. As ortogonale: totale delle due facce per metro.",11)));
        Topic("stirrups","Staffe",Ui.Stack(BuildStirrups(readOnly:true),stirrups));
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
            var evaluation=ConcreteDetailingAnalysis.Calculate(Input,settings,actions["SLU"].Concat(actions["SLV"]).Select(r=>r.Values));
            string exposure=settings["sle_comuni"].S("esposizione");
            coverDetailingForm.Set("esposizione_sle",exposure,true);
            if(evaluation.Durability is { } dur)
            {
                o["cmin_dur"]=dur.Cover.Durability;
                coverDetailingForm.Set("cmin_dur",dur.Cover.Durability.ToString("0.###"),true);
                durabilityNote.Text=$"Circolare 2019, tab. C4.1.IV · {exposure}, {dur.Environment.ToLowerInvariant()}\nTabella {dur.TableCover:0.#} + vita {dur.LifeExtra:0.#} + classe bassa {dur.LowStrengthExtra:0.#} − qualità {dur.QualityReduction:0.#} = {dur.Cover.Durability:0.#} mm. Δcdev aggiunto nel copriferro nominale. Stesso motore del modulo Materiali.";
            }
            else {o["cmin_dur"]="";coverDetailingForm.Set("cmin_dur","",true);durabilityNote.Text=evaluation.DurabilityError;}
            double n=evaluation.MaximumCompression;
            detailingResults=evaluation.Checks;
            var b=new StringBuilder($"{o.S("elemento")} · NEd,max compressione = {n:0.###} kN\n");
            b.AppendLine($"{detailingResults.Count(r=>r.Passed==true)} soddisfatti · {detailingResults.Count(r=>r.Passed==false)} non soddisfatti · {detailingResults.Count(r=>r.Passed is null)} da completare\n");
            detailingText.Text=b.ToString();
            foreach(var r in detailingResults)detailingTopics[DetailingTopic(r.Name)].AddDetail(r.Name,r.Passed,$"Valore: {r.Actual?.ToString("0.###")??"—"} {r.Unit} · limite: {r.Limit?.ToString("0.###")??"—"} {r.Unit}",r.Explanation,r.Reference);
        }
        catch(Exception ex){o["cmin_dur"]="";coverDetailingForm.Set("cmin_dur","",true);durabilityNote.Text=ex.Message;detailingText.Start();detailingText.AddDetail("Controlli della sezione",null,"Dati da completare",ex.Message,"NTC capitolo 4 / EC2");}
        foreach(var topic in detailingTopics)if(topic.Key!="anchors"&&topic.Value.Children.Count==0)topic.Value.Text=detailingResults.Count==0?"In attesa dei dati dell’elemento.":"Nessuna verifica applicabile con i dati correnti.";
        try
        {
            var a=settings["ancoraggi"]!;
            double Number(string key)=>SectionWorkspace.Number(a.S(key),key);
            var evaluation=ConcreteDetailingAnalysis.Anchorage(Input,settings);
            double phi=evaluation.Diameter,sigma=evaluation.Stress;
            anchorageForm.ShowField("cautele",phi>32);
            var result=evaluation.Check;
            anchorageResult=result;
            anchorageText.Start();
            anchorageText.AddDetail(a.S("tipo")+$" · Ø{phi:0.###}",result.LengthPassed,$"Disponibile: {a.S("lunghezza")} mm · richiesta: {result.RequiredLength:0.###} mm",$"σsd = {sigma:0.###} MPa · fbd = {result.Fbd:0.###} MPa · lb,rqd = {result.BasicLength:0.###} mm\n{result.Expression}\nη1 = {result.Eta1:0.##}; η2 = {result.Eta2:0.##}; fctk,0.05 = {evaluation.Fctk05:0.###} MPa (limite C60/75); γc = {Input.D("gamma_c"):0.###}. α1…α5 = 1; nessuna riduzione favorevole.","NTC §§4.1.2.1.1.4, 4.1.2.3.10, 4.1.6.1.4 / EC2 §§8.4 e 8.7.");
            if(lap)anchorageText.AddDetail("Interferro della giunzione",result.LapClearDistancePassed,$"Interferro: {Number("interferro"):0.###} mm · massimo: {result.MaximumLapClearDistance:0.###} mm",$"α6 = {result.Alpha6:0.###}; percentuale sovrapposta = {Number("percentuale"):0.###}%.","NTC §4.1.6.1.4 / EC2 §8.7");
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
