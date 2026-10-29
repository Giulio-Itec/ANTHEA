using System.Windows;
using System.Windows.Controls;
using Anthea.Presentation.Modules.HorizontalPile;
using X.Core;

namespace X.Desktop.Views.HorizontalPile;

/// <summary>A reference of «Diagrammi e dettagli»: title, detail, use in the model and its link (null without).</summary>
public sealed record HorizontalPileReference(string Title, string Detail, string Use, string? Url);

/// <summary>
/// «Diagrammi e dettagli» of the horizontal pile (F5.7): the dialog of the view on the result of the capacity of the sheet. The code-behind
/// builds one tab per survey of <see cref="HorizontalPileSheetViewModel.Results"/> before «Riferimenti» and «JSON», with the historic
/// <see cref="HorizontalDiagrams"/> drawn from <see cref="HorizontalPileSheetViewModel.Diagram"/> (Q3: technical drawings in code), and the tables
/// with the template of the view. No calculation and no write of the document.
/// </summary>
public partial class HorizontalPileResultsView : UserControl
{
    /// <summary>Caption of the window, as the historic dialog.</summary>
    internal const string Title = "Palo orizzontale · risultati";

    /// <summary>The smallest diagram, as the historic view (1000 × 620 in a scroll viewer).</summary>
    private const double DiagramWidth = 1000, DiagramHeight = 620;

    public HorizontalPileResultsView(HorizontalPileSheetViewModel sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        InitializeComponent();
        DataContext = sheet;
        var results = sheet.Results;
        for (int i = 0; i < results.Count; i++)
        {
            var panel = new TabControl();
            panel.Items.Add(Tab("Terreno e tensioni", Diagram(sheet.Diagram(i, true))));
            panel.Items.Add(Tab("Equilibrio del palo", Diagram(sheet.Diagram(i, false))));
            foreach (var table in results[i].Tables)
                panel.Items.Add(Tab(table.Title, new ContentControl { Content = table, ContentTemplate = (DataTemplate)Resources["Table"], Focusable = false, IsTabStop = false }));
            var info = new ContentControl { Content = results[i], ContentTemplate = (DataTemplate)Resources["SurveyInfo"], Focusable = false, IsTabStop = false };
            DockPanel.SetDock(info, Dock.Top);
            Tabs.Items.Insert(i, Tab("Sondaggio " + (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), new DockPanel { Children = { info, panel } }));
        }
        References.ItemsSource = sheet.References.OfType<System.Text.Json.Nodes.JsonObject>()
            .Select(r => new HorizontalPileReference(r.S("titolo"), r.S("dettaglio"), r.S("uso"), HorizontalPileSheetViewModel.ReferenceTarget(r))).ToArray();
        Json.Text = sheet.Result?.ToJsonString(J.Options) ?? "";
        Tabs.SelectedIndex = 0;
    }

    private static TabItem Tab(string header, object content)
    {
        var tab = new TabItem { Header = header, Content = content };
        tab.SetResourceReference(BackgroundProperty, "Token.Paper");
        return tab;
    }

    private static ScrollViewer Diagram(Anthea.Application.Presentation.HorizontalDiagram diagram) => new()
    {
        Content = new HorizontalDiagrams(diagram) { MinWidth = DiagramWidth, MinHeight = DiagramHeight },
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto
    };
}
