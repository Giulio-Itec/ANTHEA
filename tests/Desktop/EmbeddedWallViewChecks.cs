using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Anthea.Application.EmbeddedWalls;
using Anthea.Presentation.Modules.EmbeddedWalls;
using X.Desktop.Views.EmbeddedWalls;

namespace X.Desktop;

internal static class EmbeddedWallViewChecks
{
    internal static async Task Run(string root, string output)
    {
        Directory.CreateDirectory(output);
        int checks = 0;
        void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); checks++; }
        var data = EmbeddedWallDocument.Create(); data["name"] = "Paratia — esempio sintetico";
        foreach (string side in new[] { "left_soils", "right_soils" })
        {
            var soil = data[side]![0]!.AsObject(); soil["name"] = "Sabbia di esempio"; soil["gamma"] = 18; soil["gamma_sat"] = 20; soil["phi"] = 30;
            soil["c"] = 0; soil["k0"] = .5; soil["kh"] = .001; soil["source"] = "Dati sintetici per collaudo"; soil["stiffness_source"] = "Parametro assegnato nel collaudo";
        }
        data["segments"]![0]!["ei"] = 200000; data["segments"]![0]!["source"] = "EI assegnato nel collaudo";
        var excavation = EmbeddedWallDocument.NewRow("stages"); excavation["id"] = "F2"; excavation["right_ground"] = -4; data["stages"]!.AsArray().Add(excavation);
        var factory = ModuleHostBench.SheetEditors([]);
        using var editor = factory.Open(EmbeddedWallDocument.Module, data);
        Check(editor is MvvmSheetEditor, "Il nuovo modulo non apre il contenitore MVVM.");
        var sheet = (EmbeddedWallSheetViewModel)((MvvmSheetEditor)editor).Sheet;
        var window = new Window { Content = editor.View, Width = 1450, Height = 950, Left = -32000, Top = -32000,
            WindowStartupLocation = WindowStartupLocation.Manual, WindowStyle = WindowStyle.None, ShowActivated = false, ShowInTaskbar = false };
        Appearance.Watch(window); window.Show();
        async Task Settle() { window.UpdateLayout(); await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle); window.UpdateLayout(); }
        void Capture(string name)
        {
            var bitmap = new RenderTargetBitmap((int)editor.View.ActualWidth, (int)editor.View.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(editor.View);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(Path.Combine(output, name + ".png")); png.Save(file);
        }
        try
        {
            await Settle(); Check(Visual(editor.View).OfType<EmbeddedWallSheetView>().Count() == 1, "Vista XAML non risolta."); Capture("modello");
            await editor.CalculateAsync(); Check(sheet.Current?.Complete == true && sheet.Stages.Count == 2, "Scavo a due fasi non convergente nella vista.");
            var tabs = Visual(editor.View).OfType<TabControl>().First(); tabs.SelectedIndex = 1; await Settle();
            var tables = Visual(editor.View).OfType<TabControl>().Last(); tables.SelectedIndex = 4; await Settle(); Capture("fasi");
            Check(Visual(editor.View).OfType<EmbeddedWallTableView>().Any(), "Editor tabellare assente.");
            tabs.SelectedIndex = 2; sheet.Diagram = "Momento [kNm]"; sheet.ComparisonStage = sheet.Stages[0]; await Settle(); Capture("momenti");
            var plot = Visual(editor.View).OfType<EmbeddedWallDiagram>().Single();
            Check(ReferenceEquals(plot.Points, sheet.Points) && ReferenceEquals(plot.Comparison, sheet.Stages[0].Points), "Grafico non associato al risultato corrente.");
            Appearance.Set(AppAppearance.Dark, false); await Settle(); Capture("momenti-scuro");
            Appearance.Set(AppAppearance.Light, false);
            sheet.Tables.First(t => t.Key == "stages").Rows[1]["right_ground"] = "-3"; await Settle();
            Check(plot.Points?.Count == 0 && sheet.Current is null, "Grafico superato rimasto visibile.");
            var hydraulic = sheet.Data.DeepClone().AsObject(); hydraulic["hydraulic_mode"] = "FEM 2D";
            hydraulic["hydraulic_left_extent"] = 12; hydraulic["hydraulic_right_extent"] = 12; hydraulic["hydraulic_bottom"] = -18;
            hydraulic["hydraulic_thickness"] = .6; hydraulic["hydraulic_mesh"] = 1;
            foreach (var stage in hydraulic["stages"]!.AsArray().Cast<JsonObject>()) { stage["left_water"] = stage["left_ground"]!.DeepClone(); stage["right_water"] = stage["right_ground"]!.DeepClone(); }
            foreach (string side in new[] { "hydraulic_left", "hydraulic_right" })
            {
                var soil = EmbeddedWallDocument.NewRow(side); soil["kxx"] = 1e-5; soil["kzz"] = 1e-5; soil["source"] = "Prova grafica sintetica";
                hydraulic[side]!.AsArray().Add(soil);
            }
            var body = EmbeddedWallDocument.NewRow("hydraulic_upl"); hydraulic["hydraulic_upl"]!.AsArray().Add(body);
            foreach (var action in new[] { ("Acqua", 15d), ("Peso totale", -20d) })
            {
                var row = EmbeddedWallDocument.NewRow("hydraulic_actions"); row["id"] = action.Item1; row["upward_force"] = action.Item2; row["source"] = "Azione sintetica di collaudo";
                hydraulic["hydraulic_actions"]!.AsArray().Add(row);
            }
            var hyd = EmbeddedWallDocument.NewRow("hydraulic_hyd"); hydraulic["hydraulic_hyd"]!.AsArray().Add(hyd);
            sheet.Reload(hydraulic); await editor.CalculateAsync();
            Check(sheet.Current?.Complete == true && sheet.HydraulicStages.Count == 2, "Filtrazione non disponibile nella vista.");
            tabs.SelectedIndex = 4; await Settle(); Capture("filtrazione");
            var field = Visual(editor.View).OfType<SeepageDiagram>().Single();
            Check(ReferenceEquals(field.Field, sheet.SelectedHydraulicStage), "Campo idraulico non associato al risultato corrente.");
            sheet.HydraulicDiagram = "Pressione interstiziale [kPa]"; Appearance.Set(AppAppearance.Dark, false); await Settle(); Capture("pressioni-scuro");
            Appearance.Set(AppAppearance.Light, false);
            tabs.SelectedIndex = 5; await Settle(); Capture("upl-hyd");
            Check(sheet.HydraulicChecks.Count == 2 && sheet.HydraulicChecks[0].Demand == 16.5 && sheet.HydraulicChecks[0].Capacity == 18,
                "Bilancio UPL non mostrato nella vista.");
            Check(Visual(editor.View).OfType<DataGrid>().Any(g => ReferenceEquals(g.ItemsSource, sheet.HydraulicChecks)), "Tabella verifiche non associata allo snapshot.");
            sheet.SelectedHydraulicCheck = sheet.HydraulicChecks[1]; Appearance.Set(AppAppearance.Dark, false); await Settle(); Capture("hyd-non-valutato-scuro");
            Check(sheet.SelectedHydraulicCheck.Outcome == GPC.Model.Checking.Contracts.EngineeringOutcome.NotEvaluated, "Dati HYD mancanti presentati come soddisfatti.");
            Appearance.Set(AppAppearance.Light, false);
            var globalData = sheet.Data.DeepClone().AsObject(); globalData["segments"]![0]!["weight"] = 15;
            var globalRow = EmbeddedWallDocument.NewRow("global_checks"); globalRow["stage"] = "F2";
            globalRow["global_thickness"] = .6; globalRow["left_extent"] = 12; globalRow["right_extent"] = 12;
            globalRow["characteristic_confirmed"] = true; globalRow["source"] = "Parete continua e dati caratteristici sintetici di collaudo";
            globalRow["exit_min"] = -10; globalRow["exit_max"] = -1; globalRow["entry_min"] = 1; globalRow["entry_max"] = 10;
            globalRow["depth_min"] = 12.5; globalRow["depth_max"] = 17; globalRow["grid"] = 3; globalRow["slices"] = 20; globalRow["refinements"] = 0;
            globalData["global_checks"]!.AsArray().Add(globalRow);
            sheet.Reload(globalData); await editor.CalculateAsync(); tabs.SelectedIndex = 6; await Settle(); Capture("stabilita-globale");
            Check(sheet.GlobalChecks.Count == 1 && sheet.GlobalChecks[0].Surface.Count > 0, "Geometria globale non disponibile: " + sheet.GlobalChecks.FirstOrDefault()?.Diagnostic);
            var globalPlot = Visual(editor.View).OfType<EmbeddedWallGlobalDiagram>().Single();
            Check(ReferenceEquals(globalPlot.Check, sheet.SelectedGlobalCheck), "Grafico globale non associato allo snapshot corrente.");
            Appearance.Set(AppAppearance.Dark, false); await Settle(); Capture("stabilita-globale-scuro"); Appearance.Set(AppAppearance.Light, false);
            Check(sheet.GlobalChecks[0].SearchStatus == "NoSurface" && sheet.GlobalChecks[0].Outcome == GPC.Model.Checking.Contracts.EngineeringOutcome.NotEvaluated,
                "Assenza di superficie non segnalata come verifica non valutata.");
            var shorterWall = globalData.DeepClone().AsObject(); shorterWall["bottom"] = -6; shorterWall["segments"]![0]!["bottom"] = -6;
            shorterWall["global_checks"]![0]!["depth_min"] = 6.5; shorterWall["global_checks"]![0]!["depth_max"] = 15;
            sheet.Reload(shorterWall); await editor.CalculateAsync(); await Settle(); Capture("stabilita-globale-cerchio");
            Check(sheet.GlobalChecks[0].Circle is not null && sheet.GlobalChecks[0].Slices.Count > 0, "Caso grafico con cerchio e conci non risolto.");
            Appearance.Set(AppAppearance.Dark, false); await Settle(); Capture("stabilita-globale-cerchio-scuro"); Appearance.Set(AppAppearance.Light, false);
            sheet.Tables.Single(t => t.Key == "hydraulic_right").Rows[0]["kzz"] = "0.00002"; await Settle();
            Check(field.Field is null && sheet.HydraulicNodes.Count == 0, "Campo idraulico superato rimasto visibile.");
            Check(sheet.HydraulicChecks.Count == 0 && sheet.SelectedHydraulicCheck is null && !sheet.HydraulicChecksCsvCommand.CanExecute(null), "Verifiche idrauliche superate ancora esportabili.");
            Check(globalPlot.Check is null && sheet.GlobalChecks.Count == 0 && !sheet.GlobalCsvCommand.CanExecute(null), "Risultato globale superato ancora visibile o esportabile.");
            var steelData = EmbeddedWallDocument.Create(); steelData["name"] = "Puntone metallico — collaudo grafico";
            steelData["action_basis"] = "Forza di progetto sintetica, nessun coefficiente aggiuntivo";
            steelData["steel_standard"] = "NTC 2018"; steelData["steel_check_kind"] = "StaticUltimate";
            steelData["left_soils"] = new JsonArray(); steelData["right_soils"] = new JsonArray();
            steelData["fix_top_u"] = true; steelData["fix_bottom_u"] = true; steelData["fix_bottom_rotation"] = true;
            steelData["segments"]![0]!["ei"] = 200000; steelData["segments"]![0]!["source"] = "EI di collaudo";
            steelData["stages"]![0]!["left_ground"] = -12; steelData["stages"]![0]!["right_ground"] = -12;
            var steelSupport = EmbeddedWallDocument.NewRow("supports"); steelSupport["id"] = "P1"; steelSupport["kind"] = "Strut";
            steelSupport["elevation"] = 0; steelSupport["stiffness"] = 10000; steelSupport["angle"] = 30; steelSupport["spacing"] = 2;
            steelData["supports"]!.AsArray().Add(steelSupport);
            var steelActivation = EmbeddedWallDocument.NewRow("activations"); steelActivation["support"] = "P1"; steelActivation["prestress"] = 75;
            steelData["activations"]!.AsArray().Add(steelActivation);
            var steelMember = EmbeddedWallDocument.NewRow("steel_members"); steelMember["support"] = "P1";
            steelMember["diameter"] = 200; steelMember["thickness"] = 8; steelMember["nominal_grade"] = 355;
            steelMember["fy"] = 355; steelMember["fu"] = 510; steelMember["elastic_modulus"] = 210000;
            steelMember["material_source"] = "S355, resistenze assegnate per il collaudo"; steelMember["axial_only"] = true;
            steelMember["length_1"] = 3; steelMember["length_2"] = 4; steelMember["length_source"] = "Schema ideale incernierato nei due piani";
            steelData["steel_members"]!.AsArray().Add(steelMember);
            sheet.Reload(steelData); await editor.CalculateAsync(); tabs.SelectedIndex = 7; await Settle(); Capture("sostegni-acciaio");
            Check(sheet.SteelSupports.Count == 1 && Math.Abs(sheet.SteelSupports[0].NormalForce!.Value + 75) < 1e-8 && sheet.SteelChecks.Count == 3,
                "Forza del puntone o verifiche Steel assenti dalla vista.");
            Check(Visual(editor.View).OfType<DataGrid>().Any(g => ReferenceEquals(g.ItemsSource, sheet.SteelChecks)), "Tabella Steel non associata allo snapshot.");
            Appearance.Set(AppAppearance.Dark, false); await Settle(); Capture("sostegni-acciaio-scuro"); Appearance.Set(AppAppearance.Light, false);
            sheet.Tables.Single(t => t.Key == "steel_members").Rows[0]["length_2"] = "100"; await Settle();
            Check(sheet.Current is null && sheet.SteelChecks.Count == 0 && sheet.SelectedSteelSupport is null && !sheet.SteelCsvCommand.CanExecute(null),
                "Risultati Steel superati ancora visibili o esportabili.");
            await editor.CalculateAsync(); await Settle(); Capture("puntone-instabile");
            Check(sheet.SteelSupports[0].Outcome == GPC.Model.Checking.Contracts.EngineeringOutcome.NotSatisfied && sheet.SteelChecks[2].Ratio > 1,
                "Puntone oltre capacità non segnalato come non soddisfatto.");
            var anchorData = steelData.DeepClone().AsObject(); anchorData["name"] = "Sfilamento del tirante — collaudo grafico";
            anchorData["steel_standard"] = "Non richieste"; anchorData["anchor_standard"] = "NTC 2018"; anchorData["anchor_a1m1"] = true;
            anchorData["action_basis"] = "A1+M1, azione di progetto sintetica"; anchorData["supports"]![0]!["kind"] = "Anchor";
            anchorData["activations"]![0]!["prestress"] = 400;
            var anchorRow = EmbeddedWallDocument.NewRow("anchor_checks"); anchorRow["support"] = "P1";
            anchorRow["free_length"] = 10; anchorRow["bond_length"] = 6; anchorRow["drill_diameter"] = 200; anchorRow["minimum_cover"] = 6;
            anchorRow["execution_source"] = "IRS, quantità e qualità documentate nel caso sintetico";
            anchorRow["applicability_source"] = "Bulbo isolato e confinato in zona stabile";
            anchorRow["experimental_source"] = "Verbale sintetico di conferma in sito"; anchorRow["stage_source"] = "Geometria valida nella fase F1";
            anchorData["anchor_checks"]!.AsArray().Add(anchorRow);
            var anchorLayer = EmbeddedWallDocument.NewRow("anchor_layers"); anchorLayer["support"] = "P1"; anchorLayer["profile"] = "Indagine A";
            anchorLayer["axis_start"] = 0; anchorLayer["axis_end"] = 20; anchorLayer["limit_pressure"] = 2;
            anchorLayer["injection_pressure"] = 3; anchorLayer["diameter_factor"] = 1.5; anchorLayer["pressure_source"] = "Prova pressiometrica sintetica";
            anchorData["anchor_layers"]!.AsArray().Add(anchorLayer);
            sheet.Reload(anchorData); await editor.CalculateAsync(); tabs.SelectedIndex = 8; await Settle(); Capture("sfilamento-tirante");
            Check(sheet.AnchorChecks.Count == 1 && Math.Abs(sheet.AnchorChecks[0].Demand!.Value - 400) < 1e-8
                && sheet.AnchorChecks[0].Segments[0].LimitPressure == 2 && sheet.AnchorChecks[0].Segments[0].InjectionPressure == 3,
                "Forza o pressioni distinte del bulbo non mostrate.");
            Check(Visual(editor.View).OfType<DataGrid>().Any(g => ReferenceEquals(g.ItemsSource, sheet.AnchorChecks)), "Tabella sfilamento scollegata dallo snapshot.");
            Appearance.Set(AppAppearance.Dark, false); await Settle(); Capture("sfilamento-tirante-scuro"); Appearance.Set(AppAppearance.Light, false);
            sheet.Tables.Single(t => t.Key == "anchor_checks").Rows[0]["experimental_source"] = ""; await Settle();
            Check(sheet.AnchorChecks.Count == 0 && sheet.SelectedAnchorCheck is null && !sheet.AnchorCsvCommand.CanExecute(null), "Sfilamento superato ancora visibile o esportabile.");
            await editor.CalculateAsync(); await Settle(); Capture("sfilamento-preliminare");
            Check(sheet.AnchorChecks[0].Outcome == GPC.Model.Checking.Contracts.EngineeringOutcome.NotEvaluated && sheet.AnchorChecks[0].Capacity.HasValue,
                "Capacità preliminare presentata come verifica conclusa.");
            editor.InspectRevision(); var original = sheet.Data.ToJsonString(); sheet.Tables[0].AddCommand.Execute(null);
            Check(sheet.Data.ToJsonString() == original, "Editor in sola lettura modificato.");
            File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {checks} controlli WPF paratie, senza attivare finestre sul desktop");
        }
        finally { window.Content = null; window.Close(); Appearance.Set(AppAppearance.Light, false); }
    }
    private static IEnumerable<DependencyObject> Visual(DependencyObject root)
    {
        yield return root;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) foreach (var item in Visual(VisualTreeHelper.GetChild(root, i))) yield return item;
    }
}
