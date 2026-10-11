using System.Diagnostics;
using System.Numerics;
using System.Text.Json;


if(args.Length<2)throw new ArgumentException("Arguments: source directory, new output directory, optional reference plate table; --pilot uses the reference table as its source.");
string source=Path.GetFullPath(args[0]), output=Path.GetFullPath(args[1]);
Directory.CreateDirectory(output);
if(File.Exists(Path.Combine(output,"benchmark.json"))||File.Exists(Path.Combine(output,"validation.json")))throw new IOException("Use a new output directory to preserve prior measurements.");
bool pilot=args.Contains("--pilot");
string? referencePath=args.Skip(2).FirstOrDefault(a=>!a.StartsWith("--"));
var definitions=CombinationExperiment.Definitions(Path.Combine(source,"LCOM-CONC.json"));
using var casesDoc=JsonDocument.Parse(File.ReadAllText(Path.Combine(source,"STLD.json")));
var names=casesDoc.RootElement.GetProperty("STLD").EnumerateObject().Select(p=>p.Value.GetProperty("NAME").GetString()!).ToArray();
DenseFields data=pilot?TableData.Read([(referencePath??throw new ArgumentException("Pilot requires a reference table."),"PLATE")])
    :TableData.Read(Directory.EnumerateFiles(source,"plate-results-base-*.json").Order().Select(p=>(p,"PLATE"))
        .Concat(Directory.EnumerateFiles(source,"beam-results-base-*.json").Order().Select(p=>(p,"BEAM"))));
var basis=names.ToDictionary(n=>n,n=>data.Cases.TryGetValue(n,out var a)?a:throw new InvalidDataException("Missing static case: "+n),StringComparer.Ordinal);
if(!pilot)
{
    using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(source,"ELEM.json")));
    var elements=doc.RootElement.GetProperty("ELEM").EnumerateObject().Select(p=>(Id:int.Parse(p.Name),Data:p.Value)).ToArray();
    var expected=new HashSet<SampleKey>();
    foreach(var e in elements)
    {
        string type=e.Data.GetProperty("TYPE").GetString()!;
        int[] nodes=e.Data.GetProperty("NODE").EnumerateArray().Select(n=>n.GetInt32()).Where(n=>n>0).ToArray();
        if(type=="PLATE")foreach(int n in nodes)foreach(string c in TableData.PlateComponents)expected.Add(new(type,e.Id,n.ToString(),c));
        else if(type=="BEAM")foreach(string part in new[]{"I["+nodes[0]+"]","1/4","2/4","3/4","J["+nodes[1]+"]"})foreach(string c in TableData.BeamComponents)expected.Add(new(type,e.Id,part,c));
        else throw new InvalidDataException("Unsupported element family: "+type);
    }
    if(!expected.SetEquals(data.Keys))throw new InvalidDataException("Incomplete or foreign result samples in source acquisition.");
    if(data.Cases.Count!=names.Length)throw new InvalidDataException("Unexpected non-static result cases.");
}
Console.WriteLine($"{names.Length} static cases, {data.Keys.Length:N0} scalars/case, {definitions.Count} definitions; JSON read/index {data.ReadSeconds:F3} s.");
var engine=new CombinationExperiment(basis,definitions);
var firstWatch=Stopwatch.StartNew(); var first=engine.Evaluate("SLU_Q1_1"); double firstMs=firstWatch.Elapsed.TotalMilliseconds;
var checks=new List<object>(); int failed=0,compared=0,skipped=0;
if(referencePath!=null)
{
    var reference=pilot?data:Directory.Exists(referencePath)
        ?TableData.Read(Directory.EnumerateFiles(referencePath,"plate-reference-*.json").Order().Select(p=>(p,"PLATE")).Concat(Directory.EnumerateFiles(referencePath,"beam-reference-*.json").Order().Select(p=>(p,"BEAM"))))
        :TableData.Read([(referencePath,"PLATE")]);
    var index=data.Keys.Select((k,i)=>(k,i)).ToDictionary(p=>p.k,p=>p.i);
    var indices=reference.Keys.Select(k=>index.TryGetValue(k,out var n)?n:throw new InvalidDataException("Reference sample missing in base data.")).ToArray();
    foreach(var (label,expected) in reference.Cases)
    {
        bool minimum=label.EndsWith("(min)",StringComparison.Ordinal),maximum=label.EndsWith("(max)",StringComparison.Ordinal);
        bool baseCase=basis.ContainsKey(label);
        if(!minimum&&!maximum&&!baseCase) { skipped++; continue; }
        var actual=baseCase?basis[label]:minimum?engine.Evaluate(label[..^5]).Min:engine.Evaluate(label[..^5]).Max;
        double largestScaledError=0,largestAbsoluteError=0; int failures=0;
        for(int i=0;i<expected.Length;i++)
        {
            double error=Math.Abs(actual[indices[i]]-expected[i]);
            // Tolerance fixed before comparison; source exports contain twelve scientific decimals.
            if(error>1e-6+1e-9*Math.Abs(expected[i]))failures++;
            largestAbsoluteError=Math.Max(largestAbsoluteError,error);
            largestScaledError=Math.Max(largestScaledError,error/Math.Max(1,Math.Abs(expected[i])));
        }
        compared+=expected.Length; failed+=failures;
        checks.Add(new{Case=label,Samples=expected.Length,Failures=failures,MaxAbsoluteError=largestAbsoluteError,MaxScaledError=largestScaledError});
    }
}
if(args.Contains("--validate-only"))
{
    File.WriteAllText(Path.Combine(output,"validation.json"),JsonSerializer.Serialize(new{ComparedScalars=compared,FailedScalars=failed,SkippedDerivedLabels=skipped,ReferenceChecks=checks},new JsonSerializerOptions{WriteIndented=true}));
    Console.WriteLine($"Compared {compared} reference scalars; {failed} differences outside fixed tolerance.");
    if(failed>0)Environment.ExitCode=2;
    return;
}
var timings=new List<object>(); double checksum=0;
object Measure(string label,Action operation,int repetitions=31)
{
    operation();operation();operation(); var values=new double[repetitions];
    for(int i=0;i<repetitions;i++){var w=Stopwatch.StartNew();operation();values[i]=w.Elapsed.TotalMilliseconds;}
    var sorted=values.Order().ToArray(); var result=new{Operation=label,Repetitions=repetitions,MedianMs=sorted[repetitions/2],P95Ms=sorted[(int)Math.Ceiling(.95*repetitions)-1],MinimumMs=sorted[0],MaximumMs=sorted[^1],SamplesMs=values};
    Console.WriteLine($"{label}: median {result.MedianMs:F3} ms; P95 {result.P95Ms:F3} ms"); return result;
}
foreach(string name in new[]{"T_0","SLU_Q1_1","SLV_X-_1","ENV_SLU","ENV_SLV"})
    timings.Add(Measure(name+" / all local components / from base cases",()=>{engine.Clear();var value=engine.Evaluate(name);checksum+=value.Max[0]+value.Min[^1];}));
timings.Add(Measure("All 73 definitions / min and max / from base cases",()=>{engine.Clear();foreach(string name in definitions.Keys){var v=engine.Evaluate(name);checksum+=v.Max[0];}},21));
int[] fieldIndices=data.Keys.Select((k,i)=>(k,i)).Where(p=>p.k.Family=="PLATE"&&p.k.Component=="Myy").Select(p=>p.i).ToArray();
var singleBasis=basis.ToDictionary(p=>p.Key,p=>fieldIndices.Select(i=>p.Value[i]).ToArray(),StringComparer.Ordinal);
var singleEngine=new CombinationExperiment(singleBasis,definitions);
foreach(string name in new[]{"SLU_Q1_1","ENV_SLU"})
    timings.Add(Measure(name+" / Myy only / from base cases",()=>{singleEngine.Clear();var v=singleEngine.Evaluate(name);checksum+=v.Max[0];}));
var all=engine.Evaluate("ENV_SLU");
timings.Add(Measure("Existing envelope / copy Myy field and determine visible range",()=>{var values=fieldIndices.Select(i=>all.Max[i]).ToArray(); checksum+=values.Min()+values.Max();}));
var cachePath=Path.Combine(output,"base-fields.bin");
var cacheWatch=Stopwatch.StartNew();
using(var stream=File.Create(cachePath))using(var writer=new BinaryWriter(stream))
{
    writer.Write("ANTHEA experiment; normalized ordering; original N/mm units");writer.Write(names.Length);writer.Write(data.Keys.Length);
    foreach(var key in data.Keys){writer.Write(key.Family);writer.Write(key.Element);writer.Write(key.Position);writer.Write(key.Component);}
    foreach(string name in names){writer.Write(name);foreach(double value in basis[name])writer.Write(value);}
}
double cacheWriteSeconds=cacheWatch.Elapsed.TotalSeconds; cacheWatch.Restart();
using(var stream=File.OpenRead(cachePath))using(var reader=new BinaryReader(stream))
{
    reader.ReadString();int cases=reader.ReadInt32(),count=reader.ReadInt32();
    for(int i=0;i<count;i++){reader.ReadString();reader.ReadInt32();reader.ReadString();reader.ReadString();}
    for(int i=0;i<cases;i++){string name=reader.ReadString();var values=new double[count];for(int j=0;j<count;j++)values[j]=reader.ReadDouble();if(!values.SequenceEqual(basis[name]))throw new InvalidDataException("Binary cache changed values.");}
}
double cacheReadSeconds=cacheWatch.Elapsed.TotalSeconds;
var resultReport=new{
    Experiment="Prototype only; componentwise Add/Envelope, no UI/GPU, no principal reconstruction or CBall semantics",
    Pilot=pilot,Runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,OperatingSystem=Environment.OSVersion.ToString(),
    LogicalProcessors=Environment.ProcessorCount,VectorWidth=Vector<double>.Count,Simd=Vector.IsHardwareAccelerated,
    StaticCases=names.Length,Definitions=definitions.Count,DefinitionsByType=definitions.Values.GroupBy(d=>d.Type).ToDictionary(g=>g.Key,g=>g.Count()),
    ScalarsPerCase=data.Keys.Length,SourceJsonBytes=data.Bytes,DenseBaseBytes=(long)names.Length*data.Keys.Length*sizeof(double),
    JsonReadSeconds=data.ReadSeconds,FirstCombinationMs=firstMs,CacheBytes=new FileInfo(cachePath).Length,CacheWriteSeconds=cacheWriteSeconds,CacheReadAndVerifySeconds=cacheReadSeconds,
    ComparedScalars=compared,FailedScalars=failed,SkippedDerivedLabels=skipped,ReferenceChecks=checks,Timings=timings,Checksum=checksum,
    PeakWorkingSetBytes=Process.GetCurrentProcess().PeakWorkingSet64
};
File.WriteAllText(Path.Combine(output,"benchmark.json"),JsonSerializer.Serialize(resultReport,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"Compared {compared} reference scalars; {failed} differences outside fixed tolerance. Cache {new FileInfo(cachePath).Length:N0} bytes.");
if(failed>0)Environment.ExitCode=2;
