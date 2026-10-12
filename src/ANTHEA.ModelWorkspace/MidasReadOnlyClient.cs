using System.Net.Http;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ANTHEA.ModelWorkspace;

/// <summary>Only database GETs and result-table POSTs; no model mutation, analysis or automatic retry.</summary>
internal sealed class MidasReadOnlyClient(HttpClient http, MidasConnection connection, TimeSpan databaseTimeout, TimeSpan resultsTimeout)
{
    public const int MaximumTableBytes = 32_000_000;
    public async Task<JsonElement> DatabaseAsync(string table, CancellationToken token)
    {
        var root = await SendAsync("db/" + table, null, token).ConfigureAwait(false);
        if (root.TryGetProperty(table, out var data) && data.ValueKind == JsonValueKind.Object) return data;
        // MIDAS explicitly represents a database category with no records using an empty message.
        if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String && message.GetString() == "")
            return JsonSerializer.SerializeToElement(new Dictionary<string, object>());
        throw new InvalidDataException("MIDAS non ha restituito una tabella valida: " + table + ". Nessun aggiornamento applicato.");
    }

    public Task<JsonElement> ResultsAsync(Dictionary<string, object> argument, CancellationToken token) =>
        SendAsync("post/TABLE", JsonSerializer.Serialize(new { Argument = argument }), token);

    async Task<JsonElement> SendAsync(string endpoint, string? body, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(body == null ? databaseTimeout : resultsTimeout);
        try
        {
            using var request = new HttpRequestMessage(body == null ? HttpMethod.Get : HttpMethod.Post, new Uri(connection.BaseUri, endpoint));
            request.Headers.Add("MAPI-Key", connection.ApiKey);
            if (body != null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
            // The production handler disables redirects, including cross-host redirects with credentials.
            if ((int)response.StatusCode is >= 300 and < 400) throw new ModelSourceException(ModelSourceFailureKind.AccessDenied);
            if (response.Content.Headers.ContentLength > MaximumTableBytes) throw new InvalidDataException("Tabella MIDAS troppo grande. Seleziona meno casi di carico.");
            await using var input = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
            using var memory = new MemoryStream();
            var buffer = new byte[64 * 1024];
            int read;
            while ((read = await input.ReadAsync(buffer, deadline.Token).ConfigureAwait(false)) > 0)
            {
                if (memory.Length + read > MaximumTableBytes) throw new InvalidDataException("Tabella MIDAS troppo grande. Seleziona meno casi di carico.");
                memory.Write(buffer, 0, read);
            }
            string json = Encoding.UTF8.GetString(memory.GetBuffer(), 0, (int)memory.Length);
            if (!response.IsSuccessStatusCode) throw ModelSourceException.FromResponse((int)response.StatusCode, json);
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Risposta MIDAS non riconosciuta.");
            if (document.RootElement.TryGetProperty("error", out _)) throw ModelSourceException.FromResponse((int)response.StatusCode, json);
            return document.RootElement.Clone();
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new ModelSourceException(ModelSourceFailureKind.Timeout); }
        catch (HttpRequestException) { throw new ModelSourceException(ModelSourceFailureKind.Unavailable); }
        catch (JsonException) { throw new InvalidDataException("La risposta MIDAS non contiene dati validi. Riprova la connessione."); }
    }

    // Deterministic signatures ignore JSON property order and equivalent decimal spellings.
    internal static string Signature(IEnumerable<KeyValuePair<string, JsonElement>> tables)
    {
        using var memory = new MemoryStream();
        using (var writer = new Utf8JsonWriter(memory))
        {
            writer.WriteStartObject();
            foreach (var table in tables.OrderBy(t => t.Key, StringComparer.Ordinal))
            { writer.WritePropertyName(table.Key); Write(table.Value, writer); }
            writer.WriteEndObject();
        }
        return Convert.ToHexString(SHA256.HashData(memory.GetBuffer().AsSpan(0, (int)memory.Length)));
    }
    static void Write(JsonElement value, Utf8JsonWriter writer)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var p in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal)) { writer.WritePropertyName(p.Name); Write(p.Value, writer); }
                writer.WriteEndObject(); break;
            case JsonValueKind.Array:
                writer.WriteStartArray(); foreach (var item in value.EnumerateArray()) Write(item, writer); writer.WriteEndArray(); break;
            case JsonValueKind.Number when value.TryGetDecimal(out var number): writer.WriteRawValue(number.ToString("G29", CultureInfo.InvariantCulture)); break;
            default: value.WriteTo(writer); break;
        }
    }
}
