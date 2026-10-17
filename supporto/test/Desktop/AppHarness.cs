using System.IO;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;
using X.Core;
using X.Desktop.Services;

namespace X.Desktop;

// UiTests configuration only (X.Desktop.csproj): command line of the WPF checks, moved out of App.cs.
// Protocol unchanged: errore.txt and exit code 1 on failure, esito-smoke-completo.txt at the end of every --smoke*.
public partial class App
{
    private static readonly string[] SmokeFlags = ["--smoke-bridge-traffic", "--smoke-bridge-prices", "--smoke-section-actions", "--smoke", "--smoke-neutral-axis", "--smoke-horizontal", "--smoke-display", "--smoke-ca-features",
        "--smoke-ca-extensions", "--smoke-projects", "--smoke-materials", "--smoke-bridge", "--smoke-bridge-predalle", "--smoke-bridge-curves",
        "--smoke-material-report", "--smoke-project-workspace", "--smoke-project-report", "--smoke-hierarchy", "--smoke-steel", "--smoke-sharing",
        "--smoke-bridge-design", "--smoke-retaining-wall", "--smoke-global-stability"];

    partial void RunTestHarness(StartupEventArgs e, ref bool handled)
    {
        // F5.4: the application as in the product, with the sheets of --module-impl (ModuleHostBench), to try a migrated module by hand.
        if (e.Args.FirstOrDefault() == ModuleHostBench.Interactive) { handled = true; StartInteractive(e); return; }
        if (!e.Args.Any(a => a.StartsWith("--check") || a.StartsWith("--smoke") || a == "--capture-validation")) return;
        // Every check runs with the light theme; the later production call is ignored (Initialize is idempotent).
        Appearance.Initialize(false);
        handled = true;
        if (e.Args.Length == 3 && e.Args[0] is "--check-f3-concrete-figures" or "--check-f3-concrete-figures-capture")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                int code = 0;
                try { await ConcreteWorkspace.CheckF3ConcreteFigures(e.Args[1], e.Args[2], e.Args[0].EndsWith("-capture")); }
                catch (Exception ex) { Directory.CreateDirectory(e.Args[2]); File.WriteAllText(Path.Combine(e.Args[2], "errore.txt"), ex.ToString()); code = 1; }
                finally { Shutdown(code); }
            }));
            return;
        }
        // F5.6: after root and output, --module-impl pairs choose the MVVM view of the vertical piles (ModuleHostBench).
        if (e.Args.Length >= 3 && e.Args[0] is "--check-f3-figures" or "--check-f3-figures-capture" && ModuleHostBench.WithoutBench(e.Args).Length == 3)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                int code = 0;
                try { await SheetEditor.CheckF3Figures(e.Args[1], e.Args[2], e.Args[0].EndsWith("-capture")); }
                catch (Exception ex) { Directory.CreateDirectory(e.Args[2]); File.WriteAllText(Path.Combine(e.Args[2], "errore.txt"), ex.ToString()); code = 1; }
                finally { Shutdown(code); }
            }));
            return;
        }
        if (e.Args.Length >= 3 && e.Args[0] == "--check-f3-report-chapters" && ModuleHostBench.WithoutBench(e.Args).Length == 3)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                int code = 0;
                try { await F3ReportChapterChecks.Run(e.Args[1], e.Args[2]); }
                catch (Exception ex) { Directory.CreateDirectory(e.Args[2]); File.WriteAllText(Path.Combine(e.Args[2], "errore.txt"), ex.ToString()); code = 1; }
                finally { Shutdown(code); }
            }));
            return;
        }
        if (e.Args.Length >= 3 && e.Args[0] == "--check-f3-scheduling" && ModuleHostBench.WithoutBench(e.Args).Length == 3)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                int code = 0;
                try { await SheetEditor.CheckF3Scheduling(e.Args[1], e.Args[2]); }
                catch (Exception ex) { Directory.CreateDirectory(e.Args[2]); File.WriteAllText(Path.Combine(e.Args[2], "errore.txt"), ex.ToString()); code = 1; }
                finally { Shutdown(code); }
            }));
            return;
        }
        // F5.2: after root and output, "--module-impl <module>=mvvm|historic" pairs choose the implementation of a module (ModuleHostBench).
        if (e.Args.Length >= 3 && e.Args[0] == "--check-f3-equivalence" && ModuleHostBench.WithoutBench(e.Args) is [_, var root, var capture])
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                int code = 0;
                try { await F3EquivalenceCapture.Run(root, capture); }
                catch (Exception ex) { Directory.CreateDirectory(capture); File.WriteAllText(Path.Combine(capture, "errore.txt"), ex.ToString()); code = 1; }
                finally { Shutdown(code); }
            }));
            return;
        }
        // F5.4: the XAML view of the pile group hosted by MvvmSheetEditor, off screen (tests/Desktop/PileGroupViewChecks.cs). The traces
        // of the historic view (--check-pile-group-historic) are archived with it in supporto/SUPERATI/ui-storica/palificata (Q11).
        if (e.Args.Length == 3 && e.Args[0] == "--check-pile-group-view")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                int code = 0;
                try { await PileGroupViewChecks.Run(e.Args[1], e.Args[2]); }
                catch (Exception ex) { Directory.CreateDirectory(e.Args[2]); File.WriteAllText(Path.Combine(e.Args[2], "errore.txt"), ex.ToString()); code = 1; }
                finally { Shutdown(code); }
            }));
            return;
        }

        // F5.6: the XAML view of the vertical pile and micropile hosted by MvvmSheetEditor, off screen (tests/Desktop/VerticalPileViewChecks.cs);
        // the bench opens the two modules with the MVVM view, the Release keeps the historic one until the user has tried it.
        if (e.Args.Length == 3 && e.Args[0] == "--check-palo-verticale-view")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                int code = 0;
                try { await VerticalPileViewChecks.Run(e.Args[1], e.Args[2]); }
                catch (Exception ex) { Directory.CreateDirectory(e.Args[2]); File.WriteAllText(Path.Combine(e.Args[2], "errore.txt"), ex.ToString()); code = 1; }
                finally { Shutdown(code); }
            }));
            return;
        }

        // F5.5: traces of the historic material views (tests/Desktop/MaterialHistoricTrace.cs); the check also compares them
        // with the frozen ones of tests/fixtures/f5-materiali/storica.
        if (e.Args.Length == 3 && e.Args[0] is "--check-materials-historic" or "--check-materials-historic-capture")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                int code = 0;
                try { await MaterialHistoricTrace.Run(e.Args[1], e.Args[2], e.Args[0] == "--check-materials-historic"); }
                catch (Exception ex) { Directory.CreateDirectory(e.Args[2]); File.WriteAllText(Path.Combine(e.Args[2], "errore.txt"), ex.ToString()); code = 1; }
                finally { Shutdown(code); }
            }));
            return;
        }
        // F5.5: the XAML views of the materials hosted by MvvmSheetEditor, off screen (tests/Desktop/MaterialSheetViewChecks.cs).
        if (e.Args.Length == 3 && e.Args[0] == "--check-materials-view")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                int code = 0;
                try { await MaterialSheetViewChecks.Run(e.Args[1], e.Args[2]); }
                catch (Exception ex) { Directory.CreateDirectory(e.Args[2]); File.WriteAllText(Path.Combine(e.Args[2], "errore.txt"), ex.ToString()); code = 1; }
                finally { Shutdown(code); }
            }));
            return;
        }

        // F5.7: traces and drawings of the historic horizontal pile and micropile view (tests/Desktop/HorizontalPileHistoricTrace.cs);
        // the check also compares them with the frozen ones of tests/fixtures/f5-palo-orizzontale.
        if (e.Args.Length == 3 && e.Args[0] is "--check-palo-orizzontale-historic" or "--check-palo-orizzontale-historic-capture")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                int code = 0;
                try { await HorizontalPileHistoricTrace.Run(e.Args[1], e.Args[2], e.Args[0] == "--check-palo-orizzontale-historic"); }
                catch (Exception ex) { Directory.CreateDirectory(e.Args[2]); File.WriteAllText(Path.Combine(e.Args[2], "errore.txt"), ex.ToString()); code = 1; }
                finally { Shutdown(code); }
            }));
            return;
        }

        // F5.8: traces and drawings of the historic view of the elastic response of the horizontal pile and of its reinforcement
        // (tests/Desktop/ElasticPileHistoricTrace.cs); the check also compares them with the frozen ones of tests/fixtures/f5-elastico.
        if (e.Args.Length == 3 && e.Args[0] is "--check-palo-elastico-historic" or "--check-palo-elastico-historic-capture")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                int code = 0;
                try { await ElasticPileHistoricTrace.Run(e.Args[1], e.Args[2], e.Args[0] == "--check-palo-elastico-historic"); }
                catch (Exception ex) { Directory.CreateDirectory(e.Args[2]); File.WriteAllText(Path.Combine(e.Args[2], "errore.txt"), ex.ToString()); code = 1; }
                finally { Shutdown(code); }
            }));
            return;
        }

        // F5.6: traces of the historic vertical pile and micropile view (tests/Desktop/VerticalPileHistoricTrace.cs); the check also
        // compares them with the frozen ones of tests/fixtures/f5-palo-verticale/storica.
        if (e.Args.Length == 3 && e.Args[0] is "--check-palo-verticale-historic" or "--check-palo-verticale-historic-capture")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                int code = 0;
                try { await VerticalPileHistoricTrace.Run(e.Args[1], e.Args[2], e.Args[0] == "--check-palo-verticale-historic"); }
                catch (Exception ex) { Directory.CreateDirectory(e.Args[2]); File.WriteAllText(Path.Combine(e.Args[2], "errore.txt"), ex.ToString()); code = 1; }
                finally { Shutdown(code); }
            }));
            return;
        }
        // F5.6b: traces of the historic stratigraphy view (tests/Desktop/StratigraphyHistoricTrace.cs); the check also compares them
        // with the frozen ones of tests/fixtures/f5-stratigrafia/storica.
        if (e.Args.Length == 3 && e.Args[0] is "--check-stratigrafia-historic" or "--check-stratigrafia-historic-capture")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                int code = 0;
                try { await StratigraphyHistoricTrace.Run(e.Args[1], e.Args[2], e.Args[0] == "--check-stratigrafia-historic"); }
                catch (Exception ex) { Directory.CreateDirectory(e.Args[2]); File.WriteAllText(Path.Combine(e.Args[2], "errore.txt"), ex.ToString()); code = 1; }
                finally { Shutdown(code); }
            }));
            return;
        }

        // F5.10: traces and drawings of the historic RC section view (tests/Desktop/ConcreteSectionHistoricTrace.cs); the check also
        // compares them with the frozen ones of tests/fixtures/f5-sezione.
        if (e.Args.Length == 3 && e.Args[0] is "--check-sezione-historic" or "--check-sezione-historic-capture")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                int code = 0;
                try { await ConcreteSectionHistoricTrace.Run(e.Args[1], e.Args[2], e.Args[0] == "--check-sezione-historic"); }
                catch (Exception ex) { Directory.CreateDirectory(e.Args[2]); File.WriteAllText(Path.Combine(e.Args[2], "errore.txt"), ex.ToString()); code = 1; }
                finally { Shutdown(code); }
            }));
            return;
        }

        // F5.9b: traces of the historic shallow foundation view (tests/Desktop/ShallowFoundationHistoricTrace.cs); the check also
        // compares them with the frozen ones of tests/fixtures/f5-fondazioni/storica.
        if (e.Args.Length == 3 && e.Args[0] is "--check-fondazioni-historic" or "--check-fondazioni-historic-capture")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                int code = 0;
                try { await ShallowFoundationHistoricTrace.Run(e.Args[1], e.Args[2], e.Args[0] == "--check-fondazioni-historic"); }
                catch (Exception ex) { Directory.CreateDirectory(e.Args[2]); File.WriteAllText(Path.Combine(e.Args[2], "errore.txt"), ex.ToString()); code = 1; }
                finally { Shutdown(code); }
            }));
            return;
        }
        // F5.9b: the XAML view of the shallow foundations hosted by MvvmSheetEditor, off screen (tests/Desktop/ShallowFoundationViewChecks.cs).
        if (e.Args.Length == 3 && e.Args[0] == "--check-fondazioni-view")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                int code = 0;
                try { await ShallowFoundationViewChecks.Run(e.Args[1], e.Args[2]); }
                catch (Exception ex) { Directory.CreateDirectory(e.Args[2]); File.WriteAllText(Path.Combine(e.Args[2], "errore.txt"), ex.ToString()); code = 1; }
                finally { Shutdown(code); }
            }));
            return;
        }

        // F5.9: traces of the historic retaining wall view (tests/Desktop/RetainingWallHistoricTrace.cs); the check also compares them
        // with the frozen ones of tests/fixtures/f5-muri/storica.
        if (e.Args.Length == 3 && e.Args[0] is "--check-muri-historic" or "--check-muri-historic-capture")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                int code = 0;
                try { await RetainingWallHistoricTrace.Run(e.Args[1], e.Args[2], e.Args[0] == "--check-muri-historic"); }
                catch (Exception ex) { Directory.CreateDirectory(e.Args[2]); File.WriteAllText(Path.Combine(e.Args[2], "errore.txt"), ex.ToString()); code = 1; }
                finally { Shutdown(code); }
            }));
            return;
        }

        if (e.Args.Length == 2 && e.Args[0] == "--check-appearance")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                int code = 0; MainWindow? testWindow = null;
                try { testWindow = new MainWindow(TestServices.Create); await testWindow.CheckAppearance(e.Args[1]); }
                catch (Exception ex) { Directory.CreateDirectory(e.Args[1]); File.WriteAllText(Path.Combine(e.Args[1], "errore.txt"), ex.ToString()); code = 1; }
                finally { testWindow?.FinishSmoke(); testWindow?.Close(); Shutdown(code); }
            }));
            return;
        }
        if (e.Args.Length >= 2 && e.Args[0] == "--check-contrast")
        {
            // Contrast of every view, window and popup in the three appearances, off screen (ContrastAudit.cs).
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                int code = 0;
                try { await ContrastAudit.Run(e.Args.Skip(1).ToArray()); }
                catch (Exception ex) { Directory.CreateDirectory(e.Args[1]); File.WriteAllText(Path.Combine(e.Args[1], "errore.txt"), ex.ToString()); code = 1; }
                finally { Shutdown(code); }
            }));
            return;
        }
        if (e.Args.Length == 2 && e.Args[0] == "--check-wiki-offscreen")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                int code = 0;
                try { await WikiChecks.Run(e.Args[1]); }
                catch (Exception ex) { Directory.CreateDirectory(e.Args[1]); File.WriteAllText(Path.Combine(e.Args[1], "errore.txt"), ex.ToString()); code = 1; }
                finally { File.WriteAllText(Path.Combine(e.Args[1], "exit-code.txt"), code.ToString()); Shutdown(code); }
            }));
            return;
        }
        if (e.Args.Length == 2 && e.Args[0] is "--check-global-guidance-offscreen" or "--check-wall-advanced-offscreen" or "--check-wall-materials-offscreen" or "--check-error-log-offscreen" or "--check-report-appearance-offscreen" or "--check-field-form-parity" or "--check-module-host-offscreen"
            or "--check-theme-tokens")
        {
            // No native window or input focus: render controls directly to bitmaps (the FieldForm parity check opens its own
            // windows outside the screen, never activated on purpose, for the real keyboard focus of the commit on focus loss).
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                int code = 0;
                try
                {
                    if (e.Args[0] == "--check-wall-materials-offscreen") await WallMaterialChecks.Run(e.Args[1]);
                    else if (e.Args[0] == "--check-wall-advanced-offscreen") await WallAdvancedChecks.Run(e.Args[1]);
                    else if (e.Args[0] == "--check-error-log-offscreen") CheckErrorLog(e.Args[1]);
                    else if (e.Args[0] == "--check-report-appearance-offscreen") await ReportAppearanceChecks.Run(e.Args[1]);
                    else if (e.Args[0] == "--check-field-form-parity") await FieldFormParityChecks.Run(e.Args[1]);
                    else if (e.Args[0] == "--check-module-host-offscreen") await ModuleHostWpfChecks.Run(e.Args[1]);
                    else if (e.Args[0] == "--check-theme-tokens") await ThemeTokenChecks.Run(e.Args[1]);
                    else await GlobalGuidanceChecks.Run(e.Args[1]);
                }
                catch (Exception ex) { Directory.CreateDirectory(e.Args[1]); File.WriteAllText(Path.Combine(e.Args[1], "errore.txt"), ex.ToString()); code = 1; }
                finally { Shutdown(code); }
            }));
            return;
        }
        if (e.Args.Length == 2 && e.Args[0] == "--check-concrete-design") { StartConcreteDesign(e.Args[1]); return; }
        if (e.Args.Length == 3 && e.Args[0] == "--capture-validation") { StartValidationCapture(e.Args[1], e.Args[2]); return; }
        if (e.Args.Length >= 2 && SmokeFlags.Contains(e.Args[0])) { StartSmoke(e); return; }
        // Any other use of --check*/--smoke*: normal start, still with the light theme, as before.
        handled = false;
    }

    /// <summary>
    /// ANTHEA.exe --interactive --module-impl &lt;modulo&gt;=mvvm [archivio] (UiTests only): the startup of App.OnStartup (appearance of the
    /// user, error log, composition root, services of the product) with the editors of the bench, so that a module migrated to MVVM can be
    /// tried by hand before its checkpoint. Never in Release.
    /// </summary>
    private void StartInteractive(StartupEventArgs e)
    {
        Appearance.Initialize(true);
        ErrorLog.Attach();
        DispatcherUnhandledException += (_, error) =>
        {
            ErrorLog.Write("Errore non gestito nell'interfaccia", error.Exception);
            MessageBox.Show(error.Exception.Message, "ANTHEA — errore", MessageBoxButton.OK, MessageBoxImage.Error);
            error.Handled = true;
        };
        Composition = new DesktopComposition(ActiveWindow);
        var window = new MainWindow(Composition.WindowServices, ModuleHostBench.SheetEditors(e.Args, Composition));
        MainWindow = window;
        if (ModuleHostBench.WithoutBench(e.Args).Skip(1).FirstOrDefault(a => !a.StartsWith("--")) is string path)
            window.Loaded += (_, _) => window.Safe(() => window.LoadFile(path));
        window.Show();
    }

    private void StartSmoke(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, error) =>
        {
            Directory.CreateDirectory(e.Args[1]); File.WriteAllText(Path.Combine(e.Args[1], "errore.txt"), error.Exception.ToString());
            error.Handled = true; Shutdown(1);
        };
        var window = new MainWindow(TestServices.Create);
        // Smoke windows are repeatedly created and destroyed under the desktop pointer.
        // Suppress tooltip popups in the harness to avoid WPF referring to an already closed HWND.
        EventManager.RegisterClassHandler(typeof(FrameworkElement), System.Windows.Controls.ToolTipService.ToolTipOpeningEvent,
            new System.Windows.Controls.ToolTipEventHandler((_, args) => args.Handled = true));
        MainWindow = window;
        if (e.Args.Length == (e.Args[0] == "--smoke" ? 3 : 2))
        {
            window.ContentRendered += RunSmoke;
            async void RunSmoke(object? sender, EventArgs args)
            {
                window.ContentRendered -= RunSmoke;
                int code = 0;
                try
                {
                    if (e.Args[0] == "--smoke-neutral-axis") await window.SmokeNeutralAxis(e.Args[1]);
                    else if (e.Args[0] == "--smoke-bridge-predalle") await window.SmokeBridgePredalle(e.Args[1]);
                    else if (e.Args[0] == "--smoke-retaining-wall") await window.SmokeRetainingWall(e.Args[1]);
                    else if (e.Args[0] == "--smoke-global-stability") await window.SmokeGlobalStability(e.Args[1]);
                    else if (e.Args[0] == "--smoke-bridge-design") await window.SmokeBridgeDesign(e.Args[1]);
                    else if (e.Args[0] == "--smoke-bridge-curves") await window.SmokeBridgeResponse(e.Args[1]);
                    else if (e.Args[0] == "--smoke-bridge") await window.SmokeBridge(e.Args[1]);
                    else if (e.Args[0] == "--smoke-material-report") await window.SmokeProjectReport(e.Args[1], materialsOnly: true);
                    else if (e.Args[0] == "--smoke-project-workspace") await window.SmokeProjectWorkspace(e.Args[1]);
                    else if (e.Args[0] == "--smoke-project-report") await window.SmokeProjectReport(e.Args[1]);
                    else if (e.Args[0] == "--smoke-hierarchy") await window.SmokeHierarchy(e.Args[1]);
                    else if (e.Args[0] == "--smoke-steel") await window.SmokeSteel(e.Args[1]);
                    else if (e.Args[0] == "--smoke-sharing") await window.SmokeSharing(e.Args[1]);
                    else if (e.Args[0] == "--smoke-materials") await window.SmokeMaterials(e.Args[1]);
                    else if (e.Args[0] == "--smoke-projects") await window.SmokeProjects(e.Args[1]);
                    else if (e.Args[0] == "--smoke-bridge-traffic") await window.SmokeBridgeTraffic(e.Args[1]);
                    else if (e.Args[0] == "--smoke-bridge-prices") await window.SmokeBridgePrices(e.Args[1]);
                    else if (e.Args[0] == "--smoke-section-actions") await window.SmokeSectionActions(e.Args[1]);
                    else if (e.Args[0] == "--smoke-ca-features") await window.SmokeConcreteFeatures(e.Args[1]);
                    else if (e.Args[0] == "--smoke-ca-extensions") await window.SmokeConcreteExtensions(e.Args[1]);
                    else if (e.Args[0] == "--smoke-display") await window.SmokeDisplay(e.Args[1]);
                    else if (e.Args[0] == "--smoke-horizontal") await window.SmokeHorizontal(e.Args[1]);
                    else { await window.Smoke(e.Args[1], JsonNode.Parse(File.ReadAllText(e.Args[2]))!.AsArray()); await window.SmokeHorizontal(e.Args[1]); }
                    Directory.CreateDirectory(e.Args[1]);
                    File.WriteAllText(Path.Combine(e.Args[1], "esito-smoke-completo.txt"), "Completato: " + e.Args[0]);
                }
                catch (Exception ex)
                {
                    Directory.CreateDirectory(e.Args[1]);
                    File.WriteAllText(Path.Combine(e.Args[1], "errore.txt"), ex.ToString());
                    code = 1;
                }
                finally { window.FinishSmoke(); Shutdown(code); }
            }
        }
        else if (e.Args.FirstOrDefault(a => !a.StartsWith("--")) is string path)
            window.Loaded += (_, _) => window.Safe(() => window.LoadFile(path));
        window.Show();
    }

    /// <summary>
    /// --check-concrete-design &lt;cartella&gt;: reinforcement search in a real window, on the ui-fixture.json written in the same
    /// folder by supporto/test/ConcreteDesign.Checks. Formerly the App of ConcreteDesign.DesktopChecks; failures now go
    /// to errore.txt (was ui-error.txt), success still writes ui-pass.txt.
    /// </summary>
    private void StartConcreteDesign(string folder)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "ui-start.json"), System.Text.Json.JsonSerializer.Serialize(new { started = DateTimeOffset.Now,
            assembly = typeof(App).Assembly.Location, revision = typeof(App).Assembly.ManifestModule.ModuleVersionId }));
        DispatcherUnhandledException += (_, err) => { File.WriteAllText(Path.Combine(folder, "errore.txt"), err.Exception.ToString()); err.Handled = true; Shutdown(1); };
        Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                var data = JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "ui-fixture.json")))!.AsObject();
                using var view = new ConcreteWorkspace(data); view.PrepareDesignSmoke();
                var window = new Window { Title = "ANTHEA · Calcola armature", Width = 1500, Height = 1000, Content = view, ShowActivated = false };
                window.Show(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                await view.RunDesignSmoke(window, folder);
                window.Close(); File.WriteAllText(Path.Combine(folder, "ui-pass.txt"), "PASS: eight tabs; alternatives/selection; apply; persistence; stale rejection; full/limited search; parallelism; timing diagnostics; large and compact layout.");
                Shutdown(0);
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(folder, "errore.txt"), ex.ToString()); Shutdown(1); }
        });
    }

    /// <summary>
    /// --capture-validation &lt;ingressi.json&gt; &lt;cartella&gt;: screenshots of the illustrated validation (formerly the App of
    /// ValidationIllustrations, unchanged): one PNG, input and result per case, capture-manifest.json, fatal.txt on unhandled errors.
    /// </summary>
    private void StartValidationCapture(string path, string output)
    {
        ShutdownMode=ShutdownMode.OnExplicitShutdown;
        Directory.CreateDirectory(output);
        DispatcherUnhandledException+=(_,err)=>{File.AppendAllText(Path.Combine(output,"fatal.txt"),err.Exception.ToString());err.Handled=true;Shutdown(1);};
        EventManager.RegisterClassHandler(typeof(FrameworkElement),System.Windows.Controls.ToolTipService.ToolTipOpeningEvent,new System.Windows.Controls.ToolTipEventHandler((_,a)=>a.Handled=true));
        Dispatcher.InvokeAsync(async ()=> {
            var manifest=new JsonArray();
            foreach(var c in JsonNode.Parse(File.ReadAllText(path))!.AsArray().OfType<JsonObject>())
            {
                string id=c.S("id"); Window? win=null; IDisposable? content=null;
                try
                {
                    File.AppendAllText(Path.Combine(output,"progress.log"),id+" start\n");
                    if(File.Exists(Path.Combine(output,id+".png")))continue;
                    var data=(JsonObject)c["data"]!.DeepClone();
                    FrameworkElement view;
                    if(c.S("module")=="bridge")
                    {
                        var full=BridgeSection.Defaults();foreach(var (key,value) in data)full[key]=value?.DeepClone();data=full;
                        data["metodo_analisi"]=BridgeSection.CalculationMethods[(int)data.D("methodIndex")];
                        if(data["curve_sezione"] is JsonObject q){q["origine"]=BridgeSection.ResponseOrigins[(int)q.D("originIndex")];q["tipo"]=BridgeSection.ResponseModes[(int)q.D("typeIndex")];}
                        view=new BridgeWorkspace(data);
                    }
                    else {var caView=new ConcreteWorkspace(data);caView.PrepareValidationCapture(c);view=caView;}
                    content=(IDisposable)view;
                    win=new Window{Title="ANTHEA · Validazione · "+id,Width=1500,Height=950,Content=view,Background=Ui.Bg,ShowActivated=false};
                    win.Show();await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    if(view is ConcreteWorkspace ca)await ca.ValidationCapture(c,output,id);
                    else if(view is BridgeWorkspace b)
                    {
                        await b.CalculateAsync();
                        if(c.B("curve")){b.Pages.SelectedIndex=2;await b.CalculateResponseAsync();b.ResponsePoint.Value=b.ResponseCalculation?.Points.Count-1??0;}
                        else{b.Pages.SelectedIndex=1;b.StageChoice.SelectedIndex=b.StageChoice.Items.Count-1;b.DisplayChoice.SelectedIndex=0;b.Results.SelectedIndex=(int)c.D("resultTab",3);}
                        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);win.UpdateLayout();
                        File.WriteAllBytes(Path.Combine(output,id+".png"),Ui.Snapshot(win));
                        File.WriteAllText(Path.Combine(output,id+"-actual.json"),(b.Result??new JsonObject()).ToJsonString());
                    }
                    File.WriteAllText(Path.Combine(output,id+"-input.json"),data.ToJsonString());
                    manifest.Add(J.Obj(("id",id),("ok",true),("note",c.S("note"))));
                    File.AppendAllText(Path.Combine(output,"progress.log"),id+" OK\n");
                }
                catch(Exception ex){File.WriteAllText(Path.Combine(output,id+"-error.txt"),ex.ToString());manifest.Add(J.Obj(("id",id),("ok",false)));}
                finally{content?.Dispose();win?.Close();}
                File.WriteAllText(Path.Combine(output,"capture-manifest.json"),manifest.ToJsonString());
            }
            Shutdown();
        });
    }

    /// <summary>--check-error-log-offscreen: the production log of the unhandled errors (ErrorLog), on a file of the output folder.</summary>
    private static void CheckErrorLog(string directory)
    {
        Directory.CreateDirectory(directory); var log = new List<string>();
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); log.Add("OK " + message); }
        string expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ANTHEA", "logs", "errori.log");
        Check(string.Equals(ErrorLog.DefaultPath, expected, StringComparison.OrdinalIgnoreCase), "Registro predefinito in %LOCALAPPDATA%\\ANTHEA\\logs\\errori.log");
        string file = Path.Combine(directory, "logs", "errori.log");
        Exception thrown;
        try { throw new InvalidOperationException("Prova del registro degli errori"); } catch (Exception ex) { thrown = ex; }
        ErrorLog.Write("Prova", thrown, file);
        string text = File.ReadAllText(file);
        Check(Regex.IsMatch(text, @"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} [+-]\d{2}:\d{2}\] Prova · ANTHEA "), "Data, ora, fuso e origine registrati");
        Check(text.Contains("System.InvalidOperationException: Prova del registro degli errori") && text.Contains(nameof(CheckErrorLog)), "Tipo, messaggio e stack registrati");
        ErrorLog.Write("Seconda prova", null, file);
        Check(File.ReadAllText(file).StartsWith(text) && File.ReadAllText(file).Contains("Seconda prova"), "Le voci si aggiungono senza sovrascrivere");
        File.AppendAllText(file, new string('x', (int)ErrorLog.MaxBytes));
        ErrorLog.Write("Dopo la rotazione", thrown, file);
        string rotated = Path.Combine(directory, "logs", "errori.1.log");
        Check(File.Exists(rotated) && new FileInfo(file).Length < ErrorLog.MaxBytes && File.ReadAllText(file).Contains("Dopo la rotazione"), "Rotazione oltre 1 MB in errori.1.log");
        ErrorLog.Write("Percorso non valido", thrown, Path.Combine(directory, "logs", "errori.log", "non-una-cartella", "errori.log"));
        Check(true, "Un registro non scrivibile non solleva eccezioni");
        log.Add($"PASS {log.Count} controlli del registro degli errori");
        File.WriteAllLines(Path.Combine(directory, "test.txt"), log);
    }
}
