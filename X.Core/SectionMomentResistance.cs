using System.Text.Json.Nodes;

namespace X.Core;

public sealed record PrincipalMomentResistance(string Direction, double? Moment, ActionPoint? Resistance, string Status);

public static class SectionMomentResistance
{
    public static PrincipalMomentResistance[] Calculate(JsonObject input, JsonObject workspace, double axial, bool elastic, CancellationToken token = default)
    {
        if (!double.IsFinite(axial)) throw new ArgumentException("N deve essere finito.");
        var options = J.Obj(("criterio", "N costante"), ("assi", "Locali"), ("strategia", "Iterativo"), ("modello", "Non lineare"));
        var engine = new CheckerSection(input, workspace, options, elastic ? "SLV" : "SLU");
        var results = new List<PrincipalMomentResistance>();
        foreach (var (label, mx, my) in new[] { ("Mx+", 1d, 0d), ("Mx−", -1d, 0d), ("My+", 0d, 1d), ("My−", 0d, -1d) })
        {
            token.ThrowIfCancellationRequested();
            try
            {
                // Direct iterative search: neither Domain3D nor GetFailureDomainResult is called.
                var point = engine.Checker.SectionSolver.CalculateDomainPoint([engine.Force(new(axial, mx, my))])[0];
                if (point is null) throw new ArgumentException("Punto resistente non trovato.");
                var r = CheckerSection.Point(point);
                double moment = mx != 0 ? r.Mx : r.My, transverse = mx != 0 ? r.My : r.Mx;
                if (!double.IsFinite(r.N + r.Mx + r.My) || Math.Abs(r.N - axial) > Math.Max(1, Math.Abs(axial) * 1e-6)
                    || moment * (mx + my) < 0 || Math.Abs(transverse) > Math.Max(1, Math.Abs(moment) * .001))
                    throw new ArgumentException("Soluzione non coerente con N e direzione assegnati.");
                results.Add(new(label, moment, r, "Resistenza a N costante · assi locali"));
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
