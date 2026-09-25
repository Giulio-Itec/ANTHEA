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
                var h = BridgeSection.Homogenization(Data, phase);
                double value = derived == "phi" ? h.Phi : h.N;
                if (!double.IsFinite(value) || !double.IsFinite(h.PhiEffective)) throw new ArgumentException("Omogeneizzazione fuori intervallo.");
                // Update the archive and the editor together without recursively triggering Changed.
                string raw = value.ToString("R", CultureInfo.InvariantCulture);
                phase[derived] = raw;
                form.Set(derived, raw, display: true);
                info.Text = $"n₀ = {F(h.N0)}   ·   φeff = ψL · φ = {F(h.PhiEffective)}";
            }
            catch (ArgumentException ex)
            {
                form.Set(derived, "—", display: true);
                info.Text = ex.Message;
            }
        }
    }
}
