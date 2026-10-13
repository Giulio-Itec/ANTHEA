namespace Anthea.Application.Presentation;

/// <summary>Textual projection for a property panel, independent of controls and document layout.</summary>
public sealed record PresentationTable(string Title, string[] Columns, string[][] Rows, string? Note = null, bool Collapsible = false);
