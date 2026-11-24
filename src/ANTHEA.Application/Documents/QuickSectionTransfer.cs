using System.Text.Json.Nodes;
using X.Core;

namespace Anthea.Application.Documents;

/// <summary>Self-contained section exchanged by drag and drop, also between independent application processes.</summary>
public static class QuickSectionTransfer
{
    private const string Format = "ANTHEA.Sezione";

    public static string Export(JsonObject section)
    {
        ValidateSection(section);
        return J.Obj(("formato", Format), ("versione", 1), ("sezione", section.DeepClone())).ToJsonString();
    }

    public static JsonObject PrepareImport(string payload, JsonObject destination)
    {
        var section = Read(payload);
        QuickSectionDocuments.Validate(destination);
        if (section.S("modulo_id") != destination.S("modulo_id"))
            throw new ArgumentException("Le sezioni devono appartenere allo stesso modulo.");
        // Copies of the same saved file may be open in different processes: never reuse their identifiers.
        section["id"] = Guid.NewGuid().ToString("N");
        return section;
    }

    public static bool Matches(string payload, JsonObject section) => JsonNode.DeepEquals(Read(payload), section);

    private static JsonObject Read(string payload)
    {
        if (JsonNode.Parse(payload) is not JsonObject envelope || envelope.S("formato") != Format
            || J.Number(envelope["versione"]) != 1 || envelope["sezione"] is not JsonObject section)
            throw new ArgumentException("Dati della sezione trascinata non validi.");
        ValidateSection(section);
        return (JsonObject)section.DeepClone();
    }

    private static void ValidateSection(JsonObject section)
    {
        if (section.S("tipo") != "calcolo" || !QuickSectionDocuments.Supports(section.S("modulo_id")))
            throw new ArgumentException("Il trasferimento è disponibile soltanto per le sezioni rapide.");
        DocumentService.Validate(section);
    }
}
