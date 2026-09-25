using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;

internal sealed partial class HorizontalWorkspace
{
    private readonly ScrollViewer scroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly List<Border> cards = [];
    private readonly Dictionary<int, Button> expandButtons = [];
    private readonly ContentControl verification = new();
    private int expanded = -1;

    private void AddCard(string title, UIElement content, bool expandable = false)
    {
        int index = cards.Count;
        var heading = new DockPanel { Margin = new Thickness(0, 0, 0, 8), MinHeight = 28 };
        if (expandable)
        {
            var button = Ui.Button("Estendi", () => { expanded = expanded == index ? -1 : index; LayoutCards(); scroll.ScrollToTop(); }, inspection: true);
            button.FontSize = 11; button.Padding = new Thickness(5, 2, 5, 2);
            DockPanel.SetDock(button, Dock.Right); heading.Children.Add(button); expandButtons[index] = button;
        }
        heading.Children.Add(Ui.Text(title, 16, true));
        var card = Ui.Paper(Ui.Dock(content, heading), 12); card.ClipToBounds = true;
        cards.Add(card); layout.Children.Add(card);
    }

    private void LayoutCards()
    {
        if (building || cards.Count != 7) return;
        double w = Math.Max(320, (scroll.ViewportWidth > 0 ? scroll.ViewportWidth : ActualWidth) - 4);
        double h = Math.Max(280, (scroll.ViewportHeight > 0 ? scroll.ViewportHeight : ActualHeight) - 4);
        const double margin = 12, gap = 12;
        double[] widths = [Math.Max(general.UnwrappedSize().Width, model.UnwrappedSize().Width) + 30, efficiency.UnwrappedSize().Width + 48, factors.UnwrappedSize().Width + 30, 360];
        double top = Math.Max(340, general.UnwrappedSize().Height + 96) + (advanced.IsExpanded ? model.UnwrappedSize().Height : 0);
        foreach (var (i, button) in expandButtons) button.Content = expanded == i ? "Riduci" : "Estendi";
        foreach (var card in cards) card.Visibility = Visibility.Visible;
        void Place(int i, double x, double y, double cw, double ch)
        { Canvas.SetLeft(cards[i], x); Canvas.SetTop(cards[i], y); cards[i].Width = cw; cards[i].Height = ch; }
        if (expanded >= 0)
        {
            for (int i = 0; i < cards.Count; i++) cards[i].Visibility = i == expanded ? Visibility.Visible : Visibility.Collapsed;
            double expandedHeight = Math.Max(h - 2 * margin, expanded == 6 ? 540 : 350);
            Place(expanded, margin, margin, w - 2 * margin, expandedHeight);
            layout.Width = w; layout.Height = expandedHeight + 2 * margin;
        }
        else if (w >= widths.Sum() + 2 * margin + 3 * gap)
        {
            widths[3] = w - widths.Take(3).Sum() - 2 * margin - 3 * gap;
            double x = margin;
            for (int i = 0; i < 4; i++) { Place(i, x, margin, widths[i], top); x += widths[i] + gap; }
            double y = margin + top + gap, lower = Math.Max(490, h - y - margin), available = w - 2 * margin - 2 * gap;
            x = margin; double[] proportions = [.43, .20, .37];
            for (int i = 0; i < 3; i++) { double cw = available * proportions[i]; Place(i + 4, x, y, cw, lower); x += cw + gap; }
            layout.Width = w; layout.Height = y + lower + margin;
        }
        else
        {
            int columns = w >= 1050 ? 2 : 1;
            double cw = (w - 2 * margin - (columns - 1) * gap) / columns, y = margin;
            double[] heights = [top, 340, 270, 340];
            for (int i = 0; i < 4; i += columns)
            {
                double rowHeight = heights.Skip(i).Take(columns).Max();
                int count = Math.Min(columns, 4 - i);
                for (int c = 0; c < count; c++) Place(i + c, margin + c * (cw + gap), y, count == 1 ? w - 2 * margin : cw, rowHeight);
                y += rowHeight + gap;
            }
            for (int i = 4; i < 7; i++) { double ch = i == 6 ? 540 : Math.Clamp(h - 24, 350, 500); Place(i, margin, y, w - 2 * margin, ch); y += ch + gap; }
            layout.Width = w; layout.Height = y;
        }
        profile.ShowAll = expanded == 5; profile.InvalidateVisual();
    }

    private static DataGrid CenteredTable(string[] headers, IEnumerable<string[]> rows, double maximumWidth = 920)
    {
        var table = Ui.Table(headers, rows); table.FontSize = 13;
        table.MaxWidth = maximumWidth; table.HorizontalAlignment = HorizontalAlignment.Center;
        var text = new Style(typeof(TextBlock)); text.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
        text.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        text.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
        foreach (DataGridTextColumn column in table.Columns)
        { column.ElementStyle = text; column.Width = new DataGridLength(1, DataGridLengthUnitType.Star); }
        var header = new Style(typeof(DataGridColumnHeader), (Style)Application.Current.FindResource(typeof(DataGridColumnHeader)));
        header.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center)); table.ColumnHeaderStyle = header;
        return table;
    }

    private void UpdateVerification()
    {
        profile.HorizontalResult = Result; profile.InvalidateVisual();
        string Number(string key) => Result?[key] is null ? "—" : Result.D(key).ToString("N1");
        var panel = Ui.Stack(CenteredTable(["Grandezza", "Valore [kN]"], new[] {
            new[] { "Azione orizzontale HEd", Number("azione_kn") }, new[] { "Capacità ultima Hu", Number("capacita_kn") },
            new[] { "Resistenza caratteristica Rk", Number("resistenza_caratteristica_manuale_kn") },
            new[] { "Resistenza di progetto Rd", Number("resistenza_progetto_manuale_kn") } }, 650));
        bool available = Result?["resistenza_progetto_manuale_kn"] is not null;
        bool satisfied = available && Result!.D("azione_kn") <= Result.D("resistenza_progetto_manuale_kn");
        string outcome = Result is null ? "Da calcolare" : !available ? "Verifica non eseguita: Rd non determinata" :
            satisfied ? "Verifica soddisfatta" : "Verifica non soddisfatta";
        var label = Ui.Text(outcome, 14, true, !available ? Ui.Muted : satisfied ? Ui.Brush("#16703C") : Ui.Brush("#B42318"));
        label.TextAlignment = TextAlignment.Center; label.Margin = new Thickness(0, 8, 0, 8); panel.Children.Add(label);
        verification.Content = panel;
    }

    private static JsonObject NewLayer() => J.Obj(("tipologia", "Granulare"), ("spessore", "0"), ("peso_specifico", "0"),
        ("peso_specifico_saturo", "0"), ("angolo_attrito", "0"), ("coesione_non_drenata", "0"), ("coesione_efficace", "0"));

    private void DeleteSurvey(JsonArray survey, bool confirm = true)
    {
        var all = Data.Array("stratigrafie"); int index = all.IndexOf(survey);
        if (index < 0) return;
        if (all.Count == 1) { if (confirm) MessageBox.Show(Window.GetWindow(this), "Mantenere almeno una stratigrafia. È possibile eliminare tutti i suoi strati."); return; }
        if (confirm && MessageBox.Show(Window.GetWindow(this), $"Eliminare la stratigrafia {index + 1} e tutti i suoi strati?", "Stratigrafia", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        Commit(); int selected = surveys.SelectedIndex; all.RemoveAt(index);
        RebuildSurveys(index < selected ? selected - 1 : selected); Changed();
    }

    private void ChooseSurveyCopy(bool into)
    {
        int current = surveys.SelectedIndex; if (current < 0) return;
        var choices = Enumerable.Range(0, Data.Array("stratigrafie").Count).Where(i => i != current).Select(i => new KeyValuePair<int, string>(i, $"Stratigrafia {i + 1}")).ToList();
        if (into) choices.Add(new(-1, "Nuova stratigrafia"));
        if (choices.Count == 0) { MessageBox.Show(Window.GetWindow(this), "Non ci sono altre stratigrafie da cui copiare."); return; }
        var choice = new ComboBox { ItemsSource = choices, DisplayMemberPath = "Value", SelectedValuePath = "Key", SelectedIndex = 0, Margin = new Thickness(8) };
        var dialog = Ui.Dialog(this, into ? "Copia stratigrafia in…" : "Copia stratigrafia da…", new Grid(), 440, 210);
        var ok = Ui.Button("Copia", () => dialog.DialogResult = true, true); ok.IsDefault = true;
        var cancel = Ui.Button("Annulla", () => dialog.DialogResult = false); cancel.IsCancel = true;
        dialog.Content = Ui.Paper(Ui.Dock(choice, Ui.Text(into ? $"Copia la stratigrafia {current + 1} in:" : $"Sostituisci la stratigrafia {current + 1} copiando da:"), Ui.Bar(ok, cancel)));
        if (dialog.ShowDialog() == true && choice.SelectedValue is int other) CopySurvey(into ? current : other, into ? other : current);
    }

    private bool CopySurvey(int source, int destination, bool confirm = true)
    {
        var all = Data.Array("stratigrafie");
        if (source < 0 || source >= all.Count || destination < -1 || destination >= all.Count || source == destination) return false;
        if (destination >= 0 && confirm && MessageBox.Show(Window.GetWindow(this), $"Sostituire gli strati della stratigrafia {destination + 1}?", "Copia stratigrafia", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return false;
        Commit(); var copy = all[source]!.DeepClone();
        if (destination < 0) { all.Add(copy); destination = all.Count - 1; } else all[destination] = copy;
        RebuildSurveys(destination); Changed(); return true;
    }
}
