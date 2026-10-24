using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Anthea.Application.Presentation;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules.VerticalPile;
using X.Core;
using X.Desktop.Views.VerticalPile;

namespace X.Desktop;

/// <summary>
/// f5-suite-ui: the checks of ui/smoke and ui/smoke-display on the vertical pile and micropile opened with the XAML view
/// (<see cref="VerticalPileSheetView"/> hosted by <see cref="MvvmSheetEditor"/>): the same checks as the SheetEditor partials of
/// SmokeChecks.cs (VerifyWpf, WaitForAutomatic, SetGeometryForSmoke) and DisplaySmokeChecks.cs (VerifyDisplayLayout, VerifyDisplayScrolling,
/// VerifyStratigraphyEditing, VerifyPileOutcomes, VerifyPresentation, VerifyReport, VerifyNavigationThrough), driven through the controls of the
/// view as the user (fields, cells, choices, buttons «Estendi», «+», [−], «Copia in…», «Copia da…», «Aggiungi strato») and read back from the
/// view and its ViewModel. The questions and the dialog of the copy are answered through the services of the sheets of the window
/// (<see cref="BenchSheetServices"/>), where the historic suites called the methods of the view with <c>confirm: false</c>.
/// The messages are those of the historic checks. At the Release switch of the vertical piles this class stays and the historic
/// partials go away (docs/refactoring/f5-suite-ui.md).
/// </summary>
internal sealed class VerticalPileSmoke
{
    private static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it-IT");
    private readonly HostedSheet editor;
    private readonly BenchSheetServices services;

    /// <param name="editor">The sheet in MainWindow (MvvmSheetEditor) or in a tab of the new shell (F5.13 e).</param>
    internal VerticalPileSmoke(HostedSheet editor, BenchSheetServices services)
    {
        this.editor = editor; this.services = services;
        Sheet = (VerticalPileSheetViewModel)editor.Sheet;
    }

    /// <summary>The open sheet is a vertical pile or micropile with the XAML view.</summary>
    internal static bool Handles(ISheetEditor? editor) => editor is MvvmSheetEditor { Sheet: VerticalPileSheetViewModel };

    internal VerticalPileSheetViewModel Sheet { get; }

    internal VerticalPileSheetView View => Ui.Descendants<VerticalPileSheetView>(editor.View).Single();

    private bool Micro => Sheet.IsMicro;

    /// <summary>The pile, not the micropile (Pile of SheetEditor).</summary>
    private bool Pile => !Sheet.IsMicro;

    private JsonObject Data => editor.Data;

    private JsonObject? Result => editor.Result;

    private Dispatcher Dispatcher => editor.View.Dispatcher;

    // ---------------------------------------------------------------- the controls of the view

    private T Editor<T>(FormViewModel form, string key) where T : Control
    {
        var field = form[key];
        return Ui.Descendants<T>(View).Single(e => ReferenceEquals(e.DataContext, field) && e.TemplatedParent is not ComboBox);
    }

    /// <summary>The text box of a field.</summary>
    internal TextBox Box(FormViewModel form, string key) => Editor<TextBox>(form, key);

    /// <summary>The raw text of a field, as InputForm.Get (the box shows it formatted without the focus).</summary>
    private static string Get(FormViewModel form, string key) => form.Get(key);

    /// <summary>The user types <paramref name="text"/> in the field (the text box writes at every keystroke).</summary>
    internal void Type(FormViewModel form, string key, string text) => Box(form, key).Text = text;

    /// <summary>The user chooses <paramref name="item"/> in a choice of a form.</summary>
    private void Choose(FormViewModel form, string key, string item) => Editor<ComboBox>(form, key).SelectedItem = item;

    /// <summary>The general geometry as SetGeometryForSmoke of SheetEditor: the diameter typed in «Dati generali».</summary>
    internal void SetGeometry(string value) => Type(Sheet.GeneralForm, "diametro", value);

    private DataGrid LayerGrid() => Ui.Descendants<DataGrid>(View.Surveys).Single(g => g is not VerticalPileTableGrid);

    private IReadOnlyList<JsonRowViewModel> Rows(int survey = 0) => Sheet.Surveys[survey].Rows;

    /// <summary>The control of a cell of the layer table, by the automation name of the column (as ui/smoke-display reads the historic table).</summary>
    private T Cell<T>(JsonRowViewModel row, string name) where T : Control =>
        Ui.Descendants<T>(LayerGrid()).First(t => ReferenceEquals(t.DataContext, row) && AutomationProperties.GetName(t) == name);

    private Button Command(DependencyObject root, string content) => Ui.Descendants<Button>(root).First(b => Equals(b.Content, content) && b.IsVisible);

    /// <summary>A click of the user: the Click handlers and the command of the button, when the button is usable.</summary>
    private static void Press(Button button)
    {
        if (!button.IsEnabled || !button.IsVisible) throw new InvalidOperationException("Pulsante non utilizzabile: " + button.Content);
        button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
        if (button.Command is { } command && command.CanExecute(button.CommandParameter)) command.Execute(button.CommandParameter);
    }

    private IReadOnlyList<FrameworkElement> Cards => View.Cards.Children.Cast<FrameworkElement>().ToArray();

    /// <summary>The «Estendi»/«Riduci» button of a card (4 «Stratigrafia», 5 «Profilo stratigrafico», 6 «Grafici capacità portante»).</summary>
    private Button ExpandButton(int card) => card switch { 4 => View.ExpandSurveys, 5 => View.ExpandProfile, 6 => View.ExpandCharts, _ => throw new ArgumentOutOfRangeException(nameof(card)) };

    /// <summary>The cards that extend, by their index in SheetEditor (expanded): 4, 5 and 6.</summary>
    private static readonly string[] CardNames = ["", "", "", "", VerticalPileSheetViewModel.SurveysCard, VerticalPileSheetViewModel.ProfileCard, VerticalPileSheetViewModel.ChartsCard];

    /// <summary>expanded = <paramref name="card"/> of SheetEditor (−1 none): «Riduci» of the extended card, then «Estendi» of <paramref name="card"/>.</summary>
    private async Task Expand(int card)
    {
        string wanted = card >= 0 ? CardNames[card] : "";
        if (Sheet.ExpandedCard == wanted) return;
        if (Sheet.ExpandedCard.Length > 0) { Press(ExpandButton(Array.IndexOf(CardNames, Sheet.ExpandedCard))); await Settle(); }
        if (card >= 0) { Press(ExpandButton(card)); await Settle(); }
        if (Sheet.ExpandedCard != wanted) throw new InvalidOperationException("«Estendi» non porta alla scheda " + card);
    }

    /// <summary>The layers of <paramref name="now"/> hold every value of those of <paramref name="before"/> (a row written by the table also stores its shown defaults, «Laterale» and Nc).</summary>
    private static bool SameLayers(JsonArray before, JsonArray now) =>
        before.Count == now.Count && before.Zip(now).All(s => s.First!.AsArray().Count == s.Second!.AsArray().Count
            && s.First.AsArray().Zip(s.Second.AsArray()).All(l => l.First!.AsObject().All(p => JsonNode.DeepEquals(p.Value, l.Second![p.Key]))));

    private async Task Settle()
    {
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        editor.View.UpdateLayout();
    }

    private async Task Capture(string directory, string file, bool window = false)
    {
        await Settle();
        File.WriteAllBytes(Path.Combine(directory, file + ".png"), Ui.Snapshot(window ? Window.GetWindow(editor.View) : editor.View));
    }

    // ---------------------------------------------------------------- SmokeChecks.cs

    /// <summary>WaitForAutomatic of SheetEditor: the automatic recalculation (450 ms after the last edit) finished, within 10 s.</summary>
    internal Task WaitForAutomatic() => MvvmSheets.WaitForAutomatic(editor);

    /// <summary>VerifyWpf of SheetEditor for the vertical pile and micropile.</summary>
    internal async Task VerifyWpf(string directory, string name)
    {
        void Assert(bool value, string message) { if (!value) throw new Exception(name + ": " + message); }
        await Settle();
        var cards = Cards;
        Assert(cards.Count == 7, "Numero di pannelli diverso");
        Assert(cards.All(c => c.ActualWidth >= 100 && c.ActualHeight >= 100), "Pannelli troppo piccoli");
        var original = (JsonObject)Data.DeepClone(); string value = Get(Sheet.GeneralForm, "diametro");
        Type(Sheet.GeneralForm, "diametro", value + " "); Assert(Result is null, "Risultati non invalidati"); Type(Sheet.GeneralForm, "diametro", value);
        Assert(JsonNode.DeepEquals(original, Data), "Modifica/ripristino campo altera dati");
        if (Pile) await WaitForAutomatic(); else await editor.CalculateAsync();
        Assert(Result is not null, "Ricalcolo non riuscito");
        var unchanged = (JsonObject)Data.DeepClone(); View.CapacityView.SelectedIndex = 1; View.CapacityView.SelectedIndex = 0;
        Assert(JsonNode.DeepEquals(unchanged, Data), "Il filtro modifica gli input");
        await Expand(6); await Capture(directory, name + "_grafico_esteso");
        Assert(Cards.Take(6).All(c => c.Visibility == Visibility.Collapsed), "Espansione grafico");
        await Expand(-1); Assert(Cards.All(c => c.Visibility == Visibility.Visible), "Ripristino pannelli");
        View.Outputs.SelectedIndex = 2; await Expand(6); await Capture(directory, name + (Micro ? "_abachi" : "_nq")); View.Outputs.SelectedIndex = 0; await Expand(-1);
        if (Rows().Count > 0)
        {
            var row = Rows()[0]; string thickness = row.Values.S("spessore"); var cell = Cell<TextBox>(row, Micro ? "Spessore verticale [m]" : "Spessore S [m]");
            cell.Text = thickness + " "; Assert(Result is null, "Modifica cella non invalida"); cell.Text = thickness;
            if (Pile) await WaitForAutomatic(); else await editor.CalculateAsync();
            Assert(Result is not null, "Ricalcolo da tabella");
        }
        if (Pile)
        {
            View.Outputs.SelectedIndex = 1; View.TableChoice.SelectedIndex = 0;
            await Capture(directory, name + "_tabelle_compatte");
            var capacityTables = Ui.Descendants<DataGrid>(View.TableHost).ToArray();
            Assert(capacityTables.Length == 2 && capacityTables.All(t => t.Columns.Count == 4 && t.Items.Count == 3), "Riepilogo compatto drenate/non drenate");
            await Expand(6); await Capture(directory, name + "_tabelle_compatte_estese"); await Expand(-1);
            View.Outputs.SelectedIndex = 0;
            await Expand(4); await Capture(directory, name + "_stratigrafia_estesa"); await Expand(-1);
            string length = Get(Sheet.GeneralForm, "lunghezza"); Type(Sheet.GeneralForm, "lunghezza", "abc"); await WaitForAutomatic(); await Settle();
            Assert(Result is null && View.CapacityPlot.Series.Count == 0, "Input invalido conserva risultati");
            Type(Sheet.GeneralForm, "lunghezza", "3"); Type(Sheet.GeneralForm, "lunghezza", length); await WaitForAutomatic(); Assert(Result is not null, "Ripristino calcolo automatico");
            // A second stratigraphy, copy of the first with φ′ = 32 in its last layer, selected («Copia in…» → «Nuova stratigrafia», then the cell).
            await CopySurvey(into: true, other: -1);
            Assert(Sheet.SelectedSurveyIndex == 1 && View.Surveys.SelectedIndex == 1, "Copia in nuova stratigrafia non selezionata");
            await Settle();
            Cell<TextBox>(Rows(1)[^1], "φ′ [°]").Text = "32"; await WaitForAutomatic(); await Settle();
            Assert(View.ProfileDrawing.VisibleIndices.SequenceEqual(new[] { 1 }), "Selezione profilo");
            Assert(View.ReferencePlot.Markers.Count == 1 && View.ReferencePlot.Markers[0].X == 32, "Selezione Nq");
            await Expand(5); Assert(View.ProfileDrawing.VisibleIndices.Length == 2 && Cards[5].Visibility == Visibility.Visible, "Espansione profilo"); await Capture(directory, name + "_profili");
            await Expand(-1); await RemoveSurvey(1, selectFirst: true); await WaitForAutomatic();
        }
        Assert(Result is not null, "Risultato finale assente");
    }

    /// <summary>«Copia in…» (<paramref name="into"/>) or «Copia da…» of the selected stratigraphy, choosing <paramref name="other"/> (−1: a new one) in the dialog; a replacement is confirmed.</summary>
    private async Task CopySurvey(bool into, int other)
    {
        var questions = new List<string>();
        services.Dialog = dialog =>
        {
            var copy = (SurveyCopyDialogViewModel)dialog;
            copy.Selected = copy.Choices.Single(c => c.Survey == other);
            copy.ConfirmCommand.Execute(null);
            return copy.Result;
        };
        services.Confirmations.Delete = (title, question) => { questions.Add(title + ": " + question); return true; };
        try { Press(Command(View, into ? "Copia in…" : "Copia da…")); }
        finally { services.Dialog = null; services.Confirmations.Delete = null; }
        if (questions.Count != (into && other == -1 ? 0 : 1)) throw new InvalidOperationException("Domande della copia inattese: " + string.Join(" | ", questions));
        await Settle();
    }

    /// <summary>The [−] of the tab of the stratigraphy <paramref name="index"/>, confirmed (DeleteSurvey of SheetEditor without the question).</summary>
    private async Task RemoveSurvey(int index, bool selectFirst)
    {
        if (selectFirst) { View.Surveys.SelectedIndex = 0; await Settle(); }
        var tab = (TabItem)View.Surveys.ItemContainerGenerator.ContainerFromIndex(index);
        string? asked = null;
        services.Confirmations.Delete = (title, question) => { asked = title + ": " + question; return true; };
        try { Press(Ui.Descendants<Button>(tab).Single(b => Equals(b.Tag, "elimina-stratigrafia"))); }
        finally { services.Confirmations.Delete = null; }
        if (asked != $"Stratigrafia: Eliminare la stratigrafia {index + 1} e tutti i suoi strati?") throw new InvalidOperationException("Domanda dell'eliminazione della stratigrafia diversa: " + asked);
        await Settle();
    }

    // ---------------------------------------------------------------- DisplaySmokeChecks.cs

    /// <summary>VerifyDisplayLayout of SheetEditor: no outer horizontal scroll, the visible cards inside the panel and not overlapped.</summary>
    internal void VerifyDisplayLayout()
    {
        var scroll = View.Scroll; var panel = View.Cards;
        if (scroll.ScrollableWidth > 1 || panel.ActualWidth > scroll.ViewportWidth + 1)
            throw new Exception("Il modulo richiede ancora scorrimento orizzontale esterno");
        var bounds = Cards.Where(c => c.Visibility == Visibility.Visible).Select(c => LayoutInformation.GetLayoutSlot(c)).ToArray();
        for (int i = 0; i < bounds.Length; i++)
        {
            if (bounds[i].Right > panel.ActualWidth + 1 || bounds[i].Bottom > panel.ActualHeight + 1)
                throw new Exception("Pannello fuori dalla superficie scorrevole");
            for (int j = i + 1; j < bounds.Length; j++)
                if (bounds[i].IntersectsWith(bounds[j])) throw new Exception("Pannelli sovrapposti");
        }
    }

    /// <summary>VerifyDisplayScrolling of SheetEditor: the bottom reachable, then each card extended («Estendi») laid out as above.</summary>
    internal async Task VerifyDisplayScrolling(string directory, string name)
    {
        var scroll = View.Scroll;
        scroll.ScrollToBottom();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        if (scroll.VerticalOffset + scroll.ViewportHeight < View.Cards.ActualHeight - 1)
            throw new Exception("La parte inferiore del modulo non è raggiungibile");
        File.WriteAllBytes(Path.Combine(directory, name + "_fondo.png"), Ui.Snapshot(Window.GetWindow(editor.View)));
        foreach (int index in new[] { 4, 5, 6 })
        {
            await Expand(index);
            VerifyDisplayLayout();
            // The extended stratigraphy keeps the profile at its right when the width allows it (ExtendedStratigraphy, 22/10/2026).
            if (index == 4 && ExtendedStratigraphy.Split(View.Cards.ActualWidth - 32, 12) is not null
                && (Cards[5].Visibility != Visibility.Visible || LayoutInformation.GetLayoutSlot(Cards[5]).Left <= LayoutInformation.GetLayoutSlot(Cards[4]).Right))
                throw new Exception("Profilo stratigrafico non visibile accanto alla stratigrafia estesa");
            File.WriteAllBytes(Path.Combine(directory, name + $"_esteso_{index}.png"), Ui.Snapshot(Window.GetWindow(editor.View)));
        }
        await Expand(-1); scroll.ScrollToTop();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        VerifyDisplayLayout();
    }

    /// <summary>VerifyReport of SheetEditor: the Word report of the sheet with the chosen sections, and the image of the profile.</summary>
    internal void VerifyReport(string directory)
    {
        string path = Path.Combine(directory, "relazione-prova.docx");
        editor.ExportReport(path, "Verifica impaginazione", new HashSet<string> { "generali", "efficienza", "coefficienti", "stratigrafia", "nq", "risultati", "grafico_nq" });
        DisplayReportChecks.Verify(path, Micro, Data);
        File.WriteAllBytes(Path.Combine(directory, "profilo-palo.png"), View.ProfileDrawing.Png(720, 900));
    }

    /// <summary>VerifyNavigationThrough of SheetEditor: leaving the sheet and coming back keeps data, result and the state of the cards.</summary>
    internal async Task VerifyNavigationThrough(Action leave, Action resume)
    {
        string oldExpanded = Sheet.ExpandedCard; int oldTab = View.Outputs.SelectedIndex, oldCapacity = View.CapacityView.SelectedIndex;
        try
        {
            await Expand(6); View.Outputs.SelectedIndex = 2; View.CapacityView.SelectedIndex = 1;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            string data = Data.ToJsonString(); var result = Result;
            leave(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            resume(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            if (Data.ToJsonString() != data || !ReferenceEquals(Result, result) || Sheet.ExpandedCard != VerticalPileSheetViewModel.ChartsCard || View.Outputs.SelectedIndex != 2 || View.CapacityView.SelectedIndex != 1)
                throw new Exception("La navigazione Home/modulo ha perso input, risultati o stato dei pannelli");
        }
        finally
        {
            if (Sheet.ExpandedCard != oldExpanded) Sheet.ToggleCardCommand.Execute(oldExpanded.Length > 0 ? oldExpanded : Sheet.ExpandedCard);
            View.Outputs.SelectedIndex = oldTab; View.CapacityView.SelectedIndex = oldCapacity;
        }
    }

    /// <summary>VerifyPresentation of SheetEditor: copies of the stratigraphies, colours of the curves, menus, compact tables, Nq projections.</summary>
    internal async Task VerifyPresentation(string directory)
    {
        var backup = Data.Array("stratigrafie").DeepClone();
        await CopySurvey(into: true, other: -1);
        var surveys = Data.Array("stratigrafie");
        if (surveys.Count != 2) throw new Exception("Copia in nuova stratigrafia fallita");
        if (!JsonNode.DeepEquals(surveys[0], surveys[1])) throw new Exception("La copia non conserva tutti i dati");
        string thickness = Rows(1)[0].Values.S("spessore");
        Cell<TextBox>(Rows(1)[0], Micro ? "Spessore verticale [m]" : "Spessore S [m]").Text = "11";
        if (surveys[0]![0].S("spessore") == "11") throw new Exception("Le stratigrafie copiate non sono indipendenti");
        View.Surveys.SelectedIndex = 0; await Settle();
        await CopySurvey(into: false, other: 1);
        if (!JsonNode.DeepEquals(surveys[0], surveys[1])) throw new Exception("Copia da altra stratigrafia fallita");
        // The stratigraphies as before, through the view: the thickness typed again in the first one, the copy removed.
        Cell<TextBox>(Rows(0)[0], Micro ? "Spessore verticale [m]" : "Spessore S [m]").Text = thickness;
        await RemoveSurvey(1, selectFirst: true);
        if (!SameLayers(backup.AsArray(), Data.Array("stratigrafie"))) throw new Exception("Stratigrafie non ripristinate dopo le copie");
        await WaitForAutomatic();
        var project = Sheet.Curves.Where(c => c.Key.StartsWith("progetto_") || c.Key.StartsWith("azione_")).Select(c => PlotPresentation.Series(c.Series)).ToArray();
        if (project.Select(s => s.Color.ToString()).Distinct().Count() != project.Length) throw new Exception("Colori delle curve non distinti");
        await Expand(4);
        if (Ui.Descendants<ComboBox>(editor.View).Any(c => c.Items.Cast<object>().Any(v => v is string s && string.IsNullOrWhiteSpace(s)))) throw new Exception("Opzione vuota nei menu");
        await Expand(6); View.Outputs.SelectedIndex = 1; View.TableChoice.SelectedIndex = 0;
        await Settle();
        var tables = Ui.Descendants<DataGrid>(View.TableHost).ToArray();
        if (tables.Length != (Micro ? 1 : 2) || tables.Any(t => t.ActualWidth > 621)) throw new Exception("Tabelle riepilogo troppo larghe");
        foreach (var table in tables)
            foreach (var row in table.Items.Cast<string[]>())
                foreach (var value in row.Skip(1))
                    if (!Regex.IsMatch(value, @"^-?\d+,\d$")) throw new Exception("Numero non visualizzato con un decimale: " + value);
        await Capture(directory, "tabelle_estese", window: true);
        View.Outputs.SelectedIndex = 2; await Settle();
        if (!View.ReferencePlot.Markers.Any(m => m.ProjectToAxes)) throw new Exception("Proiezioni Nq mancanti");
        await Capture(directory, "nq_riferimenti", window: true);
        View.Outputs.SelectedIndex = 0; await Capture(directory, "curve_colori", window: true);
        await Expand(-1);
    }

    /// <summary>
    /// VerifyPileOutcomes of SheetEditor. The historic check replaced the result of the view and called UpdateVerification, which shows
    /// <see cref="VerticalPilePresentation.Verification"/>; the result of a ViewModel comes only from its calculation. Here: the same synthetic
    /// results through the same function (order and outcome at 99, 100 and 101 kN, incomplete coverage, no action), and the table of the view
    /// showing exactly the rows of that function for the result of the sheet.
    /// </summary>
    internal void VerifyPileOutcomes()
    {
        string[] keys = Micro ? ["compressione", "trazione"] : ["drenante_compressione", "non_drenante_compressione", "drenante_trazione", "non_drenante_trazione"];
        JsonObject Synthetic(double action)
        {
            var curves = new JsonObject();
            foreach (string key in keys) curves[key] = J.Obj(("progetto", new JsonArray(new JsonArray(1, 100))));
            return J.Obj(("copertura_completa", true), ("curve", curves),
                ("azioni", J.Obj(("compressione", new JsonArray(new JsonArray(1, action))), ("trazione", new JsonArray(new JsonArray(1, action))))));
        }
        foreach (var (action, expected) in new[] { (99.0, "Verifica soddisfatta"), (100.0, "Verifica soddisfatta"), (101.0, "Verifica non soddisfatta") })
        {
            var rows = VerticalPilePresentation.Verification(Synthetic(action), Micro);
            if (!rows.Select(r => r[0]).SequenceEqual(Micro ? new[] { "Compressione", "Trazione" } : new[] { "Comp – Dre", "Comp – Non Dre", "Tra – Dre", "Tra – Non Dre" }) || rows.Any(r => r[4] != expected))
                throw new Exception("Ordine o esito della verifica errato");
        }
        var incomplete = Synthetic(99); incomplete["copertura_completa"] = false;
        if (VerticalPilePresentation.Verification(incomplete, Micro).Any(r => r[4] != (Micro ? "Verifica incompleta: stratigrafia insufficiente" : "Da verificare")))
            throw new Exception("Esito definitivo con copertura incompleta");
        var noAction = Synthetic(99); noAction["azioni"]!["compressione"] = new JsonArray();
        if (VerticalPilePresentation.Verification(noAction, Micro).Take(Micro ? 1 : 2).Any(r => r[4] != (Micro ? "Azione non inserita" : "Da verificare")))
            throw new Exception("Esito definitivo senza azione");
        var grid = Micro ? View.MicroVerification : View.PileVerification;
        var shown = ((IEnumerable<string[]>)grid.ItemsSource).Select(r => string.Join("|", r)).ToArray();
        var expectedRows = VerticalPilePresentation.Verification(Result, Micro).Select(r => string.Join("|", r)).ToArray();
        if (!grid.IsVisible || !shown.SequenceEqual(expectedRows)) throw new Exception("La tabella della verifica non mostra le righe del risultato");
    }

    /// <summary>VerifyStratigraphyEditing of SheetEditor: typing during the recalculation, calculated columns, γsat, γb of the technology, layers and stratigraphies.</summary>
    internal async Task VerifyStratigraphyEditing()
    {
        var initial = Archivio.NuovoFoglio(editor.Module).Array("stratigrafie")[0]!.AsArray();
        if (initial.Count != 1 || initial[0].S("spessore") != "0" || (Micro ? initial[0].S("alpha") != "0" || initial[0]!["nc"] is not null : initial[0].S("nc") != "9")) throw new Exception("Valori iniziali dello strato errati");
        await Expand(4);
        var input = Cell<TextBox>(Rows()[0], Micro ? "Spessore verticale [m]" : "Spessore S [m]");
        string value = input.Text;
        input.Focus(); input.Text = value + " "; input.CaretIndex = input.Text.Length;
        await WaitForAutomatic();
        if (!input.IsKeyboardFocused || input.Text != value + " " || !input.IsEnabled) throw new Exception("Il ricalcolo interrompe la digitazione");
        input.Text = value; await WaitForAutomatic();
        var general = Sheet.GeneralForm;
        if (Micro)
        {
            var weightInput = Box(general, "__peso_lineare");
            if (!weightInput.IsReadOnly) throw new Exception("Peso lineare modificabile");
            string originalDiameter = Get(general, "diametro"), originalProfile = Get(general, "profilo_chs");
            foreach (var diameter in new[] { "0.24", "0.4" })
                foreach (var profile in new[] { "CHS 114.3 × 6.3", "CHS 139.7 × 8" })
                {
                    Type(general, "diametro", diameter); Choose(general, "profilo_chs", profile);
                    string expected = Chs.Peso(profile, Data["generali"].D("diametro"), Data["generali"].D("peso_specifico_palo", 25)).D("q_totale").ToString("0.0", Italian);
                    if (weightInput.Text != expected) throw new Exception("Peso lineare non aggiornato con diametro/profilo");
                }
            Type(general, "diametro", "0.01");
            if (weightInput.Text != "—") throw new Exception("Peso disponibile con geometria invalida");
            Type(general, "diametro", originalDiameter); Choose(general, "profilo_chs", originalProfile); await WaitForAutomatic();
            var layer = Rows()[0]; string soil = layer.Values.S("terreno"), alpha = layer.Values.S("alpha");
            var choice = Cell<ComboBox>(layer, "Terreno");
            choice.SelectedItem = "Ghiaia";
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var alphaInput = Cell<TextBox>(layer, "α adottato");
            if (alphaInput.Text != layer.Values.S("alpha") || layer.Values.D("alpha") != 1.8) throw new Exception("α non aggiornato nella cella terreno");
            choice.SelectedItem = soil; alphaInput.Text = alpha;
            var tip = Editor<CheckBox>(general, "considera_punta"); bool? oldTip = tip.IsChecked;
            tip.IsChecked = false;
            if (Box(general, "percentuale_punta").IsEnabled || Box(Sheet.NormativeForm, "sicurezza_base").IsEnabled) throw new Exception("Parametri della punta non disabilitati");
            tip.IsChecked = oldTip; await WaitForAutomatic();
            await Settle();
            foreach (var segment in Result!.Array("dettagli")[^1]!.Array("sondaggi")[0]!.Array("tratti"))
            {
                int layerIndex = (int)segment.D("strato") - 1;
                string shown = segment.D("s").ToString("0.0", Italian);
                if (Rows()[layerIndex].Values.S("__tau") != shown || Cell<TextBox>(Rows()[layerIndex], "Aderenza τ [kPa]").Text != shown) throw new Exception("τ visualizzata diversa dall’aderenza usata nel calcolo");
            }
            var tauInput = Cell<TextBox>(layer, "Aderenza τ [kPa]");
            if (!tauInput.IsReadOnly || tauInput.Text != layer.Values.S("__tau")) throw new Exception("Colonna τ non aggiornata o modificabile");
            string pressure = Get(general, "pressione_iniezione");
            Type(general, "pressione_iniezione", "4");
            if (layer.Values.S("__tau") != "450,0") throw new Exception("τ non aggiornata con la pressione");
            var lateral = Ui.Descendants<CheckBox>(LayerGrid()).First(c => ReferenceEquals(c.DataContext, layer) && Equals(c.Content, "Laterale"));
            lateral.IsChecked = false;
            if (layer.Values.S("__tau") != "0,0") throw new Exception("τ non nulla con laterale esclusa");
            lateral.IsChecked = true; Type(general, "pressione_iniezione", "99");
            if (layer.Values.S("__tau") != "—") throw new Exception("τ disponibile fuori abaco");
            Type(general, "pressione_iniezione", pressure); await WaitForAutomatic();
        }
        if (!Micro)
        {
            var layer = Rows()[0]; string gamma = layer.Values.S("peso_specifico"), sat = layer.Values.S("peso_specifico_saturo");
            var satInput = Cell<TextBox>(layer, "γsat [kN/m³]"); var gammaInput = Cell<TextBox>(layer, "γ [kN/m³]");
            var hint = Ui.Descendants<TextBlock>(LayerGrid()).First(t => ReferenceEquals(t.DataContext, layer) && Equals(t.Tag, "gamma-sat-automatico"));
            // The grey of the historic hint (Appearance.Foreground(Brushes.Gray)) is the token Token.Disabled of the view (P7).
            var grey = ((SolidColorBrush)View.FindResource("Token.Disabled")).Color;
            foreach (string blank in new[] { "", "  " })
            {
                satInput.Text = blank; gammaInput.Text = "19,5";
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                if (hint.Visibility != Visibility.Visible || hint.Text != "19,5" || (hint.Foreground as SolidColorBrush)?.Color != grey || satInput.Text != blank || layer.Values.S("peso_specifico_saturo") != blank)
                    throw new Exception($"γsat automatico non visualizzato o memorizzato come valore esplicito (visibilità {hint.Visibility}, testo '{hint.Text}', colore {(hint.Foreground as SolidColorBrush)?.Color}, campo '{satInput.Text}', valore '{layer.Values.S("peso_specifico_saturo")}')");
                gammaInput.Text = "20";
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                if (hint.Text != "20") throw new Exception("γsat automatico non segue γ");
            }
            foreach (string explicitValue in new[] { "21", "0" })
            {
                satInput.Text = explicitValue;
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                if (hint.Visibility != Visibility.Collapsed || layer.Values.S("peso_specifico_saturo") != explicitValue) throw new Exception("γsat esplicito sostituito dal valore automatico");
            }
            satInput.Text = ""; await WaitForAutomatic(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            if (hint.Visibility != Visibility.Visible || layer.Values.S("peso_specifico_saturo") != "") throw new Exception("Ricalcolo perde γsat automatico");
            gammaInput.Text = gamma; satInput.Text = sat; await WaitForAutomatic();
            // D7-d: choosing a technology proposes its γb of NTC Tab. 6.4.II; the original choice and value are restored afterwards.
            var technology = Editor<ComboBox>(general, "tipo_palo"); object? oldTechnology = technology.SelectedItem; string oldBase = Get(Sheet.NormativeForm, "sicurezza_base");
            foreach (var (choice, expected) in new[] { ("Battuto", "1.15"), ("Elica continua", "1.30"), ("Trivellato", "1.35") })
            {
                technology.SelectedItem = choice; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                if (Data["generali"].S("tipo_palo") != choice || Data["generali"].S("sicurezza_base") != expected) throw new Exception("γb non aggiornato al cambio di tecnologia: " + choice);
            }
            technology.SelectedItem = oldTechnology; Type(Sheet.NormativeForm, "sicurezza_base", oldBase); await WaitForAutomatic();
        }
        var rows = Rows();
        int count = rows.Count;
        var add = Ui.Descendants<Button>(View.Surveys).First(b => Equals(b.Content, "Aggiungi strato"));
        Press(add); Press(add);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var middle = rows[count]; var last = rows[count + 1];
        Press(Ui.Descendants<Button>(LayerGrid()).First(b => Equals(b.Content, "[−]") && ReferenceEquals(b.DataContext, middle)));
        if (rows.Contains(middle) || !ReferenceEquals(rows[^1], last) || Data.Array("stratigrafie")[0]!.AsArray().Count != count + 1)
            throw new Exception("Eliminazione dello strato intermedio errata");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Press(Ui.Descendants<Button>(LayerGrid()).First(b => Equals(b.Content, "[−]") && ReferenceEquals(b.DataContext, last)));
        await WaitForAutomatic();
        // A second stratigraphy («+»), then removed with the [−] of its tab: the first one stays, selected.
        var surveys = Data.Array("stratigrafie"); var retained = surveys[0];
        Press(Command(View, "+")); await Settle();
        if (surveys.Count != 2) throw new Exception("Aggiunta della stratigrafia non riuscita");
        await RemoveSurvey(1, selectFirst: true);
        if (surveys.Count != 1 || !ReferenceEquals(surveys[0], retained) || View.Surveys.SelectedIndex != 0)
            throw new Exception("Eliminazione della singola stratigrafia errata");
        await WaitForAutomatic(); await Expand(-1);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    }
}

/// <summary>f5-suite-ui: the checks of the Word report of the vertical piles in ui/smoke-display (VerifyReport), shared by the two views.</summary>
internal static class DisplayReportChecks
{
    internal static void Verify(string path, bool micro, JsonObject data)
    {
        using var zip = System.IO.Compression.ZipFile.OpenRead(path);
        using var stream = zip.GetEntry("word/document.xml")!.Open();
        var xml = System.Xml.Linq.XDocument.Load(stream);
        System.Xml.Linq.XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        if (xml.Descendants(w + "t").Any(t => t.Value == "Avvisi" || t.Value == "sottotipo_palo_battuto")) throw new Exception("Contenuti non richiesti nella relazione");
        if (xml.Descendants(w + "tc").Any(c => !c.Descendants(w + "jc").Any(j => (string?)j.Attribute(w + "val") == "center"))) throw new Exception("Celle della relazione non centrate");
        if (zip.Entries.Count(e => e.FullName.StartsWith("word/media/")) != (micro ? 2 * data.Array("stratigrafie").Count : data.Array("stratigrafie").Count + 1)) throw new Exception("Immagini dei sondaggi o abachi mancanti");
        if (micro && xml.Descendants(w + "t").Any(t => new[] { "tipo_palo", "metodo_nq", "presenza_falda", "considera_sottospinta", "pressione_limite" }.Contains(t.Value))) throw new Exception("Dati non applicabili presenti nella relazione micropalo");
    }
}
