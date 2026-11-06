using Anthea.Presentation.Mvvm;
using Anthea.Presentation.Services;
using Anthea.Reports;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Shell.Commands;

public sealed class ReportTemplateMenuViewModel : ViewModelBase
{
    private readonly ReportTemplateStore store;
    public const string Title = "Template dei report";
    public const string Description = "Importa solo gli stili Word. Cartiglio, sommario, intestazioni e piè di pagina del modello sono esclusi. La scelta vale subito per i prossimi report Word e PDF.";
    public string Current => store.Error is not null ? "Template salvato non leggibile: scegliere o ripristinare" : store.Name is { } name ? "In uso: " + name : "In uso: stile ANTHEA";
    public IRelayCommand SelectCommand { get; }
    public IRelayCommand ResetCommand { get; }

    public ReportTemplateMenuViewModel(ReportTemplateStore store, IFileDialogService files, IMessageService messages)
    {
        this.store = store;
        var guard = new CommandGuard(messages);
        SelectCommand = GuardedCommands.Create(guard, () =>
        {
            string? path = files.OpenFile(new OpenFileRequest([new FileFilter("Stili Word", "*.docx", "*.dotx")], Title: "Scegli il template di stili dei report"));
            if (path is null) return;
            store.Select(path);
            Changed();
            messages.Inform(Title, Description);
        });
        ResetCommand = GuardedCommands.Create(guard, () => { store.Reset(); Changed(); });
    }

    private void Changed() => OnPropertyChanged(nameof(Current));
}
