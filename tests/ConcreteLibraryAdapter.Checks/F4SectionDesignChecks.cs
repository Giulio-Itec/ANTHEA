using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Anthea.Calculations;

internal static class F4SectionDesignChecks
{
    internal static int Run(string root,string reference,string output)
    {
        CultureInfo.CurrentCulture=CultureInfo.CurrentUICulture=CultureInfo.GetCultureInfo("it-IT");
        var hashes=JsonNode.Parse(File.ReadAllText(Path.Combine(root,"tests/fixtures/f4/reference.json")))!["files"]!.AsArray()
            .ToDictionary(v=>v!["file"]!.GetValue<string>(),v=>v!["sha256"]!.GetValue<string>());
        JsonObject Read(string relative)
        {
            byte[] bytes=File.ReadAllBytes(Path.Combine(reference,relative));
            if(!Convert.ToHexString(SHA256.HashData(bytes)).Equals(hashes[relative],StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Frozen reference changed: "+relative);
            return JsonNode.Parse(bytes)!.AsObject();
        }
        var errors=new List<string>();var stats=new Stats("F4 section design");int cases=0,refused=0,candidates=0,classified=0;
        var deserialize=new JsonSerializerOptions{NumberHandling=JsonNumberHandling.AllowNamedFloatingPointLiterals|JsonNumberHandling.AllowReadingFromString};
        foreach(string file in Directory.GetFiles(Path.Combine(reference,"inputs/section-design"),"*.json").Order(StringComparer.Ordinal))
        {
            string id=Path.GetFileName(file);var captured=Read("inputs/section-design/"+id);var input=captured["document"]!.AsObject();
            string before=input.ToJsonString();var options=captured["configuration"]!.Deserialize<ConcreteDesignOptions>(deserialize)!;
            var expected=Read("results/section-design/"+id);cases++;
            try
            {
                var result=ConcreteReinforcementDesign.Optimize(input,options,id=="cancelled.json"?new CancellationToken(true):default);
                if(expected.S("status")!="ok"){errors.Add(id+": expected refusal");continue;}
                var actual=JsonSerializer.SerializeToNode(result)!.AsObject();actual.Remove("Performance");
                var frozen=expected["value"]!.DeepClone();classified+=Arm4(frozen,input);
                Compare(frozen,actual,id);candidates+=result.Candidates.Count;
                // Ricerca precedente spostata in ANTHEA.Legacy (decisione D3): stesse verifiche native, chiamata diretta.
                var old=LegacyConcreteReinforcementDesign.Optimize(input,options);
                var previous=JsonSerializer.SerializeToNode(old)!.AsObject();previous.Remove("Performance");
                Compare(previous,actual,id+"/legacy-search");
                if(old.Performance!.LongitudinalCalculations!=result.Performance!.LongitudinalCalculations||old.Performance.LongitudinalReuses!=result.Performance.LongitudinalReuses||old.Performance.ProbeSections!=result.Performance.ProbeSections)
                    errors.Add(id+": search reuse counters differ");
                foreach(var c in result.Candidates)
                {
                    var section=new SezioneCA(c.Input);
                    Compare(JsonSerializer.SerializeToNode(LegacyConcreteReinforcementDesign.Metrics(section,c.Shear,c.Detailing,options)),JsonSerializer.SerializeToNode(c.Metrics),id+"/metrics-legacy/"+c.Id);
                }
                foreach(string objective in new[]{"Peso","Numero barre","Numero diametri","Compromesso"})
                {
                    var choice=options with {Objective=objective};
                    Compare(JsonSerializer.SerializeToNode(LegacyConcreteReinforcementDesign.Rank(result.Candidates,choice)),
                        JsonSerializer.SerializeToNode(ConcreteReinforcementDesign.Rank(result.Candidates,choice)),id+"/rank/"+objective);
                }
            }
            catch(Exception ex) when(ex is ArgumentException or OperationCanceledException)
            {
                if(expected.S("status")!="refused"||expected["value"].S("type")!=ex.GetType().FullName)errors.Add(id+": unexpected "+ex);
                else refused++;
            }
            if(input.ToJsonString()!=before)errors.Add(id+": caller mutated");
        }
        if(cases!=9||refused!=3||candidates==0)errors.Add("Incomplete corpus");
        if(classified==0)errors.Add("ARM-4: no frozen N–Mx–My check renamed (classification not exercised)");
        Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"comparison.json"),JsonSerializer.Serialize(new{cases,refused,candidates,numbers=stats.Numbers,maxRelative=stats.MaxRelative,classified=new{ARM4=classified},errors=errors.Take(100),differences=errors.Count},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine(stats.Line());Console.WriteLine($"{cases} cases, {refused} refusals, {candidates} candidates, {errors.Count} differences; ARM-4 classified: {classified}");foreach(string error in errors.Take(20))Console.Error.WriteLine(error);
        return errors.Count==0?0:1;
        void Compare(JsonNode? a,JsonNode? b,string name)=>Json.Compare(Json.Sort(a),Json.Sort(b),name,stats,errors,1e-9);
    }

    // Differenza intenzionale classificata ARM-4 (analisi dei bug del 15/10/2026), applicata solo al riferimento congelato: la ricerca
    // precedente di ANTHEA.Legacy usa lo stesso LongitudinalChecks. I nomi «Resistenza plastica/elastica N–Mx–My» diventano «N–Mx–My · SLU/SLV»
    // (anche come chiavi delle esclusioni) e il dettaglio delle verifiche slu/slv dichiara il criterio resistente effettivo, cioè
    // dominio3d.modello_resistente con il predefinito per stato (Plastico per SLU, Elastico per SLV). Nessun numero cambia.
    static readonly Dictionary<string,string> Arm4Names=new(){["Resistenza plastica N–Mx–My"]="N–Mx–My · SLU",["Resistenza elastica N–Mx–My"]="N–Mx–My · SLV"};
    static int Arm4(JsonNode? node,JsonObject document)
    {
        int changed=0;
        if(node is JsonArray array)foreach(var item in array)changed+=Arm4(item,document);
        if(node is not JsonObject o)return changed;
        foreach(var (key,_) in o.ToArray())if(Arm4Names.TryGetValue(key,out var name)){var value=o[key];o.Remove(key);o[name]=value?.DeepClone();changed++;}
        foreach(var (key,value) in o.ToArray())
        {
            if(value is JsonValue v&&v.TryGetValue<string>(out var text)&&Arm4Names.TryGetValue(text,out var renamed)){o[key]=renamed;changed++;}
            else changed+=Arm4(value,document);
        }
        if(o["Key"]?.ToString() is "slu" or "slv"&&o["Detail"]?.ToString() is string detail&&detail.StartsWith("Iterativo 3D a eccentricità costante",StringComparison.Ordinal))
        {
            string criterion=document["workspace_ca"]?["dominio3d"]?["modello_resistente"]?.ToString()??(o["Key"]!.ToString()=="slv"?"Elastico":"Plastico");
            o["Detail"]="Dominio "+criterion.ToLowerInvariant()+" (criterio resistente), iterativo 3D a eccentricità costante"+detail["Iterativo 3D a eccentricità costante".Length..];changed++;
        }
        return changed;
    }
}
