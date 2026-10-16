using Anthea.Presentation.Modules.HorizontalPile;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace X.Desktop;

internal sealed partial class HorizontalWorkspace
{
    private static FrameworkElement CalculationDescription()
    {
        var content = Ui.Stack(Ui.Text("Sintesi del calcolo", 13, true), Ui.Text(HorizontalPileFields.CalculationDescription, 12, color: Ui.Muted));
        content.Margin = new Thickness(0, 14, 0, 8);
        content.Tag = "horizontal-calculation-description";
        return content;
    }

    private void AlignSurveyToolbar(UIElement buttons)
    {
        // One horizontal strip for tab headers and actions; small viewports scroll it.
        var root = new FrameworkElementFactory(typeof(DockPanel));
        var scroll = new FrameworkElementFactory(typeof(ScrollViewer), "SurveyToolbar");
        scroll.SetValue(DockPanel.DockProperty, Dock.Top);
        scroll.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
        scroll.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
        scroll.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 6));
        var strip = new FrameworkElementFactory(typeof(StackPanel));
        strip.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        var tabs = new FrameworkElementFactory(typeof(TabPanel));
        tabs.SetValue(Panel.IsItemsHostProperty, true);
        tabs.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        strip.AppendChild(tabs);
        var actions = new FrameworkElementFactory(typeof(ContentControl), "SurveyActions");
        actions.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        strip.AppendChild(actions); scroll.AppendChild(strip); root.AppendChild(scroll);
        var body = new FrameworkElementFactory(typeof(ContentPresenter), "PART_SelectedContentHost");
        body.SetValue(ContentPresenter.ContentSourceProperty, "SelectedContent");
        root.AppendChild(body);
        surveys.Template = new ControlTemplate(typeof(TabControl)) { VisualTree = root };
        surveys.ApplyTemplate();
        ((ContentControl)surveys.Template.FindName("SurveyActions", surveys)).Content = buttons;
    }
}
