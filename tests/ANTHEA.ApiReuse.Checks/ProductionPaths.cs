using Anthea.Testing;
using Anthea.Calculations;
using System.Text.Json.Nodes;

internal static class ProductionPaths
{
    internal static void Check()
    {
        foreach (string shape in new[] { "Rettangolare", "Circolare" })
        foreach (ServiceabilityEngine? requested in new ServiceabilityEngine?[] { null, ServiceabilityEngine.Library, ServiceabilityEngine.Legacy })
        {
            var data = SezioneCA.DefaultData();
            var input = data["input"]!.AsObject(); input["shape"] = shape;
            var settings = SectionWorkspace.Prepare(data);
            settings["sle_comuni"]!["esposizione"] = "XC3";
            SectionWorkspace.Prepare(data);
            using var probe = ServiceabilityProbe.Start();
            // Decision D3 of 15/10/2026: the engine is hooked by the test (null: no hook, the production path); results carry no engine marker.
            var results = ServiceabilityEngines.Run(requested, () => new ConcreteAnalysisSession().Stress(input, settings, settings["sle"]!["SLE_QP"]!.AsObject(),
                "SLE_QP", [J.Obj(("id", "test"), ("N", "-100"), ("Mx", "10"), ("My", "0"))]));
            var expected = requested ?? ServiceabilityEngine.Library;
            var cracking = probe.Entries.Where(e => e.Operation == "Cracking").ToArray();
            if (results["test"].CrackResult is null || cracking.Length == 0 || cracking.Any(e => e.Engine != expected.ToString()))
                throw new Exception($"Wrong production cracking path: {shape}, requested {requested}; {results["test"].Cracking}");
        }
    }
}
