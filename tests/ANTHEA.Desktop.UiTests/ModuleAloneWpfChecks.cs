using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Anthea.Application.Modules;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Shell;
using X.Desktop.Services;
using X.Desktop.Views.Shell;

namespace X.Desktop;

/// <summary>
/// A module alone on the taskbar (19/11/2026): the shortcut written with its target, «--modulo», the icon of the module and the identity of the
/// module for Windows; ANTHEA started on the module with the main window hidden, closed with the window of the module; «Riaggancia» shows the
/// main window.
/// </summary>
internal static class ModuleAloneWpfChecks
{
    internal static async Task Run(string directory, Action<bool, string> check)
    {
        // The shortcut, written in a folder of the check and read back through the shell of Windows.
        string folder = Path.Combine(directory, "collegamenti-moduli"), icons = Path.Combine(folder, "icone");
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
        string program = Path.Combine(AppContext.BaseDirectory, "ANTHEA.exe"), revealed = "";
        var shortcuts = new ModuleShortcuts(folder, icons, program, path => revealed = path);
        string name = ModuleDirectory.Get(SheetLayout.QuickSectionModule).Name;
        string link = shortcuts.Create(SheetLayout.QuickSectionModule, name);
        var read = ShellLinks.Read(link);
        check(File.Exists(link) && Path.GetFileName(link) == "ANTHEA · " + name + ".lnk" && revealed == link, "Collegamento del modulo: «ANTHEA · " + name + "», mostrato in Esplora file");
        check(string.Equals(read.Target, program, StringComparison.OrdinalIgnoreCase) && read.Arguments == "--modulo " + SheetLayout.QuickSectionModule,
            "Collegamento del modulo: ANTHEA.exe con --modulo (" + read.Arguments + ")");
        check(read.AppId == ModuleIdentity.AppUserModelId(SheetLayout.QuickSectionModule) && read.AppId == "ITEC.ANTHEA.Modulo.str_sezione_ca_rapida",
            "Collegamento del modulo: l'identità del modulo per la barra (" + read.AppId + ")");
        var decoder = new IconBitmapDecoder(new Uri(read.Icon), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        check(string.Equals(read.Icon, Path.Combine(icons, SheetLayout.QuickSectionModule + ".ico"), StringComparison.OrdinalIgnoreCase)
            && decoder.Frames.Single().PixelWidth == 256, "Collegamento del modulo: l'icona del modulo a 256 pixel");
        string again = shortcuts.Create(SheetLayout.QuickSectionModule, name);
        check(again == link && Directory.GetFiles(folder, "*.lnk").Length == 1, "Collegamento scritto di nuovo: lo stesso file");

        // ANTHEA on the module alone: the main window hidden, the module in its window with its icon; closing it closes ANTHEA.
        var (parts, mode) = Compose(SheetLayout.QuickSectionModule);
        mode.Start(parts, StartupRequest.Parse(["--modulo", SheetLayout.QuickSectionModule]));
        await Idle(parts.Window);
        var host = parts.Tracker.Hosts.SingleOrDefault();
        var window = Application.Current.Windows.OfType<DetachedShellWindow>().LastOrDefault();
        check(!parts.Window.IsVisible && host is not null && host.Tabs.Count == 1 && parts.Shell.Windows.Main.Tabs.Count == 0 && window is { IsVisible: true, Icon: not null },
            "Modulo da solo: la sua finestra con la sua icona, la finestra principale nascosta");
        bool mainClosed = false; parts.Window.Closed += (_, _) => mainClosed = true;
        window?.Close();
        await Idle(window!);
        check(mainClosed && window is { IsLoaded: false } && parts.Tracker.Hosts.Count == 0, "Modulo da solo: chiusa la sua finestra si chiude ANTHEA (principale chiusa " + mainClosed + ", finestra caricata " + window?.IsLoaded + ")");

        // «Riaggancia»: the tab goes to the main window, which shows itself.
        var (others, otherMode) = Compose("mat_calcestruzzo");
        otherMode.Start(others, StartupRequest.Parse(["--modulo", "mat_calcestruzzo"]));
        await Idle(others.Window);
        var tab = others.Shell.Windows.Tabs.Single();
        others.Shell.Windows.ReattachCommand.Execute(tab);
        await Idle(others.Window);
        check(others.Window.IsVisible && others.Tracker.Hosts.Count == 0 && others.Shell.Windows.Main.Tabs.Single() == tab, "Modulo da solo e «Riaggancia»: si apre ANTHEA con la scheda");
        others.Window.Close();
        await Idle(others.Window);
    }

    private static (ShellParts Parts, ModuleWindowMode Mode) Compose(string module)
    {
        var mode = new ModuleWindowMode(module);
        var services = new ShellDraftChecks.DraftServices();
        var parts = ShellComposition.Create(ShellComposition.WithShellWiki(services.Create()), DesktopComposition.CreateModules(), new CalculationLimiter(CalculationLimiter.DefaultMaximum),
            ShellComposition.ExamplesFolder, window => mode.Wrap(ShellDraftChecks.Offscreen(window)));
        ShellDraftChecks.Offscreen(parts.Window);
        return (parts, mode);
    }

    private static async Task Idle(Window window)
    {
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
    }
}
