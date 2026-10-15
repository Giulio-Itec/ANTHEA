using System.Net;
using System.Text;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

namespace ANTHEA.ModelReports;

/// <summary>Document composition and file publication. No UI dependencies or structural calculations.</summary>
public static class ModelReportExporter
{
    static readonly object FontLock = new();
    public static void ConfigureFonts(byte[] regular, byte[] bold)
    {
        lock (FontLock)
        {
            if (GlobalFontSettings.FontResolver is ReportFonts) return;
            // The application installs the shared resolver of the report PDFs at start-up: it resolves the "Anthea Report" family too
            if (GlobalFontSettings.FontResolver is { } shared)
            {
                if (shared.ResolveTypeface("Anthea Report", false, false) is null) throw new InvalidOperationException("Configurazione dei caratteri PDF incompatibile.");
                return;
            }
            GlobalFontSettings.FontResolver = new ReportFonts(regular, bold);
        }
    }
    sealed class ReportFonts(byte[] regular, byte[] bold) : IFontResolver
    {
        public byte[] GetFont(string faceName) => faceName == "AntheaBold" ? bold : regular;
        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) => new(isBold ? "AntheaBold" : "AntheaRegular", false, isItalic);
    }

    public static async Task<ReportExportResult> ExportAsync(ModelReportDocument recipe, string revision, string target,
        Func<ModelReportBlock, string, CancellationToken, Task<ReportFigure>> capture,
        IProgress<ReportProgress>? progress, CancellationToken cancellationToken)
    {
        ModelReportFiles.Validate(recipe);
        if(revision.Length!=64 || revision.Any(c=>!Uri.IsHexDigit(c)))throw new InvalidDataException("Revisione del report non valida.");
        string parent = Path.GetDirectoryName(Path.GetFullPath(target))!;
        string name = Path.GetFileNameWithoutExtension(target);
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidDataException("Nome del report mancante.");
        string token = Guid.NewGuid().ToString("N")[..8];
        string temporary = Path.Combine(parent, "." + name + "-" + token + ".pending");
        string destination = Path.Combine(parent, name + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + token);
        Directory.CreateDirectory(temporary);
        bool published = false;
        try
        {
            var blocks = recipe.Blocks.Where(b => b.Included).ToArray();
            if (blocks.Length == 0) throw new InvalidDataException("Seleziona almeno un capitolo da includere.");
            var figures = new Dictionary<Guid, ReportFigure>();
            int completed = 0;
            foreach (var block in blocks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new(completed, blocks.Length + 1, "Preparazione: " + block.Title));
                if (block.Kind == ReportBlockKind.Figure)
                {
                    string path = Path.Combine(temporary, "figura-" + block.Id.ToString("N") + ".png");
                    figures[block.Id] = await capture(block, path, cancellationToken);
                }
                completed++;
            }
            var current = recipe with { SourceRevision = revision };
            DateTimeOffset created = DateTimeOffset.Now;
            progress?.Report(new(completed, blocks.Length + 1, "Impaginazione PDF e sorgente modificabile…"));
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                WritePdf(current, figures, created, Path.Combine(temporary, name + ".pdf"));
                File.WriteAllText(Path.Combine(temporary, name + ".html"), Html(current, figures, created), Encoding.UTF8);
                ModelReportFiles.Write(Path.Combine(temporary, name + ".antheareport"), current);
                cancellationToken.ThrowIfCancellationRequested();
            }, cancellationToken);
            // Publication is a single rename of a unique directory; no partially updated report is exposed.
            Directory.Move(temporary, destination); published = true;
            progress?.Report(new(blocks.Length + 1, blocks.Length + 1, "Report pronto"));
            return new(Path.Combine(destination, name + ".pdf"), Path.Combine(destination, name + ".antheareport"),
                Path.Combine(destination, name + ".html"), revision, figures.Count);
        }
        finally
        {
            // Only files in the directory created by this invocation; never follow folders or delete recursively.
            if (!published && Directory.Exists(temporary))
            {
                foreach (string path in Directory.EnumerateFiles(temporary)) File.Delete(path);
                Directory.Delete(temporary);
            }
        }
    }

    static string Pending(ModelReportBlock b) => "SEZIONE PREDISPOSTA - RISULTATI NON COLLEGATI\n" +
        (string.IsNullOrWhiteSpace(b.SheetName) ? "Nessun foglio di verifica associato." : "Foglio: " + b.SheetName) +
        "\nQuesto capitolo non contiene esiti di verifica. Il collegamento ai risultati delle librerie GPC sarà implementato successivamente.";

    static void WritePdf(ModelReportDocument recipe, IReadOnlyDictionary<Guid, ReportFigure> figures, DateTimeOffset created, string path)
    {
        var doc = new Document(); doc.Info.Title = recipe.Title; doc.Info.Author = recipe.Author;
        var normal = doc.Styles[StyleNames.Normal]!; normal.Font.Name = "Anthea Report"; normal.Font.Size = 10;
        normal.Font.Color = Color.FromRgb(36, 53, 74); normal.ParagraphFormat.SpaceAfter = Unit.FromMillimeter(3);
        var heading = doc.Styles[StyleNames.Heading1]!; heading.Font.Size = 21; heading.Font.Bold = true;
        heading.ParagraphFormat.SpaceAfter = Unit.FromMillimeter(8);
        var section = doc.AddSection(); section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.LeftMargin = section.PageSetup.RightMargin = Unit.FromMillimeter(20);
        section.PageSetup.TopMargin = Unit.FromMillimeter(23); section.PageSetup.BottomMargin = Unit.FromMillimeter(20);
        section.Headers.Primary.AddParagraph("ANTHEA  /  MODELLI").Format.Font.Color = Color.FromRgb(56, 121, 185);
        var footer = section.Footers.Primary.AddParagraph(); footer.Format.Font.Size = 8;
        footer.AddText("Revisione " + recipe.SourceRevision[..12] + "  ·  "); footer.AddPageField(); footer.AddText(" / "); footer.AddNumPagesField();
        section.AddParagraph(recipe.Title, StyleNames.Heading1);
        section.AddParagraph(recipe.ModelName);
        section.AddParagraph(created.ToString("dd/MM/yyyy HH:mm") + (recipe.Author.Length == 0 ? "" : "  ·  " + recipe.Author));
        if (recipe.Blocks.Any(b => b.Included && b.Kind == ReportBlockKind.Verification))
            section.AddParagraph("BOZZA - contiene sezioni di verifica ancora da collegare.");
        section.AddParagraph("Documento generato dalla copia importata. Le figure conservano campo, unità, visibilità e impostazioni della ricetta. I testi sono contenuti dell’autore.");
        section.AddParagraph("Contenuti", StyleNames.Heading1);
        int index = 0;
        foreach (var block in recipe.Blocks.Where(b => b.Included)) section.AddParagraph($"{++index:00}   {block.Title}");
        index = 0;
        foreach (var b in recipe.Blocks.Where(b => b.Included))
        {
            section.AddPageBreak(); section.AddParagraph($"{++index:00}  {b.Title}", StyleNames.Heading1);
            if (b.Kind == ReportBlockKind.Figure)
            {
                var f = figures[b.Id];
                var image = section.AddImage(f.PngPath); image.LockAspectRatio = true; var png=File.ReadAllBytes(f.PngPath);
                double ratio=(double)System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20,4))/System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16,4));
                image.Width = Unit.FromMillimeter(Math.Min(170,170/ratio));
                section.AddParagraph(f.Description).Format.Font.Size = 9;
                if (f.Categories is { Count: >0 } categories)
                {
                    var table = section.AddTable(); table.AddColumn(Unit.FromMillimeter(85)); table.AddColumn(Unit.FromMillimeter(85));
                    for (int i=0; i<categories.Count; i++)
                    {
                        var row = i%2==0 ? table.AddRow() : table.Rows[table.Rows.Count-1];
                        var category=categories[i]; var p=row.Cells[i%2].AddParagraph(); p.Format.Font.Size=8;
                        var swatch=p.AddFormattedText("■  ");
                        int rgb=int.Parse(category.Color[1..],System.Globalization.NumberStyles.HexNumber);
                        swatch.Font.Color=Color.FromRgb((byte)(rgb>>16),(byte)(rgb>>8),(byte)rgb);
                        p.AddText(category.Label+" ("+category.Count+")");
                    }
                }
            }
            if (b.Kind == ReportBlockKind.Verification) section.AddParagraph(Pending(b));
            foreach (string paragraph in b.Text.Replace("\r", "").Split('\n')) section.AddParagraph(paragraph.Length == 0 ? " " : paragraph);
        }
        var renderer = new PdfDocumentRenderer { Document = doc }; renderer.RenderDocument(); renderer.PdfDocument.Save(path);
    }

    static string Html(ModelReportDocument recipe, IReadOnlyDictionary<Guid, ReportFigure> figures, DateTimeOffset created)
    {
        static string E(string value) => WebUtility.HtmlEncode(value);
        var html = new StringBuilder("<!doctype html><html lang='it'><meta charset='utf-8'><title>").Append(E(recipe.Title))
            .Append("</title><style>body{font:16px/1.55 'Segoe UI',sans-serif;color:#24354a;background:#eef2f6;margin:0}main{max-width:900px;margin:auto}section{background:white;padding:48px;margin:24px 0;break-before:page}h1{line-height:1.25}img{width:100%}p{white-space:pre-wrap;overflow-wrap:anywhere}small{color:#53667a}@media print{body{background:white}section{margin:0;padding:0}section:first-child{break-before:auto}@page{size:A4;margin:20mm}}</style><main><section><small>ANTHEA / MODELLI</small><h1>")
            .Append(E(recipe.Title)).Append("</h1><p>").Append(E(recipe.ModelName)).Append("</p><p>")
            .Append(E(recipe.Author)).Append("</p><small>").Append(E(created.ToString("dd/MM/yyyy HH:mm") + " · revisione " + recipe.SourceRevision[..12])).Append("</small></section>");
        int index = 0;
        foreach (var b in recipe.Blocks.Where(b => b.Included))
        {
            html.Append("<section><h1>").Append(++index).Append(". ").Append(E(b.Title)).Append("</h1>");
            if (b.Kind == ReportBlockKind.Figure)
            {
                var f = figures[b.Id]; html.Append("<img alt='").Append(E(b.Title)).Append("' src='data:image/png;base64,")
                    .Append(Convert.ToBase64String(File.ReadAllBytes(f.PngPath))).Append("'><p>").Append(E(f.Description)).Append("</p>");
                foreach(var category in f.Categories ?? [])
                    html.Append("<p><span style='color:").Append(E(category.Color)).Append("'>■</span> ").Append(E(category.Label)).Append(" (").Append(category.Count).Append(")</p>");
            }
            if (b.Kind == ReportBlockKind.Verification) html.Append("<p>").Append(E(Pending(b))).Append("</p>");
            html.Append("<p>").Append(E(b.Text)).Append("</p></section>");
        }
        return html.Append("</main></html>").ToString();
    }
}
