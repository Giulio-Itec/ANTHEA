using Anthea.Application.Walls;
using Anthea.Application.Presentation;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace X.Desktop;

internal sealed partial class RetainingWallWorkspace
{
    internal readonly TextBlock globalReadiness = Ui.Text("", 12);
    internal readonly TextBlock globalSearchSummary = Ui.Text("", 11, color: Ui.Muted);
    internal const string GlobalResultExplanation = RetainingWallPresentation.GlobalResultExplanation;

    internal void ShowGlobalSetup()
    {
        var g = Data["global_stability"]!;
        if (g.Array("valley").Count == 0 && g.Array("uphill").Count == 0 && g.Array("layers").Count == 0 && g.Array("valley_layers").Count == 0) PrepareGlobal();
        Pages.SelectedIndex = 0; Cards["Terreno"].IsExpanded = true; Cards["Stabilità globale"].IsExpanded = true;
        ScrollGlobalSetup();
    }

    private void ScrollGlobalSetup()
    {
        UpdateLayout(); var card = Cards["Stabilità globale"];
        DependencyObject? parent = card;
        while ((parent = VisualTreeHelper.GetParent(parent)) is not null)
            if (parent is ScrollViewer viewer)
            {
                double offset = card.TransformToAncestor(viewer).Transform(new Point()).Y + viewer.VerticalOffset;
                viewer.ScrollToVerticalOffset(Math.Max(0, offset)); break;
            }
    }

    private void InvalidateGlobalConfirmation()
    {
        Data["global_stability"]!["profile_confirmed"] = false;
        if (Forms.TryGetValue("global", out var form) && form.Editors["profile_confirmed"] is CheckBox box) box.IsChecked = false;
    }
    private void RefreshGlobalSearch()
    {
        var g = Data["global_stability"]!; bool automatic = g.S("search_mode") == "Automatica";
        if (automatic) WallModel.ProposeGlobalSearch(Data);
        // The limits are enabled by RetainingWallPresentation.GlobalRules (RefreshGlobal); the automatic proposal is shown here.
        if (automatic && Forms.TryGetValue("global_search", out var form)) foreach (string key in RetainingWallPresentation.GlobalSearchLimits) form.Set(key, g.S(key), display: true);
        globalSearchSummary.Text = RetainingWallPresentation.GlobalSearchSummary(Data);
    }

    private void RefreshGlobalGuidance()
    {
        var (text, tip, ready) = RetainingWallPresentation.GlobalGuidance(Data, independentGlobalError ?? Calculation?.GlobalError);
        globalReadiness.Text = text;
        globalReadiness.ToolTip = tip;
        ToolTipService.SetShowDuration(globalReadiness, 30000);
        globalReadiness.Foreground = Appearance.Foreground(ready ? Ui.Navy : Brushes.DarkGoldenrod);
        GlobalSetupButton.Content = RetainingWallPresentation.GlobalSetupText(Data, GlobalResult is not null);
        GlobalSetupButton.ToolTip = RetainingWallPresentation.GlobalSetupToolTip;
    }
}
