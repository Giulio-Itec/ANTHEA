using System.Text.Json.Nodes;
using Anthea.Application.EmbeddedWalls;
using Anthea.Presentation.Mvvm;
using Anthea.Presentation.Services;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.EmbeddedWalls;

/// <summary>
/// The phases of the case as tabs along the lower edge of the drawing and the menu «Fase» (organizzazione del modulo paratie, progetto del
/// 16/11, step 2): add, insert before the current one, rename the current one, rename all, delete the current one. The rules belong to
/// <see cref="EmbeddedWallDocument"/> (ANTHEA.Application); here the choice of the phase, the questions to the user and the selection
/// afterwards. Every command is one change of the sheet and one step of the history of the project (contract R01 of the review G-208):
/// before it the sheet confirms what was typed (a step of its own, when there is one), after it the operation, through
/// <see cref="ISheetHost.ConfirmEdit"/>. A refused question, a cancelled dialog or a read-only sheet record nothing.
/// </summary>
public sealed partial class EmbeddedWallSheetViewModel
{
    public IRelayCommand AddStageCommand { get; private set; } = null!;
    public IRelayCommand InsertStageCommand { get; private set; } = null!;
    public IAsyncRelayCommand RenameStageCommand { get; private set; } = null!;
    public IRelayCommand RenameAllStagesCommand { get; private set; } = null!;
    public IRelayCommand DeleteStageCommand { get; private set; } = null!;

    private void InitializeStages()
    {
        AddStageCommand = GuardedCommands.Create(Guard, AddStage, () => CanEdit);
        InsertStageCommand = GuardedCommands.Create(Guard, InsertStage, () => CanEdit && InputStage is not null);
        RenameStageCommand = GuardedCommands.CreateAsync(Guard, _ => RenameStageAsync(), Lifetime, () => CanEdit && InputStage is not null);
        RenameAllStagesCommand = GuardedCommands.Create(Guard, RenameAllStages, () => CanEdit && InputStages.Count > 0);
        DeleteStageCommand = GuardedCommands.Create(Guard, DeleteStage, () => CanEdit && InputStage is not null);
    }

    private void NotifyStageCommands()
    {
        AddStageCommand?.NotifyCanExecuteChanged(); InsertStageCommand?.NotifyCanExecuteChanged(); RenameStageCommand?.NotifyCanExecuteChanged();
        RenameAllStagesCommand?.NotifyCanExecuteChanged(); DeleteStageCommand?.NotifyCanExecuteChanged();
    }

    /// <summary>«Aggiungi fase»: a phase at the end of the history of the case, with the levels of the last one (A3), selected.</summary>
    private void AddStage()
    {
        if (!CanEdit) return;
        Context.Host.ConfirmEdit();
        var stages = Tables.Single(t => t.Key == "stages");
        stages.AddCommand.Execute(null);
        if (stages.Selected is { } added) InputStage = added;
        Context.Host.ConfirmEdit();
    }

    /// <summary>«Inserisci fase prima della corrente»: <see cref="EmbeddedWallDocument.InsertStage"/>, then the new phase is selected. When the
    /// insertion interrupts supports or leaves values of the previous phase behind (R04, <see cref="EmbeddedWallDocument.StageInsertionEffects"/>),
    /// the user confirms it first.</summary>
    private void InsertStage()
    {
        if (!CanEdit || InputStage is not { } current) return;
        Context.Host.ConfirmEdit();
        var effects = EmbeddedWallDocument.StageInsertionEffects(Data, current.Data);
        if (effects.Count > 0 && !Context.Services.Confirmations.ConfirmReplace("Inserisci fase",
            $"La fase nuova, prima di «{current["id"]}» nel caso «{InputCase}», riprende dalla fase precedente solo quote del terreno e falda. "
            + string.Join(" ", effects) + " Inserire la fase?")) return;
        var row = EmbeddedWallDocument.InsertStage(Data, current.Data);
        ApplyStageEdit(row);
    }

    /// <summary>«Rinomina la fase corrente»: asks the new identifier, then <see cref="EmbeddedWallDocument.RenameStage"/>.</summary>
    private async Task RenameStageAsync()
    {
        if (!CanEdit || InputStage is not { } current) return;
        Context.Host.ConfirmEdit();
        var name = await Context.Services.Dialogs.ShowDialogAsync(new EmbeddedWallStageNameDialogViewModel(current["id"], InputCase));
        if (name is null || IsDisposed || !CanEdit || !ReferenceEquals(InputStage, current)) return;
        if (name.Trim() == current["id"]) return;
        EmbeddedWallDocument.RenameStage(Data, current.Data, name);
        ApplyStageEdit(current.Data);
    }

    /// <summary>«Rinomina tutte»: F1…Fn in the order of the history of the case, after a confirmation that names the case.</summary>
    private void RenameAllStages()
    {
        if (!CanEdit || InputStages.Count == 0) return;
        Context.Host.ConfirmEdit();
        var current = InputStage?.Data;
        string names = string.Join(", ", InputStages.Select(s => s["id"]));
        if (!Context.Services.Confirmations.ConfirmReplace("Rinomina tutte le fasi",
            $"Le fasi del caso «{InputCase}» ({names}) diventano F1…F{InputStages.Count} nell'ordine della storia, con i loro riferimenti. Continuare?")) return;
        EmbeddedWallDocument.RenameAllStages(Data, InputCase);
        ApplyStageEdit(current);
    }

    /// <summary>«Elimina la fase corrente»: the confirmation lists the rows that cite it, which are removed with it; nothing silently.</summary>
    private void DeleteStage()
    {
        if (!CanEdit || InputStage is not { } current) return;
        Context.Host.ConfirmEdit();
        var references = EmbeddedWallDocument.StageReferences(Data, current.Data);
        string cited = references.Count == 0 ? "Nessuna riga cita la fase."
            : "Vengono eliminate anche le righe che la citano: " + string.Join("; ", references.GroupBy(r => r.Table)
                .Select(g => EmbeddedWallFields.Title(g.Key) + " " + g.Count())) + ".";
        if (!Context.Services.Confirmations.ConfirmDelete("Elimina la fase",
            $"Eliminare la fase «{current["id"]}» del caso «{InputCase}»? {cited}")) return;
        int index = InputStages.ToList().IndexOf(current);
        EmbeddedWallDocument.DeleteStage(Data, current.Data);
        ApplyStageEdit(InputStages.Where(s => !ReferenceEquals(s, current)).ElementAtOrDefault(Math.Max(0, index - 1))?.Data);
    }

    /// <summary>After a rule of the phases changed <see cref="SheetViewModelBase.Data"/>: the tables follow the arrays with the same rows,
    /// one change of the sheet, then <paramref name="select"/> becomes the phase on screen and the operation is one step of the history.</summary>
    private void ApplyStageEdit(JsonObject? select)
    {
        string caseId = InputCase;
        foreach (var table in Tables) table.Synchronize();
        Changed();
        if (InputCase != caseId && InputCases.Contains(caseId)) InputCase = caseId;
        if (select is not null && InputStages.FirstOrDefault(s => ReferenceEquals(s.Data, select)) is { } row) InputStage = row;
        NotifyStageCommands();
        Context.Host.ConfirmEdit();
    }
}

/// <summary>The question of «Rinomina la fase»: the new identifier, the current one proposed. The rule that checks it is in
/// <see cref="EmbeddedWallDocument.RenameStage"/>.</summary>
public sealed class EmbeddedWallStageNameDialogViewModel : DialogViewModel<string>
{
    private string name;

    public EmbeddedWallStageNameDialogViewModel(string current, string caseId) : base("Rinomina la fase")
    {
        name = current ?? "";
        Prompt = $"Nuovo identificativo della fase «{current}» del caso «{caseId}». Le righe che la citano lo seguono.";
        ConfirmCommand = new RelayCommand(() => Close(Name), () => !string.IsNullOrWhiteSpace(Name));
        CancelCommand = new RelayCommand(Cancel);
    }

    public string Prompt { get; }
    public string Name { get => name; set { if (SetProperty(ref name, value ?? "")) ConfirmCommand.NotifyCanExecuteChanged(); } }
    public IRelayCommand ConfirmCommand { get; }
    public IRelayCommand CancelCommand { get; }
}
