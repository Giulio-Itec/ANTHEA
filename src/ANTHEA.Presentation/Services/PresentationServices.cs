namespace Anthea.Presentation.Services;

/// <summary>
/// The services of the presentation layer, created by hand in the composition root of the desktop (decision Q2 of 16/10/2026,
/// no container) and passed to the ViewModels by their constructors. The tests build it from scripted implementations.
/// </summary>
public sealed record PresentationServices(
    IMessageService Messages,
    IConfirmationService Confirmations,
    IFileDialogService Files,
    IDialogService Dialogs,
    IClipboardService Clipboard,
    IExternalLinkService Links,
    IWikiNavigator Wiki)
{
    public IMessageService Messages { get; } = Messages ?? throw new ArgumentNullException(nameof(Messages));
    public IConfirmationService Confirmations { get; } = Confirmations ?? throw new ArgumentNullException(nameof(Confirmations));
    public IFileDialogService Files { get; } = Files ?? throw new ArgumentNullException(nameof(Files));
    public IDialogService Dialogs { get; } = Dialogs ?? throw new ArgumentNullException(nameof(Dialogs));
    public IClipboardService Clipboard { get; } = Clipboard ?? throw new ArgumentNullException(nameof(Clipboard));
    public IExternalLinkService Links { get; } = Links ?? throw new ArgumentNullException(nameof(Links));
    public IWikiNavigator Wiki { get; } = Wiki ?? throw new ArgumentNullException(nameof(Wiki));
}
