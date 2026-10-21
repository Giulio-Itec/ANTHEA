using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Application.BridgeDesign;
using Anthea.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.BridgeDesign;

/// <summary>A priced entry of the catalogue being edited (a row of «Voci del prezzario»).</summary>
public sealed partial class PriceEntryRow : ObservableObject
{
    [ObservableProperty] private string code = "";
    [ObservableProperty] private string description = "";
    [ObservableProperty] private string unit = "m";
    [ObservableProperty] private string price = "";
    [ObservableProperty] private string includes = "";
    internal PriceEntryDraft Draft() => new(Code, Description, Unit, Price, Includes);
}

/// <summary>The association of a work of the bridge (a row of «Associazioni e anteprima»): entry, override with its reason, prices before and after, outcome.</summary>
public sealed partial class WorkPriceRow : ObservableObject
{
    public required string WorkCode { get; init; }
    public required string Label { get; init; }
    public required string Unit { get; init; }
    [ObservableProperty] private string entryCode = "";
    [ObservableProperty] private string overridePrice = "";
    [ObservableProperty] private string reason = "";
    [ObservableProperty] private string previousPrice = "—";
    [ObservableProperty] private string resolvedPrice = "—";
    [ObservableProperty] private string status = "";
    internal WorkPriceDraft Draft() => new(WorkCode, EntryCode, OverridePrice, Reason);
}

/// <summary>Editable price analysis. Components: "codice = quantità" separated by ";", quantity in the unit of the component.
/// Empty percentages take the defaults of the price list.</summary>
public sealed partial class PriceAnalysisRow : ObservableObject
{
    [ObservableProperty] private string code = "";
    [ObservableProperty] private string description = "";
    [ObservableProperty] private string unit = "m³";
    [ObservableProperty] private string components = "";
    [ObservableProperty] private string generalExpenses = "";
    [ObservableProperty] private string profit = "";
    [ObservableProperty] private string unitPrice = "—";
    internal PriceAnalysisDraft Draft() => new(Code, Description, Unit, Components, GeneralExpenses, Profit);
}

/// <summary>A revision of the price document: caption and document.</summary>
public sealed record PriceRevisionItem(string Caption, JsonObject Document);

/// <summary>
/// The price editor of Bridge Design («Gestisci prezzario…»): a transactional MVVM editor, changes are drafts; applying creates an owned
/// snapshot and an audit entry. Moved from ANTHEA.Application to ANTHEA.Presentation with its module (F5.12, decision Q17 a): the rows, the
/// selections and the commands stay here; the catalogue, the preview, the documents and the refusals are those of
/// <see cref="PriceCatalogueEditing"/> in ANTHEA.Application, moved without changes from this class, so that Presentation does not reference
/// the calculation (rule P2). Members of the dialog service added (<see cref="Title"/>, <see cref="CloseRequested"/>, «Annulla» with
/// <see cref="CancelCommand"/>): the historic view (BridgePricesView) still shows it in its own window and closes it at <see cref="Applied"/>.
/// </summary>
public sealed partial class PriceCatalogueViewModel : ObservableObject, IDialogViewModel<JsonObject>
{
    private readonly PriceCatalogueEditing editing;
    private bool initializing;
    private PriceProposal? proposed;
    private IReadOnlyDictionary<string, PriceAnalysisView> resolvedAnalyses = new Dictionary<string, PriceAnalysisView>();
    public ObservableCollection<PriceEntryRow> Entries { get; } = [];
    public ObservableCollection<WorkPriceRow> Mappings { get; } = [];
    public ObservableCollection<PriceRevisionItem> History { get; } = [];
    public ObservableCollection<PriceAnalysisRow> Analyses { get; } = [];
    public string[] Units { get; } = ["m", "m²", "m³", "kg", "t", "cad", "h"];
    [ObservableProperty] private PriceEntryRow? selectedEntry;
    [ObservableProperty] private PriceAnalysisRow? selectedAnalysis;
    [ObservableProperty] private string generalExpensesPercent = "";
    [ObservableProperty] private string profitPercent = "";
    [ObservableProperty] private string analysisBreakdown = "";
    [ObservableProperty] private PriceRevisionItem? selectedRevision;
    [ObservableProperty] private string catalogueName = "";
    [ObservableProperty] private string edition = "";
    [ObservableProperty] private bool keepOverrides;
    [ObservableProperty] private bool repricePinnedAlternative;
    public bool HasPinnedAlternative => editing.HasPinnedAlternative;
    [ObservableProperty] private string status = "";
    [ObservableProperty] private string costPreview = "";
    [ObservableProperty] private bool canApply;
    public JsonObject? Result { get; private set; }
    public event Action? Applied;
    public PriceEstimate? Estimate { get; private set; }

    /// <summary>The caption of the window.</summary>
    public string Title => "Bridge Design · Gestione prezzari";

    /// <summary>Raised after «Applica prezzario» and by «Annulla»: the dialog service closes the window.</summary>
    public event EventHandler? CloseRequested;

    /// <param name="data">The data of the bridge (a copy is kept). Throws <see cref="ArgumentException"/> when the bridge cannot be priced.</param>
    public PriceCatalogueViewModel(JsonObject data)
    {
        initializing = true; editing = new PriceCatalogueEditing(data);
        foreach (var work in editing.Works)
        {
            var row = new WorkPriceRow { WorkCode = work.WorkCode, Label = work.Label, Unit = work.Unit, EntryCode = work.EntryCode, OverridePrice = work.OverridePrice, Reason = work.Reason };
            row.PropertyChanged += (_, e) => { if (e.PropertyName is nameof(WorkPriceRow.EntryCode) or nameof(WorkPriceRow.OverridePrice) or nameof(WorkPriceRow.Reason)) Preview(); };
            Mappings.Add(row);
        }
        LoadEntries(editing.Catalogue);
        foreach (var (caption, document) in editing.History) History.Add(new(caption, document));
        Entries.CollectionChanged += (_, _) => Preview();
        Analyses.CollectionChanged += (_, _) => Preview();
        initializing = false; Preview();
    }
    partial void OnGeneralExpensesPercentChanged(string value) => Preview();
    partial void OnProfitPercentChanged(string value) => Preview();
    partial void OnSelectedAnalysisChanged(PriceAnalysisRow? value) => ShowBreakdown();
    partial void OnCatalogueNameChanged(string value) => Preview();
    partial void OnEditionChanged(string value) => Preview();
    partial void OnRepricePinnedAlternativeChanged(bool value) => Preview();
    private void LoadEntries(PriceCatalogueDraft catalogue)
    {
        CatalogueName = catalogue.Name; Edition = catalogue.Edition; Entries.Clear();
        foreach (var e in catalogue.Entries)
        {
            var row = new PriceEntryRow { Code = e.Code, Description = e.Description, Unit = e.Unit, Price = e.Price, Includes = e.Includes };
            row.PropertyChanged += (_, _) => Preview(); Entries.Add(row);
        }
        GeneralExpensesPercent = catalogue.GeneralExpenses; ProfitPercent = catalogue.Profit; Analyses.Clear();
        foreach (var a in catalogue.Analyses)
        {
            var row = new PriceAnalysisRow { Code = a.Code, Description = a.Description, Unit = a.Unit, Components = a.Components, GeneralExpenses = a.GeneralExpenses, Profit = a.Profit };
            Watch(row); Analyses.Add(row);
        }
    }
    private void Watch(PriceAnalysisRow row) => row.PropertyChanged += (_, e) => { if (e.PropertyName != nameof(PriceAnalysisRow.UnitPrice)) Preview(); };
    private PriceCatalogueDraft Draft() => new(CatalogueName, Edition, Entries.Select(e => e.Draft()).ToArray(), GeneralExpensesPercent, ProfitPercent, Analyses.Select(a => a.Draft()).ToArray());
    private WorkPriceDraft[] Works() => Mappings.Select(m => m.Draft()).ToArray();

    /// <summary>A catalogue from a file (CSV, Excel, JSON); without «Mantieni i prezzi personalizzati» the overrides are cleared.</summary>
    public void Import(byte[] bytes, string fileName)
    {
        var catalogue = PriceCatalogueEditing.Import(bytes, fileName, Draft());
        initializing = true;
        try
        {
            LoadEntries(catalogue);
            if (!KeepOverrides) foreach (var m in Mappings) { m.OverridePrice = ""; m.Reason = ""; }
        }
        finally { initializing = false; }
        Preview();
    }
    public byte[] ExportCatalogue() => PriceCatalogueEditing.Export(Draft());
    [RelayCommand]
    private void AddAnalysis()
    {
        // The first analysis makes the defaults of the price list explicit (user decision of 14/10/2026): editable, never implicit
        if (GeneralExpensesPercent.Trim().Length == 0) GeneralExpensesPercent = "15";
        if (ProfitPercent.Trim().Length == 0) ProfitPercent = "10";
        var row = new PriceAnalysisRow { Code = "AN-" + Guid.NewGuid().ToString("N")[..6], Description = "Nuova analisi" };
        Watch(row); Analyses.Add(row); SelectedAnalysis = row;
    }
    [RelayCommand]
    private void RemoveAnalysis() { if (SelectedAnalysis is not null) Analyses.Remove(SelectedAnalysis); }
    private void ShowBreakdown()
    {
        if (SelectedAnalysis is null || !resolvedAnalyses.TryGetValue(SelectedAnalysis.Code.Trim(), out var r))
        { AnalysisBreakdown = SelectedAnalysis is null ? "" : PriceCatalogueEditing.UnresolvedAnalysis; return; }
        AnalysisBreakdown = PriceCatalogueEditing.Breakdown(r, SelectedAnalysis.Description);
    }
    [RelayCommand]
    private void AddEntry()
    {
        var row = new PriceEntryRow { Code = "NUOVA-" + Guid.NewGuid().ToString("N")[..8], Description = "Nuova voce", Unit = "m" };
        row.PropertyChanged += (_, _) => Preview(); Entries.Add(row); SelectedEntry = row;
    }
    [RelayCommand]
    private void RemoveEntry() { if (SelectedEntry is not null) Entries.Remove(SelectedEntry); }
    [RelayCommand]
    private void RestoreRevision()
    {
        if (SelectedRevision is null) return;
        var (catalogue, works) = PriceCatalogueEditing.Revision(SelectedRevision.Document);
        initializing = true;
        try
        {
            LoadEntries(catalogue);
            foreach (var m in Mappings)
            {
                var saved = works.SingleOrDefault(r => r.WorkCode == m.WorkCode);
                m.EntryCode = saved?.EntryCode ?? ""; m.OverridePrice = saved?.OverridePrice ?? ""; m.Reason = saved?.Reason ?? "";
            }
        }
        finally { initializing = false; }
        Preview();
    }
    [RelayCommand]
    private void Preview()
    {
        if (initializing) return;
        CanApply = false; Estimate = null; proposed = null;
        var outcome = editing.Preview(Draft(), Works(), RepricePinnedAlternative);
        // As the historic editor wrote them: the unit prices of the analyses, then the works until the first refusal.
        resolvedAnalyses = outcome.Analyses;
        foreach (var row in Analyses) row.UnitPrice = "—";
        foreach (var row in Analyses)
            if (resolvedAnalyses.TryGetValue(row.Code.Trim(), out var resolved)) row.UnitPrice = resolved.UnitPrice.ToString("N2", CultureInfo.GetCultureInfo("it-IT")) + " €/" + resolved.Unit;
        ShowBreakdown();
        foreach (var work in outcome.Works)
        {
            var row = Mappings[work.Index];
            row.PreviousPrice = work.PreviousPrice; row.ResolvedPrice = work.ResolvedPrice; row.Status = work.Status;
        }
        proposed = outcome.Proposal; Estimate = outcome.Estimate;
        CostPreview = outcome.CostPreview; Status = outcome.Status; CanApply = outcome.CanApply;
        ApplyCommand.NotifyCanExecuteChanged(); OnPropertyChanged(nameof(Estimate));
    }
    [RelayCommand(CanExecute = nameof(CanApply))]
    private void Apply()
    {
        Preview(); if (!CanApply || proposed is null) return;
        Result = editing.Apply(Draft(), Works(), proposed);
        Applied?.Invoke();
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
    /// <summary>«Annulla»: closes without applying.</summary>
    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, EventArgs.Empty);
    public void CommitTo(JsonObject target) => editing.CommitTo(Result, target, RepricePinnedAlternative);
}
