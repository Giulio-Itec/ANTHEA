using System.Security.Cryptography;
using System.Text;
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
        var cases=snapshot.Results.Where(r=>!r.IsEnvelope&&r.Combination==null&&r.Stage==null).Select(r=>r.Name).ToHashSet(StringComparer.Ordinal);
        // The definitions with results of their current revision (decision of 1/11/2026: a revision for each definition), once instead of a scan
        // of the results for each definition.
        var calculated=CurrentDefinitions(snapshot,definitions);
        var byId=definitions.ToDictionary(d=>d.Id,StringComparer.Ordinal);var counts=new Dictionary<string,(long,bool)>(StringComparer.Ordinal);
        return definitions.Select((d,i)=>
        {
            try
            {
                // A definition without nested combinations reads only its own record: the same cases and exceptions, without indexing every
                // definition again for each one (hundreds of generated rows).
                var required=CombinationExpansion.StaticCases(d.Terms.Any(t=>t.IsCombination)?records:[records[i]],d.Id).ToArray();
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
        copy.Results=CurrentResults(copy,all);copy.Validate();return copy;
    }

    public static ModelSnapshot Remove(ModelSnapshot snapshot,string id)
    {
        var all=Read(snapshot);
        if(!all.Any(d=>d.Id==id))throw new InvalidDataException("Combinazione non trovata.");
        if(all.Any(d=>d.Id!=id&&d.Terms.Any(t=>t.IsCombination&&t.Reference==id)))throw new InvalidDataException("La combinazione è usata da altre definizioni: rimuovi prima quei riferimenti.");
        var copy=Copy(snapshot);copy.CustomCombinations=(snapshot.CustomCombinations??[]).Where(d=>d.Id!=id).ToArray();
        if(!id.StartsWith("ANTHEA/",StringComparison.Ordinal))copy.ExcludedCombinationIds=[..snapshot.ExcludedCombinationIds??[],id];
        copy.Results=CurrentResults(copy,Read(copy));copy.Validate();return copy;
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
        var copy=Copy(snapshot);copy.ExcludedCombinationIds=null;copy.Results=CurrentResults(copy,Read(copy));copy.Validate();return copy;
    }

    /// <summary>
    /// Refactoring of the combinations (29/10/2026): the revision of the preparation of a copy, kept with the references of the parts it was
    /// computed from. It was a complete serialization of the copy (several MB), computed again by the calculation, Attach, the catalogue and the
    /// view model for the same parts. The copies share their arrays and records and never change them in place (ModelCombinations.Copy, the
    /// updates and the importers build new ones): the same references give the same canonical JSON, hence the same revision. Immutable, so a
    /// copy may share it and another thread may read it; a different part computes the value again, exactly as before. The definitions of the
    /// user are not parts of the preparation: each definition has its own revision (<see cref="DefinitionRevision"/>).
    /// </summary>
    internal sealed record Revisions(int Version,string Name,string Source,object?[] Parts,ModelResultSet[] Basis,string Preparation,long PreparationBytes)
    {
        static object?[] PartsOf(ModelSnapshot s)=>[s.Nodes,s.Elements,s.Plates,s.Sections,s.SourceTables,s.LocalFrames,s.NodeFrames,s.Boundaries,
            s.ImportMessages,s.UpdateInfo,s.SourceCombinations,s.Masses,s.Temperatures,s.ConstructionStages];
        internal static ModelResultSet[] BasisOf(ModelSnapshot s)=>s.Results.Where(r=>r.Combination==null).ToArray();
        internal static Revisions Of(ModelSnapshot s,ModelResultSet[] basis,string preparation,long bytes)=>new(s.Version,s.Name,s.Source,PartsOf(s),basis,preparation,bytes);
        /// <summary>Same parts except the definitions of the user: the preparation of GPC is the same.</summary>
        internal bool SameBase(ModelSnapshot s,ModelResultSet[] basis)=>Version==s.Version&&string.Equals(Name,s.Name,StringComparison.Ordinal)&&
            string.Equals(Source,s.Source,StringComparison.Ordinal)&&Parts.SequenceEqual(PartsOf(s),ReferenceEqualityComparer.Instance)&&
            Basis.SequenceEqual(basis,ReferenceEqualityComparer.Instance);
    }

    /// <summary>The revision of the preparation (the copy without the derived results and the definitions of the user) and the length of its
    /// canonical JSON.</summary>
    static (string Hash,long Bytes) Preparation(ModelSnapshot snapshot)
    {
        var basis=Revisions.BasisOf(snapshot);var known=snapshot.CombinationRevisions;
        if(known!=null&&known.SameBase(snapshot,basis))return (known.Preparation,known.PreparationBytes);
        var copy=Copy(snapshot);copy.Results=basis;
        copy.CustomCombinations=null;copy.ExcludedCombinationIds=null;
        var (hash,bytes)=copy.FingerprintAndSize();
        snapshot.CombinationRevisions=Revisions.Of(snapshot,basis,hash,bytes);
        return (hash,bytes);
    }

    internal static string PreparationRevision(ModelSnapshot snapshot)=>Preparation(snapshot).Hash;

    /// <summary>
    /// Decision of the user of 1/11/2026 («Revisione per singola definizione»): the revision of the inputs of one definition, the SHA-256 of
    /// a format label, the revision of the preparation (geometry, static cases and every source table: <see cref="PreparationRevision"/>) and
    /// the canonical JSON of the definitions it reads, itself and the nested ones at any depth, in the order of their identifiers. Saving or
    /// deleting a definition changes the revisions of that definition and of the definitions that use it, and no other: the results of the
    /// others stay valid. Until 1/11/2026 the revision was the one of the whole copy without the derived results (every definition included):
    /// the results saved with it are not current any more and are calculated again once (<see cref="WithCurrentResults"/>).
    /// </summary>
    public static string DefinitionRevision(ModelSnapshot snapshot,string definitionId)=>
        DefinitionRevisions(snapshot,Read(snapshot),[definitionId]).TryGetValue(definitionId,out var revision)?revision:throw new InvalidDataException("Combinazione non trovata.");

    const string RevisionFormat="ANTHEA combination revision 2 (1/11/2026)";

    /// <summary>The revisions of the definitions <paramref name="ids"/> that exist among <paramref name="definitions"/> (those of <see cref="Read"/>).</summary>
    static Dictionary<string,string> DefinitionRevisions(ModelSnapshot snapshot,IReadOnlyList<ModelCombinationDefinition> definitions,IEnumerable<string> ids)
    {
        var byId=definitions.ToDictionary(d=>d.Id,StringComparer.Ordinal);var revisions=new Dictionary<string,string>(StringComparer.Ordinal);
        string? preparation=null;
        foreach(string id in ids)
        {
            if(revisions.ContainsKey(id)||!byId.ContainsKey(id))continue;
            preparation??=PreparationRevision(snapshot);
            // The definitions read by the expansion, cycles and missing references included (a missing one enters by its identifier).
            var closure=new SortedSet<string>(StringComparer.Ordinal);var pending=new Stack<string>();pending.Push(id);
            while(pending.Count>0)
            {
                string next=pending.Pop();
                if(!closure.Add(next)||!byId.TryGetValue(next,out var nested))continue;
                foreach(var term in nested.Terms)if(term.IsCombination)pending.Push(term.Reference);
            }
            var read=closure.Select(c=>byId.TryGetValue(c,out var d)?(object)d:new {Missing=c}).ToArray();
            using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            hash.AppendData(Encoding.UTF8.GetBytes(RevisionFormat+"\n"+preparation+"\n"));hash.AppendData(JsonSerializer.SerializeToUtf8Bytes(read));
            revisions[id]=Convert.ToHexString(hash.GetHashAndReset());
        }
        return revisions;
    }

    /// <summary>The definitions whose derived results in the copy are of their current revision.</summary>
    static HashSet<string> CurrentDefinitions(ModelSnapshot snapshot,IReadOnlyList<ModelCombinationDefinition> definitions)
    {
        var derived=snapshot.Results.Where(r=>r.Combination!=null).ToArray();
        if(derived.Length==0)return new(StringComparer.Ordinal);
        var revisions=DefinitionRevisions(snapshot,definitions,derived.Select(r=>r.Combination!.DefinitionId));
        return derived.Where(r=>revisions.TryGetValue(r.Combination!.DefinitionId,out var current)&&current==r.Combination.InputRevision)
            .Select(r=>r.Combination!.DefinitionId).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>The results of the copy without the derived results that are not of the current revision of their definition (definition
    /// changed, removed, or one it uses changed): the static cases and every result still valid, in their order.</summary>
    internal static ModelResultSet[] CurrentResults(ModelSnapshot copy,IReadOnlyList<ModelCombinationDefinition> definitions)
    {
        var current=CurrentDefinitions(copy,definitions);
        var derived=copy.Results.Where(r=>r.Combination!=null).GroupBy(r=>r.Combination!.DefinitionId,StringComparer.Ordinal);
        // A definition is current only with all its results of the current revision (an envelope has two).
        var whole=derived.Where(g=>current.Contains(g.Key)&&g.All(r=>r.Combination!.InputRevision==g.First().Combination!.InputRevision)).Select(g=>g.Key).ToHashSet(StringComparer.Ordinal);
        return copy.Results.Where(r=>r.Combination==null||whole.Contains(r.Combination.DefinitionId)).ToArray();
    }

    /// <summary>
    /// The copy shown by the viewer: the same copy when every derived result is of the current revision of its definition, otherwise a copy
    /// without the others. A document saved before 1/11/2026 has the revisions of the whole copy: its results are not shown as valid, the
    /// catalogue offers them «Da calcolare» and the next change of the copy writes the document without them. Never an error.
    /// </summary>
    public static ModelSnapshot WithCurrentResults(ModelSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if(!snapshot.Results.Any(r=>r.Combination!=null))return snapshot;
        ModelResultSet[] results;
        try {results=CurrentResults(snapshot,Read(snapshot));}
        catch(Exception ex) when(ex is InvalidDataException or ArgumentException or InvalidOperationException or NotSupportedException or KeyNotFoundException)
        {results=snapshot.Results.Where(r=>r.Combination==null).ToArray();}
        if(results.Length==snapshot.Results.Length)return snapshot;
        var copy=Copy(snapshot);copy.Results=results;return copy;
    }

    public static ModelSnapshot Attach(ModelSnapshot snapshot,string definitionId,ModelCombinationCalculation calculated)
    {
        var copy=Copy(snapshot);
        var definitions=Read(snapshot);
        if(calculated.Results.Length==0||!definitions.Any(d=>d.Id==definitionId))throw new InvalidDataException("Il modello o la definizione sono cambiati durante il calcolo. Ricalcola la combinazione.");
        string revision=DefinitionRevision(snapshot,definitionId);
        if(calculated.Results.Any(r=>r.Combination?.DefinitionId!=definitionId||r.Combination.InputRevision!=revision))
            throw new InvalidDataException("Il modello o la definizione sono cambiati durante il calcolo. Ricalcola la combinazione.");
        // The other derived results of their current revision (all of them when the copy came from the viewer).
        var original=CurrentResults(snapshot,definitions).Where(r=>r.Combination?.DefinitionId!=definitionId).ToArray();
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

    /// <summary>Length of the canonical JSON of the copy. With the revision of the preparation of its parts known, the JSON is that of the
    /// preparation with the definitions of the user inserted (each property with its comma, after Results) and the derived results inserted in
    /// the array of the results, each with its JSON and one comma (the array already has the static cases): the same length as the complete
    /// serialization, without serializing the static cases again.</summary>
    internal static long SerializedSize(ModelSnapshot model)
    {
        var basis=Revisions.BasisOf(model);
        if(basis.Length>0&&model.CombinationRevisions is {} known&&known.SameBase(model,basis))
            return known.PreparationBytes+Property(nameof(ModelSnapshot.CustomCombinations),model.CustomCombinations)+
                Property(nameof(ModelSnapshot.ExcludedCombinationIds),model.ExcludedCombinationIds)+model.Results.Where(r=>r.Combination!=null).Sum(r=>Size(r)+1);
        return Size(model);
        static long Property<T>(string name,T? value) where T:class=>value==null?0:1+Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(name))+1+Size(value);
    }
    static long Size<T>(T value)
    {using var counter=new ModelSnapshot.ByteCounter();JsonSerializer.Serialize(counter,value);return counter.Length;}
}
