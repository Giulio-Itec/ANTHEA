using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Calculations;

namespace Anthea.Application.BridgeDesign;

/// <summary>A priced entry of the catalogue as the editor shows it: the texts of its cells.</summary>
public sealed record PriceEntryDraft(string Code, string Description, string Unit, string Price, string Includes);

/// <summary>An analysis of the catalogue as the editor shows it: components «codice = quantità» separated by «;», empty percentages from the catalogue.</summary>
public sealed record PriceAnalysisDraft(string Code, string Description, string Unit, string Components, string GeneralExpenses, string Profit);

/// <summary>The association of a work of the bridge as the editor shows it: entry code, override and its reason.</summary>
public sealed record WorkPriceDraft(string WorkCode, string EntryCode, string OverridePrice, string Reason);

/// <summary>A work of the bridge with its association, as the editor loads it.</summary>
public sealed record WorkPriceSource(string WorkCode, string Label, string Unit, string EntryCode, string OverridePrice, string Reason);

/// <summary>The catalogue being edited: name, edition, entries, default percentages of the analyses and analyses.</summary>
public sealed record PriceCatalogueDraft(string Name, string Edition, IReadOnlyList<PriceEntryDraft> Entries, string GeneralExpenses, string Profit,
    IReadOnlyList<PriceAnalysisDraft> Analyses);

/// <summary>A line of the estimate of the bridge with a set of prices.</summary>
public sealed record PriceEstimateLine(string Id, string WorkCode, decimal Quantity, string Unit, decimal? UnitPrice, decimal? Cost, string Issue, string? IncludedBy);

/// <summary>The estimate of the bridge with a set of prices: complete when every quantity has a price.</summary>
public sealed record PriceEstimate(string Revision, bool Complete, decimal? Direct, decimal? Total, PriceEstimateLine[] Lines);

/// <summary>A component of a resolved analysis.</summary>
public sealed record PriceAnalysisPart(string Code, decimal Quantity, string Unit, decimal UnitPrice, decimal Amount);

/// <summary>A resolved analysis: components, subtotal, general expenses and profit (with the percentages and whether the analysis states them), unit price.</summary>
public sealed record PriceAnalysisView(string Code, string Unit, PriceAnalysisPart[] Lines, decimal Subtotal, decimal GeneralExpensesPercent, decimal GeneralExpenses,
    decimal ProfitPercent, decimal Profit, decimal UnitPrice, bool GeneralExpensesOverridden, bool ProfitOverridden);

/// <summary>The prices of a work in the preview: before, after and the outcome of its association.</summary>
public sealed record WorkPricePreview(int Index, string PreviousPrice, string ResolvedPrice, string Status);

/// <summary>A set of prices proposed by the preview, applied by <see cref="PriceCatalogueEditing.Apply"/>.</summary>
public sealed class PriceProposal
{
    internal PriceProposal(BridgePriceSnapshot snapshot) => Snapshot = snapshot;
    internal BridgePriceSnapshot Snapshot { get; }
}

/// <summary>
/// The outcome of a preview of the editor, in the order in which the historic editor wrote it: the unit prices of the analyses (all reset,
/// then those resolved), the works processed (until the first refusal, which stops the preview: the works after it keep what they showed),
/// the estimate and the proposal when they were reached, then status, cost preview and «Applica», or the refusal.
/// </summary>
public sealed record PricePreviewOutcome(IReadOnlyDictionary<string, PriceAnalysisView> Analyses, IReadOnlyList<WorkPricePreview> Works, PriceEstimate? Estimate,
    PriceProposal? Proposal, bool CanApply, string Status, string CostPreview, string? Error);

/// <summary>
/// The price editor of Bridge Design without its rows (F5.12, decision Q17 a): what the ViewModel of the editor (PriceCatalogueViewModel,
/// moved to ANTHEA.Presentation) asks of the catalogue and of the prices of the bridge. Moved without changes from that ViewModel, which was
/// in ANTHEA.Application: the documents written, the preview, the texts and the refusals are the same; the types of the calculation
/// (BridgePricing, the snapshot and the analyses of ANTHEA.Calculations) stay here, behind the drafts of the rows.
/// </summary>
public sealed class PriceCatalogueEditing
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");
    private readonly JsonObject source;
    private readonly BridgePricing.Context context;
    private readonly BridgePriceSnapshot previous;
    private BridgePricing.Context? pinnedContext;

    /// <summary>
    /// Opens the editor on a copy of <paramref name="data"/>: the bridge without prices, the current price document (or the rates of the
    /// archive), the associations, the history. Throws <see cref="ArgumentException"/> when the bridge cannot be calculated or the document
    /// lacks its catalogue or associations.
    /// </summary>
    public PriceCatalogueEditing(JsonObject data)
    {
        source = (JsonObject)data.DeepClone(); context = BridgePricing.Prepare(source);
        if (source["pricing"] is JsonObject document && (document["catalogue"] is null || document["mappings"] is null)) throw new ArgumentException("Catalogo o associazioni mancanti nel documento prezzi.");
        var legacy = LegacyCatalogue(source);
        previous = source["pricing"]?["snapshot"]?.Deserialize<BridgePriceSnapshot>() ?? LegacySnapshot(legacy);
        var catalogue = source["pricing"]?["catalogue"]?.Deserialize<PriceCatalogue>() ?? legacy;
        var mappings = source["pricing"]?["mappings"]?.Deserialize<WorkPriceMapping[]>() ?? BridgeDesignModel.Rates.Select(r => new WorkPriceMapping(r.Key, r.Key, null, "")).ToArray();
        Works = BridgeDesignModel.Rates.Select(rate =>
        {
            var m = mappings.SingleOrDefault(m => m.WorkCode == rate.Key);
            return new WorkPriceSource(rate.Key, rate.Label, rate.Unit[2..], m?.EntryCode ?? "", Format(m?.Override), m?.Reason ?? "");
        }).ToArray();
        Catalogue = Load(catalogue);
        var history = new List<(string, JsonObject)>();
        if (source["pricing"]?["history"] is JsonArray items)
            foreach (var item in items.OfType<JsonObject>()) history.Add((item.S("date") + " · " + item["catalogue"].S("Name") + " / " + item["catalogue"].S("Edition"), (JsonObject)item.DeepClone()));
        History = history;
    }

    /// <summary>The works of the bridge with their associations, in the order of the rates of the model.</summary>
    public IReadOnlyList<WorkPriceSource> Works { get; }

    /// <summary>The catalogue of the document (or of the rates of the archive).</summary>
    public PriceCatalogueDraft Catalogue { get; }

    /// <summary>The revisions of the document: caption and document.</summary>
    public IReadOnlyList<(string Caption, JsonObject Document)> History { get; }

    /// <summary>The bridge has a pinned alternative A.</summary>
    public bool HasPinnedAlternative => source["alternative_a"] is JsonObject;

    /// <summary>
    /// The catalogue of a file: CSV and Excel carry priced entries only, so the analyses and their percentages of <paramref name="current"/>
    /// are kept (a missing component then shows in the preview); a JSON catalogue replaces them too. Refused as the historic editor refused it.
    /// </summary>
    public static PriceCatalogueDraft Import(byte[] bytes, string fileName, PriceCatalogueDraft current)
    {
        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        var catalogue = extension switch
        {
            ".xlsx" => PriceCatalogueImport.Excel(bytes, Path.GetFileNameWithoutExtension(fileName)),
            ".csv" => PriceCatalogueImport.Csv(bytes, Path.GetFileNameWithoutExtension(fileName)),
            ".json" => JsonSerializer.Deserialize<PriceCatalogue>(bytes) ?? throw new ArgumentException("Catalogo JSON vuoto."),
            _ => throw new ArgumentException("Usare CSV, XLSX oppure un catalogo JSON ANTHEA.")
        };
        if (extension != ".json" && current.Analyses.Count > 0)
            catalogue = catalogue with { GeneralExpensesPercent = PriceCatalogueImport.Number(current.GeneralExpenses), ProfitPercent = PriceCatalogueImport.Number(current.Profit),
                Analyses = current.Analyses.Select(Analysis).ToArray() };
        else PriceCatalogueImport.Validate(catalogue);
        return Load(catalogue);
    }

    /// <summary>The catalogue and the associations of a revision of the history.</summary>
    public static (PriceCatalogueDraft Catalogue, IReadOnlyList<WorkPriceDraft> Works) Revision(JsonObject document)
    {
        var c = document["catalogue"]!.Deserialize<PriceCatalogue>()!;
        var rows = document["mappings"]!.Deserialize<WorkPriceMapping[]>()!;
        var loaded = Load(c);
        return (loaded, rows.Select(r => new WorkPriceDraft(r.WorkCode, r.EntryCode, Format(r.Override), r.Reason)).ToArray());
    }

    /// <summary>The catalogue as a JSON file (indented); refused when the draft is not valid.</summary>
    public static byte[] Export(PriceCatalogueDraft draft) => JsonSerializer.SerializeToUtf8Bytes(Build(draft), new JsonSerializerOptions { WriteIndented = true });

    /// <summary>The components of an analysis as the editor writes them.</summary>
    public static string Components(PriceAnalysisComponent[] parts) => string.Join("; ", parts.Select(p => p.Code + " = " + p.Quantity.ToString("0.######", It)));

    /// <summary>The breakdown of a resolved analysis shown under the table of the analyses.</summary>
    public static string Breakdown(PriceAnalysisView r, string description)
    {
        string Money(decimal v) => v.ToString("N2", It) + " €";
        var b = new System.Text.StringBuilder($"{r.Code} · {description} · prezzo per {r.Unit}\n");
        foreach (var l in r.Lines) b.AppendLine($"{l.Code}: {l.Quantity.ToString("0.######", It)} {l.Unit} × {Money(l.UnitPrice)}/{l.Unit} = {Money(l.Amount)}");
        b.AppendLine($"Totale componenti: {Money(r.Subtotal)}");
        b.AppendLine($"Spese generali {r.GeneralExpensesPercent.ToString("0.##", It)} %{(r.GeneralExpensesOverridden ? " (valore dell'analisi)" : "")}: {Money(r.GeneralExpenses)}");
        b.AppendLine($"Utile d'impresa {r.ProfitPercent.ToString("0.##", It)} % su componenti e spese generali{(r.ProfitOverridden ? " (valore dell'analisi)" : "")}: {Money(r.Profit)}");
        b.Append($"Prezzo unitario: {Money(r.UnitPrice)}/{r.Unit}");
        return b.ToString();
    }

    /// <summary>The text shown for an analysis that the preview did not resolve.</summary>
    public const string UnresolvedAnalysis = "Analisi non risolta: vedere l'esito in basso.";

    /// <summary>The cost preview when the preview is refused.</summary>
    public const string PreviewUnavailable = "Anteprima non disponibile";

    /// <summary>
    /// The preview of the draft: the analyses resolved, every work with its price before and after and the outcome of its association, the
    /// estimate of the bridge and the proposal; with <paramref name="repricePinned"/> the proposal must also price the alternative A.
    /// </summary>
    public PricePreviewOutcome Preview(PriceCatalogueDraft draft, IReadOnlyList<WorkPriceDraft> works, bool repricePinned)
    {
        IReadOnlyDictionary<string, PriceAnalysisView> analyses = new Dictionary<string, PriceAnalysisView>();
        var processed = new List<WorkPricePreview>();
        PriceEstimate? estimate = null; PriceProposal? proposal = null;
        try
        {
            var c = Build(draft); var entries = c.Entries.ToDictionary(e => e.Code, StringComparer.Ordinal); var rates = new List<BridgePriceRate>();
            // Analyses price like entries: their unit price includes general expenses and profit
            var resolved = PriceCatalogueImport.ResolveAnalyses(c);
            analyses = resolved.ToDictionary(p => p.Key, p => View(p.Value), StringComparer.Ordinal);
            foreach (var a in c.Analyses ?? []) entries[a.Code] = new PriceEntry(a.Code, a.Description, a.Unit, resolved[a.Code].UnitPrice, []);
            var mapping = works.Select(Mapping).ToArray();
            for (int index = 0; index < works.Count; index++)
            {
                var row = works[index];
                string unit = Unit(row.WorkCode);
                var m = mapping.Single(v => v.WorkCode == row.WorkCode); entries.TryGetValue(m.EntryCode, out var e);
                var old = previous.Rates.SingleOrDefault(r => r.WorkCode == row.WorkCode);
                string previousPrice = old?.Price is decimal op ? Format(BridgePricing.ConvertPrice(op, old.Unit, unit)) : "—";
                processed.Add(new(index, previousPrice, "—", "Codice non associato"));
                if (m.Override.HasValue && m.Reason.Length == 0) throw new ArgumentException(Label(row.WorkCode) + ": motivare il prezzo personalizzato.");
                if (e is null && !m.Override.HasValue) continue;
                string priceUnit = m.Override.HasValue ? unit : e!.Unit;
                decimal? value = m.Override ?? e?.Price;
                try
                {
                    decimal? normalized = value.HasValue ? BridgePricing.ConvertPrice(value.Value, priceUnit, unit) : null;
                    processed[^1] = new(index, previousPrice, Format(normalized), normalized.HasValue ? m.Override.HasValue ? "Personalizzato · " + m.Reason : resolved.ContainsKey(m.EntryCode) ? "Da analisi" : "Da catalogo" : "Prezzo mancante");
                    rates.Add(new(row.WorkCode, priceUnit, value, c.Name + "/" + c.Edition + "/" + (m.Override.HasValue ? "override:" + m.Reason : e!.Code), e?.Includes ?? []));
                }
                catch (ArgumentException) { processed[^1] = processed[^1] with { Status = "Unità incompatibile: " + priceUnit + " → " + unit }; }
            }
            string revision = Fingerprint(JsonSerializer.SerializeToNode(new { catalogue = c, mappings = mapping })!);
            var snapshot = new BridgePriceSnapshot(revision, "EUR", rates.ToArray());
            proposal = new(snapshot); var current = context.Evaluate(snapshot); estimate = Estimate(current);
            var oldEstimate = context.Evaluate(previous);
            string costPreview = "Costo totale prima: " + (oldEstimate.Total?.ToString("N2", It) ?? "—") + " €   →   dopo: " + (current.Total?.ToString("N2", It) ?? "—") + " €";
            int unresolved = Enumerable.Range(0, works.Count).Select(i => processed.LastOrDefault(p => p.Index == i)?.Status ?? "").Count(s => s.StartsWith("Codice", StringComparison.Ordinal)
                || s.StartsWith("Unità", StringComparison.Ordinal) || s == "Prezzo mancante");
            string status = current.Complete ? $"Anteprima completa per questa geometria. {unresolved} associazioni non risolte nel catalogo complessivo. Le alternative che le richiedono saranno escluse."
                : "Computo incompleto: " + string.Join(", ", current.Lines.Where(l => l.Issue != "None").Select(l => l.WorkCode + " · " + l.Issue).Distinct());
            bool canApply = current.Complete;
            if (canApply && repricePinned && source["alternative_a"] is JsonObject pinned)
            {
                pinnedContext ??= BridgePricing.Prepare(pinned);
                var alternative = pinnedContext.Evaluate(snapshot);
                if (!alternative.Complete) { canApply = false; status = "Il listino non copre le lavorazioni dell'alternativa A: completare le associazioni oppure deselezionare la sua rivalutazione."; }
            }
            return new(analyses, processed, estimate, proposal, canApply, status, costPreview, null);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or OverflowException or JsonException)
        { return new(analyses, processed, estimate, proposal, false, e.Message, PreviewUnavailable, e.Message); }
    }

    /// <summary>
    /// «Applica prezzario»: the new price document (schema 1): catalogue, associations, the proposal as the snapshot of the prices, and the
    /// history with the current document (or the rates of the archive) dated now.
    /// </summary>
    public JsonObject Apply(PriceCatalogueDraft draft, IReadOnlyList<WorkPriceDraft> works, PriceProposal proposal)
    {
        var history = source["pricing"]?["history"]?.DeepClone() as JsonArray ?? [];
        if (source["pricing"] is JsonObject old)
        { var entry = (JsonObject)old.DeepClone(); entry.Remove("history"); entry["date"] = DateTimeOffset.UtcNow.ToString("O"); history.Add(entry); }
        else history.Add(new JsonObject { ["date"] = DateTimeOffset.UtcNow.ToString("O"), ["catalogue"] = JsonSerializer.SerializeToNode(LegacyCatalogue(source)),
            ["mappings"] = JsonSerializer.SerializeToNode(BridgeDesignModel.Rates.Select(r => new WorkPriceMapping(r.Key, r.Key, null, ""))), ["snapshot"] = JsonSerializer.SerializeToNode(previous) });
        return new JsonObject { ["schema"] = 1, ["catalogue"] = JsonSerializer.SerializeToNode(Build(draft)),
            ["mappings"] = JsonSerializer.SerializeToNode(works.Select(Mapping)), ["snapshot"] = JsonSerializer.SerializeToNode(proposal.Snapshot),
            ["history"] = history };
    }

    /// <summary>
    /// The document applied goes into <paramref name="target"/>, the data of the bridge, unchanged since the editor opened; with
    /// <paramref name="repricePinned"/> also into the alternative A, whose previous prices go into its history.
    /// </summary>
    public void CommitTo(JsonObject? result, JsonObject target, bool repricePinned)
    {
        if (result is null) throw new InvalidOperationException("Applicare prima una bozza valida.");
        if (!JsonNode.DeepEquals(source, target)) throw new InvalidOperationException("Il ponte è cambiato durante la modifica del listino. Riaprire la gestione prezzari.");
        JsonObject? pinnedPrices = null;
        if (repricePinned && target["alternative_a"] is JsonObject pinned)
        {
            pinnedPrices = (JsonObject)result.DeepClone();
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
        target["pricing"] = result.DeepClone();
        if (pinnedPrices is not null) target["alternative_a"]!["pricing"] = pinnedPrices;
    }

    // ---------------------------------------------------------------- conversions

    private static string Label(string work) => BridgeDesignModel.Rates.Single(r => r.Key == work).Label;

    private static string Unit(string work) => BridgeDesignModel.Rates.Single(r => r.Key == work).Unit[2..];

    private static PriceCatalogueDraft Load(PriceCatalogue catalogue)
    {
        // Kept analyses may refer to codes of the previous list: the preview reports them instead of refusing the import
        PriceCatalogueImport.Validate(catalogue with { Analyses = null });
        return new(catalogue.Name, catalogue.Edition,
            catalogue.Entries.Select(e => new PriceEntryDraft(e.Code, e.Description, e.Unit, Format(e.Price), string.Join('|', e.Includes))).ToArray(),
            Format(catalogue.GeneralExpensesPercent), Format(catalogue.ProfitPercent),
            (catalogue.Analyses ?? []).Select(a => new PriceAnalysisDraft(a.Code, a.Description, a.Unit, Components(a.Components ?? []), Format(a.GeneralExpensesPercent), Format(a.ProfitPercent))).ToArray());
    }

    private static PriceEntry Entry(PriceEntryDraft e) => new(e.Code.Trim(), e.Description.Trim(), e.Unit.Trim(), PriceCatalogueImport.Number(e.Price),
        e.Includes.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static WorkPriceMapping Mapping(WorkPriceDraft w) => new(w.WorkCode, w.EntryCode.Trim(), PriceCatalogueImport.Number(w.OverridePrice), w.Reason.Trim());

    private static PriceAnalysisEntry Analysis(PriceAnalysisDraft a) => new(a.Code.Trim(), a.Description.Trim(), a.Unit.Trim(), Parse(a.Components),
        PriceCatalogueImport.Number(a.GeneralExpenses), PriceCatalogueImport.Number(a.Profit));

    private static PriceAnalysisComponent[] Parse(string text) => text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(part =>
    {
        var pieces = part.Split('=', StringSplitOptions.TrimEntries);
        if (pieces.Length != 2 || pieces[0].Length == 0 || PriceCatalogueImport.Number(pieces[1]) is not decimal quantity)
            throw new ArgumentException("Componenti dell'analisi: scrivere «codice = quantità» separati da punto e virgola.");
        return new PriceAnalysisComponent(pieces[0], quantity);
    }).ToArray();

    // The catalogue of the draft, validated: analyses, entries, then the default percentages, in the order of the historic editor.
    private static PriceCatalogue Build(PriceCatalogueDraft draft)
    {
        var analyses = draft.Analyses.Select(Analysis).ToArray();
        var c = new PriceCatalogue(draft.Name.Trim(), draft.Edition.Trim(), "EUR", draft.Entries.Select(Entry).ToArray())
        {
            GeneralExpensesPercent = PriceCatalogueImport.Number(draft.GeneralExpenses), ProfitPercent = PriceCatalogueImport.Number(draft.Profit),
            Analyses = analyses.Length > 0 ? analyses : null
        };
        PriceCatalogueImport.Validate(c); return c;
    }

    private static PriceAnalysisView View(PriceAnalysisResult r) => new(r.Code, r.Unit, r.Lines.Select(l => new PriceAnalysisPart(l.Code, l.Quantity, l.Unit, l.UnitPrice, l.Amount)).ToArray(),
        r.Subtotal, r.GeneralExpensesPercent, r.GeneralExpenses, r.ProfitPercent, r.Profit, r.UnitPrice, r.GeneralExpensesOverridden, r.ProfitOverridden);

    private static PriceEstimate Estimate(BridgePriceEstimate e) => new(e.Revision, e.Complete, e.Direct, e.Total,
        e.Lines.Select(l => new PriceEstimateLine(l.Id, l.WorkCode, l.Quantity, l.Unit, l.UnitPrice, l.Cost, l.Issue, l.IncludedBy)).ToArray());

    private static PriceCatalogue LegacyCatalogue(JsonObject data) => new("Valori dell'archivio", "Conservati", "EUR",
        BridgeDesignModel.Rates.Select(r => new PriceEntry(r.Key, r.Label, r.Unit[2..], PriceCatalogueImport.Number(data["rates"].S(r.Key)), [])).ToArray());

    private static BridgePriceSnapshot LegacySnapshot(PriceCatalogue catalogue) => new("legacy:" + Fingerprint(JsonSerializer.SerializeToNode(catalogue)!), "EUR",
        catalogue.Entries.Select(e => new BridgePriceRate(e.Code, e.Unit, e.Price, "Archivio/" + e.Code, e.Includes)).ToArray());

    private static string Fingerprint(JsonNode node) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(node.ToJsonString())));

    /// <summary>A price or a percentage as the editor shows it: up to four decimals in Italian, empty without a value.</summary>
    public static string Format(decimal? value) => value?.ToString("0.####", It) ?? "";
}
