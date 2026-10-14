using GPC.Design.Sections;

namespace Anthea.Calculations;

/// <summary>Shared projection of section reinforcement into the GPC design contracts.</summary>
internal static class ConcreteDesignMapping
{
    internal static DesignBar[] Bars(SezioneCA section) => section.Bars.Select(b => new DesignBar(b.X, b.Y, b.Diametro, b.Area)).ToArray();
}
