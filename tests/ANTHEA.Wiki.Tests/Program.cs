using Anthea.Wiki;
using System.Text;
using System.Text.Json;
int count = 0;
void Check(bool ok, string text) { if (!ok) throw new InvalidOperationException(text); count++; }
try
{
    string output = Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
    // Preserve the actual identities from the requested F5 base, independently of new production chapters.
    using var baseline = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "f5-articles.json")));
    var historical = baseline.RootElement.GetProperty("articles").EnumerateArray().ToArray();
    Check(historical.Length == 73, "La base F5 deve conservare i 71 articoli originari e i due della stratigrafia");
    foreach (var expected in historical)
        Check(WikiCatalog.Articles.Any(a => a.Id == expected.GetProperty("id").GetString() && a.Key == expected.GetProperty("key").GetString()),
            "Articolo F5 perso o rinominato: " + expected.GetProperty("key").GetString());
    string[] added = ["guida-paratie", "analisi-paratie", "guida-spettri-ntc", "spettri-isolamento", "capacita-orizzontale-in-terreno-stratificato", "guida-azioni-climatiche", "azioni-climatiche", "guida-carichi-traffico", "carichi-traffico"];
    Check(WikiCatalog.Articles.Count(a => !added.Contains(a.Key)) == historical.Length, "Censimento storico F5 modificato");
    foreach (string key in added) Check(WikiCatalog.Resolve(key) is not null, "Capitolo aggiunto mancante: " + key);
    Check(WikiCatalog.ForModule("geo_paratie")?.Key == "guida-paratie", "Guida paratie non collegata");
    Check(WikiCatalog.ForModule("act_spettri_ntc")?.Key == "guida-spettri-ntc", "Guida Spettri non collegata");
    Check(WikiCatalog.Resolve("guida-stratigrafia") is not null && WikiCatalog.Resolve("modello-stratigrafia") is not null, "Capitoli stratigrafia mancanti");
    Check(WikiCatalog.ForModule("geo_stratigrafia") is not null, "Guida stratigrafia non collegata");
    foreach (var article in WikiCatalog.Articles)
    {
        string body = WikiCatalog.Body(article);
        Check(body.StartsWith("## ") && !body.Contains('\ufffd'), "Offset UTF-8 invalido: " + article.Id);
        Check(WikiMarkup.Parse(body).Count > 0, "Articolo non interpretato: " + article.Id);
        Check(WikiCatalog.Resolve(article.Id) == article && WikiCatalog.Resolve(article.Key) == article, "Identità e chiave: " + article.Id);
    }
    Check(WikiCatalog.Slug("σ à τ") == "sigma-a-tau", "Normalizzazione scientifica");
    Check(WikiCatalog.Search("FESSURAZIONE").SequenceEqual(WikiCatalog.Search("fessurazione")), "Ricerca sensibile alle maiuscole");
    Check(WikiCatalog.Search("  ").Length == 0, "Ricerca vuota");
    Check(WikiCatalog.Resolve("beam")?.Id == "/wiki/manuale/fem/elementi-beam", "Alias storico");
    Check(WikiCatalog.ForModule("geo_fondazioni_superficiali") is not null, "Guida fondazioni assente");
    using (var example = WikiCatalog.Resource("examples.beam-ca.json")) Check(example.Length > 0, "Esempio non incorporato");
    var blocks = WikiMarkup.Parse("## Titolo\n<!-- commento -->\n### Geometria\nA\nB\n\n### Geometria\n> Nota\n> Testo **forte**\n\n| A | B |\n|---|:---:|\n| 1 | 2 |\n\n```dati\nx = 4\n```\n\n![Caption](non-registrata.svg)\n");
    Check(blocks.Count == 7, "Numero blocchi");
    Check(blocks[0] is WikiBlock.Heading { Id: "geometria", Level: 3 } && blocks[2] is WikiBlock.Heading { Id: "geometria-2" }, "Ancore uniche");
    Check(blocks[1] is WikiBlock.Paragraph { Text: "A B" }, "Paragrafo su più righe");
    Check(blocks[3] is WikiBlock.Quote { Title: "Nota", Text: "Testo **forte**" }, "Citazione");
    Check(blocks[4] is WikiBlock.Table table && table.Rows.Count == 2 && table.Rows[1].SequenceEqual(new[] { "1", "2" }), "Separatore tabella");
    Check(blocks[5] is WikiBlock.Code { Text: "x = 4" }, "Blocco dati");
    Check(blocks[6] is WikiBlock.Image { Caption: "Caption", Asset: null }, "Asset non registrato");
    var formulas = WikiMarkup.Parse("## Test\n\\(E\\)\n\n\\[\\frac{N}{A}\\]\n\n$$\n\\frac{M}{W}\n$$\n\n$$ a\n$$ b");
    Check(formulas.Count == 4 && formulas[0] is WikiBlock.Paragraph { Text: "$E$" }, "Delimitatori inline");
    Check(formulas[1] is WikiBlock.Formula { Text: "\\frac{N}{A}" } && formulas[2] is WikiBlock.Formula { Text: "\\frac{M}{W}" } && formulas[3] is WikiBlock.Formula { Text: "a\nb" }, "LaTeX conservato");
    var spans = WikiMarkup.Inline("A **B** `C` $D$ [E](wiki:beam)");
    Check(spans.Count(s => s.Kind == WikiInlineKind.Bold && s.Text == "B") == 1, "Grassetto");
    Check(spans.Any(s => s.Kind == WikiInlineKind.Code && s.Text == "C") && spans.Any(s => s.Kind == WikiInlineKind.Formula && s.Text == "D"), "Codice e formula");
    Check(spans.Single(s => s.Kind == WikiInlineKind.Link).Text == WikiCatalog.Resolve("beam")!.Title, "Titolo del collegamento interno");
    foreach (string destination in new[] { "javascript:alert", "file:///a", "ftp://a", "relative.txt" })
        Check(WikiMarkup.Inline("[link](" + destination + ")").All(s => s.Kind != WikiInlineKind.Link), "Collegamento non ammesso: " + destination);
    var web = WikiMarkup.Inline("[link](https://example.test/path)").Single(s => s.Kind == WikiInlineKind.Link);
    Check(!web.Internal && web.Target == "https://example.test/path", "Collegamento web");
    // 1.16.0: «guida:» links open a PDF installed in the Guide folder; only a plain file name is a link.
    var document = WikiMarkup.Inline("[articolo](guida:Palo orizzontale in terreno stratificato.pdf)").Single(s => s.Kind == WikiInlineKind.Link);
    Check(!document.Internal && document.Text == "articolo" && document.Target == "guida:Palo orizzontale in terreno stratificato.pdf", "Collegamento a un documento installato");
    foreach (string destination in new[] { "guida:../a.pdf", "guida:C:\\a.pdf", "guida:a.exe", "guida:", "guida:.pdf" })
        Check(WikiMarkup.Inline("[link](" + destination + ")").All(s => s.Kind != WikiInlineKind.Link), "Documento non ammesso: " + destination);
    string file = Path.Combine(output, "progress.json");
    var progress = new WikiProgress(file); progress.Update("beam", "dati", double.NaN, true); progress.Update("beam", "dati", 2, true); progress.Save();
    var reopened = new WikiProgress(file); Check(reopened.Entries["beam"].Fraction == 1 && reopened.Entries["beam"].Read.SequenceEqual(new[] { "dati" }), "Persistenza lettura");
    string before = File.ReadAllText(file); var transient = new WikiProgress(file, persist: false); transient.Update("other", "", 0); transient.Save(); Check(File.ReadAllText(file) == before, "Modalità senza persistenza");
    File.WriteAllText(file, "{bad"); Check(new WikiProgress(file).Entries.Count == 0, "Archivio progressi danneggiato");
    File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {count} controlli Wiki senza WPF", new UTF8Encoding(false));
    Console.WriteLine($"PASS {count} controlli Wiki senza WPF"); return 0;
}
catch (Exception e) { Console.Error.WriteLine(e); return 1; }
