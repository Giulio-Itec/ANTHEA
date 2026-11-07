using System.Buffers.Binary;
using System.Text;

namespace Anthea.Reports.Pdf;

/// <summary>Reads family and face names from installed OpenType fonts, including per-user fonts.</summary>
internal static class InstalledFonts
{
    private const int HeaderBytes = 12, TableBytes = 16, TagBytes = 4, TableOffset = 8, TableLength = 12,
        NameHeaderBytes = 6, NameRecordBytes = 12, LanguageOffset = 4, NameIdOffset = 6, NameLengthOffset = 8, NameStringOffset = 10,
        MaximumNameBytes = 1_048_576;
    internal sealed record Face(string Path, bool Bold, bool Italic, int Weight);
    private static readonly Lazy<Dictionary<string, List<Face>>> Catalog = new(Read);
    internal static bool Contains(string name) => Catalog.Value.ContainsKey(name);
    internal static Face Find(string name, bool bold, bool italic) => Catalog.Value[name]
        .OrderBy(f => Math.Abs(f.Weight - (bold ? 700 : 400)) + (f.Italic == italic ? 0 : 2000))
        .ThenBy(f => f.Path.Contains("Variable", StringComparison.OrdinalIgnoreCase)).First();

    private static Dictionary<string, List<Face>> Read()
    {
        var result = new Dictionary<string, List<Face>>(StringComparer.OrdinalIgnoreCase);
        foreach (string folder in new[] { Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts") })
        {
            if (!Directory.Exists(folder)) continue;
            foreach (string path in Directory.EnumerateFiles(folder).Where(p => Path.GetExtension(p).ToLowerInvariant() is ".ttf" or ".otf"))
            {
                try
                {
                    using var stream = File.OpenRead(path);
                    var header = new byte[HeaderBytes]; stream.ReadExactly(header);
                    int count = U16(header, TagBytes);
                    if (count > 256) continue;
                    var tables = new byte[count * TableBytes]; stream.ReadExactly(tables);
                    for (int t = 0; t < tables.Length; t += TableBytes)
                    {
                        if (Encoding.ASCII.GetString(tables, t, TagBytes) != "name") continue;
                        uint offset = BinaryPrimitives.ReadUInt32BigEndian(tables.AsSpan(t + TableOffset, sizeof(uint)));
                        uint length = BinaryPrimitives.ReadUInt32BigEndian(tables.AsSpan(t + TableLength, sizeof(uint)));
                        if (length > MaximumNameBytes || length < NameHeaderBytes || offset + (long)length > stream.Length) continue;
                        stream.Position = offset; var data = new byte[length]; stream.ReadExactly(data);
                        int records = U16(data, sizeof(ushort)), start = U16(data, TagBytes);
                        var names = new Dictionary<int, string>();
                        for (int n = 0; n < records && NameHeaderBytes + (n + 1) * NameRecordBytes <= data.Length; n++)
                        {
                            int at = NameHeaderBytes + n * NameRecordBytes, platform = U16(data, at), language = U16(data, at + LanguageOffset), id = U16(data, at + NameIdOffset);
                            if (platform is not (0 or 3) || id is not (1 or 2 or 4 or 16 or 17)) continue;
                            int bytes = U16(data, at + NameLengthOffset), pos = start + U16(data, at + NameStringOffset);
                            if (pos + bytes > data.Length) continue;
                            if (!names.ContainsKey(id) || language == 0x409) names[id] = Encoding.BigEndianUnicode.GetString(data, pos, bytes);
                        }
                        string subfamily = names.GetValueOrDefault(17, names.GetValueOrDefault(2, ""));
                        bool bold = subfamily.Contains("Bold", StringComparison.OrdinalIgnoreCase), italic = subfamily.Contains("Italic", StringComparison.OrdinalIgnoreCase) || subfamily.Contains("Oblique", StringComparison.OrdinalIgnoreCase);
                        int weight = subfamily.ToLowerInvariant() switch
                        {
                            var s when s.Contains("extrabold") || s.Contains("ultrabold") => 800,
                            var s when s.Contains("semibold") || s.Contains("demibold") => 600,
                            var s when s.Contains("bold") => 700,
                            var s when s.Contains("black") || s.Contains("heavy") => 900,
                            var s when s.Contains("extralight") || s.Contains("ultralight") => 200,
                            var s when s.Contains("light") => 300,
                            var s when s.Contains("thin") => 100,
                            var s when s.Contains("medium") => 500,
                            _ => 400
                        };
                        foreach (int id in new[] { 1, 4, 16 })
                            if (names.TryGetValue(id, out string? name) && name.Length > 0)
                            {
                                if (!result.TryGetValue(name, out var faces)) result[name] = faces = [];
                                faces.Add(new(path, bold, italic, weight));
                            }
                    }
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException) { /* Ignore unreadable individual fonts. */ }
            }
        }
        return result;
    }
    private static int U16(byte[] data, int offset) => BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset, 2));
}
