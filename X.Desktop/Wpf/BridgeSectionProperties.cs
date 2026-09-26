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
        viewSettings["soletta_proprieta"] ??= J.Obj(("phi", "0"), ("psi", "1"), ("n", "6"), ("modo", "Da φ"));
        slabPropertyOptions = viewSettings["soletta_proprieta"]!.AsObject();
        slabPropertyForm = new InputForm(slabPropertyOptions, [new("phi", "Viscosità φ"), new("psi", "Moltiplicatore ψL"), new("n", "n = Ea / Ec,eff")], key =>
        { slabPropertyOptions["modo"] = key == "n" ? "Da n" : "Da φ"; RefreshSlabProperties(); Modified?.Invoke(); }, true);
        SectionPropertyTabs.Items.Add(new TabItem { Header = "Acciaio", Content = Scroll(steelPropertyBody) });
        SectionPropertyTabs.Items.Add(new TabItem { Header = "Soletta", Content = Scroll(Ui.Stack(slabPropertyForm,
            Ui.Text("Rapporti per esplorare la soletta; le fasi mantengono i propri φ/n. Omogeneizzazione al CLS, n delle barre = Es/Ec,eff.", 11, color: Ui.Muted), slabPropertyBody)) });
        SectionPropertyTabs.SelectedIndex = 0;
        propertiesSidebar = Panel("Proprietà della sezione", Ui.Dock(SectionPropertyTabs, propertyNotice), "Sezione lorda · quote rispetto a y = 0");
    }
    private UIElement PropertyValues(BridgeSectionProperties p)
    {
        string V(double? v) => v.HasValue ? F(v.Value) : "—";
        return ResultTable(["Proprietà", "Valore", "Unità"], new[] {
            new[] { "A", V(p.Area / 100), "cm²" }, new[] { "xG", V(p.X), "mm" }, new[] { "yG", V(p.Y), "mm" },
            new[] { "Ix", V(p.Ix / 1e4), "cm⁴" }, new[] { "Iy", V(p.Iy / 1e4), "cm⁴" }, new[] { "Ixy", "0", "cm⁴" },
            new[] { "Wx sup", V(p.WTop / 1000), "cm³" }, new[] { "Wx inf", V(p.WBottom / 1000), "cm³" },
            new[] { "ix", V(p.Rx), "mm" }, new[] { "iy", V(p.Ry), "mm" } });
    }
    private void RefreshSlabProperties()
    {
        if (slabPropertyOptions is null) return;
        try
        {
            var h = BridgeSection.Homogenization(Data, slabPropertyOptions); string derived = slabPropertyOptions.S("modo") == "Da n" ? "phi" : "n";
            slabPropertyOptions[derived] = (derived == "phi" ? h.Phi : h.N).ToString("R", CultureInfo.InvariantCulture); slabPropertyForm.Set(derived, slabPropertyOptions.S(derived), true);
            var g = BridgeSection.Geometry(Data); var m = BridgeSection.Materials(Data);
            var gross = BridgeSection.RectangleProperties("Solo calcestruzzo · lordo", g.Width, g.SlabHeight, 0, g.Width / 2);
            var transformed = BridgeSection.GrossPhaseProperties(Data, slabPropertyOptions, slabOnly: true);
            slabPropertyBody.Content = Ui.Stack(Block(gross.Name, PropertyValues(gross)),
                Block("Armature", ResultTable(["Fila", "As [cm²]", "y [mm]"], g.Bars.GroupBy(b => b.Y).OrderByDescending(p => p.Key).Select(p => new[] { p.Count() + " barre", F(p.Sum(b => b.Area) / 100), F(p.Key) }))),
                Block(transformed.Name, PropertyValues(transformed), $"n barre = {F(BridgeDerivedResults.RebarModularRatio(h.N, m.Rebar.ElasticModulusTension, m.Steel.ElasticModulusTension))} · Ec,eff = {F(BridgeDerivedResults.EffectiveConcreteModulus(m.Steel.ElasticModulusTension, h.N))} MPa"));
        }
        catch (Exception ex) { slabPropertyBody.Content = Ui.Text(ex.Message, 12, color: Ui.Muted); }
    }
    private void RefreshSectionProperties()
    {
        if (propertiesSidebar is null) return;
        try
        {
            var g = BridgeSection.Geometry(Data); var parts = BridgeSection.SteelPartProperties(g);
            steelPropertyBody.Content = Ui.Stack([
                Block("Carpenteria reale", PropertyValues(BridgeSection.CombineProperties("Reale", parts))),
                ..parts.Select(p => Group(p.Name, PropertyValues(p))),
                Group("Carpenteria equivalente di calcolo", PropertyValues(BridgeSection.CombineProperties("Equivalente", BridgeSection.SteelPartProperties(g, true))))]);
            propertyNotice.Text = BridgeSection.IsNonlinear(Data) ? "Proprietà geometriche con φ/n memorizzati. Nel non lineare istantaneo il calcolo usa φ=0; i valori adottati sono in Fasi e proprietà." : "Proprietà geometriche, indipendenti dai carichi. Le riduzioni efficaci dipendono dalle sollecitazioni e sono nella scheda Fasi e tensioni.";
        }
        catch (Exception ex) { propertyNotice.Text = "Proprietà da aggiornare: " + ex.Message; }
        RefreshSlabProperties();
        object? selection = (SectionPropertyTabs.SelectedItem as TabItem)?.Tag; int selected = SectionPropertyTabs.SelectedIndex;
        while (SectionPropertyTabs.Items.Count > 2) SectionPropertyTabs.Items.RemoveAt(2);
        int i = 0;
        foreach (var phase in Data.Array("fasi").OfType<JsonObject>())
        {
            var body = new StackPanel();
            try
            {
                var props = BridgeSection.GrossPhaseProperties(Data, phase);
                body.Children.Add(Ui.Text(phase.S("nome"), 14, true));
                body.Children.Add(Ui.Text(phase.S("tipo") + (phase.B("attiva") ? " · attiva" : " · esclusa dalla somma"), 11, color: Ui.Muted));
                if (BridgeSection.HasConcrete(phase.S("tipo")))
                {
                    var h = BridgeSection.Homogenization(Data, phase);
                    body.Children.Add(Ui.Text($"φ = {F(h.Phi)} · ψL = {F(phase.D("psi"))} · n = {F(h.N)}", 12));
                }
                body.Children.Add(Block(props.Name, PropertyValues(props), "Geometria e omogeneizzazione della fase; stessi dati della tabella Sollecitazioni."));
            }
            catch (Exception ex) { body.Children.Add(Ui.Text(ex.Message, 12, color: Ui.Muted)); }
            var item = new TabItem { Header = $"{++i:00} · {phase.S("nome")}", Tag = phase, Content = Scroll(body) };
            SectionPropertyTabs.Items.Add(item); if (ReferenceEquals(selection, phase)) selected = i + 1;
        }
        SectionPropertyTabs.SelectedIndex = Math.Clamp(selected, 0, SectionPropertyTabs.Items.Count - 1);
    }
}
