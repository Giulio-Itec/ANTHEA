using System.Text;
using ANTHEA.ModelWorkspace;
using GPC.Converter;
using GPC.Converter.Straus7;
using GPC.Model.Checking.Preparation;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Core.Identity;
using static ANTHEA.ModelWorkspace.Strand7WorkerProtocol;

Console.OutputEncoding = new UTF8Encoding(false);
return Strand7Worker.Run(args);

/// <summary>ANTHEA.Strand7Worker: the only ANTHEA process that loads St7api.dll, through the GPC Straus7 R3 converter (read-only API
/// session; nothing is solved, saved or modified). One request per process, protocol in <see cref="Strand7WorkerProtocol"/>:
/// Inspect returns the catalog of the model and of its linear static result cases, Read the presentation snapshot built by
/// <see cref="GpcModelSnapshot"/> with the Straus7 conventions of <see cref="Strand7Snapshot"/>.</summary>
static class Strand7Worker
{
    const int BeamStations = 5;
    sealed class Failure(int code, string message) : Exception(message) { public int Code { get; } = code; }
    sealed record Session(Straus7ApiConverter Converter, string ModelPath, string? ResultsPath, ImportReport Geometry, GPC.Model.Models.Model Model,
        Straus7ResultsReport? Listing, IReadOnlyDictionary<int, string>? Map);

    public static int Run(string[] args)
    {
        if (args.Length != 1) return Fail(InvalidRequest, "Uso: ANTHEA.Strand7Worker <richiesta.json>.");
        Strand7WorkerRequest request;
        try { request = ReadRequest(args[0]); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException or NotSupportedException)
        { return Fail(InvalidRequest, "Richiesta non leggibile: " + ex.Message); }
        try
        {
            if (request.Operation == Strand7WorkerOperation.Inspect) WriteResult(request.OutputPath, Inspect(request));
            else WriteResult(request.OutputPath, Read(request));
            Console.WriteLine(DoneLine);
            return Success;
        }
        catch (Failure failure) { return Fail(failure.Code, failure.Message); }
        catch (InvalidDataException ex) { return Fail(Rejected, "Copia ANTHEA non valida: " + ex.Message); }
        catch (Exception ex) { return Fail(Unexpected, "Errore imprevisto nella lettura Straus7: " + ex.GetType().Name + ": " + ex.Message); }
    }

    static int Fail(int code, string message) { Console.WriteLine(Error(message)); return code; }
    static void Step(int completed, int total, string message) => Console.WriteLine(Progress(completed, total, message));

    static string Existing(string? path, string extension, string label)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || !Path.GetExtension(path).Equals(extension, StringComparison.OrdinalIgnoreCase))
            throw new Failure(SourceUnavailable, label + " non trovato: " + path + ".");
        return Path.GetFullPath(path);
    }

    static Failure Rejection(string lead, IEnumerable<ModelDiagnostic> diagnostics)
    {
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        int code = errors.Any(d => d.Code is "Straus7MissingDependency" or "Straus7ApiError") ? NativeUnavailable : Rejected;
        return new(code, lead + ": " + (errors.Length == 0 ? "nessun dettaglio dal convertitore" : string.Join("; ", errors.Take(3).Select(d => (d.Code + " " + d.Message).Trim()))) + "."
            + (code == NativeUnavailable ? " Controlla l’installazione di Straus7 R3 a 64 bit e la licenza API." : ""));
    }

    static Session Open(Strand7WorkerRequest request, bool results, int total)
    {
        string model = Existing(request.ModelPath, ".st7", "Modello Straus7");
        string? lsa = results ? Existing(request.ResultsPath, ".lsa", "File dei risultati statici lineari") : null;
        if (request.ApiLibraryPath is { } api && !File.Exists(api)) throw new Failure(NativeUnavailable, "St7api.dll non trovata: " + api + ".");
        var converter = new Straus7ApiConverter(request.ApiLibraryPath);
        Step(1, total, "Apertura del modello Straus7 in sola lettura e importazione GPC…");
        var geometry = converter.Import(new Straus7ImportRequest { ModelPath = model, ModelRevision = "ANTHEA " + Path.GetFileName(model),
            AnalysisId = lsa == null ? "senza risultati" : "LSA " + Path.GetFileName(lsa) });
        if (geometry.Model == null) throw Rejection("Modello Straus7 non importato", geometry.Diagnostics);
        Straus7ResultsReport? listing = null; IReadOnlyDictionary<int, string>? map = null;
        if (lsa != null)
        {
            Step(2, total, "Elenco dei casi di risultato…");
            listing = converter.ReadResults(new Straus7ResultsRequest { ModelPath = model, ResultPath = lsa });
            if (listing.Status != ImportStatus.Completed) throw Rejection("Risultati Straus7 non leggibili", listing.Diagnostics);
            map = Straus7LinearStaticResults.CaseMap(geometry.Model, listing.Cases);
        }
        return new(converter, model, lsa, geometry, geometry.Model, listing, map);
    }

    static string Signature(Session session) => session.Geometry.SourceHash + (session.Listing?.ResultHash is { } results ? "/" + results : "");

    static Strand7ModelCatalog Inspect(Strand7WorkerRequest request)
    {
        var session = Open(request, !string.IsNullOrWhiteSpace(request.ResultsPath), 3);
        var model = session.Model;
        var listed = session.Listing?.Cases ?? [];
        var cases = listed.Where(c => session.Map!.ContainsKey(c.Number))
            .Select(c => new Strand7ResultCase(c.Number, Strand7Snapshot.CaseName(session.Map![c.Number])) { StageNumber = c.Stage == 0 ? null : c.Stage }).ToArray();
        var warnings = session.Geometry.Diagnostics.Concat(session.Listing?.Diagnostics ?? []).Where(d => d.Severity == DiagnosticSeverity.Warning).Select(d => d.Code + ": " + d.Message).ToList();
        if (cases.Length < listed.Count) warnings.Add("Casi di risultato senza un caso di carico corrispondente, non selezionabili: " + string.Join(", ", listed.Where(c => !session.Map!.ContainsKey(c.Number)).Select(c => c.Name)) + ".");
        Step(3, 3, "Catalogo pronto.");
        return new(model.NodesElements.Count, model.BeamElements.Count + model.AreaElements.Count, cases, Signature(session))
        {
            Beams = model.BeamElements.Count, Plates = model.AreaElements.Count, Combinations = CombinationExpansion.Definitions(model).Count,
            LoadCases = model.LoadCases.Values.Select(c => Strand7Snapshot.CaseName(c.Name)).ToArray(), ApiVersion = model.AnalysisSource?.SolverVersion ?? "",
            StageResultsAvailable = listed.Any(c => c.Stage != 0), Warnings = warnings
        };
    }

    static ModelSnapshot Read(Strand7WorkerRequest request)
    {
        bool results = request.Scope is ModelUpdateScope.Results or ModelUpdateScope.Everything;
        if (results && (request.CaseNumbers.Length == 0 || !request.Forces && !request.Displacements)) throw new Failure(InvalidRequest, "Nessun caso o tipo di risultato richiesto.");
        int total = results ? 6 : 3;
        var session = Open(request, results, total);
        var expected = (request.ExpectedSignature ?? "").Split('/');
        var actual = Signature(session).Split('/');
        if (expected[0] != actual[0] || results && (expected.Length < 2 || expected[1] != actual[1]))
            throw new Failure(SourceUnavailable, "I file Straus7 sono cambiati dopo il controllo: premi di nuovo «Controlla file e casi».");
        var messages = new List<ModelImportMessage>(Strand7Snapshot.Diagnostics(session.Geometry.Diagnostics));
        string? dataset = null; string modelFile = Path.GetFileName(session.ModelPath);
        if (results)
        {
            var names = request.CaseNumbers.Select(n => session.Map!.TryGetValue(n, out var name) ? name
                : throw new Failure(SourceUnavailable, "Il caso di risultato " + n + " non è disponibile nel file .lsa: ricontrolla i casi.")).ToArray();
            var families = new List<EntityFamily>();
            if (request.Forces) families.AddRange([EntityFamily.Beam, EntityFamily.Shell]);
            if (request.Displacements) families.Add(EntityFamily.Node);
            var plan = new ResultFilter { StaticCases = names, Elements = new ElementSelection { Families = families.ToArray() } }.Resolve(session.Model);
            Step(3, total, $"Lettura dei risultati di {names.Length} casi · {plan.Shells.Count:N0} piastre, {plan.Beams.Count:N0} aste, {plan.Nodes.Count:N0} nodi…");
            var native = session.Converter.ReadResults(session.Model, plan, session.ModelPath, session.ResultsPath!, false, BeamStations, default, request.Forces);
            if (native.Status != ImportStatus.Completed) throw Rejection("Risultati Straus7 non letti", native.Diagnostics);
            Step(4, total, "Normalizzazione GPC dei risultati…");
            dataset = "straus7-" + native.ResultHash[..12];
            var imported = Straus7LinearStaticResults.Import(session.Model, native, dataset);
            if (imported.Status is ImportStatus.Rejected or ImportStatus.Cancelled) throw Rejection("Risultati Straus7 non normalizzati", imported.Diagnostics);
            messages.AddRange(Strand7Snapshot.Diagnostics(imported.Diagnostics));
        }
        Step(total - 1, total, "Preparazione della copia ANTHEA…");
        string hash = session.Geometry.SourceHash ?? "";
        string files = modelFile + (session.ResultsPath is { } lsa ? " + " + Path.GetFileName(lsa) : "");
        var source = new ModelImportMessage("STRAUS7-SOURCE", "info", "Sorgente", $"{files} · API Straus7 {session.Model.AnalysisSource?.SolverVersion} · SHA-256 {Signature(session)} · " +
            $"{session.Model.NodesElements.Count:N0} nodi, {session.Model.BeamElements.Count:N0} aste, {session.Model.AreaElements.Count:N0} piastre nel modello GPC.");
        var options = Strand7Snapshot.Options(session.Model, Path.GetFileNameWithoutExtension(session.ModelPath),
            $"Straus7 · file {files} · SHA-256 {(hash.Length > 12 ? hash[..12] : hash)}", dataset, request.Forces, request.Displacements,
            [.. Strand7Snapshot.Conventions(results), source, .. messages]);
        var snapshot = GpcModelSnapshot.Create(session.Model, options);
        snapshot.Validate();
        Step(total, total, "Copia pronta.");
        return snapshot;
    }
}
