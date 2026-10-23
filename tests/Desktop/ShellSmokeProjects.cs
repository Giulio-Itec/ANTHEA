using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Anthea.Application.Documents;
using Anthea.Presentation.Shell.Navigation;
using Anthea.Presentation.Shell.Projects;
using X.Core;
using X.Desktop.Views.Shell;

namespace X.Desktop;

internal static partial class ShellSmoke
{
    /// <summary>
    /// ui/shell-smoke-projects: ProjectSmokeChecks (MainWindow.SmokeProjects) and ProjectDragLayoutSmokeChecks on the new shell. The same project (a
    /// work with two abutments, foundations and a pile), the same steps and assertions: selection without opening, the icon that opens the sheet, «← Torna
    /// al progetto», the drop of a sheet into a subsection keeping its edited data, moves without duplicates, saving and reopening, the recursive
    /// validation and the old archives, creation of projects and sections without dialogs, the hierarchy of the names, the rename in place (double click,
    /// Enter, Esc, a click outside, another window), the single click that shows the overview, names of new sheets, reorder of the sheets, the stable
    /// layout during a drag, the menu of a project, the icon of the RC section, the drop on a row, the drag of sections and projects before and after,
    /// invalid drops and the cancelled drag, pending edits committed by a reorder, the order kept in the file.
    /// </summary>
    private static async Task Projects(ShellSmokeBench b)
    {
        string directory = b.Output;
        var shell = b.Shell;
        var workspace = b.Workspace;
        var document = ProjectDocuments.CreateArchive();
        var project = J.Obj(("nome", "Progetto di prova"), ("strutture", new JsonArray()));
        document.Array("progetti").Add(project);
        var main = ProjectDocuments.AddSection(project, "Opera principale");
        var first = ProjectDocuments.AddSection(main, "Spalla A");
        var deep = ProjectDocuments.AddSection(first, "Fondazioni");
        var other = ProjectDocuments.AddSection(main, "Spalla B");
        var sheet = J.Obj(("nome", "Palo 1"), ("modulo_id", "geo_palo_verticale"), ("dati", Archivio.NuovoFoglio("geo_palo_verticale")));
        first.Array("fogli").Add(sheet);
        // MainWindow.ShowProjects on the archive: the shell opens it on the page «Progetti».
        workspace.OpenDocument(document, null, false);
        await b.Idle();
        var session = b.Project;
        b.Check(shell.Center is SectionOverviewViewModel && shell.Navigation.Page == ShellPage.Projects && ReferenceEquals(session.Document, document), "Archivio aperto sulla pagina Progetti");
        b.Node(sheet).IsSelected = true;
        await b.Idle();
        b.Check(shell.Center is SectionOverviewViewModel && workspace.Active is null && ReferenceEquals(shell.Tree.Selected?.Node, sheet), "La selezione di un foglio non lo apre");
        await b.Press(b.OpenButton(sheet));
        b.Check(ReferenceEquals(workspace.Active?.Sheet, sheet), "Miniatura non apre il foglio");
        b.Sheet.SetGeometry("1.25");
        b.Check(shell.SheetBar.BackLabel == "← Torna al progetto", "Pulsante di ritorno non contestuale");
        var pile = workspace.Active!;
        await b.Press(b.Button(shell.SheetBar.BackCommand));
        b.Check(shell.Center is SectionOverviewViewModel && ReferenceEquals(workspace.SheetOf(sheet), pile), "Ritorno non conserva la scheda");

        var drop = b.Drag(deep, ProjectTreeInput.SheetFormat, sheet, DragDrop.DropEvent);
        await b.Idle();
        b.Check(drop.Effects == DragDropEffects.Move && first.Array("fogli").Count == 0 && ReferenceEquals(deep.Array("fogli")[0], sheet), "Drop non sposta il foglio nella sottosezione");
        b.Check(sheet["dati"]!["generali"].S("diametro") == "1.25", "Spostamento perde input modificati");
        b.Drag(other, ProjectTreeInput.SheetFormat, sheet, DragDrop.DropEvent);
        await b.Idle();
        b.Drag(other, ProjectTreeInput.SheetFormat, sheet, DragDrop.DropEvent);
        await b.Idle();
        b.Check(other.Array("fogli").Count == 1 && deep.Array("fogli").Count == 0, "Spostamento duplica il foglio");
        string file = Path.Combine(directory, "progetti.programma");
        b.Services.SaveFile = _ => file;
        shell.Commands.SaveCommand.Execute(null);
        b.Check(ConcretePersistenceComparison.Equivalent(DocumentService.Open(file), session.Document), "Round trip gerarchia non riuscito");
        sheet["modulo_id"] = "inesistente";
        bool rejected = false; try { Archivio.Valida(session.Document); } catch (ArgumentException) { rejected = true; }
        b.Check(rejected, "Validazione non controlla i fogli annidati"); sheet["modulo_id"] = "geo_palo_verticale";
        var legacy = (JsonObject)session.Document.DeepClone();
        legacy.Array("progetti")[0]!["strutture"] = new JsonArray(J.Obj(("nome", "Vecchia struttura"), ("fogli", new JsonArray())));
        Archivio.Valida(legacy);
        b.Check(true, "Archivio con le strutture di una versione precedente valido");
        shell.Navigation.Select(ShellPage.Projects);
        await b.Idle();
        // Creation is immediate, with unique placeholder names and no naming dialogs: «+ Progetto» and «+ Sezione»/«Sottosezione».
        int count = session.Document.Array("progetti").Count;
        await b.Press(b.Button(shell.Tree.AddProjectCommand));
        await b.Press(b.Button(shell.Tree.AddProjectCommand));
        b.Check(session.Document.Array("progetti").Count == count + 2, "Creazione immediata progetto");
        b.Check(session.Document.Array("progetti").Select(p => p.S("nome")).Distinct().Count() == count + 2, "Nomi provvisori duplicati");
        b.Node(main).AddSectionCommand.Execute(null);
        await b.Idle();
        b.Node(main).AddSectionCommand.Execute(null);
        await b.Idle();
        b.Check(main.Array("strutture").Select(p => p.S("nome")).Distinct().Count() == main.Array("strutture").Count, "Nomi sezioni duplicati");
        var mainChild = main.Array("strutture")[0]!.AsObject();
        b.Check(b.Label(project).FontSize > b.Label(main).FontSize && b.Label(main).FontSize > b.Label(mainChild).FontSize, "Gerarchia grafica non progressiva");

        // The rename in place: a first click schedules the overview, the double click renames; the delayed click does not interrupt the rename. Only the
        // press that tunnels through the window and the release of the label are raised: the press of the TreeViewItem would take the keyboard focus and
        // activate the window off screen, which the checks of the night never do. A raised event does not reach the input bindings either (the
        // CommandManager reads the input of the devices): the double click runs the command of the gesture of the label, as the binding of the view does.
        var sectionLabel = b.Label(main);
        ShellSmokeBench.Mouse(sectionLabel, UIElement.PreviewMouseDownEvent, 1);
        ShellSmokeBench.Mouse(sectionLabel, UIElement.MouseLeftButtonUpEvent, 1);
        b.Check(sectionLabel.IsLoaded && ProjectTreeInput.Timer(b.Window).IsPending, "Primo clic ricrea il nodo prima del doppio clic (clic singolo in attesa)");
        ShellSmokeBench.Mouse(sectionLabel, UIElement.PreviewMouseDownEvent, 2);
        var renamed = b.Node(main);
        b.DoubleClick(sectionLabel);
        string diagnosis = "subito " + renamed.IsRenaming;
        await b.Idle();
        diagnosis += ", dopo " + renamed.IsRenaming + ", stesso nodo " + ReferenceEquals(b.Node(main), renamed) + ", attiva " + b.Window.IsActive + ", fuoco " + Keyboard.FocusedElement?.GetType().Name
            + ", in attesa " + ProjectTreeInput.Timer(b.Window).IsPending;
        var rename = b.RenameBox(main);
        b.Check(b.Node(main).IsRenaming && rename is not null && !ProjectTreeInput.Timer(b.Window).IsPending, "Doppio clic sul nome: rinomina in linea, clic singolo annullato (" + diagnosis + ")");
        var timer = ProjectTreeInput.Timer(b.Window);
        await Task.Delay(timer.Delay + TimeSpan.FromMilliseconds(50));
        await b.Idle();
        b.Check(b.Node(main).IsRenaming && ReferenceEquals(b.RenameBox(main), rename), "Il clic singolo ritardato interrompe la rinomina");
        rename!.Text = "Opera rinominata";
        b.Key(rename, Key.Enter);
        await b.Idle();
        b.Check(main.S("nome") == "Opera rinominata", "Rinomina inline non confermata");
        b.Node(main).BeginRenameCommand.Execute(null);
        await b.Idle();
        rename = b.RenameBox(main)!; rename.Text = "Da annullare";
        b.Key(rename, Key.Escape);
        await b.Idle();
        b.Check(main.S("nome") == "Opera rinominata", "Escape non annulla rinomina");
        b.Node(main).BeginRenameCommand.Execute(null);
        await b.Idle();
        rename = b.RenameBox(main)!; rename.Text = "Opera confermata fuori";
        ShellSmokeBench.Mouse((FrameworkElement)b.Window.FindName("CenterContent"), UIElement.PreviewMouseDownEvent);
        await b.Idle();
        b.Check(main.S("nome") == "Opera confermata fuori" && b.RenameBox(main) is null, "Clic su area vuota non termina la rinomina");
        b.Node(main).BeginRenameCommand.Execute(null);
        await b.Idle();
        rename = b.RenameBox(main)!; rename.Text = "Opera confermata alla disattivazione";
        b.Deactivate();
        await b.Idle();
        b.Check(main.S("nome") == "Opera confermata alla disattivazione" && b.RenameBox(main) is null, "Cambio finestra non termina la rinomina");
        sectionLabel = b.Label(main);
        ShellSmokeBench.Mouse(sectionLabel, UIElement.MouseLeftButtonUpEvent, 1);
        await Task.Delay(timer.Delay + TimeSpan.FromMilliseconds(300));
        await b.Idle();
        b.Check(shell.Center is SectionOverviewViewModel { Section: var shown } && ReferenceEquals(shown, main) && ReferenceEquals(shell.Tree.Selected?.Node, main), "Clic singolo non apre il riepilogo");
        shell.Commands.SaveCommand.Execute(null);
        b.Check(ConcretePersistenceComparison.Equivalent(DocumentService.Open(file), session.Document), "Nomi e gerarchia non persistono");
        shell.Tree.AddSheet("str_palo", other, false);
        shell.Tree.AddSheet("str_palo", other, false);
        await b.Idle();
        var ca1 = other.Array("fogli")[1]!.AsObject(); var ca2 = other.Array("fogli")[2]!.AsObject();
        b.Check(ca1.S("nome") != ca2.S("nome"), "Nomi automatici schede duplicati");
        b.Drag(sheet, ProjectTreeInput.SheetFormat, ca2, DragDrop.DropEvent, .1);
        await b.Idle();
        b.Check(ReferenceEquals(other.Array("fogli")[0], ca2), "Riordino verso inizio");
        b.Drag(ca1, ProjectTreeInput.SheetFormat, ca2, DragDrop.DropEvent, .9);
        await b.Idle();
        b.Check(ReferenceEquals(other.Array("fogli")[2], ca2), "Riordino verso fine");
        shell.Navigation.Select(ShellPage.Projects);
        await b.Idle();
        await DragLayout(b, other, sheet, ca1);
        var menu = b.Menu(project).Items.OfType<MenuItem>().Where(m => m.Visibility == Visibility.Visible).Select(m => m.Header?.ToString()).ToArray();
        b.Check(menu.SequenceEqual(["Apri", "Rinomina", "Duplica", "Sottosezione", "Report Word della sezione", "Elimina"]), "Menu gruppo incompleto: " + string.Join(", ", menu));
        var caButton = b.OpenButton(ca1);
        b.Check(caButton is { Width: 36, Height: 36, Content: Viewbox { Stretch: System.Windows.Media.Stretch.Uniform } }, "Miniatura CA non scalata");
        // The drop handler of a row, including the order within the same list: ca2 before ca1.
        var rowDrop = b.Drag(ca1, ProjectTreeInput.SheetFormat, ca2, DragDrop.DropEvent, .02);
        await b.Idle();
        b.Check(rowDrop.Effects == DragDropEffects.Move && ReferenceEquals(other.Array("fogli")[1], ca2), "Drop riga non riordina");
        b.Drag(deep, ProjectTreeInput.SheetFormat, ca2, DragDrop.DropEvent);
        await b.Idle();
        b.Check(ReferenceEquals(deep.Array("fogli")[0], ca2), "Spostamento tra gruppi dopo riordino");
        shell.Commands.SaveCommand.Execute(null);
        b.Check(ConcretePersistenceComparison.Equivalent(DocumentService.Open(file), session.Document), "Ordine non conservato nel file");
        shell.Navigation.Select(ShellPage.Projects);
        await b.Idle();

        // Drag the header of a section or project in both directions, keeping complete subtrees and data.
        async Task Reorder(JsonObject source, JsonObject target, bool after, bool capture = false)
        {
            await b.Idle();
            double edge = after ? .98 : .02;
            var over = b.Drag(target, ProjectTreeInput.SectionFormat, source, DragDrop.DragOverEvent, edge);
            b.Check(over.Effects == DragDropEffects.Move && ProjectTreeInput.Indicator(b.Tree) == (after ? ProjectDropPlace.After : ProjectDropPlace.Before), "Indicatore riordino sezioni assente o errato");
            if (capture) await b.Capture("trascinamento-sezioni.png");
            var args = b.Drag(target, ProjectTreeInput.SectionFormat, source, DragDrop.DropEvent, edge);
            b.Check(args.Effects == DragDropEffects.Move && ProjectTreeInput.Indicator(b.Tree) is null, "Drop sezione rifiutato o indicatore residuo");
            await b.Idle();
            var siblings = (JsonArray)source.Parent!;
            b.Check(siblings.IndexOf(source) == siblings.IndexOf(target) + (after ? 1 : -1), "Posizione della sezione errata");
            var parent = siblings.Parent as JsonObject;
            var displayed = (ReferenceEquals(parent, session.Document) ? shell.Tree.Roots : b.Node(parent!).Children).Where(n => n.Kind is ProjectNodeKind.Project or ProjectNodeKind.Section).Select(n => n.Node!);
            b.Check(displayed.SequenceEqual(siblings.OfType<JsonObject>()), "Ordine delle sezioni a video diverso dal documento");
            b.Check(ReferenceEquals(shell.Tree.Selected?.Node, source), "Sezione spostata non selezionata");
        }
        var firstSnapshot = first.DeepClone(); var otherSnapshot = other.DeepClone();
        workspace.Activate(pile);
        await b.Idle();
        var activeView = pile.ViewModel;
        var conflictsBefore = ProjectSharedData.Differences(main).Select(d => (d.Key, d.Left, d.Right)).OrderBy(d => d.Key).ThenBy(d => d.Left).ThenBy(d => d.Right).ToArray();
        await Reorder(first, other, true, true);
        b.Check(JsonNode.DeepEquals(first, firstSnapshot) && JsonNode.DeepEquals(other, otherSnapshot), "Riordino altera schede o sottosezioni");
        b.Check(ReferenceEquals(workspace.Active, pile) && ReferenceEquals(pile.ViewModel, activeView), "Riordino perde la scheda attiva");
        b.Check(conflictsBefore.SequenceEqual(ProjectSharedData.Differences(main).Select(d => (d.Key, d.Left, d.Right)).OrderBy(d => d.Key).ThenBy(d => d.Left).ThenBy(d => d.Right)), "Riordino sezioni altera i conflitti");
        await Reorder(first, other, false);
        var secondDeep = session.AddSection(first)!;
        session.Rename(secondDeep, "Fondazioni secondarie");
        await b.Idle();
        await Reorder(secondDeep, deep, false); await Reorder(secondDeep, deep, true);
        var otherProject = session.Document.Array("progetti")[1]!.AsObject();
        await Reorder(project, otherProject, true); await Reorder(project, otherProject, false);
        // Invalid targets and cancellation leave the archive unchanged.
        await b.Idle();
        var beforeInvalid = session.Document.DeepClone();
        foreach (var pair in new[] { (first, first), (first, deep), (deep, other), ((JsonObject)first.DeepClone(), other), (first, sheet) })
        {
            var args = b.Drag(pair.Item2, ProjectTreeInput.SectionFormat, pair.Item1, DragDrop.DropEvent, .02);
            b.Check(args.Effects == DragDropEffects.None && JsonNode.DeepEquals(beforeInvalid, session.Document), "Drop non valido ha modificato la gerarchia");
        }
        b.Drag(other, ProjectTreeInput.SectionFormat, first, DragDrop.DragOverEvent, .02);
        b.Check(ProjectTreeInput.Indicator(b.Tree) is not null, "Indicatore prima dell'annullamento assente");
        b.Drag(other, ProjectTreeInput.SectionFormat, first, DragDrop.DragLeaveEvent, .02);
        b.Check(ProjectTreeInput.Indicator(b.Tree) is null && shell.Tree.DropHint.Length == 0 && JsonNode.DeepEquals(beforeInvalid, session.Document), "Annullamento lascia indicatore o cambia documento");
        // Pending edits are committed by a reorder while the sheet has the focus.
        workspace.Activate(workspace.SheetOf(sheet));
        await b.Idle();
        b.Sheet.SetGeometry("1.35");
        await Reorder(other, first, false);
        b.Check(sheet["dati"]!["generali"].D("diametro") == 1.35, "Riordino perde modifiche in sospeso");
        shell.Commands.SaveCommand.Execute(null);
        var expectedOrder = session.Document.DeepClone();
        // MainWindow: «dirty = false; LoadFile(file)». The shell activates an archive already open: it is closed first (saved, no question), then opened.
        b.Check(workspace.CloseProject() && workspace.Project is null, "Archivio salvato chiuso senza domande");
        workspace.OpenFile(file);
        await b.Idle();
        b.Check(ConcretePersistenceComparison.Equivalent(expectedOrder, b.Project.Document), "Ordine sezioni o contenuti persi alla riapertura");
        shell.Navigation.Select(ShellPage.Projects);
        await b.Capture("progetti.png");
        File.WriteAllText(Path.Combine(directory, "smoke.txt"), "OK (shell nuova): inserimento senza dialoghi, riordino e drop sulle righe, miniatura CA scalata, menu gruppi, creazione immediata, gerarchia visiva, rinomina inline e annullamento, miniature, ritorno al progetto, sottosezioni, selezione, drop, conservazione input, spostamenti senza duplicati, salvataggio/riapertura, validazione ricorsiva e archivi precedenti. Riordino sezioni e progetti tramite eventi WPF: prima/dopo, livelli annidati, indicatore e annullamento, drop non validi, contenuti e scheda attiva conservati, conflitti invariati, modifiche in sospeso, ordine persistente.");
    }

    /// <summary>
    /// ProjectDragLayoutSmokeChecks on the new shell: while a module of the catalogue is dragged into a section with a long name, and a sheet over another
    /// sheet before and after it, the panels of the window (navigation, tree, centre, the rows of the target and of the anchor) and the scroll of the tree
    /// do not move; the hint names the section and goes when the drag leaves; the document does not change.
    /// </summary>
    private static async Task DragLayout(ShellSmokeBench b, JsonObject section, JsonObject sourceSheet, JsonObject anchorSheet)
    {
        var window = b.Window;
        Rect Bounds(FrameworkElement element) => new(element.TranslatePoint(new Point(), window), element.RenderSize);
        var documentBefore = b.Project.Document.DeepClone();
        string originalName = section.S("nome");
        try
        {
            // Long names must not turn the destination hint into extra lines above the tree.
            section["nome"] = originalName + " — " + string.Join(" ", Enumerable.Repeat("Fondazioni della struttura principale", 6));
            b.Shell.Tree.Refresh(section);
            await b.Idle();
            var hint = (TextBlock)window.FindName("DropHint");
            var panels = new FrameworkElement[] { (FrameworkElement)window.FindName("NavigationBar"), (FrameworkElement)window.FindName("ProjectTree"), (FrameworkElement)window.FindName("CenterContent"),
                b.Tree, ProjectTreeInput.Row(b.Item(section)), ProjectTreeInput.Row(b.Item(anchorSheet)) };
            var initial = panels.Select(Bounds).ToArray();
            var scroll = ShellSmokeBench.Descendants<ScrollViewer>(b.Tree).First();
            double initialOffset = scroll.VerticalOffset;
            void Stable(string phase)
            {
                for (int i = 0; i < panels.Length; i++)
                {
                    var now = Bounds(panels[i]); var before = initial[i];
                    b.Check(Math.Abs(now.X - before.X) < .1 && Math.Abs(now.Y - before.Y) < .1 && Math.Abs(now.Width - before.Width) < .1 && Math.Abs(now.Height - before.Height) < .1,
                        $"Layout stabile durante {phase}: {panels[i].GetType().Name}, prima {before}, dopo {now}");
                }
                b.Check(Math.Abs(scroll.VerticalOffset - initialOffset) < .1, "Il trascinamento non cambia lo scorrimento dell’albero (" + phase + ")");
            }
            for (int i = 0; i < 5; i++)
            {
                var over = b.Drag(section, ProjectTreeInput.ModuleFormat, "str_palo", DragDrop.DragOverEvent);
                await b.Idle();
                b.Check(over.Effects == DragDropEffects.Copy && hint.IsVisible && hint.Text.Contains(section.S("nome")), "Destinazione della scheda indicata");
                Stable("inserimento nella sezione");
                if (i == 0) await b.Capture("trascinamento-scheda-stabile.png");
                b.Drag(section, ProjectTreeInput.ModuleFormat, "str_palo", DragDrop.DragLeaveEvent);
                await b.Idle();
                b.Check(b.Shell.Tree.DropHint.Length == 0 && !hint.IsVisible, "Nessuna indicazione residua dopo l’uscita dalla sezione");
                Stable("uscita dalla sezione");
            }
            foreach (bool after in new[] { false, true, false, true })
            {
                var over = b.Drag(anchorSheet, ProjectTreeInput.SheetFormat, sourceSheet, DragDrop.DragOverEvent, after ? .9 : .1);
                await b.Idle();
                b.Check(over.Effects == DragDropEffects.Move, "Riordino schede accettato");
                Stable("riordino delle schede");
                b.Drag(anchorSheet, ProjectTreeInput.SheetFormat, sourceSheet, DragDrop.DragLeaveEvent);
                await b.Idle();
                Stable("annullamento del riordino");
            }
        }
        finally
        {
            section["nome"] = originalName;
            b.Shell.Tree.Refresh(section);
            await b.Idle();
        }
        b.Check(JsonNode.DeepEquals(documentBefore, b.Project.Document), "L’anteprima del trascinamento non modifica il documento");
        File.WriteAllText(Path.Combine(b.Output, "trascinamento-layout.txt"), "OK (shell nuova): posizione e dimensioni dei pannelli, righe e scorrimento stabili durante inserimento, riordino e uscita; nomi lunghi; documento invariato.");
    }
}
