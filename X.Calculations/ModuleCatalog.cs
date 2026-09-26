using System.Text.Json.Nodes;

namespace Anthea.Calculations;

/// <summary>One definition for archive validation, new sheets and both application catalogs.</summary>
public sealed record ModuleDefinition(string Id, string Area, string Name, string Description,
    Func<JsonObject> NewData, Action<JsonObject> Validate)
{
    public string Element => Name;
    public string ProjectTitle => Name;
    public string ProjectSubtitle => Description;
}

public static class ModuleCatalog
{
    public static IReadOnlyList<ModuleDefinition> All { get; } = Array.AsReadOnly(new ModuleDefinition[] {
        new("geo_palo_verticale", "Geotecnica", "Palo verticale", "Capacità portante", () => CalculationDefaults.Vertical(false), Calcolo.ValidaForma),
        new(PaloOrizzontale.Module, "Geotecnica", "Palo orizzontale", "Capacità portante", PaloOrizzontale.Defaults, d => ValidateHorizontal(d, false)),
        new("geo_micropalo_verticale", "Geotecnica", "Micropalo verticale", "Capacità portante · Bustamante–Doix", () => CalculationDefaults.Vertical(true), Calcolo.ValidaForma),
        new(MicropaloOrizzontale.Module, "Geotecnica", "Micropalo orizzontale", "Capacità portante", MicropaloOrizzontale.Defaults, d => ValidateHorizontal(d, true)),
        new("str_palo", "Strutture", "Sezione in c.a.", "Verifiche SLU · SLV · SLE", SezioneCA.DefaultData, ValidateConcrete),
        new(BridgeSection.Module, "Strutture", "Sezione composta da ponte", "Analisi per fasi e classe 4", BridgeSection.Defaults, BridgeSection.ValidateShape),
        new("mat_calcestruzzo", "Materiali", "Calcestruzzo", "Proprietà, copriferro e composizione", () => J.Obj(("versione_materiali", 1)), ValidateMaterial),
        new(RebarMaterial.Module, "Materiali", "Acciaio per armature", "Proprietà meccaniche e diagramma", RebarMaterial.Defaults, RebarMaterial.ValidateShape)
    });
    public static ModuleDefinition Get(string id) => All.FirstOrDefault(m => m.Id == id)
        ?? throw new ArgumentException("Modulo del foglio non disponibile: " + id, nameof(id));
    public static JsonObject CreateData(string id) => Get(id).NewData();
    public static void ValidateData(string id, JsonObject data) => Get(id).Validate(data);

    private static void ValidateHorizontal(JsonObject data, bool chs)
    {
        PaloOrizzontale.ValidateShape(data);
        if (chs != (data.S("tipo_sezione") == "CHS"))
            throw new ArgumentException("Tipo di sezione incoerente con il modulo orizzontale.");
    }
    private static void ValidateConcrete(JsonObject data)
    {
        if (data["input"] is not JsonObject || data.D("versione_sezione", 1) is not (1 or 2))
            throw new ArgumentException("Dati della sezione non validi.");
        ValidateConcreteWorkspace(data);
    }
    internal static void ValidateConcreteWorkspace(JsonObject data)
    {
        // Validate containers before migration: malformed archives must not silently lose actions.
        if (data["combinazioni"] is not null && data["combinazioni"] is not JsonObject)
            throw new ArgumentException("Formato delle combinazioni della sezione non valido.");
        if (data["workspace_ca"] is not null && data["workspace_ca"] is not JsonObject)
            throw new ArgumentException("Dati dell’interfaccia CA non validi.");
        if (data["workspace_ca"] is JsonObject workspace)
        {
            if (workspace.D("versione", 1) is not (1 or 2)) throw new ArgumentException("Versione dell’interfaccia CA non supportata.");
            foreach (string key in new[] { "dominio3d", "dominio2d", "coefficienti", "sle", "sle_comuni", "taglio", "momento_curvatura", "dettagli_costruttivi", "ancoraggi" })
                if (workspace[key] is not null && workspace[key] is not JsonObject)
                    throw new ArgumentException("Impostazioni CA non valide: " + key);
            foreach (string set in SectionWorkspace.Sets.Skip(2))
                if (workspace["sle"]?[set] is not null && workspace["sle"]?[set] is not JsonObject)
                    throw new ArgumentException("Impostazioni SLE non valide: " + set);
            foreach (var (label, node) in new[] { ("trefoli", workspace["trefoli"]), ("azioni di taglio", workspace["taglio"]?["azioni"]) })
                if (node is not null && (node is not JsonArray array || array.Any(r => r is not JsonObject)))
                    throw new ArgumentException("Elenco CA non valido: " + label);
        }
        foreach (string set in SectionWorkspace.Sets)
        {
            var value = data["combinazioni"]?[set];
            if (value is null) continue;
            if (value is not JsonArray rows) throw new ArgumentException("Combinazioni " + set + ": elenco non valido.");
            foreach (var row in rows)
                if (row is not JsonObject || row["azioni"] is not JsonArray { Count: 3 } actions ||
                    actions.Any(a => a is not JsonValue))
                    throw new ArgumentException("Combinazioni " + set + ": attese tre azioni N, Mx, My.");
        }
    }
    private static void ValidateMaterial(JsonObject data)
    {
        if (data.D("versione_materiali") != 1) throw new ArgumentException("Versione della scheda materiali non supportata.");
        foreach (string key in new[] { "numeri", "scelte", "opzioni" })
            if (data[key] is not null && data[key] is not JsonObject) throw new ArgumentException("Dati materiali non validi: " + key);
        if (data["esposizioni"] is not null && data["esposizioni"] is not JsonArray) throw new ArgumentException("Esposizioni non valide.");
    }
}
