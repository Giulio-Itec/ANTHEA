using System.Globalization;
using Anthea.Application.Concrete;
using Anthea.Presentation.Modules.ConcreteSection;

namespace Anthea.Presentation.Tests;

/// <summary>
/// The steps of the serviceability (F5.10, sub-step S4) driven on <see cref="ConcreteStressPanelViewModel"/> as the historic trace drives the
/// tab 04 (tests/Desktop/ConcreteSectionHistoricState.cs): the tabs of the families and of the details, the contour, the zones and the check
/// boxes of the map; and the part «[sle]» of the state, in the format of the historic trace (the map as the properties of its viewport).
/// The fields of the forms of the families go through <see cref="Side.Form"/>, the tables through the steps of the actions (S2).
/// </summary>
internal static partial class ConcreteSectionSheetChecks
{
    private static readonly string[] StressFamilies = ["SLE", "SLE_FREQ", "SLE_QP"];

    private sealed partial class Side
    {
        /// <summary>A step on the tab 04 outside the forms and the tables; null when the step is not one.</summary>
        private (string? Message, bool Own)? ApplyServiceabilityStep(ConcreteSectionStep step)
        {
            switch (step.Kind)
            {
                case ConcreteSectionStep.InnerTab when step.Target == "sle":
                    // The tabs of the families (and the choice «Combinazione SLE»): allowed in a read-only revision.
                    Sheet.StressFamily = step.Index;
                    return (null, true);
                case ConcreteSectionStep.InnerTab when step.Target!.StartsWith("dettagli-", StringComparison.Ordinal):
                    Sheet.StressPanel(step.Target[9..]).DetailTab = step.Index;
                    return (null, true);
                case ConcreteSectionStep.Pick when StressFamilies.Contains(step.Target) && step.Key == "contorno":
                {
                    // The choice of the contour: allowed in a read-only revision; an item not listed selects nothing.
                    var panel = Sheet.StressPanel(step.Target!);
                    if (ConcreteStressPanelViewModel.Contours.Contains(step.Text!)) panel.Contour = step.Text!;
                    return (null, true);
                }
                case ConcreteSectionStep.Pick when StressFamilies.Contains(step.Target) && step.Key == "zone":
                {
                    var panel = Sheet.StressPanel(step.Target!);
                    if (int.TryParse(step.Text, out int index) && index < panel.Regions.Count) panel.SelectedRegion = index;
                    return (null, true);
                }
                case ConcreteSectionStep.Toggle when StressFamilies.Contains(step.Target):
                {
                    var panel = Sheet.StressPanel(step.Target!);
                    // «Asse neutro» stays usable in a read-only revision; the check boxes of the values do not, and «σ/ε trefoli» is shown only
                    // with cables.
                    if (step.Key == "Asse neutro") { panel.NeutralAxis = !panel.NeutralAxis; return (null, true); }
                    var option = panel.ValueOptions.FirstOrDefault(o => o.Label == step.Key) ?? throw new InvalidOperationException("Casella non trovata: " + step.Key);
                    if (option.IsShown && option.CanChange) option.IsChecked = !option.IsChecked;
                    return (null, true);
                }
                default: return null;
            }
        }

        // The visit of the tab 04 (the tab controls in the order of the walk of the historic visit, after the main tabs): the families, then for
        // every family the column of the results and the tabs of the details, each selected and restored.
        private void VisitServiceability()
        {
            int family = Sheet.StressFamily;
            for (int i = 0; i < StressFamilies.Length; i++) Sheet.StressFamily = i;
            Sheet.StressFamily = family;
            foreach (var panel in Sheet.StressPanels)
            {
                int column = panel.VerificationTab;
                for (int i = 0; i < 2; i++) panel.VerificationTab = i;
                panel.VerificationTab = column;
                int detail = panel.DetailTab;
                for (int i = 0; i < 4; i++) panel.DetailTab = i;
                panel.DetailTab = detail;
            }
        }
    }
}
