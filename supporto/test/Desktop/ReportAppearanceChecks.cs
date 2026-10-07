using System.IO;
using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

// --check-report-appearance-offscreen <cartella>: the images of the calculation documents do not depend on the appearance of
// ANTHEA (Appearance.cs). The same reports are built in Light, Dark and Very dark, with the views created in each mode and
// without any native window; every image (word/media of each .docx) must be identical, byte for byte, to the Light one.
// confronto.txt lists every comparison, the images are kept under <cartella>/<aspetto>/<report>/ for inspection.
internal static class ReportAppearanceChecks
{
    internal static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory); var log = new List<string>(); var differences = new List<string>();
        void Check(bool ok, string text) { if (!ok) throw new Exception(text); log.Add("OK " + text); }
        Dictionary<string, byte[]>? light = null;
        try
        {
            Check(Application.Current.Windows.Count == 0, "Nessuna finestra nativa");
            foreach (var mode in new[] { AppAppearance.Light, AppAppearance.Dark, AppAppearance.VeryDark })
            {
                Appearance.Set(mode, false);
                var paper = ((SolidColorBrush)Appearance.Paper).Color;
                Check((paper == Colors.White) == (mode == AppAppearance.Light), "Palette dell'applicazione in " + mode + ": " + paper);
                var images = await Images(Path.Combine(directory, mode.ToString()), Check);
                Check(((SolidColorBrush)Appearance.Paper).Color == paper, "Palette dell'applicazione ripristinata dopo i report in " + mode);
                if (light is null) { light = images; log.Add($"Light: {images.Count} immagini"); continue; }
                foreach (var (key, bytes) in light)
                {
                    bool same = images.TryGetValue(key, out var other) && other.AsSpan().SequenceEqual(bytes);
                    log.Add((same ? "UGUALE    " : "DIVERSA   ") + mode + " · " + key + (other is null ? " (mancante)" : ""));
                    if (!same) differences.Add(mode + " · " + key);
                }
                foreach (var key in images.Keys.Except(light.Keys)) { log.Add("IN PIÙ    " + mode + " · " + key); differences.Add(mode + " · " + key + " (in più)"); }
            }
            Check(Application.Current.Windows.Count == 0, "Nessuna interazione con il desktop");
            Check(differences.Count == 0, "Immagini dei report identiche a Chiaro in Scuro e Molto scuro" + (differences.Count == 0 ? "" : ": diverse " + string.Join(" | ", differences)));
            log.Add($"PASS {light!.Count} immagini confrontate in tre aspetti");
        }
        finally { Appearance.Set(AppAppearance.Light, false); File.WriteAllLines(Path.Combine(directory, "confronto.txt"), log); }
    }

    // Every report with figures of X.Desktop, from the documents of a new sheet (or the test cases already used by the smoke).
    private static async Task<Dictionary<string, byte[]>> Images(string directory, Action<bool, string> check)
    {
        var images = new Dictionary<string, byte[]>();
        void Add(string report, byte[] docx)
        {
            Directory.CreateDirectory(Path.Combine(directory, report)); File.WriteAllBytes(Path.Combine(directory, report + ".docx"), docx);
            using var zip = new ZipArchive(new MemoryStream(docx)); int count = 0;
            foreach (var entry in zip.Entries.Where(e => e.FullName.StartsWith("word/media/")).OrderBy(e => e.FullName, StringComparer.Ordinal))
            {
                using var stream = entry.Open(); using var copy = new MemoryStream(); stream.CopyTo(copy);
                images[report + "/" + entry.Name] = copy.ToArray(); File.WriteAllBytes(Path.Combine(directory, report, entry.Name), copy.ToArray()); count++;
            }
            check(count > 0, report + ": immagini nel report " + count);
        }
        static string DocumentXml(byte[] docx)
        {
            using var zip = new ZipArchive(new MemoryStream(docx)); using var read = new StreamReader(zip.GetEntry("word/document.xml")!.Open()); return read.ReadToEnd();
        }
        using (var editor = new SheetEditor("str_palo", SezioneCA.DefaultData()))
        {
            await editor.CalculateAsync(); await editor.WaitForConcreteChecks();
            var docx = editor.BuildReport("Sezione c.a. · aspetto", ["geometria", "materiali", "coefficienti", "azioni", "dominio3d", "dominio2d", "SLE", "SLE_FREQ", "SLE_QP", "taglio", "grafici"]);
            check(DocumentXml(docx).Contains("Dominio 3D "), "Sezione c.a.: figura del dominio 3D presente");
            Add("sezione-ca", docx);
        }
        using (var editor = new SheetEditor(BridgeSection.Module, Archivio.NuovoFoglio(BridgeSection.Module)))
        {
            await editor.CalculateAsync();
            Add("sezione-ponte", editor.BuildReport("Sezione da ponte · aspetto", ReportBridge.DefaultSections()));
        }
        using (var editor = new SheetEditor(BridgeConcept.Module, Archivio.NuovoFoglio(BridgeConcept.Module)))
        {
            await editor.CalculateAsync();
            Add("bridge-design", editor.BuildReport("Bridge Design · aspetto", []));
        }
        using (var editor = new SheetEditor(RetainingWall.Module, RetainingWall.Defaults()))
        {
            // Global stability as in --smoke-global-stability: its figure is the second detached drawing of the wall report.
            var w = editor.retainingWall!; await w.CalculateAsync();
            w.LayerGrid.Rows[0]["thickness"] = "25"; w.PrepareGlobal();
            ((CheckBox)w.Forms["global"].Editors["enabled"]).IsChecked = true; await w.CalculateAsync();
            ((CheckBox)w.Forms["global"].Editors["profile_confirmed"]).IsChecked = true; w.Forms["global_search_mode"].Set("search_mode", "Assegnata"); w.Forms["global_search"].Set("depth_min", "0.1");
            await w.CalculateAsync(); check(w.GlobalResult is { Cases.Length: > 0 }, "Muro: stabilità globale calcolata");
            Add("muro", editor.BuildReport("Muro · aspetto", []));
        }
        var horizontalData = PaloOrizzontale.Defaults(); horizontalData["stratigrafie"]![0]!.AsArray().Add(PaloOrizzontale.Layer());
        using (var editor = new SheetEditor(PaloOrizzontale.Module, horizontalData))
        {
            await editor.WaitForHorizontalAutomatic();
            Add("palo-orizzontale", editor.BuildReport("Palo orizzontale · aspetto", []));
        }
        var pile = SmokeTestData.LoadCases().First(c => c.S("nome") == "palo_storico_0")!["input"]!.AsObject();
        using (var editor = new SheetEditor("geo_palo_verticale", pile))
        {
            await editor.CalculateAsync();
            Add("palo-verticale", editor.BuildReport("Palo verticale · aspetto", ReportWord.Sezioni.Select(s => s.Key).ToHashSet()));
        }
        return images;
    }
}

internal sealed partial class SheetEditor
{
    // The automatic c.a. checks (domains, SLE, shear) queued after the first calculation, without a window.
    internal async Task WaitForConcreteChecks()
    {
        for (int i = 0; i < 1200 && (Busy || !HasResults); i++) { await Task.Delay(50); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); }
        if (Busy || !HasResults) throw new Exception("Verifiche c.a. non completate");
    }
}
