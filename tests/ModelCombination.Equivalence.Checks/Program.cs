using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using ANTHEA.ModelWorkspace;
using GPC.Model.Design.Combinations;
using Assignment = ANTHEA.ModelWorkspace.ModelCaseAssignment;
using Nature = ANTHEA.ModelWorkspace.ModelCaseNature;
using Generator = ANTHEA.ModelWorkspace.ModelCombinationGenerator;
using Legacy = ANTHEA.Legacy.ModelCombinations.LegacyModelCombinationGenerator;

static partial class Program
{
    static readonly JsonSerializerOptions Json = new() { WriteIndented = false, Converters = { new BitsConverter() } };
    static int Main(string[] args)
    {
        if (args.Length is < 2 or > 3 || args[0] is not ("capture" or "check")) throw new ArgumentException("capture|check <fixture-directory> [evidence-directory]");
        var folder = Path.GetFullPath(args[1]);
        bool capture = args[0] == "capture";
        if (capture && typeof(CombinationProfile).Assembly.GetType("GPC.Model.Design.Combinations.ModelCombinationRequest") != null)
            throw new InvalidOperationException("Capture is allowed only against the pre-extraction baseline; never recapture expected output with the new engine.");
        if (capture && Directory.Exists(folder)) throw new IOException("Captures are immutable: destination already exists.");
        if (capture) Directory.CreateDirectory(folder);
        int count = 0;
        foreach (var culture in new[] { "it-IT", "en-US" })
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture;
            foreach (var input in Inputs())
            {
                var model = Snapshot(input);
                if (!capture) LegacyParity(model, input.Assignments!);
                ModelCombinationDefinition[]? rows = null;
                string? error = null, previewError = null;
                try { rows = Generator.Generate(model, input.Assignments!); }
                catch (Exception ex) { error = ex.GetType().Name + ": " + ex.Message; }
                (bool CanGenerate, int Rows, string Message) preview = default;
                try { preview = Generator.Preview(model, input.Assignments!); }
                catch (Exception ex) { previewError = ex.GetType().Name + ": " + ex.Message; }
                var output = new Capture(input, rows, error, preview.CanGenerate, preview.Rows, preview.Message, previewError,
                    Generator.Propose(model), Generator.Choices(model));
                Compare(input.Name + "." + culture + ".json", JsonSerializer.Serialize(output, Json));
            }
            Compare("results." + culture + ".json", ResultCapture().GetAwaiter().GetResult());
        }
        if (capture)
        {
            var manifest = new { AntheaCommit = "bce5de06fa3b3cfdf866e8ab3b504c03e3d0100b", ModelCommit = "55c15ed5ff64baf9d9282c56137c4581e5e97fd1",
                Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                Assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && (a.GetName().Name!.StartsWith("GPC") || a.GetName().Name == "ANTHEA.ModelWorkspace"))
                    .OrderBy(a => a.GetName().Name).Select(a => new { Name = a.GetName().Name, Version = a.GetName().Version!.ToString(), Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))) }),
                Files = Directory.GetFiles(folder).OrderBy(f => f, StringComparer.Ordinal).Select(f => new { File = Path.GetFileName(f), Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))) }).ToArray() };
            File.WriteAllText(Path.Combine(folder, "manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        }
        if (!capture)
        {
            // This request exceeds the optional GPC work budget but was accepted by ANTHEA.
            // Compare with the unchanged compiled legacy source; never rewrite the frozen corpus.
            var largeAssignments = new[] { new Assignment("G", Nature.G1) }.Concat(Enumerable.Range(0, 2000)
                .Select(i => new Assignment("A" + i, Nature.Accidental))).ToArray();
            var large = new Input("large-accidental-compatibility", largeAssignments.Select(a => a.Case).ToArray(), largeAssignments);
            LegacyParity(Snapshot(large), largeAssignments);
            Console.WriteLine("PASS optional work budget preserves legacy acceptance and preview for 2000 accidental groups.");
            if (!Enum.GetValues<Nature>().Select(n => ((int)n, n.ToString())).SequenceEqual(Enum.GetValues<GPC.Model.Design.Combinations.ModelCaseNature>().Select(n => ((int)n, n.ToString()))))
                throw new Exception("ANTHEA nature wrapper diverges from GPC.");
            if (!CombinationRuleSet.Ntc2018GeneralStructuralV1.SupportedVariableCategories.Select(c => (int)c).SequenceEqual(Enumerable.Range(0, Assignment.CategoryCount)))
                throw new Exception("UI category indices diverge from the GPC descriptors.");
        }
        string summary = $"PASS {count} immutable captures ({(capture ? "recorded before extraction" : "byte identical")}).";
        Console.WriteLine(summary);
        if (args.Length == 3) { Directory.CreateDirectory(args[2]); File.WriteAllText(Path.Combine(args[2], "esito.txt"), summary); }
        return 0;

        void Compare(string name, string text)
        {
            string path = Path.Combine(folder, name);
            if (capture) File.WriteAllText(path, text);
            else if (File.ReadAllText(path) != text) throw new InvalidDataException("Capture differs: " + name);
            count++;
        }
    }

    static void LegacyParity(ModelSnapshot model, Assignment[] assignments)
    {
        string Observe(Func<object> action)
        {
            try { return JsonSerializer.Serialize(action(), Json); }
            catch (Exception ex) { return ex.GetType().Name + ": " + ex.Message; }
        }
        void Equal(Func<object> before, Func<object> after)
        { if (Observe(before) != Observe(after)) throw new Exception("Frozen legacy engine and adapter differ."); }
        Equal(() => Legacy.Generate(model, assignments), () => Generator.Generate(model, assignments));
        Equal(() => { var p = Legacy.Preview(model, assignments); return new { p.CanGenerate, p.Rows, p.Message }; },
            () => { var p = Generator.Preview(model, assignments); return new { p.CanGenerate, p.Rows, p.Message }; });
        Equal(() => Legacy.Propose(model), () => Generator.Propose(model));
        Equal(() => Legacy.Choices(model), () => Generator.Choices(model));
    }

    public sealed record Input(string Name, string[] Known, Assignment[]? Assignments, string[]? Types = null, ModelGeneratorCase[]? Saved = null);
    public sealed record Capture(Input Input, ModelCombinationDefinition[]? Rows, string? Error, bool CanGenerate, int PreviewRows, string? PreviewMessage,
        string? PreviewError, Assignment[] Proposals, Assignment[] Choices);
    static ModelSnapshot Snapshot(Input i) => new() { SourceTables = new() { ["STLD"] = JsonSerializer.SerializeToElement(i.Known.Select((c, n) =>
        new { Key = (n + 1).ToString(CultureInfo.InvariantCulture), Value = new { NAME = c, TYPE = i.Types?[n] ?? "" } }).ToDictionary(x => x.Key, x => x.Value)) }, GeneratorCases = i.Saved };

    static IEnumerable<Input> Inputs()
    {
        Input Make(string name, params Assignment[] a) => new(name, a.Where(x => x != null && !string.IsNullOrWhiteSpace(x.Case)).Select(x => x.Case).Distinct(StringComparer.Ordinal).ToArray(), a);
        var g = new Assignment("G", Nature.G1);
        var q = new Assignment("Q", Nature.Variable, VariableActionCategory.B);
        yield return Make("general-g-b", g, q);
        yield return Make("permanent-only", g);
        yield return Make("variable-only", q);
        yield return Make("groups-default", g, new("g", Nature.G1), new("G2", Nature.G2), new("G2b", Nature.G2), q);
        yield return Make("groups-explicit", g with { Group = " x " }, new("g", Nature.G1, Group: "X"), new("G2", Nature.G2, Group: "x"),
            q with { Group = "x" }, new("Wind", Nature.Variable, VariableActionCategory.Wind, " x "), new("Snow", Nature.Variable, VariableActionCategory.SnowAbove1000m, "X"));
        yield return Make("correlated-q", g, q with { Group = "q" }, new("Wind", Nature.Variable, VariableActionCategory.Wind, "q"), new("E", Nature.Variable, VariableActionCategory.E, "q"));
        yield return Make("correlated-q-reversed", new("E", Nature.Variable, VariableActionCategory.E, "q"), q with { Group = "q" }, g, new("Wind", Nature.Variable, VariableActionCategory.Wind, "q"));
        yield return Make("case-id-spaces", new(" G ", Nature.G1), q, new("Skip", Nature.Excluded));
        foreach (var category in Enum.GetValues<VariableActionCategory>()) yield return Make("category-" + category, g, q with { Category = category });
        yield return Make("zero-dedup", g, q with { Category = VariableActionCategory.H }, new("W", Nature.Variable, VariableActionCategory.Wind), new("S", Nature.Variable, VariableActionCategory.SnowUpTo1000m));
        yield return Make("seismic-none", g, q, new("E1", Nature.Seismic), new("E2", Nature.Seismic, Group: "ignored"));
        yield return Make("seismic-xy", g, q, new("X", Nature.Seismic, Direction: SeismicDirection.X), new("Y", Nature.Seismic, Direction: SeismicDirection.Y));
        yield return Make("seismic-xyz-alternatives", g, q, new("E0", Nature.Seismic), new("X1", Nature.Seismic, Direction: SeismicDirection.X), new("Y1", Nature.Seismic, Direction: SeismicDirection.Y),
            new("X2", Nature.Seismic, Group: "ignored", Direction: SeismicDirection.X), new("Z", Nature.Seismic, Direction: SeismicDirection.Z), new("E9", Nature.Seismic));
        yield return Make("accidental", g, q, new("A1", Nature.Accidental, Group: " A "), new("A2", Nature.Accidental, Group: "A"), new("A3", Nature.Accidental), new("E", Nature.Seismic));
        yield return new("omitted-known", ["G", "Q", "Unused"], [g, q]);
        foreach (int n in new[] { 11, 12, 13 }) yield return Make("pattern-groups-" + n, Enumerable.Range(0, n).Select(i => new Assignment("G" + i, Nature.G1, Group: "g" + i)).ToArray());
        yield return Make("variable-row-limit", [g, .. Enumerable.Range(0, 11).Select(i => new Assignment("Q" + i, Nature.Variable, VariableActionCategory.A))]);
        yield return Make("groups-reduce-limit", [g, .. Enumerable.Range(0, 13).Select(i => new Assignment("Q" + i, Nature.Variable, VariableActionCategory.A, i < 7 ? "a" : "b"))]);
        foreach (int n in new[] { 2044, 2045, 2046 }) yield return Make("rows-" + (6 + 2 * n), [g, new("A", Nature.Accidental), .. Enumerable.Range(0, n).Select(i => new Assignment("E" + i, Nature.Seismic))]);
        yield return new("null-assignments", ["G"], null);
        yield return Make("null-entry", g, null!);
        yield return Make("blank-id", new Assignment(" ", Nature.G1));
        yield return Make("invalid-nature", new Assignment("G", (Nature)99));
        yield return Make("invalid-direction", g with { Direction = (SeismicDirection)99 });
        yield return Make("invalid-category", g with { Category = (VariableActionCategory)99 });
        yield return Make("duplicate", g, g);
        yield return new("unknown", ["G"], [g, q]);
        yield return Make("missing-category", g, q with { Category = null });
        yield return Make("excluded-only", g with { Nature = Nature.Excluded });
        yield return Make("seismic-only", g with { Nature = Nature.Seismic });
        yield return Make("nonseismic-direction", g with { Direction = SeismicDirection.X });
        yield return Make("excluded-direction", g, q with { Nature = Nature.Excluded, Direction = SeismicDirection.X });
        yield return new("precedence-unknown-category", ["G"], [g, q with { Category = null }]);
        yield return Make("precedence-duplicate-category", q with { Category = null }, q);
        yield return Make("precedence-empty-direction", g with { Nature = Nature.Excluded, Direction = SeismicDirection.X });
        string[] codes = ["D", "DC", "DW", "L", "LR", "W", "WL", "S", "T", "TPG", "E", "USER", "", "d", " W "];
        yield return new("proposals", codes.Select((_, i) => "C" + i).ToArray(), [], codes);
        yield return new("choices", ["G", "Q", "New", "Bad", "Direction", "RemovedCategory"], [g, q], ["D", "L", "W", "S", "E", "T"],
            [new("G", "G2", Group: " g "), new("G", "G1"), new("Q", "Variable", "B"), new("Gone", "G1"), new("Bad", "Future"), new("Direction", "Seismic", Direction: "W"), new("RemovedCategory", "Variable", "Future")]);
    }

    // Every floating-point value, including negative zero, is stored by its IEEE-754 bits, never rounded for comparison.
    sealed class BitsConverter : JsonConverter<double>
    {
        public override double Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => BitConverter.Int64BitsToDouble(long.Parse(reader.GetString()!, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options) => writer.WriteStringValue(BitConverter.DoubleToInt64Bits(value).ToString("X16", CultureInfo.InvariantCulture));
    }

    static async Task<string> ResultCapture()
    {
        var model = CombinationModel();
        model.Results = model.Results.Select(r => r with { Displacements = model.Nodes.Select(n => r.Name == "G" ? new ModelDisplacement(n.Id, .001, -.002, .003) { Rotation = new(.001, -.002, .003) }
            : new ModelDisplacement(n.Id, -.002, .008, -.012) { Rotation = new(.002, .004, -.008) }).ToArray() }).ToArray();
        var choices = new Assignment[] { new("G", Nature.G1), new("Q", Nature.Variable, VariableActionCategory.B) };
        var rows = Generator.Generate(model, choices);
        var applied = Generator.Apply(model, rows, choices);
        var service = new GpcModelCombinationService();
        var results = new List<object>();
        foreach (var row in rows)
        {
            var calculated = await service.CalculateAsync(applied, row.Id);
            results.Add(new { row.Id, calculated.Results, calculated.Warnings });
            applied = ModelCombinations.Attach(applied, row.Id, calculated);
        }
        var env = new ModelCombinationDefinition("ANTHEA/envelope", "Envelope", ModelCombinationKind.Envelope, "ANTHEA", "", rows.Select(r => new ModelCombinationTerm(r.Id, true, 1)).ToArray(), true);
        applied = ModelCombinations.Save(applied, env);
        var envelope = await service.CalculateAsync(applied, env.Id);
        results.Add(new { env.Id, envelope.Results, envelope.Warnings });
        var again = Generator.Apply(applied, rows, choices);
        if (again.Results.Length != applied.Results.Length) throw new Exception("Regeneration invalidated unchanged results.");
        var reopened = ModelSnapshot.FromJson(JsonSerializer.Serialize(again));
        if (again.Fingerprint() != reopened.Fingerprint()) throw new Exception("Document roundtrip differs.");
        return JsonSerializer.Serialize(new { Original = model, Applied = again, Fingerprint = again.Fingerprint(), Results = results, Choices = Generator.Choices(reopened) }, Json);
    }
}
