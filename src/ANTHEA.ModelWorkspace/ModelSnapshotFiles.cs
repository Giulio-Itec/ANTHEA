namespace ANTHEA.ModelWorkspace;

public static class ModelSnapshotFiles
{
    public static ModelSnapshot Read(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ModelSnapshot result;
        if (Path.GetExtension(path).Equals(".antheamodel", StringComparison.OrdinalIgnoreCase))
        {
            if (new FileInfo(path).Length > 64000000) throw new InvalidDataException("Pacchetto oltre il limite di 64 MB della bozza.");
            result = ModelSnapshot.FromJson(File.ReadAllText(path));
        }
        else if (Path.GetFileName(path).Equals("NODE.json", StringComparison.OrdinalIgnoreCase))
            result = MidasSnapshotImporter.ReadDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        else throw new InvalidDataException("Formato del modello non riconosciuto.");
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }
}
