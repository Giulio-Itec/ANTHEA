namespace X.Desktop.Services;

/// <summary>Services of a main window, defined once for the desktop (refactoring F1.4; F5 composes them with DI).</summary>
public sealed record DesktopServices(IConfirmationService Confirmations, IMessageService Messages);
