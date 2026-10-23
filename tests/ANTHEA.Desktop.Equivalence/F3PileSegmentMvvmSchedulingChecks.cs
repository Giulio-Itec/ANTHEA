using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Anthea.Application.Presentation;
using Anthea.Presentation.Modules.HorizontalPile;
using X.Core;
using X.Desktop.Views.HorizontalPile;

namespace X.Desktop;

/// <summary>
/// f5-pass-orizzontale: the probe of ui/f3-scheduling on the design of a segment of the elastic response with the MVVM view, in place of
/// PileReinforcementEditor.CheckSegmentProposals (F3PileSegmentChecks.cs): the horizontal pile opened by the factory of the bench in
/// MvvmSheetEditor, «Dimensiona…» of the first segment (<see cref="Anthea.Presentation.Modules.HorizontalPile.Elastic.PileReinforcementViewModel.Design"/>)
/// shown in its view (<see cref="PileSegmentDesignView"/>) in a window off screen, closed as «Dimensiona…» of PileReinforcementView closes it.
/// The same fixture, the same catalogues, the same interception of the first «Disposizione …» of the search and the same expectations as the
/// historic probe: the document changed during the search (a proposal of other data is refused and the revision is reported), or the window
/// closed during the search (the status is no longer updated); in both cases the proposal cannot be applied and the document is unchanged.
/// </summary>
internal static class F3PileSegmentMvvmSchedulingChecks
{
    /// <summary>True when the bench of this process opens the horizontal pile with the MVVM view.</summary>
    internal static bool Active => ModuleHostBench.Implementations(Environment.GetCommandLineArgs()).For(PaloOrizzontale.Module) == SheetImplementation.Mvvm;

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
            using var editor = ModuleHostBench.SheetEditors().Open(PaloOrizzontale.Module, data);
            var sheet = (HorizontalPileSheetViewModel)((MvvmSheetEditor)editor).Sheet;
            await sheet.Elastic.PendingCalculation.WaitAsync(TimeSpan.FromSeconds(40));
            if (sheet.Elastic.Result is null) { await sheet.Elastic.CalculateAsync().WaitAsync(TimeSpan.FromSeconds(40)); }
            var document = editor.Data;
            var design = sheet.Elastic.Reinforcement.Design(0) ?? throw new InvalidOperationException("Dimensionamento del tratto non disponibile: " + sheet.Elastic.Reinforcement.Message);
            var view = new PileSegmentDesignView { DataContext = design };
            var window = new Window
            {
                Title = PileSegmentDesignView.Title, Content = view, Width = 740, Height = 650, Left = -32000, Top = -32000,
                WindowStartupLocation = WindowStartupLocation.Manual, ShowActivated = false, ShowInTaskbar = false
            };
            // As Design_Click of PileReinforcementView: closing the dialog ends the search.
            window.Closed += (_, _) => design.Close();
            window.Show(); window.UpdateLayout();
            view.Counts.Text = "16"; view.Diameters.Text = "24"; view.Links.Text = "10"; view.Spacings.Text = "150";
            var status = view.StatusText;
            bool intercepted = false; string expected = "", atClose = "";
            var textProperty = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));
            EventHandler intercept = (_, _) =>
            {
                if (intercepted || !status.Text.StartsWith("Disposizione ")) return;
                intercepted = true;
                if (close) { atClose = status.Text; window.Close(); }
                else document["generali"]!["azione_orizzontale"] = 2;
                expected = document.ToJsonString();
            };
            textProperty.AddValueChanged(status, intercept);
            try
            {
                // A click of «Trova proposta»: OnClick runs the Click handlers and the command of the button.
                typeof(ButtonBase).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(view.FindButton, null);
                await design.Search.WaitAsync(TimeSpan.FromSeconds(40));
                Check(intercepted, "Prova non ha raggiunto il calcolo della proposta");
                Check(document.ToJsonString() == expected && !view.ApplyButton.IsEnabled, "Proposta tardiva applicabile o documento alterato");
                Check(close ? status.Text == atClose : status.Text.Contains("Input cambiati"), close ? "Finestra chiusa aggiornata dal calcolo" : "Revisione cambiata non segnalata");
            }
            finally { textProperty.RemoveValueChanged(status, intercept); window.Close(); }
        }
        await Attempt(false); await Attempt(true);
        return checks;
    }
}
