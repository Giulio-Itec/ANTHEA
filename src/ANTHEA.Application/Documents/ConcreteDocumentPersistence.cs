using System.Text.Json.Nodes;
using Anthea.ConcreteWorkspace;
using X.Core;

namespace Anthea.Application.Documents;

/// <summary>
/// Wire version 3 adds native concrete sections to both live sheets and immutable revision data.
/// The working document keeps version 1. Versions 1/2 remain readable and migrate only on save.
/// </summary>
internal static class ConcreteDocumentPersistence
{
    internal static JsonObject Pack(JsonObject document)
    {
        var packed = ProjectRevisions.Pack(document);
        bool found = Transform(packed, data =>
        {
            // Work only on the detached document. Save must not prepare the editor or alter its drafts.
            var input = data["input"]!.AsObject();
            var workspace = data["workspace_ca"] as JsonObject ?? new JsonObject();
            var stored = ConcreteSectionArchive.Restore(input, workspace);
            if (stored is null)
            {
                GPC.Model.Sections.Concrete.ReinforcedConcreteSection? section;
                try { section = CheckerSection.PrepareModel(input, workspace).Section; }
                catch (ArgumentException error)
                {
                    input[ConcreteSectionArchive.Key] = ConcreteSectionArchive.Draft(input, workspace, error.Message);
                    return;
                }
                input[ConcreteSectionArchive.Key] = ConcreteSectionArchive.Capture(input, workspace, section);
            }
        });
        if (found)
        {
            packed["versione_base"] = packed["versione"]!.DeepClone();
            packed["versione"] = 3;
        }
        return packed;
    }

    internal static void Unpack(JsonObject document)
    {
        if (J.Number(document["versione"]) != 3) return;
        if (J.Number(document["versione_base"]) is not (1 or 2))
            throw new ArgumentException("Versione di base del documento GPC non supportata.");
        if (!Transform(document, data =>
        {
            if (data["input"] is not JsonObject input || !input.ContainsKey(ConcreteSectionArchive.Key))
                throw new ArgumentException("Sezione GPC mancante nel documento.");
            var workspace = data["workspace_ca"] as JsonObject ?? new JsonObject();
            _ = ConcreteSectionArchive.Restore(input, workspace, requireCurrent: true);
        }, rewrite: false))
            throw new ArgumentException("Documento GPC privo di sezioni in cemento armato.");
        document["versione"] = document["versione_base"]!.DeepClone();
        document.Remove("versione_base");
    }

    // Explicit sheet nodes, including the contexts of old revisions. Never inspect arbitrary input/report payloads.
    private static IEnumerable<JsonObject> Sheets(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            if (obj.ContainsKey("modulo_id")) { yield return obj; yield break; }
            foreach (var pair in obj.Where(p => p.Key != "archivio_revisioni"))
                foreach (var sheet in Sheets(pair.Value)) yield return sheet;
        }
        else if (node is JsonArray array)
            foreach (var child in array)
                foreach (var sheet in Sheets(child)) yield return sheet;
    }

    private static bool Transform(JsonObject document, Action<JsonObject> transform, bool rewrite = true)
    {
        var sheets = Sheets(document).Where(s => s.S("modulo_id") == "str_palo").ToArray();
        if (sheets.Length == 0) return false;
        var pool = document["archivio_revisioni"] as JsonObject;
        var remapped = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var sheet in sheets)
        {
            if (sheet.ContainsKey("dati_ref"))
            {
                string key = sheet.S("dati_ref");
                if (!remapped.TryGetValue(key, out string? replacement))
                {
                    if (pool?[key] is not JsonObject data || ProjectRevisions.DataKey(data) != key)
                        throw new ArgumentException("Dati di revisione mancanti o danneggiati.");
                    var copy = data.DeepClone().AsObject();
                    transform(copy);
                    replacement = rewrite ? ProjectRevisions.DataKey(copy) : key;
                    remapped[key] = replacement;
                    if (rewrite && !pool.ContainsKey(replacement)) pool[replacement] = copy;
                }
                if (rewrite) sheet["dati_ref"] = replacement;
            }
            else
            {
                // Empty lazy sheets become the same default sheet the editor would create.
                if (sheet["dati"] is not JsonObject && rewrite) sheet["dati"] = DocumentService.CreateSheet("str_palo");
                if (sheet["dati"] is not JsonObject data) throw new ArgumentException("Dati della sezione mancanti.");
                transform(data);
            }
        }
        if (rewrite && pool is not null)
        {
            var used = Sheets(document).Select(s => s.S("dati_ref")).ToHashSet(StringComparer.Ordinal);
            foreach (string key in remapped.Keys.Where(k => !used.Contains(k))) pool.Remove(key);
        }
        return true;
    }
}
