using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Calculations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using X.Core;
namespace Anthea.Application.BridgeDesign;

public sealed partial class PriceEntryRow : ObservableObject
{
    [ObservableProperty] private string code = "";
    [ObservableProperty] private string description = "";
    [ObservableProperty] private string unit = "m";
    [ObservableProperty] private string price = "";
    [ObservableProperty] private string includes = "";
    internal PriceEntry Value() => new(Code.Trim(), Description.Trim(), Unit.Trim(), PriceCatalogueImport.Number(Price), Includes.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
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
    internal WorkPriceMapping Value() => new(WorkCode, EntryCode.Trim(), PriceCatalogueImport.Number(OverridePrice), Reason.Trim());
}
public sealed record PriceRevisionItem(string Caption, JsonObject Document);

/// <summary>Transactional MVVM editor: changes are drafts; applying creates an owned snapshot and an audit entry.</summary>
public sealed partial class PriceCatalogueViewModel : ObservableObject
{
    private readonly JsonObject source;
    private readonly BridgePricing.Context context;
    private readonly BridgePriceSnapshot previous;
    private bool initializing;
    private BridgePriceSnapshot? proposed;
    private BridgePricing.Context? pinnedContext;
    public ObservableCollection<PriceEntryRow> Entries { get; } = [];
    public ObservableCollection<WorkPriceRow> Mappings { get; } = [];
    public ObservableCollection<PriceRevisionItem> History { get; } = [];
    public string[] Units { get; } = ["m", "m²", "m³", "kg", "t", "cad"];
    [ObservableProperty] private PriceEntryRow? selectedEntry;
    [ObservableProperty] private PriceRevisionItem? selectedRevision;
    [ObservableProperty] private string catalogueName = "";
    [ObservableProperty] private string edition = "";
    [ObservableProperty] private bool keepOverrides;
    [ObservableProperty] private bool repricePinnedAlternative;
    public bool HasPinnedAlternative => source["alternative_a"] is JsonObject;
    [ObservableProperty] private string status = "";
    [ObservableProperty] private string costPreview = "";
    [ObservableProperty] private bool canApply;
    public JsonObject? Result { get; private set; }
    public event Action? Applied;
    public BridgePriceEstimate? Estimate { get; private set; }
    public PriceCatalogueViewModel(JsonObject data)
    {
        initializing = true; source = (JsonObject)data.DeepClone(); context = BridgePricing.Prepare(source);
        if (source["pricing"] is JsonObject document && (document["catalogue"] is null || document["mappings"] is null)) throw new ArgumentException("Catalogo o associazioni mancanti nel documento prezzi.");
        var legacy = LegacyCatalogue(source);
        previous = source["pricing"]?["snapshot"]?.Deserialize<BridgePriceSnapshot>() ?? LegacySnapshot(legacy);
        var catalogue = source["pricing"]?["catalogue"]?.Deserialize<PriceCatalogue>() ?? legacy;
        var mappings = source["pricing"]?["mappings"]?.Deserialize<WorkPriceMapping[]>() ?? BridgeDesignModel.Rates.Select(r => new WorkPriceMapping(r.Key, r.Key, null, "")).ToArray();
        foreach (var rate in BridgeDesignModel.Rates)
        {
            var m = mappings.SingleOrDefault(m => m.WorkCode == rate.Key);
            var row = new WorkPriceRow { WorkCode = rate.Key, Label = rate.Label, Unit = rate.Unit[2..], EntryCode = m?.EntryCode ?? "", OverridePrice = Format(m?.Override), Reason = m?.Reason ?? "" };
            row.PropertyChanged += (_, e) => { if (e.PropertyName is nameof(WorkPriceRow.EntryCode) or nameof(WorkPriceRow.OverridePrice) or nameof(WorkPriceRow.Reason)) Preview(); };
            Mappings.Add(row);
        }
        LoadEntries(catalogue);
        if (source["pricing"]?["history"] is JsonArray history)
            foreach (var item in history.OfType<JsonObject>()) History.Add(new(item.S("date") + " · " + item["catalogue"].S("Name") + " / " + item["catalogue"].S("Edition"), (JsonObject)item.DeepClone()));
        Entries.CollectionChanged += (_, _) => Preview();
        initializing = false; Preview();
    }
    partial void OnCatalogueNameChanged(string value) => Preview();
    partial void OnEditionChanged(string value) => Preview();
    partial void OnRepricePinnedAlternativeChanged(bool value) => Preview();
    private void LoadEntries(PriceCatalogue catalogue)
    {
        PriceCatalogueImport.Validate(catalogue);
        CatalogueName = catalogue.Name; Edition = catalogue.Edition; Entries.Clear();
        foreach (var e in catalogue.Entries)
        {
            var row = new PriceEntryRow { Code = e.Code, Description = e.Description, Unit = e.Unit, Price = Format(e.Price), Includes = string.Join('|', e.Includes) };
            row.PropertyChanged += (_, _) => Preview(); Entries.Add(row);
        }
    }
    public void Import(byte[] bytes, string fileName)
    {
        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        var catalogue = extension switch
        {
            ".xlsx" => PriceCatalogueImport.Excel(bytes, Path.GetFileNameWithoutExtension(fileName)),
            ".csv" => PriceCatalogueImport.Csv(bytes, Path.GetFileNameWithoutExtension(fileName)),
            ".json" => JsonSerializer.Deserialize<PriceCatalogue>(bytes) ?? throw new ArgumentException("Catalogo JSON vuoto."),
            _ => throw new ArgumentException("Usare CSV, XLSX oppure un catalogo JSON ANTHEA.")
        };
        PriceCatalogueImport.Validate(catalogue);
        initializing = true;
        try
        {
            LoadEntries(catalogue);
            if (!KeepOverrides) foreach (var m in Mappings) { m.OverridePrice = ""; m.Reason = ""; }
        }
        finally { initializing = false; }
        Preview();
    }
    public byte[] ExportCatalogue() => JsonSerializer.SerializeToUtf8Bytes(Catalogue(), new JsonSerializerOptions { WriteIndented = true });
    private PriceCatalogue Catalogue()
    {
        var c = new PriceCatalogue(CatalogueName.Trim(), Edition.Trim(), "EUR", Entries.Select(e => e.Value()).ToArray());
        PriceCatalogueImport.Validate(c); return c;
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
        var c = SelectedRevision.Document["catalogue"]!.Deserialize<PriceCatalogue>()!;
        var rows = SelectedRevision.Document["mappings"]!.Deserialize<WorkPriceMapping[]>()!;
        initializing = true;
        try
        {
            LoadEntries(c);
            foreach (var m in Mappings)
            {
                var saved = rows.SingleOrDefault(r => r.WorkCode == m.WorkCode);
                m.EntryCode = saved?.EntryCode ?? ""; m.OverridePrice = Format(saved?.Override); m.Reason = saved?.Reason ?? "";
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
        try
        {
            var c = Catalogue(); var entries = c.Entries.ToDictionary(e => e.Code, StringComparer.Ordinal); var rates = new List<BridgePriceRate>();
            var mapping = Mappings.Select(m => m.Value()).ToArray();
            foreach (var row in Mappings)
            {
                var m = mapping.Single(v => v.WorkCode == row.WorkCode); entries.TryGetValue(m.EntryCode, out var e);
                var old = previous.Rates.SingleOrDefault(r => r.WorkCode == row.WorkCode);
                row.PreviousPrice = old?.Price is decimal op ? Format(BridgePricing.ConvertPrice(op, old.Unit, row.Unit)) : "—";
                row.ResolvedPrice = "—"; row.Status = "Codice non associato";
                if (m.Override.HasValue && m.Reason.Length == 0) throw new ArgumentException(row.Label + ": motivare il prezzo personalizzato.");
                if (e is null && !m.Override.HasValue) continue;
                string unit = m.Override.HasValue ? row.Unit : e!.Unit;
                decimal? value = m.Override ?? e?.Price;
                try
                {
                    decimal? normalized = value.HasValue ? BridgePricing.ConvertPrice(value.Value, unit, row.Unit) : null;
                    row.ResolvedPrice = Format(normalized);
                    row.Status = normalized.HasValue ? m.Override.HasValue ? "Personalizzato · " + m.Reason : "Da catalogo" : "Prezzo mancante";
                    rates.Add(new(row.WorkCode, unit, value, c.Name + "/" + c.Edition + "/" + (m.Override.HasValue ? "override:" + m.Reason : e!.Code), e?.Includes ?? []));
                }
                catch (ArgumentException) { row.Status = "Unità incompatibile: " + unit + " → " + row.Unit; }
            }
            string revision = Fingerprint(JsonSerializer.SerializeToNode(new { catalogue = c, mappings = mapping })!);
            proposed = new(revision, "EUR", rates.ToArray()); Estimate = context.Evaluate(proposed);
            var oldEstimate = context.Evaluate(previous);
            CostPreview = "Costo totale prima: " + (oldEstimate.Total?.ToString("N2", CultureInfo.GetCultureInfo("it-IT")) ?? "—") + " €   →   dopo: " + (Estimate.Total?.ToString("N2", CultureInfo.GetCultureInfo("it-IT")) ?? "—") + " €";
            int unresolved = Mappings.Count(m => m.Status.StartsWith("Codice", StringComparison.Ordinal) || m.Status.StartsWith("Unità", StringComparison.Ordinal) || m.Status == "Prezzo mancante");
            Status = Estimate.Complete ? $"Anteprima completa per questa geometria. {unresolved} associazioni non risolte nel catalogo complessivo. Le alternative che le richiedono saranno escluse." : "Computo incompleto: " + string.Join(", ", Estimate.Lines.Where(l => l.Issue != "None").Select(l => l.WorkCode + " · " + l.Issue).Distinct());
            CanApply = Estimate.Complete;
            if (CanApply && RepricePinnedAlternative && source["alternative_a"] is JsonObject pinned)
            {
                pinnedContext ??= BridgePricing.Prepare(pinned);
                var alternative = pinnedContext.Evaluate(proposed);
                if (!alternative.Complete) { CanApply = false; Status = "Il listino non copre le lavorazioni dell'alternativa A: completare le associazioni oppure deselezionare la sua rivalutazione."; }
            }
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or OverflowException or JsonException)
        { CanApply = false; Status = e.Message; CostPreview = "Anteprima non disponibile"; }
        ApplyCommand.NotifyCanExecuteChanged(); OnPropertyChanged(nameof(Estimate));
    }
    [RelayCommand(CanExecute = nameof(CanApply))]
    private void Apply()
    {
        Preview(); if (!CanApply || proposed is null) return;
        var history = source["pricing"]?["history"]?.DeepClone() as JsonArray ?? [];
        if (source["pricing"] is JsonObject old)
        { var entry = (JsonObject)old.DeepClone(); entry.Remove("history"); entry["date"] = DateTimeOffset.UtcNow.ToString("O"); history.Add(entry); }
        else history.Add(new JsonObject { ["date"] = DateTimeOffset.UtcNow.ToString("O"), ["catalogue"] = JsonSerializer.SerializeToNode(LegacyCatalogue(source)),
            ["mappings"] = JsonSerializer.SerializeToNode(BridgeDesignModel.Rates.Select(r => new WorkPriceMapping(r.Key, r.Key, null, ""))), ["snapshot"] = JsonSerializer.SerializeToNode(previous) });
        Result = new JsonObject { ["schema"] = 1, ["catalogue"] = JsonSerializer.SerializeToNode(Catalogue()),
            ["mappings"] = JsonSerializer.SerializeToNode(Mappings.Select(m => m.Value())), ["snapshot"] = JsonSerializer.SerializeToNode(proposed),
            ["history"] = history };
        Applied?.Invoke();
    }
    public void CommitTo(JsonObject target)
    {
        if (Result is null) throw new InvalidOperationException("Applicare prima una bozza valida.");
        if (!JsonNode.DeepEquals(source, target)) throw new InvalidOperationException("Il ponte è cambiato durante la modifica del listino. Riaprire la gestione prezzari.");
        JsonObject? pinnedPrices = null;
        if (RepricePinnedAlternative && target["alternative_a"] is JsonObject pinned)
        {
            pinnedPrices = (JsonObject)Result.DeepClone();
            var history = pinned["pricing"]?["history"]?.DeepClone() as JsonArray ?? [];
            JsonObject old;
            if (pinned["pricing"] is JsonObject existing) { old = (JsonObject)existing.DeepClone(); old.Remove("history"); }
            else
            {
                var catalogue = LegacyCatalogue(pinned);
                old = new JsonObject { ["catalogue"] = JsonSerializer.SerializeToNode(catalogue), ["snapshot"] = JsonSerializer.SerializeToNode(LegacySnapshot(catalogue)),
                    ["mappings"] = JsonSerializer.SerializeToNode(BridgeDesignModel.Rates.Select(r => new WorkPriceMapping(r.Key, r.Key, null, ""))) };
            }
            old["date"] = DateTimeOffset.UtcNow.ToString("O"); history.Add(old); pinnedPrices["history"] = history;
        }
        target["pricing"] = Result.DeepClone();
        if (pinnedPrices is not null) target["alternative_a"]!["pricing"] = pinnedPrices;
    }
    private static PriceCatalogue LegacyCatalogue(JsonObject data) => new("Valori dell'archivio", "Conservati", "EUR",
        BridgeDesignModel.Rates.Select(r => new PriceEntry(r.Key, r.Label, r.Unit[2..], PriceCatalogueImport.Number(data["rates"].S(r.Key)), [])).ToArray());
    private static BridgePriceSnapshot LegacySnapshot(PriceCatalogue catalogue) => new("legacy:" + Fingerprint(JsonSerializer.SerializeToNode(catalogue)!), "EUR",
        catalogue.Entries.Select(e => new BridgePriceRate(e.Code, e.Unit, e.Price, "Archivio/" + e.Code, e.Includes)).ToArray());
    private static string Fingerprint(JsonNode node) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(node.ToJsonString())));
    private static string Format(decimal? value) => value?.ToString("0.####", CultureInfo.GetCultureInfo("it-IT")) ?? "";
}
