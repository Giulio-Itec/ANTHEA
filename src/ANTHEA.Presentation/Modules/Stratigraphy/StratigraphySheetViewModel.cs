using System.ComponentModel;
using Anthea.Application.Stratigraphy;
using Anthea.Presentation.Modules.Shared;
using Anthea.Presentation.Mvvm;
using Anthea.Reports;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.Stratigraphy;

/// <summary>
/// Sheet of the stratigraphy (geo_stratigrafia), F5.6b: the behaviour of the historic sheet (StratigraphyWorkspace in SheetEditor) without
/// WPF, on the same document and with the same writes, in the same order.
/// <list type="bullet">
/// <item>The editor of the document is <see cref="Editor"/> (<see cref="StratigraphyEditorViewModel"/>); its changes are the changes of the
/// sheet (new revision, Modified).</item>
/// <item>No calculation (<see cref="SheetCalculationMode.None"/>, as the historic sheet): «Calcola» only commits; the commit validates the
/// document and fails with the error of <see cref="StratigraphyDocument.Validate"/> when it is not valid, as today.</item>
/// <item>The AI import (<see cref="ImportCommand"/>) is disabled in the Release, as the historic button (<see cref="ImportToolTip"/>);
/// when the module enables it, the command shows <see cref="StratigraphyImportDialogViewModel"/> through the dialog service, with the
/// injected source of the drafts, and the reviewed draft replaces the data of the sheet.</item>
/// <item>«Invia / importa terreno da ANTHEA…» (<see cref="TransferSoilCommand"/>): the sheet commits, then the soil transfer dialog
/// (<see cref="ISoilTransferDialog"/>; by default the shared <see cref="SoilTransferDialogViewModel"/> on the dialog service,
/// <see cref="SoilTransferDialogService"/>, decision T5) receives its module, its data and the soil sheets of the project; a copy goes to
/// the window as <see cref="ISheetHost.OpenModuleCopy"/>, a replacement as <see cref="ISheetHost.ReplaceSoil"/>.</item>
/// <item>The errors of the two commands are shown through the message service (decision Q19).</item>
/// <item>The report is the data sheet of <see cref="StratigraphyReport"/> on the committed data, as the historic sheet exports it.</item>
/// <item>Read-only (a previous revision): the editor refuses every change, the two commands are disabled; the selection of the layers
/// and the commit stay.</item>
/// </list>
/// The equivalence is proved without WPF by tests/ANTHEA.Presentation.Tests (StratigraphySheetChecks) against the traces frozen in
/// tests/fixtures/f5-stratigrafia and against the F3 reference.
/// </summary>
public sealed class StratigraphySheetViewModel : SheetViewModelBase
{
    /// <summary>The module of the sheet.</summary>
    public const string Module = StratigraphyDocument.Module;

    private readonly StratigraphySheetOptions options;
    private StratigraphyEditorViewModel editor;

    /// <param name="context">Data of the sheet (owned copy), host, services and the calculation service of the module (never called).</param>
    /// <param name="options">Source of the AI drafts, proposed model and key, soil transfer dialog, import enabled.</param>
    public StratigraphySheetViewModel(SheetContext context, StratigraphySheetOptions options) : base(Module, context, mode: SheetCalculationMode.None)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.options = options;
        editor = NewEditor();
        Report = new StratigraphySheetReport(this);
        ImportCommand = GuardedCommands.CreateAsync(Guard, ImportAsync, Lifetime, () => CanImport);
        TransferSoilCommand = GuardedCommands.CreateAsync(Guard, TransferSoilAsync, Lifetime, () => CanEdit);
    }

    public override IModuleReportSource Report { get; }

    /// <summary>The editor of the document.</summary>
    public StratigraphyEditorViewModel Editor => editor;

    /// <summary>«Importa relazione PDF con AI…»: the import dialog, then the reviewed draft replaces the data.</summary>
    public IAsyncRelayCommand ImportCommand { get; }

    /// <summary>The import can start: enabled by the module and the sheet editable (in the Release it is disabled).</summary>
    public bool CanImport => options.ImportEnabled && CanEdit;

    /// <summary>Why the import is disabled, the tooltip of its button; empty when the module enables it.</summary>
    public string ImportToolTip => options.ImportEnabled ? "" : StratigraphyImport.Disabled;

    /// <summary>«Invia / importa terreno da ANTHEA…»: the sheet commits, then the soil transfer dialog.</summary>
    public IAsyncRelayCommand TransferSoilCommand { get; }

    /// <summary>The commit of the historic sheet: the document is validated (an invalid document throws, as today).</summary>
    protected override void OnCommit() => StratigraphyDocument.Validate(Data);

    /// <summary>The data were replaced: the editor is built again on them, as at opening (first layer selected), without Modified.</summary>
    protected override void OnReloaded() => ReplaceEditor();

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName != nameof(CanEdit) || editor is null) return;
        editor.IsReadOnly = !CanEdit;
        OnPropertyChanged(nameof(CanImport));
        ImportCommand?.NotifyCanExecuteChanged();
        TransferSoilCommand?.NotifyCanExecuteChanged();
    }

    private StratigraphyEditorViewModel NewEditor() => new(Data, Context.Services, OnEdited, importVisible: true, readOnly: !CanEdit);

    private void ReplaceEditor()
    {
        editor = NewEditor();
        OnPropertyChanged(nameof(Editor));
    }

    /// <summary>A change of the editor: the change of the sheet (new revision, Modified); nothing after the closing.</summary>
    private void OnEdited()
    {
        if (!IsDisposed) Changed();
    }

    private async Task ImportAsync(CancellationToken cancellation)
    {
        if (!CanImport) return;
        using var dialog = new StratigraphyImportDialogViewModel(options.Drafts, options.Defaults(), Context.Services, cancellation);
        var result = await Context.Services.Dialogs.ShowDialogAsync(dialog);
        if (result is null || !CanEdit) return;
        StratigraphyImport.Replace(Data, result);
        ReplaceEditor();
        Changed();
    }

    private async Task TransferSoilAsync(CancellationToken cancellation)
    {
        if (!CanEdit) return;
        Commit();
        var transfer = options.Transfer ?? new SoilTransferDialogService(Context.Services);
        var choice = await transfer.ShowAsync(ModuleId, Data, Context.Host.SoilSources());
        if (choice is null || IsDisposed) return;
        if (choice.Module is null) Context.Host.ReplaceSoil(choice.Data);
        else Context.Host.OpenModuleCopy(choice.Module, choice.Data, choice.Name);
    }

    /// <summary>The report of the sheet: the commit, then the data sheet with the title (reports frozen on 8/10).</summary>
    private sealed class StratigraphySheetReport(StratigraphySheetViewModel sheet) : IModuleReportSource
    {
        public byte[] BuildReport(string title, IReadOnlySet<string> options, bool projectReport = false)
        {
            sheet.Commit();
            return DocxWriter.Write(StratigraphyReport.Create(title, sheet.Data));
        }
    }
}

/// <summary>
/// What a stratigraphy sheet receives from its module: the source of the AI drafts, the model and key proposed when the import opens,
/// the soil transfer dialog and whether the import is enabled (false in the Release, as the historic button). Without a dialog
/// (<paramref name="Transfer"/> null, the composition root of the desktop since the view of 17/10) the sheet shows the shared dialog of the
/// soil on the dialog service of its services (<see cref="SoilTransferDialogService"/>, decision T5).
/// </summary>
public sealed record StratigraphySheetOptions(IStratigraphyDraftSource Drafts, Func<StratigraphyImportDefaults> Defaults, ISoilTransferDialog? Transfer = null,
    bool ImportEnabled = false)
{
    public IStratigraphyDraftSource Drafts { get; } = Drafts ?? throw new ArgumentNullException(nameof(Drafts));
    public Func<StratigraphyImportDefaults> Defaults { get; } = Defaults ?? throw new ArgumentNullException(nameof(Defaults));
}
