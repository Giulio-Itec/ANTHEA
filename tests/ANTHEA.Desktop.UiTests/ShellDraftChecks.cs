using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Anthea.Application.Examples;
using Anthea.Application.Documents;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Commands;
using Anthea.Presentation.Shell.Navigation;
using Anthea.Presentation.Shell.Projects;
using Anthea.Presentation.Shell.Tabs;
using X.Core;
using X.Desktop.Services;
using X.Desktop.Views.Shell;

namespace X.Desktop;

/// <summary>
/// --check-shell-draft &lt;cartella&gt; (UiTests, F5.13 c): the draft of the layout of the new shell for the user's checkpoint
/// (docs/refactoring/f5-13-shell.md, «Bozza della disposizione per il punto di controllo»), off screen, never activated, without questions:
/// <list type="bullet">
/// <item>the examples of the Wiki give the same data through the shell (WikiExamples) and through MainWindow.WikiExamples;</item>
/// <item>ShellWindow on ShellViewModel, built by the composition root of the shell (ShellComposition), with the modules of the product
/// (DesktopComposition.CreateModules) and a sample project (a bridge with an
/// abutment and a pier, four sheets), PNG of the home, of «Moduli singoli», of «Progetti», of four tabs open (three sheets of the project and a
/// single file), of «Modelli», of the Wiki tab, of a detached window with the main window beside it, of the tabs in «Scuro»;</item>
/// <item>after the user's checkpoint of 22/10, from the tree of the project: a sheet at full page (11, then Esc and the toggle of the sheet bar)
/// and a sheet opened in a new window, with the main window beside it in one image (12); the icons of the modules, the links and the groups of
/// the overview, Ctrl+Z and Ctrl+Y of the project, the Wiki of the services on the tab, the rename in place and the keys of the tree;</item>
/// <item>the night of 22-23/10 (section 10.1 of the note), what was left of part (c): the overview with the comparison and the summary of the
/// revisions in their dialogs (13-15), the drag on the tree and the single click (16), «Riprendi» and the cards of «Schede da aggiungere» (17, 18),
/// the notice of a reloaded tab (19), small screens (20), the short report of the RC section (the second shell with the historic modules of the Release, 21, is
/// archived since F5.13 g). The shell keeps every module on its MVVM view, the views of the checkpoint;</item>
/// <item>no binding error of WPF in all the captures (binding-errors.txt), the structure of the window at every step.</item>
/// </list>
/// Writes the PNG, binding-errors.txt, test.txt and esito.txt («PASS n controlli»). The draft is not the window of the application: the Release
/// opens MainWindow.
/// </summary>
internal static class ShellDraftChecks
{
    internal static async Task Run(string output)
    {
        Directory.CreateDirectory(output);
        var log = new List<string>();
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); log.Add("OK " + message); }
        var bindings = new BindingErrors();
        PresentationTraceSources.Refresh();
        PresentationTraceSources.DataBindingSource.Listeners.Add(bindings);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        try
        {
            // The examples of the Wiki: the copy of the shell gives the data of MainWindow, recorded before its archiving (F5.13 g3: MainWindow.WikiExamples is
            // archived with it; tests/fixtures/f5-shell/finestra-storica.json, «esempi_wiki»).
            foreach (var example in Anthea.Wiki.WikiCatalog.Articles.Select(a => a.Example).OfType<string>().Distinct())
            {
                string module = WikiExamples.Definition(example).S("moduleId");
                Check(JsonNode.DeepEquals(WikiExamples.Create(module, example), ShellReferences.Window["esempi_wiki"]?[example]), "Esempio della Wiki «" + example + "»: stessi dati della finestra storica (valori registrati)");
            }
            // F5.13 g: the same data as the values of MainWindow recorded before its archiving (tests/fixtures/f5-shell/finestra-storica.json, «esempi_wiki»).
            var examples = new JsonObject();
            foreach (var example in Anthea.Wiki.WikiCatalog.Articles.Select(a => a.Example).OfType<string>().Distinct().Order(StringComparer.Ordinal))
                examples[example] = WikiExamples.Create(WikiExamples.Definition(example).S("moduleId"), example);
            ShellReferences.WriteJson(Path.Combine(output, "esempi-wiki.json"), examples);
            Check(JsonNode.DeepEquals(examples, ShellReferences.Window["esempi_wiki"]), "Esempi della Wiki (" + examples.Count + "): stessi dati dei valori registrati della finestra storica");

            // The shell with the modules of the product and services that never ask the user.
            // Since the checkpoint the shell comes from its composition root (ShellComposition), with these services, the Wiki of the services on the
            // tab of the shell, and detached windows wrapped so that they are never activated.
            var services = new DraftServices();
            var presentation = ShellComposition.WithShellWiki(services.Create()) with
            { ReportTemplates = new Anthea.Reports.ReportTemplateStore(Path.Combine(output, "report-template.json")) };
            var modules = DesktopComposition.CreateModules();
            var detached = new List<QuietWindow>();
            // Since the night of 22-23/10 (R5) the shell opens every module as the Release chooses it; the draft keeps every module on its MVVM view, the
            // views of the images of the checkpoint (01-20). (21, the historic host R5, archived since F5.13 g.)
            var everyMvvm = ModuleImplementations.Declared.Aggregate(ModuleImplementations.Release, (set, entry) => set.With(entry.Module, SheetImplementation.Mvvm));
            var parts = ShellComposition.Create(presentation, modules, new CalculationLimiter(CalculationLimiter.DefaultMaximum), ShellComposition.ExamplesFolder, window =>
            {
                var quiet = new QuietWindow(Offscreen(window));
                detached.Add(quiet);
                return quiet;
            }, everyMvvm);
            var shell = parts.Shell;
            var tracker = parts.Tracker;
            var main = Offscreen(parts.Window);
            main.Show();
            try
            {
                var workspace = shell.Workspace;
                await Capture(main, output, "01-home.png", bindings);
                var shellMenu = Descendants<Menu>(main).First();
                var appearance = (MenuItem)shellMenu.Items[1];
                var templateMenu = appearance.Items.OfType<MenuItem>().Single(m => Equals(m.Header, ReportTemplateMenuViewModel.Title));
                Check(templateMenu.Visibility == Visibility.Visible && templateMenu.Items.OfType<MenuItem>().Any(m => ReferenceEquals(m.Command, shell.ReportTemplates!.SelectCommand)),
                    "Template dei report nel menu Aspetto, comando collegato al ViewModel");
                Check(shell.Center is NavigationViewModel && main.ActualWidth > 1000 && main.Title == "ANTHEA", "Home al centro, finestra principale");

                shell.Navigation.Select(ShellPage.Modules);
                await Capture(main, output, "02-moduli-singoli.png", bindings);
                Check(shell.Center is ModuleCatalogViewModel, "Moduli singoli al centro");
                int icons = Descendants<Viewbox>(main).Count(v => v is { IsVisible: true, Child: ContentControl { Content: FrameworkElement } });
                Check(icons > 0 && icons == shell.Navigation.Modules.Areas.Sum(a => a.Groups.Sum(g => g.Cards.Count)), "Icone dei moduli nelle schede del catalogo (" + icons + ")");

                // A sample project: a bridge with an abutment and a pier, four sheets.
                var document = ProjectDocuments.CreateArchive();
                var root = ProjectDocuments.AddProject(document, "Viadotto Rio Secco");
                var abutment = ProjectDocuments.AddSection(root, "Spalla A");
                var pier = ProjectDocuments.AddSection(root, "Pila 1");
                var concrete = ProjectDocuments.AddSheet(abutment, "mat_calcestruzzo", "Calcestruzzo delle fondazioni");
                var piles = ProjectDocuments.AddSheet(abutment, "geo_palo_verticale", "Pali della spalla");
                var group = ProjectDocuments.AddSheet(pier, "geo_efficienza_orizzontale", "Palificata della pila");
                var strata = ProjectDocuments.AddSheet(pier, "geo_stratigrafia", "Stratigrafia S1");
                workspace.OpenDocument(document, null, false);
                await Capture(main, output, "03-progetti.png", bindings);
                Check(shell.HasProject && shell.Center is SectionOverviewViewModel && shell.Tree.Roots.Count == 1 && shell.Navigation.Page == ShellPage.Projects,
                    "Archivio aperto: pagina Progetti con l'albero");

                // After the checkpoint: the links of «Fogli e sottosezioni» of the overview (a group closed at the start, as today); the abutment
                // opens its overview. Five common data on the card, the others in «Tutti i dati comuni».
                Check(shell.Overview is { FirstCommonData.Count: 5, HasMoreCommonData: true } && Descendants<Expander>(main).Count(x => x.Name is "MoreCommonData" or "Links" && x.IsVisible && !x.IsExpanded) == 2,
                    "Riepilogo: cinque dati comuni, gli altri e i link in due gruppi chiusi");
                Descendants<Expander>(main).Single(x => x.Name == "Links").IsExpanded = true;
                await Idle(main);
                var links = Descendants<Button>(main).Where(b => ReferenceEquals(b.Command, shell.OpenLinkCommand)).ToArray();
                Check(links.Select(b => b.Content as string).SequenceEqual(["Spalla A", "Pila 1"]) && Descendants<Button>(main).Any(b => ReferenceEquals(b.Command, shell.DuplicateSectionCommand) && b.IsVisible),
                    "Riepilogo del progetto: «Duplica sezione» e i link delle sezioni (" + links.Length + ")");
                await Press(links[0]);
                Check(shell.Overview?.Section?.S("nome") == "Spalla A" && shell.Center is SectionOverviewViewModel, "Link «Spalla A»: il suo riepilogo");

                // Four tabs: three sheets of the project and a single file; the focus on the piles.
                var project = workspace.Project!;
                var concreteSheet = workspace.OpenSheet(project.Find(concrete.S("id"))!);
                var pileSheet = workspace.OpenSheet(project.Find(piles.S("id"))!);
                var groupSheet = workspace.OpenSheet(project.Find(group.S("id"))!);
                var steel = workspace.NewCalculation("mat_acciaio_armatura");
                workspace.Activate(pileSheet);
                await Capture(main, output, "04-schede.png", bindings);
                Check(shell.Windows.Main.Tabs.Count == 4 && shell.Center is SheetTabViewModel { Session: var shown } && ReferenceEquals(shown, pileSheet)
                    && shell.SheetBar.IsVisible && shell.SheetBar.BackLabel == SheetBarViewModel.BackToProject, "Quattro schede, il foglio dei pali con la sua barra");
                await ShellTabInputChecks.Run(main, shell.Windows.Main, Check, name => Capture(main, output, name, bindings), "04-trascina-schede");
                workspace.Activate(steel);
                await Capture(main, output, "05-file-singolo.png", bindings);
                Check(shell.SheetBar.BackLabel == SheetBarViewModel.BackToHome && !shell.SheetBar.CanConfirm, "File singolo in una scheda");

                shell.Navigation.Select(ShellPage.Models);
                await Capture(main, output, "06-modelli.png", bindings);
                Check(shell.Center is ModelsPageViewModel && shell.Models.Cards.Count == 3, "Modelli: le tre sezioni del progetto");

                shell.ShowWiki(Anthea.Wiki.WikiCatalog.ForModule("geo_palo_verticale")?.Id);
                await Capture(main, output, "07-wiki.png", bindings);
                Check(shell.Center is PageTabViewModel { Kind: ShellTabKind.Wiki } && shell.Windows.Main.Tabs.Count == 5, "Wiki in una scheda");

                // «Sposta in una nuova finestra»: the pile group in a detached window, the main window beside it.
                var groupTab = shell.Windows.TabOf(groupSheet)!;
                var host = shell.Windows.MoveToNewWindow(groupTab);
                Check(detached.Count == 1, "Una finestra staccata creata (" + detached.Count + ")");
                var window = detached[0];
                await Capture(window.Window, output, "08-finestra-staccata.png", bindings);
                // 19/11/2026: every detached window has the bar of its sheet (Apri, Salva, Salva con nome, W, Wiki del modulo, Approfondimenti).
                bool Shown(string name) => window.Window.FindName(name) is Button { IsVisible: true };
                Check(new[] { "OpenFile", "SaveFile", "SaveFileAs", "WordReport", "ModuleGuide", "ModuleTopics" }.All(Shown) && !Shown("BriefWordReport")
                    && host.GuideId == Anthea.Wiki.WikiCatalog.ForModule(groupSheet.Module)?.Id && host.Topics.Count > 0,
                    "Finestra staccata: la barra del foglio con Apri, Salva, Salva con nome, W, Wiki del modulo e Approfondimenti");
                workspace.Activate(concreteSheet);
                await Capture(main, output, "09-principale-con-staccata.png", bindings);
                Check(tracker.Hosts.Count == 1 && ReferenceEquals(tracker.Hosts.First(), host) && ReferenceEquals(tracker.WindowOf(host), window),
                    "Una sola finestra staccata nel tracker, quella della scheda (" + tracker.Hosts.Count + ")");
                Check(host.Tabs.Count == 1 && ReferenceEquals(host.Tabs[0], groupTab), "La scheda della palificata nella finestra staccata (" + host.Tabs.Count + " schede)");
                Check(shell.Windows.Main.Tabs.Count == 4 && !shell.Windows.Main.Tabs.Contains(groupTab),
                    "Quattro schede rimaste nella finestra principale (" + shell.Windows.Main.Tabs.Count + ")");
                Check(window.Activations == 1 && window.IsActive, "Finestra staccata attivata una volta (" + window.Activations + " richieste), poi attiva");

                // The same tabs in «Scuro», without saving the preference of the user.
                Appearance.Set(AppAppearance.Dark, persist: false);
                try { await Capture(main, output, "10-scuro.png", bindings); }
                finally { Appearance.Set(AppAppearance.Light, persist: false); }

                // The user closes the detached window: its tab goes back to the main window.
                window.Window.Close();
                bindings.Step = "chiusura della finestra staccata";
                await Idle(main);
                Check(tracker.Hosts.Count == 0 && shell.Windows.Main.Tabs.Contains(groupTab) && workspace.Sheets.Contains(groupSheet), "Finestra staccata chiusa: scheda riportata nella principale");

                // The complete RC module reuses the quick module's action drawing and camera.
                var complete = workspace.NewCalculation("str_palo")!;
                await Idle(main);
                var completeSheet = (Anthea.Presentation.Modules.ConcreteSection.ConcreteSheetViewModel)complete.ViewModel;
                completeSheet.Reload(Anthea.Presentation.Tests.ConcreteSectionScenarios.FixtureData(ShellReferences.Root, "default"));
                await Idle(main);
                var completeView = Ui.Descendants<X.Desktop.Views.ConcreteSection.ConcreteSectionSheetView>(main).Single();
                var actionRow = completeSheet.Actions.Table("3D").Table.Shown[0];
                actionRow["N"] = "-500"; actionRow["Mx"] = "120"; actionRow["My"] = "-70";
                completeSheet.Actions.Table("3D").Table.Refresh();
                ((System.Windows.Controls.Primitives.ToggleButton)completeView.FindName("Actions3DToggle")).IsChecked = true;
                await Idle(main);
                Check(completeView.ActionsDrawing.IsVisible && completeView.ActionsDrawing.Scene is { Actions.Count: 3 },
                    "Modulo completo: Azioni 3D mostra la combinazione selezionata con N, Mx e My");
                File.WriteAllBytes(Path.Combine(output, "azioni-3d-completo-disegno.png"), completeView.ActionsDrawing.Png(800, 600));
                await Capture(main, output, "08b-azioni-3d-completo.png", bindings);
                completeSheet.IsReadOnly = true;
                completeSheet.Actions3DSource = "Taglio"; await Idle(main);
                Check(completeView.ActionsDrawing.IsVisible && completeView.ActionsDrawing.Scene!.Actions.SequenceEqual(completeSheet.Actions3DScene.Actions),
                    "Modulo completo: Azioni 3D disponibile anche in sola lettura");
                completeSheet.IsReadOnly = false;
                workspace.CloseSheet(complete); await Idle(main);
                // 17/11/2026: a new quick RC section opens by itself in its own window, with the size of SheetLayout.OwnWindow.
                int windowsBefore = detached.Count;
                var quick = workspace.NewCalculation(SheetLayout.QuickSectionModule)!;
                await Idle(main);
                Check(detached.Count == windowsBefore + 1 && tracker.Hosts.Count == 1 && shell.Windows.TabOf(quick) is { IsDetached: true }, "Verifica rapida: finestra propria aperta da sola");
                var quickWindow = detached[^1];
                var size = SheetLayout.OwnWindow(SheetLayout.QuickSectionModule)!.Value;
                Check(quickWindow.Window.Width == size.Width && quickWindow.Window.Height == size.Height, "Verifica rapida: la finestra ha la sua dimensione (" + quickWindow.Window.Width + " × " + quickWindow.Window.Height + ")");
                if (quick.ViewModel is Anthea.Presentation.Modules.QuickSection.QuickSectionSheetViewModel quickSheet)
                {
                    // Enter in a box (19/11/2026): confirms the value and goes to the next box to be written, skipping My (read-only in straight bending).
                    var quickView = Ui.Descendants<X.Desktop.Views.QuickSection.QuickSectionSheetView>(quickWindow.Window).Single();
                    TextBox RowBox(string path) => Ui.Descendants<TextBox>(quickView).Single(t => t.DataContext == quickSheet.CombinationRows[0]
                        && t.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path == path);
                    void Enter(TextBox box, string text)
                    {
                        box.Focus(); box.Text = text;
                        box.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(box), 0, Key.Enter) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                    }
                    Enter(RowBox("N"), "-1000");
                    Check(quickSheet.CombinationRows[0].N == "-1000" && Keyboard.FocusedElement == RowBox("Mx"), "Verifica rapida: Invio conferma N e passa a Mx");
                    Enter(RowBox("Mx"), "400");
                    Check(quickSheet.CombinationRows[0].Mx == "400" && Keyboard.FocusedElement is TextBox { IsReadOnly: false } afterMx && afterMx != RowBox("My") && quickView.IsAncestorOf(afterMx),
                        "Verifica rapida: Invio conferma Mx e salta My in sola lettura");
                    // Two combinations with values, the first selected, for the picture of the table.
                    string initialSectionName = quickSheet.Title;
                    var sectionName = (TextBox)quickView.FindName("TitleBox");
                    Enter(sectionName, "Pilastro P1 · base"); await Idle(main);
                    Check(quickSheet.Title == "Pilastro P1 · base" && quick.Heading == quickSheet.Title && quickWindow.Window.Title == quickSheet.Title + " — ANTHEA",
                        "Nome sezione: Invio aggiorna subito il nome salvato, la scheda e il titolo della finestra");
                    quickSheet.AddCombinationCommand.Execute(null);
                    quickSheet.CombinationRows[1].N = "-300"; quickSheet.CombinationRows[1].Mx = "-750";
                    quickSheet.CombinationRows[0].IsSelected = true;
                    await quickSheet.PendingCalculation.WaitAsync(TimeSpan.FromSeconds(120));
                    await Idle(main);
                    Check(quickSheet.CombinationRows.Count == 2 && quickSheet.CombinationRows.All(r => r.Ratio.Length > 0), "Verifica rapida: due combinazioni con η");
                    var moveUp = (Button)quickView.FindName("MoveCombinationUp");
                    var moveDown = (Button)quickView.FindName("MoveCombinationDown");
                    string[] originalOrder = quickSheet.CombinationRows.Select(r => r.Id).ToArray();
                    Check(moveUp.IsVisible && !moveUp.IsEnabled && moveDown.IsEnabled, "Ordine combinazioni: prima riga spostabile solo sotto");
                    await Press(moveDown);
                    await quickSheet.PendingCalculation.WaitAsync(TimeSpan.FromSeconds(120)); await Idle(main);
                    Check(quickSheet.CombinationRows.Select(r => r.Id).SequenceEqual(originalOrder.Reverse())
                        && quickSheet.SelectedCombinationId == originalOrder[0] && moveUp.IsEnabled && !moveDown.IsEnabled,
                        "Ordine combinazioni: pulsante sotto scambia le righe e conserva selezione e limiti");
                    await Capture(quickWindow.Window, output, "08b-ordine-combinazioni.png", bindings);
                    quickSheet.IsReadOnly = true; await Idle(main);
                    Check(!moveUp.IsEnabled && !moveDown.IsEnabled, "Ordine combinazioni: pulsanti disattivati in sola lettura");
                    Check(sectionName.IsReadOnly, "Nome sezione: campo bloccato in sola lettura");
                    quickSheet.IsReadOnly = false; await Idle(main);
                    await Press(moveUp);
                    await quickSheet.PendingCalculation.WaitAsync(TimeSpan.FromSeconds(120)); await Idle(main);
                    Check(quickSheet.CombinationRows.Select(r => r.Id).SequenceEqual(originalOrder) && quickSheet.SelectedCombinationId == originalOrder[0],
                        "Ordine combinazioni: pulsante sopra ripristina l’ordine, con la stessa combinazione selezionata");
                    Enter(sectionName, initialSectionName); await Idle(main);
                    // «Centra sezione» (19/11/2026): after a zoom the section comes back as at the opening.
                    var drawing = quickView.Drawing;
                    byte[] before = Ui.Snapshot(drawing);
                    for (int turn = 0; turn < 4; turn++)
                        drawing.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, 120) { RoutedEvent = UIElement.MouseWheelEvent });
                    await Idle(main);
                    byte[] zoomed = Ui.Snapshot(drawing);
                    var centerButton = (Button)quickView.FindName("CenterSection");
                    centerButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    await Idle(main);
                    Check(centerButton.IsVisible && !before.SequenceEqual(zoomed) && Ui.Snapshot(drawing).SequenceEqual(before), "Verifica rapida: «Centra sezione» riporta la sezione al centro dopo lo zoom");
                    string beforeActions = quickSheet.Data.ToJsonString();
                    quickSheet.Display = "Azioni 3D"; await quickSheet.PendingCalculation.WaitAsync(TimeSpan.FromSeconds(120)); await Idle(main);
                    var actions = quickView.ActionsDrawing;
                    Check(actions.IsVisible && !quickView.Chart.IsVisible && !quickView.Drawing.IsVisible, "Azioni 3D: quarta vista esclusiva del riquadro grafico");
                    var glyphs = X.Desktop.Views.QuickSection.QuickActionsViewport.ActionGeometry(quickSheet.ActionsScene, 800);
                    var axial = glyphs.Single(g => g.Action.Name == "N");
                    Check(axial.Points[^1].Z < axial.Points[0].Z && axial.Action.Label.Contains("1000"), "Azioni 3D: compressione verso la sezione con etichetta positiva");
                    var signedScene = new Anthea.Application.Concrete.ConcreteActionScene("Segni",
                        [new("N", 'z', false, 100), new("Vx", 'x', false, -30), new("Vy", 'y', false, 20), new("Mx", 'x', true, -40), new("My", 'y', true, 12)], "");
                    var signedGlyphs = X.Desktop.Views.QuickSection.QuickActionsViewport.ActionGeometry(signedScene, 800);
                    var mxGlyph = signedGlyphs.Single(g => g.Action.Name == "Mx"); var myGlyph = signedGlyphs.Single(g => g.Action.Name == "My");
                    var mxCenter = new System.Windows.Media.Media3D.Point3D(520, 0, 0); var myCenter = new System.Windows.Media.Media3D.Point3D(0, 520, 0);
                    Check(signedGlyphs.Single(g => g.Action.Name == "N").Points[^1].Z > 0
                        && signedGlyphs.Single(g => g.Action.Name == "Vx").Points[^1].X < 0 && signedGlyphs.Single(g => g.Action.Name == "Vy").Points[^1].Y > 0
                        && System.Windows.Media.Media3D.Vector3D.CrossProduct(mxGlyph.Points[0] - mxCenter, mxGlyph.Points[1] - mxCenter).X > 0
                        && System.Windows.Media.Media3D.Vector3D.CrossProduct(myGlyph.Points[0] - myCenter, myGlyph.Points[1] - myCenter).Y < 0,
                        "Azioni 3D: trazione, tagli e rotazioni seguono il segno nei rispettivi assi 3D");
                    foreach (char momentAxis in "xy")
                        foreach (int momentSign in new[] { -1, 1 })
                        {
                            var momentScene = new Anthea.Application.Concrete.ConcreteActionScene("Coppia",
                                [new("M" + momentAxis, momentAxis, true, momentSign * 40)], "");
                            var moment = X.Desktop.Views.QuickSection.QuickActionsViewport.ActionGeometry(momentScene, 800).Single();
                            var momentCenter = momentAxis == 'x' ? mxCenter : myCenter;
                            // Independent force couple on the displayed +z face: Mx+ compresses +y;
                            // My+ compresses -x. Compression points into the prism (-z), tension outwards.
                            var compressedSide = momentAxis == 'x' ? new System.Windows.Media.Media3D.Vector3D(0, momentSign, 0)
                                : new System.Windows.Media.Media3D.Vector3D(-momentSign, 0, 0);
                            var physicalCouple = System.Windows.Media.Media3D.Vector3D.CrossProduct(compressedSide, new(0, 0, -1))
                                + System.Windows.Media.Media3D.Vector3D.CrossProduct(-compressedSide, new(0, 0, 1));
                            var arrowTurn = System.Windows.Media.Media3D.Vector3D.CrossProduct(moment.Points[0] - momentCenter, moment.Points[1] - momentCenter);
                            Check(System.Windows.Media.Media3D.Vector3D.DotProduct(arrowTurn, physicalCouple) > 0
                                && moment.Action.Label.Contains("40") && !moment.Action.Label.Contains('-'),
                                $"Azioni 3D: M{momentAxis} {momentSign:+0;-0}, freccia concorde con la coppia fisica e valore sempre positivo");
                            actions.Scene = momentScene; actions.InvalidateVisual();
                            File.WriteAllBytes(Path.Combine(output, $"azioni-3d-M{momentAxis}-{(momentSign > 0 ? "positivo" : "negativo")}.png"), actions.Png(750, 500));
                        }
                    actions.Scene = quickSheet.ActionsScene; actions.InvalidateVisual();
                    var origin3 = actions.Project(new(0, 0, 0));
                    var axisX = actions.Project(new(1, 0, 0)) - origin3; var axisY = actions.Project(new(0, 1, 0)) - origin3;
                    var sectionEdge = actions.Project(new(300, 400, -800)) - actions.Project(new(-300, 400, -800));
                    Check(Math.Abs(Vector.CrossProduct(axisX, axisY)) > .1 && Math.Abs(Vector.CrossProduct(axisX, sectionEdge)) < 1e-8,
                        "Azioni 3D: x/y distinti e assi paralleli ai lati della sezione nella stessa proiezione");
                    byte[] initialActions = Ui.Snapshot(actions);
                    var orbitSize = new Size(actions.ActualWidth, actions.ActualHeight);
                    var orbitBefore = actions.Projection(orbitSize);
                    static double ProjectedBasisSize(X.Desktop.Views.QuickSection.QuickActionsViewport.Projection3D projection)
                    {
                        System.Windows.Media.Media3D.Vector3D[] basis = [new(100, 0, 0), new(0, 100, 0), new(0, 0, 100)];
                        return basis.Sum(v => (projection.Screen(projection.Pivot + v) - projection.Screen(projection.Pivot)).LengthSquared);
                    }
                    double orbitMetric = ProjectedBasisSize(orbitBefore);
                    File.WriteAllBytes(Path.Combine(output, "azioni-3d-rotazione-prima.png"), initialActions);
                    foreach (var orbitTurn in new[] { (70d, 10d), (-130d, -40d), (200d, 45d) })
                    {
                        actions.Rotate(orbitTurn.Item1, orbitTurn.Item2);
                        var orbitAfter = actions.Projection(orbitSize);
                        Check(orbitAfter.Scale == orbitBefore.Scale && orbitAfter.Pivot == orbitBefore.Pivot
                            && (orbitAfter.Screen(orbitBefore.Pivot) - orbitBefore.Screen(orbitBefore.Pivot)).Length < 1e-8
                            && Math.Abs(ProjectedBasisSize(orbitAfter) / orbitMetric - 1) < 1e-8,
                            $"Azioni 3D: rotazione {orbitTurn}, scala e centro fissi senza deformazione della proiezione");
                    }
                    await Idle(main);
                    File.WriteAllBytes(Path.Combine(output, "azioni-3d-rotazione-dopo.png"), Ui.Snapshot(actions));
                    var orbitScene = actions.Scene;
                    actions.Scene = new("Altra combinazione", [new("N", 'z', false, 40), new("Vx", 'x', false, -40), new("Vy", 'y', false, -40)], "");
                    var orbitOtherScene = actions.Projection(orbitSize);
                    Check(orbitOtherScene.Scale == orbitBefore.Scale && orbitOtherScene.Anchor == orbitBefore.Anchor,
                        "Azioni 3D: cambiare le azioni non riadatta scala o centratura");
                    actions.Scene = orbitScene;
                    actions.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, 120) { RoutedEvent = UIElement.MouseWheelEvent });
                    await Idle(main);
                    var orbitZoomed = actions.Projection(orbitSize);
                    Check(Math.Abs(orbitZoomed.Scale / orbitBefore.Scale - 1.12) < 1e-8
                        && orbitZoomed.Anchor == orbitBefore.Anchor && orbitZoomed.Pivot == orbitBefore.Pivot
                        && Math.Abs(ProjectedBasisSize(orbitZoomed) / orbitMetric - 1.12 * 1.12) < 1e-8,
                        "Azioni 3D: solo la rotella cambia la scala, uniformemente e intorno al centro fisso");
                    ((Button)quickView.FindName("CenterActions")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Idle(main);
                    Check(Ui.Snapshot(actions).SequenceEqual(initialActions), "Azioni 3D: Centra vista ripristina l’orientamento sdraiato");
                    await Capture(quickWindow.Window, output, "08b-azioni-3d-momento.png", bindings);
                    File.WriteAllBytes(Path.Combine(output, "azioni-3d-momento-disegno.png"), actions.Png(1000, 700));
                    ((ListBox)quickView.FindName("ActionsSourceSelector")).SelectedItem = "Taglio"; await Idle(main);
                    Check(quickSheet.ActionsSource == "Taglio" && quickSheet.Data.ToJsonString() == beforeActions,
                        "Azioni 3D: selettore reale delle tabelle senza creare o modificare dati");
                    quickSheet.ActionsSource = "Momento"; quickSheet.Display = "Sezione"; await Idle(main);
                }
                await Capture(quickWindow.Window, output, "08b-verifica-rapida.png", bindings);
                if (quick.ViewModel is Anthea.Presentation.Modules.QuickSection.QuickSectionSheetViewModel shearSheet)
                {
                    static void EnterShear(TextBox box, string text) { box.Text = text; box.GetBindingExpression(TextBox.TextProperty)!.UpdateSource(); }
                    var savedShearSheet = (System.Text.Json.Nodes.JsonObject)shearSheet.Data.DeepClone();
                    var shearView = Descendants<X.Desktop.Views.QuickSection.QuickSectionSheetView>(quickWindow.Window).Single();
                    var panel = (FrameworkElement)shearView.FindName("ShearPanel");
                    var shearToggle = (Button)shearView.FindName("ToggleShear");
                    double compactWidth = quickWindow.Window.Width;
                    await Press(shearToggle);
                    var copyShear = (Button)shearView.FindName("CopyShearCombinations");
                    Check(panel.IsVisible && copyShear.IsVisible && shearSheet.ShearCombinationRows.Count == 1,
                        "Taglio rapido: apertura a libro con tabella autonoma e pulsante Copia combinazioni");
                    var modelSelector = (ListBox)shearView.FindName("ShearModelSelector");
                    var resetShear = (Button)shearView.FindName("ResetShearConditions");
                    Check(((Button)shearView.FindName("ExportShearExcel")).IsVisible && ((Button)shearView.FindName("ImportShearExcel")).IsEnabled,
                        "Taglio rapido: pulsanti Excel presenti sulla tabella autonoma");
                    modelSelector.SelectedItem = "Verifica sezione non armata"; await Idle(main);
                    Check(resetShear.IsEnabled && shearSheet.ShearWithoutLinks, "Taglio rapido: selezione blu cambia il modello della combinazione");
                    await Press(resetShear);
                    Check(shearSheet.ShearWithLinks && !resetShear.IsEnabled, "Taglio rapido: ripristino riallinea il modello alla tipologia della riga");
                    await Press(copyShear);
                    var shearTable = (ItemsControl)shearView.FindName("ShearCombinations");
                    var vx = Descendants<TextBox>(shearTable).First(t => BindingOperations.GetBinding(t, TextBox.TextProperty)?.Path.Path == "Vx");
                    EnterShear(vx, "75"); await Idle(main);
                    var effective = (CheckBox)shearView.FindName("EffectiveStirrup");
                    effective.IsChecked = false; await Idle(main);
                    var independent = (ContentControl)shearView.FindName("IndependentShearDiameter");
                    var diameter = Descendants<TextBox>(independent).Single(); EnterShear(diameter, "8");
                    shearSheet.ShearForm.Set("passo_mm", "100");
                    var balanceShear = (Button)shearView.FindName("BalanceShear");
                    Check(balanceShear.IsVisible && balanceShear.IsEnabled, "Taglio rapido: pulsante Rottura bilanciata disponibile");
                    await Press(balanceShear);
                    await shearSheet.PendingCalculation.WaitAsync(TimeSpan.FromSeconds(120)); await Idle(main);
                    Check(independent.IsVisible && shearSheet.ShearCombinationRows[0].Vx == "75" && !shearSheet.EffectiveStirrup
                        && shearSheet.Current!.Shear!.All(r => r.Error.Length == 0), "Taglio rapido: editor reali di Vx e armatura indipendente con risultati");
                    Check(shearSheet.Data["combinazioni"]!.ToJsonString() == savedShearSheet["combinazioni"]!.ToJsonString()
                        && shearSheet.Data["input"]!.ToJsonString() == savedShearSheet["input"]!.ToJsonString(), "Taglio rapido: nessuna colonna o azione aggiunta alla tabella originale, geometria invariata");
                    await Capture(quickWindow.Window, output, "08b-taglio-indipendente.png", bindings);
                    ((FrameworkElement)shearView.FindName("ShearResultTable")).BringIntoView();
                    await Capture(quickWindow.Window, output, "08b-taglio-risultati.png", bindings);
                    shearSheet.Display = "Azioni 3D"; shearSheet.ActionsSource = "Taglio";
                    shearSheet.ActionCombinationId = shearSheet.ShearCombinationRows[0].Id; await Idle(main);
                    Check(shearSheet.ActionsScene.Actions.Single(a => a.Name == "Vx").Value == 75 && shearView.ActionsDrawing.IsVisible,
                        "Azioni 3D: tagli della combinazione scelta nella tabella autonoma");
                    await Capture(quickWindow.Window, output, "08b-azioni-3d-taglio.png", bindings);
                    File.WriteAllBytes(Path.Combine(output, "azioni-3d-taglio-disegno.png"), shearView.ActionsDrawing.Png(1000, 700));
                    shearSheet.ActionsSource = "Momento"; shearSheet.Display = "Sezione";
                    shearSheet.ShearModel = "Verifica sezione non armata";
                    await shearSheet.PendingCalculation.WaitAsync(TimeSpan.FromSeconds(120)); await Idle(main);
                    Check(shearSheet.SelectedShearResult is { Error.Length: 0, Directions.Count: 2 }, "Taglio rapido: selezione non armata calcola subito con barre longitudinali assunte ancorate");
                    await Capture(quickWindow.Window, output, "08b-taglio-non-armato.png", bindings);
                    shearSheet.ShearModel = "Verifica con armatura a taglio";
                    await shearSheet.PendingCalculation.WaitAsync(TimeSpan.FromSeconds(120)); await Idle(main);
                    shearSheet.IsReadOnly = true; await Idle(main);
                    Check(!copyShear.IsEnabled && !effective.IsEnabled && vx.IsReadOnly && !balanceShear.IsEnabled, "Taglio rapido: tutti gli editor e rottura bilanciata protetti in sola lettura");
                    shearSheet.IsReadOnly = false;
                    await Press(shearToggle);
                    Check(!panel.IsVisible && shearSheet.ShearCombinationRows.Count > 0 && Math.Abs(quickWindow.Window.Width - compactWidth) < 2,
                        "Taglio rapido: chiusura conserva i dati e ripristina la larghezza");
                    shearSheet.Reload(savedShearSheet);
                    await shearSheet.PendingCalculation.WaitAsync(TimeSpan.FromSeconds(120)); await Idle(main);
                }
                if (quick.ViewModel is Anthea.Presentation.Modules.QuickSection.QuickSectionSheetViewModel biaxial)
                {
                    // The biaxial bending: the check is «Capacità» or «Metodo n», without the sign of the moment.
                    biaxial.Bending = "Deviata"; biaxial.CombinationRows[0].My = "150";
                    await biaxial.PendingCalculation.WaitAsync(TimeSpan.FromSeconds(120));
                    await Idle(main);
                    Check(biaxial.Methods.SequenceEqual(["Capacità", "Metodo n"]) && biaxial.Method == "Capacità", "Verifica rapida, flessione deviata: verifica di capacità o metodo n");
                    await Capture(quickWindow.Window, output, "08c-verifica-rapida-deviata.png", bindings);
                    // The creep (18/11/2026): φ = 1,5 with the method n, the n used in evidence in the materials and in the results.
                    biaxial.Bending = "Retta"; biaxial.Method = "Metodo n";
                    biaxial.CreepForm.Set(Anthea.Application.Concrete.ConcreteQuickSection.CreepKey, "1.5");
                    await biaxial.PendingCalculation.WaitAsync(TimeSpan.FromSeconds(120));
                    await Idle(main);
                    Check(biaxial.SteelValues.Single(v => v.InUse).Value != biaxial.SteelValues.Single(v => v.Label == "n₀").Value && biaxial.ResultValues.Any(v => v.InUse),
                        "Verifica rapida: con φ = 1,5 n cambia ed è in evidenza");
                    // The stresses of the bars on the drawing (19/11/2026): the most stressed bar of each layer, with its line and σs.
                    Check(biaxial.StressCallouts.Count == biaxial.LayerRows.Count && biaxial.StressCallouts.All(c => c.Text.StartsWith("σs = ", StringComparison.Ordinal)),
                        "Verifica rapida, metodo n: una tensione σs per strato nel disegno");
                    await Capture(quickWindow.Window, output, "08d-verifica-rapida-viscosita.png", bindings);
                    // The limit states of the combinations (18/11/2026): one row for each state, the quasi-permanent one selected with the exposure XC3.
                    biaxial.Method = "Momento positivo";
                    biaxial.CombinationRows[0].My = "0"; biaxial.CombinationRows[1].Name = "SLU 1"; biaxial.CombinationRows[1].N = "-1400"; biaxial.CombinationRows[1].Mx = "560";
                    biaxial.CombinationRows[1].State = "SLU";
                    (string Name, string N, string Mx, string State)[] more = [("Sisma", "-900", "620", "SLV"), ("Rara", "-1000", "400", "SLE"), ("Frequente", "-850", "330", "SLE_FREQ"),
                        ("Q. perm.", "-800", "300", "SLE_QP")];
                    foreach (var (name, n, mx, state) in more)
                    {
                        biaxial.AddCombinationCommand.Execute(null);
                        var row = biaxial.CombinationRows[^1];
                        row.Name = name; row.N = n; row.Mx = mx; row.State = state;
                    }
                    biaxial.CreepForm.Set(Anthea.Application.Concrete.ConcreteQuickSection.ExposureKey, "XC3");
                    biaxial.CombinationRows[^1].IsSelected = true;
                    await biaxial.PendingCalculation.WaitAsync(TimeSpan.FromSeconds(120));
                    await Idle(main);
                    Check(biaxial.CombinationRows.Count == 6 && biaxial.CombinationRows.All(r => r.Ratio.Length > 0) && biaxial.Current?.State == "SLE_QP" && biaxial.Method == "Metodo n" && biaxial.Model is null,
                        "Verifica rapida: sei combinazioni, una per stato limite, tutte con il rapporto");
                    await Capture(quickWindow.Window, output, "08e-verifica-rapida-stati-limite.png", bindings);
                    var resetSettings = Descendants<Button>(quickWindow.Window).Single(b => b.Name == "ResetCombinationSettings");
                    biaxial.CombinationRows[0].IsSelected = true; biaxial.Model = "Elastica"; biaxial.Method = "Momento negativo";
                    string actionsBeforeReset = biaxial.Data["combinazioni"]!.ToJsonString();
                    string optionsBeforeReset = biaxial.Data["workspace_ca"]!.ToJsonString();
                    await Idle(main);
                    Check(resetSettings.IsVisible && resetSettings.IsEnabled, "Pulsante ripristino impostazioni visibile sopra le combinazioni e abilitato dopo modifica");
                    await Press(resetSettings); await biaxial.PendingCalculation.WaitAsync(TimeSpan.FromSeconds(120)); await Idle(main);
                    Check(biaxial.Model == "Plastica" && biaxial.Method == "Momento positivo" && !resetSettings.IsEnabled
                        && biaxial.Data["combinazioni"]!.ToJsonString() == actionsBeforeReset && biaxial.Data["workspace_ca"]!.ToJsonString() == optionsBeforeReset,
                        "Ripristino da pulsante: modello e verifica predefiniti, combinazioni e impostazioni generali intatte");
                    await Capture(quickWindow.Window, output, "08e-impostazioni-ripristinate.png", bindings);
                    biaxial.CombinationRows[^1].IsSelected = true;
                    // The circular section with the method n (19/11/2026): the most compressed bar and the most tensioned one, then back to the rectangle.
                    biaxial.Shape = Anthea.Application.Concrete.ConcreteQuickSection.Circular;
                    // 19/11/2026: the second ring with its link (Ø st.), s calculated and the link drawn between the rings.
                    biaxial.BarRows[1].Enabled!.IsChecked = true;
                    await biaxial.PendingCalculation.WaitAsync(TimeSpan.FromSeconds(120));
                    await Idle(main);
                    Check(biaxial.SecondRingLinkDiameter > 0 && biaxial.Data["input"]!["second_inner_gap"]!.ToString() == biaxial.BarRows[1].Link!.Text,
                        "Verifica rapida circolare: secondo anello con la sua staffa, il motore la usa come distanza fra gli anelli (" + biaxial.BarRows[1].Link!.Text + " mm)");
                    Check(biaxial.StressCallouts.Count == 2 && biaxial.StressCallouts.Count(c => c.Compression) == 1, "Verifica rapida circolare, metodo n: la barra più compressa e la più tesa");
                    await Capture(quickWindow.Window, output, "08f-verifica-rapida-circolare-tensioni.png", bindings);
                    biaxial.BarRows[1].Enabled!.IsChecked = false;
                    biaxial.Shape = Anthea.Application.Concrete.ConcreteQuickSection.Rectangular;
                    await biaxial.PendingCalculation.WaitAsync(TimeSpan.FromSeconds(120));
                    await Idle(main);
                }
                // Report commands are inside the detached window and follow its selected tab.
                var word = (Button)quickWindow.Window.FindName("WordReport");
                var briefWord = (Button)quickWindow.Window.FindName("BriefWordReport");
                var newSection = (Button)quickWindow.Window.FindName("NewQuickSection");
                Check(word.IsVisible && briefWord.IsVisible && newSection.IsVisible
                    && ReferenceEquals(word.Command, shell.Commands.ReportCommand) && ReferenceEquals(briefWord.Command, shell.Commands.BriefReportCommand)
                    && ReferenceEquals(word.CommandParameter, quick) && ReferenceEquals(briefWord.CommandParameter, quick), "W e W breve nella finestra staccata, riferiti alla sezione visibile");
                shell.Windows.Select(shell.Windows.TabOf(pileSheet)!);
                await Press(newSection);
                var secondQuick = workspace.Active!;
                var quickHost = shell.Windows.TabOf(quick)!.Host!;
                Check(secondQuick != quick && quickHost.Tabs.Count == 2 && quickHost.SelectedSheet == secondQuick
                    && detached.Count == windowsBefore + 1 && tracker.Hosts.Count == 1, "«+ Nuova sezione» aggiunge una scheda nella stessa finestra");
                var secondQuickModel = (Anthea.Presentation.Modules.QuickSection.QuickSectionSheetViewModel)secondQuick.ViewModel;
                secondQuickModel.CombinationRows[0].N = "-250"; secondQuickModel.CombinationRows[0].Mx = "120";
                await secondQuickModel.PendingCalculation.WaitAsync(TimeSpan.FromSeconds(120));
                await Idle(main);
                Check(secondQuickModel.CombinationRows.Count == 1 && quickHost.Selected!.Title == "Sezione 2"
                    && ReferenceEquals(word.CommandParameter, secondQuick) && ReferenceEquals(briefWord.CommandParameter, secondQuick), "Seconda sezione indipendente, pulsanti Word aggiornati");
                await Capture(quickWindow.Window, output, "08f-verifica-rapida-due-schede.png", bindings);
                await ShellTabInputChecks.Run(quickWindow.Window, quickHost, Check, name => Capture(quickWindow.Window, output, name, bindings), "08f-trascina-sezioni");
                var firstTabButton = Descendants<Button>(quickWindow.Window).Single(b => ReferenceEquals(b.Command, shell.Windows.SelectCommand) && ReferenceEquals(b.CommandParameter, shell.Windows.TabOf(quick)));
                await Press(firstTabButton);
                Check(workspace.Active == quick && ((Anthea.Presentation.Modules.QuickSection.QuickSectionSheetViewModel)quick.ViewModel).CombinationRows.Count == 6
                    && ReferenceEquals(word.CommandParameter, quick) && ReferenceEquals(briefWord.CommandParameter, quick), "Selezionando la prima scheda ritornano i suoi dati e i suoi report");
                await Capture(quickWindow.Window, output, "08g-verifica-rapida-prima-scheda.png", bindings);
                var copyTo = (Button)quickWindow.Window.FindName("CopySectionTo");
                var copyFrom = (Button)quickWindow.Window.FindName("CopySectionFrom");
                Check(copyTo.IsVisible && copyTo.IsEnabled && copyFrom.IsVisible && copyFrom.IsEnabled
                    && ReferenceEquals(copyTo.CommandParameter, quick) && ReferenceEquals(copyFrom.CommandParameter, quick),
                    "Copia in e Copia da nella finestra, riferiti alla sezione visibile");
                int copyDialogs = 0;
                services.HandleDialog = async value =>
                {
                    var dialog = (QuickSectionCopyDialogViewModel)value;
                    var window = Offscreen(new Window { Title = dialog.Title, Content = dialog, SizeToContent = SizeToContent.WidthAndHeight });
                    window.Show();
                    try
                    {
                        await Idle(window);
                        var choice = Descendants<ComboBox>(window).Single(c => c.Name == "OtherSection");
                        var confirm = Descendants<Button>(window).Single(b => b.Name == "ConfirmSectionCopy");
                        Check(choice.Items.Count == 1 && confirm.IsEnabled && dialog.Selected!.Sheet == (copyDialogs == 0 ? secondQuick : quick),
                            "Dialogo di copia: sezione corretta, scelta e comando collegati");
                        await Capture(window, output, copyDialogs == 0 ? "08g-copia-in-dialogo.png" : "08g-copia-da-dialogo.png", bindings);
                        await Press(confirm); copyDialogs++;
                    }
                    finally { window.Close(); }
                };
                try
                {
                    string originalQuickData = quick.ViewModel.Data.ToJsonString();
                    string originalSourceActions = quick.ViewModel.Data["combinazioni"]!.ToJsonString();
                    ((Anthea.Presentation.Modules.QuickSection.QuickSectionSheetViewModel)quick.ViewModel).GeometryForm.Set("width_mm", "550");
                    originalQuickData = quick.ViewModel.Data.ToJsonString();
                    await Press(copyTo);
                    if (shell.Commands.CopySectionToCommand.ExecutionTask is { } copyingTo) await copyingTo;
                    await secondQuickModel.PendingCalculation.WaitAsync(TimeSpan.FromSeconds(120)); await Idle(main);
                    Check(secondQuickModel.Data["input"].S("width_mm") == "550" && secondQuickModel.Title == "Sezione 2" && quickHost.SelectedSheet == secondQuick
                        && secondQuickModel.Data["combinazioni"]!.ToJsonString() == originalSourceActions
                        && quick.ViewModel.Data.ToJsonString() == originalQuickData, "Copia in: geometria, combinazioni e impostazioni copiate, nome proprio e origine intatta");
                    secondQuickModel.CombinationRows[0].Mx = "90";
                    secondQuickModel.GeometryForm.Set("width_mm", "650");
                    await Press(copyFrom);
                    if (shell.Commands.CopySectionFromCommand.ExecutionTask is { } copyingFrom) await copyingFrom;
                    await secondQuickModel.PendingCalculation.WaitAsync(TimeSpan.FromSeconds(120)); await Idle(main);
                    Check(copyDialogs == 2 && secondQuickModel.Data["input"].S("width_mm") == "550" && secondQuickModel.CombinationRows.Count == 6
                        && secondQuickModel.Data["combinazioni"]!.ToJsonString() == originalSourceActions && secondQuickModel.Title == "Sezione 2", "Copia da: geometria e combinazioni ripristinate dalla sorgente, nome proprio");
                    await Capture(quickWindow.Window, output, "08g-sezione-copiata.png", bindings);
                    await Press(firstTabButton);
                    ((Anthea.Presentation.Modules.QuickSection.QuickSectionSheetViewModel)quick.ViewModel).GeometryForm.Set("width_mm", "600");
                    await ((Anthea.Presentation.Modules.QuickSection.QuickSectionSheetViewModel)quick.ViewModel).PendingCalculation.WaitAsync(TimeSpan.FromSeconds(120));
                }
                finally { services.HandleDialog = null; }
                await ConcreteDxfWpfChecks.Run((Anthea.Presentation.Modules.QuickSection.QuickSectionSheetViewModel)quick.ViewModel, quickWindow.Window, Check,
                    (window,name)=>Capture(window,output,name,bindings));
                string sectionFile = Path.Combine(output, "raccolta-sezioni.programma");
                services.NextSavePath = sectionFile;
                await Press((Button)quickWindow.Window.FindName("SaveFile"));
                var savedSections = QuickSectionDocuments.Sections(DocumentService.Open(sectionFile));
                Check(savedSections.Count == 2 && JsonNode.DeepEquals(savedSections.Single(s => s.S("id") == quick.Id)["dati"], quick.ViewModel.Data)
                    && JsonNode.DeepEquals(savedSections.Single(s => s.S("id") == secondQuick.Id)["dati"], secondQuick.ViewModel.Data),
                    "Salva della finestra: entrambe le sezioni, DXF, combinazioni e impostazioni nello stesso file");
                var savedIds = savedSections.Select(s => s.S("id")).ToArray(); string selectedId = workspace.Active!.Id;
                int saveDialogs = services.Saves.Count;
                ((Anthea.Presentation.Modules.QuickSection.QuickSectionSheetViewModel)secondQuick.ViewModel).Title = "Sezione salvata insieme";
                await Press((Button)quickWindow.Window.FindName("SaveFile"));
                Check(services.Saves.Count == saveDialogs && QuickSectionDocuments.Sections(DocumentService.Open(sectionFile))
                    .Single(s => s.S("id") == secondQuick.Id)["dati"]![Anthea.Application.Concrete.ConcreteQuickSection.ViewKey].S("titolo") == "Sezione salvata insieme",
                    "Salva della finestra: sovrascrive anche la modifica della sezione non attiva senza chiedere un nuovo file");
                quickWindow.Window.Close();
                await Idle(main);
                Check(tracker.Hosts.Count == 0 && shell.Windows.Main.Tabs.Contains(shell.Windows.TabOf(quick)!), "Verifica rapida: chiusa la sua finestra, la scheda torna nella principale");
                workspace.CloseSheet(quick);
                Check(shell.Windows.Main.Tabs.Contains(shell.Windows.TabOf(secondQuick)!), "La seconda sezione resta aperta dopo la chiusura della finestra e della prima sezione");
                workspace.CloseSheet(secondQuick);
                await Idle(main);
                var restoredSections = (QuickSectionsSession)workspace.OpenFile(sectionFile)!;
                var restoredTabs = workspace.Sheets.Where(s => ReferenceEquals(s.Owner, restoredSections)).ToArray();
                foreach (var restored in restoredTabs)
                    await ((Anthea.Presentation.Modules.QuickSection.QuickSectionSheetViewModel)restored.ViewModel).PendingCalculation.WaitAsync(TimeSpan.FromSeconds(120));
                await Idle(main);
                Check(restoredTabs.Select(s => s.Id).SequenceEqual(savedIds) && workspace.Active!.Id == selectedId
                    && restoredTabs.Any(s => s.Heading == "Sezione salvata insieme") && shell.Windows.TabOf(restoredTabs[0])!.Host == shell.Windows.TabOf(restoredTabs[1])!.Host,
                    "Apri della raccolta: tutte le schede, nomi, ordine e selezione nella stessa finestra");
                await Capture(detached[^1].Window, output, "08g-sezioni-riaperte.png", bindings);
                foreach (var restored in restoredTabs) workspace.CloseSheet(restored);
                await Idle(main);

                // Quick composite section: its own window, all three states, existing stress drawing and detached report targets.
                int beforeComposite = detached.Count;
                var compositeQuick = workspace.NewCalculation(SheetLayout.QuickCompositeModule)!;
                await ((Anthea.Presentation.Modules.QuickCompositeSection.QuickCompositeSectionSheetViewModel)compositeQuick.ViewModel).PendingCalculation.WaitAsync(TimeSpan.FromSeconds(120)); await Idle(main);
                var compositeWindow = detached[^1];
                Check(detached.Count == beforeComposite + 1 && shell.Windows.TabOf(compositeQuick) is { IsDetached: true }, "Composta rapida: finestra propria");
                var compositeModel = (Anthea.Presentation.Modules.QuickCompositeSection.QuickCompositeSectionSheetViewModel)compositeQuick.ViewModel;
                var compositeView = Ui.Descendants<X.Desktop.Views.QuickCompositeSection.QuickCompositeSectionSheetView>(compositeWindow.Window).Single();
                Check(compositeModel.SelectedCase?.Ratio.HasValue == true && compositeModel.Checks.Any(c => c.Heading.Contains("Taglio")), "Composta rapida: riepilogo SLU con tensioni e taglio");
                await Capture(compositeWindow.Window, output, "08h-composta-rapida-SLU.png", bindings);
                compositeModel.AddCombinationCommand.Execute(null);
                compositeModel.CombinationRows[1].Name = "Rara"; compositeModel.CombinationRows[1].State = "SLE RA";
                compositeModel.CombinationRows[1].Casting = "1500"; compositeModel.CombinationRows[1].LongTerm = "1600";
                compositeModel.CombinationRows[1].ShortTerm = "2000"; compositeModel.CombinationRows[1].V = "350";
                await compositeModel.PendingCalculation.WaitAsync(TimeSpan.FromSeconds(120)); await Idle(main);
                Check(compositeModel.Checks.Any(c => c.Heading == "Piattabanda inferiore") && compositeView.Drawing.StressLabels.Count >= 4,
                    "Composta rapida: SLE con fibre principali ed etichette delle tensioni");
                await Capture(compositeWindow.Window, output, "08i-composta-rapida-SLE.png", bindings);
                var compositeState = Ui.Descendants<ComboBox>(compositeView).Single(c => c.DataContext is Anthea.Presentation.Modules.QuickCompositeSection.QuickCompositeCombinationRow row && row.Id == compositeModel.SelectedId);
                compositeState.SelectedItem = "SLE QP";
                await compositeModel.PendingCalculation.WaitAsync(TimeSpan.FromSeconds(120));
                bool CompositeQpVisible() => compositeModel.SelectedCase?.State == "SLE QP" && compositeState.SelectedItem as string == "SLE QP"
                    && Ui.Descendants<TextBlock>(compositeView).Any(t => t.Text == "Rara · SLE QP");
                await IdleUntil(compositeWindow.Window, CompositeQpVisible);
                Check(CompositeQpVisible(), "Composta rapida: menu SL, titolo e risultati concordi dopo il cambio a QP");
                // Let the compositor publish the changed labels before the bitmap is captured.
                await Task.Delay(100); await Idle(compositeWindow.Window);
                await Capture(compositeWindow.Window, output, "08j-composta-rapida-QP.png", bindings);
                compositeModel.Display = "Geometria"; await Idle(main);
                await Capture(compositeWindow.Window, output, "08k-composta-rapida-geometria.png", bindings);
                compositeModel.Display = "Tensioni totali"; await Idle(main);
                byte[] compositeBeforeZoom = Ui.Snapshot(compositeView.Drawing);
                compositeView.Drawing.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, 120) { RoutedEvent = UIElement.MouseWheelEvent }); await Idle(main);
                byte[] compositeZoomed = Ui.Snapshot(compositeView.Drawing);
                ((Button)compositeView.FindName("CenterSection")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Idle(main);
                Check(!compositeZoomed.SequenceEqual(compositeBeforeZoom) && Ui.Snapshot(compositeView.Drawing).SequenceEqual(compositeBeforeZoom), "Composta rapida: centra sezione dopo zoom");
                var compositeWord = (Button)compositeWindow.Window.FindName("WordReport");
                var compositeBrief = (Button)compositeWindow.Window.FindName("BriefWordReport");
                Check(compositeWord.IsVisible && compositeBrief.IsVisible && ReferenceEquals(compositeBrief.CommandParameter, compositeQuick), "Composta rapida: W/W breve riferiti al foglio visibile");
                await Press((Button)compositeWindow.Window.FindName("NewQuickSection"));
                var secondComposite = workspace.Active!; await ((Anthea.Presentation.Modules.QuickCompositeSection.QuickCompositeSectionSheetViewModel)secondComposite.ViewModel).PendingCalculation.WaitAsync(TimeSpan.FromSeconds(120)); await Idle(main);
                Check(secondComposite.Module == SheetLayout.QuickCompositeModule && shell.Windows.TabOf(secondComposite)?.Host == shell.Windows.TabOf(compositeQuick)?.Host
                    && ReferenceEquals(compositeWord.CommandParameter, secondComposite), "Nuova composta nella stessa finestra, report della seconda scheda");
                compositeWindow.Window.Close(); await Idle(main); workspace.CloseSheet(compositeQuick); workspace.CloseSheet(secondComposite); await Idle(main);

                // After the checkpoint of 22/10: the menu of a sheet of the tree offers the new window and the full page.
                var node = (DataTemplate)main.FindResource(new DataTemplateKey(typeof(ProjectNodeViewModel)));
                var menu = ((FrameworkElement)node.LoadContent()).ContextMenu!;
                var commands = menu.Items.OfType<MenuItem>().ToDictionary(i => i.Header as string ?? "", i => BindingOperations.GetBinding(i, MenuItem.CommandProperty)?.Path.Path);
                Check(commands.GetValueOrDefault(ProjectTreeViewModel.OpenInNewWindowLabel) == nameof(ProjectNodeViewModel.OpenInNewWindowCommand)
                    && commands.GetValueOrDefault(ProjectTreeViewModel.OpenFullPageLabel) == nameof(ProjectNodeViewModel.OpenFullPageCommand),
                    "Menu di un foglio dell'albero: «Apri in una nuova finestra» e «Apri a pagina intera»");

                // «Apri a pagina intera» from the tree: the piles take the whole window, «Esci da pagina intera» on the sheet bar.
                var navigationBar = (FrameworkElement)main.FindName("NavigationBar");
                var projectTree = (FrameworkElement)main.FindName("ProjectTree");
                var center = (FrameworkElement)main.FindName("CenterContent");
                shell.Tree.Find(project.Find(piles.S("id"))!)!.OpenFullPageCommand.Execute(null);
                await Capture(main, output, "11-pagina-intera.png", bindings);
                var client = (FrameworkElement)VisualTreeHelper.GetChild(main, 0);
                Check(shell.IsFullPage && !shell.IsTreeOpen && shell.Center is SheetTabViewModel { Session: var full } && ReferenceEquals(full, pileSheet),
                    "Pagina intera dall'albero: i pali al centro");
                Check(!navigationBar.IsVisible && !projectTree.IsVisible && Math.Abs(center.ActualWidth - client.ActualWidth) < 1,
                    "Pagina intera: navigazione e albero nascosti, il foglio largo quanto la finestra (" + center.ActualWidth + " di " + client.ActualWidth + ")");
                var toggle = Descendants<Button>(main).Single(b => b.Name == "FullPage");
                Check(toggle is { IsVisible: true, IsEnabled: true } && toggle.Content as string == ShellViewModel.ExitFullPageLabel && ReferenceEquals(toggle.Command, shell.ToggleFullPageCommand),
                    "«Esci da pagina intera» nella barra del foglio");

                // Esc on the window: back, with the tree as it was.
                bindings.Step = "esc";
                main.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(main), 0, Key.Escape) { RoutedEvent = Keyboard.KeyDownEvent });
                bool EscDone() => !shell.IsFullPage && shell.IsTreeOpen && navigationBar.IsVisible && projectTree.IsVisible && toggle.Content as string == ShellViewModel.FullPageLabel;
                await IdleUntil(main, EscDone);
                Check(EscDone(), "Esc: navigazione e albero di nuovo, «Pagina intera» nella barra (full=" + shell.IsFullPage + ", tree=" + shell.IsTreeOpen + ", modifiers=" + Keyboard.Modifiers + ", focus=" + Keyboard.FocusedElement?.GetType().Name + ")");

                // The toggle of the sheet bar, pressed twice.
                bindings.Step = "barra";
                await Press(toggle);
                Check(shell.IsFullPage && !navigationBar.IsVisible && !projectTree.IsVisible && toggle.Content as string == ShellViewModel.ExitFullPageLabel, "«Pagina intera» della barra del foglio");
                await Press(toggle);
                Check(!shell.IsFullPage && navigationBar.IsVisible && projectTree.IsVisible, "«Esci da pagina intera» della barra del foglio");

                // «Apri in una nuova finestra» from the tree: the stratigraphy, not open yet, in a new window; then the user goes back to the piles
                // in the main window. One image with the main window on the left and the new window on the right.
                int windowsBeforeTree = detached.Count;
                shell.Tree.Find(project.Find(strata.S("id"))!)!.OpenInNewWindowCommand.Execute(null);
                var strataSheet = workspace.SheetOf(project.Find(strata.S("id"))!)!;
                var strataTab = shell.Windows.TabOf(strataSheet)!;
                Check(detached.Count == windowsBeforeTree + 1 && tracker.Hosts.Count == 1, "Una nuova finestra staccata creata dall'albero (" + detached.Count + " in tutto, " + tracker.Hosts.Count + " aperte)");
                var fromTree = detached[^1];
                var treeHost = tracker.Hosts.Single();
                Check(treeHost.Tabs.Count == 1 && ReferenceEquals(treeHost.Tabs[0], strataTab) && !shell.Windows.Main.Tabs.Contains(strataTab)
                    && ReferenceEquals(tracker.WindowOf(treeHost), fromTree) && fromTree.Activations == 1 && fromTree.IsActive && ReferenceEquals(workspace.Active, strataSheet),
                    "La stratigrafia nella nuova finestra, attivata una volta (" + fromTree.Activations + " richieste)");
                fromTree.Deactivate();
                shell.Windows.Select(shell.Windows.TabOf(pileSheet)!);
                await CaptureSideBySide(main, fromTree.Window, output, "12-staccata-dall-albero.png", bindings);
                Check(ReferenceEquals(workspace.Active, pileSheet) && shell.Center is SheetTabViewModel { Session: var beside } && ReferenceEquals(beside, pileSheet)
                    && fromTree.Activations == 1 && tracker.Hosts.Count == 1, "Principale sui pali accanto alla finestra della stratigrafia");
                fromTree.Window.Close();
                bindings.Step = "chiusura della finestra dall'albero";
                await Idle(main);
                Check(tracker.Hosts.Count == 0 && shell.Windows.Main.Tabs.Contains(strataTab), "Finestra dall'albero chiusa: scheda riportata nella principale");

                // Ctrl+Z and Ctrl+Y of the project on a sheet of the project (the modifiers given: the check has no keyboard), not in a text box.
                bindings.Step = "ctrl-z";
                string strataId = strata.S("id");
                project.Rename(project.Find(strataId)!, "Stratigrafia S1 bis");
                Check(!ProjectHistoryKeys.Handle(shell, shell.Center, Key.Z, ModifierKeys.Control, new TextBox()) && !ProjectHistoryKeys.Handle(shell, shell.Center, Key.Z, ModifierKeys.Control | ModifierKeys.Shift, null)
                    && project.Find(strataId)!.S("nome") == "Stratigrafia S1 bis", "Ctrl+Z in una casella di testo o con Maiusc: non per il progetto");
                Check(shell.Center is SheetTabViewModel && ProjectHistoryKeys.Handle(shell, shell.Center, Key.Z, ModifierKeys.Control, null) && project.Find(strataId)!.S("nome") == "Stratigrafia S1",
                    "Ctrl+Z sul foglio del progetto: rinomina annullata");
                Check(ProjectHistoryKeys.Handle(shell, shell.Center, Key.Y, ModifierKeys.Control, null) && project.Find(strataId)!.S("nome") == "Stratigrafia S1 bis", "Ctrl+Y: rinomina ripristinata");
                await Idle(main);

                // F1 and the guides of the sheets (the Wiki of the services) open the Wiki tab of the shell.
                bindings.Step = "wiki dei servizi";
                string? guide = Anthea.Wiki.WikiCatalog.ForModule("geo_stratigrafia")?.Id ?? Anthea.Wiki.WikiCatalog.ForModule("geo_palo_verticale")?.Id;
                presentation.Wiki.Show(guide);
                await Idle(main);
                Check(shell.Center is PageTabViewModel { Kind: ShellTabKind.Wiki, Content: WikiTabViewModel wikiShown } && wikiShown.Uri == guide && services.WikiRequests == 0,
                    "Wiki dei servizi: la scheda della shell, non la finestra storica");

                // The tree after the checkpoint: the button with the icon of a sheet opens it; F2 renames in place, Enter saves and Esc cancels; Enter
                // on a node opens it (keys sent to the node, the check has no keyboard).
                bindings.Step = "albero";
                var source = PresentationSource.FromVisual(main);
                void SendKey(UIElement target, Key key) => target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.KeyDownEvent });
                var openConcrete = Descendants<Button>(main).Single(b => AutomationProperties.GetName(b) == "Apri scheda Calcestruzzo delle fondazioni");
                Check(openConcrete is { IsVisible: true, Content: Viewbox { Child: ContentControl { Content: FrameworkElement } } } && openConcrete.ToolTip as string == "Apri scheda · Calcestruzzo delle fondazioni",
                    "Foglio dell'albero: il pulsante con l'icona del modulo");
                await Press(openConcrete);
                Check(ReferenceEquals(workspace.Active, concreteSheet) && shell.Center is SheetTabViewModel { Session: var opened } && ReferenceEquals(opened, concreteSheet), "Icona: il foglio nella sua scheda");
                string pierId = pier.S("id");
                TreeViewItem Item(string id) => Descendants<TreeViewItem>(main).Single(i => i.DataContext is ProjectNodeViewModel { Id: var node } && node == id);
                TextBox Box(TreeViewItem item) => Descendants<TextBox>(item).Single(b => b.Name == "Rename" && ReferenceEquals(b.DataContext, item.DataContext));
                SendKey(Item(pierId), Key.F2);
                await Idle(main);
                var renaming = Item(pierId);
                Check(renaming.DataContext is ProjectNodeViewModel { IsRenaming: true } && Box(renaming) is { IsVisible: true, Text: "Pila 1" }, "F2: rinomina in linea con il nome attuale");
                Box(renaming).Text = "Pila 1 nord";
                SendKey(Box(renaming), Key.Enter);
                await Idle(main);
                Check(project.Find(pierId)!.S("nome") == "Pila 1 nord" && Item(pierId).DataContext is ProjectNodeViewModel { IsRenaming: false, Name: "Pila 1 nord" }, "Invio: nome salvato");
                SendKey(Item(pierId), Key.F2);
                await Idle(main);
                Box(Item(pierId)).Text = "da scartare";
                SendKey(Box(Item(pierId)), Key.Escape);
                await Idle(main);
                Check(project.Find(pierId)!.S("nome") == "Pila 1 nord" && Item(pierId).DataContext is ProjectNodeViewModel { IsRenaming: false }, "Esc: rinomina annullata");
                SendKey(Item(strataId), Key.Enter);
                await Idle(main);
                Check(ReferenceEquals(workspace.Active, strataSheet), "Invio su un foglio dell'albero: la sua scheda");

                // ---- Night of 22-23/10 (section 10.1 of the note): what was left of part (c).
                await Overview(shell, presentation, services, main, output, bindings, project, abutment.S("id"), Check);
                await TreeInput(shell, main, output, bindings, project, abutment.S("id"), pierId, pileSheet, Check);
                await Catalogues(shell, main, output, bindings, project, pierId, pileSheet, Check);
                await SmallScreens(shell, services, main, output, bindings, project, abutment.S("id"), pileSheet, strataSheet, Check);
                await ShortReport(shell, services, main, bindings, Check);
            }
            finally
            {
                main.Close();
                shell.Dispose();
            }
            // F5.13 g: the step HistoricHost (R5, image 21: the historic modules in their SheetEditor) is archived with the host in
            // supporto/SUPERATI/ui-storica/shell: no module of the Release is historic.
            // Only the errors of the draft count: those of the module views and of the historic Wiki and viewer go to a separate file.
            var draft = bindings.Errors.Where(BindingErrors.OfDraft).ToArray();
            File.WriteAllLines(Path.Combine(output, "binding-errors.txt"), draft, new UTF8Encoding(false));
            File.WriteAllLines(Path.Combine(output, "binding-errors-altre-viste.txt"), bindings.Errors.Except(draft), new UTF8Encoding(false));
            Check(draft.Length == 0, "Nessun errore di binding della bozza (" + draft.Length + ", vedi binding-errors.txt)");
            Check(services.Failures.Count == 0, "Nessun errore mostrato: " + string.Join(" | ", services.Failures));
        }
        finally
        {
            PresentationTraceSources.DataBindingSource.Listeners.Remove(bindings);
            File.WriteAllLines(Path.Combine(output, "test.txt"), log, new UTF8Encoding(false));
        }
        File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {log.Count} controlli della bozza della shell (F5.13 c)\n", new UTF8Encoding(false));
    }

    /// <summary>
    /// The overview of a section with the content of MainWindow (night of 22-23/10, first item of section 10.1): «Modelli di calcolo», «Controlli»
    /// with «Confronto e avvisi», «Riepilogo modifiche», «Dettaglio avvisi»; the comparison and the summary of the revisions in their dialogs, drawn as
    /// WpfDialogService shows them (a window around the ViewModel, its view from the resources of the application), outside the screen.
    /// </summary>
    private static async Task Overview(ShellViewModel shell, Anthea.Presentation.Services.PresentationServices presentation, DraftServices services, Window main, string output,
        BindingErrors bindings, ProjectSession project, string abutmentId, Action<bool, string> Check)
    {
        bindings.Step = "riepilogo";
        project.NewRevision(project.Find(abutmentId)!, "Prima emissione");
        // A conflict for the comparison: a second concrete of the abutment, in another class than the first one.
        var material = project.AddSheet("mat_calcestruzzo", project.Find(abutmentId)!)!;
        Check(project.Rename(material, "Calcestruzzo dei pali") && project.ChangeData(() => { material["dati"]!["classe"] = "C40/50"; return true; }, material),
            "Secondo calcestruzzo della spalla, in C40/50");
        shell.ShowOverview(project.Find(abutmentId));
        await Idle(main);
        var overview = shell.Overview!;
        var notices = Descendants<Expander>(main).SingleOrDefault(x => x.Name == "NoticeDetails");
        Check(notices is not null && notices.IsVisible == overview.HasNotices && !notices.IsExpanded, "Riepilogo: «Dettaglio avvisi» chiuso, visibile con gli avvisi (" + overview.Notices.Count + ")");
        if (notices is { IsVisible: true }) notices.IsExpanded = true;
        await Capture(main, output, "13-riepilogo.png", bindings);
        var compare = Descendants<Button>(main).Where(b => ReferenceEquals(b.Command, shell.CompareCommand)).ToArray();
        Check(compare.Any(b => b is { Name: "Compare", IsVisible: true, IsEnabled: true } && b.Content as string == "Confronto e avvisi"),
            "Riepilogo: «Confronto e avvisi» nella scheda «Controlli»");
        var history = Descendants<Button>(main).SingleOrDefault(b => ReferenceEquals(b.Command, shell.RevisionSummaryCommand));
        Check(history is { IsVisible: true, IsEnabled: true } && history.Content as string == SectionOverviewViewModel.RevisionSummaryLabel, "Riepilogo: «Riepilogo modifiche» con la revisione archiviata");
        Check(Descendants<TextBlock>(main).Any(t => t.IsVisible && t.Text == overview.ConflictsStatus) && Descendants<TextBlock>(main).Any(t => t.IsVisible && t.Text == overview.NoticesStatus)
            && Descendants<Button>(main).Any(b => ReferenceEquals(b.Command, shell.OpenOverviewModelCommand) && b.Content as string == SectionOverviewViewModel.ImportModelLabel),
            "Riepilogo: stati dei controlli e «+ Importa un modello»");

        // The buttons ask the dialogs of the services (the check answers nothing): the comparison, the summary.
        int asked = services.Dialogs.Count;
        await Press(compare.First(b => b.Name == "Compare"));
        await Press(history!);
        Check(services.Dialogs.Count == asked + 2 && services.Dialogs[asked] is CoherenceViewModel { Title: "Confronto · Spalla A" } && services.Dialogs[asked + 1] is RevisionSummaryViewModel { Title: "Revisioni · Spalla A" },
            "«Confronto e avvisi» e «Riepilogo modifiche»: i loro dialoghi");

        // The two dialogs as the dialog service draws them: the comparison of the abutment (the class of the concrete), the summary.
        bindings.Step = "confronto";
        using (var coherence = new CoherenceViewModel(project, project.Find(abutmentId)!, presentation))
        {
            var window = Offscreen(new Window { Title = coherence.Title, Content = coherence, SizeToContent = SizeToContent.WidthAndHeight });
            window.Show();
            try
            {
                foreach (var expander in Descendants<Expander>(window)) expander.IsExpanded = true;
                await Capture(window, output, "14-confronto.png", bindings);
                Check(Descendants<TextBlock>(window).Any(t => t.Text == CoherenceViewModel.Heading) && Descendants<TextBlock>(window).Any(t => t.Text == CoherenceViewModel.NoticesTitle)
                    && Descendants<Button>(window).Count(b => b.IsVisible && b.Content as string == "Uniforma a questo") == coherence.Conflicts.Sum(c => c.Sheets.Count) && coherence.Conflicts.Count == 1
                    && Descendants<Button>(window).Any(b => ReferenceEquals(b.Command, coherence.LinkCommand)) == coherence.CanLink,
                    "Confronto della spalla: un conflitto con «Uniforma a questo» per foglio, avvisi, collegamento delle stratigrafie (" + coherence.CanLink + ")");
            }
            finally { window.Close(); }
        }
        bindings.Step = "revisioni";
        using (var summary = new RevisionSummaryViewModel(project, project.Find(abutmentId)!, presentation.Messages))
        {
            var window = Offscreen(new Window { Title = summary.Title, Content = summary, SizeToContent = SizeToContent.WidthAndHeight });
            window.Show();
            try
            {
                await Capture(window, output, "15-revisioni.png", bindings);
                Check(Descendants<Button>(window).Count(b => ReferenceEquals(b.Command, summary.ShowCommand) && b.Content as string == summary.ShowLabel) == summary.Entries.Count && summary.Entries.Count == 1,
                    "Riepilogo delle revisioni: «Mostra questa revisione» per la revisione archiviata");
            }
            finally { window.Close(); }
        }
    }

    /// <summary>
    /// The tree of the project (night of 22-23/10, second item of section 10.1): the names by depth, the arrows in the token of the glyphs of the tree,
    /// the drag over a node with its hint and indicator and the drop (through ProjectTreeInput, without a mouse: the check calls what the events of the
    /// drag call), the single click on a section that shows its overview after the double click time, cancelled by another press.
    /// </summary>
    private static async Task TreeInput(ShellViewModel shell, Window main, string output, BindingErrors bindings, ProjectSession project, string abutmentId, string pierId,
        SheetSession pileSheet, Action<bool, string> Check)
    {
        bindings.Step = "albero-mouse";
        var workspace = shell.Workspace;
        workspace.Activate(pileSheet);
        await Idle(main);
        TreeViewItem Item(string id) => Descendants<TreeViewItem>(main).Single(i => i.DataContext is ProjectNodeViewModel { Id: var node } && node == id);
        TextBlock Label(TreeViewItem item) => Descendants<TextBlock>(item).First(t => t.Name == "Label" && ReferenceEquals(t.DataContext, item.DataContext));
        string rootId = project.Projects.First().S("id");
        var tree = (TreeView)main.FindName("Tree");

        // The names by depth (20, 17, then 14 for the sheets in MainWindow) on the typographic tokens; the arrows in the colour of the glyphs of the tree.
        double Size(string key) => (double)main.FindResource(key);
        var sheetLabel = Label(Item(pileSheet.Id));
        Check(Label(Item(rootId)).FontSize == Size("Font.Title") && Label(Item(abutmentId)).FontSize == Size("Font.Heading") && sheetLabel.FontSize == Size("Font.Subheading")
            && sheetLabel.FontWeight == FontWeights.Normal && Label(Item(abutmentId)).FontWeight == FontWeights.SemiBold, "Albero: grandezza dei nomi per profondità");
        var arrow = Descendants<System.Windows.Shapes.Path>(Item(rootId)).First(p => p.Name == "Arrow");
        Check(ReferenceEquals(arrow.Stroke, main.FindResource("Token.InkSecondary")), "Albero: frecce nel colore dei glifi dell'albero");

        // A section dragged over the top of another: «Prima di …» and the line above the row; over the middle of a section a sheet goes inside.
        var pierItem = Item(pierId);
        var abutment = project.Find(abutmentId)!;
        var row = ProjectTreeInput.Row(Item(abutmentId));
        Check(ProjectTreeInput.Over(Item(abutmentId), project.Find(pierId)!, new Point(8, row.ActualHeight * .1)) == DragDropEffects.Move
            && shell.Tree.DropHint == "Prima di " + abutment.S("nome") && ProjectTreeInput.Indicator(tree) == ProjectDropPlace.Before, "Sezione sopra un'altra: «Prima di …» e la linea");
        await Capture(main, output, "16-trascinamento.png", bindings);
        var hint = (TextBlock)main.FindName("DropHint");
        Check(hint.IsVisible && hint.Text == shell.Tree.DropHint, "Trascinamento: il suggerimento al posto dell'istruzione");
        var sheet = project.Find(pileSheet.Id)!;
        Check(ProjectTreeInput.Over(pierItem, sheet, new Point(8, ProjectTreeInput.Row(pierItem).ActualHeight * .5)) == DragDropEffects.Move
            && shell.Tree.DropHint == "Inserisci in: " + project.Find(pierId)!.S("nome") && ProjectTreeInput.Indicator(tree) == ProjectDropPlace.Inside, "Foglio sopra un'altra sezione: «Inserisci in: …»");
        Check(ProjectTreeInput.Over(Item(rootId), project.Find(pierId)!, new Point(8, 2)) == DragDropEffects.None && ProjectTreeInput.Indicator(tree) is null,
            "Sezione sopra il suo progetto: nessun effetto, nessun indicatore");

        // The drop: the abutment after the pier, the indicator and the hint gone.
        Check(ProjectTreeInput.Drop(pierItem, abutment, new Point(8, ProjectTreeInput.Row(pierItem).ActualHeight * .9)) && ProjectTreeInput.Indicator(tree) is null && shell.Tree.DropHint == ""
            && project.Projects.First().Array("strutture").OfType<JsonObject>().Select(s => s.S("id")).SequenceEqual([pierId, abutmentId]), "Rilascio: la spalla dopo la pila");
        await Idle(main);

        // A single click on the name of the pier: its overview after the double click time; another press in the window cancels the click.
        var timer = ProjectTreeInput.Timer(main);
        void Up(UIElement target) => target.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
        Up(Label(Item(pierId)));
        Check(timer.IsPending && ReferenceEquals(workspace.Active, pileSheet), "Clic sul nome della pila: in attesa del tempo del doppio clic");
        main.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseDownEvent });
        Check(!timer.IsPending, "Un'altra pressione nella finestra annulla il clic (il doppio clic rinomina)");
        Up(Label(Item(pierId)));
        await Task.Delay(timer.Delay + TimeSpan.FromMilliseconds(300));
        await Idle(main);
        Check(!timer.IsPending && workspace.Active is null && shell.Center is SectionOverviewViewModel { Section: var shown } && shown?.S("id") == pierId,
            "Clic singolo sulla pila: il suo riepilogo dopo " + timer.Delay.TotalMilliseconds + " ms");
        Up(Label(Item(pileSheet.Id)));
        Check(!timer.IsPending, "Clic sul nome di un foglio: nessun riepilogo");
    }

    /// <summary>
    /// The catalogues (night of 22-23/10, third item of section 10.1): «Riprendi» on the card of the module of the last sheet in «Moduli singoli»; the cards
    /// of «Schede da aggiungere» with icon and grip, dragged on a section of the tree (a copy, «Inserisci in: …») and added with the double click.
    /// </summary>
    private static async Task Catalogues(ShellViewModel shell, Window main, string output, BindingErrors bindings, ProjectSession project, string pierId,
        SheetSession pileSheet, Action<bool, string> Check)
    {
        bindings.Step = "cataloghi";
        var workspace = shell.Workspace;
        workspace.Activate(pileSheet);
        shell.Navigation.Select(ShellPage.Modules);
        await Capture(main, output, "17-moduli-riprendi.png", bindings);
        var buttons = Descendants<Button>(main).Where(b => b.IsVisible && b.Name == "Open").ToArray();
        Check(buttons.Count(b => b.Content as string == "Riprendi") == 1 && buttons.Single(b => b.Content as string == "Riprendi").DataContext is ModuleCard { Id: "geo_palo_verticale" }
            && buttons.Count(b => b.Content as string == "Apri") == buttons.Length - 1 - buttons.Count(b => b.Content as string == "Dettagli"),
            "Moduli singoli: «Riprendi» sul palo verticale, l'ultimo foglio");

        // «Schede da aggiungere» on the overview of the pier: a card dragged over the pier in the tree, then dropped there.
        shell.ShowOverview(project.Find(pierId));
        await Idle(main);
        var cards = Descendants<Border>(main).Where(b => b.Name == "Card" && b.DataContext is ProjectCatalogCard && b.IsVisible).ToArray();
        var card = cards.First(b => ((ProjectCatalogCard)b.DataContext).Module == "mat_calcestruzzo");
        Check(cards.Length > 0 && cards.All(b => Descendants<Viewbox>(b).Any(v => v.Child is ContentControl { Content: FrameworkElement }) && ProjectTreeInput.GetDragRow(b))
            && ProjectTreeInput.DragData(card) is { Format: ProjectTreeInput.ModuleFormat, Payload: "mat_calcestruzzo" }
            && card.InputBindings.OfType<MouseBinding>().Any(m => ReferenceEquals(m.Command, shell.AddCatalogSheetCommand) && m.CommandParameter == card.DataContext),
            "Schede da aggiungere: icona, trascinamento del modulo, doppio clic (" + cards.Length + " schede)");
        var pierItem = Descendants<TreeViewItem>(main).Single(i => i.DataContext is ProjectNodeViewModel { Id: var id } && id == pierId);
        var tree = (TreeView)main.FindName("Tree");
        Check(ProjectTreeInput.Over(pierItem, "mat_calcestruzzo", new Point(8, ProjectTreeInput.Row(pierItem).ActualHeight * .5)) == DragDropEffects.Copy
            && ProjectTreeInput.Indicator(tree) == Anthea.Presentation.Shell.Projects.ProjectDropPlace.Inside && shell.Tree.DropHint == "Inserisci in: " + project.Find(pierId)!.S("nome"),
            "Scheda del catalogo sopra la pila: copia, «Inserisci in: …»");
        await Capture(main, output, "18-schede-da-aggiungere.png", bindings);
        int sheets = project.Find(pierId)!.Array("fogli").Count;
        Check(ProjectTreeInput.Drop(pierItem, "mat_calcestruzzo", new Point(8, ProjectTreeInput.Row(pierItem).ActualHeight * .5)) && project.Find(pierId)!.Array("fogli").Count == sheets + 1
            && shell.Center is SectionOverviewViewModel { Section: var overview } && overview?.S("id") == pierId, "Rilascio della scheda sulla pila: un foglio nuovo, il riepilogo della pila");
        await Idle(main);
    }

    /// <summary>
    /// Small screens and the notice of a reloaded tab (night of 22-23/10, fourth item of section 10.1). SH9: the second concrete of the abutment changes its
    /// class and is confirmed with «Aggiorna tutti i fogli collegati»; the first one, reloaded, says from which sheet and which data. Small screens: in a
    /// window of 900 × 620 a sheet whose view does not follow the window (the stratigraphy) keeps 1120 × 600 and scrolls, the piles fill the tab, the home
    /// page keeps 870 × 680 and scrolls (MainWindow.LoadSheetEditor and DisplayAdaptation.Viewport).
    /// </summary>
    private static async Task SmallScreens(ShellViewModel shell, DraftServices services, Window main, string output, BindingErrors bindings, ProjectSession project, string abutmentId,
        SheetSession pileSheet, SheetSession strataSheet, Action<bool, string> Check)
    {
        bindings.Step = "avviso-ricaricata";
        var workspace = shell.Workspace;
        var sheets = project.Find(abutmentId)!.Array("fogli").OfType<JsonObject>().Where(s => s.S("modulo_id") == "mat_calcestruzzo").ToArray();
        var first = workspace.OpenSheet(sheets[0]);
        var second = workspace.OpenSheet(sheets[1]);
        ((Anthea.Presentation.Modules.Materials.ConcreteMaterialSheetViewModel)second.ViewModel).SelectedClass = "C35/45";
        services.UpdateLinkedSheets = true;
        try { workspace.Activate(first); }
        finally { services.UpdateLinkedSheets = false; }
        await Capture(main, output, "19-avviso-ricaricata.png", bindings);
        var notice = (FrameworkElement)main.FindName("ReloadNotice");
        Check(first.ReloadNotice is { } text && text.StartsWith("Aggiornato da «" + sheets[1].S("nome") + "»", StringComparison.Ordinal) && notice.IsVisible
            && Descendants<TextBlock>(notice).Any(t => t.Text == text) && ReferenceEquals(workspace.Active, first), "Scheda ricaricata: «" + first.ReloadNotice + "»");
        workspace.Activate(second);
        await Idle(main);
        Check(first.ReloadNotice is null && !notice.IsVisible, "L'avviso sparisce quando la scheda ricaricata perde il fuoco (conferma)");

        bindings.Step = "piccolo-schermo";
        double width = main.Width, height = main.Height;
        main.Width = 900; main.Height = 620;
        try
        {
            // The whole shell keeps 1120 × 680 and scrolls; the tree closed by «▸» leaves the centre to the sheet.
            workspace.Activate(strataSheet);
            await Idle(main);
            var shellViewport = (MinimumSizeViewport)main.FindName("ShellViewport");
            Check(shellViewport is { ComputedHorizontalScrollBarVisibility: Visibility.Visible, ComputedVerticalScrollBarVisibility: Visibility.Visible }
                && ((FrameworkElement)shellViewport.Content).Width == Anthea.Presentation.Shell.SheetLayout.WindowMinimumWidth, "Finestra piccola: la shell tiene 1120 × 680 e scorre");
            shell.IsTreeOpen = false;
            await Idle(main);
            var viewport = Descendants<MinimumSizeViewport>((FrameworkElement)main.FindName("CenterContent")).SingleOrDefault();
            Check(viewport is { ComputedHorizontalScrollBarVisibility: Visibility.Visible, Content: FrameworkElement { Width: Anthea.Presentation.Shell.SheetLayout.SheetMinimumWidth } },
                "Finestra piccola: la stratigrafia tiene 1120 × 600 e scorre");
            await Capture(main, output, "20-piccolo-schermo.png", bindings);
            workspace.Activate(pileSheet);
            await Idle(main);
            Check(!Descendants<MinimumSizeViewport>((FrameworkElement)main.FindName("CenterContent")).Any(), "Finestra piccola: i pali seguono la finestra");
            shell.Navigation.Select(ShellPage.Home);
            await Idle(main);
            var home = Descendants<MinimumSizeViewport>((FrameworkElement)main.FindName("CenterContent")).SingleOrDefault();
            Check(home is { Content: FrameworkElement { ActualWidth: >= Anthea.Presentation.Shell.SheetLayout.PageMinimumWidth } } && home.ComputedVerticalScrollBarVisibility == Visibility.Visible,
                "Finestra piccola: la Home tiene 870 × 680 e scorre");
        }
        finally
        {
            main.Width = width; main.Height = height;
            shell.IsTreeOpen = true;
            await Idle(main);
        }
    }

    /// <summary>
    /// The short report of the RC section in the shell (night of 22-23/10, sixth item of section 10.1), with the MVVM view of the draft: «Report short» in
    /// the sheet bar and «Report short CLS…» in the menu «File» run the command of the shell, which asks the sheet (ISheetExports through
    /// SheetFileExports.ShortReport and the report of ConcreteSheetViewModel, F5.10 S9): after the calculation, the bytes and the name of the file, or the
    /// refusal of the sheet with its own text.
    /// </summary>
    private static async Task ShortReport(ShellViewModel shell, DraftServices services, Window main, BindingErrors bindings, Action<bool, string> Check)
    {
        bindings.Step = "report-short-mvvm";
        var sheet = shell.Workspace.NewCalculation("str_palo");
        await sheet.ViewModel.CalculateAsync();
        await Idle(main);
        Check(sheet.ViewModel is Anthea.Presentation.Modules.ConcreteSection.ConcreteSheetViewModel && shell.Commands.ShortReportVisible
            && Descendants<Button>(main).Any(b => b.IsVisible && ReferenceEquals(b.Command, shell.Commands.ShortReportCommand) && b.Content as string == ShellCommandsViewModel.ShortTitle)
            && shell.Commands.File.Any(i => i.Title == "Report short CLS…" && ReferenceEquals(i.Command, shell.Commands.ShortReportCommand)), "Sezione c.a. MVVM: «Report short» nella barra e nel menu File");
        string? refusal = null;
        try { _ = SheetFileExports.ShortReport(sheet.ViewModel, sheet.Heading); } catch (InvalidOperationException error) { refusal = error.Message; }
        int failures = services.Failures.Count, saves = services.Saves.Count;
        await shell.Commands.ShortReportCommand.ExecuteAsync(null);
        Check(refusal is null ? services.Saves.Count == saves + 1 && services.Saves[^1] == "Relazione_short.docx" && services.Failures.Count == failures
            : services.Failures.Count == failures + 1 && services.Failures[^1] == ShellCommandsViewModel.ShortTitle + ": " + refusal,
            "Report short della sezione c.a. MVVM dal comando della shell: " + (refusal is null ? "costruito, richiesto il file" : "rifiutato con il testo del foglio («" + refusal + "»)"));
        if (refusal is not null) services.Failures.RemoveAt(services.Failures.Count - 1);
        shell.Workspace.CloseSheet(sheet);
        await Idle(main);
    }

    /// <summary>A window outside the screen, never activated (the checks of the night share the desktop).</summary>
    internal static T Offscreen<T>(T window) where T : Window
    {
        window.Left = -32000; window.Top = -32000;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.ShowActivated = false; window.ShowInTaskbar = false;
        return window;
    }

    private static async Task Idle(Window window)
    {
        window.UpdateLayout();
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
    }

    /// <summary>
    /// Idle until <paramref name="condition"/> holds, within 5 s (F5.13 g): with the machine loaded one idle round may come before the layout that follows
    /// a key raised by hand (a failure seen once on 67eafba5, PASS alone). The check after it stays the same.
    /// </summary>
    private static async Task IdleUntil(Window window, Func<bool> condition)
    {
        await Idle(window);
        for (int i = 0; i < 100 && !condition(); i++) { await Task.Delay(50); await Idle(window); }
    }

    /// <summary>
    /// The client area of the window as rendered, at 96 DPI (logical size). The visual tree of a window draws only its client area: a bitmap as
    /// large as the whole window (frame and title included) left an empty strip on the right and at the bottom.
    /// </summary>
    private static async Task Capture(Window window, string output, string name, BindingErrors bindings)
    {
        bindings.Step = Path.GetFileNameWithoutExtension(name);
        await Idle(window);
        Save(Render(window), output, name);
    }

    /// <summary>
    /// Two windows in one image (12, after the checkpoint of 22/10): <paramref name="left"/> (the main window) and <paramref name="right"/> (a detached
    /// one) side by side on a grey ground, as they would sit on two monitors; each one is its client area at 96 DPI, as in <see cref="Capture"/>.
    /// </summary>
    private static async Task CaptureSideBySide(Window left, Window right, string output, string name, BindingErrors bindings)
    {
        const int Gap = 24;
        bindings.Step = Path.GetFileNameWithoutExtension(name);
        await Idle(left);
        await Idle(right);
        var first = Render(left);
        var second = Render(right);
        int width = first.PixelWidth + Gap + second.PixelWidth, height = Math.Max(first.PixelHeight, second.PixelHeight);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(Brushes.Gray, null, new Rect(0, 0, width, height));
            context.DrawImage(first, new Rect(0, 0, first.PixelWidth, first.PixelHeight));
            context.DrawImage(second, new Rect(first.PixelWidth + Gap, 0, second.PixelWidth, second.PixelHeight));
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        Save(bitmap, output, name);
    }

    private static RenderTargetBitmap Render(Window window)
    {
        var client = (FrameworkElement)VisualTreeHelper.GetChild(window, 0);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(client.ActualWidth), (int)Math.Ceiling(client.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        return bitmap;
    }

    private static void Save(BitmapSource bitmap, string output, string name)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(output, name));
        encoder.Save(file);
    }

    /// <summary>A click of <paramref name="button"/> through its automation peer (no mouse, no focus), then the dispatcher idle.</summary>
    private static async Task Press(Button button)
    {
        ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
        await Idle(Window.GetWindow(button)!);
    }

    /// <summary>The elements of type <typeparamref name="T"/> in the visual tree under <paramref name="root"/>.</summary>
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var inner in Descendants<T>(child)) yield return inner;
        }
    }

    /// <summary>
    /// A detached window as the tracker sees it on the desktop, whose WPF window is never activated (the checks of the night share the desktop):
    /// shown without the focus (ShowActivated false, as Offscreen), it counts the requests to activate it and, like a real window, after
    /// <see cref="Activate"/> it is active and has reported <see cref="Activated"/> once; a request to an active window changes nothing, as
    /// Window.Activate on the desktop. It stays active until it closes (nothing else takes the focus of the system in this check).
    /// </summary>
    internal sealed class QuietWindow : IShellWindow
    {
        public QuietWindow(DetachedShellWindow window)
        {
            Window = window;
            Window.Closed += (_, _) => IsActive = false;
        }

        internal DetachedShellWindow Window { get; }
        internal int Activations { get; private set; }
        public bool IsActive { get; private set; }
        public void Show() => Window.Show();

        public void Activate()
        {
            Activations++;
            if (IsActive) return;
            IsActive = true;
            Activated?.Invoke(this, EventArgs.Empty);
        }

        public void Close() => Window.Close();

        /// <summary>The focus of the system goes to another window (the user clicks the main window).</summary>
        internal void Deactivate() => IsActive = false;

        public event EventHandler? Activated;
        public event EventHandler? Closed { add => Window.Closed += value; remove => Window.Closed -= value; }
    }

    /// <summary>The errors of the bindings of WPF (PresentationTraceSources at level Error).</summary>
    private sealed class BindingErrors : TraceListener
    {
        /// <summary>
        /// The types and the paths of the draft: an error that names one of them comes from the templates of the shell. «TreeViewItem»: the only
        /// tree on screen in this check is the tree of the project of the draft (the views of the modules opened here have none).
        /// </summary>
        private static readonly string[] DraftNames = ["TreeViewItem", "ShellViewModel", "NavigationViewModel", "NavigationItemViewModel", "ModuleCatalogViewModel", "HomeCard", "ModuleArea",
            "ModuleGroup", "ModuleCard", "SectionOverviewViewModel", "CommonDataRow", "ProjectCatalogViewModel", "ProjectCatalogGroup", "ProjectCatalogCard",
            "ModelsPageViewModel", "ModelContainerCard", "ModelLink", "ModelTabViewModel", "ModelReferenceViewModel", "RevisionBarViewModel", "RevisionTab",
            "SharedStatusViewModel", "ProjectTreeViewModel", "ProjectNodeViewModel", "SheetBarViewModel", "ShellWindowsViewModel", "TabHostViewModel",
            "SheetTabViewModel", "PageTabViewModel", "WikiTabViewModel", "ShellCommandsViewModel", "ShellMenuItem", "AppearanceMenuViewModel",
            "AppearanceItemViewModel", "ShellWindow", "SheetBarView", "ModelTabView", "WikiTabView", "SelectCommand", "CloseCommand", "MoveToNewWindowCommand",
            "ReattachCommand", "OpenCardCommand", "SelectDisciplineCommand", "OpenModelCommand", "SwitchCommand", "ToggleTreeCommand", "Catalogue", "Commands.",
            "OverviewLink", "LinkItems", "HasLinks", "OpenLinkCommand", "DuplicateSectionCommand", "ToggleFullPageCommand", "FullPageText", "FullPageToolTipText", "IsFullPage", "OpenInNewWindowCommand", "OpenFullPageCommand", "IsSheet",
            // Night of 22-23/10: the overview, the comparison and the summary of the revisions (ShellDialogs.xaml).
            "OverviewModel", "ModelButtons", "ConflictsStatus", "NoticesStatus", "NoticeLines", "CompareCommand", "RevisionSummaryCommand", "OpenOverviewModelCommand",
            "CoherenceViewModel", "ConflictViewModel", "ConflictSheetViewModel", "MissingLayerViewModel", "MissingLayerSourceViewModel", "CoherenceNotice", "SoilLinkSheet",
            "SoilLinkDialogViewModel", "RevisionSummaryViewModel", "RevisionSummaryEntry", "NewRevisionDialogViewModel", "ReportSectionsDialogViewModel", "ReportSectionChoice"];

        private readonly StringBuilder line = new();
        internal List<string> Errors { get; } = [];

        /// <summary>The step of the check (the image being drawn), written before each error to say where it comes from.</summary>
        internal string Step { get; set; } = "apertura";

        internal static bool OfDraft(string error) => DraftNames.Any(name => error.Contains(name, StringComparison.Ordinal));
        public override void Write(string? message) => line.Append(message);
        public override void WriteLine(string? message) { line.Append(message); Errors.Add("[" + Step + "] " + line); line.Clear(); }
    }

    /// <summary>Services that never ask: no file, no dialog, «Non salvare» at closing, the messages recorded.</summary>
    internal sealed class DraftServices : IMessageService, IConfirmationService, IFileDialogService, IDialogService, IClipboardService, IExternalLinkService, IWikiNavigator
    {
        internal List<string> Failures { get; } = [];
        internal PresentationServices Create() => new(this, this, this, this, this, this, this);
        public void ReportFailure(string title, Exception error) => Failures.Add(title + ": " + error.Message);
        public void Inform(string title, string message) { }
        public void Warn(string title, string message) => Failures.Add(title + ": " + message);

        /// <summary>The names proposed by the save dialogs, answered with nothing (night of 22-23/10: the short report).</summary>
        internal List<string> Saves { get; } = [];
        internal string? NextSavePath { get; set; }
        internal string? NextOpenPath { get; set; }
        public bool ConfirmSharedUpdate(SharedUpdatePrompt prompt) => UpdateLinkedSheets;

        /// <summary>The answer to «Aggiorna tutti i fogli collegati»: «Solo questo foglio» unless a step asks otherwise (SH9, night of 22-23/10).</summary>
        internal bool UpdateLinkedSheets { get; set; }
        public bool ConfirmProjectMove(string destination, IReadOnlyList<string> changes) => false;
        public bool ConfirmClose(Func<bool> saveOrDiscard) => saveOrDiscard();
        public SaveChangesAnswer AskSaveChanges() => SaveChangesAnswer.Discard;
        public bool ConfirmDelete(string title, string question) => false;
        public string? OpenFile(OpenFileRequest request) { var path = NextOpenPath; NextOpenPath = null; return path; }
        public string? SaveFile(SaveFileRequest request) { Saves.Add(request.FileName); var path = NextSavePath; NextSavePath = null; return path; }
        public string? ChooseFolder(string? title = null) => null;
        internal Func<object, Task>? HandleDialog { get; set; }
        public async Task<TResult?> ShowDialogAsync<TResult>(IDialogViewModel<TResult> dialog)
        {
            Dialogs.Add(dialog);
            if (HandleDialog is null) return default;
            await HandleDialog(dialog);
            return dialog.Result;
        }

        /// <summary>The dialogs asked, answered with nothing (night of 22-23/10: the comparison and the summary of the revisions of the overview).</summary>
        internal List<object> Dialogs { get; } = [];
        public void SetText(string text) { }
        public string GetText() => "";
        public void Open(Uri target) { }
        public void Show(string? uri = null) => WikiRequests++;

        /// <summary>Requests to the historic Wiki window (the fallback of ShellWikiNavigator): none once the shell exists.</summary>
        internal int WikiRequests { get; private set; }
    }
}
