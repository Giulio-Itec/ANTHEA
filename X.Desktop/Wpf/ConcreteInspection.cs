using System.Text;
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
        internal int Generation;
        internal object? CacheKey;
        internal CheckerStressState? Applied;
        internal Task<CheckerStressState>? AppliedTask;
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
        graphic.Toolbar.Children.Add(Ui.Button("Proprietà…",ShowSectionProperties));
        var values=new CheckBox{Content="Valori σ/ε sulle barre",Margin=new Thickness(5)};
        values.Click+=(_,_)=>{s.View.BarValues=values.IsChecked==true;s.View.InvalidateVisual();};graphic.Toolbar.Children.Add(values);
        var viewTabs = new TabControl();
        Ui.Tab(viewTabs, "Mappa σ / ε", graphic);
        Ui.Tab(viewTabs, "Piano ε", new ViewportFrame("Piano delle deformazioni",s.Plane,()=>s.Plane.InvalidateVisual()));
        Ui.Tab(viewTabs, "Valori", s.Values);
        Ui.Tab(s.Tabs, "Dominio", domain);
        Ui.Tab(s.Tabs, "Sezione σ / ε", Ui.Dock(viewTabs, top: Ui.Bar(s.State), bottom: s.Message));
        s.State.SelectionChanged += (_, _) => UpdateSectionInspection(panel);
        s.Tabs.SelectionChanged += (_, e) => { if (e.Source == s.Tabs) UpdateSectionInspection(panel); };
        return s.Tabs;
    }
    private async void UpdateSectionInspection(DomainPanel panel)
    {
        var s = panel.Inspection; int generation = ++s.Generation;
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
            if (!ReferenceEquals(s.CacheKey, check)) { s.CacheKey = check; s.Applied = null; s.AppliedTask = null; }
            CheckerStressState state;
            if (limit) state = await Task.Run(() => check.LimitState.Value);
            else
            {
                var input = (JsonObject)Input.DeepClone(); var workspace = (JsonObject)settings.DeepClone(); var options = (JsonObject)panel.Options.DeepClone();
                options["modello"] = "Non lineare";
                // Ed is the entered force (including when the 2D check uses a projection).
                s.AppliedTask ??= Task.Run(() => new CheckerSection(input, workspace, options, panel.Key).Stress(action, "ED"));
                state = await s.AppliedTask;
            }
            if (disposed || version != revision || generation != s.Generation) return;
            if (!limit) s.Applied = state;
            s.View.Stress = state; s.View.InvalidateVisual();
            s.Plane.State=state;s.Plane.InvalidateVisual();
            var force = limit ? check.Resistance!.Value : action;
            s.Values.Text = InspectionText(state, force, limit ? "PUNTO LIMITE · assi locali" : "AZIONE INSERITA · assi " + panel.Options.S("assi"), preview.Section!);
            s.Message.Text = limit ? "Piano nativo del punto resistente · nessuna nuova ricerca di Rd." : "Azione inserita completa · leggi dei materiali della sezione; compressione negativa.";
        }
        catch (Exception ex) { if (!disposed && generation == s.Generation) { s.Message.Text = "Soluzione non disponibile: " + ex.Message; s.Values.Text = s.Message.Text; } }
    }
    private static string InspectionText(CheckerStressState state, ActionPoint force, string title, SezioneCA section)
    {
        var p = state.Native.StrainPlane;
        var b = new StringBuilder(title);
        b.AppendLine($"\nN = {force.N:G8} kN · Mx = {force.Mx:G8} kNm · My = {force.My:G8} kNm");
        b.AppendLine($"\nPIANO DI DEFORMAZIONE · coordinate geometriche in mm\nε(x,y) = ε0 + gx (x − x0) + gy (y − y0)\nε0 = {p.StrainReferencePoint:G9}\nx0 = {p.ReferencePoint.X:G9} mm · y0 = {p.ReferencePoint.Y:G9} mm\ngx = {p.ChiX:G9} mm⁻¹ · gy = {p.ChiY:G9} mm⁻¹\nAsse neutro: ε(x,y) = 0. Deformazioni positive a trazione.");
        b.AppendLine(ResponseSummary(state.Response, "ESTREMI DELLA SOLUZIONE"));
        b.AppendLine("\nBARRE · coordinate geometriche · tensioni MPa · deformazioni ‰");
        for (int i = 0; i < section.Bars.Count; i++) { var bar = section.Bars[i]; b.AppendLine($"B{i+1:00}: ({bar.X:0.###}; {bar.Y:0.###}) mm · Ø{bar.Diametro:0.###}\n  σ = {state.tensioni_barre[i]:0.######} MPa · ε = {state.BarStrains[i]:0.######} ‰"); }
        foreach (var v in state.ConcreteVertices) b.AppendLine($"{v.Id}: ({v.X:0.###}; {v.Y:0.###}) mm · σ = {v.Stress:0.######} MPa · ε = {v.Strain:0.######} ‰");
        return b.ToString();
    }
}
