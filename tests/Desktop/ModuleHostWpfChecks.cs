using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Anthea.Application.Modules;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Tests;
using X.Core;
using X.Desktop.Services;

namespace X.Desktop;

/// <summary>
/// --check-module-host-offscreen &lt;cartella&gt; (UiTests, F5.2): the ModuleHost in the WPF layer, off screen. ModuleImplementations
/// lists exactly the modules of ModuleDirectory, all historic; the composition root opens historic editors; the bench argument
/// chooses an MVVM implementation and fails clearly without one; MvvmSheetEditor hosting the test-only probe module
/// (tests/Shared/ProbeSheetModule.cs, here in place of a migrated module) shows the view of its DataTemplate, raises Modified,
/// invalidates the results at a change, returns Data and Result and translates the ISheetHost commands into the ISheetEditor
/// events; MainWindow hosts it as a historic sheet. Writes test.txt, a PNG of the view and esito.txt («PASS n controlli»).
/// </summary>
internal static class ModuleHostWpfChecks
{
    /// <summary>The module the probe stands for: a small module of ModuleDirectory, with a sheet opened full size by MainWindow.</summary>
    internal const string ProbeModule = "mat_acciaio_armatura";

    private const string ProbeView = """
        <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
          <StackPanel Margin="12">
            <TextBox Text="{Binding Label, UpdateSourceTrigger=PropertyChanged}" IsReadOnly="{Binding IsReadOnly}" MinWidth="240" />
            <TextBlock Text="{Binding CalculationState}" />
          </StackPanel>
        </DataTemplate>
        """;

    internal static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory);
        var log = new List<string>();
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); log.Add("OK " + message); }
        void Fails<TException>(Action action, string text, string message) where TException : Exception
        {
            try { action(); }
            catch (TException error) when (error.Message.Contains(text, StringComparison.Ordinal)) { log.Add("OK " + message + " · " + error.Message); return; }
            throw new InvalidOperationException(message + ": nessun errore chiaro con «" + text + "»");
        }

        // The versioned choice of the Release: exactly the modules of ModuleDirectory, in its order, all historic (F5.2).
        Check(ModuleImplementations.Declared.Select(d => d.Module).SequenceEqual(ModuleDirectory.All.Select(m => m.Id)),
            "ModuleImplementations elenca esattamente i moduli di ModuleDirectory, nel suo ordine");
        Check(ModuleImplementations.Declared.All(d => d.Implementation == SheetImplementation.Historic) && ModuleImplementations.Release.Mvvm.Count == 0,
            "Release: tutti i moduli con la vista storica");

        // The composition root of the product: registry of ModuleDirectory with the MVVM pile group (F5.4), historic editors.
        var composition = new DesktopComposition(() => null);
        Check(composition.Modules.All.Select(d => d.Info).SequenceEqual(ModuleDirectory.All)
            && composition.Modules.SheetModuleIds.SequenceEqual(["geo_efficienza_orizzontale"]),
            "Radice di composizione: registro uguale a ModuleDirectory, con la sola palificata MVVM");
        using (var historic = composition.SheetEditors.Open(ProbeModule, ModuleDirectory.CreateData(ProbeModule)))
            Check(historic is SheetEditor, "Radice di composizione: la fabbrica apre la vista storica");
        using (var pileGroup = composition.SheetEditors.Open("geo_efficienza_orizzontale", ModuleDirectory.CreateData("geo_efficienza_orizzontale")))
            Check(pileGroup is SheetEditor, "Radice di composizione: la Release apre la palificata con la vista storica");
        Fails<ArgumentException>(() => composition.SheetEditors.Open("inesistente", new JsonObject()), "inesistente", "Modulo sconosciuto rifiutato come oggi");

        // The bench argument (UiTests only).
        Check(ModuleHostBench.WithoutBench(["--check-f3-equivalence", "radice", "uscita", ModuleHostBench.Argument, "str_palo=mvvm"])
            .SequenceEqual(["--check-f3-equivalence", "radice", "uscita"]), "Banco: argomenti posizionali senza le coppie --module-impl");
        Check(ModuleHostBench.Implementations([ModuleHostBench.Argument, "str_palo=mvvm"]).Mvvm.SequenceEqual(["str_palo"])
            && ModuleHostBench.Implementations(["--check-f3-equivalence"]).Mvvm.Count == 0
            && ModuleHostBench.Implementations([ModuleHostBench.Argument, "str_palo=mvvm", ModuleHostBench.Argument, "str_palo=historic"]).Mvvm.Count == 0,
            "Banco: scelte applicate in ordine sulla Release");
        Fails<InvalidOperationException>(() => ModuleHostBench.SheetEditors([ModuleHostBench.Argument, "str_palo=mvvm"]), "str_palo",
            "Banco: modulo senza implementazione MVVM");
        Fails<ArgumentException>(() => ModuleHostBench.Implementations([ModuleHostBench.Argument, "inesistente=mvvm"]), "inesistente", "Banco: modulo sconosciuto");
        Fails<ArgumentException>(() => ModuleHostBench.Implementations([ModuleHostBench.Argument, "str_palo=nuova"]), "nuova", "Banco: implementazione sconosciuta");
        Fails<ArgumentException>(() => ModuleHostBench.Implementations([ModuleHostBench.Argument, "str_palo"]), "str_palo", "Banco: coppia incompleta");
        Fails<ArgumentException>(() => ModuleHostBench.Implementations([ModuleHostBench.Argument]), ModuleHostBench.Argument, "Banco: valore mancante");

        // The probe stands for a migrated module: registered for ProbeModule and chosen with the bench argument.
        var probe = new ProbeSheetModule(ModuleDirectory.Get(ProbeModule));
        var factory = ModuleHostBench.SheetEditors([ModuleHostBench.Argument, ProbeModule + "=mvvm"], new ModuleRegistry([probe]));
        var echo = new EchoService();
        var data = new JsonObject { ["etichetta"] = "iniziale" };
        Fails<InvalidOperationException>(() => factory.Open(ProbeModule, data, echo), nameof(ProbeSheetViewModel), "Foglio senza DataTemplate rifiutato con il tipo del ViewModel");
        Check(probe.Created.Single().IsDisposed, "Foglio senza vista chiuso");

        var key = new DataTemplateKey(typeof(ProbeSheetViewModel));
        System.Windows.Application.Current.Resources[key] = (DataTemplate)XamlReader.Parse(ProbeView);
        try
        {
            using var editor = factory.Open(ProbeModule, data, echo);
            Check(editor is MvvmSheetEditor { Sheet: ProbeSheetViewModel }, "Il banco apre il modulo con MvvmSheetEditor");
            var sheet = (ProbeSheetViewModel)((MvvmSheetEditor)editor).Sheet;
            Check(editor.Module == ProbeModule && ReferenceEquals(editor.Data, sheet.Data) && ReferenceEquals(editor.PreviewData, sheet.Data)
                && !ReferenceEquals(editor.Data, data) && JsonNode.DeepEquals(editor.Data, data), "Data: copia di lavoro del foglio, distinta dall'ingresso");
            var view = editor.View;
            view.Measure(new Size(640, 200)); view.Arrange(new Rect(0, 0, 640, 200)); view.UpdateLayout();
            var box = Walk(view).OfType<TextBox>().Single();
            Check(box.Text == "iniziale" && Walk(view).OfType<TextBlock>().Any(t => t.Text == "Idle"), "La vista del DataTemplate mostra il foglio");

            int modified = 0;
            editor.Modified += () => modified++;
            await editor.CalculateAsync();
            Check(editor.HasResults && editor.Result!["eco"]!.ToString() == "iniziale" && !editor.Busy && echo.Calls == 1, "Calcolo e risultato attraverso ISheetEditor");
            box.Text = "modificata";
            Check(modified == 1 && editor.Data["etichetta"]!.ToString() == "modificata" && data["etichetta"]!.ToString() == "iniziale",
                "Modifica nella vista: copia di lavoro scritta e Modified sollevato");
            Check(!editor.HasResults && editor.Result is null && sheet.Revision == 1, "Changed: risultati invalidati e nuova revisione");
            sheet.Note = "nota";
            editor.Commit();
            Check(editor.Data["nota"]!.ToString() == "nota" && modified == 2, "Commit attraverso ISheetEditor");
            sheet.Note = "prima del calcolo";
            await editor.CalculateAsync();
            Check(editor.Data["nota"]!.ToString() == "prima del calcolo" && editor.Result!["eco"]!.ToString() == "modificata" && modified == 3, "Calcola conferma le bozze");
            sheet.Note = "bozza";
            await editor.CalculateAsync(commitEdits: false);
            Check(editor.Data["nota"]!.ToString() == "prima del calcolo" && sheet.Note == "bozza" && editor.HasResults, "Calcolo senza conferma delle bozze");

            string? copy = null; JsonObject? soil = null; int reports = 0, sections = 0;
            editor.ModuleCopyRequested += (module, copied, name) => copy = module + "|" + name + "|" + copied["etichetta"];
            editor.SoilReplacementRequested += replaced => soil = replaced;
            editor.ReportRequested += () => reports++;
            editor.ConcreteSectionRequested += (_, _) => sections++;
            editor.SoilSources = () => [("Palo · Palo verticale", "geo_palo_verticale", new JsonObject(), "id1")];
            sheet.CopyCommand.Execute(null); sheet.ReplaceSoilCommand.Execute(null); sheet.ReportCommand.Execute(null); sheet.SourcesCommand.Execute(null);
            Check(copy == ProbeModule + "|Copia di prova|modificata" && soil?["terreno"]?.ToString() == "prova" && reports == 1 && sections == 0,
                "Comandi di ISheetHost tradotti negli eventi di ISheetEditor");
            Check(sheet.Sources.Single() is { Label: "Palo · Palo verticale", Module: "geo_palo_verticale", Id: "id1" }, "SoilSources letto da ISheetEditor");

            string exported = Path.Combine(directory, "risultato.json");
            editor.ExportResult(exported);
            Check(JsonNode.Parse(File.ReadAllText(exported))!["eco"]!.ToString() == "modificata", "ExportResult scrive il risultato");
            Check(Encoding.UTF8.GetString(editor.BuildReport("Titolo", ["b", "a"])) == "Titolo|a,b|False|modificata", "BuildReport dal report del foglio");
            Fails<InvalidOperationException>(() => editor.BuildShortReport("Breve"), "calcestruzzo armato", "Report short non disponibile");
            Fails<InvalidOperationException>(() => editor.MaterialReportContent(), ProbeModule, "Capitolo dei materiali non disponibile");

            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); view.UpdateLayout();
            SavePng(view, Path.Combine(directory, "foglio-mvvm.png"));
            int edits = modified;
            editor.InspectRevision(); view.UpdateLayout();
            Check(sheet.IsReadOnly && box.IsReadOnly, "InspectRevision: foglio e vista in sola lettura");
            box.Text = "vietata";
            Check(editor.Data["etichetta"]!.ToString() == "modificata" && modified == edits, "Sola lettura: la modifica della vista non raggiunge i dati");

            editor.Dispose(); editor.Dispose();
            Check(sheet.IsDisposed && ((ContentControl)view).Content is null, "Dispose chiude il foglio e libera la vista");

            // MainWindow opens the sheet through the factory it receives and handles it as a historic sheet.
            var window = new MainWindow(TestServices.Create, factory);
            try { await window.CheckMvvmSheet(Check); }
            finally { window.FinishSmoke(); window.Close(); }
        }
        finally { System.Windows.Application.Current.Resources.Remove(key); }
        log.Add($"PASS {log.Count} controlli dell'ospite MVVM");
        File.WriteAllLines(Path.Combine(directory, "test.txt"), log);
        File.WriteAllText(Path.Combine(directory, "esito.txt"), log[^1], new UTF8Encoding(false));
    }

    internal static IEnumerable<DependencyObject> Walk(DependencyObject root)
    {
        yield return root;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Walk(VisualTreeHelper.GetChild(root, i))) yield return child;
    }

    private static void SavePng(FrameworkElement view, string path)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth), (int)Math.Ceiling(view.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        var background = new DrawingVisual();
        using (var context = background.RenderOpen()) context.DrawRectangle(Brushes.White, null, new Rect(0, 0, view.ActualWidth, view.ActualHeight));
        bitmap.Render(background); bitmap.Render(view);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); png.Save(stream);
    }

    /// <summary>Engine of the probe in the WPF check: echoes the label, immediately.</summary>
    private sealed class EchoService : IModuleService
    {
        private int calls;
        public string ModuleId => ProbeModule;
        internal int Calls => Volatile.Read(ref calls);
        public JsonObject Prepare(JsonObject source) => (JsonObject)source.DeepClone();
        public ModuleCalculation Calculate(JsonObject source, CancellationToken cancellation = default)
        {
            Interlocked.Increment(ref calls);
            return new ModuleCalculation(ModuleId, source, new JsonObject { ["eco"] = source["etichetta"]?.DeepClone() });
        }
    }
}

public sealed partial class MainWindow
{
    /// <summary>The window opens the probe through its factory, marks the document modified at an edit and commits the working copy.</summary>
    internal async Task CheckMvvmSheet(Action<bool, string> check)
    {
        NewCalculation(ModuleHostWpfChecks.ProbeModule);
        check(editor is MvvmSheetEditor && ReferenceEquals(sheetContent.Content, editor.View), "MainWindow apre il foglio MVVM dalla fabbrica ricevuta");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var sheet = (ProbeSheetViewModel)((MvvmSheetEditor)editor!).Sheet;
        check(!dirty, "Documento nuovo non modificato");
        sheet.Label = "dalla finestra";
        check(dirty && Title.EndsWith(" *", StringComparison.Ordinal), "Modified: documento segnato come modificato");
        Commit();
        check(document["dati"]?["etichetta"]?.ToString() == "dalla finestra" && !ReferenceEquals(document["dati"], editor!.Data), "Commit della finestra: copia di lavoro nel documento");
    }
}
