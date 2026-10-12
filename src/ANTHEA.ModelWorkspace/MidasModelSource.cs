using System.Net.Http;
using System.Text.Json;

namespace ANTHEA.ModelWorkspace;

/// <summary>Acquires an isolated copy and rechecks source tables before publishing it.</summary>
public sealed class MidasModelSource : IMidasModelSource, IDisposable
{
    static readonly string[] CatalogTables = ["UNIT", "NODE", "ELEM", "STLD"];
    static readonly string[] GeometryTables = ["UNIT", "NODE", "ELEM", "STLD", "SECT", "THIK", "SKEW", "OFFS"];
    static readonly string[] AllTables = ["UNIT", "NODE", "ELEM", "STLD", "SECT", "THIK", "MATL", "GRUP", "SKEW", "CONS", "OFFS", "FRLS", "PRLS", "ELNK", "RIGD", "MCON", "NSPR", "NLNK", "NLLP", "STAG", "BNGR", "LDGR", "CNLD", "BMLD", "PRES", "BODF", "NBOF", "LCOM-GEN", "LCOM-CONC", "LCOM-STEEL", "LCOM-SRC", "LCOM-STLCOMP", "LCOM-SEISMIC"];
    readonly HttpClient http;
    readonly bool ownsHttp;
    readonly TimeSpan databaseTimeout;
    readonly TimeSpan resultsTimeout;
    public MidasModelSource() : this(new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan }, true) { }
    /// <summary>Injected clients are caller-owned and must disable redirects. Intended also for offline protocol tests.</summary>
    public MidasModelSource(HttpClient http, TimeSpan? databaseTimeout = null, TimeSpan? resultsTimeout = null)
        : this(http, false, databaseTimeout, resultsTimeout) { }
    MidasModelSource(HttpClient http, bool ownsHttp, TimeSpan? databaseTimeout = null, TimeSpan? resultsTimeout = null)
    { this.http = http; this.ownsHttp = ownsHttp; this.databaseTimeout = databaseTimeout ?? TimeSpan.FromSeconds(30); this.resultsTimeout = resultsTimeout ?? TimeSpan.FromSeconds(180); }
    public void Dispose() { if (ownsHttp) http.Dispose(); }

    public async Task<MidasModelCatalog> InspectAsync(MidasConnection connection, IProgress<ModelImportProgress>? progress, CancellationToken token)
    {
        var client = new MidasReadOnlyClient(http, connection, databaseTimeout, resultsTimeout);
        var tables = new Dictionary<string, JsonElement>();
        foreach (string table in CatalogTables)
        {
            progress?.Report(new("Controllo del modello · " + table, tables.Count, CatalogTables.Length));
            tables.Add(table, await client.DatabaseAsync(table, token).ConfigureAwait(false));
        }
        var catalog = Catalog(tables);
        progress?.Report(new(catalog.Summary, 4, 4));
        return catalog;
    }

    public async Task<ModelSnapshot> ReadAsync(MidasImportRequest request, IProgress<ModelImportProgress>? progress, CancellationToken token)
    {
        if (!Enum.IsDefined(request.Scope)) throw new ArgumentException("Modalità di importazione non valida.");
        bool results = request.Scope is ModelUpdateScope.Everything or ModelUpdateScope.Results;
        var cases = request.StaticCases.ToArray();
        if (results && (cases.Length == 0 || cases.Distinct(StringComparer.Ordinal).Count() != cases.Length || !request.Forces && !request.Displacements))
            throw new InvalidDataException("Seleziona almeno un caso e un tipo di risultato, oppure scegli Geometria + attributi + carichi.");
        var client = new MidasReadOnlyClient(http, request.Connection, databaseTimeout, resultsTimeout);
        var names = request.Scope == ModelUpdateScope.Geometry ? GeometryTables : AllTables;
        var tables = new Dictionary<string, JsonElement>();
        var temporary = Directory.CreateTempSubdirectory("anthea-midas-");
        long bytes = 0;
        async Task Store(string file, JsonElement data)
        {
            byte[] content = JsonSerializer.SerializeToUtf8Bytes(data); bytes += content.Length;
            if (bytes > 512_000_000) throw new InvalidDataException("Acquisizione troppo grande. Riduci i casi selezionati.");
            await File.WriteAllBytesAsync(Path.Combine(temporary.FullName, file + ".json"), content, token).ConfigureAwait(false);
        }
        try
        {
            foreach (string table in names)
            {
                progress?.Report(new("Lettura del modello · " + table, 0, 0));
                var value = await client.DatabaseAsync(table, token).ConfigureAwait(false);
                tables.Add(table, value);
                await Store(table, JsonSerializer.SerializeToElement(new Dictionary<string, JsonElement> { [table] = value })).ConfigureAwait(false);
                if (tables.Count == CatalogTables.Length)
                {
                    var catalog = Catalog(tables);
                    if (catalog.Signature != request.ExpectedSignature) throw new InvalidDataException("Il modello o l’elenco dei casi è cambiato. Premi Controlla connessione e casi prima di importare.");
                    if (results && cases.Any(c => !catalog.StaticCases.Contains(c, StringComparer.Ordinal))) throw new InvalidDataException("Un caso selezionato non esiste più in MIDAS. Rileggi l’elenco dei casi.");
                }
            }
            // Reuse the already-tested file adapter. It performs no HTTP or structural computation.
            var geometry = MidasSnapshotImporter.ReadDirectory(temporary.FullName);
            int families = request.Forces ? new[] { "PLATE", "BEAM" }.Count(f => geometry.Elements.Any(e => e.Type == f)) : 0;
            int total = names.Length * 2 + (results ? cases.Length * (families + (request.Displacements ? 1 : 0)) : 0) + 1;
            int completed = names.Length;
            int fileNumber = 0;
            if (results)
            foreach (string name in cases)
            {
                if (request.Forces)
                foreach (string family in new[] { "PLATE", "BEAM" })
                {
                    int[] elements = geometry.Elements.Where(e => e.Type == family).Select(e => e.Id).ToArray();
                    if (elements.Length == 0) continue;
                    progress?.Report(new($"Sollecitazioni {family} · {name}", completed, total));
                    var argument = Argument(family == "PLATE" ? "PLATEFORCEUL" : "BEAMFORCE", name, elements);
                    if (family == "PLATE")
                    {
                        argument["AVERAGE_NODAL_RESULT"] = false;
                        argument["NODE_FLAG"] = new { CENTER = false, NODES = true };
                        argument["COMPONENTS"] = new[] { "Elem", "Load", "Node", "Mxx", "Myy", "Mxy", "Fxx", "Fyy", "Fxy", "Vxx", "Vyy" };
                    }
                    var response = await client.ResultsAsync(argument, token).ConfigureAwait(false);
                    CheckResultCase(response, name);
                    await Store(family.ToLowerInvariant() + "-results-live-" + fileNumber++, response).ConfigureAwait(false);
                    completed++;
                }
                if (request.Displacements)
                {
                    progress?.Report(new("Spostamenti globali · " + name, completed, total));
                    var argument = Argument("DISPLACEMENTG", name, geometry.Nodes.Select(n => n.Id).ToArray());
                    argument["COMPONENTS"] = new[] { "Node", "Load", "DX", "DY", "DZ" };
                    var response = await client.ResultsAsync(argument, token).ConfigureAwait(false);
                    CheckResultCase(response, name);
                    await Store("displacements-global-" + fileNumber++, response).ConfigureAwait(false);
                    completed++;
                }
            }
            foreach (string table in names)
            {
                progress?.Report(new("Controllo finale del modello · " + table, completed, total));
                var current = await client.DatabaseAsync(table, token).ConfigureAwait(false);
                if (MidasReadOnlyClient.Signature(new Dictionary<string, JsonElement> { [table] = current }) !=
                    MidasReadOnlyClient.Signature(new Dictionary<string, JsonElement> { [table] = tables[table] }))
                    throw new InvalidDataException("Il modello è stato modificato durante l’importazione (" + table + "). La copia precedente è conservata: ripeti la lettura.");
                completed++;
            }
            token.ThrowIfCancellationRequested();
            progress?.Report(new("Preparazione e controllo della copia", completed, total));
            var snapshot = results ? MidasSnapshotImporter.ReadDirectory(temporary.FullName) : geometry;
            if (results) CheckCoverage(snapshot, cases, request.Forces, request.Displacements);
            snapshot.Source = "MIDAS Civil NX · API · " + DateTimeOffset.UtcNow.ToString("u");
            snapshot.ImportMessages = [
                new("MIDAS-LIVE", "info", "Importazione", "Lettura API completata; tabelle del modello ricontrollate. Casi statici selezionati: " + (results ? cases.Length : 0) + "."),
                new("MIDAS-CONVERTER-PENDING", "warning", "Geometria", "Le tabelle originali sono conservate. La normalizzazione GPC di terne locali, vincoli, rilasci e link richiede il convertitore nel set di librerie distribuito; i relativi simboli non sono ancora acquisiti da questo collegamento."),
                new("MIDAS-RESULT-SCOPE", "info", "Risultati", "Sollecitazioni locali PLATE/BEAM e traslazioni nodali globali dei casi statici. Combinazioni conservate come definizioni; inviluppi, fasi e risultati di altre famiglie non acquisiti.")];
            snapshot.Validate(); token.ThrowIfCancellationRequested();
            progress?.Report(new("Acquisizione completata", total, total));
            return snapshot;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or FormatException or OverflowException)
        { throw new InvalidDataException("Il modello MIDAS contiene dati non gestiti da questa versione dell’importatore. Nessuna copia è stata sostituita."); }
        finally
        {
            // Created by this call, never obtained from the user or the remote response.
            try { temporary.Delete(true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    static MidasModelCatalog Catalog(Dictionary<string, JsonElement> tables)
    {
        int nodes = tables["NODE"].EnumerateObject().Count(), elements = tables["ELEM"].EnumerateObject().Count();
        if (nodes == 0) throw new InvalidDataException("Il modello aperto in MIDAS non contiene nodi.");
        var cases = tables["STLD"].EnumerateObject().Select(p => p.Value.GetProperty("NAME").GetString() ?? "").ToArray();
        if (cases.Any(string.IsNullOrWhiteSpace) || cases.Distinct(StringComparer.Ordinal).Count() != cases.Length) throw new InvalidDataException("L’elenco dei casi MIDAS contiene nomi mancanti o duplicati.");
        return new(nodes, elements, cases, tables["ELEM"].EnumerateObject().Select(p => p.Value.GetProperty("TYPE").GetString() ?? "").Distinct().ToArray(),
            MidasReadOnlyClient.Signature(tables.Where(p => CatalogTables.Contains(p.Key))));
    }
    static Dictionary<string, object> Argument(string type, string name, int[] ids) => new()
    {
        ["TABLE_NAME"] = "ANTHEAResults", ["TABLE_TYPE"] = type,
        ["UNIT"] = new { FORCE = "N", DIST = "mm" }, ["STYLES"] = new { FORMAT = "Scientific", PLACE = 12 },
        ["NODE_ELEMS"] = new { KEYS = ids }, ["LOAD_CASE_NAMES"] = new[] { name + "(ST)" }
    };
    static void CheckResultCase(JsonElement response, string name)
    {
        if (!response.TryGetProperty("ANTHEAResults", out var table) || !table.TryGetProperty("HEAD", out var head) || !table.TryGetProperty("DATA", out var data) || data.GetArrayLength() == 0)
            throw new InvalidDataException("Risultati non disponibili per il caso «" + name + "». Controlla l’analisi in MIDAS o importa senza risultati.");
        var headers = head.EnumerateArray().Select(h => h.GetString()).ToArray();
        int load = Array.IndexOf(headers, "Load");
        if (load < 0 || headers.Distinct().Count() != headers.Length || headers.Contains("Stage") || headers.Contains("Step") ||
            data.EnumerateArray().Any(r => r.GetArrayLength() != headers.Length || r[load].GetString() != name))
            throw new InvalidDataException("MIDAS ha restituito risultati diversi dal caso statico richiesto. Nessun aggiornamento applicato.");
    }
    static void CheckCoverage(ModelSnapshot snapshot, string[] names, bool forces, bool displacements)
    {
        if (!snapshot.Results.Select(r => r.Name).ToHashSet(StringComparer.Ordinal).SetEquals(names)) throw new InvalidDataException("Risultati incompleti per i casi selezionati.");
        foreach (var result in snapshot.Results)
        {
            if (displacements && (result.Displacements?.Select(d => d.Node).ToHashSet().SetEquals(snapshot.Nodes.Select(n => n.Id)) != true)) throw new InvalidDataException("Spostamenti incompleti per il caso «" + result.Name + "».");
            if (!forces) continue;
            var corners = snapshot.Elements.Where(e => e.Type == "PLATE").SelectMany(e => e.Nodes.Select(n => (e.Id, n))).ToHashSet();
            if (!corners.SetEquals(result.Values.Select(v => (v.Element, v.Node))) || result.Values.Length != corners.Count) throw new InvalidDataException("Risultati PLATE incompleti o duplicati per il caso «" + result.Name + "».");
            foreach (var field in ResultFields.Components("BEAM", "LOCAL"))
            {
                var endpoints = snapshot.Elements.Where(e => e.Type == "BEAM").SelectMany(e => new[] { (e.Id, 0.0), (e.Id, 1.0) }).ToHashSet();
                var values = (result.Fields ?? []).Where(f => f.Family == "BEAM" && f.Component == field).SelectMany(f => f.Values).ToArray();
                if (values.GroupBy(v => (v.Element, v.Station)).Any(g => g.Count() > 1) || !endpoints.SetEquals(values.Where(v => v.Station is 0 or 1).Select(v => (v.Element, v.Station!.Value)))) throw new InvalidDataException("Risultati BEAM incompleti o duplicati per il caso «" + result.Name + "».");
            }
        }
    }
}
