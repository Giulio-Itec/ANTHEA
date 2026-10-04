using GPC.Checkers.Geotechnics.Piles;
using System.Globalization;
CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
int count=0;
void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
void Near(double a,double b,double tolerance,string name)=>Check(Math.Abs(a-b)<=tolerance*Math.Max(1e-12,Math.Abs(b)),name+$" ({a:G8}; ref {b:G8})");
void Invalid(ElasticPileInput p,string name){try{ElasticPile.Calculate(p);throw new Exception("Accepted "+name);}catch(ArgumentException){Check(true,name);}}
ElasticPileInput Input()=>new(){Length=30,Diameter=1,EI=50000,EISource="Benchmark elastic EI",Force=100,Step=.25,Layers=new[]{new ElasticPileLayer{Thickness=30,Value=10000}}};
var input=Input();var r=ElasticPile.Calculate(input);
// Independent decaying exact solution of EI y''''+k y=0, semi-infinite free head, M0=0.
double beta=Math.Pow(10000d/(4*50000),.25),y0=100/(2*50000*Math.Pow(beta,3));
Near(r.HeadDisplacement,y0,2e-6,"analytic constant foundation head y");
Near(r.HeadRotation,-beta*y0,2e-6,"analytic constant foundation head rotation");
double zM=Math.PI/(4*beta),mMax=100/beta*Math.Exp(-beta*zM)*Math.Sin(beta*zM);
Near(r.Extrema["M"].Maximum,mMax,2e-5,"analytic constant foundation maximum M");
Near(r.Extrema["M"].MaximumDepth,zM,1e-4,"analytic depth maximum M");
double errorV=r.Points.Where(p=>p.Depth<10).Max(p=>Math.Abs(p.Shear-100*Math.Exp(-beta*p.Depth)*(Math.Cos(beta*p.Depth)-Math.Sin(beta*p.Depth))))/100;
Check(errorV<1e-6,$"analytic V over entire active depth, normalized error={errorV:G6}");
Check(Math.Abs(r.ForceResidual)<1e-7&&Math.Abs(r.MomentResidual)<1e-6,"global force/moment balance");
input.Force=0;var zero=ElasticPile.Calculate(input);Check(zero.Points.All(p=>p.Displacement==0&&p.Moment==0&&p.Shear==0),"zero load");
input.Force=200;var twice=ElasticPile.Calculate(input);Near(twice.HeadDisplacement,2*r.HeadDisplacement,1e-10,"linearity y");Near(twice.Extrema["M"].Maximum,2*r.Extrema["M"].Maximum,1e-10,"linearity M");
input.Force=-100;var reverse=ElasticPile.Calculate(input);Near(reverse.HeadDisplacement,-r.HeadDisplacement,1e-10,"load reversal y");Near(reverse.Extrema["M"].Minimum,-r.Extrema["M"].Maximum,1e-10,"load reversal M");
input=Input();input.FixedHeadRotation=true;var fix=ElasticPile.Calculate(input);Near(fix.HeadDisplacement,y0/2,2e-6,"analytic restrained head y");Check(fix.HeadRotation==0,"restrained rotation");Near(fix.HeadReactionMoment,100/(2*beta),2e-6,"head reaction moment");
input=Input();input.Length=10;input.Tip=ElasticPileTip.Fixed;input.Layers[0].Value=0;input.HeadMoment=20;var cant=ElasticPile.Calculate(input);
Near(cant.HeadDisplacement,100*1000d/(3*50000)-20*100d/(2*50000),1e-7,"exact cantilever displacement under H and C");Near(cant.HeadRotation,-100*100d/(2*50000)+20*10d/50000,1e-7,"exact cantilever rotation");Near(cant.Extrema["M"].Maximum,980,1e-7,"cantilever M");Near(cant.TipReactionForce,-100,1e-7,"tip force");Near(cant.TipReactionMoment,980,1e-7,"tip moment");
input=Input();input.FreeLength=2;var free=ElasticPile.Calculate(input);Check(free.Points.Where(p=>p.Depth<2).All(p=>p.Kh==0&&p.SoilReaction==0),"free length has no springs");Near(free.Points.First(p=>p.Depth==2).Moment,200,1e-7,"free length moment at ground");
input=Input();input.Layers=new[]{new ElasticPileLayer{Thickness=4.13,Value=10000},new ElasticPileLayer{Thickness=25.87,Value=50000}};var layered=ElasticPile.Calculate(input);var jump=layered.Points.Where(p=>Math.Abs(p.Depth-4.13)<1e-10).ToArray();Check(jump.Length==2,"mesh interface two sides");Near(jump[1].Kh/jump[0].Kh,5,1e-12,"stiffness jump");Near(jump[0].Displacement,jump[1].Displacement,1e-12,"displacement continuity");Near(jump[0].Moment,jump[1].Moment,1e-7,"moment continuity");
input.Layers=new[]{new ElasticPileLayer{Thickness=4.13,Value=10000},new ElasticPileLayer{Thickness=25.87,Value=10000}};var split=ElasticPile.Calculate(input);Near(split.HeadDisplacement,r.HeadDisplacement,1e-6,"identical sublayers y");Near(split.Extrema["M"].Maximum,r.Extrema["M"].Maximum,1e-6,"identical sublayers M");
input=Input();input.Diameter=2;input.Layers[0].Value=5000;var dia=ElasticPile.Calculate(input);Near(dia.HeadDisplacement,r.HeadDisplacement,1e-10,"kh times diameter conversion");input.Layers[0].Law=ElasticSoilLaw.ConstantDistributed;input.Layers[0].Value=10000;Near(ElasticPile.Calculate(input).HeadDisplacement,r.HeadDisplacement,1e-10,"distributed k not multiplied by diameter twice");
input=Input();input.Layers=new[]{new ElasticPileLayer{Thickness=4,Value=10000,Law=ElasticSoilLaw.LinearKhReference},new ElasticPileLayer{Thickness=26,Value=10000,Law=ElasticSoilLaw.LinearKhReference}};var linear=ElasticPile.Calculate(input);Near(linear.Points.Last(p=>p.Depth==4).Kh,40000,1e-12,"global z at layer boundary");
// Same problem in N and mm instead of kN and m.
input=Input();input.Length*=1000;input.Diameter*=1000;input.Step*=1000;input.EI*=1e9;input.Force*=1000;input.Layers[0].Thickness*=1000;input.Layers[0].Value*=1e-6;var units=ElasticPile.Calculate(input);Near(units.HeadDisplacement/1000,r.HeadDisplacement,1e-8,"unit invariance y");Near(units.Extrema["M"].Maximum/1e6,r.Extrema["M"].Maximum,1e-8,"unit invariance M");
input=Input();input.Step=1;var c=ElasticPile.Calculate(input);var m=ElasticPile.Calculate(input,2);var f=ElasticPile.Calculate(input,4);
foreach(string key in new[]{"y","M","V"}){double A(ElasticPileResult z)=>key=="y"?z.HeadDisplacement:z.Extrema[key].AbsoluteMaximum;double err=Math.Abs(A(f)-A(m))/Math.Max(1e-12,Math.Abs(A(f)));Check(err<.001,"mesh convergence "+key+$" relative={err:G6}; coarse={A(c):G9}, medium={A(m):G9}, fine={A(f):G9}");}
input=Input();input.Layers[0].Value=0;Invalid(input,"mechanism free tip");input.Tip=ElasticPileTip.Pinned;Invalid(input,"mechanism pinned tip free head");input.FixedHeadRotation=true;Check(double.IsFinite(ElasticPile.Calculate(input).HeadDisplacement),"pinned tip with restrained head stable");
input=Input();input.HeadMoment=1;input.Eccentricity=1;Invalid(input,"double counted moment");input=Input();input.EI=double.NaN;Invalid(input,"invalid EI");input=Input();input.Layers[0].Thickness=1;Invalid(input,"insufficient stratigraphy");input=Input();input.Layers[0].Value=-1;Invalid(input,"negative stiffness");input=Input();input.Step=0;Invalid(input,"zero mesh step");
count+=ViggianiChecks.Run();
Console.WriteLine($"TOTAL {count} passed");
