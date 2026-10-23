using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Navigation;
using Anthea.Presentation.Shell.Tabs;
using X.Core;
using X.Desktop.Views.Shell;

namespace X.Desktop;

internal static partial class ShellSmoke
{
    /// <summary>
    /// ui/shell-smoke-display: DisplaySmokeChecks (MainWindow.SmokeDisplay) on the new shell, at the four sizes of the window of the historic suite: the home page,
    /// «Moduli singoli», the vertical pile and micropile as single files in their tab, calculated; the module inside the visible area, no outer horizontal scroll,
    /// the last card reachable, the cards extended and back; at 1600 × 990 the editing of the stratigraphy, the outcomes, the presentation, the report and the
    /// return through the home page («Riprendi · nome») and «Moduli singoli» («Riprendi») with the same sheet. Below the minimum of the shell (SH40, 1120 × 680)
    /// the shell scrolls in the window: the module is checked inside the scrolling area, which shows its scroll bars, instead of inside the window.
    /// </summary>
    private static async Task Display(ShellSmokeBench b)
    {
        string directory = b.Output;
        var shell = b.Shell; var workspace = b.Workspace; var window = b.Window;
        var cases = SmokeTestData.LoadCases();
        var dpi = VisualTreeHelper.GetDpi(window);
        var log = new List<string> { $"Scala effettiva Windows: {dpi.DpiScaleX * 100:0}% × {dpi.DpiScaleY * 100:0}%" };
        var viewport = (MinimumSizeViewport)window.FindName("ShellViewport");
        b.Services.Confirmations.SaveChanges = () => SaveChangesAnswer.Discard;
        try
        {
            foreach (var (width, height) in new[] { (1600, 990), (1280, 720), (960, 640), (760, 480) })
            {
                window.Width = width; window.Height = height;
                bool scrolls = false;
                foreach (string page in new[] { "home", "moduli", "geo_palo_verticale", "geo_micropalo_verticale" })
                {
                    if (page == "home") shell.Navigation.Select(ShellPage.Home);
                    else if (page == "moduli") shell.Navigation.Select(ShellPage.Modules);
                    else
                    {
                        foreach (var open in workspace.Sheets.ToArray()) workspace.CloseSheet(open);
                        string caseName = page == "geo_palo_verticale" ? "palo_storico_0" : "micropalo_IRS_45_Feld";
                        var document = Archivio.Documento(page);
                        document["dati"] = cases.First(c => c.S("nome") == caseName)!["input"]!.DeepClone();
                        workspace.OpenDocument(document, null, false);
                        await b.Idle();
                        var opened = b.Sheet;
                        if (opened.Historic is { } historicEditor) await historicEditor.CalculateAsync(); else await opened.Hosted.CalculateAsync();
                        if (opened.ViewModel.Result is null || opened.ViewModel.Result.S("errore") != "") throw new InvalidOperationException("Calcolo fallito: " + page);
                    }
                    await b.Idle();
                    var root = (FrameworkElement)window.Content;
                    b.Check(double.IsFinite(root.ActualWidth) && root.ActualWidth > 0 && root.ActualHeight > 0, $"Area di lavoro valida: {page} {width}");
                    // SH40: the shell keeps 1120 × 680 under the menu and scrolls where the window is smaller, in either direction (the menu takes some height:
                    // at 1280 × 720 the shell scrolls a little in height).
                    var content = (FrameworkElement)viewport.Content;
                    bool scrollsH = viewport.ComputedHorizontalScrollBarVisibility == Visibility.Visible, scrollsV = viewport.ComputedVerticalScrollBarVisibility == Visibility.Visible;
                    scrolls = scrollsH || scrollsV;
                    b.Check(content.ActualWidth >= SheetLayout.WindowMinimumWidth - .5 && content.ActualHeight >= SheetLayout.WindowMinimumHeight - .5
                        && (scrollsH || viewport.ViewportWidth >= content.ActualWidth - 1) && (scrollsV || viewport.ViewportHeight >= content.ActualHeight - 1)
                        && (width >= 1600) == !scrolls,
                        $"SH40: la shell tiene {SheetLayout.WindowMinimumWidth} × {SheetLayout.WindowMinimumHeight} e scorre dove la finestra è più piccola ({page} {width} × {height}: {(scrollsH ? "orizzontale " : "")}{(scrollsV ? "verticale" : "")})");
                    if (page.StartsWith("geo_", StringComparison.Ordinal))
                    {
                        var sheet = b.Sheet;
                        var checks = new DisplaySheet(b, sheet);
                        checks.VerifyDisplayLayout();
                        // Inside the window (MainWindow); below the minimum of the shell inside its scrolling area (SH40).
                        var area = scrolls ? (FrameworkElement)viewport.Content : root;
                        var view = sheet.View;
                        var position = view.TranslatePoint(new Point(), area);
                        b.Check(position.X >= 0 && position.Y >= 0 && position.X + view.ActualWidth <= area.ActualWidth + 1 && position.Y + view.ActualHeight <= area.ActualHeight + 1,
                            $"Il modulo resta nell'area visibile{(scrolls ? " della shell che scorre" : "")}: {page} {width} (in {position.X:0.#}, {position.Y:0.#}, {view.ActualWidth:0.#} × {view.ActualHeight:0.#} in {area.ActualWidth:0.#} × {area.ActualHeight:0.#})");
                        await checks.VerifyDisplayScrolling(directory, $"{page}_{width}");
                        if (width == 1600)
                        {
                            string checksDirectory = Path.Combine(directory, page); Directory.CreateDirectory(checksDirectory);
                            await checks.VerifyStratigraphyEditing(); checks.VerifyPileOutcomes(); await checks.VerifyPresentation(checksDirectory);
                            checks.VerifyReport(checksDirectory);
                            var retained = sheet.ViewModel;
                            await checks.VerifyNavigationThrough(() => { workspace.CommitActive(); shell.Navigation.Select(ShellPage.Home); },
                                () => Resume(b, ShellPage.Home));
                            await checks.VerifyNavigationThrough(() => { workspace.CommitActive(); shell.Navigation.Select(ShellPage.Home); shell.Navigation.Select(ShellPage.Modules); },
                                () => Resume(b, ShellPage.Modules));
                            b.Check(ReferenceEquals(workspace.Active?.ViewModel, retained) && shell.Center is SheetTabViewModel { Session: var shown } && ReferenceEquals(shown, sheet.Session),
                                "«Riprendi» non ricrea il modulo");
                        }
                    }
                    File.WriteAllBytes(Path.Combine(directory, $"{page}_{width}.png"), Ui.Snapshot(window));
                }
                log.Add($"{width} × {height} unità logiche: modulo contenuto nella finestra{(scrolls ? " (shell che scorre, SH40)" : "")}, nessuno scorrimento orizzontale esterno del modulo, ultimo pannello raggiungibile, espansione e ripristino OK");
            }
        }
        finally { b.Services.Confirmations.SaveChanges = null; window.Width = 1600; window.Height = 990; }
        File.WriteAllLines(Path.Combine(directory, "display.txt"), log);
        b.Check(true, "Prova del display completata alle quattro dimensioni");
    }

    /// <summary>
    /// «Riprendi» of a page, as the user presses it: «Riprendi · nome» on the home page (the command of the navigation), «Riprendi» on the card of the module of
    /// the last sheet in «Moduli singoli».
    /// </summary>
    private static void Resume(ShellSmokeBench b, ShellPage page)
    {
        b.Window.UpdateLayout();
        var button = page == ShellPage.Home
            ? ShellSmokeBench.Descendants<Button>(b.Window).First(x => x.IsVisible && x.Content is string text && text.StartsWith("Riprendi ·", StringComparison.Ordinal))
            : ShellSmokeBench.Descendants<Button>(b.Window).First(x => x.IsVisible && Equals(x.Content, "Riprendi"));
        button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, button));
        if (button.Command is { } command && command.CanExecute(button.CommandParameter)) command.Execute(button.CommandParameter);
    }

    /// <summary>The checks of DisplaySmokeChecks on the sheet of the shell: the SheetEditor partials (OpenSheet) or the XAML view (VerticalPileSmoke).</summary>
    private sealed class DisplaySheet(ShellSmokeBench b, ShellSheet sheet)
    {
        private readonly OpenSheet? historic = sheet.Historic is not null ? sheet.HistoricChecks : null;
        private readonly VerticalPileSmoke? mvvm = sheet.Historic is null ? new VerticalPileSmoke(sheet.Hosted, b.Services) : null;

        internal void VerifyDisplayLayout() { if (historic is not null) historic.VerifyDisplayLayout(); else mvvm!.VerifyDisplayLayout(); }
        internal Task VerifyDisplayScrolling(string directory, string name) => historic?.VerifyDisplayScrolling(directory, name) ?? mvvm!.VerifyDisplayScrolling(directory, name);
        internal Task VerifyStratigraphyEditing() => historic?.VerifyStratigraphyEditing() ?? mvvm!.VerifyStratigraphyEditing();
        internal void VerifyPileOutcomes() { if (historic is not null) historic.VerifyPileOutcomes(); else mvvm!.VerifyPileOutcomes(); }
        internal Task VerifyPresentation(string directory) => historic?.VerifyPresentation(directory) ?? mvvm!.VerifyPresentation(directory);
        internal void VerifyReport(string directory) { if (historic is not null) historic.VerifyReport(directory); else mvvm!.VerifyReport(directory); }
        /// <summary>
        /// VerifyNavigationThrough: the historic editor is the same control after the return (HistoricSheetHost), as in MainWindow. The XAML view of a tab is
        /// built again when the tab comes back on screen (proposal R6): data, result and the state kept by the ViewModel (the extended card, the capacity view)
        /// must survive; the selected tab of the results, held by the view only, is reported (question SH49), not required.
        /// </summary>
        internal async Task VerifyNavigationThrough(Action leave, Action resume)
        {
            if (historic is not null) { await historic.VerifyNavigationThrough(leave, resume); return; }
            var model = mvvm!.Sheet;
            const string charts = Anthea.Presentation.Modules.VerticalPile.VerticalPileSheetViewModel.ChartsCard;
            string oldExpanded = model.ExpandedCard;
            int oldTab = mvvm.View.Outputs.SelectedIndex, oldCapacity = mvvm.View.CapacityView.SelectedIndex;
            try
            {
                if (model.ExpandedCard.Length > 0 && model.ExpandedCard != charts) model.ToggleCardCommand.Execute(model.ExpandedCard);
                if (model.ExpandedCard != charts) model.ToggleCardCommand.Execute(charts);
                mvvm.View.Outputs.SelectedIndex = 2; mvvm.View.CapacityView.SelectedIndex = 1;
                await b.Idle();
                var viewBefore = mvvm.View;
                string data = model.Data.ToJsonString(); var result = model.Result;
                leave(); await b.Idle();
                resume(); await b.Idle();
                b.Check(model.Data.ToJsonString() == data && ReferenceEquals(model.Result, result) && model.ExpandedCard == charts && mvvm.View.CapacityView.SelectedIndex == 1,
                    "La navigazione Home/modulo conserva input, risultati e lo stato dei pannelli del ViewModel");
                b.Note($"SH49: dopo il ritorno la vista è {(ReferenceEquals(viewBefore, mvvm.View) ? "la stessa" : "ricostruita")}, scheda dei risultati {mvvm.View.Outputs.SelectedIndex} (era 2)");
            }
            finally
            {
                if (model.ExpandedCard != oldExpanded) model.ToggleCardCommand.Execute(oldExpanded.Length > 0 ? oldExpanded : model.ExpandedCard);
                mvvm.View.Outputs.SelectedIndex = oldTab; mvvm.View.CapacityView.SelectedIndex = oldCapacity;
            }
        }
    }
}
