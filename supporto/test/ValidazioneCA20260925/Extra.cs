using System.Text.Json;
using System.Text.Json.Nodes;
using X.Core;
using Materiali;
static class Extra
{
 public static JsonNode? Run(JsonNode v)
 {
  var p=v["params"]!;string mode=v.S("mode");
  double D(string k,double fallback=0)=>p.D(k,fallback);
  var input=SezioneCA.DefaultInput();
  foreach(var(k,val) in v["input"]!.AsObject())input[k]=val?.DeepClone();
  var w=J.Obj(("normativa","NTC 2018"),("trefoli",new JsonArray()));
  if(p["tendons"] is JsonArray ts)w["trefoli"]=ts.DeepClone();
  var opts=J.Obj(("angoli","64"),("criterio","N costante"),("assi","Locali"),("strategia","Iterativo"),("modello","Lineare"),("trazione_cls","No"),("phi",p.D("phi")),("esposizione",p.S("exposure","XC1")),("sensibilita",p.S("sensitivity","Poco sensibile")),("durata",p.S("duration","Lunga")),("aderenza","Migliorata"));
  object result;
  if(mode=="extra_anchor")result=new ConcreteAnchorageCalculator().Calculate(new(D("phi"),D("sigma"),D("fct"),1.5,p.B("good"),D("length"),p.B("lap"),D("percent",100),D("gap")));
  else if(mode=="extra_detail")result=new ConcreteDetailingCalculator().Calculate(new(Enum.Parse<ConcreteMemberKind>(p.S("kind")),new SezioneCA(input),D("N"),p.B("stirrups",true),D("phiSt",8),D("s",150),(int)D("legs",2),D("dg",20),D("dur",25),D("dev",10),p.B("lap"),D("secondary"),D("secondarySpacing",150),p.B("critical"),false,false));
  else if(mode=="extra_cover")result=NtcCover.Calculate([Durability.Exposures.Single(e=>e.Code==p.S("exposure"))],D("fck",30),new((int)D("life",50),false,false,false,D("phi",20),D("dg",20),D("dev",10),false,0,0),p.B("plate"),p.B("quality"),MinimumConcrete.Fck(p.S("exposure")));
  else if(mode=="extra_shear")result=Shear(p);
  else if(mode=="extra_torsion")result=new ConcreteTorsionCalculator().Calculate(new(D("T"),new(D("Ak"),D("uk"),D("t")),17,450/1.15,D("At"),D("s"),D("Al"),D("cot"),D("Vx"),D("Vy"),Shear(p["sx"]!),Shear(p["sy"]!)));
  else if(mode=="extra_geometry") {var model=CheckerSection.PrepareModel(input,w);var geo=model.Geometry;result=new {area=geo.AreaCls,polygonArea=model.Section.Area,steel=geo.AreaSteel,vertices=geo.Outline.Count,holes=geo.Holes.Count,geo.CentroidY};}
  else if(mode=="extra_quick") result=SectionMomentResistance.Calculate(input,w,D("N"),p.B("elastic"));
  else if(mode=="extra_material") {
    var st=ConcreteStandards.Effective(input,w);var cls=ConcreteMaterials.Concrete(input);var steel=ConcreteMaterials.Rebar(input);
    result=new {stress= p.B("steel")?steel.CalculateDesignStress(st,D("strain")):cls.CalculateDesignStressConcrete(st,D("strain")),cls.Ecm,cls.Fctm};
  }
  else if(mode=="extra_curve") {
    opts["modello"]="Non lineare";var e=new CheckerSection(input,w,opts);var domain=e.Domain3D();
    result=new MomentCurvatureCalculator().Calculate(new(D("N"),D("angle"),10,1,false,1,12),a=>domain.Check(a),a=>e.Stress(a,"SLE"),450/1.15/200000);
  }
  else {
    opts["trazione_cls"]=p.S("tensile","No");var e=new CheckerSection(input,w,opts);var force=new ActionPoint(D("N"),D("Mx"),D("My"));string set=p.S("set","SLE_QP");var state=e.Stress(force,set);
    result=new {state.sigma_cls,state.sigma_acciaio,state.Ratio,state.Status,state.ConcreteVertices,state.tensioni_barre,crack=Ntc2018Checks.Cracking(e,state,force,input,w,opts,set)};
  }
  return JsonSerializer.SerializeToNode(result);
 }
 static Ntc2018Checks.ShearResult Shear(JsonNode p)=>Ntc2018Checks.Shear(p.D("N"),p.D("V"),p.D("A"),p.D("bw"),p.D("d"),p.D("Asl"),30,17,450/1.15,1.5,p.D("Asw"),p.D("s",150),p.D("alpha",90),p.D("cot",2),p.D("z",.9));
}
