using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace X.Desktop;

internal sealed partial class HorizontalWorkspace
{
    private static FrameworkElement CalculationDescription()
    {
        var content = Ui.Stack(Ui.Text("Sintesi del calcolo", 13, true), Ui.Text(
            "Per ogni sondaggio si confrontano i meccanismi applicabili e si assume la capacità ultima Hu più bassa.\n\n" +
            "• Corto: raggiungimento della resistenza del terreno, senza cerniere plastiche.\n" +
            "• Intermedio: cerniera in testa; previsto con rotazione impedita.\n" +
            "• Lungo: cerniera interna e, con testa impedita, anche in testa.\n\n" +
            "Testa libera: confronto corto/lungo. Testa impedita: corto/intermedio/lungo. La scelta non dipende dal solo rapporto L/D.\n\n" +
            "Rk = min(Hu,media/ξ3; Hu,min/ξ4).\nRd = η · Rk/1,3. Verifica soddisfatta se HEd ≤ Rd.\n\n" +
            "I grafici rappresentano lo stato ultimo. Per pali lunghi il tratto sotto la cerniera interna non è rappresentato: ciò non implica reazioni nulle. Il multistrato resta un’estensione sperimentale; le altre verifiche strutturali e di esercizio sono escluse.",
            12, color: Ui.Muted));
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
