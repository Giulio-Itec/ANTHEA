using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Calculations;
using Anthea.Presentation.Modules.QuickSection;

namespace Anthea.Presentation.Tests;

internal static class QuickActionsChecks
{
    internal static async Task Run(Expect expect, Func<JsonObject, QuickSectionSheetViewModel> open)
    {
        var data = ConcreteQuickSection.Create();
        string id = ConcreteQuickSection.Combinations(data)[0].S("id");
        ConcreteQuickSection.View(data)["flessione"] = "Deviata";
        foreach (var (key, value) in new[] { ("N", "-321.5"), ("Mx", "-40"), ("My", "12") }) ConcreteQuickSection.WriteCombination(data, id, key, value);
        ConcreteQuickShear.EnsureCombination(data);
        string shearId = ConcreteQuickShear.Combinations(data)[0].S("id");
        foreach (var (key, value) in new[] { ("N", "567"), ("Vx", "-75"), ("Vy", "30") }) ConcreteQuickShear.WriteCombination(data, shearId, key, value);
        string original = data.ToJsonString();
        var bending = ConcreteQuickActions.Read(data, false, id);
        var shear = ConcreteQuickActions.Read(data, true, shearId);
        expect.That(bending.Actions.Select(a => a.Value).SequenceEqual(new[] { -321.5, -40d, 12d })
            && shear.Actions.Select(a => a.Value).SequenceEqual(new[] { 567d, -75d, 30d }), "Azioni 3D: tabelle indipendenti, segni e valori originari conservati");
        expect.That(bending.Actions.Concat(shear.Actions).All(a => !a.Label.Contains('-') && !a.Label.Contains('+'))
            && bending.Actions[1].Label.Contains("40") && shear.Actions[1].Label.Contains("75"), "Azioni 3D: etichette in valore assoluto, segno disponibile per le frecce");
        expect.That(data.ToJsonString() == original && ConcreteQuickActions.Read(data, true, id).Actions.Count == 0,
            "Azioni 3D: lettura senza mutazioni, nessuna combinazione incrociata tra le tabelle");
        ConcreteQuickSection.View(data)["flessione"] = "Retta";
        expect.That(ConcreteQuickActions.Read(data, false, id).Actions.Single(a => a.Name == "My").Value == 0,
            "Azioni 3D: flessione retta rappresenta My effettivo nullo");
        // Independent mechanical reference: solver strains at the concrete vertices and
        // the steel stress gradient, not the drawing's rotation rule. Mx+ compresses +y; My+ compresses -x.
        var probe = ConcreteQuickSection.Create();
        // Symmetric reinforcement isolates each bending plane: asymmetric reinforcement
        // can incline the neutral axis even when only one moment component is assigned.
        ConcreteQuickSection.WriteLayer(probe, 1, "n", "4");
        ConcreteQuickSection.WriteLayer(probe, 1, "phi", "20");
        ConcreteQuickSection.ApplyLayers(probe);
        string probeId = ConcreteQuickSection.Combinations(probe)[0].S("id");
        ConcreteQuickSection.View(probe)["flessione"] = "Deviata";
        ConcreteQuickSection.SetCombinationState(probe, probeId, "SLE");
        var bars = ConcreteQuickSection.Geometry(probe).Geometry!.Bars;
        foreach (string component in new[] { "Mx", "My" })
            foreach (int sign in new[] { -1, 1 })
            {
                ConcreteQuickSection.WriteCombination(probe, probeId, "Mx", component == "Mx" ? (sign * 40).ToString() : "0");
                ConcreteQuickSection.WriteCombination(probe, probeId, "My", component == "My" ? (sign * 40).ToString() : "0");
                var state = ConcreteQuickSection.Calculate(probe).ServiceState!;
                var stress = state.tensioni_barre;
                double Coordinate(int i) => component == "Mx" ? bars[i].Y : bars[i].X;
                int max = Enumerable.Range(0, bars.Count).MaxBy(Coordinate), min = Enumerable.Range(0, bars.Count).MinBy(Coordinate);
                double expected = sign * (component == "Mx" ? -1 : 1);
                var maxVertex = state.ConcreteVertices.MaxBy(v => component == "Mx" ? v.Y : v.X)!;
                var minVertex = state.ConcreteVertices.MinBy(v => component == "Mx" ? v.Y : v.X)!;
                // Bars can all be in tension when the compressed zone lies outside their cage.
                expect.That((stress[max] - stress[min]) * expected > 0
                    && maxVertex.Strain * expected > 0 && minVertex.Strain * expected < 0,
                    $"Azioni 3D: {component} {sign:+0;-0}, fibre tese/compresse coerenti con il motore e gli assi geometrici");
            }
        ConcreteQuickShear.WriteCombination(data, shearId, "Vx", "errato");
        var invalid = ConcreteQuickActions.Read(data, true, shearId);
        expect.That(invalid.Actions.Count == 0 && invalid.Message.Contains("Vx"), "Azioni 3D: dato invalido non rappresentato come forza nulla");
        ConcreteQuickShear.WriteCombination(data, shearId, "Vx", "-75");
        using var sheet = open(data);
        await sheet.PendingCalculation;
        string before = sheet.Data.ToJsonString(), chart = sheet.Chart;
        sheet.Display = "Azioni 3D"; sheet.ActionsSource = "Taglio";
        expect.That(sheet.ShowsActions && !sheet.ShowsSection && !sheet.ShowsChart && sheet.Chart == chart
            && sheet.ActionsScene.Actions.Single(a => a.Name == "Vx").Value == -75 && sheet.Data.ToJsonString() == before,
            "Azioni 3D: vista e sorgente non modificano documento o grafico di calcolo");
        sheet.IsReadOnly = true; sheet.ActionsSource = "Momento"; sheet.Display = "Sezione"; sheet.Display = "Azioni 3D";
        expect.That(sheet.ShowsActions && sheet.ActionsScene.Actions.Single(a => a.Name == "N").Value == -321.5 && sheet.Data.ToJsonString() == before,
            "Azioni 3D: navigazione disponibile anche in sola lettura");
    }
}
