using Anthea.Application.Presentation;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using X.Core;

namespace X.Desktop;

internal sealed partial class BridgeWorkspace
{
    internal readonly TabControl SectionPropertyTabs = new();
    private readonly ContentControl steelPropertyBody = new(), slabPropertyBody = new();
    private readonly TextBlock propertyNotice = Ui.Text("", 11, color: Ui.Muted);
    private InputForm slabPropertyForm = null!;
    private JsonObject slabPropertyOptions = null!;
    private UIElement propertiesSidebar = null!, analysisSidebar = null!;
    private readonly ContentControl sidebarHost = new();

    private void BuildSectionProperties()
    {
        var dock = new FrameworkElementFactory(typeof(DockPanel));
        var scroll = new FrameworkElementFactory(typeof(ScrollViewer)); scroll.SetValue(DockPanel.DockProperty, Dock.Top);
        scroll.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
        scroll.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
        var headers = new FrameworkElementFactory(typeof(TabPanel)); headers.SetValue(System.Windows.Controls.Panel.IsItemsHostProperty, true);
        scroll.AppendChild(headers); dock.AppendChild(scroll);
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter)); presenter.SetValue(ContentPresenter.ContentSourceProperty, "SelectedContent"); dock.AppendChild(presenter);
        SectionPropertyTabs.Template = new ControlTemplate(typeof(TabControl)) { VisualTree = dock };
        slabPropertyOptions = Anthea.Application.Presentation.BridgeSheetPresentation.SlabOptions(viewSettings);
        slabPropertyForm = new InputForm(slabPropertyOptions, Anthea.Presentation.Modules.CompositeSection.CompositeSectionFields.SlabProperties, key =>
        { Anthea.Application.Presentation.BridgeSheetPresentation.HomogenizationEdited(slabPropertyOptions, key); RefreshSlabProperties(); Modified?.Invoke(); }, true);
        SectionPropertyTabs.Items.Add(new TabItem { Header = Anthea.Application.Presentation.BridgeInputPresentation.SteelPropertiesHeader, Content = Scroll(steelPropertyBody) });
        SectionPropertyTabs.Items.Add(new TabItem { Header = Anthea.Application.Presentation.BridgeInputPresentation.SlabPropertiesHeader, Content = Scroll(Ui.Stack(slabPropertyForm,
            Ui.Text(Anthea.Application.Presentation.BridgeInputPresentation.SlabPropertiesNote, 11, color: Ui.Muted), slabPropertyBody)) });
        SectionPropertyTabs.SelectedIndex = 0;
        propertiesSidebar = Panel("Proprietà della sezione", Ui.Dock(SectionPropertyTabs, propertyNotice), "Sezione lorda · quote rispetto a y = 0");
    }
    private UIElement PropertyTable(PresentationTable table)
    {
        var body = ResultTable(table.Columns, table.Rows);
        return table.Collapsible ? Group(table.Title, body) : Block(table.Title, body, table.Note);
    }
    private void RefreshSlabProperties()
    {
        if (slabPropertyOptions is null) return;
        try
        {
            var h = Anthea.Application.Presentation.BridgeSheetPresentation.UpdateHomogenization(Data, slabPropertyOptions, false); string derived = slabPropertyOptions.S("modo") == "Da n" ? "phi" : "n";
            slabPropertyForm.Set(derived, slabPropertyOptions.S(derived), true);
            slabPropertyBody.Content = Ui.Stack(BridgePropertyPresentation.Slab(Data, slabPropertyOptions, h.N).Select(PropertyTable).ToArray());
        }
        catch (Exception ex) { slabPropertyBody.Content = Ui.Text(ex.Message, 12, color: Ui.Muted); }
    }
    private void RefreshSectionProperties()
    {
        if (propertiesSidebar is null) return;
        try
        {
            var properties = BridgePropertyPresentation.Steel(Data);
            steelPropertyBody.Content = Ui.Stack(properties.Tables.Select(PropertyTable).ToArray());
            propertyNotice.Text = properties.Notice;
        }
        catch (Exception ex) { propertyNotice.Text = Anthea.Application.Presentation.BridgeInputPresentation.PropertiesStale(ex.Message); }
        RefreshSlabProperties();
        object? selection = (SectionPropertyTabs.SelectedItem as TabItem)?.Tag; int selected = SectionPropertyTabs.SelectedIndex;
        while (SectionPropertyTabs.Items.Count > 2) SectionPropertyTabs.Items.RemoveAt(2);
        int i = 0;
        foreach (var phase in Data.Array("fasi").OfType<JsonObject>())
        {
            var body = new StackPanel();
            try
            {
                var properties = BridgePropertyPresentation.Phase(Data, phase);
                body.Children.Add(Ui.Text(properties.Title, 14, true));
                body.Children.Add(Ui.Text(properties.Description, 11, color: Ui.Muted));
                if (properties.Homogenization is not null) body.Children.Add(Ui.Text(properties.Homogenization, 12));
                body.Children.Add(PropertyTable(properties.Properties));
            }
            catch (Exception ex) { body.Children.Add(Ui.Text(ex.Message, 12, color: Ui.Muted)); }
            var item = new TabItem { Header = Anthea.Application.Presentation.BridgeInputPresentation.PropertyTabHeader(i++, phase), Tag = phase, Content = Scroll(body) };
            SectionPropertyTabs.Items.Add(item); if (ReferenceEquals(selection, phase)) selected = i + 1;
        }
        SectionPropertyTabs.SelectedIndex = Math.Clamp(selected, 0, SectionPropertyTabs.Items.Count - 1);
    }
}
