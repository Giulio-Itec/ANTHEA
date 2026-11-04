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
/// Contract version 1 (ANTHEA up to 1.18): «archivio» is the XML document of ModelArchive.SaveDocument. Version 2 (ANTHEA 1.19, decision
/// of the user of 4/11/2026, «Compatto dalla prossima»): «archivio» is the compact archive of GPC.Model.Persistence 3.14
/// (<see cref="ModelArchiveFormat.Compact"/>) in base64, the same GPC model in a few kB instead of hundreds. Both are read; a section with a
/// model is written in version 2, a draft (no model) keeps version 1. The documents with a version 2 section have wire version 5
/// (ANTHEA.Application CompactSectionDocumentPersistence), so ANTHEA up to 1.18 refuses them with «Formato o versione del file non supportati.».
/// </summary>
public static class ConcreteSectionArchive
{
    public const string Key = "sezione_gpc";
    private const string Units = "mm-MPa-N";
    /// <summary>Version of the contract with the compact archive (base64).</summary>
    public const int CompactVersion = 2;

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
        return Archived(model, EditorStamp(input, workspace));
    }

    /// <summary>The model of a section in the contract of version 2: the compact archive in base64, its hash and the editor stamp.</summary>
    private static JsonObject Archived(GPC.Model.Models.Model model, string editorStamp)
    {
        using var stream = new MemoryStream();
        ModelArchive.Save(model, stream, ModelArchiveFormat.Compact);
        string archive = Convert.ToBase64String(stream.GetBuffer(), 0, checked((int)stream.Length));
        return J.Obj(("versione", CompactVersion), ("stato", "modello"), ("unita", Units),
            ("editor_sha256", editorStamp), ("archivio_sha256", Hash(archive)), ("archivio", archive));
    }

    /// <summary>True when the section of <paramref name="input"/> is a model in the compact contract (version 2).</summary>
    public static bool IsCompact(JsonObject input)
        => input[Key] is JsonObject saved && J.Number(saved["versione"]) == CompactVersion && saved.S("stato") == "modello";

    /// <summary>
    /// The section of version 1 of <paramref name="input"/> (the XML of ANTHEA up to 1.18) written again in version 2: the GPC model read from
    /// the XML and saved in the compact form, nothing rebuilt from the editor (the same section, its identity and its rebars). Null when there
    /// is nothing to convert: no section, a draft, a section already compact, or a section whose editor parameters changed (Save captures a
    /// new one). A damaged archive is refused as by <see cref="Restore"/>.
    /// </summary>
    public static JsonObject? CompactCopy(JsonObject input, JsonObject workspace)
    {
        if (!input.ContainsKey(Key) || IsCompact(input) || Stored(input, workspace, false) is not { } model) return null;
        return Archived(model, input[Key]!.AsObject().S("editor_sha256"));
    }

    /// <summary>The GPC model of a checked archive of the contract <paramref name="version"/>.</summary>
    private static GPC.Model.Models.Model Load(JsonObject saved, int version)
    {
        string text = saved.S("archivio");
        try
        {
            byte[] bytes = version == CompactVersion ? Convert.FromBase64String(text) : Encoding.UTF8.GetBytes(text);
            using var stream = new MemoryStream(bytes, writable: false);
            return ModelArchive.Load(stream);
        }
        catch (Exception error) when (error is SerializationException or XmlException or InvalidDataException or FormatException or EndOfStreamException)
        {
            throw new ArgumentException("Impossibile leggere la sezione GPC: " + error.Message, error);
        }
    }

    public static JsonObject Draft(JsonObject input, JsonObject workspace, string reason)
        => J.Obj(("versione", 1), ("stato", "bozza"), ("unita", Units),
            ("editor_sha256", EditorStamp(input, workspace)), ("motivo", reason));

    /// <summary>
    /// Returns an independent GPC section. Null means a legacy/draft document or an editor change since the snapshot.
    /// Corrupt, unknown or future contracts are refused, never silently replaced by a reconstruction.
    /// </summary>
    public static ReinforcedConcreteSection? Restore(JsonObject input, JsonObject workspace, bool requireCurrent = false)
        => Stored(input, workspace, requireCurrent) is { } model ? (ReinforcedConcreteSection)model.BeamProperties.Values.Single() : null;

    /// <summary>The checked GPC model of the section of <paramref name="input"/>, as <see cref="Restore"/> (null for a legacy or draft document or
    /// an editor change since the snapshot).</summary>
    private static GPC.Model.Models.Model? Stored(JsonObject input, JsonObject workspace, bool requireCurrent)
    {
        if (!input.ContainsKey(Key)) return null;
        if (input[Key] is not JsonObject saved || J.Number(saved["versione"]) is not (1d or 2d) || saved.S("unita") != Units)
            throw new ArgumentException("Contratto della sezione GPC non supportato.");
        int version = J.Number(saved["versione"]) == CompactVersion ? CompactVersion : 1;
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
        string archive = saved.S("archivio");
        if (archive.Length == 0 || Hash(archive) != saved.S("archivio_sha256"))
            throw new ArgumentException("Archivio della sezione GPC mancante o danneggiato.");
        // A live edit requires a new model; do not deserialize the old one for each keystroke.
        if (!current) return null;
        var model = Load(saved, version);
        if (model.AllElements.Any() || model.BeamProperties.Count != 1 ||
            model.BeamProperties.Values.Single() is not ReinforcedConcreteSection)
            throw new ArgumentException("L’archivio GPC non contiene una sola sezione in cemento armato.");
        return model;
    }
}
