using System.Text.Json.Nodes;
using Anthea.Application.Bridge;

namespace Anthea.Application.Presentation;

/// <summary>
/// The inputs of the composite bridge section as the sheet shows them (F5.11): texts of the state and of the notices, titles of the
/// pages and of the cards of the phases, the fields shown or enabled by the data, the cells of the table of the actions that do not
/// apply to a kind of phase. Moved without changes from the historic view (BridgeWorkspace), which calls them.
/// </summary>
public static class BridgeInputPresentation
{
    /// <summary>Phases accepted by the cumulative method.</summary>
    public const int CumulativePhaseLimit = 20;

    /// <summary>Delay of the automatic recalculation after an edit, as the historic view.</summary>
    public static TimeSpan RecalculationDelay { get; } = TimeSpan.FromMilliseconds(500);

    /// <summary>The coefficients that a new standard writes (ApplyStandardDefaults) and that the forms show again.</summary>
    public static IReadOnlyList<string> StandardCoefficients { get; } = ["gamma_m0", "gamma_m1", "alpha_cc"];

    /// <summary>The note under the second bottom plate of the H.</summary>
    public const string PlateNote = "Piastra 2 centrata sotto la piastra 1. Nel calcolo: t_eq = t₁ + t₂; b_eq = (b₁t₁ + b₂t₂) / t_eq. La vista mantiene i due rettangoli reali.";

    /// <summary>A phase can be added: any number with a method with history, up to <see cref="CumulativePhaseLimit"/> with the cumulative one.</summary>
    public static bool CanAddPhase(JsonObject data) => BridgeEditor.IsHistory(data) || data.Array("fasi").Count < CumulativePhaseLimit;

    /// <summary>A new phase: a shrinkage, or a load with N at the gross homogenized centroid.</summary>
    public static JsonObject NewPhase(bool shrinkage) => shrinkage ? BridgeEditor.ShrinkagePhase() : BridgeEditor.Phase(reference: BridgeEditor.GrossLoadReference);

    /// <summary>The local sketch of the panels and of the support is shown on the control panel when a stiffener is checked.</summary>
    public static bool DetailSketchShown(int page, JsonObject data) => page == 0 && (data.B("irrigidimenti") || data.B("appoggio"));

    public const string StatusPreparing = "Preparazione della sezione…";
    public const string StatusPending = "Dati modificati · aggiornamento automatico in attesa…";
    public const string StatusRunning = "Calcolo delle fasi e della sezione efficace…";
    public const string WaitBeforeExport = "Attendere risultati aggiornati prima dell’esportazione.";
    public const string PhaseLimitReached = "Il metodo cumulativo ammette 20 fasi. Selezionare un metodo con storico per aggiungerne altre.";
    public const string StaleNotice = "DA AGGIORNARE · Grafico e verifiche dell’ultimo calcolo valido. I nuovi dati sono in acquisizione o in calcolo; le esportazioni dei risultati attendono l’aggiornamento.";
    public const string ReportUnavailable = "Attendere l’aggiornamento automatico e correggere i dati non validi prima di esportare il report.";

    /// <summary>The state after a published calculation with <paramref name="situations"/> situations.</summary>
    public static string StatusUpdated(int situations) => $"Aggiornato · {situations} situazioni · geometria e risultati coerenti con i dati correnti";

    /// <summary>The state after a calculation that stopped.</summary>
    public static string StatusInvalid(string message) => "Dati da verificare: " + message;

    /// <summary>The notice above the pages: the geometry error on the control panel, the stale results elsewhere; empty when none.</summary>
    public static string ResultNotice(bool geometryPage, string geometryError, bool stale) =>
        geometryPage ? (geometryError == "" ? "" : "Geometria precedente · " + geometryError) : stale ? StaleNotice : "";

    /// <summary>Title and subtitle of the inputs of the page 0 (definition) or 1 (phases).</summary>
    public static (string Title, string Subtitle) InputHeading(int page) => page == 0
        ? ("Definizione della sezione", "Geometria, materiali e armature · dimensioni in mm")
        : ("Fasi, omogeneizzazione e limiti", "Carichi incrementali · φ, ψL e n sincronizzati");

    /// <summary>The count of the phases above the table of the actions.</summary>
    public static string PhaseCount(JsonObject data)
    {
        var phases = data.Array("fasi").OfType<JsonObject>().ToArray();
        return $"{phases.Length} fasi inserite · {phases.Count(p => p.B("attiva"))} attive · la tabella include sempre tutti gli ingressi";
    }

    /// <summary>The convention of the loads above the table of the actions, by limit state and method.</summary>
    public static string LoadConvention(JsonObject data)
    {
        if (BridgeEditor.IsNonlinear(data)) return "Non lineare: carichi incrementali già combinati, legami caratteristici senza γM. SLU/SLE seleziona soltanto le linee di confronto tensionali: non assegna una verifica normativa. φ/n memorizzati: vedere l’opzione di viscosità sopra.";
        return data.S("stato") == "SLU"
            ? "SLU: inserire incrementi di progetto già combinati e coefficientati (γF e ψ). Il modulo non applica altri coefficienti ai carichi; γM0, γc e γs riducono le resistenze."
            : data.S("stato") + ": inserire gli incrementi della corrispondente combinazione di esercizio, già con i relativi ψ. Il selettore cambia i limiti, non genera combinazioni di carico.";
    }

    /// <summary>The notice of the method of calculation.</summary>
    public static string MethodNotice(JsonObject data)
    {
        bool history = BridgeEditor.IsHistory(data), nonlinear = BridgeEditor.IsNonlinear(data);
        return nonlinear
            ? "Non lineare N–Mx sulla sezione lorda, con memoria plastica dell’acciaio e inviluppo CLS Model in compressione; CLS teso " + (data.S("cls_teso_nl", BridgeEditor.ConcreteTensionModes[0]) == BridgeEditor.ConcreteTensionModes[1] ? "con tension stiffening (Collins e Mitchell)" : "trascurato") + ". Legami caratteristici: verifica SLU di classe 4 e controlli accessori da completare. Dettagli in Info modello."
            : history ? "Storico lineare: deformazioni al getto, carichi e ritiri incrementali senza limite numerico di fasi. φ/n modifica i nuovi incrementi. Le fasi già concluse conservano la propria storia. Taglio e connessione da verificare separatamente."
            : "Metodo cumulativo precedente: ricalcola i contributi sulla stessa sezione efficace della situazione. Comprende le verifiche di taglio e connessione. Fino a 20 fasi.";
    }

    /// <summary>The header of the card of the phase <paramref name="index"/> («01  G1 …»).</summary>
    public static string PhaseHeader(int index, JsonObject phase) => $"{index + 1:00}  {phase.S("nome")}";

    /// <summary>The note of a shrinkage phase.</summary>
    public const string ShrinkageNote = "Deformazione uniforme imposta al solo CLS. Inserire l’incremento di ritiro; −250 µε = −0,25‰. Le forze equivalenti sono calcolate e non sono carichi esterni.";

    /// <summary>The note under the homogenization of a phase.</summary>
    public const string HomogenizationNote = "n ↔ φ: modificare uno aggiorna l’altro. ψL resta modificabile e aggiorna n a φ costante.";

    /// <summary>The note of a phase without homogenization (steel only, or slab excluded).</summary>
    public static string NoHomogenizationNote(JsonObject phase) => phase.S("tipo") == "Solo acciaio"
        ? "Calcestruzzo e armature non partecipano." : "Calcestruzzo interamente escluso; carpenteria e armature partecipano con i rispettivi E.";

    /// <summary>The text under the homogenization of a phase: n₀ and φeff.</summary>
    public static string HomogenizationInfo((double N0, double N, double PhiEffective, double Phi) h) =>
        $"n₀ = {BridgePropertyPresentation.Number(h.N0)}   ·   φeff = ψL · φ = {BridgePropertyPresentation.Number(h.PhiEffective)}";

    /// <summary>The header of the tab of the phase <paramref name="index"/> in the properties of the section («01 · G1 …»).</summary>
    public static string PropertyTabHeader(int index, JsonObject phase) => $"{index + 1:00} · {phase.S("nome")}";

    // ---------------------------------------------------------------- fields shown

    /// <summary>The section is a box girder.</summary>
    public static bool IsBox(JsonObject data) => data.S("sezione", BridgeEditor.SectionTypes[0]) == BridgeEditor.SectionTypes[2];

    /// <summary>The section is a welded H (the second bottom plate and its note are shown).</summary>
    public static bool IsH(JsonObject data) => data.S("sezione", BridgeEditor.SectionTypes[0]) == BridgeEditor.SectionTypes[0];

    /// <summary>
    /// The fields shown by the data in the forms of the definition of the section and of the accessories, by key of the form
    /// (the forms of the historic view: «sezione», «piastra», «materiali», «predalle», «armatura-sup», «armatura-inf», «pioli»,
    /// «irrigidimenti», «appoggio», «trasversale», «fatica», «cassoncino», «metodo»). The keys not listed are always shown.
    /// </summary>
    public static bool FieldShown(string form, string key, JsonObject data)
    {
        switch (form)
        {
            case "materiali": return key != "fy" || data.B("fy_override");
            case "predalle":
                return key switch { "h_predalle" => data.B("predalle"), "rif_ferri_inf" => data.B("predalle") && data.B("rebars_bottom"), _ => true };
            case "sezione":
            {
                string kind = data.S("sezione", BridgeEditor.SectionTypes[0]);
                return key switch { "offset_anima" => kind != BridgeEditor.SectionTypes[0], "interasse_anime" => kind == BridgeEditor.SectionTypes[2], _ => true };
            }
            case "piastra": return key is not ("b_bottom2" or "t_bottom2") || data.B("plate2");
            case "armatura-sup": return key == "rebars_top" || data.B("rebars_top");
            case "armatura-inf": return key == "rebars_bottom" || data.B("rebars_bottom");
            case "pioli": return key == "pioli" || data.B("pioli") && (key != "passo_trasv_pioli" || data.D("n_pioli") > 1);
            case "irrigidimenti":
            {
                bool on = data.B("irrigidimenti"), different = data.S("lati_irr") == BridgeEditor.StiffenerSides[3];
                return key == "irrigidimenti" || on && (key is not ("b_irr_dx" or "t_irr_dx") || different)
                    && (key != "a_irr_dx" || !data.B("pannelli_uguali")) && (key != "aw_irr" || data.B("saldature_irr"));
            }
            case "appoggio":
            {
                bool on = data.B("appoggio"), end = data.S("pos_app") != BridgeEditor.SupportLocations[0], different = data.S("lati_app") == BridgeEditor.StiffenerSides[3];
                return key == "appoggio" || on
                    && (key is not ("b_app_dx" or "t_app_dx") || different)
                    && (key is not ("c_app" or "terminale_rigido") || end)
                    && (key != "e_term" || end && data.B("terminale_rigido"))
                    && (key != "a_app_sx" || data.S("pos_app") != BridgeEditor.SupportLocations[1])
                    && (key != "a_app_dx" || data.S("pos_app") != BridgeEditor.SupportLocations[2])
                    && (key != "aw_app" || data.B("saldature_app"));
            }
            case "trasversale":
                return key == "armatura_trasv" || data.B("armatura_trasv")
                    && (key != "s_trasv_sup" || data.D("d_trasv_sup") > 0) && (key != "s_trasv_inf" || data.D("d_trasv_inf") > 0)
                    && (key != "d_forcine" || data.B("forcine_bordo"));
            case "fatica": return key == "fatica_pioli" || data.B("fatica_pioli") && (key is not ("dsigma_fat" or "gamma_mf_flangia") || data.B("flangia_fat_tesa"));
            case "cassoncino":
            {
                if (key == "torsione_cassoncino") return true;
                bool on = data.B("torsione_cassoncino"), span = on && data.D("L_campata") > 0, diaphragms = span && data.D("passo_diaframmi") > 0;
                bool plate = data.S("tipo_diaframma") == BridgeEditor.DiaphragmKinds[0], support = on && J.Number(data["T_app"]) is double t && t != 0;
                return key switch
                {
                    "t_controvento" or "L_campata" or "T_app" => on,
                    "m_t_dist" or "T_c_dist" or "passo_diaframmi" => span,
                    "tipo_diaframma" => diaphragms,
                    "t_diaframma" => diaphragms && plate,
                    "A_diagonale" or "i_diagonale" or "beta_diagonale" => diaphragms && !plate,
                    _ => support
                };
            }
            case "metodo": return key is not ("viscosita_nl" or "cls_teso_nl") || BridgeEditor.IsNonlinear(data);
            default: return true;
        }
    }

    /// <summary>The options of the local stability, enabled only outside the non-linear method (dimmed otherwise).</summary>
    public static IReadOnlyList<string> StabilityKeys { get; } = ["classe4", "instabilita_sup", "instabilita_inf", "instabilita_anima"];

    /// <summary>The common level of N is enabled when an active phase other than a shrinkage uses it.</summary>
    public static bool CommonReferenceUsed(JsonObject data) => data.Array("fasi").OfType<JsonObject>()
        .Any(p => p.B("attiva") && p.S("tipo") != BridgeEditor.ShrinkageKind && BridgeEditor.LoadReference(p) == BridgeEditor.CommonLoadReference);

    /// <summary>The fields of the form of a phase shown by its kind and by the data.</summary>
    public static bool PhaseFieldShown(string key, JsonObject data, JsonObject phase)
    {
        bool shrinkage = phase.S("tipo") == BridgeEditor.ShrinkageKind;
        return key switch
        {
            "N" or "Mx" or "V" or "riferimento_N" => !shrinkage,
            "T" => !shrinkage && BridgeEditor.TorsionEnabled(data),
            "epsilon_cs" => shrinkage,
            "q_conn" => data.B("pioli") && phase.S("tipo") != "Solo acciaio",
            _ => true
        };
    }

    // ---------------------------------------------------------------- table of the actions

    /// <summary>What the cells of the table of the actions depend on: standard, studs, box girder, torsion (the cells are configured again when it changes).</summary>
    public static string CellStyleKey(JsonObject data) => data.S("normativa") + data.B("pioli") + IsBox(data) + BridgeEditor.TorsionEnabled(data);

    /// <summary>The column of the torque ΔT is shown only for the box girder.</summary>
    public static bool TorqueColumnShown(JsonObject data) => IsBox(data);

    /// <summary>
    /// True when the cell <paramref name="key"/> does not apply to a phase of kind <paramref name="kind"/>: disabled and faded in the table,
    /// never edited (the shrinkage has no loads, the torque needs the torsion of the box, the shrinkage strain is only for the shrinkage,
    /// the additional flow of the studs is not for the steel only, the homogenization only where the phase homogenizes the concrete).
    /// </summary>
    public static bool CellInactive(JsonObject data, string kind, string key) =>
        kind == BridgeEditor.ShrinkageKind && key is "N" or "Mx" or "V" or "T" or "riferimento_N"
        || key == "T" && !BridgeEditor.TorsionEnabled(data)
        || kind != BridgeEditor.ShrinkageKind && key == "epsilon_cs"
        || kind == "Solo acciaio" && key == "q_conn"
        || !BridgeSheetPresentation.PhaseHomogenizationNeeded(data, kind) && key is "phi" or "psi" or "n";

    /// <summary>The tooltip of a cell that does not apply.</summary>
    public const string InactiveCellToolTip = "Dato non applicabile a questo tipo di fase; non utilizzato nel calcolo.";
}
