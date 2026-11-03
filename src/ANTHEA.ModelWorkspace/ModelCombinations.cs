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
    ModelCombinationGoverning[] Governing, string[]? Alternatives = null)
{
    // 1/11/2026: governing states of the principal components of the plates (GPC, PlatePrincipals) of an envelope, their alternatives
    // «P1», «P2»… in PrincipalAlternatives. Optional and omitted when null: the results calculated before have no principal components.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ModelCombinationGoverning[]? PrincipalGoverning { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string[]? PrincipalAlternatives { get; init; }
}
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
        var definitions = new List<ModelCombinationDefinition>(TableDefinitions(snapshot));
        // Sources without MIDAS tables carry their definitions in the document (Straus7: LSA combinations).
        definitions.AddRange(snapshot.SourceCombinations?.Definitions??[]);
        var overrides=(snapshot.CustomCombinations??[]).Select(d=>d.Id).ToHashSet(StringComparer.Ordinal);
        var excluded=(snapshot.ExcludedCombinationIds??[]).ToHashSet(StringComparer.Ordinal);
        definitions.RemoveAll(d=>overrides.Contains(d.Id)||excluded.Contains(d.Id));
        definitions.AddRange((snapshot.CustomCombinations??[]).Where(d=>!excluded.Contains(d.Id)));
        ValidateDefinitions(definitions);
        return definitions.ToArray();
    }

    /// <summary>2/11/2026 (series of the combinations): the definitions of the MIDAS tables, read once for each dictionary of the tables and kept by
    /// the copy and the copies made from it (the copies share the dictionary and never change it, <see cref="Revisions"/>). The same records at
    /// every reading, so their revisions are computed once (<see cref="DefinitionRevisions"/>); a table that cannot be read throws at every
    /// reading, as before.</summary>
    internal sealed record KnownTables(Dictionary<string,JsonElement> Tables,ModelCombinationDefinition[] Definitions);
    static ModelCombinationDefinition[] TableDefinitions(ModelSnapshot snapshot)
    {
        if(snapshot.SourceTables is not {} tables)return [];
        if(snapshot.CombinationTables is {} known&&ReferenceEquals(known.Tables,tables))return known.Definitions;
        var read=ReadTables(tables);snapshot.CombinationTables=new(tables,read);return read;
    }
    static ModelCombinationDefinition[] ReadTables(Dictionary<string,JsonElement> tables)
    {
        var definitions = new List<ModelCombinationDefinition>();
        foreach (string table in Tables.Values)
        {
            if (!tables.TryGetValue(table, out var rows)) continue;
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
        // 2/11/2026: the result sets the archive beside the project did not give back, with the reason (ProjectModelStore.Read).
        var unread=snapshot.UnavailableResults;
        var definitions=Read(snapshot);var records=definitions.Select(ToGpc).ToArray();
        snapshot=Upgraded(snapshot,definitions); // results of 1.16.2 still current: «Calcolata» (decision of 1/11/2026 evening)
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
                // 1/11/2026 (point 8): over the limit of the expansion the combination is calculated term by term (GpcModelCombinationService).
                var expansion=AlternativeCount(byId,d.Id,counts);
                var missing=required.Where(c=>!cases.Contains(c)).ToArray();
                if(missing.Length>0)return new(d,"Casi mancanti","Risultati da importare: "+string.Join(", ",missing)+Unread(unread.FirstOrDefault(u=>u.Definition==null&&missing.Contains(u.Name))),required,false);
                if(!GpcSnapshotModel.CanRebuild(snapshot))
                    return new(d,"Sorgente incompleta","Il collegamento GPC richiede le tabelle sorgente MIDAS oppure le terne locali e i casi della sorgente importata. La definizione può essere conservata.",required,false);
                bool ready=calculated.Contains(d.Id);
                var lost=ready?null:unread.FirstOrDefault(u=>u.Definition==d.Id);
                return new(d,ready?"Calcolata":"Da calcolare",(ready?"Risultati locali disponibili nella copia del modello.":lost is null?"Casi base disponibili; costruzione locale con GPC.":
                    "Risultati non letti dall'archivio dei risultati ("+lost.Reason+"): calcola di nuovo la combinazione.")+
                    (expansion.Over?" "+ExpansionMessage(expansion.Count):""),required,true);
            }
            catch(Exception ex) when(ex is ArgumentException or InvalidOperationException or NotSupportedException)
            { return new(d,"Non disponibile",Explain(ex),[],false); }
        }).ToArray();
    }

    static string Unread(ModelUnavailableResult? lost)=>lost is null?"":". Non letti dall'archivio dei risultati: "+lost.Reason;

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

    /// <summary>1/11/2026 (point 8): true when the definition has more alternatives than the expansion allows, so that GPC finds its extremes term
    /// by term (no principal components of the plates); false for an unknown definition.</summary>
    public static bool ExceedsExpansion(ModelSnapshot snapshot,string definitionId)
    {
        var byId=Read(snapshot).ToDictionary(d=>d.Id,StringComparer.Ordinal);
        return byId.ContainsKey(definitionId)&&AlternativeCount(byId,definitionId,new(StringComparer.Ordinal)).Over;
    }

    internal const string ZeroMessage="Tutti i coefficienti nulli (anche dopo la somma dei termini): nessun caso da combinare. Indica almeno un coefficiente diverso da zero.";

    internal static string ExpansionMessage(long count)=>$"Oltre {MaximumAlternatives} stati alternativi ({(count>=1_000_000_000_000L?"più di 10¹²":count.ToString(System.Globalization.CultureInfo.InvariantCulture))}): "+
        "GPC cerca gli estremi termine per termine, senza elencare gli stati; governanti completi. Componenti principali dei plate non disponibili: richiedono ogni stato.";

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
        snapshot=Upgraded(snapshot,Read(snapshot));
        var custom=(snapshot.CustomCombinations??[]).Where(d=>d.Id!=definition.Id).Append(definition with {Terms=definition.Terms.ToArray()}).ToArray();
        if(custom.Any(d=>d.Id!=definition.Id&&d.Name==definition.Name))throw new InvalidDataException("Esiste già una combinazione ANTHEA con questo nome.");
        var copy=Copy(snapshot);copy.CustomCombinations=custom;
        var all=Read(copy);var records=all.Select(ToGpc).ToArray();
        // COMBO-B4 (1/11/2026): the results are named «name [source]»: an imported definition may not take the name of another of its source.
        if(all.Any(d=>d.Id!=definition.Id&&d.Source==definition.Source&&d.Name==definition.Name))
            throw new InvalidDataException("Esiste già una combinazione "+definition.Source+" con questo nome: i risultati avrebbero lo stesso nome.");
        // Missing results are allowed while editing; missing definitions/cases and cycles are not.
        var required=CombinationExpansion.StaticCases(records,definition.Id);
        var known=StaticCaseNames(snapshot).ToHashSet(StringComparer.Ordinal);
        if(required.Any(c=>!known.Contains(c)))throw new InvalidDataException("La combinazione contiene un caso non presente nel modello.");
        copy.Results=CurrentResults(copy,all);copy.Validate();return copy;
    }

    public static ModelSnapshot Remove(ModelSnapshot snapshot,string id)
    {
        var all=Read(snapshot);
        snapshot=Upgraded(snapshot,all);
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
        // 1/11/2026: the choices of the generator.
        GeneratorCases=s.GeneratorCases,
        // 2/11/2026: the result sets the archive did not give back at the opening, for the catalogue.
        UnavailableResults=s.UnavailableResults,
        // The copy shares the arrays of the original: its revisions hold while the parts are the same references (Revisions).
        CombinationRevisions=s.CombinationRevisions,
        // 2/11/2026 (series of the combinations): the checks, lengths, tables and revisions already known for the same references.
        Checked=s.Checked,CombinationSizes=s.CombinationSizes,CombinationTables=s.CombinationTables,CombinationDefinitionRevisions=s.CombinationDefinitionRevisions};

    public static ModelSnapshot RestoreImported(ModelSnapshot snapshot)
    {
        snapshot=Upgraded(snapshot,Read(snapshot));
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
        copy.CustomCombinations=null;copy.ExcludedCombinationIds=null;copy.GeneratorCases=null;
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
    /// the results saved with it stay current while that revision is still the one of the copy (<see cref="Upgraded"/>), otherwise they are
    /// calculated again once (<see cref="WithCurrentResults"/>).
    /// </summary>
    public static string DefinitionRevision(ModelSnapshot snapshot,string definitionId)=>
        DefinitionRevisions(snapshot,Read(snapshot),[definitionId]).TryGetValue(definitionId,out var revision)?revision:throw new InvalidDataException("Combinazione non trovata.");

    const string RevisionFormat="ANTHEA combination revision 2 (1/11/2026)";

    /// <summary>The revisions of the definitions <paramref name="ids"/> that exist among <paramref name="definitions"/> (those of <see cref="Read"/>).</summary>
    static Dictionary<string,string> DefinitionRevisions(ModelSnapshot snapshot,IReadOnlyList<ModelCombinationDefinition> definitions,IEnumerable<string> ids)
    {
        var byId=definitions.ToDictionary(d=>d.Id,StringComparer.Ordinal);var revisions=new Dictionary<string,string>(StringComparer.Ordinal);
        string? preparation=null;var known=snapshot.CombinationDefinitionRevisions;Dictionary<string,KnownRevision>? computed=null;
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
            var read=closure.ToArray();var records=read.Select(c=>byId.GetValueOrDefault(c)).ToArray();
            if(known!=null&&known.TryGetValue(id,out var same)&&same.Preparation==preparation&&same.Ids.SequenceEqual(read,StringComparer.Ordinal)&&
                same.Records.SequenceEqual(records,ReferenceEqualityComparer.Instance)){revisions[id]=same.Revision;continue;}
            var json=read.Select(c=>byId.TryGetValue(c,out var d)?(object)d:new {Missing=c}).ToArray();
            using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            hash.AppendData(Encoding.UTF8.GetBytes(RevisionFormat+"\n"+preparation+"\n"));hash.AppendData(JsonSerializer.SerializeToUtf8Bytes(json));
            revisions[id]=Convert.ToHexString(hash.GetHashAndReset());
            (computed??=new(StringComparer.Ordinal))[id]=new(preparation,read,records,revisions[id]);
        }
        if(computed!=null)
        {
            // Kept by the copy and the copies made from it: the revisions known before with those computed now.
            var all=known?.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal)??new(StringComparer.Ordinal);
            foreach(var (id,revision) in computed)all[id]=revision;
            snapshot.CombinationDefinitionRevisions=all;
        }
        return revisions;
    }

    /// <summary>2/11/2026 (series of the combinations): the revision of a definition with the preparation and the records it was computed from (the
    /// definitions it reads, in the order of their identifiers; null for a missing one), kept by the copy. The same records give the same JSON,
    /// hence the same revision (records never changed in place, as for <see cref="Revisions"/>). Attach computed again the revision of every
    /// derived result of the copy, twice, a time growing with the series.</summary>
    internal sealed record KnownRevision(string Preparation,string[] Ids,ModelCombinationDefinition?[] Records,string Revision);

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
    /// without the others. A document saved until 1.16.2 has the revisions of the whole copy: its results still current take the revision of
    /// their definition (<see cref="Upgraded"/>) and stay «Calcolata»; the others are not shown as valid, the catalogue offers them «Da
    /// calcolare» and the next change of the copy writes the document without them. Never an error.
    /// </summary>
    public static ModelSnapshot WithCurrentResults(ModelSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if(!snapshot.Results.Any(r=>r.Combination!=null))return snapshot;
        ModelResultSet[] results;var upgraded=snapshot;
        try {var definitions=Read(snapshot);upgraded=Upgraded(snapshot,definitions);results=CurrentResults(upgraded,definitions);}
        catch(Exception ex) when(ex is InvalidDataException or ArgumentException or InvalidOperationException or NotSupportedException or KeyNotFoundException)
        {upgraded=snapshot;results=snapshot.Results.Where(r=>r.Combination==null).ToArray();}
        if(ReferenceEquals(upgraded,snapshot)&&results.Length==snapshot.Results.Length)return snapshot;
        var copy=Copy(upgraded);copy.Results=results;return copy;
    }

    /// <summary>
    /// Decision of the user of 1/11/2026 evening («Combinazioni calcolate con le versioni precedenti → Calcolate + avviso»), after COMBO-REV-01.
    /// A derived result saved until 1.16.2 carries the revision of format 1: the one of the whole copy without the derived results, every
    /// definition included (<see cref="WholeCopyRevision"/>). When it is the revision of the copy, computed exactly as 1.16.2 did, the result
    /// is still current: it keeps its values and takes the revision of its definition (format 2), which the document receives the first time
    /// the copy is written. A copy changed since (a definition, a case, the geometry) or written differently gives another revision: those
    /// results are calculated again, as before. The principal components that 1.16.2 did not calculate stay missing, as for every result
    /// without them. The same copy when no result needs it; the revision of the whole copy is computed only for results not current.
    /// </summary>
    internal static ModelSnapshot Upgraded(ModelSnapshot snapshot,IReadOnlyList<ModelCombinationDefinition> definitions)
    {
        var derived=snapshot.Results.Where(r=>r.Combination!=null).ToArray();
        if(derived.Length==0)return snapshot;
        var revisions=DefinitionRevisions(snapshot,definitions,derived.Select(r=>r.Combination!.DefinitionId));
        if(derived.All(r=>!revisions.TryGetValue(r.Combination!.DefinitionId,out var current)||current==r.Combination.InputRevision))return snapshot;
        string whole=WholeCopyRevision(snapshot);
        if(!derived.Any(r=>r.Combination!.InputRevision==whole&&revisions.ContainsKey(r.Combination.DefinitionId)))return snapshot;
        var copy=Copy(snapshot);
        copy.Results=[..snapshot.Results.Select(r=>r.Combination is {} c&&c.InputRevision==whole&&revisions.TryGetValue(c.DefinitionId,out var current)
            ?r with {Combination=c with {InputRevision=current}}:r)];
        return copy;
    }

    /// <summary>The revision of format 1, ModelCombinations.InputRevision until 1.16.2: the SHA-256 of the canonical JSON of the copy without
    /// its derived results, the definitions of the user included. The choices of the generator (1/11/2026) were not part of the copy then.</summary>
    internal static string WholeCopyRevision(ModelSnapshot snapshot)
    {
        var copy=Copy(snapshot);copy.Results=Revisions.BasisOf(snapshot);copy.GeneratorCases=null;
        return copy.Fingerprint();
    }

    public static ModelSnapshot Attach(ModelSnapshot snapshot,string definitionId,ModelCombinationCalculation calculated)=>
        Attach(snapshot,definitionId,calculated,ModelSnapshotFiles.MaximumPackageBytes);

    /// <summary>The copy with the results of <paramref name="definitionId"/>, within <paramref name="copyBytes"/> of JSON: the oldest derived
    /// results of other definitions leave the copy while it is over. 3/11/2026 (request of the user, «Sì, opzione 1»): the whole copy is in
    /// the document within <see cref="ModelSnapshotFiles.MaximumPackageBytes"/>; with the results archive of the project the copy may take
    /// <see cref="ModelResultArchive.ArchivedCopyBytes"/>, and every result set, one block of the archive, stays within the limit of a
    /// package.</summary>
    public static ModelSnapshot Attach(ModelSnapshot snapshot,string definitionId,ModelCombinationCalculation calculated,long copyBytes)
    {
        var definitions=Read(snapshot);
        snapshot=Upgraded(snapshot,definitions);
        var copy=Copy(snapshot);
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
        long total=SerializedSize(copy);
        if(calculated.Results.Any(r=>ResultLength(copy,r)>ModelSnapshotFiles.MaximumPackageBytes))
            throw new InvalidDataException("Un risultato della combinazione supera il limite di un blocco del modello (256 MiB). Riduci il numero di casi base nella copia.");
        while(total>copyBytes)
        {
            string? oldest=copy.Results.FirstOrDefault(r=>r.Combination!=null&&r.Combination.DefinitionId!=definitionId)?.Combination?.DefinitionId;
            if(oldest==null)throw new InvalidDataException($"Il risultato supera lo spazio disponibile per il modello ({CopyLimit(copyBytes)}). Riduci il numero di casi base nella copia.");
            copy.Results=copy.Results.Where(r=>r.Combination?.DefinitionId!=oldest).ToArray();
            total=SerializedSize(copy);
        }
        return copy;
    }

    /// <summary>The limit <paramref name="copyBytes"/> as written in the messages.</summary>
    public static string CopyLimit(long copyBytes)=>copyBytes==ModelResultArchive.ArchivedCopyBytes?ModelResultArchive.ArchivedCopyLimit:"256 MiB";

    /// <summary>The length of the JSON of a result set of a copy whose lengths were just measured (<see cref="SerializedSize"/>).</summary>
    static long ResultLength(ModelSnapshot copy,ModelResultSet result)=>
        copy.CombinationSizes?.Lengths.TryGetValue(result,out long length)==true?length:Size(result);

    /// <summary>Length of the canonical JSON of the copy. With the revision of the preparation of its parts known, the JSON is that of the
    /// preparation with the definitions of the user inserted (each property with its comma, after Results) and the derived results inserted in
    /// the array of the results, each with its JSON and one comma (the array already has the static cases): the same length as the complete
    /// serialization, without serializing the static cases again.</summary>
    internal static long SerializedSize(ModelSnapshot model)
    {
        var basis=Revisions.BasisOf(model);
        if(basis.Length==0||model.CombinationRevisions is not {} known||!known.SameBase(model,basis))return Size(model);
        var measured=model.CombinationSizes?.Lengths;var lengths=new Dictionary<object,long>(ReferenceEqualityComparer.Instance);
        long total=known.PreparationBytes+Property(nameof(ModelSnapshot.CustomCombinations),model.CustomCombinations)+
            Property(nameof(ModelSnapshot.ExcludedCombinationIds),model.ExcludedCombinationIds)+Property(nameof(ModelSnapshot.GeneratorCases),model.GeneratorCases)+
            model.Results.Where(r=>r.Combination!=null).Sum(r=>Length(r)+1);
        model.CombinationSizes=new(lengths);
        return total;
        long Property<T>(string name,T? value) where T:class=>value==null?0:1+Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(name))+1+Length(value);
        long Length<T>(T value) where T:class
        {
            if(measured==null||!measured.TryGetValue(value,out long length))length=Size(value);
            return lengths[value]=length;
        }
    }

    /// <summary>2/11/2026 (series of the combinations): the lengths of the JSON of the derived results and of the lists of the definitions of a copy,
    /// by reference, kept by the copy and the copies made from it (records and arrays never changed in place, as for <see cref="Revisions"/>).
    /// Each Attach measured again every derived result of the copy, a time growing with the series: from 0.24 to 0.74 s for each Attach of a
    /// series of 120 combinations of the 500 × 100 model.</summary>
    internal sealed record KnownSizes(IReadOnlyDictionary<object,long> Lengths);
    static long Size<T>(T value)
    {using var counter=new ModelSnapshot.ByteCounter();JsonSerializer.Serialize(counter,value);return counter.Length;}
}
