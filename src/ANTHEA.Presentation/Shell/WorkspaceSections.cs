using System.Text.Json.Nodes;
using Anthea.Application.Documents;
using Anthea.Calculations;
using X.Core;

namespace Anthea.Presentation.Shell;

public sealed partial class WorkspaceSession
{
    private readonly List<QuickSectionsSession> sectionDocuments = [];

    public bool CanExportSection(SheetSession sheet) => !disposed && sheets.Contains(sheet) && !sheet.IsDisposed && !sheet.IsReadOnly
        && sheet.Owner is QuickSectionsSession { IsSaving: false };

    public bool CanImportSection(string module, SheetSession target) => CanExportSection(target) && target.Module == module;

    public string ExportSection(SheetSession sheet)
    {
        if (!CanExportSection(sheet)) throw new InvalidOperationException("La sezione non può essere trasferita in questo momento.");
        CommitActive();
        if (!ReferenceEquals(Active, sheet)) sheet.Commit();
        return QuickSectionTransfer.Export(sheet.Sheet);
    }

    public SheetSession ImportSection(string payload, SheetSession target, bool after)
    {
        if (!CanExportSection(target)) throw new InvalidOperationException("La raccolta di destinazione non può essere modificata in questo momento.");
        CommitActive();
        var group = (QuickSectionsSession)target.Owner;
        var node = QuickSectionTransfer.PrepareImport(payload, group.Document);
        // Build the editor before inserting anything: a rejected payload leaves the collection untouched.
        var imported = new SheetSession(group, node, factory, s => new Host(this, s));
        try { QuickSectionDocuments.Insert(group.Document, node, target.Id, after); }
        catch { imported.Dispose(); throw; }
        group.MarkDirty();
        return Add(imported);
    }

    /// <summary>Called only after the destination accepted the drop. Changed data stay at the source.</summary>
    public bool CompleteSectionExport(SheetSession sheet, string payload)
    {
        if (!CanExportSection(sheet)) return false;
        sheet.Commit();
        if (!QuickSectionTransfer.Matches(payload, sheet.Sheet)) return false;
        var group = (QuickSectionsSession)sheet.Owner;
        SheetSession? remaining = null;
        if (group.Sections.Count > 1 && !sheets.Any(s => ReferenceEquals(s.Owner, group) && !ReferenceEquals(s, sheet)))
        {
            var node = group.Sections.First(s => !ReferenceEquals(s, sheet.Sheet));
            remaining = new SheetSession(group, node, factory, s => new Host(this, s));
        }
        QuickSectionDocuments.RemoveTransferred(group.Document, sheet.Sheet);
        if (remaining is not null) Add(remaining);
        if (group.Sections.Count == 0)
        {
            sectionDocuments.Remove(group);
            group.ReleaseFile();
            DocumentsChanged?.Invoke(this, EventArgs.Empty);
        }
        else group.MarkDirty();
        Close(sheet, SheetCloseReason.Removed);
        if (Active is null && sheets.LastOrDefault(s => ReferenceEquals(s.Owner, group)) is { } next) Activate(next);
        return true;
    }

    private SheetSession NewQuickSection(string module)
    {
        CommitActive();
        var group = Active?.Owner is QuickSectionsSession current && current.Module == module ? current : null;
        bool initial = group is null;
        var document = group?.Document ?? QuickSectionDocuments.Create(module);
        int number = 1;
        var existing = group?.Sections ?? [];
        while (existing.Any(s => s.S("nome") == "Sezione " + number)) number++;
        var node = QuickSectionDocuments.Add(document, "Sezione " + number);
        if (group is null)
        {
            group = new QuickSectionsSession(document, dirty: true);
            sectionDocuments.Add(group);
            DocumentsChanged?.Invoke(this, EventArgs.Empty);
        }
        else group.MarkDirty();
        var sheet = Add(new SheetSession(group, node, factory, s => new Host(this, s)));
        if (initial) group.RememberInitialSection(sheet.Sheet, sheet.ViewModel.Data);
        return sheet;
    }

    private QuickSectionsSession OpenSections(JsonObject document, string? path, bool dirty)
    {
        CommitActive();
        var initial = InitialSectionToReplace();
        var group = new QuickSectionsSession(document, path, dirty);
        // Prepare every editor before publishing any tab: a bad section cannot open a partial file.
        var prepared = new List<SheetSession>();
        try
        {
            foreach (var node in group.Sections) prepared.Add(new SheetSession(group, node, factory, s => new Host(this, s)));
        }
        catch { foreach (var sheet in prepared) sheet.Dispose(); throw; }
        string selected = group.SelectedId;
        sectionDocuments.Add(group);
        DocumentsChanged?.Invoke(this, EventArgs.Empty);
        foreach (var sheet in prepared) Add(sheet);
        Activate(prepared.Single(s => s.Id == selected));
        ReplaceInitialSection(initial, group);
        return group;
    }

    private SheetSession? InitialSectionToReplace() => Active is { Owner: QuickSectionsSession group } sheet
        && group.IsInitialSection(sheet.Sheet) ? sheet : null;

    private void ReplaceInitialSection(SheetSession? initial, DocumentSession opened)
    {
        if (initial?.Owner is not QuickSectionsSession previous || opened is not QuickSectionsSession next
            || ReferenceEquals(previous, next) || previous.Module != next.Module || !previous.IsInitialSection(initial.Sheet)) return;
        // The new file is already open. Closing the empty starter cannot close the last module window or discard edited data.
        Close(initial, SheetCloseReason.DocumentClosed);
        sectionDocuments.Remove(previous);
        previous.ReleaseFile();
        DocumentsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CommitSections(DocumentSession document)
    {
        if (document is not QuickSectionsSession) return;
        foreach (var sheet in sheets.Where(s => ReferenceEquals(s.Owner, document) && !ReferenceEquals(s, Active)).ToArray()) sheet.Commit();
    }

    private bool CloseSection(SheetSession sheet, QuickSectionsSession group)
    {
        bool last = sheets.Count(s => ReferenceEquals(s.Owner, group)) == 1;
        if (last && !ConfirmDiscard(group)) return false;
        if (!last) sheet.Commit();
        Close(sheet, last ? SheetCloseReason.DocumentClosed : SheetCloseReason.Closed);
        if (!last && Active is null) Activate(sheets.Last(s => ReferenceEquals(s.Owner, group)));
        if (last)
        {
            sectionDocuments.Remove(group); group.ReleaseFile();
            DocumentsChanged?.Invoke(this, EventArgs.Empty);
        }
        return true;
    }

    private void ActivateSections(QuickSectionsSession group)
    {
        string selected = group.SelectedId;
        foreach (var node in group.Sections)
            if (!sheets.Any(s => ReferenceEquals(s.Owner, group) && ReferenceEquals(s.Sheet, node)))
                Add(new SheetSession(group, node, factory, s => new Host(this, s)));
        Activate(sheets.Single(s => ReferenceEquals(s.Owner, group) && s.Id == selected));
    }
}
