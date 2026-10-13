using System.Collections;
using System.Reflection;
using System.Text.Json.Nodes;

/// <summary>Shared test oracle: compares original and owned values without serialization or numeric tolerances.</summary>
internal sealed class OwnedProjectionComparison(params string[] ignoredProperties)
{
    public int Values { get; private set; }
    internal void Compare(object? native, object? copy, string path)
    {
        if (native is null || copy is null) { if (native != copy) throw new Exception(path + ": null cambiato"); return; }
        if (native is double number) { if (BitConverter.DoubleToInt64Bits(number) != BitConverter.DoubleToInt64Bits((double)copy)) throw new Exception(path + ": valore cambiato"); Values++; return; }
        if (native is string || native is bool || native is int) { if (!native.Equals(copy)) throw new Exception(path + ": dato cambiato"); Values++; return; }
        if (native.GetType().IsEnum) { if (native.ToString() != copy.ToString() || Convert.ToInt64(native) != Convert.ToInt64(copy)) throw new Exception(path + ": enumerazione cambiata"); Values++; return; }
        if (native is JsonNode json && copy is JsonNode projected)
        {
            if (ReferenceEquals(native, copy) || !JsonNode.DeepEquals(json, projected)) throw new Exception(path + ": JSON condiviso o diverso");
            Values++; return;
        }
        if (native is IEnumerable sequence && copy is IEnumerable projection)
        {
            var a = sequence.Cast<object?>().ToArray(); var b = projection.Cast<object?>().ToArray();
            if (a.Length != b.Length) throw new Exception(path + ": conteggio cambiato");
            // Array.Empty is safely shared: it has no mutable entries.
            if (ReferenceEquals(native, copy) && a.Length > 0) throw new Exception(path + ": contenitore del motore esposto alla vista");
            for (int i = 0; i < a.Length; i++) Compare(a[i], b[i], path + "[" + i + "]");
            return;
        }
        if (ReferenceEquals(native, copy)) throw new Exception(path + ": oggetto del motore esposto alla vista");
        foreach (var p in copy.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (ignoredProperties.Contains(p.Name)) continue;
            var sourceProperty = native.GetType().GetProperty(p.Name) ?? throw new Exception(path + ": campo inatteso " + p.Name);
            Compare(sourceProperty.GetValue(native), p.GetValue(copy), path + "." + p.Name);
        }
    }
}
