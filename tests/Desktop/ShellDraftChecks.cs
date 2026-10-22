using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Anthea.Application.Examples;
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
/// <item>ShellWindow on ShellViewModel with the modules of the product (DesktopComposition.CreateModules) and a sample project (a bridge with an
/// abutment and a pier, four sheets), PNG of the home, of «Moduli singoli», of «Progetti», of four tabs open (three sheets of the project and a
/// single file), of «Modelli», of the Wiki tab, of a detached window with the main window beside it, of the tabs in «Scuro»;</item>
/// <item>after the user's checkpoint of 22/10, from the tree of the project: a sheet at full page (11, then Esc and the toggle of the sheet bar)
/// and a sheet opened in a new window, with the main window beside it in one image (12);</item>
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
            // The examples of the Wiki: the copy of the shell and the one of MainWindow give the same data.
            foreach (var example in Anthea.Wiki.WikiCatalog.Articles.Select(a => a.Example).OfType<string>().Distinct())
            {
                string module = WikiExamples.Definition(example).S("moduleId");
                Check(JsonNode.DeepEquals(WikiExamples.Create(module, example), MainWindow.WikiExamples.Create(module, example)), "Esempio della Wiki «" + example + "»: stessi dati della finestra storica");
            }

            // The shell with the modules of the product and services that never ask the user.
            // Since the checkpoint the shell comes from its composition root (ShellComposition), with these services, the Wiki of the services on the
            // tab of the shell, and detached windows wrapped so that they are never activated.
            var services = new DraftServices();
            var presentation = ShellComposition.WithShellWiki(services.Create());
            var modules = DesktopComposition.CreateModules();
            var detached = new List<QuietWindow>();
            var parts = ShellComposition.Create(presentation, modules, new CalculationLimiter(CalculationLimiter.DefaultMaximum), ShellComposition.ExamplesFolder, window =>
            {
                var quiet = new QuietWindow(Offscreen(window));
                detached.Add(quiet);
                return quiet;
            });
            var shell = parts.Shell;
            var tracker = parts.Tracker;
            var main = Offscreen(parts.Window);
            main.Show();
            try
            {
                var workspace = shell.Workspace;
                await Capture(main, output, "01-home.png", bindings);
                Check(shell.Center is NavigationViewModel && main.ActualWidth > 1000 && main.Title == "ANTHEA", "Home al centro, finestra principale");

                shell.Navigation.Select(ShellPage.Modules);
                await Capture(main, output, "02-moduli-singoli.png", bindings);
                Check(shell.Center is ModuleCatalogViewModel, "Moduli singoli al centro");

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
                await Idle(main);
                Check(!shell.IsFullPage && shell.IsTreeOpen && navigationBar.IsVisible && projectTree.IsVisible && toggle.Content as string == ShellViewModel.FullPageLabel,
                    "Esc: navigazione e albero di nuovo, «Pagina intera» nella barra");

                // The toggle of the sheet bar, pressed twice.
                bindings.Step = "barra";
                await Press(toggle);
                Check(shell.IsFullPage && !navigationBar.IsVisible && !projectTree.IsVisible && toggle.Content as string == ShellViewModel.ExitFullPageLabel, "«Pagina intera» della barra del foglio");
                await Press(toggle);
                Check(!shell.IsFullPage && navigationBar.IsVisible && projectTree.IsVisible, "«Esci da pagina intera» della barra del foglio");

                // «Apri in una nuova finestra» from the tree: the stratigraphy, not open yet, in a new window; then the user goes back to the piles
                // in the main window. One image with the main window on the left and the new window on the right.
                shell.Tree.Find(project.Find(strata.S("id"))!)!.OpenInNewWindowCommand.Execute(null);
                var strataSheet = workspace.SheetOf(project.Find(strata.S("id"))!)!;
                var strataTab = shell.Windows.TabOf(strataSheet)!;
                Check(detached.Count == 2 && tracker.Hosts.Count == 1, "Una nuova finestra staccata creata dall'albero (" + detached.Count + " in tutto, " + tracker.Hosts.Count + " aperte)");
                var fromTree = detached[1];
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
            }
            finally
            {
                main.Close();
                shell.Dispose();
            }
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

    /// <summary>A window outside the screen, never activated (the checks of the night share the desktop).</summary>
    private static T Offscreen<T>(T window) where T : Window
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
    private sealed class QuietWindow : IShellWindow
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
            "OverviewLink", "LinkItems", "HasLinks", "OpenLinkCommand", "DuplicateSectionCommand", "ToggleFullPageCommand", "FullPageText", "FullPageToolTipText", "IsFullPage", "OpenInNewWindowCommand", "OpenFullPageCommand", "IsSheet"];

        private readonly StringBuilder line = new();
        internal List<string> Errors { get; } = [];

        /// <summary>The step of the check (the image being drawn), written before each error to say where it comes from.</summary>
        internal string Step { get; set; } = "apertura";

        internal static bool OfDraft(string error) => DraftNames.Any(name => error.Contains(name, StringComparison.Ordinal));
        public override void Write(string? message) => line.Append(message);
        public override void WriteLine(string? message) { line.Append(message); Errors.Add("[" + Step + "] " + line); line.Clear(); }
    }

    /// <summary>Services that never ask: no file, no dialog, «Non salvare» at closing, the messages recorded.</summary>
    private sealed class DraftServices : IMessageService, IConfirmationService, IFileDialogService, IDialogService, IClipboardService, IExternalLinkService, IWikiNavigator
    {
        internal List<string> Failures { get; } = [];
        internal PresentationServices Create() => new(this, this, this, this, this, this, this);
        public void ReportFailure(string title, Exception error) => Failures.Add(title + ": " + error.Message);
        public void Inform(string title, string message) { }
        public void Warn(string title, string message) => Failures.Add(title + ": " + message);
        public bool ConfirmSharedUpdate(SharedUpdatePrompt prompt) => false;
        public bool ConfirmProjectMove(string destination, IReadOnlyList<string> changes) => false;
        public bool ConfirmClose(Func<bool> saveOrDiscard) => saveOrDiscard();
        public SaveChangesAnswer AskSaveChanges() => SaveChangesAnswer.Discard;
        public bool ConfirmDelete(string title, string question) => false;
        public string? OpenFile(OpenFileRequest request) => null;
        public string? SaveFile(SaveFileRequest request) => null;
        public string? ChooseFolder(string? title = null) => null;
        public Task<TResult?> ShowDialogAsync<TResult>(IDialogViewModel<TResult> dialog) => Task.FromResult<TResult?>(default);
        public void SetText(string text) { }
        public string GetText() => "";
        public void Open(Uri target) { }
        public void Show(string? uri = null) => WikiRequests++;

        /// <summary>Requests to the historic Wiki window (the fallback of ShellWikiNavigator): none once the shell exists.</summary>
        internal int WikiRequests { get; private set; }
    }
}
