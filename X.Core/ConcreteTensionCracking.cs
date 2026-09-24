using System.Text.Json.Nodes;
using GPC.Geometry;
using GPC.Model.Materials;
namespace X.Core;
public static partial class Ntc2018Checks
{
    private static CrackResult CrackSurfaceScope(CrackResult result,SezioneCA section)
    {
        if(section.Holes.Count==0)return result;
        bool exceeded=result.Width>result.Limit;
        return result with{Passed=exceeded?false:null,Ratio=exceeded?result.Ratio:null,
            Status=result.Status+" · fasce del contorno esterno; superficie del foro da verificare",
            Details=result.Details.Append(new CrackCalculationDetail("Superficie interna",null,"","Il foro è sottratto da Ac,eff. Il risultato non completa il controllo dell’apertura sulla sua superficie interna.")).ToArray()};
    }
    private static CrackResult FullyTensionedCracking(CheckerSection engine,CheckerStressState state,JsonObject input,
        JsonObject options,double limit,List<CrackCalculationDetail> details)
    {
        var section=engine.Geometry;var plane=state.Native.StrainPlane;var material=(ConcreteMaterialEuropeanCommon)engine.Section.ConcreteMaterial;
        var regions=new List<ConcreteEffectiveRegion>();var widths=new List<double>();var formulae=new List<List<CrackCalculationDetail>>();
        var strains=section.Outline.Select(p=>plane.GetStrain(p[0],p[1])).ToArray();
        double maxStrain=strains.Max(),k2=maxStrain>0?Math.Clamp((strains.Min()+maxStrain)/(2*maxStrain),.5,1):1;
        details.RemoveAll(d=>d.Symbol=="Criterio k₂");
        details.Add(new("Criterio k₂",k2,"−","Trazione non uniforme: (εmax + εmin)/(2 εmax); uniforme: 1"));
        details.Add(new("k₂ · interamente tesa",k2,"−","(εmax + εmin)/(2 εmax); trazione uniforme: 1","EC2 §7.3.4; sostituisce il criterio binario per questo ramo."));
        // Opposite faces are independent; never add effective areas belonging to different directions.
        var faces=new List<(string Name,double X,double Y)>{("Faccia +x",1,0),("Faccia −x",-1,0),("Faccia +y",0,1),("Faccia −y",0,-1)};
        if(section.Shape=="Circolare")
        {
            faces.Clear();
            var angles=section.Bars.Select(b=>Math.Atan2(b.Y,b.X)).Concat(new[]{Math.Atan2(plane.ChiY,plane.ChiX),Math.Atan2(plane.ChiY,plane.ChiX)+Math.PI}).ToArray();
            foreach(double angle in angles)
            {double x=Math.Cos(angle),y=Math.Sin(angle);if(!faces.Any(f=>double.Hypot(f.X-x,f.Y-y)<1e-8))faces.Add(($"Fascia radiale {angle*180/Math.PI:0.##}°",x,y));}
        }
        foreach(var(name,qx,qy) in faces)
        {
            double Q(double x,double y)=>qx*x+qy*y;
            double top=section.Outline.Max(p=>Q(p[0],p[1])),bottom=section.Outline.Min(p=>Q(p[0],p[1])),height=top-bottom;
            double edge=section.Bars.Max(b=>Q(b.X,b.Y));
            var face=section.Bars.Where(b=>Q(b.X,b.Y)>=edge-section.Bars.Max(b=>b.Diametro)).ToArray();
            double center=face.Sum(b=>Q(b.X,b.Y)*b.Area)/face.Sum(b=>b.Area);
            double hc=Math.Min(2.5*(top-center),height/2),level=top-hc;
            var indices=section.Bars.Select((b,i)=>(b,i)).Where(v=>Q(v.b.X,v.b.Y)>=level-1e-8&&state.tensioni_barre[v.i]>0).Select(v=>v.i).ToArray();
            var region=SectionRegions.Region(section,name,qx,qy,level,indices);
            details.Add(new(name,null,"","Verifica indipendente della fascia; nessuna somma con le aree delle altre facce."));
            details.Add(new(name+" · hc,eff",hc,"mm","min[2,5(h−d); h/2] per sezione interamente tesa"));
            details.Add(new(name+" · Ac,eff",region.Area,"mm²","Area del contorno tagliato, sottratti gli eventuali fori"));
            details.Add(new(name+" · As,eff",region.SteelArea,"mm²",string.Join(", ",indices.Select(i=>$"B{i+1:00}"))));
            if(region.Area<=0||region.SteelArea<=0)return new(null,limit,null,null,name+": area o armatura efficace assente"){Details=details.ToArray(),Regions=regions.Append(region).ToArray()};
            var bars=indices.Select(i=>section.Bars[i]).ToArray();
            double phi=bars.Sum(b=>b.Diametro*b.Diametro)/bars.Sum(b=>b.Diametro);
            double cover=options.S("copriferro_fessure").Trim()==""?bars.Min(b=>top-Q(b.X,b.Y)-b.Diametro/2):options.Required("copriferro_fessure");
            double? spacing=options.S("spaziatura_fessure").Trim()==""?SpacingCalculator.Maximum(section,indices):options.Required("spaziatura_fessure",strict:true);
            if(spacing is not >0)return new(null,limit,null,null,name+": specificare l’interasse massimo delle barre"){Details=details.ToArray(),Regions=regions.Append(region).ToArray()};
            double sigma=indices.Max(i=>state.tensioni_barre[i]);
            var calculation=new List<CrackCalculationDetail>();
            double width=CalculateCrackWidth(sigma,section.Es,material.Ecm,material.Fctm,region.SteelArea/region.Area,phi,cover,spacing.Value,height,
                options.S("durata","Lunga")=="Breve",options.S("aderenza","Migliorata")=="Migliorata",k2,calculation);
            details.AddRange(calculation.Select(d=>d with{Symbol=name+" · "+d.Symbol}));formulae.Add(calculation);
            regions.Add(region with{Width=width});widths.Add(width);
        }
        int governing=widths.IndexOf(widths.Max());var g=regions[governing];double w=widths[governing];
        details.Add(new("Faccia governante",w,"mm",g.Name));
        details.AddRange(formulae[governing]);details.Add(new("Ac,eff",g.Area,"mm²",g.Name));details.Add(new("As,eff",g.SteelArea,"mm²",g.Name));details.Add(new("ηw",w/limit,"−","wk / wlim"));
        var governingSpacing=formulae[governing].FirstOrDefault(d=>d.Symbol=="s")?.Value;
        return new(w,limit,w/limit,w<=limit,"Interamente tesa · "+g.Name+(w<=limit?" · apertura entro limite":" · apertura oltre limite"),g.Area,g.SteelArea)
            {Details=details.ToArray(),Regions=regions.ToArray(),BarSpacing=governingSpacing,SpacingSource=options.S("spaziatura_fessure").Trim()==""?"Automatico geometrico":"Manuale"};
    }
}
