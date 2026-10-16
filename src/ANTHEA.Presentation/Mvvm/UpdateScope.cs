namespace Anthea.Presentation.Mvvm;

/// <summary>Scope that runs its end action once, at the first <see cref="Dispose"/> (deferred notifications, temporary
/// appearance). Moved from X.Desktop Ui.cs in F5.2; X.Desktop keeps the name through an alias.</summary>
public sealed class UpdateScope(Action end) : IDisposable
{
    private Action? end = end;
    public void Dispose() => Interlocked.Exchange(ref end, null)?.Invoke();
}
