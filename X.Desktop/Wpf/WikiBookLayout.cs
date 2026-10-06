using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace X.Desktop;

internal sealed partial class WikiView
{
    private static Border BookRule() => new()
    {
        BorderBrush = Ui.Brush("#9CAFBF"), BorderThickness = new Thickness(0, 0, 0, 1),
        Margin = new Thickness(0, 18, 0, 18)
    };

    private static TextBlock BookLabel(string text)
    {
        var label = Ui.Text(text, 12, true, WikiPalette.Accent);
        label.Tag = "book-accent"; label.Margin = new Thickness(0, 0, 0, 10);
        return label;
    }

    // Reflow from the actual reader width, including when the side index is open.
    private static Grid BookGrid(IReadOnlyList<FrameworkElement> items, double minimum, int maximum)
    {
        var grid = new Grid(); int previous = 0;
        void Reflow(double width)
        {
            int count = Math.Clamp((int)(width / minimum), 1, maximum);
            if (count == previous) return;
            previous = count; grid.ColumnDefinitions.Clear(); grid.RowDefinitions.Clear();
            for (int i = 0; i < count; i++) grid.ColumnDefinitions.Add(new ColumnDefinition());
            for (int i = 0; i < (items.Count + count - 1) / count; i++)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int i = 0; i < items.Count; i++)
            {
                Grid.SetColumn(items[i], i % count); Grid.SetRow(items[i], i / count);
                items[i].Margin = new Thickness(0, 0, i % count < count - 1 ? 22 : 0, 20);
            }
        }
        foreach (var item in items) grid.Children.Add(item);
        Reflow(0); grid.SizeChanged += (_, e) => Reflow(e.NewSize.Width);
        return grid;
    }

    private StackPanel BookCover()
    {
        var pane = new StackPanel();
        pane.Children.Add(BookLabel("ANTHEA  /  BIBLIOTECA DI INGEGNERIA"));
        var title = PageTitle("Engineering Handbook", 44);
        title.FontFamily = new FontFamily("Georgia"); title.Margin = new Thickness(0, 0, 0, 14);
        pane.Children.Add(title);
        pane.Children.Add(Ui.Text("Comprendere la struttura. Costruire il modello. Verificare il progetto.", 21));
        pane.Children.Add(BookRule());
        pane.Children.Add(BookLabel("APPROFONDIMENTI TECNICI  /  FORMULE, ESEMPI E FONTI"));
        pane.Children.Add(Link("Esplora la nuova biblioteca tecnica →", () => Navigate("biblioteca-tecnica")));
        pane.Children.Add(Ui.Text("Taglio e dettagli del calcestruzzo, impalcati da ponte, indagini e fondazioni. Contributi attribuiti e letture di Marco De Pisapia, GeoStru e Simone Caffè.", 16));
        pane.Children.Add(Ui.Bar(Link("Taglio e inclinazione dei puntoni", () => Navigate("taglio-traliccio")),
            Link("Muri: metodi e funzioni disponibili", () => Navigate("muri-metodi-perimetro"))));
        pane.Children.Add(BookRule());
        var intro = Ui.Stack(BookLabel("IL FILO DEL MANUALE"),
            Ui.Text("Dall’opera al calcolo", 26, true),
            Ui.Text("Segui il percorso dei carichi, scegli le ipotesi e confronta il modello con un esempio. Ogni capitolo riunisce principi, metodi e applicazioni in Anthea.", 16),
            Link("Inizia dai fondamenti →", () => Chapter(WikiCatalog.Chapters[0])));
        intro.VerticalAlignment = VerticalAlignment.Center;
        var figure = WikiEditorial.Figure(new WikiVector("bridge-notebook.svg"),
            "Impalcato → appoggi → pile → fondazioni. Un’opera, un percorso dei carichi.");
        pane.Children.Add(BookGrid([intro, figure], 360, 2));
        pane.Children.Add(BookLabel($"IL MANUALE  /  {WikiCatalog.Chapters.Length} CAPITOLI  /  {WikiCatalog.Articles.Length} ARTICOLI"));
        pane.Children.Add(BookGrid(WikiCatalog.Chapters.Select(chapter => (FrameworkElement)BookChapterCard(chapter)).ToArray(), 300, 3));
        var recent = progress.Entries.OrderByDescending(p => p.Value.Visited)
            .Select(p => WikiCatalog.Resolve(p.Key)).Where(a => a is not null).DistinctBy(a => a!.Id).Take(3).ToArray();
        if (recent.Length > 0)
        {
            pane.Children.Add(Title("Riprendi la lettura"));
            foreach (var page in recent) pane.Children.Add(Link(page!.Title + " →", () => Navigate(page.Id)));
        }
        pane.Children.Add(BookRule());
        pane.Children.Add(Link("Glossario · simboli, termini e acronimi →", Glossary));
        pane.Children.Add(Link("Fonti, criteri di lettura e archivio →", () => Navigate("tracciabilita-e-riferimenti")));
        return pane;
    }

    private Border BookChapterCard(WikiChapter chapter)
    {
        var pages = WikiCatalog.InChapter(chapter.Id);
        var number = Ui.Text(chapter.Number.ToString("00"), 34, false, WikiPalette.Accent);
        number.FontFamily = new FontFamily("Georgia"); number.Tag = "book-accent";
        var title = Link(chapter.Title + " →", () => Chapter(chapter));
        ((TextBlock)title.Content).FontSize = 21;
        ((TextBlock)title.Content).FontFamily = new FontFamily("Georgia");
        var stack = Ui.Stack(number, title, Ui.Text(chapter.Description, 14));
        stack.Children.Add(BookRule());
        foreach (var page in pages.Take(3)) stack.Children.Add(Link(page.Title, () => Navigate(page.Key)));
        var all = Link($"Esplora il capitolo · {pages.Length} {(pages.Length == 1 ? "articolo" : "articoli")}", () => Chapter(chapter));
        all.Margin = new Thickness(0, 12, 0, 0); stack.Children.Add(all);
        return new Border { Child = stack, Padding = new Thickness(20), Tag = "book-surface",
            BorderBrush = Ui.Brush("#B5C4CE"), BorderThickness = new Thickness(0, 3, 0, 0) };
    }

    private StackPanel BookChapter(WikiChapter chapter)
    {
        var pages = WikiCatalog.InChapter(chapter.Id);
        var pane = Ui.Stack(Link("← Tutti i capitoli", Home), BookLabel($"CAPITOLO {chapter.Number:00}  /  {pages.Length} ARTICOLI"));
        var title = PageTitle(chapter.Title, 40); title.FontFamily = new FontFamily("Georgia");
        pane.Children.Add(title); pane.Children.Add(Ui.Text(chapter.Description, 21));
        pane.Children.Add(BookRule());
        var introduction = Ui.Text(chapter.Introduction, 18); introduction.LineHeight = 29;
        introduction.Margin = new Thickness(0, 0, 0, 22); pane.Children.Add(introduction);
        pane.Children.Add(BookLabel("PERCORSO DI LETTURA"));
        for (int i = 0; i < pages.Length; i++)
        {
            var page = pages[i];
            var label = BookLabel($"{chapter.Number:00}.{i + 1:00}  /  {(page.Type == "guide" ? "APPLICAZIONE IN ANTHEA" : Level(page).ToUpperInvariant())}  /  {page.ReadingTime} MIN");
            var link = Link(page.Title + " →", () => Navigate(page.Key));
            ((TextBlock)link.Content).FontSize = 23;
            ((TextBlock)link.Content).FontFamily = new FontFamily("Georgia");
            var row = Ui.Stack(label, link, Ui.Text(page.Summary, 16));
            row.Margin = new Thickness(0, 6, 0, 18);
            pane.Children.Add(row); pane.Children.Add(BookRule());
        }
        return pane;
    }
}
