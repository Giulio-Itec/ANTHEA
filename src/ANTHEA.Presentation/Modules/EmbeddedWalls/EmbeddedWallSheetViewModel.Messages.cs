using Anthea.Presentation.Calculation;

namespace Anthea.Presentation.Modules.EmbeddedWalls;

/// <summary>
/// The panel Messaggi of the sheet (organizzazione del modulo paratie, progetto del 16/11, step 11, S16 and R06). Its lines are only what
/// ANTHEA and GPC already produced, each with its provenance:
/// <list type="bullet">
/// <item>the request (C5): its progress, with the case and the phase of the structured progress of the module (no text read back), and its final
/// state; with the error of the engine or the missing data and the warnings of the last calculation. All of them only while no change of the
/// data has superseded the request;</item>
/// <item>the results on screen: the diagnostics of the snapshot, of every incomplete case and of every phase whose filtration did not converge;</item>
/// <item>the data drawn: the notices of the section of the phase, each with the row it comes from.</item>
/// </list>
/// A line with a target opens it: the case and the phase, then the row of its table (as the tree does).
/// </summary>
public sealed partial class EmbeddedWallSheetViewModel
{
    private EmbeddedWallMessageDraft? requestMessage;
    private bool requestSuperseded = true;
    private EmbeddedWallMessageTarget? progressTarget;

    /// <summary>The panel at the bottom of the documents.</summary>
    public EmbeddedWallMessages Messages { get; }

    /// <summary>C5, first level: the state of the request and the line of the panel that stands for it.</summary>
    private void SetRequest(EmbeddedWallMessageLevel level, long revision, string text, string suffix = "")
    {
        RequestStatus = text + suffix;
        requestMessage = new(level, EmbeddedWallMessages.Calculation, text, $"Richiesta della revisione {revision}.", null);
        RefreshMessages();
    }

    /// <summary>A request starts: its lines replace those of the previous one.</summary>
    private void StartRequest(long revision, string text)
    {
        requestSuperseded = false; progressTarget = null;
        SetRequest(EmbeddedWallMessageLevel.Information, revision, text);
    }

    private void RefreshMessages()
    {
        if (Messages is null || Tables.Count == 0) return;
        var drafts = new List<EmbeddedWallMessageDraft>();
        string request = requestMessage?.Provenance ?? "";
        if (!requestSuperseded)
        {
            if (IsBusy && !string.IsNullOrWhiteSpace(Progress))
                drafts.Add(new(EmbeddedWallMessageLevel.Information, progressTarget?.StageId is null ? EmbeddedWallMessages.Calculation : EmbeddedWallMessages.Stage,
                    Progress, request, progressTarget));
            if (requestMessage is not null) drafts.Add(requestMessage);
            if (CalculationState == CalculationState.Failed && !string.IsNullOrWhiteSpace(CalculationMessage))
                drafts.Add(new(EmbeddedWallMessageLevel.Error, EmbeddedWallMessages.Calculation, CalculationMessage, request, null));
            drafts.AddRange(Warnings.Select(w => new EmbeddedWallMessageDraft(EmbeddedWallMessageLevel.Warning, EmbeddedWallMessages.Calculation, w, request, null)));
        }
        if (current is not null)
        {
            string print = $"Risultati dell'impronta {current.Fingerprint[..Math.Min(8, current.Fingerprint.Length)]}.";
            drafts.Add(new(current.Complete ? EmbeddedWallMessageLevel.Information : EmbeddedWallMessageLevel.Warning, EmbeddedWallMessages.Calculation,
                current.EmbedmentSearch is null ? current.Diagnostic : EmbedmentStatus, print, null));
            if (current.Cases is not null)
                drafts.AddRange(current.Cases.Where(c => !c.Complete).Select(c => new EmbeddedWallMessageDraft(EmbeddedWallMessageLevel.Warning,
                    EmbeddedWallMessages.Calculation, c.Diagnostic, print, new(c.CaseId, null, null, null))));
            foreach (var result in ResultCases)
                if (result.Hydraulics is { } hydraulics)
                    drafts.AddRange(hydraulics.Where(h => !h.Complete).Select(h => new EmbeddedWallMessageDraft(EmbeddedWallMessageLevel.Error,
                        EmbeddedWallMessages.Stage, h.Diagnostic, print, new(result.CaseId, h.Stage, null, null))));
        }
        if (Sketch is { } sketch)
        {
            string data = sketch.StageRow is null ? $"Dati del caso «{sketch.CaseId}»." : $"Dati della fase {sketch.Phase} del caso «{sketch.CaseId}».";
            drafts.AddRange(sketch.Issues.Select(i => new EmbeddedWallMessageDraft(i.Level, EmbeddedWallMessages.Data, i.Text, data,
                new(sketch.CaseId, sketch.StageRow?["id"], i.Table, i.Row))));
        }
        Messages.Update(drafts);
    }

    /// <summary>A line of the panel opens its element: the case and the phase, then the row of its table or the settings of the wall.</summary>
    private void OpenMessage(EmbeddedWallMessage message)
    {
        if (message.Target is not { } target) return;
        if (target.CaseId is not null && InputCases.Contains(target.CaseId)) InputCase = target.CaseId;
        if (target.StageId is not null && InputStages.FirstOrDefault(s => s["id"] == target.StageId) is { } stage) InputStage = stage;
        if (target.Table == EmbeddedWallMessages.GeneralTarget) { EditModelCommand.Execute("general"); return; }
        if (target.Table is not null && Tables.Any(t => t.Key == target.Table))
        {
            EditModelCommand.Execute(target.Table);
            if (target.Row is not null && InspectorTable!.Rows.Contains(target.Row)) InspectorTable.Selected = target.Row;
            return;
        }
        Documents.Show(EmbeddedWallDocuments.Section);
    }
}
