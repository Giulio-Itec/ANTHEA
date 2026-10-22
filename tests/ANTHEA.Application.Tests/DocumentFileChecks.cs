using Anthea.Reports;

/// <summary>Final copy or rename of <see cref="DocumentFile.Write"/> with the target held by another handle.</summary>
internal static class DocumentFileChecks
{
    internal static int Run(string output)
    {
        int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks++; }
        string folder = Path.Combine(output, "document-file");
        Directory.CreateDirectory(folder);
        foreach (string name in new[] { "archivio.anth", "relazione.docx", "figura.png" })
        {
            string path = Path.Combine(folder, name);
            File.WriteAllBytes(path, [1, 2, 3]);
            // Lock released while the write is retrying: the write succeeds with the new content
            var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            var release = Task.Run(async () => { await Task.Delay(250); held.Dispose(); });
            DocumentFile.Write(path, [4, 5, 6, 7]);
            release.Wait();
            Check(File.ReadAllBytes(path).SequenceEqual(new byte[] { 4, 5, 6, 7 }), name + ": scrittura persa dopo il blocco temporaneo");
            // Lock kept for longer than all the attempts: the write fails and the previous file stays intact
            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                bool failed = false;
                try { DocumentFile.Write(path, [8]); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { failed = true; }
                Check(failed, name + ": blocco permanente non segnalato");
            }
            Check(File.ReadAllBytes(path).SequenceEqual(new byte[] { 4, 5, 6, 7 }), name + ": file precedente alterato dal fallimento");
            Check(Directory.GetFiles(folder, ".x_*.tmp").Length == 0, name + ": file temporaneo rimasto");
        }
        // A missing folder is not transient: no retry, immediate failure
        var watch = System.Diagnostics.Stopwatch.StartNew();
        bool missing = false;
        try { DocumentFile.Write(Path.Combine(folder, "manca", "x.anth"), [1]); } catch (DirectoryNotFoundException) { missing = true; }
        Check(missing && watch.ElapsedMilliseconds < DocumentFile.RetryDelayMs, "Cartella mancante ritentata o non segnalata");
        return checks;
    }
}
