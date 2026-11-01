using System.ComponentModel;
using System.Text.Json.Nodes;
using Anthea.Application.Materials;
using Anthea.Application.Modules;
using Anthea.Reports;

namespace Anthea.Presentation.Modules.Rheology;

public sealed class RheologySheetModule : ISheetModule
{
    public ModuleInfo Info => ModuleDirectory.Get(RheologyDocument.Module);
    public ModuleHelp Help => ModuleHelp.For(Info.Id);
    public ModulePersistence Persistence { get; } = new(["LogX", "LogY", "SelectedCurve", "QueryAge"]);
    public ModuleReportCapabilities Reports { get; } = new(false);
    public ISheetViewModel CreateSheet(SheetContext context) => new RheologySheetViewModel(context);
}

/// <summary>Saved sheet around the existing rheology interaction. GPC is accessed only by the application adapter.</summary>
public sealed class RheologySheetViewModel : SheetViewModelBase
{
    private bool loading;
    private JsonObject? result;
    public ConcreteTimeEffectsViewModel Model { get; } = new();
    public RheologySheetViewModel(SheetContext context) : base(RheologyDocument.Module, context, mode: SheetCalculationMode.None)
    {
        Report = new ReportSource(this);
        Restore(); Model.PropertyChanged += ModelChanged;
    }
    public override IModuleReportSource Report { get; }
    public override JsonObject? Result => result;
    public override bool HasResults => result is not null;
    public override JsonObject PreviewData => CanEdit ? Capture() : (JsonObject)Data.DeepClone();
    private JsonObject Capture()
    {
        var data = (JsonObject)Data.DeepClone();
        data["Fck"] = Model.Fck; data["Humidity"] = Model.Humidity; data["NotionalSize"] = Model.NotionalSize;
        data["LoadingAge"] = Model.LoadingAge; data["Cement"] = Model.Cement.Value; data["StartAge"] = Model.StartAge;
        data["EndAge"] = Model.EndAge; data["Samples"] = Model.Samples; data["QueryAge"] = Model.QueryAge;
        data["SelectedCurve"] = Model.SelectedCurve.Value.ToString(); data["LogX"] = Model.LogX; data["LogY"] = Model.LogY;
        return data;
    }
    private void Write(JsonObject target)
    {
        var state = Capture(); target.Clear(); foreach (var p in state) target[p.Key] = p.Value?.DeepClone();
    }
    private void Restore()
    {
        RheologyDocument.Validate(Data); loading = true; Model.IsReadOnly = false;
        try
        {
            string Text(string key) => Data[key]!.GetValue<string>();
            Model.Fck = Text("Fck"); Model.Humidity = Text("Humidity"); Model.NotionalSize = Text("NotionalSize");
            Model.LoadingAge = Text("LoadingAge"); Model.Cement = Model.Cements.Single(c => c.Value == Text("Cement"));
            Model.StartAge = Text("StartAge"); Model.EndAge = Text("EndAge"); Model.Samples = Text("Samples");
            Model.QueryAge = Text("QueryAge"); Model.SelectedCurve = Model.Curves.Single(c => c.Value.ToString() == Text("SelectedCurve"));
            Model.LogX = Data["LogX"]!.GetValue<bool>(); Model.LogY = Data["LogY"]!.GetValue<bool>();
            Model.CalculateCommand.Execute(null);
        }
        finally { loading = false; Model.IsReadOnly = !CanEdit; }
        RefreshResult();
    }
    private void ModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (loading || IsDisposed) return;
        bool input = RheologyDocument.TextKeys.Contains(e.PropertyName ?? "") || e.PropertyName is "LogX" or "LogY";
        if (input && CanEdit && !JsonNode.DeepEquals(Data, Capture())) Edit(Write);
        if (e.PropertyName is nameof(Model.Status) or nameof(Model.HasResults) or nameof(Model.QueryPoint)) RefreshResult();
    }
    private void RefreshResult()
    {
        result = Model.HasResults ? RheologyDocument.Calculate(Capture()) : null;
        OnPropertyChanged(nameof(Result)); OnPropertyChanged(nameof(HasResults));
    }
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(CanEdit) && Model is not null) Model.IsReadOnly = !CanEdit;
    }
    protected override void OnCommit() { if (CanEdit) Write(Data); RheologyDocument.Validate(Data); }
    protected override void OnReloaded() => Restore();
    public override Task<bool> CalculateAsync(CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested(); Model.CalculateCommand.Execute(null); RefreshResult(); return Task.FromResult(HasResults);
    }
    protected override void OnDisposed()
    {
        Model.PropertyChanged -= ModelChanged; Model.Dispose();
        base.OnDisposed();
    }
    private sealed class ReportSource(RheologySheetViewModel sheet) : IModuleReportSource
    {
        public byte[] BuildReport(string title, IReadOnlySet<string> options, bool projectReport = false)
        {
            sheet.Commit(); var calculation = sheet.CalculateModule(sheet.Data);
            return DocxWriter.Write(RheologyReport.Create(title, calculation.Data, calculation.Values));
        }
    }
}
