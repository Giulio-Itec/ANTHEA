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
using Anthea.Presentation.Modules.PileGroup;
using Anthea.Presentation.Modules.Spectra;
using Anthea.Presentation.Modules.EmbeddedWalls;
using Anthea.Presentation.Modules.HorizontalPile;
using Anthea.Presentation.Modules.Materials;
using Anthea.Presentation.Tests;
using X.Core;
using X.Desktop.Services;

namespace X.Desktop;

/// <summary>
/// --check-module-host-offscreen &lt;cartella&gt; (UiTests, F5.2): the ModuleHost in the WPF layer, off screen. ModuleImplementations
/// lists exactly the modules of ModuleDirectory, the pile group MVVM since F5.4 and the others historic; the composition root opens
/// the editors of the Release; the bench argument chooses an MVVM implementation and fails clearly without one or for an archived
/// historic view; MvvmSheetEditor hosting the test-only probe module
/// (tests/Shared/ProbeSheetModule.cs, here in place of a migrated module) shows the view of its DataTemplate, raises Modified,
/// invalidates the results at a change, returns Data and Result and translates the ISheetHost commands into the ISheetEditor
/// events; MainWindow hosts it as a historic sheet. Writes test.txt, a PNG of the view and esito.txt («PASS n controlli»).
/// </summary>
internal static class ModuleHostWpfChecks
{
    /// <summary>The module the probe stands for: a small module of ModuleDirectory, with a sheet opened full size by MainWindow.</summary>
    internal const string ProbeModule = "mat_acciaio_armatura";

    /// <summary>The horizontal pile group, MVVM in the Release since the F5.4 checkpoint.</summary>
    private const string PileGroupModule = "geo_efficienza_orizzontale";
    private const string SpectraModule = "act_spettri_ntc";
    private const string EmbeddedWallModule = "geo_paratie";
    /// <summary>The horizontal pile and micropile, MVVM in the Release since 23/10/2026 (f5-pass-orizzontale).</summary>
    private const string HorizontalPileModule = "geo_palo_orizzontale", HorizontalMicropileModule = "geo_micropalo_orizzontale";
    /// <summary>The composite bridge section, MVVM in the Release since the switch of 23/10/2026 (F5.11).</summary>
    private const string CompositeSectionModule = "str_mista_ponte";
    /// <summary>Bridge Design, MVVM in the Release since the switch of 23/10/2026 (F5.12).</summary>
    private const string BridgeDesignModule = "str_bridge_design";
    private const string ConcreteModule = "mat_calcestruzzo";

    /// <summary>
    /// The modules MVVM in the Release, in ordinal order: the pile group (F5.4), Spectra and the embedded walls (new modules), the modules
    /// switched on 23/10 (passaggi della Release: materials, vertical and horizontal pile and micropile, stratigraphy, shallow foundations,
    /// composite bridge section, Bridge Design, RC section, retaining walls).
    /// </summary>
    private static readonly string[] ReleaseMvvm = [SpectraModule, PileGroupModule, "geo_fondazioni_superficiali", HorizontalMicropileModule, "geo_micropalo_verticale", RetainingWallModule, HorizontalPileModule, "geo_palo_verticale", EmbeddedWallModule, "geo_stratigrafia", ProbeModule, ConcreteModule, BridgeDesignModule, CompositeSectionModule, ConcreteSectionModule];
    /// <summary>The RC section, MVVM in the Release since the switch of 23/10/2026 (F5.10).</summary>
    private const string ConcreteSectionModule = "str_palo";

    /// <summary>The retaining walls, MVVM in the Release since 23/10/2026 (f5-pass-muri).</summary>
    private const string RetainingWallModule = "geo_muri_sostegno";

    private const string ProbeView = """
        <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
          <StackPanel Margin="12">
            <TextBox Text="{Binding Label, UpdateSourceTrigger=PropertyChanged}" IsReadOnly="{Binding IsReadOnly}" MinWidth="240" />
            <TextBlock Text="{Binding CalculationState}" />
          </StackPanel>
        </DataTemplate>
        """;

    /// <summary>The modules of the horizontal pile and micropile, MVVM in the Release: every registry of the bench has them.</summary>
    private static ISheetModule[] HorizontalModules() =>
        [new HorizontalPileSheetModule(HorizontalPileModule, new HorizontalPileFigureRenderer()), new HorizontalPileSheetModule(HorizontalMicropileModule, new HorizontalPileFigureRenderer())];

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

        // The versioned choice of the Release: exactly the modules of ModuleDirectory, in its order (F5.2); since the F5.4 checkpoint the
        // pile group with the MVVM view, since 23/10/2026 the horizontal pile and micropile; the new Spectra module starts in MVVM, the other modules keep the historic view.
        Check(ModuleImplementations.Declared.Select(d => d.Module).SequenceEqual(ModuleDirectory.All.Select(m => m.Id)),
            "ModuleImplementations elenca esattamente i moduli di ModuleDirectory, nel suo ordine");
        Check(ModuleImplementations.Declared.All(d => d.Implementation == (ReleaseMvvm.Contains(d.Module) ? SheetImplementation.Mvvm : SheetImplementation.Historic))
            && ModuleImplementations.Release.Mvvm.SequenceEqual(ReleaseMvvm),
            "Release: tutti i moduli con la vista MVVM (palificata, Spettri, paratie, materiali, pali e micropali verticali e orizzontali, stratigrafia, fondazioni, muri, sezione composta da ponte, Bridge Design e sezione c.a.), nessuno con la vista storica");

        // The composition root of the product: registry of ModuleDirectory with the MVVM pile group (F5.4), materials (F5.5) and vertical
        // piles (F5.6), the editors of the Release.
        var composition = new DesktopComposition(() => null);
        Check(composition.Modules.All.Select(d => d.Info).SequenceEqual(ModuleDirectory.All)
            && composition.Modules.SheetModuleIds.SequenceEqual(ModuleDirectory.All.Select(m => m.Id)
                .Where(m => m is "geo_efficienza_orizzontale" or "geo_palo_verticale" or "geo_micropalo_verticale" or "mat_calcestruzzo" or "mat_acciaio_armatura"
                    or "geo_fondazioni_superficiali" or "geo_palo_orizzontale" or "geo_micropalo_orizzontale" or "geo_stratigrafia" or "geo_muri_sostegno"
                    or "str_palo" or SpectraModule or EmbeddedWallModule
                    or "str_mista_ponte"
                    or "str_bridge_design")),
            "Radice di composizione: registro uguale a ModuleDirectory, con la palificata, i pali verticali e orizzontali, i materiali, le fondazioni, la stratigrafia, i muri e la sezione c.a. MVVM");
        using (var steel = composition.SheetEditors.Open(ProbeModule, ModuleDirectory.CreateData(ProbeModule)))
            Check(steel is MvvmSheetEditor { Sheet: RebarMaterialSheetViewModel }, "Radice di composizione: la Release apre l'acciaio (il modulo della sonda) con la vista MVVM");
        using (var concrete = composition.SheetEditors.Open(ConcreteModule, ModuleDirectory.CreateData(ConcreteModule)))
            Check(concrete is MvvmSheetEditor { Sheet: ConcreteMaterialSheetViewModel }, "Radice di composizione: la Release apre il calcestruzzo con la vista MVVM");
        using (var pileGroup = composition.SheetEditors.Open(PileGroupModule, ModuleDirectory.CreateData(PileGroupModule)))
            Check(pileGroup is MvvmSheetEditor { Sheet: PileGroupSheetViewModel { HasResults: true } }, "Radice di composizione: la Release apre la palificata con la vista MVVM");
        using (var wall = composition.SheetEditors.Open(EmbeddedWallModule, ModuleDirectory.CreateData(EmbeddedWallModule)))
            Check(wall is MvvmSheetEditor { Sheet: EmbeddedWallSheetViewModel }, "Radice di composizione: la Release apre paratie con la vista MVVM");
        using (var spectra = composition.SheetEditors.Open(SpectraModule, ModuleDirectory.CreateData(SpectraModule)))
            Check(spectra is MvvmSheetEditor { Sheet: SpectraSheetViewModel }, "Radice di composizione: la Release apre Spettri con la vista MVVM");
        foreach (string horizontal in new[] { HorizontalPileModule, HorizontalMicropileModule })
            using (var pile = composition.SheetEditors.Open(horizontal, ModuleDirectory.CreateData(horizontal)))
                Check(pile is MvvmSheetEditor { Sheet: HorizontalPileSheetViewModel }, "Radice di composizione: la Release apre " + horizontal + " con la vista MVVM");
        using (var composite = composition.SheetEditors.Open(CompositeSectionModule, ModuleDirectory.CreateData(CompositeSectionModule)))
            Check(composite is MvvmSheetEditor { Sheet: Anthea.Presentation.Modules.CompositeSection.CompositeSectionSheetViewModel },
                "Radice di composizione: la Release apre la sezione composta da ponte con la vista MVVM");
        using (var design = composition.SheetEditors.Open(BridgeDesignModule, ModuleDirectory.CreateData(BridgeDesignModule)))
            Check(design is MvvmSheetEditor { Sheet: Anthea.Presentation.Modules.BridgeDesign.BridgeDesignSheetViewModel },
                "Radice di composizione: la Release apre Bridge Design con la vista MVVM");
        using (var section = composition.SheetEditors.Open(ConcreteSectionModule, ModuleDirectory.CreateData(ConcreteSectionModule)))
            Check(section is MvvmSheetEditor { Sheet: Anthea.Presentation.Modules.ConcreteSection.ConcreteSheetViewModel },
                "Radice di composizione: la Release apre la sezione c.a. con la vista MVVM");
        using (var retaining = composition.SheetEditors.Open(RetainingWallModule, ModuleDirectory.CreateData(RetainingWallModule)))
            Check(retaining is MvvmSheetEditor { Sheet: Anthea.Presentation.Modules.RetainingWall.RetainingWallSheetViewModel }, "Radice di composizione: la Release apre i muri con la vista MVVM");
        Fails<ArgumentException>(() => composition.SheetEditors.Open("inesistente", new JsonObject()), "inesistente", "Modulo sconosciuto rifiutato come oggi");

        // The bench argument (UiTests only).
        Check(ModuleHostBench.WithoutBench(["--check-f3-equivalence", "radice", "uscita", ModuleHostBench.Argument, RetainingWallModule + "=mvvm"])
            .SequenceEqual(["--check-f3-equivalence", "radice", "uscita"]), "Banco: argomenti posizionali senza le coppie --module-impl");
        // Since 23/10/2026 no module is historic in the Release: a choice «=mvvm» leaves it as it is, every «=historic» is refused below.
        Check(ModuleHostBench.Implementations([ModuleHostBench.Argument, RetainingWallModule + "=mvvm"]).Mvvm.SequenceEqual(ReleaseMvvm)
            && ModuleHostBench.Implementations(["--check-f3-equivalence"]).Mvvm.SequenceEqual(ReleaseMvvm),
            "Banco: scelte applicate in ordine sulla Release");
        Fails<ArgumentException>(() => ModuleHostBench.Implementations([ModuleHostBench.Argument, PileGroupModule + "=historic"]), "archiviata",
            "Banco: vista storica della palificata archiviata (Q11), «historic» rifiutato");
        foreach (string horizontal in new[] { HorizontalPileModule, HorizontalMicropileModule })
            Fails<ArgumentException>(() => ModuleHostBench.Implementations([ModuleHostBench.Argument, horizontal + "=historic"]), "archiviata",
                "Banco: vista storica di " + horizontal + " archiviata (Q11), «historic» rifiutato");
        Fails<ArgumentException>(() => ModuleHostBench.Implementations([ModuleHostBench.Argument, CompositeSectionModule + "=historic"]), "archiviata",
            "Banco: vista storica della sezione composta da ponte archiviata (Q11), «historic» rifiutato");
        Fails<ArgumentException>(() => ModuleHostBench.Implementations([ModuleHostBench.Argument, BridgeDesignModule + "=historic"]), "archiviata",
            "Banco: vista storica di Bridge Design archiviata (Q11), «historic» rifiutato");
        Fails<ArgumentException>(() => ModuleHostBench.Implementations([ModuleHostBench.Argument, ConcreteModule + "=historic"]), "archiviata",
            "Banco: vista storica del calcestruzzo archiviata (Q11), «historic» rifiutato");
        Fails<ArgumentException>(() => ModuleHostBench.Implementations([ModuleHostBench.Argument, ConcreteSectionModule + "=historic"]), "archiviata",
            "Banco: vista storica della sezione c.a. archiviata (Q11), «historic» rifiutato");
        Fails<ArgumentException>(() => ModuleHostBench.Implementations([ModuleHostBench.Argument, RetainingWallModule + "=historic"]), "archiviata",
            "Banco: vista storica dei muri archiviata (Q11), «historic» rifiutato");
        // F5.10: the RC section has its ViewModel (S1); F5.11: the composite bridge section too; F5.12: Bridge Design too, so every module of the
        // directory has one: a registry without the module stands for a module without implementation.
        Fails<InvalidOperationException>(() => ModuleHostBench.SheetEditors([ModuleHostBench.Argument, "str_bridge_design=mvvm"],
            new ModuleRegistry([new PileGroupSheetModule(), new SpectraSheetModule(), new EmbeddedWallSheetModule(), .. HorizontalModules()])), "str_bridge_design",
            "Banco: modulo senza implementazione MVVM");
        Fails<ArgumentException>(() => ModuleHostBench.Implementations([ModuleHostBench.Argument, "inesistente=mvvm"]), "inesistente", "Banco: modulo sconosciuto");
        Fails<ArgumentException>(() => ModuleHostBench.Implementations([ModuleHostBench.Argument, "str_palo=nuova"]), "nuova", "Banco: implementazione sconosciuta");
        Fails<ArgumentException>(() => ModuleHostBench.Implementations([ModuleHostBench.Argument, "str_palo"]), "str_palo", "Banco: coppia incompleta");
        Fails<ArgumentException>(() => ModuleHostBench.Implementations([ModuleHostBench.Argument]), ModuleHostBench.Argument, "Banco: valore mancante");

        // The probe stands for a migrated module: registered for ProbeModule (the reinforcing steel, MVVM in the Release) and chosen with the
        // bench argument; the registry has the other modules of the composition root (the factory refuses a Release whose MVVM modules are not
        // registered).
        var probe = new ProbeSheetModule(ModuleDirectory.Get(ProbeModule));
        Fails<InvalidOperationException>(() => ModuleHostBench.SheetEditors([ModuleHostBench.Argument, ProbeModule + "=mvvm"], new ModuleRegistry([probe])), PileGroupModule,
            "Banco: registro senza la palificata, MVVM nella Release, rifiutato");
        var probeModules = new ModuleRegistry(DesktopComposition.CreateModules() is var defaults ? defaults.SheetModuleIds.Select(id => id == ProbeModule ? probe : defaults.SheetModule(id)) : []);
        var factory = ModuleHostBench.SheetEditors([ModuleHostBench.Argument, ProbeModule + "=mvvm"], probeModules);
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

            // The window of the application opens the sheet through its factory: since F5.13 g the new shell (ShellSmokeWindow, off screen) with the registry of
            // the probe, as MainWindow did with the factory it received.
            using (var window = await ShellSmokeWindow.OpenAsync(directory, probeModules)) await window.CheckMvvmSheet(Check);
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

internal sealed partial class ShellSmokeWindow
{
    /// <summary>The window opens the probe through its factory, marks the document modified at an edit and commits the working copy (F5.13 g: the shell).</summary>
    internal async Task CheckMvvmSheet(Action<bool, string> check)
    {
        NewCalculation(ModuleHostWpfChecks.ProbeModule);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout();
        check(SheetOf(editor) is ProbeSheetViewModel && Ui.Descendants<FrameworkElement>(Window).Any(e => e.DataContext is ProbeSheetViewModel),
            "La finestra apre il foglio MVVM dalla fabbrica ricevuta");
        var sheet = (ProbeSheetViewModel)SheetOf(editor)!;
        check(!dirty, "Documento nuovo non modificato");
        sheet.Label = "dalla finestra";
        check(dirty && Title.EndsWith(" *", StringComparison.Ordinal), "Modified: documento segnato come modificato");
        Commit();
        check(document["dati"]?["etichetta"]?.ToString() == "dalla finestra" && !ReferenceEquals(document["dati"], editor!.Data), "Commit della finestra: copia di lavoro nel documento");
    }
}
