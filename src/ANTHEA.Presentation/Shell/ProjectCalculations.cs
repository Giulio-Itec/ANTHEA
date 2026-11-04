using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Calculations;
using X.Core;

namespace Anthea.Presentation.Shell;

/// <summary>The state of the calculation of a sheet of the project (docs/sviluppo/progetto-sistema.md, step 4).</summary>
public enum SheetCalculationState
{
    /// <summary>Never calculated while the project is open.</summary>
    ToCalculate,
    /// <summary>Calculated, but its data changed since.</summary>
    ToRecalculate,
    /// <summary>Calculated with its current data, without a uniform outcome (the modules other than concrete and composite sections).</summary>
    Calculated,
    /// <summary>Calculated with its current data, every verification passes.</summary>
    Verified,
    /// <summary>Calculated with its current data, a verification fails.</summary>
    NotVerified
}

/// <summary>
/// The calculations of the sheets of an open project (step 4; decision of the user of 4/11/2026, «Solo in memoria»): for each sheet the hash
/// of the data it was calculated with and its outcome, in memory only. A calculation does not change the document, does not enter the undo
/// and is not saved: at the next opening every sheet is «da calcolare». The state compares the hash with the data of the sheet: those of
/// its open tab when it is open, otherwise those of the document.
/// </summary>
public sealed class ProjectCalculations
{
    private readonly Dictionary<string, (string Print, bool? Passed)> stamps = new(StringComparer.Ordinal);

    /// <summary>The data of the open tab of a sheet, null when it is not open (set by the workspace).</summary>
    internal Func<JsonObject, JsonObject?> Live { get; set; } = _ => null;

    /// <summary>The calculation or the results of a sheet changed (its identifier): the tree and the overview show its state again.</summary>
    public event EventHandler<string>? Changed;

    /// <summary>A calculation of the sheet <paramref name="id"/> was published for <paramref name="data"/>, with <paramref name="passed"/>.</summary>
    public void Record(string id, JsonObject data, bool? passed)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentNullException.ThrowIfNull(data);
        stamps[id] = (Print(data), passed);
        Changed?.Invoke(this, id);
    }

    /// <summary>The results of the sheet <paramref name="id"/> changed without a new calculation (its data were edited): its state is read again.</summary>
    public void Touch(string id) => Changed?.Invoke(this, id);

    /// <summary>The state of <paramref name="sheet"/>.</summary>
    public SheetCalculationState State(JsonObject sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        if (!stamps.TryGetValue(sheet.S("id"), out var stamp)) return SheetCalculationState.ToCalculate;
        if ((Live(sheet) ?? sheet["dati"] as JsonObject) is not { } data || Print(data) != stamp.Print) return SheetCalculationState.ToRecalculate;
        return stamp.Passed switch
        {
            true => SheetCalculationState.Verified,
            false => SheetCalculationState.NotVerified,
            _ => SheetCalculationState.Calculated
        };
    }

    /// <summary>The words of a state: «Verificato», «Non verificato», «Calcolato», «Da ricalcolare», «Da calcolare».</summary>
    public static string Text(SheetCalculationState state) => state switch
    {
        SheetCalculationState.Verified => "Verificato",
        SheetCalculationState.NotVerified => "Non verificato",
        SheetCalculationState.Calculated => "Calcolato",
        SheetCalculationState.ToRecalculate => "Da ricalcolare",
        _ => "Da calcolare"
    };

    /// <summary>The sign of a state in the tree: «✓», «✗», «●», «↻», «○».</summary>
    public static string Glyph(SheetCalculationState state) => state switch
    {
        SheetCalculationState.Verified => "✓",
        SheetCalculationState.NotVerified => "✗",
        SheetCalculationState.Calculated => "●",
        SheetCalculationState.ToRecalculate => "↻",
        _ => "○"
    };

    /// <summary>
    /// The states of the sheets of <paramref name="section"/> and of its subsections, for the overview: «2 verificati · 1 non verificato · 3 da
    /// calcolare», the states without sheets left out; empty without sheets or before any of them is calculated.
    /// </summary>
    public string Summary(JsonObject section)
    {
        ArgumentNullException.ThrowIfNull(section);
        var counts = ProjectSharedData.SubtreeSheets(section).GroupBy(State).ToDictionary(g => g.Key, g => g.Count());
        // Nothing calculated yet: nothing to say (every sheet is «da calcolare» at the opening).
        if (counts.Keys.All(state => state == SheetCalculationState.ToCalculate)) return "";
        string Part(SheetCalculationState state, string one, string many) => counts.TryGetValue(state, out int n) ? n + " " + (n == 1 ? one : many) : "";
        return string.Join(" · ", new[]
        {
            Part(SheetCalculationState.Verified, "verificato", "verificati"),
            Part(SheetCalculationState.NotVerified, "non verificato", "non verificati"),
            Part(SheetCalculationState.Calculated, "calcolato", "calcolati"),
            Part(SheetCalculationState.ToRecalculate, "da ricalcolare", "da ricalcolare"),
            Part(SheetCalculationState.ToCalculate, "da calcolare", "da calcolare")
        }.Where(p => p.Length > 0));
    }

    private static string Print(JsonObject data) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(data)));
}
