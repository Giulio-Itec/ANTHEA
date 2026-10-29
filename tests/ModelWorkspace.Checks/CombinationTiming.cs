using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelWorkspace;
using GPC.Model.Design.Combinations;

/// <summary>
/// Combinations of the Models module, refactoring of 29/10/2026 (agent combo-modelli): a synthetic MIDAS model, reproducible from code, with the
/// combinations a user usually has (generator of Model: SLU, SLE, SLV, accidental; envelopes of the generated rows; a few user and imported
/// definitions), the times of every phase and a digest of every calculated result. The digest is the exact before/after comparison of the
/// optimisations: the bits of every value, governing state, field order and input revision.
/// </summary>
static partial class Program
{
    internal sealed record TimingModel(string Label, ModelSnapshot Snapshot, ModelCaseAssignment[] Natures, string[] UserIds);

    /// <summary>Digest of the pipeline on the 100 elements × 50 cases model with 8 generated linear rows, captured with the code of main 61dc4ff3 (1.16.0)
    /// and the libraries of the set AM before any change.</summary>
    const string CombinationPipelineDigest100x50 = "0B143997D020FF1C42FFC47F3200EA3764183F037DCA092B54967745F71C1CA0";
    /// <summary>Digest of the pipeline on the reduced model of the fast suite (24 elements × 12 cases, 4 generated linear rows), captured as above.</summary>
    const string CombinationPipelineDigestSmall = "9F1336D34363F62E1E878294C7002B3C8974810B29C5AE7FC0F99F9821340011";

    /// <summary>
    /// A slab of nx × ny plates on its edge beams and on columns, MIDAS tables included, so that the GPC import of the geometry is the real one.
    /// Cases: 16 % G1 (D), 12 % G2 (DW), 60 % variable in six groups (L of categories B, C, E, W, S, T), 8 % seismic (E), the rest accidental (USER).
    /// Results of every static case: plate corners (8 components), beams at I, 1/4, 2/4, 3/4, J (6 components), displacements and rotations of
    /// every node. Values from an integer generator, so the model is the same on every machine.
    /// </summary>
    internal static TimingModel SyntheticMidasModel(int nx, int ny, int elementCount, int caseCount)
    {
        int Slab(int i, int j) => 1 + i + j * (nx + 1);
        var nodes = new List<ModelNode>();
        for (int j = 0; j <= ny; j++) for (int i = 0; i <= nx; i++) nodes.Add(new(Slab(i, j), i * 1.5, j * 1.25, 3.5));
        var elements = new List<ModelElement>();
        for (int j = 0; j < ny; j++) for (int i = 0; i < nx; i++) elements.Add(new(1 + i + j * nx, "PLATE", 1, [Slab(i, j), Slab(i + 1, j), Slab(i + 1, j + 1), Slab(i, j + 1)]));
        var ring = new List<int>();
        for (int i = 0; i < nx; i++) ring.Add(Slab(i, 0));
        for (int j = 0; j < ny; j++) ring.Add(Slab(nx, j));
        for (int i = nx; i > 0; i--) ring.Add(Slab(i, ny));
        for (int j = ny; j > 0; j--) ring.Add(Slab(0, j));
        for (int k = 0; k < ring.Count; k++) elements.Add(new(10001 + k, "BEAM", 1, [ring[k], ring[(k + 1) % ring.Count]]));
        int columns = elementCount - elements.Count, slabNodes = nodes.Count;
        if (columns < 0 || columns > slabNodes) throw new ArgumentException("Synthetic model: wrong element count.");
        for (int k = 0; k < columns; k++)
        {
            var top = nodes[(int)((long)k * slabNodes / columns)];
            nodes.Add(new(20001 + k, top.X, top.Y, 0));
            elements.Add(new(30001 + k, "BEAM", 1, [20001 + k, top.Id]));
        }
        // Load cases and their natures.
        int g1 = caseCount * 16 / 100, g2 = caseCount * 12 / 100, variable = caseCount * 60 / 100, seismic = Math.Max(1, caseCount * 8 / 100);
        int accidental = caseCount - g1 - g2 - variable - seismic;
        var groups = new (string Name, string Type, VariableActionCategory Category, double Share)[]
        { ("QB", "L", VariableActionCategory.B, 8 / 30d), ("QC", "L", VariableActionCategory.C, 6 / 30d), ("QE", "L", VariableActionCategory.E, 4 / 30d),
          ("W", "W", VariableActionCategory.Wind, 6 / 30d), ("S", "S", VariableActionCategory.SnowUpTo1000m, 2 / 30d), ("T", "T", VariableActionCategory.Temperature, 4 / 30d) };
        var cases = new List<(string Name, string Type, ModelCaseAssignment Nature)>();
        for (int k = 1; k <= g1; k++) cases.Add(("G1-" + k, "D", new("G1-" + k, ModelCaseNature.G1)));
        for (int k = 1; k <= g2; k++) cases.Add(("G2-" + k, "DW", new("G2-" + k, ModelCaseNature.G2)));
        int assigned = 0;
        for (int g = 0; g < groups.Length; g++)
        {
            int count = g == groups.Length - 1 ? variable - assigned : (int)Math.Round(variable * groups[g].Share); assigned += count;
            for (int k = 1; k <= count; k++)
            {
                string name = groups[g].Name + "-" + k;
                cases.Add((name, groups[g].Type, new(name, ModelCaseNature.Variable, groups[g].Category, groups[g].Name)));
            }
        }
        for (int k = 1; k <= seismic; k++) cases.Add(("E-" + k, "E", new("E-" + k, ModelCaseNature.Seismic)));
        for (int k = 1; k <= accidental; k++) cases.Add(("A-" + k, "USER", new("A-" + k, ModelCaseNature.Accidental)));
        // MIDAS tables.
        string N(double v) => v.ToString("R", CultureInfo.InvariantCulture);
        var tables = new StringBuilder("{\"UNIT\":{\"1\":{\"FORCE\":\"KN\",\"DIST\":\"M\"}},\"NODE\":{");
        tables.AppendJoin(",", nodes.Select(n => $"\"{n.Id}\":{{\"X\":{N(n.X)},\"Y\":{N(n.Y)},\"Z\":{N(n.Z)}}}"));
        tables.Append("},\"ELEM\":{");
        tables.AppendJoin(",", elements.Select(e => e.Type == "PLATE"
            ? $"\"{e.Id}\":{{\"TYPE\":\"PLATE\",\"MATL\":1,\"SECT\":1,\"NODE\":[{string.Join(",", e.Nodes)}],\"ANGLE\":0,\"STYPE\":1}}"
            : $"\"{e.Id}\":{{\"TYPE\":\"BEAM\",\"MATL\":1,\"SECT\":1,\"NODE\":[{e.Nodes[0]},{e.Nodes[1]},0,0],\"ANGLE\":0,\"STYPE\":0}}"));
        tables.Append("},\"MATL\":{\"1\":{\"TYPE\":\"CONC\",\"NAME\":\"C30/37\",\"PARAM\":[{\"P_TYPE\":1,\"STANDARD\":\"EN04(RC)\",\"DB\":\"C30/37\",\"ELAST\":32836000}]}},");
        tables.Append("\"THIK\":{\"1\":{\"NAME\":\"Soletta\",\"TYPE\":\"VALUE\",\"T_IN\":0.25,\"O_VALUE\":0}},");
        tables.Append("\"SECT\":{\"1\":{\"SECTTYPE\":\"DBUSER\",\"SECT_NAME\":\"Trave\",\"SECT_BEFORE\":{\"OFFSET_PT\":\"CC\",\"OFFSET_CENTER\":0,\"SHAPE\":\"SB\",\"DATATYPE\":2,\"SECT_I\":{\"vSIZE\":[0.6,0.4,0,0]}}}},");
        tables.Append("\"STLD\":{");
        tables.AppendJoin(",", cases.Select((c, i) => $"\"{i + 1}\":{{\"NAME\":\"{c.Name}\",\"TYPE\":\"{c.Type}\"}}"));
        // Two definitions imported from MIDAS, one nested in an envelope.
        string Case(int index) => cases[index % cases.Count].Name;
        tables.Append("},\"LCOM-GEN\":{");
        tables.Append($"\"1\":{{\"NAME\":\"cLCB1\",\"iTYPE\":0,\"ACTIVE\":\"ACTIVE\",\"vCOMB\":[{{\"ANAL\":\"ST\",\"LCNAME\":\"{Case(0)}\",\"FACTOR\":1.35}},{{\"ANAL\":\"ST\",\"LCNAME\":\"{Case(g1)}\",\"FACTOR\":1.5}},{{\"ANAL\":\"ST\",\"LCNAME\":\"{Case(g1 + g2)}\",\"FACTOR\":1.5}}]}},");
        tables.Append($"\"2\":{{\"NAME\":\"cENV\",\"iTYPE\":1,\"ACTIVE\":\"ACTIVE\",\"vCOMB\":[{{\"ANAL\":\"CB\",\"LCNAME\":\"cLCB1\",\"FACTOR\":1}},{{\"ANAL\":\"ST\",\"LCNAME\":\"{Case(1)}\",\"FACTOR\":-1}},{{\"ANAL\":\"ST\",\"LCNAME\":\"{Case(g1 + g2 + 1)}\",\"FACTOR\":2}}]}}}}}}");
        var model = new ModelSnapshot
        {
            Name = $"Modello sintetico MIDAS {elementCount} elementi × {caseCount} casi", Source = "MIDAS Civil NX",
            Nodes = nodes.ToArray(), Elements = elements.ToArray(),
            Plates = [new(1, "Soletta", .25, 0)], Sections = [new(1, "Trave", "SB", .4, .6, true)],
            SourceTables = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(tables.ToString())
        };
        // Results: an integer generator (PCG-like LCG) gives the same values everywhere.
        ulong state = 0x9E3779B97F4A7C15UL;
        double Next(double scale)
        {
            state = state * 6364136223846793005UL + 1442695040888963407UL;
            return (long)((state >> 33) % 20001) / 100.0 * scale / 100 - scale;
        }
        string[] beamComponents = ["Fx", "Fy", "Fz", "Mx", "My", "Mz"];
        double[] stations = [0, .25, .5, .75, 1];
        var plateElements = elements.Where(e => e.Type == "PLATE").ToArray();
        var beamElements = elements.Where(e => e.Type == "BEAM").ToArray();
        model.Results = cases.Select(c =>
        {
            var corners = plateElements.SelectMany(e => e.Nodes.Select(n => new CornerResult(e.Id, n, [Next(80), Next(80), Next(20), Next(400), Next(400), Next(100), Next(60), Next(60)]))).ToArray();
            var beam = beamComponents.Select(_ => new List<ResultValue>()).ToArray();
            foreach (var e in beamElements)
                foreach (double s in stations)
                {
                    int node = s == 0 ? e.Nodes[0] : s == 1 ? e.Nodes[1] : 0;
                    for (int k = 0; k < beamComponents.Length; k++) beam[k].Add(new(e.Id, node, Next(k < 3 ? 300 : 500), s));
                }
            var displacements = nodes.Select(n => new ModelDisplacement(n.Id, Next(.01), Next(.01), Next(.02)) { Rotation = new(Next(.002), Next(.002), Next(.002)) }).ToArray();
            return new ModelResultSet(c.Name, false, corners)
            {
                Fields = beamComponents.Select((k, i) => new ResultField("BEAM", "LOCAL", k, beam[i].ToArray())).ToArray(),
                Displacements = displacements
            };
        }).ToArray();
        model.Validate();
        return new($"{elementCount}x{caseCount}", model, cases.Select(c => c.Nature).ToArray(),
            ["ANTHEA/utente-somma", "ANTHEA/utente-inviluppo-slu", "ANTHEA/utente-inviluppo-slv", "ANTHEA/utente-annidata", "LCOM-GEN/cENV"]);
    }

    /// <summary>The definitions of the user over the generated rows: a sum of cases, the envelopes of the SLU and SLV rows, a negative nested envelope.</summary>
    static ModelSnapshot AddUserCombinations(ModelSnapshot model, TimingModel source)
    {
        var all = ModelCombinations.Read(model);
        string Name(int index) => source.Natures[index % source.Natures.Length].Case;
        model = ModelCombinations.Save(model, new("ANTHEA/utente-somma", "Somma utente", ModelCombinationKind.Linear, "ANTHEA", "",
            [new(Name(0), false, 1.3), new(Name(source.Natures.Length / 5), false, 1.5), new(Name(source.Natures.Length / 3), false, -.9), new(Name(source.Natures.Length / 2), false, 1.05)], true));
        ModelCombinationTerm[] Envelope(string prefix) => all.Where(d => d.Id.StartsWith(ModelCombinationGenerator.IdPrefix + prefix, StringComparison.Ordinal))
            .Select(d => new ModelCombinationTerm(d.Id, true, 1, "CB")).ToArray();
        model = ModelCombinations.Save(model, new("ANTHEA/utente-inviluppo-slu", "Inviluppo SLU", ModelCombinationKind.Envelope, "ANTHEA", "", Envelope("SLU "), true));
        model = ModelCombinations.Save(model, new("ANTHEA/utente-inviluppo-slv", "Inviluppo SLV", ModelCombinationKind.Envelope, "ANTHEA", "", Envelope("SLV "), true));
        model = ModelCombinations.Save(model, new("ANTHEA/utente-annidata", "Annidata utente", ModelCombinationKind.Linear, "ANTHEA", "",
            [new("ANTHEA/utente-inviluppo-slv", true, -1, "CB"), new(Name(1), false, .5)], true));
        return model;
    }

    sealed class TimingRecorder
    {
        public readonly Dictionary<string, List<double>> Phases = new(StringComparer.Ordinal);
        public readonly List<string> Order = [];
        public void Add(string phase, double ms) { if (!Phases.TryGetValue(phase, out var list)) { Phases[phase] = list = []; Order.Add(phase); } list.Add(ms); }
        public T Time<T>(string phase, Func<T> action) { var w = Stopwatch.StartNew(); var r = action(); Add(phase, w.Elapsed.TotalMilliseconds); return r; }
        public async Task<T> TimeAsync<T>(string phase, Func<Task<T>> action) { var w = Stopwatch.StartNew(); var r = await action(); Add(phase, w.Elapsed.TotalMilliseconds); return r; }
    }

    sealed class DigestWriter
    {
        readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        public int Items;
        public void Add(string label, object value) { hash.AppendData(Encoding.UTF8.GetBytes(label + "\n" + JsonSerializer.Serialize(value) + "\n")); Items++; }
        public string Value => Convert.ToHexString(hash.GetCurrentHash());
    }

    /// <summary>
    /// The pipeline of a user on one model: generation and user definitions, catalogue, calculation of linear rows, envelopes and nested
    /// definitions (each attached to the copy), view model with tables and member diagram, edit and calculation from the view model, save and
    /// reopen. Every calculated field enters the digest; the times enter the recorder.
    /// </summary>
    static async Task<(string Digest, int Definitions, int Linear)> CombinationPipeline(TimingModel source, TimingRecorder times, int linearCount, bool viewModel = true)
    {
        var digest = new DigestWriter();
        var model = source.Snapshot;
        var generated = times.Time("generazione: definizioni NTC 2018 (Generate + Apply)", () =>
        {
            var rows = ModelCombinationGenerator.Generate(model, source.Natures);
            return ModelCombinationGenerator.Apply(model, rows);
        });
        digest.Add("generated", generated.CustomCombinations!);
        model = times.Time("generazione: definizioni dell'utente (4 × Save)", () => AddUserCombinations(generated, source));
        var catalogue = times.Time("catalogo: Inspect", () => ModelCombinations.Inspect(model));
        digest.Add("catalogue", catalogue.Select(c => (c.Definition.Id, c.State, c.Message, c.RequiredCases, c.CanCalculate)));
        var service = new GpcModelCombinationService();
        var linear = catalogue.Where(c => c.Definition.Id.StartsWith(ModelCombinationGenerator.IdPrefix, StringComparison.Ordinal)).Select(c => c.Definition.Id).ToArray();
        var chosen = Enumerable.Range(0, Math.Min(linearCount, linear.Length)).Select(i => linear[(int)((long)i * linear.Length / Math.Min(linearCount, linear.Length))]).ToArray();
        async Task Calculate(string id, string phase)
        {
            var calculated = await times.TimeAsync("calcolo: " + phase + " (totale)", () => service.CalculateAsync(model, id));
            times.Add("calcolo: " + phase + " · preparazione", calculated.PreparationMilliseconds);
            times.Add("calcolo: " + phase + " · somma GPC", calculated.RebuildMilliseconds);
            times.Add("calcolo: " + phase + " · campi ANTHEA", calculated.DisplayMilliseconds);
            digest.Add(id, calculated.Results);
            digest.Add(id + "/warnings", calculated.Warnings);
            model = times.Time("calcolo: " + phase + " · Attach", () => ModelCombinations.Attach(model, id, calculated));
        }
        await Calculate(chosen[0], "prima lineare (fredda)");
        foreach (var id in chosen.Skip(1)) await Calculate(id, "lineare generata (calda)");
        await Calculate("ANTHEA/utente-somma", "lineare utente");
        await Calculate("ANTHEA/utente-inviluppo-slu", "inviluppo SLU");
        await Calculate("ANTHEA/utente-inviluppo-slv", "inviluppo SLV");
        await Calculate("ANTHEA/utente-annidata", "annidata negativa");
        await Calculate("LCOM-GEN/cENV", "inviluppo importato MIDAS");
        digest.Add("catalogue/after", ModelCombinations.Inspect(model).Select(c => (c.Definition.Id, c.State)));
        // Save and reopen as the project document does (ProjectModelStore, document JSON).
        var container = new JsonObject();
        string modelId = "";
        string text = times.Time("salvataggio: ProjectModelStore.Set + JSON", () => { modelId = ProjectModelStore.Set(container, model); return container.ToJsonString(); });
        var reopened = times.Time("riapertura: JSON + ProjectModelStore.Read", () => ProjectModelStore.Read((JsonObject)JsonNode.Parse(text)!, modelId)!);
        digest.Add("reopened", reopened.Fingerprint() == model.Fingerprint());
        if (viewModel) await ViewModelPhases(reopened, times, digest);
        return (digest.Value, catalogue.Length, chosen.Length);
    }

    static async Task ViewModelPhases(ModelSnapshot model, TimingRecorder times, DigestWriter digest)
    {
        var container = new JsonObject(); string modelId = ProjectModelStore.Set(container, model);
        int publications = 0;
        using var vm = times.Time("vista: apertura del modello (view model)", () => new ModelViewerViewModel(model, [], false, new FakeServices(),
            s => { publications++; ProjectModelStore.Set(container, s, modelId); }, (_, _, _, _) => { }));
        string envelope = model.Results.First(r => r.Combination?.DefinitionId == "ANTHEA/utente-inviluppo-slu" && r.Name.EndsWith("(max)", StringComparison.Ordinal)).Name;
        string linear = model.Results.First(r => r.Combination?.DefinitionId == "ANTHEA/utente-somma").Name;
        vm.ShowTable = true; vm.SelectedTableKind = 5;
        times.Time("vista: scelta dell'inviluppo, piastre + tabella", () => { vm.SelectedCase = envelope; return 0; });
        digest.Add("vm/table/plate", vm.TableCsv());
        times.Time("vista: famiglia aste + tabella", () => { vm.SelectedFamily = 1; return 0; });
        digest.Add("vm/table/beam", vm.TableCsv());
        times.Time("vista: combinazione lineare + tabella", () => { vm.SelectedCase = linear; return 0; });
        times.Time("vista: diagramma dell'asta", () => { vm.MemberDiagramElementId = model.Elements.First(e => e.Type == "BEAM").Id; vm.ShowMemberDiagram = true; vm.MemberDiagramAllComponents = true; return 0; });
        digest.Add("vm/diagram", vm.MemberDiagramSeries.Select(s => (s.Component, s.Unit, s.Samples)));
        times.Time("vista: inviluppo nel diagramma", () => { vm.SelectedCase = envelope; return 0; });
        digest.Add("vm/diagram/envelope", vm.MemberDiagramSeries.Select(s => (s.Component, s.Unit, s.Samples)));
        times.Time("vista: apertura del gestore delle combinazioni", () => { vm.OpenCombinationManagerCommand.Execute(null); vm.OpenCombinationManagerCommand.Execute(null); return 0; });
        times.Time("vista: griglia delle combinazioni", () => { vm.ShowCombinationGrid = true; return 0; });
        digest.Add("vm/grid", vm.CombinationGridRows!.Count);
        vm.ShowCombinationGrid = false;
        vm.SelectedCombination = vm.Combinations.Single(c => c.Definition.Id == "ANTHEA/utente-somma");
        vm.CombinationTerms[0].Factor = "1.35";
        times.Time("modifica: salvataggio di una definizione dal view model", () => { vm.SaveCombinationCommand.Execute(null); return 0; });
        if (vm.IsCombinationDirty) throw new InvalidOperationException("Timing pipeline: definition not saved: " + vm.CombinationMessage);
        await times.TimeAsync("modifica: calcolo dal view model (calcolo + Attach + pubblicazione)", async () => { await vm.CalculateCombinationCommand.ExecuteAsync(null); return 0; });
        if (!vm.CombinationMessage.Contains("calcolo", StringComparison.Ordinal)) throw new InvalidOperationException("Timing pipeline: calculation from the view model: " + vm.CombinationMessage);
        digest.Add("vm/calculated", vm.Snapshot!.Results.Where(r => r.Combination?.DefinitionId == "ANTHEA/utente-somma").Select(r => r with { }));
        digest.Add("vm/publications", publications);
        // After the digest: a second calculation from the view model, with the GPC preparation of the first one reused.
        vm.SelectedCombination = vm.Combinations.Single(c => c.Definition.Id == ModelCombinationGenerator.IdPrefix + "SLU 2");
        await times.TimeAsync("modifica: secondo calcolo dal view model (preparazione riusata)", async () => { await vm.CalculateCombinationCommand.ExecuteAsync(null); return 0; });
        if (!vm.CombinationMessage.Contains("Casi base riutilizzati", StringComparison.Ordinal)) throw new InvalidOperationException("Timing pipeline: second calculation: " + vm.CombinationMessage);
    }

    /// <summary>Exact comparison of the optimisations on the reduced synthetic model and the hand fixture (fast suite).</summary>
    static async Task CombinationEquivalenceChecks(string output)
    {
        var small = SyntheticMidasModel(3, 2, 24, 12);
        var times = new TimingRecorder();
        var (digest, definitions, _) = await CombinationPipeline(small, times, 4);
        File.WriteAllText(Path.Combine(output, "combinazioni-impronta.txt"), digest + Environment.NewLine);
        Check(definitions > 50, $"timing model: {definitions} definitions from the generator and the user");
        Check(digest == CombinationPipelineDigestSmall,
            "combinations: every calculated field, governing state, table and diagram bit-identical to 1.16.0 (" + digest[..12] + ")");
        // The shortcuts of the refactoring against the complete computations they replace, on a copy with derived results.
        var model = AddUserCombinations(ModelCombinationGenerator.Apply(small.Snapshot, ModelCombinationGenerator.Generate(small.Snapshot, small.Natures)), small);
        var service = new GpcModelCombinationService();
        foreach (var id in new[] { "ANTHEA/utente-somma", "ANTHEA/utente-inviluppo-slu" }) model = ModelCombinations.Attach(model, id, await service.CalculateAsync(model, id));
        var fresh = ModelSnapshot.FromJson(JsonSerializer.Serialize(model));
        Check(ModelCombinations.InputRevision(model) == ModelCombinations.InputRevision(fresh) && model.Results.Count(r => r.Combination != null) == 3,
            "combinations: the input revision kept by the copies equals the one computed again from the document");
        var size = typeof(ModelCombinations).GetMethod("SerializedSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        Check((long)size.Invoke(null, [model])! == JsonSerializer.SerializeToUtf8Bytes(model).LongLength && (long)size.Invoke(null, [fresh])! == JsonSerializer.SerializeToUtf8Bytes(fresh).LongLength,
            "combinations: the size of a copy from its input revision equals the complete serialization");
        var container = new JsonObject(); ProjectModelStore.Set(container, model);
        var entry = ProjectModelStore.Models(container).Single();
        Check(entry["revisione"]!.GetValue<string>() == model.Fingerprint() && entry["snapshot"]!.ToJsonString() == JsonSerializer.SerializeToNode(model)!.ToJsonString(),
            "combinations: the document entry written once equals SerializeToNode and the fingerprint");
        var renamed = ModelCombinations.RestoreImported(model); renamed.Name = "Altro nome"; // a copy that carries the revisions of the original
        Check(ModelCombinations.InputRevision(renamed) == ModelCombinations.InputRevision(ModelSnapshot.FromJson(JsonSerializer.Serialize(renamed))) &&
            ModelCombinations.InputRevision(renamed) != ModelCombinations.InputRevision(model), "combinations: a part set on a copy gives its own revision");
    }

    /// <summary>
    /// --combination-timing [--models 100x50,500x100] [--runs N] [--warmup N] [--linear N]: times of every phase (median, minimum, maximum of the
    /// runs after the warm-up) in tempi.json and tempi.md of the output folder, with the digest of each model.
    /// </summary>
    static async Task<int> CombinationTiming(string output, string[] args)
    {
        string Option(string name, string fallback) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback; }
        int runs = int.Parse(Option("--runs", "3"), CultureInfo.InvariantCulture), warmup = int.Parse(Option("--warmup", "1"), CultureInfo.InvariantCulture);
        int linearCount = int.Parse(Option("--linear", "8"), CultureInfo.InvariantCulture);
        var sizes = Option("--models", "100x50").Split(',', StringSplitOptions.RemoveEmptyEntries);
        var report = new List<object>(); var markdown = new StringBuilder();
        markdown.AppendLine($"# Tempi delle combinazioni del modulo Modelli · {DateTime.Now:yyyy-MM-dd HH:mm}").AppendLine();
        markdown.AppendLine($"Build {(IsRelease() ? "Release" : "Debug")}, {Environment.ProcessorCount} processori, .NET {Environment.Version}; {warmup} corse di riscaldamento, {runs} misurate; mediana [minimo–massimo] in ms.").AppendLine();
        foreach (string size in sizes)
        {
            var parts = size.Split('x'); int elements = int.Parse(parts[0], CultureInfo.InvariantCulture), cases = int.Parse(parts[1], CultureInfo.InvariantCulture);
            var watch = Stopwatch.StartNew();
            var source = ModelOfSize(elements, cases);
            Console.WriteLine($"{source.Label}: modello sintetico in {watch.Elapsed.TotalMilliseconds:F0} ms; {source.Snapshot.Nodes.Length} nodi");
            var times = new TimingRecorder(); string? digest = null; int definitions = 0, linear = 0;
            for (int run = 0; run < warmup + runs; run++)
            {
                var recorder = run < warmup ? new TimingRecorder() : times;
                watch.Restart();
                var result = await CombinationPipeline(source, recorder, linearCount);
                recorder.Add("totale della corsa", watch.Elapsed.TotalMilliseconds);
                if (digest != null && digest != result.Digest) throw new InvalidOperationException("Impronta diversa tra due corse: risultato non deterministico.");
                digest = result.Digest; definitions = result.Definitions; linear = result.Linear;
                Console.WriteLine($"{source.Label} corsa {run + 1}{(run < warmup ? " (riscaldamento)" : "")}: {watch.Elapsed.TotalSeconds:F1} s · impronta {digest[..16]}");
            }
            string expected = (source.Label, linearCount) switch { ("100x50", 8) => CombinationPipelineDigest100x50, ("24x12", 4) => CombinationPipelineDigestSmall, _ => "" };
            string equivalence = expected.Length == 0 ? "non registrata" : expected == digest ? "uguale a 1.16.0" : "DIVERSA da 1.16.0";
            markdown.AppendLine($"## {source.Label}: {source.Snapshot.Elements.Length} elementi, {source.Snapshot.Nodes.Length} nodi, {source.Natures.Length} casi, {definitions} definizioni ({linear} lineari generate calcolate)").AppendLine();
            markdown.AppendLine($"Impronta dei risultati: `{digest}` ({equivalence}).").AppendLine();
            markdown.AppendLine("| Fase | Mediana | Min | Max | n |").AppendLine("|---|---:|---:|---:|---:|");
            var phases = new List<object>();
            foreach (string phase in times.Order)
            {
                var values = times.Phases[phase].Order().ToArray();
                double median = values.Length % 2 == 1 ? values[values.Length / 2] : (values[values.Length / 2 - 1] + values[values.Length / 2]) / 2;
                markdown.AppendLine($"| {phase} | {median:F1} | {values[0]:F1} | {values[^1]:F1} | {values.Length} |");
                phases.Add(new { Phase = phase, Median = median, Minimum = values[0], Maximum = values[^1], Samples = values });
            }
            markdown.AppendLine();
            report.Add(new { Model = source.Label, Elements = source.Snapshot.Elements.Length, Nodes = source.Snapshot.Nodes.Length, Cases = source.Natures.Length, Definitions = definitions,
                Digest = digest, Equivalence = equivalence, Runs = runs, Warmup = warmup, Phases = phases });
            if (expected.Length > 0 && expected != digest) { File.WriteAllText(Path.Combine(output, "tempi.md"), markdown.ToString()); Console.Error.WriteLine("FAIL impronta diversa da 1.16.0 per " + source.Label); return 1; }
        }
        File.WriteAllText(Path.Combine(output, "tempi.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(output, "tempi.md"), markdown.ToString());
        Console.WriteLine(markdown);
        File.WriteAllText(Path.Combine(output, "esito.txt"), "PASS tempi delle combinazioni");
        return 0;
    }

    /// <summary>The models of the measures (24, 100 and 500 elements, the layouts of the registered digests) and, for the long campaign, any
    /// other size: about half plates on a slab of proportion 3:2, the rest edge beams and columns under the slab nodes.</summary>
    static TimingModel ModelOfSize(int elements, int cases)
    {
        if (elements == 24) return SyntheticMidasModel(3, 2, 24, cases);
        if (elements == 100) return SyntheticMidasModel(5, 8, 100, cases);
        if (elements == 500) return SyntheticMidasModel(15, 14, 500, cases);
        int nx = Math.Max(2, (int)Math.Round(Math.Sqrt(elements * .75))), ny = Math.Max(1, (int)Math.Round(elements * .5 / nx));
        while (ny > 1 && nx * ny + 2 * (nx + ny) > elements) ny--;
        int columns = elements - nx * ny - 2 * (nx + ny);
        if (elements < 24 || columns < 0 || columns > (nx + 1) * (ny + 1)) throw new ArgumentException($"Modello di {elements} elementi non costruibile: usa almeno 24 elementi.");
        return SyntheticMidasModel(nx, ny, elements, cases);
    }

    static bool IsRelease() =>
        typeof(Program).Assembly.GetCustomAttributes(typeof(DebuggableAttribute), false).OfType<DebuggableAttribute>().All(a => !a.IsJITOptimizerDisabled);
}
