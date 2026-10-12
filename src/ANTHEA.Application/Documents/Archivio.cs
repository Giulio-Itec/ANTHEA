using System.Text.Json.Nodes;
using Anthea.Application.Documents;

namespace X.Core;

/// <summary>Compatibility facade. All document handling lives in Application.DocumentService.</summary>
public static class Archivio
{
    public static readonly string[] Moduli = DocumentService.ModuleIds;
    public static JsonObject Leggi(string path) => DocumentService.Open(path);
    public static void Valida(JsonObject document) => DocumentService.Validate(document);
    public static void Scrivi(string path, JsonObject document) => DocumentService.Save(path, document);
    public static void ScriviAtomico(string path, byte[] bytes) => Anthea.Reports.DocumentFile.Write(path, bytes);
    public static JsonObject NuovoFoglio(string module) => DocumentService.CreateSheet(module);
    public static JsonObject NuovoStratoMicropalo() => DocumentService.CreateMicropileLayer();
    public static JsonObject NuovoStratoPalo() => DocumentService.CreatePileLayer();
    public static JsonObject Documento(string module) => DocumentService.CreateDocument(module);
}
