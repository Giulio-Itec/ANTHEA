using System.IO;
using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Anthea.Application.Concrete;
using Anthea.Presentation.Modules.ConcreteSection;
using X.Core;
using X.Desktop.Views.ConcreteSection;

namespace X.Desktop;

internal sealed partial class ConcreteSectionSmoke
{
    /// <summary>
    /// ui/smoke-ca-extensions (VerifyExtensions of the historic ConcreteExtensionSmokeChecks.cs): the same steps and messages on the XAML view. The
    /// hole and the circular section are chosen with the controls of the inputs (the historic check wrote the data and invalidated); the native
    /// geometry of the domain on its observable effect (the domains calculated again on the new section, CA-P1).
    /// </summary>
    internal async Task VerifyExtensions(string directory)
    {
        int count = 0; void Check(bool value, string text) { if (!value) throw new Exception(text); count++; }
        async Task Updated() { await Automatic(); await Sheet.CalculateAsync(); await Settle(); }
        Task Capture(string name) => this.Capture(directory, name);
        // The eighth tab (reinforcement design, 895bf31) follows the seven checked here: their indices do not change.
        Check(View.Tabs.Items.Count == 8 && ConcreteSectionViewChecks.AutomationName((TabItem)View.Tabs.Items[6]) == "Momento–curvatura"
            && ConcreteSectionViewChecks.AutomationName((TabItem)View.Tabs.Items[7]) == "Calcola armature", "Otto schede CA, l'ultima per il calcolo armature");
        Tab(0);
        var materials = Inputs.Materials;
        var originalCls = Input.S("cls_diagramma"); var originalSteel = Input.S("steel_diagramma");
        var clsName = Input.S("classe_cls"); var steelName = Input.S("classe_acciaio");
        materials.Set("cls_diagramma", "Bilineare"); materials.Set("steel_diagramma", "Incrudente");
        Check(materials["cls_diagramma"].IsEnabled && materials["steel_diagramma"].IsEnabled, "Legami dei materiali selezionabili");
        Check(Input.S("classe_cls") == clsName && Input.S("classe_acciaio") == steelName, "Cambio legame conserva le classi dei materiali");
        Check(ConcreteMaterials.Rebar(Input).StressStrainCurve == GPC.Model.Materials.SteelMaterial.StressStrainCurveType.ElasticHardening, "Legame incrudente passato al materiale nativo");
        // The catalogues of the standard read again (RefreshStandardMaterialChoices): a change of the standard and back.
        string standard = Settings.S("normativa");
        Inputs.Standard.Set("normativa", standard == "EN 1992-1-1" ? "NTC 2018" : "EN 1992-1-1"); Inputs.Standard.Set("normativa", standard);
        Check(Input.S("cls_diagramma") == "Bilineare" && Input.S("steel_diagramma") == "Incrudente", "Aggiornamento catalogo conserva i legami scelti");
        materials.Set("cls_diagramma", originalCls); materials.Set("steel_diagramma", originalSteel);
        await Updated();
        foreach (var (group, ratio) in new[] { ("ingressi", .32), ("risultati", .62), ("righe", .64) })
        {
            bool rows = group == "righe"; Sheet.MoveLayout(group, ratio); Layout();
            var grids = ConcreteSectionSheetView.Logical<Grid>(View.Tabs).Where(g => Equals(g.Tag, group)).ToArray();
            Check(grids.Length > 0 && grids.All(g => Math.Abs((rows ? g.RowDefinitions[0].Height.Value : g.ColumnDefinitions[0].Width.Value) - ratio) < 1e-10), "Divisori sincronizzati: " + group);
        }
        Sheet.MoveLayout("ingressi", .3); Sheet.MoveLayout("risultati", .6); Sheet.MoveLayout("righe", 3.7 / 5.7);
        Tab(3); await Capture("00_allineamento_sle");
        var labels = new[] { "SLE", "SLE_FREQ", "SLE_QP" }.Select(SectionWorkspace.Label).ToArray();
        await Idle(); var sleChoice = Ui.Descendants<ComboBox>(TabContent(3)).SingleOrDefault(c => c.IsVisible && c.Items.Cast<object>().Select(i => i?.ToString()).SequenceEqual(labels));
        Check(sleChoice is not null && sleChoice.IsVisible, "Selettore combinazione SLE visibile nella colonna dati");
        sleChoice!.SelectedIndex = 2; await Idle();
        Check(View.StressTabs.SelectedIndex == 2 && StressView("SLE_QP").IsVisible, "Selettore SLE apre Quasi permanente");
        View.StressTabs.SelectedIndex = 0; await Idle();
        Check(sleChoice.SelectedIndex == 0 && StressView("SLE").IsVisible, "Navigazione SLE mantiene selettore e vista coerenti");
        Tab(0); await Idle();
        var tendonMaterialForm = Inputs.TendonProperties;
        string savedTendonDiagram = Settings["materiale_trefolo"].S("diagramma"), tendonId = Settings["materiale_trefolo"].S("id");
        string changedTendonDiagram = savedTendonDiagram == "Incrudente" ? "Elastoplastico" : "Incrudente";
        tendonMaterialForm.Set("diagramma", changedTendonDiagram);
        Check(Settings["materiale_trefolo"].S("diagramma") == changedTendonDiagram && ConcreteInputPresentation.TendonMaterials(Settings).Single(m => m.S("id") == tendonId).S("diagramma") == changedTendonDiagram,
            "Legame trefolo predefinito aggiornato per nuovi cavi");
        tendonMaterialForm.Set("diagramma", savedTendonDiagram);
        await Capture("00_materiali");
        var preservedResult = Result;
        Inputs.QuickResistance.Set("N", "-500"); await Settle();
        Check(Inputs.QuickResistanceText.Split('\n').Length == 4 && !Inputs.QuickResistanceText.Contains("—"), "Quattro resistenze rapide plastiche: " + Inputs.QuickResistanceText);
        Inputs.QuickResistance.Set("tipo", "Elastico"); await Settle();
        Check(Inputs.QuickResistanceText.Split('\n').Length == 4 && !Inputs.QuickResistanceText.Contains("—"), "Quattro resistenze rapide elastiche");
        Check(ReferenceEquals(preservedResult, Result) && Pending.Length == 0 && !Queued, "Input resistenze rapide non rigenera il dominio");
        await Capture("00_resistenze_rapide");
        Tab(1); await Idle(); var panel = Three; panel.Table.Table.SelectedIndex = 0;
        await Capture("00b_linea_verifica");
        View.Domain3DView.InnerTabs.SelectedIndex = 1; await Settle();
        for (int i = 0; i < 200 && View.Domain3DView.Map.Stress is null; i++) await Task.Delay(25);
        Check(View.Domain3DView.Map.Stress is not null, "Mappa del punto limite: " + panel.Inspection.Message);
        await Capture("01_limite");
        foreach (var inspectedPanel in new[] { Three, Two })
        {
            Tab(inspectedPanel.ThreeD ? 1 : 2); await Idle();
            var panelView = inspectedPanel.ThreeD ? View.Domain3DView : View.Domain2DView;
            if (!inspectedPanel.ThreeD)
            {
                // The sample action is biaxial: explicitly project it into the 2D test plane.
                inspectedPanel.Form.Set("proietta", "Sì");
                await Automatic();
            }
            inspectedPanel.Table.Table.SelectedIndex = 0;
            panelView.InnerTabs.SelectedIndex = 1; await Settle();
            for (int i = 0; i < 200 && panelView.Map.Stress is null; i++) await Task.Delay(25);
            Check(panelView.Map.Stress is not null, "Soluzione disponibile per il test del menu " + inspectedPanel.Prefix);
            await Idle();
            var view = panelView.Map;
            var contourChoice = Named<ComboBox>(panelView, "Contorno della mappa");
            foreach (var contour in ConcreteSectionViewport.Contours.Concat(ConcreteSectionViewport.Contours.Reverse()))
            {
                contourChoice.SelectedItem = contour;
                await Idle();
                Check(view.Contour == contour, $"Menu dominio {inspectedPanel.Prefix}: selezionato '{contour}', visualizzato '{view.Contour}'");
            }
            for (int i = 0; i < ConcreteSectionViewport.Contours.Length; i++)
            {
                contourChoice.SelectedIndex = i;
                await Capture($"contour_{(inspectedPanel.ThreeD ? "3d" : "2d")}_{i}");
                _ = view.Png(800, 600);
                Check(i == 7 ? view.ContourLegend == "" : view.ContourLegend.Length > 0, "Legenda della vista selezionata");
                if (i == 6)
                    Check(view.ContourLegend.Contains(EngineeringFormat.Number(view.Stress!.ConcreteVertices.Min(v => v.Strain))), "Scala delle deformazioni include il bordo compresso");
            }
            contourChoice.SelectedIndex = 0;
        }
        Tab(1); await Idle();
        Named<ComboBox>(View.Domain3DView, "Soluzione mostrata").SelectedIndex = 0; await Settle();
        for (int i = 0; i < 300 && View.Domain3DView.Map.Stress is null; i++) await Task.Delay(25);
        Check(View.Domain3DView.Map.Stress is not null, "Mappa azione inserita: " + panel.Inspection.Message);
        await Capture("02_azione");
        File.WriteAllBytes(Path.Combine(directory, "03_piano.png"), View.Domain3DView.Plane.Png(800, 600));
        var detailing = Sheet.Detailing;
        detailing.Element.Set("elemento", "Pilastro"); detailing.Anchorage.Set("lunghezza", "1200");
        Tab(5); await Idle();
        bool Detailed(string name) => Result?["dettagli_costruttivi"] is JsonArray checks && checks.Any(c => c?["Name"]?.ToString() == name);
        string AnchorageText() => Result.S("ancoraggio_esito");
        Check(Detailed("Diametro staffe"), "Esposizione non scelta non blocca le verifiche indipendenti");
        Stress("SLE_QP").Form.Set("esposizione", "XC1");
        double ordinary = Settings["dettagli_costruttivi"].D("cmin_dur");
        Stress("SLE_QP").Form.Set("esposizione", "XS3");
        Check(Settings["dettagli_costruttivi"].D("cmin_dur") > ordinary && detailing.Cover.Get("esposizione_sle") == "XS3", "Copriferro aggiornato automaticamente da esposizione SLE");
        Stress("SLE_QP").Form.Set("esposizione", "XC1");
        await Automatic();
        var anchorage = detailing.Anchorage;
        anchorage.Set("percentuale", ""); anchorage.Set("interferro", "");
        Check(Result?["ancoraggio"] is JsonObject, "Ancoraggio rettilineo indipendente dai dati di sovrapposizione");
        anchorage.Set("lunghezza", "");
        Check(Result?["ancoraggio"] is null && AnchorageText().Contains("Inserire la lunghezza"), "Lunghezza vuota invalida il risultato");
        anchorage.Set("lunghezza", "1200"); anchorage.Set("tipo", "Sovrapposizione rettilinea");
        anchorage.Set("percentuale", "100"); anchorage.Set("interferro", "1000");
        Check(Result?["ancoraggio"]?["Passed"] is JsonValue passed && !passed.GetValue<bool>() && AnchorageText().Contains("Interferro della giunzione"), "Interferro giunzione verificato separatamente");
        anchorage.Set("confinamento", "Non conforme");
        Check(AnchorageText().Contains("Non conforme"), "Riscontro esecutivo modificabile");
        anchorage.Set("tipo", "Ancoraggio rettilineo"); anchorage.Set("confinamento", "Da verificare");
        await Capture("04_dettagli");
        string Topic(string key) => detailing.Topics.Single(t => t.Key == key).Checks.Text;
        Check(Topic("cover").Contains("Interferro minimo") && Topic("stirrups").Contains("Diametro staffe") && Topic("bars").Contains("Armatura longitudinale"), "Verifiche affiancate per argomento");
        var detailScroll = Ui.Descendants<ScrollViewer>(View.DetailingView).FirstOrDefault(s => s.IsVisible);
        detailScroll?.ScrollToVerticalOffset(detailScroll.ScrollableHeight / 2); await Capture("04b_staffe");
        detailScroll?.ScrollToBottom(); await Capture("04c_ancoraggi"); detailScroll?.ScrollToTop();
        Check(Detailed("Diametro staffe"), "Controlli pilastro eseguiti");
        var curvature = Sheet.Curvature;
        curvature.Form.Set("N", "-500"); curvature.Form.Set("passi", "12"); curvature.Form.Set("angoli", "16");
        Tab(6); await curvature.CalculateCommand.ExecuteAsync(null); await Settle();
        Check(curvature.Result?.Points.Count >= 13, "Diagramma fino al limite: " + curvature.Text); await Capture("05_curvatura");
        // The hole, through its switch and its fields (the historic check wrote the data and invalidated).
        Tab(0); await Idle();
        if (!Inputs.HoleChecked) Click(Named<CheckBox>(TabContent(0), ConcreteSectionViewChecks.AutomationName(View.HoleToggle)));
        Inputs.Hole.Set("inner_width_mm", "200"); Inputs.Hole.Set("inner_height_mm", "300"); await Updated();
        Tab(0); await Capture("06_rettangolare_forata"); Check(Inputs.Drawing.Section!.Holes.Count == 1, "Foro rettangolare in anteprima");
        Inputs.Geometry.Set("shape", "Circolare"); Inputs.Hole.Set("inner_diameter_mm", "500"); await Updated(); await Capture("07_circolare_forata");
        Check(Result?["domini"]?["3D:SLU"] is JsonObject, "Dominio circolare forato calcolato");
        Check(Inputs.Drawing.Section!.Outline.Count == 32 && Inputs.Drawing.Section.Holes[0].Length == 32, "Anteprima circolare: default 32 lati per contorno");
        Inputs.Geometry.Set("circular_sides", "64");
        var pending = Pending;
        await Updated();
        Check(pending.Contains("3D:SLU") && Result?["domini"]?["3D:SLU"] is JsonObject && Inputs.Drawing.Section!.Outline.Count == 64 && Inputs.Drawing.Section.Holes[0].Length == 64,
            "Modifica dei lati: anteprima e verificatore aggiornati " + PendingText(pending));
        FieldEditor<TextBox>(Inputs.Geometry, "circular_sides", TabContent(0)).BringIntoView(); await Capture("07_lati_circolari");
        var circle = AddShear("Taglio circolare", "-100", "20", "30"); await Automatic();
        Check(ShearResults[circle.Values.S("id")] is null, "Circolare senza scelta del modello non verificata");
        Sheet.Shear.Torsion.Set("modello_circolare", "Pile · NTC §7.9.5.2"); await Automatic(); Check(ShearResults[circle.Values.S("id")] is not null, "Taglio circolare cavo con scelta esplicita");
        Sheet.Actions.Shear.Table.Selected = circle; Sheet.Actions.Shear.RemoveCommand.Execute(null);
        Inputs.Geometry.Set("shape", "Rettangolare");
        Tab(0); await Idle();
        if (Inputs.HoleChecked) Click(Named<CheckBox>(TabContent(0), ConcreteSectionViewChecks.AutomationName(View.HoleToggle)));
        await Updated();
        var torsion = Sheet.Shear.Torsion;
        torsion.Set("chiusura_torsione", "Confermato"); torsion.Set("as_torsione", "600"); torsion.Set("cot_torsione", "1");
        var torsionRow = AddShear("Taglio e torsione", "-300", "50", "30", "20"); await Automatic();
        Tab(4); Sheet.Actions.Shear.Table.Selected = torsionRow; await Capture("08_torsione");
        Check(Result?["torsione"]?[torsionRow.Values.S("id")] is JsonObject, "Torsione calcolata dalla UI: " + torsionRow.Values.S("esito"));
        Stress("SLE_QP").Form.Set("esposizione", "XC1"); Stress("SLE_QP").Form.Set("spaziatura_fessure", "100");
        var row = AddAction("SLE_QP", "Trazione pura", "300", "0", "0"); await Updated();
        Tab(3); Family("SLE_QP"); await Idle(); Stress("SLE_QP").Table.Table.Selected = row; await Capture("09_fasce_tese");
        Check(Stress("SLE_QP").Regions.Count == 4, "Quattro zone efficaci selezionabili");
        Check(Result?["torsione"]?[torsionRow.Values.S("id")] is not null && Result?["dettagli_costruttivi"] is JsonArray, "Esportazione dei nuovi risultati");
        Tab(6); await curvature.CalculateCommand.ExecuteAsync(null); await Settle();
        Check(Result?["momento_curvatura"]?["Points"] is JsonArray points && points.Count >= 13, "Esportazione della curva");
        var curveReport = Editor.BuildReport("Momento curvatura", ["curvatura", "grafici"]);
        using (var zip = new ZipArchive(new MemoryStream(curveReport)))
            Check(zip.Entries.Count(e => e.FullName.StartsWith("word/media/")) == 1, "Grafico della curvatura disponibile anche senza selezionare Taglio");
        Editor.ExportReport(Path.Combine(directory, "estensioni.docx"), "Controllo integrazione CA", ["geometria", "azioni", "taglio", "costruttivi", "curvatura", "grafici"]);
        using (var zip = ZipFile.OpenRead(Path.Combine(directory, "estensioni.docx")))
        using (var stream = new StreamReader(zip.GetEntry("word/document.xml")!.Open()))
        { string xml = stream.ReadToEnd(); Check(xml.Contains("TRsd") && xml.Contains("Dettagli costruttivi") && xml.Contains("Momento–curvatura"), "Report delle estensioni"); }
        _ = Result; curvature.Form.Set("N", "-600"); Check(Result?["momento_curvatura"] is null, "Modifica del percorso invalida anche la curva esportata");
        Editor.Commit();
        var reopened = Reopen(JsonNode.Parse(Data.ToJsonString())!.AsObject(), out var reopenedEditor);
        using (reopenedEditor)
        {
            Check(reopened.ShearRowCollection.Any(r => r.Values.S("T") == "20") && reopened.Settings["dettagli_costruttivi"].S("elemento") == "Pilastro", "Persistenza di torsione e tipo elemento");
            Check(reopened.Input.D("circular_sides") == 64, "Numero di lati conservato alla riapertura");
            Check(reopened.Settings["resistenze_rapide"].S("N") == "-500" && reopened.Settings["resistenze_rapide"].S("tipo") == "Elastico", "Input resistenze rapide conservati alla riapertura");
        }
        File.WriteAllText(Path.Combine(directory, "esito.txt"), count + " controlli interfaccia superati.");
    }
}
