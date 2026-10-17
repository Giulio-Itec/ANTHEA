using System.Text.Json.Nodes;

namespace Anthea.Presentation.Modules.Stratigraphy;

/// <summary>
/// The soil transfer dialog («Terreno · invio e riutilizzo»), shared by the soil modules (stratigraphy, walls, piles, foundations). Until
/// its own ViewModel exists (question V9 of F5.6, S3 of F5.6b) the desktop shows the historic window SoilTransferDialog behind this
/// interface (X.Desktop/Wpf/WpfSoilTransferDialog.cs): the sheet commits and passes its module, its data and the soil sheets of the
/// project; the dialog returns what the user chose, or null when it is closed without a choice.
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
