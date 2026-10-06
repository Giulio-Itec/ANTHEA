using System.IO;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;

namespace X.Desktop;

// UiTests configuration only (X.Desktop.csproj): command line of the WPF checks, moved out of App.cs.
// Protocol unchanged: errore.txt and exit code 1 on failure, esito-smoke-completo.txt at the end of every --smoke*.
public partial class App
{
    private static readonly string[] SmokeFlags = ["--smoke", "--smoke-neutral-axis", "--smoke-horizontal", "--smoke-display", "--smoke-ca-features",
        "--smoke-ca-extensions", "--smoke-projects", "--smoke-materials", "--smoke-bridge", "--smoke-bridge-predalle", "--smoke-bridge-curves",
        "--smoke-material-report", "--smoke-project-workspace", "--smoke-project-report", "--smoke-hierarchy", "--smoke-steel", "--smoke-sharing",
        "--smoke-bridge-design", "--smoke-retaining-wall", "--smoke-global-stability"];

    partial void RunTestHarness(StartupEventArgs e, ref bool handled)
    {
        if (!e.Args.Any(a => a.StartsWith("--check") || a.StartsWith("--smoke"))) return;
        // Every check runs with the light theme; the later production call is ignored (Initialize is idempotent).
        Appearance.Initialize(false);
        handled = true;
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
        if (e.Args.Length == 2 && e.Args[0] is "--check-global-guidance-offscreen" or "--check-wall-advanced-offscreen" or "--check-wall-materials-offscreen" or "--check-error-log-offscreen")
        {
            // No native window or input focus: render controls directly to bitmaps.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                int code = 0;
                try
                {
                    if (e.Args[0] == "--check-wall-materials-offscreen") await WallMaterialChecks.Run(e.Args[1]);
                    else if (e.Args[0] == "--check-wall-advanced-offscreen") await WallAdvancedChecks.Run(e.Args[1]);
                    else if (e.Args[0] == "--check-error-log-offscreen") CheckErrorLog(e.Args[1]);
                    else await GlobalGuidanceChecks.Run(e.Args[1]);
                }
                catch (Exception ex) { Directory.CreateDirectory(e.Args[1]); File.WriteAllText(Path.Combine(e.Args[1], "errore.txt"), ex.ToString()); code = 1; }
                finally { Shutdown(code); }
            }));
            return;
        }
        if (e.Args.Length >= 2 && SmokeFlags.Contains(e.Args[0])) { StartSmoke(e); return; }
        // Any other use of --check*/--smoke*: normal start, still with the light theme, as before.
        handled = false;
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
