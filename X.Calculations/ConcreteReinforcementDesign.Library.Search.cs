using System.Text.Json.Nodes;
using GPC.Design.Sections;
namespace Anthea.Calculations;

public static partial class ConcreteReinforcementDesign
{
    sealed record NativeCandidateDocument(JsonObject Input,JsonObject Shear,JsonObject Detailing);
    static SectionDesignCheck NativeCheck(ConcreteDesignCheck check)=>new(check.Key,check.Name,check.Combination,check.Ratio,check.Target,check.Passed,check.Detail);
    static ConcreteDesignCheck DisplayCheck(SectionDesignCheck check)=>new(check.Key,check.Name,check.Combination,check.Ratio,check.Target,check.Passed,check.Detail);
    static ConcreteDesignCandidate DisplayCandidate(SectionDesignCandidate<NativeCandidateDocument> candidate)=>new(candidate.Id,candidate.State.Input,candidate.State.Shear,
        candidate.State.Detailing,Describe(candidate.State.Input,candidate.State.Detailing),DisplayMetric(candidate.Metrics),candidate.Checks.Select(DisplayCheck).ToArray(),candidate.Score,candidate.Pareto);
    static ConcreteDesignResult OptimizeNative(JsonObject source,ConcreteDesignOptions options,CancellationToken token,IProgress<ConcreteDesignProgress>? progress)
    {
        var context=PrepareDesignInput(source,options,token);var input=context.Input;var workspace=context.Workspace;var rows=context.Rows;
        bool torsion=context.Torsion;var timers=new DesignTimers();
        bool circular=input.S("shape")=="Circolare";var objective=NativeObjective(options);
        bool keepLinks=options.Stirrups=="Mantieni"&&ConcreteCalculationSettings.HasStirrups(input);
        var plans=SectionDesignCatalogue.Create(new(circular,circular?input.Required("diameter_mm"):input.Required("width_mm"),circular?input.Required("diameter_mm"):input.Required("height_mm"),options.Symmetric,
            options.Stirrups switch {"Mantieni"=>SectionStirrupMode.Keep,"Senza staffe"=>SectionStirrupMode.None,"Ottimizza"=>SectionStirrupMode.Optimize,"Con e senza staffe"=>SectionStirrupMode.WithOrWithout,_=>throw new ArgumentException("Modalità staffe non riconosciuta.")},
            ConcreteCalculationSettings.HasStirrups(input),keepLinks?input.Required("transverse_bar_diameter_mm"):0,keepLinks?input.Required("transverse_spacing_mm"):0,
            options.Diameters,options.TopCounts,options.BottomCounts,options.SideCounts,options.CircularCounts,options.StirrupDiameters,options.StirrupSpacings),objective);
        ConcreteDesignMetrics? baseline=null;
        try{baseline=Metrics(new SezioneCA(input),workspace["taglio"]!.AsObject(),workspace["dettagli_costruttivi"]!.AsObject(),options);}catch(ArgumentException){}
        SectionDesignPreparation<NativeCandidateDocument> Prepare(int id,SectionDesignLayout layout,CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            var plan=new Layout(layout.Top,layout.Bottom,layout.Side,layout.TopPhi,layout.BottomPhi,layout.SidePhi,layout.Stirrups,layout.StirrupPhi,layout.Spacing);
            var ci=CandidateInput(input,plan);var cw=(JsonObject)workspace.DeepClone();var so=cw["taglio"]!.AsObject();var details=cw["dettagli_costruttivi"]!.AsObject();
            details["barre_trattenute"]="Da confermare";details["ancoraggio_appoggi"]="Da confermare";so["modello"]=plan.Stirrups?"Con staffe":"Senza staffe";
            if(ci.S("shape")!="Circolare")so["parametri"]="Automatici da sezione";
            var section=timers.Measure(0,()=>ConcreteCalculationSettings.UpdateAutomaticShear(ci,so));
            so["ancoraggio"]=options.AnchorageConfirmed?"Confermato":"Da verificare";so["chiusura_torsione"]=options.TorsionLayoutConfirmed?"Confermato":"Da confermare";
            if(torsion)so["as_torsione"]=SectionDesignQuantities.ReservedArea(section.AreaSteel,options.TorsionReservePercent);
            var dc=timers.Measure(1,()=>ConcreteDetailingAnalysis.Calculate(ci,cw,ConcreteDetailingAnalysis.AllRows(rows)));
            if(dc.DurabilityError!=null)throw new ArgumentException(dc.DurabilityError);
            if(details.S("elemento") is "Soletta piena" or "Parete")
            {
                SelectSecondary(section,so,details,dc.Checks,options);
                dc=timers.Measure(1,()=>ConcreteDetailingAnalysis.Calculate(ci,cw,ConcreteDetailingAnalysis.AllRows(rows)));
            }
            var checks=dc.Checks.Select(check=>new ConcreteDesignCheck("dettagli",check.Name,"Sezione proposta",null,1,check.Passed,
                $"{check.Actual:0.###} / {check.Limit:0.###} {check.Unit}. {check.Explanation} {check.Reference}")).ToList();
            SectionDesignPreparation<NativeCandidateDocument> Excluded(ConcreteDesignCheck check)=>new(null,check.Name,NativeCheck(check));
            var failed=checks.FirstOrDefault(c=>c.Passed==false);if(failed!=null)return Excluded(failed);
            var metrics=Metrics(section,so,details,options);if(metrics.Diameters>options.MaxDiameterKinds)return new(null,"Troppi diametri distinti");
            timers.Measure(2,()=>AnchorageChecks(ci,cw,options,checks));failed=checks.FirstOrDefault(c=>c.Passed==false);if(failed!=null)return Excluded(failed);
            foreach(var row in context.ShearRows)
            {
                cancellation.ThrowIfCancellationRequested();var r=timers.Measure(3,()=>ConcreteShearAnalysis.Calculate(ci,cw,so,row));
                AddRatio(checks,"vx",row.S("nome"),r.Shear[0].Ratio,options,$"VRd,x = {r.Shear[0].VRd:0.###} kN; cotθ = {r.Shear[0].CotTheta:0.###}");
                AddRatio(checks,"vy",row.S("nome"),r.Shear[1].Ratio,options,$"VRd,y = {r.Shear[1].VRd:0.###} kN; cotθ = {r.Shear[1].CotTheta:0.###}");
                if(r.Torsion is {} t)
                {
                    AddRatio(checks,"torsione",row.S("nome"),t.TorsionRatio,options,$"TRd = {t.TRd:0.###} kNm; As,T richiesta = {t.RequiredLongitudinalArea:0.###} mm²; riserva {options.TorsionReservePercent:0.#}% per barra.");
                    AddRatio(checks,"vt_cls",row.S("nome"),t.ConcreteCombinedRatio,options,t.Status);AddRatio(checks,"vt_staffe",row.S("nome"),t.SteelCombinedRatio,options,t.Status);
                    CheckTorsionDistribution(section,t,options,checks,row.S("nome"));
                }
                failed=checks.FirstOrDefault(c=>c.Passed==false);if(failed!=null)return Excluded(failed);
            }
            var key=(JsonObject)ci.DeepClone();key.Remove("transverse_spacing_mm");cw.Remove("taglio");cw.Remove("dettagli_costruttivi");
            return new(new(id,new(ci,so,details),layout,NativeMetric(metrics),checks.Select(NativeCheck).ToArray(),key.ToJsonString()));
        }
        IReadOnlyList<SectionDesignCheck> Longitudinal(NativeCandidateDocument candidate,CancellationToken cancellation)
        {
            var cw=(JsonObject)workspace.DeepClone();cw["taglio"]=candidate.Shear.DeepClone();cw["dettagli_costruttivi"]=candidate.Detailing.DeepClone();
            return timers.Measure(4,()=>LongitudinalChecks(candidate.Input,cw,rows,torsion,options,cancellation)).Select(NativeCheck).ToArray();
        }
        var search=SectionDesignSearch.Run(plans,options.MaxEvaluations,options.Workers,objective,Prepare,Longitudinal,token,
            progress==null?null:new NativeSectionProgress(progress));
        return new(search.Candidates.Select(DisplayCandidate).ToArray(),search.Evaluated,search.Planned,search.Completed,search.Exclusions,baseline,DesignNotes(torsion))
        {ExclusionExamples=search.ExclusionExamples.ToDictionary(p=>p.Key,p=>DisplayCheck(p.Value)),
            Performance=new(search.Seconds,search.FirstSolutionSeconds,options.Workers,search.LongitudinalCalculations,search.LongitudinalReuses,search.ProbeSections,timers.Results())};
    }
    sealed class NativeSectionProgress(IProgress<ConcreteDesignProgress> target):IProgress<SectionDesignProgress<NativeCandidateDocument>>
    {
        public void Report(SectionDesignProgress<NativeCandidateDocument> value)=>target.Report(new(value.Evaluated,value.Planned,value.Accepted,value.Best==null?null:DisplayCandidate(value.Best))
        {Phase=value.Stage switch{SectionSearchStage.Preliminary=>"Controlli preliminari: dettagli, ancoraggi, taglio",SectionSearchStage.Probes=>"Prove iniziali: estremi e valori intermedi",SectionSearchStage.Neighbours=>"Affinamento attorno alle prime soluzioni",SectionSearchStage.Completion=>"Completamento: resistenza SLU/SLV e SLE",_=>throw new ArgumentOutOfRangeException()},
            Workers=value.Workers,Seconds=value.Seconds,PreliminaryChecked=value.PreliminaryChecked});
    }
}
