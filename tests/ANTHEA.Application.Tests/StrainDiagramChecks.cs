using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Application.Concrete;
using Anthea.Application.Presentation;

internal static class StrainDiagramChecks
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); checks++; }
        bool Exact(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);
        foreach (bool hole in new[] { false, true })
        {
            var data = ModuleCatalog.CreateData("str_palo"); var settings = SectionWorkspace.Prepare(data);
            var input = data["input"]!.AsObject(); input["foro_presente"] = hole; input["inner_diameter_mm"] = 200; input["inner_width_mm"] = 100; input["inner_height_mm"] = 100;
            var geometry = ConcretePreview.PrepareGeometry(input);
            var engine = new CheckerSection(input, settings, settings["sle"]!["SLE"]!.AsObject());
            foreach (var force in new[] { new ActionPoint(-100, 80, 35), new ActionPoint(-100, 0, 0) })
            {
                var native = engine.Stress(force, "TEST"); var diagram = ConcreteStrainDiagram.Read(new ConcreteResults.StressState(native), geometry);
                var plane = native.Native.TotalStrainPlane;
                Check(Exact(diagram.StrainReferencePoint, plane.StrainReferencePoint) && Exact(diagram.ChiX, plane.ChiX)
                    && Exact(diagram.ChiY, plane.ChiY) && Exact(diagram.Width, geometry.Width) && Exact(diagram.Height, geometry.Height), "Piano o ingombro alterati");
                var actual = diagram.Holes.Prepend(diagram.Outline).ToArray();
                var expected = geometry.Holes.Select(h => (IReadOnlyList<double[]>)h).Prepend(geometry.Outline).ToArray();
                Check(actual.Length == expected.Length && actual.Zip(expected).All(r => r.First.Count == r.Second.Count
                    && r.First.Zip(r.Second).All(p => Exact(p.First.X, p.Second[0]) && Exact(p.First.Y, p.Second[1])
                        && Exact(p.First.Strain, plane.GetStrain(p.Second[0], p.Second[1])))), "Campioni del piano diversi dal nativo");
                var axis = NeutralAxisPresentation.Concrete(new ConcreteResults.StressState(native)); var nativeAxis = SectionNeutralAxis.Concrete(plane);
                Check(axis.Caption == nativeAxis.Caption && axis.Explanation == nativeAxis.Explanation && ((axis.Line is null && nativeAxis.Line is null)
                    || (axis.Line is { } a && nativeAxis.Line is { } n && Exact(a.Start.X,n.Start.X) && Exact(a.Start.Y,n.Start.Y)
                        && Exact(a.End.X,n.End.X) && Exact(a.End.Y,n.End.Y))), "Asse neutro diverso dal nativo");
                double original = diagram.Outline[0].X; geometry.Outline[0][0] += 100;
                Check(Exact(diagram.Outline[0].X, original), "Campioni condivisi con la geometria della vista");
                geometry.Outline[0][0] = original;
            }
        }
        Check(typeof(ConcreteStrainDiagram).GetProperties().All(p => !p.PropertyType.Assembly.GetName().Name!.StartsWith("GPC")), "Il piano espone un motore");
        return checks;
    }
}
