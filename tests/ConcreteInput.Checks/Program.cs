using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Anthea.Calculations;

internal static class Program
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly List<string> outcomes = [];
    private static void Check(bool ok, string name) => outcomes.Add((ok ? "PASS " : "FAIL ") + name);
    private static void Reject(Action action, string name)
    {
        try { action(); Check(false, name); }
        catch (ArgumentException) { Check(true, name); }
    }
    [STAThread] private static int Main(string[] args)
    {
        string output = Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
        try { if (args.Contains("--ui")) Ui(output); else if (args.Contains("--catalog")) { CatalogAudit.Run(args[2], args[3]); Check(true, "catalogo verificato sui cinque casi rifiutati"); } else Domain(); }
        catch (Exception e) { outcomes.Add("FAIL " + e); }
        File.WriteAllLines(Path.Combine(output, "esito.txt"), outcomes);
        foreach (var line in outcomes) Console.WriteLine(line);
        Console.WriteLine($"{outcomes.Count} controlli, {outcomes.Count(x => x.StartsWith("FAIL"))} falliti");
        return outcomes.Any(x => x.StartsWith("FAIL")) ? 1 : 0;
    }
    private static void Domain()
    {
        foreach (string shape in new[] { "Rettangolare", "Circolare", "A T" })
        {
            var input = SezioneCA.DefaultInput(); input["shape"] = shape;
            var missing = new SezioneCA(input);
            input["staffe_presenti"] = "Sì";
            Check(missing.Bars.SequenceEqual(new SezioneCA(input).Bars), shape + ": default storico Sì");
            input["staffe_presenti"] = "No"; input["transverse_bar_diameter_mm"] = ""; input["transverse_spacing_mm"] = "";
            Check(new SezioneCA(input).Bars.Count > 0, shape + ": staffe disattivate, dimensioni inattive");
            foreach (JsonNode? bad in new JsonNode?[] { null, JsonValue.Create(""), JsonValue.Create("si"), JsonValue.Create("SI"), JsonValue.Create(true), JsonValue.Create(1) })
            {
                var value = SezioneCA.DefaultInput(); value["shape"] = shape; value["staffe_presenti"] = bad?.DeepClone();
                Reject(() => new SezioneCA(value), shape + ": rifiuto flag " + (bad?.ToJsonString() ?? "null"));
            }
            var data = SezioneCA.DefaultData(); data["input"]!["shape"] = shape;
            var settings = SectionWorkspace.Prepare(data); settings["sle_comuni"]!["esposizione"] = "XC3";
            settings["dettagli_costruttivi"]!["elemento"] = "Trave";
            foreach (string bad in new[] { "-2", "0", "1", "2.5", "101", "NaN", "", "abc" })
            {
                settings["taglio"]!["rami_y"] = bad;
                Reject(() => ConcreteDetailingAnalysis.Calculate(data["input"]!.AsObject(), settings, []), shape + ": rami attivi " + bad);
            }
            foreach (string good in new[] { "2", "100" })
            {
                settings["taglio"]!["rami_y"] = good;
                Check(ConcreteDetailingAnalysis.Calculate(data["input"]!.AsObject(), settings, []).Checks.Count > 0, shape + ": rami limite " + good);
            }
            settings["taglio"]!["rami_y"] = "non usato";
            data["input"]!["staffe_presenti"] = "No";
            Check(ConcreteDetailingAnalysis.Calculate(data["input"]!.AsObject(), settings, []).Checks.Count > 0, shape + ": rami disattivati non bloccano");
            data["input"]!["staffe_presenti"] = "Sì";
            settings["dettagli_costruttivi"]!["elemento"] = "Pilastro";
            Check(ConcreteDetailingAnalysis.Calculate(data["input"]!.AsObject(), settings, []).Checks.Count > 0, shape + ": rami non usati dal dettaglio pilastro");
        }
        var rect = SezioneCA.DefaultInput(); var options = J.Obj(("rami_x", "2.5"), ("rami_y", "2"));
        Reject(() => ConcreteCalculationSettings.ValidateStirrups(rect, options), "taglio: intero obbligatorio");
        rect["staffe_presenti"] = "No";
        ConcreteCalculationSettings.ValidateStirrups(rect, options);
        Check(true, "taglio: campi disattivati ignorati");
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); ++i)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T item) yield return item;
            foreach (var item2 in Descendants<T>(child)) yield return item2;
        }
    }
    private static void Layout(FrameworkElement form)
    {
        form.Measure(new Size(600, 1200)); form.Arrange(new Rect(0, 0, 600, 1200)); form.UpdateLayout();
        foreach (var group in Descendants<Expander>(form)) group.IsExpanded = true;
        form.UpdateLayout();
    }
    private static void Ui(string output)
    {
        RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var document = System.Xml.Linq.XDocument.Load("X.Desktop/App.xaml");
        var dictionary = document.Root!.Elements().Single().Elements().Single();
        dictionary.SetAttributeValue(System.Xml.Linq.XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml");
        dictionary.SetAttributeValue(System.Xml.Linq.XNamespace.Xmlns + "local", "clr-namespace:X.Desktop;assembly=ANTHEA");
        foreach (var source in dictionary.Descendants().SelectMany(x => x.Attributes("Source"))) source.Value = "/ANTHEA;component/" + source.Value;
        app.Resources = (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(dictionary.ToString());
        var data = SezioneCA.DefaultData();
        SectionWorkspace.Prepare(data)["trefoli"]!.AsArray().Add(J.Obj(("id", "T1"), ("x", "0"), ("y", "-150"), ("area", "150"),
            ("sigma0", "1000"), ("Ep", "195000"), ("fpyk", "1670"), ("fpk", "1860"), ("eps_u", "35")));
        var type = typeof(X.Desktop.MainWindow).Assembly.GetType("X.Desktop.ConcreteWorkspace")!;
        using var workspace = (IDisposable)Activator.CreateInstance(type, Members, null, [data], null)!;
        var settings = (JsonObject)type.GetField("settings", Members)!.GetValue(workspace)!;
        var panels = (IDictionary)type.GetField("stressPanels", Members)!.GetValue(workspace)!;
        FrameworkElement Form(string key) => (FrameworkElement)panels[key]!.GetType().GetField("Options", Members)!.GetValue(panels[key])!;
        void Set(string key, string value) => Form("SLE").GetType().GetMethod("Set", Members)!.Invoke(Form("SLE"), [key, value, false]);
        bool HasNote(string set)
        {
            var form = Form(set); Layout(form);
            return Descendants<TextBlock>(form).Any(t => t.Text.Contains("φ ≥ 0") && VisualTreeHelper.GetParent(t) is Border { Visibility: Visibility.Visible });
        }
        foreach (var (phi, n) in new[] { ("phi", "n_armature"), ("phi_trefoli", "n_trefoli") })
        {
            Set(phi, "-1");
            foreach (string set in SectionWorkspace.Sets.Skip(2))
            {
                Check(settings["sle"]![set].S(phi) == "-1" && settings["sle"]![set].S(n) == "", set + ": φ negativo conserva ingresso e svuota n");
                Check(HasNote(set), set + ": errore visibile accanto al campo " + phi);
            }
            Set(phi, "0");
            Check(settings["sle"]!["SLE"].D(n) > 0 && !HasNote("SLE"), phi + ": correzione rimuove errore");
            Set(n, "1");
            Check(settings["sle"]!["SLE"].S(n) == "1" && settings["sle"]!["SLE"].S(phi) == "", n + ": rapporto troppo piccolo rifiutato");
            Check(HasNote("SLE_QP"), n + ": errore propagato alle altre SLE");
            Set(n, "15");
            Check(settings["sle"]!["SLE"].D(phi) >= 0 && !HasNote("SLE"), n + ": recupero dopo correzione");
        }
        Set("phi", "-1"); var picture = Form("SLE"); Layout(picture);
        var bitmap = new RenderTargetBitmap(600, 1200, 96, 96, PixelFormats.Pbgra32);
        var background = new DrawingVisual(); using (var canvas = background.RenderOpen()) canvas.DrawRectangle(Brushes.White, null, new Rect(0, 0, 600, 1200));
        bitmap.Render(background); bitmap.Render(picture);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(output, "viscosita-rifiuto.png"))) encoder.Save(stream);
        app.Shutdown();
    }
}
