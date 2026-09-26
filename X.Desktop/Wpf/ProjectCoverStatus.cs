using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    private readonly TextBlock sharedStatus = Ui.Text("", 12);
    private static List<ProjectValidation.CoverStatus> CoverChecks(JsonObject section, JsonObject? only = null) =>
        ProjectValidation.CoverChecks(section, only);

    private void RefreshSharedStatus()
    {
        sharedStatus.Visibility = Visibility.Collapsed;
        if (document.S("tipo") != "progetti" || currentSheet?.Parent?.Parent is not JsonObject section || editor is null) return;
        var (temporary, active) = ProjectSharedData.PreviewSection(section, currentSheet, editor.materials?.CaptureState() ?? editor.Data);
        var checks = CoverChecks(temporary, currentSheet.S("modulo_id") == "mat_calcestruzzo" ? null : active);
        var conflicts = ProjectSharedData.Differences(temporary).Where(d =>
            ReferenceEquals(d.First, active) || ReferenceEquals(d.Second, active)).ToArray();
        var messages = checks.Select(c => c.Text).ToList();
        var properties = conflicts.Select(d => SharedFieldLabel(d.Key)).Distinct().ToArray();
        if (properties.Length > 0)
            messages.Insert(0, "⚠ Dati diversi tra i fogli: " + string.Join(", ", properties.Take(5)) +
                (properties.Length > 5 ? $" e altre {properties.Length - 5} proprietà" : "") + ". Apri il confronto nella pagina del progetto per uniformare.");
        if (messages.Count == 0) return;
        sharedStatus.Text = string.Join("\n", messages);
        sharedStatus.ToolTip = string.Join("\n", properties.Concat(checks.Select(c => c.Text)));
        sharedStatus.Foreground = conflicts.Length == 0 && checks.All(c => c.Passed == true) ? Brushes.DarkGreen : Brushes.DarkOrange;
        sharedStatus.Margin = new Thickness(12, 5, 12, 5); sharedStatus.Visibility = Visibility.Visible;
    }
}
