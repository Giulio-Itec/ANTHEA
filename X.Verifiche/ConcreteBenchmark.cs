using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using X.Core;

internal static class ConcreteBenchmark
{
    private sealed record Sample(string Shape,string Operation,int Run,double Milliseconds,long AllocatedBytes,double Checksum);
    internal static void Run(string directory)
    {
        Directory.CreateDirectory(directory);
        var samples=new List<Sample>();var cases=new JsonObject();
        foreach(string name in new[]{"rettangolare","rettangolare_cava","circolare","circolare_cava"})
        {
            var data=SezioneCA.DefaultData();var workspace=SectionWorkspace.Prepare(data);var input=data["input"]!.AsObject();
            input["shape"]=name.StartsWith("circolare")?"Circolare":"Rettangolare";
            input["foro_presente"]=name.EndsWith("cava");input["inner_width_mm"]="200";input["inner_height_mm"]="300";input["inner_diameter_mm"]="500";
            var options=(JsonObject)workspace["dominio3d"]!.DeepClone();options["angoli"]="32";options["criterio"]="N costante";options["assi"]="Locali";
            var actions=Enumerable.Range(0,24).Select(i=>new ActionPoint(-300-20*i,60+3*i,20+2*i)).ToArray();
            var sleActions=Enumerable.Range(0,24).Select(i=>new ActionPoint(200+5*i,0,0)).ToArray();
            cases[name]=J.Obj(("input",input),("workspace",workspace),("options",options),("actions",actions),("sleActions",sleActions),("curva",new MomentCurvatureRequest(-500,0,30,.95)));
            // Run 0 includes first-use/JIT costs for this case; three later runs are separately retained.
            for(int run=0;run<4;run++)
            {
                double Measure(string operation,Func<double> action)
                {
                    long before=GC.GetTotalAllocatedBytes(false);var watch=Stopwatch.StartNew();double sum=action();watch.Stop();
                    if(!double.IsFinite(sum))throw new Exception(name+" / "+operation+": risultato non finito");
                    samples.Add(new(name,operation,run,watch.Elapsed.TotalMilliseconds,GC.GetTotalAllocatedBytes(false)-before,sum));return sum;
                }
                CheckerSection? engine=null;CheckerDomain3D? domain=null;
                Measure("preparazione",()=>{engine=new(input,workspace,options);return engine.Section.Area;});
                Measure("dominio_3d_32",()=>{domain=engine!.Domain3D();return domain.Native.Domain.DomainPoints.Length;});
                Measure("24_verifiche",()=>domain!.CheckMany(actions).Sum(r=>r.Utilization??throw new Exception(r.Status)));
                Measure("24_tensioni_non_lineari",()=>actions.Sum(a=>engine!.Stress(a,"BENCH").sigma_acciaio));
                var sle=(JsonObject)workspace["sle"]!["SLE_QP"]!.DeepClone();sle["esposizione"]="XC1";sle["spaziatura_fessure"]="150";
                if(run==0)cases[name]!["sleOptions"]=sle.DeepClone();
                var linear=new CheckerSection(input,workspace,sle);
                Measure("24_tensioni_lineari_fessure",()=>sleActions.Sum(a=>{var state=linear.Stress(a,"SLE_QP");var crack=Ntc2018Checks.Cracking(linear,state,a,input,workspace,sle,"SLE_QP");return state.sigma_acciaio+(crack.Width??throw new Exception(crack.Status));}));
                Measure("curva_30_passi",()=>{var curve=new MomentCurvatureCalculator().Calculate(new(-500,0,30,.95),domain!.Check,a=>engine!.Stress(a,"BENCH"),engine!.Geometry.Fyd/engine.Geometry.Es);if(curve.Points.Count<31)throw new Exception(curve.Status);return curve.Points.Sum(p=>p.Curvature);});
            }
            Console.WriteLine("Benchmark completato: "+name);
        }
        var dlls=Directory.GetFiles(AppContext.BaseDirectory,"*.dll").Where(p=>Path.GetFileName(p).StartsWith("GPC")||Path.GetFileName(p).StartsWith("ANTHEA")||new[]{"DelaunayMesh.dll","GMsh.Net.dll","UnsafeEx.dll","MathNet.Numerics.dll"}.Contains(Path.GetFileName(p)))
            .Select(p=>new{File=Path.GetFileName(p),Version=FileVersionInfo.GetVersionInfo(p).FileVersion,Sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)))}).ToArray();
        var output=new{Schema=1,Utc=DateTime.UtcNow,Runtime=RuntimeInformation.FrameworkDescription,OS=RuntimeInformation.OSDescription,Architecture=RuntimeInformation.ProcessArchitecture.ToString(),LogicalProcessors=Environment.ProcessorCount,
            Notes="Tempi di processo senza UI; eseguire su stessa macchina senza altri calcoli. Run 0: primo uso del caso, non processo freddo indipendente. Allocazioni complessive del processo, incluse attività native gestite. Checksum diagnostico, non validazione ingegneristica.",Libraries=dlls,Cases=cases,Samples=samples,
            WarmSummary=samples.Where(s=>s.Run>0).GroupBy(s=>(s.Shape,s.Operation)).Select(g=>new{g.Key.Shape,g.Key.Operation,MedianMs=g.Select(s=>s.Milliseconds).Order().ElementAt(1),MinMs=g.Min(s=>s.Milliseconds),MaxMs=g.Max(s=>s.Milliseconds)})};
        File.WriteAllText(Path.Combine(directory,"benchmark.json"),JsonSerializer.Serialize(output,J.Options));
        string F(double v)=>v.ToString("G17",CultureInfo.InvariantCulture);
        File.WriteAllText(Path.Combine(directory,"benchmark.csv"),"forma;operazione;run;ms;bytes;checksum\n"+string.Join("\n",samples.Select(s=>$"{s.Shape};{s.Operation};{s.Run};{F(s.Milliseconds)};{s.AllocatedBytes};{F(s.Checksum)}")),Encoding.UTF8);
    }
}
