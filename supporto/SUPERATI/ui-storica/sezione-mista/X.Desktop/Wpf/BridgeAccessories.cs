using Anthea.Application.Presentation;
using Anthea.Presentation.Modules.CompositeSection;
using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;

internal sealed partial class BridgeWorkspace
{
    private readonly ContentControl shearResults = new(), studResults = new();
    private UIElement BuildAccessories()
    {
        var transverseInputs = BuildTransverseInputs(); var fatigueInputs = BuildFatigueInputs();
        InputForm? studs = null;
        studs = Form(Data, CompositeSectionFields.Studs, _ =>
            { UpdateStuds(); Dispatcher.BeginInvoke(BuildPhases); });
        void UpdateStuds()
        {
            transverseInputs.Visibility = fatigueInputs.Visibility = Data.B("pioli") ? Visibility.Visible : Visibility.Collapsed;
            ShowFields(studs, "pioli");
        }
        UpdateStuds();
        return Ui.Stack(BuildStiffenerInputs(), BuildSupportInputs(), Group("Connessione a pioli", Ui.Stack(studs,
            Ui.Text("File uniformi e centrate sulla piattabanda. Soletta piena, pioli Ø16–25 mm. q = Σ(V·S/I + Δq); gli effetti locali richiedono Δq assegnato per fase.", 11, color: Ui.Muted),
            transverseInputs, fatigueInputs)), boxInputs = BuildBoxInputs());
    }

    private bool PhaseHomogenizationNeeded(string kind) => Anthea.Application.Presentation.BridgeSheetPresentation.PhaseHomogenizationNeeded(Data, kind);
}
