using System.Text;
using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Presentation.Modules;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Tests;

/// <summary>
/// Test-only module of the ModuleHost contract (F5.2): compiled into tests/ANTHEA.Presentation.Tests and into the UiTests build of
/// X.Desktop, never into Release (tools/qa/Assert-NoTestCode.ps1 looks for the name). Form-less: its ViewModel writes into its
/// copy of the data and calculates through the IModuleService of the context (a barrier in the checks). It stands for any module
/// whose <see cref="ModuleInfo"/> it receives.
/// </summary>
internal sealed class ProbeSheetModule(ModuleInfo info, TimeSpan? automaticDelay = null) : ISheetModule
{
    public ModuleInfo Info { get; } = info;
    public ModuleHelp Help { get; } = new(null);
    public ModulePersistence Persistence { get; } = new([ProbeSheetViewModel.ViewKey]);
    public ModuleReportCapabilities Reports { get; } = new(false);

    /// <summary>Sheets created by this module, in order.</summary>
    internal List<ProbeSheetViewModel> Created { get; } = [];

    public ISheetViewModel CreateSheet(SheetContext context)
    {
        var sheet = new ProbeSheetViewModel(Info.Id, context, automaticDelay);
        Created.Add(sheet);
        return sheet;
    }
}

/// <summary>ViewModel of the probe module: one field written at once («etichetta»), one draft written at Commit («nota»), host commands.</summary>
internal sealed class ProbeSheetViewModel : SheetViewModelBase
{
    /// <summary>Key of interface state declared by the probe (its <see cref="ModulePersistence"/>).</summary>
    internal const string ViewKey = "vista_prova";
    private string? note;
    private IReadOnlyList<SoilSource> sources = [];

    internal ProbeSheetViewModel(string module, SheetContext context, TimeSpan? automaticDelay) : base(module, context, automaticDelay)
    {
        Report = new ProbeReport(this);
        CopyCommand = Guarded(() => Context.Host.OpenModuleCopy(ModuleId, (JsonObject)Data.DeepClone(), "Copia di prova"));
        ReplaceSoilCommand = Guarded(() => Context.Host.ReplaceSoil(new JsonObject { ["terreno"] = "prova" }), () => CanEdit);
        ReportCommand = Guarded(Context.Host.RequestReport);
        SourcesCommand = Guarded(() => Sources = Context.Host.SoilSources());
    }

    /// <summary>Written into the data at every edit, as the raw text of InputForm.</summary>
    public string Label
    {
        get => Data["etichetta"]?.ToString() ?? "";
        set { Edit(data => data["etichetta"] = value); OnPropertyChanged(); }
    }

    /// <summary>Numeric value read by the barrier engine of the checks («valore»).</summary>
    public double Value
    {
        get => Data["valore"]?.GetValue<double>() ?? 0;
        set { Edit(data => data["valore"] = value); OnPropertyChanged(); }
    }

    /// <summary>Draft of a field: kept here and written into the data only by Commit.</summary>
    public string? Note
    {
        get => note;
        set { if (CanEdit) SetProperty(ref note, value); else OnPropertyChanged(); }
    }

    public IReadOnlyList<SoilSource> Sources { get => sources; private set => SetProperty(ref sources, value); }

    public IRelayCommand CopyCommand { get; }
    public IRelayCommand ReplaceSoilCommand { get; }
    public IRelayCommand ReportCommand { get; }
    public IRelayCommand SourcesCommand { get; }

    public override IModuleReportSource Report { get; }

    /// <summary>Times the drafts were written by Commit.</summary>
    internal int Commits { get; private set; }

    protected override void OnCommit()
    {
        if (note is null) return;
        Data["nota"] = note;
        note = null;
        Commits++;
        OnPropertyChanged(nameof(Note));
        Changed();
    }

    protected override void OnReloaded()
    {
        note = null;
        OnPropertyChanged(nameof(Note)); OnPropertyChanged(nameof(Label)); OnPropertyChanged(nameof(Value));
    }

    private IRelayCommand Guarded(Action action, Func<bool>? canExecute = null) => Anthea.Presentation.Mvvm.GuardedCommands.Create(Guard, action, canExecute);

    /// <summary>Report of the probe: the bytes of a line naming title, sections, kind and label (no report code involved).</summary>
    private sealed class ProbeReport(ProbeSheetViewModel sheet) : IModuleReportSource
    {
        public byte[] BuildReport(string title, IReadOnlySet<string> options, bool projectReport = false) =>
            Encoding.UTF8.GetBytes(title + "|" + string.Join(",", options.Order(StringComparer.Ordinal)) + "|" + projectReport + "|" + sheet.Label);
    }
}
