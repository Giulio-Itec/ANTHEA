using System.Collections.Concurrent;
using GPC.Checkers.Concrete.Piles;
namespace Anthea.Calculations;

/// <summary>Workspace-scoped cache, excluded from archives. Keys include the complete section, materials and solver configuration.</summary>
public sealed class ElasticPileVerificationCache
{
    readonly ConcurrentDictionary<string,PileResistanceTable> sections=new();
    internal PileResistanceTable For(string signature)=>sections.GetOrAdd(signature,_=>new PileResistanceTable());
    public int SectionCount=>sections.Count;
    public int ResistanceCount=>sections.Values.Sum(t=>t.Count);
    public void Clear()=>sections.Clear();
}
