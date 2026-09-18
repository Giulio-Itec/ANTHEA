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
        if (settings.D("versione", 1) != 1) throw new ArgumentException("Versione dell’interfaccia CA non supportata.");
        void Default(string key, object value) { if (!settings.ContainsKey(key)) settings[key] = J.Node(value); }
        Default("versione", 1); Default("normativa", "NTC 2018"); Default("nota", ""); Default("tab", 0);
        Default("dominio3d", J.Obj(("stato", "SLU"), ("angoli", "36"), ("campioni", "81"), ("criterio", "Eccentricità costante"), ("filtro", "Tutte")));
        Default("dominio2d", J.Obj(("stato", "SLU"), ("tipo", "N–M"), ("theta", "0"), ("N", "1000"), ("filtro", "Tutte")));
        Default("trefoli", new JsonArray());
        Default("sle", new JsonObject());
        foreach (string set in Sets.Skip(2))
        {
            if (settings["sle"]![set] is not JsonObject) settings["sle"]![set] = J.Obj(("modello", "Lineare · sezione fessurata"), ("sigma_c_lim", ""), ("sigma_s_lim", ""), ("wk_lim", ""));
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
public sealed record DomainCheck(double? Utilization, ActionPoint? Resistance, string Status);
public sealed record DomainSegment(ActionPoint A, ActionPoint B);

/// <summary>Sampled surface of the existing ANTHEA engine, in kN / kNm, compression positive.
/// This is not the future Checker adapter. No normative crack-width verification is performed here.</summary>
public sealed class SectionDomainMesh
{
    public List<ActionPoint> Vertices { get; } = [];
    public List<int> Triangles { get; } = [];
    public string Mode { get; }
    public ActionPoint Scale { get; private set; }
    private SectionDomainMesh(string mode) { Mode = mode; }
    public static SectionDomainMesh Build(JsonObject input, string mode, int angles = 36, int samples = 81, CancellationToken cancel = default)
    {
        if (mode is not ("Plastico" or "Elastico")) throw new ArgumentException("Tipo di dominio non valido.");
        if (angles < 12 || angles > 144 || samples < 21 || samples > 321) throw new ArgumentException("Discretizzazione fuori intervallo.");
        var engine = new SezioneCA(input); var mesh = new SectionDomainMesh(mode); int count = 0;
        for (int a = 0; a < angles; a++)
        {
            cancel.ThrowIfCancellationRequested();
            var curve = Domini.Profili(engine, mode, engine.NAutomatico, 2 * Math.PI * a / angles, samples); count = curve.Count;
            mesh.Vertices.AddRange(curve.Select(p => new ActionPoint(p[0], p[1], p[2])));
        }
        mesh.Scale = new(Math.Max(1, mesh.Vertices.Max(p => Math.Abs(p.N))), Math.Max(1, mesh.Vertices.Max(p => Math.Abs(p.Mx))), Math.Max(1, mesh.Vertices.Max(p => Math.Abs(p.My))));
        void Triangle(int a, int b, int c)
        {
            if (mesh.Normalize(mesh.Vertices[b] - mesh.Vertices[a]).Cross(mesh.Normalize(mesh.Vertices[c] - mesh.Vertices[a])).Length < 1e-14) return;
            mesh.Triangles.AddRange([a, b, c]);
        }
        for (int a = 0; a < angles; a++) for (int i = 0; i < count - 1; i++)
        {
            int p = a * count + i, q = (a + 1) % angles * count + i;
            Triangle(p, q, p + 1); Triangle(p + 1, q, q + 1);
        }
        // Close the asymptotic ends (elastic profiles do not contain a single pure axial vertex).
        foreach (int end in new[] { 0, count - 1 })
        {
            ActionPoint center = default; for (int a = 0; a < angles; a++) center += mesh.Vertices[a * count + end] * (1d / angles);
            int index = mesh.Vertices.Count; mesh.Vertices.Add(center);
            for (int a = 0; a < angles; a++) Triangle(index, a * count + end, (a + 1) % angles * count + end);
        }
        return mesh;
    }
    public ActionPoint Normalize(ActionPoint p) => new(p.N / Scale.N, p.Mx / Scale.Mx, p.My / Scale.My);
    public DomainCheck Check(ActionPoint force, bool constantN = false)
    {
        var origin = constantN ? new ActionPoint(force.N, 0, 0) : default;
        var direction = force - origin;
        if (constantN && Check(origin, false).Utilization is not <= 1)
            return new(null, null, "N fuori campo per il percorso a N costante");
        if (direction.Length < 1e-12)
        {
            if (force.Length < 1e-12) return new(0, null, "Azione nulla");
            return Check(force, false);
        }
        var o = Normalize(origin); var d = Normalize(direction); double? factor = null;
        for (int i = 0; i < Triangles.Count; i += 3)
        {
            var a = Normalize(Vertices[Triangles[i]]); var b = Normalize(Vertices[Triangles[i + 1]]); var c = Normalize(Vertices[Triangles[i + 2]]);
            var e1 = b - a; var e2 = c - a; var h = d.Cross(e2); double det = e1.Dot(h);
            if (Math.Abs(det) < 1e-13) continue;
            var s = o - a; double u = s.Dot(h) / det; if (u < -1e-8 || u > 1 + 1e-8) continue;
            var q = s.Cross(e1); double v = d.Dot(q) / det; if (v < -1e-8 || u + v > 1 + 1e-8) continue;
            double t = e2.Dot(q) / det;
            if (t > 1e-10 && (factor is null || t < factor)) factor = t;
        }
        if (factor is not double f) return new(null, null, "Fuori campo / intersezione assente");
        double eta = 1 / f;
        return new(eta, origin + direction * f, eta <= 1 + 1e-8 ? "Entro il dominio" : "Fuori dominio");
    }
    public List<DomainSegment> Cut(bool nm, double value)
    {
        // N-M: plane with M_perpendicular = 0, not a projection of a neutral-axis profile.
        var normal = nm ? new ActionPoint(0, -Math.Sin(value), Math.Cos(value)) : new ActionPoint(1, 0, 0);
        double offset = nm ? 0 : value; double tolerance = 1e-8 * (nm ? Math.Max(Scale.Mx, Scale.My) : Scale.N);
        var result = new List<DomainSegment>();
        for (int i = 0; i < Triangles.Count; i += 3)
        {
            var triangle = new[] { Vertices[Triangles[i]], Vertices[Triangles[i + 1]], Vertices[Triangles[i + 2]] }; var hits = new List<ActionPoint>();
            void Add(ActionPoint p) { if (!hits.Any(q => Normalize(q - p).Length < 1e-8)) hits.Add(p); }
            for (int j = 0; j < 3; j++)
            {
                var a = triangle[j]; var b = triangle[(j + 1) % 3]; double da = a.Dot(normal) - offset, db = b.Dot(normal) - offset;
                if (Math.Abs(da) <= tolerance) Add(a);
                if (da * db < 0) Add(a + (b - a) * (da / (da - db)));
            }
            if (hits.Count == 2 && Normalize(hits[1] - hits[0]).Length > 1e-10) result.Add(new(hits[0], hits[1]));
        }
        return result;
    }
}
