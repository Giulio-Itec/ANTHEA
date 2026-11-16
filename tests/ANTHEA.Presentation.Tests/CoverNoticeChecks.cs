using System.Text.Json.Nodes;
using Anthea.Presentation.Projects;
using Anthea.Presentation.Shell.Projects;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// P10 of the note of step 6 (16/11/2026): the notice of the Materiali of a nearer level that the check of the cover ignores is shown
/// where the user sees the check, with the warnings of today: the status line above the sheet (orange), the notices of the comparison
/// (orange), the overview of the section («Dettaglio avvisi») and the badge of the tree. The check line keeps its outcome.
/// </summary>
internal static class CoverNoticeChecks
{
    internal static int Run() => SingleThreadContext.Run(RunAsync);

    private static Task<int> RunAsync(SingleThreadContext ui)
    {
        var expect = new Expect();
        var bench = new ShellBench();
        var workspace = bench.Workspace;
        var project = workspace.NewProject()!;
        var root = project.AddProject()!;
        project.Rename(root, "Viadotto");
        var general = project.AddSheet("mat_calcestruzzo", root)!;
        project.Rename(general, "CLS generale");
        var data = general["dati"]!.AsObject();
        data["classe"] = "C30/37"; data["esposizione_principale"] = "XC2"; data["esposizioni"] = new JsonArray("XC2");
        data["scelte"]!["life"] = "50 anni"; data["scelte"]!["ground"] = "Casseratura";
        var pila = project.AddSection(root)!;
        project.Rename(pila, "Pila 1");
        var piles = project.AddSheet("mat_calcestruzzo", pila)!;
        project.Rename(piles, "CLS pali");
        piles["dati"] = data.DeepClone();
        piles["dati"]!["scelte"]!["ground"] = "Direttamente su terra";
        var pile = project.AddSheet("str_palo", pila)!;
        project.Rename(pile, "Palo P1");
        pile["dati"]!["input"]!["cover_mm"] = 60;

        var checks = ProjectValidation.CoverChecks(pila, pile);
        expect.That(checks.Count == 2 && checks[0].Passed == true && checks[1].Passed is null && checks[1].Text.Contains("non «CLS pali» della sezione «Pila 1»", StringComparison.Ordinal),
            "Scenario della nota: " + string.Join(" | ", checks.Select(c => c.Text)));
        string notice = checks[1].Text;

        // The status line of the pile and of the Materiali ignored: the check and the notice, in orange.
        var status = new SharedStatusViewModel();
        var tab = workspace.OpenSheet(pile)!;
        status.Update(tab);
        expect.That(status.IsVisible && status.IsWarning && status.Text.Contains(checks[0].Text, StringComparison.Ordinal) && status.Text.Contains(notice, StringComparison.Ordinal),
            "Riga di stato del palo: " + status.Text);
        var materialTab = workspace.OpenSheet(piles)!;
        status.Update(materialTab);
        expect.That(status.IsVisible && status.IsWarning && status.Text.Contains(notice, StringComparison.Ordinal), "Riga di stato di «CLS pali»: " + status.Text);

        // The comparison, the overview and the badge of the section.
        var compare = new CoherenceViewModel(project, pila, bench.Services);
        expect.That(compare.Notices.Contains(new CoherenceNotice("• " + notice, true)) && compare.Notices.Contains(new CoherenceNotice("• " + checks[0].Text, false)),
            "Avvisi del confronto: " + string.Join(" | ", compare.Notices.Select(n => n.Text)));
        var overview = new SectionOverviewViewModel(project, pila);
        expect.That(overview.HasNotices && overview.Notices.Contains(notice) && overview.NoticeLines.Contains("• " + notice) && !overview.Notices.Contains(checks[0].Text),
            "Riepilogo della sezione: " + string.Join(" | ", overview.Notices));
        var badge = SharedDataCoordinator.Coherence(pila);
        expect.That(badge is { Warning: true } && badge.Title.Contains("avvis", StringComparison.Ordinal) && badge.ToolTip.Contains(notice, StringComparison.Ordinal),
            "Badge della sezione: " + badge?.Title);
        var cache = new ProjectCoherenceCache();
        cache.NewReading();
        expect.That(SharedDataCoordinator.Coherence(pila, cache) == badge && SharedDataCoordinator.Coherence(root, cache) == SharedDataCoordinator.Coherence(root), "Badge dalla cache");

        // The same casting on the two levels: the line goes back to the check alone, in green. The pile first (leaving the tab of «CLS
        // pali» confirms its data), then the change in the document.
        workspace.Activate(tab);
        piles["dati"]!["scelte"]!["ground"] = "Casseratura";
        status.Update(tab);
        expect.That(status.IsVisible && !status.IsWarning && !status.Text.Contains("più vicino al foglio", StringComparison.Ordinal), "Riga di stato senza avviso: " + status.Text);
        expect.That(new SectionOverviewViewModel(project, pila).Notices.Count == ProjectValidation.Warnings(pila).Length
            && !ProjectValidation.Warnings(pila).Any(w => w.Contains("più vicino al foglio", StringComparison.Ordinal)), "Riepilogo senza avviso");
        return Task.FromResult(expect.Count);
    }
}
