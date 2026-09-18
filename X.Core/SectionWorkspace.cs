using System.Text.Json.Nodes;

namespace X.Core;

/// <summary>Persisted workspace settings. Legacy SLE combinations become Rara without duplicating loads.</summary>
public static class SectionWorkspace
{
    public static readonly string[] Sets = ["SLU", "SLV", "SLE", "SLE_FREQ", "SLE_QP"];
    public static string Label(string key) => key switch { "SLU" => "SLU plastico", "SLV" => "SLV elastico", "SLE" => "Rara", "SLE_FREQ" => "Frequente", "SLE_QP" => "Quasi permanente", _ => key };
    public static JsonObject Prepare(JsonObject data)
    {
        if (data["input"] is not JsonObject) data["input"] = SezioneCA.DefaultInput();
        foreach (var (key, value) in SezioneCA.DefaultInput()) if (!data["input"]!.AsObject().ContainsKey(key)) data["input"]![key] = value?.DeepClone();
        if (data["input"]!["gettato_sottile"] is null) data["input"]!["gettato_sottile"] = "No";
        data["versione_sezione"] = 2;
        if (data["combinazioni"] is null)
            data["combinazioni"] = J.Obj(("SLU", new JsonArray(J.Obj(("nome", "Combo 1"), ("azioni", new[] { data["input"].S("axial_force_kn", "0"), data["input"].S("moment_x_knm", "0"), data["input"].S("moment_y_knm", "0") })))));
        else if (data["combinazioni"] is not JsonObject) throw new ArgumentException("Formato delle combinazioni della sezione non valido.");
        var ids = new HashSet<string>();
        foreach (string set in Sets)
        {
            if (data["combinazioni"]![set] is not JsonArray) data["combinazioni"]![set] = new JsonArray();
            foreach (var row in data["combinazioni"]![set]!.AsArray().OfType<JsonObject>())
                if (row.S("id") == "" || !ids.Add(row.S("id"))) { row["id"] = Guid.NewGuid().ToString("N"); ids.Add(row.S("id")); }
        }
        if (data["workspace_ca"] is not JsonObject) data["workspace_ca"] = new JsonObject();
        var settings = data["workspace_ca"]!.AsObject();
        if (settings.D("versione", 1) is not (1 or 2)) throw new ArgumentException("Versione dell’interfaccia CA non supportata.");
        void Default(string key, object value) { if (!settings.ContainsKey(key)) settings[key] = J.Node(value); }
        Default("versione", 1); Default("normativa", "NTC 2018"); Default("nota", ""); Default("tab", 0);
        Default("dominio3d", J.Obj(("stato", "SLU"), ("angoli", "32"), ("criterio", "N costante"), ("filtro", "Tutte")));
        Default("dominio2d", J.Obj(("stato", "SLU"), ("tipo", "Mx–My"), ("theta", "0"), ("N", "1000"), ("filtro", "Tutte")));
        Default("trefoli", new JsonArray());
        Default("sle", new JsonObject());
        foreach (string set in Sets.Skip(2))
        {
            if (settings["sle"]![set] is not JsonObject) settings["sle"]![set] = J.Obj(("modello", "Lineare · sezione fessurata"), ("sigma_c_lim", ""), ("sigma_s_lim", ""), ("wk_lim", ""));
        }
        // v1 used compression-positive N; Mx/My retain their physical sign.
        if (settings.D("versione", 1) == 1)
        {
            static void Flip(JsonNode node, string key) { if (J.Number(node[key]) is double n) node[key] = (-n).ToString("G17", System.Globalization.CultureInfo.InvariantCulture); }
            Flip(data["input"]!, "axial_force_kn");
            foreach (var set in Sets) foreach (var row in data["combinazioni"]![set]!.AsArray())
                if (row?["azioni"] is JsonArray a && J.Number(a[0]) is double n) a[0] = (-n).ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
            Flip(settings["dominio2d"]!, "N");
            settings["versione"] = 2;
            settings["convenzione"] = "N negativo a compressione";
            settings["migrazione_segni"] = "N convertito dalla precedente convenzione ANTHEA; momenti invariati";
        }
        foreach (var name in new[] { "dominio3d", "dominio2d" })
        {
            var o = settings[name]!.AsObject();
            void Opt(string key, object value) { if (!o.ContainsKey(key)) o[key] = J.Node(value); }
            Opt("angoli", name == "dominio3d" ? "32" : "64"); Opt("strategia", "Iterativo"); Opt("criterio", "N costante");
            Opt("trazione_cls", "No"); Opt("assi", "Locali"); Opt("origine_x", "0"); Opt("origine_y", "0"); Opt("rotazione", "0");
            Opt("proietta", "No"); Opt("interpolazione", "Quadratica"); Opt("suddivisioni_n", "50");
        }
        foreach (string set in Sets.Skip(2))
        {
            var o = settings["sle"]![set]!.AsObject();
            o["modello"] = o.S("modello").StartsWith("Non lineare") ? "Non lineare" : "Lineare";
            foreach (var (k, v) in new[] { ("phi", "0"), ("phi_trefoli", "0"), ("trazione_cls", "No"), ("assi", "Locali"), ("origine_x", "0"), ("origine_y", "0"), ("rotazione", "0"), ("esposizione", "Da scegliere"), ("sensibilita", "Poco sensibile"), ("durata", "Lunga"), ("aderenza", "Migliorata"), ("copriferro_fessure", ""), ("spaziatura_fessure", "") })
                if (!o.ContainsKey(k)) o[k] = v;
        }
        return settings;
    }
    public static double Number(string text, string label)
        => J.Number(JsonValue.Create(text)) ?? throw new ArgumentException(label + ": inserire un numero finito.");
    public static int Subdivisions(string text, string label, int min, int max)
    {
        double value = Number(text, label);
        if (value != Math.Truncate(value) || value < min || value > max) throw new ArgumentException($"{label}: intero tra {min} e {max}.");
        return (int)value;
    }
}

public readonly record struct ActionPoint(double N, double Mx, double My)
{
    public static ActionPoint operator +(ActionPoint a, ActionPoint b) => new(a.N + b.N, a.Mx + b.Mx, a.My + b.My);
    public static ActionPoint operator -(ActionPoint a, ActionPoint b) => new(a.N - b.N, a.Mx - b.Mx, a.My - b.My);
    public static ActionPoint operator *(ActionPoint a, double t) => new(a.N * t, a.Mx * t, a.My * t);
    public double Dot(ActionPoint b) => N * b.N + Mx * b.Mx + My * b.My;
    public ActionPoint Cross(ActionPoint b) => new(Mx * b.My - My * b.Mx, My * b.N - N * b.My, N * b.Mx - Mx * b.N);
    public double Length => Math.Sqrt(Dot(this));
}
public sealed record DomainCheck(double? Utilization, ActionPoint? Resistance, string Status, SectionResponse? Response = null);
public sealed record DomainSegment(ActionPoint A, ActionPoint B);

/// <summary>Display-only geometry from Checker; no ANTHEA resistance algorithm.</summary>
public sealed class SectionDomainMesh
{
    public List<ActionPoint> Vertices { get; } = [];
    public List<int> Triangles { get; } = [];
    public string Mode { get; }
    public ActionPoint Scale { get; }
    internal SectionDomainMesh(string mode, GPC.Geometry.Meshes.Mesh mesh)
    {
        Mode = mode;
        var vertices = mesh.GetVertices();
        var indices = vertices.Select((v, i) => (v.Id, i)).ToDictionary(v => v.Id, v => v.i);
        Vertices.AddRange(vertices.Select(v => new ActionPoint(v.Point.Z / 1000, v.Point.X / 1e6, v.Point.Y / 1e6)));
        foreach (var f in mesh.GetFaces())
        {
            Triangles.AddRange([indices[f.A], indices[f.B], indices[f.C]]);
            if (f.IsQuad) Triangles.AddRange([indices[f.A], indices[f.C], indices[f.D]]);
        }
        if (Vertices.Count == 0 || Vertices.Any(p => !double.IsFinite(p.N + p.Mx + p.My))) throw new ArgumentException("Checker: mesh assente o non valida.");
        Scale = new(Math.Max(1, Vertices.Max(p => Math.Abs(p.N))), Math.Max(1, Vertices.Max(p => Math.Abs(p.Mx))), Math.Max(1, Vertices.Max(p => Math.Abs(p.My))));
    }
    public ActionPoint Normalize(ActionPoint p) => new(p.N / Scale.N, p.Mx / Scale.Mx, p.My / Scale.My);
}
