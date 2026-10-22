using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Calculations;
using Anthea.Presentation.Modules.ConcreteSection;

namespace Anthea.Presentation.Tests;

/// <summary>
/// The detailing and the anchorages of the RC section (F5.10 S6): the part «[dettagli]» of the state written from
/// <see cref="ConcreteDetailingViewModel"/> in the format of the historic trace (tests/Desktop/ConcreteSectionHistoricState.cs, TraceDetailing),
/// and the checks beyond the traces: the texts typed in the anchorage written by «Aggiorna verifiche ancoraggi», the aggregate shared with the
/// shear, the read-only revision, the fields that follow the element and the lap, Reload.
/// </summary>
internal static partial class ConcreteSectionSheetChecks
{
    private sealed partial class Side
    {
        // The forms of the detailing in the order of the historic view, the cover of the inputs, the anchorage, the summary, the topics, the
        // checks of the anchorage and the note of the durability.
        private void TraceDetailing(ConcreteSectionBlock block)
        {
            var detailing = Sheet.Detailing;
            for (int i = 0; i < detailing.Forms.Count; i++) block.Form(FormState("dettagli-" + i.ToString(CultureInfo.InvariantCulture), detailing.Forms[i]));
            block.Form(FormState("copriferro", detailing.CoverGeometry));
            block.Form(FormState("ancoraggi", detailing.Anchorage));
            block.Add(Cards("dettagli", detailing.Summary).Line());
            foreach (var topic in detailing.Topics) block.Add(Cards("dettagli-" + topic.Key, topic.Checks).Line());
            block.Add(Cards("ancoraggi", detailing.AnchorageChecks).Line());
            block.Text("durabilita", detailing.DurabilityNote);
        }
    }

    private static async Task DetailingAsync(string root, Expect expect)
    {
        using (var sheet = OpenSheet(ConcreteSectionScenarios.FixtureData(root, "default")))
        {
            await IdleAsync(sheet);
            var detailing = sheet.Detailing;
            int modified = 0; sheet.Modified += (_, _) => modified++;
            expect.That(detailing.Forms.Count == 5 && string.Join("|", detailing.Topics.Select(t => t.Key)) == "cover|bars|stirrups|anchors"
                && detailing.DurabilityNote == "Scegliere il tipo di elemento." && detailing.Summary.Cards.Count == 1 && !detailing.Bars["as_secondaria"].IsVisible
                && !detailing.Anchorage["percentuale"].IsVisible && detailing.CoverGeometry.Get("cover_mm") == sheet.Input.S("cover_mm"),
                "Dettagli all'apertura: cinque form, quattro temi, elemento da scegliere, campi della soletta e della sovrapposizione nascosti");
            // A text typed in the anchorage stays a draft while the field has the focus; «Aggiorna verifiche ancoraggi» writes it and calculates.
            var length = detailing.Anchorage["lunghezza"];
            length.IsFocused = true; length.EditText = "800";
            expect.That(sheet.Settings["ancoraggi"].S("lunghezza") == "" && modified == 0, "Lunghezza digitata: bozza del campo");
            detailing.UpdateAnchoragesCommand.Execute(null);
            length.IsFocused = false;
            expect.That(sheet.Settings["ancoraggi"].S("lunghezza") == "800" && modified == 1 && detailing.AnchorageChecks.Cards.Count > 0
                && detailing.AnchorageChecks.Cards[0].Heading.StartsWith("Ancoraggio rettilineo · Ø", StringComparison.Ordinal),
                "Aggiorna verifiche ancoraggi: lunghezza scritta, verifica dell'ancoraggio con un Modified");
            detailing.Anchorage["tipo"].SelectedChoice = "Sovrapposizione rettilinea";
            expect.That(detailing.Anchorage["percentuale"].IsVisible && detailing.Anchorage["posizione"].IsVisible && modified == 2, "Sovrapposizione: campi della giunzione mostrati");
            detailing.Element["elemento"].SelectedChoice = "Soletta piena";
            expect.That(detailing.Bars["as_secondaria"].IsVisible && detailing.Bars["zona_critica"].IsVisible && !detailing.Supports["ancoraggio_appoggi"].IsVisible
                && sheet.Settings["dettagli_costruttivi"].S("esposizione_sle") == detailing.Cover.Get("esposizione_sle"),
                "Soletta piena: barre ortogonali e zona critica mostrate, appoggi nascosti, esposizione condivisa");
            await IdleAsync(sheet);
            // The aggregate is shared with the shear: its change calculates the shear again.
            detailing.Cover["aggregato"].EditText = "25";
            expect.That(sheet.Settings["dettagli_costruttivi"].S("aggregato") == "25" && sheet.IsCalculationQueued && sheet.ShearRowCollection.All(r => r.Values.S("esito") == "—"),
                "Inerte massimo: taglio di nuovo da calcolare");
            await IdleAsync(sheet);
            detailing.ShowControlPanelCommand.Execute(null);
            expect.That(sheet.SelectedTab == 0, "Modifica nel pannello di controllo: scheda 01");
            // Reload: a new panel on the options saved.
            sheet.Reload((JsonObject)sheet.Data.DeepClone());
            expect.That(!ReferenceEquals(detailing, sheet.Detailing) && sheet.Detailing.Element.Get("elemento") == "Soletta piena" && sheet.Detailing.Anchorage.Get("lunghezza") == "800"
                && sheet.Detailing.Bars["as_secondaria"].IsVisible, "Reload: pannello dei dettagli nuovo sulle opzioni salvate");
            await IdleAsync(sheet);
        }

        using (var sheet = OpenSheet(ConcreteSectionScenarios.FixtureData(root, "circolare-cava"), readOnly: true))
        {
            await IdleAsync(sheet);
            var detailing = sheet.Detailing;
            expect.That(detailing.Forms.All(f => f.IsReadOnly) && !detailing.Element["elemento"].CanChange && detailing.Anchorage["lunghezza"].IsInputReadOnly
                && detailing.Stirrups.Values.IsReadOnly && !detailing.UpdateAnchoragesCommand.CanExecute(null) && !detailing.ShowControlPanelCommand.CanExecute(null),
                "Sola lettura: form dei dettagli e dell'ancoraggio protetti, comandi spenti");
        }
    }
}
