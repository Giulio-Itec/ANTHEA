using System.Globalization;
using System.Text;
using System.Text.Json;
using GPC.Converter;
using GPC.Converter.MidasCivil;
using GPC.Model.Core.Diagnostics;

namespace ANTHEA.ModelWorkspace;

/// <summary>MIDAS Civil (.mct) and Gen (.mgt) text models. The GPC converter translates the commands into the Civil NX tables
/// (N, mm, °C) and maps them; this adapter builds the presentation snapshot from those tables with the same core as the API copy and adds
/// the GPC frames, assignments and diagnostics. Geometry, attributes, loads and combinations only: a model file has no analysis results.</summary>
public static class MidasTextImporter
{
    public static bool IsTextModel(string path) => Path.GetExtension(path).ToLowerInvariant() is ".mct" or ".mgt";

    public static ModelSnapshot Read(string path, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) throw new InvalidDataException("File di testo MIDAS non trovato.");
        token.ThrowIfCancellationRequested();
        string file = Path.GetFileName(path);
        MidasTextTables tables;
        try { tables = MidasTextFiles.ReadTables(path, "ANTHEA " + file, null, TextEncoding(path), token); }
        catch (NotSupportedException) { throw new InvalidDataException("Il file non è un modello di testo MIDAS Civil (.mct) o Gen (.mgt)."); }
        catch (IOException ex) { throw new InvalidDataException("File di testo MIDAS non leggibile: " + ex.Message, ex); }
        if (tables.Batch == null)
        {
            var error = tables.Diagnostics.FirstOrDefault(d => d.Severity == DiagnosticSeverity.Error);
            throw new InvalidDataException("File MIDAS non importato" + (error?.Record is { Length: > 0 } record ? " (" + record + ")" : "") + ": " + (error?.Message ?? "lettura non riuscita") +
                ". Correggi il file o ripeti l’esportazione da MIDAS. La copia precedente è stata conservata.");
        }
        token.ThrowIfCancellationRequested();
        var json = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var table in tables.Tables)
        {
            using var document = JsonDocument.Parse(table.Value);
            if (!document.RootElement.TryGetProperty(table.Key, out var rows)) throw new InvalidDataException("Tabella tradotta non riconosciuta: " + table.Key);
            json.Add(table.Key, rows.Clone());
        }
        bool gen = tables.Program == MidasTextFiles.GenProgram;
        string extension = Path.GetExtension(path).ToLowerInvariant();
        var selection = new MidasTextSelection();
        ModelSnapshot snapshot;
        try { snapshot = MidasSnapshotImporter.FromTables(json, Path.GetFileNameWithoutExtension(path), $"{tables.Program} · file {extension} · {file} · SHA-256 {tables.SourceHash[..12]}", selection); }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        { throw new InvalidDataException("Tabelle del file MIDAS non gestite dalla vista: " + ex.Message + ". La copia precedente è stata conservata.", ex); }
        token.ThrowIfCancellationRequested();
        // The same mapping as the GPC file adapter: a model GPC rejects is not published.
        var mapped = ModelMapper.Map(tables.Batch, token);
        token.ThrowIfCancellationRequested();
        if (mapped.Model == null)
            throw new InvalidDataException("Il convertitore GPC ha rifiutato il modello: " + string.Join("; ", mapped.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Take(3)
                .Select(d => (d.Record is { Length: > 0 } r ? r + ": " : "") + d.Message)) + ". La copia precedente è stata conservata.");
        GpcBatchNormalization.Apply(snapshot, tables.Batch, mapped.Diagnostics);
        snapshot.ImportMessages = [.. selection.Messages, .. snapshot.ImportMessages ?? [],
            new("MIDAS-TEXT", "info", "Importazione", $"{(gen ? "MIDAS Gen" : "MIDAS Civil")} da file di testo {extension}: geometria, sezioni, spessori, vincoli, molle, link, rilasci, carichi e combinazioni tradotti dal convertitore GPC nelle tabelle Civil NX (N, mm). Il file non contiene risultati di analisi."),
            new("MIDAS-TEXT-SOURCE", "info", "Sorgente", $"{file} · SHA-256 {tables.SourceHash} · {tables.Tables.Count} tabelle · {tables.Batch.Nodes.Count:N0} nodi, {tables.Batch.Beams.Count:N0} aste, {tables.Batch.Shells.Count:N0} piastre nel modello GPC.")];
        snapshot.Validate();
        return snapshot;
    }

    /// <summary>MIDAS writes ANSI text; a UTF-8 byte order mark identifies the files saved again as UTF-8.</summary>
    static Encoding TextEncoding(string path)
    {
        Span<byte> head = stackalloc byte[3];
        using (var stream = File.OpenRead(path)) { int read = stream.Read(head); if (read == 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF) return new UTF8Encoding(false, true); }
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }
}

/// <summary>Elements, sections and thicknesses of a MIDAS text file that the presentation snapshot cannot hold as they are: element families
/// without a viewer element (walls, plane and solid elements), plates without a single physical thickness, members of undefined sections.
/// They are left out or kept without outline, never reshaped, and reported as import messages.</summary>
internal sealed class MidasTextSelection
{
    readonly Dictionary<string, int> excludedFamilies = new(StringComparer.Ordinal);
    readonly HashSet<int> unsupportedThickness = [];
    public List<ModelImportMessage> Messages { get; } = [];

    public ModelElement[] Elements(JsonElement.ObjectEnumerator rows)
    {
        var result = new List<ModelElement>();
        foreach (var p in rows)
        {
            string type = p.Value.TryGetProperty("TYPE", out var kind) ? kind.GetString() ?? "" : "";
            int[] nodes = p.Value.GetProperty("NODE").EnumerateArray().Select(n => n.GetInt32()).Where(n => n > 0).ToArray();
            bool supported = type == "PLATE" && nodes.Length is 3 or 4 || ElementFamilies.IsLine(type) && nodes.Length == 2;
            if (!supported) { excludedFamilies[type] = excludedFamilies.GetValueOrDefault(type) + 1; continue; }
            int property = p.Value.TryGetProperty("SECT", out var section) && section.ValueKind == JsonValueKind.Number ? section.GetInt32() : 0;
            result.Add(new(int.Parse(p.Name, CultureInfo.InvariantCulture), type, property, nodes, p.Value.TryGetProperty("ANGLE", out var a) && a.ValueKind == JsonValueKind.Number ? a.GetDouble() : 0));
        }
        return result.ToArray();
    }

    public void UnsupportedThickness(int id) => unsupportedThickness.Add(id);

    public void Complete(ModelSnapshot snapshot)
    {
        if (excludedFamilies.Count > 0)
            Messages.Add(new("MIDAS-TEXT-FAMILIES-EXCLUDED", "Warning", "ELEM", $"{excludedFamilies.Values.Sum():N0} elementi senza equivalente nella vista: " +
                string.Join(", ", excludedFamilies.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => (p.Key.Length > 0 ? p.Key : "tipo non indicato") + " × " + p.Value.ToString("N0", CultureInfo.CurrentCulture))) +
                ". Esclusi dalla copia con carichi e gruppi; i loro nodi restano. Pareti ed elementi piani o solidi sono conservati nel file e nel modello GPC come record non interpretati."));
        var plates = snapshot.Plates.Select(p => p.Id).ToHashSet();
        var dropped = snapshot.Elements.Where(e => e.Type == "PLATE" && !plates.Contains(e.Property)).ToArray();
        if (dropped.Length > 0)
        {
            var unsupported = dropped.Where(e => unsupportedThickness.Contains(e.Property)).ToArray(); var missing = dropped.Except(unsupported).ToArray();
            if (unsupported.Length > 0)
                Messages.Add(new("MIDAS-TEXT-THICKNESS-UNSUPPORTED", "Warning", "THIK", $"{unsupported.Length:N0} piastre escluse: gli spessori {string.Join(", ", unsupported.Select(e => e.Property).Distinct().Order())} " +
                    "sono irrigiditi o hanno spessori diversi nel piano e fuori piano. La vista richiede un unico spessore fisico; nessuno spessore equivalente è stato dedotto."));
            if (missing.Length > 0)
                Messages.Add(new("MIDAS-TEXT-THICKNESS-MISSING", "Warning", "ELEM", $"{missing.Length:N0} piastre escluse: lo spessore indicato ({string.Join(", ", missing.Select(e => e.Property).Distinct().Order())}) non è definito nel file."));
            var removed = dropped.Select(e => e.Id).ToHashSet();
            snapshot.Elements = snapshot.Elements.Where(e => !removed.Contains(e.Id)).ToArray();
        }
        var sections = snapshot.Sections.Select(s => s.Id).ToHashSet();
        var undefined = snapshot.Elements.Where(e => ElementFamilies.IsLine(e.Type) && !sections.Contains(e.Property)).Select(e => e.Property).Distinct().Order().ToArray();
        if (undefined.Length > 0)
        {
            snapshot.Sections = [.. snapshot.Sections, .. undefined.Select(id => new SectionProperty(id, "Sezione non definita nel file", "", 0, 0, false))];
            Messages.Add(new("MIDAS-TEXT-SECTION-MISSING", "Warning", "ELEM", $"Aste con sezioni non definite nel file ({string.Join(", ", undefined)}): rappresentate ad asse, senza profilo. Il modello GPC le conserva senza proprietà."));
        }
    }
}
