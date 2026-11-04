using Anthea.Application.Presentation;
using Anthea.Calculations;

namespace Anthea.Presentation.Modules.CompositeSection;

/// <summary>Step 4 of docs/sviluppo/progetto-sistema.md (4/11/2026): the outcome of the sheet for the state of the project, from the summaries
/// of the tab «Verifiche» of every situation of the calculation.</summary>
public sealed partial class CompositeSectionSheetViewModel : ISheetOutcome
{
    /// <inheritdoc/>
    public bool? Passed => HasResults && calculationResult is { } result
        ? VerificationSummary.Combine(Enumerable.Range(0, result.Stages.Count).SelectMany(stage => BridgeResultPresentation.Show(result, stage).Summaries))
        : null;
}
