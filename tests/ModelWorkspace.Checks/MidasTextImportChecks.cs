using System.Text;
using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;

static partial class Program
{
    internal static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ANTHEA.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("ANTHEA.sln non trovato.");
    }

    /// <summary>1.15.0: MIDAS Civil .mct and Gen .mgt text models read through the GPC converter (fixtures copied from the GPC Model tests).</summary>
    static async Task MidasTextImportChecks(string output)
    {
        string fixtures = Path.Combine(RepositoryRoot(), "tests", "fixtures", "model-viewer-midas-text");
        string directory = Path.Combine(output, "midas-text"); Directory.CreateDirectory(directory);
        static bool Near(double a, double b, double tolerance = 1e-9) => Math.Abs(a - b) <= tolerance;
        static bool Same(ModelDirection v, double x, double y, double z) => Near(v.X, x, 1e-9) && Near(v.Y, y, 1e-9) && Near(v.Z, z, 1e-9);

        // Coverage model: every mapped command of the converter.
        string coveragePath = Path.Combine(fixtures, "coverage.mct");
        var coverage = ModelSnapshotFiles.Read(coveragePath);
        Check(coverage.Name == "coverage" && coverage.Source.StartsWith("MIDAS Civil · file .mct · coverage.mct · SHA-256 ", StringComparison.Ordinal) && coverage.Results.Length == 0,
            "MIDAS .mct: name, declared source and no analysis results");
        Check(coverage.Nodes.Length == 8 && coverage.Nodes.Single(n => n.Id == 4) is { X: 6, Y: 4, Z: 3 }, "MIDAS .mct: nodes converted from the N/mm tables to metres");
        Check(coverage.Elements.Select(e => (e.Id, e.Type)).SequenceEqual([(1, "BEAM"), (2, "BEAM"), (3, "BEAM"), (4, "TRUSS"), (5, "TENSTR"), (10, "PLATE"), (11, "PLATE")]) &&
              coverage.Elements.Single(e => e.Id == 2).Angle == 90 && coverage.Elements.Single(e => e.Id == 11).Nodes.SequenceEqual([2, 3, 4]) && coverage.Elements.Single(e => e.Id == 11).Angle == 30,
            "MIDAS .mct: beams, truss, tension-only member, quadrilateral and triangular plates with beta and ANGLE");
        var pillar = coverage.Sections.Single(s => s.Id == 1);
        Check(pillar is { Shape: "SB", Centered: true } && Near(pillar.Width, .4) && Near(pillar.Height, .6) && coverage.Sections.Single(s => s.Id == 2) is { Shape: "H", Width: 0, Centered: false } &&
              coverage.Sections.Single(s => s.Id == 4).Shape == "SR", "MIDAS .mct: solid rectangle with its size, other sections by name and shape only");
        Check(coverage.Plates.Single(p => p.Id == 1) is { Name: "Soletta", Offset: 0 } && Near(coverage.Plates.Single(p => p.Id == 1).Thickness, .25) &&
              Near(coverage.Plates.Single(p => p.Id == 2).Thickness, .2) && Near(coverage.Plates.Single(p => p.Id == 2).Offset, .1), "MIDAS .mct: thicknesses and offset ratio 0.5 of 0.2 m");
        var frames = coverage.LocalFrames!.ToDictionary(f => f.Element);
        Check(frames.Count == 7 && Same(frames[1].X, 0, 0, 1) && Same(frames[1].Z, 1, 0, 0) && Same(frames[2].Z, 0, 1, 0) &&
              Same(frames[10].X, 1, 0, 0) && Same(frames[10].Z, 0, 0, 1) && Same(frames[11].X, Math.Cos(Math.PI / 6), Math.Sin(Math.PI / 6), 0),
            "MIDAS .mct: GPC frames of vertical and rotated members and of plates rotated by ANGLE");
        Check(coverage.NodeFrames!.Length == 8 && Same(coverage.NodeFrames.Single(f => f.Node == 6).X, Math.Sqrt(.5), Math.Sqrt(.5), 0) && Same(coverage.NodeFrames.Single(f => f.Node == 1).X, 1, 0, 0),
            "MIDAS .mct: node axes of LOCALAXIS, global triads elsewhere");
        var boundaries = coverage.Boundaries!;
        Check(boundaries.Where(b => b.Kind == "RESTRAIN").Select(b => (b.Nodes[0], b.Dofs)).OrderBy(b => b.Item1)
                .SequenceEqual([(1, "DX, DY, DZ, RX, RY, RZ"), (6, "DX, DY, DZ"), (10, "DX, DY, DZ, RX, RY, RZ")]),
            "MIDAS .mct: supports with their DOFs, the warping flag excluded");
        Check(boundaries.Count(b => b.Kind == "LINK" && b.Nodes.SequenceEqual([4, 9]) && b.Dofs == "Matrice elastica") == 1 && boundaries.Count(b => b.Kind == "CONSTRAINT") == 2 &&
              boundaries.Where(b => b.Kind == "RELEASE").Select(b => (b.Element, b.Nodes[0])).OrderBy(b => b.Item2).SequenceEqual([((int?)3, 2), (3, 3)]),
            "MIDAS .mct: elastic and rigid links and releases at both ends of girder 3");
        var definitions = ModelCombinations.Read(coverage);
        Check(definitions.Select(d => d.Name).Order().SequenceEqual(["C1", "ENV", "S1"]) && definitions.Single(d => d.Name == "C1").Terms.Single(t => t.Reference == "G").Factor == 1.35 &&
              definitions.Single(d => d.Name == "ENV").Kind == ModelCombinationKind.Envelope && definitions.Single(d => d.Name == "ENV").Terms.Any(t => t.IsCombination && t.Reference == "LCOM-GEN/C1") &&
              definitions.Single(d => d.Name == "S1").Source == "LCOM-STEEL" && ModelCombinations.StaticCaseNames(coverage).SequenceEqual(["G", "Q", "W"]),
            "MIDAS .mct: GEN and STEEL combinations, nested references and static cases read by ModelCombinations");
        var availability = ModelCombinations.Inspect(coverage);
        Check(availability.All(a => !a.CanCalculate && a.State == "Casi mancanti"), "MIDAS .mct: combinations wait for results the text file cannot contain");
        var loads = MidasGraphicalLoads.Read(coverage);
        Check(loads.Loads.Any(l => l.Case == "Q" && l.Node == 3 && !l.Moment && Near(l.Vector.Y, 5) && Near(l.Vector.Z, -20)) && loads.Loads.Any(l => l.Case == "Q" && l.Node == 3 && l.Moment && Near(l.Vector.X, 1)) &&
              loads.Loads.Count(l => l.Element == 3 && Near(l.Vector.Z, -10) && l.Unit == "kN/m") == 5 && loads.Loads.Any(l => l.Element == 10 && Near(l.Vector.Z, -2) && l.Unit == "kN/m²") &&
              loads.Loads.Any(l => l.Case == "W" && l.Element == 11 && Near(l.Vector.X, 1.5)), "MIDAS .mct: nodal, uniform beam and pressure glyphs in kN, kN/m, kN/m²");
        var codes = coverage.ImportMessages!.Select(m => m.Code).ToHashSet();
        Check(codes.IsSupersetOf(["MIDAS-TEXT", "MIDAS-TEXT-SOURCE", "AxialOnlyBeamFormulation", "GeometryOnly"]) && !codes.Contains("MIDAS-TEXT-FAMILIES-EXCLUDED"),
            "MIDAS .mct: GPC diagnostics and source summary become import messages");
        Check(ModelSnapshotFiles.Read(coveragePath).Fingerprint() == coverage.Fingerprint(), "MIDAS .mct: the same file gives the same document fingerprint");
        coverage.Validate();
        var reopened = ModelSnapshot.FromJson(System.Text.Json.JsonSerializer.Serialize(coverage));
        Check(reopened.Fingerprint() == coverage.Fingerprint(), "MIDAS .mct: the copy survives the package round trip");
        var catalog = ModelSectionCatalog.Create(coverage);
        Check(catalog.Find(1)!.HasGeometry && catalog.Find(1)!.WidthMillimetres is > 399 and < 401 && catalog.Find(2)!.HasGeometry, "MIDAS .mct: section details read from the translated SECT table");

        // Point springs and undefined sections.
        var springs = ModelSnapshotFiles.Read(Path.Combine(fixtures, "springs.mct"));
        var springSymbols = springs.Boundaries!.Where(b => b.Dofs.StartsWith("Molla elastica", StringComparison.Ordinal)).ToArray();
        var fixedDofs = springs.Boundaries!.Where(b => b.Kind == "RESTRAIN" && b.Nodes[0] == 1 && !b.Dofs.StartsWith("Molla", StringComparison.Ordinal)).SelectMany(b => b.Dofs.Split(", ")).ToHashSet();
        Check(springSymbols.Select(b => (b.Nodes[0], b.Dofs)).OrderBy(b => b.Item1).SequenceEqual([(1, "Molla elastica · DX, DY, RZ"), (3, "Molla elastica · DX")]) &&
              fixedDofs.SetEquals(["DX", "DY", "DZ"]) && Same(springs.NodeFrames!.Single(f => f.Node == 3).X, 0, 1, 0),
            "MIDAS .mct: linear point springs and fixed spring DOFs in the node axes; the COMP spring is not drawn as linear");
        Check(springs.Sections.Single() is { Id: 1, Width: 0, Name: "Sezione non definita nel file" } && springs.ImportMessages!.Any(m => m.Code == "MIDAS-TEXT-SECTION-MISSING") &&
              springs.ImportMessages!.Any(m => m.Code == "CivilNxSpringLawPreserved"), "MIDAS .mct: members of an undefined section stay as axes, with a message");

        // Element families without a viewer element.
        var families = ModelSnapshotFiles.Read(Path.Combine(fixtures, "families.mct"));
        Check(families.Elements.Select(e => e.Type).SequenceEqual(["COMPTR", "COMPTR", "TENSTR", "TENSTR"]) && families.Nodes.Length == 8 &&
              families.ImportMessages!.Single(m => m.Code == "MIDAS-TEXT-FAMILIES-EXCLUDED").Message.StartsWith("3 elementi", StringComparison.Ordinal) &&
              families.ImportMessages!.Count(m => m.Code == "CivilElementFamilyPreserved") == 5, "MIDAS .mct: walls, plane-stress and solid elements are filtered with a message before validation");

        // MIDAS Gen.
        var gen = ModelSnapshotFiles.Read(Path.Combine(fixtures, "gen-walls.mgt"));
        Check(gen.Source.StartsWith("MIDAS Gen · file .mgt · gen-walls.mgt", StringComparison.Ordinal) && gen.Elements.Select(e => e.Id).SequenceEqual([1, 3]) &&
              Same(gen.LocalFrames!.Single(f => f.Element == 3).X, 0, -1, 0) && gen.Sections.Single() is { Shape: "SB" } && gen.Plates.Single().Thickness == .2 &&
              gen.Boundaries!.Count(b => b.Kind == "RESTRAIN") == 2 && gen.ImportMessages!.Any(m => m.Code == "GenFloorDiaphragmPreserved") && gen.ImportMessages!.Any(m => m.Code == "GenWallPreserved"),
            "MIDAS .mgt: Gen export, wall excluded, plate rotated by ANGLE -90, diaphragm preserved with a message");

        // Native Civil NX 9.5.5 exports: the text copy equals the API copy of the same model.
        foreach (string name in new[] { "native-packed-pairs-9.5.5", "native-catalog-pairs-9.5.5" })
        {
            var native = ModelSnapshotFiles.Read(Path.Combine(fixtures, name + ".mct"));
            Check(native.Nodes.Length > 0 && native.Elements.All(e => e.Type == "BEAM") && native.LocalFrames!.Length == native.Elements.Length && native.Boundaries!.Any(b => b.Kind == "RESTRAIN"),
                "MIDAS native " + name + ": export read with frames and supports");
        }
        string api = Path.Combine(directory, "api-packed-pairs"); Directory.CreateDirectory(api);
        foreach (var file in Directory.GetFiles(Path.Combine(fixtures, "api-packed-pairs-9.5.5"))) File.Copy(file, Path.Combine(api, Path.GetFileName(file)), true);
        File.WriteAllText(Path.Combine(api, "THIK.json"), "{\"THIK\":{}}");
        var fromApi = ModelSnapshotFiles.Read(Path.Combine(api, "NODE.json")); var fromText = ModelSnapshotFiles.Read(Path.Combine(fixtures, "native-packed-pairs-9.5.5.mct"));
        Check(fromApi.Nodes.Length == fromText.Nodes.Length && fromApi.Nodes.Zip(fromText.Nodes.OrderBy(n => n.Id)).All(p => p.First.Id == p.Second.Id && Near(p.First.X, p.Second.X) && Near(p.First.Y, p.Second.Y) && Near(p.First.Z, p.Second.Z)) &&
              fromApi.Elements.OrderBy(e => e.Id).Zip(fromText.Elements.OrderBy(e => e.Id)).All(p => p.First.Id == p.Second.Id && p.First.Type == p.Second.Type && p.First.Property == p.Second.Property && p.First.Angle == p.Second.Angle && p.First.Nodes.SequenceEqual(p.Second.Nodes)) &&
              fromApi.Sections.OrderBy(s => s.Id).Select(s => (s.Id, s.Name, s.Shape)).SequenceEqual(fromText.Sections.OrderBy(s => s.Id).Select(s => (s.Id, s.Name, s.Shape))) &&
              ModelCombinations.StaticCaseNames(fromApi).SequenceEqual(ModelCombinations.StaticCaseNames(fromText)),
            "MIDAS native 9.5.5: the .mct copy has the nodes, members, sections and cases of the API copy");

        // Rejections are atomic: no partial copy and the displayed model is kept.
        string broken = Path.Combine(directory, "senza-enddata.mct");
        File.WriteAllText(broken, File.ReadAllText(coveragePath).Replace("*ENDDATA", ""), Encoding.ASCII);
        Reject(() => ModelSnapshotFiles.Read(broken), "MIDAS .mct without *ENDDATA is rejected");
        string dangling = Path.Combine(directory, "nodo-mancante.mct");
        File.WriteAllText(dangling, File.ReadAllText(coveragePath).Replace("     4, TRUSS ,    2,     3,     1,     3,", "     4, TRUSS ,    2,     3,     1,    99,"), Encoding.ASCII);
        Reject(() => ModelSnapshotFiles.Read(dangling), "MIDAS .mct with a member on a missing node is rejected");
        // Real exports keep the offset value of a thickness whose offset is switched off: no offset, as in GPC (found on 3 of 36 real files).
        string switchedOff = Path.Combine(directory, "offset-disattivato.mct");
        File.WriteAllText(switchedOff, File.ReadAllText(coveragePath).Replace("1, VALUE, Soletta, YES, 0.25, 0,  NO, 0, 0", "1, VALUE, Soletta, YES, 0.25, 0,  NO, 0, 0.3"), Encoding.ASCII);
        var offsetOff = ModelSnapshotFiles.Read(switchedOff);
        Check(offsetOff.Plates.Single(p => p.Id == 1).Offset == 0 && File.ReadAllText(switchedOff).Contains("NO, 0, 0.3"), "MIDAS .mct: a switched-off thickness offset keeps its value in the file and gives no offset");
        string foreign = Path.Combine(directory, "testo.mct"); File.WriteAllText(foreign, "questo non è un modello\n", Encoding.UTF8);
        Reject(() => ModelSnapshotFiles.Read(foreign), "a text file that is not a MIDAS model is rejected");
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            try { ModelSnapshotFiles.Read(coveragePath, cancelled.Token); throw new Exception("Cancellation ignored"); }
            catch (OperationCanceledException) { Check(true, "MIDAS .mct import observes cancellation"); }
        }
        var files = new TextFileServices { Next = coveragePath }; int commits = 0;
        using (var vm = new ModelViewerViewModel(null, [], false, files, _ => commits++, (_, _, _, _) => { }))
        {
            await vm.ImportCommand.ExecuteAsync(null);
            Check(commits == 1 && vm.Snapshot?.Fingerprint() == coverage.Fingerprint() && vm.Summary.StartsWith("8 nodi", StringComparison.Ordinal), "viewer imports a MIDAS .mct through the file source");
            string before = vm.Snapshot!.Fingerprint(); files.Next = broken;
            await vm.ImportCommand.ExecuteAsync(null);
            Check(commits == 1 && vm.Snapshot!.Fingerprint() == before && vm.HasImportFailure && vm.Status.Contains("non completata"), "a rejected MIDAS file keeps the previous copy without a partial update");
        }
    }

    sealed class TextFileServices : IModelViewerServices
    {
        public string Next = "";
        public Task<ModelSnapshot?> ImportAsync(CancellationToken token) => Task.Run(() => (ModelSnapshot?)ModelSnapshotFiles.Read(Next, token), token);
        public void FitView() { }
        public void SaveImage() { }
        public void ExportTable(string name, string csv) { }
    }
}
