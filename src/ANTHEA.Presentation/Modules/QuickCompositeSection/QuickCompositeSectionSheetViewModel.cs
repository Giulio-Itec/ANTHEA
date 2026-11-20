using Anthea.Calculations;
using System.ComponentModel;
using System.Text.Json.Nodes;
using Anthea.Application.Bridge;
using Anthea.Application.Modules;
using Anthea.Application.Reporting;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Mvvm;
using Anthea.Presentation.Services;
using Anthea.Reports;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.QuickCompositeSection;

public interface IQuickCompositeFigureRenderer
{
    IReadOnlyList<ReportFigure> Render(BridgeQuickReportAnalysis analysis, IReadOnlySet<string> options);
}

public sealed class QuickCompositeSectionSheetModule(IQuickCompositeFigureRenderer? figures = null) : ISheetModule
{
    public ModuleInfo Info => BridgeQuickSection.Info;
    public ModuleHelp Help => ModuleHelp.For(Info.Id);
    public ModulePersistence Persistence { get; } = new([BridgeQuickSection.ViewKey, "ui_mista", "curve_sezione"]);
    public ModuleReportCapabilities Reports { get; } = new(true);
    public ISheetViewModel CreateSheet(SheetContext context) => new QuickCompositeSectionSheetViewModel(context, figures);
}

public sealed record QuickCompositeBarRow(string Name, FieldViewModel Present, FieldViewModel Diameter, FieldViewModel Pitch, FieldViewModel Distance, string Area);

public sealed class QuickCompositeCombinationRow : ObservableObject
{
    private readonly QuickCompositeSectionSheetViewModel owner;
    private readonly JsonObject row;
    internal QuickCompositeCombinationRow(QuickCompositeSectionSheetViewModel owner, JsonObject row)
    {
        this.owner = owner; this.row = row;
        RemoveCommand = new RelayCommand(() => owner.Remove(Id), () => owner.CanEdit && owner.CombinationRows.Count > 1);
    }
    public string Id => row.S("id");
    public string Name { get => row.S("nome"); set => Write("nome", value); }
    public string State { get => row.S("stato"); set => Write("stato", value); }
    public string Casting { get => Read("M_getto"); set => Write("M_getto", value); }
    public string LongTerm { get => Read("M_lungo"); set => Write("M_lungo", value); }
    public string ShortTerm { get => Read("M_breve"); set => Write("M_breve", value); }
    public string V { get => Read("V"); set => Write("V", value); }
    public string N { get => Read("N"); set => Write("N", value); }
    public IReadOnlyList<string> States => BridgeQuickSection.States;
    public bool IsReadOnly => !owner.CanEdit;
    public bool CanEdit => owner.CanEdit;
    public bool IsSelected { get => owner.SelectedId == Id; set { if (value) owner.Select(Id); } }
    private BridgeQuickCase? Result => owner.Current?.Cases.FirstOrDefault(c => c.Id == Id);
    public string Ratio => EngineeringFormat.Number(Result?.Ratio);
    public string Status => Result?.Status ?? owner.Status;
    public bool? Passed => Result?.Passed;
    public IRelayCommand RemoveCommand { get; }
    private string Read(string key) => BridgeQuickSection.Action(row, key);
    private void Write(string key, string? value, [System.Runtime.CompilerServices.CallerMemberName] string property = "")
    { if (value is not null) owner.Write(Id, key, value); OnPropertyChanged(property); }
    internal void Refresh()
    {
        foreach (string property in new[] { nameof(Ratio), nameof(Status), nameof(Passed), nameof(IsSelected), nameof(CanEdit), nameof(IsReadOnly) }) OnPropertyChanged(property);
        RemoveCommand.NotifyCanExecuteChanged();
    }
}

/// <summary>MVVM state only; all data preparation and engineering values are supplied by Application.</summary>
public sealed class QuickCompositeSectionSheetViewModel : SheetViewModelBase
{
    private readonly IQuickCompositeFigureRenderer? figures;
    private bool building;
    private string selectedId = "", display = "Tensioni totali", geometryError = "";
    private BridgeGeometry? geometry;
    public QuickCompositeSectionSheetViewModel(SheetContext context, IQuickCompositeFigureRenderer? figures = null)
        : base(BridgeQuickSection.Module, context, Anthea.Application.Presentation.BridgeInputPresentation.RecalculationDelay)
    {
        this.figures = figures;
        BridgeQuickSection.Initialize(Data); selectedId = BridgeQuickSection.SelectedId(Data);
        Report = new ReportSource(this);
        AddCombinationCommand = GuardedCommands.Create(Guard, () => { if (Edit(d => selectedId = BridgeQuickSection.Add(d))) BuildRows(); }, () => CanEdit);
        ExportExcelCommand = GuardedCommands.Create(Guard, ExportExcel);
        ImportExcelCommand = GuardedCommands.Create(Guard, ImportExcel, () => CanEdit);
        OpenCompleteCommand = GuardedCommands.Create(Guard, () =>
        {
            Commit(); var copy = BridgeQuickSection.ForCombination(Data, SelectedId);
            Context.Host.OpenModuleCopy(BridgeQuickSection.SourceModule, copy, Title);
        });
        BuildForms(); RefreshGeometry(); _ = ScheduleCalculationAsync();
    }
    public override IModuleReportSource Report { get; }
    public string Title { get => Data.S("nome"); set { if (value is not null && value != Title && Edit(d => d["nome"] = value)) OnPropertyChanged(); } }
    public string SelectedId => selectedId;
    public BridgeQuickResult? Current => PublishedCalculation?.Native as BridgeQuickResult;
    public BridgeQuickCase? SelectedCase => Current?.Cases.FirstOrDefault(c => c.Id == SelectedId);
    public string ResultTitle => CombinationRows.FirstOrDefault(r => r.Id == SelectedId) is { } row ? row.Name + " · " + row.State : "Risultato";
    public IReadOnlyList<BridgeQuickCheck> Checks => SelectedCase?.Checks ?? [];
    public IReadOnlyList<QuickReportValue> ResultValues => SelectedCase?.Values ?? [];
    public IReadOnlyList<QuickReportValue> ConcreteValues => BridgeQuickSection.MaterialValues(SelectedCase?.Calculation, "cls");
    public IReadOnlyList<QuickReportValue> SteelValues => BridgeQuickSection.MaterialValues(SelectedCase?.Calculation, "acciaio");
    public IReadOnlyList<QuickReportValue> RebarValues => BridgeQuickSection.MaterialValues(SelectedCase?.Calculation, "armatura");
    public string Notice => string.Join("\n", SelectedCase?.Warnings ?? []);
    public string Scope => BridgeQuickSection.Scope;
    public string ActionHelp => BridgeQuickSection.ActionHelp;
    public string Status => CalculationState is CalculationState.Pending or CalculationState.Running ? "Aggiornamento delle combinazioni…"
        : CalculationState is CalculationState.Failed or CalculationState.Incomplete ? CalculationMessage
        : SelectedCase?.Error is { Length: > 0 } error ? error : geometryError.Length > 0 ? geometryError : "Risultati riferiti ai dati correnti · " + (SelectedCase?.Status ?? "in attesa");
    public BridgeGeometry? Geometry => SelectedCase?.Calculation?.Geometry ?? geometry;
    public BridgeStage? Stage => SelectedCase?.Stage;
    public JsonObject DrawingInput => SelectedCase?.Calculation?.Input ?? Data;
    public IReadOnlyList<string> Displays { get; } = ["Tensioni totali", "Geometria", "Contributi delle fasi"];
    public string Display { get => display; set { if (Displays.Contains(value) && SetProperty(ref display, value)) OnPropertyChanged(nameof(DrawingMode)); } }
    public int DrawingMode => Display == "Geometria" ? 2 : Display == "Contributi delle fasi" ? 1 : 0;
    public FormViewModel GeometryForm { get; private set; } = null!;
    public FormViewModel BarsForm { get; private set; } = null!;
    public FormViewModel MaterialsForm { get; private set; } = null!;
    public IReadOnlyList<QuickCompositeBarRow> BarRows { get; private set; } = [];
    public IReadOnlyList<QuickCompositeCombinationRow> CombinationRows { get; private set; } = [];
    public IRelayCommand AddCombinationCommand { get; }
    public IRelayCommand ExportExcelCommand { get; }
    public IRelayCommand ImportExcelCommand { get; }
    public IRelayCommand OpenCompleteCommand { get; }
    private IEnumerable<FormViewModel> Forms => new[] { GeometryForm, BarsForm, MaterialsForm }.Where(f => f is not null);
    private void BuildForms()
    {
        building = true;
        FormViewModel Form(IEnumerable<FieldSpec> fields) => new(Data, fields, _ => { if (!building) { Changed(); RefreshGeometry(); } }, compact: true) { IsReadOnly = !CanEdit };
        GeometryForm = Form([new("h_trave", "Altezza H", "mm"), new("t_web", "Anima t", "mm"),
            new("b_top", "b sup.", "mm"), new("t_top", "t sup.", "mm"), new("b_bottom", "b inf.", "mm"), new("t_bottom", "t inf.", "mm"),
            new("b_cls", "b collaborante", "mm"), new("h_cls", "Spessore soletta", "mm")]);
        BarsForm = Form(Modules.CompositeSection.CompositeSectionFields.Bars("top").Concat(Modules.CompositeSection.CompositeSectionFields.Bars("bottom")));
        MaterialsForm = Form([new("classe_cls", "Calcestruzzo", Choices: BridgeEditor.ConcreteNames), new("acciaio", "Carpenteria", Choices: BridgeEditor.SteelNames), new("armatura", "Armatura", Choices: BridgeEditor.RebarNames)]);
        BuildRows(); building = false;
        foreach (string property in new[] { nameof(GeometryForm), nameof(BarsForm), nameof(MaterialsForm), nameof(Title) }) OnPropertyChanged(property);
    }
    private void BuildRows()
    {
        CombinationRows = BridgeQuickSection.Combinations(Data).Select(r => new QuickCompositeCombinationRow(this, r)).ToArray();
        OnPropertyChanged(nameof(CombinationRows)); RefreshResults();
    }
    private void RefreshGeometry()
    {
        (geometry, geometryError) = BridgeQuickSection.Preview(Data);
        var areas = BridgeQuickSection.BarAreas(Data, geometry);
        BarRows = new[] { "top", "bottom" }.Select((side, index) => new QuickCompositeBarRow(index == 0 ? "Superiore" : "Inferiore",
            BarsForm["rebars_" + side], BarsForm["d_" + side], BarsForm["pitch_" + side], BarsForm["cover_" + side], areas[index])).ToArray();
        OnPropertyChanged(nameof(BarRows)); OnPropertyChanged(nameof(Geometry)); OnPropertyChanged(nameof(DrawingInput)); OnPropertyChanged(nameof(Status));
    }
    internal void Write(string id, string key, string value)
    {
        if (building || IsDisposed) return;
        Guard.Run(() => { if (Edit(d => BridgeQuickSection.Write(d, id, key, value))) RefreshResults(); });
    }
    public void Select(string id)
    {
        if (IsDisposed || id == selectedId || !CombinationRows.Any(r => r.Id == id)) return;
        selectedId = id;
        if (CanEdit) { BridgeQuickSection.Select(Data, id); ChangedKeepingResults(); }
        RefreshResults();
    }
    internal void Remove(string id)
    {
        if (Edit(d => BridgeQuickSection.Remove(d, id))) { selectedId = BridgeQuickSection.SelectedId(Data); BuildRows(); }
    }
    protected override void OnCalculationPublished() => RefreshResults();
    private void RefreshResults()
    {
        foreach (string property in new[] { nameof(Current), nameof(SelectedId), nameof(SelectedCase), nameof(Checks), nameof(ResultTitle), nameof(ResultValues),
            nameof(ConcreteValues), nameof(SteelValues), nameof(RebarValues), nameof(Notice), nameof(Status), nameof(Geometry), nameof(Stage), nameof(DrawingInput) }) OnPropertyChanged(property);
        foreach (var row in CombinationRows) row.Refresh();
    }
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(CanEdit))
        {
            foreach (var form in Forms) form.IsReadOnly = !CanEdit;
            foreach (var row in CombinationRows) row.Refresh();
            AddCombinationCommand?.NotifyCanExecuteChanged(); ImportExcelCommand?.NotifyCanExecuteChanged();
        }
        if (e.PropertyName is nameof(Result) or nameof(Revision)) RefreshResults();
        if (e.PropertyName is nameof(CalculationState) or nameof(CalculationMessage)) OnPropertyChanged(nameof(Status));
    }
    protected override void OnCommit() { foreach (var form in Forms) form.Commit(); BridgeQuickSection.Validate(Data); }
    protected override void OnReloaded() { BridgeQuickSection.Initialize(Data); selectedId = BridgeQuickSection.SelectedId(Data); BuildForms(); RefreshGeometry(); }

    public string ExcelFolder { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ANTHEA", "Sollecitazioni");
    public string? ExcelPath { get; private set; }
    private void ExportExcel()
    {
        Commit(); byte[] bytes = BridgeQuickActionsExcel.Export(Data); Directory.CreateDirectory(ExcelFolder);
        string NewPath() => Path.Combine(ExcelFolder, "Sezione composta " + Guid.NewGuid().ToString("N") + ".xlsx");
        string path = ExcelPath ?? NewPath();
        try { X.Core.Archivio.ScriviAtomico(path, bytes); }
        catch (IOException) { path = NewPath(); X.Core.Archivio.ScriviAtomico(path, bytes); }
        ExcelPath = path; OnPropertyChanged(nameof(ExcelPath)); Context.Services.Links.Open(new Uri(path));
    }
    private void ImportExcel()
    {
        string? path = ExcelPath is { } previous && File.Exists(previous) ? previous
            : Context.Services.Files.OpenFile(new OpenFileRequest([new FileFilter("Cartella Excel", "*.xlsx")], null, "Importa sollecitazioni"));
        if (path is null) return;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var buffer = new MemoryStream(); stream.CopyTo(buffer);
        var rows = BridgeQuickActionsExcel.Read(buffer.ToArray());
        if (Edit(d => BridgeQuickSection.Replace(d, rows)))
        { ExcelPath = path; selectedId = BridgeQuickSection.SelectedId(Data); OnPropertyChanged(nameof(ExcelPath)); BuildRows(); }
    }
    private sealed class ReportSource(QuickCompositeSectionSheetViewModel sheet) : IModuleReportSource, IModuleShortReport
    {
        public byte[] BuildReport(string title, IReadOnlySet<string> options, bool projectReport = false)
        {
            sheet.ThrowIfDisposed(); sheet.Commit();
            if (sheet.IsBusy || sheet.Current is null) throw new InvalidOperationException("Attendere risultati aggiornati prima di esportare.");
            var analysis = BridgeQuickReportAnalysis.Create(sheet.Data, sheet.Current);
            if (sheet.figures is null && (options.Contains("sezione") || options.Contains("tensioni") || options.Contains(BridgeQuickReport.Brief)))
                throw new InvalidOperationException("Disegni della relazione non disponibili.");
            var images = sheet.figures?.Render(analysis, options).Select(i => i.Image()).ToArray() ?? [];
            return DocxWriter.Write(BridgeQuickReport.Create(title, analysis.Report, options, images));
        }
        public byte[] BuildShortReport(string title) => BuildReport(title, new HashSet<string> { BridgeQuickReport.Version, BridgeQuickReport.Brief });
    }
}
