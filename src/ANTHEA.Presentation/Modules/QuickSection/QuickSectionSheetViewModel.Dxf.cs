using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.ConcreteWorkspace;
using Anthea.Presentation.Services;
using Anthea.Presentation.Mvvm;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.QuickSection;

public sealed partial class QuickSectionSheetViewModel
{
    private IAsyncRelayCommand? importDxfCommand;
    public IAsyncRelayCommand ImportDxfCommand => importDxfCommand ??= GuardedCommands.CreateAsync(Guard, ImportDxf, Lifetime, () => CanEdit);
    public bool IsDxf => Shape == ConcreteQuickSection.Dxf;
    public bool IsParametric => !IsDxf;
    public bool HasLayers => !IsCircular && !IsDxf;
    public string DxfDescription => Input[ConcreteFreeSectionAdapter.Key] is JsonObject source
        ? source["file"] + " · unità origine: " + source["unita_origine"] + ". Geometria salvata nel foglio; non serve il file originale."
        : "Importare contorno, fori e barre dal disegno.";
    public string DxfDimensions => Section is { } geometry ? $"{geometry.Width:0.###} × {geometry.Height:0.###} mm · {geometry.Holes.Count} fori · {geometry.Bars.Count} barre" : "";
    public IReadOnlyList<ReinforcementBar> DxfBars => IsDxf ? Section?.Bars ?? [] : [];
    private async Task ImportDxf(CancellationToken cancellation)
    {
        if (!CanEdit || IsDisposed) return;
        string? path = Context.Services.Files.OpenFile(new OpenFileRequest([new FileFilter("Disegno DXF", "*.dxf")], null, "Importa la sezione da DXF"));
        if (path is null) { OnPropertyChanged(nameof(Shape)); return; }
        try
        {
            var drawing = await Task.Run(() => ConcreteDxfImport.Read(path), cancellation);
            cancellation.ThrowIfCancellationRequested();
            if (!CanEdit || IsDisposed) return;
            var dialog = new ConcreteDxfImportViewModel(Input, drawing);
            if (!await Context.Services.Dialogs.ShowDialogAsync(dialog) || dialog.Prepared is null || !CanEdit || IsDisposed) return;
            if (Edit(d => d["input"] = dialog.Prepared.Input.DeepClone()))
            { BuildForms(); RefreshDrawing(); AddLayerCommand.NotifyCanExecuteChanged(); }
        }
        catch (Exception failure) when (failure is ArgumentException or IOException or UnauthorizedAccessException)
        { Context.Services.Messages.Inform("Importazione DXF", failure.Message); }
        finally { OnPropertyChanged(nameof(Shape)); }
    }
}
