using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Calculations;

internal static class F4ResidualChecks
{
    internal static int Run(string root, string output)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("it-IT");
        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(root,"tests/fixtures/f4/residual-reference.json")))!;
        byte[] bytes = File.ReadAllBytes(Path.Combine(root,manifest["file"]!.GetValue<string>()));
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(manifest["sha256"]!.GetValue<string>(),StringComparison.OrdinalIgnoreCase)) throw new Exception("Immutable residual capture changed.");
        var errors = new List<string>();var stats = new Stats("F4 residual adapters"); int cases=0, rejected=0, pv1=0;
        foreach(var row in JsonNode.Parse(bytes)!["cases"]!.AsArray().OfType<JsonObject>())
        {
            string before=row["input"]!.ToJsonString();var actual=Pv1(F4ResidualCapture.Evaluate(row),row.S("id"),errors,ref pv1);
            Json.Compare(Json.Sort(row["expected"]),Json.Sort(actual),row.S("id"),stats,errors,1e-10);
            if(row["input"]!.ToJsonString()!=before) errors.Add(row.S("id")+": input mutated");
            if(row["expected"]?["error"] is not null)rejected++;
            cases++;
        }
        if(cases!=53||rejected!=6)errors.Add("Incomplete frozen corpus");
        if(pv1==0)errors.Add("PV-1: no pile result classified (classification not exercised)");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output,"comparison.json"),JsonSerializer.Serialize(new{cases,rejected,stats.Numbers,stats.MaxRelative,classified=new{PV1=pv1},errors},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine(stats.Line()+$"; PV-1 classified: {pv1}"); foreach(string error in errors.Take(20))Console.Error.WriteLine(error);
        return errors.Count==0?0:1;
    }

    // Differenza intenzionale classificata PV-1 (analisi dei bug del 15/10/2026, GPCChecker.Geotechnics a9b36481), applicata solo al risultato
    // attuale: il risultato del palo ha la nuova chiave «azioni_condizione» (azioni di ogni curva). Le azioni drenate devono coincidere con
    // «azioni» per verso, confrontate con il riferimento congelato; nel micropalo (solo condizione drenata, peso intero) la chiave nuova è
    // proprio «azioni». Verificata la regola, la chiave è tolta e il resto si confronta come prima.
    static JsonNode? Pv1(JsonNode? actual,string id,List<string> errors,ref int classified)
    {
        if(actual is null)return null;
        var copy=actual.DeepClone();int count=0;
        foreach(var node in Objects(copy).Where(o=>o["azioni_condizione"] is JsonObject&&o["azioni"] is JsonObject).ToArray())
        {
            var byCondition=node["azioni_condizione"]!.AsObject();var actions=node["azioni"]!.AsObject();
            foreach(var (curve,points) in byCondition)
            {
                string direction=curve.EndsWith("trazione")?"trazione":"compressione";
                if((curve==direction||curve.StartsWith("drenante_"))&&!JsonNode.DeepEquals(points,actions[direction]))errors.Add(id+": PV-1 drained actions of "+curve+" differ from azioni."+direction);
            }
            node.Remove("azioni_condizione");count++;
        }
        classified+=count;return copy;
        static IEnumerable<JsonObject> Objects(JsonNode node)
        {
            if(node is JsonObject o){yield return o;foreach(var p in o)if(p.Value is not null)foreach(var x in Objects(p.Value))yield return x;}
            else if(node is JsonArray a)foreach(var item in a)if(item is not null)foreach(var x in Objects(item))yield return x;
        }
    }
}
