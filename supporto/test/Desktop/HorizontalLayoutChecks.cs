using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

internal sealed partial class HorizontalWorkspace
{
    internal async Task WaitForAutomatic()
    {
        var until = DateTime.UtcNow.AddSeconds(25);
        while (Busy || timer.IsEnabled)
        {
            if (DateTime.UtcNow > until) throw new Exception("Ricalcolo automatico non terminato");
            await Task.Delay(30);
        }
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    }

    private async Task VerifyAutomatic()
    {
        await WaitForAutomatic();
        if (Result is null || Ui.Descendants<Button>(this).Any(b => b.Content?.ToString()?.StartsWith("Calcola") == true))
            throw new Exception("Calcolo automatico iniziale o rimozione pulsanti fallita");
        string action = general.Get("azione_orizzontale"), diameter = general.Get("diametro"), concrete = sectionFields.Get("fck_mpa");
        double initialMoment = Result.D("momento_resistente_knm");
        string materialFck = sectionFields.Get("fck_mpa");
        ((ComboBox)sectionFields.Editors["classe_cls"]).SelectedItem = "C25/30";
        await WaitForAutomatic();
        if (Data["sezione"].D("fck_mpa") != 25 || sectionFields.Get("__fcd") != (25 * Data["sezione"].D("alpha_cc") / Data["sezione"].D("gamma_c")).ToString("F1"))
            throw new Exception("Classe CLS e resistenza di progetto discordanti");
        if (sectionFields.Get("__fyd") != (Data["sezione"].D("fyk_mpa") / Data["sezione"].D("gamma_s")).ToString("F1") ||
            sectionFields.Editors["longitudinal_bar_diameter_mm"] is not ComboBox || sectionFields.Editors["transverse_bar_diameter_mm"] is not ComboBox)
            throw new Exception("Materiali o menu armatura errati");
        sectionFields.Set("fck_mpa", "33");
        if (sectionFields.Get("classe_cls") != "Personalizzato") throw new Exception("Resistenza personalizzata non conservata");
        sectionFields.Set("gamma_c", "");
        if (sectionFields.Get("__fcd") != "—") throw new Exception("fcd obsoleto con input incompleto");
        sectionFields.Set("gamma_c", "1.5"); sectionFields.Set("fck_mpa", materialFck);
        await WaitForAutomatic();
        var surveyDiagram = Result.Array("sondaggi")[0]!;
        double hingeDepth = StratigraphyDrawing.ReactionLimit(surveyDiagram) ?? throw new Exception("Caso lungo senza cerniera interna");
        var physical = StratigraphyDrawing.PhysicalDiagram(surveyDiagram);
        if (physical.Length == 0 || physical.Max(r => r.D("z")) != hingeDepth ||
            !surveyDiagram.Array("diagrammi").Any(r => r.D("z") > hingeDepth))
            throw new Exception("Diagramma non limitato esattamente alla cerniera o dati motore alterati");
        var fixedDiagram = surveyDiagram.DeepClone(); fixedDiagram["cerniere_m"]!.AsArray().Insert(0, JsonValue.Create(0));
        if (StratigraphyDrawing.ReactionLimit(fixedDiagram) != hingeDepth) throw new Exception("Taglio alla cerniera di testa anziché interna");
        foreach (string mechanism in new[] { "Corto", "Intermedio" }) {
            fixedDiagram["meccanismo"] = mechanism;
            if (StratigraphyDrawing.PhysicalDiagram(fixedDiagram).Length != fixedDiagram.Array("diagrammi").Count)
                throw new Exception("Diagramma corto/intermedio troncato");
        }
        fixedDiagram["meccanismo"] = "Lungo"; fixedDiagram["coesivo"] = true;
        if (StratigraphyDrawing.ReactionLimit(fixedDiagram) != hingeDepth || StratigraphyDrawing.PhysicalDiagram(fixedDiagram).Max(r => r.D("z")) != hingeDepth)
            throw new Exception("Diagramma coesivo non limitato alla cerniera interna");
        if (!ReferenceEquals(profile.HorizontalResult, Result) || grids[0].Rows[0].Values.S("__kp") != 3d.ToString("F1"))
            throw new Exception("Profilo risultati o Kp non aggiornati");
        var kpRow = grids[0].Rows[0];
        kpRow["angolo_attrito"] = "0";
        if (kpRow.Values.S("__kp") != 1d.ToString("F1") || profile.HorizontalResult is not null)
            throw new Exception("Kp non ricalcolato o profilo obsoleto");
        kpRow["tipologia"] = "Coesivo";
        if (kpRow.Values.S("__kp") != "—") throw new Exception("Kp mostrato per terreno coesivo");
        kpRow["tipologia"] = "Granulare"; kpRow["angolo_attrito"] = "30";
        await WaitForAutomatic();
        double initialResistance = Result.D("resistenza_progetto_manuale_kn");
        efficiency.Set("efficienza_eta", "0.5"); await WaitForAutomatic();
        if (Result is null || Math.Abs(Result.D("resistenza_progetto_manuale_kn") - initialResistance * .5) > 1e-8)
            throw new Exception("Efficienza manuale non applicata automaticamente");
        efficiency.Set("efficienza_eta", "1");
        ((ComboBox)efficiency.Editors["efficienza_metodo"]).SelectedItem = "Reese & Van Impe (foglio)";
        foreach (string key in new[] { "interasse_anteriore", "interasse_posteriore", "interasse_sinistro", "interasse_destro" }) efficiency.Set(key, "3");
        await WaitForAutomatic();
        if (Result is null || Result["efficienza"].D("eta") >= 1 || efficiency.Editors["efficienza_eta"].IsEnabled)
            throw new Exception("Metodo efficienza foglio non attivo");
        ((ComboBox)efficiency.Editors["efficienza_metodo"]).SelectedItem = "Manuale";
        await WaitForAutomatic();
        string initialVerticals = factors.Get("verticali_indagate");
        ((ComboBox)factors.Editors["verticali_indagate"]).SelectedItem = "2";
        await WaitForAutomatic();
        if (Result?.D("xi3") != 1.65 || Result.D("xi4") != 1.55 || Result.D("gamma_r") != 1.3 ||
            factors.Get("__xi3") != 1.65.ToString("F2") || factors.Get("__xi4") != 1.55.ToString("F2"))
            throw new Exception("Selezione coefficienti non aggiorna campi e calcolo");
        ((ComboBox)factors.Editors["verticali_indagate"]).SelectedItem = initialVerticals;
        await WaitForAutomatic();
        general.Set("azione_orizzontale", "101");
        var inFlight = CalculateAsync();
        general.Set("azione_orizzontale", "102");
        if (!layout.IsEnabled || !general.Editors["azione_orizzontale"].IsEnabled) throw new Exception("Calcolo disabilita gli input");
        await inFlight;
        if (Result is not null) throw new Exception("Risultato superato pubblicato dopo nuove modifiche");
        await WaitForAutomatic();
        if (Result?.D("azione_kn") != 102 || !JsonNode.DeepEquals(Result, PaloOrizzontale.Calculate(Data))) throw new Exception("Risultato automatico non corrisponde agli ultimi dati");
        sectionFields.Set("fck_mpa", "40"); await WaitForAutomatic();
        if (Result is null || Result.D("momento_resistente_knm") == initialMoment || !momentValue.Text.StartsWith("My =")) throw new Exception("Momento non ricalcolato automaticamente");
        sectionFields.Set("fck_mpa", concrete);
        general.Set("diametro", ""); await WaitForAutomatic();
        if (Result is not null || details.IsEnabled || csv.IsEnabled || !momentValue.Text.StartsWith("Momento non disponibile")) throw new Exception("Risultati obsoleti con input incompleti");
        general.Set("diametro", diameter); general.Set("azione_orizzontale", action); await WaitForAutomatic();
        if (Result is null) throw new Exception("Ricalcolo non riparte dopo correzione");
        string thickness = grids[0].Rows[0].Values.S("spessore"); grids[0].Rows[0]["spessore"] = "0";
        await WaitForAutomatic();
        if (Result is not null || !momentValue.Text.StartsWith("My =")) throw new Exception("Momento non aggiornato con stratigrafia incompleta");
        grids[0].Rows[0]["spessore"] = thickness; await WaitForAutomatic();
        var independent = new HorizontalWorkspace((JsonObject)Data.DeepClone());
        var pending = independent.CalculateAsync(); independent.Dispose(); await pending;
        if (independent.Result is not null || independent.timer.IsEnabled) throw new Exception("Calcolo pubblicato dopo Dispose");
    }

    internal async Task VerifyLayout(string directory, string size)
    {
        void Check()
        {
            if (cards[6].Visibility == Visibility.Visible)
            {
                if (Ui.Descendants<TabControl>(cards[6]).Any()) throw new Exception("Sezione ancora in una sottoscheda");
                FrameworkElement drawing = MicroHorizontal ? chsDrawing : sectionDrawing;
                var drawingBounds = drawing.TransformToAncestor(sectionLayout).TransformBounds(new Rect(0, 0, drawing.ActualWidth, drawing.ActualHeight));
                if (drawingBounds.Width <= 0 || drawingBounds.Left < sectionLayout.ColumnDefinitions[0].ActualWidth - 1 ||
                    drawingBounds.Right > sectionLayout.ActualWidth + 1 || drawingBounds.Bottom > sectionLayout.ActualHeight + 1)
                    throw new Exception("Sezione non ridimensionata a destra dei dati");
            }
            if (scroll.ExtentWidth > scroll.ViewportWidth + 1) throw new Exception("Scorrimento orizzontale esterno");
            if (ActualWidth > ((FrameworkElement)Window.GetWindow(this).Content).ActualWidth + 1) throw new Exception("Modulo più largo della finestra");
            var bounds = cards.Where(c => c.Visibility == Visibility.Visible).Select(c => new Rect(Canvas.GetLeft(c), Canvas.GetTop(c), c.Width, c.Height)).ToArray();
            for (int i = 0; i < bounds.Length; i++)
            {
                if (bounds[i].Left < 0 || bounds[i].Right > layout.Width + 1) throw new Exception("Pannello fuori finestra");
                for (int j = i + 1; j < bounds.Length; j++) if (bounds[i].IntersectsWith(bounds[j])) throw new Exception("Schede sovrapposte");
            }
        }
        expanded = -1; LayoutCards(); scroll.ScrollToTop();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); Check();
        if (cards.Count != 7 || model.Editors.ContainsKey("modalita")) throw new Exception("Numero schede o modello errato");
        if (!Ui.Descendants<TextBlock>(cards[1]).Any(t => t.Text == "Efficienza") ||
            !Ui.Descendants<TextBlock>(cards[2]).Any(t => t.Text == "Coefficienti normativa") || factors.Editors.ContainsKey("efficienza_metodo"))
            throw new Exception("Efficienza non separata dalla normativa nell'ordine richiesto");
        File.WriteAllBytes(Path.Combine(directory, $"orizzontale_{size}.png"), Ui.Snapshot(Window.GetWindow(this)));
        var verificationScroll = Ui.Descendants<ScrollViewer>(cards[3]).First(s => s.Content is StackPanel);
        verificationScroll.ScrollToBottom(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        if (!Ui.Descendants<FrameworkElement>(cards[3]).Any(e => Equals(e.Tag, "horizontal-calculation-description")))
            throw new Exception("Sintesi calcolo mancante");
        File.WriteAllBytes(Path.Combine(directory, $"orizzontale_{size}_sintesi.png"), Ui.Snapshot(Window.GetWindow(this)));
        verificationScroll.ScrollToTop();
        var tabHeader = Ui.Descendants<TabPanel>(surveys).Single();
        var addSurvey = Ui.Descendants<Button>(surveys).Single(b => b.Content?.ToString() == "+ Stratigrafia");
        double tabCenter = tabHeader.TranslatePoint(new Point(0, tabHeader.ActualHeight / 2), surveys).Y;
        double buttonCenter = addSurvey.TranslatePoint(new Point(0, addSurvey.ActualHeight / 2), surveys).Y;
        if (Math.Abs(tabCenter - buttonCenter) > 6) throw new Exception("Sondaggi e comandi non allineati");
        advanced.IsExpanded = true; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); Check();
        if (!model.Editors["passo"].IsVisible || !model.Editors["tolleranza"].IsVisible) throw new Exception("Opzioni avanzate non accessibili");
        File.WriteAllBytes(Path.Combine(directory, $"orizzontale_{size}_avanzate.png"), Ui.Snapshot(Window.GetWindow(this)));
        advanced.IsExpanded = false; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        scroll.ScrollToBottom(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        if (scroll.VerticalOffset + scroll.ViewportHeight < layout.Height - 1) throw new Exception("Ultima scheda non raggiungibile");
        File.WriteAllBytes(Path.Combine(directory, $"orizzontale_{size}_fondo.png"), Ui.Snapshot(Window.GetWindow(this)));
        foreach (int index in expandButtons.Keys)
        {
            expanded = index; LayoutCards(); scroll.ScrollToTop();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); Check();
            File.WriteAllBytes(Path.Combine(directory, $"orizzontale_{size}_esteso_{index}.png"), Ui.Snapshot(Window.GetWindow(this)));
        }
        expanded = -1; LayoutCards(); scroll.ScrollToTop(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    }

    private async Task VerifyEditing(string directory)
    {
        string original = Data.ToJsonString(); var backup = Data.Array("stratigrafie").DeepClone();
        var expected = Result!.DeepClone();
        expanded = 4; LayoutCards(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var grid = grids[0]; var row = grid.Rows[0];
        var input = Ui.Descendants<TextBox>(grid).First(t => ReferenceEquals(t.DataContext, row) && System.Windows.Automation.AutomationProperties.GetName(t) == "Spessore [m]");
        string thickness = input.Text; input.Focus(); input.Text = thickness + " ";
        await WaitForAutomatic();
        if (!input.IsKeyboardFocused || input.Text != thickness + " " || row.Values.S("spessore") != input.Text) throw new Exception("Digitazione interrotta");
        input.Text = thickness; await WaitForAutomatic();
        if (row.Values.S("__color") != StratigraphyDrawing.LayerColors[0] || row.Values.S("__strato") != "A") throw new Exception("Colore/nome strato errato");
        if (Ui.Descendants<CheckBox>(grid).Any()) throw new Exception("Opzione laterale non applicabile al modulo orizzontale");
        int count = grid.Rows.Count;
        var add = Ui.Descendants<Button>(surveys).First(b => Equals(b.Content, "Aggiungi strato"));
        add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var middle = grid.Rows[count]; var last = grid.Rows[count + 1];
        if (middle.Values.S("spessore") != "0") throw new Exception("Nuovo strato non nullo");
        Ui.Descendants<Button>(grid).First(b => Equals(b.Content, "[−]") && ReferenceEquals(b.DataContext, middle)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (grid.Rows.Contains(middle) || !ReferenceEquals(grid.Rows[^1], last)) throw new Exception("Eliminazione strato errata");
        if (!CopySurvey(0, -1, false)) throw new Exception("Copia in nuova stratigrafia fallita");
        var all = Data.Array("stratigrafie");
        if (!JsonNode.DeepEquals(all[0], all[1])) throw new Exception("Copia non identica");
        all[1]![0]!["spessore"] = "123";
        if (all[0]![0].S("spessore") == "123") throw new Exception("Copia non indipendente");
        CopySurvey(0, 1, false);
        if (!JsonNode.DeepEquals(all[0], all[1])) throw new Exception("Copia tra stratigrafie fallita");
        var keep = all[1]; DeleteSurvey(all[0]!.AsArray(), false);
        if (all.Count != 1 || !ReferenceEquals(all[0], keep)) throw new Exception("Eliminazione stratigrafia errata");
        Data["stratigrafie"] = backup; RebuildSurveys(); Preview(); expanded = -1; LayoutCards();
        if (Data.ToJsonString() != original) throw new Exception("Metadati di presentazione salvati nei dati");
        await WaitForAutomatic();
        if (!JsonNode.DeepEquals(Result, expected)) throw new Exception("Presentazione modifica il risultato");
        var actual = Result;
        foreach (var rd in new double?[] { null, 50, 200 })
        {
            Result = (JsonObject)actual!.DeepClone(); Result["azione_kn"] = 100; Result["resistenza_progetto_manuale_kn"] = rd;
            UpdateVerification();
            var label = ((StackPanel)verification.Content).Children.OfType<TextBlock>().Single();
            string expectedColor = rd is null ? Ui.Muted.ToString() : rd < 100 ? "#FFB42318" : "#FF16703C";
            if (label.Foreground.ToString() != expectedColor) throw new Exception("Colore esito errato");
        }
        Result = actual; UpdateVerification();
        if (Ui.Descendants<ComboBox>(this).Any(c => c.Items.Cast<object>().Any(v => v is string s && string.IsNullOrWhiteSpace(s)))) throw new Exception("Opzione vuota");
        File.WriteAllBytes(Path.Combine(directory, "orizzontale_verifica.png"), Ui.Snapshot(this));
    }
}
