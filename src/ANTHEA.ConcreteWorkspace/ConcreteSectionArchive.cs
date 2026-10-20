using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml;
using Anthea.Calculations;
using GPC.Model.Persistence;
using GPC.Model.Sections.Concrete;

namespace Anthea.ConcreteWorkspace;

/// <summary>
/// Persistence of the native section through GPC's versioned contracts. The editor recipe is retained separately:
/// changing it invalidates the stored section; changing checks, actions or view settings does not.
/// No section, material or mesh algorithm belongs to this codec.
/// </summary>
public static class ConcreteSectionArchive
{
    public const string Key = "sezione_gpc";
    private const string Units = "mm-MPa-N";

    public static string EditorStamp(JsonObject input, JsonObject workspace)
    {
        var recipe = input.DeepClone().AsObject();
        recipe.Remove(Key);
        return Hash(Canonical(J.Obj(("input", recipe), ("trefoli", workspace["trefoli"] ?? new JsonArray()))).ToJsonString());
    }

    private static JsonNode Canonical(JsonNode node) => node switch
    {
        JsonObject obj => new JsonObject(obj.OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => KeyValuePair.Create(p.Key, p.Value is null ? null : Canonical(p.Value)))),
        JsonArray array => new JsonArray(array.Select(n => n is null ? null : Canonical(n)).ToArray()),
        _ => node.DeepClone()
    };
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static JsonObject Capture(JsonObject input, JsonObject workspace, ReinforcedConcreteSection section)
    {
        var model = new GPC.Model.Models.Model("ANTHEA · Sezione c.a.");
        model.AddProperty(section);
        using var stream = new MemoryStream();
        ModelArchive.SaveDocument(model, stream);
        string xml = Encoding.UTF8.GetString(stream.ToArray());
        return J.Obj(("versione", 1), ("stato", "modello"), ("unita", Units),
            ("editor_sha256", EditorStamp(input, workspace)), ("archivio_sha256", Hash(xml)), ("archivio", xml));
    }

    public static JsonObject Draft(JsonObject input, JsonObject workspace, string reason)
        => J.Obj(("versione", 1), ("stato", "bozza"), ("unita", Units),
            ("editor_sha256", EditorStamp(input, workspace)), ("motivo", reason));

    /// <summary>
    /// Returns an independent GPC section. Null means a legacy/draft document or an editor change since the snapshot.
    /// Corrupt, unknown or future contracts are refused, never silently replaced by a reconstruction.
    /// </summary>
    public static ReinforcedConcreteSection? Restore(JsonObject input, JsonObject workspace, bool requireCurrent = false)
    {
        if (!input.ContainsKey(Key)) return null;
        if (input[Key] is not JsonObject saved || J.Number(saved["versione"]) != 1 || saved.S("unita") != Units)
            throw new ArgumentException("Contratto della sezione GPC non supportato.");
        string state = saved.S("stato");
        if (state is not ("modello" or "bozza") || saved.S("editor_sha256").Length != 64)
            throw new ArgumentException("Stato della sezione GPC non valido.");
        bool current = saved.S("editor_sha256") == EditorStamp(input, workspace);
        if (requireCurrent && !current)
            throw new ArgumentException("Sezione GPC e parametri dell’editor non corrispondono: archivio incompleto o modificato.");
        if (state == "bozza")
        {
            if (saved.ContainsKey("archivio") || string.IsNullOrWhiteSpace(saved.S("motivo")))
                throw new ArgumentException("Bozza della sezione GPC non valida.");
            return null;
        }
        string xml = saved.S("archivio");
        if (xml.Length == 0 || Hash(xml) != saved.S("archivio_sha256"))
            throw new ArgumentException("Archivio della sezione GPC mancante o danneggiato.");
        // A live edit requires a new model; do not deserialize the old one for each keystroke.
        if (!current) return null;
        try
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml), writable: false);
            var model = ModelArchive.Load(stream);
            if (model.AllElements.Any() || model.BeamProperties.Count != 1 ||
                model.BeamProperties.Values.Single() is not ReinforcedConcreteSection section)
                throw new ArgumentException("L’archivio GPC non contiene una sola sezione in cemento armato.");
            return section;
        }
        catch (Exception error) when (error is SerializationException or XmlException or InvalidDataException)
        {
            throw new ArgumentException("Impossibile leggere la sezione GPC: " + error.Message, error);
        }
    }
}
