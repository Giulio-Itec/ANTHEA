using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using X.Core;

namespace X.Desktop;

internal sealed partial class BridgeWorkspace : UserControl, IDisposable
{
    internal JsonObject Data { get; }
    internal BridgeResult? Calculation { get; private set; }
    internal BridgeResult? DisplayedCalculation { get; private set; }
    internal bool ResultsAreStale => Calculation is null && DisplayedCalculation is not null;
    internal JsonObject? Result => Calculation?.Json();
    internal bool Busy { get; private set; }
    internal event Action? Modified;
    internal readonly BridgeDrawing Drawing = new();
    internal readonly TabControl Pages = new() { Margin = new Thickness(12, 8, 12, 0), BorderThickness = new Thickness(0), Background = Ui.Bg }, Results = new();
    internal readonly ComboBox StageChoice = new() { MinWidth = 200, MaxWidth = 360, Margin = new Thickness(4) };
    internal readonly ComboBox DisplayChoice = Ui.Choice(["Tensioni totali", "Contributi delle fasi", "Geometria"], "Tensioni totali");
    private readonly TextBlock status = Ui.Text("Preparazione della sezione…", 12), overview = Ui.Text("", 12), materialInfo = Ui.Text("", 12);
    private readonly TextBlock warnings = Ui.Text("", 12, color: Ui.Brush("#8B5916"));
    private readonly ContentControl stressTable = new(), propertiesTable = new(), classTable = new(), phaseTable = new(), geometryTable = new();
    private readonly StackPanel phaseForms = new();
    private readonly TextBox details = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, BorderThickness = new Thickness(0), Padding = new Thickness(12) };
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly List<InputForm> inputForms = [], phaseInputForms = [];
    internal IReadOnlyList<InputForm> InputForms => inputForms;
    private CancellationTokenSource? cancellation;
    private JsonObject[] displayedPhases = [];
    private JsonObject? selectedPhase;
    private bool followLatestStage = true;
    private int revision;
    private bool building = true, disposed;
    private BridgeGeometry? previewGeometry;
    private JsonObject? previewInput;
    private string geometryError = "";
    internal BridgeWorkspace(JsonObject data)
    {
        Data = data; BridgeSection.ValidateShape(data); BridgeSection.EnsureAccessoryDefaults(data); Background = Ui.Bg;
        RevisionInspection.Allow(StageChoice); RevisionInspection.Allow(DisplayChoice);
        SetValue(InputForm.CommitOnFocusLossProperty, true);
        SetValue(NumericPresentation.EnabledProperty, true);
        BuildInputs();
        Ui.Tab(Results, "Sollecitazioni", BuildActionsTable());
        Ui.Tab(Results, "Fasi e proprietà", Scroll(Ui.Stack(Block("Omogeneizzazione e proprietà per fase", propertiesTable),
            Block("Azioni ed equilibrio", phaseTable))));
        Ui.Tab(Results, "Sezione efficace", Scroll(Ui.Stack(Block("Pannelli di classe 4", classTable),
            Group("Proprietà geometriche · lorda ed efficace", geometryTable, true), Group("Convergenza ed equilibrio", details))));
        Ui.Tab(Results, "Tensioni", Ui.Dock(stressTable, Ui.Text("La trave accumula anche G1, portato dal solo acciaio. Le armature accumulano le fasi in cui partecipano; il confronto va fatto per contributo e quota, tenendo conto di Es/Ea.", 11, color: Ui.Muted)));
        Ui.Tab(Results, "Verifiche", Scroll(Ui.Stack(
            Ui.Text("Esiti locali della sezione. Leggere anche i controlli da completare: i controlli disattivati o privi di dati restano da verificare; fatica e reazione d’appoggio usano inviluppi dedicati.", 11, color: Ui.Muted),
            verificationTable, Group("Taglio · parametri e resistenze", shearResults, true), Group("Pioli · flusso per fase e resistenze", studResults, true))));
        BuildLayout();
        StageChoice.SelectionChanged += (_, _) =>
        {
            if (Busy) return;
            if (StageChoice.SelectedIndex is var i && i >= 0 && i < displayedPhases.Length) selectedPhase = displayedPhases[i];
            followLatestStage = StageChoice.SelectedIndex == displayedPhases.Length - 1;
            ShowResults(); ViewChanged();
        };
        DisplayChoice.SelectionChanged += (_, _) => { RefreshDrawing(); ViewChanged(); };
        Results.SelectionChanged += (_, e) => { if (ReferenceEquals(e.Source, Results)) ViewChanged(); };
        timer.Tick += Tick; building = false; RefreshPreview(); timer.Start();
    }
    private static ScrollViewer Scroll(UIElement body) => new ChainedScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private static UIElement Block(string title, UIElement body, string? subtitle = null)
    {
        var head = Ui.Text(title, 15, true); head.Margin = new Thickness(0, 12, 0, 8);
        var s = Ui.Stack(head); if (subtitle is not null) { var text = Ui.Text(subtitle, 11, color: Ui.Muted); text.Margin = new Thickness(0, 0, 0, 7); s.Children.Add(text); } s.Children.Add(body); return s;
    }
    private InputForm Form(JsonObject data, Field[] fields, Action<string>? handler = null)
    {
        var form = new InputForm(data, fields, key => { handler?.Invoke(key); Changed(); }, true, true);
        inputForms.Add(form); return form;
    }
    private void BuildInputs()
    {
        var norm = Form(Data, [new("nome", "Nome della sezione", Wide: true), new("normativa", "Normativa", Choices: BridgeSection.Standards)], key =>
            {
                if (key == "normativa")
                {
                    Data["gamma_m0"] = Data.S("normativa").StartsWith("NTC") ? "1.05" : "1.0";
                    Data["gamma_m1"] = "1.1"; // NTC Table 4.2.VII and EN 1993-2:2006 §6.1, bridges.
                    Dispatcher.BeginInvoke(BuildPhases);
                    Data["alpha_cc"] = Data.S("normativa").StartsWith("NTC") ? "0.85" : "1.0";
                    Dispatcher.BeginInvoke(() => { foreach (var f in inputForms) { f.Set("gamma_m0", Data.S("gamma_m0"), true); f.Set("gamma_m1", Data.S("gamma_m1"), true); f.Set("alpha_cc", Data.S("alpha_cc"), true); } });
                }
            });
        var coefficients = Form(Data, [new("gamma_m0", "Acciaio strutturale γM0"), new("gamma_m1", "Instabilità γM1"), new("eta_taglio", "Taglio η"), new("gamma_v", "Pioli γV"), new("gamma_m2", "Saldature γM2"), new("gamma_c", "Calcestruzzo γc"), new("alpha_cc", "Calcestruzzo αcc"), new("gamma_s", "Armature γs")]);
        InputForm? materials = null;
        materials = Form(Data, [new("classe_cls", "Calcestruzzo", Choices: BridgeSection.ConcreteNames), new("acciaio", "Carpenteria", Choices: BridgeSection.SteelNames),
            new("armatura", "Armatura ordinaria", Choices: BridgeSection.RebarNames), new("fy_override", "Sovrascrivi fy per tutta la carpenteria", Bool: true), new("fy", "fy assegnato", "MPa")],
            _ => materials?.ShowField("fy", Data.B("fy_override")));
        materials.ShowField("fy", Data.B("fy_override"));
        foreach (string key in new[] { "fy_override", "fy" }) materials.Editors[key].ToolTip =
            "Sostituisce il valore di catalogo con un unico fy per anima e piattabande. Inserire la tensione di snervamento in MPa, prima di γM0. Non modifica le armature e non applica correzioni automatiche in funzione dello spessore.";
        var materialBody = Ui.Stack(materials, Block("Proprietà adottate", materialInfo),
            Ui.Text("Se attivo, il valore assegnato sostituisce fy di catalogo per anima e tutte le piattabande.", 11, color: Ui.Muted));
        var slab = Form(Data, [new("b_cls", "Larghezza collaborante", "mm", Symbol: "b_eff"), new("h_cls", "Spessore soletta", "mm")]);
        var steel = Form(Data, [new("h_web", "Altezza libera anima", "mm"), new("t_web", "Spessore anima", "mm"),
            new("b_top", "Larghezza superiore", "mm"), new("t_top", "Spessore superiore", "mm"), new("b_bottom", "Larghezza inferiore 1", "mm"), new("t_bottom", "Spessore inferiore 1", "mm")]);
        InputForm? plate = null;
        plate = Form(Data, [new("plate2", "Seconda piattabanda inferiore", Bool: true), new("b_bottom2", "Larghezza inferiore 2", "mm"), new("t_bottom2", "Spessore inferiore 2", "mm")],
            _ => { plate?.ShowField("b_bottom2", Data.B("plate2")); plate?.ShowField("t_bottom2", Data.B("plate2")); });
        plate.ShowField("b_bottom2", Data.B("plate2")); plate.ShowField("t_bottom2", Data.B("plate2"));
        var geometry = Ui.Stack(Block("Soletta", slab, "b_eff già comprensiva della larghezza collaborante adottata."), Block("Carpenteria saldata", steel), plate,
            Ui.Text("Piastra 2 centrata sotto la piastra 1. Nel calcolo: t_eq = t₁ + t₂; b_eq = (b₁t₁ + b₂t₂) / t_eq. La vista mantiene i due rettangoli reali.", 11, color: Ui.Muted));
        var reinforcement = new StackPanel();
        foreach (string side in new[] { "top", "bottom" })
        {
            InputForm? bars = null;
            bars = Form(Data, [new("rebars_" + side, "Fila presente", Bool: true), new("d_" + side, "Diametro barre", "mm"), new("pitch_" + side, "Passo", "mm"), new("cover_" + side, "Faccia → asse barra", "mm")],
                _ => { foreach (string k in new[] { "d_", "pitch_", "cover_" }) bars?.ShowField(k + side, Data.B("rebars_" + side)); });
            foreach (string k in new[] { "d_", "pitch_", "cover_" }) bars.ShowField(k + side, Data.B("rebars_" + side));
            reinforcement.Children.Add(Block(side == "top" ? "Armatura superiore" : "Armatura inferiore", bars));
        }
        reinforcement.Children.Add(Ui.Text("Le file possono essere entrambe assenti. Le barre sono distribuite e centrate secondo il passo; la distanza inserita è all’asse, non il copriferro netto.", 11, color: Ui.Muted));
        pageInputs.Add(Scroll(Ui.Stack(norm,
            Ui.Text("Dati comuni a tutte le situazioni. Coefficienti modificabili per l’Appendice Nazionale applicabile.", 11, color: Ui.Muted),
            Group("Coefficienti da normativa", coefficients), Group("Materiali", materialBody), Group("Geometria", geometry, true), Group("Armature", reinforcement), BuildAccessories())));
        pageInputs.Add(Scroll(Ui.Stack(
            Form(Data, [new("stato", "Limiti tensionali", Choices: ["SLU", "SLE rara", "SLE quasi permanente"]),
                new("classe4", "Riduzioni locali · classe 4", Bool: true), new("y_ref", "Quota comune di N", "mm")]),
            Ui.Text("y = 0 all’interfaccia; positivo verso l’alto. La quota comune si usa solo per le fasi che la selezionano; ogni fase può applicare N al proprio baricentro.", 11, color: Ui.Muted),
            Notice("N > 0 trazione; Mx > 0 comprime la parte superiore. Inserire i soli incrementi di carico già combinati: le fasi precedenti sono sommate automaticamente."),
            phaseForms,
            Ui.Bar(Ui.Button("+ Aggiungi fase", () => AddPhase(false)), Ui.Button("+ Ritiro", () => AddPhase(true))))));
        BuildPhases();
    }
    private void BuildPhases()
    {
        foreach (var form in phaseInputForms) inputForms.Remove(form);
        phaseInputForms.Clear(); int staticForms = inputForms.Count;
        homogenizationForms.Clear();
        phaseForms.Children.Clear();
        int index = 0;
        foreach (JsonObject p in Data.Array("fasi").OfType<JsonObject>().ToArray())
        {
            // Old archives keep the original common application point.
            p["riferimento_N"] ??= BridgeSection.CommonLoadReference;
            p["V"] ??= 0; p["epsilon_cs"] ??= 0; p["q_conn"] ??= 0;
            int i = index++;
            var form = Form(p, [new("nome", "Nome", Wide: true), new("attiva", "Includi nella somma", Bool: true), new("tipo", "Sezione reagente", Choices: BridgeSection.PhaseKinds),
                new("N", "Forza assiale N", "kN"), new("Mx", "Momento Mx", "kNm"), new("V", "Taglio V", "kN"), new("q_conn", "Scorrimento aggiuntivo Δq", "kN/m"), new("epsilon_cs", "Ritiro Δεcs · negativo = accorciamento", "µε"),
                new("riferimento_N", "Punto di applicazione N e riferimento Mx", Choices: BridgeSection.LoadReferences)], key =>
                { if (key == "tipo") { if (p.S("tipo") == BridgeSection.ShrinkageKind) { p["psi"] = .55; p["modo"] = "Da φ"; } Dispatcher.BeginInvoke(BuildPhases); } });
            bool shrinkage = p.S("tipo") == BridgeSection.ShrinkageKind;
            foreach (string key in new[] { "N", "Mx", "V", "riferimento_N" }) form.ShowField(key, !shrinkage);
            form.ShowField("epsilon_cs", shrinkage);
            form.ShowField("q_conn", Data.B("pioli") && p.S("tipo") != "Solo acciaio");
            var commands = Ui.Bar(Ui.Button("↑", () => MovePhase(i, -1)), Ui.Button("↓", () => MovePhase(i, 1)), Ui.Button("Elimina", () => { if (Data.Array("fasi").Count <= 1) return; ActionsTable.Commit(); Data.Array("fasi").Remove(p); BuildPhases(); Changed(); }));
            var phaseBody = Ui.Stack(form);
            if (shrinkage) phaseBody.Children.Add(Ui.Text("Deformazione uniforme imposta al solo CLS. Inserire l’incremento di ritiro; −250 µε = −0,25‰. Le forze equivalenti sono calcolate e non sono carichi esterni.", 11, color: Ui.Muted));
            if (PhaseHomogenizationNeeded(p.S("tipo")))
            {
                var h = Form(p, [new("phi", "Viscosità φ"), new("psi", "Moltiplicatore ψL"), new("n", "Fattore di calcolo n")],
                    key => p["modo"] = key == "n" ? "Da n" : "Da φ");
                var info = Ui.Text("", 11, color: Ui.Muted);
                homogenizationForms.Add((p, h, info));
                phaseBody.Children.Add(Block("Omogeneizzazione", Ui.Stack(h, info,
                    Ui.Text("n ↔ φ: modificare uno aggiorna l’altro. ψL resta modificabile e aggiorna n a φ costante.", 11, color: Ui.Muted))));
            }
            else phaseBody.Children.Add(Ui.Text(p.S("tipo") == "Solo acciaio" ? "Calcestruzzo e armature non partecipano." : "Calcestruzzo interamente escluso; carpenteria e armature partecipano con i rispettivi E.", 11, color: Ui.Muted));
            phaseBody.Children.Add(commands);
            phaseForms.Children.Add(Group($"{i + 1:00}  {p.S("nome")}", phaseBody, true));
        }
        phaseInputForms.AddRange(inputForms.Skip(staticForms));
        SynchronizeHomogenization();
        UpdateCommonReferenceField();
        RefreshActionsTable(); RefreshSectionProperties();
    }
    private void MovePhase(int i, int delta)
    {
        ActionsTable.Commit();
        var list = Data.Array("fasi"); int target = i + delta; if (target < 0 || target >= list.Count) return;
        var phase = list[i]!; list.RemoveAt(i); list.Insert(target, phase); BuildPhases(); Changed();
    }
    internal void Commit() { ActionsTable.Commit(); slabPropertyForm?.Commit(); foreach (var f in inputForms.ToArray()) f.Commit(); SaveView(); }
    private void Changed()
    {
        if (building || disposed) return;
        SynchronizeHomogenization();
        UpdateCommonReferenceField();
        RefreshActionsTable();
        revision++; cancellation?.Cancel(); Calculation = null;
        if (DisplayedCalculation is null) ClearResults();
        RefreshPreview(); UpdateResultNotice();
        status.Text = "Dati modificati · aggiornamento automatico in attesa…"; timer.Stop(); timer.Start(); Modified?.Invoke();
    }
    private void RefreshPreview()
    {
        try
        {
            previewGeometry = BridgeSection.Geometry(Data); previewInput = (JsonObject)Data.DeepClone(); geometryError = ""; var m = BridgeSection.Materials(Data);
            materialInfo.Text = $"{m.Concrete.Name}   fck = {F(Math.Abs(m.Concrete.Fck))} MPa\nEcm = {F(m.Concrete.ElasticModulusCompression)} MPa\n\n{m.Steel.Name}   fy = {F(m.Steel.Fyk)} MPa\nEa = {F(m.Steel.ElasticModulusTension)} MPa\n\n{m.Rebar.Name}   fyk = {F(m.Rebar.Fyk)} MPa\nEs = {F(m.Rebar.ElasticModulusTension)} MPa";
        }
        catch (Exception ex) { geometryError = ex.Message; materialInfo.Text = ex.Message; }
        RefreshSectionProperties();
        RefreshDetailSketch();
        RefreshDrawing();
    }
    private async void Tick(object? sender, EventArgs e) { timer.Stop(); if (!Busy) await CalculateAsync(false); else timer.Start(); }
    internal async Task CalculateAsync(bool commit = true)
    {
        if (disposed || Busy) return; if (commit) Commit(); timer.Stop(); Busy = true; progress.Visibility = Visibility.Visible;
        int requested = revision; cancellation?.Dispose(); cancellation = new(); var token = cancellation.Token;
        var snapshot = (JsonObject)Data.DeepClone(); status.Text = "Calcolo delle fasi e della sezione efficace…";
        var activePhases = Data.Array("fasi").OfType<JsonObject>().Where(p => p.B("attiva")).ToArray();
        try
        {
            var computed = await Task.Run(() => BridgeSection.Calculate(snapshot, token), token);
            if (disposed || requested != revision) return;
            int previous = selectedPhase is null ? StageChoice.SelectedIndex : Array.IndexOf(activePhases, selectedPhase);
            Calculation = computed; DisplayedCalculation = computed; displayedPhases = activePhases;
            StageChoice.ItemsSource = computed.Stages.Select((s, i) => $"{i + 1:00} · Dopo {s.Name}").ToArray();
            if (previous < 0 && selectedPhase is null) previous = (int)viewSettings.D("fase", -1);
            StageChoice.SelectedIndex = followLatestStage ? computed.Stages.Count - 1 : previous >= 0 && previous < computed.Stages.Count ? previous : computed.Stages.Count - 1;
            selectedPhase = activePhases[StageChoice.SelectedIndex];
            ShowResults(); UpdateResultNotice(); status.Text = $"Aggiornato · {computed.Stages.Count} situazioni · geometria e risultati coerenti con i dati correnti";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!disposed && requested == revision) { Calculation = null; if (DisplayedCalculation is null) ClearResults(); UpdateResultNotice(); status.Text = "Dati da verificare: " + ex.Message; } }
        finally { Busy = false; progress.Visibility = Visibility.Collapsed; if (!disposed && requested != revision) timer.Start(); }
    }
    private void ClearResults()
    {
        Drawing.Stage = null; Drawing.InvalidateVisual(); overview.Text = "Risultati da aggiornare"; warnings.Text = "";
        foreach (var host in new[] { stressTable, propertiesTable, classTable, phaseTable, geometryTable, verificationTable, shearResults, studResults }) host.Content = Ui.Text("Nessun risultato aggiornato", 12, color: Ui.Muted);
        details.Text = "";
        summaryCards.Text = "Nessun risultato aggiornato";
    }
    private void ShowResults()
    {
        if (DisplayedCalculation is not { } result || StageChoice.SelectedIndex < 0 || StageChoice.SelectedIndex >= result.Stages.Count) return;
        var stage = result.Stages[StageChoice.SelectedIndex]; var g = result.Geometry;
        RefreshDrawing();
        double removed = Math.Max(0, g.WebHeight - stage.Effective.WebTop - stage.Effective.WebBottom);
        overview.Text = $"|σa|max = {F(stage.Points.Where(p => p.Material == "Acciaio").Max(p => Math.Abs(p.Stress)))} MPa\nσc,min = {F(stage.Points.Where(p => p.Material == "CLS").Min(p => p.Stress))} MPa\n\nAnima inefficace: {F(removed)} mm\nAeff / Alorda = {F(stage.EffectiveSteel.Area / g.SteelArea)}\nConvergenza: {stage.Iterations} iterazioni";
        summaryCards.Start();
        foreach (string material in new[] { "Acciaio", "CLS", "Armatura" })
        {
            var points = stage.Points.Where(p => p.Material == material && p.Active).ToArray();
            if (points.Length > 0) summaryCards.AddCheck(material + " · tensioni", points.Length, points.Select(p => (p.Name, p.Utilization, p.Utilization is { } u ? (bool?)(u <= 1) : null)));
        }
        ShowAccessoryResults(stage);
        warnings.Text = string.Join("  ", stage.Warnings);
        stressTable.Content = ResultTable(["Punto", "y [mm]", ..stage.Contributions.Select((_, i) => $"Δσ {i + 1} [MPa]"), "Σσ [MPa]"],
            stage.Points.Select(p => new[] { p.Name, F(p.Y) }.Concat(p.Contributions.Select(F)).Append(p.Active ? F(p.Stress) : "—").ToArray()));
        verificationTable.Content = ResultTable(["Controllo", "Stato limite", "Domanda", "Limite", "η", "Esito", "Note"],
            stage.Points.Select(p => new[] { p.Name, result.Input.S("stato"), p.Active ? F(p.Stress) : "—", p.Active ? F(p.Limit) : "—", p.Utilization is { } u ? F(u) : "—",
                !p.Active ? "Non attivo" : p.Utilization is null ? "CLS teso" : p.Utilization <= 1 ? "Entro limite locale" : "Limite superato", "σ e limite in MPa" }).Concat((stage.Shear?.Checks ?? []).Concat(stage.Studs?.Checks ?? []).Select(c => new[] { c.Name, result.Input.S("stato"), F(c.Demand) + " " + c.Unit, F(c.Resistance) + " " + c.Unit, c.Ratio is {} r ? F(r) : "—", c.Status, c.Note })));
        propertiesTable.Content = ResultTable(["Fase", "n₀", "n = Ea/Ec,eff", "φ", "ψLφ", "Ec,eff [MPa]", "A* [mm²]", "yG [mm]", "Ix* [mm⁴]", "Wsup* [mm³]", "Winf* [mm³]", "yσ=0 [mm]", "κ [1/m]"],
            stage.Contributions.Select(c => new[] { c.Name, Dash(c.N0), Dash(c.HomogenizationN), c.HasConcrete ? F(c.Phi) : "—", c.HasConcrete ? F(c.EffectivePhi) : "—",
                c.HasConcrete ? F(result.Materials.Ea / c.HomogenizationN) : "—", F(c.Area), F(c.Centroid), E(c.Inertia), E(c.WTop), E(c.WBottom), c.NeutralAxis is { } z ? F(z) : "—", E(-c.StressSlope / result.Materials.Ea * 1000) }));
        classTable.Content = ResultTable(["Pannello", "b [mm]", "t [mm]", "σ₁ [MPa]", "σ₂ [MPa]", "ψ", "kσ", "λp", "ρ", "bc [mm]", "b₁ eff [mm]", "b₂ eff [mm]"],
            new[] { ("Anima · sup → inf", stage.Effective.Web), ("Sbalzo superiore", stage.Effective.Top), ("Sbalzo inferiore eq.", stage.Effective.Bottom) }.Select(x =>
                new[] { x.Item1, F(x.Item2.Width), F(x.Item2.Thickness), F(x.Item2.StartStress), F(x.Item2.EndStress), F(x.Item2.Psi), F(x.Item2.KSigma), F(x.Item2.Lambda), F(x.Item2.Rho), F(x.Item2.CompressedWidth), F(x.Item2.EffectiveAtStart), F(x.Item2.EffectiveAtEnd) }));
        phaseTable.Content = ResultTable(["Fase", "Sezione", "Riferimento N", "yN [mm]", "ΔN [kN]", "ΔMx al punto N [kNm]", "ΔV [kN]", "ΣN [kN]", "ΣMx a y=0 [kNm]", "ΣV [kN]", "Δεcs [µε]", "N eq. [kN]", "M eq. a y=0 [kNm]", "σc impedita [MPa]", "As [mm²]", "Ix* integrazione [mm⁴]", "Residuo equilibrio"],
            stage.Contributions.Select((c, i) => new[] { c.Name, c.Kind, c.LoadReference, F(c.LoadY), F(c.N), F(c.Mx), F(c.V), F(stage.Contributions.Take(i + 1).Sum(x => x.N)), F(stage.Contributions.Take(i + 1).Sum(x => x.MomentAtInterface)), F(stage.Contributions.Take(i + 1).Sum(x => x.V)),
                c.IsShrinkage ? F(c.ShrinkageStrain * 1e6) : "—", c.IsShrinkage ? F(c.EquivalentN) : "—", c.IsShrinkage ? F(c.EquivalentMomentAtInterface) : "—", c.IsShrinkage ? F(c.ConcreteStressOffset) : "—", F(c.RebarArea), E(c.SolverInertia), E(c.EquilibriumResidual) }));
        geometryTable.Content = ResultTable(["Proprietà", "Valore", "Unità"], new[] {
            new[] { "Area carpenteria lorda (equivalente)", F(g.SteelArea), "mm²" }, new[] { "Baricentro carpenteria lorda", F(g.SteelCentroid), "mm" }, new[] { "Ix carpenteria lorda", E(g.SteelInertia), "mm⁴" },
            new[] { "Area carpenteria efficace", F(stage.EffectiveSteel.Area), "mm²" }, new[] { "Aeff / Alorda", F(stage.EffectiveSteel.Area / g.SteelArea), "—" },
            new[] { "Baricentro carpenteria efficace", F(stage.EffectiveSteel.Centroid), "mm" }, new[] { "Spostamento baricentro efficace − lordo", F(stage.EffectiveSteel.Centroid - g.SteelCentroid), "mm" },
            new[] { "Ix carpenteria efficace", E(stage.EffectiveSteel.Inertia), "mm⁴" }, new[] { "Ieff / Ilorda", F(stage.EffectiveSteel.Inertia / g.SteelInertia), "—" },
            new[] { "Anima inefficace · limite inferiore y", F(-g.TopThickness - g.WebHeight + stage.Effective.WebBottom), "mm" },
            new[] { "Anima inefficace · limite superiore y", F(-g.TopThickness - stage.Effective.WebTop), "mm" },
            new[] { "Piattabanda inferiore · b equivalente", F(g.BottomEquivalentWidth), "mm" }, new[] { "Piattabanda inferiore · t equivalente", F(g.BottomEquivalentThickness), "mm" },
            new[] { "Piastre inferiori · area reale = equivalente", F(g.BottomArea), "mm²" }, new[] { "Piastre inferiori · yG reale", F(g.BottomRealCentroid), "mm" },
            new[] { "Piastre inferiori · yG equivalente", F(-g.Height + g.BottomEquivalentThickness / 2), "mm" }, new[] { "Piastre inferiori · Ix reale al proprio G", E(g.BottomRealInertia), "mm⁴" },
            new[] { "Piattabanda equivalente · Ix al proprio G", E(g.BottomEquivalentWidth * Math.Pow(g.BottomEquivalentThickness, 3) / 12), "mm⁴" },
            new[] { "Area soletta lorda", F(g.Width * g.SlabHeight), "mm²" }, new[] { "Barre complessive", g.Bars.Length.ToString(), "n." }, new[] { "Area armature", F(g.Bars.Sum(b => b.Area)), "mm²" },
            new[] { "Altezza complessiva", F(g.Height + g.SlabHeight), "mm" }, new[] { "Asse neutro delle tensioni totali nell’acciaio", stage.SteelNeutralAxis is { } z ? F(z) : "—", "mm" }
        }.Concat(g.Bars.GroupBy(b => b.Y).Select(r => new[] { $"Fila y={F(r.Key)} mm · {r.Count()} barre Ø{F(r.First().Diameter)}", F(r.Sum(b => b.Area)), "mm²" })));
        details.Text = $"Convergenza: {stage.Iterations} iterazioni.\nVariazione relativa delle larghezze: {stage.Residual:E3} · tolleranza 1E−7.\n\n" +
            $"Residuo massimo dell’equilibrio N–Mx: {stage.Contributions.Max(c => Math.Abs(c.EquilibriumResidual)):E3} · limite 1E−5.";
    }
    private void UpdateCommonReferenceField()
    {
        bool used = Data.Array("fasi").OfType<JsonObject>().Any(p => p.B("attiva") && p.S("tipo") != BridgeSection.ShrinkageKind && BridgeSection.LoadReference(p) == BridgeSection.CommonLoadReference);
        foreach (var form in inputForms.Where(f => f.Editors.ContainsKey("y_ref"))) form.Enable("y_ref", used, dim: true);
    }
    private void RefreshDrawing()
    {
        bool geometryPage = currentPage == 0;
        var result = DisplayedCalculation;
        var stage = result is not null && StageChoice.SelectedIndex >= 0 && StageChoice.SelectedIndex < result.Stages.Count ? result.Stages[StageChoice.SelectedIndex] : null;
        Drawing.Geometry = geometryPage || stage is null ? previewGeometry : result!.Geometry;
        Drawing.Input = geometryPage || stage is null ? previewInput : result!.Input;
        Drawing.Stage = geometryPage ? null : stage;
        Drawing.Mode = geometryPage ? 2 : DisplayChoice.SelectedIndex;
        GeometryLabels.Visibility = RebarLabels.Visibility = Drawing.Mode == 2 ? Visibility.Visible : Visibility.Collapsed;
        Drawing.IsStale = !geometryPage && ResultsAreStale;
        Drawing.LoadY = null;
        Drawing.LoadPoints = !geometryPage && stage is not null ? stage.Contributions.Select((c, i) => (c, i)).Where(p => !p.c.IsShrinkage).Select(p => new BridgeLoadPoint(p.i + 1, p.c.Name, p.c.LoadY, p.c.N)).ToArray() : [];
        RefreshStressScale();
        Drawing.InvalidateVisual();
        UpdateResultNotice();
    }
    private void UpdateResultNotice()
    {
        resultNotice.Text = currentPage == 0 ? (geometryError == "" ? "" : "Geometria precedente · " + geometryError)
            : ResultsAreStale ? "DA AGGIORNARE · Grafico e verifiche dell’ultimo calcolo valido. I nuovi dati sono in acquisizione o in calcolo; le esportazioni dei risultati attendono l’aggiornamento." : "";
        resultNotice.Visibility = resultNotice.Text == "" ? Visibility.Collapsed : Visibility.Visible;
        Drawing.IsStale = currentPage != 0 && ResultsAreStale;
    }
    private static DataGrid ResultTable(string[] headers, IEnumerable<string[]> rows)
    {
        var table = Ui.Table(headers, rows);
        foreach (var column in table.Columns)
        {
            column.Width = DataGridLength.Auto; column.MinWidth = 72; column.MaxWidth = 300;
            if (column is DataGridTextColumn textColumn)
            {
                var style = new Style(typeof(TextBlock), textColumn.ElementStyle);
                style.Setters.Add(new Setter(MarginProperty, new Thickness(6, 3, 6, 3)));
                style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
                style.Setters.Add(new Setter(ToolTipProperty, new System.Windows.Data.Binding("Text") { RelativeSource = System.Windows.Data.RelativeSource.Self }));
                textColumn.ElementStyle = style;
            }
        }
        return table;
    }
    internal static string F(double x) => x.ToString("0.###", CultureInfo.GetCultureInfo("it-IT"));
    private static string E(double? x) => x?.ToString("0.#####E+0", CultureInfo.GetCultureInfo("it-IT")) ?? "—";
    private static string Dash(double x) => x > 0 ? F(x) : "—";
    private void ExportCsv()
    {
        if (Calculation is null || Busy) { status.Text = "Attendere risultati aggiornati prima dell’esportazione."; return; }
        var dialog = new SaveFileDialog { Filter = "Tabella CSV|*.csv", FileName = "sezione_mista_tensioni.csv" };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        var lines = new List<string> { "Situazione;Punto;Materiale;y [mm];Sigma [MPa];Limite [MPa];Eta;Attivo" };
        string Q(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
        foreach (var s in Calculation.Stages) foreach (var p in s.Points) lines.Add(string.Join(";", Q(s.Name), Q(p.Name), Q(p.Material), p.Y.ToString("R", CultureInfo.GetCultureInfo("it-IT")), p.Stress.ToString("R", CultureInfo.GetCultureInfo("it-IT")), F(p.Limit), p.Utilization is { } u ? u.ToString("R", CultureInfo.GetCultureInfo("it-IT")) : "", p.Active));
        Archivio.ScriviAtomico(dialog.FileName, Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, lines))).ToArray());
    }
    public void Dispose() { disposed = true; revision++; timer.Stop(); timer.Tick -= Tick; cancellation?.Cancel(); }
}
