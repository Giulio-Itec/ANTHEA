using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Anthea.Testing.Normalization;

/// <summary>
/// 'normalize &lt;in&gt; &lt;out&gt;': makes a file or a folder of outputs (WPF tests, reports) comparable with 'compare'.
/// DOCX → text per paragraph and cell (.txt); JSON → canonical JSON; CSV, TXT, LOG, MD, TSV → normalised text;
/// PNG and BMP → dimensions only (.info.json, the pixels depend on the machine). Other files are listed in skipped.txt.
/// </summary>
public static class Normalizer
{
    static readonly string[] TextExtensions = [".txt", ".csv", ".tsv", ".log", ".md", ".xml"];

    public static int Run(string input, string output, TextNormalizer text)
    {
        input = Path.GetFullPath(input); output = Path.GetFullPath(output);
        if (System.IO.File.Exists(input))
        {
            string target = Directory.Exists(output) || output.EndsWith('\\') || output.EndsWith('/') ? Path.Combine(output, Target(Path.GetFileName(input))) : output;
            return NormalizeFile(input, target, text) ? 0 : 1;
        }
        if (!Directory.Exists(input)) { Console.Error.WriteLine("Ingresso non trovato: " + input); return 2; }
        int done = 0; var skipped = new List<string>();
        foreach (var file in Directory.EnumerateFiles(input, "*", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
        {
            string relative = Path.GetRelativePath(input, file);
            if (NormalizeFile(file, Path.Combine(output, Path.GetDirectoryName(relative) ?? "", Target(Path.GetFileName(relative))), text)) done++;
            else skipped.Add(relative.Replace('\\', '/'));
        }
        Directory.CreateDirectory(output);
        if (skipped.Count > 0) System.IO.File.WriteAllText(Path.Combine(output, "skipped.txt"), string.Join("\n", skipped) + "\n", new UTF8Encoding(false));
        Console.WriteLine($"{done} file normalizzati in {output}; {skipped.Count} non supportati.");
        return 0;
    }

    static string Target(string name)
    {
        string extension = Path.GetExtension(name).ToLowerInvariant();
        return extension switch
        {
            ".docx" => Path.ChangeExtension(name, ".docx.txt"),
            ".png" or ".bmp" => name + ".info.json",
            _ => name
        };
    }

    static bool NormalizeFile(string source, string target, TextNormalizer text)
    {
        string extension = Path.GetExtension(source).ToLowerInvariant();
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var guids = new GuidMap();
        switch (extension)
        {
            case ".docx":
                System.IO.File.WriteAllText(target, DocxText.Extract(System.IO.File.ReadAllBytes(source), text, guids), new UTF8Encoding(false));
                return true;
            case ".json":
                JsonNode? node;
                try { node = JsonNode.Parse(System.IO.File.ReadAllText(source, Encoding.UTF8), documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip }); }
                catch (JsonException) { System.IO.File.WriteAllText(target, NormalizeLines(System.IO.File.ReadAllText(source, Encoding.UTF8), text, guids), new UTF8Encoding(false)); return true; }
                CanonicalJson.Register(node, guids);
                CanonicalJson.WriteFile(target, node, guids, text);
                return true;
            case ".png" or ".bmp":
                var bytes = System.IO.File.ReadAllBytes(source);
                var info = new JsonObject { ["formato"] = extension.TrimStart('.') };
                if (extension == ".png" && bytes.Length >= 24) { info["larghezza"] = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)); info["altezza"] = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)); }
                if (extension == ".bmp" && bytes.Length >= 26) { info["larghezza"] = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(18, 4)); info["altezza"] = Math.Abs(BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(22, 4))); }
                CanonicalJson.WriteFile(target, info, guids, text);
                return true;
            default:
                if (!TextExtensions.Contains(extension)) return false;
                System.IO.File.WriteAllText(target, NormalizeLines(System.IO.File.ReadAllText(source, Encoding.UTF8), text, guids), new UTF8Encoding(false));
                return true;
        }
    }

    /// <summary>Normalised text with LF line ends and without the BOM.</summary>
    public static string NormalizeLines(string content, TextNormalizer text, GuidMap guids)
    {
        var lines = content.TrimStart('\uFEFF').Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new StringBuilder();
        for (int i = 0; i < lines.Length; i++)
        {
            if (i == lines.Length - 1 && lines[i].Length == 0) break;
            output.Append(text.Normalize(lines[i], guids)).Append('\n');
        }
        return output.ToString();
    }
}
