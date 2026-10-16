namespace Anthea.Presentation.Services;

/// <summary>Clipboard text (copy of tables and properties, paste of actions). Production: WpfClipboardService.</summary>
public interface IClipboardService
{
    void SetText(string text);

    /// <summary>Text on the clipboard, empty when there is none.</summary>
    string GetText();
}

/// <summary>External links (references, web pages of the Wiki) opened by the system. Production: WpfExternalLinkService.</summary>
public interface IExternalLinkService
{
    /// <summary>Opens an absolute URI with the default program of the system.</summary>
    void Open(Uri target);
}

/// <summary>
/// Guide of the module, topics, examples and F1 (proposal F5, section 3.3): opens the Wiki by URI or article key, for example
/// "sezione-in-calcestruzzo-armato#apertura-delle-fessure"; null opens the home of the Wiki. It replaces the search of the
/// main window along the owners of the windows. Production: WpfWikiNavigator.
/// </summary>
public interface IWikiNavigator
{
    void Show(string? uri = null);
}
