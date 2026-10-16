using CommunityToolkit.Mvvm.ComponentModel;

namespace Anthea.Presentation.Mvvm;

/// <summary>
/// Base of the ViewModels (proposal F5, section 3.1): CommunityToolkit <see cref="ObservableObject"/> plus a lifetime that
/// ends at <see cref="Dispose"/>. No reference to Dispatcher or Application.Current: a ViewModel is created on the UI
/// thread and the awaited work returns there through the captured synchronization context.
/// </summary>
public abstract class ViewModelBase : ObservableObject, IDisposable
{
    // Never disposed: it owns no timer or wait handle, and its token stays valid (cancelled) for late completions.
    private readonly CancellationTokenSource lifetime = new();
    private bool disposed;

    /// <summary>Cancelled when the ViewModel is disposed (sheet or dialog closed): link it to the work the ViewModel starts.</summary>
    protected CancellationToken Lifetime => lifetime.Token;

    /// <summary>True after <see cref="Dispose"/>; late completions must not change the state of a disposed ViewModel.</summary>
    public bool IsDisposed => disposed;

    /// <summary>Cancels the lifetime, then releases the resources of the derived ViewModel, once.</summary>
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try { lifetime.Cancel(); }
        finally
        {
            OnDisposed();
            OnPropertyChanged(nameof(IsDisposed));
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>Releases schedulers, subscriptions and child ViewModels. Called once, after the lifetime is cancelled.</summary>
    protected virtual void OnDisposed() { }

    /// <summary>Guard of the public operations that make no sense after closing.</summary>
    protected void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);
}
