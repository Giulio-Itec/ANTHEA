using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Calculations;
using X.Core;

internal static class F4InputContractsCapture
{
    internal static int Run(string root,string output)
    {
        CultureInfo.CurrentCulture=CultureInfo.CurrentUICulture=CultureInfo.GetCultureInfo("it-IT");
        string target=Path.Combine(output,"input-contracts-before.json");
        if(File.Exists(target))throw new IOException("Capture already exists; never replace a before-port result.");
        var rows=new JsonArray();
        var wall=RetainingWall.Defaults();var material=RetainingWall.CoverMaterialState(wall);
        foreach(string shape in new[]{"Rettangolare","Circolare"})foreach(double cover in new[]{0d,40d,80d})
        {
            var input=SezioneCA.DefaultInput();input["shape"]=shape;input["cover_mm"]=cover;
            Add(new JsonObject{["id"]=shape+"-"+cover,["kind"]="cover",["input"]=input,["material"]=material.DeepClone(),["fck"]=30});
        }
        foreach(double cover in new[]{0d,80d})
        {
            var input=SezioneCA.DefaultInput();input["shape"]="Circolare";input["cover_mm"]=cover;input["longitudinal_bar_count"]=0;
            Add(new JsonObject{["id"]="missing-bars-"+cover,["kind"]="cover",["input"]=input,["material"]=material.DeepClone(),["fck"]=30});
        }
        foreach(bool inherited in new[]{false,true})foreach(double cover in new[]{0d,80d})
        {
            var archive=ProjectDocuments.CreateArchive();var project=ProjectDocuments.AddProject(archive);
            var cls=ProjectDocuments.AddSheet(project,"mat_calcestruzzo");cls["dati"]!["classe"]="C40/50";cls["dati"]!["esposizione_principale"]="XC4";
            var owner=inherited?ProjectDocuments.AddSection(project):project;
            var pile=ProjectDocuments.AddSheet(owner,"str_palo");pile["dati"]!["input"]!["cover_mm"]=cover;
            var sheet=ProjectDocuments.AddSheet(owner,RetainingWall.Module);sheet["dati"]!["materials"]!["cover"]=cover;
            Add(new JsonObject{["id"]="project-"+inherited+"-"+cover,["kind"]="project-cover",["input"]=project.DeepClone()});
        }
        var data=SezioneCA.DefaultData();var ws=SectionWorkspace.Prepare(data);
        data["input"]!["width_mm"]=400;data["input"]!["height_mm"]=600;data["input"]!["cover_mm"]=40;
        ws["sle_comuni"]!["esposizione"]="XC1";ws["dettagli_costruttivi"]!["elemento"]="Pilastro";
        ws["taglio"]!["azioni"]=new JsonArray(J.Obj(("nome","Taglio con compressione massima"),("N","-6000"),("Mx","0"),("My","0"),("Vx","0"),("Vy","0"),("T","0")));
        Add(new JsonObject{["id"]="ned-detailing-vs-design",["kind"]="ned",["input"]=data,
            ["actions"]=new JsonArray(J.Obj(("nome","SLU"),("N","-100"),("Mx","0"),("My","0")))});
        Directory.CreateDirectory(output);File.WriteAllText(target,new JsonObject{["capturedFrom"]="F4 before input consistency changes; see runner provenance",["cases"]=rows}.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"Captured {rows.Count} input contract cases before modification.");return 0;
        void Add(JsonObject row)
        {
            string before=row.ToJsonString();var actual=Evaluate(row);
            if(row.ToJsonString()!=before)throw new InvalidOperationException("Capture changed the input.");row["expected"]=actual;rows.Add(row);
        }
    }
    internal static JsonNode? Evaluate(JsonObject row)
    {
        var input=row["input"]!.AsObject();
        return row.S("kind") switch
        {
            "cover"=>JsonSerializer.SerializeToNode(ConcreteCoverAnalysis.Calculate(input,row["material"]!.AsObject(),row.Required("fck"))),
            "project-cover"=>JsonSerializer.SerializeToNode(ProjectValidation.CoverChecks(input)),
            "ned"=>Detailing(input,row["actions"]!.AsArray().OfType<JsonObject>().ToArray()),
            _=>throw new ArgumentException("Unknown contract case.")
        };
    }
    static JsonObject Detailing(JsonObject data,JsonObject[] actions)
    {
        var document=(JsonObject)data.DeepClone();var settings=SectionWorkspace.Prepare(document);var input=document["input"]!.AsObject();
        var ui=ConcreteDetailingAnalysis.Calculate(input,settings,actions);
        var design=ConcreteDetailingAnalysis.Calculate(input,settings,actions.Concat(settings["taglio"]!.Array("azioni").OfType<JsonObject>()));
        return new JsonObject{["ui"]=JsonSerializer.SerializeToNode(ui),["design"]=JsonSerializer.SerializeToNode(design)};
    }
}
