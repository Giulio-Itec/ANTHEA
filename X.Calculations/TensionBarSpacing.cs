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
