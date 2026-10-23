using Anthea.Application.Materials;
using Anthea.Application.Walls;
using Anthea.Application.BridgeDesign;
using Anthea.Application.Modules;
using Anthea.Reports;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;

internal sealed partial class SheetEditor : UserControl, ISheetEditor
{
    public string Module { get; }
    public JsonObject Data { get; }
    private ConcreteWorkspace? concrete;
    private readonly HorizontalWorkspace? horizontal;
    internal readonly BridgeWorkspace? bridge;
    internal readonly BridgeDesignWorkspace? bridgeDesign;
    internal readonly RetainingWallWorkspace? retainingWall;
    public JsonObject? Result => retainingWall is not null ? retainingWall.Result : bridgeDesign is not null ? bridgeDesign.Result : bridge is not null ? bridge.Result : horizontal is not null ? horizontal.ActiveResult : concrete?.Result;
    public bool HasResults => concrete is not null ? concrete.HasResults : Result is not null;
    public bool Busy => retainingWall?.Busy ?? bridge?.Busy ?? horizontal?.Busy ?? concrete?.Busy ?? false;
    public event Action? Modified;
    public event Action<JsonObject, string>? ConcreteSectionRequested;
    public event Action? ReportRequested;
    public byte[] BuildShortReport(string title) => concrete?.BuildShortReport(title)
        ?? throw new InvalidOperationException("Il report short è disponibile per le sezioni in calcestruzzo armato.");
    public event Action<string, JsonObject, string>? ModuleCopyRequested;
    public event Action<JsonObject>? SoilReplacementRequested;
    public Func<IEnumerable<(string Label, string Module, JsonObject Data, string Id)>>? SoilSources { get; set; }
    public FrameworkElement View => this;
    public JsonObject PreviewData => Data;
    private bool Section => Module == "str_palo";

    /// <param name="elasticAnalysis">Horizontal piles: creates the calculation of the elastic response; null (production) uses ElasticPileAnalysisSession (F5.8 traces).</param>
    internal SheetEditor(string module, JsonObject data, IModuleService? calculationService = null, Func<IElasticPileAnalysis>? elasticAnalysis = null)
    {
        _ = ModuleDirectory.Get(module);
        Module = module; Data = (JsonObject)data.DeepClone(); Background = Appearance.Surface;
        SetValue(WikiContextHelp.ModuleProperty, module);
        KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.F1 && WikiCatalog.ForModule(Module) is { } guide)
            { e.Handled = true; WikiContextHelp.Open(this, guide.Id); }
        };
        if (module == WallModel.Module)
        {
            retainingWall = new RetainingWallWorkspace(Data); retainingWall.Modified += () => Modified?.Invoke();
            retainingWall.SectionRequested += (section, name) => ConcreteSectionRequested?.Invoke(section, name);
            retainingWall.ReportRequested += () => ReportRequested?.Invoke();
            retainingWall.SoilTransferRequested += OpenSoilTransfer;
            Content = retainingWall; return;
        }
        if (module == BridgeDesignModel.Module)
        {
            bridgeDesign = new BridgeDesignWorkspace(Data); bridgeDesign.Modified += () => Modified?.Invoke(); Content = bridgeDesign; return;
        }
        if (module == BridgeEditor.Module)
        {
            bridge = new BridgeWorkspace(Data); bridge.Modified += () => Modified?.Invoke(); Content = bridge; return;
        }
        if (module is HorizontalPileEditor.Module or MicropaloOrizzontale.Module)
        {
            horizontal = new HorizontalWorkspace(Data, calculationService, elasticAnalysis); horizontal.Modified += () => Modified?.Invoke(); Content = Ui.Dock(horizontal, Ui.Bar(Ui.Button("Invia / carica terreno…", OpenSoilTransfer))); return;
        }
        if (Section)
        {
            void OpenConcrete()
            {
                concrete?.Dispose(); concrete = new ConcreteWorkspace(Data);
                concrete.Modified += () => Modified?.Invoke(); concrete.ReloadRequested += OpenConcrete; Content = concrete;
            }
            OpenConcrete(); return;
        }
        // The vertical pile and micropile (the last sheets of the generic branch) open with their MVVM view since the Release switch of 23/10
        // (f5-pass-geo): their historic view (GeoEditor.cs) is archived in supporto/SUPERATI/ui-storica/palo-verticale.
        throw new InvalidOperationException("Vista storica non disponibile per " + module + ": il modulo si apre con la vista MVVM.");
    }
    internal void OpenSoilTransfer()
    {
        Commit(); var dialog = new SoilTransferDialog(Window.GetWindow(this), Module, Data, SoilSources?.Invoke());
        if (dialog.ShowDialog() != true || dialog.Output is null) return;
        if (dialog.Replace) SoilReplacementRequested?.Invoke(dialog.Output);
        else if (dialog.DestinationModule is string target) ModuleCopyRequested?.Invoke(target, dialog.Output, dialog.OutputName);
    }
    private Action? releaseRevisionInspection;
    public void InspectRevision() => releaseRevisionInspection = RevisionInspection.Protect(this);
    public void Dispose() { releaseRevisionInspection?.Invoke(); concrete?.Dispose(); horizontal?.Dispose(); bridge?.Dispose(); retainingWall?.Dispose(); bridgeDesign?.Dispose(); }
    public void Commit() { if (retainingWall is not null) retainingWall.Commit(); else if (bridge is not null) bridge.Commit(); else if (horizontal is not null) horizontal.Commit(); else if (concrete is not null) concrete.Commit(); }
    public async Task CalculateAsync(bool commitEdits = true)
    {
        if (retainingWall is not null) { await retainingWall.CalculateAsync(); return; }
        if (bridgeDesign is not null) { await bridgeDesign.CalculateAsync(); return; }
        if (bridge is not null) { await bridge.CalculateAsync(commitEdits); return; }
        if (horizontal is not null) { await horizontal.CalculateActiveAsync(); return; }
        if (concrete is not null) await concrete.CalculateAllAsync();
    }
    public void ExportResult(string filename)
    {
        if (!HasResults || Busy) throw new InvalidOperationException(concrete is not null ? "Attendere l’aggiornamento automatico e correggere gli eventuali dati incompleti prima di esportare." : "Premere Calcola prima di esportare.");
        var exported = Result!;
        if (bridgeDesign is not null) exported["dati"] = Data.DeepClone();
        if (concrete is not null) exported["dati"] = Data.DeepClone();
        Archivio.ScriviAtomico(filename, Encoding.UTF8.GetBytes(exported.ToJsonString(J.Options)));
    }
    public void ExportReport(string filename, string title, HashSet<string> options, bool projectReport = false)
    {
        Anthea.Reports.ReportFiles.Write(filename, BuildReport(title, options, projectReport), title);
    }
    public ReportProject.SheetContent MaterialReportContent()
    {
        Commit(); return Materiali.MaterialReportChapter.Create(Module, Data);
    }
    public byte[] BuildReport(string title, HashSet<string> options, bool projectReport = false) => DocxWriter.Write(BuildReportDocument(title, options, projectReport));
    internal DocxDocument BuildReportDocument(string title, HashSet<string> options, bool projectReport = false)
    {
        if (retainingWall is not null) return retainingWall.BuildReportDocument(title);
        if (bridgeDesign is not null) return bridgeDesign.BuildReportDocument(title);
        if (bridge is not null) return bridge.BuildReportDocument(title, options, projectReport);
        if (concrete is not null) return concrete.BuildReportDocument(title, options, projectReport);
        if (Result is null) throw new InvalidOperationException(concrete is not null ? "Attendere l’aggiornamento automatico e correggere gli eventuali dati incompleti prima di esportare." : "Premere Calcola prima di esportare.");
        if (horizontal is not null && Result.S("tipo_risultato") == "palo_elastico") return ReportElasticPile.CreateDocument(title, Result);
        if (horizontal is not null) return ReportOrizzontale.CreateDocument(title, Result, !projectReport, horizontal.ReportFigures());
        throw new InvalidOperationException("Report non disponibile per " + Module + ".");
    }
}
