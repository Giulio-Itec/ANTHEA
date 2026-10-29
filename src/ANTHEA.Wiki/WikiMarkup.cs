using System.Text.RegularExpressions;
namespace Anthea.Wiki;

public abstract record WikiBlock
{
    public sealed record Heading(string Id, string Text, int Level) : WikiBlock;
    public sealed record Formula(string Text) : WikiBlock;
    public sealed record Code(string Text) : WikiBlock;
    public sealed record Image(string Caption, string Path, string? Asset) : WikiBlock;
    public sealed record Quote(string Title, string Text) : WikiBlock;
    public sealed record Table(IReadOnlyList<string[]> Rows) : WikiBlock;
    public sealed record Paragraph(string Text) : WikiBlock;
}
public enum WikiInlineKind { Text, Bold, Code, Formula, Link }
public sealed record WikiInline(WikiInlineKind Kind, string Text, string? Target = null, bool Internal = false);

/// <summary>The packaged Wiki dialect. Article title is rendered by the host; parsing starts after its first line.</summary>
public static class WikiMarkup
{
    public static IReadOnlyList<WikiBlock> Parse(string markdown)
    {
        var blocks = new List<WikiBlock>();
        markdown = Regex.Replace(markdown, @"(?ms)^\$\$\s*\r?\n(.*?)^\$\$[ \t]*$", m => "```math\n" + m.Groups[1].Value.Trim() + "\n```");
        markdown = Regex.Replace(markdown, @"\\\((.*?)\\\)", m => "$" + m.Groups[1].Value + "$", RegexOptions.Singleline);
        markdown = Regex.Replace(markdown, @"\\\[(.*?)\\\]", m => "\n```math\n" + m.Groups[1].Value.Trim() + "\n```\n", RegexOptions.Singleline);
        var lines = markdown.Replace("\r", "").Split('\n'); var ids = new HashSet<string>();
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim(); if (line.Length == 0 || line.StartsWith("<!--")) continue;
            if (line.StartsWith("$$ "))
            {
                var formulas = new List<string> { line[3..] };
                while (i + 1 < lines.Length && lines[i + 1].StartsWith("$$ ")) formulas.Add(lines[++i][3..]);
                blocks.Add(new WikiBlock.Formula(string.Join("\n", formulas))); continue;
            }
            if (line.StartsWith("###"))
            {
                var label = line.TrimStart('#', ' '); var id = WikiCatalog.Slug(label); var unique = id; int n = 2;
                while (!ids.Add(unique)) unique = id + "-" + n++;
                blocks.Add(new WikiBlock.Heading(unique, label, line.TakeWhile(c => c == '#').Count())); continue;
            }
            if (line.StartsWith("```"))
            {
                bool formula = line is "```formula" or "```math" or "```latex"; var text = new List<string>();
                while (++i < lines.Length && !lines[i].StartsWith("```")) text.Add(lines[i]);
                blocks.Add(formula ? new WikiBlock.Formula(string.Join("\n", text)) : new WikiBlock.Code(string.Join("\n", text))); continue;
            }
            if (Regex.Match(line, @"^!\[([^\]]*)\]\(([^)]+)\)$") is { Success: true } image)
            {
                blocks.Add(new WikiBlock.Image(image.Groups[1].Value, image.Groups[2].Value, WikiCatalog.AssetFor(image.Groups[2].Value))); continue;
            }
            if (line.StartsWith('>'))
            {
                var texts = new List<string> { line.TrimStart('>', ' ') };
                while (i + 1 < lines.Length && lines[i + 1].TrimStart().StartsWith('>')) texts.Add(lines[++i].TrimStart('>', ' '));
                blocks.Add(new WikiBlock.Quote(texts[0], string.Join("\n", texts.Skip(1)))); continue;
            }
            if (line.StartsWith('|'))
            {
                var rows = new List<string[]>();
                do
                {
                    var cells = lines[i].Trim().Trim('|').Split('|').Select(c => c.Trim()).ToArray();
                    if (!cells.All(c => Regex.IsMatch(c, "^[: -]+$"))) rows.Add(cells);
                    i++;
                } while (i < lines.Length && lines[i].TrimStart().StartsWith('|'));
                i--; if (rows.Count > 0) blocks.Add(new WikiBlock.Table(rows)); continue;
            }
            var paragraph = line;
            if (!Regex.IsMatch(line, @"^[-*]\s|^\d+[.)]\s"))
                while (i + 1 < lines.Length && !string.IsNullOrWhiteSpace(lines[i + 1]) && !Regex.IsMatch(lines[i + 1], @"^(#|>|\||!|```|\$\$ |[-*] |\d+[.)] |<!--)")) paragraph += " " + lines[++i].Trim();
            blocks.Add(new WikiBlock.Paragraph(paragraph));
        }
        return blocks;
    }
    public static IReadOnlyList<WikiInline> Inline(string value)
    {
        var spans = new List<WikiInline>();
        var tokens = Regex.Matches(value, @"\*\*([^*]+)\*\*|`([^`]+)`|\[([^\]]+)\]\(([^)]+)\)|(https?://[^\s)]+)|\$([^$]+)\$"); int position = 0;
        foreach (Match token in tokens)
        {
            spans.Add(new(WikiInlineKind.Text, value[position..token.Index])); position = token.Index + token.Length;
            if (token.Groups[1].Success) spans.Add(new(WikiInlineKind.Bold, token.Groups[1].Value));
            else if (token.Groups[2].Success) spans.Add(new(WikiInlineKind.Code, token.Groups[2].Value));
            else if (token.Groups[6].Success) spans.Add(new(WikiInlineKind.Formula, token.Groups[6].Value));
            else
            {
                var url = token.Groups[5].Success ? token.Groups[5].Value : token.Groups[4].Value;
                var label = token.Groups[5].Success ? url : token.Groups[3].Value;
                bool internalLink = url.StartsWith("/wiki/") || url.StartsWith("wiki:");
                if (internalLink && WikiCatalog.Resolve(url) is { } article) label = article.Title;
                // «guida:Nome.pdf»: a PDF document installed with the program (the article on the layered horizontal pile); only a plain file name.
                if (IsDocumentLink(url)) spans.Add(new(WikiInlineKind.Link, label, url, false));
                else if (!internalLink && !(Uri.TryCreate(url, UriKind.Absolute, out var destination) && destination.Scheme is "https" or "http"))
                    spans.Add(new(WikiInlineKind.Text, label + " (" + url + ")"));
                else spans.Add(new(WikiInlineKind.Link, label, url, internalLink));
            }
        }
        spans.Add(new(WikiInlineKind.Text, value[position..]));
        return spans;
    }

    /// <summary>True for «guida:» followed by a plain PDF file name (no folders, drive or parent references): a document of the installation.</summary>
    public static bool IsDocumentLink(string url)
        => url.StartsWith("guida:", StringComparison.Ordinal) && url[6..] is { Length: > 4 } name && name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
           && name.IndexOfAny(['/', '\\', ':']) < 0 && !name.StartsWith('.') && name.Trim() == name;
}
