using Anthea.Calculations;
using Anthea.Presentation.Modules.ConcreteSection;

namespace Anthea.Presentation.Tests;

/// <summary>
/// The domain panels of the RC section (F5.10 S3) beyond the traces: the read-only revision (limit state, filter and bar of the drawing still
/// usable, the other options and the sliders of the sizes refused), the point picked in the 3D drawing, the windows «Scala…» (factors refused
/// and accepted, zoom) and «Adatta», the reset of the view of the 2D chart.
/// </summary>
internal static partial class ConcreteSectionSheetChecks
{
    /// <summary>
    /// The 3D domain of every F3 fixture as plain data, against tests/fixtures/f5-sezione/disegni/domini-3d.txt (decision C8: the 3D viewport
    /// stays WPF and is compared as data): opened, calculated, then the tabs shown as the drawings of the historic trace show them (control
    /// panel, serviceability, shear, 3D domain with its section σ/ε, 2D domain, M–χ), with the long traces.
    /// </summary>
    private static async Task DomainDataAsync(string root, string output, Expect expect)
    {
        var lines = new List<string>();
        foreach (string id in ConcreteSectionScenarios.Fixtures(root))
        {
            using var sheet = OpenSheet(ConcreteSectionScenarios.FixtureData(root, id));
            await IdleAsync(sheet);
            await sheet.CalculateAsync();
            await IdleAsync(sheet);
            foreach (int tab in new[] { 0, 3, 4, 1 }) { sheet.SelectedTab = tab; await IdleAsync(sheet); }
            sheet.Domain3D.InspectionTab = 1; await IdleAsync(sheet);
            foreach (int tab in new[] { 2, 6 }) { sheet.SelectedTab = tab; await IdleAsync(sheet); }
            lines.AddRange(ConcreteSectionSheetReader.View3DLines(sheet.Domain3D.View3D!).Select(line => id + " · " + line));
        }
        string folder = Path.Combine(output, "sezione-ca");
        ConcreteSectionTrace.Write(Path.Combine(folder, "domini-3d.txt"), lines);
        var expected = ConcreteSectionTrace.Read(Path.Combine(ConcreteSectionScenarios.Drawings(root), "domini-3d.txt"));
        string? difference = ConcreteSectionTrace.FirstDifference(expected, lines);
        expect.That(difference is null, "Domini 3D come dati diversi da quelli congelati: " + difference);
    }

    private static async Task DomainsAsync(string root, Expect expect)
    {
        using (var sheet = OpenSheet(ConcreteSectionScenarios.FixtureData(root, "circolare-cava"), readOnly: true))
        {
            await IdleAsync(sheet);
            int modified = 0; sheet.Modified += (_, _) => modified++;
            var panel = sheet.Domain3D;
            expect.That(panel.Form.IsReadOnly && panel.Form["stato"].CanChange && panel.Form["filtro"].CanChange && !panel.Form["criterio"].CanChange
                && panel.Form["angoli"].IsInputReadOnly && !panel.SectionOnActionCommand.CanExecute(null) && sheet.Domain2D.Form["filtro"].CanChange,
                "Sola lettura: stato e filtro usabili, le altre opzioni protette");
            double size = panel.ActionPointSize;
            panel.ActionPointSize = size + 3; panel.BarValues = true;
            expect.That(panel.ActionPointSize == size && !panel.BarValues && modified == 0, "Sola lettura: dimensioni dei punti e valori sulle barre rifiutati");
            panel.Transparency = 60; panel.DisplayOptions[4].IsChecked = true; panel.ForceCommand.Execute(null);
            expect.That(panel.Options.D("trasparenza") == 60 && panel.Options.B("colora_eta") && panel.Options.B("solo_selezionata") && modified == 3
                && panel.ForceText == "Forze: selezionata", "Sola lettura: trasparenza, caselle e forze della barra scritte con Modified (storico, C5)");
        }

        ConcreteDomainScaleDialogViewModel? shown = null;
        string? refused = null;
        var dialogs = new ScriptedDialogs()
            .Then(dialog =>
            {
                shown = (ConcreteDomainScaleDialogViewModel)dialog;
                shown.Form["scala_mx"].EditText = "9"; shown.ApplyCommand.Execute(null); refused = shown.Message;
                shown.Form["scala_mx"].EditText = "2"; shown.Form["zoom"].EditText = "150"; shown.Form["fit_azioni"].SelectedChoice = "Sì";
                shown.ApplyCommand.Execute(null);
            })
            .Then(dialog => ((ConcreteDomainScaleDialogViewModel)dialog).CancelCommand.Execute(null));
        using (var sheet = OpenSheet(ConcreteSectionScenarios.FixtureData(root, "default"), services: Scripted.Services(dialogs: dialogs)))
        {
            await IdleAsync(sheet);
            sheet.SelectedTab = 1;
            var panel = sheet.Domain3D;
            int modified = 0, fitted = 0, zoomed = 0; sheet.Modified += (_, _) => modified++;
            panel.FitRequested += (_, _) => fitted++; panel.ZoomRequested += (_, _) => zoomed++;
            string id = panel.Table.Table.Shown[0].Values.S("id");
            panel.Table.Table.SelectedIndex = -1;
            panel.View3D!.Pick(id);
            expect.That(panel.Table.Table.Selected?.Values.S("id") == id && panel.View3D.SelectedId == id, "Punto del dominio 3D scelto nel disegno: riga selezionata");
            await panel.ScaleCommand.ExecuteAsync(null);
            expect.That(refused == "Fattori ammessi 0,25–4; zoom 10–2000%." && shown!.Result && shown.Title == "Scala del dominio 3D",
                "«Scala…»: fattore fuori intervallo rifiutato, poi applicato");
            expect.That(panel.Options.D("scala_mx") == 2 && panel.View3D.Scale == (2, 1, 1) && panel.Options.B("fit_azioni") && panel.View3D.FitIncludesActions
                && panel.Zoom == 1.5 && fitted == 1 && zoomed == 1 && modified == 1, "«Scala…»: fattori, zoom e «Adatta anche le azioni» con Modified");
            await panel.ScaleCommand.ExecuteAsync(null);
            expect.That(panel.Options.D("scala_mx") == 2 && modified == 1, "«Scala…» annullata: nessuna modifica");
            panel.FitCommand.Execute(null);
            expect.That(panel.Options.D("scala_mx") == 1 && panel.Options.D("scala_x") == 1 && panel.View3D.Scale == (1, 1, 1) && fitted == 2 && modified == 2,
                "«Adatta»: fattori unitari, disegno adattato, Modified");
            sheet.Domain2D.Options["scala_x"] = 3;
            sheet.Domain2D.ChartViewReset();
            expect.That(sheet.Domain2D.Options.D("scala_x") == 1 && modified == 3, "Vista del grafico 2D ripristinata: fattori x e y unitari, Modified");
        }
    }
}
