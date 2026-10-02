using System.Windows;
using System.Windows.Controls;

namespace X.Desktop;

internal static class WikiContextHelp
{
    private static readonly Dictionary<string, (string Text, string Uri)> Topics = new()
    {
        ["cover_mm"] = ("Copriferro netto: distanza dal bordo alla staffa. La distanza al centro della barra include anche i diametri.", "/wiki/guide/moduli/sezione-ca#input-geometria-e-unita"),
        ["moment_x_knm"] = ("Momento della sezione in kNm. Controlla la conversione degli assi del modello e il bordo compresso.", "/wiki/manuale/fem/elementi-beam#convenzioni-di-segno"),
        ["moment_y_knm"] = ("Momento biassiale: conserva la terna N-Mx-My della stessa combinazione.", "/wiki/manuale/fem/elementi-beam#convenzioni-di-segno"),
        ["axial_force_kn"] = ("Sforzo normale in kN; nell'interfaccia della Sezione in c.a. la compressione è negativa.", "/wiki/guide/moduli/sezione-ca#input-azioni-e-convenzioni")
    };
    internal static FrameworkElement Label(TextBlock label, string key, string? module)
    {
        if (module != "str_palo" || !Topics.TryGetValue(key, out var topic)) return label;
        label.ToolTip = topic.Text;
        var help = Ui.Button("?", () => { if (Window.GetWindow(label) is MainWindow main) main.Safe(() => main.ShowWiki(topic.Uri)); });
        help.ToolTip = topic.Text + "\nApri l'approfondimento nella Wiki";
        System.Windows.Automation.AutomationProperties.SetName(help, "Approfondisci " + label.Text);
        help.Style = (Style)Application.Current.FindResource("ProjectButton");
        help.Padding = new Thickness(2); help.Margin = new Thickness(1); help.MinHeight = 20; help.Width = 20;
        var row = new DockPanel(); DockPanel.SetDock(help, Dock.Right); row.Children.Add(help); row.Children.Add(label); return row;
    }
    internal static string? Description(string key, string? module) => module == "str_palo" && Topics.TryGetValue(key, out var topic) ? topic.Text : null;
}
