using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Anthea.Application.Materials;
using Anthea.Application.Presentation;
using Anthea.Application.Walls;
using X.Core;
using X.Desktop.Views.Common;

namespace X.Desktop;

/// <summary>
/// --check-field-form-parity &lt;cartella&gt; (F5.2, proposal F5 sections 3.2 and 7.3): the same edit sequences through the
/// historic <see cref="InputForm"/> and through <see cref="FieldForm"/> + <see cref="FormViewModel"/>, on field lists taken
/// from real modules (numbers, choices, booleans, read-only fields, symbols, notes, groups, default choices; a read-only revision,
/// RevisionInspection on InputForm and FormViewModel.IsReadOnly on FieldForm, D4 of F5.4). After every
/// step the two sides must give the same bytes of the <see cref="JsonObject"/>, the same callbacks, the same Get and the same
/// layout properties: order and bounds of the elements, labels, units, symbols, editors, enabled and visible state, notes and
/// help. No pixel comparison. Each side runs alone in a window placed outside the screen (never activated on purpose), so
/// that the keyboard focus of the commit on focus loss is the real one; no mouse.
/// </summary>
internal static class FieldFormParityChecks
{
    sealed record Options(bool Compact = false, bool WideChoices = false, bool SymbolColumns = false, string? WikiModule = null,
        string? HostModule = null, bool CommitOnFocusLoss = false, bool NumericPresentation = false);
    sealed record Group(string Title, string[] Keys, bool Expanded);
    sealed record Step(string Description, Action<Side> Apply);
    sealed record Scenario(string Name, string Source, Func<JsonObject> Data, Field[] Fields, Options Options, Group[] Groups, Step[] Steps);
    sealed record Parts(FrameworkElement Editor, TextBlock? Label, Button? Help, TextBlock? Unit, TextBlock? Symbol, Border? Note);

    internal static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory);
        int checks = 0; var log = new List<string>();
        foreach (var scenario in Scenarios())
        {
            var historic = await Trace(scenario, new InputFormSide(scenario));
            var mvvm = await Trace(scenario, new FieldFormSide(scenario));
            File.WriteAllLines(Path.Combine(directory, scenario.Name + "-inputform.txt"), historic, new UTF8Encoding(false));
            File.WriteAllLines(Path.Combine(directory, scenario.Name + "-fieldform.txt"), mvvm, new UTF8Encoding(false));
            if (historic.Count != mvvm.Count) throw new InvalidOperationException($"{scenario.Name}: tracce di lunghezza diversa ({historic.Count}, {mvvm.Count})");
            for (int i = 0; i < historic.Count; i++)
            {
                if (historic[i] != mvvm[i])
                    throw new InvalidOperationException($"{scenario.Name} ({scenario.Source}), riga {i + 1} delle tracce:\nInputForm: {historic[i]}\nFieldForm: {mvvm[i]}");
                checks++;
            }
            log.Add($"OK {scenario.Name}: {scenario.Fields.Length} campi, {scenario.Steps.Length} passi, {historic.Count} righe uguali ({scenario.Source})");
        }
        log.Add($"PASS {checks} controlli di parità FieldForm/InputForm");
        File.WriteAllLines(Path.Combine(directory, "dettaglio.txt"), log, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(directory, "esito.txt"), log[^1], new UTF8Encoding(false));
    }

    /// <summary>Opens the side, then after the opening and after every step writes the state: document, callbacks, Get, fields, groups.</summary>
    static async Task<List<string>> Trace(Scenario scenario, Side side)
    {
        var lines = new List<string>();
        try
        {
            await side.Open();
            string opened = side.Values.ToJsonString(J.Options);
            if (opened != scenario.Data().ToJsonString(J.Options)) throw new InvalidOperationException($"{scenario.Name}: {side.Name} scrive nel documento alla sola apertura");
            lines.AddRange(side.State("apertura"));
            foreach (var step in scenario.Steps)
            {
                step.Apply(side);
                await side.Settle();
                lines.AddRange(side.State(step.Description));
            }
        }
        finally { side.Close(); }
        return lines;
    }

    abstract class Side(Scenario scenario)
    {
        protected readonly Scenario Scenario = scenario;
        protected Window? Window;
        protected readonly Button Neutral = new() { Content = "Fuori dal form" };
        internal readonly List<string> Calls = [];
        internal JsonObject Values { get; } = scenario.Data();
        internal abstract string Name { get; }
        protected abstract FrameworkElement Form { get; }
        protected abstract Parts? PartsOf(string key);
        protected abstract string Get(string key);
        protected abstract IEnumerable<string> GroupState();
        internal abstract void Set(string key, string value, bool display);
        internal abstract void Enable(string key, bool enabled, bool dim);
        internal abstract void Show(string key, bool show);
        internal abstract void Commit();
        internal abstract void UpdateNote(string key);
        /// <summary>A previous revision of a project: RevisionInspection on InputForm, FormViewModel.IsReadOnly on FieldForm (D4 of F5.4).</summary>
        internal abstract void ReadOnly();

        internal async Task Open()
        {
            // Without layout rounding: at fractional display scales WPF gives the slack of a single grid (InputForm) one device
            // pixel per row and none to separate grids (FieldForm). That rounding is not a property of the layout; with it off
            // the bounds of the two forms must be equal.
            var host = new DockPanel { UseLayoutRounding = false };
            DockPanel.SetDock(Neutral, Dock.Top); host.Children.Add(Neutral); host.Children.Add(Form);
            host.SetValue(NumericPresentation.EnabledProperty, Scenario.Options.NumericPresentation);
            host.SetValue(InputForm.CommitOnFocusLossProperty, Scenario.Options.CommitOnFocusLoss);
            if (Scenario.Options.HostModule is { } module) host.SetValue(WikiContextHelp.ModuleProperty, module);
            Window = new Window
            {
                Content = host, Width = 560, Height = 1400, Left = -32000, Top = -32000, WindowStartupLocation = WindowStartupLocation.Manual,
                ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, Title = "Parità " + Name
            };
            Window.Show();
            await Settle();
        }
        internal void Close() => Window?.Close();
        internal async Task Settle()
        {
            Window!.UpdateLayout();
            await Window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            Window.UpdateLayout();
        }

        FrameworkElement Editor(string key) => PartsOf(key)?.Editor ?? throw new InvalidOperationException($"{Scenario.Name}: editor di {key} non generato ({Name})");
        internal void Type(string key, string text) => ((TextBox)Editor(key)).Text = text;
        internal void Choose(string key, string value) => ((ComboBox)Editor(key)).SelectedItem = value;
        internal void Check(string key, bool value) => ((CheckBox)Editor(key)).IsChecked = value;
        internal void Focus(string key)
        {
            var editor = Editor(key);
            editor.Focus();
            if (!editor.IsKeyboardFocusWithin) throw new InvalidOperationException($"{Scenario.Name}: fuoco della tastiera non disponibile su {key} ({Name})");
        }
        internal void Blur()
        {
            Neutral.Focus();
            if (!Neutral.IsKeyboardFocused) throw new InvalidOperationException($"{Scenario.Name}: fuoco della tastiera non spostato fuori dal form ({Name})");
        }
        internal void Expand(string title, bool expanded) =>
            Ui.Descendants<Expander>(Form).Single(e => (e.Header as TextBlock)?.Text == title).IsExpanded = expanded;
        internal void External(Action<JsonObject> change) => change(Values);

        internal IEnumerable<string> State(string step)
        {
            yield return "## " + step;
            yield return "documento " + Values.ToJsonString(J.Options);
            yield return "richiami " + string.Join(",", Calls);
            foreach (var field in Scenario.Fields) yield return Describe(field.Key);
            foreach (string group in GroupState()) yield return group;
        }

        string Describe(string key)
        {
            var p = PartsOf(key);
            var text = new StringBuilder(key).Append(" = «").Append(Get(key)).Append('»');
            // A hidden row (ShowField, closed group) has no layout to compare: only its value.
            if (p is null || !p.Editor.IsVisible) return text.Append(" | nascosto").ToString();
            text.Append(" | editor ").Append(p.Editor.GetType().Name).Append(' ').Append(Box(p.Editor))
                .Append(" abilitato=").Append(p.Editor.IsEnabled).Append(" opacità=").Append(Opacity(p.Editor).ToString("0.##", CultureInfo.InvariantCulture))
                .Append(" suggerimento=").Append(p.Editor.ToolTip).Append(" nome=").Append(AutomationProperties.GetName(p.Editor))
                .Append(" altezza minima=").Append(p.Editor.MinHeight.ToString(CultureInfo.InvariantCulture));
            switch (p.Editor)
            {
                case TextBox t:
                    text.Append(" testo=«").Append(t.Text).Append("» sola lettura=").Append(t.IsReadOnly).Append(" allineamento=").Append(t.TextAlignment)
                        .Append(" fondo=").Append(Colour(t.Background));
                    break;
                case ComboBox c:
                    text.Append(" voci=").Append(string.Join("/", c.Items.Cast<object>())).Append(" scelta=").Append(c.SelectedItem);
                    break;
                case CheckBox k:
                    var inner = k.Content as DockPanel;
                    var label = inner?.Children.OfType<TextBlock>().SingleOrDefault();
                    var help = inner?.Children.OfType<Button>().SingleOrDefault();
                    text.Append(" spunta=").Append(k.IsChecked).Append(" contenuto=").Append(label is null ? "-" : $"«{label.Text}» {label.FontSize} {Colour(label.Foreground)} suggerimento={label.ToolTip}")
                        .Append(" aiuto=").Append(help is null ? "-" : Visible(help) ? "visibile " + help.ToolTip : "nascosto");
                    break;
            }
            text.Append(" | etichetta ").Append(TextOf(p.Label)).Append(" suggerimento=").Append(Visible(p.Label) ? p.Label!.ToolTip : "-")
                .Append(" | aiuto ").Append(p.Help is null || !Visible(p.Help) ? "-" : $"{Box(p.Help)} {p.Help.ToolTip} {AutomationProperties.GetName(p.Help)}")
                .Append(" | unità ").Append(TextOf(p.Unit)).Append(" | simbolo ").Append(TextOf(p.Symbol))
                .Append(" | nota ").Append(Visible(p.Note) ? $"«{((TextBlock)p.Note!.Child).Text}» {Box(p.Note)} {Colour(p.Note.Background)} {Colour(p.Note.BorderBrush)} {Colour(((TextBlock)p.Note.Child).Foreground)}" : "-");
            return text.ToString();
        }

        string TextOf(TextBlock? block) => Visible(block) ? $"«{block!.Text}» {Box(block)} {block.FontSize.ToString(CultureInfo.InvariantCulture)} {block.FontWeight} {Colour(block.Foreground)} {block.TextAlignment}" : "-";
        static bool Visible(FrameworkElement? element) => element is { IsVisible: true };
        string Box(FrameworkElement element)
        {
            if (!element.IsVisible) return "[nascosto]";
            var bounds = element.TransformToAncestor(Form).TransformBounds(new Rect(element.RenderSize));
            return string.Create(CultureInfo.InvariantCulture, $"[{bounds.X:0.##},{bounds.Y:0.##} {bounds.Width:0.##}x{bounds.Height:0.##}]");
        }
        double Opacity(DependencyObject element)
        {
            double opacity = 1;
            for (var node = element; node is not null; node = VisualTreeHelper.GetParent(node))
            {
                if (node is UIElement ui) opacity *= ui.Opacity;
                if (ReferenceEquals(node, Form)) break;
            }
            return opacity;
        }
        static string Colour(Brush? brush) => brush is SolidColorBrush solid ? solid.Color.ToString() : brush?.ToString() ?? "-";
    }

    /// <summary>The historic form, read through its rows (private, by reflection: test only).</summary>
    sealed class InputFormSide : Side
    {
        readonly InputForm form;
        readonly Dictionary<string, List<FrameworkElement>> rows;
        internal InputFormSide(Scenario scenario) : base(scenario)
        {
            var o = scenario.Options;
            form = new InputForm(Values, scenario.Fields, Calls.Add, o.Compact, o.WideChoices, o.SymbolColumns, o.WikiModule);
            foreach (var group in scenario.Groups) form.GroupFields(group.Title, group.Keys, group.Expanded);
            rows = (Dictionary<string, List<FrameworkElement>>)typeof(InputForm).GetField("rows", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(form)!;
        }
        internal override string Name => "InputForm";
        protected override FrameworkElement Form => form;
        protected override Parts PartsOf(string key)
        {
            var elements = rows[key];
            var host = elements.OfType<DockPanel>().FirstOrDefault();
            int unitColumn = Scenario.Options.SymbolColumns ? 3 : 2;
            return new(form.Editors[key], host?.Children.OfType<TextBlock>().Single(), host?.Children.OfType<Button>().Single(),
                elements.OfType<TextBlock>().SingleOrDefault(t => Grid.GetColumn(t) == unitColumn),
                Scenario.Options.SymbolColumns ? elements.OfType<TextBlock>().SingleOrDefault(t => Grid.GetColumn(t) == 1) : null,
                elements.OfType<Border>().SingleOrDefault());
        }
        protected override string Get(string key) => form.Get(key);
        protected override IEnumerable<string> GroupState()
        {
            foreach (var expander in (form.Content as StackPanel)?.Children.OfType<Expander>() ?? [])
            {
                var grid = (Grid)expander.Content;
                var keys = form.Editors.Where(e => ReferenceEquals(VisualTreeHelper.GetParent(e.Value) ?? LogicalTreeHelper.GetParent(e.Value), grid))
                    .OrderBy(e => Grid.GetRow(e.Value)).Select(e => e.Key);
                yield return $"gruppo «{((TextBlock)expander.Header).Text}» aperto={expander.IsExpanded} campi={string.Join(",", keys)}";
            }
        }
        internal override void Set(string key, string value, bool display) => form.Set(key, value, display);
        internal override void Enable(string key, bool enabled, bool dim) => form.Enable(key, enabled, dim);
        internal override void Show(string key, bool show) => form.ShowField(key, show);
        internal override void Commit() => form.Commit();
        internal override void UpdateNote(string key) => form.UpdateNote(key);
        internal override void ReadOnly() => RevisionInspection.Protect(form);
    }

    /// <summary>The new form: FormViewModel shown by FieldForm, read through the named parts of its row template.</summary>
    sealed class FieldFormSide : Side
    {
        readonly FormViewModel model;
        readonly FieldForm view;
        internal FieldFormSide(Scenario scenario) : base(scenario)
        {
            var o = scenario.Options;
            model = new FormViewModel(Values, scenario.Fields, Calls.Add, o.Compact, o.WideChoices, o.SymbolColumns, o.WikiModule) { CommitOnFocusLoss = o.CommitOnFocusLoss };
            foreach (var group in scenario.Groups) model.GroupFields(group.Title, group.Keys, group.Expanded);
            view = new FieldForm { DataContext = model };
        }
        internal override string Name => "FieldForm";
        protected override FrameworkElement Form => view;
        protected override Parts PartsOf(string key)
        {
            var field = model[key];
            var presenter = Ui.Descendants<ItemsControl>(view).Select(i => i.ItemContainerGenerator.ContainerFromItem(field)).OfType<ContentPresenter>().SingleOrDefault();
            if (presenter is null) return null;
            T Part<T>(string name) where T : class => presenter.ContentTemplate.FindName(name, presenter) as T ?? throw new InvalidOperationException("Parte assente: " + name);
            var editor = Ui.Descendants<Control>(Part<ContentControl>("Editor")).First(c => c is TextBox or ComboBox or CheckBox);
            var host = Part<DockPanel>("LabelHost");
            return new(editor, Part<TextBlock>("LabelText"), host.Children.OfType<Button>().Single(), Part<TextBlock>("UnitText"), Part<TextBlock>("SymbolText"), Part<Border>("NoteBox"));
        }
        protected override string Get(string key) => model.Get(key);
        protected override IEnumerable<string> GroupState()
        {
            foreach (var expander in Ui.Descendants<Expander>(view))
            {
                var group = (FieldGroupViewModel)expander.DataContext;
                yield return $"gruppo «{((TextBlock)expander.Header).Text}» aperto={expander.IsExpanded} campi={string.Join(",", group.Fields.Select(f => f.Key))}";
            }
        }
        internal override void Set(string key, string value, bool display) => model.Set(key, value, display);
        internal override void Enable(string key, bool enabled, bool dim) => model.Enable(key, enabled, dim);
        internal override void Show(string key, bool show) => model.ShowField(key, show);
        internal override void Commit() => model.Commit();
        internal override void UpdateNote(string key) => model.UpdateNote(key);
        internal override void ReadOnly() => model.IsReadOnly = true;
    }

    // ---------------------------------------------------------------- scenarios from real modules

    static Step Type(string key, string text) => new($"digita {key} «{text}»", s => s.Type(key, text));
    static Step Choose(string key, string value) => new($"sceglie {key} «{value}»", s => s.Choose(key, value));
    static Step Check(string key, bool value) => new($"spunta {key} {value}", s => s.Check(key, value));
    static Step Focus(string key) => new($"fuoco su {key}", s => s.Focus(key));
    static Step Blur() => new("fuoco fuori dal form", s => s.Blur());
    static Step Set(string key, string value, bool display = false) => new($"Set {key} «{value}» display={display}", s => s.Set(key, value, display));
    static Step Enable(string key, bool enabled, bool dim = false) => new($"Enable {key} {enabled} dim={dim}", s => s.Enable(key, enabled, dim));
    static Step Show(string key, bool show) => new($"ShowField {key} {show}", s => s.Show(key, show));
    static Step Commit() => new("Commit", s => s.Commit());
    static Step UpdateNote(string key) => new($"UpdateNote {key}", s => s.UpdateNote(key));
    static Step Expand(string title, bool expanded) => new($"gruppo «{title}» aperto={expanded}", s => s.Expand(title, expanded));
    static Step External(string description, Action<JsonObject> change) => new("fuori dal form: " + description, s => s.External(change));
    static Step ReadOnly() => new("sola lettura (revisione precedente)", s => s.ReadOnly());

    static IEnumerable<Scenario> Scenarios()
    {
        // Vertical pile, general data (SheetEditor.BuildGeo, bored pile): symbol columns, choices, booleans, real default sheet.
        Field[] general = [new("tipo_palo", "Tipo di palo", Choices: ["Trivellato", "Elica continua", "Battuto"]), new("sottotipo_palo_battuto", "Tipo battuto", Choices: PileInputCatalog.DrivenPileTypes),
            new("diametro", "Diametro palo D", "m"), new("lunghezza", "Lunghezza palo L", "m"), new("peso_specifico_palo", "Peso specifico CLS / palo", "kN/m³"),
            new("presenza_falda", "Presenza falda", Bool: true), new("profondita_falda", "Profondità falda zf", "m"), new("considera_sottospinta", "Considera sottospinta", Bool: true),
            new("azione_compressione", "Azione assiale di progetto — Compressione", "kN"), new("azione_trazione", "Azione assiale di progetto — Trazione", "kN"),
            new("metodo_nq", "Metodo Nq", Choices: ["Parametrizzata"])];
        JsonObject Pile() => Archivio.NuovoFoglio("geo_palo_verticale")["generali"]!.AsObject().DeepClone().AsObject();
        yield return new("palo-verticale-generali", "X.Desktop/Wpf/GeoEditor.cs BuildGeo", Pile, general, new(SymbolColumns: true, HostModule: "geo_palo_verticale"), [],
        [
            Type("diametro", "1,2"), Choose("tipo_palo", "Battuto"), Enable("sottotipo_palo_battuto", true, true), Check("presenza_falda", true), Type("profondita_falda", "3.50"),
            Enable("profondita_falda", true, true), Set("lunghezza", "18"), Set("azione_compressione", "1500", true), Show("metodo_nq", false), Type("azione_trazione", ""),
            Choose("sottotipo_palo_battuto", PileInputCatalog.DrivenPileTypes[^1]), Check("presenza_falda", false), Enable("profondita_falda", false, true),
            Enable("considera_sottospinta", false), Show("metodo_nq", true), Set("tipo_palo", "Non previsto"), ReadOnly(),
        ]);

        // Vertical pile, coefficients of the standard (GeoEditor): compact symbol columns, read-only correlation factors.
        Field[] norm = [new("verticali_indagate", "Verticali indagate", Choices: PileInputCatalog.InvestigatedProfiles), new("__xi3", "Correlazione ξ3", ReadOnly: true),
            new("__xi4", "Correlazione ξ4", ReadOnly: true), new("sicurezza_laterale_compressione", "Sicurezza laterale — Compressione γs"),
            new("sicurezza_laterale_trazione", "Sicurezza laterale — Trazione γt"), new("sicurezza_base", "Sicurezza di base γb"),
            new("peso_palo_sfavorevole", "Peso proprio palo — Sfavorevole γG"), new("peso_palo_favorevole", "Peso proprio palo — Favorevole γG")];
        yield return new("palo-verticale-coefficienti", "X.Desktop/Wpf/GeoEditor.cs normativeForm", Pile, norm, new(Compact: true, SymbolColumns: true, HostModule: "geo_palo_verticale"), [],
        [
            Choose("verticali_indagate", PileInputCatalog.InvestigatedProfiles[^1]), Set("__xi3", "1.65"), Set("__xi4", "1.55", true), Type("sicurezza_base", "1.35"),
            Type("sicurezza_laterale_trazione", "abc"), Set("sicurezza_base", "1,35"), Type("__xi3", "9"), Set("__xi3", "1.65"),
        ]);

        // c.a. section, SLE options (ConcreteStress): compact, wide choices, fixed help, notes, four groups, commit on focus loss
        // and presentation of the numbers of ConcreteWorkspace. The notes stand for the homogenization errors of the module.
        string? Positive(JsonObject v, string key) => J.Number(v[key]) is <= 0 ? key + ": valore non positivo" : null;
        string[] exposures = Anthea.Application.Concrete.ConcreteEditor.ExposureChoices;
        Field[] stress = [new("modello", "Analisi", Choices: ["Lineare", "Non lineare"], HelpTopic: WikiContextHelp.StressAnalysis.Uri),
            new("n_armature", "n armature", Note: v => Positive(v, "n_armature")), new("phi", "Viscosità φ armature", Note: v => Positive(v, "phi")),
            new("n_trefoli", "n trefoli (Ep riferimento)", Note: v => Positive(v, "n_trefoli")), new("phi_trefoli", "Viscosità φ trefoli", Note: v => Positive(v, "phi_trefoli")),
            new("__ep_ref", "Ep di riferimento", "MPa", ReadOnly: true), new("trazione_cls", "CLS resistente a trazione", Choices: ["No", "Sì"]),
            new("assi", "Assi delle azioni", Choices: ["Locali", "Principali", "Personalizzati"]), new("origine_x", "Origine x", "mm"), new("origine_y", "Origine y", "mm"),
            new("rotazione", "Rotazione assi", "°"), new("esposizione", "Esposizione", Choices: exposures), new("sensibilita", "Armatura", Choices: ["Poco sensibile", "Sensibile"]),
            new("durata", "Durata del carico", Choices: ["Lunga", "Breve"]), new("aderenza", "Barre", Choices: ["Migliorata", "Liscia"]),
            new("copriferro_fessure", "c barra (vuoto: dalle barre efficaci)", "mm"), new("spaziatura_fessure", "Interasse massimo (vuoto: auto)", "mm"),
            new("limite_fessure", "wlim di progetto (EC2/MC)", "mm")];
        JsonObject Stress() => new()
        {
            ["modello"] = "Lineare", ["n_armature"] = "15", ["phi"] = "", ["n_trefoli"] = "", ["phi_trefoli"] = "", ["__ep_ref"] = "195000.123", ["trazione_cls"] = "No",
            ["assi"] = "Locali", ["origine_x"] = "0", ["origine_y"] = "0", ["rotazione"] = "0", ["esposizione"] = exposures[1], ["sensibilita"] = "Poco sensibile",
            ["durata"] = "Lunga", ["aderenza"] = "Migliorata", ["copriferro_fessure"] = "", ["spaziatura_fessure"] = "", ["limite_fessure"] = ""
        };
        Group[] stressGroups = [new("Analisi e omogeneizzazione", ["modello", "n_armature", "phi", "n_trefoli", "phi_trefoli", "__ep_ref"], true),
            new("Verifiche SLE · ambiente e armatura", ["esposizione", "sensibilita", "durata", "aderenza", "limite_fessure"], true),
            new("Fessurazione · disposizione delle barre", ["copriferro_fessure", "spaziatura_fessure"], false),
            new("Avanzate · CLS teso e assi", ["trazione_cls", "assi", "origine_x", "origine_y", "rotazione"], false)];
        yield return new("sezione-ca-opzioni-sle", "X.Desktop/Wpf/ConcreteStress.cs", Stress, stress,
            new(Compact: true, WideChoices: true, HostModule: "str_palo", CommitOnFocusLoss: true, NumericPresentation: true), stressGroups,
        [
            Expand("Fessurazione · disposizione delle barre", true), Expand("Avanzate · CLS teso e assi", true),
            Focus("n_armature"), Type("n_armature", "1"), Type("n_armature", "12"), Type("n_armature", "12.5"), Blur(),
            Focus("phi"), Type("phi", "2"), Commit(), Blur(),
            Focus("origine_x"), Type("origine_x", "10.123456"), Set("origine_x", "5"), Blur(),
            Focus("rotazione"), Type("rotazione", "30.5"), Focus("origine_y"), Type("origine_y", "-0.000000001"), Blur(),
            Choose("modello", "Non lineare"), Type("copriferro_fessure", "45.555"), Set("limite_fessure", "0.3"), Set("rotazione", "12.345678", true),
            External("n armature 0", v => v["n_armature"] = "0"), UpdateNote("n_armature"), Set("n_armature", "0"),
            Enable("trazione_cls", false, true), Enable("trazione_cls", true), Show("phi_trefoli", false), Choose("assi", "Personalizzati"),
            Set("__ep_ref", "200000"), Expand("Analisi e omogeneizzazione", false), Expand("Analisi e omogeneizzazione", true), Show("phi_trefoli", true),
        ]);

        // c.a. section, moment-curvature (ConcreteCurvature): compact, wide choices, a default choice for sheets saved before the field.
        string[] tension = Anthea.Application.Concrete.ConcreteEditor.ResponseConcreteTensionModes;
        Field[] curvature = [new("N", "N costante (− compressione)", "kN"), new("theta", "Direzione del momento θ", "°"), new("passi", "Passi di carico"),
            new("frazione", "Frazione di MRd finale", "−"), new("campionamento", "Distribuzione dei passi", Choices: ["Quadratico", "Uniforme"]),
            new("trazione_cls", "CLS resistente a trazione", Choices: ["No", "Sì"]), new("cls_teso_risposte", "CLS teso nelle risposte", Choices: tension, Default: tension[0]),
            new("angoli", "Direzioni del dominio"), new("tolleranza_n", "Residuo N massimo al limite", "kN"), new("raffina_snervamento", "Bisezioni primo snervamento (0–30)")];
        JsonObject Curvature() => new() { ["N"] = "-1200", ["theta"] = "0", ["passi"] = "30", ["frazione"] = "1", ["campionamento"] = "Quadratico", ["trazione_cls"] = "No",
            ["angoli"] = "36", ["tolleranza_n"] = "0.5", ["raffina_snervamento"] = "12" };
        yield return new("sezione-ca-curvatura", "X.Desktop/Wpf/ConcreteCurvature.cs", Curvature, curvature,
            new(Compact: true, WideChoices: true, HostModule: "str_palo", CommitOnFocusLoss: true, NumericPresentation: true), [],
        [
            Choose("cls_teso_risposte", tension[0]), Choose("campionamento", "Uniforme"), Set("cls_teso_risposte", tension[^1], true), Choose("cls_teso_risposte", tension[0]),
            Type("passi", "40"), Set("N", "-1500.555"), Focus("theta"), Type("theta", "45.678"), Blur(), Set("trazione_cls", "Sì"),
        ]);

        // Retaining wall, materials (RetainingWallWorkspace.Materials): compact symbol columns, real catalogues and example, notes
        // (stress block, steel different from its catalogue), parameters of the library with their symbols.
        JsonObject Wall() => WallModel.Example("cantilever")["materials"]!.AsObject().DeepClone().AsObject();
        var m = Wall();
        string[] Catalogue(JsonObject[] catalog, string key) => catalog.Select(x => x.S("nome")).Append(m.S(key)).Append("Personalizzato").Distinct().ToArray();
        Field[] wall = new Field[] {
            new("gamma", "Peso specifico muro", "kN/m³"), new("classe_cls", "Calcestruzzo da normativa", Choices: Catalogue(MaterialInputCatalog.Concrete("NTC 2018"), "classe_cls")),
            new("materiale_cls_nome", "Nome CLS personalizzato"), new("fck", "fck", "MPa"),
            new("cls_diagramma", "Diagramma CLS", Choices: MaterialInputCatalog.ConcreteDiagrams, Note: FieldNotes.StressBlock),
            new("__fcd", "fcd", "MPa", ReadOnly: true), new("__ecm", "Ecm", "MPa", ReadOnly: true),
            new("classe_acciaio", "Acciaio da normativa", Choices: Catalogue(MaterialInputCatalog.Steel(false, "NTC 2018"), "classe_acciaio"), Note: WallModel.SteelDifference),
            new("materiale_acciaio_nome", "Nome acciaio personalizzato"), new("fyk", "fyk", "MPa"), new("steel_modulus_mpa", "Es", "MPa"),
            new("steel_fu_mpa", "fu", "MPa"), new("steel_eps_u", "εu", "‰"), new("steel_diagramma", "Diagramma acciaio", Choices: ["Elastoplastico", "Incrudente"]),
            new("__fyd", "fyd", "MPa", ReadOnly: true), new("alpha_cc", "αcc"), new("gamma_c", "γc"), new("gamma_s", "γs"),
            new("cover", "Copriferro netto adottato", "mm"), new("exposure", "Esposizione", Choices: exposures.Skip(1).ToArray()), new("creep", "Viscosità SLE", "−")
        }.Concat(WallModel.MaterialFields.Where(f => f.Key.EndsWith("_rd")).Select(p => new Field(p.Key, p.Label, p.Unit, Symbol: p.Symbol))).ToArray();
        yield return new("muri-materiali", "supporto/SUPERATI/ui-storica/muri/X.Desktop/Wpf/RetainingWallWorkspace.Materials.cs", Wall, wall,
            new(Compact: true, SymbolColumns: true, HostModule: "geo_muri_sostegno", NumericPresentation: true), [],
        [
            Choose("cls_diagramma", "Stress block"), Type("fck", "32,5"), Set("classe_cls", "Personalizzato", true), Type("fyk", "500"),
            External("εu diverso dal catalogo", v => v["steel_eps_u"] = "30"), UpdateNote("classe_acciaio"), Choose("cls_diagramma", MaterialInputCatalog.ConcreteDiagrams[0]),
            Set("__fcd", "18.13"), Type("cover", ""), Choose("steel_diagramma", "Incrudente"), Enable("materiale_cls_nome", false, true), Show("creep", false), ReadOnly(),
        ]);

        // Retaining wall, seismic action (RetainingWallWorkspace.Seismic): compact, wide choices, a boolean on the whole row.
        Field[] seismic = [new("enabled", "Considera il sisma", Bool: true),
            new("method", "Comportamento del muro / modello di spinta", Choices: ["Mononobe–Okabe", "Wood semplificato"]),
            new("source", "Come definire l’azione sismica", Choices: [WallModel.SeismicSite, WallModel.SeismicManual]),
            new("ag_g", "Accelerazione su suolo rigido allo SLV · ag/g", "−"),
            new("ss_mode", "Amplificazione del sottosuolo · Ss", Choices: [WallModel.AmplificationCalculated, WallModel.AmplificationAssigned]),
            new("soil_class", "Categoria dalla relazione geotecnica", Choices: ["Da scegliere", "A", "B", "C", "D", "E"]),
            new("f0", "Fattore spettrale del sito allo SLV · F₀", "−"), new("ss", "Coefficiente Ss assegnato", "−"),
            new("st_mode", "Amplificazione topografica · St", Choices: [WallModel.AmplificationCalculated, WallModel.AmplificationAssigned]),
            new("topography", "Forma del terreno circostante", Choices: [WallModel.TopographyFlat, WallModel.TopographySlope, WallModel.TopographyRidge]),
            new("slope", "Inclinazione media del pendio / rilievo", "°"),
            new("relief_height", "Altezza del pendio / rilievo (non del muro)", "m"), new("site_height", "Quota del muro sopra la base del pendio / rilievo", "m"),
            new("st", "Coefficiente St assegnato", "−"), new("kh", "Coefficiente orizzontale kh assegnato", "−"), new("kv", "Coefficiente verticale |kv| assegnato", "−")];
        JsonObject Seismic() => WallModel.Example("cantilever")["seismic"]!.AsObject().DeepClone().AsObject();
        yield return new("muri-sisma", "supporto/SUPERATI/ui-storica/muri/X.Desktop/Wpf/RetainingWallWorkspace.Seismic.cs", Seismic, seismic,
            new(Compact: true, WideChoices: true, HostModule: "geo_muri_sostegno", NumericPresentation: true), [],
        [
            Check("enabled", true), Choose("method", "Wood semplificato"), Choose("source", WallModel.SeismicManual), Type("kh", "0.12"), Type("ag_g", "0,20"),
            Choose("soil_class", "C"), Show("f0", false), Show("ss", false), Enable("slope", false, true), Check("enabled", false), Show("f0", true),
        ]);

        // Horizontal pile, assigned EJ (HorizontalWorkspace): compact, boolean, unit with a space, a wide text.
        Field[] ej = [new("ej_override", "EJ manuale motivato", Bool: true), new("ej_assegnato", "EJ assegnato", "kN m²"), new("ej_motivo", "Motivazione EJ", Wide: true)];
        yield return new("palo-orizzontale-ej", "X.Desktop/Wpf/HorizontalWorkspace.cs ejForm", () => new JsonObject { ["ej_override"] = false, ["ej_assegnato"] = "", ["ej_motivo"] = "" }, ej,
            new(Compact: true, HostModule: "geo_palo_orizzontale"), [],
        [
            Check("ej_override", true), Type("ej_assegnato", "123456.789"), Type("ej_motivo", "Sezione fessurata"), Enable("ej_assegnato", false, true), Enable("ej_assegnato", true, true), ReadOnly(),
        ]);

        // New concrete material (ConcreteMaterialEditor): wide name, wide choices, the stress block notice, not compact.
        Field[] material = [new("nome", "Nome", Wide: true), new("fck_mpa", "fck", "MPa"), new("cls_diagramma", "Diagramma", Choices: MaterialInputCatalog.ConcreteDiagrams, Note: FieldNotes.StressBlock)];
        yield return new("materiale-calcestruzzo", "X.Desktop/Wpf/ConcreteMaterialEditor.cs", () => new JsonObject { ["nome"] = "", ["fck_mpa"] = "30", ["cls_diagramma"] = MaterialInputCatalog.ConcreteDiagrams[0] }, material,
            new(WideChoices: true, HostModule: "mat_calcestruzzo"), [],
        [
            Type("nome", "C30/37 di progetto"), Choose("cls_diagramma", "Stress block"), Type("fck_mpa", "35"), Set("cls_diagramma", MaterialInputCatalog.ConcreteDiagrams[0], true),
        ]);
    }
}
