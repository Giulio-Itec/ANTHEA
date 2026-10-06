namespace X.Desktop.Services;

/// <summary>
/// Messages to the user. No WPF type in the signatures (see IConfirmationService).
/// Production: WpfMessageService; WPF checks: RethrowingMessages.
/// </summary>
public interface IMessageService
{
    /// <summary>A command failed: production shows the message of the error; the WPF checks rethrow it.</summary>
    void ReportFailure(string title, Exception error);
}
