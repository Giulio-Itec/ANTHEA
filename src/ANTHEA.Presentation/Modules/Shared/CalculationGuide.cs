using Anthea.Presentation.Services;
using Anthea.Wiki;

namespace Anthea.Presentation.Modules.Shared;

/// <summary>
/// «Modello e dati comuni…» (decision T5 of 17/10, V9 of F5.6, O7 of F5.7): the theory of the calculation of a module, a page of the
/// Wiki opened through <see cref="IWikiNavigator"/>. The historic CalculationHelpView.Show calls MainWindow.ShowContextualWikiTheory:
/// the first article related to the guide of the module that is a theory page of the module, otherwise the guide itself; nothing when
/// the module has no guide. (Outside the main window the historic button showed a text dialog; the sheets hosted by MainWindow never
/// reach that branch, and WpfWikiNavigator opens the Wiki of the main window.)
/// </summary>
public static class CalculationGuide
{
    /// <summary>Caption of the button of the sheets.</summary>
    public const string Caption = "Modello e dati comuni…";

    /// <summary>The Wiki page of <paramref name="module"/>, or null when the module has no guide.</summary>
    public static string? Target(string module)
    {
        var guide = WikiCatalog.ForModule(module);
        var target = guide?.Related.Select(WikiCatalog.Resolve).FirstOrDefault(a => a is not null && a.Type == "theory" && a.Modules.Contains(module)) ?? guide;
        return target?.Id;
    }

    /// <summary>Opens the page of <paramref name="module"/> with <paramref name="wiki"/>; nothing when there is none.</summary>
    public static void Open(IWikiNavigator wiki, string module)
    {
        ArgumentNullException.ThrowIfNull(wiki);
        if (Target(module) is { } target) wiki.Show(target);
    }
}
