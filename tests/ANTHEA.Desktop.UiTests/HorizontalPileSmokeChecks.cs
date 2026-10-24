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
using Anthea.Application.Documents;
using Anthea.Application.Presentation;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules.HorizontalPile;
using Anthea.Presentation.Modules.VerticalPile;
using X.Core;
using X.Desktop.Views.HorizontalPile;

namespace X.Desktop;

/// <summary>
/// f5-suite-passaggi: the checks of ui/smoke-horizontal (and of the horizontal part of ui/smoke) on the horizontal pile and micropile opened with the
/// XAML view (<see cref="HorizontalPileSheetView"/> hosted by <see cref="MvvmSheetEditor"/>): the same checks as the historic partials
/// (HorizontalSmokeChecks.cs: WaitForHorizontalAutomatic, VerifyHorizontal, VerifyHorizontalLayout, VerifyChs; HorizontalLayoutChecks.cs,
/// HorizontalStratifiedSmokeChecks.cs and HorizontalChsChecks.cs on HorizontalWorkspace), driven through the controls of the view as the user (the
/// fields of the FieldForm, the cells and the commands of the stratigraphies, «Estendi»/«Riduci», «Opzioni avanzate», «Diagrammi e dettagli») and read
/// back from the view and its ViewModel, with the messages of the historic checks. Where the historic checks wrote the document directly (the two
/// stratigraphies of the stratified model and the restore after the edits) the data go to the sheet with <see cref="Anthea.Presentation.Modules.SheetViewModelBase.Reload"/>,
/// which builds the sheet again on them as RebuildSurveys of the historic view (question P1 of docs/refactoring/f5-suite-passaggi.md). The questions
/// and the dialog of the copies are answered through the services of the sheets of the window (<see cref="BenchSheetServices"/>). At the Release switch
/// of the two modules this class stays and the historic partials go away with the historic view.
/// </summary>
internal sealed class HorizontalPileSmoke
{
    private readonly HostedSheet editor;
    private readonly BenchSheetServices services;

    internal HorizontalPileSmoke(HostedSheet editor, BenchSheetServices services)
    {
        this.editor = editor; this.services = services;
        Sheet = (HorizontalPileSheetViewModel)editor.Sheet;
    }

    /// <summary>The open sheet is a horizontal pile or micropile with the XAML view.</summary>
    internal static bool Handles(ISheetEditor? editor) => editor is MvvmSheetEditor { Sheet: HorizontalPileSheetViewModel };

    internal HorizontalPileSheetViewModel Sheet { get; }

    internal HorizontalPileSheetView View => Ui.Descendants<HorizontalPileSheetView>(editor.View).Single();

    private bool Micro => Sheet.IsMicro;

    private JsonObject Data => editor.Data;

    /// <summary>The result of the capacity (Result of HorizontalWorkspace).</summary>
    private JsonObject? Result => Sheet.Result;

    private Dispatcher Dispatcher => editor.View.Dispatcher;

    private Window Window => Window.GetWindow(editor.View);

    // ---------------------------------------------------------------- the controls of the view

    /// <summary>The seven cards of the tab of the capacity, in the historic order.</summary>
    private IReadOnlyList<FrameworkElement> Cards => View.Cards.Children.Cast<FrameworkElement>().ToArray();

    /// <summary>
    /// The editor of a field of a form. The content of a closed expander is laid out only when it is opened: as the historic InputForm builds every
    /// editor, the closed expanders of the page are opened and closed again once (their state unchanged) when an editor is not there yet.
    /// </summary>
    private T Editor<T>(FormViewModel form, string key) where T : Control
    {
        var field = form[key];
        T? Find() => Ui.Descendants<T>(View).SingleOrDefault(e => ReferenceEquals(e.DataContext, field) && e.TemplatedParent is not ComboBox);
        if (Find() is { } found) return found;
        Realize();
        return Find() ?? throw new InvalidOperationException("Campo non disposto nella vista: " + key);
    }

    private void Realize()
    {
        for (int pass = 0; pass < 3; pass++)
        {
            var closed = Ui.Descendants<Expander>(View.CapacityPage).Where(e => !e.IsExpanded).ToList();
            if (closed.Count == 0) break;
            foreach (var expander in closed) expander.IsExpanded = true;
            Layout();
            foreach (var expander in closed) expander.IsExpanded = false;
            Layout();
        }
    }

    /// <summary>The editor of a field, whatever its kind (text box, choice or check box), as InputForm.Editors.</summary>
    private Control FieldEditor(FormViewModel form, string key)
    {
        var field = form[key];
        Control? Find() => Ui.Descendants<Control>(View).SingleOrDefault(e => ReferenceEquals(e.DataContext, field) && e is TextBox or ComboBox or CheckBox && e.TemplatedParent is not ComboBox);
        if (Find() is { } found) return found;
        Realize();
        return Find() ?? throw new InvalidOperationException("Campo non disposto nella vista: " + key);
    }

    /// <summary>The user types <paramref name="text"/> in the field (Set of InputForm; the text box writes at every keystroke). A read-only field refuses the typing.</summary>
    private void Type(FormViewModel form, string key, string text)
    {
        if (form[key].IsReadOnly) throw new InvalidOperationException("Digitazione in un campo di sola lettura: " + key);
        Editor<TextBox>(form, key).Text = text;
    }

    /// <summary>
    /// The module writes a read-only field of the material («fck del materiale», «γc archivio»: the user cannot type there), as InputForm.Set of the
    /// historic check: the same write and the same callbacks (<see cref="FormViewModel.Set"/>).
    /// </summary>
    private static void Write(FormViewModel form, string key, string text)
    {
        if (!form[key].IsReadOnly) throw new InvalidOperationException("Campo scrivibile dall'utente, da digitare: " + key);
        form.Set(key, text);
    }

    /// <summary>The user chooses <paramref name="item"/> in a choice of a form.</summary>
    private void Choose(FormViewModel form, string key, string item) => Editor<ComboBox>(form, key).SelectedItem = item;

    /// <summary>The raw text of a field, as InputForm.Get.</summary>
    private static string Get(FormViewModel form, string key) => form.Get(key);

    private DataGrid LayerGrid() => Ui.Descendants<DataGrid>(View.Surveys).Single();

    private IReadOnlyList<JsonRowViewModel> Rows(int survey = 0) => Sheet.Surveys[survey].Rows;

    /// <summary>The control of a cell of the table of the selected stratigraphy, by the automation name of its column.</summary>
    private T Cell<T>(JsonRowViewModel row, string name) where T : Control
    {
        var grid = LayerGrid();
        grid.ScrollIntoView(row); Layout();
        return Ui.Descendants<T>(grid).First(t => ReferenceEquals(t.DataContext, row) && AutomationProperties.GetName(t) == name);
    }

    private DataGridRow RowOf(JsonRowViewModel row)
    {
        var grid = LayerGrid();
        grid.ScrollIntoView(row); Layout();
        return (DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(row);
    }

    private Button StripButton(string content) => Ui.Descendants<Button>(View.Surveys).Single(b => Equals(b.Content, content));

    /// <summary>A click of the user: the Click handlers and the command of the button, when the button is usable.</summary>
    private static void Press(Button button)
    {
        if (!button.IsEnabled || !button.IsVisible) throw new InvalidOperationException("Pulsante non utilizzabile: " + button.Content);
        button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
        if (button.Command is { } command && command.CanExecute(button.CommandParameter)) command.Execute(button.CommandParameter);
    }

    /// <summary>The «Estendi»/«Riduci» button of a card (4 «Stratigrafia», 5 «Profilo stratigrafico», 6 the section), as expandButtons.</summary>
    private Button ExpandButton(int card) => card switch { 4 => View.ExpandSurveys, 5 => View.ExpandProfile, 6 => View.ExpandSection, _ => throw new ArgumentOutOfRangeException(nameof(card)) };

    private static readonly int[] Expandable = [4, 5, 6];

    private static readonly string[] CardNames = ["", "", "", "", HorizontalPileSheetViewModel.SurveysCard, HorizontalPileSheetViewModel.ProfileCard, HorizontalPileSheetViewModel.SectionCard];

    /// <summary>expanded = <paramref name="card"/> of the historic view (−1 none): «Riduci» of the extended card, then «Estendi» of <paramref name="card"/>.</summary>
    private async Task Expand(int card)
    {
        string wanted = card >= 0 ? CardNames[card] : "";
        if (Sheet.ExpandedCard == wanted) return;
        if (Sheet.ExpandedCard.Length > 0) { Press(ExpandButton(Array.IndexOf(CardNames, Sheet.ExpandedCard))); await Settle(); }
        if (card >= 0) { Press(ExpandButton(card)); await Settle(); }
        if (Sheet.ExpandedCard != wanted) throw new InvalidOperationException("«Estendi» non porta alla scheda " + card);
    }

    private void Layout() { Window.UpdateLayout(); editor.View.UpdateLayout(); }

    private async Task Settle()
    {
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Layout();
    }

    private void Snapshot(string directory, string file, FrameworkElement element) => File.WriteAllBytes(Path.Combine(directory, file), Ui.Snapshot(element));

    /// <summary>
    /// «Diagrammi e dettagli» pressed: the dialog of the view (HorizontalPileResultsView), shown without waiting (ShowDialog in the product), at the
    /// size of the historic dialog of the check when given.
    /// </summary>
    private async Task<(Window Window, HorizontalPileResultsView Results)> ShowDetails(double? width = null, double? height = null)
    {
        Window? shown = null;
        var previous = View.ShowWindow;
        View.ShowWindow = window =>
        {
            shown = window;
            if (width is { } w) window.Width = w;
            if (height is { } h) window.Height = h;
            window.Show();
            return null;
        };
        try { Press(View.DetailsButton); }
        finally { View.ShowWindow = previous; }
        if (shown is null) throw new Exception("«Diagrammi e dettagli» non ha aperto la finestra dei risultati");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        shown.UpdateLayout();
        return (shown, (HorizontalPileResultsView)shown.Content);
    }

    /// <summary>The data of the sheet replaced, the sheet built again on them (the historic check wrote the document and called RebuildSurveys).</summary>
    private void Reload(Action<JsonObject> change)
    {
        var data = (JsonObject)Data.DeepClone();
        change(data);
        Sheet.Reload(data);
        Layout();
    }

    // ---------------------------------------------------------------- HorizontalSmokeChecks.cs

    /// <summary>WaitForAutomatic of HorizontalWorkspace: the last automatic recalculation finished (each within 25 s), then the dispatcher idle.</summary>
    internal Task WaitForAutomatic() => WaitForAutomatic(editor.Sheet);

    /// <summary>WaitForAutomatic on a horizontal pile or micropile opened elsewhere with the XAML view (the report of ReportAppearanceChecks).</summary>
    internal static Task WaitForAutomatic(ISheetEditor opened) => WaitForAutomatic(((MvvmSheetEditor)opened).Sheet);

    /// <summary>The same on the ViewModel of the sheet (F5.13 g: a tab of the shell or an MvvmSheetEditor).</summary>
    private static async Task WaitForAutomatic(Anthea.Presentation.Modules.ISheetViewModel opened)
    {
        var sheet = (HorizontalPileSheetViewModel)opened;
        Task pending;
        do { pending = sheet.PendingCalculation; await pending.WaitAsync(TimeSpan.FromSeconds(25)); }
        while (!ReferenceEquals(pending, sheet.PendingCalculation));
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    }

    /// <summary>Smoke of HorizontalWorkspace (VerifyHorizontal of SheetEditor).</summary>
    internal async Task Verify(string directory)
    {
        await VerifyStratified(directory);
        await VerifyAutomatic(); if (Result is null) throw new Exception("Orizzontale WPF: calcolo non disponibile");
        await VerifyEditing(directory);
        string previous = Get(Sheet.GeneralForm, "diametro"); Type(Sheet.GeneralForm, "diametro", "1.01");
        if (Result is not null || View.DetailsButton.IsEnabled) throw new Exception("Orizzontale: risultati obsoleti non invalidati");
        Type(Sheet.GeneralForm, "diametro", previous); await WaitForAutomatic();
        var (dialog, results) = await ShowDetails(1120, 760);
        Snapshot(directory, "orizzontale_diagrammi.png", dialog);
        var resultPanel = Ui.Descendants<TabControl>(results.Tabs).First(); resultPanel.SelectedIndex = 3; dialog.Width = 1400;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); dialog.UpdateLayout();
        var displayedTable = Ui.Descendants<DataGrid>(resultPanel).Single();
        if (displayedTable.ActualWidth > 921) throw new Exception("Tabella orizzontale troppo larga");
        foreach (var tableRow in displayedTable.Items.Cast<string[]>())
            foreach (int column in new[] { 0, 2, 3, 4 })
                if (!Regex.IsMatch(tableRow[column], @"^-?\d+[.,]\d$")) throw new Exception("Valori non a un decimale");
        Snapshot(directory, "orizzontale_tabella.png", dialog); dialog.Close();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Snapshot(directory, "orizzontale_sezione.png", View);
    }

    /// <summary>VerifyStratified of HorizontalWorkspace: the mixed profile refused by Broms, accepted by the stratified model, its options, dialog, report and archive.</summary>
    private async Task VerifyStratified(string directory)
    {
        var backup = Data.Array("stratigrafie").DeepClone();
        string method = Get(Sheet.ModelForm, "metodo_calcolo");
        var upper = PaloOrizzontale.Layer(); upper["tipologia"] = "Coesivo"; upper["spessore"] = 2;
        var lower = PaloOrizzontale.Layer(); lower["spessore"] = Data["generali"].D("lunghezza") - 2;
        Reload(d => d["stratigrafie"] = new JsonArray(new JsonArray(upper, lower)));
        await WaitForAutomatic();
        if (Result is not null) throw new Exception("Il metodo Broms ha accettato implicitamente un profilo misto");
        View.Advanced.IsExpanded = true; await Settle();
        Choose(Sheet.ModelForm, "metodo_calcolo", PaloOrizzontale.StratifiedMethod);
        await WaitForAutomatic();
        if (Result is null || Result.S("metodo_calcolo") != PaloOrizzontale.StratifiedMethod || !Result["sondaggi"]![0].B("misto"))
            throw new Exception("Selezione del metodo stratificato: " + View.StatusText.Text);
        if (!View.WarningsText.Text.Contains("MODELLO STRATIFICATO SPERIMENTALE")) throw new Exception("Ipotesi del nuovo modello non esposte");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); Window.UpdateLayout();
        var selector = Editor<ComboBox>(Sheet.ModelForm, "metodo_calcolo"); var first = Cards[0];
        var bounds = selector.TransformToAncestor(first).TransformBounds(new Rect(0, 0, selector.ActualWidth, selector.ActualHeight));
        // The selector inside its card, as the historic check; its width is the one of the choices of the compact FieldForm (D10, declared
        // difference «Opzioni avanzate» of f5-7-palo-orizzontale.md, 5.4) instead of 180 px or more: the chosen method must be shown whole in it
        // (question P3 of docs/refactoring/f5-suite-passaggi.md).
        var site = Ui.Descendants<ContentPresenter>(selector).First();
        var shownText = new FormattedText((string)selector.SelectedItem, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(selector.FontFamily, selector.FontStyle, selector.FontWeight, selector.FontStretch), selector.FontSize, Brushes.Black, VisualTreeHelper.GetDpi(selector).PixelsPerDip);
        double compactChoice = FieldEditor(Sheet.GeneralForm, "vincolo").ActualWidth;
        Snapshot(directory, "stratificato-opzioni.png", Window);
        if (bounds.Width < compactChoice - 0.5 || shownText.Width > site.ActualWidth + 0.5 || bounds.Right > first.ActualWidth - 8)
            throw new Exception($"Selettore del metodo stratificato tagliato: {bounds.Width:0.#} px (scelte del form {compactChoice:0.#}), testo {shownText.Width:0.#} su {site.ActualWidth:0.#}, bordo destro {bounds.Right:0.#} su {first.ActualWidth:0.#}");
        File.WriteAllText(Path.Combine(directory, "stratificato-ui.json"), Result.ToJsonString(J.Options));
        var (diagnosticsDialog, diagnostics) = await ShowDetails(1280, 900);
        Snapshot(directory, "stratificato-terreno-ui.png", diagnosticsDialog);
        var childTabs = Ui.Descendants<TabControl>(diagnostics.Tabs).First(); childTabs.SelectedIndex = 1;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Snapshot(directory, "stratificato-equilibrio-ui.png", diagnosticsDialog);
        diagnostics.Tabs.SelectedIndex = diagnostics.Tabs.Items.Count - 2;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Snapshot(directory, "stratificato-riferimenti-ui.png", diagnosticsDialog); diagnosticsDialog.Close();
        File.WriteAllBytes(Path.Combine(directory, "stratificato-relazione.docx"), editor.BuildReport("Palo stratificato esempio di lettura", []));
        foreach (bool soil in new[] { true, false }) File.WriteAllBytes(Path.Combine(directory, soil ? "stratificato-terreno.png" : "stratificato-equilibrio.png"), new HorizontalDiagrams(Sheet.Diagram(0, soil)).Png(1320, 880));
        HorizontalStratifiedExample.Write(directory, Result);
        HorizontalStratifiedExample.SaveAndReopen(directory, Data);
        Reload(d => d["stratigrafie"] = backup);
        Choose(Sheet.ModelForm, "metodo_calcolo", method);
        View.Advanced.IsExpanded = false; await WaitForAutomatic();
        if (Result is null) throw new Exception("Ripristino metodo Broms fallito");
    }

    /// <summary>VerifyAutomatic of HorizontalWorkspace: the automatic recalculation after every kind of edit, latest request wins, closing.</summary>
    private async Task VerifyAutomatic()
    {
        await WaitForAutomatic();
        if (Result is null || Ui.Descendants<Button>(View).Any(b => b.Content?.ToString()?.StartsWith("Calcola") == true))
            throw new Exception("Calcolo automatico iniziale o rimozione pulsanti fallita");
        FormViewModel general = Sheet.GeneralForm, section = Sheet.SectionForm, efficiency = Sheet.EfficiencyForm, factors = Sheet.FactorsForm;
        string action = Get(general, "azione_orizzontale"), diameter = Get(general, "diametro"), concrete = Get(section, "fck_mpa");
        double initialMoment = Result.D("momento_resistente_knm");
        string materialFck = Get(section, "fck_mpa");
        Choose(section, "classe_cls", "C25/30");
        await WaitForAutomatic();
        if (Data["sezione"].D("fck_mpa") != 25 || Get(section, "__fcd") != (25 * Data["sezione"].D("alpha_cc") / Data["sezione"].D("gamma_c")).ToString("F1"))
            throw new Exception("Classe CLS e resistenza di progetto discordanti");
        if (Get(section, "__fyd") != (Data["sezione"].D("fyk_mpa") / Data["sezione"].D("gamma_s")).ToString("F1") ||
            FieldEditor(section, "longitudinal_bar_diameter_mm") is not ComboBox || FieldEditor(section, "transverse_bar_diameter_mm") is not ComboBox)
            throw new Exception("Materiali o menu armatura errati");
        Write(section, "fck_mpa", "33");
        if (Get(section, "classe_cls") != "Personalizzato") throw new Exception("Resistenza personalizzata non conservata");
        Write(section, "gamma_c", "");
        if (Get(section, "__fcd") != "—") throw new Exception("fcd obsoleto con input incompleto");
        Write(section, "gamma_c", "1.5"); Write(section, "fck_mpa", materialFck);
        await WaitForAutomatic();
        var surveyDiagram = Result!.Array("sondaggi")[0]!;
        double hingeDepth = StratigraphyDrawing.ReactionLimit(surveyDiagram) ?? throw new Exception("Caso lungo senza cerniera interna");
        var physical = StratigraphyDrawing.PhysicalDiagram(surveyDiagram);
        if (physical.Length == 0 || physical.Max(r => r.D("z")) != hingeDepth ||
            !surveyDiagram.Array("diagrammi").Any(r => r.D("z") > hingeDepth))
            throw new Exception("Diagramma non limitato esattamente alla cerniera o dati motore alterati");
        var fixedDiagram = surveyDiagram.DeepClone(); fixedDiagram["cerniere_m"]!.AsArray().Insert(0, JsonValue.Create(0));
        if (StratigraphyDrawing.ReactionLimit(fixedDiagram) != hingeDepth) throw new Exception("Taglio alla cerniera di testa anziché interna");
        foreach (string mechanism in new[] { "Corto", "Intermedio" })
        {
            fixedDiagram["meccanismo"] = mechanism;
            if (StratigraphyDrawing.PhysicalDiagram(fixedDiagram).Length != fixedDiagram.Array("diagrammi").Count)
                throw new Exception("Diagramma corto/intermedio troncato");
        }
        fixedDiagram["meccanismo"] = "Lungo"; fixedDiagram["coesivo"] = true;
        if (StratigraphyDrawing.ReactionLimit(fixedDiagram) != hingeDepth || StratigraphyDrawing.PhysicalDiagram(fixedDiagram).Max(r => r.D("z")) != hingeDepth)
            throw new Exception("Diagramma coesivo non limitato alla cerniera interna");
        View.Surveys.SelectedIndex = 0; Layout();
        var kpRow = Rows()[0];
        if (!ReferenceEquals(View.ProfileDrawing.HorizontalResult, Result) || kpRow.Values.S("__kp") != 3d.ToString("F1") || Cell<TextBox>(kpRow, "Kp [−]").Text != 3d.ToString("F1"))
            throw new Exception("Profilo risultati o Kp non aggiornati");
        Cell<TextBox>(kpRow, "φ′ [°]").Text = "0";
        if (kpRow.Values.S("__kp") != 1d.ToString("F1") || View.ProfileDrawing.HorizontalResult is not null)
            throw new Exception("Kp non ricalcolato o profilo obsoleto");
        Cell<ComboBox>(kpRow, "Terreno").SelectedItem = "Coesivo";
        if (kpRow.Values.S("__kp") != "—") throw new Exception("Kp mostrato per terreno coesivo");
        Cell<ComboBox>(kpRow, "Terreno").SelectedItem = "Granulare"; Cell<TextBox>(kpRow, "φ′ [°]").Text = "30";
        await WaitForAutomatic();
        double initialResistance = Result!.D("resistenza_progetto_manuale_kn");
        Type(efficiency, "efficienza_eta", "0.5"); await WaitForAutomatic();
        if (Result is null || Math.Abs(Result.D("resistenza_progetto_manuale_kn") - initialResistance * .5) > 1e-8)
            throw new Exception("Efficienza manuale non applicata automaticamente");
        Type(efficiency, "efficienza_eta", "1");
        Choose(efficiency, "efficienza_metodo", "Reese & Van Impe (foglio)");
        foreach (string key in new[] { "interasse_anteriore", "interasse_posteriore", "interasse_sinistro", "interasse_destro" }) Type(efficiency, key, "3");
        await WaitForAutomatic();
        if (Result is null || Result["efficienza"].D("eta") >= 1 || Editor<TextBox>(efficiency, "efficienza_eta").IsEnabled)
            throw new Exception("Metodo efficienza foglio non attivo");
        Choose(efficiency, "efficienza_metodo", "Manuale");
        await WaitForAutomatic();
        string initialVerticals = Get(factors, "verticali_indagate");
        Choose(factors, "verticali_indagate", "2");
        await WaitForAutomatic();
        if (Result?.D("xi3") != 1.65 || Result.D("xi4") != 1.55 || Result.D("gamma_r") != 1.3 ||
            Get(factors, "__xi3") != 1.65.ToString("F2") || Get(factors, "__xi4") != 1.55.ToString("F2"))
            throw new Exception("Selezione coefficienti non aggiorna campi e calcolo");
        Choose(factors, "verticali_indagate", initialVerticals);
        await WaitForAutomatic();
        Type(general, "azione_orizzontale", "101");
        var inFlight = Sheet.CalculateAsync();
        Type(general, "azione_orizzontale", "102");
        if (!View.Cards.IsEnabled || !Editor<TextBox>(general, "azione_orizzontale").IsEnabled) throw new Exception("Calcolo disabilita gli input");
        await inFlight;
        if (Result is not null) throw new Exception("Risultato superato pubblicato dopo nuove modifiche");
        await WaitForAutomatic();
        if (Result?.D("azione_kn") != 102 || !JsonNode.DeepEquals(Result, PaloOrizzontale.Calculate(Data))) throw new Exception("Risultato automatico non corrisponde agli ultimi dati");
        Write(section, "fck_mpa", "40"); await WaitForAutomatic();
        if (Result is null || Result.D("momento_resistente_knm") == initialMoment || !View.MomentText.Text.StartsWith("MRd =")) throw new Exception("Momento non ricalcolato automaticamente");
        Write(section, "fck_mpa", concrete);
        Type(general, "diametro", ""); await WaitForAutomatic();
        if (Result is not null || View.DetailsButton.IsEnabled || View.CsvButton.IsEnabled || !View.MomentText.Text.StartsWith("Momento non disponibile")) throw new Exception("Risultati obsoleti con input incompleti");
        CheckOutcome("dati incompleti");
        Type(general, "diametro", diameter); Type(general, "azione_orizzontale", action); await WaitForAutomatic();
        if (Result is null) throw new Exception("Ricalcolo non riparte dopo correzione");
        var thicknessCell = Cell<TextBox>(Rows()[0], "Spessore [m]");
        string thickness = Rows()[0].Values.S("spessore"); thicknessCell.Text = "0";
        await WaitForAutomatic();
        if (Result is not null || !View.MomentText.Text.StartsWith("MRd =")) throw new Exception("Momento non aggiornato con stratigrafia incompleta");
        Cell<TextBox>(Rows()[0], "Spessore [m]").Text = thickness; await WaitForAutomatic();
        // A sheet closed while its calculation runs (new HorizontalWorkspace + Dispose): opened apart by the factory of the bench of the checks.
        var independent = (MvvmSheetEditor)ModuleHostBench.SheetEditors().Open(editor.Module, (JsonObject)Data.DeepClone());
        var closed = (HorizontalPileSheetViewModel)independent.Sheet;
        var pending = closed.CalculateAsync(); independent.Dispose(); await pending;
        if (independent.Result is not null || !closed.PendingCalculation.IsCompleted) throw new Exception("Calcolo pubblicato dopo Dispose");
    }

    /// <summary>VerifyLayout of HorizontalWorkspace at the size of the window <paramref name="size"/>.</summary>
    internal async Task VerifyLayout(string directory, string size)
    {
        var scroll = View.Scroll; var panel = View.Cards;
        void Check()
        {
            var cards = Cards;
            if (cards[6].Visibility == Visibility.Visible)
            {
                if (Ui.Descendants<TabControl>(cards[6]).Any()) throw new Exception("Sezione ancora in una sottoscheda");
                FrameworkElement drawing = Micro ? View.ChsDrawing : View.ConcreteDrawing;
                var sectionLayout = (Grid)View.SectionFormHost.Parent;
                var drawingBounds = drawing.TransformToAncestor(sectionLayout).TransformBounds(new Rect(0, 0, drawing.ActualWidth, drawing.ActualHeight));
                if (drawingBounds.Width <= 0 || drawingBounds.Left < sectionLayout.ColumnDefinitions[0].ActualWidth - 1 ||
                    drawingBounds.Right > sectionLayout.ActualWidth + 1 || drawingBounds.Bottom > sectionLayout.ActualHeight + 1)
                    throw new Exception("Sezione non ridimensionata a destra dei dati");
            }
            if (scroll.ExtentWidth > scroll.ViewportWidth + 1) throw new Exception("Scorrimento orizzontale esterno");
            // SH40 (approved): below 1120 × 680 the new shell keeps its minimum and scrolls; the module is checked inside the scrolling area, as in
            // ui/shell-smoke-display. Inside the window otherwise, and always in MainWindow.
            var host = Window is X.Desktop.Views.Shell.ShellWindow shellWindow && shellWindow.FindName("ShellViewport") is X.Desktop.Views.Shell.MinimumSizeViewport { Content: FrameworkElement scrolled } outer
                && (outer.ComputedHorizontalScrollBarVisibility == Visibility.Visible || outer.ComputedVerticalScrollBarVisibility == Visibility.Visible) ? scrolled : (FrameworkElement)Window.Content;
            if (View.ActualWidth > host.ActualWidth + 1) throw new Exception("Modulo più largo della finestra");
            // The places of the panel (LayoutCards on the canvas of the historic view).
            var bounds = cards.Where(c => c.Visibility == Visibility.Visible).Select(c => LayoutInformation.GetLayoutSlot(c)).ToArray();
            for (int i = 0; i < bounds.Length; i++)
            {
                if (bounds[i].Left < 0 || bounds[i].Right > panel.ActualWidth + 1) throw new Exception("Pannello fuori finestra");
                for (int j = i + 1; j < bounds.Length; j++) if (bounds[i].IntersectsWith(bounds[j])) throw new Exception("Schede sovrapposte");
            }
        }
        await Expand(-1); scroll.ScrollToTop();
        await Settle(); Check();
        if (Math.Abs(Window.ActualWidth - int.Parse(size, CultureInfo.InvariantCulture)) > 2) throw new Exception($"Smoke viewport not resized: requested {size}, actual {Window.ActualWidth}");
        if (size == "1920" && Window is X.Desktop.Views.Shell.ShellWindow shell)
        {
            // G6 (F5-61), decided by the user on 24/10/2026: in the new shell the main page may scroll inside its tab at Full HD; the check is on the area of
            // the tab, as for SH40: the module inside the area of its tab, the shell itself not scrolling.
            var area = editor.View; var position = View.TranslatePoint(new Point(), area);
            var outer = (X.Desktop.Views.Shell.MinimumSizeViewport)shell.FindName("ShellViewport");
            if (position.X < -1 || position.Y < -1 || position.X + View.ActualWidth > area.ActualWidth + 1 || position.Y + View.ActualHeight > area.ActualHeight + 1)
                throw new Exception($"Full HD: module outside the area of its tab ({position.X:0.#}, {position.Y:0.#}, {View.ActualWidth:0.#} × {View.ActualHeight:0.#} in {area.ActualWidth:0.#} × {area.ActualHeight:0.#})");
            if (outer.ComputedVerticalScrollBarVisibility == Visibility.Visible || outer.ComputedHorizontalScrollBarVisibility == Visibility.Visible)
                throw new Exception("Full HD: the shell scrolls around the tab");
        }
        else if (size == "1920" && scroll.ExtentHeight > scroll.ViewportHeight + 1) throw new Exception($"Full HD main page scrolls vertically: {scroll.ExtentHeight}/{scroll.ViewportHeight}");
        if (Cards.Count != 7 || Sheet.ModelForm.Contains("modalita")) throw new Exception("Numero schede o modello errato");
        if (!Ui.Descendants<TextBlock>(Cards[1]).Any(t => t.Text == "Efficienza") ||
            !Ui.Descendants<TextBlock>(Cards[2]).Any(t => t.Text == "Coefficienti normativa") || Sheet.FactorsForm.Contains("efficienza_metodo"))
            throw new Exception("Efficienza non separata dalla normativa nell'ordine richiesto");
        Snapshot(directory, $"orizzontale_{size}.png", Window);
        var verificationScroll = Ui.Descendants<ScrollViewer>(Cards[3]).First(s => s.Content is StackPanel);
        verificationScroll.ScrollToBottom(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        if (!Ui.Descendants<FrameworkElement>(Cards[3]).Any(e => Equals(e.Tag, "horizontal-calculation-description")))
            throw new Exception("Sintesi calcolo mancante");
        Snapshot(directory, $"orizzontale_{size}_sintesi.png", Window);
        verificationScroll.ScrollToTop();
        var tabHeader = Ui.Descendants<TabPanel>(View.Surveys).Single();
        var addSurvey = StripButton("+ Stratigrafia");
        double tabCenter = tabHeader.TranslatePoint(new Point(0, tabHeader.ActualHeight / 2), View.Surveys).Y;
        double buttonCenter = addSurvey.TranslatePoint(new Point(0, addSurvey.ActualHeight / 2), View.Surveys).Y;
        if (Math.Abs(tabCenter - buttonCenter) > 6) throw new Exception("Sondaggi e comandi non allineati");
        View.Advanced.IsExpanded = true; await Settle(); Check();
        if (!FieldEditor(Sheet.ModelForm, "passo").IsVisible || !FieldEditor(Sheet.ModelForm, "tolleranza").IsVisible) throw new Exception("Opzioni avanzate non accessibili");
        Snapshot(directory, $"orizzontale_{size}_avanzate.png", Window);
        View.Advanced.IsExpanded = false; await Settle();
        scroll.ScrollToBottom(); await Settle();
        if (scroll.VerticalOffset + scroll.ViewportHeight < panel.ActualHeight - 1) throw new Exception("Ultima scheda non raggiungibile");
        Snapshot(directory, $"orizzontale_{size}_fondo.png", Window);
        foreach (int index in Expandable)
        {
            await Expand(index); scroll.ScrollToTop();
            await Settle(); Check();
            // The extended stratigraphy keeps the profile at its right when the width allows it (ExtendedStratigraphy, 22/10/2026).
            if (index == 4 && ExtendedStratigraphy.Split(panel.DesiredSize.Width - 24, 12) is not null
                && (Cards[5].Visibility != Visibility.Visible || LayoutInformation.GetLayoutSlot(Cards[5]).Left <= LayoutInformation.GetLayoutSlot(Cards[4]).Right))
                throw new Exception("Profilo stratigrafico non visibile accanto alla stratigrafia estesa");
            Snapshot(directory, $"orizzontale_{size}_esteso_{index}.png", Window);
        }
        await Expand(-1); scroll.ScrollToTop(); await Settle();
    }

    /// <summary>VerifyEditing of HorizontalWorkspace: typing during the recalculation, layers and stratigraphies, the presentation and the outcome.</summary>
    private async Task VerifyEditing(string directory)
    {
        string original = Data.ToJsonString(); var backup = Data.Array("stratigrafie").DeepClone();
        var elasticSurvey = Data["elastico"]!["stratigrafia"]!.DeepClone();
        var expected = Result!.DeepClone();
        await Expand(4);
        View.Surveys.SelectedIndex = 0; Layout();
        var row = Rows()[0];
        var input = Cell<TextBox>(row, "Spessore [m]");
        string thickness = input.Text; input.Focus(); input.Text = thickness + " ";
        await WaitForAutomatic();
        if (!input.IsKeyboardFocused || input.Text != thickness + " " || row.Values.S("spessore") != input.Text) throw new Exception("Digitazione interrotta");
        input.Text = thickness; await WaitForAutomatic();
        // The colour of the layer: the swatch of the row (Token.Swatch.Soil1 = StratigraphyDrawing.LayerColors[0] in Chiara) and its name.
        var swatch = Ui.Descendants<Border>(RowOf(row)).Single(b => b.Width == 18 && b.Height == 12);
        if (((SolidColorBrush)swatch.Background).Color != (Color)ColorConverter.ConvertFromString(StratigraphyDrawing.LayerColors[0]) || row.Values.S("__strato") != "A")
            throw new Exception("Colore/nome strato errato");
        var grid = LayerGrid();
        if (Ui.Descendants<CheckBox>(grid).Any(b => Equals(b.Content, "Laterale"))) throw new Exception("Opzione laterale non applicabile al modulo orizzontale");
        int count = Rows().Count;
        var add = Ui.Descendants<Button>(View.Surveys).First(b => Equals(b.Content, "Aggiungi strato"));
        Press(add); Press(add);
        await Settle();
        var middle = Rows()[count]; var last = Rows()[count + 1];
        if (middle.Values.S("spessore") != "0") throw new Exception("Nuovo strato non nullo");
        Press(Ui.Descendants<Button>(LayerGrid()).First(b => Equals(b.Content, "[−]") && ReferenceEquals(b.DataContext, middle)));
        if (Rows().Contains(middle) || !ReferenceEquals(Rows()[^1], last)) throw new Exception("Eliminazione strato errata");
        // «Copia in…» → «Nuova stratigrafia».
        if (!await CopySurvey(other: -1)) throw new Exception("Copia in nuova stratigrafia fallita");
        var all = Data.Array("stratigrafie");
        if (!JsonNode.DeepEquals(all[0], all[1])) throw new Exception("Copia non identica");
        // The independence of the copy: typed in the cell of the copy (the copy is selected).
        View.Surveys.SelectedIndex = 1; await Settle();
        Cell<TextBox>(Rows(1)[0], "Spessore [m]").Text = "123";
        if (all[1]![0].S("spessore") != "123") throw new Exception("Cella della copia non scritta");
        if (all[0]![0].S("spessore") == "123") throw new Exception("Copia non indipendente");
        View.Surveys.SelectedIndex = 0; await Settle();
        await CopySurvey(other: 1);
        if (!JsonNode.DeepEquals(all[0], all[1])) throw new Exception("Copia tra stratigrafie fallita");
        var keep = all[1]; await RemoveSurvey(0);
        if (all.Count != 1 || !ReferenceEquals(all[0], keep)) throw new Exception("Eliminazione stratigrafia errata");
        if (Data["elastico"].D("stratigrafia") != -1) throw new Exception("La modifica delle stratigrafie non richiede una nuova selezione elastica");
        // Restore the explicit analysis selection together with the test's original surveys, the sheet built again on them.
        Reload(d => { d["elastico"]!["stratigrafia"] = elasticSurvey.DeepClone(); d["stratigrafie"] = backup; });
        await Expand(-1);
        if (Data.ToJsonString() != original) throw new Exception("Metadati di presentazione salvati nei dati");
        await WaitForAutomatic();
        if (!JsonNode.DeepEquals(Result, expected)) throw new Exception("Presentazione modifica il risultato");
        // The outcome (question P2): the synthetic results of the historic check through the function both views show, then the view.
        var actual = Result!;
        foreach (var rd in new double?[] { null, 50, 200 })
        {
            var synthetic = (JsonObject)actual.DeepClone(); synthetic["azione_kn"] = 100; synthetic["resistenza_progetto_manuale_kn"] = rd;
            var kind = HorizontalPilePresentation.Verification(synthetic).Kind;
            var expectedKind = rd is null ? HorizontalVerificationOutcome.Muted : rd < 100 ? HorizontalVerificationOutcome.NotSatisfied : HorizontalVerificationOutcome.Satisfied;
            if (kind != expectedKind) throw new Exception("Colore esito errato");
        }
        CheckOutcome("verifica soddisfatta");
        string action = Get(Sheet.GeneralForm, "azione_orizzontale");
        Type(Sheet.GeneralForm, "azione_orizzontale", "100000"); await WaitForAutomatic();
        CheckOutcome("verifica non soddisfatta");
        Type(Sheet.GeneralForm, "azione_orizzontale", action); await WaitForAutomatic();
        if (!JsonNode.DeepEquals(Result, expected)) throw new Exception("Presentazione modifica il risultato");
        if (Ui.Descendants<ComboBox>(View).Any(c => c.Items.Cast<object>().Any(v => v is string s && string.IsNullOrWhiteSpace(s)))) throw new Exception("Opzione vuota");
        Snapshot(directory, "orizzontale_verifica.png", View);
    }

    /// <summary>
    /// The outcome shown by the view for the result of the sheet: the rows and the text of HorizontalPilePresentation.Verification and the colour of
    /// its kind, the theme tokens of the view (declared difference: the green and the red of the catalogue, as the vertical pile).
    /// </summary>
    private void CheckOutcome(string what)
    {
        Layout();
        var shown = HorizontalPilePresentation.Verification(Result);
        string token = shown.Kind switch
        {
            HorizontalVerificationOutcome.Satisfied => "Token.Status.Ok",
            HorizontalVerificationOutcome.NotSatisfied => "Token.Status.Fail",
            _ => "Token.Muted"
        };
        var rows = ((IEnumerable<string[]>?)View.VerificationGrid.ItemsSource)?.ToList() ?? [];
        if (View.OutcomeText.Text != shown.Outcome || rows.Count != shown.Rows.Count || !rows.Zip(shown.Rows).All(p => p.First.SequenceEqual(p.Second))
            || ((SolidColorBrush)View.OutcomeText.Foreground).Color != ((SolidColorBrush)View.OutcomeText.FindResource(token)).Color)
            throw new Exception("Colore esito errato (" + what + ")");
    }

    /// <summary>«Copia in…» of the selected stratigraphy into <paramref name="other"/> (−1: a new one) chosen in the dialog; a replacement is confirmed.</summary>
    private async Task<bool> CopySurvey(int other)
    {
        int before = Data.Array("stratigrafie").Count;
        var questions = new List<string>();
        services.Dialog = dialog =>
        {
            var copy = (SurveyCopyDialogViewModel)dialog;
            copy.Selected = copy.Choices.Single(c => c.Survey == other);
            copy.ConfirmCommand.Execute(null);
            return copy.Result;
        };
        services.Confirmations.Delete = (title, question) => { questions.Add(title + ": " + question); return true; };
        try { Press(StripButton("Copia in…")); }
        finally { services.Dialog = null; services.Confirmations.Delete = null; }
        string? expected = other == -1 ? null : $"Copia stratigrafia: Sostituire gli strati della stratigrafia {other + 1}?";
        if (questions.Count != (expected is null ? 0 : 1) || (expected is not null && questions[0] != expected))
            throw new InvalidOperationException("Domande della copia inattese: " + string.Join(" | ", questions));
        await Settle();
        return other == -1 ? Data.Array("stratigrafie").Count == before + 1 : Sheet.SelectedSurveyIndex == other;
    }

    /// <summary>The [−] of the tab of the stratigraphy <paramref name="index"/>, confirmed (DeleteSurvey of the historic view without the question).</summary>
    private async Task RemoveSurvey(int index)
    {
        var tab = (TabItem)View.Surveys.ItemContainerGenerator.ContainerFromIndex(index);
        string? asked = null;
        services.Confirmations.Delete = (title, question) => { asked = title + ": " + question; return true; };
        try { Press(Ui.Descendants<Button>(tab).Single(b => Equals(b.Tag, "elimina-stratigrafia"))); }
        finally { services.Confirmations.Delete = null; }
        if (asked != $"Stratigrafia: Eliminare la stratigrafia {index + 1} e tutti i suoi strati?") throw new InvalidOperationException("Domanda dell'eliminazione della stratigrafia diversa: " + asked);
        await Settle();
    }

    // ---------------------------------------------------------------- HorizontalChsChecks.cs

    /// <summary>VerifyChs of HorizontalWorkspace: the CHS of the micropile from the catalogue and manual.</summary>
    internal async Task VerifyChs()
    {
        await WaitForAutomatic();
        var section = Sheet.SectionForm;
        if (Result?["sezione"].S("tipo") != "CHS" || View.ChsDrawing.Properties is null) throw new Exception("Calcolo CHS iniziale fallito");
        double original = Result.D("momento_resistente_knm");
        Choose(section, "modo_chs", "Manuale");
        await WaitForAutomatic();
        if (Result is null || Math.Abs(Result.D("momento_resistente_knm") - original) > 1e-8) throw new Exception("CHS manuale discordante dal catalogo");
        Type(section, "spessore_chs_mm", "10"); await WaitForAutomatic();
        if (Result is null || Result.D("momento_resistente_knm") <= original || View.ChsDrawing.Properties?.D("spessore_mm") != 10) throw new Exception("CHS manuale non aggiornato");
        Type(section, "spessore_chs_mm", "0"); await WaitForAutomatic();
        if (Result is not null || View.ChsDrawing.Properties is not null) throw new Exception("CHS risultati obsoleti");
        Type(section, "spessore_chs_mm", "8");
        Choose(section, "modo_chs", "Catalogo");
        Choose(section, "profilo_chs", "CHS 114.3 × 6.3");
        await WaitForAutomatic();
        if (Result is null || View.ChsDrawing.Properties?.D("diametro_mm") != 114.3 || Editor<TextBox>(section, "diametro_chs_mm").IsEnabled)
            throw new Exception("Catalogo CHS non applicato");
    }
}

/// <summary>
/// f5-suite-passaggi: the part of VerifyStratified that does not read the view (the example with water and its report, the archive saved and opened
/// again), shared by the two views of the horizontal pile.
/// </summary>
internal static class HorizontalStratifiedExample
{
    /// <summary>Three alternating layers and water above the granular layer: continuous vertical stress, discontinuous local limit and both closures.</summary>
    internal static void Write(string directory, JsonObject result)
    {
        var example = (JsonObject)result["input"]!.DeepClone();
        example["generali"]!["presenza_falda"] = true; example["generali"]!["profondita_falda"] = 1;
        example["generali"]!["origine_momento"] = "Manuale"; example["generali"]!["momento_resistente"] = 900;
        example["generali"]!["provenienza_momento"] = "Esempio illustrativo dei diagrammi";
        example["generali"]!["passo"] = .25;
        var c1 = PaloOrizzontale.Layer(); c1["tipologia"] = "Coesivo"; c1["spessore"] = 2; c1["coesione_non_drenata"] = 40;
        var sand = PaloOrizzontale.Layer(); sand["spessore"] = 3;
        var c2 = PaloOrizzontale.Layer(); c2["tipologia"] = "Coesivo"; c2["spessore"] = 5; c2["coesione_non_drenata"] = 65;
        example["generali"]!["lunghezza"] = 10;
        example["stratigrafie"] = new JsonArray(new JsonArray(c1, sand, c2));
        var illustrated = PaloOrizzontale.Calculate(example);
        if (illustrated.S("errore") != "") throw new Exception(illustrated.S("errore"));
        File.WriteAllText(Path.Combine(directory, "esempio-falda.json"), illustrated.ToJsonString(J.Options));
        var illustratedFigures = new List<ReportOrizzontale.Figure>();
        foreach (bool soil in new[] { true, false })
        {
            var drawing = new HorizontalDiagrams(illustrated, 0, soil);
            File.WriteAllBytes(Path.Combine(directory, soil ? "esempio-falda-terreno.png" : "esempio-falda-equilibrio.png"), drawing.Png(1320, 880));
            illustratedFigures.Add(new(1, soil ? "Tensioni del terreno stratificato con falda" : "Reazioni e sollecitazioni nel palo", drawing.Png(1000, 660), 1000d / 660));
        }
        byte[] exampleReport = ReportOrizzontale.Create("Palo in tre strati con falda", illustrated, true, illustratedFigures);
        File.WriteAllBytes(Path.Combine(directory, "esempio-falda.docx"), exampleReport);
        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(exampleReport));
        if (zip.Entries.Count(e => e.FullName.StartsWith("word/media/")) != 2) throw new Exception("Diagrammi mancanti dalla relazione");
        using var sr = new StreamReader(zip.GetEntry("word/document.xml")!.Open()); string xml = sr.ReadToEnd();
        if (!xml.Contains("landscape") || !xml.Contains("nzgs.org") || !xml.Contains("FHWA")) throw new Exception("Riferimenti o impaginazione relazione incompleti");
    }

    /// <summary>The sheet with the stratified model saved and opened again: the method is kept and the archive calculates.</summary>
    internal static void SaveAndReopen(string directory, JsonObject data)
    {
        string path = Path.Combine(directory, "stratificato.programma");
        var doc = J.Obj(("formato", "X"), ("versione", 1), ("tipo", "calcolo"), ("modulo_id", PaloOrizzontale.Module), ("dati", data.DeepClone()));
        DocumentService.Save(path, doc);
        var reopened = DocumentService.Open(path)["dati"]!.AsObject();
        if (reopened["generali"].S("metodo_calcolo") != PaloOrizzontale.StratifiedMethod || PaloOrizzontale.Calculate(reopened).S("errore") != "")
            throw new Exception("Metodo stratificato non conservato nel salvataggio");
    }
}
