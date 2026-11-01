using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.ConcreteWorkspace;
using GPC.Model.Actions.Prestressing;
using GPC.Model.Materials;
using GPC.Model.Sections.Concrete;

namespace Anthea.Application.Actions;

public sealed record PrestressStation(int Index,double XM,double EccentricityM,double JackingMpa,double InitialMpa,double EffectiveMpa,double AreaMm2)
{
    public string Label=>"x = "+XM.ToString("G7",CultureInfo.CurrentCulture)+" m";
    public string Description=>$"{Label} · e = {EccentricityM:G5} m · Ap = {AreaMm2:G7} mm² · σjack = {JackingMpa:G7} MPa · σpm0 = {InitialMpa:G7} MPa · σeff = {EffectiveMpa:G7} MPa";
}
public sealed record PrestressTarget(string Id,string Label);

public static partial class ClimateDocument
{
    public const string Prestressing="act_precompressione";
    private static readonly IReadOnlyDictionary<string,string> PrestressCaptions=new Dictionary<string,string>
    {
        ["Ntc2018"]="NTC 2018 · perdite con modelli EC2",["EurocodeFirstGeneration"]="EN 1992-1-1:2004 · prima generazione",
        ["PreTensioned"]="Preteso · banco rettilineo",["PostTensionedBonded"]="Posteso aderente",["PostTensionedUnbonded"]="Posteso interno non aderente",
        ["Straight"]="Rettilineo",["Parabolic"]="Parabolico",["Left"]="Tiro da sinistra",["Right"]="Tiro da destra",["BothSymmetric"]="Bilaterale simultaneo e simmetrico",
        ["OrdinaryWireStrand"]="Classe 1 · fili / trefoli ordinari",["LowRelaxationWireStrand"]="Classe 2 · basso rilassamento",["HotRolledBar"]="Classe 3 · barre laminate"
    };
    private static JsonObject CalculatePrestress(PrestressActionsInput input)
    {
        var calculated=GPC.Design.Actions.Prestressing.PrestressActions.Calculate(input);
        var result=JsonSerializer.SerializeToNode(calculated.Summary,Options)!.AsObject();
        result["Prestress"]=JsonSerializer.SerializeToNode(calculated,Options);result["PrestressInput"]=JsonSerializer.SerializeToNode(input,Options);return result;
    }
    private static string? PrestressLabel(string key)=>key switch
    {
        "Method"=>"Sistema di precompressione","Profile"=>"Profilo longitudinale del cavo","Jacking"=>"Estremità attiva / modalità di tiro","LengthM"=>"Lunghezza orizzontale L|m",
        "StartEccentricityM"=>"Eccentricità iniziale z (+ verso l’alto)|m","MiddleEccentricityM"=>"Eccentricità in mezzeria z|m","EndEccentricityM"=>"Eccentricità finale z|m",
        "TendonAreaMm2"=>"Area totale del cavo / famiglia equivalente Ap|mm²","TendonModulusMpa"=>"Modulo elastico del cavo Ep|MPa","FpkMpa"=>"Resistenza caratteristica fpk|MPa","Fp01kMpa"=>"Tensione caratteristica fp0,1k|MPa",
        "JackingStressMpa"=>"Tensione al martinetto σjack|MPa","FrictionCoefficient"=>"Coefficiente di attrito μ","WobbleRadM"=>"Deviazione involontaria k (dentro μ)|rad/m","AnchorageSlipMm"=>"Rientro di ciascun ancoraggio attivo|mm",
        "SystemSource"=>"Fonte del sistema: ETA, materiali, attrito e ancoraggi","JackingAgeDays"=>"Età al tiro (uguale al trasferimento nel posteso)|giorni","TransferAgeDays"=>"Età del trasferimento al calcestruzzo|giorni","SuccessiveTendons"=>"Cavi identici tesati in successione n",
        "ConcreteStressChangeMpa"=>"Incremento compressivo nel cls Δσc (uniforme)|MPa","ConcreteQpStressMpa"=>"Compressione nel cls σc,QP (uniforme)|MPa","TendonQpStressIncrementMpa"=>"Incremento nel cavo da carichi QP (0: σp,QP=σpm0)|MPa","ConcreteStressSource"=>"Fonte delle tensioni dal modello strutturale",
        "ConcreteAreaMm2"=>"Area del calcestruzzo Ac|mm²","ConcreteInertiaMm4"=>"Inerzia del cls nel piano del cavo Ic|mm⁴","Relaxation"=>"Classe di rilassamento EN",
        "CertifiedRelaxation"=>"Usa ρ1000 da certificato","CertifiedRho1000Percent"=>"Rilassamento certificato ρ1000|%","IncludeDeferredLosses"=>"Applica le perdite differite accoppiate",
        "EnJackingFpkFactor"=>"EN · k1 al tiro (su fpk)","EnJackingFp01Factor"=>"EN · k2 al tiro (su fp0,1k)","EnInitialFpkFactor"=>"EN · k7 iniziale (su fpk)","EnInitialFp01Factor"=>"EN · k8 iniziale (su fp0,1k)","ProfileIntervals"=>"Intervalli del cavo (pari, verifica con griglia doppia)",_=>TimeLabel(key)
    };
    private static bool PrestressVisible(string key,JsonObject values)=>key switch
    {
        "NationalParameters" or "EnJackingFpkFactor" or "EnJackingFp01Factor" or "EnInitialFpkFactor" or "EnInitialFp01Factor"=>values["Standard"]?.ToString()=="EurocodeFirstGeneration",
        "MiddleEccentricityM"=>values["Profile"]?.ToString()=="Parabolic",
        "FrictionCoefficient" or "WobbleRadM" or "SuccessiveTendons"=>values["Method"]?.ToString()!="PreTensioned",
        "CertifiedRho1000Percent"=>values["CertifiedRelaxation"]?.GetValue<bool>()==true,_=>true
    };
    private static JsonObject? PrestressStage(JsonObject? result,int selected)=>result?["Prestress"]?["Stages"] is JsonArray stages&&selected>=0&&selected<stages.Count?stages[selected]!.AsObject():null;
    public static IReadOnlyList<PrestressStation> PrestressStations(JsonObject? result,int selected)
    {
        var stage=PrestressStage(result,selected);if(stage is null)return [];
        double area=result!["PrestressInput"]!["TendonAreaMm2"]!.GetValue<double>();
        return stage["Points"]!.AsArray().Select((p,n)=>new PrestressStation(n,p!["XM"]!.GetValue<double>(),p["EccentricityM"]!.GetValue<double>(),p["JackingStressMpa"]!.GetValue<double>(),p["InitialStressMpa"]!.GetValue<double>(),p["EffectiveStressMpa"]!.GetValue<double>(),area)).ToArray();
    }
    public static bool PrestressCanTransfer(JsonObject? result)=>result?["Prestress"] is JsonObject p&&p["Converged"]!.GetValue<bool>()&&p["WithinStressLimits"]!.GetValue<bool>()&&result["PrestressInput"]?["Method"]?.ToString()!="PostTensionedUnbonded";
    public static IReadOnlyList<ActionPlotDrawing> PrestressPlots(JsonObject? result,int selected)
    {
        var stage=PrestressStage(result,selected);if(stage is null)return [];
        var rows=stage["Points"]!.AsArray();string note=PrestressCanTransfer(result)?"Stati distinti: il martinetto non è la tensione efficace":"Stato non trasferibile a Concrete: verificare limiti, convergenza o aderenza";
        ActionPlotSeries Series(string label,string key)=>new(label,rows.Select(p=>new ActionPlotPoint(p!["XM"]!.GetValue<double>(),p[key]!.GetValue<double>())).ToArray());
        ActionPlotDrawing Plot(string name,string unit,params (string Label,string Key)[] fields)=>new(name,"x [m]",unit,note,fields.Select(f=>Series(f.Label,f.Key)).ToArray());
        return [Plot("Profilo del cavo","z [m]",("Eccentricità dal baricentro","EccentricityM")),
            Plot("Tensioni lungo il cavo","σp [MPa]",("Martinetto","JackingStressMpa"),("Dopo attrito","AfterFrictionMpa"),("Dopo ancoraggio","AfterAnchoringMpa"),("Dopo trasferimento","InitialStressMpa"),("Efficace","EffectiveStressMpa")),
            Plot("Contributi alle perdite differite accoppiate","Δσp [MPa]",("Ritiro","ShrinkageContributionMpa"),("Viscosità","CreepContributionMpa"),("Rilassamento","RelaxationContributionMpa")),
            Plot("Forza efficace","P [kN]",("Forza nel cavo","EffectiveForceKn"))];
    }
    public static IReadOnlyList<PrestressTarget> PrestressTargets(JsonObject data)
    {
        if(data["workspace_ca"]?["trefoli"] is not JsonArray tendons||tendons.Count==0)throw new ArgumentException("La sezione deve contenere almeno un cavo/trefolo: definirne posizione, area e materiale prima del trasferimento.");
        return tendons.Select(t=>new PrestressTarget(t!["id"]!.ToString(),t["id"]+" · "+t["materiale"]+" · Ap="+t["area"]+" mm² · ("+t["x"]+"; "+t["y"]+") mm")).ToArray();
    }
    /// <summary>Independent copy; native section retained, only the selected tendon's prestress state is replaced.
    /// The concrete solver already represents prestress as a tendon prestrain: no extra N/M load is added.</summary>
    public static JsonObject ApplyPrestressToSection(JsonObject sectionData,JsonObject result,int selectedStage,int station,string tendonId)
    {
        if(!PrestressCanTransfer(result))throw new ArgumentException("Stato non trasferibile: controllare limiti, convergenza e aderenza. Concrete richiede un cavo aderente.");
        var stage=PrestressStage(result,selectedStage)??throw new ArgumentException("Stato temporale non disponibile.");
        var points=stage["Points"]!.AsArray();if(station<0||station>=points.Count)throw new ArgumentException("Ascissa non disponibile.");
        var point=points[station]!;var source=result["PrestressInput"]!;
        var copy=sectionData.DeepClone().AsObject();var input=copy["input"]!.AsObject();var workspace=copy["workspace_ca"]!.AsObject();
        var row=workspace["trefoli"]!.AsArray().OfType<JsonObject>().SingleOrDefault(t=>t["id"]?.ToString()==tendonId)??throw new ArgumentException("Cavo di destinazione non trovato.");
        static double Read(JsonObject row,string key)=>SectionWorkspace.Number(row[key]?.ToString()??"",key);
        const double RelativeTolerance=1e-8,PositionTolerance=1e-8;
        static bool Same(double a,double b)=>Math.Abs(a-b)<=RelativeTolerance*Math.Max(1,Math.Max(Math.Abs(a),Math.Abs(b)));
        foreach(var pair in new[]{("area","TendonAreaMm2"),("Ep","TendonModulusMpa"),("fpyk","Fp01kMpa"),("fpk","FpkMpa")})
            if(!Same(Read(row,pair.Item1),source[pair.Item2]!.GetValue<double>()))throw new ArgumentException("Il cavo selezionato ha "+pair.Item1+" diverso dal calcolo delle perdite: allineare area e materiale.");
        var native=CheckerSection.PrepareModel(input,workspace).Section;
        double x=Read(row,"x"),y=Read(row,"y");
        var matches=native.Rebars.Where(r=>r.RebarMaterial.SteelType==SteelMaterial.SteelTypes.Tendon&&Math.Abs(r.Position.X-x)<=PositionTolerance&&Math.Abs(r.Position.Y-y)<=PositionTolerance&&Same(r.Area,Read(row,"area"))).ToArray();
        if(matches.Length!=1)throw new ArgumentException("Impossibile associare univocamente il cavo dell’editor all’armatura GPC.");
        var old=matches[0];double stress=point["EffectiveStressMpa"]!.GetValue<double>();
        var replacement=new ReinforcedConcreteRebar(old.RebarSection,old.Position,stress,old.Id,old.Name,old.Guid);
        if(!native.RemoveRebar(old)||!native.AddRebar(replacement))throw new ArgumentException("Aggiornamento del cavo GPC non riuscito.");
        row["sigma0"]=stress.ToString("G17",CultureInfo.InvariantCulture);
        row["origine_precompressione"]=new JsonObject{["eta_giorni"]=stage["AgeDays"]!.DeepClone(),["x_m"]=point["XM"]!.DeepClone(),["sigma_martinetto_mpa"]=point["JackingStressMpa"]!.DeepClone(),["sigma_trasferimento_mpa"]=point["InitialStressMpa"]!.DeepClone(),["sigma_efficace_mpa"]=point["EffectiveStressMpa"]!.DeepClone(),["fonte"]=source["SystemSource"]!.DeepClone()};
        input[ConcreteSectionArchive.Key]=ConcreteSectionArchive.Capture(input,workspace,native);
        return copy;
    }
}
