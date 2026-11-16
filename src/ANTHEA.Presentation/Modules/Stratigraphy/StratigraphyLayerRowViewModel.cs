using System.Text.Json.Nodes;
using Anthea.Application.Stratigraphy;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Anthea.Presentation.Modules.Stratigraphy;

/// <summary>One layer, edited by the table and by the selected-layer details using the same fields.</summary>
public sealed class StratigraphyLayerRowViewModel : ObservableObject
{
    private readonly Action changed;
    private readonly PresentationServices services;
    private bool readOnly;

    public StratigraphyLayerRowViewModel(JsonObject data, PresentationServices services, Action changed, bool readOnly)
    {
        Data = data; this.services = services; this.changed = changed; this.readOnly = readOnly;
        var fields = StratigraphyFields.Layer();
        Identity = Form(data, fields[..3]);
        Identity.ShowField("name", false); // The name is edited in the table.
        Identity.ShowField("behavior", false);
        Description = Form(data, fields[3..]);
        Means = Form(data, StratigraphyFields.Means());
        foreach (var parameter in StratigraphyFields.Parameters()) Means.ShowField(parameter.Key, false);
        Notes = Form(data, StratigraphyFields.Notes());
        RefreshOptions();
    }

    public JsonObject Data { get; }
    public FormViewModel Identity { get; }
    public FormViewModel Description { get; }
    public FormViewModel Means { get; }
    public FormViewModel Notes { get; }
    public FormViewModel? Thickness { get; private set; }
    public FormViewModel? Minima { get; private set; }
    public string Color => Description.Get("color");
    public bool IsInfinite => StratigraphyDocument.Infinite(Data);
    public IEnumerable<FormViewModel> Forms => new[] { Identity, Description, Thickness, Means, Minima, Notes }.OfType<FormViewModel>();

    public void SetReadOnly(bool value)
    {
        readOnly = value;
        foreach (var form in Forms) form.IsReadOnly = value;
    }

    public void RefreshOptions()
    {
        Thickness = IsInfinite ? null : Form(Data, StratigraphyFields.Thickness());
        Minima = Data["minimum"] is JsonObject minimum ? Form(minimum, StratigraphyFields.Minima()) : null;
        OnPropertyChanged(nameof(Thickness)); OnPropertyChanged(nameof(Minima)); OnPropertyChanged(nameof(IsInfinite));
    }

    private FormViewModel Form(JsonObject values, FieldSpec[] fields) => new(values, fields, key =>
    {
        if (key == "color") OnPropertyChanged(nameof(Color));
        changed();
    }, compact: true) { IsReadOnly = readOnly, WikiNavigator = services.Wiki };
}
