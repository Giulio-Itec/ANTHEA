using Anthea.Application.Presentation;
using Anthea.Calculations;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Anthea.Presentation.Modules.ConcreteSection;

/// <summary>Meaning of the colour of a card: the view maps it to its theme, the historic VerificationCards used fixed brushes.</summary>
public enum ConcreteCardTone
{
    /// <summary>Outcome missing or not applicable (the grey of <see cref="UtilizationColors"/> for no value).</summary>
    Missing,
    /// <summary>Satisfied without a ratio (green).</summary>
    Passed,
    /// <summary>Not satisfied (the red of a ratio above 1).</summary>
    Failed,
    /// <summary>Coloured by the utilization ratio <see cref="ConcreteCard.Ratio"/> (<see cref="UtilizationColors"/>).</summary>
    Utilization,
    /// <summary>A value without an outcome (the blue of the historic AddValue).</summary>
    Information
}

/// <summary>A card of <see cref="ConcreteCardsViewModel"/>: heading, value, explanation, reference and the meaning of its colour.</summary>
/// <param name="Heading">First line, in bold (title and state, or the title of a value).</param>
/// <param name="Value">The value, or the governing combination of a summary.</param>
/// <param name="Explanation">Explanation or the second line of a summary.</param>
/// <param name="Reference">Reference of a detail; empty otherwise.</param>
/// <param name="ToolTip">Tooltip of the card, as the historic card.</param>
/// <param name="Tone">Meaning of the colour.</param>
/// <param name="Ratio">The ratio of a card coloured by <see cref="ConcreteCardTone.Utilization"/>.</param>
public sealed record ConcreteCard(string Heading, string Value, string Explanation, string Reference, string ToolTip, ConcreteCardTone Tone, double? Ratio = null);

/// <summary>
/// The verification cards of the RC section (the historic VerificationCards of X.Desktop, F5.10 S1) without WPF: a message
/// (<see cref="Text"/> set) or a list of cards, and the text that the export and the summaries read, built exactly as the historic
/// cards build it (<see cref="AddDetail"/>, <see cref="AddValue"/>, <see cref="AddSummary"/>).
/// </summary>
public sealed class ConcreteCardsViewModel : ObservableObject
{
    private readonly List<ConcreteCard> cards = [];
    private string text = "";
    private string? message;

    /// <param name="columns">Columns of the cards in the view (1 or 2).</param>
    public ConcreteCardsViewModel(int columns = 1) => Columns = Math.Max(1, columns);

    public int Columns { get; }

    /// <summary>The cards, in their order (a new list at every change, for the bindings of the view).</summary>
    public IReadOnlyList<ConcreteCard> Cards { get; private set; } = [];

    /// <summary>The message shown in place of the cards (the text set); null when the cards are shown.</summary>
    public string? Message { get => message; private set => SetProperty(ref message, value); }

    /// <summary>The text of the cards (read by the export of the sheet); setting it shows it as a message, after <see cref="Start"/>.</summary>
    public string Text
    {
        get => text;
        set
        {
            Start();
            text = value ?? "";
            Message = text;
            OnPropertyChanged();
        }
    }

    /// <summary>Removes the cards and the message.</summary>
    public void Start()
    {
        cards.Clear(); text = ""; Message = null;
        Cards = []; OnPropertyChanged(nameof(Cards)); OnPropertyChanged(nameof(Text));
    }

    /// <summary>A verification detail: state from <paramref name="passed"/>, values, explanation and reference.</summary>
    public void AddDetail(string title, bool? passed, string values, string explanation, string reference)
    {
        var detail = new VerificationDetail(title, passed, values, explanation, reference);
        var tone = passed is null ? ConcreteCardTone.Missing : passed.Value ? ConcreteCardTone.Passed : ConcreteCardTone.Failed;
        Add(new ConcreteCard(title + " · " + detail.State, values, explanation, reference, detail.State + "\n" + explanation + "\n" + reference, tone), detail.Text);
    }

    /// <summary>A value without outcome.</summary>
    public void AddValue(string title, string value, string explanation) =>
        Add(new ConcreteCard(title, value, explanation, "", explanation, ConcreteCardTone.Information), $"{title}\n{value}\n{explanation}\n\n");

    /// <summary>The summary of a verification: state, governing ratio and completed combinations.</summary>
    public void AddSummary(VerificationSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        int total = summary.Total, missing = summary.Missing;
        double? ratio = summary.Ratio;
        var tone = summary.Passed == false ? ConcreteCardTone.Failed : summary.Passed is null ? ConcreteCardTone.Missing
            : ratio is null ? ConcreteCardTone.Passed : ConcreteCardTone.Utilization;
        string governing = summary.Governing == "—" ? "—" : "Governa: " + summary.Governing;
        string value = ratio is not null ? "η = " + EngineeringFormat.Number(ratio) : "η —";
        string footer = governing + (missing > 0 ? $"\n{missing}/{total} verifiche mancanti / non applicabili" : $"\n{summary.Completed}/{total} combinazioni");
        string detail = governing + (missing > 0 ? $" · {missing}/{total} mancanti" : $" · {summary.Completed}/{total}");
        Add(new ConcreteCard(summary.Title + " · " + summary.Status, value, detail, "", summary.Status + "\n" + footer + "\n" + UtilizationColors.Legend, tone, ratio),
            summary.Title + "\n" + summary.Status + "\n" + value + "\n" + footer + "\n");
    }

    // As the historic cards, a message already shown stays above the cards added after it without Start.
    private void Add(ConcreteCard card, string addition)
    {
        cards.Add(card); text += addition;
        Cards = cards.ToArray(); OnPropertyChanged(nameof(Cards)); OnPropertyChanged(nameof(Text));
    }
}
