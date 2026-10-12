namespace Anthea.Application.Documents;

/// <summary>Durable write shared by archives and exported documents; preserves the existing Office copy policy.</summary>
public static class AtomicFile
{
    public static void Write(string path,byte[] bytes)
    {
        path=Path.GetFullPath(path);string temp=Path.Combine(Path.GetDirectoryName(path)!,".x_"+Guid.NewGuid().ToString("N")+".tmp");
        try
        {
            using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes);stream.Flush(true);}
            // Office documents are copied from the temporary file instead of renamed: the ransomware protection of the behaviour monitoring
            // (Trend Micro Security Agent, 27/09/2026) terminates an unsigned program that renames temporary files into documents (the third
            // report of a session closed ANTHEA without errors). The archives keep the atomic rename
            if(Path.GetExtension(path).ToLowerInvariant() is ".docx" or ".xlsx" or ".pptx" or ".pdf")File.Copy(temp,path,true);
            else File.Move(temp,path,true);
        }
        finally{if(File.Exists(temp))File.Delete(temp);}
    }
}
