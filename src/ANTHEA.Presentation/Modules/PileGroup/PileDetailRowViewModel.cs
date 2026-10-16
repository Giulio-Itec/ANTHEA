using System.Text.Json.Nodes;
using Anthea.Application.Presentation;
using Anthea.Calculations;

namespace Anthea.Presentation.Modules.PileGroup;

/// <summary>
/// Row of the values per pile of the selected result (the anonymous rows of the historic view): β only for the pile-to-pile
/// methods, α only for Caltrans, factors and reductions only when the method is applicable; «—» otherwise.
/// </summary>
/// <param name="Pile">Identifier of the pile.</param>
/// <param name="Row">Row in the direction of the load (1 = leading row).</param>
/// <param name="Parallel">Coordinate q along the load [m].</param>
/// <param name="Transverse">Coordinate t orthogonal to the load [m].</param>
/// <param name="Beta">β of the pile.</param>
/// <param name="Alpha">α of the row.</param>
/// <param name="Factor">Coefficient of the pile.</param>
/// <param name="Reduction">Reduction of the pile [%].</param>
public sealed record PileDetailRowViewModel(string Pile, double Row, string Parallel, string Transverse, string Beta, string Alpha, string Factor, string Reduction)
{
    /// <summary>The row of <paramref name="pile"/> of the selected result, with the rules of the historic view.</summary>
    internal static PileDetailRowViewModel For(JsonNode pile, int method, bool available) => new(
        pile.S("Id"), PileGroupPresentation.Row(pile),
        PileGroupPresentation.Format(J.Number(pile["ParallelCoordinate"])), PileGroupPresentation.Format(J.Number(pile["TransverseCoordinate"])),
        PileGroupPresentation.ShowsBeta(method) && available ? PileGroupPresentation.Format(J.Number(pile["Beta"])) : "—",
        PileGroupPresentation.ShowsAlpha(method) && available ? PileGroupPresentation.Format(J.Number(pile["Alpha"])) : "—",
        available ? PileGroupPresentation.Format(J.Number(pile["Factor"])) : "—",
        available ? PileGroupPresentation.Format(PileGroupPresentation.Reduction(pile)) + "%" : "—");
}

/// <summary>
/// What the drawing of the plan (PileGroupPlan, hosted unchanged by the view) receives: the data, the selected result and the
/// outline of the cap; both null while there is no result.
/// </summary>
public sealed record PileGroupPlanInput(JsonObject Data, JsonNode? Selected, JsonArray? Cap);
