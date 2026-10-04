using System.IO;
using System.Text.Json.Nodes;
using System.Windows;

namespace X.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
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
        if (e.Args.Length == 2 && e.Args[0] is "--check-global-guidance-offscreen" or "--check-wall-advanced-offscreen" or "--check-wall-materials-offscreen")
        {
            // No native window or input focus: render controls directly to bitmaps.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                int code = 0;
                try { if (e.Args[0] == "--check-wall-materials-offscreen") await WallMaterialChecks.Run(e.Args[1]); else if (e.Args[0] == "--check-wall-advanced-offscreen") await WallAdvancedChecks.Run(e.Args[1]); else await GlobalGuidanceChecks.Run(e.Args[1]); }
                catch (Exception ex) { Directory.CreateDirectory(e.Args[1]); File.WriteAllText(Path.Combine(e.Args[1], "errore.txt"), ex.ToString()); code = 1; }
                finally { Shutdown(code); }
            }));
            return;
        }
        bool smoke = e.Args.Length >= 2 && e.Args[0] is "--smoke" or "--smoke-neutral-axis" or "--smoke-horizontal" or "--smoke-display" or "--smoke-ca-features" or "--smoke-ca-extensions" or "--smoke-projects" or "--smoke-materials" or "--smoke-bridge" or "--smoke-bridge-predalle" or "--smoke-bridge-curves" or "--smoke-material-report" or "--smoke-project-workspace" or "--smoke-project-report" or "--smoke-hierarchy" or "--smoke-steel" or "--smoke-sharing" or "--smoke-bridge-design" or "--smoke-retaining-wall" or "--smoke-global-stability";
        DispatcherUnhandledException += (_, error) =>
        {
            if (smoke)
            {
                Directory.CreateDirectory(e.Args[1]); File.WriteAllText(Path.Combine(e.Args[1], "errore.txt"), error.Exception.ToString());
                error.Handled = true; Shutdown(1); return;
            }
            MessageBox.Show(error.Exception.Message, "ANTHEA — errore", MessageBoxButton.OK, MessageBoxImage.Error);
            error.Handled = true;
        };
        var window = new MainWindow();
        // Smoke windows are repeatedly created and destroyed under the desktop pointer.
        // Suppress tooltip popups in the harness to avoid WPF referring to an already closed HWND.
        if (smoke) EventManager.RegisterClassHandler(typeof(FrameworkElement), System.Windows.Controls.ToolTipService.ToolTipOpeningEvent,
            new System.Windows.Controls.ToolTipEventHandler((_, args) => args.Handled = true));
        MainWindow = window;
        if (smoke && e.Args.Length == (e.Args[0] == "--smoke" ? 3 : 2))
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
        else if (e.Args.Length == 2 && e.Args[0] == "--wiki")
            window.Loaded += (_, _) => window.Safe(() => window.ShowWiki(e.Args[1]));
        else if (e.Args.FirstOrDefault(a => !a.StartsWith("--")) is string path)
            window.Loaded += (_, _) => window.Safe(() => window.LoadFile(path));
        window.Show();
    }
}
