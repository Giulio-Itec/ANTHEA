using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Anthea.Application.Modules;
using Anthea.Application.Presentation;
using Anthea.Application.Reporting;
using Anthea.Application.Walls;
using Anthea.Calculations;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.RetainingWall;
using CommunityToolkit.Mvvm.Input;
using Anthea.Presentation.Services;
using Anthea.Testing.Comparison;
using Anthea.Testing.Normalization;

namespace Anthea.Presentation.Tests;

/// <summary>
/// The ViewModel of the retaining walls (F5.9) against its historic view, without WPF. This is the evidence of equivalence that survives
/// the archiving of the historic view:
/// <list type="bullet">
/// <item>every scenario of tests/Shared/RetainingWallTrace.cs (the path of the F3 capture for each fixture of the module, edits of every
/// kind, matrix, results and the section for the module c.a., global stability, the window of the values with its calculation, the
/// search of the reinforcement, read-only, the background calculation with a held engine) gives the trace of the historic view frozen
/// in tests/fixtures/f5-muri/storica, line by line;</item>
/// <item>for each F3 fixture of the module the data after opening, visit, calculation, export and reopening, the result, the status and
/// the text and structure of the report are byte-identical to the F3 reference of the historic view, with the F4 transitions of the
/// four old fixtures admitted exactly as ui/f3-equivalence admits them;</item>
/// <item>module and keys of interface state; the background calculation with barriers (thread, busy until the engine returns, only the
/// current revision published, closing during a calculation); read-only from the context, also in the window of the values; Reload.</item>
/// </list>
/// One "UI" thread (SingleThreadContext), as on the dispatcher.
/// </summary>
internal static class RetainingWallSheetChecks
{
    internal static int Run(string root, string output) => SingleThreadContext.Run(_ => RunAsync(root, output));

    private static readonly RetainingWallSheetModule Module = new(new PlanFigures());

    private static RetainingWallSheetViewModel Open(JsonObject data, IModuleService? service = null, Host? host = null, bool readOnly = false,
        PresentationServices? services = null, ISheetHost? routing = null) =>
        (RetainingWallSheetViewModel)Module.CreateSheet(new SheetContext((JsonObject)data.DeepClone(), readOnly, routing ?? host ?? new Host(),
            services ?? Scripted.Services(), service ?? ModuleServices.Get(RetainingWallScenarios.Module), new CalculationLimiter(CalculationLimiter.DefaultMaximum)));

    private static async Task<int> RunAsync(string root, string output)
    {
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        // The culture of the captures of the historic view (F3 and traces).
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("it-IT");
        try
        {
            var expect = new Expect();
            string traces = Path.Combine(output, "muri");
            Directory.CreateDirectory(traces);
            foreach (var scenario in RetainingWallScenarios.All(root))
            {
                var lines = await TraceAsync(root, scenario);
                RetainingWallTrace.Write(Path.Combine(traces, scenario.Name + ".txt"), lines);
                string stored = Path.Combine(RetainingWallScenarios.Reference(root), scenario.Name + ".txt");
                expect.That(File.Exists(stored), "Traccia della vista storica assente: " + stored);
                string? difference = RetainingWallTrace.FirstDifference(RetainingWallTrace.Read(stored), lines);
                expect.That(difference is null, "Muri MVVM diversi dalla vista storica, scenario " + scenario.Name + ", " + difference);
            }
            var fixtures = RetainingWallScenarios.Fixtures(root);
            expect.That(fixtures.Count == 16, "Fixture F3 dei muri: " + fixtures.Count);
            var transitions = Transition.Load(Path.Combine(root, "tests", "fixtures", "f3", "transitions-f4.json"))
                .Where(t => t.File.StartsWith("sheets/" + RetainingWallScenarios.Module + "/", StringComparison.Ordinal)).ToList();
            foreach (string id in fixtures) await F3Async(root, traces, id, transitions.Select(t => t.File).ToHashSet(StringComparer.Ordinal), expect);
            CompareTransitions(root, traces, transitions, expect);
            Contract(expect);
            await StateKeysAsync(root, expect);
            await BackgroundAsync(root, expect);
            await ReadOnlyAsync(root, expect);
            await ReloadAsync(root, expect);
            await ExportsAsync(root, output, expect);
            await ViewContractAsync(root, output, expect);
            return expect.Count;
        }
        finally { CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = uiCulture; }
    }

    // ---------------------------------------------------------------- traces

    /// <summary>
    /// F5.10 (section 5 of f5-10-sezione.md): the sections that the scenario «risultati-sezione» sends to the module c.a. («Apri sezione in c.a.»),
    /// with their hash in the trace of the walls, for the checks of the RC section. Runs on the UI thread of the checks (SingleThreadContext).
    /// </summary>
    internal static async Task<IReadOnlyList<(string Name, JsonObject Data, string Hash)>> SentSectionsAsync(string root)
    {
        var scenario = RetainingWallScenarios.All(root).Single(s => s.Name == "risultati-sezione");
        var engine = new HeldModuleService(RetainingWallScenarios.Module);
        var ids = new RetainingWallIds();
        var side = new Side(RetainingWallScenarios.FixtureData(root, scenario.Fixture), engine, ids);
        try
        {
            // The states of the trace, in order: the identifiers of the sections are named as the trace meets them.
            side.State();
            foreach (var step in scenario.Steps) { await side.ApplyAsync(step); side.State(); }
            return side.SentSections();
        }
        finally { engine.Release(); side.Dispose(); }
    }

    private static async Task<List<string>> TraceAsync(string root, RetainingWallScenario scenario)
    {
        var lines = new List<string>();
        var engine = new HeldModuleService(RetainingWallScenarios.Module);
        var ids = new RetainingWallIds();
        var side = new Side(RetainingWallScenarios.FixtureData(root, scenario.Fixture), engine, ids);
        try
        {
            var previous = side.State();
            lines.AddRange(RetainingWallTrace.Lines("apertura", null, previous));
            foreach (var step in scenario.Steps)
            {
                await side.ApplyAsync(step);
                var state = side.State();
                lines.AddRange(RetainingWallTrace.Lines(step.ToString(), previous, state));
                previous = state;
            }
        }
        // A step the ViewModel cannot apply ends the trace: the comparison then shows the first different line before it.
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or KeyNotFoundException or NullReferenceException)
        { lines.Add("errore " + error.GetType().Name + ": " + error.Message + " " + error.StackTrace?.Split('\n').FirstOrDefault()?.Trim()); }
        finally { engine.Release(); side.Dispose(); }
        return lines;
    }

    /// <summary>The ViewModel driven as its view would drive it, step by step: a control the user cannot use does nothing.</summary>
    private sealed class Side : IDisposable
    {
        private readonly HeldModuleService engine;
        private readonly RetainingWallIds ids;
        private Host host = new();
        private SoilDialogs soils = new();
        private int modified;
        private RetainingWallParametersViewModel? parameters;
        private Task parametersApplying = Task.CompletedTask;

        internal Side(JsonObject data, HeldModuleService engine, RetainingWallIds ids) { this.engine = engine; this.ids = ids; Attach(data); }

        internal RetainingWallSheetViewModel Sheet { get; private set; } = null!;

        private void Attach(JsonObject data)
        {
            host = new Host();
            // «Invia / carica terreno…» opens the shared soil transfer dialog (MU9, T5): the trace counts the dialogs shown, closed without a choice.
            soils = new SoilDialogs();
            Sheet = Open(data, engine, host, services: new PresentationServices(new ScriptedMessages(), new ScriptedConfirmations(), new ScriptedFileDialogs(), soils,
                new RecordingClipboard(), new RecordingLinks(), new RecordingWiki()));
            modified = 0;
            Sheet.Modified += (_, _) => modified++;
        }

        private void CloseParameters() { parameters?.Close(); parameters = null; }

        public void Dispose() { CloseParameters(); Sheet.Dispose(); }

        /// <summary>F5.10: the sections sent to the module c.a., with their hash in the trace.</summary>
        internal IReadOnlyList<(string Name, JsonObject Data, string Hash)> SentSections() =>
            host.Sections.Select(c => (c.Name, c.Data, new RetainingWallTraceState(ids).Hash(c.Data))).ToArray();

        private static bool Usable(RetainingWallSheetViewModel s, string form, FieldViewModel field) => field.IsVisible && field.IsEnabled && s.IsFormShown(form);

        private (string Form, FieldViewModel Field)? Editor(string path)
        {
            var parts = path.Split('/');
            return Sheet.Forms.TryGetValue(parts[0], out var form) && form.Contains(parts[1]) ? (parts[0], form[parts[1]]) : null;
        }

        private WallTableViewModel? Table(string name) => name switch
        {
            "valle" => Sheet.ValleyLayers, "monte" => Sheet.MountainLayers, "cedimenti" => Sheet.ServiceLayers, "azioni" => Sheet.Actions, "matrice" => Sheet.Matrix,
            "globale-monte" => Sheet.GlobalRearLayers, "globale-valle" => Sheet.GlobalValleyLayers, "matrice-globale" => Sheet.GlobalMatrix,
            _ when Sheet.ProfilePoints.TryGetValue(name, out var points) => points,
            _ => throw new ArgumentException("Tabella sconosciuta: " + name)
        };

        /// <summary>A table can be seen: only the valley column of the deep layers is in a card that the rules hide.</summary>
        private bool TableShown(string name) => name != "globale-valle" || Sheet.Cards["Strati globali valle"].IsVisible;

        internal async Task ApplyAsync(RetainingWallStep step)
        {
            var s = Sheet;
            if (RetainingWallScenarios.PageOf(step) is int page && s.Page != page) s.Page = page;
            switch (step.Kind)
            {
                case RetainingWallStep.Commit: s.Commit(); break;
                case RetainingWallStep.Calculate: await s.RecalculateAsync(); break;
                case RetainingWallStep.Start: _ = s.RecalculateAsync(); break;
                case RetainingWallStep.Wait: await Expect.Within(s.PendingWork); break;
                case RetainingWallStep.ReadOnly: s.IsReadOnly = true; break;
                case RetainingWallStep.Reopen:
                    var data = s.Data; CloseParameters(); s.Dispose(); Attach((JsonObject)data.DeepClone()); break;
                case RetainingWallStep.Hold: engine.Hold(); break;
                case RetainingWallStep.Entered: await engine.EnteredAsync(); break;
                case RetainingWallStep.Release: engine.Release(); break;
                case RetainingWallStep.ParametersWait: await Expect.Within(parametersApplying); break;
                case RetainingWallStep.Parameters:
                    var opened = await s.OpenParametersAsync();
                    if (opened is not null) { parameters = opened; opened.Closed += (_, _) => { if (ReferenceEquals(parameters, opened)) parameters = null; }; }
                    break;
                case RetainingWallStep.Field:
                    if (Editor(step.Key!) is ({ } form, { Kind: FieldKind.Text } text) && Usable(s, form, text) && !text.IsInputReadOnly)
                        s.Forms[form].Set(text.Key, step.Text!);
                    break;
                case RetainingWallStep.Choice:
                    if (Editor(step.Key!) is ({ } choiceForm, { Kind: FieldKind.Choice } choice) && Usable(s, choiceForm, choice) && choice.CanChange && choice.SelectedChoice != step.Text)
                        choice.SelectedChoice = step.Text;
                    break;
                case RetainingWallStep.Option:
                    if (Editor(step.Key!) is ({ } boxForm, { Kind: FieldKind.Bool } box) && Usable(s, boxForm, box) && box.CanChange) box.IsChecked = !box.IsChecked;
                    break;
                case RetainingWallStep.Family: s.Family = step.Text!; break;
                case RetainingWallStep.Example: s.LoadExample(step.Text!); break;
                case RetainingWallStep.Cell:
                {
                    var parts = step.Key!.Split('/');
                    var table = Table(parts[0]);
                    if (table is null || !s.CanEdit || !TableShown(parts[0]) || step.Row >= table.Rows.Count) break;
                    if (table.Columns.Single(c => c.Key == parts[1]).ReadOnly) break;
                    var row = table.Rows[step.Row];
                    if (step.Text is null) row[parts[1]] = !(row[parts[1]] is true);
                    else row[parts[1]] = step.Text;
                    break;
                }
                case RetainingWallStep.Select:
                {
                    var table = Table(step.Key!);
                    if (table is not null && TableShown(step.Key!)) table.SelectedIndex = step.Row;
                    break;
                }
                case RetainingWallStep.Add:
                    switch (step.Key)
                    {
                        case "azioni": if (s.CanEdit) { s.NewActionType = step.Text!; s.AddAction(); } break;
                        case "valle" or "monte": s.AddSoilLayer(step.Key == "valle"); break;
                        case "cedimenti": s.AddServiceLayer(); break;
                        case "globale-monte" or "globale-valle": if (TableShown(step.Key)) s.AddGlobalLayer(step.Key == "globale-valle"); break;
                        case "matrice" or "matrice-globale": break;
                        default: s.AddProfilePoint(step.Key!); break;
                    }
                    break;
                case RetainingWallStep.Remove:
                    switch (step.Key)
                    {
                        case "azioni": s.RemoveAction(); break;
                        case "valle" or "monte": s.RemoveSoilLayer(step.Key == "valle"); break;
                        case "cedimenti": s.RemoveServiceLayer(); break;
                        case "globale-monte" or "globale-valle": if (TableShown(step.Key)) s.RemoveGlobalLayer(step.Key == "globale-valle"); break;
                        case "matrice" or "matrice-globale": break;
                        default: s.RemoveProfilePoint(step.Key!); break;
                    }
                    break;
                case RetainingWallStep.Move: if (step.Key is "valle" or "monte") s.MoveSoilLayer(step.Key == "valle", step.Index < 0 ? -1 : 1); break;
                case RetainingWallStep.Button: await ButtonAsync(s, step.Text!); break;
                case RetainingWallStep.RemoveHistory: s.RemoveHistory(step.Index); break;
                case RetainingWallStep.Combination: s.SelectedCombination = step.Text; break;
                case RetainingWallStep.View: s.SelectedView = step.Text!; break;
                case RetainingWallStep.Member: s.SelectedMember = step.Text!; break;
                case RetainingWallStep.Filter: s.SelectedFilter = step.Text!; break;
                case RetainingWallStep.GlobalCombination: s.SelectedGlobalCombination = step.Text; break;
                case RetainingWallStep.ResultRow: s.SelectResultRow(step.Row); break;
                case RetainingWallStep.Toggle:
                    switch (step.Key)
                    {
                        case "Carichi": s.ShowLoads = !s.ShowLoads; break;
                        case "Dati": s.ShowLabels = !s.ShowLabels; break;
                        case "Armature": s.ShowRebar = !s.ShowRebar; break;
                        case "Spinte": s.ShowPressures = !s.ShowPressures; break;
                        case "Intero profilo": if (s.IsGlobalView) s.GlobalFullProfile = !s.GlobalFullProfile; break;
                    }
                    break;
                case RetainingWallStep.Parameter:
                    if (parameters is { IsReadOnly: false, IsEnabled: true } window && window.Rows.SingleOrDefault(r => r.Values.S("path") == step.Key) is { } parameter)
                        parameter["value"] = step.Text;
                    break;
                case RetainingWallStep.ParameterCombination: if (parameters is not null) parameters.SelectedCombination = step.Text; break;
                case RetainingWallStep.ParametersApply: if (parameters is not null) parametersApplying = parameters.ApplyAsync(); break;
                case RetainingWallStep.ParametersClose: CloseParameters(); break;
                default: throw new ArgumentException("Passo sconosciuto: " + step.Kind);
            }
        }

        private static async Task ButtonAsync(RetainingWallSheetViewModel s, string caption)
        {
            switch (caption)
            {
                case "Genera / ripristina automatiche": s.GenerateMatrix(); break;
                case "Copia riga": s.CopyCombination(); break;
                case "Elimina riga": s.RemoveCombination(); break;
                case "Conferma matrice": s.ConfirmMatrix(); break;
                case "Genera globali": s.GenerateGlobalMatrix(); break;
                case "Conferma matrice globale": s.ConfirmGlobalMatrix(); break;
                case "Prepara dal muro": s.PrepareGlobal(); break;
                case "Calcola globale": _ = s.CalculateGlobalAsync(); break;
                case "Sisma": s.ShowSeismic(); break;
                case "Stabilità globale": s.ShowGlobalSetup(); break;
                case "Adotta copriferro minimo": s.AdoptMinimumCover(); break;
                case "Calcola armature": _ = s.DesignRebarAsync(); break;
                case "Interrompi ricerca": s.InterruptSearch(); break;
                case "Applica proposta":
                    if (s.ProposalPanel?.Any(b => b.Command == RetainingWallSheetViewModel.ApplyProposalCommand) == true) s.ApplyProposal();
                    break;
                case "Relazione Word": s.RequestReport(); break;
                case "Invia / carica terreno…": await s.TransferSoilAsync(); break;
                case "Apri sezione in c.a.": s.SendSelectedSection(); break;
                case "+ Accelerogramma": s.AddHistory(); break;
                case "Modifica profilo, strati e ricerca":
                    if (s.ChecksPanel?.Any(b => b.Command == RetainingWallSheetViewModel.EditGlobalSetupCommand) == true) s.ShowGlobalSetup();
                    break;
                default: throw new ArgumentException("Pulsante sconosciuto: " + caption);
            }
            await Task.CompletedTask;
        }

        // ---------------------------------------------------------------- state

        internal RetainingWallTraceState State()
        {
            var s = Sheet;
            var t = new RetainingWallTraceState(ids);
            string Q(string? text) => RetainingWallTrace.Quote(text);
            string N(int value) => RetainingWallTrace.Number(value);
            string YN(bool value) => RetainingWallTrace.YesNo(value);
            t.Data(s.Data);
            t.Add("foglio", "modificato " + N(modified) + " · sola lettura " + YN(s.IsReadOnly) + " · occupato " + YN(s.IsBusy) + " · report " + N(host.Reports)
                + " · terreno " + N(soils.Shown));
            t.Add("sezioni", string.Join(" | ", host.Sections.Select(c => Q(c.Name) + " " + t.Hash(c.Data))));
            t.Add("risultato", s.Calculation is null ? "-" : t.Hash(s.Calculation.Json()));
            t.Add("globale", s.GlobalResult is null ? "-" : (s.HasIndependentGlobal ? "indipendente" : "dal calcolo") + " · casi " + N(s.GlobalResult.Cases.Length));
            t.Add("stato", Q(s.Status));
            t.Add("tipologia", s.Family + " · voci " + Q(string.Join("|", s.Families.Select(f => f.Name + (f.Available ? "" : " (spenta)")))));
            t.Add("pulsante.sisma", Q(s.SeismicButton) + " · " + Q(s.SeismicToolTip));
            t.Add("pulsante.globale", Q(s.GlobalSetupText) + " · " + Q(RetainingWallPresentation.GlobalSetupToolTip));
            t.Add("pulsante.armature", YN(s.CanDesign));
            t.Add("pagina", N(s.Page));
            t.Add("combinazioni", Q(string.Join("|", s.Combinations ?? [])) + " · scelta " + Q(s.SelectedCombination));
            t.Add("vista", Q(s.SelectedView) + " · elemento " + Q(s.SelectedMember) + (s.IsMemberShown ? "" : " nascosto") + " · filtro " + Q(s.SelectedFilter));
            t.Add("combinazioni-globali", Q(string.Join("|", s.GlobalCombinations ?? [])) + " · scelta " + Q(s.SelectedGlobalCombination) + (s.IsGlobalView ? "" : " · nascoste"));
            t.Add("interruttori", "Carichi=" + YN(s.ShowLoads) + " Dati=" + YN(s.ShowLabels) + " Armature=" + YN(s.ShowRebar) + " Spinte=" + YN(s.ShowPressures)
                + " Intero profilo=" + YN(s.GlobalFullProfile) + (s.IsGlobalView ? "" : " · profilo nascosto"));
            t.Add("riepilogo", Q(s.Summary));
            foreach (string key in RetainingWallScenarios.FormKeys.Concat(s.Forms.Keys.Where(k => k.StartsWith("history_", StringComparison.Ordinal))))
            {
                if (!s.Forms.TryGetValue(key, out var form)) continue;
                t.Add("form." + key, s.IsFormShown(key) ? "visibile" : "nascosto");
                foreach (var field in form.Fields) t.Field(FieldState(key, form, field));
            }
            foreach (var (name, card) in s.Cards.OrderBy(c => c.Key, StringComparer.Ordinal))
                t.Add("scheda." + name, (card.IsVisible ? "visibile" : "nascosta") + " · " + (card.IsExpanded ? "aperta" : "chiusa"));
            foreach (string name in RetainingWallScenarios.TableKeys)
            {
                var table = Table(name);
                if (table is null) { t.Add("tabella." + name, "-"); continue; }
                t.Table(name, table.Columns.Select(c => c.Label).ToArray(), table.Rows.Select(r => r.Values).ToArray(), table.SelectedIndex);
            }
            t.Add("testo.strati", Q(s.LayerStatus));
            t.Add("testo.matrice", Q(s.MatrixStatus));
            t.Add("testo.matrice-globale", Q(s.GlobalMessage));
            t.Add("testo.globale", Q(s.GlobalReadiness) + " · " + Q(s.GlobalReadinessToolTip));
            t.Add("testo.ricerca", Q(s.GlobalSearchSummary));
            t.Add("testo.sisma", Q(s.SeismicAudit));
            t.Add("testo.copriferro", Q(s.CoverStatus));
            t.Add("accelerogrammi", string.Join(" | ", s.Histories.Select(h => Q(h.Name) + " " + Q(h.Samples))));
            t.Panel("spinte", Blocks(s.PressurePanel, s.CanEdit));
            t.Panel("verifiche", Blocks(s.ChecksPanel, s.CanEdit));
            t.Add("sezione", Q(s.SectionSelection) + " · invio " + YN(s.CanSendSection));
            t.Panel("proposta", Blocks(s.ProposalPanel, s.CanEdit));
            var d = s.Drawing;
            t.Add("disegno", "caso " + Q(d.CaseName) + " · risultato " + YN(d.HasCalculation) + " · azione " + (d.SelectedAction ?? "-") + " · strato " + N(d.SelectedLayer)
                + (d.SelectedValley ? " valle" : "") + " · spinta " + N(d.SelectedPressure) + " · carichi " + YN(d.ShowLoads) + " · dati " + YN(d.ShowLabels)
                + " · armature " + YN(d.ShowRebar) + " · spinte " + YN(d.ShowPressures));
            var g = s.Diagrams;
            t.Add("diagrammi", (g.Visible ? "visibili" : "nascosti") + " · modo " + Q(g.Mode) + " · diagrammi " + YN(g.Diagrams) + " · elemento " + Q(g.Member)
                + " · caso " + Q(g.CaseName) + " · armature " + YN(g.ShowRebar) + " · carichi " + YN(g.ShowLoads) + " · larghezza " + (g.WideLimit ? "1050" : "libera"));
            var gd = s.GlobalDrawing;
            t.Add("disegno-globale", (gd.Visible ? "visibile" : "nascosto") + " · caso " + Q(gd.CaseName) + " · risultato " + YN(gd.HasResult) + " · critica " + YN(gd.FocusCritical)
                + " · concio " + N(gd.SelectedSlice));
            var p = s.GlobalPreview;
            t.Add("anteprima-globale", "caso " + Q(p.CaseName) + " · risultato " + YN(p.HasResult) + " · strato " + N(p.SelectedSoil) + " " + p.SelectedColumn);
            if (parameters is not { } window) t.Add("parametri", "-");
            else
            {
                t.Add("parametri", "attiva " + YN(window.IsEnabled) + " · messaggio " + Q(window.Message) + " · combinazioni " + Q(string.Join("|", window.Combinations))
                    + " · scelta " + Q(window.SelectedCombination));
                t.Table("parametri", window.Columns.Select(c => c.Label).ToArray(), window.Rows.Select(r => r.Values).ToArray(), -1);
                t.Panel("parametri.derivati", Blocks(window.Derived, true));
            }
            return t;
        }

        private static RetainingWallFieldState FieldState(string formKey, FormViewModel form, FieldViewModel f)
        {
            bool active = f.IsEnabled && (f.Kind == FieldKind.Text ? !f.IsInputReadOnly : f.CanChange);
            string symbol = form.SymbolColumns ? f.Symbol : "";
            string? note = f.IsNoteVisible ? f.Note : null;
            return f.Kind switch
            {
                FieldKind.Text => new(formKey, f.Key, "campo", f.Label, symbol, f.Unit, f.Text, null, active, f.IsDimmed, f.IsVisible, note),
                FieldKind.Choice => new(formKey, f.Key, "scelta", f.Label, symbol, f.Unit, f.SelectedChoice ?? "", f.Choices, active, f.IsDimmed, f.IsVisible, note),
                _ => new(formKey, f.Key, "opzione", f.Label, symbol, f.Unit, RetainingWallTrace.YesNo(f.IsChecked), null, active, f.IsDimmed, f.IsVisible, note)
            };
        }

        /// <summary>The blocks of a panel as the trace writes them; the buttons that change the data are disabled when read-only.</summary>
        private static IReadOnlyList<RetainingWallBlock>? Blocks(IReadOnlyList<WallPanelBlock>? blocks, bool canEdit) => blocks?.Select(b => b.Kind switch
        {
            WallBlockKind.Table => new RetainingWallBlock(RetainingWallBlock.TableKind, "", b.Columns, b.Rows),
            WallBlockKind.Group => new RetainingWallBlock(RetainingWallBlock.GroupKind, b.Text, Children: Blocks(b.Children, canEdit) ?? []),
            WallBlockKind.Button => new RetainingWallBlock(RetainingWallBlock.ButtonKind, b.Text, Enabled: canEdit || b.Command == RetainingWallSheetViewModel.EditGlobalSetupCommand),
            _ => new RetainingWallBlock(RetainingWallBlock.TextKind, b.Text)
        }).ToArray();
    }

    // ---------------------------------------------------------------- F3 reference

    /// <summary>
    /// The path of ui/f3-equivalence through the ViewModel (opened, visited, calculated, result, report, exported, reopened), compared with
    /// the F3 reference of the historic view in the canonical form of the capture: byte for byte, except the files with F4 transitions
    /// (the four old fixtures, result and report of globale-non-drenata-sisma since PARATIE-20, result and report of six more fixtures since
    /// AG-MUR-01), kept for <see cref="CompareTransitions"/>.
    /// </summary>
    private static async Task F3Async(string root, string output, string id, IReadOnlySet<string> withTransitions, Expect expect)
    {
        string folder = Path.Combine(root, "tests", "fixtures", "f3", "expected", "sheets", RetainingWallScenarios.Module, id);
        var normalizer = new TextNormalizer([(root, "<RADICE>")]);
        var guids = new GuidMap();
        var data = RetainingWallScenarios.FixtureData(root, id);
        CanonicalJson.Register(data, guids);
        string Canonical(JsonNode? node) { CanonicalJson.Register(node, guids); return CanonicalJson.Serialize(node, guids, normalizer).Replace("\r\n", "\n"); }
        string Expected(string name) => File.ReadAllText(Path.Combine(folder, name), Encoding.UTF8).Replace("\r\n", "\n");
        void Same(string name, string actual)
        {
            string file = "sheets/" + RetainingWallScenarios.Module + "/" + id + "/" + name;
            if (withTransitions.Contains(file))
            {
                foreach (var (side, text) in new[] { ("f3-riferimento", Expected(name)), ("f3-transizioni", actual) })
                {
                    string path = Path.Combine(output, side, file);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(path, text, new UTF8Encoding(false));
                }
                return;
            }
            // The text of a difference is kept beside the traces of the run, to read it against the reference.
            if (actual != Expected(name)) File.WriteAllText(Path.Combine(output, "f3-" + id + "-" + name), actual, new UTF8Encoding(false));
            expect.That(actual == Expected(name), id + ": " + name + " diverso dal riferimento F3 della vista storica");
        }
        using var sheet = Open(data);
        sheet.Commit(); Same("opened.json", Canonical(sheet.Data));
        // The visit of the capture opens every expander and every page of the view: nothing of it is saved in the data.
        foreach (var card in sheet.Cards.Values) card.IsExpanded = true;
        sheet.Page = 1; sheet.Page = 0;
        sheet.Commit(); Same("visited.json", Canonical(sheet.Data));
        await Expect.Within(sheet.CalculateAsync()); sheet.Commit();
        Same("calculated-data.json", Canonical(sheet.Data)); Same("result.json", Canonical(sheet.Calculation?.Json()));
        var status = new JsonObject { ["hasResults"] = sheet.Calculation is not null };
        try
        {
            byte[] report = sheet.Report.BuildReport("Fixture F3 · " + RetainingWallScenarios.Module + " · " + id, new HashSet<string>());
            Same("report.txt", DocxText.Extract(report, normalizer, guids).Replace("\r\n", "\n"));
            Same("report-structure.json", Canonical(Structure(report)));
            status["report"] = "available";
        }
        catch (Exception error) when (error is not IOException && error is not UnauthorizedAccessException)
        { status["report"] = "unavailable"; status["reason"] = error.GetType().Name + ": " + error.Message; }
        Same("exported-data.json", Canonical(sheet.Data)); Same("status.json", Canonical(status));
        using var reopened = Open(sheet.Data);
        reopened.Commit(); Same("reopened.json", Canonical(reopened.Data));
    }

    /// <summary>The files with F4 transitions compared as ui/f3-equivalence compares them, with exactly those transitions admitted.</summary>
    private static void CompareTransitions(string root, string output, IReadOnlyList<Transition> transitions, Expect expect)
    {
        var comparer = new BaselineComparer(Tolerances.Load(Path.Combine(root, "tests", "ANTHEA.Testing", "tolerances.json")));
        comparer.Admit(transitions);
        var report = comparer.Compare(Path.Combine(output, "f3-riferimento"), Path.Combine(output, "f3-transizioni"));
        File.WriteAllText(Path.Combine(output, "f3-confronto.json"), report.ToJsonString(J.Options));
        // PARATIE-20 (22/10/2026): the Bishop circles with φ = 0 are no longer rejected, so the global stability of the fixture
        // globale-non-drenata-sisma changes; the other transitions are those of the four old fixtures.
        // AG-MUR-01 (24/10/2026): the analytic parabola of the concrete (Model 5.21.0.0, Concrete 0.20.0.0) changes the N–M GPC resistances
        // of the structural checks of ten fixtures: 15271 new transitions, 1668 earlier ones with the new value after (entry extended with
        // « / AG-MUR-01»), 3 earlier ones removed (the new ratio prints as a rounding of the reference).
        // BUG-MUR-2b (libraries of the 1.14.2, Checker 4a1b0090): the passive in front is no longer an overturning resistance: 112 new
        // transitions, 16 earlier ones with the new value after (entry extended with « / BUG-MUR-2b»).
        // COMB-MURI-D4D5 (libraries of the 1.14.4, set AK, decision of 28/10/2026): favourable G2 0.8 and the γR of Tab. 7.11.III in the
        // seismic rows: 4703 new transitions, 744 AG-MUR-01 ones with the new value after (entry «AG-MUR-01 / COMB-MURI-D4D5»).
        int undrained = transitions.Count(t => t.Entry == "PARATIE-20");
        int parabola = transitions.Count(t => t.Entry == "AG-MUR-01"), extended = transitions.Count(t => t.Entry.EndsWith(" / AG-MUR-01", StringComparison.Ordinal));
        int overturning = transitions.Count(t => t.Entry == "BUG-MUR-2b"), overturningExtended = transitions.Count(t => t.Entry.EndsWith(" / BUG-MUR-2b", StringComparison.Ordinal));
        int combinations = transitions.Count(t => t.Entry == "COMB-MURI-D4D5"), combinationsExtended = transitions.Count(t => t.Entry.EndsWith(" / COMB-MURI-D4D5", StringComparison.Ordinal));
        expect.That(undrained == 2898 && parabola == 14527 && extended == 1668 && overturning == 112 && overturningExtended == 16
            && combinations == 4703 && combinationsExtended == 744
            && transitions.Count - undrained - parabola - overturning - combinations == 15182 && comparer.NotAdmitted == 0,
            "File con transizioni F4: " + transitions.Count + " transizioni, "
            + comparer.NotAdmitted + " differenze non ammesse o transizioni assenti");
    }

    /// <summary>Tables, rows, cells, images and styles of the report, as F3EquivalenceCapture.CaptureReport counts them.</summary>
    private static JsonObject Structure(byte[] docx)
    {
        using var zip = new ZipArchive(new MemoryStream(docx));
        using var stream = zip.GetEntry("word/document.xml")!.Open();
        var document = XDocument.Load(stream);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        XNamespace wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
        return new JsonObject
        {
            ["tables"] = document.Descendants(w + "tbl").Count(),
            ["rows"] = document.Descendants(w + "tr").Count(),
            ["cells"] = document.Descendants(w + "tc").Count(),
            ["images"] = new JsonArray(document.Descendants(wp + "extent").Select(e => (JsonNode)new JsonObject {
                ["cx"] = (string?)e.Attribute("cx"), ["cy"] = (string?)e.Attribute("cy") }).ToArray()),
            ["styles"] = new JsonArray(document.Descendants(w + "pStyle").Select(e => JsonValue.Create((string?)e.Attribute(w + "val"))).ToArray())
        };
    }

    // ---------------------------------------------------------------- contract

    private static void Contract(Expect expect)
    {
        expect.That(Module.Info == ModuleDirectory.Get(RetainingWallScenarios.Module) && Module.Reports == new ModuleReportCapabilities(false)
            && Module.Help.GuideId == ModuleHelp.For(RetainingWallScenarios.Module).GuideId, "Modulo dei muri");
        expect.That(Module.Persistence.UiStateKeys.Count == 0 && RetainingWallSheetViewModel.UiStateKeys.Count == 0, "Nessuna chiave di stato dell'interfaccia");
        var registry = new ModuleRegistry([Module]);
        expect.That(registry.SheetModuleIds.SequenceEqual([RetainingWallScenarios.Module]) && ReferenceEquals(registry.SheetModule(RetainingWallScenarios.Module), Module),
            "Registro con i muri");
        expect.Throws<ArgumentNullException>(() => new RetainingWallSheetModule(null!), "Modulo senza disegni del report");
    }

    /// <summary>Views, filters, members, switches, cards, pages and selections write nothing into the data and keep the result.</summary>
    private static async Task StateKeysAsync(string root, Expect expect)
    {
        using var sheet = Open(RetainingWallScenarios.FixtureData(root, "mensola-sisma-sito-pendio"));
        await Expect.Within(sheet.RecalculateAsync());
        string before = sheet.Data.ToJsonString();
        var result = sheet.Calculation;
        int modified = 0;
        sheet.Modified += (_, _) => modified++;
        foreach (var card in sheet.Cards.Values) card.IsExpanded = !card.IsExpanded;
        sheet.Page = 1;
        foreach (string view in RetainingWallFields.ViewModes) sheet.SelectedView = view;
        sheet.SelectedView = "Sollecitazioni";
        foreach (string member in RetainingWallFields.Members) sheet.SelectedMember = member;
        foreach (string filter in RetainingWallFields.CheckFilters) { sheet.SelectedFilter = filter; sheet.SelectResultRow(0); }
        foreach (string combination in sheet.Combinations!) sheet.SelectedCombination = combination;
        sheet.ShowLoads = false; sheet.ShowLabels = false; sheet.ShowRebar = false; sheet.ShowPressures = false; sheet.GlobalFullProfile = true;
        sheet.ValleyLayers.SelectedIndex = 0; sheet.MountainLayers.SelectedIndex = 0; sheet.Actions.SelectedIndex = 0; sheet.Matrix!.SelectedIndex = 1;
        sheet.SelectPressure(1); sheet.SelectSlice(2);
        expect.That(sheet.Data.ToJsonString() == before && modified == 0 && ReferenceEquals(result, sheet.Calculation), "Viste e selezioni scritte nei dati o risultato perso");
        sheet.Actions.Rows[0]["visible"] = false;
        expect.That(modified == 1 && ReferenceEquals(result, sheet.Calculation) && sheet.Data.Array("actions")[0]!["visible"]!.GetValue<bool>() == false,
            "«Disegna» di un'azione: modifica del documento senza ricalcolo");
    }

    // ---------------------------------------------------------------- background calculation

    /// <summary>
    /// The calculation on a background thread through the service of the context: busy while the engine runs, also for a request
    /// overtaken by an edit; only the current revision is published; closing during a calculation publishes nothing.
    /// </summary>
    private static async Task BackgroundAsync(string root, Expect expect)
    {
        var engine = new HeldModuleService(RetainingWallScenarios.Module);
        var counting = new ThreadService(engine);
        var sheet = Open(RetainingWallScenarios.FixtureData(root, "default"), counting);
        int ui = Environment.CurrentManagedThreadId;
        expect.That(sheet.CalculationMode == SheetCalculationMode.Background && sheet.AutomaticDelay == RetainingWallSheetViewModel.RecalculationDelay
            && RetainingWallSheetViewModel.RecalculationDelay == TimeSpan.FromMilliseconds(450), "Calcolo sullo sfondo con il ritardo di 450 ms");
        await Expect.Within(sheet.PendingWork);
        expect.That(sheet.Calculation is not null && counting.Threads.All(t => t != ui) && !sheet.IsBusy, "Calcolo all'apertura fuori dal thread dell'interfaccia");
        engine.Hold();
        var first = sheet.RecalculateAsync();
        await engine.EnteredAsync();
        expect.That(sheet.IsBusy && sheet.Status == RetainingWallPresentation.StatusRunning && sheet.CalculationState == CalculationState.Running, "Occupato durante il calcolo");
        var notified = new List<string?>();
        sheet.PropertyChanged += (_, e) => notified.Add(e.PropertyName);
        sheet.Forms["geometry"].Set("height", "3,4");
        expect.That(notified.Contains(nameof(sheet.CanDesign)) && notified.Contains(nameof(sheet.GlobalResult)) && notified.Contains(nameof(sheet.Calculation)),
            "Notifiche di «Calcola armature», del risultato globale o del calcolo");
        expect.That(sheet.IsBusy && sheet.Calculation is null && sheet.Status == RetainingWallPresentation.StatusPending && !sheet.CanDesign,
            "Calcolo superato: occupato finché il motore non torna");
        engine.Release();
        expect.That(!await Expect.Within(first), "Calcolo superato pubblicato");
        await Expect.Within(sheet.PendingWork);
        expect.That(sheet.Calculation is not null && sheet.Calculation.Input["geometry"]!["height"]!.ToString() == "3,4" && !sheet.IsBusy, "Revisione corrente pubblicata");
        // F5.15, MU14: «Ricalcola» is disabled while its work runs, as the convention of GuardedCommands.
        engine.Hold();
        var button = (IAsyncRelayCommand)sheet.RecalculateCommand;
        button.Execute(null);
        await engine.EnteredAsync();
        expect.That(!button.CanExecute(null) && button.IsRunning, "«Ricalcola» spento durante il suo lavoro (MU14)");
        engine.Release();
        await Expect.Within(button.ExecutionTask!);
        expect.That(button.CanExecute(null) && !sheet.IsBusy, "«Ricalcola» di nuovo attivo alla fine del lavoro (MU14)");
        engine.Hold();
        var last = sheet.RecalculateAsync();
        await engine.EnteredAsync();
        sheet.Dispose();
        engine.Release();
        expect.That(!await Expect.Within(last) && sheet.IsDisposed, "Calcolo pubblicato dopo la chiusura");
        expect.Throws<ObjectDisposedException>(() => sheet.RecalculateAsync(), "Ricalcola dopo la chiusura");
    }

    // ---------------------------------------------------------------- read-only

    /// <summary>A sheet opened read-only (a previous revision): every edit is refused, also in the window of the values; views, global
    /// calculation, report and calculation are allowed.</summary>
    private static async Task ReadOnlyAsync(string root, Expect expect)
    {
        var host = new Host();
        using var sheet = Open(RetainingWallScenarios.FixtureData(root, "globale-drenata-due-colonne"), host: host, readOnly: true);
        int modified = 0;
        sheet.Modified += (_, _) => modified++;
        await Expect.Within(sheet.PendingWork);
        string before = sheet.Data.ToJsonString();
        expect.That(!sheet.CanEdit && sheet.Forms.Values.All(f => f.IsReadOnly) && !sheet.CanDesign, "Sola lettura dal contesto");
        sheet.Forms["geometry"]["height"].EditText = "4";
        sheet.Forms["valley"]["height_mode"].SelectedChoice = "Assegnato";
        sheet.Forms["water"]["enabled"].IsChecked = true;
        sheet.Family = "gravity"; sheet.LoadExample("gravity");
        sheet.AddSoilLayer(false); sheet.RemoveSoilLayer(false); sheet.MoveSoilLayer(false, 1); sheet.AddAction(); sheet.RemoveAction(); sheet.AddServiceLayer();
        sheet.GenerateMatrix(); sheet.ConfirmMatrix(); sheet.CopyCombination(); sheet.RemoveCombination(); sheet.PrepareGlobal(); sheet.GenerateGlobalMatrix();
        sheet.AddGlobalLayer(false); sheet.AddProfilePoint("punti-valle"); sheet.AdoptMinimumCover(); sheet.AddHistory(); await sheet.TransferSoilAsync();
        await sheet.DesignRebarAsync();
        sheet.Page = 1; sheet.SelectedFilter = "Sollecitazioni numeriche"; sheet.SelectResultRow(3); sheet.SendSelectedSection();
        expect.That(sheet.Data.ToJsonString() == before && modified == 0 && host.Sections.Count == 0 && !sheet.CanSendSection, "Sola lettura: modifica scritta nel documento");
        var window = await sheet.OpenParametersAsync();
        expect.That(window is { IsReadOnly: true } && sheet.Data.ToJsonString() == before, "Sola lettura: finestra dei valori modificabile");
        window!.Rows[0]["value"] = "9";
        await window.ApplyAsync();
        expect.That(sheet.Data.ToJsonString() == before && modified == 0 && !window.IsClosed && window.Message == RetainingWallPresentation.ParametersHint,
            "Sola lettura: valori applicati dalla finestra (difetto della vista storica)");
        window.Close();
        sheet.SelectedView = "Stabilità globale";
        await Expect.Within(sheet.CalculateGlobalAsync());
        sheet.RequestReport();
        expect.That(sheet.HasIndependentGlobal && host.Reports == 1 && sheet.Data.ToJsonString() == before && modified == 0, "Sola lettura: globale o report");
        sheet.IsReadOnly = false;
        sheet.Forms["geometry"].Set("height", "3,2");
        expect.That(sheet.Data["geometry"]!["height"]!.ToString() == "3,2" && sheet.Calculation is null && modified == 1 && sheet.Status == RetainingWallPresentation.StatusPending,
            "Ritorno alla modifica");
    }

    // ---------------------------------------------------------------- reload

    /// <summary>Data replaced from outside: built again as at opening, without Modified and without a result; then as a reopening.</summary>
    private static async Task ReloadAsync(string root, Expect expect)
    {
        using var sheet = Open(RetainingWallScenarios.FixtureData(root, "default"));
        int modified = 0;
        sheet.Modified += (_, _) => modified++;
        await Expect.Within(sheet.PendingWork);
        sheet.Forms["geometry"].Set("height", "3,6");
        var other = RetainingWallScenarios.FixtureData(root, "gravita-cls-passiva");
        string source = other.ToJsonString();
        sheet.Reload(other);
        expect.That(modified == 1 && sheet.Calculation is null && sheet.Family == "gravity" && sheet.Status == RetainingWallPresentation.StatusToVerify
            && other.ToJsonString() == source, "Reload: tipologia, stato o origine modificata");
        await Expect.Within(sheet.PendingWork);
        using var reopened = Open(other);
        await Expect.Within(reopened.PendingWork);
        expect.That(sheet.Data.ToJsonString() == reopened.Data.ToJsonString() && sheet.Calculation is not null && reopened.Calculation is not null
            && sheet.Calculation.Json().ToJsonString() == reopened.Calculation.Json().ToJsonString() && modified == 1, "Reload: dati o risultato diversi da una riapertura");
    }

    // ---------------------------------------------------------------- exports

    /// <summary>
    /// «CSV» of the checks and of the slices of the global stability, the bar schedule, the import of an accelerogram and the refusals
    /// of the exports without a result, through the services of the context.
    /// </summary>
    private static async Task ExportsAsync(string root, string output, Expect expect)
    {
        string folder = Path.Combine(output, "muri-esportazioni");
        Directory.CreateDirectory(folder);
        string checks = Path.Combine(folder, "verifiche.csv"), slices = Path.Combine(folder, "conci.csv"), history = Path.Combine(folder, "storia.csv");
        File.WriteAllLines(history, ["t; a/g", "0;0", "0,01;0,05", "0,02;-0,02"], new UTF8Encoding(false));
        var messages = new ScriptedMessages();
        var files = new ScriptedFileDialogs().Then(checks).Then(slices).Then(history);
        using var sheet = Open(RetainingWallScenarios.FixtureData(root, "globale-drenata-due-colonne"), services: Scripted.Services(messages, files: files));
        sheet.ExportCsv();
        sheet.ExportGlobalWord();
        expect.Throws<ArgumentException>(() => sheet.CreateBarSchedule(), "Distinta senza risultato");
        expect.That(messages.Shown.Select(m => m.Message).SequenceEqual([RetainingWallPresentation.WaitBeforeExport, RetainingWallPresentation.WaitBeforeGlobalWord])
            && files.Requests.Count == 0, "Esportazioni senza risultato non rifiutate");
        await Expect.Within(sheet.PendingWork);
        var result = sheet.Calculation!;
        sheet.ExportCsv();
        byte[] bom = Encoding.UTF8.GetPreamble();
        expect.That(File.ReadAllBytes(checks).SequenceEqual(bom.Concat(Encoding.UTF8.GetBytes(WallExports.Csv(result)))), "CSV delle verifiche");
        sheet.SelectedView = "Stabilità globale";
        sheet.ExportCsv();
        expect.That(File.ReadAllBytes(slices).SequenceEqual(bom.Concat(Encoding.UTF8.GetBytes(WallExports.GlobalCsv(result.GlobalStability!))))
            && ((SaveFileRequest)files.Requests[1]).FileName == "Muro_stabilita_globale_conci.csv", "CSV dei conci della stabilità globale");
        var schedule = sheet.CreateBarSchedule();
        expect.That(WallExports.BarScheduleCsv(schedule) == WallExports.BarScheduleCsv(WallModel.CalculateBarSchedule(result)), "Distinta ferri del risultato");
        sheet.AddHistory();
        int modified = 0;
        sheet.Modified += (_, _) => modified++;
        sheet.ImportHistory(0);
        var samples = sheet.Data["serviceability"]!.Array("histories")[0]!.Array("samples");
        expect.That(samples.Count == 3 && samples[1].D("t") == 0.01 && samples[2].D("a_g") == -0.02 && modified == 1 && sheet.Histories[0].Samples.StartsWith("3 campioni", StringComparison.Ordinal),
            "Importazione dell'accelerogramma");
    }

    // ---------------------------------------------------------------- the contract of the view (F5.9, decisions of 17/10)

    /// <summary>
    /// What the XAML view needs from the sheet, decided on 17/10: every form with the navigator of the Wiki of the services and read-only
    /// through <see cref="FormViewModel.IsReadOnly"/> (T2, also for the forms built after the opening), the window of the values as a modal
    /// dialog of the services (MU10), the shared soil transfer dialog (MU9, T5), the commands of the buttons, the visibility of the forms
    /// notified, the cards shown, the bar schedule of the view and the type of a new action back to the first one at every rebuild.
    /// </summary>
    private static async Task ViewContractAsync(string root, string output, Expect expect)
    {
        var dialogs = new ModelessDialogs();
        var wiki = new RecordingWiki();
        var messages = new ScriptedMessages();
        string folder = Path.Combine(output, "muri-vista");
        Directory.CreateDirectory(folder);
        string schedule = Path.Combine(folder, "distinta.csv");
        var files = new ScriptedFileDialogs().Then(schedule).Then(null).Then(Path.Combine(folder, "manca", "distinta.pdf"));
        var host = new RoutingHost();
        var services = new PresentationServices(messages, new ScriptedConfirmations().Then(true), files, dialogs, new RecordingClipboard(), new RecordingLinks(), wiki);
        using var sheet = Open(RetainingWallScenarios.FixtureData(root, "default"), services: services, routing: host);
        bool Forms() => sheet.Forms.Values.All(f => ReferenceEquals(f.WikiNavigator, wiki) && f.IsReadOnly == !sheet.CanEdit);
        expect.That(Forms() && sheet.Forms.Count > 20, "Tutti i form con il navigatore della Wiki dei servizi e la sola lettura del foglio: " + sheet.Forms.Count);
        await Expect.Within(sheet.PendingWork);
        // Forms built after the opening: the action selected, a deep layer, an accelerogram, a prepared profile.
        sheet.Actions.SelectedIndex = 0; sheet.AddHistory(); sheet.PrepareGlobal(); sheet.GlobalRearLayers.SelectedIndex = 0;
        expect.That(Forms() && sheet.Forms.ContainsKey("action") && sheet.Forms.ContainsKey("history_0") && sheet.Forms.ContainsKey("global_soil_rear"),
            "Form costruiti dopo l'apertura con il navigatore della Wiki");
        sheet.IsReadOnly = true;
        expect.That(Forms(), "Form in sola lettura con IsReadOnly");
        string[] allowed = ["Recalculate", "Report", "ExportCsv", "ShowSeismic", "ShowGlobalSetup", "Parameters", "CalculateGlobal", "ExportGlobalWord"];
        var commands = typeof(RetainingWallSheetViewModel).GetProperties().Where(p => p.Name.EndsWith("Command", StringComparison.Ordinal)
            && typeof(System.Windows.Input.ICommand).IsAssignableFrom(p.PropertyType)).ToArray();
        static object? Parameter(string name) => name switch
        {
            "LoadExample" => "gravity",
            "AddSoilLayer" or "RemoveSoilLayer" or "MoveSoilLayerUp" or "MoveSoilLayerDown" or "AddGlobalLayer" or "RemoveGlobalLayer" => "monte",
            "AddProfilePoint" or "RemoveProfilePoint" => "punti-valle",
            _ => null
        };
        string? Wrong(bool readOnly) => commands.Select(p => (Name: p.Name[..^"Command".Length], Command: (System.Windows.Input.ICommand)p.GetValue(sheet)!))
            .Where(c => c.Name is not ("DesignRebar" or "SendSection" or "Panel" or "ImportHistory" or "RemoveHistory"))
            .Where(c => c.Command.CanExecute(Parameter(c.Name)) != (!readOnly || allowed.Contains(c.Name))).Select(c => c.Name).FirstOrDefault();
        expect.That(commands.Length == 37 && Wrong(true) is null && !sheet.DesignRebarCommand.CanExecute(null) && !sheet.ImportHistoryCommand.CanExecute(sheet.Histories[0])
            && sheet.PanelCommand.CanExecute(RetainingWallSheetViewModel.EditGlobalSetupCommand) && !sheet.PanelCommand.CanExecute(RetainingWallSheetViewModel.ApplyProposalCommand),
            "Comandi in sola lettura: spenti quelli che modificano i dati, attivi quelli ammessi dalla vista storica (" + commands.Length + "): " + Wrong(true));
        string before = sheet.Data.ToJsonString();
        await sheet.TransferSoilAsync();
        expect.That(dialogs.Shown.Count == 0 && sheet.Data.ToJsonString() == before, "Sola lettura: «Invia / carica terreno…» rifiutato");
        sheet.IsReadOnly = false;
        expect.That(Wrong(false) is null && Forms() && sheet.RemoveHistoryCommand.CanExecute(sheet.Histories[0]), "Comandi e form di nuovo modificabili: " + Wrong(false));
        // The soil transfer dialog (MU9): a copy to the window, then the replacement of the soil of the sheet.
        dialogs.Answer = dialog =>
        {
            var transfer = (Modules.Shared.SoilTransferDialogViewModel)dialog;
            transfer.SelectedDestination = "geo_stratigrafia";
            transfer.SendCommand.Execute(null);
        };
        await sheet.TransferSoilCommand.ExecuteAsync(null);
        expect.That(dialogs.Shown.LastOrDefault() is Modules.Shared.SoilTransferDialogViewModel && host.Copies.Count == 1 && host.Copies[0].Module == "geo_stratigrafia"
            && host.Copies[0].Name.StartsWith("Terreno da ", StringComparison.Ordinal), "«Invia / carica terreno…»: dialogo condiviso, copia inviata alla finestra");
        dialogs.Answer = dialog => ((Modules.Shared.SoilTransferDialogViewModel)dialog).ApplyCommand.Execute(null);
        await sheet.TransferSoilCommand.ExecuteAsync(null);
        expect.That(host.Replacements.Count == 1 && host.Replacements[0]["family"] is not null, "«Invia / carica terreno…»: sostituzione del terreno del foglio");
        // The window of the values as a modal dialog (on a sheet whose data the engine accepts, as the trace «valori-di-calcolo»): values
        // applied, the dialog closed with true and the sheet calculated.
        using var values = Open(RetainingWallScenarios.FixtureData(root, "gravita-cls-passiva"), services: services, routing: host);
        await Expect.Within(values.PendingWork);
        dialogs.Answer = dialog =>
        {
            var window = (RetainingWallParametersViewModel)dialog;
            window.Rows.Single(r => r.Values.S("path") == "geometry/height")["value"] = "3,4";
            window.ApplyCommand.Execute(null);
        };
        await Expect.Within(values.ShowParametersAsync());
        var shown = (RetainingWallParametersViewModel)dialogs.Shown[^1];
        await Expect.Within(shown.Applying);
        expect.That(shown.IsClosed && shown.Result && dialogs.Results[^1] is true && values.Data["geometry"].D("height") == 3.4 && values.ParameterWindows.Count == 0
            && shown.Title == RetainingWallPresentation.ParametersTitle, "«Valori di calcolo…»: dialogo modale dei servizi, valori applicati e finestra chiusa");
        dialogs.Answer = dialog => ((RetainingWallParametersViewModel)dialog).CloseCommand.Execute(null);
        string current = values.Data.ToJsonString();
        await Expect.Within(values.ShowParametersAsync());
        expect.That(dialogs.Results[^1] is false && values.Data.ToJsonString() == current && values.ParameterWindows.Count == 0, "«Valori di calcolo…»: «Chiudi» senza applicare");
        // The window closed by its title bar during the check: the dialog returns and the check is cancelled.
        dialogs.Answer = dialog =>
        {
            var window = (RetainingWallParametersViewModel)dialog;
            window.ApplyCommand.Execute(null);
            dialogs.CloseFromTitleBar();
        };
        await Expect.Within(values.ShowParametersAsync());
        var cancelled = (RetainingWallParametersViewModel)dialogs.Shown[^1];
        await Expect.Within(cancelled.Applying);
        expect.That(cancelled.IsClosed && !cancelled.Result && values.Data.ToJsonString() == current, "«Valori di calcolo…»: finestra chiusa durante il controllo, niente applicato");
        values.IsReadOnly = true;
        bool refused = false;
        dialogs.Answer = dialog =>
        {
            var window = (RetainingWallParametersViewModel)dialog;
            refused = window.IsReadOnly && !window.ApplyCommand.CanExecute(null);
            window.CloseCommand.Execute(null);
        };
        await Expect.Within(values.ShowParametersAsync());
        expect.That(refused, "Sola lettura: «Applica e ricalcola» spento (MU3)");
        values.IsReadOnly = false;
        // The visibility of the forms, notified with a new copy; the cards shown; the type of a new action.
        var notified = new List<string?>();
        sheet.PropertyChanged += (_, e) => notified.Add(e.PropertyName);
        var visibility = sheet.FormVisibility;
        sheet.Family = "gravity";
        expect.That(notified.Contains(nameof(sheet.FormVisibility)) && !ReferenceEquals(visibility, sheet.FormVisibility) && !sheet.FormVisibility["material_cover"]
            && sheet.FormVisibility["gravity_design"] && !sheet.FormVisibility["detailing"] && visibility["detailing"], "Visibilità dei form notificata con una copia nuova");
        var cards = new List<string>();
        sheet.CardShown += (_, card) => cards.Add(card);
        sheet.NewActionType = WallModel.ActionTypes[^1];
        sheet.ShowSeismicCommand.Execute(null); sheet.ShowGlobalSetupCommand.Execute(null); sheet.PrepareGlobalCommand.Execute(null);
        expect.That(cards.SequenceEqual(["Sisma", "Stabilità globale", "Stabilità globale"]) && sheet.Page == 0, "Schede mostrate per lo scorrimento della vista: " + string.Join(", ", cards));
        expect.That(sheet.NewActionType == WallModel.ActionTypes[0] && sheet.ActionTypes.SequenceEqual(WallModel.ActionTypes), "Tipo della nuova azione di nuovo il primo dopo la ricostruzione degli input");
        sheet.NewActionType = "nessuno";
        expect.That(sheet.NewActionType == WallModel.ActionTypes[0], "Tipo di azione sconosciuto rifiutato");
        // The bar schedule of the view: refused without a result, then exported, cancelled and failed.
        using var bars = Open(RetainingWallScenarios.FixtureData(root, "default"), services: services, routing: host);
        expect.That(bars.OpenBarSchedule() is null && messages.Shown[^1] == (RetainingWallSheetViewModel.BarScheduleTitle, "Attendere il calcolo aggiornato prima di aprire la distinta.", false),
            "Distinta ferri senza risultato: messaggio della vista storica");
        await Expect.Within(bars.PendingWork);
        expect.That(bars.OpenBarSchedule() is not null, "Distinta ferri del risultato");
        bars.SaveBarSchedule("csv", () => [1, 2, 3]);
        bars.SaveBarSchedule("png", () => throw new InvalidOperationException("mai"));
        bars.SaveBarSchedule("pdf", () => [4]);
        var requests = files.Requests.Cast<SaveFileRequest>().ToArray();
        expect.That(File.ReadAllBytes(schedule).SequenceEqual(new byte[] { 1, 2, 3 }) && requests.Length == 3 && requests[0].FileName == "Muro_distinta_ferri.csv"
            && requests[0].Filters[0].Name == "CSV" && requests[0].Filters[0].Patterns.SequenceEqual(["*.csv"])
            && messages.Shown[^1].Message.StartsWith("Esportazione non riuscita: ", StringComparison.Ordinal),
            "Esportazioni della distinta: file scelto, annullamento, errore mostrato");
    }

    /// <summary>
    /// A dialog service whose dialog stays open (as the modal window) until it closes: <see cref="Answer"/> acts on it as the user,
    /// <see cref="CloseFromTitleBar"/> closes the window without a result, as WpfDialogService when the user closes it.
    /// </summary>
    private sealed class ModelessDialogs : IDialogService
    {
        private Action? close;
        internal List<object> Shown { get; } = [];
        internal List<object?> Results { get; } = [];
        internal Action<object>? Answer { get; set; }

        internal void CloseFromTitleBar() => close?.Invoke();

        public Task<TResult?> ShowDialogAsync<TResult>(IDialogViewModel<TResult> dialog)
        {
            Shown.Add(dialog);
            var done = new TaskCompletionSource<TResult?>(TaskCreationOptions.RunContinuationsAsynchronously);
            void Closed(object? sender, EventArgs e) { dialog.CloseRequested -= Closed; Results.Add(dialog.Result); done.TrySetResult(dialog.Result); }
            dialog.CloseRequested += Closed;
            close = () => { dialog.CloseRequested -= Closed; Results.Add(default(TResult)); done.TrySetResult(default); };
            Answer?.Invoke(dialog);
            return done.Task;
        }
    }

    /// <summary>The window side for the soil transfer: copies and replacements recorded.</summary>
    private sealed class RoutingHost : ISheetHost
    {
        internal List<(string Module, JsonObject Data, string Name)> Copies { get; } = [];
        internal List<JsonObject> Replacements { get; } = [];
        public void OpenModuleCopy(string module, JsonObject data, string name) => Copies.Add((module, data, name));
        public void ReplaceSoil(JsonObject data) => Replacements.Add(data);
        public IReadOnlyList<SoilSource> SoilSources() => [];
        public void RequestReport() { }
    }

    // ---------------------------------------------------------------- services of the checks

    /// <summary>Figures of the report as RetainingWallPresentation.Figures plans them: only the aspect ratio enters the report (extent of the images).</summary>
    private sealed class PlanFigures : IRetainingWallFigureRenderer
    {
        public IReadOnlyList<ReportFigure> Render(WallModel.Result calculation, WallReportPresentation context) =>
            RetainingWallPresentation.Figures(calculation, context).Select(f => new ReportFigure(f.Caption, Png((int)f.Width, (int)f.Height), f.Category, AspectRatio: f.AspectRatio)).ToArray();

        public ReportFigure RenderGlobal(JsonObject input, SlopeResult result, SlopeCaseResult? shown)
        {
            var f = RetainingWallPresentation.GlobalExportFigure;
            return new(f.Caption, Png((int)f.Width, (int)f.Height), f.Category, AspectRatio: f.AspectRatio);
        }

        /// <summary>The signature and the header of a PNG of the given size.</summary>
        private static byte[] Png(int width, int height)
        {
            var png = new byte[33];
            new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52 }.CopyTo(png, 0);
            BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(16, 4), width);
            BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(20, 4), height);
            png[24] = 8; png[25] = 6;
            return png;
        }
    }

    /// <summary>The module service of the walls with the threads of its calls.</summary>
    private sealed class ThreadService(IModuleService inner) : IModuleService
    {
        internal System.Collections.Concurrent.ConcurrentQueue<int> Threads { get; } = new();
        public string ModuleId => inner.ModuleId;
        public JsonObject Prepare(JsonObject source) => inner.Prepare(source);
        public ModuleCalculation Calculate(JsonObject source, CancellationToken cancellation = default)
        {
            Threads.Enqueue(Environment.CurrentManagedThreadId);
            return inner.Calculate(source, cancellation);
        }
    }

    /// <summary>The dialog service of the traces: the soil transfer dialogs are counted and closed without a choice; any other dialog fails.</summary>
    private sealed class SoilDialogs : IDialogService
    {
        internal int Shown { get; private set; }
        public Task<TResult?> ShowDialogAsync<TResult>(IDialogViewModel<TResult> dialog)
        {
            if (dialog is not Modules.Shared.SoilTransferDialogViewModel) throw new InvalidOperationException("Dialogo non previsto: " + dialog.Title);
            Shown++;
            return Task.FromResult<TResult?>(default);
        }
    }

    private sealed class Host : ISheetHost
    {
        internal int Reports { get; private set; }
        internal List<(string Name, JsonObject Data)> Sections { get; } = [];
        public void OpenModuleCopy(string module, JsonObject data, string name)
        {
            if (module != "str_palo") throw new InvalidOperationException("Copia in un modulo non previsto: " + module);
            Sections.Add((name, data));
        }
        public void ReplaceSoil(JsonObject data) => throw new InvalidOperationException("Terreno non previsto.");
        public IReadOnlyList<SoilSource> SoilSources() => [];
        public void RequestReport() => Reports++;
    }
}
