namespace Anthea.Presentation.Services;

/// <summary>
/// The shortcut of a module (19/11/2026): ANTHEA started with «--modulo» on that module alone, with the icon of the module and its own place on
/// the taskbar of Windows. Windows lets no program pin itself: the desktop writes the shortcut in the Start menu and shows it, the user pins it
/// with the right click («Aggiungi alla barra delle applicazioni»).
/// </summary>
public interface IModuleShortcuts
{
    /// <summary>Writes (or writes again) the shortcut of <paramref name="module"/>, named after <paramref name="name"/>; returns its path.</summary>
    string Create(string module, string name);
}
