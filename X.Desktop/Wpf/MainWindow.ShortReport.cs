using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace X.Desktop;
public sealed partial class MainWindow
{
    private readonly List<Button> shortReportButtons=[];
    private bool shortReportRunning;
    private async Task ExportShortReport()
    {
        if(shortReportRunning)return;
        try
        {
            Commit();
            if(editor?.Module!="str_palo")throw new InvalidOperationException("Selezionare una sezione in calcestruzzo armato.");
            var bytes=editor.BuildShortReport(heading.Text);
            var dialog=new SaveFileDialog{Filter="Report short Word e PDF|*.docx",FileName="Relazione_short.docx"};
            if(dialog.ShowDialog(this)!=true)return;
            shortReportRunning=true;
            foreach(var button in shortReportButtons)button.IsEnabled=false;
            await ShortReportExport.WriteAsync(dialog.FileName,bytes);
            MessageBox.Show(this,"Report short salvato in Word e PDF, entro due pagine.","Report short");
        }
        catch(Exception ex){MessageBox.Show(this,ex.Message,"Report short",MessageBoxButton.OK,MessageBoxImage.Warning);}
        finally{shortReportRunning=false;foreach(var button in shortReportButtons)button.IsEnabled=true;}
    }
}
