using System.Text.RegularExpressions;
using Anthea.Presentation.Help;
using Anthea.Presentation.Mvvm;
using Anthea.Presentation.Services;
using Anthea.Wiki;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Shell.Navigation;

/// <summary>An item of the menu «Approfondimenti»: a link of the Wiki, a group with its links, or a separator (empty title, no link).</summary>
public sealed record WikiMenuItem(string Title, string? Uri, IReadOnlyList<WikiMenuItem> Children)
{
    public static WikiMenuItem Separator { get; } = new("", null, []);

    public bool IsSeparator => Title.Length == 0;
}

/// <summary>
/// The menu «Approfondimenti ▾» of a module (MainWindow.ShowModuleWikiTopics, WikiContextHelp.ArticlesFor), as data: the topics of the fields of
/// the module (one per article section), a separator, then for every article of the module («Guida · » or «Teoria · ») «Apri l'articolo» and
/// its headings of level 3 and 4.
/// </summary>
public static class WikiMenu
{
    /// <summary>The items of the menu of <paramref name="module"/>, empty when the Wiki has nothing for it.</summary>
    public static IReadOnlyList<WikiMenuItem> For(string module)
    {
        ArgumentNullException.ThrowIfNull(module);
        var items = HelpTopics.Topics.Where(t => t.Modules.Contains(module)).DistinctBy(t => t.Uri).Select(t => new WikiMenuItem(t.Title, t.Uri, [])).ToList();
        if (items.Count > 0) items.Add(WikiMenuItem.Separator);
        foreach (var article in ArticlesFor(module))
        {
            var children = new List<WikiMenuItem> { new("Apri l'articolo", article.Id, []) };
            var headings = Regex.Matches(WikiCatalog.Body(article), @"^#{3,4} (.+)", RegexOptions.Multiline);
            for (int i = 0; i < headings.Count; i++) children.Add(new(headings[i].Groups[1].Value.Trim(), article.Id + "#" + article.Sections[i], []));
            items.Add(new((article.Type == "guide" ? "Guida · " : "Teoria · ") + article.Title, null, children));
        }
        return items;
    }

    /// <summary>The articles of a module (WikiContextHelp.ArticlesFor): guides first, then by order.</summary>
    public static WikiArticle[] ArticlesFor(string module) => WikiCatalog.Articles
        .Where(a => a.Modules.Contains(module) || HelpTopics.Topics.Any(t => t.Modules.Contains(module) && WikiCatalog.Resolve(t.Uri)?.Id == a.Id))
        .OrderBy(a => a.Type == "guide" ? 0 : 1).ThenBy(a => a.Order).ToArray();
}

/// <summary>
/// The bar above the sheet with the focus (F5.13 c, proposal 4.2; MainWindow.BuildShell and UpdateBackButton): heading and subtitle, «← Torna
/// al progetto» or «← Torna ad ANTHEA», «Conferma modifiche» in an editable project, «Wiki del modulo» when the module has a guide, the menu
/// «Approfondimenti ▾». The file commands and «Report short» come from <see cref="Commands.ShellCommandsViewModel"/>.
/// </summary>
public sealed class SheetBarViewModel : ViewModelBase
{
    public const string Subtitle = "Scheda di calcolo · input, profilo e risultati";
    public const string BackToProject = "← Torna al progetto";
    public const string BackToHome = "← Torna ad ANTHEA";
    public const string ConfirmLabel = "Conferma modifiche";
    public const string GuideLabel = "Wiki del modulo";
    public const string GuideToolTip = "Apri la guida del modulo nella Wiki";
    public const string TopicsLabel = "Approfondimenti ▾";
    public const string TopicsToolTip = "Teoria, parametri, dettagli costruttivi ed esempi del modulo";

    private readonly WorkspaceSession workspace;
    private SheetSession? sheet;
    private IReadOnlyList<WikiMenuItem>? topics;

    /// <param name="back">«← Torna…»: the shell shows the project or the home page.</param>
    /// <param name="wiki">An article of the Wiki in its tab.</param>
    public SheetBarViewModel(WorkspaceSession workspace, IMessageService messages, Action<SheetSession> back, Action<string> wiki)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(back);
        ArgumentNullException.ThrowIfNull(wiki);
        this.workspace = workspace;
        var guard = new CommandGuard(messages);
        BackCommand = GuardedCommands.Create(guard, () => { if (sheet is { } current) back(current); }, () => sheet is not null);
        ConfirmCommand = GuardedCommands.Create(guard, workspace.CommitActive, () => CanConfirm);
        GuideCommand = GuardedCommands.Create(guard, () => { if (GuideId is { } guide) wiki(guide); }, () => GuideId is not null);
        OpenTopicCommand = new RelayCommand<string>(uri => guard.Run(() => wiki(uri!)), uri => !string.IsNullOrEmpty(uri));
        workspace.ActiveChanged += OnActiveChanged;
        Update();
    }

    /// <summary>A sheet has the focus: the bar shows.</summary>
    public bool IsVisible => sheet is not null;

    /// <summary>The heading of the sheet (MainWindow.SheetHeading), empty without a sheet.</summary>
    public string Heading => sheet?.Heading ?? "";

    /// <summary>The module of the sheet with the focus, null without one.</summary>
    public string? Module => sheet?.Module;

    public string BackLabel => sheet?.Project is null ? BackToHome : BackToProject;

    /// <summary>«Conferma modifiche» shows for a sheet of a project outside the preview of a revision.</summary>
    public bool CanConfirm => sheet is { Project: not null, IsReadOnly: false };

    /// <summary>The guide of the module in the Wiki, null when there is none («Wiki del modulo» hidden).</summary>
    public string? GuideId => sheet is null ? null : WikiCatalog.ForModule(sheet.Module)?.Id;

    /// <summary>
    /// The notice of the sheet with the focus when it was reloaded because another sheet confirmed shared data (SH9: «Aggiornato da «foglio»: …»), shown
    /// in a line under the bar; null without one.
    /// </summary>
    public string? ReloadNotice => sheet?.ReloadNotice;

    /// <summary>The menu «Approfondimenti ▾» of the module, read when asked.</summary>
    public IReadOnlyList<WikiMenuItem> Topics => topics ??= sheet is null ? [] : WikiMenu.For(sheet.Module);

    public IRelayCommand BackCommand { get; }
    public IRelayCommand ConfirmCommand { get; }
    public IRelayCommand GuideCommand { get; }
    public IRelayCommand<string> OpenTopicCommand { get; }

    /// <summary>The sheet changed its heading or the project its state: the texts are read again.</summary>
    public void Update()
    {
        if (!ReferenceEquals(sheet, workspace.Active))
        {
            if (sheet is not null) sheet.StateChanged -= OnSheetState;
            sheet = workspace.Active;
            if (sheet is not null) sheet.StateChanged += OnSheetState;
            topics = null;
        }
        foreach (var name in new[] { nameof(IsVisible), nameof(Heading), nameof(Module), nameof(BackLabel), nameof(CanConfirm), nameof(GuideId), nameof(Topics), nameof(ReloadNotice) }) OnPropertyChanged(name);
        BackCommand.NotifyCanExecuteChanged(); ConfirmCommand.NotifyCanExecuteChanged(); GuideCommand.NotifyCanExecuteChanged();
    }

    protected override void OnDisposed()
    {
        workspace.ActiveChanged -= OnActiveChanged;
        if (sheet is not null) sheet.StateChanged -= OnSheetState;
    }

    private void OnActiveChanged(object? sender, EventArgs e) => Update();

    private void OnSheetState(object? sender, EventArgs e) => Update();
}
