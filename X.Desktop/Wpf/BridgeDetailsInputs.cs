using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;

internal sealed partial class BridgeWorkspace
{
    internal readonly BridgeDetailSketch DetailSketch = new();
    internal readonly ComboBox DetailSectionChoice = Ui.Choice(["Intermedio", "Appoggio"], "Intermedio");
    private Expander detailExpander = null!;
    private UIElement BuildStiffenerInputs()
    {
        InputForm? f = null;
        f = Form(Data, [new("irrigidimenti", "Irrigidimenti intermedi", Bool: true),
            new("lati_irr", "Piatti sulle facce dell’anima", Choices: BridgeSection.StiffenerSides),
            new("b_irr", "Sporgenza sinistra / piatto unico", "mm"), new("t_irr", "Spessore sinistro / piatto unico", "mm"),
            new("b_irr_dx", "Sporgenza destra", "mm"), new("t_irr_dx", "Spessore destro", "mm"),
            new("pannelli_uguali", "Pannelli adiacenti uguali", Bool: true), new("a_irr", "Pannello sinistro aL / passo", "mm"),
            new("a_irr_dx", "Pannello destro aR", "mm"), new("N_irr", "Compressione esterna · inviluppo SLU", "kN"),
            new("x_irr", "Posizione della compressione x", "mm"), new("betaL_irr", "Lcr / distanza fra flange"),
            new("saldature_irr", "Verifica saldature continue", Bool: true), new("aw_irr", "Gola dei cordoni", "mm")], _ => Update());
        void Update()
        {
            if (f is null) return;
            bool on = Data.B("irrigidimenti"), different = Data.S("lati_irr") == BridgeSection.StiffenerSides[3];
            foreach (string key in f.Editors.Keys) if (key != "irrigidimenti") f.ShowField(key, on
                && (key is not ("b_irr_dx" or "t_irr_dx") || different)
                && (key != "a_irr_dx" || !Data.B("pannelli_uguali")) && (key != "aw_irr" || Data.B("saldature_irr")));
        }
        f.Editors["x_irr"].ToolTip = "x=0 nel piano medio dell’anima; positivo verso destra nella sezione. Il baricentro dell’irrigidimento e l’eccentricità x−xG sono calcolati automaticamente.";
        f.Editors["betaL_irr"].ToolTip = "Valore iniziale 1,00. Campo 0,75–2,00; riduzioni richiedono vincoli laterali e rotazionali coerenti. Il modello richiede vincolo laterale a entrambe le flange.";
        Update();
        return Group("Taglio e irrigidimenti intermedi", Ui.Stack(f,
            Ui.Text("Piatti continui senza intagli. La verifica considera eccentricità, II ordine, rigidezza e instabilità. Un irrigidimento non idoneo non aumenta la resistenza a taglio.", 11, color: Ui.Muted)));
    }
    private UIElement BuildSupportInputs()
    {
        InputForm? f = null;
        f = Form(Data, [new("appoggio", "Verifica irrigidimento d’appoggio", Bool: true),
            new("pos_app", "Posizione lungo la trave", Choices: BridgeSection.SupportLocations),
            new("lati_app", "Piatti sulle facce dell’anima", Choices: BridgeSection.StiffenerSides),
            new("b_app", "Sporgenza sinistra / piatto unico", "mm"), new("t_app", "Spessore sinistro / piatto unico", "mm"),
            new("b_app_dx", "Sporgenza destra", "mm"), new("t_app_dx", "Spessore destro", "mm"),
            new("a_app_sx", "Pannello a sinistra dell’appoggio", "mm"), new("a_app_dx", "Pannello a destra dell’appoggio", "mm"),
            new("c_app", "Asse appoggio → estremità trave", "mm"), new("R_app", "Reazione R · inviluppo SLU", "kN"),
            new("x_app", "Posizione reazione x · trasversale", "mm"), new("z_app", "Eccentricità z · longitudinale", "mm"),
            new("s_app", "Impronta · lunghezza lungo trave", "mm"), new("B_app", "Impronta · larghezza trasversale", "mm"),
            new("betaL_app", "Lcr / distanza fra flange"), new("terminale_rigido", "Montante terminale rigido · due coppie", Bool: true),
            new("e_term", "Interasse fra le due coppie e", "mm"), new("saldature_app", "Verifica saldature continue", Bool: true), new("aw_app", "Gola dei cordoni", "mm")], _ => Update());
        void Update()
        {
            if (f is null) return;
            bool on = Data.B("appoggio"), end = Data.S("pos_app") != BridgeSection.SupportLocations[0], different = Data.S("lati_app") == BridgeSection.StiffenerSides[3];
            foreach (string key in f.Editors.Keys) if (key != "appoggio") f.ShowField(key, on
                && (key is not ("b_app_dx" or "t_app_dx") || different)
                && (key is not ("c_app" or "terminale_rigido") || end)
                && (key != "e_term" || end && Data.B("terminale_rigido"))
                && (key != "a_app_sx" || Data.S("pos_app") != BridgeSection.SupportLocations[1])
                && (key != "a_app_dx" || Data.S("pos_app") != BridgeSection.SupportLocations[2])
                && (key != "aw_app" || Data.B("saldature_app")));
        }
        f.Editors["R_app"].ToolTip = "Reazione di progetto positiva in compressione, già combinata. È un inviluppo indipendente: non viene ricavata da V e non si somma alle fasi della sezione.";
        f.Editors["terminale_rigido"].ToolTip = "Due coppie bilaterali simmetriche identiche, distanziate di e verso l’interno. La curva rigida a taglio si attiva soltanto quando i controlli sono soddisfatti.";
        Update();
        return Group("Appoggi e montanti terminali", Ui.Stack(f,
            Ui.Text("R è un dato del modello globale. Piatti a contatto con la flangia inferiore e impronta interamente sottostante; piastra di ripartizione e apparecchio d’appoggio restano verifiche distinte. x=0 sul piano dell’anima, z=0 sull’asse dei piatti.", 11, color: Ui.Muted)));
    }
    private UIElement BuildTransverseInputs()
    {
        InputForm? f = null;
        f = Form(Data, [new("armatura_trasv", "Verifica taglio longitudinale della soletta", Bool: true),
            new("d_trasv_sup", "Barre trasversali superiori · Ø (0 assenti)", "mm"), new("s_trasv_sup", "Passo superiore lungo la trave", "mm"),
            new("d_trasv_inf", "Barre trasversali inferiori · Ø (0 assenti)", "mm"), new("s_trasv_inf", "Passo inferiore lungo la trave", "mm"),
            new("cot_trasv", "Puntone · cot θ (1–1,25)"), new("quota_q_sx", "Quota di q trasferita a sinistra (0–1)"),
            new("As_m_trasv", "As richiesta per flessione trasversale", "mm²/m"), new("l_anc_trasv", "Ancoraggio disponibile oltre le superfici", "mm"),
            new("buona_aderenza_trasv", "Condizioni di buona aderenza", Bool: true),
            new("bordo_cls_sx", "Piolo esterno → bordo reale sinistro", "mm"), new("bordo_cls_dx", "Piolo esterno → bordo reale destro", "mm"),
            new("forcine_bordo", "Forcine a U ancorate attorno ai pioli", Bool: true), new("d_forcine", "Diametro forcine", "mm")], _ => Update());
        void Update()
        {
            if (f is null) return;
            foreach (string key in f.Editors.Keys) if (key != "armatura_trasv") f.ShowField(key, Data.B("armatura_trasv")
                && (key != "s_trasv_sup" || Data.D("d_trasv_sup") > 0) && (key != "s_trasv_inf" || Data.D("d_trasv_inf") > 0)
                && (key != "d_forcine" || Data.B("forcine_bordo")));
        }
        f.Editors["l_anc_trasv"].ToolTip = "Minima lunghezza disponibile delle barre oltre ogni superficie a–a e b–b, su entrambi i lati. Si verifica per fyd, senza riduzioni favorevoli per forma o confinamento.";
        Update();
        return Group("Soletta · armatura trasversale e splitting", Ui.Stack(f,
            Ui.Text("Barre ortogonali alla trave, distinte dalle due file longitudinali della sezione. Materiale e γs sono comuni. Ø=0 esclude la fila. Si controllano superfici a–a, gruppi b–b, puntoni e ancoraggi. I bordi sono fisici: b_eff non definisce il bordo della soletta.", 11, color: Ui.Muted)));
    }
    private UIElement BuildFatigueInputs()
    {
        InputForm? f = null;
        f = Form(Data, [new("fatica_pioli", "Verifica resistente a fatica", Bool: true), new("q_fat_min", "Flusso minimo · combinazione di fatica", "kN/m"),
            new("q_fat_max", "Flusso massimo · combinazione di fatica", "kN/m"), new("lambda_v", "Equivalenza del danno λv"),
            new("phi_fat", "Amplificazione dinamica φfat"), new("flangia_fat_tesa", "Flangia anche in trazione a fatica", Bool: true),
            new("dsigma_fat", "Flangia · intervallo equivalente ΔσE,2", "MPa"), new("gamma_ff", "Azioni di fatica γFf"),
            new("gamma_mf_pioli", "Resistenza pioli γMf,s"), new("gamma_mf_flangia", "Resistenza flangia γMf")], _ => Update());
        void Update()
        {
            if (f is null) return;
            foreach (string key in f.Editors.Keys) if (key != "fatica_pioli") f.ShowField(key, Data.B("fatica_pioli")
                && (key is not ("dsigma_fat" or "gamma_mf_flangia") || Data.B("flangia_fat_tesa")));
        }
        f.Editors["dsigma_fat"].ToolTip = "Intervallo equivalente a 2 milioni di cicli, già comprensivo di λ e amplificazione dinamica. Inviluppare i casi fessurati/non fessurati; non viene ricavato dalle fasi costruttive.";
        Update();
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
        if (detailExpander is not null) detailExpander.Visibility = currentPage == 0 && (Data.B("irrigidimenti") || Data.B("appoggio")) ? Visibility.Visible : Visibility.Collapsed;
        if (viewSettings is not null) viewSettings["dettaglio_sezione"] = DetailSectionChoice.SelectedIndex;
    }
}
