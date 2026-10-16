using Anthea.Application.Materials;
using Anthea.Application.Modules;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;

internal sealed partial class RebarMaterialView : UserControl
{
    internal JsonObject Data { get; }
    internal JsonObject Input => Data["input"]!.AsObject();
    internal readonly ComboBox Selection;
    internal readonly InputForm Properties;
    private readonly InputForm identity, coefficient;
    internal readonly TextBlock Status = Ui.Text("", 12);
    private readonly TextBlock description = Ui.Text("", 12, color: Ui.Muted), results = Ui.Text("", 13), source = Ui.Text("", 11, color: Ui.Muted);
    private readonly Plot diagram = new() { Height = 295, Title = "Legame caratteristico σ–ε", XLabel = "Deformazione ε [‰]", YLabel = "Tensione σ [MPa]", InvertY = false, EmptyMessage = "Completare i dati per visualizzare il diagramma." };
    private bool refreshing;
    internal event Action? Modified;

    internal RebarMaterialView(JsonObject data)
    {
        Data = data;
        Selection = Ui.Choice(RebarPresentation.Types());
        Selection.MinHeight = 28; Selection.FontSize = 14;
        Selection.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, "Tipo di acciaio");
        identity = new InputForm(Input, [new("materiale_acciaio_nome", "Nome", Wide: true)], _ => Changed(), compact: true);
        Properties = new InputForm(Input, [new("fyk_mpa", "Snervamento fyk", "MPa"), new("steel_fu_mpa", "Rottura fu", "MPa"),
            new("steel_modulus_mpa", "Modulo elastico Es", "MPa"), new("steel_eps_u", "Deformazione ultima εu", "‰"),
            new("steel_diagramma", "Diagramma", Choices: ["Elastoplastico", "Incrudente"])], _ => Changed(), compact: true, wideChoices: true);
        coefficient = new InputForm(Input, [new("gamma_s", "Coefficiente parziale γs")], _ => Changed(), compact: true);
        var reference = new TextBox { Text = data.S("riferimento"), MinHeight = 58, MaxHeight = 90, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        reference.TextChanged += (_, _) => { Data["riferimento"] = reference.Text; Modified?.Invoke(); };
        reference.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, "Certificato o riferimento");
        var grid = new Grid { Margin = new Thickness(12), MaxWidth = 1330, HorizontalAlignment = HorizontalAlignment.Left };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });
        void Add(int col, string title, UIElement body)
        {
            var header = new Border { Background = Ui.Navy, Padding = new Thickness(12, 9, 12, 9), Child = Ui.Text(title, 14, true, Brushes.White) };
            var content = new Border { Child = body, Padding = new Thickness(12) };
            var card = Ui.Paper(Ui.Stack(header, content), 0); card.Margin = new Thickness(0, 0, 10, 0); card.VerticalAlignment = VerticalAlignment.Top;
            Grid.SetColumn(card, col); grid.Children.Add(card);
        }
        static FrameworkElement Space(UIElement value, double top = 10) => new Border { Child = value, Margin = new Thickness(0, top, 0, 0) };
        Add(0, "Acciaio per armature", Ui.Stack(Ui.Text("Tipo di acciaio", 12, true), Selection, identity, Space(description),
            Space(Ui.Text("Certificato / riferimento", 12, true), 18), reference, Space(source)));
        Add(1, "Proprietà meccaniche", Ui.Stack(Properties, Space(coefficient, 16), Space(results),
            Space(Ui.Text("fyd = fyk / γs   ·   εyd = fyd / Es\nValori aggiornati automaticamente.", 11, color: Ui.Muted)),
            Space(Ui.Paper(Status, 9))));
        Add(2, "Diagramma del materiale", Ui.Stack(diagram, Ui.Text("Curva caratteristica in trazione. Il coefficiente γs è riportato nelle proprietà di calcolo.", 11, color: Ui.Muted)));
        Content = new ScrollViewer { Content = grid, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Appearance.Surface };
        Selection.SelectionChanged += (_, _) =>
        {
            if (refreshing || Selection.SelectedItem is not string name) return;
            RebarEditor.Select(Input, name); RefreshEditors(); Refresh(); Modified?.Invoke();
        };
        Refresh();
    }
    internal void Commit() { Properties.Commit(); identity.Commit(); coefficient.Commit(); }
    private void Changed()
    {
        if (refreshing) return;
        if (RebarPresentation.MarkCustom(Input)) identity.Set("materiale_acciaio_nome", Input.S("materiale_acciaio_nome"), true);
        Refresh(); Modified?.Invoke();
    }
    private void RefreshEditors()
    {
        refreshing = true;
        foreach (var form in new[] { identity, Properties, coefficient })
            foreach (string key in form.Editors.Keys) form.Set(key, Input.S(key), true);
        refreshing = false;
    }
    private void Refresh()
    {
        // Type, fixed fields, texts and values: Anthea.Application.Materials.RebarPresentation (F5.5).
        var state = RebarPresentation.Read(Input);
        refreshing = true; Selection.SelectedItem = state.Selected; refreshing = false;
        foreach (string key in RebarPresentation.PropertyKeys)
        {
            var box = (TextBox)Properties.Editors[key]; box.IsReadOnly = state.Locked(key);
            box.Background = box.IsReadOnly ? Appearance.Calculated : Appearance.Paper;
        }
        Properties.Enable("steel_diagramma", state.DiagramEnabled);
        ((TextBox)identity.Editors["materiale_acciaio_nome"]).IsReadOnly = state.NameLocked;
        description.Text = state.Description;
        source.Text = state.Source;
        try
        {
            var values = RebarPresentation.Results(ModuleServices.Get(RebarEditor.Module).Calculate(Data), state.Historical);
            results.Text = values.Text;
            Status.Text = values.Status;
            Status.Foreground = Appearance.Foreground(Ui.Brush(values.Coherent ? "#18794E" : "#9A6700"));
            diagram.Series = [new(Input.S("materiale_acciaio_nome"), values.Diagram.ToList(), Ui.Blue)];
        }
        catch (ArgumentException ex)
        {
            var values = RebarPresentation.Failed(ex);
            results.Text = values.Text;
            Status.Text = values.Status; Status.Foreground = Appearance.Foreground(Ui.Brush("#9A6700")); diagram.Series = [];
        }
        diagram.ResetView();
    }
}

internal sealed class RebarIcon : FrameworkElement
{
    protected override void OnRender(DrawingContext dc)
    {
        var rib = new Pen(Ui.Brush("#C7953E"), 3);
        for (int bar = 0; bar < 3; bar++)
        {
            double y = 20 + bar * 19;
            dc.DrawLine(new Pen(Appearance.Ink, 9), new Point(12, y + 12), new Point(68, y - 5));
            for (int i = 0; i < 6; i++) { double x = 16 + i * 9, cy = y + 12 - (x - 12) * 17 / 56; dc.DrawLine(rib, new Point(x - 2, cy - 4), new Point(x + 2, cy + 4)); }
        }
    }
}
