using System.Windows;

namespace X.Desktop.Services;

/// <summary>
/// Composition root of the desktop (refactoring F5.1, decision Q2 of 16/10/2026): every service of the presentation layer is
/// created here, by hand, without a container, and reaches the ViewModels through their constructors. The dialogs of the
/// application-wide services are owned by the active window, read at every call; the services of a main window
/// (<see cref="WindowServices"/>) stay owned by that window, as in F1.4.
/// </summary>
internal sealed class DesktopComposition
{
    internal DesktopComposition(Func<Window?> activeWindow)
    {
        ArgumentNullException.ThrowIfNull(activeWindow);
        Services = new PresentationServices(
            Messages: new WpfMessageService(activeWindow),
            Confirmations: new WpfConfirmationService(activeWindow),
            Files: new WpfFileDialogService(activeWindow),
            Dialogs: new WpfDialogService(activeWindow),
            Clipboard: new WpfClipboardService(),
            Links: new WpfExternalLinkService(),
            Wiki: new WpfWikiNavigator(activeWindow));
    }

    /// <summary>Application-wide services; no call site uses them yet (F5.1: no visible change).</summary>
    internal PresentationServices Services { get; }

    /// <summary>Factory of the services of a main window, unchanged since F1.4 (the WPF checks pass TestServices.Create instead).</summary>
    internal DesktopServices WindowServices(Window owner) => WpfDesktopServices.Create(owner);
}
