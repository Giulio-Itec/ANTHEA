using Anthea.Application.BridgeDesign;
using Anthea.Application.Presentation;
using System.Text;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;

internal sealed partial class BridgeDesignWorkspace
{
    private readonly ContentControl technical = new();
    private void ShowTechnical(BridgeDesignModel.Result r)
    {
        var panel = Ui.Stack(Ui.Text(BridgeDesignPresentation.TechnicalTitle(r), 17, true),
            Ui.Text(BridgeDesignPresentation.TechnicalNote, 12, color: Ui.Muted));
        var sectionTable = Table([.. BridgeDesignPresentation.TechnicalColumns], BridgeDesignPresentation.TechnicalRows(Data, r), 480);
        double[] columnWidths = [1.2, .7, .8, .5, .8, 3];
        for (int k = 0; k < columnWidths.Length; k++) sectionTable.Columns[k].Width = new DataGridLength(columnWidths[k], DataGridLengthUnitType.Star);
        panel.Children.Add(sectionTable);
        panel.Children.Add(Ui.Text(BridgeDesignPresentation.SpanTitle, 15, true));
        panel.Children.Add(Table([.. BridgeDesignPresentation.SpanColumns], BridgeDesignPresentation.SpanRows(r), 300));
        panel.Children.Add(Ui.Text(BridgeDesignPresentation.SupportTitle, 15, true));
        panel.Children.Add(Ui.Text(BridgeDesignPresentation.SupportNote, 12, color: Ui.Muted));
        panel.Children.Add(Table([.. BridgeDesignPresentation.SupportScheduleColumns], BridgeDesignPresentation.SupportScheduleRows(Data, r), 400));
        panel.Children.Add(Ui.Bar(Ui.Button(BridgeDesignPresentation.TechnicalCsvButton, () => Save("Sezioni e quote CSV|*.csv", "BridgeDesign_sezioni_quote.csv", filename =>
            Archivio.ScriviAtomico(filename, Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(BridgeDesignModel.TechnicalCsv(Data, Calculation!))).ToArray())), inspection: true)));
        technical.Content = Scroll(panel, 500);
    }
}
