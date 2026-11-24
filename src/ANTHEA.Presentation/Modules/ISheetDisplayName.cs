namespace Anthea.Presentation.Modules;

/// <summary>An optional name edited inside a sheet. Null keeps the document's existing label; revisions still add their suffix.</summary>
public interface ISheetDisplayName
{
    string? DisplayName { get; }
}
