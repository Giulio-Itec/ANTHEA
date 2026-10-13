using System.Globalization;
using System.Text.Json.Nodes;
using GPC.Checkers.Concrete.Walls;
using GPC.Checkers.Masonry;
using GPC.Design.Walls;

namespace Anthea.Calculations;

public static partial class RetainingWall
{
    private static string WallMemberText(WallMember member) => member switch
    {
        WallMember.Stem => "Fusto", WallMember.Toe => "Valle", WallMember.Heel => "Monte",
        _ => throw new ArgumentOutOfRangeException(nameof(member))
    };
    private static GravityWallMaterial NativeGravityMaterial(JsonObject d)
    {
        var p=d["gravity_design"]!;var m=d["materials"]!;
        switch(p.S("type"))
        {
            case "Resistenze assegnate":
                return GravityWallMaterial.Assigned(m.Required("compression_rd"),m.Required("tension_rd"),m.Required("shear_rd"));
            case "Calcestruzzo non armato":
                var input=MaterialSectionInput(d);var material=ConcreteMaterials.Concrete(input);
                var strengths=ConcreteMaterials.DesignValues(input,SectionWorkspace.Prepare(SezioneCA.DefaultData()));
                return GravityWallMaterial.From(new PlainConcreteWallMaterial(strengths.Fcd,material.Fctk05,input.Required("gamma_c"),material.Ecm,m.Required("creep")));
            case "Muratura":
                return GravityWallMaterial.From(new MasonryWallMaterial(AdvancedNumber(p,"fk",.01,100),AdvancedNumber(p,"gamma_m",1,5),
                    AdvancedNumber(p,"confidence",1,2),AdvancedNumber(p,"elastic_modulus",1,100000),m.Required("creep"),
                    AdvancedNumber(p,"fvk0",0,10),AdvancedNumber(p,"fvk_limit",.001,20)));
            default: throw new ArgumentException("Materiale del muro a gravità non riconosciuto.");
        }
    }
    private static List<Check> GravityChecksNative(JsonObject d,List<LoadCase> cases,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        GravityWallMaterial material;
        try { material=NativeGravityMaterial(d); }
        catch(ArgumentException ex) { return [CheckValue("Materiale muro a gravità","Dettagli",0,null,"MPa",ex.Message)]; }
        var p=d["gravity_design"]!;var g=d["geometry"]!;bool assigned=material.Kind==GravityWallMaterialKind.Assigned;
        double h=g.Required("height")*Mm;
        double effective=assigned||string.IsNullOrWhiteSpace(p["effective_height"]?.ToString())?2*h:AdvancedNumber(p,"effective_height")*Mm;
        if(effective==0)effective=2*h;
        if(effective<2*h)throw new ArgumentException("Gravità a mensola libera: la lunghezza efficace non può essere inferiore a 2H.");
        double forcePerWidth=WallForceToNative/Mm,momentPerWidth=WallMomentToNative/Mm;
        var inputs=cases.Select(c=>new GravityCombination(c.Name,c.State.StartsWith("SLE",StringComparison.Ordinal),c.Contact.Valid,
            c.Sections.Select(f=>new GravitySection(NativeWallMember(f.Name),f.Position*Mm,f.Thickness*Mm,f.N*forcePerWidth,f.V*forcePerWidth,f.M*momentPerWidth)).ToArray())).ToArray();
        var output=GravityWallAnalysis.Calculate(material,h,g.Required("stem_top")*Mm,effective,assigned?0:AdvancedNumber(p,"eccentricity",0,1000),inputs,token);
        var result=new List<Check>();var byName=cases.ToDictionary(c=>c.Name,StringComparer.Ordinal);
        foreach(var c in output)
        {
            foreach(var curve in c.Curvatures)byName[c.Id].Curvatures.Add(new(curve.Height/Mm,curve.Curvature*Mm));
            foreach(var check in c.Checks)
            {
                string member=check.Member is WallMember wm?WallMemberText(wm):"";
                double? position=check.Position/Mm;
                string label=assigned?member+" z="+position?.ToString("0.00",CultureInfo.InvariantCulture)+" m":member+$" z={position:0.00}";
                string suffix=check.Method switch
                {
                    GravityCheckMethod.ElasticStability=>"Gravità · stabilità elastica (EI minimo)",
                    GravityCheckMethod.AssignedCompression=>"compressione",
                    GravityCheckMethod.NoTension=>assigned?"assenza trazione":"assenza trazione / applicabilità rigidezza",
                    GravityCheckMethod.AssignedFoundationFlexure=>"flessione fondazione",GravityCheckMethod.AssignedShear=>"taglio",
                    GravityCheckMethod.CompressedBlock=>"pressoflessione · blocco compresso",GravityCheckMethod.MasonryJointShear=>"scorrimento giunto",
                    GravityCheckMethod.PlainConcreteShear=>"taglio NTC 4.1.11",GravityCheckMethod.FoundationCompression=>"compressione fondazione",
                    GravityCheckMethod.FoundationTension=>"trazione fondazione",GravityCheckMethod.FoundationShear=>"taglio fondazione",
                    GravityCheckMethod.Contact=>"resistenza",_=>throw new NotSupportedException("Verifica del muro a gravità non riconosciuta.")
                };
                string name=check.Method==GravityCheckMethod.ElasticStability?suffix:(assigned&&check.Method==GravityCheckMethod.Contact?member:label)+" · "+suffix;
                string unavailable=check.Method switch
                {
                    GravityCheckMethod.Contact=>assigned?"Reazioni non disponibili: perdita di equilibrio":"Contatto fondazione non disponibile",
                    GravityCheckMethod.CompressedBlock=>"Fuori campo: fessurazione o instabilità; necessario modello non lineare",
                    _=>"Non disponibile"
                };
                double Convert(double value)=>check.Unit switch {"N/mm"=>value/forcePerWidth,"Nmm/mm"=>value/momentPerWidth,"MPa"=>value,_=>throw new NotSupportedException("Unità della verifica a gravità non riconosciuta.")};
                string unit=check.Unit switch {"N/mm"=>"kN/m","Nmm/mm"=>"kNm/m",_=>check.Unit};
                bool unstamped=check.Member is null||assigned&&check.Method==GravityCheckMethod.Contact;
                result.Add(new(name,c.Id,Convert(check.Demand),check.Capacity is double capacity?Convert(capacity):null,unit,check.Ratio,
                    check.Capacity is null or <0?unavailable:NativeOutcomeText(check.Outcome,check.Capacity))
                    {Member=unstamped?null:member,Position=unstamped?null:position,Outcome=check.Outcome,Execution=check.Execution,Data=check.Data});
            }
        }
        return result;
    }
}
