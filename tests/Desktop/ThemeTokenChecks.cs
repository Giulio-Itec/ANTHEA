using System.IO;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using X.Desktop.Themes;
using X.Desktop.Views.Common;

namespace X.Desktop;

/// <summary>
/// --check-theme-tokens &lt;cartella&gt; (UiTests, F5.3, proposal F5 sections 6.1 and 7.3), off screen, without native windows:
/// <list type="bullet">
/// <item>the catalogue of X.Desktop/Themes/ThemeTokens.cs (names, colours, roles) and its registration: every Token.&lt;nome&gt; of the
/// application resources is the same object as Appearance.Colour(colore, ruolo), as are the historic resources with the same colour
/// and role (AppearancePaper, Appearance.background.F3F5F8…);</item>
/// <item>colour equality: for every token and the three appearances the colour of the brush, and of an element that takes it with
/// DynamicResource, equals exactly the transformation of the palette (Appearance.Transform) and the frozen table
/// tests/fixtures/f5/token-colori.json; the brush object stays the same when the appearance changes;</item>
/// <item>the dictionaries of X.Desktop/Themes load on their own (pack URI, a new ResourceDictionary) and App.xaml merges them; the
/// typographic tokens have the values of the census; the five F5.2 resources absorbed by the tokens are gone;</item>
/// <item>light scope of the documents (DocumentAppearance): the image of the sample FieldForm built in Scuro and Molto scuro is
/// byte for byte the Light one, and the palette returns to the appearance afterwards.</item>
/// </list>
/// Writes token-colori.json (the table of the run), immagini/ (the FieldForm images), test.txt and esito.txt («PASS n controlli»).
/// </summary>
internal static class ThemeTokenChecks
{
    internal const string Reference = "tests/fixtures/f5/token-colori.json";
    internal static readonly string[] Dictionaries = ["ProjectStyles", "AppearanceControls", "Controls", "Typography"];
    /// <summary>Typographic tokens fixed by the census of F5.3 (docs/refactoring/f5-temi-token.md).</summary>
    internal static readonly (string Key, double Size)[] Fonts =
        [("Font.Display", 30), ("Font.Title", 22), ("Font.Heading", 16), ("Font.Subheading", 14), ("Font.Body", 13), ("Font.Small", 12), ("Font.Caption", 11)];
    /// <summary>The resources added by the XAML FieldForm in F5.2, absorbed by Token.Calculated, Token.Note.* and Token.Muted.</summary>
    internal static readonly string[] Absorbed = ["Appearance.calculated.EAF2FA", "Appearance.background.FFF6DD", "Appearance.border.EEDCAF",
        "Appearance.foreground.64748B", "Appearance.foreground.865D16"];

    internal static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory);
        var log = new List<string>(); int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); log.Add("OK " + message); checks++; }
        try
        {
            var app = Application.Current;
            Check(app.Windows.Count == 0, "Nessuna finestra nativa");
            var catalogue = ThemeTokens.Catalogue;
            var roles = typeof(ThemeRole).GetFields(BindingFlags.Static | BindingFlags.NonPublic).Select(f => (string)f.GetValue(null)!).ToHashSet();

            // Catalogue.
            Check(catalogue.Select(t => t.Name).Distinct().Count() == catalogue.Length, "Nomi dei token distinti");
            Check(catalogue.All(t => System.Text.RegularExpressions.Regex.IsMatch(t.Light, "^#[0-9A-F]{6}$")), "Colori della Chiara in #RRGGBB maiuscolo, opachi");
            Check(catalogue.All(t => roles.Contains(t.Role)), "Ruoli della tavolozza noti: " + string.Join(", ", roles));
            Check(catalogue.All(t => t.Use.Length > 0 && t.Historic.Length > 0), "Uso e corrispondenza storica per ogni token");
            // F5.8: Segment. (the cards of the segments of the elastic pile) is a group of its own, with the role background.
            var groups = new[] { "Series.", "Drawing.", "Swatch.", "Utilization.", "Segment." };
            var semantic = catalogue.Where(t => !groups.Any(g => t.Name.StartsWith(g, StringComparison.Ordinal))).ToList();
            Check(semantic.Count is >= 30 and <= 40, $"Insieme semantico di circa 35 token: {semantic.Count}");
            Check(catalogue.Where(t => t.Name.StartsWith("Drawing.", StringComparison.Ordinal) || t.Name.StartsWith("Swatch.", StringComparison.Ordinal)
                || t.Name.StartsWith("Utilization.", StringComparison.Ordinal)).All(t => t.Role == ThemeRole.Swatch), "Drawing, Swatch e Utilization con il ruolo identità (swatch)");
            Check(catalogue.Where(t => t.Name.StartsWith("Series.", StringComparison.Ordinal)).All(t => t.Role == ThemeRole.Series), "Series con il ruolo series");
            Check(catalogue.Where(t => t.Name.StartsWith("Segment.", StringComparison.Ordinal)).All(t => t.Role == ThemeRole.Background), "Segment con il ruolo background (schede dei tratti, F5.8)");

            // Registration: the same palette brushes.
            foreach (var token in catalogue)
            {
                var resource = app.Resources[token.Key];
                Check(resource is SolidColorBrush && ReferenceEquals(resource, Appearance.Colour(token.Color, token.Role)) && ReferenceEquals(resource, app.FindResource(token.Key)),
                    $"{token.Key}: stesso pennello di Appearance.Colour({token.Light}, {token.Role})");
            }
            foreach (var (key, token) in new[] { ("AppearancePaper", "Paper"), ("AppearanceSurface", "Surface"), ("AppearanceInk", "Ink"), ("AppearanceBorder", "Border.Default"),
                ("AppearanceSelection", "Selection"), ("Appearance.background.F3F5F8", "Surface"), ("Appearance.background.F8FAFC", "Field.Background"),
                ("Appearance.background.F8FAFC", "PaperAlt"), ("Appearance.foreground.0B2A4A", "Ink"), ("Appearance.foreground.9A4D0A", "Warning.Foreground"),
                ("Appearance.border.DCE2E9", "Border"), ("Appearance.selection.E2EFFC", "Selection"), ("Appearance.background.0B2A4A", "CommandBar") })
                Check(ReferenceEquals(app.FindResource(key), app.FindResource(ThemeTokens.Prefix + token)), $"{key} e Token.{token}: stesso oggetto");
            Check(ReferenceEquals(Appearance.Paper, ThemeTokens.Brush("Paper")) && ReferenceEquals(Appearance.Calculated, ThemeTokens.Brush("Calculated"))
                && ReferenceEquals(Appearance.Selected, ThemeTokens.Brush("Selected")) && ReferenceEquals(Appearance.Accent, ThemeTokens.Brush("Accent"))
                && ReferenceEquals(Appearance.Dim(Brushes.Gray), ThemeTokens.Brush("Disabled")) && ReferenceEquals(Appearance.Series(Ui.Blue), ThemeTokens.Brush("Series.Blue")),
                "Pennelli nominati della tavolozza (Paper, Calculated, Selected, Accent, Dim, Series) uguali ai token");
            foreach (var key in Absorbed) Check(!app.Resources.Contains(key) && app.TryFindResource(key) is null, "Risorsa F5.2 assorbita dai token: " + key);

            // Colour equality in the three appearances, against the transformation of the palette and the frozen table.
            var transform = typeof(Appearance).GetMethod("Transform", BindingFlags.NonPublic | BindingFlags.Static, [typeof(Color), typeof(string)])
                ?? throw new InvalidOperationException("Appearance.Transform non trovato");
            var brushes = catalogue.ToDictionary(t => t.Name, t => (SolidColorBrush)app.Resources[t.Key]);
            var hosts = catalogue.ToDictionary(t => t.Name, t => { var b = new Border(); b.SetResourceReference(Border.BackgroundProperty, t.Key); return b; });
            var table = new JsonObject();
            foreach (var token in catalogue) table[token.Name] = new JsonObject { ["ruolo"] = token.Role };
            foreach (var mode in new[] { AppAppearance.Light, AppAppearance.Dark, AppAppearance.VeryDark })
            {
                Appearance.Set(mode, false);
                int equal = 0;
                foreach (var token in catalogue)
                {
                    var expected = (Color)transform.Invoke(null, [token.Color, token.Role])!;
                    var brush = brushes[token.Name];
                    if (brush.Color != expected) throw new InvalidOperationException($"{token.Key} in {mode}: {ContrastAudit.Hex(brush.Color)} invece di {ContrastAudit.Hex(expected)}");
                    if (!ReferenceEquals(app.Resources[token.Key], brush)) throw new InvalidOperationException($"{token.Key} in {mode}: pennello sostituito");
                    if (hosts[token.Name].Background is not SolidColorBrush host || !ReferenceEquals(host, brush) || host.Color != expected)
                        throw new InvalidOperationException($"{token.Key} in {mode}: l'elemento con DynamicResource non segue la tavolozza");
                    if (mode == AppAppearance.Light && expected != token.Color) throw new InvalidOperationException($"{token.Key}: in Chiara {ContrastAudit.Hex(expected)} invece di {token.Light}");
                    table[token.Name]![mode.ToString()] = ContrastAudit.Hex(expected); equal++;
                }
                Check(equal == catalogue.Length, $"{mode}: {equal} token uguali alla trasformazione della tavolozza, anche negli elementi con DynamicResource");
            }
            Appearance.Set(AppAppearance.Light, false);
            var options = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            var document = new JsonObject
            {
                ["nota"] = "Colori dei token di tema nei tre aspetti (F5.3): Chiara (Light), Scuro (Dark), Molto scuro (VeryDark). Trasformazione della tavolozza di " +
                    "X.Desktop/Wpf/Appearance.cs al commit di F5.3; si aggiorna solo con una modifica motivata della tavolozza o del catalogo.",
                ["token"] = table
            };
            string written = document.ToJsonString(options).ReplaceLineEndings("\r\n") + "\r\n";
            File.WriteAllText(Path.Combine(directory, "token-colori.json"), written);
            string reference = Path.Combine(SourceIndex.RepositoryRoot(), Reference);
            Check(File.Exists(reference), "Tabella congelata presente: " + Reference);
            var frozen = JsonNode.Parse(File.ReadAllText(reference))!["token"]!.AsObject();
            var differences = table.Select(p => p.Key).Union(frozen.Select(p => p.Key))
                .Where(name => !JsonNode.DeepEquals(table[name], frozen[name])).ToList();
            Check(differences.Count == 0, "Colori uguali alla tabella congelata " + Reference + (differences.Count == 0 ? "" : ": diversi " + string.Join(", ", differences)));

            // Dictionaries of X.Desktop/Themes, each on its own, merged by App.xaml.
            var merged = app.Resources.MergedDictionaries.Select(d => d.Source?.OriginalString ?? "").ToList();
            // The data templates of the views follow the theme; a migrated module may add its own dictionary of templates under Views/.
            Check(merged.Take(3).SequenceEqual(["Themes/ProjectStyles.xaml", "Themes/Typography.xaml", "Themes/Controls.xaml"])
                && merged.Skip(3).All(s => s.StartsWith("Views/", StringComparison.Ordinal)) && merged.Contains("Views/Common/FieldFormTemplates.xaml"),
                "App.xaml unisce i dizionari di Themes, poi i modelli delle viste: " + string.Join(", ", merged));
            Check(app.Resources.Keys.Cast<object>().All(k => k is string s && (s.StartsWith("Appearance", StringComparison.Ordinal) || s.StartsWith(ThemeTokens.Prefix, StringComparison.Ordinal)) || k is ResourceKey),
                "App.xaml senza stili propri: in Application.Resources solo le risorse registrate dalla tavolozza");
            foreach (var name in Dictionaries)
            {
                var loaded = new ResourceDictionary { Source = new Uri($"pack://application:,,,/ANTHEA;component/Themes/{name}.xaml") };
                Check(loaded.Count > 0, $"Themes/{name}.xaml caricabile da solo ({loaded.Count} chiavi)");
                if (name == "Controls")
                {
                    foreach (var type in new[] { typeof(Window), typeof(Button), typeof(TextBox), typeof(ComboBox), typeof(DataGrid), typeof(TabItem), typeof(MenuItem), typeof(ScrollBar) })
                        Check(loaded[type] is Style style && style.TargetType == type, "Controls.xaml: stile implicito di " + type.Name);
                    Check(loaded["AppearanceMenuItemTemplate"] is ControlTemplate && loaded["AppearanceDrawingLabel"] is Style, "Controls.xaml: modelli scuri di AppearanceControls.xaml uniti");
                }
                if (name == "Typography")
                    foreach (var (key, size) in Fonts) Check(loaded[key] is double value && value == size && app.FindResource(key) is double used && used == size, $"{key} = {size}");
            }

            // Light scope of the documents: the FieldForm built in each appearance gives the image of Chiara.
            Directory.CreateDirectory(Path.Combine(directory, "immagini"));
            byte[]? light = null; var imageSize = new Size(640, 760);
            foreach (var mode in new[] { AppAppearance.Light, AppAppearance.Dark, AppAppearance.VeryDark })
            {
                Appearance.Set(mode, false);
                var paper = ((SolidColorBrush)Appearance.Paper).Color;
                var (concrete, pile) = FieldFormContrast.SampleForms();
                var body = new StackPanel();
                body.Children.Add(new FieldForm { DataContext = concrete });
                body.Children.Add(new FieldForm { DataContext = pile, Margin = new Thickness(0, 16, 0, 0) });
                var host = new Border { Child = body, Padding = new Thickness(12) };
                host.SetResourceReference(Border.BackgroundProperty, ThemeTokens.Prefix + "Paper");
                Appearance.SetDark(host, mode != AppAppearance.Light);
                await Task.Yield();
                var image = DocumentAppearance.Image(host, imageSize);
                File.WriteAllBytes(Path.Combine(directory, "immagini", $"fieldform-documento-{mode}.png"), image);
                Check(Appearance.Current == mode && ((SolidColorBrush)Appearance.Paper).Color == paper && Appearance.GetDark(host) == (mode != AppAppearance.Light),
                    $"{mode}: tavolozza e stile scuro ripristinati dopo l'ambito chiaro");
                if (light is null) { light = image; Check(image.Length > 1000, "Immagine Chiara del FieldForm prodotta"); continue; }
                Check(image.AsSpan().SequenceEqual(light), $"{mode}: immagine del FieldForm nell'ambito chiaro uguale byte per byte alla Chiara");
            }
            Check(Application.Current.Windows.Count == 0, "Nessuna interazione con il desktop");
            log.Add($"PASS {checks} controlli");
            File.WriteAllText(Path.Combine(directory, "esito.txt"), $"PASS {checks} controlli: {catalogue.Length} token ({semantic.Count} semantici) uguali alla tavolozza nei tre aspetti, " +
                $"dizionari di Themes caricabili, {Fonts.Length} token tipografici, ambito chiaro dei documenti\r\n");
        }
        finally
        {
            Appearance.Set(AppAppearance.Light, false);
            File.WriteAllLines(Path.Combine(directory, "test.txt"), log);
        }
    }
}
