using System.Reflection;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Anthea.Application.Modules;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules.HorizontalPile;
using Anthea.Presentation.Modules.HorizontalPile.Elastic;
using Anthea.Presentation.Services;
using X.Desktop.Views.HorizontalPile;

/// <summary>
/// f5-pass-orizzontale: a horizontal pile or micropile opened with the MVVM view, as the window opens it: the module of the desktop
/// (<see cref="HorizontalPileSheetModule"/> with the WPF figures of the report), its sheet hosted by MvvmSheetEditor (reached by reflection: the
/// host is internal to the program) with the services of the composition root, the view in a window off screen (never activated, never in the
/// taskbar). The elastic response calculates through <see cref="ScriptedAnalysis"/>, the session of the program whose result a check may change
/// before it is published (the bill of a segment with an error of the calculation). The checks drive the view and read it, its ViewModels
/// (<see cref="Sheet"/>, <see cref="Elastic"/>, <see cref="Reinforcement"/>) and the data of the sheet (<see cref="Data"/>: the sheet works on a copy).
/// </summary>
sealed class PileHost : IDisposable
{
    const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly Assembly Desktop = typeof(X.Desktop.App).Assembly;
    readonly object editor;
    readonly Type editorType;

    internal PileHost(JsonObject data, string module = "geo_palo_orizzontale", int width = 1500, int height = 1050)
    {
        var figures = (IHorizontalPileFigureRenderer)Activator.CreateInstance(Desktop.GetType("X.Desktop.HorizontalPileFigureRenderer")!, true)!;
        var sheetModule = new HorizontalPileSheetModule(module, figures, () => Analysis);
        var composition = Activator.CreateInstance(Desktop.GetType("X.Desktop.Services.DesktopComposition")!, Members, null, [(Func<Window?>)(() => null)], null)!;
        var services = (PresentationServices)Get(composition, "Services");
        editorType = Desktop.GetType("X.Desktop.MvvmSheetEditor")!;
        editor = Activator.CreateInstance(editorType, Members, null, [sheetModule, data, services, ModuleServices.Get(module), new CalculationLimiter(CalculationLimiter.DefaultMaximum)], null)!;
        Sheet = (HorizontalPileSheetViewModel)Get(editor, "Sheet");
        View = (FrameworkElement)Get(editor, "View");
        Window = new Window
        {
            Content = View, Width = width, Height = height, Left = -32000, Top = -32000, WindowStartupLocation = WindowStartupLocation.Manual,
            ShowActivated = false, ShowInTaskbar = false
        };
        Window.Show();
        Settle();
    }

    /// <summary>The elastic analysis of this sheet: the session of the program, with an optional change of the result before it is published.</summary>
    internal ScriptedAnalysis Analysis { get; } = new();

    internal HorizontalPileSheetViewModel Sheet { get; }

    internal ElasticPileViewModel Elastic => Sheet.Elastic;

    internal PileReinforcementViewModel Reinforcement => Elastic.Reinforcement;

    /// <summary>The data of the sheet (a copy of the data given).</summary>
    internal JsonObject Data => Sheet.Data;

    /// <summary>The view of the sheet (HorizontalPileSheetView in the content control of the host).</summary>
    internal FrameworkElement View { get; }

    internal Window Window { get; }

    /// <summary>The result of the window (ISheetEditor.Result of the host: the result of the tab shown).</summary>
    internal JsonObject? EditorResult => (JsonObject?)Get(editor, "Result");

    internal HorizontalPileSheetView SheetView => Find<HorizontalPileSheetView>(View).Single();

    internal ElasticPileView ElasticView => Find<ElasticPileView>(View).Single();

    internal PileReinforcementView ReinforcementView => Find<PileReinforcementView>(View).Single();

    /// <summary>«Calcola» of the window (ISheetEditor.CalculateAsync: the edits confirmed, then the calculation of the tab shown).</summary>
    internal Task CalculateAsync() => (Task)editorType.GetMethod("CalculateAsync")!.Invoke(editor, [true])!;

    /// <summary>The dispatcher idle, then the layout of the window.</summary>
    internal void Settle()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
        Window.UpdateLayout();
    }

    /// <summary>A field or a property of an object of the program, public or not (named elements of the views, internal members).</summary>
    internal static object Get(object target, string member)
    {
        var type = target.GetType();
        return type.GetProperty(member, Members)?.GetValue(target) ?? type.GetField(member, Members)?.GetValue(target)
            ?? throw new MissingMemberException(type.Name, member);
    }

    /// <summary>The elements of type <typeparamref name="T"/> under <paramref name="root"/> in the visual tree, in order.</summary>
    internal static IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T value) yield return value;
            foreach (var d in Find<T>(child)) yield return d;
        }
    }

    /// <summary>An internal method of a ViewModel or of a view, without arguments.</summary>
    internal static void Call(object target, string method) => target.GetType().GetMethod(method, Members, Type.EmptyTypes)!.Invoke(target, null);

    /// <summary>A click of the user: the Click handlers and the command of the button (ButtonBase.OnClick).</summary>
    internal static void Press(ButtonBase button)
    {
        if (!button.IsEnabled) throw new InvalidOperationException("Pulsante disabilitato: " + button.Content);
        typeof(ButtonBase).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(button, null);
    }

    public void Dispose()
    {
        Window.Close();
        ((IDisposable)editor).Dispose();
    }
}

/// <summary>The elastic analysis of the program (<see cref="ElasticPileAnalysisSession"/>); <see cref="Change"/> edits a result before it is published.</summary>
sealed class ScriptedAnalysis : IElasticPileAnalysis
{
    readonly ElasticPileAnalysisSession session = new();

    internal Func<JsonObject, JsonObject>? Change { get; set; }

    public int ResponseCalculations => session.ResponseCalculations;

    public async Task<JsonObject> CalculateAsync(JsonObject source, Action<JsonObject> responseReady, IProgress<PileProgress>? progress = null, CancellationToken cancellation = default)
    {
        var result = await session.CalculateAsync(source, responseReady, progress, cancellation);
        return Change is { } change ? change(result) : result;
    }

    public void Dispose() => session.Dispose();
}
