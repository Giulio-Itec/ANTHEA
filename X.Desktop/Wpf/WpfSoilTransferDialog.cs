using System.Text.Json.Nodes;
using System.Windows;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.Shared;

namespace X.Desktop;

/// <summary>
/// The soil transfer dialog of the MVVM sheets in the desktop (F5.6b): the historic window <see cref="SoilTransferDialog"/>, shared by the
/// soil modules and unchanged, shown as the historic sheet shows it (SheetEditor.OpenSoilTransfer), owned by the active window. Its
/// answer becomes a <see cref="SoilTransferChoice"/>: a copy in a new sheet, or the replacement of the soil of the sheet.
/// </summary>
internal sealed class WpfSoilTransferDialog : ISoilTransferDialog
{
    public Task<SoilTransferChoice?> ShowAsync(string module, JsonObject data, IReadOnlyList<SoilSource> sources)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(sources);
        var owner = System.Windows.Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        var dialog = new SoilTransferDialog(owner!, module, data, sources.Select(s => (s.Label, s.Module, s.Data, s.Id)));
        SoilTransferChoice? choice = dialog.ShowDialog() != true || dialog.Output is null ? null
            : dialog.Replace ? new(dialog.Output)
            : dialog.DestinationModule is string target ? new(dialog.Output, target, dialog.OutputName) : null;
        return Task.FromResult(choice);
    }
}
