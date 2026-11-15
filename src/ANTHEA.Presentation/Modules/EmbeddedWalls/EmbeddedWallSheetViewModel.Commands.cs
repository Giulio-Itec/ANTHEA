namespace Anthea.Presentation.Modules.EmbeddedWalls;

/// <summary>
/// Command bar and documents of the sheet (organizzazione del modulo paratie, progetto del 16/11, step 1): pages of commands grouped by
/// subject above the work area and the documents opened in the centre. The commands are those of the sheet; the pages and the documents are
/// state of the view, never saved.
/// </summary>
public sealed partial class EmbeddedWallSheetViewModel
{
    /// <summary>Keys of the pages of the command bar, in their order.</summary>
    public static IReadOnlyList<string> CommandPages { get; } = Array.AsReadOnly(new[]
        { "generale", "sezioni", "modello", "filtrazione", "stabilita", "analisi", "verifiche", "risultati", "vista" });

    private string commandPage = "modello";

    /// <summary>The page of the command bar on screen; an unknown key is ignored.</summary>
    public string CommandPage
    {
        get => commandPage;
        set { if (value is not null && CommandPages.Contains(value)) SetProperty(ref commandPage, value); }
    }

    /// <summary>The documents of the central area: the section of the phase and the views opened on request.</summary>
    public EmbeddedWallDocuments Documents { get; }
}
