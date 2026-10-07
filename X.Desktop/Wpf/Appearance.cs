using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Data;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Threading;

namespace X.Desktop;

internal enum AppAppearance { Light, Dark, VeryDark }

/// <summary>Application colours are independent of calculation documents and their revisions.</summary>
internal static class Appearance
{
    internal static AppAppearance Current { get; private set; }
    internal static event Action? Changed;
    internal static string PreferencePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ANTHEA", "appearance.json");
    private static readonly Dictionary<(Color, string), SolidColorBrush> brushes = new();
    private static readonly ConditionalWeakTable<SolidColorBrush, SourceColour> origins = new();
    private static readonly Dictionary<(GradientBrush, string), GradientBrush> gradients = new();
    private static readonly HashSet<GradientBrush> themedGradients = new();
    private sealed class SourceColour(Color color, string role) : INotifyPropertyChanged
    {
        internal Color Color { get; } = color;
        internal string Role { get; } = role;
        private Color value = Transform(color, role);
        public Color Value { get => value; set { if (this.value == value) return; this.value = value; PropertyChanged?.Invoke(this, new(nameof(Value))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private static bool initialized;
    internal static readonly DependencyProperty DarkProperty = DependencyProperty.RegisterAttached("Dark", typeof(bool), typeof(Appearance),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));
    public static bool GetDark(DependencyObject value) => (bool)value.GetValue(DarkProperty);
    public static void SetDark(DependencyObject value, bool dark) => value.SetValue(DarkProperty, dark);
    internal static Brush Paper => Colour(Colors.White, "background");
    internal static Brush Surface => Colour((Color)ColorConverter.ConvertFromString("#F3F5F8"), "background");
    internal static Brush Ink => Colour((Color)ColorConverter.ConvertFromString("#0B2A4A"), "foreground");
    internal static Brush Accent => Colour((Color)ColorConverter.ConvertFromString("#0B5CAD"), "foreground");
    internal static Brush Background(string hex) => Colour((Color)ColorConverter.ConvertFromString(hex), "background");
    internal static Brush Foreground(Brush brush) => Map(brush, "foreground");
    internal static Brush Outline(string hex) => Colour((Color)ColorConverter.ConvertFromString(hex), "border");

    internal static AppAppearance ReadPreference(string path)
    {
        try
        {
            var value = JsonSerializer.Deserialize<string>(File.ReadAllText(path));
            return Enum.TryParse<AppAppearance>(value, out var mode) && Enum.IsDefined(mode) ? mode : AppAppearance.Light;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return AppAppearance.Light; }
    }
    internal static void SavePreference(string path, AppAppearance mode)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path + ".new", JsonSerializer.Serialize(mode.ToString()));
        File.Move(path + ".new", path, true);
    }
    internal static void Initialize(bool loadPreference = true)
    {
        if (initialized) return;
        initialized = true;
        Current = loadPreference ? ReadPreference(PreferencePath) : AppAppearance.Light;
        var resources = Application.Current.Resources;
        AppearanceResources.Register(resources);
        resources["AppearancePaper"] = Paper;
        resources["AppearanceSurface"] = Surface;
        resources["AppearanceInk"] = Ink;
        resources["AppearanceBorder"] = Colour(Colors.LightGray, "border");
        resources[SystemColors.WindowBrushKey] = Paper;
        resources[SystemColors.WindowTextBrushKey] = Ink;
        resources[SystemColors.MenuBrushKey] = Paper;
        resources[SystemColors.MenuTextBrushKey] = Ink;
        resources[SystemColors.ControlBrushKey] = Colour(SystemColors.ControlColor, "background");
        resources[SystemColors.ControlTextBrushKey] = Colour(SystemColors.ControlTextColor, "foreground");
        // Selection without focus (DataGrid cells, list and tree items): light grey in the system theme.
        resources[SystemColors.InactiveSelectionHighlightBrushKey] = Colour(((SolidColorBrush)SystemColors.InactiveSelectionHighlightBrush).Color, "background");
        resources[SystemColors.InactiveSelectionHighlightTextBrushKey] = Colour(((SolidColorBrush)SystemColors.InactiveSelectionHighlightTextBrush).Color, "foreground");
        EventManager.RegisterClassHandler(typeof(FrameworkElement), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) => ApplyElement((DependencyObject)sender)));
        // WPF raises Loaded only on elements with a Loaded handler of their own: an element added to a window
        // already loaded (module views, project pages, tree rows) is reached by its first layout instead, when
        // it lies in a dark window (IsLoaded is not reliable there). In Light the palette is the identity, and
        // Set themes the whole window when the mode changes.
        EventManager.RegisterClassHandler(typeof(FrameworkElement), FrameworkElement.SizeChangedEvent,
            new SizeChangedEventHandler((sender, _) => { if (sender is FrameworkElement element && GetDark(element)) ApplyElement(element); }), true);
        Set(Current, false);
    }
    internal static void Set(AppAppearance mode, bool persist = true)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        // Write first so a failed save is visible to the caller, without claiming persistence.
        if (persist) SavePreference(PreferencePath, mode);
        Current = mode; Repaint();
        if (Application.Current is { } app)
        {
            foreach (Window window in app.Windows) { SetDark(window, mode != AppAppearance.Light); ApplyTree(window); }
        }
        Changed?.Invoke();
    }
    private static void Repaint()
    {
        foreach (var (key, brush) in brushes) origins.GetValue(brush, _ => throw new InvalidOperationException()).Value = Transform(key.Item1, key.Item2);
    }
    /// <summary>Images and printouts of calculation documents keep the Light colours whatever the appearance. Until the scope
    /// is disposed the palette brushes take their Light colours, and <paramref name="root"/>, when it lies in a dark window,
    /// leaves the dark styles. Render synchronously inside the scope: everything returns to the current appearance before
    /// WPF draws the windows again, the open views are not re-themed and <see cref="Changed"/> is not raised.
    /// In Light the scope does nothing.</summary>
    internal static IDisposable Document(DependencyObject? root = null)
    {
        if (Current == AppAppearance.Light) return new UpdateScope(() => { });
        var mode = Current; var dark = root?.ReadLocalValue(DarkProperty);
        Current = AppAppearance.Light; Repaint();
        if (root is not null && GetDark(root)) SetDark(root, false);
        return new UpdateScope(() =>
        {
            if (root is not null) { if (dark == DependencyProperty.UnsetValue) root.ClearValue(DarkProperty); else root.SetValue(DarkProperty, dark); }
            Current = mode; Repaint();
        });
    }
    internal static ComboBox Selector()
    {
        var choice = Ui.Choice(["Chiaro", "Scuro", "Molto scuro"]);
        choice.SelectedIndex = (int)Current; choice.MinWidth = 130;
        System.Windows.Automation.AutomationProperties.SetName(choice, "Aspetto di ANTHEA");
        bool syncing = false;
        void Refresh() { syncing = true; choice.SelectedIndex = (int)Current; syncing = false; }
        choice.Loaded += (_, _) => { Refresh(); Changed += Refresh; };
        choice.Unloaded += (_, _) => Changed -= Refresh;
        choice.SelectionChanged += (_, _) => { if (!syncing && choice.SelectedIndex >= 0 && choice.SelectedIndex != (int)Current) Set((AppAppearance)choice.SelectedIndex); };
        return choice;
    }
    internal static void Watch(Window window)
    {
        SetDark(window, Current != AppAppearance.Light);
        window.Loaded += (_, _) => ApplyTree(window);
    }
    internal static Brush Colour(Color original, string role)
    {
        var key = (original, role);
        if (!brushes.TryGetValue(key, out var brush))
        {
            var source = new SourceColour(original, role);
            brush = new SolidColorBrush();
            // WPF seals styles/templates and may freeze their literal brushes. A bound
            // palette brush stays live, including after opening a view for the first time.
            BindingOperations.SetBinding(brush, SolidColorBrush.ColorProperty, new Binding(nameof(SourceColour.Value)) { Source = source, Mode = BindingMode.OneWay });
            brushes.Add(key, brush); origins.Add(brush, source);
        }
        return brush;
    }
    private static Brush Map(Brush brush, string role)
    {
        if (brush is GradientBrush gradient)
        {
            if (themedGradients.Contains(gradient)) return gradient;
            var key = (gradient, role);
            if (!gradients.TryGetValue(key, out var mapped))
            {
                mapped = gradient.Clone();
                for (int i = 0; i < mapped.GradientStops.Count; i++)
                    BindingOperations.SetBinding(mapped.GradientStops[i], GradientStop.ColorProperty,
                        new Binding("Color") { Source = Colour(gradient.GradientStops[i].Color, role), Mode = BindingMode.OneWay });
                gradients.Add(key, mapped); themedGradients.Add(mapped);
            }
            return mapped;
        }
        if (brush is not SolidColorBrush solid || solid.Color.A == 0) return brush;
        if (origins.TryGetValue(solid, out var source)) return source.Role == role ? brush : Colour(source.Color, role);
        return Colour(solid.Color, role);
    }
    private static Color Transform(Color c, string role)
    {
        if (Current == AppAppearance.Light || c.A == 0) return c;
        double l = (c.R * .2126 + c.G * .7152 + c.B * .0722) / 255;
        Color Hex(string s) { var value = (Color)ColorConverter.ConvertFromString(s); value.A = c.A; return value; }
        if (role == "background")
        {
            if (Current == AppAppearance.VeryDark)
                return Hex(l < .38 ? "#17191C" : l > .985 ? "#141518" : l < .85 ? "#2B2D31" : "#090A0C");
            if (l < .38) return c; // dark command bars retain white text
            bool tinted = Math.Max(c.R, Math.Max(c.G, c.B)) - Math.Min(c.R, Math.Min(c.G, c.B)) > 20;
            if (tinted) return Color.FromArgb(c.A, (byte)(c.R * .19 + 12), (byte)(c.G * .19 + 12), (byte)(c.B * .19 + 12));
            return Hex(l > .985 ? "#142337" : "#1D3047");
        }
        int chroma = Math.Max(c.R, Math.Max(c.G, c.B)) - Math.Min(c.R, Math.Min(c.G, c.B));
        if (role == "border")
        {
            // Saturated lines carry a meaning (selection, validation, soil layer colours) and keep their hue.
            if (chroma > 55 && l < .7) return c;
            return Hex(Current == AppAppearance.VeryDark ? "#454C55" : "#596B7E");
        }
        if (l > .78) return c;
        // Preserve status hues, with enough luminance for dark surfaces.
        if (chroma > 55 && (c.G > c.B * 1.2 || c.R > c.B * 1.3))
            return Color.FromArgb(c.A, (byte)(c.R * .45 + 140), (byte)(c.G * .45 + 140), (byte)(c.B * .45 + 140));
        return Hex(Current == AppAppearance.VeryDark ? (l < .3 ? "#E8E9EB" : "#BFC2C7") : (l < .3 ? "#E5EDF7" : "#B9C9DD"));
    }
    private static void Paint(DependencyObject item, DependencyProperty property, string role)
    {
        // Only explicit instance colours belong here. Never pin an inherited, default,
        // template or trigger value: doing so masks later selection/validation changes.
        var valueSource = DependencyPropertyHelper.GetValueSource(item, property);
        if (valueSource.IsExpression || valueSource.BaseValueSource != BaseValueSource.Local) return;
        if (item.GetValue(property) is Brush old)
        {
            var next = Map(old, role);
            if (!ReferenceEquals(old, next)) item.SetCurrentValue(property, next);
        }
    }
    private static void ApplyElement(DependencyObject item)
    {
        switch (item)
        {
            case Control c:
                Paint(c, Control.BackgroundProperty, "background"); Paint(c, Control.ForegroundProperty, "foreground"); Paint(c, Control.BorderBrushProperty, "border");
                if (c is DataGrid grid)
                {
                    Paint(grid, DataGrid.RowBackgroundProperty, "background"); Paint(grid, DataGrid.AlternatingRowBackgroundProperty, "background");
                    Paint(grid, DataGrid.HorizontalGridLinesBrushProperty, "border");
                }
                break;
            case Border b: Paint(b, Border.BackgroundProperty, "background"); Paint(b, Border.BorderBrushProperty, "border"); break;
            case Panel p: Paint(p, Panel.BackgroundProperty, "background"); break;
            case TextBlock t: Paint(t, TextBlock.ForegroundProperty, "foreground"); Paint(t, TextBlock.BackgroundProperty, "background"); break;
            case System.Windows.Shapes.Shape s when s.TemplatedParent is not null:
                break; // Native glyphs and scroll thumbs remain under template control.
        }
    }
    internal static void ApplyTree(DependencyObject root)
    {
        var visited = new HashSet<DependencyObject>();
        void Visit(DependencyObject item)
        {
            if (!visited.Add(item)) return;
            ApplyElement(item);
            if (item is Visual)
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(item); i++) Visit(VisualTreeHelper.GetChild(item, i));
            foreach (var child in LogicalTreeHelper.GetChildren(item).OfType<DependencyObject>()) Visit(child);
        }
        Visit(root);
    }
}
