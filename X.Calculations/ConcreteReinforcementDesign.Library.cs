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

    sealed record DesignDocument(JsonObject Data,JsonObject Workspace,JsonObject Input,Dictionary<string,JsonObject[]> Rows,JsonObject[] ShearRows,bool Torsion);
    static DesignDocument PrepareDesignInput(JsonObject source,ConcreteDesignOptions options,CancellationToken token)
    {
        options.Validate(); token.ThrowIfCancellationRequested();
        var data = (JsonObject)source.DeepClone(); var workspace = SectionWorkspace.Prepare(data); var input = data["input"]!.AsObject();
        ValidateScope(data, workspace);
        ConcreteCalculationSettings.ValidateStirrups(input, workspace["taglio"]!.AsObject());
        var rows = SectionWorkspace.Sets.ToDictionary(k => k, k => data["combinazioni"]!.Array(k).OfType<JsonObject>()
            .Select(r => J.Obj(("nome", r.S("nome")), ("N", r["azioni"]?[0]), ("Mx", r["azioni"]?[1]), ("My", r["azioni"]?[2]))).ToArray());
        foreach (var row in rows.Values.SelectMany(r => r)) _ = SectionWorkspace.ReadAction(row);
        var shearRows = workspace["taglio"]!.Array("azioni").OfType<JsonObject>().ToArray();
        // Shear/torsion rows also carry N/M: the design must not ignore those simultaneous actions.
        rows["SLU"] = rows["SLU"].Concat(shearRows.Select(r => J.Obj(("nome", "Taglio · " + r.S("nome")),
            ("N", r.S("N", "0")), ("Mx", r.S("Mx", "0")), ("My", r.S("My", "0")), ("__locali", true)))).ToArray();
        foreach (var row in rows["SLU"]) _ = SectionWorkspace.ReadAction(row);
        bool torsion = shearRows.Any(r => SectionWorkspace.Number(r.S("T", "0"), "T") != 0);
        if (torsion && !options.TorsionLayoutConfirmed) throw new ArgumentException("Torsione: confermare nella ricerca lo schema periferico con staffe chiuse e barre di spigolo.");
        return new(data,workspace,input,rows,shearRows,torsion);
    }
    static string[] DesignNotes(bool torsion)=>[ "Migliore tra le configurazioni calcolate; ricerca discreta, nessuna garanzia di minimo fuori dagli elenchi assegnati.",
          "Geometria, materiali, copriferro e sollecitazioni mantenuti. Barre su una fila per faccia / un anello; barre laterali con il diametro inferiore. Le armature manuali e i secondi strati vengono sostituiti solo con Applica.",
          "Peso per metro: acciaio 7850 kg/m³, staffe con sviluppo geometrico e maggiorazione ganci impostata; escluse sovrapposizioni, ancoraggi longitudinali, sfridi e armature locali.",
          "Numero barre: pezzi equivalenti per metro, con una barra longitudinale continua contata come un pezzo, più staffe, bracci aggiunti e barre ortogonali. Compromesso: p_peso·kg/m + p_barre·pezzi/m + p_diametri·numero diametri. I pesi sono coefficienti assegnati dall’utente, senza normalizzazione automatica.",
          "Dettagli NTC capitolo 4 / EC2: i controlli numerici sono obbligatori. Le verifiche esecutive non deducibili dalla sezione restano da completare.",
          "Solette / pareti: rete ortogonale uguale sulle due facce; per ogni disposizione principale si conserva la rete ammissibile migliore per l’obiettivo. La verifica della direzione ortogonale sotto proprie azioni richiede un’altra sezione.",
          torsion ? "Torsione: quota uniforme di area riservata su OGNI barra; distribuzione controllata per lunghezza tributaria periferica. Resistenza N–Mx–My calcolata solo con l’area residua, alle stesse coordinate. SLE usa l’area totale." : "Nessuna torsione nelle azioni assegnate.",
          "Tutte le combinazioni compilate sono considerate, anche se nascoste nei grafici. Non si aggiungono azioni, effetti del secondo ordine o verifiche sismiche non presenti nel foglio.",
          "Ricerca completa: tutte le disposizioni ammesse. Con limite tentativi, il campione copre l’intero elenco ordinato per obiettivo, comprese le disposizioni più armate; nessuna conclusione sulle configurazioni non calcolate.",
          "Ordine: dettagli, ancoraggi, taglio/torsione, poi resistenza e SLE. Dopo il primo controllo negativo la configurazione è esclusa. Le prove su estremi e valori intermedi delle sezioni sopravvissute orientano solo l’ordine, senza eliminare intervalli. Risultati longitudinali riusati per i passi staffa compatibili." ];
}
