using System.ComponentModel;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Anthea.Application.Presentation;
namespace X.Desktop;

internal sealed partial class PileReinforcementEditor
{
    internal static async Task<int> CheckSegmentProposals(string repository)
    {
        int checks = 0;
        void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); checks++; }
        async Task Attempt(bool close)
        {
            var data = JsonNode.Parse(File.ReadAllText(Path.Combine(repository,
                "tests/fixtures/f3/inputs/geo_palo_orizzontale--single-soil-elastic.json")))!["data"]!.AsObject();
            HorizontalSheetPresentation.PrepareReport(data); data["generali"]!["azione_orizzontale"] = 1;
            data["elastico"]!["dettagli"]!["azioni_progetto"] = true; data["elastico"]!["dettagli"]!["taglio_confermato"] = true;
            var response = ElasticHorizontalPile.CalculateResponse(data);
            var editor = new PileReinforcementEditor(data, () => { }, () => response);
            var window = editor.CreateSegmentDesignWindow(0)!;
            window.Left = -10000; window.Top = -10000; window.ShowActivated = false; window.Show();
            var boxes = Ui.Descendants<TextBox>(window).ToArray();
            boxes[0].Text = "16"; boxes[1].Text = "24"; boxes[2].Text = "10"; boxes[3].Text = "150";
            var status = Ui.Descendants<TextBlock>(window).Single(t => t.Text.StartsWith("Ricerca nel catalogo"));
            var run = Ui.Descendants<Button>(window).Single(b => b.Content?.ToString() == "Trova proposta");
            var apply = Ui.Descendants<Button>(window).Single(b => b.Content?.ToString() == "Applica alla sezione principale");
            bool intercepted = false; string expected = "", atClose = "";
            var textProperty = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));
            EventHandler intercept = (_, _) =>
            {
                if (intercepted || !status.Text.StartsWith("Disposizione ")) return;
                intercepted = true;
                if (close) { atClose = status.Text; window.Close(); }
                else data["generali"]!["azione_orizzontale"] = 2;
                expected = data.ToJsonString();
            };
            textProperty.AddValueChanged(status, intercept);
            try
            {
                run.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await editor.segmentDesignTask.WaitAsync(TimeSpan.FromSeconds(40));
                Check(intercepted, "Prova non ha raggiunto il calcolo della proposta");
                Check(data.ToJsonString() == expected && !apply.IsEnabled, "Proposta tardiva applicabile o documento alterato");
                Check(close ? status.Text == atClose : status.Text.Contains("Input cambiati"), close ? "Finestra chiusa aggiornata dal calcolo" : "Revisione cambiata non segnalata");
            }
            finally { textProperty.RemoveValueChanged(status, intercept); window.Close(); }
        }
        await Attempt(false); await Attempt(true);
        return checks;
    }
}
