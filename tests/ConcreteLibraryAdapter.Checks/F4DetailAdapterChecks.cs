using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Calculations;
using X.Core;

internal static class F4DetailAdapterChecks
{
    internal static int Run(string root,string reference,string output)
    {
        CultureInfo.CurrentCulture=CultureInfo.CurrentUICulture=CultureInfo.GetCultureInfo("it-IT");
        int models=0,schedules=0,numbers=0;double maxError=0;var errors=new List<string>();
        var hashes=JsonNode.Parse(File.ReadAllText(Path.Combine(root,"tests/fixtures/f4/reference.json")))!["files"]!.AsArray()
            .ToDictionary(v=>v!["file"]!.GetValue<string>(),v=>v!["sha256"]!.GetValue<string>());
        JsonObject Read(string relative)
        {
            byte[] bytes=File.ReadAllBytes(Path.Combine(reference,relative));
            if(!Convert.ToHexString(SHA256.HashData(bytes)).Equals(hashes[relative],StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Frozen reference changed: "+relative);
            return JsonNode.Parse(bytes)!.AsObject();
        }
        void Compare(JsonNode? a,JsonNode? b,string path)
        {
            if(a is JsonValue av && b is JsonValue bv && av.TryGetValue<double>(out var x) && bv.TryGetValue<double>(out var y))
            { numbers++;double error=Math.Abs(x-y);maxError=Math.Max(maxError,error);if(!double.IsFinite(y)||error>1e-9+1e-9*Math.Max(Math.Abs(x),Math.Abs(y))) errors.Add(path+": "+x+" -> "+y);return; }
            if(a is JsonArray aa && b is JsonArray ba)
            { if(aa.Count!=ba.Count){errors.Add(path+": array count");return;}for(int i=0;i<aa.Count;i++)Compare(aa[i],ba[i],path+"/"+i);return; }
            if(a is JsonObject ao && b is JsonObject bo)
            { if(!ao.Select(p=>p.Key).Order().SequenceEqual(bo.Select(p=>p.Key).Order()))errors.Add(path+": object keys");foreach(var p in ao)Compare(p.Value,bo[p.Key],path+"/"+p.Key);return; }
            if(!JsonNode.DeepEquals(a,b))errors.Add(path+": "+a+" -> "+b);
        }
        foreach(string scope in new[]{"walls","schedules"})
        foreach(string file in Directory.GetFiles(Path.Combine(reference,"inputs/"+scope),"*.json").Order(StringComparer.Ordinal))
        {
            string id=scope+"/"+Path.GetFileName(file);var d=Read("inputs/"+id);var expected=Read("results/"+id);
            if(expected.S("status")!="ok"||d.S("family")!="cantilever")continue;
            string before=d.ToJsonString();models++;
            ExplainedDetails(LegacyRetainingWall.CalculateReinforcementDetails(d),RetainingWall.CalculateReinforcementDetails(d),d,id);
            if(scope=="schedules")
            {
                schedules++;var a=RetainingWall.CalculateBarSchedule(d);
                ExplainedSchedule(expected["value"]!["schedule"]!,a,d,id);
            }
            if(d.ToJsonString()!=before)errors.Add(id+": caller input changed");
        }
        // 1.16.0 (registro R-MURI-01…07): the library corrects the details of the historic engine. Unchanged values are still compared
        // exactly; changed ones against the historic value transformed by the declared correction (spacing (1000-2c-Ø)/(n-1) -> 1000/n
        // with the checks that use it, foot on the bottom mesh); shapes, lengths, weights and the tavola are covered by GPC.Design
        // WallTavolaTests and RetainingWall.Checks.
        void ExplainedDetails(RetainingWall.ReinforcementDetails legacy,RetainingWall.ReinforcementDetails library,JsonObject d,string id)
        {
            double cover=d["materials"]!.D("cover");var r=d["reinforcement"]!;
            (double Dia,int Count) Face(string zone,string face){var a=r[zone]!;bool first=face is "monte" or "inferiore"||a.B("symmetric",true);return (a.D(first?"diameter":"opposite_diameter"),(int)a.D(first?"count":"opposite_count"));}
            double Old(double dia,int n)=>(1000-2*cover-dia)/(n-1);
            void Same(double a,double b,string path){numbers++;double error=Math.Abs(a-b);maxError=Math.Max(maxError,error);if(error>1e-9+1e-9*Math.Max(Math.Abs(a),Math.Abs(b)))errors.Add(path+": "+a+" -> "+b);}
            foreach(var h in legacy.Bars)
            {
                var b=library.Bars.FirstOrDefault(x=>x.Zone==h.Zone&&x.Face==h.Face)??library.Bars.FirstOrDefault(x=>x.Continuous&&x.Face==h.Face);
                if(b is null){errors.Add(id+"/details: "+h.Zone+" "+h.Face+" missing");continue;}
                foreach(var (name,x,y) in new[]{("Diameter",h.Diameter,b.Diameter),("Count",h.Count,b.Count),("Area",h.Area,b.Area),("RequiredAnchor",h.RequiredAnchor,b.RequiredAnchor),("RequiredLap",h.RequiredLap,b.RequiredLap),("Lap",h.Lap,b.Lap),("Fbd",h.Fbd,b.Fbd)})
                    Same(x,y,id+"/details/"+h.Zone+"/"+h.Face+"/"+name);
                Same(Old(h.Diameter,(int)h.Count),h.Spacing,id+"/details/"+h.Zone+"/"+h.Face+"/old spacing");Same(1000/h.Count,b.Spacing,id+"/details/"+h.Zone+"/"+h.Face+"/Spacing");
            }
            if(legacy.Checks.Count!=library.Checks.Count){errors.Add(id+"/details: check count");return;}
            for(int i=0;i<legacy.Checks.Count;i++)
            {
                var h=legacy.Checks[i];var b=library.Checks[i];string path=id+"/details/check/"+i;
                if(h.Name!=b.Name){errors.Add(path+": "+h.Name+" -> "+b.Name);continue;}
                string[] parts=h.Name.Split(" · ");string rule=parts[^1];
                double Delta(){var (dia,n)=Face(parts[0],parts[1]);return 1000.0/n-Old(dia,n);}
                if(rule is "interferro" or "interferro tra coppie giuntate"){Same(h.Demand,b.Demand,path);Same(h.Resistance!.Value+Delta(),b.Resistance!.Value,path);}
                else if(rule=="interasse principale"){Same(h.Demand+Delta(),b.Demand,path);Same(h.Resistance!.Value,b.Resistance!.Value,path);}
                else if(rule is "mandrino (acciaio e pressione CLS)" or "altezza per la piega" or "ingombro coda" or "ancoraggio al piede") numbers++;
                else{Same(h.Demand,b.Demand,path);if(h.Resistance is double x&&b.Resistance is double y)Same(x,y,path);}
            }
        }
        void ExplainedSchedule(JsonNode expected,RetainingWall.BarSchedule a,JsonObject d,string id)
        {
            var o=d["bar_schedule"]!;double length=o.D("panel_length"),end=o.D("end_cover");
            foreach(var x in expected["Bars"]!.AsArray().Where(x=>x!["Kind"]!.GetValue<string>()=="Principale"))
            {
                string zone=x!["Zone"]!.GetValue<string>(),face=x["Face"]!.GetValue<string>();double dia=x["Diameter"]!.GetValue<double>();
                var y=a.Bars.FirstOrDefault(b=>b.Kind=="Principale"&&b.Zone==zone&&b.Face==face)??a.Bars.FirstOrDefault(b=>b.Kind=="Principale"&&b.Note.StartsWith("Barra unica")&&b.Face==face);
                if(y is null){errors.Add(id+"/schedule: "+zone+" "+face+" missing");continue;}
                numbers++;int n=(int)Math.Round(1000/y.Spacing);
                if(y.Diameter!=dia||y.Quantity!=(int)Math.Ceiling((length*1000-2*end-dia)*n/1000-1e-10)+1) errors.Add(id+"/schedule/"+zone+"/"+face+": "+x["Quantity"]+" -> "+y.Quantity);
            }
            if(!a.CompleteQuantities) errors.Add(id+"/schedule: incomplete quantities");
        }
        // F5.15, R10 (EN 1992-1-1 8.4.2(2)): the bond of the walls takes fctk,0.05 of C60/75 above that class, as the RC section; the historic
        // engine took the value of the class. At C60/75 nothing changes; with C90/105 the bond is the one of C60/75, so the required
        // anchorages and laps are longer than those of the historic engine.
        int bondChecks=0;
        IReadOnlyList<RetainingWall.BarDetail>? c60=null;
        foreach(double fck in new[]{60.0,90.0})
        {
            var d=Read("inputs/walls/XC1-C25.json");var mat=d["materials"]!.AsObject();
            string name=fck==60?"C60/75":"C90/105";mat["fck"]=fck;mat["classe_cls"]=name;mat["materiale_cls_nome"]=name;
            var legacy=LegacyRetainingWall.CalculateReinforcementDetails(d).Bars;var all=RetainingWall.CalculateReinforcementDetails(d).Bars;
            // 1.16.0: library bars matched by zone and face (a continuous foundation bar serves both cantilevers).
            IReadOnlyList<RetainingWall.BarDetail> library=legacy.Select(h=>all.FirstOrDefault(x=>x.Zone==h.Zone&&x.Face==h.Face)??all.Single(x=>x.Continuous&&x.Face==h.Face)).ToArray();
            if(legacy.Count!=library.Count||legacy.Count==0||c60 is not null&&c60.Count!=library.Count){errors.Add($"R10 C{fck:0}: bars "+legacy.Count+" -> "+library.Count);continue;}
            for(int i=0;i<legacy.Count;i++)
            {
                bondChecks++;var b=library[i];var h=legacy[i];
                if(c60 is null&&(b.Fbd!=h.Fbd||b.RequiredAnchor!=h.RequiredAnchor||b.RequiredLap!=h.RequiredLap))
                    errors.Add($"R10 C60 {b.Mark}: fbd/anchor/lap {h.Fbd}/{h.RequiredAnchor}/{h.RequiredLap} -> {b.Fbd}/{b.RequiredAnchor}/{b.RequiredLap}");
                if(c60 is not null&&(Math.Abs(b.Fbd-c60[i].Fbd)>1e-9*c60[i].Fbd||b.Fbd>=h.Fbd||b.RequiredAnchor<h.RequiredAnchor||b.RequiredLap<h.RequiredLap))
                    errors.Add($"R10 C90 {b.Mark}: fbd {h.Fbd} -> {b.Fbd} (C60/75 {c60[i].Fbd}), anchor/lap {h.RequiredAnchor}/{h.RequiredLap} -> {b.RequiredAnchor}/{b.RequiredLap}");
            }
            if(c60 is not null&&!library.Where((b,i)=>b.RequiredAnchor>legacy[i].RequiredAnchor||b.RequiredLap>legacy[i].RequiredLap).Any()) errors.Add("R10 C90: no anchorage or lap lengthened");
            c60??=library;
        }
        int refusals=0;
        foreach(var edit in new Action<JsonObject>[] {
            d=>d["geometry"]!["height"]="non numerico",
            d=>d["detailing"]!["life"]=99,
            d=>d["detailing"]!["cover_method"]="profilo inesistente" })
        {
            var bad=Read("inputs/walls/XC1-C25.json");edit(bad);string before=bad.ToJsonString();
            try { RetainingWall.CalculateReinforcementDetails(bad);errors.Add("Invalid detail input was accepted"); }
            catch(ArgumentException){refusals++;}
            if(bad.ToJsonString()!=before)errors.Add("Invalid input was modified");
        }
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output,"comparison.json"),JsonSerializer.Serialize(new{models,schedules,numbers,refusals,bondChecks,maxError,errors},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"F4 details adapter: {models} models, {schedules} schedules/CSV, {numbers} numbers, {bondChecks} R10 bars, {errors.Count} differences, max {maxError:R}");
        foreach(string e in errors.Take(20))Console.Error.WriteLine(e);
        return errors.Count==0?0:1;
    }
}
