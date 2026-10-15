namespace Anthea.Reports;

/// <summary>Durable write shared by archives and exported documents; preserves the existing Office copy policy.</summary>
public static class DocumentFile
{
    /// <summary>Attempts of the final copy or rename; the waits grow by <see cref="RetryDelayMs"/> each time (1,5 s in total).</summary>
    public const int Attempts = 6, RetryDelayMs = 100;

    public static void Write(string path,byte[] bytes)
    {
        path=Path.GetFullPath(path);string temp=Path.Combine(Path.GetDirectoryName(path)!,".x_"+Guid.NewGuid().ToString("N")+".tmp");
        try
        {
            using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes);stream.Flush(true);}
            // Office documents are copied from the temporary file instead of renamed: the ransomware protection of the behaviour monitoring
            // (Trend Micro Security Agent, 27/09/2026) terminates an unsigned program that renames temporary files into documents (the third
            // report of a session closed ANTHEA without errors). The archives keep the atomic rename
            bool office=Path.GetExtension(path).ToLowerInvariant() is ".docx" or ".xlsx" or ".pptx" or ".pdf";
            // A target held for a moment by an antivirus scan, the indexer or another reader is retried before failing (Application.Tests
            // 14/10/2026: UnauthorizedAccessException from File.Move during the standard profile, PASS when repeated alone)
            for(int attempt=1;;attempt++)
            {
                try{if(office)File.Copy(temp,path,true);else File.Move(temp,path,true);break;}
                catch(Exception ex) when(attempt<Attempts&&Transient(ex)){Thread.Sleep(RetryDelayMs*attempt);}
            }
        }
        finally{if(File.Exists(temp))File.Delete(temp);}
    }

    // Sharing and lock violations (0x80070020, 0x80070021) and access denied; missing folders and other I/O errors fail at once
    static bool Transient(Exception ex) => ex is UnauthorizedAccessException
        || ex is IOException and not (FileNotFoundException or DirectoryNotFoundException or PathTooLongException) && (ex.HResult & 0xFFFF) is 32 or 33;
}
