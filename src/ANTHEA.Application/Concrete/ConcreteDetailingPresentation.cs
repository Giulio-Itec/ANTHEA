using System.Text;
using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Application.Presentation;

namespace Anthea.Application.Concrete;

public sealed record ConcreteDetailingPresentation(IReadOnlyList<ConcreteResults.DetailingCheck> Checks, ConcreteResults.AnchorageResult? Anchorage,
    VerificationDetails Summary, IReadOnlyDictionary<string, VerificationDetails> Topics,
    VerificationDetails AnchorageDetails, JsonNode? DurabilityCover, string? Exposure, string DurabilityNote,
    bool? LargeBarPrecautions)
{
    public string DetailsText => Summary.Text + string.Join("\n", Topics.Values.Select(t => t.Text));
    public void ApplyDerivedValues(JsonObject workspace)
    {
        var options = workspace["dettagli_costruttivi"]!.AsObject();
        options["cmin_dur"] = DurabilityCover?.DeepClone();
        if (Exposure is not null) options["esposizione_sle"] = Exposure;
    }
    public static string Topic(string name)
    {
        if(name.Contains("staffe",StringComparison.OrdinalIgnoreCase)||name.Contains("Trattenimento"))return "stirrups";
        if(name.Contains("copriferro",StringComparison.OrdinalIgnoreCase)||name.StartsWith("Interferro"))return "cover";
        if(name.Contains("Ancoraggio")||name.StartsWith("Bordi"))return "anchors";
        return "bars";
    }
    public static ConcreteDetailingPresentation Calculate(JsonObject sourceInput, JsonObject sourceSettings, IEnumerable<JsonObject> sourceActions)
    {
        var Input = (JsonObject)sourceInput.DeepClone();
        var settings = (JsonObject)sourceSettings.DeepClone();
        var actions = sourceActions.Select(a => (JsonObject)a.DeepClone()).ToArray();
        var o = settings["dettagli_costruttivi"]!.AsObject();
        bool lap = settings["ancoraggi"].S("tipo").StartsWith("Sovrapposizione");
        IReadOnlyList<DetailingCheck> detailingResults;
        AnchorageResult? anchorageResult;
        var detailingText = new VerificationDetails();
        var anchorageText = new VerificationDetails();
        var detailingTopics = new[] { "cover", "bars", "stirrups", "anchors" }.ToDictionary(k => k, _ => new VerificationDetails());
        string durabilityNote = ""; string? exposureValue = null; bool? largeBars = null;
        detailingResults=[];anchorageResult=null;foreach(var cards in detailingTopics.Values)cards.Start();
        try
        {
            var evaluation=ConcreteDetailingAnalysis.Calculate(Input,settings,actions);
            string exposure=settings["sle_comuni"].S("esposizione");
            exposureValue = exposure;
            if(evaluation.Durability is { } dur)
            {
                o["cmin_dur"]=dur.Cover.Durability;

                durabilityNote=$"Circolare 2019, tab. C4.1.IV · {exposure}, {dur.Environment.ToLowerInvariant()}\nTabella {dur.TableCover:0.#} + vita {dur.LifeExtra:0.#} + classe bassa {dur.LowStrengthExtra:0.#} − qualità {dur.QualityReduction:0.#} = {dur.Cover.Durability:0.#} mm. Δcdev aggiunto nel copriferro nominale. Stesso motore del modulo Materiali.";
            }
            else {o["cmin_dur"]="";durabilityNote=evaluation.DurabilityError ?? "";}
            double n=evaluation.MaximumCompression;
            detailingResults=evaluation.Checks;
            var b=new StringBuilder($"{o.S("elemento")} · NEd,max compressione = {n:0.###} kN\n");
            b.AppendLine($"{detailingResults.Count(r=>r.Passed==true)} soddisfatti · {detailingResults.Count(r=>r.Passed==false)} non soddisfatti · {detailingResults.Count(r=>r.Passed is null)} da completare\n");
            detailingText.Text=b.ToString();
            foreach(var r in detailingResults)detailingTopics[Topic(r.Name)].AddDetail(r.Name,r.Passed,$"Valore: {r.Actual?.ToString("0.###")??"—"} {r.Unit} · limite: {r.Limit?.ToString("0.###")??"—"} {r.Unit}",r.Explanation,r.Reference);
        }
        catch(Exception ex){o["cmin_dur"]="";durabilityNote=ex.Message;detailingText.Start();detailingText.AddDetail("Controlli della sezione",null,"Dati da completare",ex.Message,"NTC capitolo 4 / EC2");}
        foreach(var topic in detailingTopics)if(topic.Key!="anchors"&&topic.Value.Items.Count==0)topic.Value.Text=detailingResults.Count==0?"In attesa dei dati dell’elemento.":"Nessuna verifica applicabile con i dati correnti.";
        try
        {
            var a=settings["ancoraggi"]!;
            double Number(string key)=>SectionWorkspace.Number(a.S(key),key);
            var evaluation=ConcreteDetailingAnalysis.Anchorage(Input,settings);
            double phi=evaluation.Diameter,sigma=evaluation.Stress;
            largeBars = phi > 32;
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
        return new(detailingResults.Select(value => new ConcreteResults.DetailingCheck(value)).ToArray(),
            anchorageResult is null ? null : new ConcreteResults.AnchorageResult(anchorageResult), detailingText, detailingTopics, anchorageText,
            o["cmin_dur"]?.DeepClone(), exposureValue, durabilityNote, largeBars);
    }
}
