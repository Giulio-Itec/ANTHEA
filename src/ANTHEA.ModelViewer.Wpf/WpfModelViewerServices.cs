using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelWorkspace;
using Microsoft.Win32;
using System.Windows;

namespace ANTHEA.ModelViewer.Wpf;

internal sealed class WpfModelViewerServices(Func<Window?> owner, Action fit, Action<string> saveImage) : IModelViewerServices
{
    public async Task<ModelSnapshot?> ImportAsync(CancellationToken cancellationToken)
    {
        var dialog = new OpenFileDialog { Title = "Importa modello nel progetto", Filter = "Modello ANTHEA|*.antheamodel|Copia del laboratorio (NODE.json)|NODE.json" };
        if (dialog.ShowDialog(owner()) != true) return null;
        return await Task.Run(() => ModelSnapshotFiles.Read(dialog.FileName, cancellationToken), cancellationToken);
    }

    public void FitView() => fit();
    public void SaveImage()
    {
        var dialog = new SaveFileDialog { Filter = "Immagine PNG|*.png", FileName = "modello.png" };
        if (dialog.ShowDialog(owner()) == true) saveImage(dialog.FileName);
    }
}
