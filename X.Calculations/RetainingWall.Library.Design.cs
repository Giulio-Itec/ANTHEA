using System.Text.Json.Nodes;
using GPC.Design.Walls;

namespace Anthea.Calculations;

public static partial class RetainingWall
{
    static ReinforcementProposal DesignReinforcementNative(JsonObject input,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();var document=(JsonObject)input.DeepClone();Upgrade(document);
        if(document.S("family")!="cantilever")throw new ArgumentException("Il predimensionamento delle armature richiede un muro a mensola in c.a.");
        var original=Calculate(document,token);var options=document["detailing"]!;
        double countValue=options.Required("max_count");int maxCount=checked((int)countValue);
        if(countValue!=maxCount)throw new ArgumentException("Numero massimo di barre: inserire un intero.");
        double diameter=options.Required("max_diameter"),target=options.Required("target_ratio");
        var materialInput=MaterialSectionInput(document);var sectionDocument=SezioneCA.DefaultData();sectionDocument["input"]=materialInput.DeepClone();
        var workspace=SectionWorkspace.Prepare(sectionDocument);double fyd=ConcreteMaterials.DesignValues(materialInput,workspace).Fyd;
        var failures=new List<string>();
        foreach(var zone in Enum.GetValues<WallRebarZone>())
        {
            string key=NativeZoneKeys[zone];
            if(zone==WallRebarZone.StemUpper&&!document["reinforcement"].B("two_zones"))continue;
            var selected=original.Cases.Where(c=>c.Factors.S("purpose")!="Ribaltamento")
                .Select(c=>c with {Sections=c.Sections.Where(f=>ReinforcementKey(document,f.Name,f.Position)==key).ToList(),Curvatures=[]})
                .Where(c=>c.Sections.Count>0).ToList();
            if(selected.Count==0)continue;
            var actions=selected.SelectMany(c=>c.Sections).Select(f=>new WallDesignAction(f.M*WallMomentToNative,f.Thickness*Mm)).ToArray();
            IReadOnlyList<WallDesignCheck> Evaluate(WallDesignLayout layout)
            {
                var candidate=(JsonObject)document.DeepClone();Apply(candidate,key,layout,false);
                return StructuralChecksNative(candidate,selected.Select(c=>c with {Curvatures=[]}).ToList(),token,out _)
                    .Select(c=>new WallDesignCheck(c.Ratio,c.Combination=="Dettagli")).ToArray();
            }
            var search=WallReinforcementDesign.Search(zone,maxCount,diameter,target,fyd,document["geometry"]!.Required("stem_base")*Mm,actions,Evaluate,token);
            if(search.Selected is {} result)Apply(document,key,result,true);
            else failures.Add(key+": nessuna armatura verificata nei limiti scelti");
        }
        if(document["reinforcement"].B("two_zones"))
        {
            var reinforcement=document["reinforcement"]!;
            int count=WallReinforcementDesign.ContinuousStemCount(checked((int)reinforcement["stem"]!.Required("count")),checked((int)reinforcement["stem_upper"]!.Required("count")));
            reinforcement["stem"]!["count"]=count;reinforcement["stem_upper"]!["count"]=count;
        }
        document["detailing"]!["enabled"]=true;var final=Calculate(document,token);
        bool pass=failures.Count==0&&WallReinforcementDesign.Accepts(final.Structural.Select(c=>new WallDesignCheck(c.Ratio,c.Combination=="Dettagli")),target);
        return new(document,final.Structural,pass,pass?"Proposta verificata per le combinazioni inserite; controllare il dettaglio costruttivo prima dell’esecuzione."
            :string.Join("; ",failures.Append("Predimensionamento con controlli non soddisfatti o incompleti: consultare la tabella; può essere necessario aumentare gli spessori.")));
    }
    static void Apply(JsonObject document,string key,WallDesignLayout layout,bool secondary)
    {
        var arm=document["reinforcement"]![key]!;arm["diameter"]=layout.Diameter;arm["count"]=layout.Count;arm["symmetric"]=true;
        if(secondary){arm["secondary_diameter"]=layout.SecondaryDiameter;arm["secondary_spacing"]=layout.SecondarySpacing;}
    }
}
