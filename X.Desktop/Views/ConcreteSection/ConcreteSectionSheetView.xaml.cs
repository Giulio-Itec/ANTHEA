using System.Collections.Immutable;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Anthea.Presentation.Modules.ConcreteSection;
using X.Core;

namespace X.Desktop.Views.ConcreteSection;

/// <summary>
/// View of <see cref="ConcreteSheetViewModel"/> (F5.10, view after S9). The code-behind does only what decision Q3 allows and what is state of the
/// view:
/// <list type="bullet">
/// <item>the drawing of the section of the control panel: the historic <see cref="ConcreteSectionViewport"/> receives the plain data of
/// <see cref="ConcreteInputViewModel.Drawing"/> at every change (<see cref="ConcreteSectionDrawings.Apply(ConcreteSectionViewport, ConcreteSectionDrawingViewModel)"/>);
/// a bar clicked goes back to the ViewModel; «Adatta», «PNG» and «Espandi» act on the drawing, as the ViewportFrame of the historic view;</item>
/// <item>the splitters, with the ratios saved in the sheet (<see cref="ConcreteSheetViewModel.LayoutRatio"/>, <see cref="ConcreteSheetViewModel.MoveLayout"/>:
/// the groups «ingressi», «risultati» and «righe» of ConcreteLayout.cs), the size of the page (at least 1120 × 700, with scroll bars below);</item>
/// <item>the windows of the view: «Reologia del calcestruzzo…» (the window of F5.5 with fck) and «Modello e dati comuni…».</item>
/// </list>
/// No calculation and no write of the document.
/// </summary>
public partial class ConcreteSectionSheetView : UserControl
{
    private ConcreteSheetViewModel? sheet;
    private ConcreteInputViewModel? inputs;

    /// <summary>The drawing of the section of the control panel (the preview of the historic view).</summary>
    internal ConcreteSectionViewport Preview { get; } = new();

    /// <summary>Shows a dialog of the view («Espandi»): modal, as the historic one; the checks show it off screen without waiting.</summary>
    internal Func<Window, bool?> ShowWindow { get; set; } = window => window.ShowDialog();

    /// <summary>Asks where to save an image (the dialog «PNG» of the historic frame); the checks give a path.</summary>
    internal Func<string, string?> ChooseImagePath { get; set; } = ChooseImage;

    /// <summary>Shows a non modal window of the view («Reologia del calcestruzzo…»); the checks keep it off screen.</summary>
    internal Action<Window> ShowModeless { get; set; } = window => window.Show();

    public ConcreteSectionSheetView()
    {
        InitializeComponent();
        // As the historic view: the numbers of the forms and of the tables with the numeric presentation, the forms committed on focus loss.
        SetValue(NumericPresentation.EnabledProperty, true);
        PreviewHost.Content = Preview;
        ReadDividers();
        Preview.BarSelected += index => inputs?.Drawing.ClickBar(index);
        Scroll.SizeChanged += (_, _) => Resize();
        Scroll.ScrollChanged += (_, e) => { if (ReferenceEquals(e.Source, Scroll) && (e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0)) Resize(); };
        DataContextChanged += (_, _) => Attach(DataContext as ConcreteSheetViewModel);
        Loaded += (_, _) => Attach(DataContext as ConcreteSheetViewModel);
    }

    private void Attach(ConcreteSheetViewModel? next)
    {
        if (ReferenceEquals(sheet, next)) return;
        if (sheet is not null)
        {
            sheet.PropertyChanged -= OnSheetChanged; sheet.LayoutChanged -= OnLayoutChanged; sheet.RheologyRequested -= OnRheologyRequested; sheet.PropertiesRequested -= OnPropertiesRequested;
            AttachInputs(null);
        }
        sheet = next;
        if (sheet is null) return;
        sheet.PropertyChanged += OnSheetChanged; sheet.LayoutChanged += OnLayoutChanged; sheet.RheologyRequested += OnRheologyRequested; sheet.PropertiesRequested += OnPropertiesRequested;
        AttachInputs(sheet.Inputs);
        foreach (string group in Groups) OnLayoutChanged(sheet, group);
    }

    // The inputs are new after every Reload of the sheet.
    private void AttachInputs(ConcreteInputViewModel? next)
    {
        if (inputs is not null) { inputs.Drawing.Changed -= OnPreviewChanged; inputs.Drawing.PropertyChanged -= OnPreviewChanged; }
        inputs = next;
        if (inputs is null) return;
        inputs.Drawing.Changed += OnPreviewChanged; inputs.Drawing.PropertyChanged += OnPreviewChanged;
        DrawPreview();
    }

    private void OnSheetChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ConcreteSheetViewModel.Inputs) && sheet is not null) AttachInputs(sheet.Inputs);
    }

    private void OnPreviewChanged(object? sender, EventArgs e) => DrawPreview();

    private void DrawPreview()
    {
        if (inputs is null) return;
        ConcreteSectionDrawings.Apply(Preview, inputs.Drawing);
    }

    // ---------------------------------------------------------------- splitters (ConcreteLayout.cs of the historic view)

    /// <summary>The groups of dividers of the sheet (RegisterWorkspaceSplit of the historic view).</summary>
    private static readonly ImmutableArray<string> Groups = ["ingressi", "risultati", "righe"];

    /// <summary>
    /// Every grid of the tabs with a divider: its group (the Tag of the grid in the XAML: «ingressi», «risultati», «righe»), its direction and its
    /// initial ratio, from the star sizes written in the XAML, those of the historic view (3 and 7 for the inputs, 6 and 4 for the results, 3.7 and 2
    /// for the drawing and the tables). Read once, before any ratio of the sheet replaces them.
    /// </summary>
    private readonly List<(Grid Grid, string Group, bool Rows, double Initial)> dividers = [];

    private void ReadDividers()
    {
        foreach (var grid in Logical<Grid>(Tabs).Where(g => g.Tag is string tag && Groups.Contains(tag)))
        {
            string group = (string)grid.Tag;
            bool rows = group == "righe";
            double first = rows ? grid.RowDefinitions[0].Height.Value : grid.ColumnDefinitions[0].Width.Value;
            double last = rows ? grid.RowDefinitions[2].Height.Value : grid.ColumnDefinitions[2].Width.Value;
            dividers.Add((grid, group, rows, first / (first + last)));
            foreach (var splitter in grid.Children.OfType<GridSplitter>()) splitter.DragCompleted += Splitter_DragCompleted;
        }
    }

    /// <summary>The elements of type <typeparamref name="T"/> in the logical tree under <paramref name="root"/> (also in the tabs not shown).</summary>
    internal static IEnumerable<T> Logical<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T found) yield return found;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var item in Logical<T>(child)) yield return item;
    }

    private void OnLayoutChanged(object? sender, string group)
    {
        if (sheet is null || !Groups.Contains(group)) return;
        foreach (var (grid, _, rows, initial) in dividers.Where(d => d.Group == group))
        {
            double ratio = sheet.LayoutRatio(group, initial);
            if (rows) { grid.RowDefinitions[0].Height = new(ratio, GridUnitType.Star); grid.RowDefinitions[2].Height = new(1 - ratio, GridUnitType.Star); }
            else { grid.ColumnDefinitions[0].Width = new(ratio, GridUnitType.Star); grid.ColumnDefinitions[2].Width = new(1 - ratio, GridUnitType.Star); }
        }
    }

    private void Splitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (sheet is null || sender is not GridSplitter splitter || splitter.Parent is not Grid grid) return;
        var divider = dividers.FirstOrDefault(d => ReferenceEquals(d.Grid, grid));
        if (divider.Grid is null) return;
        double first = divider.Rows ? grid.RowDefinitions[0].ActualHeight : grid.ColumnDefinitions[0].ActualWidth;
        double last = divider.Rows ? grid.RowDefinitions[2].ActualHeight : grid.ColumnDefinitions[2].ActualWidth;
        if (first + last > 0) sheet.MoveLayout(divider.Group, first / (first + last));
    }

    // The page at least 1120 × 700, with the scroll bars of the sheet below (ResizeWorkspace of the historic view).
    private void Resize()
    {
        Body.Width = Math.Max(1120, double.IsFinite(Scroll.ViewportWidth) ? Scroll.ViewportWidth : 0);
        Body.Height = Math.Max(700, double.IsFinite(Scroll.ViewportHeight) ? Scroll.ViewportHeight : 0);
    }

    // ---------------------------------------------------------------- the toolbar of the drawing (ViewportFrame of the historic view)

    private void FitPreview_Click(object sender, RoutedEventArgs e) => Preview.ResetView();

    private void SavePreview_Click(object sender, RoutedEventArgs e)
    {
        if (ChooseImagePath("ANTHEA_Sezione_geometrica.png") is not { } path) return;
        try { Archivio.ScriviAtomico(path, Preview.Png()); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { MessageBox.Show(Window.GetWindow(this), error.Message, "Esportazione immagine"); }
    }

    internal static string? ChooseImage(string name)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Immagine PNG|*.png", FileName = name };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private void ExpandPreview_Click(object sender, RoutedEventArgs e)
    {
        PreviewHost.Content = null;
        var window = Ui.Dialog(this, "Sezione geometrica", Preview, 1200, 800);
        window.Closed += (_, _) => { window.Content = null; PreviewHost.Content = Preview; };
        ShowWindow(window);
    }

    // ---------------------------------------------------------------- windows of the view

    private void OnRheologyRequested(object? sender, double strength) => ShowModeless(ConcreteTimeEffectsView.CreateWindow(this, strength));

    // «Proprietà / report…» (ShowSectionProperties of the historic view): a non modal window on the ViewModel of the properties, disposed when it closes.
    private void OnPropertiesRequested(object? sender, ConcreteSectionPropertiesViewModel properties)
    {
        var window = Ui.Dialog(this, properties.Title, new ContentControl { Content = properties, Focusable = false, IsTabStop = false }, 720, 780);
        window.Closed += (_, _) => properties.Dispose();
        ShowModeless(window);
    }

    private void Model_Click(object sender, RoutedEventArgs e) => CalculationHelpView.Show(this, false);
}
