using System.Text.Json.Nodes;
using GPC.Model.Materials;

namespace Anthea.Calculations;

public static partial class Ntc2018Checks
{
    /// <summary>Additional checks on the cavity boundary. Each wall/ring is checked independently;
    /// effective areas are never summed between faces. An unreinforced tensile face cannot pass.</summary>
    private static CrackResult InnerCracking(CrackResult outer, CheckerSection engine, CheckerStressState state, JsonObject options)
    {
        var s = engine.Geometry;
        if (s.Holes.Count == 0 || outer.Limit is not double limit || outer.Width is null) return outer;
        var plane = state.Native.StrainPlane;
        double E(double[] p) => plane.GetStrain(p[0], p[1]);
        if (s.Holes.SelectMany(h => h).Max(E) <= 1e-12) return outer with { Status = outer.Status + " · contorno interno compresso" };
        var details = outer.Details.ToList(); var regions = outer.Regions.ToList();
        var results = new List<CrackResult> { outer };
        var material = (ConcreteMaterialEuropeanCommon)engine.Section.ConcreteMaterial;
        double[][] Tension(double[][] polygon)
        {
            double gradient = double.Hypot(plane.ChiX, plane.ChiY);
            return gradient < 1e-15 ? polygon : SectionRegions.Clip(polygon, plane.ChiX/gradient, plane.ChiY/gradient, -plane.GetStrain(0,0)/gradient);
        }
        void Check(string name, double[][] outline, double[][][] holes, int[] indices, Func<Barra,double> cover, double effectiveDepth)
        {
            var p = Tension(outline); var h = holes.Select(Tension).Where(v => v.Length >= 3).ToArray();
            double area = SectionRegions.Area(p) - h.Sum(SectionRegions.Area), steel = indices.Sum(i => s.Bars[i].Area);
            if (area <= 1e-8) return;
            var region = new ConcreteEffectiveRegion(name, 0, 0, 0, area, indices, steel, null, p, h);
            regions.Add(region);
            if (steel <= 0)
            {
                results.Add(new(null,limit,null,null,name+": superficie del foro tesa senza armatura efficace")); return;
            }
            var bars = indices.Select(i => s.Bars[i]).ToArray();
            double sigma = indices.Max(i => state.tensioni_barre[i]);
            double phi = bars.Sum(b=>b.Diametro*b.Diametro)/bars.Sum(b=>b.Diametro);
            double c = options.S("copriferro_fessure").Trim()=="" ? bars.Min(cover) : options.Required("copriferro_fessure");
            double? spacing = options.S("spaziatura_fessure").Trim()=="" ? SpacingCalculator.Maximum(s,indices) : options.Required("spaziatura_fessure",strict:true);
            if (spacing is not >0) { results.Add(new(null,limit,null,null,name+": inserire l’interasse massimo per la superficie del foro")); return; }
            double[] strains = p.Select(E).ToArray();
            double k2 = strains.Max()>0 ? Math.Clamp((Math.Max(0,strains.Min())+strains.Max())/(2*strains.Max()),.5,1) : 1;
            double gradient = double.Hypot(plane.ChiX,plane.ChiY);
            double depth = gradient > 1e-15 ? strains.Max()/gradient : Math.Max(s.Width,s.Height);
            var trace = new List<CrackCalculationDetail>();
            trace.Add(new("hc,eff", effectiveDepth, "mm", "Fascia della parete/anello interno, limitata a metà spessore"));
            double width = ConcreteCodeChecks.CrackWidth(options.S("__normativa_fessure","NTC 2018"), sigma,s.Es,material.Ecm,material.Fctm,steel/area,phi,c,spacing.Value,depth,
                options.S("durata","Lunga")=="Breve",options.S("aderenza","Migliorata")=="Migliorata",k2,trace);
            details.Add(new(name+" · Ac,eff",area,"mm²","Area della fascia interna tesa, depurata del foro"));
            details.Add(new(name+" · As,eff",steel,"mm²",string.Join(", ",indices.Select(i=>$"B{i+1:00}"))));
            details.AddRange(trace.Select(d=>d with{Symbol=name+" · "+d.Symbol}));
            regions[^1] = region with { Width = width };
            results.Add(new(width,limit,width/limit,width<=limit,name,area,steel,spacing,"Geometria della superficie interna") { Details=trace.ToArray() });
        }
        var tensile = Enumerable.Range(0,s.Bars.Count).Where(i=>state.tensioni_barre[i]>0).ToArray();
        if (s.Shape == "Circolare")
        {
            double ri=s.Input.Required("inner_diameter_mm")/2, wall=s.Radius-ri;
            double nearest=tensile.Select(i=>double.Hypot(s.Bars[i].X,s.Bars[i].Y)-ri).DefaultIfEmpty(wall).Min();
            double hc=Math.Min((options.S("__normativa_fessure").StartsWith("DS")?2:2.5)*nearest,wall/2), radius=ri+hc;
            var outline=s.Holes[0].Select(p=>new[]{p[0]*radius/ri,p[1]*radius/ri}).Reverse().ToArray();
            var indices=tensile.Where(i=>double.Hypot(s.Bars[i].X,s.Bars[i].Y)<=radius+1e-8).ToArray();
            Check("Anello interno",outline,s.Holes.ToArray(),indices,b=>double.Hypot(b.X,b.Y)-ri-b.Diametro/2,hc);
        }
        else
        {
            foreach (var (name,qx,qy) in new[]{("Parete interna +x",1d,0d),("Parete interna −x",-1d,0d),("Parete interna +y",0d,1d),("Parete interna −y",0d,-1d)})
            {
                double Q(double x,double y)=>qx*x+qy*y;
                double inner=s.Holes[0].Max(p=>Q(p[0],p[1])),edge=s.Outline.Max(p=>Q(p[0],p[1])),wall=edge-inner;
                var face=s.Holes[0].Where(p=>Math.Abs(Q(p[0],p[1])-inner)<1e-8).ToArray();
                if(face.Max(E)<=1e-12)continue;
                double tangentMin=face.Min(p=>-qy*p[0]+qx*p[1]), tangentMax=face.Max(p=>-qy*p[0]+qx*p[1]);
                var candidates=tensile.Where(i=>Q(s.Bars[i].X,s.Bars[i].Y)>inner
                    && -qy*s.Bars[i].X+qx*s.Bars[i].Y>=tangentMin-1e-8
                    && -qy*s.Bars[i].X+qx*s.Bars[i].Y<=tangentMax+1e-8).ToArray();
                double nearest=candidates.Select(i=>Q(s.Bars[i].X,s.Bars[i].Y)-inner).DefaultIfEmpty(wall).Min();
                double hc=Math.Min((options.S("__normativa_fessure").StartsWith("DS")?2:2.5)*nearest,wall/2);
                double[][] Band(IReadOnlyList<double[]> p)=>SectionRegions.Clip(SectionRegions.Clip(
                    SectionRegions.Clip(SectionRegions.Clip(p,qx,qy,inner),-qx,-qy,-inner-hc),
                    -qy,qx,tangentMin),qy,-qx,-tangentMax);
                var indices=candidates.Where(i=>Q(s.Bars[i].X,s.Bars[i].Y)<=inner+hc+1e-8).ToArray();
                Check(name,Band(s.Outline),s.Holes.Select(Band).Where(h=>h.Length>=3).ToArray(),indices,b=>Q(b.X,b.Y)-inner-b.Diametro/2,hc);
            }
        }
        var governing=results.Where(r=>r.Width.HasValue).MaxBy(r=>r.Width)!;
        bool incomplete=results.Any(r=>r.Width is null);
        if (!ReferenceEquals(governing, outer))
        {
            details.Add(new("Superficie governante", null, "", governing.Status));
            details.AddRange(governing.Details);
            details.Add(new("Ac,eff", governing.EffectiveArea, "mm²", governing.Status));
            details.Add(new("As,eff", governing.EffectiveSteel, "mm²", governing.Status));
            details.Add(new("wk", governing.Width, "mm", "Inviluppo delle superfici esterne e interne"));
            details.Add(new("ηw", incomplete && governing.Width <= limit ? null : governing.Ratio, "−", "wk / wlim; esito sospeso se una superficie resta senza verifica"));
        }
        details.Add(new("Contorno interno",null,"","Fasce di parete/anello limitate a metà spessore. Inviluppo con il massimo σs; le fasce sono controlli indipendenti."));
        string missing=string.Join("; ",results.Where(r=>r.Width is null).Select(r=>r.Status));
        return governing with { Details=details.ToArray(), Regions=regions.ToArray(),
            Passed=governing.Width>limit?false:incomplete?null:true, Ratio=incomplete&&governing.Width<=limit?null:governing.Ratio,
            Status=incomplete?governing.Status+" · "+missing:governing.Status+" · controllate superfici esterne e interne" };
    }
}
