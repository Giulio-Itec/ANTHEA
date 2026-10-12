using System.Text;
using System.Xml.Linq;

namespace Anthea.Reports;

/// <summary>In-memory Open XML parts. Composers share these parts without compressing and reopening intermediate files.</summary>
public sealed class DocxDocument
{
    private readonly Dictionary<string, byte[]> parts = new(StringComparer.Ordinal);
    internal IEnumerable<KeyValuePair<string, byte[]>> Parts => parts;
    public void SetXml(string name, string xml) => SetBytes(name, Encoding.UTF8.GetBytes(xml));
    public void SetBytes(string name, byte[] bytes)
    {
        if (string.IsNullOrWhiteSpace(name) || name.StartsWith('/') || name.Contains('\\') || name.Split('/').Any(p => p is ".." or "." or ""))
            throw new ArgumentException("Nome di parte Open XML non valido.", nameof(name));
        parts[name] = bytes.ToArray();
    }
    public bool Contains(string name) => parts.ContainsKey(name);
    public byte[] GetBytes(string name) => parts.TryGetValue(name, out var bytes) ? bytes.ToArray()
        : throw new InvalidDataException("Parte del documento assente: " + name);
    public XDocument GetXml(string name)
    {
        using var stream = new MemoryStream(GetBytes(name), false);
        return XDocument.Load(stream);
    }
}
