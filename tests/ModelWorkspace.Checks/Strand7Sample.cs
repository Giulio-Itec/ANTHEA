using System.IO;
using ANTHEA.ModelWorkspace;
using GPC.Converter;
using GPC.Geometry;
using GPC.Model.Analysis;
using GPC.Model.Attributes;
using GPC.Model.Core.Units;
using GPC.Model.Models;
using GPC.Model.Results.Locations;
using GPC.Model.Results.State;
using GPC.Model.Structure.Assignments;

/// <summary>Synthetic GPC model with the records of the GPC Straus7 converter (program "Straus7", "Straus7 API" evidence, "Case n: name"
/// load cases, LSA combination), mapped by ModelMapper and given results by ResultMapper. No native API: every expected value is an input
/// of this file, so the bridge is checked against known numbers. Units: mm, N, N/mm, N·mm/mm, rad.</summary>
static class Strand7Sample
{
    public const string Dataset = "synthetic-straus7";
    public const string Dead = "Case 1: Dead", Live = "Case 2: Live";

    static CoordinateSystem Axes(Point3d origin, Vector3d v1, Vector3d v2, Vector3d v3) => new(origin, v1, v2, v3);
    static ResultState State(string name, int count) => new() { Semantics = AnalysisSemantics.LinearStatic, ConcomitantStateId = "synthetic " + name, IsCombined = false,
        IsCumulative = true, Components = Enumerable.Repeat(ComponentAvailability.Available, count).ToArray() };

    public static Model Model()
    {
        var batch = new ImportBatch { Program = "Straus7", SolverVersion = "3.1.5", ModelRevision = "synthetic", AnalysisId = "LSA synthetic", SourceHash = "SYNTHETICGEOMETRY" };
        Point3d[] points = [new(0, 0, 0), new(2000, 0, 0), new(2000, 1000, 0), new(0, 1000, 0), new(0, 0, 3000), new(2000, 0, 3000),
            new(4000, 0, 0), new(4000, 1000, 0), new(6000, 0, 0), new(6000, 1000, 0), new(0, 0, 3500), new(2000, 0, 3500)];
        for (int i = 0; i < points.Length; i++) batch.Nodes.Add(new NodeRecord { Id = (i + 1).ToString(), GlobalPosition = points[i], Record = "Node/" + (i + 1) });
        batch.Materials.Add(new MaterialRecord { Id = "Beam/7", Name = "S355", Kind = MaterialKind.Steel, Grade = "S355", ElasticModulus = 210000, Poisson = .3, Density = 7.85e-9, ThermalExpansion = 1.2e-5, Record = "m" });
        batch.Materials.Add(new MaterialRecord { Id = "Plate/3", Name = "S355", Kind = MaterialKind.Steel, Grade = "S355", ElasticModulus = 210000, Poisson = .3, Density = 7.85e-9, ThermalExpansion = 1.2e-5, Record = "m" });
        batch.Sections.Add(new SectionRecord { Id = "Beam/7", Name = "Pila 400x300", Shape = SectionShapeKind.SolidRectangle, Dimensions = [400, 300], Reference = SectionReference.Centroid, Record = "s7" });
        batch.Sections.Add(new SectionRecord { Id = "Beam/8", Name = "Tirante CHS", Shape = SectionShapeKind.Pipe, Dimensions = [114.3, 6.3], Reference = SectionReference.Centroid, Record = "s8" });
        batch.Thicknesses.Add(new ThicknessRecord { Id = "Plate/3", Name = "Soletta 250", InPlane = 250, Record = "t3" });
        var vertical = Axes(points[0], new(1, 0, 0), new(0, 1, 0), new(0, 0, 1));
        var girder = Axes(points[4], new(0, 1, 0), new(0, 0, 1), new(1, 0, 0));
        var tie = Axes(points[5], new(1, 0, 0), new(0, -1, 0), new(0, 0, -1));
        batch.Beams.Add(new BeamRecord { Id = "1", I = "1", J = "5", CoordinateSystem = vertical, MaterialId = "Beam/7", SectionId = "Beam/7", Formulation = GPC.Model.Structure.Assignments.BeamFormulation.StraightTwoNode, Record = "Beam/1" });
        batch.Beams.Add(new BeamRecord { Id = "2", I = "5", J = "6", CoordinateSystem = girder, MaterialId = "Beam/7", SectionId = "Beam/7", Formulation = GPC.Model.Structure.Assignments.BeamFormulation.StraightTwoNode, Record = "Beam/2" });
        batch.Beams.Add(new BeamRecord { Id = "3", I = "6", J = "2", CoordinateSystem = tie, MaterialId = "Beam/7", SectionId = "Beam/8", Formulation = GPC.Model.Structure.Assignments.BeamFormulation.Truss, Record = "Beam/3" });
        CoordinateSystem Plate(params int[] nodes)
        {
            var corners = nodes.Select(n => points[n - 1]).ToArray();
            return Axes(new Point3d(corners.Average(p => p.X), corners.Average(p => p.Y), 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1));
        }
        batch.Shells.Add(new ShellRecord { Id = "1", Nodes = ["1", "2", "3", "4"], CoordinateSystem = Plate(1, 2, 3, 4), MaterialId = "Plate/3", ThicknessId = "Plate/3", Record = "Plate/1" });
        // Plate 2: a property the converter could not map (no physical thickness in GPC); its Straus7 record gives 0.3 m.
        batch.Shells.Add(new ShellRecord { Id = "2", Nodes = ["2", "7", "8", "3"], CoordinateSystem = Plate(2, 7, 8, 3), Record = "Plate/2" });
        // Plate 3: property 3 with an element offset of 50 mm, a second document property.
        batch.Shells.Add(new ShellRecord { Id = "3", Nodes = ["7", "9", "10", "8"], CoordinateSystem = Plate(7, 9, 10, 8), MaterialId = "Plate/3", ThicknessId = "Plate/3", Offset = 50, Record = "Plate/3" });
        foreach (var (record, raw) in new[] { ("St7GetUnits", "[2,0,0,0,0,0]"), ("Beam/1", "{\"Number\":1,\"Property\":7}"), ("Beam/2", "{\"Number\":2,\"Property\":7}"), ("Beam/3", "{\"Number\":3,\"Property\":8}"),
            ("Plate/1", "{\"Number\":1,\"Property\":3}"), ("Plate/2", "{\"Number\":2,\"Property\":4}"), ("Plate/3", "{\"Number\":3,\"Property\":3}"),
            ("Property/Beam/7", "{\"Name\":\"Pila 400x300\"}"), ("Property/Beam/8", "{\"Name\":\"Tirante CHS\"}"),
            ("Property/Plate/3", "{\"Name\":\"Soletta 250\",\"Geometry\":[250,250]}"), ("Property/Plate/4", "{\"Name\":\"\",\"Geometry\":[300,300]}") })
            batch.Uninterpreted.Add(new PreservedAssignment { Kind = "Straus7 API", SourceRecord = record, RawData = raw });
        batch.NodeRestrains.Add(new NodeRestrainRecord { NodeId = "1", FixedDofs = [true, true, true, true, true, true], CoordinateSystem = GPC.Geometry.CoordinateSystem.Global, Record = "Restraint/1" });
        batch.NodeRestrains.Add(new NodeRestrainRecord { NodeId = "4", FixedDofs = [true, true, true, false, false, false], CoordinateSystem = GPC.Geometry.CoordinateSystem.Global, Record = "Restraint/4" });
        var stiffness = new double[36]; stiffness[2 * 6 + 2] = 500;
        batch.NodeSprings.Add(new NodeSpringRecord { NodeId = "2", Spring = new SpringMatrix(stiffness, GPC.Geometry.CoordinateSystem.Global), Record = "NodeSpring/2" });
        batch.NodeLinks.Add(new NodeLinkRecord { I = "5", J = "11", RigidDofs = [true, true, true, true, true, true], Record = "Link/1" });
        var link = new double[36]; for (int i = 0; i < 6; i++) link[i * 6 + i] = 1000;
        batch.NodeLinks.Add(new NodeLinkRecord { I = "6", J = "12", Spring = new SpringMatrix(link, GPC.Geometry.CoordinateSystem.Global), Record = "Link/2" });
        var release = new BeamReleasesAttribute { CoordinateSystem = girder, SourceRecord = "Release/2" };
        release.J[4] = new BeamDofConnection(BeamConnectionKind.Released);
        batch.BeamReleases.Add(new BeamReleaseRecord { BeamId = "2", Release = release, Record = "Release/2" });
        batch.LoadCases.Add(new LoadCaseRecord { Name = Dead, Record = "LoadCase/1" }); batch.LoadCases.Add(new LoadCaseRecord { Name = Live, Record = "LoadCase/2" });
        batch.Combinations.Add(new CombinationRecord { Id = "LSA/1", Name = "ULS", Kind = CombinationKind.Linear, Source = "LSA", Status = "ACTIVE", Record = "LSA combination 1",
            Terms = [new() { Name = Dead, IsStaticCase = true, Analysis = "LSA", Factor = 1.35 }, new() { Name = Live, IsStaticCase = true, Analysis = "LSA", Factor = 1.5 }] });
        var mapped = ModelMapper.Map(batch);
        var model = mapped.Model ?? throw new InvalidDataException("Synthetic Straus7 model rejected: " + string.Join("; ", mapped.Diagnostics.Select(d => d.Code + " " + d.Message)));

        var results = new ResultImportBatch { Source = model.AnalysisSource, DatasetId = Dataset, ExpectedInputFingerprint = model.AnalysisFingerprint(), SourceHash = "SYNTHETICRESULTS",
            ReaderVersion = "synthetic", Units = new ResultUnits(1, 1, 1), ShellDenominatorLengthToMm = 1, ResolvedConvention = "GPC components", IsSynthetic = true, Semantics = AnalysisSemantics.LinearStatic };
        foreach (var (name, scale) in new[] { (Dead, 1.0), (Live, 2.0) })
        {
            foreach (var shell in model.AreaElements.Values)
                foreach (var (node, k) in shell.Nodes.Select((n, k) => (n, k)))
                {
                    var d = node.Position - shell.CoordinateSystem.Origin;
                    results.Shells.Add(new ShellForceRecord { ElementId = shell.Source.OriginalId, Case = name, State = State(name, 8), Axes = shell.CoordinateSystem,
                        Values = [scale * (10 + k), scale * 20, scale, scale * 2, scale * 3, scale * 1000 * (k + 1), scale * -500, scale * 50],
                        Location = new Point2d(d.X, d.Y), CoordinateKind = ResultCoordinateKind.LocalPhysical, PointKind = ShellResultPointKind.ElementNodeExtrapolated,
                        SourceNodeId = node.Source.OriginalId, Record = "plate " + shell.Source.OriginalId + "/" + name + "/" + node.Source.OriginalId });
                }
            foreach (var beam in model.BeamElements.Values)
                foreach (double xi in beam.Assignments.Formulation == GPC.Model.Structure.Assignments.BeamFormulation.Truss ? new[] { 0d, 1 } : [0, .5, 1])
                {
                    var axes = beam.Assignments.SectionAxes; var at = beam.StartPoint + (beam.EndPoint - beam.StartPoint) * xi;
                    results.Beams.Add(new BeamForceRecord { ElementId = beam.Source.OriginalId, Case = name, State = State(name, 6), Axes = new CoordinateSystem(at, axes.V1, axes.V2, axes.V3),
                        Values = [scale * -5000, scale * 100, scale * 200, scale * 3e5, scale * 2e6 * (1 - xi), scale * 4e5], Station = xi, PhysicalDistance = beam.Length * xi,
                        Body = ActionBody.PositiveSectionFace, StationDomain = "NodeToNode", Record = "beam " + beam.Source.OriginalId + "/" + name + "/" + xi });
                }
            foreach (var node in model.NodesElements.Values)
                results.NodeDisplacements.Add(new NodeDisplacementRecord { ElementId = node.Source.OriginalId, Case = name, State = State(name, 6),
                    Axes = new CoordinateSystem(node.Position, new Vector3d(1, 0, 0), new Vector3d(0, 1, 0), new Vector3d(0, 0, 1)),
                    Values = [scale * 1, scale * -2, scale * -3, scale * 1e-4, 0, scale * -2e-4], Record = "node " + node.Source.OriginalId + "/" + name });
            // 1.16.0: support reactions of the restrained nodes 1 (0, 0, 0) and 4 (0, 1000, 0), a self-balanced pair: Fz ±100 N and Mx 1e5 N·mm at node 4.
            foreach (var (node, values) in new[] { ("1", new[] { 0, 0, scale * 100, 0, 0, 0 }), ("4", new[] { 0, 0, scale * -100, scale * 1e5, 0, 0 }) })
                results.NodeForces.Add(new NodeForceRecord { ElementId = node, Case = name, State = State(name, 6), Kind = NodalForceKind.SupportReaction, Body = ActionBody.OnNode,
                    Axes = new CoordinateSystem(model.NodesElements.Values.Single(n => n.Source.OriginalId == node).Position, new Vector3d(1, 0, 0), new Vector3d(0, 1, 0), new Vector3d(0, 0, 1)),
                    Values = values.Select(v => (double?)v).ToArray(), Record = "reaction " + node + "/" + name });
        }
        var imported = ResultMapper.Import(model, results);
        if (imported.Status != ImportStatus.Completed) throw new InvalidDataException("Synthetic Straus7 results rejected: " + string.Join("; ", imported.Diagnostics.Select(d => d.Code + " " + d.Message)));
        return model;
    }

    public static ModelSnapshot Snapshot(bool results = true)
    {
        var model = Model();
        return GpcModelSnapshot.Create(model, Strand7Snapshot.Options(model, "sintetico-straus7", "Straus7 · modello sintetico", results ? Dataset : null, true, true, Strand7Snapshot.Conventions(results)));
    }
}
