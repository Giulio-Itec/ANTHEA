using System.Text.Json.Nodes;

namespace Anthea.Application.Stratigraphy;

/// <summary>
/// Source of the drafts of the AI import: <see cref="StratigraphyImport.OpenAi()"/> in the product, a scripted source in the checks,
/// which never call the network or a real AI service.
/// </summary>
public interface IStratigraphyDraftSource
{
    /// <summary>The drafts of the PDF at <paramref name="path"/>, with the errors of <see cref="StratigraphyAiImporter.ExtractAsync"/>.</summary>
    Task<IReadOnlyList<JsonObject>> ExtractAsync(string path, string apiKey, string model, CancellationToken cancellation);
}

/// <summary>
/// Model and key proposed when the import opens. The key stays in memory: it is never saved in the document, and
/// <see cref="ToString"/> only says whether it is there.
/// </summary>
public sealed class StratigraphyImportDefaults(string model, string apiKey)
{
    public string Model { get; } = model ?? "";
    public string ApiKey { get; } = apiKey ?? "";
    public override string ToString() => "modello «" + Model + "», chiave " + (ApiKey.Length == 0 ? "assente" : "presente");
}

/// <summary>
/// The import of a geological report with AI (STRAT-2, disabled in the Release on request of the user), without WPF: moved in F5.6b
/// from StratigraphyImportWindow, which keeps only the state of its controls, with the same texts, the same errors and the same rules,
/// so that the window and the ViewModel of the import use one implementation. The HTTP client of the window is created here,
/// one per extraction (<see cref="OpenAi()"/>), as the window created it.
/// </summary>
public static class StratigraphyImport
{
    /// <summary>Caption of the window of the import.</summary>
    public const string Title = "Importa relazione geologica · bozza AI";
    /// <summary>Why the import button of the sheet is disabled (its tooltip).</summary>
    public const string Disabled = "Importazione AI temporaneamente disattivata: configurazione e fatturazione aziendale da definire.";
    /// <summary>The message when the window opens.</summary>
    public const string Ready = "Selezionare il PDF, indicare il modello e la propria chiave API.";
    /// <summary>The message while the report is read.</summary>
    public const string Reading = "Lettura della relazione in corso…";
    /// <summary>The message of an interrupted extraction (the cancel button, the closing of the window or the time limit).</summary>
    public const string Interrupted = "Importazione interrotta o tempo massimo raggiunto. Nessun dato applicato.";
    /// <summary>The message of a failed connection.</summary>
    public const string ConnectionFailed = "Connessione al servizio non riuscita. Nessun dato applicato.";
    /// <summary>The message of a key or a configuration in a wrong format.</summary>
    public const string InvalidConfiguration = "Formato della chiave o della configurazione non valido.";

    /// <summary>
    /// The extraction of the product: <see cref="StratigraphyAiImporter"/> with a new HTTP client without redirects and without its own
    /// timeout (the importer stops after three minutes), disposed at the end of the extraction.
    /// </summary>
    public static IStratigraphyDraftSource OpenAi() => OpenAi(() => new HttpClientHandler { AllowAutoRedirect = false });

    /// <summary>The same extraction with another handler of the requests: in the checks a scripted one, never the network.</summary>
    public static IStratigraphyDraftSource OpenAi(Func<HttpMessageHandler> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return new ClientPerExtraction(handler);
    }

    /// <summary>Model and key proposed by the environment (ANTHEA_AI_MODEL, OPENAI_API_KEY), read when the window opens.</summary>
    public static StratigraphyImportDefaults EnvironmentDefaults() =>
        new(Environment.GetEnvironmentVariable("ANTHEA_AI_MODEL") ?? "", Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "");

    /// <summary>
    /// True for the errors of an extraction that the window shows in its message (<see cref="Failure"/>) instead of the drafts: the
    /// interruption, invalid input or answer, HTTP and file errors, formats. Any other error is not handled by the import.
    /// </summary>
    public static bool Shown(Exception error) => error is OperationCanceledException or ArgumentException or InvalidOperationException
        or HttpRequestException or IOException or UnauthorizedAccessException or FormatException;

    /// <summary>The message of an error of the extraction (<see cref="Shown"/>).</summary>
    public static string Failure(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error switch
        {
            OperationCanceledException => Interrupted,
            HttpRequestException => ConnectionFailed,
            FormatException => InvalidConfiguration,
            _ => error.Message
        };
    }

    /// <summary>The items of the choice of the profile: number and name of every draft.</summary>
    public static IReadOnlyList<string> ProfileNames(IReadOnlyList<JsonObject> drafts)
    {
        ArgumentNullException.ThrowIfNull(drafts);
        return drafts.Select((d, i) => $"{i + 1} · {d["name"]}").ToArray();
    }

    /// <summary>The message shown with a draft: the warnings of the extraction, to be checked against the report.</summary>
    public static string DraftMessage(JsonObject draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return "Bozza AI da controllare · " + draft["import_source"]!["warnings"];
    }

    /// <summary>
    /// The reviewed draft that replaces the stratigraphy: validated (an invalid draft throws <see cref="ArgumentException"/>, shown with
    /// <see cref="ApplyFailure"/>), copied and marked as reviewed.
    /// </summary>
    public static JsonObject Apply(JsonObject draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        StratigraphyDocument.Validate(draft);
        var result = draft.DeepClone().AsObject();
        result["import_source"]!["reviewed"] = true;
        return result;
    }

    /// <summary>The message of a draft that cannot be applied.</summary>
    public static string ApplyFailure(ArgumentException error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return "Correggere la bozza prima di applicarla: " + error.Message;
    }

    /// <summary>The data of the sheet become the applied draft, key by key in its order (the import of the sheet).</summary>
    public static void Replace(JsonObject data, JsonObject result)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(result);
        data.Clear();
        foreach (var pair in result) data[pair.Key] = pair.Value?.DeepClone();
    }

    private sealed class ClientPerExtraction(Func<HttpMessageHandler> handler) : IStratigraphyDraftSource
    {
        public async Task<IReadOnlyList<JsonObject>> ExtractAsync(string path, string apiKey, string model, CancellationToken cancellation)
        {
            using var http = new HttpClient(handler()) { Timeout = Timeout.InfiniteTimeSpan };
            return await new StratigraphyAiImporter(http).ExtractAsync(path, apiKey, model, cancellation).ConfigureAwait(false);
        }
    }
}
