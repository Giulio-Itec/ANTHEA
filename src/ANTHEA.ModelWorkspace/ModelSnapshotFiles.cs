namespace ANTHEA.ModelWorkspace;

public static class ModelSnapshotFiles
{
    public const long MaximumPackageBytes = 256L * 1024 * 1024;
    internal const string PackageSizeMessage = "Pacchetto oltre il limite di 256 MiB. Suddividi l’acquisizione in più modelli.";
    /// <summary>Formats read by the file source: ANTHEA package, MIDAS Civil/Gen text model, laboratory copy of the Civil NX API.</summary>
    public const string SupportedFormats = "pacchetto .antheamodel, file di testo MIDAS Civil .mct o Gen .mgt, copia MIDAS già acquisita (NODE.json)";
    public static ModelSnapshot Read(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ModelSnapshot result;
        if (Path.GetExtension(path).Equals(".antheamodel", StringComparison.OrdinalIgnoreCase))
        {
            using var stream = File.OpenRead(path);
            if (stream.Length > MaximumPackageBytes) throw new InvalidDataException(PackageSizeMessage);
            result = System.Text.Json.JsonSerializer.DeserializeAsync<ModelSnapshot>(stream, cancellationToken:cancellationToken)
                .AsTask().ConfigureAwait(false).GetAwaiter().GetResult() ?? throw new InvalidDataException("Modello vuoto.");
            result.Validate();
        }
        else if (MidasTextImporter.IsTextModel(path))
            result = MidasTextImporter.Read(path, cancellationToken);
        else if (Path.GetFileName(path).Equals("NODE.json", StringComparison.OrdinalIgnoreCase))
            result = MidasSnapshotImporter.ReadDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        else throw new InvalidDataException("Formato del modello non riconosciuto. Formati letti: " + SupportedFormats + ".");
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }
}
