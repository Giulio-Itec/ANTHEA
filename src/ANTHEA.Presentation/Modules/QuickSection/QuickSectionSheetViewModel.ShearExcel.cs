using Anthea.Application.Concrete;
using Anthea.Presentation.Mvvm;
using Anthea.Presentation.Services;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.QuickSection;

public sealed partial class QuickSectionSheetViewModel
{
    private IRelayCommand? exportShearExcelCommand, importShearExcelCommand;
    public const string ShearExcelTitle = "Combinazioni a taglio in Excel";
    public string? ShearExcelPath { get; private set; }
    public IRelayCommand ExportShearExcelCommand => exportShearExcelCommand ??= GuardedCommands.Create(Guard, ExportShearExcel, () => ShearCombinationRows.Count > 0);
    public IRelayCommand ImportShearExcelCommand => importShearExcelCommand ??= GuardedCommands.Create(Guard, ImportShearExcel, () => CanEdit);
    private string NewShearExcelPath()
    {
        string source = NewExcelPath();
        string name = "Taglio " + Path.GetFileNameWithoutExtension(source);
        string path = Path.Combine(Path.GetDirectoryName(source)!, name + ".xlsx");
        for (int k = 2; File.Exists(path); k++) path = Path.Combine(Path.GetDirectoryName(source)!, name + " (" + k + ").xlsx");
        return path;
    }
    private void ExportShearExcel()
    {
        byte[] bytes;
        try { bytes = ConcreteQuickShearExcel.Export(Data); }
        catch (ArgumentException error) { Context.Services.Messages.Inform(ShearExcelTitle, error.Message); return; }
        Directory.CreateDirectory(ExcelFolder);
        string path = ShearExcelPath ?? NewShearExcelPath();
        try { X.Core.Archivio.ScriviAtomico(path, bytes); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { path = NewShearExcelPath(); X.Core.Archivio.ScriviAtomico(path, bytes); }
        ShearExcelPath = path; OnPropertyChanged(nameof(ShearExcelPath));
        Context.Services.Links.Open(new Uri(path));
    }
    private void ImportShearExcel()
    {
        string? path = ShearExcelPath is { } last && File.Exists(last) ? last
            : Context.Services.Files.OpenFile(new OpenFileRequest([ExcelFilter], null, "Importa le combinazioni a taglio da Excel"));
        if (path is null) return;
        IReadOnlyList<ConcreteQuickShearExcel.Row> rows;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var buffer = new MemoryStream(); stream.CopyTo(buffer);
            rows = ConcreteQuickShearExcel.Read(buffer.ToArray());
        }
        catch (Exception error) when (error is ArgumentException or IOException or InvalidDataException or UnauthorizedAccessException)
        { Context.Services.Messages.Inform(ShearExcelTitle, error.Message + "\n\nFile: " + path); return; }
        ChangeShearRows(d => ConcreteQuickShear.ReplaceCombinations(d, rows));
        ShearExcelPath = path; OnPropertyChanged(nameof(ShearExcelPath));
    }
}
