namespace Anthea.Presentation.Services;

/// <summary>
/// Messages to the user (moved from X.Desktop/Services in F5.1, same signature of ReportFailure). No WPF type in the
/// signatures: the owner window belongs to the implementation. Production: WpfMessageService; WPF checks: RethrowingMessages;
/// Presentation.Tests: scripted messages.
/// </summary>
public interface IMessageService
{
    /// <summary>A command failed: production shows the message of the error; the WPF checks rethrow it.</summary>
    void ReportFailure(string title, Exception error);

    /// <summary>A note with the OK button and no icon, as the plain message boxes of the views today (empty title allowed).</summary>
    void Inform(string title, string message);

    /// <summary>A warning with the OK button and the warning icon (for example an export that could not be written).</summary>
    void Warn(string title, string message);
}
