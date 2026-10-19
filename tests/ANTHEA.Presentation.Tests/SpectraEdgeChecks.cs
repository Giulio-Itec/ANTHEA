using System.Text.Json.Nodes;
using Anthea.Application.Actions;

namespace Anthea.Presentation.Tests;

/// <summary>Independent Excel captures: exact node, quadrilateral, all four triangular configurations and outside sites.</summary>
internal static class SpectraEdgeChecks
{
    internal static void Run(Expect expect, string root)
    {
        foreach (string file in new[] { "cslp103-grid-edges.json", "cslp103-grid-kind1.json" })
        {
            var capture = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "tests/fixtures/spettri", file)))!;
            foreach (var test in capture["cases"]!.AsArray())
            {
                double Number(JsonNode node) => node.GetValue<double>();
                string label = test!["name"] + "/" + test["method"];
                bool outside = Number(test["status"]!) == 0;
                var input = SpectraDocument.Create();
                input["longitude"] = Number(test["longitude"]!);
                input["latitude"] = Number(test["latitude"]!);
                input["method"] = Number(test["method"]!) == 1 ? "Distanza inversa" : "Superficie rigata";
                JsonObject result;
                try { result = SpectraDocument.Calculate(input); }
                catch (ArgumentException error) when (outside)
                {
                    expect.That(error.Message.Contains("esterno al reticolo"), label + ": rifiuto esplicito del sito esterno");
                    continue;
                }
                expect.That(!outside, label + ": sito esterno erroneamente accettato");
                var site = result["site"]!;
                void Compare(string field, double expected, double actual) => expect.That(
                    double.IsFinite(actual) && Math.Abs(actual - expected) <= 1e-10 * Math.Max(1, Math.Abs(expected)),
                    $"{label}/{field}: Excel {expected:G17}, modulo {actual:G17}");
                Compare("kind", Number(test["kind"]!), Number(site["GridType"]!));
                var values = test["selected"]![0]!.AsArray();
                expect.That(values.Count == 126 && site["Periods"]!.AsArray().Count == 9, label + ": copertura dei nove TR e delle 99 ordinate tabulate");
                for (int i = 0; i < 9; i++)
                {
                    var period = site["Periods"]![i]!;
                    var parameters = period["Parameters"]!;
                    Compare($"{i}/ag", Number(values[3 * i]!) / 10, Number(parameters["AgG"]!));
                    Compare($"{i}/F0", Number(values[3 * i + 1]!), Number(parameters["F0"]!));
                    Compare($"{i}/TcStar", Number(values[3 * i + 2]!), Number(parameters["TcStarSeconds"]!));
                    for (int j = 0; j < 11; j++)
                        Compare($"{i}/tabulated/{j}", Number(values[27 + 11 * i + j]!), Number(period["TabulatedSpectrum"]![j]!["AccelerationG"]!));
                }
            }
        }
    }
}
