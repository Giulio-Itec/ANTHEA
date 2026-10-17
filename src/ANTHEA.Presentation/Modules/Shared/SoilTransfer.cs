using System.Text.Json.Nodes;
using Anthea.Presentation.Services;

namespace Anthea.Presentation.Modules.Shared;

/// <summary>
/// The soil transfer dialog («Terreno · invio e riutilizzo»), shared by the soil modules (stratigraphy, walls, piles, foundations). Moved
/// from Modules/Stratigraphy in F5.6 (decision T5, S3 of F5.6b): the sheet commits and passes its module, its data and the soil sheets of
/// the project; the dialog returns what the user chose, or null when it is closed without a choice. Two implementations:
/// <list type="bullet">
/// <item><see cref="SoilTransferDialogService"/>: the shared ViewModel <see cref="SoilTransferDialogViewModel"/> shown through
/// <see cref="IDialogService"/> (T5), used by the vertical pile since F5.6;</item>
/// <item>X.Desktop/Wpf/WpfSoilTransferDialog.cs: the historic window SoilTransferDialog, still given to the stratigraphy by the
/// composition root (its behaviour does not change in F5.6).</item>
/// </list>
/// </summary>
public interface ISoilTransferDialog
{
    Task<SoilTransferChoice?> ShowAsync(string module, JsonObject data, IReadOnlyList<SoilSource> sources);
}

/// <summary>
/// The choice of the soil transfer dialog: a copy of the soil in a new sheet of <paramref name="Module"/> named <paramref name="Name"/>,
/// or, when <paramref name="Module"/> is null, the data of this sheet with the soil replaced.
/// </summary>
public sealed record SoilTransferChoice(JsonObject Data, string? Module = null, string Name = "")
{
    public JsonObject Data { get; } = Data ?? throw new ArgumentNullException(nameof(Data));
}

/// <summary>
/// The soil transfer dialog on <see cref="IDialogService"/> (decision T5): <see cref="SoilTransferDialogViewModel"/> with the file
/// dialogs and the questions of the services; its view is a DataTemplate of the desktop (X.Desktop/Views/Shared).
/// </summary>
public sealed class SoilTransferDialogService(PresentationServices services) : ISoilTransferDialog
{
    private readonly PresentationServices services = services ?? throw new ArgumentNullException(nameof(services));

    public async Task<SoilTransferChoice?> ShowAsync(string module, JsonObject data, IReadOnlyList<SoilSource> sources)
    {
        var dialog = new SoilTransferDialogViewModel(module, data, sources, services.Files, services.Confirmations);
        return await services.Dialogs.ShowDialogAsync(dialog);
    }
}
