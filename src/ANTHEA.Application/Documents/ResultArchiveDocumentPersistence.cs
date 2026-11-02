using System.Text.Json.Nodes;
using X.Core;

namespace Anthea.Application.Documents;

/// <summary>
/// Wire version 4 (results archive, decision of the user of 2/11/2026): a project with at least one model whose results are in the archive
/// beside the file (entry of schema 2 in «modelli_calcolo», ANTHEA.ModelWorkspace.ProjectModelStore). The document without it keeps the
/// versions 1–3 and its bytes. ANTHEA up to 1.17.0 removes the layers 3 and 2 only and then refuses every version but 1 with «Formato o
/// versione del file non supportati.» (Validate), so a version 4 is never read as a project without results.
/// </summary>
internal static class ResultArchiveDocumentPersistence
{
    internal const int Version = 4;
    /// <summary>The version of the document under this layer (1, 2 or 3).</summary>
    const string BaseVersion = "versione_senza_archivio";
    const string Models = "modelli_calcolo";
    const int ArchivedSchema = 2;

    internal static JsonObject Pack(JsonObject packed)
    {
        if (!HasArchivedModels(packed)) return packed;
        packed[BaseVersion] = packed["versione"]!.DeepClone();
        packed["versione"] = Version;
        return packed;
    }

    internal static void Unpack(JsonObject document)
    {
        if (J.Number(document["versione"]) != Version) return;
        if (J.Number(document[BaseVersion]) is not (1 or 2 or 3)) throw new ArgumentException("Versione di base del documento con l'archivio dei risultati non supportata.");
        document["versione"] = document[BaseVersion]!.DeepClone();
        document.Remove(BaseVersion);
    }

    /// <summary>A model of schema 2 in a project, in a section or in the context of a revision of a section. Only the containers are read,
    /// never the copies themselves (they may be large).</summary>
    static bool HasArchivedModels(JsonNode? node) => node switch
    {
        JsonArray items => items.Any(HasArchivedModels),
        JsonObject container => container[Models] is JsonArray models && models.OfType<JsonObject>().Any(m => J.Number(m["schema"]) == ArchivedSchema)
            || HasArchivedModels(container["progetti"]) || HasArchivedModels(container["strutture"])
            || HasArchivedModels(container["revisioni"]) || HasArchivedModels(container["contesto"]),
        _ => false
    };
}
