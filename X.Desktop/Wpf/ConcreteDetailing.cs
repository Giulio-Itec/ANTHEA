using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;
internal sealed partial class ConcreteWorkspace
{
    private readonly VerificationCards detailingText = new();
    private readonly VerificationCards anchorageText = new();
    private InputForm? detailingForm;
    private IReadOnlyList<DetailingCheck> detailingResults = [];
    private AnchorageResult? anchorageResult;
    private JsonObject DetailingOptions => settings["dettagli_costruttivi"]!.AsObject();
    private UIElement BuildDetailingPanel()
    {
        if(settings["dettagli_costruttivi"] is not JsonObject) settings["dettagli_costruttivi"]=new JsonObject();
        var o=DetailingOptions;
        foreach(var(k,v) in new[]{("elemento","Da scegliere"),("aggregato","20"),("cmin_dur",""),("delta_c","10"),("zona_sovrapposizione","No"),("as_secondaria","0"),("passo_secondaria","0"),("zona_critica","No"),("barre_trattenute","Da confermare"),("ancoraggio_appoggi","Da confermare")})if(!o.ContainsKey(k))o[k]=v;
        detailingForm=new(o,[new("elemento","Elemento",Choices:["Da scegliere","Trave","Pilastro","Soletta piena","Parete"]),new("aggregato","Inerte massimo dg","mm"),new("cmin_dur","cmin,dur da durabilità","mm"),new("delta_c","Tolleranza Δcdev","mm"),new("zona_sovrapposizione","Zona di sovrapposizione",Choices:["No","Sì"]),new("as_secondaria","As ortogonale totale / m","mm²/m"),new("passo_secondaria","Interasse barre ortogonali","mm"),new("zona_critica","Zona di massimo momento / carico concentrato",Choices:["No","Sì"]),new("barre_trattenute","Barre compresse trattenute",Choices:["Da confermare","Confermato"]),new("ancoraggio_appoggi","Traslazione e ancoraggio agli appoggi",Choices:["Da confermare","Confermato"])],_=>{RefreshDetailing();exportResult=null;Modified?.Invoke();},true,true);
        var results=new TabControl();Ui.Tab(results,"Controlli della sezione",Scroller(detailingText));Ui.Tab(results,"Ancoraggi e giunzioni",Scroller(anchorageText));
        if(settings["ancoraggi"] is not JsonObject) settings["ancoraggi"]=J.Obj(("schema_versione",1),("tipo","Ancoraggio rettilineo"),("diametro",""),("sigma",""),("aderenza","Buona"),("lunghezza",""),("percentuale","100"),("interferro","0"));
        var a=settings["ancoraggi"]!.AsObject();
        var af=new InputForm(a,[new("tipo","Verifica",Choices:["Ancoraggio rettilineo","Sovrapposizione rettilinea"]),new("diametro","Ø barra (vuoto: max sezione)","mm"),new("sigma","|σsd| (vuoto: fyd)","MPa"),new("aderenza","Condizioni di aderenza",Choices:["Buona","Altre condizioni"]),new("lunghezza","Lunghezza disponibile","mm"),new("percentuale","Barre sovrapposte","%"),new("interferro","Interferro fra barre giuntate","mm")],_=>{RefreshDetailing();exportResult=null;Modified?.Invoke();},true,true);
        return WorkspaceLayout(Panel("Dati dell’elemento",Scroller(Ui.Stack(detailingForm,Group("Staffe · dati comuni",BuildStirrups()),Group("Ancoraggi e giunzioni",af),Ui.Text("Soletta: b è la larghezza della striscia, h lo spessore. Parete: la dimensione minore è lo spessore. L’armatura ortogonale è il totale delle due facce per metro. Materiali, copriferro e staffe sono condivisi con le altre schede.",11)))),Panel("Dettagli costruttivi · NTC capitolo 4 / EC2",results));
    }
    private void RefreshDetailing()
    {
        if(detailingForm is null)return;
        var o=DetailingOptions;
        bool plate=o.S("elemento") is "Soletta piena" or "Parete";
        foreach(string k in new[]{"as_secondaria","passo_secondaria"})detailingForm.ShowField(k,plate);
        detailingForm.ShowField("zona_critica",o.S("elemento")=="Soletta piena");detailingForm.ShowField("ancoraggio_appoggi",o.S("elemento")=="Trave");
        detailingResults=[];anchorageResult=null;exportResult=null;
        try
        {
            if(settings.S("normativa")!="NTC 2018")throw new ArgumentException("I dettagli implementati sono riferiti a NTC 2018 con integrazioni EC2.");
            var kind=o.S("elemento") switch {"Trave"=>ConcreteMemberKind.Beam,"Pilastro"=>ConcreteMemberKind.Column,"Soletta piena"=>ConcreteMemberKind.Slab,"Parete"=>ConcreteMemberKind.Wall,_=>throw new ArgumentException("Scegliere il tipo di elemento.")};
            var s=new SezioneCA(Input);
            double n=actions["SLU"].Concat(actions["SLV"]).Select(r=>Math.Max(0,-SectionWorkspace.Number(r.Values.S("N"),"N"))).DefaultIfEmpty(0).Max();
            double V(string key)=>SectionWorkspace.Number(o.S(key),key);
            bool present=Input.S("staffe_presenti","Sì")=="Sì";
            var request=new ConcreteDetailingInput(kind,s,n,present,Input.D("transverse_bar_diameter_mm"),Input.D("transverse_spacing_mm"),(int)ShearOptions.D("rami_y",2),V("aggregato"),o.S("cmin_dur").Trim()==""?double.NaN:V("cmin_dur"),V("delta_c"),o.S("zona_sovrapposizione")=="Sì",plate?V("as_secondaria"):0,plate?V("passo_secondaria"):0,o.S("zona_critica")=="Sì",o.S("barre_trattenute")=="Confermato",o.S("ancoraggio_appoggi")=="Confermato");
            detailingResults=new ConcreteDetailingCalculator().Calculate(request);
            var b=new StringBuilder($"{o.S("elemento")} · NEd,max compressione = {n:0.###} kN\n");
            b.AppendLine($"{detailingResults.Count(r=>r.Passed==true)} soddisfatti · {detailingResults.Count(r=>r.Passed==false)} non soddisfatti · {detailingResults.Count(r=>r.Passed is null)} da completare\n");
            detailingText.Text=b.ToString();
            foreach(var r in detailingResults)detailingText.AddDetail(r.Name,r.Passed,$"Valore: {r.Actual?.ToString("0.###")??"—"} {r.Unit} · limite: {r.Limit?.ToString("0.###")??"—"} {r.Unit}",r.Explanation,r.Reference);
        }
        catch(Exception ex){detailingText.Start();detailingText.AddDetail("Controlli della sezione",null,"Dati da completare",ex.Message,"NTC capitolo 4 / EC2");}
        try
        {
            var s=new SezioneCA(Input);var a=settings["ancoraggi"]!;
            double Number(string key)=>SectionWorkspace.Number(a.S(key),key);
            double phi=a.S("diametro").Trim()==""?s.Bars.Max(v=>v.Diametro):Number("diametro"),sigma=a.S("sigma").Trim()==""?s.Fyd:Number("sigma");
            if(sigma>s.Fyd)throw new ArgumentException("La tensione di ancoraggio non può superare fyd.");
            var concrete=ConcreteMaterials.Concrete(Input);
            var result=new ConcreteAnchorageCalculator().Calculate(new(phi,sigma,Math.Abs(concrete.Fctk05),Input.D("gamma_c"),a.S("aderenza")=="Buona",Number("lunghezza"),a.S("tipo").StartsWith("Sovrapposizione"),Number("percentuale"),Number("interferro")));
            anchorageResult=result;
            anchorageText.Start();
            anchorageText.AddDetail(a.S("tipo")+$" · Ø{phi:0.###}",result.Passed,$"Disponibile: {a.S("lunghezza")} mm · richiesta: {result.RequiredLength:0.###} mm",$"σsd = {sigma:0.###} MPa · fbd = {result.Fbd:0.###} MPa · lb,rqd = {result.BasicLength:0.###} mm\n{result.Expression}\n{(result.Passed?"Lunghezza e interferro soddisfatti":"Lunghezza o interferro non soddisfatti")}","NTC §§4.1.2.1.1.4, 4.1.2.3.10, 4.1.6.1.4 / EC2 §§8.4 e 8.7.");
            anchorageText.AddDetail("Dettaglio esecutivo",null,"Controlli da completare","Confinamento, posizione delle giunzioni e cautele per Ø > 32 mm devono essere verificati nel dettaglio esecutivo.","EC2 §§8.4 e 8.7");
        }
        catch(Exception ex){anchorageText.Start();anchorageText.AddDetail("Ancoraggi e giunzioni",null,"Dati da completare",ex.Message,"NTC capitolo 4 / EC2 §§8.4 e 8.7");}
    }
}

