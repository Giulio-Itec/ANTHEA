using System.Text.Json.Nodes;
using X.Core;

namespace Anthea.Application.Documents;

/// <summary>
/// Wire version 5 (compact GPC sections, ANTHEA 1.19, decision of the user of 4/11/2026, «Compatto dalla prossima»): a document with at least
/// one concrete section whose GPC model is in the compact archive (ConcreteSectionArchive version 2, ModelArchiveFormat.Compact of
/// GPC.Model.Persistence 3.14, which the libraries of ANTHEA up to 1.18 cannot read). It is the outer layer, over the results archive (4)
/// and the concrete sections (3), so ANTHEA up to 1.18 removes none of its layers (each one looks for its own version only) and refuses
/// the file with «Formato o versione del file non supportati.» (DocumentService.Validate), the message of a file of a newer version,
/// instead of failing on a section. The document without compact sections keeps the versions 1–4 and its bytes.
/// </summary>
internal static class CompactSectionDocumentPersistence
{
    internal const int Version = 5;
    /// <summary>The version of the document under this layer: 3 (concrete sections) or 4 (results archive over them).</summary>
    const string BaseVersion = "versione_senza_compatto";

    internal static JsonObject Pack(JsonObject packed, bool compact)
    {
        if (!compact) return packed;
        packed[BaseVersion] = packed["versione"]!.DeepClone();
        packed["versione"] = Version;
        return packed;
    }

    internal static void Unpack(JsonObject document)
    {
        if (J.Number(document["versione"]) != Version) return;
        if (J.Number(document[BaseVersion]) is not (3d or 4d)) throw new ArgumentException("Versione di base del documento con le sezioni GPC compatte non supportata.");
        document["versione"] = document[BaseVersion]!.DeepClone();
        document.Remove(BaseVersion);
    }
}
