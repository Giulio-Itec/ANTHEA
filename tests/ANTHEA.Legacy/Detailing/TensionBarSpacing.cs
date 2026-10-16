// Spostato da X.Calculations/TensionBarSpacing.cs (commit 8b9ce6d3) con la decisione E5 del 15/10/2026 (F2.11c): classe e interfaccia
// senza chiamanti di produzione, usate solo dalle prove (motori storici della fessurazione e dei dettagli circolari, X.Verifiche e
// ANTHEA.ApiReuse.Checks). Corpi invariati; la regola geometrica resta in Checker (CrackSectionGeometry.MaximumSpacing).
namespace Anthea.Calculations;

/// <summary>Compatibility entry point; the geometric rules belong to Checker.</summary>
public interface ITensionBarSpacing
{
    double? Maximum(SezioneCA section, IReadOnlyList<int> tensileBarIndices);
}

public sealed class TensionBarSpacing : ITensionBarSpacing
{
    public double? Maximum(SezioneCA section, IReadOnlyList<int> tensileBarIndices)
    {
        return ConcreteLibraryMapping.CrackGeometry(section).MaximumSpacing(tensileBarIndices);
    }
}
