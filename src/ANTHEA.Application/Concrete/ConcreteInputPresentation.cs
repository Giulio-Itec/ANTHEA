using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Application.Materials;

namespace Anthea.Application.Concrete;

/// <summary>
/// Derivations of the inputs and materials of the RC section sheet (str_palo), moved without changes from the historic view
/// (ConcreteWorkspace and its partials, preparation of F5.10): which fields each shape shows, the hole, the second layers and the manual
/// bars; the choices of the materials, the catalogue material of a class and the materials defined by the user; the tendons (equivalent
/// diameter and area, new cable, available materials); the strength passed to the rheology window. The historic view calls them; the
/// ViewModel of the sub-step «ingressi e materiali» will call them too. No engineering rule: the formulas stay in the libraries
/// (ReinforcementGeometry through ConcreteEditor).
/// </summary>
public static class ConcreteInputPresentation
{
    // ---------------------------------------------------------------- geometry

    /// <summary>The fields of the geometry that depend on the shape, in the order of the form.</summary>
    public static ImmutableArray<string> ShapeFields { get; } = ["diameter_mm", "circular_sides", "width_mm", "height_mm", "flange_width_mm", "web_width_mm", "flange_thickness_mm"];

    /// <summary>Whether a field of <see cref="ShapeFields"/> is shown for <paramref name="shape"/>.</summary>
    public static bool GeometryFieldShown(string key, string shape) => key switch
    {
        "diameter_mm" or "circular_sides" => shape == "Circolare",
        "width_mm" => shape == "Rettangolare",
        "height_mm" => shape != "Circolare",
        _ => shape == "A T"
    };

    /// <summary>Whether a field of the reinforcement (not the links) is shown for <paramref name="shape"/>.</summary>
    public static bool ReinforcementFieldShown(string key, string shape) =>
        key.StartsWith("flange_bottom", StringComparison.Ordinal) ? shape == "A T" : key.StartsWith("longitudinal", StringComparison.Ordinal) ? shape == "Circolare" : shape != "Circolare";

    /// <summary>The central hole exists only for the circular and the rectangular section.</summary>
    public static bool HoleAvailable(string shape) => shape is "Circolare" or "Rettangolare";

    /// <summary>Whether a field of the hole is shown: the diameter for the circle, width and height for the rectangle.</summary>
    public static bool HoleFieldShown(string key, string shape) => key == "inner_diameter_mm" ? shape == "Circolare" : shape == "Rettangolare";

    /// <summary>Removes the hole of a shape that has none (the sheet writes <c>foro_presente = false</c> at every refresh of such a shape).</summary>
    public static void NormalizeHole(JsonObject input)
    {
        if (!HoleAvailable(input.S("shape"))) input["foro_presente"] = false;
    }

    /// <summary>Whether the coordinates of the bars replace the wizard (the fields of the reinforcement are disabled).</summary>
    public static bool ManualBars(JsonObject input) => input["barre_manuali"] is JsonArray;

    /// <summary>The note of the fields of the reinforcement when the coordinates of the bars are active.</summary>
    public static string? ReinforcementToolTip(bool wizard) => wizard ? null : "Sono attive le coordinate delle barre. Ripristina le barre da wizard per usare questi parametri.";

    /// <summary>The second layers: key, title of the toggle and source of the default values.</summary>
    public static ImmutableArray<(string Key, string Title, string Source)> Layers { get; } =
        [("top", "Seconda fila superiore", "top"), ("bottom", "Seconda fila inferiore", "bottom"), ("inner", "Secondo anello interno", "longitudinal")];

    /// <summary>A second layer is shown for its shape: the inner ring for the circle, the rows for the rectangle.</summary>
    public static bool LayerShown(string key, string shape) => shape == (key == "inner" ? "Circolare" : "Rettangolare");

    /// <summary>The second layers are locked by the coordinates of the bars: the key, whatever its value (unlike <see cref="ManualBars"/>).</summary>
    public static bool LayersLocked(JsonObject input) => input.ContainsKey("barre_manuali");

    public static string LayerToggleText(bool enabled, string title) => (enabled ? "− Disattiva · " : "+ Attiva · ") + title;

    public static string LayerToolTip(bool locked) => locked ? "Ripristina le barre da wizard per modificare gli strati automatici." : "Distanza libera tra le superfici delle barre dei due strati.";

    // ---------------------------------------------------------------- sections made by the wizard (F5.10 S1b)

    /// <summary>
    /// The section is made by the wizard of the types (a shape of the wizard, or a hole with two contours, ConcreteTemplateAdapter.Uses):
    /// geometry, hole and number of bars are edited in the wizard, the single bars in the table of the coordinates.
    /// </summary>
    public static bool TemplateSection(JsonObject input) => Anthea.ConcreteWorkspace.ConcreteTemplateAdapter.Uses(input);

    /// <summary>The note shown instead of the form of the geometry for a section of the wizard.</summary>
    public const string TemplateGeometryNote = "Geometria e copriferro si modificano nel wizard.";

    /// <summary>The note shown instead of the form of the reinforcement for a section of the wizard.</summary>
    public const string TemplateReinforcementNote = "Disposizione definita nel wizard. Le singole barre si modificano nella tabella delle coordinate.";

    /// <summary>«Foro centrale» accepts the user: a shape with a hole that is not a section of the wizard.</summary>
    public static bool HoleToggleEnabled(JsonObject input) => HoleAvailable(input.S("shape")) && !TemplateSection(input);

    /// <summary>The note of the fields of the reinforcement: the wizard for its sections, otherwise <see cref="ReinforcementToolTip(bool)"/>.</summary>
    public static string? ReinforcementToolTip(JsonObject input, bool enabled) => TemplateSection(input)
        ? "Modificare la disposizione da Wizard tipologie oppure dalla tabella delle coordinate delle barre." : ReinforcementToolTip(enabled);

    /// <summary>A second layer is shown for its shape (<see cref="LayerShown"/>), never for a section of the wizard.</summary>
    public static bool LayerVisible(string key, JsonObject input) => !TemplateSection(input) && LayerShown(key, input.S("shape"));

    /// <summary>The fields of the circular scheme of the links (type, inner legs, inner scheme, rotation): an ordinary circular section only.</summary>
    public static bool CircularLinkScheme(JsonObject input) => input.S("shape") == "Circolare" && !TemplateSection(input);

    /// <summary>The choices of «Sezione» after the wizard: the shape of the wizard added when it is not among them; null when nothing changes.</summary>
    public static string[]? ShapeChoicesAfterWizard(IEnumerable<string> choices, string shape)
    {
        var current = choices.ToArray();
        return current.Contains(shape, StringComparer.Ordinal) ? null : [.. current, shape];
    }

    /// <summary>The note of the shear with manual parameters: the one written by the wizard (<c>taglio.nota_wizard</c>), otherwise the default.</summary>
    public static string ManualShearNote(JsonObject shear) => shear.S("nota_wizard", "Parametri manuali: verificare geometria, armatura tesa e ancoraggio.");

    // ---------------------------------------------------------------- materials

    /// <summary>The choices of a class when the form is built: the catalogue, the current value, «Personalizzato», without empty texts.</summary>
    public static string[] MaterialChoices(IEnumerable<JsonObject> catalog, string current) => catalog.Select(m => m.S("nome"))
        .Append(current).Append("Personalizzato").Where(s => s.Length > 0).Distinct().ToArray();

    /// <summary>The choices of a class after a change of the standard: the catalogue, the current value, «Personalizzato» (empty texts kept).</summary>
    public static string[] StandardChoices(IEnumerable<JsonObject> catalog, string current) => catalog.Select(m => m.S("nome")).Append(current).Append("Personalizzato").Distinct().ToArray();

    /// <summary>The material of the catalogue named <paramref name="current"/>, or the default class of the sheet (C35/45, B450C).</summary>
    public static JsonObject StandardMaterial(IEnumerable<JsonObject> catalog, string current, string key) =>
        catalog.FirstOrDefault(m => m.S("nome") == current) ?? catalog.First(m => m.S("nome") == (key == "classe_cls" ? "C35/45" : "B450C"));

    /// <summary>
    /// The values of a material of the catalogue that the sheet writes into the input, in order: every key but the name and the laws. The
    /// sheet writes each value and shows it before the next one (the note of the class of the steel is computed at that moment).
    /// </summary>
    public static IEnumerable<KeyValuePair<string, JsonNode?>> StandardMaterialValues(JsonObject material) =>
        material.Where(p => p.Key != "nome" && p.Key is not ("cls_diagramma" or "steel_diagramma"));

    /// <summary>The properties of the steel that make it «Personalizzato» when typed.</summary>
    public static ImmutableArray<string> SteelProperties { get; } = ["fyk_mpa", "steel_modulus_mpa", "steel_fu_mpa", "steel_eps_u"];

    /// <summary>A typed fck makes the concrete «Personalizzato».</summary>
    public static void CustomConcrete(JsonObject input) { input["classe_cls"] = "Personalizzato"; input["materiale_cls_nome"] = "CLS personalizzato"; }

    /// <summary>A typed property of <see cref="SteelProperties"/> makes the steel «Personalizzato».</summary>
    public static void CustomSteel(JsonObject input) { input["classe_acciaio"] = "Personalizzato"; input["materiale_acciaio_nome"] = "Acciaio personalizzato"; }

    /// <summary>The text of a saved material in the list of the sheet.</summary>
    public static string SavedMaterialLabel(JsonNode? material) => material.S("tipo") + " · " + material.S("nome");

    /// <summary>The keys of the steel copied from a saved material, in order.</summary>
    public static ImmutableArray<string> SavedSteelKeys { get; } = ["steel_modulus_mpa", "fyk_mpa", "steel_fu_mpa", "steel_eps_u", "steel_diagramma"];

    /// <summary>
    /// Applies a saved material: a concrete or a steel to the input (class «Personalizzato» and the name of the material), a tendon as the
    /// default material of the new cables. Returns its type.
    /// </summary>
    public static string ApplySavedMaterial(JsonObject input, JsonObject settings, JsonObject material)
    {
        string type = material.S("tipo");
        if (type == "Calcestruzzo")
        {
            input["classe_cls"] = "Personalizzato"; input["fck_mpa"] = material["fck_mpa"]?.DeepClone(); input["cls_diagramma"] = material["cls_diagramma"]?.DeepClone();
            input["materiale_cls_nome"] = material.S("nome");
        }
        else if (type == "Acciaio")
        {
            foreach (var key in SavedSteelKeys) input[key] = material[key]?.DeepClone();
            input["materiale_acciaio_nome"] = material.S("nome");
            input["classe_acciaio"] = "Personalizzato";
        }
        else settings["materiale_trefolo"] = material.DeepClone();
        return type;
    }

    /// <summary>The catalogue of the window «Nuovo materiale»: concrete of the standard, steel or tendons.</summary>
    public static JsonObject[] NewMaterialCatalog(string type, string standard) =>
        type == "Calcestruzzo" ? MaterialInputCatalog.Concrete(standard) : MaterialInputCatalog.Steel(type == "Trefoli", standard);

    /// <summary>
    /// The draft of a new material: the material of the catalogue of the current class (or the first), renamed «&lt;tipo&gt; personalizzato»,
    /// with its type, a new identifier, the origin and the standard.
    /// </summary>
    public static JsonObject NewMaterial(string type, JsonObject[] catalog, string current, string standard)
    {
        var preset = catalog.FirstOrDefault(m => m.S("nome") == current) ?? catalog[0];
        var value = (JsonObject)preset.DeepClone();
        value["nome"] = type + " personalizzato"; value["tipo"] = type; value["id"] = Guid.NewGuid().ToString("N");
        value["origine"] = "Personalizzato"; value["normativa_origine"] = standard;
        return value;
    }

    /// <summary>The current class shown by the window «Nuovo materiale» of <paramref name="type"/>.</summary>
    public static string NewMaterialCurrent(string type, JsonObject input, JsonObject settings) =>
        type == "Calcestruzzo" ? input.S("classe_cls") : type == "Acciaio" ? input.S("classe_acciaio") : settings["materiale_trefolo"].S("nome");

    /// <summary>Why the name of a new material is refused (empty, or equal to a material of the catalogue or to a saved one of the same type), or null.</summary>
    public static string? NewMaterialNameError(JsonObject value, IEnumerable<JsonObject> catalog, JsonObject settings, string type)
    {
        if (string.IsNullOrWhiteSpace(value.S("nome"))) return "Inserire il nome del materiale.";
        if (catalog.Any(m => m.S("nome") == value.S("nome")) || settings.Array("materiali_custom").Any(m => m.S("tipo") == type && m.S("nome") == value.S("nome")))
            return "Usare un nome distinto dai materiali esistenti.";
        return null;
    }

    /// <summary>The strength passed to the rheology window (fck as a magnitude).</summary>
    public static double RheologyStrength(JsonObject input) => Math.Abs(input.D("fck_mpa"));

    // ---------------------------------------------------------------- tendons

    /// <summary>A number written exactly (round-trip, invariant culture), as the sheet writes coordinates, areas and diameters.</summary>
    public static string Exact(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>
    /// The materials of the cables: the tendons of the catalogue and those saved in the sheet, with the law chosen for them in the sheet
    /// (<c>legami_trefoli</c>).
    /// </summary>
    public static IEnumerable<JsonObject> TendonMaterials(JsonObject settings) => MaterialInputCatalog.Steel(true, settings.S("normativa"))
        .Concat(settings.Array("materiali_custom").OfType<JsonObject>().Where(m => m.S("tipo") == "Trefoli"))
        .Select(m =>
        {
            var copy = (JsonObject)m.DeepClone();
            string? diagram = settings["legami_trefoli"]?[m.S("id")]?.ToString();
            if (diagram is "Elastoplastico" or "Incrudente") copy["diagramma"] = diagram;
            return copy;
        });

    /// <summary>The equivalent diameter of a cable read without it (from its area), or empty without an area.</summary>
    public static string TendonDiameter(JsonObject values)
    {
        double area = values.D("area");
        return area > 0 ? Exact(ConcreteEditor.ReinforcementDiameter(area)) : "";
    }

    /// <summary>
    /// The value of the other field when the area or the equivalent diameter of a cable is typed: the diameter of the area, or the area of
    /// the diameter; empty when the text is not a positive number.
    /// </summary>
    public static string TendonCounterpart(string field, string text)
    {
        if (double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double number) && double.IsFinite(number) && number > 0)
            return Exact(field == "area" ? ConcreteEditor.ReinforcementDiameter(number) : ConcreteEditor.ReinforcementArea(number));
        return "";
    }

    /// <summary>The properties of a cable copied from its material, in order, and the value of each (the law defaults to «Incrudente»).</summary>
    public static ImmutableArray<string> TendonMaterialKeys { get; } = ["Ep", "fpyk", "fpk", "eps_u", "diagramma"];

    public static string TendonMaterialValue(JsonObject material, string key) => material.S(key, key == "diagramma" ? "Incrudente" : "");

    /// <summary>The values of the form «+ Cavo / trefolo» when the sheet opens.</summary>
    public static JsonObject NewTendonForm() => J.Obj(("numero", "1"), ("diametro", Exact(ConcreteEditor.ReinforcementDiameter(150))), ("x", "0"), ("y", "0"), ("sigma0", "1000"));

    /// <summary>
    /// A new cable from the form «+ Cavo / trefolo» (number of strands, equivalent diameter of one strand, position, σp0) and its material:
    /// the first free identifier «Tnn», the position and σp0 written exactly, the total area of the strands. Refuses, with the message of the
    /// sheet, a missing material, a number of strands that is not an integer between 1 and 1000, and σp0 not lower than fpk.
    /// </summary>
    public static JsonObject NewTendon(JsonObject form, JsonObject? material, IEnumerable<string> ids)
    {
        if (material is null) throw new ArgumentException("Creare e selezionare un materiale trefolo nel gruppo Materiali.");
        double n = form.Required("numero", strict: true), d = form.Required("diametro", strict: true);
        if (n != Math.Truncate(n) || n > 1000) throw new ArgumentException("Numero trefoli: intero fra 1 e 1000.");
        double x = ConcreteEditor.Number(form.S("x"), "x"), y = ConcreteEditor.Number(form.S("y"), "y"), sigma = form.Required("sigma0");
        if (sigma >= material.Required("fpk", strict: true)) throw new ArgumentException("σp0 deve essere inferiore a fpk.");
        var used = ids.ToHashSet(StringComparer.Ordinal);
        int index = 1; while (used.Contains("T" + index.ToString("D2", CultureInfo.InvariantCulture))) index++;
        return J.Obj(("id", "T" + index.ToString("D2", CultureInfo.InvariantCulture)), ("x", Exact(x)), ("y", Exact(y)), ("area", Exact(ConcreteEditor.ReinforcementArea(d, (int)n))), ("sigma0", Exact(sigma)));
    }
}
