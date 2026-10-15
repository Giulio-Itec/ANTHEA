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
    // Staffe chiuse della T sull'anima (ARM-3 del 15/10/2026): larghezza delle staffe web_width_mm, come taglio e dettagli; prima l'ingombro del
    // contorno, cioè l'ala (bf 1200, bw 300, h 700: 9,491 invece di 4,756 kg/m). Per le altre forme LinkWidth = Width, come il costruttore storico.
    static SectionDesignGeometry NativeGeometry(SezioneCA section)=>new(section.Shape=="Circolare",section.Width,section.Height,
        section.Input.Required("cover_mm"),section.AreaSteel,ConcreteDesignMapping.Bars(section),
        section.Shape=="A T"?section.Input.Required("web_width_mm"):section.Width);
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
        double worst=SectionDesignQuantities.TorsionDistributionRatio(ConcreteDesignMapping.Bars(section),torsion.RequiredLongitudinalArea,options.TorsionReservePercent);
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

    internal sealed record DesignDocument(JsonObject Data,JsonObject Workspace,JsonObject Input,Dictionary<string,JsonObject[]> Rows,JsonObject[] ShearRows,bool Torsion)
    {
        /// <summary>Parametri del taglio manuali del foglio (ARM-1).</summary>
        public bool ManualShear { get; init; }
    }
    internal static DesignDocument PrepareDesignInput(JsonObject source,ConcreteDesignOptions options,CancellationToken token)
    {
        options.Validate(); token.ThrowIfCancellationRequested();
        var data = (JsonObject)source.DeepClone(); var workspace = SectionWorkspace.Prepare(data); var input = data["input"]!.AsObject();
        ValidateScope(data, workspace);
        ConcreteCalculationSettings.ValidateStirrups(input, workspace["taglio"]!.AsObject());
        bool manualShear = ConcreteCalculationSettings.ManualShear(workspace["taglio"]!.AsObject());
        // Valori manuali completi prima della ricerca: un dato mancante è un rifiuto della ricerca, non l'esclusione di ogni proposta.
        if (manualShear) foreach (string key in new[] { "bw_x", "d_x", "asl_x", "bw_y", "d_y", "asl_y" }) _ = SectionWorkspace.Number(workspace["taglio"].S(key), key);
        var rows = SectionWorkspace.Sets.ToDictionary(k => k, k => data["combinazioni"]!.Array(k).OfType<JsonObject>()
            .Select(r => J.Obj(("nome", r.S("nome")), ("N", r["azioni"]?[0]), ("Mx", r["azioni"]?[1]), ("My", r["azioni"]?[2]))).ToArray());
        foreach (var row in rows.Values.SelectMany(r => r)) _ = SectionWorkspace.ReadAction(row);
        var shearRows = workspace["taglio"]!.Array("azioni").OfType<JsonObject>().ToArray();
        // Contratto storico: le righe di taglio e torsione portano anche N/M, in assi locali, e la ricerca non deve ignorarle. Nei fogli con
        // azioni unificate (sollecitazioni_versione = 1) le righe di taglio sono copie delle righe SLU/SLV, già verificate a N–Mx–My negli
        // assi di dominio3d come nella scheda N–M: non si aggiungono una seconda volta in assi locali (analisi dei bug del 15/10/2026, ARM-2).
        bool unified = workspace["sollecitazioni_versione"]?.ToString() == "1";
        if (!unified)
            rows["SLU"] = rows["SLU"].Concat(shearRows.Select(r => J.Obj(("nome", "Taglio · " + r.S("nome")),
                ("N", r.S("N", "0")), ("Mx", r.S("Mx", "0")), ("My", r.S("My", "0")), ("__locali", true)))).ToArray();
        foreach (var row in rows["SLU"]) _ = SectionWorkspace.ReadAction(row);
        bool torsion = shearRows.Any(r => SectionWorkspace.Number(r.S("T", "0"), "T") != 0);
        if (torsion && !options.TorsionLayoutConfirmed) throw new ArgumentException("Torsione: confermare nella ricerca lo schema periferico con staffe chiuse e barre di spigolo.");
        return new(data,workspace,input,rows,shearRows,torsion){ManualShear=manualShear};
    }
    /// <summary>
    /// Campi del taglio che «Applica» copia dalla proposta nel foglio (ARM-1): con parametri manuali «parametri», bw, d e Asl restano quelli
    /// dell'utente e l'avviso lo dichiara; altrimenti tutti i campi della proposta salvo le azioni.
    /// </summary>
    public static (string[] Keys, string Notice) AppliedShearFields(JsonObject sheet,JsonObject candidate)
    {
        bool manual=ConcreteCalculationSettings.ManualShear(sheet);
        string[] kept=manual?["parametri","bw_x","d_x","asl_x","bw_y","d_y","asl_y"]:[];
        return (candidate.Select(p=>p.Key).Where(k=>k!="azioni"&&!kept.Contains(k)).ToArray(),
            manual?"Parametri del taglio «Manuali» conservati: la ricerca ha usato per ogni asse il minimo fra bw, d e Asl del foglio e quelli della disposizione proposta; controllarli sulla nuova disposizione.":"");
    }
    /// <summary>Nota della regola dei parametri manuali del taglio (ARM-1), presente solo con parametri «Manuali».</summary>
    public const string ManualShearNote="Taglio con parametri «Manuali»: per ogni proposta e per asse bw, d e Asl sono il minimo fra il valore manuale del foglio e quello ricavato dalla disposizione proposta; i parametri restano manuali e «Applica» conserva i valori del foglio.";
    internal static string[] DesignNotes(bool torsion,bool manualShear)=>[ .. DesignNotes(torsion), .. manualShear?new[]{ManualShearNote}:[] ];
    internal static string[] DesignNotes(bool torsion)=>[ "Migliore tra le configurazioni calcolate; ricerca discreta, nessuna garanzia di minimo fuori dagli elenchi assegnati.",
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
