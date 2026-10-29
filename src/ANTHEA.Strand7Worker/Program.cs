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
/// <see cref="GpcModelSnapshot"/> with the Straus7 conventions of <see cref="Strand7Snapshot"/>. 1.16.0: results of a linear static solution (.lsa,
/// with or without an active stage) or of a staged nonlinear static solution (.nla, stage results); support reactions with the GPC equilibrium check of
/// every unstaged linear static case; stages, masses and temperatures as data of the copy.</summary>
static class Strand7Worker
{
    const int BeamStations = 5;
    sealed class Failure(int code, string message) : Exception(message) { public int Code { get; } = code; }
    sealed record Session(Straus7ApiConverter Converter, string ModelPath, string? ResultsPath, ImportReport Geometry, GPC.Model.Models.Model Model,
        Straus7ResultsReport? Listing, IReadOnlyDictionary<int, string>? Map)
    {
        /// <summary>Staged nonlinear static solution (St7ValidateResultFile solver 3).</summary>
        public bool Nonlinear => Listing?.File?.Solver == 3;
    }

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

    static string Existing(string? path, string label, params string[] extensions)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || !extensions.Any(e => Path.GetExtension(path).Equals(e, StringComparison.OrdinalIgnoreCase)))
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
        string model = Existing(request.ModelPath, "Modello Straus7", ".st7");
        string? lsa = results ? Existing(request.ResultsPath, "File dei risultati (.lsa o .nla)", [.. Strand7ImportSetup.ResultExtensions]) : null;
        if (request.ApiLibraryPath is { } api && !File.Exists(api)) throw new Failure(NativeUnavailable, "St7api.dll non trovata: " + api + ".");
        var converter = new Straus7ApiConverter(request.ApiLibraryPath);
        Step(1, total, "Apertura del modello Straus7 in sola lettura e importazione GPC…");
        var geometry = converter.Import(new Straus7ImportRequest { ModelPath = model, ModelRevision = "ANTHEA " + Path.GetFileName(model),
            AnalysisId = lsa == null ? "senza risultati" : "Risultati " + Path.GetFileName(lsa) });
        if (geometry.Model == null) throw Rejection("Modello Straus7 non importato", geometry.Diagnostics);
        Straus7ResultsReport? listing = null; IReadOnlyDictionary<int, string>? map = null;
        if (lsa != null)
        {
            Step(2, total, "Elenco dei casi di risultato…");
            listing = converter.ReadResults(new Straus7ResultsRequest { ModelPath = model, ResultPath = lsa });
            if (listing.Status != ImportStatus.Completed) throw Rejection("Risultati Straus7 non leggibili", listing.Diagnostics);
            if (listing.File?.Solver is not (1 or 3)) throw new Failure(Rejected, "Il file dei risultati non è di un’analisi statica lineare (.lsa) o statica non lineare per fasi (.nla).");
            if (listing.File.Solver == 3 && (listing.ModelStageCount == 0 || listing.Cases.Any(c => c.Stage < 1)))
                throw new Failure(Rejected, "Risultati non lineari senza fasi: dal file .nla ANTHEA legge solo l’analisi statica non lineare per fasi (stage).");
            map = Straus7LinearStaticResults.CaseMap(geometry.Model, listing.Cases);
        }
        return new(converter, model, lsa, geometry, geometry.Model, listing, map);
    }

    static string Signature(Session session) => session.Geometry.SourceHash + (session.Listing?.ResultHash is { } results ? "/" + results : "");

    static Strand7ModelCatalog Inspect(Strand7WorkerRequest request)
    {
        var session = Open(request, !string.IsNullOrWhiteSpace(request.ResultsPath), 3);
        var model = session.Model; var data = Straus7ModelData.Read(model);
        var listed = session.Listing?.Cases ?? [];
        var phases = data.Stages.ToDictionary(s => s.Number, s => Strand7Snapshot.StageLabel(s.Number, s.Name));
        var warnings = session.Geometry.Diagnostics.Concat(session.Listing?.Diagnostics ?? []).Where(d => d.Severity == DiagnosticSeverity.Warning).Select(d => d.Code + ": " + d.Message).ToList();
        Strand7ResultCase[] cases;
        if (session.Nonlinear)
        {
            if (listed.Any(c => !phases.ContainsKey(c.Stage))) throw new Failure(Rejected, "Casi di risultato non lineari di fasi assenti dal modello.");
            cases = listed.Select(c => new Strand7ResultCase(c.Number, Strand7Snapshot.NonlinearCaseName(phases[c.Stage], Straus7StageResults.Step(c))) { StageNumber = c.Stage }).ToArray();
        }
        else
        {
            cases = listed.Where(c => session.Map!.ContainsKey(c.Number))
                .Select(c => new Strand7ResultCase(c.Number, Strand7Snapshot.CaseName(session.Map![c.Number])) { StageNumber = c.Stage == 0 ? null : c.Stage }).ToArray();
            if (cases.Length < listed.Count) warnings.Add("Casi di risultato senza un caso di carico corrispondente, non selezionabili: " + string.Join(", ", listed.Where(c => !session.Map!.ContainsKey(c.Number)).Select(c => c.Name)) + ".");
            if (listed.FirstOrDefault(c => c.Stage != 0) is { } staged)
                warnings.Add("Risultati statici lineari della fase attiva " + (phases.TryGetValue(staged.Stage, out var phase) ? phase : staged.Stage.ToString(System.Globalization.CultureInfo.InvariantCulture)) +
                    ": gli elementi non attivi in quella fase non hanno risultati; i casi si combinano fra loro.");
        }
        Step(3, 3, "Catalogo pronto.");
        return new(model.NodesElements.Count, model.BeamElements.Count + model.AreaElements.Count, cases, Signature(session))
        {
            Beams = model.BeamElements.Count, Plates = model.AreaElements.Count, Combinations = CombinationExpansion.Definitions(model).Count,
            LoadCases = model.LoadCases.Values.Where(c => c is not GPC.Model.Stages.StageResultCase).Select(c => Strand7Snapshot.CaseName(c.Name)).ToArray(),
            ApiVersion = model.AnalysisSource?.SolverVersion ?? "",
            Phases = data.Stages.Select(s => new ModelConstructionPhase(s.Number, phases[s.Number], null, null, s.EnabledGroups?.Length ?? 0)).ToArray(),
            StageResultsAvailable = listed.Any(c => c.Stage != 0), NonlinearStages = session.Nonlinear, Warnings = warnings
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
        string? revision = null; var equilibria = new Dictionary<string, ModelEquilibrium>(StringComparer.Ordinal);
        if (results)
        {
            var model = session.Model; var listed = session.Listing!.Cases.ToDictionary(c => c.Number);
            foreach (int n in request.CaseNumbers)
                if (!listed.ContainsKey(n) || !session.Nonlinear && !session.Map!.ContainsKey(n))
                    throw new Failure(SourceUnavailable, "Il caso di risultato " + n + " non è disponibile nel file dei risultati: ricontrolla i casi.");
            static int[] Numbers(IEnumerable<GPC.Model.Elements.Element> elements) =>
                elements.Select(e => int.Parse(e.Source.OriginalId, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture)).Order().ToArray();
            // Reactions of the supports (restraints, springs to ground) always; every node with the displacements.
            var nodes = request.Displacements ? model.NodesElements.Values
                : model.NodesElements.Values.Where(n => n.Assignments.Restrains.Any(r => r.Restrain != null) || n.Assignments.GroundSprings.Count > 0);
            var read = new Straus7ResultsRequest { ModelPath = session.ModelPath, ResultPath = session.ResultsPath!, CaseNumbers = request.CaseNumbers.ToArray(),
                BeamNumbers = request.Forces ? Numbers(model.BeamElements.Values) : [], PlateNumbers = request.Forces ? Numbers(model.AreaElements.Values) : [],
                NodeNumbers = Numbers(nodes), MinimumBeamStations = BeamStations, IncludePlateNodeResults = request.Forces };
            Step(3, total, $"Lettura dei risultati di {read.CaseNumbers.Length} casi · {read.PlateNumbers.Length:N0} piastre, {read.BeamNumbers.Length:N0} aste, {read.NodeNumbers.Length:N0} nodi…");
            var native = session.Converter.ReadResults(read);
            if (native.Status != ImportStatus.Completed) throw Rejection("Risultati Straus7 non letti", native.Diagnostics);
            Step(4, total, "Normalizzazione GPC dei risultati…");
            dataset = "straus7-" + native.ResultHash[..12]; revision = native.ResultHash;
            var imported = session.Nonlinear ? Straus7StageResults.Import(model, native, dataset) : Straus7LinearStaticResults.Import(model, native, dataset);
            if (imported.Status is ImportStatus.Rejected or ImportStatus.Cancelled) throw Rejection("Risultati Straus7 non normalizzati", imported.Diagnostics);
            messages.AddRange(Strand7Snapshot.Diagnostics(native.Diagnostics.Where(d => d.Code == "Straus7InactiveElementsSkipped").Concat(imported.Diagnostics)));
            if (!session.Nonlinear)
                foreach (var c in native.Cases)
                {
                    // Applied loads against the solver reactions (GPC Equilibrium.Check), for every unstaged linear static case.
                    string name = session.Map![c.Number]; var balance = Straus7Equilibrium.Check(model, name, dataset);
                    var check = Strand7Snapshot.Equilibrium(balance); equilibria[name] = check;
                    messages.Add(new("STRAUS7-EQUILIBRIUM", balance.Balanced == false ? "Warning" : "info", Strand7Snapshot.CaseName(name),
                        check.Note + (balance.Balanced.HasValue ? FormattableString.Invariant($" Risultante dei carichi Z {check.AppliedForce[2]:G6} kN, delle reazioni Z {check.ReactionForce[2]:G6} kN.") : "")));
                }
        }
        Step(total - 1, total, "Preparazione della copia ANTHEA…");
        string hash = session.Geometry.SourceHash ?? "";
        string files = modelFile + (session.ResultsPath is { } lsa ? " + " + Path.GetFileName(lsa) : "");
        var source = new ModelImportMessage("STRAUS7-SOURCE", "info", "Sorgente", $"{files} · API Straus7 {session.Model.AnalysisSource?.SolverVersion} · SHA-256 {Signature(session)} · " +
            $"{session.Model.NodesElements.Count:N0} nodi, {session.Model.BeamElements.Count:N0} aste, {session.Model.AreaElements.Count:N0} piastre nel modello GPC.");
        var options = Strand7Snapshot.Options(session.Model, Path.GetFileNameWithoutExtension(session.ModelPath),
            $"Straus7 · file {files} · SHA-256 {(hash.Length > 12 ? hash[..12] : hash)}", dataset, request.Forces, request.Displacements,
            [.. Strand7Snapshot.Conventions(results, session.Nonlinear), source, .. messages], revision, name => equilibria.TryGetValue(name, out var check) ? check : null);
        var snapshot = GpcModelSnapshot.Create(session.Model, options);
        if (Strand7Snapshot.AddModelData(session.Model, snapshot) is { } data) snapshot.ImportMessages = [.. snapshot.ImportMessages ?? [], data];
        snapshot.Validate();
        Step(total, total, "Copia pronta.");
        return snapshot;
    }
}
