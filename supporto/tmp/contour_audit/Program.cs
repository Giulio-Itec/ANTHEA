using X.Core;
using System.Text.Json.Nodes;
var data=SezioneCA.DefaultData(); var w=SectionWorkspace.Prepare(data);var input=data["input"]!.AsObject();var opts=(JsonObject)w["dominio3d"]!.DeepClone();opts["angoli"]="16";
var engine=new CheckerSection(input,w,opts);var domain=engine.Domain3D();int checkedSamples=0;
foreach(var action in new[]{new ActionPoint(-500,100,0),new ActionPoint(-500,0,100),new ActionPoint(-500,100,50)}) {
foreach(var pair in new[]{("Ed",engine.Stress(action,"ED")),("Rd",domain.Check(action).LimitState!.Value)}) {
var state=pair.Item2;var r=state.Raster!;double maxError=0,maxStressError=0,maxTableError=0;
for(int j=0;j<r.Size;j++)for(int i=0;i<r.Size;i++) {
double x=r.XMin+(i+.5)*(r.XMax-r.XMin)/r.Size,y=r.YMax-(j+.5)*(r.YMax-r.YMin)/r.Size;
double strain=state.Native.StrainPlane.GetStrain(x,y);maxError=Math.Max(maxError,Math.Abs(r.Strains[j*r.Size+i]-1000*strain));
// Independent parabola-rectangle law for this default C35/45, with tension neglected.
double sigma=strain>=0?0:strain<=-.002?-state.ConcreteCompressionStrength:-state.ConcreteCompressionStrength*(1-Math.Pow(1+strain/.002,2));
maxStressError=Math.Max(maxStressError,Math.Abs(r.Stresses[j*r.Size+i]-sigma)); double e=Math.Clamp(-strain,0,.002), a=Math.Floor(e/.00025)*.00025, b=Math.Min(.002,a+.00025); double Law(double z)=>-state.ConcreteCompressionStrength*(1-Math.Pow(1-z/.002,2)); double table=b==a?Law(a):Law(a)+(Law(b)-Law(a))*(e-a)/(b-a); maxTableError=Math.Max(maxTableError,Math.Abs(r.Stresses[j*r.Size+i]-table));checkedSamples++;
}
Console.WriteLine($"{pair.Item1} N={action.N} Mx={action.Mx} My={action.My}: max error strain={maxError:G6} permille stress={maxStressError:G6} MPa table interpolation={maxTableError:G8} MPa; strain legend [{state.FiberStrains.Min():G8},{state.FiberStrains.Max():G8}] vs edges [{state.ConcreteVertices.Min(v=>v.Strain):G8},{state.ConcreteVertices.Max(v=>v.Strain):G8}]");
if(maxError>1e-10||maxTableError>1e-8)throw new Exception("Mismatch contouring strain");
}}
Console.WriteLine($"CHECKED: {checkedSamples} samples, 6 maps.");


