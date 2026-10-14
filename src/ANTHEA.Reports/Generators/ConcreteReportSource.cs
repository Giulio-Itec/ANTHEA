using System.Text.Json.Nodes;

namespace X.Core;

/// <summary>Reports use the input snapshot already archived with the calculated result.</summary>
internal static class ConcreteReportSource
{
    internal static JsonObject Read(JsonObject result)
    {
        if (result["dati"] is not JsonObject source || source["workspace_ca"] is not JsonObject settings)
            throw new ArgumentException("Risultato privo degli input di calcolo: ricalcolare la sezione prima del report.");
        string standard = ConcreteStandards.RequiredName(settings);
        if (result.S("normativa_riferimento") != standard)
            throw new ArgumentException("Normativa del risultato incoerente con gli input archiviati: ricalcolare la sezione.");
        return (JsonObject)source.DeepClone();
    }
}
