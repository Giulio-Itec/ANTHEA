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
    // F5.13 g3: --smoke-projects, --smoke-project-workspace, --smoke-hierarchy, --smoke-sharing, --smoke-project-report and --smoke-display are archived with
    // MainWindow (supporto/SUPERATI/ui-storica/shell); their suites on the new shell are ui/shell-smoke-*.
    private static readonly string[] SmokeFlags = ["--smoke-bridge-traffic", "--smoke-bridge-prices", "--smoke-section-actions", "--smoke", "--smoke-neutral-axis", "--smoke-horizontal", "--smoke-ca-features",
        "--smoke-ca-extensions", "--smoke-materials", "--smoke-bridge", "--smoke-bridge-predalle", "--smoke-bridge-curves",
        "--smoke-material-report", "--smoke-steel", "--smoke-bridge-design", "--smoke-retaining-wall", "--smoke-global-stability"];

    partial void RunTestHarness(StartupEventArgs e, ref bool handled)
    {
        // F5.4: the application as in the product, with the sheets of --module-impl (ModuleHostBench), to try a migrated module by hand.
        // F5.13 g3: the bench by hand opens the new shell (MainWindow is archived), with the same --module-impl pairs.
        if (e.Args.FirstOrDefault() == ModuleHostBench.Interactive) { handled = true; StartInteractiveShell(e); return; }
        // F5.13 c: the new shell by hand, through its composition root (ShellComposition); since F5.13 g the startup of the Release, here with the bench.
        if (e.Args.FirstOrDefault() == InteractiveShell) { handled = true; StartInteractiveShell(e); return; }
        if (!e.Args.Any(a => a.StartsWith("--check") || a.StartsWith("--smoke") || a == "--capture-validation")) return;
        // Every check runs with the light theme; the later production call is ignored (Initialize is idempotent).
        Appearance.Initialize(false);
        handled = true;
        if (e.Args.Length == 2 && e.Args[0] == "--check-spettri-contrast")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                int code = 0;
                try { await SpectraContrastChecks.Run(e.Args[1]); }
                catch (Exception ex) { Directory.CreateDirectory(e.Args[1]); File.WriteAllText(Path.Combine(e.Args[1], "errore.txt"), ex.ToString()); code = 1; }
                finally { Shutdown(code); }
            }));
            return;
        }
        if(e.Args.Length==3&&e.Args[0]=="--check-spettri-view"){
            ShutdownMode=ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(new Action(async()=>{int code=0;try{await SpectraViewChecks.Run(e.Args[1],e.Args[2]);}catch(Exception ex){Directory.CreateDirectory(e.Args[2]);File.WriteAllText(Path.Combine(e.Args[2],"errore.txt"),ex.ToString());code=1;}finally{Shutdown(code);}}));return;
        }
        if (e.Args.Length == 3 && e.Args[0] is "--check-f3-concrete-figures" or "--check-f3-concrete-figures-capture")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                int code = 0;
                try { await ConcreteSectionSmoke.CheckF3ConcreteFigures(e.Args[1], e.Args[2], e.Args[0].EndsWith("-capture")); }
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
                try { await F3FigureChecks.CheckF3Figures(e.Args[1], e.Args[2], e.Args[0].EndsWith("-capture")); }
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
                try { await F3SchedulingChecks.CheckF3Scheduling(e.Args[1], e.Args[2]); }
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

        // F5.6: the XAML view of the vertical pile and micropile hosted by MvvmSheetEditor, off screen (tests/Desktop/VerticalPileViewChecks.cs). The
        // traces of the historic view (--check-palo-verticale-historic) are archived with it in supporto/SUPERATI/ui-storica/palo-verticale (Q11).
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

        // F5.5: the XAML views of the materials hosted by MvvmSheetEditor, off screen (tests/Desktop/MaterialSheetViewChecks.cs). The traces
        // of the historic views (--check-materials-historic) are archived with them in supporto/SUPERATI/ui-storica/materiali (Q11).
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

        if (e.Args.Length == 3 && e.Args[0] == "--check-paratie-view")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                int code = 0;
                try { await EmbeddedWallViewChecks.Run(e.Args[1], e.Args[2]); }
                catch (Exception ex) { Directory.CreateDirectory(e.Args[2]); File.WriteAllText(Path.Combine(e.Args[2], "errore.txt"), ex.ToString()); code = 1; }
                finally { Shutdown(code); }
            }));
            return;
        }
        // F5.6b: the XAML view of the stratigraphy hosted by MvvmSheetEditor, off screen (tests/Desktop/StratigraphyViewChecks.cs). The traces of
        // the historic view (--check-stratigrafia-historic) are archived with it in supporto/SUPERATI/ui-storica/stratigrafia (Q11).
        if (e.Args.Length == 3 && e.Args[0] == "--check-stratigrafia-view")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                int code = 0;
                try { await StratigraphyViewChecks.Run(e.Args[1], e.Args[2]); }
                catch (Exception ex) { Directory.CreateDirectory(e.Args[2]); File.WriteAllText(Path.Combine(e.Args[2], "errore.txt"), ex.ToString()); code = 1; }
                finally { Shutdown(code); }
            }));
            return;
        }

        // F5.11: the XAML view of the composite bridge section hosted by MvvmSheetEditor, off screen (tests/Desktop/CompositeSectionViewChecks*.cs), as
        // the Release opens it since the switch of 23/10/2026 (the historic view and its command --check-sezione-mista-historic are archived in
        // supporto/SUPERATI/ui-storica/sezione-mista). A fourth argument (comma separated parts) runs only those parts, for the work on the view.
        if (e.Args.Length is 3 or 4 && e.Args[0] == "--check-sezione-mista-view")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                int code = 0;
                try { await CompositeSectionViewChecks.Run(e.Args[1], e.Args[2], e.Args.Length == 4 ? e.Args[3].Split(',') : null); }
                catch (Exception ex) { Directory.CreateDirectory(e.Args[2]); File.WriteAllText(Path.Combine(e.Args[2], "errore.txt"), ex.ToString()); code = 1; }
                finally { Shutdown(code); }
            }));
            return;
        }

        // F5.10 (view after S9): the XAML view of the RC section hosted by MvvmSheetEditor, off screen (tests/Desktop/ConcreteSectionViewChecks.cs); the
        // bench opens the section with the MVVM view, the Release keeps the historic one. A fourth argument (comma separated parts) runs only those parts.
        if (e.Args.Length is 3 or 4 && e.Args[0] == "--check-sezione-view")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                int code = 0;
                try { await ConcreteSectionViewChecks.Run(e.Args[1], e.Args[2], e.Args.Length == 4 ? e.Args[3].Split(',') : null); }
                catch (Exception ex) { Directory.CreateDirectory(e.Args[2]); File.WriteAllText(Path.Combine(e.Args[2], "errore.txt"), ex.ToString()); code = 1; }
                finally { Shutdown(code); }
            }));
            return;
        }

        // F5.9b: the XAML view of the shallow foundations hosted by MvvmSheetEditor, off screen (tests/Desktop/ShallowFoundationViewChecks.cs). The
        // traces of the historic view (--check-fondazioni-historic) are archived with it in supporto/SUPERATI/ui-storica/fondazioni (Q11).
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

        // F5.12: the XAML view of Bridge Design hosted by MvvmSheetEditor, off screen (tests/Desktop/BridgeDesignViewChecks*.cs), as the Release opens it
        // since the switch of 23/10/2026 (the historic view and its command --check-bridge-design-historic are archived in
        // supporto/SUPERATI/ui-storica/bridge-design). A fourth argument (comma separated parts) runs only those parts, for the work on the view.
        if (e.Args.Length is 3 or 4 && e.Args[0] == "--check-bridge-design-view")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                int code = 0;
                try { await BridgeDesignViewChecks.Run(e.Args[1], e.Args[2], e.Args.Length == 4 ? e.Args[3].Split(',') : null); }
                catch (Exception ex) { Directory.CreateDirectory(e.Args[2]); File.WriteAllText(Path.Combine(e.Args[2], "errore.txt"), ex.ToString()); code = 1; }
                finally { Shutdown(code); }
            }));
            return;
        }

        // F5.9: the XAML view of the retaining walls hosted by MvvmSheetEditor, off screen (tests/Desktop/RetainingWallViewChecks.cs); the bench
        // opens the walls with the MVVM view, the Release keeps the historic one until the user has tried it.
        if (e.Args.Length == 3 && e.Args[0] == "--check-muri-view")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                int code = 0;
                try { await RetainingWallViewChecks.Run(e.Args[1], e.Args[2]); }
                catch (Exception ex) { Directory.CreateDirectory(e.Args[2]); File.WriteAllText(Path.Combine(e.Args[2], "errore.txt"), ex.ToString()); code = 1; }
                finally { Shutdown(code); }
            }));
            return;
        }

        // F5.7/F5.8: the XAML views of the horizontal pile and micropile (capacity, elastic response) hosted by MvvmSheetEditor, off screen
        // (tests/Desktop/HorizontalPileViewChecks.cs); the bench opens the two modules with the MVVM view, the Release keeps the historic one
        // until the user has tried it. A fourth argument (comma separated parts) runs only those parts, for the work on the view.
        if (e.Args.Length is 3 or 4 && e.Args[0] == "--check-palo-orizzontale-view")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                int code = 0;
                try { await HorizontalPileViewChecks.Run(e.Args[1], e.Args[2], e.Args.Length == 4 ? e.Args[3] : null); }
                catch (Exception ex) { Directory.CreateDirectory(e.Args[2]); File.WriteAllText(Path.Combine(e.Args[2], "errore.txt"), ex.ToString()); code = 1; }
                finally { Shutdown(code); }
            }));
            return;
        }

        // f5-suite-ui: after the output, --module-impl pairs choose the MVVM views (ModuleHostBench; the suite ui/check-appearance-mvvm went away
        // with the Release switch of the walls, the last module of its bench).
        if (e.Args.Length >= 2 && e.Args[0] == "--check-appearance" && ModuleHostBench.WithoutBench(e.Args).Length == 2)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                int code = 0;
                // F5.13 g: on the new shell (ShellSmokeWindow), off screen.
                try { await ShellSmokeWindow.Run(e.Args[1], w => w.CheckAppearance(e.Args[1])); }
                catch (Exception ex) { Directory.CreateDirectory(e.Args[1]); File.WriteAllText(Path.Combine(e.Args[1], "errore.txt"), ex.ToString()); code = 1; }
                finally { Shutdown(code); }
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
        if (e.Args.Length == 2 && e.Args[0] is "--check-concrete-template-wizard" or "--check-examples")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.InvokeAsync(async () =>
            {
                int code = 0;
                try
                {
                    if (e.Args[0] == "--check-examples") await ExampleLibraryChecks.Run(e.Args[1]);
                    else await ConcreteTemplateViewChecks.Run(e.Args[1]);
                }
                catch (Exception ex) { Directory.CreateDirectory(e.Args[1]); File.WriteAllText(Path.Combine(e.Args[1], "errore.txt"), ex.ToString()); code = 1; }
                finally { Shutdown(code); }
            });
            return;
        }
        // F5.13 c: the draft of the layout of the new shell, off screen and never activated, for the user's checkpoint (tests/Desktop/ShellDraftChecks.cs).
        if (e.Args.Length == 2 && e.Args[0] == "--check-shell-draft")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.InvokeAsync(async () =>
            {
                int code = 0;
                try { await ShellDraftChecks.Run(e.Args[1]); }
                catch (Exception ex) { Directory.CreateDirectory(e.Args[1]); File.WriteAllText(Path.Combine(e.Args[1], "errore.txt"), ex.ToString()); code = 1; }
                finally { Shutdown(code); }
            });
            return;
        }
        // F5.13 e: the suites of MainWindow on the new shell, off screen (tests/Desktop/ShellSmoke*.cs); --module-impl pairs after the output choose the MVVM
        // views (ui/shell-smoke-<suite>-mvvm).
        if (e.Args.Length >= 2 && e.Args[0].StartsWith(ShellSmoke.Prefix, StringComparison.Ordinal) && ModuleHostBench.WithoutBench(e.Args).Length == 2)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.InvokeAsync(async () =>
            {
                int code = 0;
                try { await ShellSmoke.Run(e.Args[0], e.Args[1]); }
                catch (Exception ex) { Directory.CreateDirectory(e.Args[1]); File.WriteAllText(Path.Combine(e.Args[1], "errore.txt"), ex.ToString()); code = 1; }
                finally { Shutdown(code); }
            });
            return;
        }
        // F5.13 e: the capture of a project through the new shell and through MainWindow, compared (tests/Desktop/ShellProjectCapture.cs).
        if (e.Args.Length == 2 && e.Args[0] == ShellProjectCapture.Flag)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.InvokeAsync(async () =>
            {
                int code = 0;
                try { await ShellProjectCapture.Run(e.Args[1]); }
                catch (Exception ex) { Directory.CreateDirectory(e.Args[1]); File.WriteAllText(Path.Combine(e.Args[1], "errore.txt"), ex.ToString()); code = 1; }
                finally { Shutdown(code); }
            });
            return;
        }
        if (e.Args.Length == 2 && e.Args[0] == "--check-concrete-design") { StartConcreteDesign(e.Args[1]); return; }
        if (e.Args.Length == 3 && e.Args[0] == "--capture-validation") { StartValidationCapture(e.Args[1], e.Args[2]); return; }
        if (e.Args.Length >= 2 && SmokeFlags.Contains(e.Args[0])) { StartSmoke(e); return; }
        // Any other use of --check*/--smoke*: normal start, still with the light theme, as before.
        handled = false;
    }

    /// <summary>The flag of <see cref="StartInteractiveShell"/>.</summary>
    internal const string InteractiveShell = "--interactive-shell";

    /// <summary>
    /// ANTHEA.exe --interactive-shell [--wiki articolo | archivio [--models]] (UiTests only, F5.13 c): the new shell by hand, with the startup of
    /// App.OnStartup (appearance of the user, error log, the services of the product from DesktopComposition) and the composition root of the shell
    /// (<see cref="ShellComposition"/>): the modules open in tabs as the Release chooses them (since the night of 22-23/10, R5: the modules still
    /// historic in their SheetEditor), with the pairs <c>--module-impl modulo=mvvm|historic</c> of the bench; F1 and the guides open the Wiki tab, the
    /// rest of the command line as App.OnStartup reads it (StartupRequest). Since F5.13 g the startup of the Release (App.StartShell) with the choices
    /// of the bench.
    /// </summary>
    private void StartInteractiveShell(StartupEventArgs e)
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
        StartShell(ModuleHostBench.WithoutBench(e.Args).Skip(1).ToArray(), ModuleHostBench.Implementations(e.Args));
    }

    /// <summary>
    /// F5.13 g: the smokes passed to the new shell (docs/refactoring/f5-13-shell.md, 14.2), by flag: they run on the bench of the shell (ShellSmokeBench),
    /// never in a MainWindow, with the same flag, the same output and the same proof (esito-smoke-completo.txt).
    /// </summary>
    private static readonly Dictionary<string, Func<string[], Task>> ShellSmokes = new(StringComparer.Ordinal)
    {
        ["--smoke-global-stability"] = a => GlobalStabilitySmoke.Run(a[1]),
        ["--smoke-neutral-axis"] = a => NeutralAxisSmoke.Run(a[1]),
        ["--smoke-horizontal"] = a => ShellSmokeWindow.Run(a[1], w => w.SmokeHorizontal(a[1])),
        ["--smoke"] = a => ShellSmokeWindow.Run(a[1], async w => { await w.Smoke(a[1], JsonNode.Parse(File.ReadAllText(a[2]))!.AsArray()); await w.SmokeHorizontal(a[1]); }),
        ["--smoke-material-report"] = a => MaterialReportSmoke.Run(a[1]),
        ["--smoke-retaining-wall"] = a => ShellSmokeWindow.Run(a[1], w => w.SmokeRetainingWall(a[1])),
        ["--smoke-steel"] = a => ShellSmokeWindow.Run(a[1], w => w.SmokeSteel(a[1])),
        ["--smoke-materials"] = a => ShellSmokeWindow.Run(a[1], w => w.SmokeMaterials(a[1])),
        ["--smoke-bridge"] = a => ShellSmokeWindow.Run(a[1], w => w.SmokeBridge(a[1])),
        ["--smoke-bridge-predalle"] = a => ShellSmokeWindow.Run(a[1], w => w.SmokeBridgePredalle(a[1])),
        ["--smoke-bridge-curves"] = a => ShellSmokeWindow.Run(a[1], w => w.SmokeBridgeResponse(a[1])),
        ["--smoke-bridge-design"] = a => ShellSmokeWindow.Run(a[1], w => w.SmokeBridgeDesign(a[1])),
        ["--smoke-bridge-traffic"] = a => ShellSmokeWindow.Run(a[1], w => w.SmokeBridgeTraffic(a[1])),
        ["--smoke-bridge-prices"] = a => ShellSmokeWindow.Run(a[1], w => w.SmokeBridgePrices(a[1])),
        ["--smoke-section-actions"] = a => ConcreteSectionSmokeSuites.SectionActions(a[1]),
        ["--smoke-ca-extensions"] = a => ConcreteSectionSmokeSuites.Extensions(a[1]),
        ["--smoke-ca-features"] = a => ConcreteSectionSmokeSuites.Features(a[1]),
    };

    private void StartSmoke(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, error) =>
        {
            Directory.CreateDirectory(e.Args[1]); File.WriteAllText(Path.Combine(e.Args[1], "errore.txt"), error.Exception.ToString());
            error.Handled = true; Shutdown(1);
        };
        if (ShellSmokes.TryGetValue(e.Args[0], out var shellSmoke) && ModuleHostBench.WithoutBench(e.Args).Length == (e.Args[0] == "--smoke" ? 3 : 2))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            EventManager.RegisterClassHandler(typeof(FrameworkElement), System.Windows.Controls.ToolTipService.ToolTipOpeningEvent,
                new System.Windows.Controls.ToolTipEventHandler((_, args) => args.Handled = true));
            Dispatcher.InvokeAsync(async () =>
            {
                int code = 0;
                try
                {
                    await shellSmoke(ModuleHostBench.WithoutBench(e.Args));
                    Directory.CreateDirectory(e.Args[1]);
                    File.WriteAllText(Path.Combine(e.Args[1], "esito-smoke-completo.txt"), "Completato: " + e.Args[0]);
                }
                catch (Exception ex) { Directory.CreateDirectory(e.Args[1]); File.WriteAllText(Path.Combine(e.Args[1], "errore.txt"), ex.ToString()); code = 1; }
                finally { Shutdown(code); }
            });
            return;
        }
        // F5.13 g3: every smoke runs on the new shell (ShellSmokes); MainWindow is archived.
        Directory.CreateDirectory(e.Args[1]);
        File.WriteAllText(Path.Combine(e.Args[1], "errore.txt"), e.Args[0] + ": argomenti non validi per la suite sulla shell (ShellSmokes).");
        Shutdown(1);
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
                // The RC section with the XAML view in a window off screen (passaggio della Release, 23/10/2026; before, the historic ConcreteWorkspace).
                using (var view = ConcreteSectionSmoke.Open(data, 1500, 1000, "ANTHEA · Calcola armature"))
                {
                    await view.PrepareDesignSmoke();
                    await view.RunDesignSmoke(folder);
                }
                File.WriteAllText(Path.Combine(folder, "ui-pass.txt"), "PASS: eight tabs; alternatives/selection; apply; persistence; stale rejection; full/limited search; parallelism; timing diagnostics; large and compact layout.");
                Shutdown(0);
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(folder, "errore.txt"), ex.ToString()); Shutdown(1); }
        });
    }

    /// <summary>
    /// --capture-validation &lt;ingressi.json&gt; &lt;cartella&gt;: screenshots of the illustrated validation (formerly the App of
    /// ValidationIllustrations, unchanged): one PNG, input and result per case, capture-manifest.json, fatal.txt on unhandled errors. The composite
    /// bridge section opens with the XAML view of the Release since the switch of 23/10/2026 (BridgeSmoke, the members of the historic view
    /// BridgeWorkspace with the same names; the input written is the data of the sheet, as the historic view wrote the data it worked on).
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
                    FrameworkElement? view = null; BridgeSmoke? bridge = null; ConcreteSectionSmoke? section = null;
                    if(c.S("module")=="bridge")
                    {
                        var full=BridgeSection.Defaults();foreach(var (key,value) in data)full[key]=value?.DeepClone();data=full;
                        data["metodo_analisi"]=BridgeSection.CalculationMethods[(int)data.D("methodIndex")];
                        if(data["curve_sezione"] is JsonObject q){q["origine"]=BridgeSection.ResponseOrigins[(int)q.D("originIndex")];q["tipo"]=BridgeSection.ResponseModes[(int)q.D("typeIndex")];}
                        bridge=BridgeSmoke.Open(ModuleHostBench.SheetEditors(),data);view=bridge.View;
                    }
                    // The RC section with the XAML view in a window of its own, off screen (passaggio della Release, 23/10/2026).
                    else section=ConcreteSectionSmoke.Open(data,1500,950,"ANTHEA · Validazione · "+id);
                    content=bridge ?? (IDisposable?)section;
                    if(view is not null){win=new Window{Title="ANTHEA · Validazione · "+id,Width=1500,Height=950,Content=view,Background=Ui.Bg,ShowActivated=false};
                    win.Show();await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);}
                    if(section is { } ca){await ca.ValidationCapture(c,output,id);ca.Editor.Commit();data=ca.Data;}
                    else if(bridge is { } b)
                    {
                        await b.CalculateAsync();
                        if(c.B("curve")){b.Pages.SelectedIndex=2;await b.CalculateResponseAsync();b.ResponsePoint.Value=b.ResponseCalculation?.Points.Count-1??0;}
                        else{b.Pages.SelectedIndex=1;b.StageChoice.SelectedIndex=b.StageChoice.Items.Count-1;b.DisplayChoice.SelectedIndex=0;b.Results.SelectedIndex=(int)c.D("resultTab",3);}
                        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);win!.UpdateLayout();
                        File.WriteAllBytes(Path.Combine(output,id+".png"),Ui.Snapshot(win!));
                        File.WriteAllText(Path.Combine(output,id+"-actual.json"),(b.Result??new JsonObject()).ToJsonString());
                        data=b.Data;
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
