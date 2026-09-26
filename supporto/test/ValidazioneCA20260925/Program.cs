using System.Text.Json;
using System.Text.Json.Nodes;
using X.Core;
var root=JsonNode.Parse(File.ReadAllText(args[0]))!;
var results=new JsonArray();
foreach(var v in root["cases"]!.AsArray()) {
 string id=v!.S("id"), mode=v.S("mode");
 var o=new JsonObject{["id"]=id};
 try {
 if(mode.StartsWith("extra_")) { o["actual"]=Extra.Run(v); }
 else if(mode=="crack_scalar") {
 var a=v["args"]!.AsArray();
 double D(int i)=>a[i]!.GetValue<double>();
 o["wk"]=Ntc2018Checks.CrackWidth(D(0),D(1),D(2),D(3),D(4),D(5),D(6),D(7),D(8),a[9]!.GetValue<bool>(),true,.5);
 } else if(mode=="shear") {
 var r=Ntc2018Checks.Shear(-300,v.D("V"),150000,v.D("bw"),v.D("d"),2*Math.PI*100,30,17,450/1.15,1.5,v.D("asw"),150,90,2);
 o["actual"]=JsonSerializer.SerializeToNode(r);
 } else {
 var input=SezioneCA.DefaultInput(); var sec=v["section"]!;
 input["width_mm"]=sec["b"]!.DeepClone();input["height_mm"]=sec["h"]!.DeepClone();input["fck_mpa"]="30";input["fyk_mpa"]="450";
 input["alpha_cc"]="0.85";input["gamma_c"]="1.5";input["gamma_s"]="1.15";input["steel_modulus_mpa"]="200000";
 input["cls_diagramma"]="Parabola-rettangolo";input["steel_diagramma"]="Elastoplastico";input["steel_fu_mpa"]="450";input["steel_eps_u"]="100";input["cover_mm"]="30";input["transverse_bar_diameter_mm"]="10";
 var bars=new JsonArray();foreach(var b in sec["bars"]!.AsArray()) bars.Add(new JsonObject{["x"]=b![0]!.DeepClone(),["y"]=b[1]!.DeepClone(),["phi"]=b[2]!.DeepClone()});input["barre_manuali"]=bars;
 var ws=new JsonObject{["normativa"]="NTC 2018",["trefoli"]=new JsonArray()};
 var opts=new JsonObject{["angoli"]=args.Length>2?args[2]:"64",["criterio"]="Eccentricità costante",["trazione_cls"]="No",["phi"]="0",["assi"]="Locali",["modello"]=v.S("law")=="linear"?"Lineare":"Non lineare",["tipo"]="N–M",["theta"]="0",["N"]="0",["strategia"]="Iterativo",["esposizione"]="XC1",["sensibilita"]="Poco sensibile",["durata"]="Lunga",["aderenza"]="Migliorata"};
 var r=v["reference"]!;var force=new ActionPoint(r.D("N"),r.D("Mx"),r.D("My"));
 var engine=new CheckerSection(input,ws,opts,v.S("state","SLU"));
 if(mode=="domain") {
 DomainCheck Run(double factor)=>v.D("dimension")==3 ? engine.Domain3D().Check(force*factor) : engine.Domain2D().Check(force*factor);
 o["boundary"]=JsonSerializer.SerializeToNode(Run(1));o["inside"]=JsonSerializer.SerializeToNode(Run(.8));o["outside"]=JsonSerializer.SerializeToNode(Run(1.2));
 } else {
 var state=engine.Stress(force,v.S("set","SLE"));
 o["actual"]=JsonSerializer.SerializeToNode(new{state.sigma_cls,state.sigma_acciaio,state.Ratio,state.Status,state.Response,state.tensioni_barre,state.ConcreteVertices});
 if(mode=="crack_full") o["crack"]=JsonSerializer.SerializeToNode(Ntc2018Checks.Cracking(engine,state,force,input,ws,opts,v.S("set")));
 }
 }
 } catch(Exception e){o["error"]=e.ToString();}
 results.Add(o); Console.WriteLine(o.ToJsonString()); File.WriteAllText(args[1],results.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));
}
