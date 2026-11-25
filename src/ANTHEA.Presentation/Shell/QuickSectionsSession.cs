using System.Text.Json.Nodes;
using Anthea.Application.Documents;
using Anthea.Calculations;
using X.Core;

namespace Anthea.Presentation.Shell;

/// <summary>One file and one save state for the sections of a quick module.</summary>
public sealed class QuickSectionsSession : DocumentSession
{
    private JsonObject? initialSection;
    public QuickSectionsSession(JsonObject document, string? path = null, bool dirty = false) : base(document, path, dirty)
    { QuickSectionDocuments.Validate(document); }

    public override DocumentKind Kind => DocumentKind.Sections;
    public string Module => Document.S("modulo_id");
    public IReadOnlyList<JsonObject> Sections => QuickSectionDocuments.Sections(Document);
    public string SelectedId => Document.S("attiva");
    internal void Select(string id) => Document["attiva"] = id;
    internal void Reorder(string moving, string target, bool after)
    { QuickSectionDocuments.Reorder(Document, moving, target, after); MarkDirty(); }

    internal void RememberInitialSection(JsonObject section, JsonObject data)
    {
        initialSection = (JsonObject)section.DeepClone();
        initialSection["dati"] = data.DeepClone();
    }

    internal bool IsInitialSection(JsonObject section) => Path is null && !IsSaving && Sections.Count == 1
        && initialSection is not null && ReferenceEquals(Sections[0], section) && JsonNode.DeepEquals(initialSection, section);
}
