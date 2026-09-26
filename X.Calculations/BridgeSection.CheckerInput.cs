using System.Text.Json.Nodes;
namespace Anthea.Calculations;

public static partial class BridgeSection
{
    public static HBridgeInput ToCheckerInput(JsonObject data)
    {
        ValidateShape(data);
        var d = (JsonObject)data.DeepClone(); EnsureAccessoryDefaults(d);
        var materials = Materials(d);
        return new HBridgeInput
        {
            Materials = new(materials.Concrete, materials.Steel, materials.Rebar),
            Phases = d.Array("fasi").OfType<JsonObject>().Select(ToCheckerPhase).ToArray(),
            Geometry = new HSectionDimensions
            {
                SlabWidth = J.Number(d["b_cls"]) ?? double.NaN,
                SlabHeight = J.Number(d["h_cls"]) ?? double.NaN,
                WebHeight = J.Number(d["h_web"]) ?? double.NaN,
                WebThickness = J.Number(d["t_web"]) ?? double.NaN,
                TopWidth = J.Number(d["b_top"]) ?? double.NaN,
                TopThickness = J.Number(d["t_top"]) ?? double.NaN,
                BottomWidth = J.Number(d["b_bottom"]) ?? double.NaN,
                BottomThickness = J.Number(d["t_bottom"]) ?? double.NaN,
                SecondBottomWidth = J.Number(d["b_bottom2"]) ?? double.NaN,
                SecondBottomThickness = J.Number(d["t_bottom2"]) ?? double.NaN,
                SecondBottomEnabled = d.B("plate2"),
            },
            TopRebars = new BridgeRebarRow
            {
                Enabled = d.B("rebars_top"),
                Diameter = J.Number(d["d_top"]) ?? double.NaN,
                Pitch = J.Number(d["pitch_top"]) ?? double.NaN,
                AxisDistance = J.Number(d["cover_top"]) ?? double.NaN,
            },
            BottomRebars = new BridgeRebarRow
            {
                Enabled = d.B("rebars_bottom"),
                Diameter = J.Number(d["d_bottom"]) ?? double.NaN,
                Pitch = J.Number(d["pitch_bottom"]) ?? double.NaN,
                AxisDistance = J.Number(d["cover_bottom"]) ?? double.NaN,
            },
            Options = new BridgeAnalysisOptions
            {
                GammaM0 = J.Number(d["gamma_m0"]) ?? double.NaN,
                GammaC = J.Number(d["gamma_c"]) ?? double.NaN,
                GammaS = J.Number(d["gamma_s"]) ?? double.NaN,
                AlphaCC = J.Number(d["alpha_cc"]) ?? double.NaN,
                Class4 = d.B("classe4"),
                CommonLoadY = J.Number(d["y_ref"]) ?? double.NaN,
                GammaM1 = J.Number(d["gamma_m1"]) ?? double.NaN,
                GammaM2 = J.Number(d["gamma_m2"]) ?? double.NaN,
                ShearEta = J.Number(d["eta_taglio"]) ?? double.NaN,
                Standard = (BridgeStandard)Array.IndexOf(Standards, d.S("normativa")),
                LimitState = (BridgeLimitState)Array.IndexOf(new[] { "SLU", "SLE rara", "SLE quasi permanente" }, d.S("stato")),
            },
            Intermediate = new BridgeIntermediateStiffener
            {
                Enabled = d.B("irrigidimenti"),
                LeftPanel = J.Number(d["a_irr"]) ?? double.NaN,
                RightPanel = J.Number(d["a_irr_dx"]) ?? double.NaN,
                EqualPanels = d.B("pannelli_uguali"),
                ExternalCompressionKN = J.Number(d["N_irr"]) ?? double.NaN,
                LoadX = J.Number(d["x_irr"]) ?? double.NaN,
                Width = J.Number(d["b_irr"]) ?? double.NaN,
                Thickness = J.Number(d["t_irr"]) ?? double.NaN,
                RightWidth = J.Number(d["b_irr_dx"]) ?? double.NaN,
                RightThickness = J.Number(d["t_irr_dx"]) ?? double.NaN,
                LengthFactor = J.Number(d["betaL_irr"]) ?? double.NaN,
                CheckWelds = d.B("saldature_irr"),
                WeldThroat = J.Number(d["aw_irr"]) ?? double.NaN,
                Layout = (BridgeStiffenerLayout)Array.IndexOf(StiffenerSides, d.S("lati_irr")),
            },
            Support = new BridgeSupportStiffener
            {
                Enabled = d.B("appoggio"),
                Width = J.Number(d["b_app"]) ?? double.NaN,
                Thickness = J.Number(d["t_app"]) ?? double.NaN,
                RightWidth = J.Number(d["b_app_dx"]) ?? double.NaN,
                RightThickness = J.Number(d["t_app_dx"]) ?? double.NaN,
                LengthFactor = J.Number(d["betaL_app"]) ?? double.NaN,
                CheckWelds = d.B("saldature_app"),
                WeldThroat = J.Number(d["aw_app"]) ?? double.NaN,
                LeftPanel = J.Number(d["a_app_sx"]) ?? double.NaN,
                RightPanel = J.Number(d["a_app_dx"]) ?? double.NaN,
                EndDistance = J.Number(d["c_app"]) ?? double.NaN,
                ReactionKN = J.Number(d["R_app"]) ?? double.NaN,
                LoadX = J.Number(d["x_app"]) ?? double.NaN,
                LoadZ = J.Number(d["z_app"]) ?? double.NaN,
                FootprintLength = J.Number(d["s_app"]) ?? double.NaN,
                FootprintWidth = J.Number(d["B_app"]) ?? double.NaN,
                RigidEndPost = d.B("terminale_rigido"),
                EndPostSpacing = J.Number(d["e_term"]) ?? double.NaN,
                Layout = (BridgeStiffenerLayout)Array.IndexOf(StiffenerSides, d.S("lati_app")),
                Location = (BridgeSupportLocation)Array.IndexOf(SupportLocations, d.S("pos_app")),
            },
            Studs = new BridgeStudOptions
            {
                Enabled = d.B("pioli"),
                CountPerRow = J.Number(d["n_pioli"]) ?? double.NaN,
                Diameter = J.Number(d["d_pioli"]) ?? double.NaN,
                Height = J.Number(d["h_pioli"]) ?? double.NaN,
                LongitudinalPitch = J.Number(d["passo_pioli"]) ?? double.NaN,
                TransversePitch = J.Number(d["passo_trasv_pioli"]) ?? double.NaN,
                Fu = J.Number(d["fu_pioli"]) ?? double.NaN,
                GammaV = J.Number(d["gamma_v"]) ?? double.NaN,
                HeadDiameter = J.Number(d["d_testa_pioli"]) ?? double.NaN,
                HeadThickness = J.Number(d["t_testa_pioli"]) ?? double.NaN,
                RepeatedActionDetail = d.B("pioli_fatica"),
                RequiredCover = J.Number(d["copriferro_pioli"]) ?? double.NaN,
            },
            Transverse = new BridgeTransverseReinforcement
            {
                Enabled = d.B("armatura_trasv"),
                TopDiameter = J.Number(d["d_trasv_sup"]) ?? double.NaN,
                TopPitch = J.Number(d["s_trasv_sup"]) ?? double.NaN,
                BottomDiameter = J.Number(d["d_trasv_inf"]) ?? double.NaN,
                BottomPitch = J.Number(d["s_trasv_inf"]) ?? double.NaN,
                CotTheta = J.Number(d["cot_trasv"]) ?? double.NaN,
                LeftFlowFraction = J.Number(d["quota_q_sx"]) ?? double.NaN,
                BendingSteelPerMetre = J.Number(d["As_m_trasv"]) ?? double.NaN,
                AnchorageLength = J.Number(d["l_anc_trasv"]) ?? double.NaN,
                GoodBond = d.B("buona_aderenza_trasv"),
                LeftConcreteEdge = J.Number(d["bordo_cls_sx"]) ?? double.NaN,
                RightConcreteEdge = J.Number(d["bordo_cls_dx"]) ?? double.NaN,
                EdgeUBar = d.B("forcine_bordo"),
                UBarDiameter = J.Number(d["d_forcine"]) ?? double.NaN,
            },
            Fatigue = new BridgeFatigueOptions
            {
                Enabled = d.B("fatica_pioli"),
                MinimumFlow = J.Number(d["q_fat_min"]) ?? double.NaN,
                MaximumFlow = J.Number(d["q_fat_max"]) ?? double.NaN,
                EquivalenceFactor = J.Number(d["lambda_v"]) ?? double.NaN,
                DynamicFactor = J.Number(d["phi_fat"]) ?? double.NaN,
                TensileFlange = d.B("flangia_fat_tesa"),
                FlangeStressRange = J.Number(d["dsigma_fat"]) ?? double.NaN,
                GammaFf = J.Number(d["gamma_ff"]) ?? double.NaN,
                GammaMfStud = J.Number(d["gamma_mf_pioli"]) ?? double.NaN,
                GammaMfFlange = J.Number(d["gamma_mf_flangia"]) ?? double.NaN,
            },
        };
    }
    public static BridgePhase ToCheckerPhase(JsonObject p) => new()
    {
        Name = p.S("nome"), Active = p.B("attiva"),
        Kind = (BridgePhaseKind)Array.IndexOf(PhaseKinds, p.S("tipo")),
        Reference = (BridgeLoadReference)Array.IndexOf(LoadReferences, LoadReference(p)),
        HomogenizationSource = (BridgeHomogenizationSource)Array.IndexOf(HomoModes, p.S("modo")),
        ForceKN = J.Number(p["N"]) ?? double.NaN,
        MomentKNm = J.Number(p["Mx"]) ?? double.NaN,
        ShearKN = p.ContainsKey("V") ? J.Number(p["V"]) ?? double.NaN : 0,
        AdditionalConnectionFlow = p.ContainsKey("q_conn") ? J.Number(p["q_conn"]) ?? double.NaN : 0,
        ShrinkageMicrostrain = J.Number(p["epsilon_cs"]) ?? double.NaN,
        Phi = J.Number(p["phi"]) ?? double.NaN,
        PsiL = J.Number(p["psi"]) ?? double.NaN,
        N = J.Number(p["n"]) ?? double.NaN,
    };
}
