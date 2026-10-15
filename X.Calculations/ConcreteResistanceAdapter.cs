using GPC.Checkers.Concrete.Analysis;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Model.Results;

namespace Anthea.Calculations;

/// <summary>
/// Punto resistente negli assi delle forze del solutore (N e N·mm) con il piano di deformazione che lo genera, dalla porta
/// <see cref="ISectionResistanceSolver"/> di GPCChecker.Concrete. Il tasso di lavoro è quello della libreria, calcolato con il criterio della
/// ricerca.
/// </summary>
public sealed class ResistancePoint
{
    readonly SectionSolver.FailureAnalysisTypes searchCriterion;
    readonly double libraryRatio;

    internal ResistancePoint(SectionResistanceResponse response, SectionSolver.FailureAnalysisTypes criterion)
    {
        searchCriterion = criterion; libraryRatio = response.Utilization!.Value;
        NRd = response.N!.Value; MxRd = response.M1!.Value; MyRd = response.M2!.Value; StrainPlane = response.Strain!.ToStrainPlane();
    }

    public double NRd { get; }
    public double MxRd { get; }
    public double MyRd { get; }
    public StrainPlane StrainPlane { get; }

    /// <summary>Tasso di lavoro dell'azione, calcolato dalla libreria con il criterio della ricerca: chiederlo con un altro criterio è un errore
    /// di programma.</summary>
    public double WorkingRatio(SectionSolver.FailureAnalysisTypes criterion)
    {
        if (criterion != searchCriterion)
            throw new InvalidOperationException($"Punto resistente cercato con il criterio {searchCriterion}: tasso di lavoro chiesto con {criterion}.");
        return libraryRatio;
    }
}

/// <summary>Esito della ricerca per una forza: il punto, oppure il motivo per cui manca.</summary>
public sealed record ResistanceOutcome(ResistancePoint? Point, string? Rejection);

/// <summary>
/// Adattatore del punto resistente SLU della sezione c.a.: unico ingresso di ANTHEA per il punto del dominio N–Mx–My (verifiche del dominio
/// 3D, resistenza a N assegnato, progetto delle armature). Il motore è solo quello della libreria (decisione dell'utente dell'11/10/2026,
/// registro F2-17): la porta <see cref="ISectionResistanceSolver"/> di GPCChecker.Concrete (LegacySectionCalculation.SolveResistance), il
/// calcolo di ModelChecker, che accetta il punto solo se rispetta i vincoli della ricerca (equilibrio su N, direzione del criterio) e
/// altrimenti prova le strategie di bisezione e di intersezione; se nessuna converge non dà il punto e ne dà il motivo. Fino alla revisione
/// R2 ANTHEA usava il punto grezzo di SectionSolver.CalculateDomainPoint, che nessuno controlla: con una direzione di ricerca nulla dava
/// «Entro il dominio» qualunque fosse il momento. Una sola sessione della libreria per sezione, che serializza le chiamate (contratto della
/// libreria). Nessuna formula: il punto e il tasso sono della libreria.
/// </summary>
public static class ConcreteResistanceAdapter
{
    /// <summary>Nome dell'adattatore nella sonda delle prove (<see cref="EngineHook.Record"/>).</summary>
    public const string ProbeName = "ConcreteResistanceAdapter";

    /// <summary>Nome del motore nella sonda.</summary>
    public const string Engine = "Library";

    /// <summary>Punti resistenti di un lotto di forze, nell'ordine, con il criterio delle opzioni del solutore della sezione.</summary>
    public static ResistanceOutcome[] Points(CheckerSection section, IReadOnlyList<ResultBeamForces> forces, CancellationToken token = default)
    {
        EngineHook.Record(ProbeName, "Points", Engine);
        token.ThrowIfCancellationRequested();
        var outcomes = new ResistanceOutcome[forces.Count];
        for (int i = 0; i < forces.Count; i++) outcomes[i] = Solve(section, forces[i], token);
        return outcomes;
    }

    /// <summary>Punto resistente di una forza del dominio 3D (CheckerDomain3D.Check).</summary>
    public static ResistanceOutcome Point(CheckerSection section, ResultBeamForces force, CancellationToken token = default)
    {
        EngineHook.Record(ProbeName, "Point", Engine);
        token.ThrowIfCancellationRequested();
        return Solve(section, force, token);
    }

    static ResistanceOutcome Solve(CheckerSection section, ResultBeamForces force, CancellationToken token)
    {
        var criterion = section.Checker.SectionCheckerOptions.FailureAnalysisType;
        var response = section.ResistanceSolver.SolveResistance(new SectionAnalysisInput(force), token);
        EngineHook.RecordResult(response);
        return response.Diagnostics.Status == CalculationStatus.Completed && !response.IsZeroDemand
            ? new(new ResistancePoint(response, criterion), null)
            : new(null, ConcreteLibraryMapping.ResistanceRejection(response));
    }
}
