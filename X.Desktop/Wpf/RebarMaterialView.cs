using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;

internal sealed class RebarMaterialView : UserControl
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
        Selection = Ui.Choice(RebarMaterial.Catalog().Select(p => p.Name).Append("Personalizzato"));
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
        Content = new ScrollViewer { Content = grid, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Ui.Bg };
        Selection.SelectionChanged += (_, _) =>
        {
            if (refreshing || Selection.SelectedItem is not string name) return;
            RebarMaterial.Select(Input, name); RefreshEditors(); Refresh(); Modified?.Invoke();
        };
        Refresh();
    }
    internal void Commit() { Properties.Commit(); identity.Commit(); coefficient.Commit(); }
    private void Changed()
    {
        if (refreshing) return;
        if (RebarMaterial.Match(Input) is null && Input.S("classe_acciaio") != "Personalizzato")
        { Input["classe_acciaio"] = "Personalizzato"; Input["materiale_acciaio_nome"] = "Acciaio personalizzato"; identity.Set("materiale_acciaio_nome", Input.S("materiale_acciaio_nome"), true); }
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
        var preset = RebarMaterial.Match(Input); bool custom = preset is null, historical = preset?.A5 is not null;
        refreshing = true; Selection.SelectedItem = preset?.Name ?? "Personalizzato"; refreshing = false;
        foreach (string key in new[] { "fyk_mpa", "steel_fu_mpa", "steel_modulus_mpa", "steel_eps_u" })
        {
            var box = (TextBox)Properties.Editors[key]; box.IsReadOnly = !custom && !(historical && key == "steel_eps_u");
            box.Background = box.IsReadOnly ? Ui.Brush("#EAF2FA") : Brushes.White;
        }
        Properties.Enable("steel_diagramma", custom || historical);
        ((TextBox)identity.Editors["materiale_acciaio_nome"]).IsReadOnly = !custom;
        description.Text = custom ? "Dati personalizzati. Specificare la provenienza dei valori adottati."
            : historical ? $"{preset!.Surface}\nAcciaio storico · A5 minimo {preset.A5:0}%\nA5 non coincide con εu. Definire εu per le verifiche."
            : preset!.Name == "B450A" ? "Aderenza migliorata · classe A\nL'impiego del B450A è soggetto alle limitazioni delle NTC 2018, §§ 7.4.2.2 e 11.3.2.1."
            : "Aderenza migliorata · classe C\nAcciaio B450C da catalogo.";
        source.Text = historical ? "D.M. 09/01/1996, prospetti 1-I e 2-I: resistenze minime nominali, non valori misurati. Es = 200 000 MPa assunto. Per dati da prove scegliere Personalizzato."
            : custom ? "Il riferimento rimane nella scheda. Le proprietà meccaniche sono condivisibili con i fogli della stessa sezione."
            : "Proprietà dal catalogo dei materiali già utilizzato dalla verifica in c.a. Per modificarle scegliere Personalizzato.";
        try
        {
            var v = RebarMaterial.Evaluate(Input);
            string F(double n) => n.ToString("0.##", CultureInfo.GetCultureInfo("it-IT"));
            results.Text = $"fyd     {F(v.Fyd)} MPa\nεyd     {F(v.EpsilonYd)} ‰\nfu / fyk     {F(v.Ratio)}";
            Status.Text = historical ? "Dati completi. Per strutture esistenti verificare i valori adottati e i coefficienti applicabili." : "✓ Proprietà coerenti";
            Status.Foreground = historical ? Ui.Brush("#9A6700") : Ui.Brush("#18794E");
            diagram.Series = [new(Input.S("materiale_acciaio_nome"), RebarMaterial.Curve(Input), Ui.Blue)];
        }
        catch (ArgumentException ex)
        {
            results.Text = "fyd     —\nεyd     —\nfu / fyk     —";
            Status.Text = "⚠ " + ex.Message; Status.Foreground = Ui.Brush("#9A6700"); diagram.Series = [];
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
            dc.DrawLine(new Pen(Ui.Navy, 9), new Point(12, y + 12), new Point(68, y - 5));
            for (int i = 0; i < 6; i++) { double x = 16 + i * 9, cy = y + 12 - (x - 12) * 17 / 56; dc.DrawLine(rib, new Point(x - 2, cy - 4), new Point(x + 2, cy + 4)); }
        }
    }
}
