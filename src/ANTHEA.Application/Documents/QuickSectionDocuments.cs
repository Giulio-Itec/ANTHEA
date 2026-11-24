using System.Text.Json.Nodes;
using X.Core;

namespace Anthea.Application.Documents;

/// <summary>A file containing all the independent sections of one quick module, in tab order.</summary>
public static class QuickSectionDocuments
{
    public const string Kind = "sezioni";
    private const int WireVersion = 6;
    public static bool Supports(string module) => module is "str_sezione_ca_rapida" or "str_sezione_composta_rapida";

    public static JsonObject Create(string module)
    {
        if (!Supports(module)) throw new ArgumentException("Modulo della raccolta di sezioni non valido.");
        return J.Obj(("formato", "X"), ("versione", 1), ("tipo", Kind), ("modulo_id", module), ("sezioni", new JsonArray()));
    }

    public static JsonObject Add(JsonObject document, string name)
    {
        var sheet = DocumentService.CreateDocument(document.S("modulo_id"));
        sheet["id"] = Guid.NewGuid().ToString("N"); sheet["nome"] = name;
        if (sheet.S("modulo_id") == "str_sezione_ca_rapida")
            Concrete.ConcreteQuickSection.View(sheet["dati"]!.AsObject())["titolo"] = name;
        document["sezioni"]!.AsArray().Add(sheet);
        document["attiva"] = sheet.S("id");
        return sheet;
    }

    public static IReadOnlyList<JsonObject> Sections(JsonObject document) => document["sezioni"]!.AsArray().OfType<JsonObject>().ToArray();

    public static JsonObject FromSingle(JsonObject sheet)
    {
        var document = Create(sheet.S("modulo_id"));
        var copy = (JsonObject)sheet.DeepClone(); copy["id"] = Guid.NewGuid().ToString("N");
        document["sezioni"]!.AsArray().Add(copy); document["attiva"] = copy.S("id");
        return document;
    }

    public static void Validate(JsonObject document)
    {
        if (J.Number(document["versione"]) != 1) throw new ArgumentException("Versione della raccolta di sezioni non supportata.");
        Structure(document);
        foreach (var sheet in Sections(document)) DocumentService.Validate(sheet);
    }

    private static void Structure(JsonObject document)
    {
        if (document.S("formato") != "X" || document.S("tipo") != Kind || !Supports(document.S("modulo_id"))
            || document["sezioni"] is not JsonArray { Count: > 0 } sections)
            throw new ArgumentException("Raccolta di sezioni mancante o non valida.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in sections)
            if (node is not JsonObject sheet || sheet.S("tipo") != "calcolo" || sheet.S("modulo_id") != document.S("modulo_id")
                || !Guid.TryParse(sheet.S("id"), out _) || !ids.Add(sheet.S("id")))
                throw new ArgumentException("Sezione della raccolta non valida o identificatore duplicato.");
        if (!ids.Contains(document.S("attiva"))) throw new ArgumentException("Sezione attiva della raccolta non valida.");
    }

    internal static JsonObject Pack(JsonObject document)
    {
        Validate(document);
        var packed = (JsonObject)document.DeepClone();
        packed["versione"] = WireVersion;
        packed["sezioni"] = new JsonArray(Sections(document).Select(s => (JsonNode)DocumentService.Encode(s)).ToArray());
        return packed;
    }

    internal static bool Unpack(JsonObject document)
    {
        if (document.S("tipo") != Kind || J.Number(document["versione"]) != WireVersion) return false;
        Structure(document);
        foreach (var sheet in Sections(document)) DocumentService.Decode(sheet, null);
        document["versione"] = 1;
        Validate(document);
        return true;
    }

    public static void Reorder(JsonObject document, string moving, string target, bool after)
    {
        var sections = document["sezioni"]!.AsArray();
        var item = sections.OfType<JsonObject>().Single(s => s.S("id") == moving);
        var destination = sections.OfType<JsonObject>().Single(s => s.S("id") == target);
        if (ReferenceEquals(item, destination)) return;
        sections.Remove(item);
        sections.Insert(sections.IndexOf(destination) + (after ? 1 : 0), item);
    }

    public static void Insert(JsonObject document, JsonObject section, string target, bool after)
    {
        Validate(document);
        DocumentService.Validate(section);
        var sections = document["sezioni"]!.AsArray();
        var destination = sections.OfType<JsonObject>().Single(s => s.S("id") == target);
        if (section.Parent is not null || section.S("modulo_id") != document.S("modulo_id")
            || !Guid.TryParse(section.S("id"), out _) || sections.OfType<JsonObject>().Any(s => s.S("id") == section.S("id")))
            throw new ArgumentException("Sezione da inserire non valida.");
        sections.Insert(sections.IndexOf(destination) + (after ? 1 : 0), section);
        document["attiva"] = section.S("id");
    }

    /// <summary>Removes a transferred section. An empty collection must be closed, never saved as a document.</summary>
    public static void RemoveTransferred(JsonObject document, JsonObject section)
    {
        var sections = document["sezioni"]!.AsArray();
        int index = sections.IndexOf(section);
        if (index < 0) throw new ArgumentException("La sezione non appartiene alla raccolta di origine.");
        bool selected = document.S("attiva") == section.S("id");
        sections.RemoveAt(index);
        if (selected) document["attiva"] = sections.Count == 0 ? "" : sections[Math.Min(index, sections.Count - 1)].S("id");
    }
}
