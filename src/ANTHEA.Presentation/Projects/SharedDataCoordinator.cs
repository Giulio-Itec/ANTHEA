using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Calculations;
using Anthea.Presentation.Services;
using X.Core;

namespace Anthea.Presentation.Projects;

/// <summary>
/// Confirmation of the shared data of a project sheet (proposal F5, sections 2.1 and 4.4; step F5.2), moved without changes
/// from MainWindow.ConfirmSharedChanges: which shared keys and groups changed, whether the section controls descendant
/// sections (automatic propagation, no question), which linked sheets differ, the question «Aggiorna tutti i fogli
/// collegati» / «Solo questo foglio» through <see cref="IConfirmationService"/>, then <see cref="ProjectSharedData.ApplyHierarchy"/>
/// with the data of the sheet restored when the update fails. The window keeps the sheet baseline and the editor; this class
/// has no state of its own.
/// </summary>
public sealed class SharedDataCoordinator
{
    private readonly IConfirmationService confirmations;

    public SharedDataCoordinator(IConfirmationService confirmations)
    {
        ArgumentNullException.ThrowIfNull(confirmations);
        this.confirmations = confirmations;
    }

    /// <summary>
    /// Called when the open sheet is committed, before its data are written back. Nothing happens outside a project document,
    /// without a sheet in a section, without a baseline, when no shared key changed or when no linked sheet differs. When the
    /// section controls descendant sections the linked sheets are updated without asking; otherwise the user chooses. On
    /// update the proposed data are written into <paramref name="sheet"/>, the linked sheets are updated and
    /// <paramref name="updated"/> runs (the window marks the document as modified); if either fails, the data of
    /// <paramref name="sheet"/> are restored and the error is rethrown.
    /// </summary>
    /// <param name="document">The open document: only a project document ("tipo": "progetti") shares data.</param>
    /// <param name="sheet">The sheet being committed, inside the document; null when no sheet is open.</param>
    /// <param name="baseline">Copy of the sheet at its last confirmation; null when there is none.</param>
    /// <param name="proposed">Copy of the sheet with the data of the editor.</param>
    /// <param name="updated">Runs after the linked sheets have been updated, inside the rollback.</param>
    /// <returns>True when the linked sheets were updated.</returns>
    public bool ConfirmSharedChanges(JsonObject document, JsonObject? sheet, JsonObject? baseline, JsonObject proposed, Action updated)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(proposed);
        ArgumentNullException.ThrowIfNull(updated);
        if (document.S("tipo") != "progetti" || sheet?.Parent?.Parent is not JsonObject section || baseline is null) return false;
        var preview = ProjectSharedData.PreviewSection(section, sheet, proposed["dati"]!.AsObject());
        var keys = ProjectSharedData.ReferenceKeys(preview.Sheet, ProjectSharedData.ChangedKeys(baseline, proposed));
        var groups = ProjectSharedData.Fields(proposed).Values.Where(f => keys.Contains(f.Key)).Select(f => f.Group).ToHashSet();
        if (groups.Count == 0) return false;
        bool controlsDescendants = ProjectSharedData.Sections(section).Skip(1).Any(s => s.Array("fogli").Count > 0);
        var targets = ProjectSharedData.SubtreeSheets(section).Where(s => !ReferenceEquals(s, sheet) &&
            ProjectSharedData.Common(proposed, s).Any(p => keys.Contains(p.Source.Key) && !ProjectSharedData.Equal(p.Source.Value, p.Target.Value))).ToArray();
        if (targets.Length == 0) return false;
        // The prompt is built only when the question is asked, as before: naming a sheet may fail for an unknown module.
        bool update = controlsDescendants || confirmations.ConfirmSharedUpdate(Prompt(groups, section, targets, proposed, keys));
        if (!update) return false;
        var before = sheet["dati"]?.DeepClone();
        sheet["dati"] = proposed["dati"]!.DeepClone();
        try { ProjectSharedData.ApplyHierarchy(sheet, groups, keys); updated(); }
        catch { sheet["dati"] = before; throw; }
        return true;
    }

    /// <summary>The question about the linked sheets: groups in order of the fields, then each target with "label: before → after".</summary>
    private static SharedUpdatePrompt Prompt(HashSet<string> groups, JsonObject section, JsonObject[] targets, JsonObject proposed, HashSet<string> keys)
        => new(groups.ToList(), section.S("nome"),
            targets.Select(s => new SharedUpdateTarget(s.S("nome", ModuleDirectory.Get(s.S("modulo_id")).Name), ProjectSharedData.Common(proposed, s)
                .Where(p => keys.Contains(p.Source.Key) && !ProjectSharedData.Equal(p.Source.Value, p.Target.Value))
                .Select(p => FieldLabel(p.Source.Key) + ": " + ProjectSharedData.Text(p.Target.Value) + " → " + ProjectSharedData.Text(p.Source.Value)).ToList())).ToList());

    /// <summary>Label of a shared field in the questions and in the comparison (formerly MainWindow.SharedFieldLabel).</summary>
    public static string FieldLabel(string key) => ProjectReportPlan.Label(key);
}
