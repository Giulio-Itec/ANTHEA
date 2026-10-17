using System.Text.Json.Nodes;
using Anthea.Application.Stratigraphy;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Mvvm;
using Anthea.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.Stratigraphy;

/// <summary>
/// The editor of a stratigraphy document (F5.6b): the behaviour of the historic StratigraphyWorkspace without WPF, on the same document,
/// with the same writes in the same order. The sheet (<see cref="StratigraphySheetViewModel"/>) and the preview of an AI draft
/// (<see cref="StratigraphyImportDialogViewModel"/>) use it, as the historic view was used by the sheet and by the import window.
/// <list type="bullet">
/// <item>General data, the identity (name, family, behaviour), the description and colour, the thickness, the means, the minima and the
/// notes of the selected layer: <see cref="FormViewModel"/> with the fields of <see cref="StratigraphyFields"/> and the rule of the view (the
/// raw text into the document at every change; a choice writes the chosen item and shows its default while the key is missing), one form
/// per visual block of the XAML view (D10, S8: the note on mixed soils between the behaviour and the description). The forms of the layer
/// are built again at every change of layer, extent or minima, as the view built its controls again (ShowLayer). Every form opens the Wiki
/// through the navigator of the services (<see cref="FormViewModel.WikiNavigator"/>).</item>
/// <item>The layers: <see cref="Layers"/> (one row per layer object), the selection (<see cref="SelectedLayerIndex"/>, allowed also when
/// read-only), the actions «+ Strato», «Duplica», «Elimina», «↑», «↓» on the selected layer (the first when none is selected), whose
/// errors are shown with <see cref="IMessageService.Inform"/> as the message boxes of the view; the commands of the view go through the
/// guard of the commands (Q19).</item>
/// <item>«Ultimo strato esteso indefinitamente», only for the last layer; «Gestisci anche i valori minimi», whose removal asks
/// <see cref="IConfirmationService.ConfirmDelete"/>: the answer «No» still reports a change, as the view does. After a click the two check
/// boxes show again the state of the layer, as the controls built again by the view.</item>
/// <item>After every change the status (<see cref="StratigraphyPresentation.Status"/>) and the profile again, then <c>changed</c>, the
/// Modified of the view.</item>
/// <item>Read-only (<see cref="IsReadOnly"/>, T2): the forms refuse the typing, the actions and the check boxes do nothing.</item>
/// </list>
/// </summary>
public sealed class StratigraphyEditorViewModel : ObservableObject
{
    private readonly PresentationServices services;
    private readonly Action changed;
    private readonly FormViewModel general;
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
        Data = data; this.services = services; this.changed = changed; ImportVisible = importVisible; this.readOnly = readOnly;
        var guard = new CommandGuard(services.Messages);
        AddLayerCommand = GuardedCommands.Create(guard, AddLayer, () => CanEdit);
        DuplicateLayerCommand = GuardedCommands.Create(guard, DuplicateLayer, () => CanEdit);
        RemoveLayerCommand = GuardedCommands.Create(guard, RemoveLayer, () => CanEdit);
        MoveLayerUpCommand = GuardedCommands.Create(guard, MoveLayerUp, () => CanEdit);
        MoveLayerDownCommand = GuardedCommands.Create(guard, MoveLayerDown, () => CanEdit);
        ToggleInfiniteCommand = GuardedCommands.Create(guard, ToggleInfinite, () => CanToggleInfinite);
        ToggleMinimumCommand = GuardedCommands.Create(guard, ToggleMinimum, () => CanEdit);
        general = Form(data, StratigraphyFields.General(), _ => Edited());
        Rebuild(0);
    }

    /// <summary>The document.</summary>
    public JsonObject Data { get; }

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
                foreach (var layer in StratigraphyDocument.Layers(Data)) Layers.Add(new JsonRowViewModel(layer!.AsObject()));
            }
        }
        finally { rebuilding = false; }
        selected = Math.Clamp(selection, 0, Layers.Count - 1);
        OnPropertyChanged(nameof(SelectedLayerIndex));
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

    /// <summary>The fields of the selected layer again (ShowLayer of the view).</summary>
    private void ShowLayer()
    {
        identity = description = thickness = means = minima = notes = null;
        if (SelectedLayer() is { } layer)
        {
            var fields = StratigraphyFields.Layer();
            identity = Form(layer, fields[..3], key =>
            {
                // The list shows the name of the selected layer as it is typed.
                if (key == "name") Layers[Selected].Refresh();
                Edited();
            });
            description = Form(layer, fields[3..], _ => Edited());
            infinite = StratigraphyDocument.Infinite(layer);
            canToggleInfinite = Selected == Layers.Count - 1;
            if (!infinite) thickness = Form(layer, StratigraphyFields.Thickness(), _ => Edited());
            hasMinimum = layer["minimum"] is JsonObject;
            means = Form(layer, StratigraphyFields.Means(), _ => Edited());
            if (layer["minimum"] is JsonObject minimum) minima = Form(minimum, StratigraphyFields.Minima(), _ => Edited());
            notes = Form(layer, StratigraphyFields.Notes(), _ => Edited());
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
        var current = StratigraphyPresentation.Status(Data);
        Status = current.Text;
        profile = current.Drawn ? Data : null;
        OnPropertyChanged(nameof(Profile));
        ProfileSelection = Selected;
    }

    private void NotifyCommands()
    {
        foreach (var command in new[] { AddLayerCommand, DuplicateLayerCommand, RemoveLayerCommand, MoveLayerUpCommand, MoveLayerDownCommand, ToggleInfiniteCommand, ToggleMinimumCommand })
            command.NotifyCanExecuteChanged();
    }

    /// <summary>A form of the editor: compact (D10), read-only with the editor, with the Wiki navigator of the services.</summary>
    private FormViewModel Form(JsonObject values, FieldSpec[] fields, Action<string> edited) =>
        new(values, fields, edited, compact: true) { IsReadOnly = readOnly, WikiNavigator = services.Wiki };
}
