using GPC.Checkers.Concrete.Analysis;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Model.Results;

namespace Anthea.Calculations;

/// <summary>
/// Motore del punto resistente SLU della sezione c.a. (revisione di architettura R2, registro F2-17). Legacy: punto grezzo di
/// SectionSolver.CalculateDomainPoint e FailureDomainResult.CalculateForce, il percorso di ANTHEA fino a R2, che non controlla il punto.
/// Library: porta <see cref="ISectionResistanceSolver"/> di GPCChecker.Concrete (LegacySectionCalculation.SolveResistance), il calcolo di
/// ModelChecker, che accetta il punto solo se rispetta i vincoli della ricerca (equilibrio su N, direzione del criterio) e altrimenti prova
/// le strategie di bisezione e di intersezione; se nessuna converge non dà il punto.
/// </summary>
public enum ResistanceEngine { Legacy, Library }

/// <summary>
/// Punto resistente negli assi delle forze del solutore (N e N·mm) con il piano di deformazione che lo genera. Con il motore Legacy porta il
/// punto nativo e il tasso di lavoro si calcola come prima di R2; con il motore Library il tasso è quello della libreria, calcolato con il
/// criterio della ricerca.
/// </summary>
public sealed class ResistancePoint
{
    readonly FailureDomain.FailureDomainPoint? native;
    readonly SectionSolver.FailureAnalysisTypes searchCriterion;
    readonly double libraryRatio;

    internal ResistancePoint(FailureDomain.FailureDomainPoint point)
    {
        native = point; Engine = ResistanceEngine.Legacy;
        NRd = point.NRd; MxRd = point.MxRd; MyRd = point.MyRd; StrainPlane = point.StrainPlane;
    }

    internal ResistancePoint(SectionResistanceResponse response, SectionSolver.FailureAnalysisTypes criterion)
    {
        Engine = ResistanceEngine.Library; searchCriterion = criterion; libraryRatio = response.Utilization!.Value;
        NRd = response.N!.Value; MxRd = response.M1!.Value; MyRd = response.M2!.Value; StrainPlane = response.Strain!.ToStrainPlane();
    }

    public ResistanceEngine Engine { get; }
    public double NRd { get; }
    public double MxRd { get; }
    public double MyRd { get; }
    public StrainPlane StrainPlane { get; }

    /// <summary>
    /// Tasso di lavoro dell'azione (FailureDomainPoint.CalculateWorkingRatio con le scale dei momenti e delle forze di ANTHEA). Con il motore
    /// Library la libreria lo ha calcolato con il criterio della ricerca: chiederlo con un altro criterio è un errore di programma.
    /// </summary>
    public double WorkingRatio(SectionSolver.FailureAnalysisTypes criterion, ResultBeamForces force)
    {
        if (native is not null) return native.CalculateWorkingRatio(criterion, force, 1e6, 1000);
        if (criterion != searchCriterion)
            throw new InvalidOperationException($"Punto resistente cercato con il criterio {searchCriterion}: tasso di lavoro chiesto con {criterion}.");
        return libraryRatio;
    }
}

/// <summary>Esito della ricerca per una forza: il punto, oppure il motivo per cui manca (null se il motore Legacy non trova il punto).</summary>
public sealed record ResistanceOutcome(ResistancePoint? Point, string? Rejection);

/// <summary>
/// Adattatore del punto resistente SLU della sezione c.a. (revisione di architettura R2, registro F2-17): unico ingresso di ANTHEA per il
/// punto del dominio N–Mx–My (verifiche del dominio 3D, resistenza a N assegnato, progetto delle armature). Il motore è quello fissato
/// alla costruzione di <see cref="CheckerSection"/>; null = <see cref="Default"/>. Nessuna formula: il punto e il tasso sono della libreria.
/// Il motore Library usa una sola sessione LegacySectionCalculation per sezione, che serializza le chiamate (contratto della libreria);
/// il motore Legacy conserva il vettore nativo con le ricerche in parallelo.
/// </summary>
public static class ConcreteResistanceAdapter
{
    /// <summary>Nome dell'adattatore nella sonda (<see cref="ServiceabilityProbe"/>).</summary>
    public const string ProbeName = "ConcreteResistanceAdapter";

    /// <summary>Motore usato quando la sezione non ne indica uno: legacy finché l'utente non decide il passaggio (registro F2-17).</summary>
    public static ResistanceEngine Default => ResistanceEngine.Legacy;

    /// <summary>Punti resistenti di un lotto di forze, nell'ordine, con il criterio delle opzioni del solutore della sezione.</summary>
    public static ResistanceOutcome[] Points(CheckerSection section, IReadOnlyList<ResultBeamForces> forces, CancellationToken token = default)
    {
        var chosen = section.ResistanceEngine ?? Default;
        ServiceabilityProbe.Record(ProbeName, "Points", chosen);
        token.ThrowIfCancellationRequested();
        if (chosen == ResistanceEngine.Legacy)
        {
#pragma warning disable CS0618 // percorso legacy raggiungibile fino alla decisione di F2-17
            var points = section.Checker.SectionSolver.CalculateDomainPoint(forces.ToArray());
#pragma warning restore CS0618
            return points.Select(p => new ResistanceOutcome(p is null ? null : new ResistancePoint(p), null)).ToArray();
        }
        var outcomes = new ResistanceOutcome[forces.Count];
        for (int i = 0; i < forces.Count; i++) outcomes[i] = Solve(section, forces[i], token);
        return outcomes;
    }

    /// <summary>Punto resistente di una forza del dominio 3D (CheckerDomain3D.Check); il motore Legacy usa il dominio nativo come prima di R2.</summary>
    public static ResistanceOutcome Point(CheckerSection section, FailureDomainResult domain, ResultBeamForces force, CancellationToken token = default)
    {
        var chosen = section.ResistanceEngine ?? Default;
        ServiceabilityProbe.Record(ProbeName, "Point", chosen);
        token.ThrowIfCancellationRequested();
        if (chosen == ResistanceEngine.Legacy)
        {
#pragma warning disable CS0618 // percorso legacy raggiungibile fino alla decisione di F2-17
            var p = domain.CalculateForce(force);
#pragma warning restore CS0618
            return new(p is null ? null : new ResistancePoint(p), null);
        }
        return Solve(section, force, token);
    }

    static ResistanceOutcome Solve(CheckerSection section, ResultBeamForces force, CancellationToken token)
    {
        var criterion = section.Checker.SectionCheckerOptions.FailureAnalysisType;
        var response = section.ResistanceSolver.SolveResistance(new SectionAnalysisInput(force), token);
        ServiceabilityProbe.RecordResult(response);
        return response.Diagnostics.Status == CalculationStatus.Completed && !response.IsZeroDemand
            ? new(new ResistancePoint(response, criterion), null)
            : new(null, ConcreteLibraryMapping.ResistanceRejection(response));
    }
}
