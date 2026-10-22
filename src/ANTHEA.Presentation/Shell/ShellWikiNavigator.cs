using Anthea.Presentation.Services;

namespace Anthea.Presentation.Shell;

/// <summary>
/// The Wiki of the services in the new shell (F5.13 c, composition root): F1 of a field and the guides asked by a sheet open the Wiki tab of the
/// shell (Q18 a) instead of the historic window. The services exist before the shell, so the shell is given afterwards (<see cref="Attach"/>);
/// before it, or after it is disposed, a request goes to <paramref name="fallback"/> (the historic Wiki window) when there is one.
/// </summary>
public sealed class ShellWikiNavigator(IWikiNavigator? fallback = null) : IWikiNavigator
{
    private ShellViewModel? shell;

    /// <summary>The shell whose Wiki tab opens; once only.</summary>
    public void Attach(ShellViewModel target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (shell is not null && !ReferenceEquals(shell, target)) throw new InvalidOperationException("Wiki già collegata a una shell.");
        shell = target;
    }

    public void Show(string? uri = null)
    {
        if (shell is { IsDisposed: false } target) target.ShowWiki(uri);
        else fallback?.Show(uri);
    }
}
