using System.Text.Json.Nodes;
using GPC.Design.Sections;
namespace Anthea.Calculations;

public static partial class ConcreteReinforcementDesign
{
    static SectionDesignObjective NativeObjective(ConcreteDesignOptions options)=>new(options.Objective switch
    {
        "Peso"=>ReinforcementObjective.Weight,"Numero barre"=>ReinforcementObjective.Pieces,
        "Numero diametri"=>ReinforcementObjective.DiameterKinds,"Compromesso"=>ReinforcementObjective.Weighted,
        _=>throw new ArgumentException("Obiettivo della ricerca non riconosciuto.")
    },options.WeightCost,options.BarCost,options.DiameterCost);
    static SectionDesignMetric NativeMetric(ConcreteDesignMetrics m)=>new(m.SteelArea,m.LongitudinalKgPerM,m.TransverseKgPerM,m.Bars,m.Diameters,m.PiecesPerM);
    static ConcreteDesignMetrics DisplayMetric(SectionDesignMetric m)=>new(m.SteelArea,m.LongitudinalKgPerM,m.TransverseKgPerM,m.Bars,m.Diameters,m.PiecesPerM);
    static SectionDesignGeometry NativeGeometry(SezioneCA section)=>new(section.Shape=="Circolare",section.Width,section.Height,
        section.Input.Required("cover_mm"),section.AreaSteel,section.Bars.Select(b=>new DesignBar(b.X,b.Y,b.Diametro,b.Area)));
    static SectionDesignLinks NativeLinks(SezioneCA section,JsonObject shear,ConcreteDesignOptions options)
    {
        bool present=ConcreteCalculationSettings.HasStirrups(section.Input);
        return new(present,present?section.Input.Required("transverse_bar_diameter_mm",strict:true):0,
            present?section.Input.Required("transverse_spacing_mm",strict:true):0,
            present&&! (section.Shape=="Circolare")?shear.Required("rami_x"):0,
            present&&! (section.Shape=="Circolare")?shear.Required("rami_y"):0,
            present&&section.Shape=="Circolare"?shear.Required("rami_interni"):0,options.HookAllowance);
    }
    static SecondaryReinforcementKind NativeSecondaryKind(JsonObject detailing)=>detailing.S("elemento") switch
    {"Soletta piena"=>SecondaryReinforcementKind.Slab,"Parete"=>SecondaryReinforcementKind.Wall,
        "Trave" or "Pilastro"=>SecondaryReinforcementKind.None,_=>throw new ArgumentException("Tipo di elemento non riconosciuto.")};
    static ConcreteDesignMetrics MetricsNative(SezioneCA section,JsonObject shear,JsonObject detailing,ConcreteDesignOptions options)
    {
        var kind=NativeSecondaryKind(detailing);
        var secondary=kind==SecondaryReinforcementKind.None?new SectionSecondaryReinforcement(kind,0,0,0)
            :new(kind,detailing.Required("as_secondaria"),detailing.Required("passo_secondaria"),detailing.Required("diametro_secondaria"));
        return DisplayMetric(SectionDesignQuantities.Calculate(NativeGeometry(section),NativeLinks(section,shear,options),secondary));
    }
    static double ScoreNative(ConcreteDesignMetrics metrics,ConcreteDesignOptions options)=>SectionDesignQuantities.Score(NativeMetric(metrics),NativeObjective(options));
    static ConcreteDesignCandidate[] RankNative(IEnumerable<ConcreteDesignCandidate> source,ConcreteDesignOptions options)
    {
        var rows=source.ToDictionary(c=>c.Id);
        return SectionDesignSelection.Rank(rows.Values.Select(c=>new SectionDesignRankEntry(c.Id,NativeMetric(c.Metrics))),NativeObjective(options))
            .Select(r=>rows[r.Id] with {Score=r.Score,Pareto=r.Pareto}).ToArray();
    }
    static void CheckTorsionDistributionNative(SezioneCA section,TorsionResult torsion,ConcreteDesignOptions options,List<ConcreteDesignCheck> checks,string combination)
    {
        double worst=SectionDesignQuantities.TorsionDistributionRatio(section.Bars.Select(b=>new DesignBar(b.X,b.Y,b.Diametro,b.Area)),torsion.RequiredLongitudinalArea,options.TorsionReservePercent);
        AddRatio(checks,"torsione",combination+" · distribuzione longitudinale",worst,options,
            "As,T ripartita per lunghezza tributaria fra barre consecutive sul perimetro; verifica della quota riservata su ciascuna barra.");
    }
    static void SelectSecondaryNative(SezioneCA section,JsonObject shear,JsonObject detailing,IReadOnlyList<DetailingCheck> checks,ConcreteDesignOptions options)
    {
        var kind=NativeSecondaryKind(detailing);bool slab=kind==SecondaryReinforcementKind.Slab;
        double minimum=checks.Single(c=>c.Name==(slab?"Armatura secondaria":"Armatura orizzontale per metro")).Limit!.Value;
        double? maximum=checks.Single(c=>c.Name==(slab?"Interasse armatura secondaria":"Interasse orizzontale")).Limit;
        SectionSecondaryDesign result;
        try
        {
            result=SectionDesignSelection.SelectSecondary(NativeGeometry(section),NativeLinks(section,shear,options),kind,minimum,maximum,
                detailing.S("zona_critica")=="Sì",section.Outline.Min(p=>p[1]),section.Outline.Max(p=>p[1]),detailing.Required("aggregato"),
                options.SecondaryDiameters,options.SecondarySpacings,options.MaxDiameterKinds,NativeObjective(options));
        }
        catch(ArgumentException){throw new ArgumentException("Nessuna rete ortogonale ammissibile: controllare diametri, passi, spazio e numero di diametri distinti.");}
        detailing["diametro_secondaria"]=result.Reinforcement.Diameter;detailing["passo_secondaria"]=result.Reinforcement.Spacing;
        detailing["as_secondaria"]=result.Reinforcement.AreaPerMetre;
    }
}
