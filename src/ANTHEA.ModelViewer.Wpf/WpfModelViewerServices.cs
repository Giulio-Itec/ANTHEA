using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelWorkspace;
using ANTHEA.ModelReports;
using System.IO;
using System.Diagnostics;
using Microsoft.Win32;
using System.Windows;

namespace ANTHEA.ModelViewer.Wpf;

internal sealed class WpfModelViewerServices(Func<Window?> owner, Action fit, Action<string> saveImage, Func<ModelCameraState?>? captureCamera=null, Action<ModelCameraState>? restoreCamera=null, ModelViewPresetStore? viewStore=null) : IModelViewerServices
{
    private readonly ModelViewPresetStore views=viewStore??new(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ANTHEA","ModelViews"));
    // 1.15.0: also the MIDAS Civil/Gen text models, read by ModelSnapshotFiles through the GPC converter.
    internal const string ModelFileFilter = "Modelli leggibili|*.antheamodel;*.mct;*.mgt;NODE.json|Modello ANTHEA|*.antheamodel|File di testo MIDAS Civil / Gen (*.mct;*.mgt)|*.mct;*.mgt|Copia del laboratorio (NODE.json)|NODE.json";
    public ModelCameraState? CaptureCamera()=>captureCamera?.Invoke();
    public void RestoreCamera(ModelCameraState camera)=>restoreCamera?.Invoke(camera);
    public IReadOnlyList<ModelViewPreset> LoadViews(string revision)=>views.Read(revision);
    public void SaveViews(string revision,IReadOnlyList<ModelViewPreset> presets)=>views.Write(revision,presets);
    public ModelReportDocument? OpenReport(bool hasChanges)
    {
        if(hasChanges && MessageBox.Show(owner(),"Aprendo una composizione verrà sostituita quella in modifica. Continuare?","Apri composizione",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes) return null;
        var dialog=new OpenFileDialog { Title="Apri composizione del report",Filter="Report ANTHEA|*.antheareport" };
        return dialog.ShowDialog(owner())==true ? ModelReportFiles.Read(dialog.FileName) : null;
    }
    public bool SaveReport(ModelReportDocument document)
    {
        var dialog=new SaveFileDialog { Title="Salva composizione del report",Filter="Report ANTHEA|*.antheareport",FileName="report-modello.antheareport" };
        if(dialog.ShowDialog(owner())!=true)return false;
        ModelReportFiles.Write(dialog.FileName,document);return true;
    }
    public async Task<ReportExportResult?> ExportReportAsync(ModelReportDocument document, ModelSnapshot snapshot,IProgress<ReportProgress> progress,CancellationToken cancellationToken)
    {
        var dialog=new SaveFileDialog { Title="Nome del report · verrà creata una cartella con PDF e sorgenti",Filter="Report PDF|*.pdf",FileName="report-modello.pdf",OverwritePrompt=false };
        if(dialog.ShowDialog(owner())!=true)return null;
        return await ExportReportToAsync(document,snapshot,dialog.FileName,progress,cancellationToken);
    }
    internal static async Task<ReportExportResult> ExportReportToAsync(ModelReportDocument document,ModelSnapshot snapshot,string path,IProgress<ReportProgress>? progress,CancellationToken cancellationToken)
    {
        ModelReportFiles.ValidateReferences(document,snapshot);
        var fontDirectory=Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        ModelReportExporter.ConfigureFonts(File.ReadAllBytes(Path.Combine(fontDirectory,"segoeui.ttf")),File.ReadAllBytes(Path.Combine(fontDirectory,"segoeuib.ttf")));
        using var session=new ModelReportRenderSession(snapshot,document);
        return await ModelReportExporter.ExportAsync(document with {ModelName=snapshot.Name},snapshot.Fingerprint(),path,session.CaptureAsync,progress,cancellationToken);
    }
    public void OpenReportOutput(string path)
    {
        if(!File.Exists(path))throw new FileNotFoundException("Il PDF è stato spostato o eliminato.");
        Process.Start(new ProcessStartInfo(path) { UseShellExecute=true });
    }
    public async Task<ModelSnapshot?> ImportAsync(CancellationToken cancellationToken)
    {
        var dialog = new OpenFileDialog { Title = "Importa modello nel progetto", Filter = ModelFileFilter };
        if (dialog.ShowDialog(owner()) != true) return null;
        return await Task.Run(() => ModelSnapshotFiles.Read(dialog.FileName, cancellationToken), cancellationToken);
    }
    public async Task<MidasModelCatalog> InspectMidasAsync(MidasConnection connection,IProgress<ModelImportProgress> progress,CancellationToken token)
    {
        using var source=new MidasModelSource();
        return await Task.Run(()=>source.InspectAsync(connection,progress,token),token);
    }
    public string? ChooseStrand7File(Strand7ImportFile kind)
    {
        var dialog=new OpenFileDialog {Title="Predisponi importazione Straus / Strand7",Filter=kind switch
        {Strand7ImportFile.Model=>"Modello Straus / Strand7|*.st7",Strand7ImportFile.Results=>"Risultati statici lineari|*.lsa",_=>"API Straus / Strand7 R3|St7api.dll|Librerie native|*.dll"}};
        return dialog.ShowDialog(owner())==true?dialog.FileName:null;
    }
    public async Task<ModelSnapshot> ImportMidasAsync(MidasImportRequest request,IProgress<ModelImportProgress> progress,CancellationToken token)
    {
        using var source=new MidasModelSource();
        return await Task.Run(()=>source.ReadAsync(request,progress,token),token);
    }
    public async Task<ModelSnapshot?> OpenPreviousModelAsync(CancellationToken cancellationToken)
    {
        var dialog=new OpenFileDialog {Title="Confronta con una copia precedente del modello",Filter=ModelFileFilter};
        if(dialog.ShowDialog(owner())!=true)return null;
        return await Task.Run(()=>ModelSnapshotFiles.Read(dialog.FileName,cancellationToken),cancellationToken);
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
