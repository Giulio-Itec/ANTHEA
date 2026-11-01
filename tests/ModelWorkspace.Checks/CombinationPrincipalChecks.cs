using System.Text.Json;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelWorkspace;

/// <summary>
/// Principal components of the plates computed by GPC (request of 12/10/2026, point 8 of 1/11/2026): for a combination the principal values of
/// the combined local state, for an envelope those of each simultaneous state with extremes and governing states; never summed over the cases
/// and never from extremes of the local components that do not occur together. Expected values by hand, in the displayed convention (MIDAS,
/// kN·m/m and kN/m), from the local values of the fixture: G (10, 20, 30 | 40, 50, 60) and Q (−4, 8, −12 | 16, −20, 24).
/// </summary>
static partial class Program
{
    static async Task CombinationPrincipalChecks()
    {
        // max, min = (xx + yy)/2 ± √(((xx − yy)/2)² + xy²), on the displayed local values.
        static (double Max, double Min) P(double xx, double yy, double xy) { double c = (xx + yy) / 2, r = Math.Sqrt((xx - yy) * (xx - yy) / 4 + xy * xy); return (c + r, c - r); }
        static bool Near(double a, double b) => Math.Abs(a - b) <= 1e-9 * Math.Max(1, Math.Abs(b));
        double[] Field(ModelResultSet r, string c) => ResultFields.Read(r, "PLATE", "PRINCIPAL", c).Select(v => v.Value).ToArray();
        var service = new GpcModelCombinationService();
        var linear = new ModelCombinationDefinition("ANTHEA/slu", "SLU", ModelCombinationKind.Linear, "ANTHEA", "", [new("G", false, 1.3), new("Q", false, 1.5)], true);
        var envelope = new ModelCombinationDefinition("ANTHEA/inv", "Inviluppo", ModelCombinationKind.Envelope, "ANTHEA", "", [new("G", false, 1), new("Q", false, 1)], true);
        var model = ModelCombinations.Save(ModelCombinations.Save(CombinationModel(), linear), envelope);

        // Linear: 1.3 G + 1.5 Q = (7, 38, 21 | 76, 35, 114): Mmax, Mmin from (7, 38, 21), Fmax, Fmin from (76, 35, 114).
        var sum = (await service.CalculateAsync(model, linear.Id)).Results.Single();
        var (m1, m2) = P(1.3 * 10 + 1.5 * -4, 1.3 * 20 + 1.5 * 8, 1.3 * 30 + 1.5 * -12); var (f1, f2) = P(1.3 * 40 + 1.5 * 16, 1.3 * 50 + 1.5 * -20, 1.3 * 60 + 1.5 * 24);
        Check(sum.Values.Length == 4 && new[] { ("Mmax", m1), ("Mmin", m2), ("Fmax", f1), ("Fmin", f2) }.All(e => Field(sum, e.Item1) is { Length: 4 } v && v.All(x => Near(x, e.Item2))),
            $"principals: linear combination, principal values of the combined local state by hand (Mmax {m1:F4}, Mmin {m2:F4}, Fmax {f1:F4}, Fmin {f2:F4})");
        var (g1, g2) = P(10, 20, 30); var (q1, q2) = P(-4, 8, -12);
        Check(!Near(Field(sum, "Mmax")[0], 1.3 * g1 + 1.5 * q1), "principals: never the sum of the principal values of the cases");
        Check(sum.Combination!.PrincipalGoverning == null && sum.Fields!.Take(sum.Fields.Length - 4).All(f => f.Axes == "LOCAL"), "principals: after the local fields, no governing for a linear combination");

        // Envelope of G and Q: each principal component is the extreme over the two simultaneous states, with its governing state.
        var both = (await service.CalculateAsync(model, envelope.Id)).Results;
        var (gf1, gf2) = P(40, 50, 60); var (qf1, qf2) = P(16, -20, 24);
        var expected = new (string Component, double Min, string MinState, double Max, string MaxState)[]
        {
            ("Mmax", Math.Min(g1, q1), q1 < g1 ? "Q" : "G", Math.Max(g1, q1), g1 > q1 ? "G" : "Q"), ("Mmin", Math.Min(g2, q2), q2 < g2 ? "Q" : "G", Math.Max(g2, q2), g2 > q2 ? "G" : "Q"),
            ("Fmax", Math.Min(gf1, qf1), qf1 < gf1 ? "Q" : "G", Math.Max(gf1, qf1), gf1 > qf1 ? "G" : "Q"), ("Fmin", Math.Min(gf2, qf2), qf2 < gf2 ? "Q" : "G", Math.Max(gf2, qf2), gf2 > qf2 ? "G" : "Q")
        };
        double[] gState = [10, 20, 30, 40, 50, 60, 70, 80], qState = [-4, 8, -12, 16, -20, 24, -28, 32];
        bool Governs(ModelResultSet r, string component, double value, string state)
        {
            var governing = r.Combination!.PrincipalGoverning!.Where(g => g.Component == component).ToArray();
            return governing.Length == 4 && governing.All(g => r.Combination.PrincipalAlternatives![int.Parse(g.Alternative[1..]) - 1].EndsWith("=" + state, StringComparison.Ordinal)
                && g.ConcomitantValues.Zip(state == "G" ? gState : qState).All(p => Near(p.First, p.Second))) && Field(r, component).All(v => Near(v, value));
        }
        Check(both.Length == 2 && expected.All(e => Governs(both[0], e.Component, e.Min, e.MinState) && Governs(both[1], e.Component, e.Max, e.MaxState)),
            "principals: envelope, extremes of the principal values of the simultaneous states with the governing state and its local vector");

        // In the document and in the viewer: kept and reopened; selector «Principali», table with the governing states.
        var attached = ModelCombinations.Attach(model, envelope.Id, new([.. both], [], 0));
        var reopened = ModelSnapshot.FromJson(JsonSerializer.Serialize(attached));
        Check(reopened.Fingerprint() == attached.Fingerprint() && reopened.Results.Last().Combination!.PrincipalGoverning!.Length == 16, "principals: kept in the document with their governing states");
        using var vm = new ModelViewerViewModel(reopened, [], false, new FakeServices(), _ => { }, (_, _, _, _) => { });
        vm.SelectedCase = both[1].Name; vm.SelectedAxes = 1; vm.SelectedComponent = "Mmax";
        Check(vm.HasResults && vm.Axes == "PRINCIPAL", "principals: the selector «Principali» shows the computed field: " + vm.ResultAvailability);
        vm.ShowTable = true; vm.SelectedTableKind = 5;
        string csv = vm.TableCsv();
        Check(csv.Contains("Governante", StringComparison.Ordinal) && csv.Contains("=G", StringComparison.Ordinal), "principals: the table gives the governing alternative of each value");
        // A combination calculated before: no principal field, and the viewer says why.
        var older = ModelSnapshot.FromJson(JsonSerializer.Serialize(attached));
        older.Results = [.. older.Results.Select(r => r.Combination is { } c ? r with { Fields = r.Fields?.Where(f => f.Axes != "PRINCIPAL").ToArray(), Combination = c with { PrincipalGoverning = null, PrincipalAlternatives = null } } : r)];
        using var old = new ModelViewerViewModel(older, [], false, new FakeServices(), _ => { }, (_, _, _, _) => { });
        old.SelectedCase = both[1].Name; old.SelectedAxes = 1;
        Check(!old.HasResults && old.ResultAvailability.Contains("calcolala di nuovo", StringComparison.Ordinal), "principals: a combination calculated before says to calculate it again: " + old.ResultAvailability);
    }
}
