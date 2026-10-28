using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ANTHEA.ModelWorkspace;

public enum Strand7WorkerOperation { Inspect, Read }

/// <summary>One request of ANTHEA to the Straus7 worker (ANTHEA.Strand7Worker.exe), written as UTF-8 JSON in a file whose path is the
/// only argument of the process. Read uses the result case numbers and the source signature of a previous Inspect.</summary>
public sealed record Strand7WorkerRequest(int Protocol, Strand7WorkerOperation Operation, string ModelPath, string? ResultsPath, string? ApiLibraryPath,
    ModelUpdateScope Scope, int[] CaseNumbers, bool Forces, bool Displacements, string? ExpectedSignature, string OutputPath);

/// <summary>Protocol of the Straus7 worker, version 1:
/// <list type="bullet">
/// <item>request: JSON file (<see cref="Strand7WorkerRequest"/>), path as the only command-line argument;</item>
/// <item>standard output, one line per message, UTF-8: <c>PROGRESSO &lt;completati&gt; &lt;totale&gt; &lt;testo&gt;</c> (totale 0 = durata non nota),
/// <c>ERRORE &lt;testo in italiano&gt;</c> before a failure exit, <c>FATTO</c> after the result is in place;</item>
/// <item>result: Inspect writes a <see cref="Strand7ModelCatalog"/>, Read a <see cref="ModelSnapshot"/>, as JSON in <c>OutputPath</c>, first in
/// <c>OutputPath.parziale</c> and then renamed: a result file exists only when it is complete;</item>
/// <item>exit codes: <see cref="Success"/>, <see cref="InvalidRequest"/>, <see cref="SourceUnavailable"/>, <see cref="NativeUnavailable"/>,
/// <see cref="Rejected"/>, <see cref="Unexpected"/>;</item>
/// <item>cancellation: ANTHEA ends the process (and its tree). Nothing is published: the caller deletes its temporary folder.</item>
/// </list>
/// One API session at a time per process; the worker opens files read-only and never saves, solves or modifies a model.</summary>
public static class Strand7WorkerProtocol
{
    public const int Version = 1;
    public const string ProgressPrefix = "PROGRESSO", ErrorPrefix = "ERRORE", DoneLine = "FATTO", PartialSuffix = ".parziale";
    public const int Success = 0, InvalidRequest = 2, SourceUnavailable = 3, NativeUnavailable = 4, Rejected = 5, Unexpected = 6;
    public const string WorkerFileName = "ANTHEA.Strand7Worker.exe";
    static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

    public static string Progress(int completed, int total, string message) =>
        ProgressPrefix + " " + completed.ToString(CultureInfo.InvariantCulture) + " " + total.ToString(CultureInfo.InvariantCulture) + " " + OneLine(message);
    public static string Error(string message) => ErrorPrefix + " " + OneLine(message);
    static string OneLine(string text) => text.ReplaceLineEndings(" ").Trim();

    public static bool TryProgress(string line, out ModelImportProgress progress)
    {
        progress = new("", 0, 0);
        var parts = line.Split(' ', 4);
        if (parts.Length < 3 || parts[0] != ProgressPrefix || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int completed)
            || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int total) || completed > total && total > 0) return false;
        progress = new(parts.Length > 3 ? parts[3] : "", completed, total);
        return true;
    }
    public static string? ErrorText(string line) => line.StartsWith(ErrorPrefix + " ", StringComparison.Ordinal) ? line[(ErrorPrefix.Length + 1)..] : null;

    public static string Serialize(Strand7WorkerRequest request) => JsonSerializer.Serialize(request, Json);
    public static Strand7WorkerRequest ReadRequest(string path)
    {
        var request = JsonSerializer.Deserialize<Strand7WorkerRequest>(File.ReadAllText(path), Json) ?? throw new InvalidDataException("Richiesta vuota.");
        if (request.Protocol != Version) throw new InvalidDataException("Versione del protocollo non supportata: " + request.Protocol.ToString(CultureInfo.InvariantCulture) + ".");
        if (!Enum.IsDefined(request.Operation) || !Enum.IsDefined(request.Scope) || string.IsNullOrWhiteSpace(request.ModelPath) || string.IsNullOrWhiteSpace(request.OutputPath)
            || request.CaseNumbers == null || request.CaseNumbers.Any(n => n < 1) || request.CaseNumbers.Distinct().Count() != request.CaseNumbers.Length)
            throw new InvalidDataException("Richiesta incompleta o non valida.");
        return request;
    }

    /// <summary>Writes the complete result next to its final path, then renames it; an existing result is never replaced.</summary>
    public static void WriteResult<T>(string path, T value)
    {
        string partial = path + PartialSuffix;
        using (var stream = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            JsonSerializer.Serialize(stream, value, Json);
        File.Move(partial, path, false);
    }
    public static T ReadResult<T>(string path, long limit)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > limit) throw new InvalidDataException(ModelSnapshotFiles.PackageSizeMessage);
        return JsonSerializer.Deserialize<T>(stream, Json) ?? throw new InvalidDataException("Risultato del processo Straus7 vuoto.");
    }

    public static string ExitMeaning(int code) => code switch
    {
        InvalidRequest => "richiesta non valida",
        SourceUnavailable => "file del modello o dei risultati non disponibili o cambiati",
        NativeUnavailable => "API Straus7 non disponibile (installazione R3 a 64 bit o licenza API)",
        Rejected => "modello o risultati non importabili",
        _ => "errore imprevisto"
    };
}
