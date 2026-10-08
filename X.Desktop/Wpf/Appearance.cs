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
    // Roles with a meaning of their own. Unlike background, foreground and border they are never re-assigned when the
    // brush is painted on another property (see Map): the code that chooses them knows what the colour stands for.
    /// <summary>Fill of a primary button and of the selected choice (navigation, filters, revisions, typologies): Navy in Light.</summary>
    internal static Brush Selected => Colour((Color)ColorConverter.ConvertFromString("#0B2A4A"), "selected");
    /// <summary>Highlight of the selected row of a tree or grid and of the selected tab.</summary>
    internal static Brush Selection(string hex) => Colour((Color)ColorConverter.ConvertFromString(hex), "selection");
    /// <summary>Background of a calculated or linked field, which the user does not edit.</summary>
    internal static Brush Calculated => Colour((Color)ColorConverter.ConvertFromString("#EAF2FA"), "calculated");
    /// <summary>Colour of a series in a legend or of an axis on a dark view: the hue identifies the curve, so it is kept and lightened.</summary>
    internal static Brush Series(Brush brush) => brush is SolidColorBrush solid ? Colour(origins.TryGetValue(solid, out var source) ? source.Color : solid.Color, "series") : brush;
    /// <summary>Text of a disabled control or of an automatic value shown in grey: dimmer than the muted text, as in Light.</summary>
    internal static Brush Dim(Brush brush) => brush is SolidColorBrush solid ? Colour(origins.TryGetValue(solid, out var source) ? source.Color : solid.Color, "disabled") : brush;
    /// <summary>Identification colour (soil layer) shown also in the drawings: the same in every appearance.</summary>
    internal static Brush Swatch(string hex) => Colour((Color)ColorConverter.ConvertFromString(hex), "swatch");
    private static bool Generic(string role) => role is "background" or "foreground" or "border";

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
        // Selection without focus (DataGrid cells, list and tree items): light grey in the system theme, the selection
        // highlight of the palette in the dark appearances (distinct from the alternate rows and the column headers).
        resources[SystemColors.InactiveSelectionHighlightBrushKey] = Colour(((SolidColorBrush)SystemColors.InactiveSelectionHighlightBrush).Color, "selection");
        resources["AppearanceSelection"] = Selection("#E2EFFC");
        resources[SystemColors.InactiveSelectionHighlightTextBrushKey] = Colour(((SolidColorBrush)SystemColors.InactiveSelectionHighlightTextBrush).Color, "foreground");
        // Text of disabled items (ComboBoxItem): dimmer than the muted text in the dark appearances, as in Light.
        resources[SystemColors.GrayTextBrushKey] = Colour(SystemColors.GrayTextColor, "disabled");
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
        if (origins.TryGetValue(solid, out var source)) return source.Role == role || !Generic(source.Role) ? brush : Colour(source.Color, role);
        return Colour(solid.Color, role);
    }
    private static Color Transform(Color c, string role)
    {
        if (Current == AppAppearance.Light || c.A == 0) return c;
        double l = (c.R * .2126 + c.G * .7152 + c.B * .0722) / 255;
        bool veryDark = Current == AppAppearance.VeryDark;
        Color Hex(string s) { var value = (Color)ColorConverter.ConvertFromString(s); value.A = c.A; return value; }
        int chroma = Math.Max(c.R, Math.Max(c.G, c.B)) - Math.Min(c.R, Math.Min(c.G, c.B));
        Color Lighter() => Color.FromArgb(c.A, (byte)(c.R * .45 + 140), (byte)(c.G * .45 + 140), (byte)(c.B * .45 + 140));
        switch (role)
        {
            case "swatch": return c;
            // Lighter than the surfaces, with light text on it (white, #BCCFE2): at least 4.5:1 in both appearances.
            case "selected": return Hex(veryDark ? "#2F4C6E" : "#24507E");
            // Lighter than the rows, the alternate rows and the headers; status text on it stays above 4.5:1.
            case "selection": return Hex(veryDark ? "#2E3D52" : "#26425E");
            // Bluish as in Light, lighter than the editable fields (#1D3047, #090A0C).
            case "calculated": return Tinted(c, veryDark ? .03 : .05, veryDark ? .5 : .7);
            // Every hue, blue and violet included (the foreground role keeps only the status hues).
            case "series": return l > .78 ? c : Lighter();
            // 4.9:1 on #142337 and 5.5:1 on #141518, clearly dimmer than the text (#E5EDF7, #E8E9EB) and the muted text.
            case "disabled": return Hex(veryDark ? "#8A8D92" : "#8291A5");
        }
        if (role == "background")
        {
            // Tinted fills (highlights, notes, warnings, segment colours) keep their hue at the luminance of the
            // appearance, so that they stay distinct from the neutral surfaces and from each other: in Scuro the
            // luminance of the former grey (c × 0.19 + 12, the contrast of the text is unchanged), in Molto scuro
            // that of #2B2D31.
            bool tinted = chroma > 20 && l >= .38;
            if (veryDark)
                return tinted ? Tinted(c, .03, .5) : Hex(l < .38 ? "#17191C" : l > .985 ? "#141518" : l < .85 ? "#2B2D31" : "#090A0C");
            if (l < .38) return c; // dark command bars retain white text
            if (tinted) return Tinted(c, Luminance(Color.FromRgb((byte)(c.R * .19 + 12), (byte)(c.G * .19 + 12), (byte)(c.B * .19 + 12))), .7);
            return Hex(l > .985 ? "#142337" : "#1D3047");
        }
        if (role == "border")
        {
            // Saturated lines carry a meaning (selection, validation, status) and keep their hue; the strongest
            // hues (the yellow of the utilization palette, 0.70 < η ≤ 0.90) are light by nature.
            if (chroma > 55 && l < .7 || chroma > 100) return c;
            return Hex(Current == AppAppearance.VeryDark ? "#454C55" : "#596B7E");
        }
        if (l > .78) return c;
        // Preserve status hues, with enough luminance for dark surfaces.
        if (chroma > 55 && (c.G > c.B * 1.2 || c.R > c.B * 1.3)) return Lighter();
        return Hex(veryDark ? (l < .3 ? "#E8E9EB" : "#BFC2C7") : (l < .3 ? "#E5EDF7" : "#B9C9DD"));
    }
    private static double Linear(byte v) { double s = v / 255.0; return s <= .04045 ? s / 12.92 : Math.Pow((s + .055) / 1.055, 2.4); }
    private static byte Gamma(double v) { v = Math.Clamp(v, 0, 1); return (byte)Math.Round(255 * (v <= .0031308 ? 12.92 * v : 1.055 * Math.Pow(v, 1 / 2.4) - .055)); }
    private static double Luminance(Color c) => .2126 * Linear(c.R) + .7152 * Linear(c.G) + .0722 * Linear(c.B);
    /// <summary>Colour with the hue of <paramref name="c"/> and relative luminance <paramref name="luminance"/>: the deviation of
    /// each linear channel from the luminance is normalised to <paramref name="saturation"/>, so that pale tints keep a visible hue.</summary>
    private static Color Tinted(Color c, double luminance, double saturation)
    {
        double y = Math.Max(1e-6, Luminance(c));
        double[] deviation = [Linear(c.R) / y - 1, Linear(c.G) / y - 1, Linear(c.B) / y - 1];
        double largest = Math.Max(1e-6, deviation.Max(Math.Abs));
        byte Channel(int i) => Gamma(luminance * (1 + saturation * deviation[i] / largest));
        return Color.FromArgb(c.A, Channel(0), Channel(1), Channel(2));
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
