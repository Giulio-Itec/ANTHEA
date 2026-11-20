using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Anthea.Application.Concrete;
using Anthea.Presentation.Modules.QuickSection;
using X.Desktop.Views.QuickSection;

namespace X.Desktop;

internal static class ConcreteDxfWpfChecks
{
    internal static ConcreteDxfDrawing Drawing()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName,"ANTHEA.sln"))) root=root.Parent;
        return ConcreteDxfImport.Read(Path.Combine(root!.FullName,"tests/fixtures/dxf/sezione-forata-cm.dxf"));
    }
    internal static JsonObject Data()
    {
        var data=ConcreteQuickSection.Create(); var dialog=new ConcreteDxfImportViewModel(data["input"]!.AsObject(),Drawing());
        data["input"]=dialog.Prepared!.Input.DeepClone();
        ConcreteQuickSection.ReplaceCombinations(data,[new("SLU DXF",-600,180,0),new("SLE DXF",-400,110,0,"SLE")]);
        return data;
    }
    internal static async Task Run(QuickSectionSheetViewModel sheet,Window window,Action<bool,string> check,Func<Window,string,Task> capture)
    {
        var dialog=new ConcreteDxfImportViewModel(sheet.Data["input"]!.AsObject(),Drawing());
        var view=new ConcreteDxfImportView {DataContext=dialog};
        var preview=new Window {Title=dialog.Title,Content=view,SizeToContent=SizeToContent.WidthAndHeight,Left=-20000,Top=-20000,ShowActivated=false,ShowInTaskbar=false};
        try
        {
            preview.Show(); preview.UpdateLayout();
            check(dialog.Prepared is not null && ReferenceEquals(view.Preview.Section,dialog.Geometry),"DXF: anteprima WPF collegata alla geometria validata");
            await capture(preview,"08l-dxf-anteprima.png");
            dialog.Form["unita"].SelectedChoice=ConcreteDxfImport.NoSelection; preview.UpdateLayout();
            check(view.Preview.Section is null && !dialog.ApplyCommand.CanExecute(null),"DXF: errore cancella l’anteprima e disabilita Importa");
        }
        finally { preview.Close(); }
        sheet.Reload(Data()); await sheet.PendingCalculation; window.UpdateLayout();
        var table=Ui.Descendants<DataGrid>(window).Single(g=>ReferenceEquals(g.ItemsSource,sheet.DxfBars));
        check(table.IsVisible && table.IsReadOnly && table.Items.Count==4 && sheet.Section?.Holes.Count==1 && sheet.HasResults,"DXF: sezione forata calcolata e quattro barre nella tabella di sola lettura");
        await capture(window,"08m-dxf-sezione-calcolata.png");
    }
}
