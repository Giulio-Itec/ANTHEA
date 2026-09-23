using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;
internal sealed class VerificationCards : StackPanel
{
    private string text = "";
    internal string Text { get => text; set { text = value; Children.Clear(); Children.Add(Ui.Text(value, 12)); } }
    internal void Start() { Children.Clear(); text = ""; }
    internal void AddCheck(string title, int total, IEnumerable<(string Name, double? Ratio, bool? Passed)> checks)
    {
        var rows = checks.ToArray(); var numeric = rows.Where(r => r.Ratio is double v && double.IsFinite(v)).OrderByDescending(r => r.Ratio).ToArray();
        var worst = numeric.FirstOrDefault(); bool failed = rows.Any(r => r.Passed == false || r.Ratio > 1);
        int missing = Math.Max(0, total - rows.Count(r => r.Ratio.HasValue || r.Passed.HasValue));
        string state = total == 0 ? "NESSUNA AZIONE" : failed ? "NON VERIFICATA" : missing > 0 ? "DA COMPLETARE" : "VERIFICATA";
        double? ratio = numeric.Length > 0 ? worst.Ratio : null;
        Brush color = failed ? UtilizationPalette.Brush(2) : missing > 0 || total == 0 ? UtilizationPalette.Brush(null) : ratio is null ? Ui.Brush("#39A879") : UtilizationPalette.Brush(ratio);
        string governing = numeric.Length > 0 ? "Governa: " + worst.Name : rows.FirstOrDefault(r => r.Passed == false).Name ?? "—";
        string value = numeric.Length > 0 ? "η = " + EngineeringFormat.Number(ratio) : "η —";
        string footer = governing + (missing > 0 ? $"\n{missing}/{total} verifiche mancanti / non applicabili" : $"\n{rows.Length}/{total} combinazioni");
        var rate = Ui.Text(value, 12, true); rate.Margin = new Thickness(6, 0, 0, 0); rate.VerticalAlignment = VerticalAlignment.Center;
        var heading = Ui.Text(title + " · " + state, 10, true); heading.TextWrapping = TextWrapping.NoWrap; heading.TextTrimming = TextTrimming.CharacterEllipsis;
        var top = new DockPanel(); DockPanel.SetDock(rate, Dock.Right); top.Children.Add(rate); top.Children.Add(heading);
        var detail = Ui.Text(governing + (missing > 0 ? $" · {missing}/{total} mancanti" : $" · {rows.Length}/{total}"), 10);
        detail.TextWrapping = TextWrapping.NoWrap; detail.TextTrimming = TextTrimming.CharacterEllipsis;
        Children.Add(new Border { BorderBrush = color, BorderThickness = new Thickness(5, 0, 0, 0), Background = Brushes.White, Padding = new Thickness(5, 2, 4, 2), Margin = new Thickness(0, 0, 0, 3), Child = Ui.Stack(top, detail), ToolTip = state + "\n" + footer + "\n" + UtilizationPalette.Legend });
        text += title + "\n" + state + "\n" + value + "\n" + footer + "\n";
    }
}
