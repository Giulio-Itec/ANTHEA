using Anthea.Calculations;

namespace Anthea.Presentation.Modules.ConcreteSection;

/// <summary>Step 4 of docs/sviluppo/progetto-sistema.md (4/11/2026): the outcome of the sheet for the state of the project, from the same
/// summaries of the control panel and of the export (SLU, SLV, SLE, shear, torsion).</summary>
public sealed partial class ConcreteSheetViewModel : ISheetOutcome
{
    /// <inheritdoc/>
    public bool? Passed => HasResults ? VerificationSummary.Combine(CollectVerificationSummaries().Values) : null;
}
