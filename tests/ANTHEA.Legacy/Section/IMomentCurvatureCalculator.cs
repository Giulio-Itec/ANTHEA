// Spostato da X.Calculations/MomentCurvature.cs (commit 8b9ce6d3) con la decisione E5 del 15/10/2026 (F2.11c): interfaccia senza
// utilizzatori, né nel prodotto né nelle prove. MomentCurvatureCalculator, che la implementava, resta nel prodotto senza di essa.
// Dichiarazione invariata, conservata per le prove, fuori dall'installer.
namespace Anthea.Calculations;

/// <summary>Native equilibrium is injected. Portable orchestration, no UI or file dependencies.</summary>
public interface IMomentCurvatureCalculator
{
    MomentCurvatureResult Calculate(MomentCurvatureRequest request, Func<ActionPoint,DomainCheck> resistance,
        Func<ActionPoint,CheckerStressState> response, double steelYieldStrain, CancellationToken token=default);
}
