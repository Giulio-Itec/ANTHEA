using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Elements;

namespace ANTHEA.ModelWorkspace;

/// <summary>Straus7 conventions for <see cref="GpcModelSnapshot"/> on a model imported by the GPC Straus7 R3 converter. Reads only the
/// records the converter preserves ("Straus7 API" evidence: unit codes, element and property records); no native code, no file access.
/// Numbering: nodes and plates keep their Straus7 API numbers; beams, trusses and cables get <see cref="BeamIdOffset"/> + their number,
/// because Straus7 numbers beams and plates separately and the document needs one identity per element.
/// Cases: "1: Dead", as Straus7 names its linear static result cases. Properties: Straus7 number and name.</summary>
public static partial class Strand7Snapshot
{
    public const int BeamIdOffset = 1000000;
    public const string CombinationSource = "STRAUS7";
    const string Evidence = "Straus7 API";
    static readonly IReadOnlyList<double> LengthToMillimetres = [1000, 10, 1, 304.8, 25.4];

    [GeneratedRegex(@"^Case (\d+): (.*)$", RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex ModelCase();

    /// <summary>"Case 1: Dead" (GPC load case of the converter) → "1: Dead" (the Straus7 result case).</summary>
    public static string CaseName(string gpcName) => ModelCase().Match(gpcName) is { Success: true } m ? m.Groups[1].Value + ": " + m.Groups[2].Value : gpcName;

    public static GpcSnapshotOptions Options(GPC.Model.Models.Model model, string name, string source, string? datasetId, bool forces, bool displacements,
        IEnumerable<ModelImportMessage> messages)
    {
        var records = model.PreservedSourceData.Where(p => p.Kind == Evidence && p.SourceRecord != null && p.RawData != null)
            .GroupBy(p => p.SourceRecord, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().RawData, StringComparer.Ordinal);
        JsonElement? Record(string key)
        {
            if (!records.TryGetValue(key, out var raw)) return null;
            try { using var document = JsonDocument.Parse(raw); return document.RootElement.Clone(); } catch (JsonException) { return null; }
        }
        double? lengthToMm = Record("St7GetUnits") is { ValueKind: JsonValueKind.Array } units && units.GetArrayLength() > 0 && units[0].TryGetInt32(out int code)
            && code >= 0 && code < LengthToMillimetres.Count ? LengthToMillimetres[code] : null;
        int? Property(string family, Element element) => element.Source?.OriginalId is { } id && Record(family + "/" + id) is { ValueKind: JsonValueKind.Object } e
            && e.TryGetProperty("Property", out var p) && p.TryGetInt32(out int number) && number > 0 ? number : null;
        string Name(string family, int number) => Record("Property/" + family + "/" + number.ToString(CultureInfo.InvariantCulture)) is { ValueKind: JsonValueKind.Object } p
            && p.TryGetProperty("Name", out var n) && n.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(n.GetString()) ? n.GetString()!.Trim()
            : "Proprietà " + number.ToString(CultureInfo.InvariantCulture);
        // Thickness of the plate property when the converter mapped no physical thickness (for example a non-isotropic material).
        double? Thickness(int number) => lengthToMm is double factor && Record("Property/Plate/" + number.ToString(CultureInfo.InvariantCulture)) is { ValueKind: JsonValueKind.Object } p
            && p.TryGetProperty("Geometry", out var g) && g.ValueKind == JsonValueKind.Array && g.GetArrayLength() > 0 && g[0].TryGetDouble(out double t) && t > 0 ? t * factor : null;
        return new GpcSnapshotOptions
        {
            Name = name, Source = source, BeamIdOffset = BeamIdOffset, CaseName = CaseName, DatasetId = datasetId, Forces = forces, Displacements = displacements,
            CombinationSource = CombinationSource, Messages = messages.ToArray(),
            BeamProperty = beam => Property("Beam", beam) is int number ? new GpcPropertyIdentity(number, Name("Beam", number)) : null,
            PlateProperty = plate => Property("Plate", plate) is int number ? new GpcPropertyIdentity(number, Name("Plate", number), plate.Assignments.PhysicalThickness ?? Thickness(number)) : null
        };
    }

    /// <summary>GPC diagnostics as import messages; repeated codes are kept, each with its record.</summary>
    public static IEnumerable<ModelImportMessage> Diagnostics(IEnumerable<ModelDiagnostic> diagnostics) =>
        diagnostics.Select(d => new ModelImportMessage(d.Code, d.Severity.ToString(), d.Record ?? "", d.Message ?? ""));

    public static IEnumerable<ModelImportMessage> Conventions(bool results) =>
    [
        new("STRAUS7-NUMBERING", "info", "Numerazione", $"Nodi e piastre con i numeri API di Straus7; aste (beam, truss, cavi) con {BeamIdOffset:N0} + numero Straus7, perché Straus7 numera beam e plate separatamente. Proprietà con numero e nome di Straus7; casi «numero: nome» come i casi di risultato di Straus7."),
        new("STRAUS7-CONVENTIONS", "info", "Unità e segni", "Convertitore GPC Straus7 R3 (sola lettura): N e mm convertiti in m, kN/m e kN·m/m (piastre), kN e kN·m (aste). Segni come la copia MIDAS: Mxx, Myy, Mxy delle piastre positivi con trazione sulla faccia −z (opposti alla convenzione Straus7 e GPC, trazione su +z); My delle aste opposto a M1 GPC. Spostamenti globali in m, rotazioni in rad."),
        .. results ? new ModelImportMessage[] { new("STRAUS7-RESULTS", "info", "Risultati", "Statico lineare, casi primari scelti: piastre ai nodi di ogni elemento senza media fra elementi (valori estrapolati dal solutore), aste alle stazioni restituite da Straus7, spostamenti e rotazioni nodali globali. Le combinazioni LSA sono definizioni: i loro risultati si costruiscono in ANTHEA con GPC dai casi importati.") } : []
    ];
}
