using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Application.Modules;
namespace Anthea.Application.Materials;

/// <summary>
/// What the reinforcing steel sheet shows for its input (<see cref="RebarPresentation.Read"/>): the type of the menu, which values the
/// user can change, the description of the steel and the source of its values.
/// </summary>
/// <param name="Selected">The type of the menu: the catalogue steel the input matches, otherwise <see cref="RebarPresentation.Custom"/>.</param>
/// <param name="Custom">The input matches no steel of the catalogue.</param>
/// <param name="Historical">The input matches a historic steel (D.M. 09/01/1996), whose εu and diagram stay free.</param>
/// <param name="Description">Surface, class and limits of the steel, or the difference from the catalogue of a custom sheet (DET-3).</param>
/// <param name="Source">Where the values come from.</param>
public sealed record RebarSheetState(string Selected, bool Custom, bool Historical, string Description, string Source)
{
    /// <summary>The mechanical property <paramref name="key"/> (one of <see cref="RebarPresentation.PropertyKeys"/>) is fixed by the catalogue.</summary>
    public bool Locked(string key) => !Custom && !(Historical && key == "steel_eps_u");

    /// <summary>The name is fixed by the catalogue.</summary>
    public bool NameLocked => !Custom;

    /// <summary>The diagram can be chosen: custom and historic steels.</summary>
    public bool DiagramEnabled => Custom || Historical;
}

/// <summary>Values of the steel shown by the sheet after its calculation (<see cref="RebarPresentation.Results"/>, <see cref="RebarPresentation.Failed"/>).</summary>
/// <param name="Text">fyd, εyd and fu / fyk, one per line.</param>
/// <param name="Status">The status line: coherent, historic values to check, or «⚠» and the message of the missing data.</param>
/// <param name="Coherent">The status is the coherent one (shown in green); otherwise it is a warning.</param>
/// <param name="Diagram">Points (ε ‰, σ MPa) of the characteristic curve; empty when the data are incomplete.</param>
public sealed record RebarResults(string Text, string Status, bool Coherent, IReadOnlyList<double[]> Diagram);

/// <summary>
/// Presentation of the reinforcing steel sheet (mat_acciaio_armatura): the rules that the historic view RebarMaterialView applied
/// to its controls, moved without changes in F5.5 so that the view and its ViewModel give the same texts and the same states. The
/// steel itself comes from <see cref="RebarEditor"/> and from the calculation service of the module.
/// </summary>
public static class RebarPresentation
{
    /// <summary>The type of a sheet that matches no steel of the catalogue.</summary>
    public const string Custom = "Personalizzato";

    /// <summary>The mechanical properties that the catalogue fixes, in the order of the sheet.</summary>
    public static ImmutableArray<string> PropertyKeys { get; } = ["fyk_mpa", "steel_fu_mpa", "steel_modulus_mpa", "steel_eps_u"];

    /// <summary>The types of the menu: the steels of the catalogue, then <see cref="Custom"/>.</summary>
    public static IReadOnlyList<string> Types() => RebarEditor.Catalog().Select(p => p.Name).Append(Custom).ToArray();

    /// <summary>
    /// After an edit of the user: a sheet that no longer matches a steel of the catalogue and is not custom yet becomes custom, with
    /// the generic name. True when the input was changed.
    /// </summary>
    public static bool MarkCustom(JsonObject input)
    {
        if (RebarEditor.Match(input) is not null || input.S("classe_acciaio") == Custom) return false;
        input["classe_acciaio"] = Custom; input["materiale_acciaio_nome"] = "Acciaio personalizzato";
        return true;
    }

    /// <summary>The type, the free values, the description and the source for <paramref name="input"/>.</summary>
    public static RebarSheetState Read(JsonObject input)
    {
        var preset = RebarEditor.Match(input); bool custom = preset is null, historical = preset?.A5 is not null;
        // DET-3: una scheda salvata come B450A con εu = 30 ‰ (catalogo precedente) non è più il preset; lo si dichiara senza riscrivere i valori.
        string description = custom ? RebarEditor.CatalogueDifference(input) ?? "Dati personalizzati. Specificare la provenienza dei valori adottati."
            : historical ? $"{preset!.Surface}\nAcciaio storico · A5 minimo {preset.A5:0}%\nA5 non coincide con εu. Definire εu per le verifiche."
            : preset!.Name == "B450A" ? "Aderenza migliorata · classe A\nL'impiego del B450A è soggetto alle limitazioni delle NTC 2018, §§ 7.4.2.2 e 11.3.2.1."
            : "Aderenza migliorata · classe C\nAcciaio B450C da catalogo.";
        string source = historical ? "D.M. 09/01/1996, prospetti 1-I e 2-I: resistenze minime nominali, non valori misurati. Es = 200 000 MPa assunto. Per dati da prove scegliere Personalizzato."
            : custom ? "Il riferimento rimane nella scheda. Le proprietà meccaniche sono condivisibili con i fogli della stessa sezione."
            : "Proprietà dal catalogo dei materiali già utilizzato dalla verifica in c.a. Per modificarle scegliere Personalizzato.";
        return new(preset?.Name ?? Custom, custom, historical, description, source);
    }

    /// <summary>The values of the calculation of the module (<see cref="RebarEditor.Read"/> and its «diagramma»).</summary>
    public static RebarResults Results(ModuleCalculation calculated, bool historical)
    {
        var v = RebarEditor.Read(calculated);
        string F(double n) => n.ToString("0.##", CultureInfo.GetCultureInfo("it-IT"));
        return new($"fyd     {F(v.Fyd)} MPa\nεyd     {F(v.EpsilonYd)} ‰\nfu / fyk     {F(v.Ratio)}",
            historical ? "Dati completi. Per strutture esistenti verificare i valori adottati e i coefficienti applicabili." : "✓ Proprietà coerenti",
            !historical,
            calculated.Values.Array("diagramma").Select(p => p!.AsArray().Select(v => v!.GetValue<double>()).ToArray()).ToList());
    }

    /// <summary>What the sheet shows when the calculation stops on incomplete or invalid data.</summary>
    public static RebarResults Failed(ArgumentException error) => new("fyd     —\nεyd     —\nfu / fyk     —", "⚠ " + error.Message, false, []);
}
