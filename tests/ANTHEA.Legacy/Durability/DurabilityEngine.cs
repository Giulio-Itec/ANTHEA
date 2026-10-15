// Spostato da X.Calculations/ConcreteDurabilityAdapter.cs (commit 4144fe9e) con la decisione D3 del 15/10/2026: il selettore del motore
// della durabilità serve solo alle prove. Codice storico conservato per le prove, fuori dall'installer.
namespace Anthea.Calculations;

/// <summary>Motore della durabilità e dei copriferri: il nucleo legacy (DurabilityLegacy) o GPCChecker.Concrete.</summary>
public enum DurabilityEngine { Legacy, Library }
