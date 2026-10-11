using System.Text.Json;

namespace ANTHEA.ModelWorkspace;

public enum ModelSourceFailureKind { SessionMissing, DocumentClosed, AccessDenied, Timeout, Unavailable }

/// <summary>Source adapter failure contract. Raw server bodies and credentials never reach the view.</summary>
public sealed class ModelSourceException(ModelSourceFailureKind kind) : IOException(TitleFor(kind))
{
    public ModelSourceFailureKind Kind { get; } = kind;
    public string Recovery => Kind switch
    {
        ModelSourceFailureKind.SessionMissing => "Riattiva il collegamento API in MIDAS. Se è stata generata una nuova chiave, aggiorna la connessione e riprova.",
        ModelSourceFailureKind.DocumentClosed => "Apri il modello in MIDAS, attendi che sia pronto e riprova l’aggiornamento.",
        ModelSourceFailureKind.AccessDenied => "Controlla l’URL e la chiave del collegamento MIDAS, poi riprova.",
        ModelSourceFailureKind.Timeout => "Controlla che MIDAS risponda e abbia terminato l’operazione. Riprova con un insieme più piccolo di risultati.",
        _ => "Controlla che MIDAS e il collegamento API siano attivi, poi riprova."
    };
    static string TitleFor(ModelSourceFailureKind kind) => kind switch
    {
        ModelSourceFailureKind.SessionMissing => "Collegamento MIDAS non più disponibile",
        ModelSourceFailureKind.DocumentClosed => "Nessun modello aperto in MIDAS",
        ModelSourceFailureKind.AccessDenied => "Accesso a MIDAS rifiutato",
        ModelSourceFailureKind.Timeout => "MIDAS non ha risposto entro il tempo previsto",
        _ => "MIDAS non è raggiungibile"
    };
    public static ModelSourceException FromResponse(int statusCode, string? body)
    {
        string message="";
        try
        {
            using var json=JsonDocument.Parse(body??"{}");
            if(json.RootElement.ValueKind==JsonValueKind.Object&&json.RootElement.TryGetProperty("error",out var error)&&error.ValueKind==JsonValueKind.Object&&error.TryGetProperty("message",out var value)&&value.ValueKind==JsonValueKind.String)message=value.GetString()??"";
        }
        catch(JsonException) { }
        if(message.Contains("client does not exist",StringComparison.OrdinalIgnoreCase))return new(ModelSourceFailureKind.SessionMissing);
        if(message.Contains("document",StringComparison.OrdinalIgnoreCase)&&(message.Contains("not open",StringComparison.OrdinalIgnoreCase)||message.Contains("no document",StringComparison.OrdinalIgnoreCase)||message.Contains("not exist",StringComparison.OrdinalIgnoreCase)))return new(ModelSourceFailureKind.DocumentClosed);
        return new(statusCode is 401 or 403?ModelSourceFailureKind.AccessDenied:ModelSourceFailureKind.Unavailable);
    }
}
