using System.Windows;
using System.Windows.Controls;
namespace X.Desktop;

internal sealed partial class RetainingWallWorkspace
{
    internal static async Task<int> CheckParameterRevision()
    {
        int checks = 0;
        void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); checks++; }
        using var service = new GatedModuleService(RetainingWall.Module); service.Release();
        using var view = new RetainingWallWorkspace(RetainingWall.Defaults(), service);
        await view.CalculateAsync();
        async Task Attempt(bool close)
        {
            service.Arm();
            var window = view.CalculationParametersWindow(); window.Show();
            try
            {
                var row = Ui.Descendants<JsonGrid>(window).First().Rows.Single(r => r.Values.S("path") == "geometry/height");
                row["value"] = "3.3";
                Ui.Descendants<Button>(window).Single(b => b.Content?.ToString() == "Applica e ricalcola").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var apply = view.parameterApplyTask;
                await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(15));
                if (close) window.Close();
                else { view.Forms["geometry"].Set("height", "3.7"); view.Forms["geometry"].Commit(); }
                string expected = view.Data.ToJsonString();
                service.Release(); await apply.WaitAsync(TimeSpan.FromSeconds(20));
                Check(view.Data.ToJsonString() == expected, close ? "Parametri applicati dopo chiusura" : "Parametri sovrascrivono la revisione corrente");
                if (!close)
                {
                    Check(window.IsEnabled, "Finestra non recupera dal conflitto di revisione");
                    Check(Ui.Descendants<TextBlock>(window).Any(t => t.Text.Contains("Il foglio è cambiato")), "Conflitto non spiegato all’utente");
                }
            }
            finally { service.Release(); window.Close(); }
            await view.CalculateAsync();
        }
        await Attempt(false); await Attempt(true);
        return checks;
    }
}
