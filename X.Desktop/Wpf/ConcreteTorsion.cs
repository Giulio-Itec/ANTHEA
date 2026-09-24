using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using X.Core;
namespace X.Desktop;
internal sealed partial class ConcreteWorkspace
{
    private readonly Dictionary<string,TorsionResult> torsionResults=new();
    private InputForm torsionForm=null!;
    private UIElement BuildTorsionOptions()
    {
        var o=ShearOptions;
        foreach(var(k,v) in new[]{("cot_torsione","1"),("as_torsione","0"),("chiusura_torsione","Da confermare"),("modello_circolare","Da scegliere"),("z_d","0.75")})if(!o.ContainsKey(k))o[k]=v;
        torsionForm=new InputForm(o,[new("cot_torsione","cot θ comune (T ≠ 0)"),new("as_torsione","As longitudinale disponibile per torsione","mm²"),new("chiusura_torsione","Staffe chiuse e barre sul profilo",Choices:["Da confermare","Confermato"]),new("modello_circolare","Taglio circolare",Choices:["Da scegliere","Pile · NTC §7.9.5.2","Parametri assegnati"]),new("z_d","z/d assegnato · circolare")],_=>{InvalidateActions("Taglio");Modified?.Invoke();},true,true);
        return Ui.Stack(torsionForm,Ui.Text("Torsione: As è la quota disponibile dopo pressoflessione. Staffe chiuse ortogonali all’asse; area di un ramo per torsione. Per Vx+Vy si adotta la somma conservativa dei contributi sul calcestruzzo, non una formula normativa di interazione biassiale. Modello delle pile: z/d = 0,75 piena, 0,60 cava.",11));
    }
    private void CalculateTorsion(JsonRow row,SezioneCA geometry,Ntc2018Checks.ShearResult[] shear,double fcd)
    {
        double torque=SectionWorkspace.Number(row.Values.S("T","0"),"T");
        if(torque==0){row.Output("eta_t","—");row.Output("eta_vt","—");return;}
        if(Input.S("staffe_presenti","Sì")!="Sì"||ShearOptions.S("modello")!="Con staffe")throw new ArgumentException("Torsione: occorrono staffe resistenti chiuse.");
        if(ShearOptions.S("chiusura_torsione")!="Confermato")throw new ArgumentException("Confermare staffe chiuse e barre contenute nel profilo resistente, incluse quelle di spigolo.");
        if(ShearOptions.D("alpha_x")!=90||ShearOptions.D("alpha_y")!=90)throw new ArgumentException("Torsione: modello implementato con staffe a 90°.");
        if(Input.S("shape")=="Circolare"&&ShearOptions.S("tipo_staffa")=="Spirale")throw new ArgumentException("Torsione: selezionare staffa chiusa; spirale non equivalente automaticamente.");
        double al=ShearOptions.Required("as_torsione");if(al>geometry.AreaSteel)throw new ArgumentException("As disponibile per torsione supera l’armatura totale.");
        var g=ConcreteTorsionCalculator.Geometry(geometry);
        var result=new ConcreteTorsionCalculator().Calculate(new(torque,g,fcd,geometry.Fyd,Math.PI*Math.Pow(Input.D("transverse_bar_diameter_mm"),2)/4,Input.D("transverse_spacing_mm"),al,ShearOptions.Required("cot_torsione"),row.Values.D("Vx"),row.Values.D("Vy"),shear[0],shear[1]));
        torsionResults[row.Values.S("id")]=result;row.Output("eta_t",result.TorsionRatio?.ToString("0.00")??"∞");row.Output("eta_vt",result.ConcreteCombinedRatio is double ec&&result.SteelCombinedRatio is double es?Math.Max(ec,es).ToString("0.00"):"∞");
        row.Output("esito",row.Values.S("esito")+" · "+result.Status);
    }
    private string TorsionSummary(JsonRow row)
    {
        if(!torsionResults.TryGetValue(row.Values.S("id"),out var r))return "";
        return $"\nTORSIONE · NTC §4.1.2.3.6\nTEd = {row.Values.S("T")} kNm\nAk = {r.Geometry?.Area:0.###} mm² · uk = {r.Geometry?.Perimeter:0.###} mm · t = {r.Geometry?.Thickness:0.###} mm · cot θ = {r.CotTheta:0.###}\nTRcd = 2 Ak t (0,5 fcd) cot θ/(1+cot²θ)\nTRsd = 2 Ak (Asta/s) fyd cot θ · Asta = area di un ramo\nTRld = 2 Ak (As,l/uk) fyd / cot θ\nTRcd / TRsd / TRld = {r.TRcd:0.###} / {r.TRsd:0.###} / {r.TRld:0.###} kNm\nTRd = {r.TRd:0.###} kNm · ηT = {r.TorsionRatio?.ToString("0.###")??"non definito (resistenza nulla)"}\nAs,l richiesta per sola torsione = {r.RequiredLongitudinalArea:0.###} mm²\nη calcestruzzo = |T|/TRcd + |Vx|/VRcd,x + |Vy|/VRcd,y = {r.ConcreteCombinedRatio:0.###}\nη staffe = |T|/TRsd + max(|Vx|/VRsd,x; |Vy|/VRsd,y) = {r.SteelCombinedRatio:0.###}\n{r.Status}\nLa quota longitudinale deve essere aggiuntiva a quella impegnata in N–M.\n";
    }
}
