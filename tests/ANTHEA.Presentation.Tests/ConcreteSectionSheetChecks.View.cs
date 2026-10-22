using Anthea.Application.Concrete;
using Anthea.Calculations;
using Anthea.Presentation.Modules.ConcreteSection;

namespace Anthea.Presentation.Tests;

/// <summary>
/// What the XAML view asks of the sheet besides the children (F5.10, view after S9): the dividers saved in «layout» (ConcreteLayout.cs of the historic
/// view), «Apri →» of the summaries, the blocks of the summaries, the groups of the panel by key, the check boxes of the dimensions, «×» of the bar of
/// the filters.
/// </summary>
internal static partial class ConcreteSectionSheetChecks
{
    private static async Task ViewSupportAsync(string root, Expect expect)
    {
        using var sheet = OpenSheet(ConcreteSectionScenarios.FixtureData(root, "default"));
        await IdleAsync(sheet);
        int modified = 0; sheet.Modified += (_, _) => modified++;
        var moved = new List<string>(); sheet.LayoutChanged += (_, group) => moved.Add(group);
        // Dividers: the saved ratio or the initial one; a move limited to 0.15–0.85, saved with Modified, announced to the view.
        expect.That(sheet.LayoutRatio("righe", .65) == ConcreteSheetPresentation.SplitRatio(sheet.Settings, "righe", .65), "Rapporto dei divisori dal foglio");
        sheet.MoveLayout("ingressi", .95);
        expect.That(sheet.Settings["layout"].D("ingressi") == .85 && sheet.LayoutRatio("ingressi", .3) == .85 && modified == 1 && moved.SequenceEqual(["ingressi"]),
            "Divisore spostato: rapporto limitato, salvato con Modified, annunciato alla vista");
        // The blocks of the summaries, in the order of the historic view, on the cards of the sheet.
        var blocks = sheet.SummaryBlocks;
        expect.That(blocks.Select(b => b.Key).SequenceEqual(ConcreteEditor.Sets.Append("Taglio")) && ReferenceEquals(blocks[0].Cards, sheet.Summaries["SLU"])
            && ReferenceEquals(blocks[^1].Cards, sheet.ShearDashboard) && blocks[2].Title == sheet.SummaryTitle("SLE"), "Blocchi dei riepiloghi del pannello di controllo");
        // «Apri →»: the tab of the check; the limit state of the 3D domain; the family of the serviceability.
        sheet.OpenSummaryCommand.Execute("SLV");
        expect.That(sheet.SelectedTab == 1 && sheet.Domain3D.Key == "SLV", "«Apri →» di SLV: dominio 3D sullo stato SLV");
        sheet.OpenSummaryCommand.Execute("SLE_QP");
        expect.That(sheet.SelectedTab == ConcreteSheetViewModel.StressTab && sheet.StressFamily == 2, "«Apri →» della quasi permanente");
        sheet.OpenSummaryCommand.Execute("Taglio");
        expect.That(sheet.SelectedTab == ConcreteSheetViewModel.ShearTab, "«Apri →» del taglio");
        // The groups of the panel by key, the same objects as the list.
        var inputs = sheet.Inputs;
        expect.That(inputs.Group.Keys.Order().SequenceEqual(new[] { "armature", "coefficienti", "geometria", "materiali", "staffe", "trefoli" }.Order())
            && inputs.Group.Values.All(inputs.Groups.Contains) && inputs.Group["materiali"].IsExpanded && !inputs.Group["armature"].IsExpanded, "Gruppi del pannello per chiave");
        // The check boxes of the dimensions: saved with Modified; refused in a read-only revision.
        int before = modified;
        inputs.ShowCoverDimensions = false;
        expect.That(!inputs.ShowCoverDimensions && !sheet.Settings.B("quote_copriferro", true) && modified == before + 1, "«Copriferro» salvato con Modified");
        // «×» of the bar of the filters: the text cleared, the order of insertion.
        var filter = inputs.Bars.Filter;
        filter.Query = "B0"; filter.Order = 2;
        filter.ClearCommand.Execute(null);
        expect.That(filter.Query == "" && filter.Order == 0 && inputs.Bars.Shown.Count == inputs.Bars.Rows.Count, "«×» del filtro");
        sheet.IsReadOnly = true;
        inputs.ShowDimensions = false;
        expect.That(inputs.ShowDimensions && sheet.Settings.B("quote_dimensioni", true), "Sola lettura: «Dimensioni» rifiutato");
        sheet.MoveLayout("righe", .5);
        expect.That(sheet.Settings["layout"].D("righe") == .5, "Sola lettura: i divisori si salvano ancora, come i divisori storici");
    }
}
