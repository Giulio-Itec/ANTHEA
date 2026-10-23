using Anthea.Application.Materials;
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
    public JsonObject? Result => null;
    public bool HasResults => Result is not null;
    public bool Busy => false;
    public event Action? Modified;
    public event Action<JsonObject, string>? ConcreteSectionRequested;
    public event Action? ReportRequested;
    public byte[] BuildShortReport(string title) => throw new InvalidOperationException("Il report short è disponibile per le sezioni in calcestruzzo armato.");
    public event Action<string, JsonObject, string>? ModuleCopyRequested;
    public event Action<JsonObject>? SoilReplacementRequested;
    public Func<IEnumerable<(string Label, string Module, JsonObject Data, string Id)>>? SoilSources { get; set; }
    public FrameworkElement View => this;
    public JsonObject PreviewData => Data;
    private bool Section => Module == "str_palo";

    internal SheetEditor(string module, JsonObject data, IModuleService? calculationService = null)
    {
        _ = ModuleDirectory.Get(module);
        Module = module; Data = (JsonObject)data.DeepClone(); Background = Appearance.Surface;
        SetValue(WikiContextHelp.ModuleProperty, module);
        KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.F1 && WikiCatalog.ForModule(Module) is { } guide)
            { e.Handled = true; WikiContextHelp.Open(this, guide.Id); }
        };
        // The vertical pile and micropile (the last sheets of the generic branch) open with their MVVM view since the Release switch of 23/10
        // (f5-pass-geo): their historic view (GeoEditor.cs) is archived in supporto/SUPERATI/ui-storica/palo-verticale. The RC section
        // (f5-pass-sezione) and the retaining walls (f5-pass-muri) went the same way: no module has a historic sheet any more (part g).
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
    public void Dispose() { releaseRevisionInspection?.Invoke(); }
    public void Commit() { }
    public Task CalculateAsync(bool commitEdits = true) => Task.CompletedTask;
    public void ExportResult(string filename)
    {
        if (!HasResults || Busy) throw new InvalidOperationException("Premere Calcola prima di esportare.");
        var exported = Result!;
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
        if (Result is null) throw new InvalidOperationException("Premere Calcola prima di esportare.");
        throw new InvalidOperationException("Report non disponibile per " + Module + ".");
    }
}
