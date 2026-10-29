using System.Text.Json;
using GPC.Converter;

namespace ANTHEA.ModelWorkspace;

public enum ModelCombinationKind { Linear, Envelope, Absolute, Srss }
public sealed record ModelCombinationTerm(string Reference, bool IsCombination, double Factor, string Analysis = "ST");
public sealed record ModelCombinationDefinition(string Id, string Name, ModelCombinationKind Kind, string Source,
    string Status, ModelCombinationTerm[] Terms, bool Editable = false);
public sealed record ModelCombinationAvailability(ModelCombinationDefinition Definition, string State, string Message, string[] RequiredCases, bool CanCalculate);
public sealed record ModelCombinationDerivation(string DefinitionId, string InputRevision, string Engine, string[] RequiredCases,
    ModelCombinationGoverning[] Governing, string[]? Alternatives = null);
// The whole local vector belongs to one alternative; it is never assembled from independent extrema.
public sealed record ModelCombinationGoverning(string Family, int Element, int Node, double? Station, string Component,
    string Alternative, double[] ConcomitantValues);
public sealed record ModelCombinationProgress(string Message);
public sealed record ModelCombinationCalculation(ModelResultSet[] Results, string[] Warnings, double Milliseconds,
    double PreparationMilliseconds=0,double RebuildMilliseconds=0,double DisplayMilliseconds=0,bool PreparationReused=false);

/// <summary>Document definitions, source identity and editing. GPC owns dependency expansion and result algebra.</summary>
public static class ModelCombinations
{
    static readonly IReadOnlyDictionary<string, string> Tables = new Dictionary<string, string>(StringComparer.Ordinal)
    { ["CB"]="LCOM-GEN", ["CBC"]="LCOM-CONC", ["CBS"]="LCOM-STEEL", ["CBR"]="LCOM-SRC", ["CBSC"]="LCOM-STLCOMP", ["CBSM"]="LCOM-SEISMIC" };

    public static ModelCombinationDefinition[] Read(ModelSnapshot snapshot)
    {
        var definitions = new List<ModelCombinationDefinition>();
        foreach (string table in Tables.Values)
        {
            if (snapshot.SourceTables?.TryGetValue(table, out var rows) != true) continue;
            foreach (var row in rows.EnumerateObject())
            {
                var value = row.Value;
                string name = value.GetProperty("NAME").GetString() ?? "";
                var terms = value.GetProperty("vCOMB").EnumerateArray().Select(t =>
                {
                    string analysis=t.GetProperty("ANAL").GetString()??"", target=t.GetProperty("LCNAME").GetString()??"";
                    bool nested=Tables.TryGetValue(analysis,out var referenced);
                    return new ModelCombinationTerm(nested?referenced+"/"+target:target,nested,t.GetProperty("FACTOR").GetDouble(),analysis);
                }).ToArray();
                definitions.Add(new(table+"/"+name,name,(ModelCombinationKind)value.GetProperty("iTYPE").GetInt32(),table,
                    value.TryGetProperty("ACTIVE",out var active)?active.ToString():"",terms));
            }
        }
        // Sources without MIDAS tables carry their definitions in the document (Straus7: LSA combinations).
        definitions.AddRange(snapshot.SourceCombinations?.Definitions??[]);
        var overrides=(snapshot.CustomCombinations??[]).Select(d=>d.Id).ToHashSet(StringComparer.Ordinal);
        var excluded=(snapshot.ExcludedCombinationIds??[]).ToHashSet(StringComparer.Ordinal);
        definitions.RemoveAll(d=>overrides.Contains(d.Id)||excluded.Contains(d.Id));
        definitions.AddRange((snapshot.CustomCombinations??[]).Where(d=>!excluded.Contains(d.Id)));
        ValidateDefinitions(definitions);
        return definitions.ToArray();
    }

    public static void ValidateDefinitions(IEnumerable<ModelCombinationDefinition> source)
    {
        var definitions=source.ToArray();
        if(definitions.Length>10000 || definitions.Any(d=>d==null || string.IsNullOrWhiteSpace(d.Id) || string.IsNullOrWhiteSpace(d.Name) ||
            d.Id.Length>1000 || d.Name.Length>200 || string.IsNullOrWhiteSpace(d.Source) || d.Status==null || !Enum.IsDefined(d.Kind) || d.Terms==null || d.Terms.Length>1000 ||
            d.Terms.Any(t=>t==null || string.IsNullOrWhiteSpace(t.Reference) || string.IsNullOrWhiteSpace(t.Analysis) || !double.IsFinite(t.Factor))) ||
            definitions.Select(d=>d.Id).Distinct(StringComparer.Ordinal).Count()!=definitions.Length)
            throw new InvalidDataException("Definizioni delle combinazioni mancanti, duplicate o non valide.");
    }

    internal static CombinationRecord ToGpc(ModelCombinationDefinition d)=>new()
    {
        Id=d.Id,Name=d.Name,Kind=(CombinationKind)d.Kind,Source=d.Source,Status=d.Status,Record=d.Id,
        Terms=d.Terms.Select(t=>new CombinationTermRecord {Name=t.Reference,Factor=t.Factor,Analysis=t.Analysis,
            IsCombination=t.IsCombination,IsStaticCase=!t.IsCombination&&t.Analysis=="ST"}).ToList()
    };

    public static ModelCombinationAvailability[] Inspect(ModelSnapshot snapshot)
    {
        var definitions=Read(snapshot);var records=definitions.Select(ToGpc).ToArray();
        string? revision=snapshot.Results.Any(r=>r.Combination!=null)?InputRevision(snapshot):null;
        var cases=snapshot.Results.Where(r=>!r.IsEnvelope&&r.Combination==null&&r.Stage==null).Select(r=>r.Name).ToHashSet(StringComparer.Ordinal);
        // The definitions with results of the current revision, once instead of a scan of the results for each definition.
        var calculated=snapshot.Results.Where(r=>r.Combination!=null&&r.Combination.InputRevision==revision).Select(r=>r.Combination!.DefinitionId).ToHashSet(StringComparer.Ordinal);
        var byId=definitions.ToDictionary(d=>d.Id,StringComparer.Ordinal);var counts=new Dictionary<string,(long,bool)>(StringComparer.Ordinal);
        return definitions.Select(d=>
        {
            try
            {
                var required=CombinationExpansion.StaticCases(records,d.Id).ToArray();
                if(required.Length==0) return new ModelCombinationAvailability(d,"Vuota","Aggiungi almeno un caso di carico.",required,false);
                // COMBO-B3: every coefficient zero gives no state to combine, nested definitions included (scaled by zero).
                if(d.Terms.All(t=>t.Factor==0))return new(d,"Vuota",ZeroMessage,required,false);
                if(AlternativeCount(byId,d.Id,counts) is {Over:true} expansion)return new(d,"Non disponibile",ExpansionMessage(expansion.Count),required,false);
                var missing=required.Where(c=>!cases.Contains(c)).ToArray();
                if(missing.Length>0)return new(d,"Casi mancanti","Risultati da importare: "+string.Join(", ",missing),required,false);
                if(!GpcSnapshotModel.CanRebuild(snapshot))
                    return new(d,"Sorgente incompleta","Il collegamento GPC richiede le tabelle sorgente MIDAS oppure le terne locali e i casi della sorgente importata. La definizione può essere conservata.",required,false);
                bool ready=calculated.Contains(d.Id);
                return new(d,ready?"Calcolata":"Da calcolare",ready?"Risultati locali disponibili nella copia del modello.":"Casi base disponibili; costruzione locale con GPC.",required,true);
            }
            catch(Exception ex) when(ex is ArgumentException or InvalidOperationException or NotSupportedException)
            { return new(d,"Non disponibile",Explain(ex),[],false); }
        }).ToArray();
    }

    /// <summary>Alternative states a combination may have: the limit of the expansion of GPC (CombinationExpansion.Expand).</summary>
    public const int MaximumAlternatives=4096;

    /// <summary>
    /// COMBO-B2 (29/10/2026): the alternatives of a definition with the rules of CombinationExpansion.Expand, without building them: one per
    /// static term, the sum over the terms for an envelope, the product for a linear combination. GPC refuses an expansion as soon as one step
    /// exceeds the limit (a nested definition, a partial sum or product): over the limit the count stays over it. The definitions are those of
    /// <see cref="Read"/> whose static cases were resolved (no cycles, no unknown references).
    /// </summary>
    internal static (long Count,bool Over) AlternativeCount(IReadOnlyDictionary<string,ModelCombinationDefinition> definitions,string id,Dictionary<string,(long,bool)> counts)
    {
        if(counts.TryGetValue(id,out var known))return known;
        const long cap=1_000_000_000_000L; // the number in the message, without overflow
        var definition=definitions[id];bool envelope=definition.Kind==ModelCombinationKind.Envelope;
        long count=envelope?0:1;bool over=false;
        foreach(var term in definition.Terms)
        {
            var (part,nestedOver)=term.IsCombination?AlternativeCount(definitions,term.Reference,counts):(1L,false);
            count=Math.Min(cap,envelope?count+part:part==0?0:count>cap/part?cap:count*part);
            over|=nestedOver||count>MaximumAlternatives;
        }
        return counts[id]=(count,over);
    }

    internal const string ZeroMessage="Tutti i coefficienti nulli (anche dopo la somma dei termini): nessun caso da combinare. Indica almeno un coefficiente diverso da zero.";

    internal static string ExpansionMessage(long count)=>$"Oltre {MaximumAlternatives} stati alternativi ({count}): una somma lineare di inviluppi ha il prodotto "+
        "delle loro alternative. Usa un inviluppo delle combinazioni invece della somma, o riduci i termini degli inviluppi annidati.";

    internal static string Explain(Exception ex)=>ex.Message.StartsWith("CyclicCombination",StringComparison.Ordinal)
        ?"Dipendenza circolare tra combinazioni. Correggi i riferimenti."
        :ex is NotSupportedException?"Tipo di combinazione o analisi non supportato dalla sovrapposizione statica GPC."
        :"Definizione non risolvibile: "+ex.Message;

    public static ModelSnapshot Save(ModelSnapshot snapshot,ModelCombinationDefinition definition)
    {
        ValidateDefinitions([definition]);
        if(!definition.Editable || !(definition.Id.StartsWith("ANTHEA/",StringComparison.Ordinal)&&definition.Source=="ANTHEA" || Read(snapshot).Any(d=>d.Id==definition.Id&&d.Source==definition.Source)))
            throw new InvalidDataException("Identità della combinazione non valida.");
        if(definition.Kind is not(ModelCombinationKind.Linear or ModelCombinationKind.Envelope) || definition.Terms.Length==0)
            throw new InvalidDataException("Scegli Somma lineare o Inviluppo e almeno un termine.");
        var custom=(snapshot.CustomCombinations??[]).Where(d=>d.Id!=definition.Id).Append(definition with {Terms=definition.Terms.ToArray()}).ToArray();
        if(custom.Any(d=>d.Id!=definition.Id&&d.Name==definition.Name))throw new InvalidDataException("Esiste già una combinazione ANTHEA con questo nome.");
        var copy=Copy(snapshot);copy.CustomCombinations=custom;
        var all=Read(copy);var records=all.Select(ToGpc).ToArray();
        // Missing results are allowed while editing; missing definitions/cases and cycles are not.
        var required=CombinationExpansion.StaticCases(records,definition.Id);
        var known=StaticCaseNames(snapshot).ToHashSet(StringComparer.Ordinal);
        if(required.Any(c=>!known.Contains(c)))throw new InvalidDataException("La combinazione contiene un caso non presente nel modello.");
        copy.Results=copy.Results.Where(r=>r.Combination==null).ToArray();copy.Validate();return copy;
    }

    public static ModelSnapshot Remove(ModelSnapshot snapshot,string id)
    {
        var all=Read(snapshot);
        if(!all.Any(d=>d.Id==id))throw new InvalidDataException("Combinazione non trovata.");
        if(all.Any(d=>d.Id!=id&&d.Terms.Any(t=>t.IsCombination&&t.Reference==id)))throw new InvalidDataException("La combinazione è usata da altre definizioni: rimuovi prima quei riferimenti.");
        var copy=Copy(snapshot);copy.CustomCombinations=(snapshot.CustomCombinations??[]).Where(d=>d.Id!=id).ToArray();
        if(!id.StartsWith("ANTHEA/",StringComparison.Ordinal))copy.ExcludedCombinationIds=[..snapshot.ExcludedCombinationIds??[],id];
        copy.Results=copy.Results.Where(r=>r.Combination==null).ToArray();copy.Validate();return copy;
    }

    public static string[] StaticCaseNames(ModelSnapshot snapshot)
    {
        var names=snapshot.SourceTables?.TryGetValue("STLD",out var cases)==true
            ?cases.EnumerateObject().Select(p=>p.Value.GetProperty("NAME").GetString()!).ToArray():[];
        names=[..names,..snapshot.SourceCombinations?.StaticCases??[]];
        if(names.Any(string.IsNullOrWhiteSpace)||names.Distinct(StringComparer.Ordinal).Count()!=names.Length)
            throw new InvalidDataException("Nomi dei casi di carico mancanti o duplicati nella tabella sorgente.");
        return names.Concat(snapshot.Results.Where(r=>!r.IsEnvelope&&r.Combination==null&&r.Stage==null).Select(r=>r.Name)).Distinct(StringComparer.Ordinal).ToArray();
    }

    internal static ModelSnapshot Copy(ModelSnapshot s)=>new()
    {Version=s.Version,Name=s.Name,Source=s.Source,Nodes=s.Nodes,Elements=s.Elements,Plates=s.Plates,Sections=s.Sections,
        Results=s.Results,SourceTables=s.SourceTables,LocalFrames=s.LocalFrames,NodeFrames=s.NodeFrames,Boundaries=s.Boundaries,
        ImportMessages=s.ImportMessages,UpdateInfo=s.UpdateInfo,CustomCombinations=s.CustomCombinations,ExcludedCombinationIds=s.ExcludedCombinationIds,
        SourceCombinations=s.SourceCombinations,
        // 1.16.0: data of the model (Straus7), kept by every copy.
        Masses=s.Masses,Temperatures=s.Temperatures,ConstructionStages=s.ConstructionStages,
        // The copy shares the arrays of the original: its revisions hold while the parts are the same references (Revisions).
        CombinationRevisions=s.CombinationRevisions};

    public static ModelSnapshot RestoreImported(ModelSnapshot snapshot)
    {
        var copy=Copy(snapshot);copy.ExcludedCombinationIds=null;copy.Results=copy.Results.Where(r=>r.Combination==null).ToArray();copy.Validate();return copy;
    }

    /// <summary>
    /// Refactoring of the combinations (29/10/2026): the revisions of a copy, kept with the references of the parts they were computed from.
    /// Each was a complete serialization of the copy (several MB), computed again by the calculation, Attach, the catalogue and the view
    /// model for the same parts. The copies share their arrays and records and never change them in place (ModelCombinations.Copy, the
    /// updates and the importers build new ones): the same references give the same canonical JSON, hence the same revision. Immutable, so a
    /// copy may share it and another thread may read it; a different part computes the value again, exactly as before.
    /// </summary>
    internal sealed record Revisions(int Version,string Name,string Source,object?[] Parts,ModelResultSet[] Basis,object? Custom,object? Excluded,
        string? Input,long InputBytes,string? Preparation)
    {
        static object?[] PartsOf(ModelSnapshot s)=>[s.Nodes,s.Elements,s.Plates,s.Sections,s.SourceTables,s.LocalFrames,s.NodeFrames,s.Boundaries,
            s.ImportMessages,s.UpdateInfo,s.SourceCombinations,s.Masses,s.Temperatures,s.ConstructionStages];
        internal static ModelResultSet[] BasisOf(ModelSnapshot s)=>s.Results.Where(r=>r.Combination==null).ToArray();
        internal static Revisions Of(ModelSnapshot s,ModelResultSet[] basis)=>new(s.Version,s.Name,s.Source,PartsOf(s),basis,s.CustomCombinations,s.ExcludedCombinationIds,null,0,null);
        /// <summary>Same parts except the definitions of the user: the preparation of GPC is the same.</summary>
        internal bool SameBase(ModelSnapshot s,ModelResultSet[] basis)=>Version==s.Version&&string.Equals(Name,s.Name,StringComparison.Ordinal)&&
            string.Equals(Source,s.Source,StringComparison.Ordinal)&&Parts.SequenceEqual(PartsOf(s),ReferenceEqualityComparer.Instance)&&
            Basis.SequenceEqual(basis,ReferenceEqualityComparer.Instance);
        internal bool SameInput(ModelSnapshot s,ModelResultSet[] basis)=>ReferenceEquals(Custom,s.CustomCombinations)&&ReferenceEquals(Excluded,s.ExcludedCombinationIds)&&SameBase(s,basis);
    }

    public static string InputRevision(ModelSnapshot snapshot)=>Input(snapshot).Hash;

    /// <summary>The input revision and the length of its canonical JSON (the copy without the derived results).</summary>
    static (string Hash,long Bytes) Input(ModelSnapshot snapshot)
    {
        var basis=Revisions.BasisOf(snapshot);var known=snapshot.CombinationRevisions;
        if(known is {Input:{} input}&&known.SameInput(snapshot,basis))return (input,known.InputBytes);
        var copy=Copy(snapshot);copy.Results=basis;
        var (hash,bytes)=copy.FingerprintAndSize();
        var next=known!=null&&known.SameBase(snapshot,basis)?known with {Custom=snapshot.CustomCombinations,Excluded=snapshot.ExcludedCombinationIds}:Revisions.Of(snapshot,basis);
        snapshot.CombinationRevisions=next with {Input=hash,InputBytes=bytes};
        return (hash,bytes);
    }

    internal static string PreparationRevision(ModelSnapshot snapshot)
    {
        var basis=Revisions.BasisOf(snapshot);var known=snapshot.CombinationRevisions;
        if(known is {Preparation:{} preparation}&&known.SameBase(snapshot,basis))return preparation;
        var copy=Copy(snapshot);copy.Results=basis;
        copy.CustomCombinations=null;copy.ExcludedCombinationIds=null;
        string value=copy.Fingerprint();
        var next=known!=null&&known.SameBase(snapshot,basis)?known:Revisions.Of(snapshot,basis);
        snapshot.CombinationRevisions=next with {Preparation=value};
        return value;
    }

    public static ModelSnapshot Attach(ModelSnapshot snapshot,string definitionId,ModelCombinationCalculation calculated)
    {
        var copy=Copy(snapshot);
        string revision=InputRevision(snapshot);
        if(calculated.Results.Length==0||!Read(snapshot).Any(d=>d.Id==definitionId)||calculated.Results.Any(r=>r.Combination?.DefinitionId!=definitionId||r.Combination.InputRevision!=revision))
            throw new InvalidDataException("Il modello o la definizione sono cambiati durante il calcolo. Ricalcola la combinazione.");
        var original=snapshot.Results.Where(r=>r.Combination?.DefinitionId!=definitionId).ToArray();
        var names=original.Select(r=>r.Name).ToHashSet(StringComparer.Ordinal);
        if(calculated.Results.Any(r=>names.Contains(r.Name)))throw new InvalidDataException("Il nome del risultato calcolato coincide con un risultato esistente.");
        copy.Results=[..original,..calculated.Results];copy.Validate();
        // Keep the document reopenable. Only derived caches may be evicted; static cases and definitions are retained.
        while(SerializedSize(copy)>ModelSnapshotFiles.MaximumPackageBytes)
        {
            string? oldest=copy.Results.FirstOrDefault(r=>r.Combination!=null&&r.Combination.DefinitionId!=definitionId)?.Combination?.DefinitionId;
            if(oldest==null)throw new InvalidDataException("Il risultato supera lo spazio disponibile nel pacchetto del modello (256 MiB). Riduci il numero di casi base nella copia.");
            copy.Results=copy.Results.Where(r=>r.Combination?.DefinitionId!=oldest).ToArray();
        }
        return copy;
    }

    /// <summary>Length of the canonical JSON of the copy. With the input revision of its parts known, the JSON is that of the input revision
    /// with the derived results inserted in the array of the results, each with its JSON and one comma (the array already has the static
    /// cases): the same length as the complete serialization, without serializing the static cases again.</summary>
    internal static long SerializedSize(ModelSnapshot model)
    {
        var basis=Revisions.BasisOf(model);
        if(basis.Length>0&&model.CombinationRevisions is {Input:{}} known&&known.SameInput(model,basis))
            return known.InputBytes+model.Results.Where(r=>r.Combination!=null).Sum(r=>Size(r)+1);
        return Size(model);
    }
    static long Size<T>(T value)
    {using var counter=new ModelSnapshot.ByteCounter();JsonSerializer.Serialize(counter,value);return counter.Length;}
}
