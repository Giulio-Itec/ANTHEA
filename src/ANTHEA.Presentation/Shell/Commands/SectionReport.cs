using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Presentation.Mvvm;
using Anthea.Presentation.Projects;
using Anthea.Presentation.Services;
using CommunityToolkit.Mvvm.Input;
using X.Core;

namespace Anthea.Presentation.Shell.Commands;

/// <summary>
/// The writer of the report of a section (F5.13 e; MainWindow.GenerateSectionReport): the snapshot of the section written as a Word document with the
/// PDF beside it, the number of sheets whose chapter is missing or incomplete returned. The desktop implements it with the same call of MainWindow
/// (ProjectReportService.WriteSnapshotAsync with ModuleReportService and the figures of the desktop): the code of the reports does not change
/// (frozen on 8/10). The checks record the calls.
/// </summary>
public interface ISectionReports
{
    /// <param name="snapshot">The copy of the section (ProjectReportService.Snapshot), never the open project.</param>
    /// <param name="path">The Word document; the PDF is written beside it.</param>
    /// <param name="progress">The text of the progress, on the thread of the caller.</param>
    /// <param name="cancellation">«Annulla» of the progress: the report stops at the end of the calculation under way.</param>
    Task<int> WriteAsync(JsonObject snapshot, string path, Action<string> progress, CancellationToken cancellation);
}

/// <summary>The answer to «Controllo prima del report»: <see cref="Cancel"/> for «Annulla» and the closing of the window.</summary>
public enum SectionReportChoice
{
    Cancel,
    /// <summary>«Genera con segnalazioni».</summary>
    Generate,
    /// <summary>«Apri confronto»: the comparison of the section, no report.</summary>
    Compare
}

/// <summary>A common field different between the sheets: its label and one line «posizione › foglio: valore» per sheet involved.</summary>
public sealed record SectionReportDifference(string Label, IReadOnlyList<string> Lines);

/// <summary>
/// «Controllo prima del report» (MainWindow.ExportSectionReport, dialog «Report · sezione», 780 × 560): shown before the report of a section when its
/// sheets have different common data or the section has notices; the differences by field, the notices, «Apri confronto», «Genera con segnalazioni»,
/// «Annulla». Nothing is aligned: the report keeps the differences and lists them.
/// </summary>
public sealed class SectionReportCheckViewModel : DialogViewModel<SectionReportChoice>
{
    public const string Heading = "Controllo prima del report";
    public const string Intro = "Il report comprende tutte le schede della sezione e delle sottosezioni. Le differenze vengono conservate e segnalate nel documento; nessun valore viene uniformato automaticamente.";
    public const string NoticesTitle = "Avvisi";
    public const string CompareLabel = "Apri confronto";
    public const string GenerateLabel = "Genera con segnalazioni";
    public const string CancelLabel = "Annulla";

    /// <param name="section">The section of the report.</param>
    /// <param name="differences">ProjectSharedData.Differences of the section, in their order.</param>
    /// <param name="notices">ProjectValidation.Warnings of the section.</param>
    public SectionReportCheckViewModel(JsonObject section, IReadOnlyList<ProjectSharedData.Difference> differences, IReadOnlyList<string> notices) : base("Report · " + section.S("nome"))
    {
        ArgumentNullException.ThrowIfNull(differences);
        ArgumentNullException.ThrowIfNull(notices);
        Differences = differences.GroupBy(d => d.Key).Select(group => new SectionReportDifference(SharedDataCoordinator.FieldLabel(group.Key),
            group.SelectMany(d => new[] { d.First, d.Second }).Distinct().Select(sheet => ProjectSharedData.Location(sheet) + " › " + sheet.S("nome") + ": "
                + ProjectSharedData.Text(ProjectSharedData.Fields(sheet)[group.Key].Value)).ToArray())).ToArray();
        Notices = notices.ToArray();
        CompareCommand = new RelayCommand(() => Close(SectionReportChoice.Compare));
        GenerateCommand = new RelayCommand(() => Close(SectionReportChoice.Generate));
        CancelCommand = new RelayCommand(Cancel);
    }

    public IReadOnlyList<SectionReportDifference> Differences { get; }

    /// <summary>The notices of the section; «Avvisi» shows with one.</summary>
    public IReadOnlyList<string> Notices { get; }

    public bool HasNotices => Notices.Count > 0;

    public IRelayCommand CompareCommand { get; }
    public IRelayCommand GenerateCommand { get; }
    public IRelayCommand CancelCommand { get; }
}

/// <summary>
/// A dialog that may refuse to be closed by the user while it works (F5.13 e): the window asks it before closing. «Generazione del report» refuses and
/// cancels the report instead, as MainWindow (the window stays until the calculation under way ends).
/// </summary>
public interface IGuardedDialog
{
    /// <summary>True when the window may close now; false keeps it open (the dialog decides what the request means).</summary>
    bool CanClose();
}

/// <summary>
/// «Generazione del report» (MainWindow.ExportSectionReport, 580 × 190): the text of the progress of the report, an indeterminate bar and «Annulla», which
/// stops the report at the end of the calculation under way («Annullamento al termine del calcolo in corso…», the button disabled). The closing of the
/// window by the user cancels in the same way and keeps it open; the dialog closes itself when the report ends.
/// </summary>
public sealed class SectionReportProgressViewModel : DialogViewModel<bool>, IGuardedDialog
{
    public const string Starting = "Preparazione delle schede…";
    public const string Cancelling = "Annullamento al termine del calcolo in corso…";
    public const string CancelLabel = "Annulla";

    private readonly CancellationTokenSource cancellation = new();
    private string status = Starting;
    private bool finished;

    public SectionReportProgressViewModel() : base("Generazione del report")
    {
        CancelCommand = new RelayCommand(RequestCancel, () => !cancellation.IsCancellationRequested && !finished);
    }

    /// <summary>The text of the progress, as the report writes it.</summary>
    public string Status { get => status; private set => SetProperty(ref status, value); }

    /// <summary>The report ended, written, failed or cancelled.</summary>
    public bool IsFinished => finished;

    public bool IsCancelled => cancellation.IsCancellationRequested;

    public IRelayCommand CancelCommand { get; }

    /// <summary>
    /// Runs <paramref name="write"/> with the progress and the cancellation of this dialog; at the end, written or not, the dialog asks to close. The
    /// errors (and the cancellation) go to the caller.
    /// </summary>
    public async Task<int> RunAsync(Func<Action<string>, CancellationToken, Task<int>> write)
    {
        ArgumentNullException.ThrowIfNull(write);
        try { return await write(text => Status = text, cancellation.Token); }
        finally
        {
            finished = true;
            CancelCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(IsFinished));
            Close(true);
        }
    }

    /// <summary>The closing of the window: allowed when the report ended; otherwise the report is cancelled and the window stays.</summary>
    public bool CanClose()
    {
        if (finished) return true;
        RequestCancel();
        return false;
    }

    private void RequestCancel()
    {
        if (cancellation.IsCancellationRequested) return;
        cancellation.Cancel();
        Status = Cancelling;
        CancelCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsCancelled));
    }

    protected override void OnDisposed() => cancellation.Dispose();
}

/// <summary>
/// The report of a section from the shell (F5.13 e; MainWindow.ExportSectionReport): «Genera report» of the overview and «Report Word della sezione» of the
/// tree. The same sequence and texts of MainWindow:
/// <list type="bullet">
/// <item>the sheet with the focus is confirmed; a section without sheets does nothing;</item>
/// <item>with different common data or notices, «Controllo prima del report» first: «Apri confronto» opens the comparison, «Annulla» stops;</item>
/// <item>the file «Relazione nome.docx» (the characters not allowed in a file name become «_»), «Word e PDF affiancati»;</item>
/// <item>the snapshot of the section (ProjectReportService.Snapshot) written through <see cref="ISectionReports"/> under «Generazione del report»; a
/// cancellation ends silently, an error goes on;</item>
/// <item>the message «Report salvato in Word e PDF: …», a warning when some sheets have missing or incomplete results.</item>
/// </list>
/// </summary>
public sealed class SectionReportRunner
{
    public const string Filter = "Word e PDF affiancati";
    public const string MessageTitle = "ANTHEA";

    private readonly WorkspaceSession workspace;
    private readonly PresentationServices services;
    private readonly ISectionReports reports;
    private readonly Func<JsonObject, Task> compare;

    /// <param name="compare">The comparison of a section (the shell: its dialog).</param>
    public SectionReportRunner(WorkspaceSession workspace, PresentationServices services, ISectionReports reports, Func<JsonObject, Task> compare)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(reports);
        ArgumentNullException.ThrowIfNull(compare);
        this.workspace = workspace; this.services = services; this.reports = reports; this.compare = compare;
    }

    /// <summary>The name proposed for the report of <paramref name="section"/>: «Relazione nome.docx», the characters not allowed replaced by «_».</summary>
    public static string FileName(JsonObject section)
    {
        ArgumentNullException.ThrowIfNull(section);
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        return "Relazione " + string.Concat(section.S("nome", "Sezione").Select(c => invalid.Contains(c) ? '_' : c)) + ".docx";
    }

    /// <summary>The message after the report: the Word document, the PDF beside it and, with <paramref name="unavailable"/> sheets, the note of today.</summary>
    public static string SavedMessage(string path, int unavailable) => "Report salvato in Word e PDF:\n" + path + "\n" + System.IO.Path.ChangeExtension(path, ".pdf")
        + (unavailable > 0 ? $"\n\n{unavailable} schede contengono risultati mancanti o incompleti, segnalati nel documento." : "");

    /// <summary>The report of <paramref name="section"/>: null when nothing was written (no sheets, «Annulla», no file, cancelled), otherwise the sheets with missing results.</summary>
    public async Task<int?> RunAsync(JsonObject section)
    {
        ArgumentNullException.ThrowIfNull(section);
        workspace.CommitActive();
        if (!ProjectSharedData.SubtreeSheets(section).Any()) return null;
        var differences = ProjectSharedData.Differences(section);
        var notices = ProjectValidation.Warnings(section);
        if (differences.Count > 0 || notices.Length > 0)
        {
            var choice = await services.Dialogs.ShowDialogAsync(new SectionReportCheckViewModel(section, differences, notices));
            if (choice != SectionReportChoice.Generate)
            {
                if (choice == SectionReportChoice.Compare) await compare(section);
                return null;
            }
        }
        if (services.Files.SaveFile(new SaveFileRequest([new FileFilter(Filter, "*.docx")], FileName(section))) is not { } path) return null;
        var snapshot = Anthea.Application.Reporting.ProjectReportService.Snapshot(section);
        using var progress = new SectionReportProgressViewModel();
        var work = progress.RunAsync((report, token) => reports.WriteAsync(snapshot, path, report, token));
        // The dialog closes itself when the report ends; a report that ended before it opens needs no dialog.
        if (!work.IsCompleted) await services.Dialogs.ShowDialogAsync(progress);
        int unavailable;
        try { unavailable = await work; }
        catch (OperationCanceledException) { return null; }
        string message = SavedMessage(path, unavailable);
        if (unavailable > 0) services.Messages.Warn(MessageTitle, message);
        else services.Messages.Inform(MessageTitle, message);
        return unavailable;
    }
}
