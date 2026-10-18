using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Application.Materials;
using Anthea.Calculations;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Services;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.ConcreteSection;

/// <summary>A curve of the σ–ε diagram of the window «Nuovo materiale»: name, points (ε [‰], σ [MPa]) and dashed for the design curve.</summary>
public sealed record ConcreteMaterialCurve(string Name, IReadOnlyList<double[]> Points, bool Dashed);

/// <summary>
/// The window «Nuovo materiale · Calcestruzzo | Acciaio | Trefoli» of the RC section (ConcreteMaterialEditor.cs of the historic view, F5.10
/// S1) on the dialog service: the draft of the material (the catalogue material of the current class renamed «… personalizzato»), its form,
/// the characteristic and design curves of the library with the coefficients of the sheet, the derived values and the error. «Salva materiale
/// e applica» closes with the material when the values are valid and the name is new; the sheet adds it to its saved materials and applies
/// it. Nothing changes in the sheet until then.
/// </summary>
public sealed class ConcreteMaterialDialogViewModel : DialogViewModel<JsonObject>
{
    private readonly JsonObject input, settings;
    private readonly JsonObject[] catalog;
    private IReadOnlyList<ConcreteMaterialCurve> curves = [];
    private string derived = "", error = "";
    private bool valid;

    internal ConcreteMaterialDialogViewModel(string type, JsonObject input, JsonObject settings, IWikiNavigator wiki) : base("Nuovo materiale · " + type)
    {
        Type = type; this.input = input; this.settings = settings;
        string standard = settings.S("normativa");
        catalog = ConcreteInputPresentation.NewMaterialCatalog(type, standard);
        Value = ConcreteInputPresentation.NewMaterial(type, catalog, ConcreteInputPresentation.NewMaterialCurrent(type, input, settings), standard);
        FieldSpec[] parameters = type == "Calcestruzzo" ? [new("fck_mpa", "fck", "MPa"), new("cls_diagramma", "Diagramma", Choices: MaterialInputCatalog.ConcreteDiagrams, Note: FieldNotes.StressBlock)]
            : type == "Acciaio" ? [new("steel_modulus_mpa", "Es", "MPa"), new("fyk_mpa", "fyk", "MPa"), new("steel_fu_mpa", "fu", "MPa"), new("steel_eps_u", "εu", "‰"), new("steel_diagramma", "Diagramma", Choices: ["Elastoplastico", "Incrudente"])]
            : [new("Ep", "Ep", "MPa"), new("fpyk", "fpyk", "MPa"), new("fpk", "fpk", "MPa"), new("eps_u", "εpu", "‰"), new("diagramma", "Diagramma", Choices: ["Elastoplastico", "Incrudente"])];
        Notice = standard + " · inserire le proprietà caratteristiche del nuovo materiale. Le leggi della DLL ricavano il diagramma di progetto con i coefficienti del foglio. Nessuna modifica finché non si salva.";
        PlotNote = type == "Calcestruzzo" ? "Compressione (−ε, −σ) nel primo quadrante · sola rappresentazione" : "Compressione negativa · curve native della DLL";
        Form = new FormViewModel(Value, new FieldSpec[] { new("nome", "Nome", Wide: true) }.Concat(parameters), _ => Refresh(), wideChoices: true) { WikiNavigator = wiki };
        Refresh();
        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(Cancel);
    }

    /// <summary>Calcestruzzo, Acciaio or Trefoli.</summary>
    public string Type { get; }
    /// <summary>The draft of the material, written by <see cref="Form"/>.</summary>
    public JsonObject Value { get; }
    public FormViewModel Form { get; }
    /// <summary>The notice above the form.</summary>
    public string Notice { get; }
    /// <summary>Note of the diagram; the concrete is drawn with negated axes (compression in the first quadrant).</summary>
    public string PlotNote { get; }
    public bool NegateAxisLabels => Type == "Calcestruzzo";
    /// <summary>The characteristic and design curves; empty while the values are not valid.</summary>
    public IReadOnlyList<ConcreteMaterialCurve> Curves { get => curves; private set => SetProperty(ref curves, value); }
    /// <summary>The values derived by the library (Ecm, strains of the diagram…); empty while the values are not valid.</summary>
    public string Derived { get => derived; private set => SetProperty(ref derived, value); }
    /// <summary>Why the values or the name are refused; empty otherwise.</summary>
    public string Error { get => error; private set => SetProperty(ref error, value); }
    /// <summary>«Salva materiale e applica».</summary>
    public IRelayCommand SaveCommand { get; }
    public IRelayCommand CancelCommand { get; }

    // The diagram of the draft through the library, at every change of the form.
    private void Refresh()
    {
        try
        {
            var prepared = MaterialEditorPreview.Read(Type, Value, input, settings);
            List<double[]> Display(double[][] points) => points.Select(p => Type == "Calcestruzzo" ? [-p[0], -p[1]] : p).ToList();
            Curves = [new("Caratteristica", Display(prepared.Curves.Characteristic), false), new("Progetto · " + settings.S("normativa"), Display(prepared.Curves.Design), true)];
            Derived = prepared.Description;
            Error = ""; valid = true;
        }
        catch (ArgumentException failure) { Curves = []; Error = failure.Message; valid = false; Derived = ""; }
    }

    private void Save()
    {
        Form.Commit(); Refresh();
        if (!valid) return;
        if (ConcreteInputPresentation.NewMaterialNameError(Value, catalog, settings, Type) is string refused) { Error = refused; return; }
        Close((JsonObject)Value.DeepClone());
    }
}
