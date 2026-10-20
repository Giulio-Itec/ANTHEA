using Anthea.Application.Examples;
using Anthea.Application.Modules;
using Anthea.Presentation.Mvvm;
using Anthea.Presentation.Services;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Examples;

public sealed record ExampleGroup(string Module, string Title, IReadOnlyList<ExampleEntry> Entries)
{
    public string Label => $"{Title} ({Entries.Count})";
}

public sealed class ExampleLibraryViewModel : ViewModelBase
{
    private ExampleGroup? selectedGroup;
    private ExampleEntry? selectedExample;
    public IReadOnlyList<ExampleGroup> Groups { get; }
    public string Summary { get; }
    public IRelayCommand OpenCommand { get; }
    public IReadOnlyList<ExampleEntry> Examples => SelectedGroup?.Entries ?? [];
    public ExampleGroup? SelectedGroup
    {
        get => selectedGroup;
        set
        {
            if (!SetProperty(ref selectedGroup, value)) return;
            OnPropertyChanged(nameof(Examples));
            SelectedExample = Examples.FirstOrDefault();
        }
    }
    public ExampleEntry? SelectedExample
    {
        get => selectedExample;
        set { if (SetProperty(ref selectedExample, value)) OpenCommand.NotifyCanExecuteChanged(); }
    }

    public ExampleLibraryViewModel(ExampleCatalog catalog, Action<ExampleEntry> open, IMessageService messages)
    {
        Groups = ModuleDirectory.All.OrderBy(m => m.Area).ThenBy(m => m.Name)
            .Select(m => new ExampleGroup(m.Id, m.Name, catalog.Entries.Where(e => e.Module == m.Id).ToArray()))
            .Where(g => g.Entries.Count > 0).ToArray();
        Summary = catalog.Entries.Count == 0
            ? "Esempi non installati. Riesegui l’installer e seleziona il componente Esempi."
            : $"{catalog.Entries.Count} esempi · {Groups.Count} moduli. Apri una copia e salvala nella cartella dei tuoi lavori.";
        OpenCommand = GuardedCommands.Create(new CommandGuard(messages),
            () => open(SelectedExample!), () => SelectedExample is not null && !IsDisposed);
        SelectedGroup = Groups.FirstOrDefault();
    }
}
