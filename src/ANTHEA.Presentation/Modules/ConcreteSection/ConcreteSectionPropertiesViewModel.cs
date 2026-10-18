using System.Text;
using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Calculations;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Mvvm;
using Anthea.Presentation.Services;
using CommunityToolkit.Mvvm.Input;
using X.Core;

namespace Anthea.Presentation.Modules.ConcreteSection;

/// <summary>
/// The non modal window «Proprietà / report della sezione» (ConcreteSectionProperties.cs of the historic view, F5.10 S1): the elastic
/// properties of the uncracked section for the homogenization chosen (from φ or from n), on a copy of the data taken when the window opens
/// (<see cref="ConcretePropertySession"/>). Every result writes φ and n into <c>proprieta_sezione</c> of the sheet, with Modified, as the
/// historic window; the text can be copied or exported. The view closes the window by disposing this ViewModel.
/// </summary>
public sealed class ConcreteSectionPropertiesViewModel : ViewModelBase
{
    private readonly ConcretePropertySession session;
    private readonly JsonObject options;
    private readonly Action modified;
    private readonly PresentationServices services;
    private string text = "Calcolo proprietà della sezione…";
    private bool ready;

    internal ConcreteSectionPropertiesViewModel(JsonObject input, JsonObject settings, Action modified, PresentationServices services)
    {
        this.modified = modified; this.services = services;
        var inputCopy = (JsonObject)input.DeepClone(); var workspace = (JsonObject)settings.DeepClone();
        if (settings["proprieta_sezione"] is not JsonObject) settings["proprieta_sezione"] = J.Obj(("metodo", "Da φ"), ("phi", "0"));
        options = settings["proprieta_sezione"]!.AsObject();
        session = new ConcretePropertySession(inputCopy, workspace);
        Form = new FormViewModel(options, [new("metodo", "Omogeneizzazione", Choices: ["Da φ", "Da n"]), new("phi", "Coefficiente di viscosità φ"), new("n", "n armature = Es (1 + φ) / Ecm")],
            key => { _ = RefreshAsync(); }, compact: true) { CommitOnFocusLoss = true, WikiNavigator = services.Wiki };
        ExportCommand = new RelayCommand(Export, () => ready);
        CopyCommand = new RelayCommand(() => { if (ready) services.Clipboard.SetText(text); }, () => ready);
    }

    public string Title => "Proprietà / report della sezione";
    /// <summary>Homogenization: method, φ, n (the field of the method not chosen is disabled).</summary>
    public FormViewModel Form { get; }
    /// <summary>The report of the properties, or the message of the calculation in progress or of its error.</summary>
    public string Text { get => text; private set => SetProperty(ref text, value); }
    /// <summary>True when <see cref="Text"/> is a report (export and copy enabled).</summary>
    public bool IsReady { get => ready; private set { if (SetProperty(ref ready, value)) { ExportCommand.NotifyCanExecuteChanged(); CopyCommand.NotifyCanExecuteChanged(); } } }
    /// <summary>«Esporta report TXT…».</summary>
    public IRelayCommand ExportCommand { get; }
    /// <summary>«Copia».</summary>
    public IRelayCommand CopyCommand { get; }

    /// <summary>Calculates the properties for the current options (the window calls it when it opens).</summary>
    public async Task RefreshAsync()
    {
        if (IsDisposed) return;
        IsReady = false;
        Form.Enable("phi", options.S("metodo") == "Da φ"); Form.Enable("n", options.S("metodo") == "Da n");
        var completion = await session.CalculateAsync(options);
        completion.TryPublish(current =>
        {
            if (IsDisposed) return;
            if (current.Error is not null) { Text = "Proprietà non disponibili: " + current.Error.Message; return; }
            var properties = current.Value!;
            options["phi"] = ConcreteInputPresentation.Exact(properties.Phi); options["n"] = ConcreteInputPresentation.Exact(properties.N);
            Form.Set("phi", EngineeringFormat.Number(properties.Phi), true); Form.Set("n", EngineeringFormat.Number(properties.N), true);
            modified();
            Text = properties.Text; IsReady = true;
        });
    }

    private void Export()
    {
        if (!ready) return;
        string? path = services.Files.SaveFile(new SaveFileRequest([new FileFilter("Report testo", "*.txt")], "ANTHEA_Proprieta_sezione.txt"));
        if (path is null) return;
        try { Archivio.ScriviAtomico(path, Encoding.UTF8.GetBytes(text)); }
        catch (Exception failure) { services.Messages.Inform("Esportazione proprietà", failure.Message); }
    }

    protected override void OnDisposed() => session.Dispose();
}
