using Anthea.Application.Concrete;
using Anthea.Calculations;

namespace Anthea.Presentation.Tests;

internal static partial class ConcreteSectionSheetChecks
{
    private static async Task Actions3DAsync(string root, Expect expect)
    {
        using var sheet = OpenSheet(ConcreteSectionScenarios.FixtureData(root, "default"));
        await IdleAsync(sheet);
        var table = sheet.Actions.Table("3D");
        var row = table.Table.Shown[0];
        row["N"] = "-500"; row["Mx"] = "120"; row["My"] = "-70";
        table.Table.Refresh();
        await IdleAsync(sheet);
        int modified = 0; sheet.Modified += (_, _) => modified++;
        sheet.ShowsActions3D = true;
        sheet.Actions3DSource = "SLU"; sheet.Actions3DCombinationId = row.Values.S("id");
        expect.That(sheet.Actions3DScene.Actions.Select(v => v.Value).SequenceEqual(new double[] { -500, 120, -70 }),
            "Azioni 3D completo: N, Mx e My con segno, senza esclusione della flessione deviata");
        foreach (string source in new[] { "SLV", "SLE", "SLE_FREQ", "SLE_QP", "Taglio", "SLU" }) sheet.Actions3DSource = source;
        expect.That(modified == 0, "Azioni 3D completo: la navigazione non modifica il documento");
        row["Mx"] = "-30"; table.Table.Refresh(); await IdleAsync(sheet);
        expect.That(sheet.Actions3DScene.Actions.Single(v => v.Name == "Mx").Value == -30, "Azioni 3D completo: modifica delle sollecitazioni subito riportata nella scena");
        row["My"] = "non valido"; table.Table.Refresh();
        expect.That(sheet.Actions3DScene.Actions.Count == 0 && sheet.Actions3DScene.Message.Contains("My"), "Azioni 3D completo: dati non validi non disegnano forze fuorvianti");
        sheet.IsReadOnly = true; modified = 0;
        sheet.Actions3DSource = "Taglio"; sheet.ShowsActions3D = false; sheet.ShowsActions3D = true;
        expect.That(sheet.Actions3DSource == "Taglio" && sheet.ShowsActions3D && modified == 0,
            "Azioni 3D completo: vista e combinazioni consultabili in sola lettura");
    }
}
