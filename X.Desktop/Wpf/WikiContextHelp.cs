using System.Windows;
using System.Windows.Controls;

namespace X.Desktop;

internal static class WikiContextHelp
{
    private static readonly DependencyProperty RefreshHelpProperty = DependencyProperty.RegisterAttached(
        "RefreshHelp", typeof(Action), typeof(WikiContextHelp), new PropertyMetadata(null));
    internal static readonly DependencyProperty ModuleProperty = DependencyProperty.RegisterAttached(
        "Module", typeof(string), typeof(WikiContextHelp), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits,
            (d, _) => { if (d.GetValue(RefreshHelpProperty) is Action refresh) refresh(); }));
    // Topic record and table moved to Anthea.Presentation.Help (F5.2, proposal F5 section 2.1): the historic callers keep these names.
    internal static IReadOnlyList<Topic> Topics => HelpTopics.Topics;
    /// <summary>Type of stress analysis of the c.a. section («modello» of the SLE tab; the same key names the shear model).</summary>
    internal static Topic StressAnalysis => HelpTopics.StressAnalysis;
    internal static Topic? ForField(string key, string? module) => HelpTopics.ForField(key, module);
    internal static string? Description(string key, string? module) => HelpTopics.Description(key, module);
    internal static void Open(FrameworkElement source, string uri)
    {
        for (var window = Window.GetWindow(source); window is not null; window = window.Owner)
            if (window is MainWindow main) { main.Safe(() => main.ShowWiki(uri)); return; }
        // F5.13 c (R5): a historic sheet hosted in a window of the new shell opens the Wiki tab of the shell; no such window in the Release.
        for (var window = Window.GetWindow(source); window is not null; window = window.Owner)
            if (window is Views.Shell.IShellWikiHost shell) { shell.ShowWiki(uri); return; }
    }
    internal static FrameworkElement Label(TextBlock label, string key, string? module, Topic? fixedTopic = null)
    {
        Topic? topic = null;
        var help = Ui.Button("?", () => { if (topic is not null) Open(label, topic.Uri); }, inspection: true);
        help.Style = (Style)Application.Current.FindResource("ProjectButton");
        help.Padding = new Thickness(2); help.Margin = new Thickness(1); help.MinHeight = 20; help.Width = 20;
        help.Visibility = Visibility.Collapsed;
        System.Windows.Automation.AutomationProperties.SetName(help, "Wiki: " + label.Text);
        var row = new DockPanel(); DockPanel.SetDock(help, Dock.Right); row.Children.Add(help); row.Children.Add(label);
        void Refresh()
        {
            topic = fixedTopic ?? ForField(key, module ?? row.GetValue(ModuleProperty) as string);
            help.Visibility = topic is null ? Visibility.Collapsed : Visibility.Visible;
            if (topic is not null) help.ToolTip = label.ToolTip = topic.Title + "\nApri questa sezione della Wiki";
        }
        row.SetValue(RefreshHelpProperty, (Action)Refresh);
        row.Loaded += (_, _) => Refresh(); Refresh();
        return row;
    }
    internal static WikiArticle[] ArticlesFor(string module) => WikiCatalog.Articles
        .Where(a => a.Modules.Contains(module) || Topics.Any(t => t.Modules.Contains(module) && WikiCatalog.Resolve(t.Uri)?.Id == a.Id))
        .OrderBy(a => a.Type == "guide" ? 0 : 1).ThenBy(a => a.Order).ToArray();
}
