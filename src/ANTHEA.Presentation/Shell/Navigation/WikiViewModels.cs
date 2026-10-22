using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Anthea.Application.Modules;
using Anthea.Calculations;
using Anthea.Presentation.Mvvm;
using Anthea.Wiki;

namespace Anthea.Presentation.Shell.Navigation;

/// <summary>
/// The examples of the Wiki that open a module (F5.13 c; MainWindow.WikiExamples, the same code): the definition of an example read from the
/// resources of the Wiki, the data of the module with its overrides merged into the defaults (objects merged, the combination families
/// replaced as a whole), validated. MainWindow keeps its own copy until the historic window retires (part g): the WPF check of the draft of
/// the shell compares the two on every example.
/// </summary>
public static class WikiExamples
{
    /// <summary>An identifier that is not a name of an example.</summary>
    public const string InvalidExample = "Identificativo esempio non valido.";

    /// <summary>An example of another module.</summary>
    public const string OtherModule = "Esempio non compatibile con il modulo richiesto.";

    /// <summary>The definition of <paramref name="example"/> («moduleId», «overrides»…).</summary>
    public static JsonObject Definition(string example)
    {
        ArgumentNullException.ThrowIfNull(example);
        if (!Regex.IsMatch(example, "^[a-z0-9-]+$")) throw new ArgumentException(InvalidExample);
        using var stream = WikiCatalog.Resource("examples." + example + ".json");
        return JsonNode.Parse(stream)!.AsObject();
    }

    /// <summary>The data of <paramref name="module"/> for <paramref name="example"/>: the defaults with the overrides, validated.</summary>
    public static JsonObject Create(string module, string example)
    {
        var definition = Definition(example);
        if (definition.S("moduleId") != module) throw new ArgumentException(OtherModule);
        var data = ModuleDirectory.CreateData(module);
        static void Apply(JsonObject target, JsonObject values)
        {
            foreach (var (key, value) in values)
            {
                if (key != "combinazioni" && value is JsonObject child && target[key] is JsonObject existing) Apply(existing, child);
                else target[key] = value?.DeepClone();
            }
        }
        // Replace combination families as a whole, avoiding residual factory load cases.
        Apply(data, definition["overrides"]!.AsObject());
        ModuleDirectory.ValidateData(module, data);
        return data;
    }
}

/// <summary>
/// The content of the Wiki tab (F5.13 c, Q18 a, SH4): the article asked last and the two requests of the Wiki view to the shell, the module of an
/// example and the return to the work (WikiView of today, with the callbacks of MainWindow.ShowWiki). The view of the Wiki stays the historic
/// WikiView, hosted by the template of this ViewModel.
/// </summary>
public sealed class WikiTabViewModel : ViewModelBase
{
    private readonly Action<string, string?> openModule;
    private readonly Action returnToWork;

    /// <param name="openModule">A module asked by the Wiki, with an example or without (ShellViewModel.OpenWikiModule).</param>
    /// <param name="returnToWork">«Torna al lavoro» of the Wiki (ShellViewModel.ReturnToWork).</param>
    public WikiTabViewModel(Action<string, string?> openModule, Action returnToWork)
    {
        this.openModule = openModule ?? throw new ArgumentNullException(nameof(openModule));
        this.returnToWork = returnToWork ?? throw new ArgumentNullException(nameof(returnToWork));
    }

    /// <summary>The article asked last, null for the start page of the Wiki.</summary>
    public string? Uri { get; private set; }

    /// <summary>An article is asked: the view navigates to it.</summary>
    public event EventHandler<string>? NavigationRequested;

    /// <summary>Asks the view to show <paramref name="uri"/>.</summary>
    public void Navigate(string uri)
    {
        ArgumentException.ThrowIfNullOrEmpty(uri);
        ThrowIfDisposed();
        Uri = uri;
        OnPropertyChanged(nameof(Uri));
        NavigationRequested?.Invoke(this, uri);
    }

    /// <summary>The Wiki asks a module, with the identifier of an example or without.</summary>
    public void OpenModule(string module, string? example) => openModule(module, example);

    /// <summary>«Torna al lavoro» of the Wiki.</summary>
    public void ReturnToWork() => returnToWork();
}
