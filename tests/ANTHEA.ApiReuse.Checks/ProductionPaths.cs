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
            var results = new ConcreteAnalysisSession(requested).Stress(input, settings, settings["sle"]!["SLE_QP"]!.AsObject(),
                "SLE_QP", [J.Obj(("id", "test"), ("N", "-100"), ("Mx", "10"), ("My", "0"))]);
            var expected = requested ?? ServiceabilityEngine.Library;
            if (results["test"].CrackResult?.GetNativeForTest().Engine != expected || !probe.Entries.Any(e => e.Operation == "Cracking" && e.Engine == expected.ToString()))
                throw new Exception($"Wrong production cracking path: {shape}, requested {requested}; {results["test"].Cracking}");
        }
    }
}
