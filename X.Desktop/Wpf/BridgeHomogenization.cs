using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;

internal sealed partial class BridgeWorkspace
{
    private readonly List<(JsonObject Phase, InputForm Form, TextBlock Info)> homogenizationForms = [];

    private void SynchronizeHomogenization()
    {
        foreach (var (phase, form, info) in homogenizationForms)
        {
            // The existing archive key retains the last edited quantity, including in old files.
            // A material change preserves n if it was assigned; otherwise it preserves phi.
            string derived = phase.S("modo") == "Da n" ? "phi" : "n";
            try
            {
                var h = Anthea.Application.Presentation.BridgeSheetPresentation.UpdateHomogenization(Data, phase);
                string raw = phase.S(derived);
                form.Set(derived, raw, display: true);
                info.Text = Anthea.Application.Presentation.BridgeInputPresentation.HomogenizationInfo(h);
            }
            catch (ArgumentException ex)
            {
                form.Set(derived, "—", display: true);
                info.Text = ex.Message;
            }
        }
    }
}
