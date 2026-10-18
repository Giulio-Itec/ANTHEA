using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Anthea.Presentation.Modules.HorizontalPile.Elastic;

namespace X.Desktop.Views.HorizontalPile;

/// <summary>
/// The dialog of the sheet of the reinforcement (F5.8): the historic <see cref="PileReinforcementDrawing"/> of the result (Q3), the zoom is state of the
/// view, «Esporta PNG…» builds the image as the historic dialog and the ViewModel writes it in the file chosen through its file dialog service.
/// </summary>
public partial class ElasticSheetView : UserControl
{
    /// <summary>Caption of the window, as the historic dialog.</summary>
    internal const string Title = "Armature del palo · elevazione e distinta";

    /// <summary>The zoom at the opening and the factor of the percentage.</summary>
    private const double InitialZoom = 75, Percent = 100;

    private readonly ElasticPileViewModel sheet;

    internal ElasticSheetView(ElasticPileViewModel sheet, JsonObject result)
    {
        this.sheet = sheet;
        InitializeComponent();
        Drawing.Set(result);
        Drawing.LayoutTransform = new ScaleTransform(InitialZoom / Percent, InitialZoom / Percent);
        SheetScroll.Content = Drawing;
    }

    /// <summary>The sheet of the reinforcement.</summary>
    internal PileReinforcementDrawing Drawing { get; } = new() { HorizontalAlignment = HorizontalAlignment.Left };

    private void Zoom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Drawing is null || ZoomText is null) return;
        double factor = Zoom.Value / Percent;
        Drawing.LayoutTransform = new ScaleTransform(factor, factor);
        ZoomText.Text = Zoom.Value.ToString("0", CultureInfo.CurrentCulture) + "%";
    }

    /// <summary>«Esporta PNG…»: the image of the sheet at twice its size, as the historic button.</summary>
    private void Export_Click(object sender, RoutedEventArgs e) =>
        sheet.SaveSheetImage(() => Drawing.Png((int)Math.Ceiling(Drawing.SheetSize.Width * 2), (int)Math.Ceiling(Drawing.SheetSize.Height * 2)));
}
