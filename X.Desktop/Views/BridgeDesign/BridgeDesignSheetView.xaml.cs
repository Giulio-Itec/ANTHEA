using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Anthea.Application.BridgeDesign;
using Anthea.Application.Presentation;
using Anthea.Presentation.Modules.BridgeDesign;

namespace X.Desktop.Views.BridgeDesign;

/// <summary>
/// View of <see cref="BridgeDesignSheetViewModel"/> (F5.12). The code-behind does only what decision Q3 allows and what is state of the view:
/// <list type="bullet">
/// <item>the technical drawings: the historic <see cref="BridgeDesignDrawing"/> of the card receives <see cref="BridgeDesignSheetViewModel.DrawingData"/>,
/// <see cref="BridgeDesignSheetViewModel.DrawingResult"/> and <see cref="BridgeDesignSheetViewModel.SectionView"/>, the one of the preview the solution
/// (<see cref="BridgeDesignSheetViewModel.SolutionData"/>, <see cref="BridgeDesignSheetViewModel.SolutionResult"/>,
/// <see cref="BridgeDesignSheetViewModel.SolutionSection"/>), the two historic <see cref="BridgeOptimizationPlot"/> the data of the search
/// (<see cref="BridgeDesignSheetViewModel.Cloud"/>, <see cref="BridgeDesignSheetViewModel.Trace"/>, the rank, the variable, the filter, Pareto);
/// a click on a point of the cloud selects its solution through the sheet;</item>
/// <item>the layout of the two widths of the historic view: below 1100 the drawing under the inputs and the metrics in two columns below 850;
/// below 1120 the preview under the choices of the search and the cloud under the explored variables;</item>
/// <item>the locks and the limits of the search, written through the sheet (<see cref="BridgeDesignSheetViewModel.SetLock"/>,
/// <see cref="BridgeDesignSheetViewModel.SetLimit"/>, <see cref="BridgeDesignSheetViewModel.LeaveLimit"/>), whose dictionaries the boxes show;</item>
/// <item>«Sezioni e quote» and «Applica soluzione selezionata» bring the results into view; «Stampa scheda» prints the page of the result with the
/// window of WPF (decision BD8: <see cref="BridgeDesignSheetViewModel.PrintLines"/> laid out by <see cref="BridgeDesignPrintPage"/>).</item>
/// </list>
/// No calculation and no write of the document.
/// </summary>
public partial class BridgeDesignSheetView : UserControl
{
    /// <summary>The note under the ranking of the search.</summary>
    public const string SearchScope = BridgeDesignModel.OptimizationScope + " Il motore non è equivalente al sito di riferimento; la graduatoria è interna al modello ANTHEA.";

    /// <summary>The items of the filter of the cloud: every typology, then each one.</summary>
    public static IReadOnlyList<string> FilterItems => BridgeDesignOptimizationPresentation.Filters();

    private BridgeDesignSheetViewModel? sheet;

    /// <summary>The drawing of the card (Drawing of the historic view): 400 high, at least 300 wide.</summary>
    internal BridgeDesignDrawing Drawing { get; } = new() { Height = 400, MinWidth = 300 };

    /// <summary>The preview of the search (OptimizationPreview of the historic view).</summary>
    internal BridgeDesignDrawing Preview { get; } = new() { Height = 265 };

    /// <summary>The cloud of the solutions (OptimizationCloud of the historic view).</summary>
    internal BridgeOptimizationPlot CloudPlot { get; } = new() { Height = 310, Scatter = true };

    /// <summary>The explored variables (OptimizationTrace of the historic view), on the third variable at the opening.</summary>
    internal BridgeOptimizationPlot TracePlot { get; } = new() { Height = 310, Variable = 2 };

    /// <summary>Shows the window of the printer: modal, as the historic one; the checks answer it without a window.</summary>
    internal Func<PrintDialog, bool?> ShowPrintDialog { get; set; } = dialog => dialog.ShowDialog();

    /// <summary>Prints the page laid out (the checks keep it).</summary>
    internal Action<PrintDialog, FrameworkElement> PrintVisual { get; set; } = (dialog, page) => dialog.PrintVisual(page, "ANTHEA Bridge Design");

    public BridgeDesignSheetView()
    {
        InitializeComponent();
        DrawingHost.Content = Drawing; PreviewHost.Content = Preview; CloudHost.Content = CloudPlot; TraceHost.Content = TracePlot;
        CloudPlot.Selected += rank =>
        {
            if (sheet is null) return;
            sheet.SelectSolution(rank - 1);
            SelectionText.BringIntoView();
        };
        SizeChanged += (_, _) => Layout();
        DataContextChanged += (_, _) => Attach(DataContext as BridgeDesignSheetViewModel);
        Loaded += (_, _) => Attach(DataContext as BridgeDesignSheetViewModel);
    }

    /// <summary>The lock boxes of «Mantieni costante» by key.</summary>
    internal IEnumerable<CheckBox> LockBoxes => LockPanel.Children.OfType<CheckBox>();

    private void Attach(BridgeDesignSheetViewModel? next)
    {
        if (ReferenceEquals(sheet, next)) return;
        if (sheet is not null) sheet.PropertyChanged -= OnSheetChanged;
        sheet = next;
        if (sheet is null) return;
        sheet.PropertyChanged += OnSheetChanged;
        Draw(); DrawPreview(); DrawCloud(); DrawTrace();
    }

    private void OnSheetChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(BridgeDesignSheetViewModel.DrawingData) or nameof(BridgeDesignSheetViewModel.DrawingResult) or nameof(BridgeDesignSheetViewModel.SectionView): Draw(); break;
            case nameof(BridgeDesignSheetViewModel.SolutionData) or nameof(BridgeDesignSheetViewModel.SolutionResult) or nameof(BridgeDesignSheetViewModel.SolutionSection): DrawPreview(); break;
            case nameof(BridgeDesignSheetViewModel.Cloud): DrawCloud(); break;
            case nameof(BridgeDesignSheetViewModel.CloudRank) or nameof(BridgeDesignSheetViewModel.FamilyFilter) or nameof(BridgeDesignSheetViewModel.ParetoOnly): MarkCloud(); break;
            case nameof(BridgeDesignSheetViewModel.Trace): DrawTrace(); break;
            case nameof(BridgeDesignSheetViewModel.Variable): TracePlot.Variable = sheet!.Variable; TracePlot.InvalidateVisual(); break;
        }
    }

    // ---------------------------------------------------------------- drawings (Q3)

    /// <summary>The drawing of the card, as the historic view sets it at every publication and at «Prospetto» and «Sezione».</summary>
    private void Draw()
    {
        if (sheet is null) return;
        Drawing.Data = sheet.DrawingData; Drawing.Result = sheet.DrawingResult; Drawing.Section = sheet.SectionView;
        Drawing.InvalidateVisual();
    }

    private void DrawPreview()
    {
        if (sheet is null) return;
        Preview.Data = sheet.SolutionData; Preview.Result = sheet.SolutionResult; Preview.Section = sheet.SolutionSection;
        Preview.InvalidateVisual();
    }

    /// <summary>The cloud: the data of the sheet (SetData clears the rank), then the rank, the filter and Pareto.</summary>
    private void DrawCloud()
    {
        if (sheet is null) return;
        var cloud = sheet.Cloud;
        CloudPlot.SetData([.. cloud.Trials], [.. cloud.Solutions], cloud.Baseline);
        MarkCloud();
    }

    private void MarkCloud()
    {
        if (sheet is null) return;
        CloudPlot.SelectedRank = sheet.CloudRank; CloudPlot.FamilyFilter = sheet.FamilyFilter; CloudPlot.ParetoOnly = sheet.ParetoOnly;
        CloudPlot.InvalidateVisual();
    }

    private void DrawTrace()
    {
        if (sheet is null) return;
        var trace = sheet.Trace;
        TracePlot.SetData([.. trace.Trials], [.. trace.Solutions], trace.Baseline);
        TracePlot.Variable = sheet.Variable;
        TracePlot.InvalidateVisual();
    }

    // ---------------------------------------------------------------- layout

    /// <summary>The two widths of the historic view (SizeChanged of BridgeDesignWorkspace and Layout of its page of the search).</summary>
    private void Layout()
    {
        bool compact = ActualWidth < 1100;
        Upper.ColumnDefinitions[0].Width = compact ? new GridLength(1, GridUnitType.Star) : new GridLength(375);
        Upper.ColumnDefinitions[1].Width = compact ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        Grid.SetColumn(DrawingCard, compact ? 0 : 1); Grid.SetRow(DrawingCard, compact ? 1 : 0);
        InputCard.Margin = compact ? new Thickness(0, 0, 0, 12) : new Thickness(0, 0, 12, 0);
        InputTabs.MinHeight = compact ? 330 : 470;
        MetricsGrid.Columns = ActualWidth < 850 ? 2 : 4;
        bool narrow = ActualWidth < 1120;
        SearchUpper.ColumnDefinitions[0].Width = narrow ? new GridLength(1, GridUnitType.Star) : new GridLength(335);
        SearchUpper.ColumnDefinitions[1].Width = narrow ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        Grid.SetColumn(PreviewCard, narrow ? 0 : 1); Grid.SetRow(PreviewCard, narrow ? 1 : 0);
        SetupCard.Margin = new Thickness(0, 0, narrow ? 0 : 12, 12);
        Charts.ColumnDefinitions[1].Width = narrow ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        Grid.SetColumn(CloudCard, narrow ? 0 : 1); Grid.SetRow(CloudCard, narrow ? 1 : 0);
        TraceCard.Margin = new Thickness(0, 0, narrow ? 0 : 12, 12);
    }

    /// <summary>«Sezioni e quote» and «Applica soluzione selezionata»: the results brought into view once the sheet has chosen the tab.</summary>
    private void ShowOutputs_Click(object sender, RoutedEventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => OutputTabs.BringIntoView()));

    // ---------------------------------------------------------------- the search

    /// <summary>A lock of «Mantieni costante» clicked: the sheet sets it (and the lock that goes with it); the boxes show the locks of the sheet.</summary>
    private void Lock_Changed(object sender, RoutedEventArgs e)
    {
        if (sheet is null || sender is not CheckBox { Tag: string key } box) return;
        sheet.SetLock(key, box.IsChecked == true);
        if (sheet.Locks.TryGetValue(key, out bool value) && box.IsChecked != value) box.SetCurrentValue(CheckBox.IsCheckedProperty, value);
    }

    /// <summary>A limit typed: the sheet writes it (a change clears the search, except «Mostra le prime N»).</summary>
    private void Limit_Changed(object sender, TextChangedEventArgs e)
    {
        if (sheet is null || sender is not TextBox { Tag: string key } box) return;
        sheet.SetLimit(key, box.Text);
    }

    /// <summary>Leaving a limit: «Mostra le prime N» shows the ranking again.</summary>
    private void Limit_Left(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sheet is null || sender is not TextBox { Tag: string key }) return;
        sheet.LeaveLimit(key);
    }

    /// <summary>The limit box of the search with this key («campate-min» … «prime»).</summary>
    internal TextBox LimitBox(string key) => key == "prime" ? TopBox
        : Logical<TextBox>(SetupCard).Single(t => Equals(t.Tag, key));

    private static IEnumerable<T> Logical<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T found) yield return found;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var item in Logical<T>(child)) yield return item;
    }

    // ---------------------------------------------------------------- «Stampa scheda» (BD8)

    /// <summary>The page printed by «Stampa scheda» on the printable area, null without a result.</summary>
    internal FrameworkElement? PrintPage(double width, double height) =>
        sheet?.Calculation is { } result && sheet.PrintLines is { } lines ? BridgeDesignPrintPage.Create(sheet.Data, result, lines, width, height) : null;

    /// <summary>«Stampa scheda»: with a result, the window of the printer, then the page in the light appearance (as the historic view).</summary>
    private void Print_Click(object sender, RoutedEventArgs e)
    {
        if (sheet?.Calculation is null) return;
        var dialog = new PrintDialog();
        if (ShowPrintDialog(dialog) != true) return;
        var page = PrintPage(dialog.PrintableAreaWidth, dialog.PrintableAreaHeight);
        if (page is null) return;
        using (Appearance.Document(page)) PrintVisual(dialog, page);
    }
}
