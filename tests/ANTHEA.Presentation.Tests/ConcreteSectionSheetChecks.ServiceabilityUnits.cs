using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Calculations;
using Anthea.Presentation.Modules.ConcreteSection;

namespace Anthea.Presentation.Tests;

/// <summary>
/// The serviceability families of the RC section (F5.10 S4) beyond the traces: the calculation held at the barrier of the engine (only the
/// current revision is shown, the stale stresses never), the read-only revision (contour, neutral axis, zones and families still usable; the
/// options and the check boxes of the values refused), the options common to the families, the bar clicked on the map, the check boxes of the
/// values, «Reologia…» and Reload.
/// </summary>
internal static partial class ConcreteSectionSheetChecks
{
    private static async Task ServiceabilityAsync(string root, Expect expect)
    {
        await ServiceabilityBarrierAsync(root, expect);
        await ServiceabilityReadOnlyAsync(root, expect);

        using (var sheet = OpenSheet(ConcreteSectionScenarios.FixtureData(root, "default")))
        {
            await IdleAsync(sheet);
            int modified = 0; sheet.Modified += (_, _) => modified++;
            var rara = sheet.StressPanel("SLE"); var frequent = sheet.StressPanel("SLE_FREQ");
            // F5.15, S4-c (F5-38): the first inner tab of the details from the opening (the historic tab control had -1 until it was laid out).
            expect.That(sheet.StressPanels.Select(p => p.Key).SequenceEqual(["SLE", "SLE_FREQ", "SLE_QP"]) && sheet.StressFamily == 0
                && sheet.StressFamilyLabels.SequenceEqual(sheet.StressPanels.Select(p => p.Label)) && rara.DetailTab == 0 && rara.VerificationTab == 1
                && rara.Detail == "Selezionare una combinazione" && rara.Map.Legend == "", "Famiglie SLE: ordine, famiglia mostrata, schede, dettaglio prima della prima vista");
            sheet.SelectedTab = ConcreteSheetViewModel.StressTab;
            expect.That(rara.IsShown && !frequent.IsShown && rara.Map.Stress is not null && rara.Bars.Rows.Count == 14 && rara.Concrete.Rows.Count == 4
                && rara.Map.Legend == "-20,73 … 0,00 MPa" && rara.Detail.StartsWith("Rara · Lineare\nCombo 1", StringComparison.Ordinal) && !rara.CrackTabVisible,
                "Rara mostrata: stato tensionale, barre, vertici, legenda e dettaglio della riga selezionata");
            rara.VerificationTab = 0;
            expect.That(rara.DetailTab == 0, "Schede dei dettagli mostrate la prima volta: la prima selezionata");
            rara.Map.ClickBar(2);
            expect.That(rara.DetailTab == 1 && rara.Bars.SelectedIndex == 2 && rara.Map.SelectedBar == "B03", "Barra scelta sulla mappa: scheda e riga delle barre");
            // The options common to the families: written into the other families and into sle_comuni; one Modified.
            modified = 0;
            rara.Form["esposizione"].SelectedChoice = "XC3";
            var settings = sheet.Settings;
            expect.That(settings["sle"]!["SLE_FREQ"].S("esposizione") == "XC3" && settings["sle"]!["SLE_QP"].S("esposizione") == "XC3" && settings["sle_comuni"].S("esposizione") == "XC3"
                && frequent.Form.Get("esposizione") == "XC3" && modified == 1 && sheet.IsCalculationQueued, "Opzione comune copiata nelle altre famiglie e in sle_comuni, ricalcolo in coda");
            await IdleAsync(sheet);
            // F5.15, C5·S4 (F5-36): the other families show the text of the family changed, not the number as written in the data.
            rara.Form["phi"].EditText = "2";
            expect.That(rara.Form.Get("n_armature") == "17,61" && frequent.Form.Get("n_armature") == "17,61" && settings["sle"]!["SLE_FREQ"].S("n_armature") == settings["sle"]!["SLE"].S("n_armature")
                && rara.Detail == "Tensioni da calcolare" && rara.Map.Stress is null && rara.Map.Legend == "", "φ: n calcolato, mostrato formattato nella famiglia e nelle altre");
            rara.Form["phi"].EditText = "abc";
            // The error belongs to φ, the value typed; the other families show φ as written and read the shared error with it.
            expect.That(rara.Form["phi"].IsNoteVisible && frequent.Form["phi"].IsNoteVisible && rara.Form["n_armature"].Note is null && rara.Form.Get("n_armature") == "",
                "Errore dell'omogeneizzazione: avviso di φ in tutte le famiglie, n vuoto");
            rara.Form["phi"].EditText = "0";
            await IdleAsync(sheet);
            // The check boxes of the values and the contour.
            var cables = rara.ValueOptions.Single(o => o.Key == "testi_trefoli");
            modified = 0;
            rara.ValueOptions[0].IsChecked = true;
            expect.That(!cables.IsShown && rara.Options.B("testi_barre") && rara.Map.BarValues && modified == 1, "«σ/ε barre» scritta con Modified; «σ/ε trefoli» nascosta senza cavi");
            rara.Contour = "Barre · tasso resistenza";
            expect.That(rara.Options.S("contour") == "Barre · tasso resistenza" && rara.Map.Legend == Anthea.Application.Presentation.UtilizationColors.Legend && modified == 2,
                "Contorno scritto con Modified, legenda dei tassi");
            rara.Contour = "Contorno sconosciuto";
            expect.That(rara.Options.S("contour") == "Barre · tasso resistenza" && modified == 2, "Contorno non previsto rifiutato");
            // A family not shown keeps the legend of its last drawing.
            sheet.StressFamily = 1;
            expect.That(frequent.IsShown && !rara.IsShown && rara.Map.Legend == Anthea.Application.Presentation.UtilizationColors.Legend, "Famiglia nascosta: legenda del suo ultimo disegno");
            double? strength = null; sheet.RheologyRequested += (_, value) => strength = value;
            frequent.RheologyCommand.Execute(null);
            expect.That(strength == sheet.Inputs.RheologyStrength, "«Reologia del calcestruzzo…» dalla scheda SLE");
            // Reload: new families on the data opened again.
            var panels = sheet.StressPanels;
            sheet.Reload((JsonObject)sheet.Data.DeepClone());
            expect.That(!panels.Intersect(sheet.StressPanels).Any() && sheet.StressFamily == 0 && sheet.StressPanel("SLE").DetailTab == 0
                && sheet.StressPanel("SLE").Contour == "Barre · tasso resistenza" && sheet.StressPanel("SLE").Map.BarValues, "Reload: famiglie nuove sulle opzioni salvate");
            await IdleAsync(sheet);
            expect.That(sheet.StressPanel("SLE").IsShown && sheet.StressPanel("SLE").Map.Stress is not null, "Reload: la famiglia mostrata riceve il calcolo dell'apertura");
        }
    }

    // The calculation held at the barrier of the engine: a change during the calculation supersedes it, and the family shows only the stresses of
    // the current revision.
    private static async Task ServiceabilityBarrierAsync(string root, Expect expect)
    {
        var session = new ConcreteAnalysisSession();
        using var sheet = OpenSheet(ConcreteSectionScenarios.FixtureData(root, "default"), session: session);
        await IdleAsync(sheet);
        sheet.SelectedTab = ConcreteSheetViewModel.StressTab;
        var rara = sheet.StressPanel("SLE");
        var before = rara.Map.Stress;
        var gate = (SemaphoreSlim)typeof(ConcreteAnalysisSession).GetField("batches", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(session)!;
        await gate.WaitAsync();
        try
        {
            rara.Form["phi"].EditText = "1";
            await EnteredAsync(sheet);
            expect.That(rara.Map.Stress is null && rara.Detail == "Tensioni da calcolare" && sheet.ActionRowSets["SLE"][0].Values.S("stress_status") == "Da calcolare",
                "Calcolo trattenuto: famiglia senza stato tensionale, righe da calcolare");
            rara.Form["phi"].EditText = "2";
            gate.Release(); gate = null!;
            await IdleAsync(sheet);
        }
        finally { gate?.Release(); }
        // The publication writes the detail with the culture of the UI thread (behaviour 1 of S3): «2,00» or «2.00».
        expect.That(rara.Map.Stress is not null && !ReferenceEquals(rara.Map.Stress, before) && rara.Detail.Contains(" · φ = 2", StringComparison.Ordinal)
            && sheet.Settings["sle"]!["SLE"].S("phi") == "2"
            && sheet.ActionRowSets["SLE"][0].Values.S("stress_status") != "Da calcolare", "Dopo la barriera: solo le tensioni della revisione corrente (φ = 2)");
    }

    private static async Task EnteredAsync(ConcreteSheetViewModel sheet)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (!sheet.IsBusy || sheet.IsCalculationQueued)
        {
            await Task.Yield(); await Task.Delay(10);
            if (watch.Elapsed > Expect.Deadline) throw new TimeoutException("Nessun calcolo avviato.");
        }
    }

    private static async Task ServiceabilityReadOnlyAsync(string root, Expect expect)
    {
        using var sheet = OpenSheet(ConcreteSectionScenarios.FixtureData(root, "sle-fessurazione-assi"), readOnly: true);
        await IdleAsync(sheet);
        int modified = 0; sheet.Modified += (_, _) => modified++;
        sheet.SelectedTab = ConcreteSheetViewModel.StressTab;
        modified = 0;
        var panel = sheet.StressPanel("SLE_QP");
        sheet.StressFamily = 2;
        expect.That(panel.IsShown && panel.Form.IsReadOnly && !panel.Form["esposizione"].CanChange && panel.Form["phi"].IsInputReadOnly && panel.Bars.IsReadOnly
            && panel.Concrete.IsReadOnly && panel.Table.Table.IsReadOnly && modified == 0, "Sola lettura: opzioni e tabelle protette, famiglia scelta senza scritture");
        string data = sheet.Data.ToJsonString();
        panel.ValueOptions[2].IsChecked = true;
        expect.That(!panel.ValueOptions[2].CanChange && !panel.Options.B("testi_cls") && sheet.Data.ToJsonString() == data && modified == 0, "Sola lettura: caselle dei valori rifiutate");
        panel.Contour = "Solo geometria"; panel.NeutralAxis = !panel.NeutralAxis;
        expect.That(panel.Options.S("contour") == "Solo geometria" && modified == 2 && panel.Map.Legend == "", "Sola lettura: contorno e asse neutro scritti con Modified (storico, C5)");
        int zones = panel.Regions.Count;
        if (zones > 1) panel.SelectedRegion = 1;
        expect.That(zones > 0 && panel.Map.EffectiveRegion == panel.Regions[panel.SelectedRegion] && modified == 2, "Sola lettura: zona efficace scelta senza scritture");
    }
}
