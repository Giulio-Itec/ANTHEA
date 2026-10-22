using System.Globalization;
using Anthea.Application.Concrete;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules.ConcreteSection;

namespace Anthea.Presentation.Tests;

/// <summary>
/// The steps of the domains (F5.10, sub-step S3) driven on <see cref="ConcreteDomainPanelViewModel"/> as the historic trace drives the panels of
/// the tabs 02 and 03 (tests/Desktop/ConcreteSectionHistoricState.cs): the bar of the drawing (forces, check boxes, sliders, mesh, view), the
/// section on the selected action, the filters of the tables; and the part «[domini]» of the state, in the format of the historic trace (the 3D
/// domain as plain data, the chart of the 2D domain as the Plot of the historic panel).
/// </summary>
internal static partial class ConcreteSectionSheetChecks
{
    private sealed partial class Side
    {
        private ConcreteDomainPanelViewModel DomainPanel(string target) => target == "3D" ? Sheet.Domain3D : Sheet.Domain2D;

        /// <summary>A step on the bar or on the table of a domain panel; null when the step is not one.</summary>
        private (string? Message, bool Own)? ApplyDomainStep(ConcreteSectionStep step)
        {
            if (step.Target is not ("3D" or "2D")) return null;
            var panel = DomainPanel(step.Target);
            switch (step.Kind)
            {
                case ConcreteSectionStep.Toggle:
                {
                    // The check boxes of the bar of the drawing and «Asse neutro» stay usable in a read-only revision; «Valori σ/ε sulle barre» does not.
                    var option = panel.DisplayOptions.FirstOrDefault(o => o.Label == step.Key);
                    if (option is not null) option.IsChecked = !option.IsChecked;
                    else if (step.Key == "Asse neutro") panel.NeutralAxis = !panel.NeutralAxis;
                    else if (step.Key == "Valori σ/ε sulle barre") panel.BarValues = !panel.BarValues;
                    else throw new InvalidOperationException("Casella non trovata: " + step.Key);
                    return (null, true);
                }
                case ConcreteSectionStep.Button:
                {
                    var command = step.Key switch
                    {
                        "Mesh" => panel.MeshCommand,
                        "Sezione sull’azione selezionata" => panel.SectionOnActionCommand,
                        _ when step.Key!.StartsWith("Forze", StringComparison.Ordinal) => panel.ForceCommand,
                        _ => throw new InvalidOperationException("Pulsante non trovato: " + step.Key)
                    };
                    if (command.CanExecute(null)) command.Execute(null);
                    return (null, true);
                }
                // The view of the 3D domain moves only the camera.
                case ConcreteSectionStep.Pick when step.Key == "vista": return (null, true);
                case ConcreteSectionStep.Pick when step.Key == "stato":
                    return (ChooseInForm(panel, step), true);
                case ConcreteSectionStep.Slider:
                {
                    double value = double.Parse(step.Text!, CultureInfo.InvariantCulture);
                    if (step.Key == "trasparenza") panel.Transparency = value;
                    else if (step.Key == "dimensione_ed") panel.ActionPointSize = value;
                    else panel.ResistancePointSize = value;
                    return (null, true);
                }
                case ConcreteSectionStep.Filter:
                {
                    // The bar of the filter: column, mode, order and text, each applied when it changes (allowed in a read-only revision).
                    var filter = panel.Table.Table.Filter;
                    filter.Column = step.Key!;
                    filter.Mode = step.Index; filter.Order = step.Row; filter.Query = step.Text ?? "";
                    return (null, true);
                }
                default: return null;
            }
        }

        // The choice of the form of a panel, as the combo box of the historic form (Pick «stato» is the same control).
        private string? ChooseInForm(ConcreteDomainPanelViewModel panel, ConcreteSectionStep step)
        {
            var form = panel.Form;
            if (form.Contains(step.Key!) && form[step.Key!] is { Kind: FieldKind.Choice } field && Usable(form, field)) field.SelectedChoice = step.Text;
            return null;
        }
    }
}
