using System.Windows;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules;
using X.Desktop.Services;

namespace X.Desktop;

/// <summary>
/// UiTests only (F5.2, proposal 5.2): the sheets of the WPF checks open as in the product, with
/// <see cref="ModuleImplementations.Release"/> and the modules of the composition root, and with the services of the checks.
/// On the command line of the harness, <c>--module-impl &lt;module&gt;=mvvm|historic</c> (repeatable) chooses another
/// implementation for a module: before its checkpoint a migrated module is selectable only here, never in the product and never
/// with an environment variable (decision D3). Accepted after the arguments of --check-f3-equivalence and --check-f3-report-chapters
/// (F5.4); a module without an MVVM implementation makes the factory fail with its name, a module already MVVM in the Release (the
/// pile group since F5.4) refuses «historic».
/// </summary>
internal static class ModuleHostBench
{
    /// <summary>The bench argument.</summary>
    internal const string Argument = "--module-impl";

    /// <summary>First argument of the interactive bench (F5.4): the application of the product with the editors of the bench (App.StartInteractive).</summary>
    internal const string Interactive = "--interactive";

    /// <summary>Factory of the checks with the bench choices of the command line of this process.</summary>
    internal static SheetEditorFactory SheetEditors() => SheetEditors(Environment.GetCommandLineArgs());

    /// <summary>Factory of the checks with the bench choices of <paramref name="arguments"/> and the modules of <paramref name="modules"/>.</summary>
    internal static SheetEditorFactory SheetEditors(IReadOnlyList<string> arguments, ModuleRegistry? modules = null) =>
        new(Implementations(arguments), modules ?? DesktopComposition.CreateModules(), Services(), new CalculationLimiter(CalculationLimiter.DefaultMaximum));

    /// <summary>
    /// Factory of the interactive bench: the bench choices of <paramref name="arguments"/> with the modules, the services and the limiter of
    /// the composition root of the product (real dialogs and Wiki), as App.OnStartup builds them.
    /// </summary>
    internal static SheetEditorFactory SheetEditors(IReadOnlyList<string> arguments, DesktopComposition composition) =>
        new(Implementations(arguments), composition.Modules, composition.Services, composition.Limiter);

    /// <summary>The Release choices with the <c>--module-impl</c> pairs of <paramref name="arguments"/> applied in order.</summary>
    internal static ModuleImplementations Implementations(IReadOnlyList<string> arguments)
    {
        var implementations = ModuleImplementations.Release;
        for (int i = 0; i < arguments.Count; i++)
        {
            if (arguments[i] != Argument) continue;
            if (i + 1 >= arguments.Count) throw new ArgumentException(Argument + ": manca <modulo>=mvvm|historic.");
            string[] pair = arguments[++i].Split('=');
            if (pair.Length != 2 || pair[0].Length == 0) throw new ArgumentException(Argument + ": atteso <modulo>=mvvm|historic, trovato " + arguments[i] + ".");
            var choice = pair[1] switch
            {
                "mvvm" => SheetImplementation.Mvvm,
                "historic" => SheetImplementation.Historic,
                _ => throw new ArgumentException(Argument + ": implementazione sconosciuta " + pair[1] + " (mvvm o historic).")
            };
            implementations = implementations.With(pair[0], choice);
        }
        return implementations;
    }

    /// <summary>The arguments without the <c>--module-impl</c> pairs, for the positional arguments of the checks.</summary>
    internal static string[] WithoutBench(IReadOnlyList<string> arguments)
    {
        var kept = new List<string>();
        for (int i = 0; i < arguments.Count; i++)
        {
            if (arguments[i] == Argument) { i++; continue; }
            kept.Add(arguments[i]);
        }
        return [.. kept];
    }

    /// <summary>Services of the sheets in the checks: errors rethrown, scripted questions, no dialog, clipboard, link or Wiki expected.</summary>
    internal static PresentationServices Services() =>
        new(new RethrowingMessages(), new ScriptedConfirmations(), new Unexpected(), new Unexpected(), new Unexpected(), new Unexpected(), new Unexpected());

    /// <summary>A service the checks do not expect a sheet to use: any call fails the check.</summary>
    private sealed class Unexpected : IFileDialogService, IDialogService, IClipboardService, IExternalLinkService, IWikiNavigator
    {
        private static InvalidOperationException Fail(string what) => new("Servizio non previsto dalla prova: " + what + ".");
        public string? OpenFile(OpenFileRequest request) => throw Fail("dialogo di apertura");
        public string? SaveFile(SaveFileRequest request) => throw Fail("dialogo di salvataggio");
        public string? ChooseFolder(string? title = null) => throw Fail("scelta della cartella");
        public Task<TResult?> ShowDialogAsync<TResult>(IDialogViewModel<TResult> dialog) => throw Fail("finestra " + dialog.Title);
        public void SetText(string text) => throw Fail("appunti");
        public string GetText() => throw Fail("appunti");
        public void Open(Uri target) => throw Fail("collegamento " + target);
        public void Show(string? uri = null) => throw Fail("Wiki " + uri);
    }
}

internal sealed partial class ModuleImplementations
{
    /// <summary>
    /// UiTests: these choices with <paramref name="module"/> opened by <paramref name="implementation"/>. A module MVVM in the Release has no
    /// historic view any more (archived with its switch, Q11): choosing it fails with the name of the module.
    /// </summary>
    internal ModuleImplementations With(string module, SheetImplementation implementation)
    {
        if (!choices.ContainsKey(module)) throw new ArgumentException(ModuleHostBench.Argument + ": modulo sconosciuto " + module + ".");
        if (implementation == SheetImplementation.Historic && Release.For(module) == SheetImplementation.Mvvm)
            throw new ArgumentException(ModuleHostBench.Argument + ": la vista storica di " + module + " è archiviata (Q11); il modulo si apre solo con la vista MVVM.");
        return new(choices.Select(c => (c.Key, c.Key == module ? implementation : c.Value)));
    }
}

public sealed partial class MainWindow
{
    /// <summary>Windows of the WPF checks (UiTests): the services of <paramref name="serviceFactory"/> and the editors of the bench.</summary>
    internal MainWindow(Func<Window, DesktopServices> serviceFactory) : this(serviceFactory, ModuleHostBench.SheetEditors()) { }

    /// <summary>Window of the external checks (ConcreteShort.UiChecks): the services of the product and the editors of the bench.</summary>
    public MainWindow() : this(WpfDesktopServices.Create) { }
}
