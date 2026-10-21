using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Application.Bridge;
using Anthea.Application.Modules;
using Anthea.Application.Presentation;
using Anthea.Calculations;

/// <summary>
/// Presentation of the composite bridge section sheet moved from the historic view in F5.11 (BridgeInputPresentation,
/// BridgeResultPresentation, BridgeResponsePresentation, BridgeReportPresentation, BridgeSheetPresentation): texts of the status and of
/// the notices, the fields shown by the data, the cells of the table of the actions, the situation shown after a calculation, the results
/// of the default section, the figures of the report, with values written by hand. The identity of the historic view with these
/// functions is proved by ui/check-sezione-mista-historic (traces and drawing lists frozen before the move) and by the F3 reference.
/// </summary>
internal static class BridgeSectionPresentationChecks
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException("Sezione composta: " + message); checks++; }
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("it-IT");
        try
        {
            var data = BridgeSection.Defaults(); BridgeSection.EnsureAccessoryDefaults(data);
            Check(BridgeInputPresentation.StatusUpdated(3) == "Aggiornato · 3 situazioni · geometria e risultati coerenti con i dati correnti"
                && BridgeInputPresentation.StatusInvalid("x") == "Dati da verificare: x", "Testi dello stato");
            Check(BridgeInputPresentation.ResultNotice(true, "", true) == "" && BridgeInputPresentation.ResultNotice(true, "g", false) == "Geometria precedente · g"
                && BridgeInputPresentation.ResultNotice(false, "g", true).StartsWith("DA AGGIORNARE", StringComparison.Ordinal) && BridgeInputPresentation.ResultNotice(false, "g", false) == "",
                "Avviso sopra le pagine");
            Check(BridgeInputPresentation.InputHeading(1) == ("Fasi, omogeneizzazione e limiti", "Carichi incrementali · φ, ψL e n sincronizzati"), "Intestazione della pagina delle fasi");
            Check(BridgeInputPresentation.PhaseCount(data) == "3 fasi inserite · 3 attive · la tabella include sempre tutti gli ingressi", "Numero delle fasi");
            Check(BridgeInputPresentation.LoadConvention(data).StartsWith("SLU: inserire incrementi di progetto", StringComparison.Ordinal), "Convenzione SLU");
            data["stato"] = "SLE rara";
            Check(BridgeInputPresentation.LoadConvention(data).StartsWith("SLE rara: inserire gli incrementi", StringComparison.Ordinal), "Convenzione SLE");
            data["metodo_analisi"] = BridgeSection.CalculationMethods[2];
            Check(BridgeInputPresentation.LoadConvention(data).StartsWith("Non lineare:", StringComparison.Ordinal) && BridgeInputPresentation.MethodNotice(data).Contains("CLS teso trascurato", StringComparison.Ordinal),
                "Convenzione e nota del non lineare");
            Check(BridgeInputPresentation.FieldShown("metodo", "cls_teso_nl", data) && BridgeInputPresentation.CanAddPhase(data), "Campi e fasi dello storico");
            data["metodo_analisi"] = BridgeSection.CalculationMethods[0]; data["stato"] = "SLU";
            Check(!BridgeInputPresentation.FieldShown("metodo", "cls_teso_nl", data) && BridgeInputPresentation.MethodNotice(data).StartsWith("Metodo cumulativo", StringComparison.Ordinal), "Metodo cumulativo");

            // Fields shown by the data.
            Check(!BridgeInputPresentation.FieldShown("materiali", "fy", data) && BridgeInputPresentation.FieldShown("materiali", "acciaio", data), "fy assegnato nascosto");
            Check(!BridgeInputPresentation.FieldShown("sezione", "offset_anima", data) && !BridgeInputPresentation.FieldShown("sezione", "interasse_anime", data), "Campi dell'H");
            data["sezione"] = BridgeSection.SectionTypes[2];
            Check(BridgeInputPresentation.FieldShown("sezione", "offset_anima", data) && BridgeInputPresentation.FieldShown("sezione", "interasse_anime", data)
                && BridgeInputPresentation.IsBox(data) && !BridgeInputPresentation.IsH(data) && BridgeInputPresentation.TorqueColumnShown(data), "Campi del cassoncino");
            Check(!BridgeInputPresentation.FieldShown("cassoncino", "t_controvento", data), "Torsione spenta");
            data["torsione_cassoncino"] = true; data["L_campata"] = "40000"; data["passo_diaframmi"] = "5000"; data["tipo_diaframma"] = BridgeSection.DiaphragmKinds[1];
            Check(BridgeInputPresentation.FieldShown("cassoncino", "A_diagonale", data) && !BridgeInputPresentation.FieldShown("cassoncino", "t_diaframma", data)
                && !BridgeInputPresentation.FieldShown("cassoncino", "e_appoggi", data), "Diaframmi a controvento senza torcente d'appoggio");
            data["T_app"] = "100";
            Check(BridgeInputPresentation.FieldShown("cassoncino", "e_appoggi", data), "Torcente d'appoggio");
            data["pioli"] = true; data["n_pioli"] = "1";
            Check(BridgeInputPresentation.FieldShown("pioli", "d_pioli", data) && !BridgeInputPresentation.FieldShown("pioli", "passo_trasv_pioli", data), "Pioli in una fila");
            data["appoggio"] = true; data["pos_app"] = BridgeSection.SupportLocations[1];
            Check(BridgeInputPresentation.FieldShown("appoggio", "c_app", data) && !BridgeInputPresentation.FieldShown("appoggio", "a_app_sx", data)
                && !BridgeInputPresentation.FieldShown("appoggio", "e_term", data) && BridgeInputPresentation.DetailSketchShown(0, data) && !BridgeInputPresentation.DetailSketchShown(1, data),
                "Appoggio all'estremità sinistra");

            // Phases and cells of the table of the actions.
            var steel = data.Array("fasi")[0]!.AsObject(); var composite = data.Array("fasi")[1]!.AsObject();
            var shrinkage = BridgeInputPresentation.NewPhase(true);
            Check(BridgeInputPresentation.PhaseFieldShown("T", data, composite) && !BridgeInputPresentation.PhaseFieldShown("q_conn", data, steel)
                && BridgeInputPresentation.PhaseFieldShown("epsilon_cs", data, shrinkage) && !BridgeInputPresentation.PhaseFieldShown("Mx", data, shrinkage), "Campi delle fasi");
            Check(BridgeInputPresentation.CellInactive(data, "Solo acciaio", "q_conn") && BridgeInputPresentation.CellInactive(data, "Solo acciaio", "phi")
                && !BridgeInputPresentation.CellInactive(data, "Composta", "T") && BridgeInputPresentation.CellInactive(data, BridgeSection.ShrinkageKind, "N")
                && !BridgeInputPresentation.CellInactive(data, BridgeSection.ShrinkageKind, "epsilon_cs"), "Celle non applicabili");
            Check(BridgeInputPresentation.PhaseHeader(1, composite) == "02  G2 · permanenti portati" && BridgeInputPresentation.PropertyTabHeader(1, composite) == "02 · G2 · permanenti portati",
                "Intestazioni delle fasi");
            var edited = BridgeEditor.Phase();
            BridgeSheetPresentation.CellEdited(edited, "n");
            Check(edited.S("modo") == "Da n", "n assegnato");
            edited["tipo"] = BridgeSection.ShrinkageKind; BridgeSheetPresentation.CellEdited(edited, "tipo");
            Check(edited.S("modo") == "Da φ" && edited.D("psi") == .55, "Ritiro: ψL 0,55 e φ");
            Check(BridgeSheetPresentation.PublishedSituation(true, 0, 3) == 2 && BridgeSheetPresentation.PublishedSituation(false, 1, 3) == 1
                && BridgeSheetPresentation.PublishedSituation(false, 5, 3) == 2 && BridgeSheetPresentation.PublishedSituation(false, -1, 3) == 2, "Situazione mostrata dopo un calcolo");

            // The results of the default section, the figures of its report, the curves.
            var section = BridgeSection.Defaults();
            var result = BridgeResultView.Read(ModuleServices.Get(BridgeSection.Module).Calculate(section));
            Check(BridgeResultPresentation.Situations(result).SequenceEqual(["01 · Dopo G1 · getto e carpenteria", "02 · Dopo G2 · permanenti portati", "03 · Dopo Q · variabili"]), "Situazioni");
            var shown = BridgeResultPresentation.Show(result, 2);
            Check(shown.Overview.StartsWith("|σa|max = ", StringComparison.Ordinal) && shown.Overview.EndsWith("Convergenza: 4 iterazioni", StringComparison.Ordinal), "Panoramica");
            Check(shown.Summaries.Select(s => s.Title).SequenceEqual(["Acciaio · tensioni", "CLS · tensioni", "Armatura · tensioni", "Taglio e irrigidimenti"]) && shown.SummaryText is null, "Schede del riepilogo");
            Check(shown.Stress.Blocks.Single().Columns!.SequenceEqual(["Punto", "y [mm]", "Δσ 1 [MPa]", "Δσ 2 [MPa]", "Δσ 3 [MPa]", "Σσ [MPa]"]) && !shown.Stress.Scroll, "Tabella delle tensioni");
            Check(shown.Studs.Blocks.Count == 4 && shown.Studs.Blocks[0].Text == "Verifica pioli disattivata; flussi riportati per consultazione." && shown.Studs.Blocks[0].Style == BridgeTextStyle.Body
                && shown.Studs.Blocks[1] == BridgeBlock.Paragraph("", BridgeTextStyle.Plain) && shown.Studs.Blocks[2].Kind == BridgeBlockKind.Table,
                "Pioli disattivati: flussi per consultazione");
            Check(shown.Torsion.Blocks.Single().Text == "Solo per il cassoncino." && shown.Torsion.Blocks[0].Style == BridgeTextStyle.Muted && !shown.TorsionShown, "Torsione solo per il cassoncino");
            Check(BridgeResultPresentation.LoadPoints(result.Stages[2]).Select(p => p.Index).SequenceEqual([1, 2, 3]), "Punti di applicazione di N");
            Check(BridgeResultPresentation.Csv(result).StartsWith("Situazione;Punto;Materiale;y [mm];Sigma [MPa];Limite [MPa];Eta;Attivo" + Environment.NewLine + "\"G1 · getto e carpenteria\";", StringComparison.Ordinal),
                "CSV delle tensioni");
            var settings = new JsonObject();
            var figures = BridgeReportPresentation.Figures(result, settings, X.Core.ReportBridge.DefaultSections());
            Check(figures.Select(f => f.Category).SequenceEqual(["geometria", "fase_0", "fase_1", "fase_2"]) && figures.All(f => f.Width == 1200 && f.Height == 640)
                && figures[1].Caption.Contains("CLS amplificato ×6,162 (n · Q · variabili)", StringComparison.Ordinal), "Figure del report");
            Check(BridgeReportPresentation.Figures(result, settings, new HashSet<string> { "tensioni" }).Count == 0, "Figure solo con i grafici");
            Check(BridgeReportPresentation.Style(result, settings) == new BridgeFigureStyle(true, false, false, false, result.StressScale(settings).Factor), "Interruttori dei disegni del report");
            var request = BridgeSection.ResponseDefaults();
            Check(BridgeResponsePresentation.FieldShown("incremento_k", request) && !BridgeResponsePresentation.FieldShown("N", request) && !BridgeResponsePresentation.PhaseShown(request)
                && BridgeResponsePresentation.Phases(section).Count == 3, "Campi delle curve");
        }
        finally { CultureInfo.CurrentCulture = culture; }
        return checks;
    }
}
