using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;

internal sealed partial class ConcreteWorkspace
{
    private sealed class SectionInspection
    {
        internal readonly ConcreteSectionViewport View = new();
        internal readonly ConcreteStrainPlaneView Plane = new();
        internal readonly TextBox Values = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        internal readonly ComboBox State = Ui.Choice(["Azione inserita", "Punto limite"], "Punto limite");
        internal readonly TextBlock Message = Ui.Text("Selezionare una combinazione", 11);
        internal readonly TabControl Tabs = new();
        internal readonly Anthea.Application.Concrete.ConcreteInspectionSession Session = new();
    }
    private UIElement BuildSectionInspection(DomainPanel panel, UIElement domain)
    {
        var s = panel.Inspection;
        var contour = Ui.Choice(ConcreteSectionViewport.Contours, ConcreteSectionViewport.Contours[0]);
        RevisionInspection.Allow(contour); RevisionInspection.Allow(s.State);
        contour.SelectionChanged += (_, _) =>
        {
            if (contour.SelectedItem is not string selected) return;
            s.View.Contour = selected;
            s.View.InvalidateVisual();
        };
        var graphic = new ViewportFrame("Sezione · tensioni e deformazioni", s.View, s.View.ResetView);
        graphic.Toolbar.Children.Insert(0, contour);
        graphic.Toolbar.Children.Add(NeutralAxisToggle(s.View, panel.Options));
        graphic.Toolbar.Children.Add(Ui.Button("Proprietà…",ShowSectionProperties));
        var values=new CheckBox{Content="Valori σ/ε sulle barre",Margin=new Thickness(5)};
        values.Click+=(_,_)=>{s.View.BarValues=values.IsChecked==true;s.View.InvalidateVisual();};graphic.Toolbar.Children.Add(values);
        var viewTabs = new TabControl();
        Ui.Tab(viewTabs, "Mappa σ / ε", graphic);
        Ui.Tab(viewTabs, "Piano ε", new ViewportFrame("Piano delle deformazioni",s.Plane,()=>s.Plane.InvalidateVisual()));
        Ui.Tab(viewTabs, "Valori", s.Values);
        Ui.Tab(s.Tabs, "Dominio", domain);
        Ui.Tab(s.Tabs, "Sezione σ / ε", Ui.Dock(viewTabs, top: Ui.Bar(s.State), bottom: s.Message));
        s.State.SelectionChanged += (_, _) => { _ = UpdateSectionInspection(panel); };
        s.Tabs.SelectionChanged += (_, e) => { if (e.Source == s.Tabs) _ = UpdateSectionInspection(panel); };
        return s.Tabs;
    }
    private CheckBox NeutralAxisToggle(ConcreteSectionViewport view, JsonObject options)
    {
        view.ShowNeutralAxis = options.B("asse_neutro", true);
        var toggle = new CheckBox { Content = "Asse neutro", IsChecked = view.ShowNeutralAxis, Margin = new Thickness(5, 3, 5, 3),
            ToolTip = "Linea ε = 0 del piano calcolato. Con deformazione uniforme l’asse non è univoco; non coincide necessariamente con lo zero delle tensioni nei trefoli." };
        RevisionInspection.Allow(toggle);
        toggle.Click += (_, _) => { options["asse_neutro"] = view.ShowNeutralAxis = toggle.IsChecked == true; view.InvalidateVisual(); Modified?.Invoke(); };
        return toggle;
    }
    private async Task UpdateSectionInspection(DomainPanel panel)
    {
        var s = panel.Inspection; s.Session.Invalidate();
        s.View.Stress = null; s.View.Section = preview.Section; s.View.Tendons = preview.Tendons; s.View.InvalidateVisual();
        s.Plane.State=null;s.Plane.Section=preview.Section;s.Plane.InvalidateVisual();
        s.Values.Text = "Nessuna soluzione aggiornata disponibile.";
        if (initializing || disposed || s.Tabs.SelectedIndex != 1) return;
        var row = panel.Grid?.SelectedItem as JsonRow;
        var check = row is null ? null : domainResults.GetValueOrDefault(panel.Prefix + panel.Key)?.GetValueOrDefault(row.Values.S("id"));
        if (row is null || check?.LimitState is null) { s.Message.Text = "Selezionare una combinazione calcolata."; return; }
        bool limit = s.State.SelectedIndex == 1; int version = revision;
        s.Message.Text = limit ? "Lettura del piano già calcolato al limite…" : "Analisi dell’azione inserita…";
        try
        {
            var action = ReadAction(row);
            var completion = await s.Session.CalculateAsync(check, limit, Input, settings, panel.Options, panel.Key, action);
            completion.TryPublish(current =>
            {
                if (disposed || version != revision) return;
                if (current.Error is not null) { s.Message.Text = "Soluzione non disponibile: " + current.Error.Message; s.Values.Text = s.Message.Text; return; }
                var state = current.Value!;
                s.View.Stress = state; s.View.InvalidateVisual();
                s.Plane.State = state; s.Plane.InvalidateVisual();
                var force = limit ? check.Resistance!.Value : action;
                s.Values.Text = Anthea.Application.Concrete.ConcreteResponsePresentation.InspectionText(state, force,
                    limit ? "PUNTO LIMITE · assi locali" : "AZIONE INSERITA · assi " + panel.Options.S("assi"), preview.Section!);
                s.Message.Text = limit ? "Piano nativo del punto resistente · nessuna nuova ricerca di Rd." : "Azione inserita completa · leggi dei materiali della sezione; compressione negativa.";
            });
        }
        catch (Exception ex) { if (!disposed && version == revision) { s.Message.Text = "Soluzione non disponibile: " + ex.Message; s.Values.Text = s.Message.Text; } }
    }
}
