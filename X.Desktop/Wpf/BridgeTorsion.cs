using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;

/// <summary>Box girder: the inputs of torsion, distortion and diaphragms and their results</summary>
internal sealed partial class BridgeWorkspace
{
    private readonly ContentControl torsionResults = new();
    private Expander? boxInputs;
    private Expander torsionGroup = null!;

    private Expander BuildBoxInputs()
    {
        InputForm? f = null;
        f = Form(Data, Anthea.Presentation.Modules.CompositeSection.CompositeSectionFields.Box, key =>
            {
                Update();
                if (key == "torsione_cassoncino") Dispatcher.BeginInvoke(BuildPhases);
            });
        void Update() => ShowFields(f, "cassoncino");
        Update();
        var group = Group("Cassoncino · torsione, distorsione e diaframmi", Ui.Stack(f,
            Ui.Text("Torsione: cella chiusa di Bredt, q = T/(2A0), con la soletta (fasi composte) o il controvento (fasi di solo acciaio). q si somma al taglio delle anime, al fondo, ai pioli e alla soletta. " +
                "Distorsione: trave su suolo elastico sulla campata appoggiata, diaframmi intermedi come molle; σdw oltre il 10% della flessione entra nelle verifiche del fondo (EN 1993-2 §6.2.7). " +
                "Diaframmi a piastra (taglio e imbozzamento) o controventi a X (diagonale compressa); diaframma d'appoggio a taglio e coppia degli apparecchi sugli irrigidimenti d'appoggio.", 11, color: Ui.Muted)));
        group.Visibility = Anthea.Application.Presentation.BridgeInputPresentation.IsBox(Data) ? Visibility.Visible : Visibility.Collapsed;
        return group;
    }
}
