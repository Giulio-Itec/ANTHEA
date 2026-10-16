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

    /// <summary>
    /// Preview of a move into another section (formerly MainWindow.PreviewProjectMove): the node is moved in a copy of the
    /// document, its sheets inherit the common data of the destination and its ancestors, and each changed value becomes
    /// "sheet · field: before → after". When there are changes the user confirms («Sposta e aggiorna» / «Annulla»); without
    /// changes nobody is asked. The document is never modified: the window moves the live node and copies the data.
    /// </summary>
    /// <param name="document">The open project document.</param>
    /// <param name="node">The sheet or section being moved, inside the document.</param>
    /// <param name="destination">The destination section, inside the document.</param>
    /// <returns>The moved node inside the copy, with the inherited data; null when the user cancels.</returns>
    public JsonObject? PreviewMove(JsonObject document, JsonObject node, JsonObject destination)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(destination);
        var copy = (JsonObject)document.DeepClone();
        var proposed = ProjectRevisions.Find(copy, node.S("id"))!;
        var target = ProjectRevisions.Find(copy, destination.S("id"))!;
        ((JsonArray)proposed.Parent!).Remove(proposed);
        string list = node.ContainsKey("modulo_id") ? "fogli" : "strutture";
        if (target[list] is not JsonArray) target[list] = new JsonArray(); target.Array(list).Add(proposed);
        var sheets = proposed.ContainsKey("modulo_id") ? new[] { proposed } : ProjectSharedData.SubtreeSheets(proposed).ToArray();
        var changes = new List<string>();
        foreach (var sheet in sheets)
        {
            var before = ProjectSharedData.Fields(sheet);
            ProjectSharedData.InheritHierarchy(sheet, sheet.Parent!.Parent!.AsObject());
            var after = ProjectSharedData.Fields(sheet);
            foreach (var key in before.Keys.Intersect(after.Keys).Where(k => !ProjectSharedData.Equal(before[k].Value, after[k].Value)))
                changes.Add(sheet.S("nome") + " · " + FieldLabel(key) + ": " + ProjectSharedData.Text(before[key].Value) + " → " + ProjectSharedData.Text(after[key].Value));
        }
        if (changes.Count > 0 && !confirmations.ConfirmProjectMove(destination.S("nome"), changes)) return null;
        return proposed;
    }

    /// <summary>The question about the linked sheets: groups in order of the fields, then each target with "label: before → after".</summary>
    private static SharedUpdatePrompt Prompt(HashSet<string> groups, JsonObject section, JsonObject[] targets, JsonObject proposed, HashSet<string> keys)
        => new(groups.ToList(), section.S("nome"),
            targets.Select(s => new SharedUpdateTarget(s.S("nome", ModuleDirectory.Get(s.S("modulo_id")).Name), ProjectSharedData.Common(proposed, s)
                .Where(p => keys.Contains(p.Source.Key) && !ProjectSharedData.Equal(p.Source.Value, p.Target.Value))
                .Select(p => FieldLabel(p.Source.Key) + ": " + ProjectSharedData.Text(p.Target.Value) + " → " + ProjectSharedData.Text(p.Source.Value)).ToList())).ToList());

    /// <summary>Label of a shared field in the questions and in the comparison (formerly MainWindow.SharedFieldLabel).</summary>
    public static string FieldLabel(string key) => ProjectReportPlan.Label(key);

    /// <summary>
    /// «Uniforma a questo» in the comparison of a section (formerly in MainWindow.ShowCoherence): a soil type chosen in the
    /// dialog and different from the sheet is written into the sheet first; then the field is applied from the sheet to its
    /// section and subsections (<see cref="ProjectSharedData.ApplyHierarchy"/>).
    /// </summary>
    /// <param name="sheet">The sheet whose value is kept.</param>
    /// <param name="key">The shared field in conflict.</param>
    /// <param name="group">Group of the field in the conflict.</param>
    /// <param name="soilType">Soil type chosen in the dialog for a «tipologia» conflict; null otherwise.</param>
    /// <returns>True when the document changed: the window marks it modified and reloads the open sheet.</returns>
    public static bool Align(JsonObject sheet, string key, string group, string? soilType)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        bool sourceChanged = soilType is not null && ProjectSharedData.Fields(sheet)[key].Value?.ToString() != soilType;
        if (sourceChanged) ProjectSharedData.SetSoilType(sheet, key, soilType!);
        int count = ProjectSharedData.ApplyHierarchy(sheet, new HashSet<string> { group }, new HashSet<string> { key });
        return count > 0 || sourceChanged;
    }

    /// <summary>
    /// «Uniforma a questo» for a layer missing in other sheets of the section (formerly in MainWindow.ShowCoherence): the soil
    /// type chosen in the dialog is written into the source sheet, then the layer and the missing layers before it are copied,
    /// common data only, into the other sheets of its section.
    /// </summary>
    /// <returns>True when layers were copied: the window marks the document modified and reloads the open sheet.</returns>
    public static bool CopyMissingLayers(JsonObject sheet, string typeKey, int survey, int layer, string? soilType)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        if (soilType is not null) ProjectSharedData.SetSoilType(sheet, typeKey, soilType);
        return ProjectSharedData.CopyMissingSoilLayers(sheet, sheet.Parent!.Parent!.AsObject(), survey, layer) > 0;
    }

    /// <summary>
    /// Badge of a section in the project overview (formerly decided in MainWindow.AddCoherenceBadge): not shown without sheets,
    /// nor when nothing is comparable and there are no warnings; otherwise conflicts (differences or missing layers), coherent
    /// common data and the number of warnings.
    /// </summary>
    public static CoherenceBadge? Coherence(JsonObject section)
    {
        ArgumentNullException.ThrowIfNull(section);
        if (!ProjectSharedData.SubtreeSheets(section).Any()) return null;
        var comparison = new ProjectComparison(section);
        var differences = comparison.Differences;
        bool comparable = comparison.Matches.Count > 0;
        var warnings = ProjectValidation.Warnings(section);
        if (!comparable && warnings.Length == 0) return null;
        int missingLayers = ProjectSharedData.MissingSoilLayers(section).Count;
        bool hasConflicts = differences.Count > 0 || missingLayers > 0;
        string title = hasConflicts ? "⚠ Differenze tra fogli" : comparable ? "✓ Dati comuni coerenti" : "";
        if (warnings.Length > 0) title += (title.Length > 0 ? " · " : "") + $"⚠ {warnings.Length} {(warnings.Length == 1 ? "avviso" : "avvisi")}";
        bool warning = hasConflicts || warnings.Length > 0;
        string content = warning ? (differences.Select(d => d.Key).Distinct().Count() + missingLayers + warnings.Length).ToString() : "✓";
        string toolTip = warnings.Length == 0 ? "Apri il confronto dei dati comuni della sezione." :
            "Apri il confronto e gli avvisi della sezione.\n\n" + string.Join("\n\n", warnings);
        return new(title, content, warning, toolTip);
    }
}

/// <summary>Badge of a section: automation name and tooltip of the button, its content (number or «✓») and its warning colour.</summary>
public sealed record CoherenceBadge(string Title, string Content, bool Warning, string ToolTip);

/// <summary>
/// Link of the stratigraphies of two sheets (formerly in MainWindow.ShowCoherence): staged on copies for the preview, written
/// into the two sheets only by <see cref="Apply"/>, after the user confirmed.
/// </summary>
public sealed class SoilLink
{
    private readonly JsonObject source, target;

    private SoilLink(JsonObject source, JsonObject target, JsonObject stagedSource, JsonObject stagedTarget)
    {
        this.source = source; this.target = target; StagedSource = stagedSource; StagedTarget = stagedTarget;
    }

    /// <summary>The reference sheet after the link (a copy).</summary>
    public JsonObject StagedSource { get; }

    /// <summary>The linked sheet after the link (a copy): its stratigraphies are shown in the preview.</summary>
    public JsonObject StagedTarget { get; }

    /// <summary>Links copies of the two sheets (<see cref="ProjectSharedData.LinkSoils"/>); the sheets do not change.</summary>
    public static SoilLink Stage(JsonObject source, JsonObject target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        var stagedSource = (JsonObject)source.DeepClone(); var stagedTarget = (JsonObject)target.DeepClone();
        ProjectSharedData.LinkSoils(stagedSource, stagedTarget);
        return new(source, target, stagedSource, stagedTarget);
    }

    /// <summary>Writes the linked data into the two sheets.</summary>
    public void Apply()
    {
        source["dati"] = StagedSource["dati"]!.DeepClone(); target["dati"] = StagedTarget["dati"]!.DeepClone();
    }
}
