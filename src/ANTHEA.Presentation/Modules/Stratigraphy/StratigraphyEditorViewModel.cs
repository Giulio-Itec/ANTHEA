using System.Text.Json.Nodes;
using System.Collections.ObjectModel;
using Anthea.Application.Stratigraphy;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules.VerticalPile;
using Anthea.Presentation.Mvvm;
using Anthea.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.Stratigraphy;

/// <summary>
/// Shared editor for the stratigraphy sheet and the reviewed import preview. Layer rows own their forms:
/// the full-width table and the selected-layer details edit the same fields, with no duplicate writes.
/// Commands preserve the document operations, optional minima, infinite last layer, validation and read-only
/// rules of the historic editor. Every edit refreshes the status and profile, then notifies the sheet.
/// </summary>
public sealed class StratigraphyEditorViewModel : ObservableObject
{
    private const double ProfileRowPixels = 132, ProfilePaddingPixels = 110;
    private readonly PresentationServices services;
    private readonly Action changed;
    private FormViewModel general;
    private int selectedProfile;
    private bool rebuildingProfiles;
    private bool readOnly, infinite, hasMinimum, canToggleInfinite, rebuilding;
    private int selected = -1;
    private FormViewModel? identity, description, thickness, means, minima, notes;
    private string status = "";
    private JsonObject? profile;
    private int profileSelection;

    /// <param name="data">The document edited in place.</param>
    /// <param name="services">Messages, questions and the Wiki of the editor.</param>
    /// <param name="changed">Called after every change of the user (the Modified of the view).</param>
    /// <param name="importVisible">The import button is shown (the sheet); the preview of a draft has none.</param>
    /// <param name="readOnly">Read-only at creation.</param>
    public StratigraphyEditorViewModel(JsonObject data, PresentationServices services, Action changed, bool importVisible, bool readOnly = false)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(changed);
        RootData = data; this.services = services; this.changed = changed; ImportVisible = importVisible; this.readOnly = readOnly;
        var guard = new CommandGuard(services.Messages);
        AddLayerCommand = GuardedCommands.Create(guard, AddLayer, () => CanEdit);
        DuplicateLayerCommand = GuardedCommands.Create(guard, DuplicateLayer, () => CanEdit);
        RemoveLayerCommand = GuardedCommands.Create(guard, RemoveLayer, () => CanEdit);
        MoveLayerUpCommand = GuardedCommands.Create(guard, MoveLayerUp, () => CanEdit);
        MoveLayerDownCommand = GuardedCommands.Create(guard, MoveLayerDown, () => CanEdit);
        ToggleInfiniteCommand = GuardedCommands.Create(guard, ToggleInfinite, () => CanToggleInfinite);
        ToggleMinimumCommand = GuardedCommands.Create(guard, ToggleMinimum, () => CanEdit);
        AddProfileCommand = GuardedCommands.Create(guard, () => ChangeProfiles(() => StratigraphyProfiles.Add(RootData)), () => CanEdit);
        DuplicateProfileCommand = GuardedCommands.Create(guard, () => ChangeProfiles(() => StratigraphyProfiles.Add(RootData, selectedProfile)), () => CanEdit);
        RemoveProfileCommand = GuardedCommands.Create(guard, RemoveProfile, () => CanEdit && Profiles.Count > 1);
        CopyProfileIntoCommand = GuardedCommands.CreateAsync(guard, _ => ChooseProfileCopyAsync(copyInto: true), CancellationToken.None, () => CanEdit);
        CopyProfileFromCommand = GuardedCommands.CreateAsync(guard, _ => ChooseProfileCopyAsync(copyInto: false), CancellationToken.None, () => CanEdit);
        general = General();
        RefreshProfiles(0);
        Rebuild(0);
    }

    /// <summary>The document.</summary>
    public JsonObject RootData { get; }
    public JsonObject Data => StratigraphyProfiles.All(RootData)[selectedProfile];
    public JsonRowCollection Profiles { get; } = [];
    public int SelectedProfileIndex
    {
        get => selectedProfile;
        set
        {
            if (rebuildingProfiles || value < 0 || value >= Profiles.Count || value == selectedProfile) return;
            selectedProfile = value;
            OnPropertyChanged(); OnPropertyChanged(nameof(Data));
            general = General();
            OnPropertyChanged(nameof(GeneralForm)); OnPropertyChanged(nameof(CompactGeneralFields));
            Rebuild(0);
        }
    }
    public IEnumerable<FieldViewModel> CompactGeneralFields => general.Fields.Where(f => f.Key is not ("scenario" or "description"));
    public double ProfileHeight => Math.Max(420, TableRows.Count * ProfileRowPixels + ProfilePaddingPixels);
    public IRelayCommand AddProfileCommand { get; }
    public IRelayCommand DuplicateProfileCommand { get; }
    public IRelayCommand RemoveProfileCommand { get; }

    /// <summary>«Copia in…»: copies the selected stratigraphy into the one chosen in the dialog (or into a new one).</summary>
    public IAsyncRelayCommand CopyProfileIntoCommand { get; }

    /// <summary>«Copia da…»: replaces the selected stratigraphy with a copy of the one chosen in the dialog.</summary>
    public IAsyncRelayCommand CopyProfileFromCommand { get; }

    /// <summary>
    /// Copies the stratigraphy <paramref name="source"/> into <paramref name="destination"/> (−1: a new stratigraphy). An existing
    /// destination keeps its name and is replaced after the confirmation of the user; the destination is selected. False when nothing was copied.
    /// </summary>
    public bool CopyProfile(int source, int destination)
    {
        if (!CanEdit || source < 0 || source >= Profiles.Count || destination < -1 || destination >= Profiles.Count || source == destination) return false;
        if (destination == -1)
        {
            ChangeProfiles(() => StratigraphyProfiles.Add(RootData, source));
            return true;
        }
        if (!services.Confirmations.ConfirmDelete("Copia stratigrafia", "Sostituire dati generali e strati della stratigrafia «" + ProfileName(destination)
            + "» con una copia di «" + ProfileName(source) + "»?")) return false;
        ChangeProfiles(() => { StratigraphyProfiles.CopyInto(RootData, source, destination); return destination; });
        return true;
    }

    private async Task ChooseProfileCopyAsync(bool copyInto)
    {
        if (!CanEdit) return;
        int current = selectedProfile;
        var choices = Enumerable.Range(0, Profiles.Count).Where(i => i != current).Select(i => new SurveyCopyChoice(i, ProfileName(i))).ToList();
        if (copyInto) choices.Add(new(-1, "Nuova stratigrafia"));
        if (choices.Count == 0) { services.Messages.Inform(StratigraphyPresentation.Caption, "Non ci sono altre stratigrafie da cui copiare."); return; }
        var dialog = new SurveyCopyDialogViewModel(copyInto ? "Copia stratigrafia in…" : "Copia stratigrafia da…",
            copyInto ? "Copia la stratigrafia «" + ProfileName(current) + "» in:"
                     : "Sostituisci dati generali e strati della stratigrafia «" + ProfileName(current) + "» copiando da:", choices);
        var chosen = await services.Dialogs.ShowDialogAsync(dialog);
        if (chosen is null) return;
        CopyProfile(copyInto ? current : chosen.Survey, copyInto ? chosen.Survey : current);
    }

    private string ProfileName(int index)
    {
        string? name = StratigraphyProfiles.All(RootData)[index]["name"]?.ToString();
        return string.IsNullOrWhiteSpace(name) ? "Stratigrafia " + (index + 1) : name;
    }

    private FormViewModel General() => Form(Data, StratigraphyFields.General(), _ =>
    {
        if (Profiles.Count > selectedProfile) Profiles[selectedProfile].Refresh();
        Edited();
    });

    private void RefreshProfiles(int index)
    {
        rebuildingProfiles = true;
        using (Profiles.DeferRefresh())
        {
            Profiles.Clear();
            foreach (var item in StratigraphyProfiles.All(RootData)) Profiles.Add(new(item));
        }
        rebuildingProfiles = false;
        selectedProfile = index;
        OnPropertyChanged(nameof(SelectedProfileIndex)); OnPropertyChanged(nameof(Data));
        general = General();
        OnPropertyChanged(nameof(GeneralForm)); OnPropertyChanged(nameof(CompactGeneralFields));
        RemoveProfileCommand.NotifyCanExecuteChanged();
    }

    private void ChangeProfiles(Func<int> action)
    {
        if (readOnly) return;
        RefreshProfiles(action());
        Rebuild(0);
        changed();
    }

    private void RemoveProfile()
    {
        if (!CanEdit || Profiles.Count <= 1 || !services.Confirmations.ConfirmDelete("Elimina stratigrafia",
            "Eliminare la stratigrafia «" + Data["name"] + "» e tutti i suoi strati?")) return;
        ChangeProfiles(() =>
        {
            int next = Math.Max(0, selectedProfile - 1);
            StratigraphyProfiles.Remove(RootData, selectedProfile);
            return next;
        });
    }

    /// <summary>The import button is part of the editor (the sheet), not of the preview of a draft.</summary>
    public bool ImportVisible { get; }

    /// <summary>Read-only (a previous revision, or the sheet closed): typing, choices, check boxes and actions refused.</summary>
    public bool IsReadOnly
    {
        get => readOnly;
        set
        {
            if (!SetProperty(ref readOnly, value)) return;
            foreach (var form in Forms) form.IsReadOnly = value;
            foreach (var row in TableRows) row.SetReadOnly(value);
            OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(CanToggleInfinite));
            NotifyCommands();
        }
    }

    /// <summary>The user can change the document.</summary>
    public bool CanEdit => !readOnly;

    // ---------------------------------------------------------------- general data and layers

    /// <summary>Choice of the parameters, name, description, ground elevation, water table and its depth.</summary>
    public FormViewModel GeneralForm => general;

    /// <summary>The layers, from the top: one row on every layer object of the document (the list shows its name).</summary>
    public JsonRowCollection Layers { get; } = [];

    /// <summary>All layer fields, shared by the full-width table and the selected-layer details.</summary>
    public ObservableCollection<StratigraphyLayerRowViewModel> TableRows { get; } = [];

    /// <summary>
    /// The selected layer (−1 when none is selected: the actions use the first). Allowed also when read-only. While the list is built again
    /// (<see cref="Rebuild"/>) the selection that the list loses is ignored, as the view ignored its SelectionChanged while refreshing.
    /// </summary>
    public int SelectedLayerIndex
    {
        get => selected;
        set
        {
            if (rebuilding || value == selected || value < -1 || value >= Layers.Count) return;
            selected = value;
            OnPropertyChanged();
            ShowLayer();
            Refresh();
        }
    }

    /// <summary>The layer the actions and the fields refer to: the selected one, the first when none is selected.</summary>
    private int Selected => Math.Max(0, selected);

    /// <summary>«+ Strato»: a new layer, before the last one when it is of indefinite extent.</summary>
    public void AddLayer() => Act(() => StratigraphyDocument.Add(Data));

    /// <summary>«Duplica»: a copy of the selected layer after it, with a new identifier.</summary>
    public void DuplicateLayer() => Act(() => StratigraphyDocument.Duplicate(Data, Selected));

    /// <summary>«Elimina»: removes the selected layer.</summary>
    public void RemoveLayer() => Act(() => StratigraphyDocument.Remove(Data, Selected));

    /// <summary>«↑»: the selected layer moves up and stays selected.</summary>
    public void MoveLayerUp()
    {
        int layer = Selected;
        Act(() => StratigraphyDocument.Move(Data, layer, layer - 1), layer - 1);
    }

    /// <summary>«↓»: the selected layer moves down and stays selected.</summary>
    public void MoveLayerDown()
    {
        int layer = Selected;
        Act(() => StratigraphyDocument.Move(Data, layer, layer + 1), layer + 1);
    }

    /// <summary>«+ Strato» of the view.</summary>
    public IRelayCommand AddLayerCommand { get; }

    /// <summary>«Duplica» of the view.</summary>
    public IRelayCommand DuplicateLayerCommand { get; }

    /// <summary>«Elimina» of the view.</summary>
    public IRelayCommand RemoveLayerCommand { get; }

    /// <summary>«↑» of the view.</summary>
    public IRelayCommand MoveLayerUpCommand { get; }

    /// <summary>«↓» of the view.</summary>
    public IRelayCommand MoveLayerDownCommand { get; }

    // ---------------------------------------------------------------- selected layer

    /// <summary>Name, family and behaviour of the selected layer; null without a selected layer.</summary>
    public FormViewModel? LayerForm => identity;

    /// <summary>
    /// Description and colour of the selected layer, after the note on mixed soils (the second part of the identity of the historic view,
    /// a form of its own for the block of the view); null without a selected layer.
    /// </summary>
    public FormViewModel? DescriptionForm => description;

    /// <summary>The thickness of the selected layer; null when the layer is of indefinite extent.</summary>
    public FormViewModel? ThicknessForm => thickness;

    /// <summary>The means of the selected layer: parameters, qualification, source.</summary>
    public FormViewModel? MeansForm => means;

    /// <summary>The minima of the selected layer; null when it has none (<see cref="MinimaNote"/> is shown).</summary>
    public FormViewModel? MinimaForm => minima;

    /// <summary>The notes of the selected layer.</summary>
    public FormViewModel? NotesForm => notes;

    /// <summary>The note shown in place of the minima of a layer that has none.</summary>
    public string MinimaNote => StratigraphyPresentation.MinimaNote;

    /// <summary>The forms shown, in the order of the sheet.</summary>
    public IEnumerable<FormViewModel> Forms => new[] { general, identity, description, thickness, means, minima, notes }.OfType<FormViewModel>();

    /// <summary>«Ultimo strato esteso indefinitamente» is ticked (raised again after every click, as the check box built again by the view).</summary>
    public bool IsInfinite => infinite;

    /// <summary>The extent can be changed: the selected layer is the last one and the editor is not read-only.</summary>
    public bool CanToggleInfinite => canToggleInfinite && !readOnly;

    /// <summary>«Gestisci anche i valori minimi (opzionali)» is ticked (raised again after every click: the answer «No» keeps it ticked).</summary>
    public bool HasMinimum => hasMinimum;

    /// <summary>The click on «Ultimo strato esteso indefinitamente»: indefinite without thickness, or finite again with 1 m.</summary>
    public void ToggleInfinite()
    {
        if (!CanToggleInfinite || SelectedLayer() is not { } layer) return;
        StratigraphyPresentation.SetInfinite(layer, !infinite);
        TableRows[Selected].RefreshOptions();
        ShowLayer();
        Edited();
    }

    /// <summary>
    /// The click on «Gestisci anche i valori minimi»: new empty minima, or their removal after the question; the answer «No» keeps the
    /// minima and still reports a change, as the historic view does.
    /// </summary>
    public void ToggleMinimum()
    {
        if (readOnly || SelectedLayer() is not { } layer) return;
        if (!hasMinimum) layer["minimum"] = StratigraphyDocument.NewMinimum();
        else if (services.Confirmations.ConfirmDelete(StratigraphyPresentation.MinimaCaption, StratigraphyPresentation.MinimaRemoval)) layer["minimum"] = null;
        TableRows[Selected].RefreshOptions();
        ShowLayer();
        Edited();
    }

    /// <summary>The check box «Ultimo strato esteso indefinitamente» of the view.</summary>
    public IRelayCommand ToggleInfiniteCommand { get; }

    /// <summary>The check box «Gestisci anche i valori minimi (opzionali)» of the view.</summary>
    public IRelayCommand ToggleMinimumCommand { get; }

    // ---------------------------------------------------------------- status and profile

    /// <summary>The warnings, «Dati validi…» or the error to correct before saving.</summary>
    public string Status { get => status; private set => SetProperty(ref status, value); }

    /// <summary>The document drawn by the profile; null when it is not valid. Raised again after every change (the drawing is redrawn).</summary>
    public JsonObject? Profile => profile;

    /// <summary>The layer highlighted in the profile (the selected one, the first when none is selected).</summary>
    public int ProfileSelection { get => profileSelection; private set => SetProperty(ref profileSelection, value); }

    /// <summary>Builds the editor again on the document (after a replacement of the data), with the layer <paramref name="selection"/>.</summary>
    internal void Rebuild(int selection)
    {
        rebuilding = true;
        try
        {
            using (Layers.DeferRefresh())
            {
                Layers.Clear();
                TableRows.Clear();
                foreach (var layer in StratigraphyDocument.Layers(Data))
                {
                    var values = layer!.AsObject();
                    var item = new JsonRowViewModel(values);
                    Layers.Add(item);
                    TableRows.Add(new(values, services, () => { item.Refresh(); Edited(); }, readOnly, TableRows.Count));
                }
            }
        }
        finally { rebuilding = false; }
        selected = Math.Clamp(selection, 0, Layers.Count - 1);
        OnPropertyChanged(nameof(SelectedLayerIndex));
        OnPropertyChanged(nameof(ProfileHeight));
        ShowLayer();
        Refresh();
    }

    // ---------------------------------------------------------------- internals

    private JsonObject? SelectedLayer() => selected < 0 ? null : StratigraphyDocument.Layers(Data)[Selected]!.AsObject();

    /// <summary>Edit of the view: the action, then the layers again with the selection, then the change; the errors are shown.</summary>
    private void Act(Action action, int? selection = null)
    {
        if (readOnly) return;
        try
        {
            action();
            Rebuild(selection ?? Selected);
            changed();
        }
        catch (ArgumentException error) { services.Messages.Inform(StratigraphyPresentation.Caption, error.Message); }
    }

    /// <summary>A field, an extent or the minima changed: status and profile again, then the change (Changed of the view).</summary>
    private void Edited()
    {
        Refresh();
        changed();
    }

    /// <summary>Select the existing row forms; switching layers never writes or recreates their values.</summary>
    private void ShowLayer()
    {
        identity = description = thickness = means = minima = notes = null;
        if (SelectedLayer() is { } layer)
        {
            var row = TableRows[Selected];
            identity = row.Identity;
            description = row.Description;
            infinite = StratigraphyDocument.Infinite(layer);
            canToggleInfinite = Selected == Layers.Count - 1;
            thickness = row.Thickness;
            hasMinimum = layer["minimum"] is JsonObject;
            means = row.Means;
            minima = row.Minima;
            notes = row.Notes;
        }
        else canToggleInfinite = false;
        // Raised also when the state does not change: the check box clicked by the user shows again the state of the layer.
        OnPropertyChanged(nameof(IsInfinite)); OnPropertyChanged(nameof(HasMinimum));
        OnPropertyChanged(nameof(LayerForm)); OnPropertyChanged(nameof(DescriptionForm)); OnPropertyChanged(nameof(ThicknessForm)); OnPropertyChanged(nameof(MeansForm));
        OnPropertyChanged(nameof(MinimaForm)); OnPropertyChanged(nameof(NotesForm)); OnPropertyChanged(nameof(Forms)); OnPropertyChanged(nameof(CanToggleInfinite));
        ToggleInfiniteCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Refresh of the view: status, profile and its selection.</summary>
    private void Refresh()
    {
        var current = StratigraphyPresentation.Status(StratigraphyProfiles.CopyProfile(Data));
        Status = current.Text;
        profile = current.Drawn ? Data : null;
        OnPropertyChanged(nameof(Profile));
        ProfileSelection = Selected;
    }

    private void NotifyCommands()
    {
        foreach (var command in new[] { AddLayerCommand, DuplicateLayerCommand, RemoveLayerCommand, MoveLayerUpCommand, MoveLayerDownCommand, ToggleInfiniteCommand, ToggleMinimumCommand, AddProfileCommand, DuplicateProfileCommand, RemoveProfileCommand, CopyProfileIntoCommand, CopyProfileFromCommand })
            command.NotifyCanExecuteChanged();
    }

    /// <summary>A form of the editor: compact (D10), read-only with the editor, with the Wiki navigator of the services.</summary>
    private FormViewModel Form(JsonObject values, FieldSpec[] fields, Action<string> edited) =>
        new(values, fields, edited, compact: true) { IsReadOnly = readOnly, WikiNavigator = services.Wiki };
}
