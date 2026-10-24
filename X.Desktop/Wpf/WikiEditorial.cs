using Anthea.Application.Modules;
using System.Diagnostics;
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
    // WPF-Math 2.1 cannot render mathbf. Use the equivalent arrow notation for the Darcy flux vector only.
    private static string RenderLatex(string value) => value.Replace(@"\ ", @"\,").Replace(@"\qquad", @"\;\;\;\;\;\;").Replace(@"\quad", @"\;\;\;")
        .Replace(@"\mathbf{q}", @"\vec{q}");
    internal static void Render(StackPanel pane, string markdown, Action<string> navigate,
        Func<string, string, int, FrameworkElement> heading)
    {
        foreach (var block in WikiMarkup.Parse(markdown))
        {
            switch (block)
            {
                case WikiBlock.Heading title: pane.Children.Add(heading(title.Id, title.Text, title.Level)); break;
                case WikiBlock.Formula formula: pane.Children.Add(Formula(formula.Text)); break;
                case WikiBlock.Code code: pane.Children.Add(Note("DATI E PROCEDURA", code.Text, Ui.Bg)); break;
                case WikiBlock.Image image:
                    if (image.Asset is not { } name) { pane.Children.Add(Note("FIGURA", image.Caption, Ui.Bg)); break; }
                    FrameworkElement graphic = name.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
                        ? new WikiVector(name)
                        : new Image { Source = Ui.Asset("Wiki/" + name), MaxHeight = 320, Stretch = Stretch.Uniform };
                    System.Windows.Automation.AutomationProperties.SetName(graphic, image.Caption);
                    pane.Children.Add(Figure(graphic, image.Caption)); break;
                case WikiBlock.Quote quote:
                    var note = Note(quote.Title, "", WikiPalette.Surface);
                    ((StackPanel)note.Child).Children.Add(Text(quote.Text, navigate)); pane.Children.Add(note); break;
                case WikiBlock.Table table: pane.Children.Add(Comparison(table.Rows, navigate)); break;
                case WikiBlock.Paragraph paragraph: pane.Children.Add(Text(paragraph.Text, navigate)); break;
            }
        }
    }
    private static TextBlock Text(string value, Action<string> navigate)
    {
        var text = Ui.Text("", 15.5, color: WikiPalette.Ink); text.LineHeight = 26; text.Margin = new Thickness(0, 5, 0, 12);
        foreach (var token in WikiMarkup.Inline(value))
        {
            switch (token.Kind)
            {
                case WikiInlineKind.Text: text.Inlines.Add(new Run(token.Text)); break;
                case WikiInlineKind.Bold: text.Inlines.Add(new Bold(new Run(token.Text))); break;
                case WikiInlineKind.Code: text.Inlines.Add(new Run(token.Text) { FontFamily = new FontFamily("Consolas"), Background = Appearance.Surface }); break;
                case WikiInlineKind.Formula:
                    var math = new FormulaControl { Formula = RenderLatex(token.Text), Scale = 16, Foreground = text.Foreground };
                    if (math.HasError) throw new InvalidOperationException("Formula inline LaTeX non valida: " + token.Text);
                    System.Windows.Automation.AutomationProperties.SetName(math, token.Text);
                    text.Inlines.Add(new InlineUIContainer(math) { BaselineAlignment = BaselineAlignment.Center }); break;
                case WikiInlineKind.Link:
                    var link = new Hyperlink(new Run(token.Text)) { Foreground = WikiPalette.Accent };
                    link.Click += (_, _) =>
                    {
                        if (token.Internal) navigate(token.Target!);
                        else if (Uri.TryCreate(token.Target, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http")
                            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
                    };
                    text.Inlines.Add(link); break;
            }
        }
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
    private static FrameworkElement Comparison(IReadOnlyList<string[]> rows, Action<string> navigate)
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
            var definition = ModuleDirectory.Get(module);
            stack.Children.Add(Ui.Button("Apri · " + definition.Name, () => openModule(module, null), true));
            if (article.Example is not null)
            {
                var example = Anthea.Presentation.Shell.Navigation.WikiExamples.Definition(article.Example);
                if (example["moduleId"]?.ToString() == module)
                    stack.Children.Add(Ui.Button("Apri esempio · " + example["label"]?.ToString(), () => openModule(module, article.Example)));
            }
        }
        stack.Children.Add(Ui.Text("L'apertura di un esempio crea un nuovo foglio e segue la richiesta di salvataggio del documento corrente. Verifica ipotesi, assi e dati prima del calcolo.", 12, color: Ui.Muted));
        return Ui.Paper(stack, 20);
    }
}
