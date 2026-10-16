// Spostato da X.Calculations/ConcreteDetailing.cs (commit 8b9ce6d3) con la decisione E5 del 15/10/2026 (F2.11c): interfaccia senza
// utilizzatori, né nel prodotto né nelle prove. ConcreteAnchorageCalculator, che la implementava, resta nel prodotto senza di essa.
// Dichiarazione invariata, conservata per le prove, fuori dall'installer.
namespace Anthea.Calculations;

public interface IConcreteAnchorageCalculator { AnchorageResult Calculate(AnchorageInput input); }
