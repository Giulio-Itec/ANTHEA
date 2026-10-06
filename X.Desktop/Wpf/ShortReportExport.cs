using System.IO;
using System.Runtime.InteropServices;
using X.Core;

namespace X.Desktop;
/// <summary>Word supplies the final pagination and matching PDF; never delivers a report exceeding two pages.</summary>
internal static class ShortReportExport
{
    internal static Task WriteAsync(string filename,byte[] docx)
    {
        var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread=new Thread(()=>
        {
            string directory=Path.Combine(Path.GetTempPath(),"AntheaShort-"+Guid.NewGuid().ToString("N"));
            dynamic? word=null,document=null;
            try
            {
                var type=Type.GetTypeFromProgID("Word.Application")??throw new InvalidOperationException("Il report short Word/PDF con controllo delle due pagine richiede Microsoft Word installato.");
                Directory.CreateDirectory(directory);string source=Path.Combine(directory,"report.docx"),pdf=Path.Combine(directory,"report.pdf");
                File.WriteAllBytes(source,docx);
                word=Activator.CreateInstance(type)!;word.Visible=false;word.DisplayAlerts=0;word.AutomationSecurity=3;
                document=word.Documents.Open(source,ConfirmConversions:false,ReadOnly:true,AddToRecentFiles:false);
                document.Repaginate();int pages=document.ComputeStatistics(2);
                if(pages>2)throw new InvalidOperationException($"Il contenuto richiede {pages} pagine (ad esempio per armature libere o nomi molto lunghi). Nessun dato è stato tagliato e nessun report short è stato salvato. Usare il report completo oppure abbreviare titolo e nomi delle combinazioni.");
                document.ExportAsFixedFormat(pdf,17);
                Archivio.ScriviAtomico(filename,docx);Archivio.ScriviAtomico(Path.ChangeExtension(filename,".pdf"),File.ReadAllBytes(pdf));
                completion.SetResult();
            }
            catch(Exception ex){completion.SetException(ex);}
            finally
            {
                try{if(document is not null){document.Close(0);Marshal.FinalReleaseComObject(document);}}catch{ }
                try{if(word is not null){word.Quit();Marshal.FinalReleaseComObject(word);}}catch{ }
                try{if(Directory.Exists(directory))Directory.Delete(directory,true);}catch{ }
            }
        }){IsBackground=true,Name="ANTHEA report short"};
        thread.SetApartmentState(ApartmentState.STA);thread.Start();return completion.Task;
    }
}
