using System.Text.Json.Nodes;

namespace Anthea.Calculations;

public sealed record PrincipalMomentResistance(string Direction, double? Moment, ActionPoint? Resistance, string Status)
{
    public SectionResponse? Section { get; init; }
}

public static class SectionMomentResistance
{
    /// <summary>Tolerance on N (kN) of a resistance point at the assigned axial force: the rule of the library (refactoring S-1), which
    /// depends on the concrete diagram (larger with the stress block, less precise) and on the size of the section.</summary>
    public static double AxialToleranceKn(GPC.Checkers.Concrete.SectionSolvers.SectionSolver solver, double axial) =>
        GPC.Checkers.Concrete.SectionSolvers.DomainPointAxialTolerance.Calculate(solver, axial * 1000) / 1000;
    internal static double AxialToleranceKn(CheckerSection engine, double axial) => AxialToleranceKn(engine.Checker.SectionSolver, axial);
    public static PrincipalMomentResistance[] Calculate(JsonObject input, JsonObject workspace, double axial, bool elastic, CancellationToken token = default)
    {
        if (!double.IsFinite(axial)) throw new ArgumentException("N deve essere finito.");
        var options = J.Obj(("criterio", "N costante"), ("assi", "Locali"), ("strategia", "Iterativo"), ("modello", "Non lineare"));
        var engine = new CheckerSection(input, workspace, options, elastic ? "SLV" : "SLU");
        return Calculate(engine, axial, [("Mx+", 1d, 0d), ("Mx−", -1d, 0d), ("My+", 0d, 1d), ("My−", 0d, -1d)], token);
    }
    internal static PrincipalMomentResistance[] Calculate(CheckerSection engine, double axial,
        (string Label, double Mx, double My)[] directions, CancellationToken token = default)
    {
        var results = new List<PrincipalMomentResistance>();
        foreach (var (label, mx, my) in directions)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                // Direct iterative search: neither Domain3D nor GetFailureDomainResult is called.
                var point = engine.Checker.SectionSolver.CalculateDomainPoint([engine.Force(new(axial, mx, my))])[0];
                if (point is null) throw new ArgumentException("Punto resistente non trovato.");
                var r = CheckerSection.Point(point);
                double moment = mx != 0 ? r.Mx : r.My, transverse = mx != 0 ? r.My : r.Mx;
                if (!double.IsFinite(r.N + r.Mx + r.My) || Math.Abs(r.N - axial) > AxialToleranceKn(engine, axial)
                    || moment * (mx + my) < 0 || Math.Abs(transverse) > Math.Max(1, Math.Abs(moment) * .001))
                    throw new ArgumentException("Soluzione non coerente con N e direzione assegnati.");
                results.Add(new(label, moment, r, "Resistenza a N costante · assi locali") { Section = engine.Describe(point) });
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { results.Add(new(label, null, null, ex.Message)); }
        }
        return results.ToArray();
    }

    public static ActionPoint VerificationOrigin(ActionPoint action, string criterion) => criterion switch
    {
        "N costante" => new(action.N, 0, 0),
        "N e Mx costanti" => new(action.N, action.Mx, 0),
        "N e My costanti" => new(action.N, 0, action.My),
        "Mx–My costanti" => new(0, action.Mx, action.My),
        _ => new(0, 0, 0)
    };
}
