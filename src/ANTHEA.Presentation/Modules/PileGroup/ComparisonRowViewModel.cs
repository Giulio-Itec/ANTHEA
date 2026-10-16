using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Application.Presentation;
using Anthea.Calculations;

namespace Anthea.Presentation.Modules.PileGroup;

/// <summary>
/// Row of the comparison of the methods and directions (the ComparisonRow of the historic view): the texts of the columns of one
/// result of the comparison, read with the rules of <see cref="PileGroupPresentation"/>.
/// </summary>
public sealed class ComparisonRowViewModel(JsonNode node)
{
    /// <summary>The result of one method in one direction, as the calculation writes it in «confronto».</summary>
    public JsonNode Node { get; } = node ?? throw new ArgumentNullException(nameof(node));

    public int Method => PileGroupPresentation.Method(Node);

    public string DirectionId => Node.S("DirectionId");

    public string Direction => Node.S("DirectionName") + " · " + PileGroupPresentation.Format(J.Number(Node["Angle"])) + "°";

    public string MethodName => HorizontalPileGroupEditor.MethodNames[Method];

    public string Minimum => PileGroupPresentation.Format(J.Number(Node["Minimum"]));

    public string Mean => PileGroupPresentation.Format(J.Number(Node["Factor"]));

    public string Maximum => PileGroupPresentation.Format(J.Number(Node["Maximum"]));

    public string Reduction => PileGroupPresentation.Format(J.Number(Node["ReductionPercent"])) + (PileGroupPresentation.Available(Node) ? "%" : "");

    public string Status => PileGroupPresentation.Applicability(Node);
}
