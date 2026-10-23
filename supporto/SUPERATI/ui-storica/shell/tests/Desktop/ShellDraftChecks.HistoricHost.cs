// Frammento archiviato (F5.13 g, 23/10/2026): il passo HistoricHost di tests/Desktop/ShellDraftChecks.cs (ui/check-shell-draft, immagine 21,
// ospite R5 dei moduli storici nella shell), da refactoring/f5 67eafba5 righe 567-681. Non compilato: README.md accanto.

    /// <summary>
    /// The host of the modules still historic (night of 22-23/10, fifth and sixth items of section 10.1; R5): a shell built by its composition root with
    /// the choices of the Release. Since the switches of the Release of 23/10/2026 the step does not depend on a given module: it opens the first module
    /// still historic in the Release (and a second one, if there is one) with the pile group (MVVM): the historic sheets open in their SheetEditor inside
    /// the tab (21), are confirmed into the document, move to a detached window and back with the same control, reopen read-only for an archived revision,
    /// open the Wiki tab with F1; for the RC section, when it is still historic, the report short goes to the editor (the bytes or the refusal of the
    /// editor itself), from the command of the shell. When no module of the Release is historic any more the step is not applicable: it writes why in
    /// 21-ospite-storico-non-applicabile.txt and does not fail. The infrastructure of R5 (HistoricSheetViewModel, HistoricSheetHost, ReleaseSheetFactory)
    /// is retired with part (g) of F5.13, together with SheetEditor and MainWindow.
    /// </summary>
    private static async Task HistoricHost(string output, BindingErrors bindings, Action<bool, string> Check)
    {
        bindings.Step = "ospite-storico";
        var historicModules = ModuleImplementations.Declared.Where(d => d.Implementation == SheetImplementation.Historic).Select(d => d.Module).ToArray();
        if (historicModules.Length == 0)
        {
            string reason = "Passo R5 non applicabile: nessun modulo della Release apre ancora la vista storica (ModuleImplementations.Declared, passaggi del 23/10/2026). "
                + "L'ospite dei moduli storici nella shell (HistoricSheetViewModel, HistoricSheetHost, ReleaseSheetFactory) si ritira con la parte (g) di F5.13.";
            File.WriteAllText(Path.Combine(output, "21-ospite-storico-non-applicabile.txt"), reason, new UTF8Encoding(false));
            Check(true, reason);
            return;
        }
        string first = historicModules[0];
        string? second = historicModules.Length > 1 ? historicModules[1] : null;
        var services = new DraftServices();
        var presentation = ShellComposition.WithShellWiki(services.Create());
        var detached = new List<QuietWindow>();
        var parts = ShellComposition.Create(presentation, DesktopComposition.CreateModules(), new CalculationLimiter(CalculationLimiter.DefaultMaximum), ShellComposition.ExamplesFolder,
            window => { var quiet = new QuietWindow(Offscreen(window)); detached.Add(quiet); return quiet; });
        var shell = parts.Shell;
        var main = Offscreen(parts.Window);
        main.Show();
        try
        {
            var workspace = shell.Workspace;
            var document = ProjectDocuments.CreateArchive();
            var root = ProjectDocuments.AddProject(document, "Galleria artificiale");
            var section = ProjectDocuments.AddSection(root, "Imbocco nord");
            string firstId = ProjectDocuments.AddSheet(section, first, "Primo foglio storico").S("id");
            string? secondId = second is null ? null : ProjectDocuments.AddSheet(section, second, "Secondo foglio storico").S("id");
            string groupId = ProjectDocuments.AddSheet(section, "geo_efficienza_orizzontale", "Palificata").S("id");
            workspace.OpenDocument(document, null, false);
            var project = workspace.Project!;
            var group = workspace.OpenSheet(project.Find(groupId)!);
            var other = secondId is null ? null : workspace.OpenSheet(project.Find(secondId)!);
            var rc = workspace.OpenSheet(project.Find(firstId)!);
            await Capture(main, output, "21-ospite-storico.png", bindings);
            var historic = rc.ViewModel as HistoricSheetViewModel;
            var view = historic?.View;
            Check(historic is { Editor: SheetEditor } && historic.ModuleId == first && (other is null || other.ViewModel is HistoricSheetViewModel) && group.ViewModel is not HistoricSheetViewModel
                && Descendants<SheetEditor>(main).SingleOrDefault() is { } shown && ReferenceEquals(shown, view) && Descendants<HistoricSheetHost>(main).Count() == 1,
                $"Release: {first}" + (second is null ? "" : " e " + second) + " storici nel loro SheetEditor dentro la scheda, palificata MVVM");

            // Confirmation into the document, as MainWindow.Commit.
            workspace.CommitActive();
            Check(System.Text.Json.Nodes.JsonNode.DeepEquals(project.Find(firstId)!["dati"], historic!.Data) && ReferenceEquals(workspace.Active, rc), "Conferma del foglio storico nel documento");

            // F1 on the historic sheet: the Wiki tab of the shell (WikiContextHelp through the window of the shell), not the historic window.
            if (Anthea.Wiki.WikiCatalog.ForModule(first) is { } guide)
            {
                view!.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(view), 0, Key.F1) { RoutedEvent = Keyboard.KeyDownEvent });
                await Idle(main);
                Check(shell.Center is PageTabViewModel { Kind: ShellTabKind.Wiki, Content: WikiTabViewModel { Uri: var uri } } && uri == guide.Id && services.WikiRequests == 0,
                    "F1 sul foglio storico: la scheda Wiki della shell");
                workspace.Activate(rc);
                await Idle(main);
            }

            // The short report of the RC section from the command of the shell, when the RC section is still historic: the editor writes it, or
            // refuses it with its own text.
            if (first == "str_palo")
            {
                bindings.Step = "report-short";
                string title = rc.Heading;
                string? refusal = null;
                try { _ = historic.Editor.BuildShortReport(title); } catch (InvalidOperationException error) { refusal = error.Message; }
                Check(shell.Commands.ShortReportVisible && shell.Commands.ShortReportCommand.CanExecute(null), "«Report short» per la sezione c.a. storica");
                int failures = services.Failures.Count;
                await shell.Commands.ShortReportCommand.ExecuteAsync(null);
                Check(refusal is null ? services.Saves.LastOrDefault() == "Relazione_short.docx" && services.Failures.Count == failures
                    : services.Failures.Count == failures + 1 && services.Failures[^1] == ShellCommandsViewModel.ShortTitle + ": " + refusal,
                    "Report short dal comando della shell: " + (refusal is null ? "costruito dal SheetEditor, richiesto il file" : "rifiutato con il testo del SheetEditor («" + refusal + "»)"));
                if (refusal is not null) services.Failures.RemoveAt(services.Failures.Count - 1);
            }

            // A detached window takes the same control; closing it brings the tab back with the control.
            var tab = shell.Windows.TabOf(rc)!;
            shell.Windows.MoveToNewWindow(tab);
            var window = detached.Single().Window;
            await Idle(window);
            await Idle(main);
            Check(Descendants<SheetEditor>(window).SingleOrDefault() is { } moved && ReferenceEquals(moved, view) && !Descendants<SheetEditor>(main).Any(),
                "Finestra staccata: lo stesso SheetEditor");
            window.Close();
            await Idle(main);
            shell.Windows.Select(tab);
            await Idle(main);
            Check(Descendants<SheetEditor>(main).SingleOrDefault() is { } back && ReferenceEquals(back, view), "Scheda riportata nella principale: lo stesso SheetEditor");

            // An archived revision: the historic sheet reopens read-only (InspectRevision), then editable again on the current version.
            project.NewRevision(project.Find(section.S("id"))!, "Prima emissione");
            project.SwitchRevision(section.S("id"), 0);
            await Idle(main);
            Check(rc.ViewModel is HistoricSheetViewModel { IsReadOnly: true } reopened && !ReferenceEquals(reopened, historic), "Revisione archiviata: foglio storico riaperto in sola lettura");
            project.ExitPreview();
            await Idle(main);
            Check(rc.ViewModel is HistoricSheetViewModel { IsReadOnly: false }, "Fine dell'anteprima: foglio storico modificabile");
        }
        finally
        {
            main.Close();
            shell.Dispose();
        }
        Check(services.Failures.Count == 0, "Ospite storico: nessun errore mostrato: " + string.Join(" | ", services.Failures));
    }
