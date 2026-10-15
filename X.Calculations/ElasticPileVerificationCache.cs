using System.Collections.Concurrent;
using GPC.Checkers.Concrete.Piles;
namespace Anthea.Calculations;

/// <summary>Workspace-scoped cache, excluded from archives. Keys include the complete section, materials and solver configuration.</summary>
public sealed class ElasticPileVerificationCache
{
    internal readonly ConcurrentDictionary<string,PileResistanceTable> sections=new();
    internal PileResistanceTable For(string signature)=>sections.GetOrAdd(signature,_=>new PileResistanceTable());
    public void Clear()=>sections.Clear();
}
