using System.Text.Json.Nodes;
using GPC.Model.Materials;

namespace Anthea.Calculations;

public static partial class Ntc2018Checks
{
    /// <summary>Relative strain variation |∇ε|·h/εmax over the section below which the inner bands are in uniform tension (k₂ = 1): the
    /// solver returns gradients of 1e-15…1e-12 1/mm for axial tension, whose direction means nothing; 1e-4 is an eccentricity of about h/120000.</summary>
    private const double UniformTensionTolerance = 1e-4;

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
        // Tensile depth h − x of a band (EN 1992-1-1 7.3.4(3), eq. (7.14); NTC Δsm,distante; R15): h − x = min[εmax/|∇ε|; h of the
        // section along the gradient]. εmax/|∇ε| is measured from the neutral axis; with the neutral axis inside the section it does not
        // exceed the height along the gradient (the min only guards rounding, the rule before R15 is unchanged there); with the neutral
        // axis outside (x = 0) the bound acts where εmax/|∇ε| exceeds that height, that is for the bands far from the neutral axis and for
        // every band close to uniform tension; a band near the less tensioned side keeps εmax/|∇ε|. A gradient negligible against the
        // strain (|∇ε|·h ≤ 1e-4 εmax: solver noise, ≈ 1e-15…1e-12 1/mm, has no direction) is uniform tension: the whole band is tensile,
        // k₂ = 1 and h − x = h of the section normal to the face (diameter for the ring).
        // Continuity: where the neutral axis enters the section εmax/|∇ε| tends to the height along the gradient, so h − x and wk of the
        // bands are continuous there (straight and biaxial bending, square or not). Residual jump: at the uniform-tension threshold, a
        // negligible eccentricity, h − x passes from the height normal to the face to the height along the gradient whenever they differ,
        // i.e. in non-square boxes (box 400 × 600 of the Checker tests, gradient along y: ±x bands 400 mm below, 600 mm above, NTC
        // Δsm,distante and EN sr,max +50 %) and with an oblique gradient, also in square boxes (X.Verifiche (k): box 1000 × 1000, moment along
        // (0,6; 0,8), every band 1000 → 1372.5 mm, section wk +37 %). In the ring only the polygon: h along the gradient between D cos(π/n) and D.
        // Decision of the user (7/10/2026, R15): "Altezza lungo il gradiente". The intermediate rule of 76a2062 (height normal to the face
        // in a fully tensioned section) was continuous at the threshold but jumped at the entry of the neutral axis (square box 1000 × 1000
        // with an oblique moment: +x band 1000 → 1183.68 mm, section wk +18.4 %) and has been withdrawn.
        // Before 7/10/2026 εmax/|∇ε| was used unbounded (≈ 1e12 mm).
        double gradient = double.Hypot(plane.ChiX, plane.ChiY);
        double gx = gradient > 0 ? plane.ChiX/gradient : 0, gy = gradient > 0 ? plane.ChiY/gradient : 0;
        double sectionAlongGradient = s.Outline.Max(p => gx*p[0]+gy*p[1]) - s.Outline.Min(p => gx*p[0]+gy*p[1]);
        bool uniform = gradient < 1e-15 || gradient*sectionAlongGradient <= UniformTensionTolerance*s.Outline.Max(E);
        double[][] Tension(double[][] polygon) => uniform ? polygon : SectionRegions.Clip(polygon, gx, gy, -plane.GetStrain(0,0)/gradient);
        void Check(string name, double[][] outline, double[][][] holes, int[] indices, Func<Barra,double> cover, double effectiveDepth, double uniformDepth)
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
            double k2 = uniform ? 1 : strains.Max()>0 ? Math.Clamp((Math.Max(0,strains.Min())+strains.Max())/(2*strains.Max()),.5,1) : 1;
            double fromNeutralAxis = uniform ? double.PositiveInfinity : strains.Max()/gradient;
            double depth = uniform ? uniformDepth : Math.Min(fromNeutralAxis, sectionAlongGradient);
            var trace = new List<CrackCalculationDetail>();
            trace.Add(new("hc,eff", effectiveDepth, "mm", "Fascia della parete/anello interno, limitata a metà spessore"));
            // Written only where the bound acts, so that the trace of the other cases is unchanged.
            if (uniform) trace.Add(new("h − x della fascia", depth, "mm", "Trazione uniforme: h della sezione normale alla faccia",
                $"|∇ε|·h = {gradient*sectionAlongGradient:G3} ≤ {UniformTensionTolerance:G1}·εmax: gradiente trascurabile, senza direzione; fascia tutta tesa, k₂ = 1 (EN 1992-1-1 7.3.4(3), eq. (7.14))."));
            else if (fromNeutralAxis > sectionAlongGradient) trace.Add(new("h − x della fascia", depth, "mm", "min[εmax/|∇ε|; h lungo il gradiente]",
                $"εmax/|∇ε| = {fromNeutralAxis:G6} mm oltre l'altezza della sezione lungo il gradiente: asse neutro fuori dalla sezione, x = 0 (EN 1992-1-1 7.3.4(3), eq. (7.14))."));
            // Local check: k₂ from the strains of the tensile part of the band, not the Criterio k₂ of the whole section.
            // Not written with the legacy rule of NtcK2FromCompressedBars, whose trace had no such entry (local copy: no unreachable branch).
            bool legacyK2 = NtcK2FromCompressedBars;
            if (!legacyK2) trace.Add(uniform ? new("k₂ della fascia", k2, "−", "Trazione uniforme: 1", "Gradiente trascurabile rispetto alla deformazione (EN 1992-1-1 7.3.4(3), eq. (7.13) con ε1 = ε2).")
                : new("k₂ della fascia", k2, "−", "(εmax + εmin)/(2 εmax) ai vertici della parte tesa della fascia, limitato fra 0,50 e 1",
                "Distribuzione locale delle deformazioni (EN 1992-1-1 7.3.4(3), aree locali): 0,50 se l'asse neutro taglia la fascia, oltre 0,50 se la fascia è tutta tesa. Indipendente dal Criterio k₂ della sezione."));
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
            Check("Anello interno",outline,s.Holes.ToArray(),indices,b=>double.Hypot(b.X,b.Y)-ri-b.Diametro/2,hc,Math.Max(s.Width,s.Height));
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
                Check(name,Band(s.Outline),s.Holes.Select(Band).Where(h=>h.Length>=3).ToArray(),indices,b=>Q(b.X,b.Y)-inner-b.Diametro/2,hc,edge-s.Outline.Min(p=>Q(p[0],p[1])));
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
