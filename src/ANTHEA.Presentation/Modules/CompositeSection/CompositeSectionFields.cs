using Anthea.Application.Bridge;
using Anthea.Presentation.Forms;

namespace Anthea.Presentation.Modules.CompositeSection;

/// <summary>
/// The fields of the composite bridge section sheet (F5.11): the only definition of labels, units, items and columns, read by the historic
/// view BridgeWorkspace and by the ViewModel of the sheet, in the order of the view. The forms are compact with wide choices, as the
/// historic InputForm of the sheet (<c>Form(…, true, true)</c>); the slab properties are compact; the curves compact with wide choices.
/// </summary>
public static class CompositeSectionFields
{
    /// <summary>The displays of the drawing: total stresses, contributions of the phases, geometry.</summary>
    public static IReadOnlyList<string> Displays { get; } = ["Tensioni totali", "Contributi delle fasi", "Geometria"];

    /// <summary>The section of the local sketch: an intermediate stiffener or the support.</summary>
    public static IReadOnlyList<string> DetailSections { get; } = ["Intermedio", "Appoggio"];

    /// <summary>«Normativa»: the name of the section and the standard.</summary>
    public static IReadOnlyList<FieldSpec> Standard { get; } = [new("nome", "Nome della sezione", Wide: true), new("normativa", "Normativa", Choices: BridgeEditor.Standards)];

    /// <summary>«Coefficienti da normativa».</summary>
    public static IReadOnlyList<FieldSpec> Coefficients { get; } = [new("gamma_m0", "Acciaio strutturale γM0"), new("gamma_m1", "Instabilità γM1"), new("eta_taglio", "Taglio η"),
        new("gamma_v", "Pioli γV"), new("gamma_m2", "Saldature γM2"), new("gamma_c", "Calcestruzzo γc"), new("alpha_cc", "Calcestruzzo αcc"), new("gamma_s", "Armature γs")];

    /// <summary>«Materiali».</summary>
    public static IReadOnlyList<FieldSpec> Materials { get; } = [new("classe_cls", "Calcestruzzo", Choices: BridgeEditor.ConcreteNames), new("acciaio", "Carpenteria", Choices: BridgeEditor.SteelNames),
        new("armatura", "Armatura ordinaria", Choices: BridgeEditor.RebarNames), new("fy_override", "Sovrascrivi fy per tutta la carpenteria", Bool: true), new("fy", "fy assegnato", "MPa")];

    /// <summary>«Soletta».</summary>
    public static IReadOnlyList<FieldSpec> Slab { get; } = [new("b_cls", "Larghezza collaborante", "mm", Symbol: "b_eff"), new("h_cls", "Spessore totale soletta", "mm")];

    /// <summary>«Predalle · geometria».</summary>
    public static IReadOnlyList<FieldSpec> Predalle { get; } = [new("predalle", "Predalle presente · solo geometria", Bool: true),
        new("h_predalle", "Spessore predalle", "mm"), new("rif_ferri_inf", "Riferimento ferri inferiori", Choices: BridgeEditor.BottomRebarReferences)];

    /// <summary>«Carpenteria saldata».</summary>
    public static IReadOnlyList<FieldSpec> Steel { get; } = [new("h_trave", "Altezza totale H trave", "mm"), new("t_web", "Spessore anima", "mm"),
        new("b_top", "Larghezza superiore", "mm"), new("t_top", "Spessore superiore", "mm"), new("b_bottom", "Larghezza inferiore 1", "mm"), new("t_bottom", "Spessore inferiore 1", "mm")];

    /// <summary>«Tipo di sezione».</summary>
    public static IReadOnlyList<FieldSpec> SectionType { get; } = [new("sezione", "Tipo di sezione", Choices: BridgeEditor.SectionTypes), new("offset_anima", "Scostamento anima al piede", "mm"),
        new("interasse_anime", "Interasse anime in sommità", "mm")];

    /// <summary>The second bottom plate of the H.</summary>
    public static IReadOnlyList<FieldSpec> Plate { get; } = [new("plate2", "Seconda piattabanda inferiore", Bool: true), new("b_bottom2", "Larghezza inferiore 2", "mm"), new("t_bottom2", "Spessore inferiore 2", "mm")];

    /// <summary>A row of longitudinal bars, «top» or «bottom».</summary>
    public static IReadOnlyList<FieldSpec> Bars(string side) => [new("rebars_" + side, "Fila presente", Bool: true), new("d_" + side, "Diametro barre", "mm"), new("pitch_" + side, "Passo", "mm"),
        new("cover_" + side, side == "top" ? "Estradosso → asse barra" : "Riferimento → asse barra", "mm")];

    /// <summary>«Taglio e irrigidimenti intermedi».</summary>
    public static IReadOnlyList<FieldSpec> Stiffeners { get; } = [new("irrigidimenti", "Irrigidimenti intermedi", Bool: true),
        new("lati_irr", "Piatti sulle facce dell’anima", Choices: BridgeEditor.StiffenerSides),
        new("b_irr", "Sporgenza sinistra / piatto unico", "mm"), new("t_irr", "Spessore sinistro / piatto unico", "mm"),
        new("b_irr_dx", "Sporgenza destra", "mm"), new("t_irr_dx", "Spessore destro", "mm"),
        new("pannelli_uguali", "Pannelli adiacenti uguali", Bool: true), new("a_irr", "Pannello sinistro aL / passo", "mm"),
        new("a_irr_dx", "Pannello destro aR", "mm"), new("N_irr", "Compressione esterna · inviluppo SLU", "kN"),
        new("x_irr", "Posizione della compressione x", "mm"), new("betaL_irr", "Lcr / distanza fra flange"),
        new("saldature_irr", "Verifica saldature continue", Bool: true), new("aw_irr", "Gola dei cordoni", "mm")];

    /// <summary>«Appoggi e montanti terminali».</summary>
    public static IReadOnlyList<FieldSpec> Support { get; } = [new("appoggio", "Verifica irrigidimento d’appoggio", Bool: true),
        new("pos_app", "Posizione lungo la trave", Choices: BridgeEditor.SupportLocations),
        new("lati_app", "Piatti sulle facce dell’anima", Choices: BridgeEditor.StiffenerSides),
        new("b_app", "Sporgenza sinistra / piatto unico", "mm"), new("t_app", "Spessore sinistro / piatto unico", "mm"),
        new("b_app_dx", "Sporgenza destra", "mm"), new("t_app_dx", "Spessore destro", "mm"),
        new("a_app_sx", "Pannello a sinistra dell’appoggio", "mm"), new("a_app_dx", "Pannello a destra dell’appoggio", "mm"),
        new("c_app", "Asse appoggio → estremità trave", "mm"), new("R_app", "Reazione R · inviluppo SLU", "kN"),
        new("x_app", "Posizione reazione x · trasversale", "mm"), new("z_app", "Eccentricità z · longitudinale", "mm"),
        new("s_app", "Impronta · lunghezza lungo trave", "mm"), new("B_app", "Impronta · larghezza trasversale", "mm"),
        new("betaL_app", "Lcr / distanza fra flange"), new("terminale_rigido", "Montante terminale rigido · due coppie", Bool: true),
        new("e_term", "Interasse fra le due coppie e", "mm"), new("saldature_app", "Verifica saldature continue", Bool: true), new("aw_app", "Gola dei cordoni", "mm")];

    /// <summary>«Soletta · armatura trasversale e splitting».</summary>
    public static IReadOnlyList<FieldSpec> Transverse { get; } = [new("armatura_trasv", "Verifica taglio longitudinale della soletta", Bool: true),
        new("d_trasv_sup", "Barre trasversali superiori · Ø (0 assenti)", "mm"), new("s_trasv_sup", "Passo superiore lungo la trave", "mm"),
        new("d_trasv_inf", "Barre trasversali inferiori · Ø (0 assenti)", "mm"), new("s_trasv_inf", "Passo inferiore lungo la trave", "mm"),
        new("cot_trasv", "Puntone · cot θ (1–1,25)"), new("quota_q_sx", "Quota di q trasferita a sinistra (0–1)"),
        new("As_m_trasv", "As richiesta per flessione trasversale", "mm²/m"), new("l_anc_trasv", "Ancoraggio disponibile oltre le superfici", "mm"),
        new("buona_aderenza_trasv", "Condizioni di buona aderenza", Bool: true),
        new("bordo_cls_sx", "Piolo esterno → bordo reale sinistro", "mm"), new("bordo_cls_dx", "Piolo esterno → bordo reale destro", "mm"),
        new("forcine_bordo", "Forcine a U ancorate attorno ai pioli", Bool: true), new("d_forcine", "Diametro forcine", "mm")];

    /// <summary>«Fatica · pioli e flangia collegata».</summary>
    public static IReadOnlyList<FieldSpec> Fatigue { get; } = [new("fatica_pioli", "Verifica resistente a fatica", Bool: true), new("q_fat_min", "Flusso minimo · combinazione di fatica", "kN/m"),
        new("q_fat_max", "Flusso massimo · combinazione di fatica", "kN/m"), new("lambda_v", "Equivalenza del danno λv"),
        new("phi_fat", "Amplificazione dinamica φfat"), new("flangia_fat_tesa", "Flangia anche in trazione a fatica", Bool: true),
        new("dsigma_fat", "Flangia · intervallo equivalente ΔσE,2", "MPa"), new("gamma_ff", "Azioni di fatica γFf"),
        new("gamma_mf_pioli", "Resistenza pioli γMf,s"), new("gamma_mf_flangia", "Resistenza flangia γMf")];

    /// <summary>«Connessione a pioli».</summary>
    public static IReadOnlyList<FieldSpec> Studs { get; } = [new("pioli", "Verifica pioli a testa saldata", Bool: true), new("n_pioli", "Pioli per fila trasversale"),
        new("d_pioli", "Diametro gambo d", "mm"), new("h_pioli", "Altezza dopo saldatura", "mm"),
        new("passo_pioli", "Passo longitudinale", "mm"), new("passo_trasv_pioli", "Passo trasversale", "mm"),
        new("fu_pioli", "Resistenza ultima fu", "MPa"), new("d_testa_pioli", "Diametro testa", "mm"), new("t_testa_pioli", "Spessore testa", "mm"),
        new("copriferro_pioli", "Copriferro minimo di progetto", "mm"), new("pioli_fatica", "Dettagli per azioni ripetute / fatica", Bool: true)];

    /// <summary>«Cassoncino · torsione, distorsione e diaframmi».</summary>
    public static IReadOnlyList<FieldSpec> Box { get; } = [new("torsione_cassoncino", "Verifiche a torsione del cassoncino", Bool: true),
        new("t_controvento", "Controvento superiore · spessore equivalente t*", "mm"),
        new("L_campata", "Distorsione · luce della campata (0 = non analizzata)", "mm"),
        new("m_t_dist", "Distorsione · torcente distribuito m_t", "kNm/m"), new("T_c_dist", "Distorsione · torcente concentrato T_c", "kNm"),
        new("passo_diaframmi", "Diaframmi intermedi · passo (0 = assenti)", "mm"), new("tipo_diaframma", "Diaframmi intermedi · tipo", Choices: BridgeEditor.DiaphragmKinds),
        new("t_diaframma", "Piastra · spessore", "mm"), new("A_diagonale", "Controvento a X · area di una diagonale", "mm²"),
        new("i_diagonale", "Controvento a X · raggio d'inerzia minimo", "mm"), new("beta_diagonale", "Controvento a X · Lcr / L diagonale"),
        new("T_app", "Appoggio · torcente trasferito agli apparecchi", "kNm"), new("e_appoggi", "Appoggio · interasse trasversale apparecchi", "mm"),
        new("t_diaframma_app", "Diaframma d'appoggio · spessore (0 = non verificato)", "mm")];

    /// <summary>The method of calculation.</summary>
    public static IReadOnlyList<FieldSpec> Method { get; } = [new("metodo_analisi", "Metodo di calcolo", Choices: BridgeEditor.CalculationMethods, Wide: true),
        new("viscosita_nl", "Viscosità nel non lineare", Choices: BridgeEditor.NonlinearCreepModes, Wide: true),
        new("cls_teso_nl", "CLS teso (non lineare)", Choices: BridgeEditor.ConcreteTensionModes, Wide: true)];

    /// <summary>«Discretizzazione e sottopassi» of the methods with history.</summary>
    public static IReadOnlyList<FieldSpec> Mesh { get; } = [new("fibre_anima", "Strisce anima"), new("fibre_flange", "Strisce per piattabanda"),
        new("fibre_cls", "Strisce soletta"), new("sottopassi", "Sottopassi per fase")];

    /// <summary>The limits and the local stability.</summary>
    public static IReadOnlyList<FieldSpec> Limits { get; } = [new("stato", "Limiti tensionali", Choices: ["SLU", "SLE rara", "SLE quasi permanente"]),
        new("classe4", "Riduzioni locali · classe 4", Bool: true),
        new("instabilita_sup", "Instabilità locale · piattabanda superiore", Bool: true),
        new("instabilita_inf", "Instabilità locale · piattabanda inferiore", Bool: true),
        new("instabilita_anima", "Instabilità locale · anima", Bool: true), new("y_ref", "Quota comune di N", "mm")];

    /// <summary>The form of a phase.</summary>
    public static IReadOnlyList<FieldSpec> Phase { get; } = [new("nome", "Nome", Wide: true), new("attiva", "Includi nella somma", Bool: true), new("tipo", "Sezione reagente", Choices: BridgeEditor.PhaseKinds),
        new("N", "Forza assiale N", "kN"), new("Mx", "Momento Mx", "kNm"), new("V", "Taglio V", "kN"), new("T", "Momento torcente T · cassoncino", "kNm"),
        new("q_conn", "Scorrimento aggiuntivo Δq", "kN/m"), new("epsilon_cs", "Ritiro Δεcs · negativo = accorciamento", "µε"),
        new("riferimento_N", "Punto di applicazione N e riferimento Mx", Choices: BridgeEditor.LoadReferences)];

    /// <summary>The homogenization of a phase.</summary>
    public static IReadOnlyList<FieldSpec> Homogenization { get; } = [new("phi", "Viscosità φ"), new("psi", "Moltiplicatore ψL"), new("n", "Fattore di calcolo n")];

    /// <summary>The slab properties of the side panel.</summary>
    public static IReadOnlyList<FieldSpec> SlabProperties { get; } = [new("phi", "Viscosità φ"), new("psi", "Moltiplicatore ψL"), new("n", "n = Ea / Ec,eff")];

    /// <summary>The path of the curves of the section.</summary>
    public static IReadOnlyList<FieldSpec> Curve { get; } = [new("tipo", "Curva", Choices: BridgeEditor.ResponseModes, Wide: true),
        new("origine", "Stato di partenza", Choices: BridgeEditor.ResponseOrigins, Wide: true),
        new("y", "Quota di riferimento", "mm"), new("n_storico", "Mantieni N dello stato iniziale", Bool: true, Wide: true), new("N", "N costante", "kN"),
        new("k_storico", "Mantieni κ dello stato iniziale", Bool: true, Wide: true), new("k", "κ costante", "1/m"),
        new("incremento_k", "Incremento finale Δκ", "1/m"), new("incremento_e", "Incremento finale Δε", "µε"),
        new("punti", "Punti della curva"), new("sottopassi", "Sottopassi per punto")];

    /// <summary>The fields of the curves shown without the presentation of the numbers (κ in 1/m, small values).</summary>
    public static IReadOnlyList<string> CurveRawKeys { get; } = ["k", "incremento_k"];

    /// <summary>The columns of the table of the actions, in the order and with the widths of the view.</summary>
    public static IReadOnlyList<FieldSpec> Actions { get; } = [
        new("attiva", "Attiva", Bool: true), new("nome", "Fase"), new("tipo", "Sezione / azione", Choices: BridgeEditor.PhaseKinds),
        new("N", "ΔN [kN]"), new("Mx", "ΔMx [kNm]"), new("V", "ΔV [kN]"), new("T", "ΔT [kNm]"),
        new("riferimento_N", "Punto N / riferimento Mx", Choices: BridgeEditor.LoadReferences),
        new("phi", "φ"), new("psi", "ψL"), new("n", "n = Ea/Ec,eff"), new("epsilon_cs", "Δεcs [µε]"), new("q_conn", "Δq pioli [kN/m]")];

    /// <summary>The widths of the columns of <see cref="Actions"/>.</summary>
    public static IReadOnlyList<double> ActionWidths { get; } = [60, 180, 130, 90, 100, 90, 95, 220, 70, 70, 110, 105, 120];

    /// <summary>The tooltips that the sheet gives to some editors, in place of the help of the key.</summary>
    public static IReadOnlyDictionary<string, string> EditorToolTips { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["fy_override"] = FyToolTip, ["fy"] = FyToolTip,
        ["h_cls"] = "Spessore complessivo, predalle inclusa. La predalle non aumenta questa dimensione.",
        ["h_trave"] = "Altezza esterna totale della trave d'acciaio H, fra le facce esterne delle piattabande. Il programma ricava l'altezza libera sottraendo gli spessori delle piattabande.",
        ["predalle"] = BridgeEditor.PredalleScope,
        ["h_predalle"] = "Quota dell'estradosso predalle rispetto a y=0. Compresa nello spessore totale della soletta, non aggiunta.",
        ["rif_ferri_inf"] = "Estradosso predalle: y dei ferri = spessore predalle + distanza all'asse. Intradosso soletta: y = distanza all'asse. Cambiare il riferimento conserva la distanza inserita e sposta la fila.",
        ["cover_top"] = "Distanza dall'estradosso della soletta all'asse dei ferri superiori, diretta verso il basso.",
        ["cover_bottom"] = "Distanza verso l'alto all'asse dei ferri inferiori. Con predalle attiva il riferimento si sceglie in Geometria → Predalle; altrimenti è l'intradosso soletta. Non è il copriferro netto.",
        ["x_irr"] = "x=0 nel piano medio dell’anima; positivo verso destra nella sezione. Il baricentro dell’irrigidimento e l’eccentricità x−xG sono calcolati automaticamente.",
        ["betaL_irr"] = "Valore iniziale 1,00. Campo 0,75–2,00; riduzioni richiedono vincoli laterali e rotazionali coerenti. Il modello richiede vincolo laterale a entrambe le flange.",
        ["R_app"] = "Reazione di progetto positiva in compressione, già combinata. È un inviluppo indipendente: non viene ricavata da V e non si somma alle fasi della sezione.",
        ["terminale_rigido"] = "Due coppie bilaterali simmetriche identiche, distanziate di e verso l’interno. La curva rigida a taglio si attiva soltanto quando i controlli sono soddisfatti.",
        ["l_anc_trasv"] = "Minima lunghezza disponibile delle barre oltre ogni superficie a–a e b–b, su entrambi i lati. Si verifica per fyd, senza riduzioni favorevoli per forma o confinamento.",
        ["dsigma_fat"] = "Intervallo equivalente a 2 milioni di cicli, già comprensivo di λ e amplificazione dinamica. Inviluppare i casi fessurati/non fessurati; non viene ricavato dalle fasi costruttive.",
        ["t_controvento"] = "Spessore della lamiera equivalente al controvento orizzontale superiore del cassone in acciaio (Kollbrunner–Basler), per le fasi di solo acciaio. 0: cassone aperto, torsione delle fasi di solo acciaio non verificata.",
        ["m_t_dist"] = "Torcente dei carichi eccentrici su tutta la luce, già combinato. Con T_c, dello stesso segno, è applicato come coppia verticale alla sommità delle anime per la distorsione.",
        ["T_c_dist"] = "Torcente concentrato dei carichi eccentrici (ad esempio il tandem), nella posizione più sfavorevole lungo la luce.",
        ["T_app"] = "Momento torcente trasferito dal diaframma d'appoggio agli apparecchi, inviluppo indipendente dalle fasi come la reazione R. Coppia T/e_b sugli apparecchi.",
    };

    /// <summary>The tooltip of ΔT in the form of a phase.</summary>
    public const string TorqueToolTip = "Incremento del momento torcente della fase nella sezione, dal modello globale. Fasi di solo acciaio: cella chiusa dal controvento superiore (t* > 0); fasi composte: cella chiusa dalla soletta.";

    private const string FyToolTip = "Sostituisce il valore di catalogo con un unico fy per anima e piattabande. Inserire la tensione di snervamento in MPa, prima di γM0. Non modifica le armature e non applica correzioni automatiche in funzione dello spessore.";
}
