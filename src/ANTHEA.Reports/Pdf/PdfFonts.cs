using System.Collections.Concurrent;
using PdfSharp.Fonts;

namespace Anthea.Reports.Pdf;

/// <summary>
/// The single font resolver of the process for every PDF (PDFsharp accepts one resolver, fixed after the first use): the Windows fonts
/// of the report families, the alias of the model reports and the glyph coverage used to move a character missing from a font to a
/// fallback font (PDFsharp has no glyph fallback of its own).
/// </summary>
public static class PdfFonts
{
    /// <summary>Family name used by the model reports, resolved to Segoe UI.</summary>
    public const string ModelReportAlias = "Anthea Report";
    /// <summary>Family of the runs and styles without a font, as Word shows a document without font settings.</summary>
    public const string DefaultFamily = "Times New Roman";

    // Regular, bold, italic, bold italic; a family with one file simulates the missing faces
    static readonly Dictionary<string, string[]> Families = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Calibri"] = ["calibri.ttf", "calibrib.ttf", "calibrii.ttf", "calibriz.ttf"],
        ["Arial"] = ["arial.ttf", "arialbd.ttf", "ariali.ttf", "arialbi.ttf"],
        ["Times New Roman"] = ["times.ttf", "timesbd.ttf", "timesi.ttf", "timesbi.ttf"],
        ["Segoe UI"] = ["segoeui.ttf", "segoeuib.ttf", "segoeuii.ttf", "segoeuiz.ttf"],
        [ModelReportAlias] = ["segoeui.ttf", "segoeuib.ttf", "segoeuii.ttf", "segoeuiz.ttf"],
        ["Segoe UI Symbol"] = ["seguisym.ttf"],
    };
    /// <summary>Fonts tried, in order, for a character missing from the font of the run.</summary>
    public static readonly IReadOnlyList<string> Fallbacks = ["Segoe UI", "Segoe UI Symbol"];

    static readonly object Gate = new();
    static readonly ConcurrentDictionary<string, byte[]> Files = new(StringComparer.OrdinalIgnoreCase);
    static readonly ConcurrentDictionary<string, CharacterMap> Maps = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Installs the resolver once; another resolver already installed is an incompatible configuration.</summary>
    public static void Ensure()
    {
        lock (Gate)
        {
            if (GlobalFontSettings.FontResolver is Resolver) return;
            if (GlobalFontSettings.FontResolver is not null) throw new InvalidOperationException("Configurazione dei caratteri PDF incompatibile.");
            GlobalFontSettings.FontResolver = new Resolver();
        }
    }

    /// <summary>Known family, otherwise the default family (the PDF never fails on an unknown font name).</summary>
    public static string Family(string? name) => name is not null && Families.ContainsKey(name) ? name : DefaultFamily;

    /// <summary>Text split into runs of characters that each font can draw: the requested family first, then the fallbacks.</summary>
    public static IEnumerable<(string Text, string Family)> Segments(string text, string family, bool bold)
    {
        family = Family(family);
        var current = new System.Text.StringBuilder(); string? currentFamily = null;
        for (int i = 0; i < text.Length; i++)
        {
            int code = char.IsSurrogatePair(text, i) ? char.ConvertToUtf32(text, i) : text[i];
            string chosen = Covers(family, bold, code) ? family : Fallbacks.FirstOrDefault(f => Covers(f, bold, code)) ?? family;
            if (chosen != currentFamily && current.Length > 0) { yield return (current.ToString(), currentFamily!); current.Clear(); }
            currentFamily = chosen; current.Append(char.ConvertFromUtf32(code));
            if (code > char.MaxValue) i++;
        }
        if (current.Length > 0) yield return (current.ToString(), currentFamily!);
    }

    /// <summary>True when the face of the family maps the code point to a glyph (cmap format 4 or 12). Controls count as covered.</summary>
    public static bool Covers(string family, bool bold, int codePoint)
    {
        if (codePoint < ' ' || codePoint is ' ' or '­') return true;
        string file = Face(Family(family), bold, false).File;
        return Maps.GetOrAdd(file, f => CharacterMap.Read(Load(f))).Contains(codePoint);
    }

    static (string File, bool SimulateBold, bool SimulateItalic) Face(string family, bool bold, bool italic)
    {
        var files = Families[family];
        int index = (bold ? 1 : 0) + (italic ? 2 : 0);
        return index < files.Length ? (files[index], false, false) : (files[0], bold, italic);
    }

    static byte[] Load(string file) => Files.GetOrAdd(file, name =>
    {
        foreach (string folder in new[] { Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts") })
        {
            string path = Path.Combine(folder, name);
            if (File.Exists(path)) return File.ReadAllBytes(path);
        }
        throw new InvalidOperationException("Carattere non installato per il PDF: " + name);
    });

    sealed class Resolver : IFontResolver
    {
        public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            var (file, simulateBold, simulateItalic) = Face(Family(familyName), isBold, isItalic);
            return new FontResolverInfo(file, simulateBold, simulateItalic);
        }
        public byte[]? GetFont(string faceName) => Load(faceName);
    }

    /// <summary>Character map of a TrueType or OpenType font: the Windows Unicode subtable, format 12 when present, otherwise format 4.</summary>
    sealed class CharacterMap
    {
        readonly List<(int First, int Last, int Glyph, bool Linear)> ranges = [];
        readonly Func<int, bool>? format4;
        public bool Contains(int code) => format4?.Invoke(code) ?? ranges.Any(r => code >= r.First && code <= r.Last);

        CharacterMap(Func<int, bool>? lookup) => format4 = lookup;

        // Byte offsets of the OpenType table directory and of the cmap subtables (OpenType specification, tables "cmap")
        const int TableCountAt = 4, TableRecordsAt = 12, TableRecordSize = 16, TableOffsetField = 8;
        const int SubtableCountAt = 2, SubtableRecordsAt = 4, SubtableRecordSize = 8, SubtableOffsetField = 4, EncodingField = 2;
        const int WindowsPlatform = 3, SymbolEncoding = 0, BmpEncoding = 1, FullEncoding = 10, SegmentFormat = 4, GroupFormat = 12;
        const int GroupCountAt = 12, GroupsAt = 16, GroupSize = 12, GroupLastField = 4, GroupGlyphField = 8;
        const int SegmentCountAt = 6, EndCodesAt = 14, ByteBits = 8, WordBits = 16, TagLength = 4;

        public static CharacterMap Read(byte[] font)
        {
            int U16(int at) => font[at] << ByteBits | font[at + 1];
            long U32(int at) => (long)U16(at) << WordBits | (long)U16(at + 2);
            int tables = U16(TableCountAt), cmap = -1;
            for (int i = 0; i < tables; i++)
            {
                int record = TableRecordsAt + TableRecordSize * i;
                if (System.Text.Encoding.ASCII.GetString(font, record, TagLength) == "cmap") { cmap = (int)U32(record + TableOffsetField); break; }
            }
            if (cmap < 0) throw new InvalidDataException("Carattere senza tabella cmap.");
            int format12 = -1, format4Offset = -1, subtables = U16(cmap + SubtableCountAt);
            for (int i = 0; i < subtables; i++)
            {
                int record = cmap + SubtableRecordsAt + SubtableRecordSize * i, platform = U16(record), encoding = U16(record + EncodingField);
                int offset = cmap + (int)U32(record + SubtableOffsetField);
                if (platform == WindowsPlatform && encoding == FullEncoding && U16(offset) == GroupFormat) format12 = offset;
                if (platform == WindowsPlatform && encoding is BmpEncoding or SymbolEncoding && U16(offset) == SegmentFormat) format4Offset = offset;
            }
            if (format12 >= 0)
            {
                var map = new CharacterMap(null); long groups = U32(format12 + GroupCountAt);
                for (int g = 0; g < groups; g++)
                {
                    int at = format12 + GroupsAt + GroupSize * g;
                    map.ranges.Add(((int)U32(at), (int)U32(at + GroupLastField), (int)U32(at + GroupGlyphField), true));
                }
                return map;
            }
            if (format4Offset < 0) throw new InvalidDataException("Carattere senza mappa Unicode.");
            int t = format4Offset, segments = U16(t + SegmentCountAt) / 2, ends = t + EndCodesAt, starts = ends + 2 * segments + 2, deltas = starts + 2 * segments, offsets = deltas + 2 * segments;
            return new CharacterMap(code =>
            {
                if (code > char.MaxValue) return false;
                for (int s = 0; s < segments; s++)
                {
                    int end = U16(ends + 2 * s); if (code > end) continue;
                    int start = U16(starts + 2 * s); if (code < start) return false;
                    int delta = U16(deltas + 2 * s), rangeOffset = U16(offsets + 2 * s);
                    if (rangeOffset == 0) return (code + delta & ushort.MaxValue) != 0;
                    int glyphAt = offsets + 2 * s + rangeOffset + 2 * (code - start);
                    int glyph = glyphAt + 1 < font.Length ? U16(glyphAt) : 0;
                    return glyph != 0;
                }
                return false;
            });
        }
    }
}
