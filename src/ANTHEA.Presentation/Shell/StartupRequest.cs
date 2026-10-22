namespace Anthea.Presentation.Shell;

/// <summary>What the shell does at startup, from the command line.</summary>
public enum StartupAction
{
    /// <summary>Nothing: the home page.</summary>
    None,
    /// <summary>«--wiki &lt;uri&gt;»: the Wiki on that article.</summary>
    Wiki,
    /// <summary>A path: the document opens (an archive or a single file).</summary>
    OpenFile
}

/// <summary>
/// The command line of the application as App.OnStartup reads it for MainWindow (F5.13 c, proposal 4.3), as a pure parser:
/// <list type="bullet">
/// <item>exactly two arguments, the first «--wiki»: the Wiki on the second;</item>
/// <item>otherwise the first argument that does not start with «--» is a document to open; «--models» anywhere then shows the page
/// «Modelli» after the opening (only when the opening succeeds, as the lambda of MainWindow.Loaded);</item>
/// <item>otherwise nothing (for example «--wiki» alone or «--models» without a path).</item>
/// </list>
/// The flags of the checks never reach it: the harness of the UiTests build handles them before (App.RunTestHarness).
/// </summary>
/// <param name="Action">What to do.</param>
/// <param name="Target">The URI of the Wiki or the path of the document; null for <see cref="StartupAction.None"/>.</param>
/// <param name="Models">After the opening of the document, the page «Modelli».</param>
public sealed record StartupRequest(StartupAction Action, string? Target, bool Models)
{
    /// <summary>The argument of the Wiki.</summary>
    public const string WikiFlag = "--wiki";

    /// <summary>The argument of the page «Modelli».</summary>
    public const string ModelsFlag = "--models";

    /// <summary>Nothing to do.</summary>
    public static StartupRequest None { get; } = new(StartupAction.None, null, false);

    /// <summary>The request of <paramref name="args"/>, the arguments of the process without the name of the program.</summary>
    public static StartupRequest Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Count == 2 && args[0] == WikiFlag) return new(StartupAction.Wiki, args[1], false);
        if (args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)) is { } path) return new(StartupAction.OpenFile, path, args.Contains(ModelsFlag));
        return None;
    }
}
