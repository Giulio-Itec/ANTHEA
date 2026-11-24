using Anthea.Presentation.Modules.QuickSection;
using Anthea.Presentation.Services;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Shell.Commands;

/// <summary>The sheet identity is independent of its label: two open sections may have the same name.</summary>
public sealed record QuickSectionCopyChoice(SheetSession Sheet, string Label);

/// <summary>Choosing a source or destination does not modify either section before «Copia».</summary>
public sealed class QuickSectionCopyDialogViewModel : DialogViewModel<SheetSession>
{
    private QuickSectionCopyChoice? selected;
    private readonly string current;
    private readonly bool copyTo;

    public QuickSectionCopyDialogViewModel(string current, bool copyTo, IReadOnlyList<QuickSectionCopyChoice> choices)
        : base(copyTo ? "Copia in" : "Copia da")
    {
        this.current = current; this.copyTo = copyTo; Choices = choices;
        selected = choices.FirstOrDefault();
        CopyCommand = new RelayCommand(() => { if (Selected is { } choice && Choices.Contains(choice)) Close(choice.Sheet); }, () => Selected is not null && Choices.Contains(Selected));
        CancelCommand = new RelayCommand(Cancel);
    }

    public string Current => "Sezione corrente: " + current;
    public string ChoiceLabel => copyTo ? "Sezione di destinazione" : "Sezione di origine";
    public string Description => "Sostituisce geometria, armature, materiali, combinazioni e tutte le impostazioni della destinazione. Resta il nome della destinazione. I risultati vengono ricalcolati.";
    public string Direction => Selected is { } choice
        ? "Da «" + (copyTo ? current : choice.Sheet.Heading) + "» a «" + (copyTo ? choice.Sheet.Heading : current) + "»." : "";
    public IReadOnlyList<QuickSectionCopyChoice> Choices { get; }
    public QuickSectionCopyChoice? Selected
    {
        get => selected;
        set { if (SetProperty(ref selected, value)) { OnPropertyChanged(nameof(Direction)); CopyCommand.NotifyCanExecuteChanged(); } }
    }
    public IRelayCommand CopyCommand { get; }
    public IRelayCommand CancelCommand { get; }
}

public sealed partial class ShellCommandsViewModel
{
    public IAsyncRelayCommand<SheetSession> CopySectionToCommand { get; }
    public IAsyncRelayCommand<SheetSession> CopySectionFromCommand { get; }
    public bool SectionCopyVisible => workspace.Active?.Module == SheetLayout.QuickSectionModule;

    private bool AvailableSection(SheetSession? sheet) => sheet is { IsDisposed: false, Unreadable: null, ViewModel: QuickSectionSheetViewModel }
        && sheet.Module == SheetLayout.QuickSectionModule && workspace.Sheets.Contains(sheet);

    private static bool WritableSection(SheetSession sheet) => !sheet.IsReadOnly && sheet.ViewModel is QuickSectionSheetViewModel { CanEdit: true };

    private SheetSession[] CopyCandidates(SheetSession current, bool copyTo) => workspace.Sheets
        .Where(sheet => !ReferenceEquals(sheet, current) && AvailableSection(sheet) && (!copyTo || WritableSection(sheet))).ToArray();

    private bool CanCopySection(SheetSession? current, bool copyTo)
    {
        current ??= workspace.Active;
        return !IsDisposed && AvailableSection(current) && (copyTo || WritableSection(current!)) && CopyCandidates(current!, copyTo).Length > 0;
    }

    private async Task CopySectionAsync(SheetSession? current, bool copyTo)
    {
        current ??= workspace.Active;
        if (!CanCopySection(current, copyTo)) return;
        workspace.Activate(current);
        var choices = CopyCandidates(current!, copyTo).Select((sheet, i) => new QuickSectionCopyChoice(sheet,
            $"{i + 1}. {sheet.Heading}" + (sheet.Owner.Path is { } path ? " · " + System.IO.Path.GetFileName(path) : " · non salvata"))).ToArray();
        using var dialog = new QuickSectionCopyDialogViewModel(current!.Heading, copyTo, choices);
        var other = await services.Dialogs.ShowDialogAsync(dialog);
        if (other is null || IsDisposed) return;
        var source = copyTo ? current : other;
        var destination = copyTo ? other : current;
        // Other windows can change or close while the dialog is open. Recheck the actual identities and destination at confirmation.
        if (ReferenceEquals(source, destination) || !choices.Any(c => ReferenceEquals(c.Sheet, other))
            || !AvailableSection(source) || !AvailableSection(destination) || !WritableSection(destination)) return;
        source.Commit(); destination.Commit();
        if (((QuickSectionSheetViewModel)destination.ViewModel).CopyFrom(source.ViewModel.Data)) destination.Commit();
        workspace.Activate(destination);
        NotifySectionCopy();
    }

    private void NotifySectionCopy()
    {
        OnPropertyChanged(nameof(SectionCopyVisible));
        CopySectionToCommand.NotifyCanExecuteChanged(); CopySectionFromCommand.NotifyCanExecuteChanged();
    }
}
