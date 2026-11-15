using Anthea.Presentation.Mvvm;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.EmbeddedWalls;

/// <summary>
/// A document of the central area of the paratie sheet (organizzazione del modulo paratie, progetto del 16/11, step 1): the section of the
/// phase, always open, or one of the views that the command bar opens on request and its tab closes (tables, results, checks). State of
/// the view only: it is never saved and it changes neither the data nor the calculation.
/// </summary>
public sealed class EmbeddedWallDocumentTab : ObservableObject
{
    private bool isOpen;

    internal EmbeddedWallDocumentTab(string key, string title, bool canClose, CommandGuard guard, Action<string> close)
    {
        Key = key; Title = title; CanClose = canClose; isOpen = !canClose;
        CloseCommand = GuardedCommands.Create(guard, () => close(key), () => CanClose);
    }

    public string Key { get; }
    public string Title { get; }
    /// <summary>False for the section of the phase, which stays open.</summary>
    public bool CanClose { get; }
    public bool IsOpen { get => isOpen; internal set => SetProperty(ref isOpen, value); }
    public IRelayCommand CloseCommand { get; }
}

/// <summary>
/// The open documents of the paratie sheet and the active one (step 1 of the project of 16/11). The thirteen views of the former fixed tabs
/// keep their content; they are opened by the command bar, the work tree and the commands of the sheet, and closed from their tab. Closing
/// the active document activates the one used before it, finally the section.
/// </summary>
public sealed class EmbeddedWallDocuments : ObservableObject
{
    public const string Section = "sezione", Tables = "tabelle", Embedment = "infissione", Diagrams = "diagrammi", ConcreteChecks = "verifiche-ca",
        Hydraulics = "filtrazione", HydraulicChecks = "upl-hyd", GlobalStability = "stabilita", SteelSupports = "acciaio",
        AnchorPullout = "sfilamento", SteelSections = "tubolari", RcPiles = "pali-ca", Envelopes = "inviluppi";

    private readonly Dictionary<string, EmbeddedWallDocumentTab> byKey = new(StringComparer.Ordinal);
    private readonly List<string> used = [Section];
    private string active = Section;

    internal EmbeddedWallDocuments(CommandGuard guard)
    {
        (string Key, string Title)[] catalogue =
        [
            (Section, "Sezione"), (Tables, "Dati e fasi"), (Embedment, "Ricerca infissione"), (Diagrams, "Diagrammi e risultati"),
            (ConcreteChecks, "C.a. e sostegni"), (Hydraulics, "Filtrazione"), (HydraulicChecks, "UPL / HYD"), (GlobalStability, "Stabilità globale"),
            (SteelSupports, "Acciaio sostegni"), (AnchorPullout, "Sfilamento tiranti"), (SteelSections, "Sezioni acciaio"), (RcPiles, "Pali in c.a."),
            (Envelopes, "Inviluppi")
        ];
        All = Array.AsReadOnly(catalogue.Select(d => byKey[d.Key] = new EmbeddedWallDocumentTab(d.Key, d.Title, d.Key != Section, guard, Close)).ToArray());
        ShowCommand = new RelayCommand<string>(key => guard.Run(() => Show(key ?? throw new ArgumentNullException(nameof(key)))));
        CloseAllCommand = GuardedCommands.Create(guard, CloseAll);
    }

    /// <summary>Every document, in the order of their tabs.</summary>
    public IReadOnlyList<EmbeddedWallDocumentTab> All { get; }
    public EmbeddedWallDocumentTab this[string key] => byKey.TryGetValue(key, out var document) ? document
        : throw new ArgumentException("Documento paratie sconosciuto: " + key, nameof(key));
    /// <summary>The open documents, in the order of their tabs.</summary>
    public IReadOnlyList<EmbeddedWallDocumentTab> Open => All.Where(d => d.IsOpen).ToArray();

    /// <summary>Key of the active document; setting a document opens it.</summary>
    public string Active
    {
        get => active;
        set { if (value is not null && byKey.ContainsKey(value)) Show(value); }
    }

    public IRelayCommand<string> ShowCommand { get; }
    public IRelayCommand CloseAllCommand { get; }

    /// <summary>Opens the document, when closed, and makes it the active one.</summary>
    public void Show(string key)
    {
        var document = this[key];
        bool opened = !document.IsOpen;
        document.IsOpen = true;
        used.Remove(key); used.Add(key);
        if (opened) OnPropertyChanged(nameof(Open));
        SetProperty(ref active, key, nameof(Active));
    }

    /// <summary>Closes the document (never the section); the active one passes to the document used before.</summary>
    public void Close(string key)
    {
        var document = this[key];
        if (!document.CanClose || !document.IsOpen) return;
        used.Remove(key);
        if (active == key) SetProperty(ref active, used.LastOrDefault(k => byKey[k].IsOpen) ?? Section, nameof(Active));
        document.IsOpen = false;
        OnPropertyChanged(nameof(Open));
    }

    /// <summary>Closes every document except the section, which becomes the active one.</summary>
    public void CloseAll()
    {
        SetProperty(ref active, Section, nameof(Active));
        foreach (var document in All.Where(d => d.CanClose)) document.IsOpen = false;
        used.Clear(); used.Add(Section);
        OnPropertyChanged(nameof(Open));
    }
}
