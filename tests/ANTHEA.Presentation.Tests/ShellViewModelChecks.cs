using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Commands;
using Anthea.Presentation.Shell.Navigation;
using Anthea.Presentation.Shell.Projects;
using Anthea.Presentation.Shell.Tabs;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// F5.13 b7: the shell as one ViewModel without WPF: the title of the window, the overview asked by the tree with its revision bar, the
/// revision bar and the status line of the sheet with the focus, the report asked by a sheet.
/// </summary>
internal static class ShellViewModelChecks
{
    internal static int Run() => SingleThreadContext.Run(_ => Task.FromResult(RunAll()));

    private sealed class NoWindows : IWindowService
    {
        public void Open(TabHostViewModel host) { }
        public void Close(TabHostViewModel host) { }
        public void Activate(TabHostViewModel host) { }
    }

    private sealed class NoExports : ISheetExports
    {
        public void ExportResult(SheetSession sheet, string path) { }
        public void ExportReport(SheetSession sheet, string path, string title, IReadOnlySet<string> options) { }
        public byte[] BuildShortReport(SheetSession sheet, string title) => [];
        public Task WriteShortReportAsync(string path, byte[] report, string title) => Task.CompletedTask;
    }

    private static int RunAll()
    {
        var expect = new Expect();
        var bench = new ShellBench();
        using var shell = new ShellViewModel(bench.Services, bench.Factory, new DocumentFiles(Path.Combine(Path.GetTempPath(), "anthea-esempi-assenti")), new NoWindows(), new NoExports());
        var workspace = shell.Workspace;
        expect.That(shell.Title == "ANTHEA" && shell.Revisions is null && shell.Overview is null && shell.Navigation.Page == ShellPage.Home, "Shell all'avvio");

        // The title follows the document of the sheet with the focus.
        var single = workspace.NewCalculation("str_palo");
        expect.That(shell.Title == "ANTHEA — Nuovo documento" && shell.Windows.TabOf(single) is not null, "Titolo di un file nuovo");
        ShellBench.Probe(single).Label = "x";
        expect.That(shell.Title == "ANTHEA — Nuovo documento *", "Titolo dopo una modifica");

        // The tree asks the overview: the page «Progetti» on that section, with its revision bar.
        var root = shell.Tree.AddProject()!;
        expect.That(shell.Revisions is not null && shell.Navigation.Page == ShellPage.Projects && ReferenceEquals(shell.Overview?.Section, root)
            && ReferenceEquals(shell.Revisions!.Scope, root) && workspace.Active is null && shell.Title == "ANTHEA — Nuovo documento *", "Riepilogo del nuovo progetto");
        shell.Tree.AddSectionCommand.Execute(null);
        var section = (JsonObject)root.Array("strutture")[0]!;
        expect.That(ReferenceEquals(shell.Overview?.Section, section) && ReferenceEquals(shell.Revisions.Scope, section), "Riepilogo della nuova sezione");
        var material = shell.Tree.AddSheet("mat_calcestruzzo", section)!;
        var second = workspace.Project!.AddSheet("mat_calcestruzzo", section)!;

        // A sheet of the project with the focus: its revision bar and the status line of its edits.
        var tab = workspace.SheetOf(material)!;
        expect.That(ReferenceEquals(workspace.Active, tab) && ReferenceEquals(shell.Revisions.Scope, section), "Barra delle revisioni del foglio");
        tab.ViewModel.Data["classe"] = "C40/50";
        ShellBench.Probe(tab).Label = "modifica";
        expect.That(shell.SharedStatus.IsVisible && shell.SharedStatus.IsWarning && shell.SharedStatus.Text.StartsWith("⚠ Dati diversi tra i fogli", StringComparison.Ordinal), "Riga di stato del foglio");

        // The overview follows the changes of the project: a deleted section gives the first project.
        bench.Confirmations.Then(false);
        shell.ShowOverview(section);
        expect.That(workspace.Active is null && ReferenceEquals(shell.Overview?.Section, section) && second["dati"].S("classe") != "C40/50", "Riepilogo dopo la conferma «Solo questo foglio»");
        bench.Confirmations.Then(true);
        workspace.Project.Delete(section, out _);
        expect.That(ReferenceEquals(shell.Overview?.Section, root), "Riepilogo dopo l'eliminazione della sezione");

        // A sheet asks its report: the command of the shell runs (here: no results, the message of today).
        var other = workspace.NewCalculation("geo_palo_verticale");
        ShellBench.Probe(other).ReportCommand.Execute(null);
        expect.That(bench.Messages.Shown.Last() == ("", ShellCommandsViewModel.CompleteFirst, false), "Report richiesto dal foglio");
        expect.That(shell.Navigation.Page is null && shell.Title == "ANTHEA — Nuovo documento", "Titolo del foglio con il fuoco");
        return expect.Count;
    }
}
