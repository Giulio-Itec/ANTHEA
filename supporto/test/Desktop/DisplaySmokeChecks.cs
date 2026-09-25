using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    internal async Task SmokeDisplay(string directory)
    {
        testing = true;
        Directory.CreateDirectory(directory);
        var cases = SmokeTestData.LoadCases();
        var dpi = VisualTreeHelper.GetDpi(this);
        var log = new List<string> { $"Scala effettiva Windows: {dpi.DpiScaleX * 100:0}% × {dpi.DpiScaleY * 100:0}%" };
        foreach (var (width, height) in new[] { (1600, 990), (1280, 720), (960, 640), (760, 480) })
        {
            Width = width; Height = height;
            foreach (string page in new[] { "home", "moduli", "geo_palo_verticale", "geo_micropalo_verticale" })
            {
                if (page == "home") ShowHome();
                else if (page == "moduli") ShowModules();
                else
                {
                    string caseName = page == "geo_palo_verticale" ? "palo_storico_0" : "micropalo_IRS_45_Feld";
                    document = Archivio.Documento(page);
                    document["dati"] = cases.First(c => c.S("nome") == caseName)!["input"]!.DeepClone();
                    ShowSheet(document); await editor!.CalculateAsync();
                    if (editor.Result is null || editor.Result.S("errore") != "") throw new Exception("Calcolo fallito: " + page);
                }
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                UpdateLayout();
                var root = (FrameworkElement)Content;
                if (!double.IsFinite(root.ActualWidth) || root.ActualWidth <= 0 || root.ActualHeight <= 0)
                    throw new Exception("Area di lavoro non valida: " + page);
                if (page.StartsWith("geo_"))
                {
                    editor!.VerifyDisplayLayout();
                    var position = editor.TranslatePoint(new Point(), root);
                    if (position.X < 0 || position.Y < 0 || position.X + editor.ActualWidth > root.ActualWidth + 1 || position.Y + editor.ActualHeight > root.ActualHeight + 1)
                        throw new Exception("Il modulo esce dalla finestra visibile");
                    await editor.VerifyDisplayScrolling(directory, $"{page}_{width}");
                    if (width == 1600)
                    {
                        string checksDirectory = Path.Combine(directory, page); Directory.CreateDirectory(checksDirectory);
                        await editor.VerifyStratigraphyEditing(); editor.VerifyPileOutcomes(); await editor.VerifyPresentation(checksDirectory);
                        editor.VerifyReport(checksDirectory);
                        var retainedEditor = editor;
                        await editor.VerifyNavigationThrough(() => { Commit(); ShowHome(); }, () =>
                            Ui.Descendants<Button>(dashboardBody).First(b => b.Content is string text && text.StartsWith("Riprendi ·")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
                        await editor.VerifyNavigationThrough(() => { Commit(); ShowHome(); ShowModules(); }, () =>
                            Ui.Descendants<Button>(dashboardBody).First(b => Equals(b.Content, "Riprendi")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
                        if (!ReferenceEquals(editor, retainedEditor) || body.Content != moduleView) throw new Exception("Riprendi ha ricreato il modulo");
                    }
                }
                File.WriteAllBytes(Path.Combine(directory, $"{page}_{width}.png"), Ui.Snapshot(this));
            }
            log.Add($"{width} × {height} unità logiche: modulo contenuto nella finestra, nessuno scorrimento orizzontale esterno, ultimo pannello raggiungibile, espansione e ripristino OK");
        }
        File.WriteAllLines(Path.Combine(directory, "display.txt"), log);
    }
}

internal sealed partial class SheetEditor
{
    internal void VerifyReport(string directory)
    {
        string path=Path.Combine(directory,"relazione-prova.docx");
        ExportReport(path,"Verifica impaginazione",new HashSet<string>{"generali","efficienza","coefficienti","stratigrafia","nq","risultati","grafico_nq"});
        using var zip=System.IO.Compression.ZipFile.OpenRead(path);
        using var stream=zip.GetEntry("word/document.xml")!.Open();
        var xml=System.Xml.Linq.XDocument.Load(stream);
        System.Xml.Linq.XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        if(xml.Descendants(w+"t").Any(t=>t.Value=="Avvisi"||t.Value=="sottotipo_palo_battuto"))throw new Exception("Contenuti non richiesti nella relazione");
        if(xml.Descendants(w+"tc").Any(c=>!c.Descendants(w+"jc").Any(j=>(string?)j.Attribute(w+"val")=="center")))throw new Exception("Celle della relazione non centrate");
        if(zip.Entries.Count(e=>e.FullName.StartsWith("word/media/"))!=(Micro ? 2 * Data.Array("stratigrafie").Count : Data.Array("stratigrafie").Count+1))throw new Exception("Immagini dei sondaggi o abachi mancanti");
        if(Micro && xml.Descendants(w+"t").Any(t=>new[]{"tipo_palo","metodo_nq","presenza_falda","considera_sottospinta","pressione_limite"}.Contains(t.Value)))throw new Exception("Dati non applicabili presenti nella relazione micropalo");
        File.WriteAllBytes(Path.Combine(directory,"profilo-palo.png"),stratigraphy.Png(720,900));
    }
    internal async Task VerifyNavigationThrough(Action leave, Action resume)
    {
        int oldExpanded = expanded, oldTab = outputs.SelectedIndex, oldCapacity = capacityView.SelectedIndex;
        try
        {
            expanded = 6; LayoutCards(); outputs.SelectedIndex = 2; capacityView.SelectedIndex = 1;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            string data = Data.ToJsonString(); var result = Result;
            leave(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            resume(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            if (Data.ToJsonString() != data || !ReferenceEquals(Result, result) || expanded != 6 || outputs.SelectedIndex != 2 || capacityView.SelectedIndex != 1)
                throw new Exception("La navigazione Home/modulo ha perso input, risultati o stato dei pannelli");
        }
        finally { expanded = oldExpanded; outputs.SelectedIndex = oldTab; capacityView.SelectedIndex = oldCapacity; LayoutCards(); }
    }
    internal async Task VerifyPresentation(string directory)
    {
        var backup = Data.Array("stratigrafie").DeepClone();
        try
        {
            if (!CopySurvey(0, -1, confirm: false)) throw new Exception("Copia in nuova stratigrafia fallita");
            var surveys = Data.Array("stratigrafie");
            if (!JsonNode.DeepEquals(surveys[0], surveys[1])) throw new Exception("La copia non conserva tutti i dati");
            surveys[1]![0]!["spessore"] = "11";
            if (surveys[0]![0].S("spessore") == "11") throw new Exception("Le stratigrafie copiate non sono indipendenti");
            if (!CopySurvey(1, 0, confirm: false) || !JsonNode.DeepEquals(surveys[0], surveys[1])) throw new Exception("Copia da altra stratigrafia fallita");
        }
        finally { Data["stratigrafie"] = backup; RebuildSondages(0); Changed(); await WaitForAutomatic(); }
        var project = allSeries.Where(s => s.Key.StartsWith("progetto_") || s.Key.StartsWith("azione_")).ToArray();
        if (project.Select(s => s.Series.Color.ToString()).Distinct().Count() != project.Length) throw new Exception("Colori delle curve non distinti");
        expanded = 4; LayoutCards(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        if (Ui.Descendants<ComboBox>(this).Any(c => c.Items.Cast<object>().Any(v => v is string s && string.IsNullOrWhiteSpace(s)))) throw new Exception("Opzione vuota nei menu");
        expanded = 6; LayoutCards(); outputs.SelectedIndex = 1; tableSelect.SelectedIndex = 0;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var tables = Ui.Descendants<DataGrid>(tableHost).ToArray();
        if (tables.Length != (Micro ? 1 : 2) || tables.Any(t => t.ActualWidth > 621)) throw new Exception("Tabelle riepilogo troppo larghe");
        foreach (var table in tables)
            foreach (var row in table.Items.Cast<string[]>())
                foreach (var value in row.Skip(1))
                    if (!System.Text.RegularExpressions.Regex.IsMatch(value, @"^-?\d+,\d$")) throw new Exception("Numero non visualizzato con un decimale: " + value);
        File.WriteAllBytes(Path.Combine(directory, "tabelle_estese.png"), Ui.Snapshot(Window.GetWindow(this)));
        outputs.SelectedIndex = 2; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        if (!reference.Markers.Any(m => m.ProjectToAxes)) throw new Exception("Proiezioni Nq mancanti");
        File.WriteAllBytes(Path.Combine(directory, "nq_riferimenti.png"), Ui.Snapshot(Window.GetWindow(this)));
        outputs.SelectedIndex = 0; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        File.WriteAllBytes(Path.Combine(directory, "curve_colori.png"), Ui.Snapshot(Window.GetWindow(this)));
        expanded = -1; LayoutCards();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    }
    internal void VerifyPileOutcomes()
    {
        var original = Result;
        try
        {
            string[] keys = Micro ? ["compressione", "trazione"] : ["drenante_compressione", "non_drenante_compressione", "drenante_trazione", "non_drenante_trazione"];
            foreach (var (action, expected) in new[] { (99.0, "Verifica soddisfatta"), (100.0, "Verifica soddisfatta"), (101.0, "Verifica non soddisfatta") })
            {
                var curves = new JsonObject();
                foreach (string key in keys) curves[key] = J.Obj(("progetto", new JsonArray(new JsonArray(1, 100))));
                Result = J.Obj(("copertura_completa", true), ("curve", curves),
                    ("azioni", J.Obj(("compressione", new JsonArray(new JsonArray(1, action))), ("trazione", new JsonArray(new JsonArray(1, action))))));
                UpdateVerification();
                var rows = ((DataGrid)verification.Content).Items.Cast<string[]>().ToArray();
                if (!rows.Select(r => r[0]).SequenceEqual(Micro ? new[] { "Compressione", "Trazione" } : new[] { "Comp – Dre", "Comp – Non Dre", "Tra – Dre", "Tra – Non Dre" }) || rows.Any(r => r[4] != expected))
                    throw new Exception("Ordine o esito della verifica errato");
            }
            Result!["copertura_completa"] = false; UpdateVerification();
            if (((DataGrid)verification.Content).Items.Cast<string[]>().Any(r => r[4] != (Micro ? "Verifica incompleta: stratigrafia insufficiente" : "Da verificare")))
                throw new Exception("Esito definitivo con copertura incompleta");
            Result["copertura_completa"] = true; Result["azioni"]!["compressione"] = new JsonArray(); UpdateVerification();
            if (((DataGrid)verification.Content).Items.Cast<string[]>().Take(Micro ? 1 : 2).Any(r => r[4] != (Micro ? "Azione non inserita" : "Da verificare")))
                throw new Exception("Esito definitivo senza azione");
        }
        finally { Result = original; UpdateVerification(); }
    }
    internal async Task VerifyStratigraphyEditing()
    {
        var initial = Archivio.NuovoFoglio(Module).Array("stratigrafie")[0]!.AsArray();
        if (initial.Count != 1 || initial[0].S("spessore") != "0" || (Micro ? initial[0].S("alpha") != "0" || initial[0]!["nc"] is not null : initial[0].S("nc") != "9")) throw new Exception("Valori iniziali dello strato errati");
        expanded = 4; LayoutCards();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var grid = layerGrids[0];
        var input = Ui.Descendants<TextBox>(grid).First(t => System.Windows.Automation.AutomationProperties.GetName(t) == (Micro ? "Spessore verticale [m]" : "Spessore S [m]"));
        string value = input.Text;
        input.Focus(); input.Text = value + " "; input.CaretIndex = input.Text.Length;
        await WaitForAutomatic();
        if (!input.IsKeyboardFocused || input.Text != value + " " || !input.IsEnabled) throw new Exception("Il ricalcolo interrompe la digitazione");
        input.Text = value; await WaitForAutomatic();
        if (Micro)
        {
            var weightInput = (TextBox)generalForm.Editors["__peso_lineare"];
            if (!weightInput.IsReadOnly) throw new Exception("Peso lineare modificabile");
            string originalDiameter = generalForm.Get("diametro"), originalProfile = generalForm.Get("profilo_chs");
            foreach (var diameter in new[] { "0.24", "0.4" })
            foreach (var profile in new[] { "CHS 114.3 × 6.3", "CHS 139.7 × 8" })
            {
                generalForm.Set("diametro", diameter); generalForm.Set("profilo_chs", profile);
                string expected = Chs.Peso(profile, Data["generali"].D("diametro"), Data["generali"].D("peso_specifico_palo",25)).D("q_totale").ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("it-IT"));
                if (weightInput.Text != expected) throw new Exception("Peso lineare non aggiornato con diametro/profilo");
            }
            generalForm.Set("diametro", "0.01");
            if (weightInput.Text != "—") throw new Exception("Peso disponibile con geometria invalida");
            generalForm.Set("diametro", originalDiameter); generalForm.Set("profilo_chs", originalProfile); await WaitForAutomatic();
            var layer = grid.Rows[0]; string soil = layer.Values.S("terreno"), alpha = layer.Values.S("alpha");
            var choice = Ui.Descendants<ComboBox>(grid).First(c => ReferenceEquals(c.DataContext, layer));
            choice.SelectedItem = "Ghiaia";
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var alphaInput = Ui.Descendants<TextBox>(grid).First(t => ReferenceEquals(t.DataContext, layer) && System.Windows.Automation.AutomationProperties.GetName(t) == "α adottato");
            if (alphaInput.Text != layer.Values.S("alpha") || layer.Values.D("alpha") != 1.8) throw new Exception("α non aggiornato nella cella terreno");
            choice.SelectedItem = soil; layer["alpha"] = alpha;
            var tip = (CheckBox)generalForm.Editors["considera_punta"]; bool? oldTip = tip.IsChecked;
            tip.IsChecked = false;
            if (generalForm.Editors["percentuale_punta"].IsEnabled || normativeForm.Editors["sicurezza_base"].IsEnabled) throw new Exception("Parametri della punta non disabilitati");
            tip.IsChecked = oldTip; await WaitForAutomatic();
            foreach (var segment in Result!.Array("dettagli")[^1]!.Array("sondaggi")[0]!.Array("tratti"))
            {
                int layerIndex = (int)segment.D("strato") - 1;
                if (grid.Rows[layerIndex].Values.S("__tau") != segment.D("s").ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("it-IT"))) throw new Exception("τ visualizzata diversa dall’aderenza usata nel calcolo");
            }
            var tauInput = Ui.Descendants<TextBox>(grid).First(t => ReferenceEquals(t.DataContext, layer) && System.Windows.Automation.AutomationProperties.GetName(t) == "Aderenza τ [kPa]");
            if (!tauInput.IsReadOnly || tauInput.Text != layer.Values.S("__tau")) throw new Exception("Colonna τ non aggiornata o modificabile");
            string pressure = generalForm.Get("pressione_iniezione");
            generalForm.Set("pressione_iniezione", "4");
            if (layer.Values.S("__tau") != "450,0") throw new Exception("τ non aggiornata con la pressione");
            layer["laterale_attiva"] = false;
            if (layer.Values.S("__tau") != "0,0") throw new Exception("τ non nulla con laterale esclusa");
            layer["laterale_attiva"] = true; generalForm.Set("pressione_iniezione", "99");
            if (layer.Values.S("__tau") != "—") throw new Exception("τ disponibile fuori abaco");
            generalForm.Set("pressione_iniezione", pressure); await WaitForAutomatic();
        }
        if (!Micro)
        {
            var layer = grid.Rows[0]; string gamma = layer.Values.S("peso_specifico"), sat = layer.Values.S("peso_specifico_saturo");
            var satInput = Ui.Descendants<TextBox>(grid).First(t => ReferenceEquals(t.DataContext, layer) && System.Windows.Automation.AutomationProperties.GetName(t) == "γsat [kN/m³]");
            var hint = Ui.Descendants<TextBlock>(grid).First(t => ReferenceEquals(t.DataContext, layer) && Equals(t.Tag, "gamma-sat-automatico"));
            foreach (string blank in new[] { "", "  " })
            {
                satInput.Text = blank; layer["peso_specifico"] = "19,5";
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                if (hint.Visibility != Visibility.Visible || hint.Text != "19,5" || !Equals(hint.Foreground, Brushes.Gray) || satInput.Text != blank || layer.Values.S("peso_specifico_saturo") != blank)
                    throw new Exception("γsat automatico non visualizzato o memorizzato come valore esplicito");
                layer["peso_specifico"] = "20";
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                if (hint.Text != "20") throw new Exception("γsat automatico non segue γ");
            }
            foreach (string explicitValue in new[] { "21", "0" })
            {
                satInput.Text = explicitValue;
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                if (hint.Visibility != Visibility.Collapsed || layer.Values.S("peso_specifico_saturo") != explicitValue) throw new Exception("γsat esplicito sostituito dal valore automatico");
            }
            satInput.Text = ""; await WaitForAutomatic();
            if (hint.Visibility != Visibility.Visible || layer.Values.S("peso_specifico_saturo") != "") throw new Exception("Ricalcolo perde γsat automatico");
            layer["peso_specifico"] = gamma; satInput.Text = sat; await WaitForAutomatic();
        }
        int count = grid.Rows.Count;
        var add = Ui.Descendants<Button>(sondages).First(b => Equals(b.Content, "Aggiungi strato"));
        add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var middle = grid.Rows[count]; var last = grid.Rows[count + 1];
        var remove = Ui.Descendants<Button>(grid).First(b => Equals(b.Content, "[−]") && ReferenceEquals(b.DataContext, middle));
        remove.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (grid.Rows.Contains(middle) || !ReferenceEquals(grid.Rows[^1], last) || Data.Array("stratigrafie")[0]!.AsArray().Count != count + 1)
            throw new Exception("Eliminazione dello strato intermedio errata");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Ui.Descendants<Button>(grid).First(b => Equals(b.Content, "[−]") && ReferenceEquals(b.DataContext, last)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await WaitForAutomatic();
        var surveys = Data.Array("stratigrafie");
        var extra = new JsonArray(NewLayer()); var retained = surveys[0];
        surveys.Add(extra); RebuildSondages(0);
        DeleteSurvey(extra, confirm: false);
        if (surveys.Count != 1 || !ReferenceEquals(surveys[0], retained) || sondages.SelectedIndex != 0)
            throw new Exception("Eliminazione della singola stratigrafia errata");
        await WaitForAutomatic(); expanded = -1; LayoutCards();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    }
    internal void VerifyDisplayLayout()
    {
        if (scroll.ScrollableWidth > 1 || canvas.Width > scroll.ViewportWidth + 1)
            throw new Exception("Il modulo richiede ancora scorrimento orizzontale esterno");
        var bounds = cards.Where(c => c.Visibility == Visibility.Visible)
            .Select(c => new Rect(Canvas.GetLeft(c), Canvas.GetTop(c), c.Width, c.Height)).ToArray();
        for (int i = 0; i < bounds.Length; i++)
        {
            if (bounds[i].Right > canvas.Width + 1 || bounds[i].Bottom > canvas.Height + 1)
                throw new Exception("Pannello fuori dalla superficie scorrevole");
            for (int j = i + 1; j < bounds.Length; j++)
                if (bounds[i].IntersectsWith(bounds[j])) throw new Exception("Pannelli sovrapposti");
        }
    }

    internal async Task VerifyDisplayScrolling(string directory, string name)
    {
        scroll.ScrollToBottom();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        if (scroll.VerticalOffset + scroll.ViewportHeight < canvas.Height - 1)
            throw new Exception("La parte inferiore del modulo non è raggiungibile");
        File.WriteAllBytes(Path.Combine(directory, name + "_fondo.png"), Ui.Snapshot(Window.GetWindow(this)));
        foreach (int index in expandButtons.Keys)
        {
            expanded = index; LayoutCards();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            VerifyDisplayLayout();
            File.WriteAllBytes(Path.Combine(directory, name + $"_esteso_{index}.png"), Ui.Snapshot(Window.GetWindow(this)));
        }
        expanded = -1; LayoutCards(); scroll.ScrollToTop();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        VerifyDisplayLayout();
    }
}
