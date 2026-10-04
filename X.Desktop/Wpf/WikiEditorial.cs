using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using WpfMath.Controls;

namespace X.Desktop;

/// <summary>Small, safe Markdown dialect: no HTML/script execution; shared editorial blocks.</summary>
internal static class WikiEditorial
{
    // Equivalent spacing supported by WPF-Math; preserve the original LaTeX for copying.
    private static string RenderLatex(string value) => value.Replace(@"\ ", @"\,").Replace(@"\qquad", @"\;\;\;\;\;\;").Replace(@"\quad", @"\;\;\;");
    internal static void Render(StackPanel pane, string markdown, Action<string> navigate,
        Func<string, string, int, FrameworkElement> heading)
    {
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
                pane.Children.Add(Formula(string.Join("\n", formulas))); continue;
            }
            if (line.StartsWith("###"))
            {
                var label = line.TrimStart('#', ' '); var id = WikiCatalog.Slug(label); var unique = id; int n = 2;
                while (!ids.Add(unique)) unique = id + "-" + n++;
                pane.Children.Add(heading(unique, label, line.TakeWhile(c => c == '#').Count())); continue;
            }
            if (line.StartsWith("```"))
            {
                bool formula = line is "```formula" or "```math" or "```latex"; var text = new List<string>();
                while (++i < lines.Length && !lines[i].StartsWith("```")) text.Add(lines[i]);
                pane.Children.Add(formula ? Formula(string.Join("\n", text)) : Note("DATI E PROCEDURA", string.Join("\n", text), Ui.Bg)); continue;
            }
            if (Regex.Match(line, @"^!\[([^\]]*)\]\(([^)]+)\)$") is { Success: true } image)
            {
                // Packaged assets only. Documentation paths resolve to a known asset name.
                var name = WikiCatalog.AssetFor(image.Groups[2].Value);
                if (name is null) { pane.Children.Add(Note("FIGURA", image.Groups[1].Value, Ui.Bg)); continue; }
                FrameworkElement graphic = name.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
                    ? new WikiVector(name)
                    : new Image { Source = Ui.Asset("Wiki/" + name), MaxHeight = 320, Stretch = Stretch.Uniform };
                System.Windows.Automation.AutomationProperties.SetName(graphic, image.Groups[1].Value);
                pane.Children.Add(Figure(graphic, image.Groups[1].Value)); continue;
            }
            if (line.StartsWith('>'))
            {
                var texts = new List<string> { line.TrimStart('>', ' ') };
                while (i + 1 < lines.Length && lines[i + 1].TrimStart().StartsWith('>')) texts.Add(lines[++i].TrimStart('>', ' '));
                var first = texts[0]; var warning = first.Contains("ERRORE") || first.Contains("ATTENZIONE");
                var note = Note(first, "", WikiPalette.Surface);
                ((StackPanel)note.Child).Children.Add(Text(string.Join("\n", texts.Skip(1)), navigate));
                pane.Children.Add(note); continue;
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
                i--; if (rows.Count > 0) pane.Children.Add(Comparison(rows, navigate)); continue;
            }
            var paragraph = line;
            if (!Regex.IsMatch(line, @"^[-*]\s|^\d+[.)]\s"))
                while (i + 1 < lines.Length && !string.IsNullOrWhiteSpace(lines[i + 1]) && !Regex.IsMatch(lines[i + 1], @"^(#|>|\||!|```|\$\$ |[-*] |\d+[.)] |<!--)")) paragraph += " " + lines[++i].Trim();
            pane.Children.Add(Text(paragraph, navigate));
        }
    }
    private static TextBlock Text(string value, Action<string> navigate)
    {
        var text = Ui.Text("", 15.5, color: WikiPalette.Ink); text.LineHeight = 26; text.Margin = new Thickness(0, 5, 0, 12);
        var tokens = Regex.Matches(value, @"\*\*([^*]+)\*\*|`([^`]+)`|\[([^\]]+)\]\(([^)]+)\)|(https?://[^\s)]+)|\$([^$]+)\$"); int position = 0;
        foreach (Match token in tokens)
        {
            text.Inlines.Add(new Run(value[position..token.Index])); position = token.Index + token.Length;
            if (token.Groups[1].Success) text.Inlines.Add(new Bold(new Run(token.Groups[1].Value)));
            else if (token.Groups[2].Success) text.Inlines.Add(new Run(token.Groups[2].Value) { FontFamily = new FontFamily("Consolas"), Background = Ui.Bg });
            else if (token.Groups[6].Success)
            {
                var math = new FormulaControl { Formula = RenderLatex(token.Groups[6].Value), Scale = 16, Foreground = text.Foreground };
                if (math.HasError) throw new InvalidOperationException("Formula inline LaTeX non valida: " + token.Groups[6].Value);
                System.Windows.Automation.AutomationProperties.SetName(math, token.Groups[6].Value);
                text.Inlines.Add(new InlineUIContainer(math) { BaselineAlignment = BaselineAlignment.Center });
            }
            else
            {
                var url = token.Groups[5].Success ? token.Groups[5].Value : token.Groups[4].Value;
                var label = token.Groups[5].Success ? url : token.Groups[3].Value;
                bool internalLink = url.StartsWith("/wiki/") || url.StartsWith("wiki:");
                if (internalLink && WikiCatalog.Resolve(url) is { } article) label = article.Title;
                if (!internalLink && !(Uri.TryCreate(url, UriKind.Absolute, out var destination) && destination.Scheme is "https" or "http"))
                { text.Inlines.Add(new Run(label + " (" + url + ")")); continue; }
                var link = new Hyperlink(new Run(label)) { Foreground = WikiPalette.Accent };
                link.Click += (_, _) =>
                {
                    if (internalLink) navigate(url);
                    else if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http")
                        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
                };
                text.Inlines.Add(link);
            }
        }
        text.Inlines.Add(new Run(value[position..]));
        var menu = new ContextMenu(); var copy = new MenuItem { Header = "Copia testo" }; copy.Click += (_, _) => Clipboard.SetText(value); menu.Items.Add(copy); text.ContextMenu = menu;
        return text;
    }
    internal static Border Formula(string equation)
    {
        var formulas = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        foreach (var line in equation.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var math = new FormulaControl { Formula = RenderLatex(line.Trim()), Scale = 23, Foreground = WikiPalette.Ink,
                SystemTextFontName = "Segoe UI", Margin = new Thickness(8, 9, 8, 9) };
            System.Windows.Automation.AutomationProperties.SetName(math, line.Trim());
            if (math.HasError) throw new InvalidOperationException("Formula LaTeX non valida: " + line + " · " + string.Join("; ", math.Errors.Select(e => e.Message)));
            formulas.Children.Add(math);
        }
        var copy = WikiView.Link("Copia LaTeX", () => Clipboard.SetText(equation));
        copy.HorizontalAlignment = HorizontalAlignment.Right;
        var scroll = new ScrollViewer { Content = formulas, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(8) };
        var card = Ui.Paper(Ui.Stack(scroll, copy), 12);
        card.Background = WikiPalette.Surface; card.BorderThickness = new Thickness(0);
        card.Margin = new Thickness(0, 14, 0, 18); return card;
    }
    internal static Border Note(string label, string body, Brush background)
    {
        var card = Ui.Paper(Ui.Stack(Ui.Text(label.Replace("**", ""), 13, true), Ui.Text(body.Replace("**", ""), 15)), 18);
        card.Background = background; card.BorderThickness = new Thickness(3, 0, 0, 0); card.BorderBrush = Ui.Blue; card.Margin = new Thickness(0, 12, 0, 12); return card;
    }
    internal static Border Figure(FrameworkElement drawing, string caption)
    {
        var figure = Ui.Paper(Ui.Stack(drawing, Ui.Text(caption, 12, color: Ui.Muted)), 10);
        figure.BorderThickness = new Thickness(0); figure.Margin = new Thickness(0, 14, 0, 18); return figure;
    }
    private static FrameworkElement Comparison(List<string[]> rows, Action<string> navigate)
    {
        var grid = new Grid(); int count = rows.Max(r => r.Length);
        for (int c = 0; c < count; c++) grid.ColumnDefinitions.Add(new ColumnDefinition());
        for (int r = 0; r < rows.Count; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int c = 0; c < rows[r].Length; c++)
            {
                var text = Text(rows[r][c], navigate); text.FontSize = 13;
                var cell = new Border { Child = text, Padding = new Thickness(7), Background = r == 0 ? Ui.Bg : Brushes.White, BorderBrush = Ui.Brush("#DCE2E9"), BorderThickness = new Thickness(0, 0, 0, 1) };
                Grid.SetRow(cell, r); Grid.SetColumn(cell, c); grid.Children.Add(cell);
            }
        }
        grid.MinWidth = 440; grid.Width = 720;
        var scroll = new ScrollViewer { Content = grid, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        scroll.SizeChanged += (_, e) => grid.Width = Math.Max(440, e.NewSize.Width);
        return scroll;
    }
    internal static Border TryInAnthea(WikiArticle article, Action<string, string?> openModule)
    {
        var stack = Ui.Stack(Ui.Text("Applica nel modulo", 20, true));
        foreach (var module in article.Modules)
        {
            var definition = ModuleCatalog.Get(module);
            stack.Children.Add(Ui.Button("Apri · " + definition.Name, () => openModule(module, null), true));
            if (article.Example is not null)
            {
                var example = MainWindow.WikiExamples.Definition(article.Example);
                if (example["moduleId"]?.ToString() == module)
                    stack.Children.Add(Ui.Button("Apri esempio · " + example["label"]?.ToString(), () => openModule(module, article.Example)));
            }
        }
        stack.Children.Add(Ui.Text("L'apertura di un esempio crea un nuovo foglio e segue la richiesta di salvataggio del documento corrente. Verifica ipotesi, assi e dati prima del calcolo.", 12, color: Ui.Muted));
        return Ui.Paper(stack, 20);
    }
}
