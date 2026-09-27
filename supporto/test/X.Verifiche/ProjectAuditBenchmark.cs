using System.Diagnostics;
using System.Text.Json.Nodes;
using X.Core;

internal static class ProjectAuditBenchmark
{
    internal static void Run(string file)
    {
        var project = ProjectDocuments.AddProject(ProjectDocuments.CreateArchive());
        // Build directly to measure comparison, not creation/inheritance.
        for (int i=0; i<24; i++)
        {
            var sheet = Archivio.Documento("str_palo"); sheet["id"] = "audit-"+i; sheet["nome"] = "Sezione "+i;
            SectionWorkspace.Prepare(sheet["dati"]!.AsObject()); sheet["dati"]!["input"]!["fck_mpa"] = 30 + i%2*5;
            project.Array("fogli").Add(sheet);
        }
        var samples = new JsonArray();
        void Measure(string operation, Func<double> action)
        {
            for(int run=0; run<5; run++)
            {
                long allocation = GC.GetTotalAllocatedBytes(true); var watch=Stopwatch.StartNew();
                double result=action(); watch.Stop();
                samples.Add(J.Obj(("operazione",operation),("run",run),("ms",watch.Elapsed.TotalMilliseconds),
                    ("bytes",GC.GetTotalAllocatedBytes(true)-allocation),("riscontro",result)));
            }
        }
        Measure("confronto_24_fogli",()=>ProjectSharedData.Differences(project).Count);
        Measure("piano_report_24_fogli",()=>new ProjectReportPlan(project).Conflicts.Count);
        var data=SezioneCA.DefaultData(); var settings=SectionWorkspace.Prepare(data); var input=data["input"]!.AsObject();
        var options=settings["sle"]!["SLE"]!.AsObject(); var session=new ConcreteAnalysisSession();
        var rows=Enumerable.Range(0,12).Select(i=>J.Obj(("id",i.ToString()),("N",-100-i*10),("Mx",40+i),("My",10))).ToArray();
        Measure("tensioni_12_righe_cache",()=>session.Stress(input,settings,options,"SLE",rows).Values.Sum(r=>r.State?.sigma_acciaio??throw new Exception(r.Status)));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        File.WriteAllText(file,J.Obj(("processori",Environment.ProcessorCount),("campioni",samples),
            ("nota","Run 0 include riscaldamento; run 1–4 per confronto. Stessa macchina, nessuna soglia temporale di validazione.")).ToJsonString(J.Options));
        Console.WriteLine("Benchmark completato: "+file);
    }
}
