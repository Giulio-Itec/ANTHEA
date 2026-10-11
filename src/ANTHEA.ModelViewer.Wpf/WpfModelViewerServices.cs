using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelWorkspace;
using Microsoft.Win32;
using System.Windows;

namespace ANTHEA.ModelViewer.Wpf;

internal sealed class WpfModelViewerServices(Func<Window?> owner, Action fit, Action<string> saveImage, Func<ModelCameraState?>? captureCamera=null, Action<ModelCameraState>? restoreCamera=null) : IModelViewerServices
{
    private readonly ModelViewPresetStore views=new(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ANTHEA","ModelViews"));
    public ModelCameraState? CaptureCamera()=>captureCamera?.Invoke();
    public void RestoreCamera(ModelCameraState camera)=>restoreCamera?.Invoke(camera);
    public IReadOnlyList<ModelViewPreset> LoadViews(string revision)=>views.Read(revision);
    public void SaveViews(string revision,IReadOnlyList<ModelViewPreset> presets)=>views.Write(revision,presets);
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
    public void ExportTable(string name, string csv)
    {
        var dialog = new SaveFileDialog { Title = "Esporta tabella", Filter = "Tabella CSV|*.csv", FileName = "modello-tabella.csv" };
        if (dialog.ShowDialog(owner()) == true) System.IO.File.WriteAllText(dialog.FileName, csv, new System.Text.UTF8Encoding(true));
    }
}
