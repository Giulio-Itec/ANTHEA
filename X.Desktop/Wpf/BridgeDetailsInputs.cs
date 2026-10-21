using Anthea.Presentation.Modules.CompositeSection;
using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;

internal sealed partial class BridgeWorkspace
{
    internal readonly BridgeDetailSketch DetailSketch = new();
    internal readonly ComboBox DetailSectionChoice = Ui.Choice(CompositeSectionFields.DetailSections, CompositeSectionFields.DetailSections[0]);
    private Expander detailExpander = null!;
    private UIElement BuildStiffenerInputs()
    {
        InputForm? f = null;
        f = Form(Data, CompositeSectionFields.Stiffeners, _ => ShowFields(f, "irrigidimenti"));
        ShowFields(f, "irrigidimenti");
        return Group("Taglio e irrigidimenti intermedi", Ui.Stack(f,
            Ui.Text("Piatti continui senza intagli. La verifica considera eccentricità, II ordine, rigidezza e instabilità. Un irrigidimento non idoneo non aumenta la resistenza a taglio.", 11, color: Ui.Muted)));
    }
    private UIElement BuildSupportInputs()
    {
        InputForm? f = null;
        f = Form(Data, CompositeSectionFields.Support, _ => ShowFields(f, "appoggio"));
        ShowFields(f, "appoggio");
        return Group("Appoggi e montanti terminali", Ui.Stack(f,
            Ui.Text("R è un dato del modello globale. Piatti a contatto con la flangia inferiore e impronta interamente sottostante; piastra di ripartizione e apparecchio d’appoggio restano verifiche distinte. x=0 sul piano dell’anima, z=0 sull’asse dei piatti.", 11, color: Ui.Muted)));
    }
    private UIElement BuildTransverseInputs()
    {
        InputForm? f = null;
        f = Form(Data, CompositeSectionFields.Transverse, _ => ShowFields(f, "trasversale"));
        ShowFields(f, "trasversale");
        return Group("Soletta · armatura trasversale e splitting", Ui.Stack(f,
            Ui.Text("Barre ortogonali alla trave, distinte dalle due file longitudinali della sezione. Materiale e γs sono comuni. Ø=0 esclude la fila. Si controllano superfici a–a, gruppi b–b, puntoni e ancoraggi. I bordi sono fisici: b_eff non definisce il bordo della soletta.", 11, color: Ui.Muted)));
    }
    private UIElement BuildFatigueInputs()
    {
        InputForm? f = null;
        f = Form(Data, CompositeSectionFields.Fatigue, _ => ShowFields(f, "fatica"));
        ShowFields(f, "fatica");
        return Group("Fatica · pioli e flangia collegata", Ui.Stack(f,
            Ui.Text("ΔqE,2 = λv · φfat · (qmax − qmin). Dati della combinazione di fatica dal modello globale, indipendenti dalle fasi. Pioli: categoria 90; flangia tesa: categoria 80 e interazione. Coefficienti da confermare per la classe di affidabilità adottata.", 11, color: Ui.Muted)));
    }
    private UIElement BuildDetailSketch()
    {
        DetailSectionChoice.MaxWidth = 180;
        DetailSectionChoice.SelectionChanged += (_, _) => { Drawing.DetailAtSupport = DetailSectionChoice.SelectedIndex == 1; RefreshDetailSketch(); RefreshDrawing(); ViewChanged(); };
        detailExpander = Group("Prospetto locale · pannelli e appoggio", Ui.Stack(Ui.Bar(Ui.Text("Sezione in vista", 11, true), DetailSectionChoice), DetailSketch), true);
        detailExpander.Margin = new Thickness(10, 0, 10, 4);
        detailExpander.Expanded += (_, _) => { if (viewSettings is not null) viewSettings["prospetto"] = true; };
        detailExpander.Collapsed += (_, _) => { if (viewSettings is not null) viewSettings["prospetto"] = false; };
        DetailSectionChoice.SelectedIndex = (int)viewSettings.D("dettaglio_sezione", 0);
        detailExpander.IsExpanded = viewSettings.B("prospetto", true);
        RefreshDetailSketch(); return detailExpander;
    }
    private void RefreshDetailSketch()
    {
        DetailSketch.Input = Data; DetailSketch.AtSupport = DetailSectionChoice.SelectedIndex == 1; DetailSketch.InvalidateVisual();
        if (detailExpander is not null) detailExpander.Visibility = Anthea.Application.Presentation.BridgeInputPresentation.DetailSketchShown(currentPage, Data) ? Visibility.Visible : Visibility.Collapsed;
        if (viewSettings is not null) viewSettings["dettaglio_sezione"] = DetailSectionChoice.SelectedIndex;
    }
}
