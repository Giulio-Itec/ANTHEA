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
        var cases=snapshot.Results.Where(r=>!r.IsEnvelope&&r.Combination==null).Select(r=>r.Name).ToHashSet(StringComparer.Ordinal);
        return definitions.Select(d=>
        {
            try
            {
                var required=CombinationExpansion.StaticCases(records,d.Id).ToArray();
                if(required.Length==0) return new ModelCombinationAvailability(d,"Vuota","Aggiungi almeno un caso di carico.",required,false);
                var missing=required.Where(c=>!cases.Contains(c)).ToArray();
                if(missing.Length>0)return new(d,"Casi mancanti","Risultati da importare: "+string.Join(", ",missing),required,false);
                if(snapshot.SourceTables==null||new[]{"UNIT","NODE","ELEM","STLD"}.Any(t=>!snapshot.SourceTables.ContainsKey(t)))
                    return new(d,"Sorgente incompleta","Il collegamento GPC richiede le tabelle sorgente MIDAS. La definizione può essere conservata.",required,false);
                bool ready=snapshot.Results.Any(r=>r.Combination?.DefinitionId==d.Id&&r.Combination.InputRevision==revision);
                return new(d,ready?"Calcolata":"Da calcolare",ready?"Risultati locali disponibili nella copia del modello.":"Casi base disponibili; costruzione locale con GPC.",required,true);
            }
            catch(Exception ex) when(ex is ArgumentException or InvalidOperationException or NotSupportedException)
            { return new(d,"Non disponibile",Explain(ex),[],false); }
        }).ToArray();
    }

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
        if(names.Any(string.IsNullOrWhiteSpace)||names.Distinct(StringComparer.Ordinal).Count()!=names.Length)
            throw new InvalidDataException("Nomi dei casi di carico mancanti o duplicati nella tabella sorgente.");
        return names.Concat(snapshot.Results.Where(r=>!r.IsEnvelope&&r.Combination==null).Select(r=>r.Name)).Distinct(StringComparer.Ordinal).ToArray();
    }

    internal static ModelSnapshot Copy(ModelSnapshot s)=>new()
    {Version=s.Version,Name=s.Name,Source=s.Source,Nodes=s.Nodes,Elements=s.Elements,Plates=s.Plates,Sections=s.Sections,
        Results=s.Results,SourceTables=s.SourceTables,LocalFrames=s.LocalFrames,NodeFrames=s.NodeFrames,Boundaries=s.Boundaries,
        ImportMessages=s.ImportMessages,UpdateInfo=s.UpdateInfo,CustomCombinations=s.CustomCombinations,ExcludedCombinationIds=s.ExcludedCombinationIds};

    public static ModelSnapshot RestoreImported(ModelSnapshot snapshot)
    {
        var copy=Copy(snapshot);copy.ExcludedCombinationIds=null;copy.Results=copy.Results.Where(r=>r.Combination==null).ToArray();copy.Validate();return copy;
    }

    public static string InputRevision(ModelSnapshot snapshot)
    {
        var basis=Copy(snapshot);basis.Results=basis.Results.Where(r=>r.Combination==null).ToArray();
        return basis.Fingerprint();
    }

    internal static string PreparationRevision(ModelSnapshot snapshot)
    {
        var basis=Copy(snapshot);basis.Results=basis.Results.Where(r=>r.Combination==null).ToArray();
        basis.CustomCombinations=null;basis.ExcludedCombinationIds=null;
        return basis.Fingerprint();
    }

    public static ModelSnapshot Attach(ModelSnapshot snapshot,string definitionId,ModelCombinationCalculation calculated)
    {
        var copy=Copy(snapshot);
        string revision=InputRevision(snapshot);
        if(calculated.Results.Length==0||!Read(snapshot).Any(d=>d.Id==definitionId)||calculated.Results.Any(r=>r.Combination?.DefinitionId!=definitionId||r.Combination.InputRevision!=revision))
            throw new InvalidDataException("Il modello o la definizione sono cambiati durante il calcolo. Ricalcola la combinazione.");
        var original=snapshot.Results.Where(r=>r.Combination?.DefinitionId!=definitionId).ToArray();
        if(calculated.Results.Any(r=>original.Any(old=>old.Name==r.Name)))throw new InvalidDataException("Il nome del risultato calcolato coincide con un risultato esistente.");
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

    static long SerializedSize(ModelSnapshot model)
    {using var counter=new SizeStream();JsonSerializer.Serialize(counter,model);return counter.Length;}
    sealed class SizeStream:Stream
    {
        long count;
        public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>true;
        public override long Length=>count;public override long Position{get=>count;set=>throw new NotSupportedException();}
        public override void Write(byte[] buffer,int offset,int size)=>count+=size;
        public override void Write(ReadOnlySpan<byte> buffer)=>count+=buffer.Length;
        public override void Flush(){}public override int Read(byte[] buffer,int offset,int size)=>throw new NotSupportedException();
        public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();public override void SetLength(long value)=>throw new NotSupportedException();
    }
}
