using System.Security.Cryptography;
using System.Text.Json;
using ANTHEA.ModelWorkspace;

static partial class Program
{
    static void SnapshotPackageChecks(string output)
    {
        var model=Sample.Model();
        string original=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(model)));
        Check(model.Fingerprint()==original,"streamed fingerprint preserves the exact pre-existing UTF-8 revision contract");
        string path=Path.Combine(output,"streamed.antheamodel");
        using(var file=File.Create(path))JsonSerializer.Serialize(file,model);
        Check(ModelSnapshotFiles.Read(path).Fingerprint()==original,"streamed package import preserves all values and revision identity");
        using(var cancelled=new CancellationTokenSource())
        {
            cancelled.Cancel();
            try {ModelSnapshotFiles.Read(path,cancelled.Token);throw new Exception("Cancellation ignored");}
            catch(OperationCanceledException){Check(true,"package cancellation is honored before parsing");}
        }
        string large=Path.Combine(output,"oversize.antheamodel");
        try
        {
            using(var file=File.Create(large))file.SetLength(ModelSnapshotFiles.MaximumPackageBytes+1);
            Reject(()=>ModelSnapshotFiles.Read(large),"oversized package is rejected before allocation or JSON parsing");
        }
        finally{File.Delete(large);}
    }
}
