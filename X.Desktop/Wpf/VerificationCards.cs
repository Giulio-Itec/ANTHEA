using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;
internal sealed class VerificationCards : Grid
{
    private readonly int columns;
    internal VerificationCards(int columns = 1)
    {
        this.columns = Math.Max(1, columns);
        for (int i = 0; i < this.columns; i++) ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
    }
    private void AddCard(FrameworkElement card)
    {
        int index = Children.Count;
        if (index % columns == 0) RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        SetRow(card, index / columns); SetColumn(card, index % columns);
        if (columns > 1) card.Margin = new Thickness(index % columns == 0 ? 0 : 6, 0, index % columns == columns - 1 ? 0 : 6, 6);
        Children.Add(card);
    }
    private string text = "";
    internal string Text { get => text; set { Start(); text = value; var message = Ui.Text(value, 12); SetColumnSpan(message, columns); Children.Add(message); } }
    internal void Start() { Children.Clear(); RowDefinitions.Clear(); text = ""; }
    private static Border Card(UIElement content, Brush color, string tooltip) => new()
    {
        BorderBrush = color, BorderThickness = new Thickness(5, 0, 0, 0), Background = Brushes.White,
        Padding = new Thickness(5, 2, 4, 2), Margin = new Thickness(0, 0, 0, 3), Child = content, ToolTip = tooltip
    };
    internal void AddDetail(string title, bool? passed, string values, string explanation, string reference)
    {
        string state = passed is null ? "DA COMPLETARE" : passed.Value ? "VERIFICATA" : "NON VERIFICATA";
        Brush color = passed is null ? UtilizationPalette.Brush(null) : passed.Value ? Ui.Brush("#39A879") : UtilizationPalette.Brush(2);
        var body = Ui.Stack(Ui.Text(title + " · " + state, 10, true), Ui.Text(values, 12), Ui.Text(explanation, 11), Ui.Text(reference, 10, color: Ui.Muted));
        body.Margin = new Thickness(0, 3, 0, 5);
        AddCard(Card(body, color, state + "\n" + explanation + "\n" + reference));
        text += $"{title}\n{state}\n{values}\n{explanation}\n{reference}\n\n";
    }
    internal void AddCheck(string title, int total, IEnumerable<(string Name, double? Ratio, bool? Passed)> checks)
        => AddSummary(VerificationSummary.Create(title, total, checks));
    internal void AddSummary(VerificationSummary summary)
    {
        string title = summary.Title, state = summary.Status;
        int total = summary.Total, missing = summary.Missing;
        double? ratio = summary.Ratio;
        Brush color = summary.Passed == false ? UtilizationPalette.Brush(2) : summary.Passed is null ? UtilizationPalette.Brush(null) : ratio is null ? Ui.Brush("#39A879") : UtilizationPalette.Brush(ratio);
        string governing = summary.Governing == "—" ? "—" : "Governa: " + summary.Governing;
        string value = ratio is not null ? "η = " + EngineeringFormat.Number(ratio) : "η —";
        string footer = governing + (missing > 0 ? $"\n{missing}/{total} verifiche mancanti / non applicabili" : $"\n{summary.Completed}/{total} combinazioni");
        var rate = Ui.Text(value, 12, true); rate.Margin = new Thickness(6, 0, 0, 0); rate.VerticalAlignment = VerticalAlignment.Center;
        var heading = Ui.Text(title + " · " + state, 10, true); heading.TextWrapping = TextWrapping.NoWrap; heading.TextTrimming = TextTrimming.CharacterEllipsis;
        var top = new DockPanel(); DockPanel.SetDock(rate, Dock.Right); top.Children.Add(rate); top.Children.Add(heading);
        var detail = Ui.Text(governing + (missing > 0 ? $" · {missing}/{total} mancanti" : $" · {summary.Completed}/{total}"), 10);
        detail.TextWrapping = TextWrapping.NoWrap; detail.TextTrimming = TextTrimming.CharacterEllipsis;
        AddCard(Card(Ui.Stack(top, detail), color, state + "\n" + footer + "\n" + UtilizationPalette.Legend));
        text += title + "\n" + state + "\n" + value + "\n" + footer + "\n";
    }
}
