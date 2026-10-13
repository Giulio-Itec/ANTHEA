using System.IO.Compression;

namespace Anthea.Reports;

/// <summary>The single package serializer; document generators only prepare Open XML parts.</summary>
public static class DocxWriter
{
    public static byte[] Write(DocxDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!document.Contains("word/document.xml") || !document.Contains("[Content_Types].xml"))
            throw new InvalidDataException("Documento Word incompleto.");
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
            foreach (var (name, bytes) in document.Parts)
            {
                using var stream = zip.CreateEntry(name).Open();
                stream.Write(bytes);
            }
        return memory.ToArray();
    }
    /// <summary>Compatibility adapter for callers supplying an already generated DOCX. Production composition uses DocxDocument directly.</summary>
    public static DocxDocument Read(byte[] bytes)
    {
        using var memory = new MemoryStream(bytes, false);
        using var zip = new ZipArchive(memory, ZipArchiveMode.Read);
        var result = new DocxDocument();
        foreach (var entry in zip.Entries)
        {
            using var input = entry.Open(); using var output = new MemoryStream();
            input.CopyTo(output); result.SetBytes(entry.FullName, output.ToArray());
        }
        return result;
    }
}
