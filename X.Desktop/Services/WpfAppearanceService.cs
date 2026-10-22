using Anthea.Presentation.Shell.Commands;

namespace X.Desktop.Services;

/// <summary>
/// The appearance of the application for the new shell (F5.13 c, menu «Aspetto»): the same palette and the same saved preference of
/// MainWindow.BuildMenu (Appearance.Current, Appearance.Set).
/// </summary>
internal sealed class WpfAppearanceService : IAppearanceService
{
    public AppearanceChoice Current => Appearance.Current switch
    {
        AppAppearance.Dark => AppearanceChoice.Dark,
        AppAppearance.VeryDark => AppearanceChoice.VeryDark,
        _ => AppearanceChoice.Light
    };

    public void Apply(AppearanceChoice choice) => Appearance.Set(choice switch
    {
        AppearanceChoice.Dark => AppAppearance.Dark,
        AppearanceChoice.VeryDark => AppAppearance.VeryDark,
        _ => AppAppearance.Light
    });
}
