using System.Text.Json;
using System.Text.Json.Nodes;
using GPC.Checkers.Geotechnics.Piles;

namespace Anthea.Calculations;

/// <summary>Archive adapter only. All geometry and engineering formulas live in Checker.</summary>
public static class HorizontalPileGroup
{
    public const string Module = "geo_efficienza_orizzontale";
    public static readonly string[] MethodNames = ["Davisson (1970) · kh/nh", "AASHTO (2014) · p-multiplier", "FHWA (2018) · p-multiplier", "Rollins / FEMA · p-multiplier", "Reese / Van Impe · interazione palo-palo", "Caltrans (2025) · p-multiplier modificato"];
    public static readonly string[] LayoutNames = ["Rettangolare", "Quinconce", "Triangolare", "Pentagonale", "Esagonale", "Generica"];
    public static JsonObject GeneratorDefaults() => J.Obj(("tipo", "Rettangolare"), ("nx", 3), ("ny", 3), ("lato", 3), ("anelli", 1), ("suddivisioni", 1), ("centro", true), ("sx_d", 3d), ("sy_d", 3d), ("rotazione", 0d));
    public static JsonObject Defaults()
    {
        var data = J.Obj(("versione", 2), ("diametro", 1d), ("angolo", 0d), ("metodo", 4), ("direzione_selezionata", "X"),
            ("estrapolazioni", false), ("file_proiettate", false), ("generatore", GeneratorDefaults()),
            ("direzioni", J.Obj(("X", true), ("Y", true), ("X-", false), ("Y-", false), ("custom", false), ("angolo_custom", 45d))),
            ("interassi", J.Obj(("X", new JsonObject()), ("Y", new JsonObject()), ("X-", new JsonObject()), ("Y-", new JsonObject()), ("custom", new JsonObject()))),
            ("basamento", J.Obj(("tipo", "Contorno palificata"), ("bordo_d", 1d))), ("pali", new JsonArray()));
        Regenerate(data); return data;
    }
    public static void Upgrade(JsonObject data)
    {
        if (data.D("versione") != 1) return;
        var defaults = Defaults();
        foreach (string key in new[] { "generatore", "direzioni", "interassi", "basamento", "direzione_selezionata" }) data[key] = defaults[key]!.DeepClone();
        data["generatore"]!["tipo"] = "Generica"; // Never replace a legacy archive's manually edited geometry.
        double angle = data.D("angolo"); string direction = angle == 0 ? "X" : angle == 90 ? "Y" : "custom";
        data["direzione_selezionata"] = direction;
        if (direction == "custom") { data["direzioni"]!["custom"] = true; data["direzioni"]!["angolo_custom"] = angle; }
        foreach (string key in new[] { "interasse_parallelo", "interasse_trasversale" }) { if (data[key] is not null) data["interassi"]![direction]![key] = data[key]!.DeepClone(); data.Remove(key); }
        data["versione"] = 2;
    }
    public static void Regenerate(JsonObject data)
    {
        if (data["generatore"] is not JsonObject g || g.S("tipo") == "Generica") return;
        int kind = Array.IndexOf(LayoutNames, g.S("tipo")); if (kind < 0 || kind > 4) throw new ArgumentException("Disposizione della palificata non riconosciuta.");
        int Count(string key) { double n = Required(g[key], key); if (n < 1 || n > 500 || n != Math.Truncate(n)) throw new ArgumentException(key + ": richiesto un intero positivo."); return (int)n; }
        var options = new PileLayoutOptions { Kind = (PileLayoutKind)kind, Diameter = Required(data["diametro"], "D"), RotationDegrees = Required(g["rotazione"], "rotazione"), SpacingXDiameters = Required(g["sx_d"], "S/D") };
        if (kind <= 1) { options.Columns = Count("nx"); options.Rows = Count("ny"); options.SpacingYDiameters = Required(g["sy_d"], "Sy/D"); }
        if (kind == 2) options.SideCount = Count("lato");
        if (kind >= 3) { options.Rings = Count("anelli"); options.SideDivisions = Count("suddivisioni"); options.CenterPile = g.B("centro"); }
        data["pali"] = new JsonArray(PileGroupLayout.Generate(options).Select(p => (JsonNode)J.Obj(("id", p.Id), ("x", p.X), ("y", p.Y))).ToArray());
    }
    public sealed record Direction(string Id, string Name, double Angle);
    public static IReadOnlyList<Direction> Directions(JsonObject data)
    {
        if (data.D("versione") == 1) return [new("legacy", "H", Required(data["angolo"], "angolo"))];
        var d = data["direzioni"] as JsonObject ?? throw new ArgumentException("Direzioni mancanti.");
        var result = new List<Direction>();
        foreach (var (id, angle) in new[] { ("X", 0d), ("Y", 90d), ("X-", 180d), ("Y-", 270d), ("custom", 45d) })
        {
            if(d[id] is null && id is "X-" or "Y-") continue; // Older archives did not have the optional negative directions.
            if (d[id] is not JsonValue value || !value.TryGetValue<bool>(out bool enabled)) throw new ArgumentException("Direzione non valida: " + id);
            if (enabled) result.Add(new(id, id == "custom" ? "Personalizzata" : id, id == "custom" ? Required(d["angolo_custom"], "angolo personalizzato") : angle));
        }
        if (result.Count == 0) throw new ArgumentException("Attivare almeno una direzione di carico.");
        return result;
    }
    public static JsonArray Grid(int nx, int ny, double sx, double sy)
    {
        if (nx < 1 || ny < 1 || (long)nx * ny > 500 || !double.IsFinite(sx) || !double.IsFinite(sy) || sx <= 0 || sy <= 0) throw new ArgumentException("Griglia: 1–500 pali e interassi positivi.");
        var piles = new JsonArray();
        for (int y = 0; y < ny; y++) for (int x = 0; x < nx; x++) piles.Add(J.Obj(("id", "P" + (piles.Count + 1)), ("x", x * sx), ("y", y * sy)));
        return piles;
    }
    static double Required(JsonNode? node, string name) => J.Number(node) is double x && double.IsFinite(x) ? x : throw new ArgumentException("Valore numerico non valido: " + name);
    public static LateralGroupInput Read(JsonObject data)
    {
        if (data.D("versione") is not (1 or 2) || data["pali"] is not JsonArray rows) throw new ArgumentException("Scheda efficienza orizzontale non valida.");
        double method = Required(data["metodo"], "metodo");
        if (method < 0 || method > 5 || method != Math.Truncate(method)) throw new ArgumentException("Metodo non valido.");
        foreach (string key in new[] { "estrapolazioni", "file_proiettate" })
            if (data[key] is not JsonValue v || !v.TryGetValue<bool>(out _)) throw new ArgumentException("Opzione non valida: " + key);
        return new LateralGroupInput
        {
            Diameter = Required(data["diametro"], "diametro"), LoadAngleDegrees = Required(data["angolo"], "angolo"),
            AllowExtrapolation = data.B("estrapolazioni"), AcceptProjectedRows = data.B("file_proiettate"),
            RepresentativeParallelSpacing = data["interasse_parallelo"] is null ? null : Required(data["interasse_parallelo"], "S parallelo"),
            RepresentativeTransverseSpacing = data["interasse_trasversale"] is null ? null : Required(data["interasse_trasversale"], "S trasversale"),
            Piles = rows.Select(p => p is JsonObject ? new GroupPile { Id = p.S("id"), X = Required(p["x"], "x"), Y = Required(p["y"], "y") } : throw new ArgumentException("Riga palo non valida.")).ToArray()
        };
    }
    // Archive validation permits incomplete engineering inputs, but never malformed data.
    public static void ValidateShape(JsonObject data)
    {
        _ = Read(data); _ = Directions(data);
        if (data.D("versione") == 2 && (data["generatore"] is not JsonObject || data["basamento"] is not JsonObject || data["interassi"] is not JsonObject)) throw new ArgumentException("Impostazioni della palificata non valide.");
    }
    public static JsonObject Calculate(JsonObject data)
    {
        var snapshot = (JsonObject)data.DeepClone();
        Regenerate(snapshot);
        var comparison = new JsonArray();
        foreach (var direction in Directions(snapshot))
        {
            var input = Read(snapshot); input.LoadAngleDegrees = direction.Angle;
            if (snapshot.D("versione") == 2)
            {
                var spacings = snapshot["interassi"]?[direction.Id];
                input.RepresentativeParallelSpacing = spacings?["interasse_parallelo"] is null ? null : Required(spacings["interasse_parallelo"], "S parallelo " + direction.Name);
                input.RepresentativeTransverseSpacing = spacings?["interasse_trasversale"] is null ? null : Required(spacings["interasse_trasversale"], "S trasversale " + direction.Name);
            }
            foreach (var method in Enum.GetValues<LateralGroupMethod>())
            {
                var calculated = LateralPileGroup.Calculate(input, method);
                var node = JsonSerializer.SerializeToNode(calculated)!.AsObject();
                node["DirectionId"] = direction.Id; node["DirectionName"] = direction.Name; node["Angle"] = direction.Angle;
                node["Minimum"] = calculated.Available ? calculated.Piles.Min(p => p.Factor) : null;
                node["Maximum"] = calculated.Available ? calculated.Piles.Max(p => p.Factor) : null;
                node["ReductionPercent"] = calculated.Factor.HasValue ? (1 - calculated.Factor.Value) * 100 : null;
                comparison.Add(node);
            }
        }
        var result = J.Obj(("errore", ""), ("input", snapshot), ("confronto", comparison));
        try
        {
            var input = Read(snapshot); var cap = snapshot["basamento"];
            result["basamento"] = JsonSerializer.SerializeToNode(PileGroupLayout.CapOutline(input.Piles, input.Diameter, cap is null ? 1 : Required(cap["bordo_d"], "distanza asse–bordo / D"), cap.S("tipo") == "Rettangolare"));
            result["errore_basamento"] = "";
        }
        catch (ArgumentException ex) { result["errore_basamento"] = ex.Message; }
        Select(result, (int)data.D("metodo"), data.S("direzione_selezionata", data.D("versione") == 1 ? "legacy" : "X"));
        return result;
    }
    public static void Select(JsonObject result, int method, string direction)
    {
        var rows = result.Array("confronto");
        var selected = rows.FirstOrDefault(r => r.D("Method") == method && r.S("DirectionId") == direction) ?? rows.First(r => r.D("Method") == method);
        result["selezionato"] = selected!.DeepClone();
        result["input"]!["metodo"] = method; result["input"]!["direzione_selezionata"] = selected.S("DirectionId"); result["input"]!["angolo"] = selected.D("Angle");
    }
}
