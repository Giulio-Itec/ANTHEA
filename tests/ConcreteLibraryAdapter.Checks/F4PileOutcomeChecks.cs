using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Calculations;

internal static class F4PileOutcomeChecks
{
    internal static int Run(string root,string reference,string output)
    {
        CultureInfo.CurrentCulture=CultureInfo.CurrentUICulture=CultureInfo.GetCultureInfo("it-IT");
        int models=0,segments=0,checks=0,numbers=0,classified=0;var errors=new List<string>();
        var hashes=JsonNode.Parse(File.ReadAllText(Path.Combine(root,"tests/fixtures/f4/reference.json")))!["files"]!.AsArray()
            .ToDictionary(v=>v!["file"]!.GetValue<string>(),v=>v!["sha256"]!.GetValue<string>());
        JsonObject Read(string relative)
        {
            byte[] bytes=File.ReadAllBytes(Path.Combine(reference,relative));
            if(!Convert.ToHexString(SHA256.HashData(bytes)).Equals(hashes[relative],StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Frozen reference changed: "+relative);
            return JsonNode.Parse(bytes)!.AsObject();
        }
        foreach(string file in Directory.GetFiles(Path.Combine(reference,"inputs/piles"),"*.json").Order(StringComparer.Ordinal))
        {
            string id=Path.GetFileName(file);if(!id.StartsWith("law-",StringComparison.Ordinal)&&!id.StartsWith("segments-",StringComparison.Ordinal))continue;
            var input=Read("inputs/piles/"+id);var expected=Read("results/piles/"+id)["value"]!["result"]!;string before=input.ToJsonString();
            var result=ElasticHorizontalPile.CompleteReinforcement(input,ElasticHorizontalPile.CalculateResponse(input),maximumParallelism:1);models++;
            var old=expected["armature"]!["tratti"]!.AsArray();var current=result["armature"]!["tratti"]!.AsArray();
            if(old.Count!=current.Count){errors.Add(id+": segment count");continue;}
            for(int i=0;i<old.Count;i++)
            {
                segments++;var a=Po5(old[i]!,ref classified);var b=current[i]!.AsObject();
                foreach(string field in new[]{"stato","da_completare","riepilogo"})
                    if(!JsonNode.DeepEquals(a[field],b[field]))errors.Add(id+"/"+i+"/"+field);
                if(!JsonNode.DeepEquals(LegacyElasticHorizontalPile.ReinforcementSummary(b),b["riepilogo"]))errors.Add(id+": legacy summary differs");
                var originalChecks=a["verifiche"]!.AsArray();var actualChecks=b["verifiche"]!.AsArray();
                if(originalChecks.Count!=actualChecks.Count){errors.Add(id+": section check count");continue;}
                for(int j=0;j<originalChecks.Count;j++)
                {
                    checks++;var x=originalChecks[j]!;var y=actualChecks[j]!;
                    if(x.S("Status")!=y.S("Status"))errors.Add(id+": status differs");
                    foreach(string key in new[]{"MRdPositive","MRdNegative","BendingRatio","ShearRatio","VRd","GoverningRatio"})
                    {
                        numbers++;double? first=J.Number(x[key]),second=J.Number(y[key]);
                        if(first.HasValue!=second.HasValue||first.HasValue&&Math.Abs(first.Value-second!.Value)>1e-9+1e-9*Math.Max(Math.Abs(first.Value),Math.Abs(second.Value)))errors.Add(id+": numeric "+key);
                    }
                }
                var translated=(JsonObject)b.DeepClone();
                foreach(var issue in translated["da_completare_tipizzati"]!.AsArray())issue!["Text"]="Translated or arbitrary display label";
                translated["da_completare"]=new JsonArray("Soddisfatto");
                foreach(var check in translated["verifiche"]!.AsArray())check!["Status"]="Soddisfatto";
                if(!JsonNode.DeepEquals(ElasticHorizontalPile.ReinforcementSummary(translated),b["riepilogo"]))errors.Add(id+": display strings control verdict");
            }
            if(input.ToJsonString()!=before)errors.Add(id+": caller mutated");
        }
        var unknown=J.Obj(("verifiche",new JsonArray(J.Obj(("Status","Soddisfatto")))),("da_completare",new JsonArray("Non eseguiti: SLE")));
        if(ElasticHorizontalPile.ReinforcementSummary(unknown).B("eseguiti_soddisfatti"))errors.Add("Legacy labels certified an untyped result");
        if(models!=20||segments==0||checks==0)errors.Add("Incomplete pile corpus");
        if(classified==0)errors.Add("PO-5: no frozen development failure reclassified (classification not exercised)");
        Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"comparison.json"),JsonSerializer.Serialize(new{models,segments,checks,numbers,classified=new{PO5=classified},errors},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"F4 pile outcomes: {models} models, {segments} segments, {checks} checks, {numbers} numbers, {errors.Count} differences; PO-5 classified: {classified}");
        foreach(string error in errors.Take(15))Console.Error.WriteLine(error);return errors.Count==0?0:1;
    }

    // Differenza intenzionale classificata PO-5 (analisi dei bug del 15/10/2026), applicata solo al riferimento congelato: lo sviluppo delle
    // barre insufficiente ad alcune quote (esito NotEvaluated della libreria) è un punto da completare e non un «NON SODDISFATTO». Nel tratto
    // congelato il testo storico diventa ElasticHorizontalPile.DevelopmentPendingText in da_completare; nel riepilogo passa da non_soddisfatti
    // a da_completare nella posizione dei punti da completare, e stato, colore ed esito seguono la regola del riepilogo tipizzato. Stato del
    // tratto («Parziale: sviluppo barre insufficiente») e numeri invariati.
    const string Po5Previous="Sviluppo barre insufficiente ad alcune quote: MRd nominale non utilizzabile.";
    static JsonNode Po5(JsonNode frozen,ref int classified)
    {
        if(!frozen["da_completare"]!.AsArray().Any(v=>v!.ToString()==Po5Previous))return frozen;
        var segment=(JsonObject)frozen.DeepClone();classified++;
        var texts=segment["da_completare"]!.AsArray().Select(v=>v!.ToString()).Select(t=>t==Po5Previous?ElasticHorizontalPile.DevelopmentPendingText:t).ToArray();
        segment["da_completare"]=J.Node(texts);
        var summary=segment["riepilogo"]!.AsObject();
        var failures=summary["non_soddisfatti"]!.AsArray().Select(v=>v!.ToString()).Where(t=>t!=Po5Previous).ToArray();
        var previousPending=summary["da_completare"]!.AsArray().Select(v=>v!.ToString()).ToHashSet();
        var pending=texts.Where(t=>previousPending.Contains(t)||t==ElasticHorizontalPile.DevelopmentPendingText).Distinct().ToArray();
        summary["non_soddisfatti"]=J.Node(failures);summary["da_completare"]=J.Node(pending);
        summary["stato"]=failures.Length>0?"NON VERIFICATO":"VERIFICA INCOMPLETA";summary["colore"]=failures.Length>0?"rosso":"ambra";summary["eseguiti_soddisfatti"]=false;
        return segment;
    }
}
