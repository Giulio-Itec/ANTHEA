using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Anthea.Application.Stratigraphy;

/// <summary>
/// F5.6b: the import of the AI drafts moved from StratigraphyImportWindow to Application (<see cref="StratigraphyImport"/>) and what the
/// stratigraphy sheet derives from its document (<see cref="StratigraphyPresentation"/>), with values written by hand: the texts and the
/// errors of the window, the HTTP client created for every extraction with a scripted handler (never the network), the application of a
/// reviewed draft and the replacement of the data of the sheet.
/// </summary>
internal static class StratigraphyImportChecks
{
    internal static int Run(string output)
    {
        int count = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message); count++; }
        void Reject<T>(Action action, string message) where T : Exception
        {
            try { action(); } catch (T) { count++; return; }
            throw new Exception(message);
        }

        // The texts of the window, unchanged.
        Check(StratigraphyImport.Title == "Importa relazione geologica · bozza AI", "Titolo della finestra");
        Check(StratigraphyImport.Ready == "Selezionare il PDF, indicare il modello e la propria chiave API.", "Messaggio iniziale");
        Check(StratigraphyImport.Reading == "Lettura della relazione in corso…", "Messaggio della lettura");
        Check(StratigraphyImport.Disabled == "Importazione AI temporaneamente disattivata: configurazione e fatturazione aziendale da definire.", "Importazione spenta");

        // The errors shown by the window, in its order: interruption first, then the HTTP and format errors, then the message.
        Check(StratigraphyImport.Shown(new TaskCanceledException()) && StratigraphyImport.Failure(new TaskCanceledException())
            == "Importazione interrotta o tempo massimo raggiunto. Nessun dato applicato.", "Interruzione");
        Check(StratigraphyImport.Failure(new HttpRequestException("rete")) == "Connessione al servizio non riuscita. Nessun dato applicato.", "Connessione");
        Check(StratigraphyImport.Failure(new UriFormatException("uri")) == "Formato della chiave o della configurazione non valido.", "Formato");
        foreach (var error in new Exception[] { new ArgumentException("argomento"), new InvalidOperationException("operazione"), new IOException("file"),
            new UnauthorizedAccessException("accesso"), new ObjectDisposedException("oggetto") })
            Check(StratigraphyImport.Shown(error) && StratigraphyImport.Failure(error) == error.Message, "Messaggio dell'errore " + error.GetType().Name);
        Check(!StratigraphyImport.Shown(new NullReferenceException()) && !StratigraphyImport.Shown(new Exception("altro")), "Errore non dell'importazione mostrato");

        // Drafts, their names and their message; the application of a reviewed draft.
        var first = StratigraphyDocument.Create(); first["name"] = "S1"; first["import_source"] = new JsonObject { ["warnings"] = "Controllare le unità", ["reviewed"] = false };
        var second = (JsonObject)first.DeepClone(); second["name"] = "S2";
        Check(StratigraphyImport.ProfileNames([first, second]).SequenceEqual(["1 · S1", "2 · S2"]), "Nomi dei profili");
        Check(StratigraphyImport.DraftMessage(first) == "Bozza AI da controllare · Controllare le unità", "Messaggio della bozza");
        var applied = StratigraphyImport.Apply(first);
        Check(applied["import_source"]!["reviewed"]!.GetValue<bool>() && first["import_source"]!["reviewed"]!.GetValue<bool>() == false
            && applied.Select(p => p.Key).SequenceEqual(first.Select(p => p.Key)), "Bozza applicata non copiata o non revisionata");
        var broken = (JsonObject)first.DeepClone(); StratigraphyDocument.Layers(broken)[0]!["thickness"] = null;
        try { StratigraphyImport.Apply(broken); throw new Exception("Bozza non valida applicata"); }
        catch (ArgumentException error)
        {
            Check(StratigraphyImport.ApplyFailure(error) == "Correggere la bozza prima di applicarla: Strato 1: lo spessore deve essere positivo.", "Bozza non valida");
        }
        var sheet = new JsonObject { ["altro"] = 1, ["name"] = "Vecchia" };
        StratigraphyImport.Replace(sheet, applied);
        Check(sheet.Select(p => p.Key).SequenceEqual(applied.Select(p => p.Key)) && JsonNode.DeepEquals(sheet, applied)
            && !ReferenceEquals(sheet["layers"], applied["layers"]), "Dati del foglio non sostituiti dalla bozza");

        // The key never appears in the text of the defaults.
        var defaults = new StratigraphyImportDefaults("modello-di-prova", "chiave-di-prova-non-valida");
        Check(!defaults.ToString().Contains("chiave-di-prova", StringComparison.Ordinal) && defaults.ToString().Contains("presente", StringComparison.Ordinal)
            && new StratigraphyImportDefaults("", "").ToString().Contains("assente", StringComparison.Ordinal), "Chiave nel testo dei valori proposti");

        // The extraction of the product with a scripted handler: one client per extraction, disposed with its handler at the end;
        // the request goes only to the handler (never the network); the errors and the cancellation are those of the importer.
        string pdf = Path.Combine(output, "importazione-f56b.pdf");
        File.WriteAllText(pdf, "%PDF-1.4\nsynthetic transport fixture");
        var handlers = new List<ScriptedHandler>();
        var source = StratigraphyImport.OpenAi(() => { var handler = new ScriptedHandler(Envelope()); handlers.Add(handler); return handler; });
        var drafts = source.ExtractAsync(pdf, "chiave-di-prova", "modello-di-prova", CancellationToken.None).GetAwaiter().GetResult();
        Check(drafts.Count == 1 && drafts[0]["name"]!.ToString() == "S1" && handlers.Count == 1 && handlers[0].Calls == 1 && handlers[0].Disposed,
            "Estrazione con un client per richiesta");
        drafts = source.ExtractAsync(pdf, "chiave-di-prova", "modello-di-prova", CancellationToken.None).GetAwaiter().GetResult();
        Check(handlers.Count == 2 && handlers[1].Disposed && !drafts[0].ToJsonString().Contains("chiave-di-prova", StringComparison.Ordinal), "Secondo client o chiave nella bozza");
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            Reject<OperationCanceledException>(() => source.ExtractAsync(pdf, "chiave", "modello", cancelled.Token).GetAwaiter().GetResult(), "Annullamento ignorato");
        }
        var refused = StratigraphyImport.OpenAi(() => new ScriptedHandler(Envelope()) { Status = HttpStatusCode.Unauthorized });
        try { refused.ExtractAsync(pdf, "chiave", "modello", CancellationToken.None).GetAwaiter().GetResult(); throw new Exception("Errore HTTP ignorato"); }
        catch (InvalidOperationException error) { Check(StratigraphyImport.Shown(error) && StratigraphyImport.Failure(error).Contains("HTTP 401", StringComparison.Ordinal), "Errore HTTP"); }
        Reject<ArgumentException>(() => source.ExtractAsync(pdf, "", "modello", CancellationToken.None).GetAwaiter().GetResult(), "Chiave mancante accettata");

        // The status of the sheet and its other rules.
        var data = StratigraphyDocument.Create();
        Check(StratigraphyPresentation.Status(data) == new StratigraphyStatus("Livello della falda non noto.\nNuovo strato: parametri incompleti; salvabile, da completare per la verifica richiesta.", true),
            "Stato con gli avvisi");
        data["water_state"] = "Non considerata";
        foreach (string key in StratigraphyDocument.Parameters) StratigraphyDocument.Layers(data)[0]![key] = 18;
        Check(StratigraphyPresentation.Status(data) == new StratigraphyStatus("Dati validi. Il modulo non esegue verifiche geotecniche.", true), "Stato valido");
        data["name"] = " ";
        Check(StratigraphyPresentation.Status(data) == new StratigraphyStatus("Correggere prima di salvare: Assegnare un nome alla stratigrafia.", false), "Stato non valido");
        Check(StratigraphyPresentation.DefaultChoice("scenario") == "Medi" && StratigraphyPresentation.DefaultChoice("soil_type") == "Non classificato"
            && StratigraphyPresentation.DefaultChoice("behavior") == "Da definire" && StratigraphyPresentation.DefaultChoice("qualification") is null, "Scelte predefinite");
        Check(StratigraphyPresentation.Behaviors().SequenceEqual(["Da definire", "Granulare", "Coesivo"]), "Comportamenti");
        var layer = StratigraphyDocument.NewLayer();
        StratigraphyPresentation.SetInfinite(layer, true);
        Check(layer["infinite"]!.GetValue<bool>() && layer["thickness"] is null && layer.ContainsKey("thickness"), "Strato indefinito");
        StratigraphyPresentation.SetInfinite(layer, false);
        Check(!layer["infinite"]!.GetValue<bool>() && layer["thickness"]!.ToJsonString() == "1", "Strato di nuovo finito");
        return count;
    }

    /// <summary>A completed answer of the service with one profile of one layer.</summary>
    private static string Envelope()
    {
        JsonObject Values() => new(StratigraphyDocument.Parameters.Select(p => new KeyValuePair<string, JsonNode?>(p, new JsonObject { ["value"] = null, ["source"] = "" })));
        var layer = new JsonObject { ["name"] = "Sabbia", ["description"] = "", ["soil_type"] = "Sabbia", ["thickness"] = 2, ["infinite"] = false,
            ["geometry_source"] = "p. 2", ["mean"] = Values(), ["minimum"] = Values(), ["notes"] = "" };
        var profile = new JsonObject { ["name"] = "S1", ["description"] = "", ["ground_elevation"] = null, ["water_depth"] = null, ["source"] = "", ["notes"] = "",
            ["layers"] = new JsonArray(layer) };
        var payload = new JsonObject { ["warnings"] = "", ["profiles"] = new JsonArray(profile) };
        return new JsonObject { ["status"] = "completed", ["output"] = new JsonArray(new JsonObject { ["type"] = "message",
            ["content"] = new JsonArray(new JsonObject { ["type"] = "output_text", ["text"] = payload.ToJsonString() }) }) }.ToJsonString();
    }

    /// <summary>The service, scripted: it answers every request with <paramref name="response"/> and records that it was disposed.</summary>
    private sealed class ScriptedHandler(string response) : HttpMessageHandler
    {
        internal int Calls { get; private set; }
        internal bool Disposed { get; private set; }
        internal HttpStatusCode Status { get; init; } = HttpStatusCode.OK;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsoluteUri != "https://api.openai.com/v1/responses") throw new Exception("Destinazione inattesa");
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(response, Encoding.UTF8, "application/json") });
        }

        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
}
