using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Calculations;
using Anthea.Presentation.Modules.ConcreteSection;

namespace Anthea.Presentation.Tests;

/// <summary>
/// The reinforcement design of the RC section (F5.10 S8): the steps of the tab 08 driven on <see cref="ConcreteDesignViewModel"/> as the historic
/// trace drives its controls, the part «[armature]» of the state in the format of the historic trace (tests/Desktop/ConcreteSectionHistoricState.cs,
/// TraceDesign: the times of the search left out), and the checks beyond the traces.
/// </summary>
internal static partial class ConcreteSectionSheetChecks
{
    private sealed partial class Side
    {
        /// <summary>A button of the tab 08 or the selection of an alternative; null when the step is not one.</summary>
        private (string? Message, bool Own)? ApplyDesignStep(ConcreteSectionStep step)
        {
            var design = Sheet.Design;
            if (step.Kind == ConcreteSectionStep.Button && step.Target == "ricerca")
            {
                CommunityToolkit.Mvvm.Input.IRelayCommand command = step.Key switch
                {
                    "Calcola armature" => design.RunCommand,
                    "Ferma ricerca" => design.StopCommand,
                    "Applica configurazione" => design.ApplyCommand,
                    _ => throw new InvalidOperationException("Pulsante non trovato: " + step.Key)
                };
                // «Calcola armature» starts the search on a worker thread; the trace waits for its end with the next «attendi».
                if (command.CanExecute(null)) command.Execute(null);
                return (null, true);
            }
            if (step.Kind == ConcreteSectionStep.Select && step.Target == "alternative")
            {
                if (step.Index < design.Ranking.Shown.Count) design.Ranking.SelectedIndex = step.Index;
                return (null, true);
            }
            return null;
        }

        /// <summary>The part «[armature]»: the three forms, the commands, the texts, the cards, the alternatives and the section of the proposal.</summary>
        private void TraceDesign(ConcreteSectionBlock block)
        {
            var design = Sheet.Design;
            block.Form(FormState("ricerca", design.Form)); block.Form(FormState("limiti", design.Limits)); block.Form(FormState("mostra", design.Shown));
            static string Button(string text, bool enabled) => text + (enabled ? "" : "(spento)");
            block.Add("ricerca comandi " + string.Join(" ", Button("Calcola armature", design.RunCommand.CanExecute(null)), Button("Ferma ricerca", design.StopCommand.CanExecute(null)),
                Button("Applica configurazione", design.ApplyCommand.CanExecute(null))));
            block.Text("ricerca stato", ConcreteSectionTrace.WithoutTimes(design.Status));
            block.Text("ricerca proposta", design.Description);
            block.Text("ricerca ambito", design.Scope);
            block.Add(Cards("ricerca-metriche", design.Metrics).Line());
            block.Add(Cards("ricerca-verifiche", design.Checks).Line());
            block.Text("ricerca scarti", ConcreteSectionTrace.WithoutTimes(design.Diagnostics));
            foreach (string line in TableState("alternative", design.Ranking).Lines()) block.Add(line);
            block.Add("ricerca anteprima " + Viewport(design.Preview));
        }
    }

    private static async Task DesignAsync(string root, Expect expect)
    {
        using (var sheet = OpenSheet(ConcreteSectionScenarios.FixtureData(root, "default")))
        {
            await IdleAsync(sheet);
            var design = sheet.Design;
            expect.That(design.Status == ConcreteDesignPresentation.Idle && design.RunCommand.CanExecute(null) && !design.StopCommand.CanExecute(null) && !design.ApplyCommand.CanExecute(null)
                && design.Form["n_superiori"].IsVisible && !design.Form["n_circolari"].IsVisible && !design.Form["diametri_ortogonali"].IsVisible,
                "Ricerca all'apertura: campi della sezione rettangolare, solo «Calcola armature»");
            // The fields follow every Modified of the sheet: the element of the detailing shows the orthogonal bars.
            sheet.Detailing.Element["elemento"].SelectedChoice = "Parete";
            expect.That(design.Form["diametri_ortogonali"].IsVisible && design.Form["passi_ortogonali"].IsVisible, "Parete nei dettagli: barre ortogonali della ricerca mostrate");
            // A search refused before it starts.
            design.Form["diametri"].EditText = "abc";
            await design.RunCommand.ExecuteAsync(null);
            expect.That(design.Status.StartsWith("Ricerca non avviata: ", StringComparison.Ordinal) && design.Description == ConcreteDesignPresentation.CompleteTheData
                && !design.IsRunning && design.RunCommand.CanExecute(null), "Elenco non valido: ricerca non avviata, comando di nuovo disponibile");
            // The fixture default has no exposure for the durability cover: the search stops before it starts.
            design.Form["diametri"].EditText = "20";
            await design.RunCommand.ExecuteAsync(null);
            expect.That(design.Status.StartsWith("Ricerca non avviata: ", StringComparison.Ordinal) && !design.IsStale, "Esposizione mancante: ricerca non avviata · " + design.Status);
            // Reload: a new panel; the search of the previous one is not published.
            var before = sheet.Design;
            sheet.Reload((JsonObject)sheet.Data.DeepClone());
            expect.That(!ReferenceEquals(before, sheet.Design) && sheet.Design.Status == ConcreteDesignPresentation.Idle && sheet.Design.Ranking.Rows.Count == 0, "Reload: pannello della ricerca nuovo");
            await IdleAsync(sheet);
        }

        // The slab of the trace «armature/ricerca-armature» with the same changes (cover, torsion reserve, lap zone, lap length) and one
        // short lists: its proposal, the data changed (stale), the search again and «Applica configurazione».
        using (var sheet = OpenSheet(ConcreteSectionScenarios.FixtureData(root, "soletta-dettagli-ricerca")))
        {
            await IdleAsync(sheet);
            var design = sheet.Design;
            sheet.Inputs.Geometry["cover_mm"].EditText = "60";
            sheet.Detailing.Bars["zona_sovrapposizione"].SelectedChoice = "No";
            sheet.Detailing.Anchorage["lunghezza"].EditText = "900";
            foreach (var (key, value) in new[] { ("riserva_torsione", "25"), ("diametri", "16"), ("n_superiori", "10"), ("n_inferiori", "6"), ("diametri_ortogonali", "10"),
                ("passi_ortogonali", "150") }) design.Form[key].EditText = value;
            design.Form["parallelismo"].SelectedChoice = "1";
            await IdleAsync(sheet);
            var run = design.RunCommand.ExecuteAsync(null);
            expect.That(design.IsRunning && design.StopCommand.CanExecute(null) && !design.RunCommand.CanExecute(null) && design.Description == ConcreteDesignPresentation.Searching,
                "Ricerca in corso: «Ferma ricerca» disponibile");
            await run; await IdleAsync(sheet);
            expect.That(!design.IsRunning && design.Status.StartsWith("Ricerca completata · ", StringComparison.Ordinal) && design.Ranking.Rows.Count >= 1
                && design.Ranking.SelectedIndex == 0 && design.ApplyCommand.CanExecute(null) && design.Preview.Section is not null && design.Metrics.Cards.Count >= 2
                && design.Diagnostics.StartsWith("Esaminate tutte le ", StringComparison.Ordinal), "Ricerca completata: proposta, anteprima, metriche · " + design.Status);
            sheet.Detailing.Anchorage["lunghezza"].EditText = "950";
            expect.That(design.IsStale && design.Status == ConcreteDesignPresentation.Stale && !design.ApplyCommand.CanExecute(null), "Dati cambiati: ricerca da ripetere, «Applica» spento");
            await IdleAsync(sheet);
            await design.RunCommand.ExecuteAsync(null); await IdleAsync(sheet);
            string description = design.Description; int modified = 0; sheet.Modified += (_, _) => modified++;
            design.ApplyCommand.Execute(null);
            expect.That(design.Status.StartsWith("Configurazione applicata.", StringComparison.Ordinal) && !design.ApplyCommand.CanExecute(null)
                && sheet.Settings["proposta_armature_applicata"].S("descrizione") == description && sheet.Settings["ancoraggi"].S("confinamento") == "Da verificare"
                && sheet.IsCalculationQueued && modified > 0 && sheet.Inputs.Reinforcement.Get("top_bar_count") == sheet.Input.S("top_bar_count")
                && sheet.Detailing.Retained.Get("barre_trattenute") == "Da confermare", "Applica configurazione: proposta nei dati, conferme da rifare, foglio invalidato, form riletti");
            await IdleAsync(sheet);
            // «Ferma ricerca»: the search is cancelled.
            run = design.RunCommand.ExecuteAsync(null);
            design.StopCommand.Execute(null);
            await run; await IdleAsync(sheet);
            expect.That(!design.IsRunning && (design.Status == ConcreteDesignPresentation.Interrupted || design.Status.StartsWith("Ricerca parziale", StringComparison.Ordinal)),
                "Ferma ricerca: ricerca interrotta · " + design.Status);
        }

        using (var sheet = OpenSheet(ConcreteSectionScenarios.FixtureData(root, "default"), readOnly: true))
        {
            await IdleAsync(sheet);
            var design = sheet.Design;
            expect.That(design.Form.IsReadOnly && design.Limits["slu"].IsInputReadOnly && !design.Shown["mostra"].CanChange && design.Ranking.IsReadOnly
                && !design.RunCommand.CanExecute(null) && !design.StopCommand.CanExecute(null) && !design.ApplyCommand.CanExecute(null) && !design.ShowTabCommand.CanExecute(0),
                "Sola lettura: form, alternative e comandi della ricerca protetti");
        }
    }
}
