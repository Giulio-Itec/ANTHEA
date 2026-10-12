namespace ANTHEA.ModelWorkspace;

/// <summary>Credentials live only for the open viewer session; never serialize this object.</summary>
public sealed class MidasConnection
{
    public Uri BaseUri { get; }
    internal string ApiKey { get; }
    public MidasConnection(string url, string apiKey)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new ArgumentException("Inserisci il Base URL HTTPS mostrato nelle impostazioni API di MIDAS, senza chiave o parametri nell’indirizzo.");
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Any(char.IsControl))
            throw new ArgumentException("Inserisci una chiave MAPI valida.");
        BaseUri = new Uri(uri.AbsoluteUri.TrimEnd('/') + "/");
        ApiKey = apiKey.Trim();
    }
    public override string ToString() => "Connessione MIDAS (chiave riservata)";
}

public sealed record MidasModelCatalog(int Nodes, int Elements, IReadOnlyList<string> StaticCases,
    IReadOnlyList<string> ElementFamilies, string Signature)
{
    public IReadOnlyList<ModelConstructionPhase> Phases { get; init; } = [];
    public IReadOnlyList<string> StageCases { get; init; } = [];
    public string Summary => $"{Nodes:N0} nodi · {Elements:N0} elementi · {StaticCases.Count} casi statici · {Phases.Count} fasi";
}

public sealed record MidasImportRequest(MidasConnection Connection, ModelUpdateScope Scope,
    string ExpectedSignature, IReadOnlyList<string> StaticCases, bool Forces, bool Displacements)
{
    public MidasStageReadRequest? Stages { get; init; }
}
public sealed record ModelImportProgress(string Message, int Completed, int Total)
{
    public double Percent => Total > 0 ? 100.0 * Completed / Total : 0;
}

/// <summary>Read-only source boundary. Implementations return a complete validated copy or throw.</summary>
public interface IMidasModelSource
{
    Task<MidasModelCatalog> InspectAsync(MidasConnection connection, IProgress<ModelImportProgress>? progress, CancellationToken token);
    Task<ModelSnapshot> ReadAsync(MidasImportRequest request, IProgress<ModelImportProgress>? progress, CancellationToken token);
}
