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
        f = Form(Data, [new("torsione_cassoncino", "Verifiche a torsione del cassoncino", Bool: true),
            new("t_controvento", "Controvento superiore · spessore equivalente t*", "mm"),
            new("L_campata", "Distorsione · luce della campata (0 = non analizzata)", "mm"),
            new("m_t_dist", "Distorsione · torcente distribuito m_t", "kNm/m"), new("T_c_dist", "Distorsione · torcente concentrato T_c", "kNm"),
            new("passo_diaframmi", "Diaframmi intermedi · passo (0 = assenti)", "mm"), new("tipo_diaframma", "Diaframmi intermedi · tipo", Choices: BridgeSection.DiaphragmKinds),
            new("t_diaframma", "Piastra · spessore", "mm"), new("A_diagonale", "Controvento a X · area di una diagonale", "mm²"),
            new("i_diagonale", "Controvento a X · raggio d'inerzia minimo", "mm"), new("beta_diagonale", "Controvento a X · Lcr / L diagonale"),
            new("T_app", "Appoggio · torcente trasferito agli apparecchi", "kNm"), new("e_appoggi", "Appoggio · interasse trasversale apparecchi", "mm"),
            new("t_diaframma_app", "Diaframma d'appoggio · spessore (0 = non verificato)", "mm")], key =>
            {
                Update();
                if (key == "torsione_cassoncino") Dispatcher.BeginInvoke(BuildPhases);
            });
        void Update()
        {
            if (f is null) return;
            bool on = Data.B("torsione_cassoncino"), span = on && Data.D("L_campata") > 0, diaphragms = span && Data.D("passo_diaframmi") > 0;
            bool plate = Data.S("tipo_diaframma") == BridgeSection.DiaphragmKinds[0], support = on && J.Number(Data["T_app"]) is double t && t != 0;
            foreach (string key in f.Editors.Keys) if (key != "torsione_cassoncino") f.ShowField(key, key switch
            {
                "t_controvento" or "L_campata" or "T_app" => on,
                "m_t_dist" or "T_c_dist" or "passo_diaframmi" => span,
                "tipo_diaframma" => diaphragms,
                "t_diaframma" => diaphragms && plate,
                "A_diagonale" or "i_diagonale" or "beta_diagonale" => diaphragms && !plate,
                _ => support
            });
        }
        f.Editors["t_controvento"].ToolTip = "Spessore della lamiera equivalente al controvento orizzontale superiore del cassone in acciaio (Kollbrunner–Basler), per le fasi di solo acciaio. 0: cassone aperto, torsione delle fasi di solo acciaio non verificata.";
        f.Editors["m_t_dist"].ToolTip = "Torcente dei carichi eccentrici su tutta la luce, già combinato. Con T_c, dello stesso segno, è applicato come coppia verticale alla sommità delle anime per la distorsione.";
        f.Editors["T_c_dist"].ToolTip = "Torcente concentrato dei carichi eccentrici (ad esempio il tandem), nella posizione più sfavorevole lungo la luce.";
        f.Editors["T_app"].ToolTip = "Momento torcente trasferito dal diaframma d'appoggio agli apparecchi, inviluppo indipendente dalle fasi come la reazione R. Coppia T/e_b sugli apparecchi.";
        Update();
        var group = Group("Cassoncino · torsione, distorsione e diaframmi", Ui.Stack(f,
            Ui.Text("Torsione: cella chiusa di Bredt, q = T/(2A0), con la soletta (fasi composte) o il controvento (fasi di solo acciaio). q si somma al taglio delle anime, al fondo, ai pioli e alla soletta. " +
                "Distorsione: trave su suolo elastico sulla campata appoggiata, diaframmi intermedi come molle; σdw oltre il 10% della flessione entra nelle verifiche del fondo (EN 1993-2 §6.2.7). " +
                "Diaframmi a piastra (taglio e imbozzamento) o controventi a X (diagonale compressa); diaframma d'appoggio a taglio e coppia degli apparecchi sugli irrigidimenti d'appoggio.", 11, color: Ui.Muted)));
        group.Visibility = Data.S("sezione", BridgeSection.SectionTypes[0]) == BridgeSection.SectionTypes[2] ? Visibility.Visible : Visibility.Collapsed;
        return group;
    }

    private void ShowTorsionResults(BridgeStage stage)
    {
        bool box = DisplayedCalculation?.Geometry.SectionType == BridgeSteelSectionType.Box;
        torsionGroup.Visibility = box ? Visibility.Visible : Visibility.Collapsed;
        if (stage.Torsion is not { } t)
        {
            torsionResults.Content = Ui.Text(box ? "Verifiche a torsione del cassoncino non attive: attivarle negli ingressi del cassoncino." : "Solo per il cassoncino.", 12, color: Ui.Muted);
            return;
        }
        var body = Ui.Stack(Ui.Text($"q anime e fondo = {F(t.WebFlow)} kN/m · q soletta e connessione = {F(t.SlabFlow)} kN/m · q controvento = {F(t.BracingFlow)} kN/m", 12),
            ResultTable(["Fase", "Sezione", "ΔT [kNm]", "A0 [m²]", "J acciaio [m⁴]", "q [kN/m]", "Cella"], t.Flows.Select(x => new[] {
                x.Phase, x.Kind, F(x.TorqueKNm), F(x.CellArea / 1e6), E(x.TorsionConstant / 1e12), F(x.Flow), x.Closed ? "Chiusa" : "Aperta · non verificata" })));
        if (t.Distortion is { } dw)
            body.Children.Add(Ui.Text($"Distorsione: σdw fondo = {F(dw.WarpingStressBottom)} MPa ({F(dw.BendingRatio * 100)}% della flessione, " +
                (dw.Included ? "sommata al fondo" : "trascurata nel fondo") + $") · ψ max = {E(dw.Amplitude)} rad · diaframmi intermedi {dw.Diaphragms}", 12));
        body.Children.Add(ResultTable(["Parametro", "Valore", "Unità"], t.Details.Select(x => new[] { x.Name, F(x.Value), x.Unit })));
        torsionResults.Content = body;
        summaryCards.AddCheck("Cassoncino · torsione e distorsione", t.Checks.Count, t.Checks.Select(c => (c.Name, c.Ratio, c.Ratio is { } r ? (bool?)(r <= 1) : null)));
    }
}
