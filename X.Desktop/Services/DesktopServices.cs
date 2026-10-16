namespace X.Desktop.Services;

/// <summary>
/// Services of a main window, defined once for the desktop (refactoring F1.4). The interfaces moved to
/// Anthea.Presentation.Services in F5.1; the composition root (DesktopComposition) creates these per window, by hand.
/// </summary>
public sealed record DesktopServices(IConfirmationService Confirmations, IMessageService Messages);
