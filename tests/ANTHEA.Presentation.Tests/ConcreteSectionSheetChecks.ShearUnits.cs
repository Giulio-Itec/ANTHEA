using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Calculations;
using Anthea.Presentation.Modules.ConcreteSection;

namespace Anthea.Presentation.Tests;

/// <summary>
/// The shear and torsion of the RC section (F5.10 S5) beyond the traces: the calculation held at the barrier of the engine (the shear waits for
/// the batch and shows only the current revision), the read-only revision (forms, table and commands protected but the copy, the template and
/// the export), the links shown read-only (circular fields hidden for the sections of the wizard), the two Modified of the torsion, the model
/// with links that brings the links back, Reload.
/// </summary>
internal static partial class ConcreteSectionSheetChecks
{
    private static async Task ShearAsync(string root, Expect expect)
    {
        await ShearBarrierAsync(root, expect);

        using (var sheet = OpenSheet(ConcreteSectionScenarios.FixtureData(root, "circolare-cava"), readOnly: true))
        {
            await IdleAsync(sheet);
            var shear = sheet.Shear; var table = shear.Table;
            expect.That(shear.Form.IsReadOnly && !shear.Form["modello"].CanChange && shear.Torsion.IsReadOnly && shear.Torsion["cot_torsione"].IsInputReadOnly
                && shear.Stirrups.Values.IsReadOnly && table.Table.IsReadOnly && !table.AddCommand.CanExecute(null) && !table.RemoveCommand.CanExecute(null)
                && !table.PasteCommand.CanExecute(null) && !table.BrowseCommand.CanExecute(null) && !table.ImportCommand.CanExecute(null)
                && table.CopyCommand.CanExecute(null) && table.TemplateCommand.CanExecute(null) && table.ExportCommand.CanExecute(null),
                "Sola lettura: form, tabella e comandi protetti; copia, template ed esportazione disponibili");
            expect.That(shear.Stirrups.Scheme["tipo_staffa"].IsVisible && sheet.Detailing.Stirrups.Scheme["tipo_staffa"].IsVisible
                && shear.Stirrups.Scheme["rami_x"].IsVisible && shear.Stirrups.Values["transverse_spacing_mm"].IsReadOnly,
                "Staffe in sola lettura di una sezione circolare: campi della staffa circolare mostrati");
        }

        using (var sheet = OpenSheet(ConcreteSectionScenarios.FixtureData(root, ConcreteSectionScenarios.ExamplePrefix + "wizard-circolare-cava")))
        {
            await IdleAsync(sheet);
            var forms = new[] { sheet.Inputs.StirrupScheme, sheet.Shear.Stirrups.Scheme, sheet.Detailing.Stirrups.Scheme };
            expect.That(forms.All(f => !f["tipo_staffa"].IsVisible && !f["schema_interno"].IsVisible && !f["rami_interni"].IsVisible && !f["rotazione_staffa"].IsVisible && f["rami_x"].IsVisible),
                "Sezione da wizard: campi della staffa circolare nascosti anche nei form delle staffe in sola lettura del taglio e dei dettagli");
        }

        using (var sheet = OpenSheet(ConcreteSectionScenarios.FixtureData(root, "default")))
        {
            await IdleAsync(sheet);
            int modified = 0; sheet.Modified += (_, _) => modified++;
            var shear = sheet.Shear;
            // The calculation of the opening empties the detail of the hidden tab, which waits to be shown (behaviour kept, C5).
            expect.That(shear.DetailTab == 1 && shear.Detail == ConcreteShearPresentation.DetailPending && !shear.IsShown,
                "Taglio prima della prima vista: riepilogo delle verifiche, dettaglio in attesa della scheda");
            sheet.SelectedTab = ConcreteSheetViewModel.ShearTab;
            expect.That(shear.IsShown && shear.Drawing.Section is not null && shear.Detail == ConcreteShearPresentation.NoSelection
                && shear.Summary.StartsWith("Taglio: 0/0 combinazioni calcolate", StringComparison.Ordinal), "Taglio mostrato: sezione disegnata, nessuna combinazione");
            modified = 0;
            shear.Torsion["cot_torsione"].EditText = "2";
            expect.That(sheet.Settings["taglio"].S("cot_torsione") == "2" && modified == 2, "Torsione: scritta con due Modified (storico, C5)");
            // The model with links brings back the links absent in the inputs (the sheet is invalidated).
            sheet.Inputs.Stirrups["staffe_presenti"].SelectedChoice = "No";
            expect.That(sheet.Settings["taglio"].S("modello") == "Senza staffe" && shear.Form.Get("modello") == "Senza staffe", "Staffe assenti: modello senza staffe mostrato nel form del taglio");
            await IdleAsync(sheet);
            shear.Form["modello"].SelectedChoice = "Con staffe";
            expect.That(sheet.Input.S("staffe_presenti") == "Sì" && sheet.Inputs.Stirrups.Get("staffe_presenti") == "Sì" && shear.Form["alpha_x"].IsEnabled,
                "Modello con staffe: staffe di nuovo presenti, campi delle staffe abilitati");
            shear.Form["parametri"].SelectedChoice = "Manuali";
            expect.That(shear.Form["bw_x"].IsEnabled && !shear.Form["ancoraggio"].IsVisible && !shear.Form["asl_x"].IsEnabled, "Parametri manuali: bw e d modificabili, Asl solo senza staffe");
            await IdleAsync(sheet);
            // Reload: a new panel of the shear on the data opened again.
            var before = sheet.Shear;
            sheet.Reload((JsonObject)sheet.Data.DeepClone());
            expect.That(!ReferenceEquals(before, sheet.Shear) && sheet.Shear.Form.Get("parametri") == "Manuali" && sheet.Shear.Summary == "Taglio · da calcolare",
                "Reload: pannello del taglio nuovo sulle opzioni salvate");
            await IdleAsync(sheet);
        }
    }

    // The calculation held at the barrier of the engine: the shear waits for the batch (it is calculated after the stresses, on the UI thread, C2),
    // and a change during the calculation supersedes it.
    private static async Task ShearBarrierAsync(string root, Expect expect)
    {
        var session = new ConcreteAnalysisSession();
        using var sheet = OpenSheet(ConcreteSectionScenarios.FixtureData(root, "taglio-senza-staffe-manuali"), session: session);
        await IdleAsync(sheet);
        sheet.SelectedTab = ConcreteSheetViewModel.ShearTab;
        var shear = sheet.Shear;
        var gate = (SemaphoreSlim)typeof(ConcreteAnalysisSession).GetField("batches", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(session)!;
        await gate.WaitAsync();
        try
        {
            sheet.Inputs.Geometry["cover_mm"].EditText = "45";
            await EnteredAsync(sheet);
            // The summary of the checks is written again by the summaries of the sheet after the invalidation (no result yet).
            expect.That(shear.Summary == ConcreteShearPresentation.Pending && shear.Detail == ConcreteShearPresentation.DetailPending && !shear.Worst.Text.Contains("Governa:", StringComparison.Ordinal)
                && sheet.ShearRowCollection.All(r => r.Values.S("esito") == "—"), "Calcolo trattenuto: taglio in attesa del lotto");
            sheet.Inputs.Geometry["cover_mm"].EditText = "50";
            gate.Release(); gate = null!;
            await IdleAsync(sheet);
        }
        finally { gate?.Release(); }
        string rows = sheet.ShearRowCollection.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        // Every row calculated, or with its error (two rows of the fixture stop on their data).
        expect.That(shear.Summary.StartsWith("Taglio: ", StringComparison.Ordinal) && shear.Summary.Contains("/" + rows + " combinazioni calcolate", StringComparison.Ordinal) && sheet.Input.S("cover_mm") == "50"
            && sheet.ShearRowCollection.All(r => r.Values.S("esito") != "—"), "Dopo la barriera: taglio della revisione corrente · " + shear.Summary);
        expect.That(shear.Table.Table.Selected is { } selected && shear.Detail.StartsWith(selected.Values.S("nome") + " · ", StringComparison.Ordinal),
            "Dopo la barriera: dettaglio della riga selezionata · " + shear.Detail);
    }
}
