using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Anthea.Calculations;

// Fingerprints of the concrete meshes (DelaunayMesh through GPC.Model) of usual sections, built along the path of the RC solver:
// CheckerSection.PrepareModel -> ReinforcedConcreteSection.Mesh (automatic element size), the mesh that SectionSolver integrates and
// Ntc2018Checks.Cracking cuts. Node coordinates (format 'R') and connectivity are hashed with SHA-256: equal hashes mean bit-identical
// meshes (same nodes, same order, same faces). The canonical hash ignores the numbering and the order of nodes and faces.
internal static class MeshCapture
{
    sealed record Case(string Group, string Name, Func<(JsonObject Input, JsonObject Workspace)> Build);

    static string F(double v) => v.ToString("R", CultureInfo.InvariantCulture);
    static string I(int v) => v.ToString(CultureInfo.InvariantCulture);
    static string Clean(string s) => s.Replace(";", ",").Replace("\n", " ").Replace("|", "/");
    static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    internal static void Run(string output, string commit, string sha)
    {
        string header = "ANTHEA commit " + commit + "; ANTHEA.Calculations.dll SHA-256 " + sha;
        var cases = new List<Case>(Catalog());
        string? wallError = null;
        try { cases.AddRange(DefaultWall()); } catch (Exception ex) { wallError = ex.GetType().Name + ": " + ex.Message; }

        var csv = new StringBuilder();
        csv.AppendLine("# ReinforcedConcreteSection.Mesh fingerprints (CheckerSection.PrepareModel, automatic mesh size); " + header);
        csv.AppendLine("# Lengths mm, areas mm2. shaOutline: outline and holes passed to Shape2d (ANTHEA input); shaNodes: 'id;x;y;z' per vertex in mesh order;");
        csv.AppendLine("# shaFaces: 'id;a;b;c;d' per face in mesh order; shaCanonical: faces as coordinate loops, rotated to the smallest vertex and sorted. Coordinates 'R' invariant.");
        csv.AppendLine("# solverMesh: 'same' if, after the construction of the native solver and one serviceability stress analysis, the section still holds the hashed mesh.");
        csv.AppendLine("id;group;name;shape;width;height;circularSides;outlinePoints;holes;bars;areaCls;bboxX;bboxY;outcome;nodes;faces;triangles;quads;meshArea;shaOutline;shaNodes;shaFaces;shaCanonical;solverMesh;message");
        int id = 0, ok = 0;
        using var file = File.Create(Path.Combine(output, "mesh-detail.jsonl.gz"));
        using var zip = new System.IO.Compression.GZipStream(file, System.IO.Compression.CompressionLevel.SmallestSize);
        using var detail = new StreamWriter(zip, new UTF8Encoding(false));
        detail.WriteLine(new JsonObject { ["header"] = "ReinforcedConcreteSection.Mesh vertices [id, x, y, z] and faces [id, a, b, c, d] in mesh order; " + header }.ToJsonString());
        foreach (var c in cases)
        {
            string head = string.Join(";", id++, c.Group, c.Name);
            JsonObject input, workspace;
            CheckerSectionModel model;
            try { (input, workspace) = c.Build(); model = CheckerSection.PrepareModel(input, workspace); }
            catch (Exception ex) { csv.AppendLine(string.Join(";", head, "", "", "", "", "", "", "", "", "", "", "section-error:" + ex.GetType().Name, "", "", "", "", "", "", "", "", "", "", Clean(ex.Message))); continue; }
            var geometry = model.Geometry; var section = model.Section;
            var box = section.ConcreteShape.Get2dBoundingBox().Size;
            string outline = string.Join("\n", new[] { geometry.Outline.ToArray() }.Concat(geometry.Holes).Select(loop => string.Join("|", loop.Select(p => F(p[0]) + "," + F(p[1])))));
            head = string.Join(";", head, geometry.Shape, F(geometry.Width), F(geometry.Height), geometry.Shape == "Circolare" ? geometry.CircularSides.ToString(CultureInfo.InvariantCulture) : "",
                geometry.Outline.Count, geometry.Holes.Count, geometry.Bars.Count, F(geometry.AreaCls), F(box.X), F(box.Y));
            try
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var mesh = section.Mesh;
                var vertices = mesh.GetVertices(); var faces = mesh.GetFaces();
                var nodes = new StringBuilder(); var connectivity = new StringBuilder();
                foreach (var v in vertices) nodes.Append(string.Join(";", I(v.Id), F(v.Point.X), F(v.Point.Y), F(v.Point.Z))).Append('\n');
                foreach (var f in faces) connectivity.Append(string.Join(";", I(f.Id), I(f.A), I(f.B), I(f.C), I(f.D))).Append('\n');
                var points = vertices.ToDictionary(v => v.Id, v => v.Point);
                var canonical = faces.Select(f =>
                {
                    var loop = (f.IsQuad ? new[] { f.A, f.B, f.C, f.D } : new[] { f.A, f.B, f.C }).Select(i => points[i]).ToArray();
                    int first = 0;
                    for (int k = 1; k < loop.Length; k++)
                        if (loop[k].X < loop[first].X || (loop[k].X == loop[first].X && loop[k].Y < loop[first].Y)) first = k;
                    return string.Join("|", loop.Skip(first).Concat(loop.Take(first)).Select(p => F(p.X) + "," + F(p.Y) + "," + F(p.Z)));
                }).OrderBy(s => s, StringComparer.Ordinal);
                double area = 0;
                foreach (var f in faces) area += mesh.FaceArea(f);
                long elapsed = watch.ElapsedMilliseconds;
                // The solver reads ReinforcedConcreteSection.Mesh: another element size would replace the cached mesh.
                string solver;
                try
                {
                    var engine = new CheckerSection(model, input, workspace, J.Obj(("criterio", "N costante"), ("modello", "Non lineare")));
                    engine.Stress(new ActionPoint(-100, 10, 0), "SLE");
                    solver = ReferenceEquals(section.Mesh, mesh) ? "same" : "different";
                }
                catch (Exception ex) { solver = "error:" + ex.GetType().Name; }
                csv.AppendLine(string.Join(";", head, "ok", vertices.Length, faces.Length, faces.Count(f => f.IsTriangle), faces.Count(f => f.IsQuad), F(area),
                    Hash(outline), Hash(nodes.ToString()), Hash(connectivity.ToString()), Hash(string.Join("\n", canonical)), solver, ""));
                detail.WriteLine(new JsonObject
                {
                    ["id"] = id - 1, ["name"] = c.Name,
                    ["vertices"] = new JsonArray(vertices.Select(v => (JsonNode)new JsonArray(v.Id, v.Point.X, v.Point.Y, v.Point.Z)).ToArray()),
                    ["faces"] = new JsonArray(faces.Select(f => (JsonNode)new JsonArray(f.Id, f.A, f.B, f.C, f.D)).ToArray())
                }.ToJsonString());
                ok++;
                Console.WriteLine($"  mesh {id - 1} {c.Name}: {vertices.Length} nodi, {faces.Length} facce, {elapsed} ms, solutore {solver}");
            }
            catch (Exception ex) { csv.AppendLine(string.Join(";", head, "error:" + ex.GetType().Name, "", "", "", "", "", Hash(outline), "", "", "", "", Clean(ex.Message))); }
        }
        if (wallError is not null) csv.AppendLine(string.Join(";", id++, "modulo", "geo_muri_sostegno predefinito", "", "", "", "", "", "", "", "", "", "", "module-error", "", "", "", "", "", "", "", "", "", "", Clean(wallError)));
        File.WriteAllText(Path.Combine(output, "mesh-fingerprint.csv"), csv.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"Mesh: {ok} sezioni su {id} -> {Path.Combine(output, "mesh-fingerprint.csv")}");
    }

    // Generic sections: the input of the RC module (SezioneCA.DefaultData) with the given edits, as in the other captures.
    static (JsonObject, JsonObject) Concrete(Action<JsonObject> edit)
    {
        var data = SezioneCA.DefaultData(); var workspace = SectionWorkspace.Prepare(data); var input = data["input"]!.AsObject(); edit(input);
        return (input, workspace);
    }

    static string N(double v) => F(v);

    static Action<JsonObject> Rect(double b, double h, int top, double topD, int bottom, double bottomD, int side, double sideD, double cover, double stirrup) => i =>
    {
        i["shape"] = "Rettangolare"; i["width_mm"] = N(b); i["height_mm"] = N(h); i["top_bar_count"] = N(top); i["top_bar_diameter_mm"] = N(topD);
        i["bottom_bar_count"] = N(bottom); i["bottom_bar_diameter_mm"] = N(bottomD); i["side_bar_count_per_side"] = N(side); i["side_bar_diameter_mm"] = N(sideD);
        i["cover_mm"] = N(cover); i["transverse_bar_diameter_mm"] = N(stirrup);
    };

    static Action<JsonObject> Tee(double bf, double bw, double hf, double h, int top, double topD, int bottom, double bottomD, int side, double sideD, double cover, double stirrup) => i =>
    {
        Rect(bf, h, top, topD, bottom, bottomD, side, sideD, cover, stirrup)(i);
        i["shape"] = "A T"; i["flange_width_mm"] = N(bf); i["web_width_mm"] = N(bw); i["flange_thickness_mm"] = N(hf);
    };

    static Action<JsonObject> Circle(double d, int count, double diameter, double cover, double stirrup, int sides = 32) => i =>
    {
        i["shape"] = "Circolare"; i["diameter_mm"] = N(d); i["longitudinal_bar_count"] = N(count); i["longitudinal_bar_diameter_mm"] = N(diameter);
        i["cover_mm"] = N(cover); i["transverse_bar_diameter_mm"] = N(stirrup); i["circular_sides"] = N(sides);
    };

    static Action<JsonObject> With(this Action<JsonObject> first, Action<JsonObject> then) => i => { first(i); then(i); };
    static Action<JsonObject> RectHole(double bi, double hi) => i => { i["foro_presente"] = true; i["inner_width_mm"] = N(bi); i["inner_height_mm"] = N(hi); };
    static Action<JsonObject> CircleHole(double di) => i => { i["foro_presente"] = true; i["inner_diameter_mm"] = N(di); };
    static Action<JsonObject> SecondRing(int count, double diameter, double gap) => i =>
    {
        i["second_inner_enabled"] = true; i["second_inner_count"] = N(count); i["second_inner_diameter"] = N(diameter); i["second_inner_gap"] = N(gap);
    };

    static IEnumerable<Case> Catalog()
    {
        Case C(string group, string name, Action<JsonObject> edit) => new(group, name, () => Concrete(edit));
        // Rectangles: beams, columns, deep and wide beams, slab and wall strips one metre wide.
        foreach (var (name, edit) in new (string, Action<JsonObject>)[]
        {
            ("R160x160", Rect(160, 160, 2, 12, 2, 12, 0, 12, 40, 8)), ("R200x200", Rect(200, 200, 2, 16, 2, 16, 0, 16, 30, 8)),
            ("R250x250", Rect(250, 250, 2, 10, 2, 10, 0, 10, 25, 6)), ("R300x300", Rect(300, 300, 2, 14, 2, 14, 0, 14, 30, 8)),
            ("R300x500", Rect(300, 500, 2, 16, 3, 20, 0, 16, 30, 8)), ("R300x600", Rect(300, 600, 2, 14, 4, 20, 1, 12, 35, 8)),
            ("R400x400", Rect(400, 400, 3, 16, 3, 16, 1, 16, 35, 8)), ("R400x700", Rect(400, 700, 3, 16, 4, 24, 1, 14, 35, 10)),
            ("R500x1000", Rect(500, 1000, 4, 20, 6, 24, 2, 16, 70, 10)), ("R300x1200", Rect(300, 1200, 2, 16, 4, 20, 4, 12, 40, 10)),
            ("R1200x300", Rect(1200, 300, 6, 14, 8, 16, 0, 12, 35, 10)), ("R1000x200", Rect(1000, 200, 5, 12, 5, 12, 0, 12, 30, 8)),
            ("R1000x300", Rect(1000, 300, 5, 14, 5, 14, 0, 14, 30, 8)), ("R1000x500", Rect(1000, 500, 5, 16, 5, 20, 0, 16, 40, 8)),
            ("R1000x800", Rect(1000, 800, 5, 20, 7, 24, 0, 20, 50, 10)), ("R1000x1000", Rect(1000, 1000, 4, 20, 6, 24, 2, 16, 70, 10)),
            ("R2000x500", Rect(2000, 500, 10, 16, 10, 20, 0, 16, 40, 10))
        })
            yield return C("rettangolare", name, edit);
        // T sections: the default of the RC module and beams from small to bridge size.
        yield return C("a T", "T1200x800", i => { i["shape"] = "A T"; });
        yield return C("a T", "T600x500", Tee(600, 250, 120, 500, 4, 14, 3, 20, 0, 12, 30, 8));
        yield return C("a T", "T1500x600", Tee(1500, 300, 150, 600, 6, 14, 3, 20, 1, 12, 30, 8));
        yield return C("a T", "T2000x1000", Tee(2000, 300, 200, 1000, 8, 16, 4, 25, 2, 12, 35, 10));
        yield return C("a T", "T2500x1500", Tee(2500, 400, 250, 1500, 9, 16, 6, 26, 3, 14, 40, 12));
        // Circles with one ring of bars (32 sides, the default) and the default pile with other numbers of sides.
        foreach (var (name, edit) in new (string, Action<JsonObject>)[]
        {
            ("C300", Circle(300, 6, 14, 40, 8)), ("C400", Circle(400, 8, 16, 40, 8)), ("C500", Circle(500, 10, 16, 50, 8)), ("C600", Circle(600, 12, 20, 50, 10)),
            ("C800", Circle(800, 14, 24, 60, 10)), ("C1000", Circle(1000, 16, 24, 70, 10)), ("C1200", Circle(1200, 20, 26, 60, 12)), ("C1500", Circle(1500, 24, 26, 70, 12)),
            ("C2000", Circle(2000, 32, 30, 75, 14))
        })
            yield return C("circolare", name, edit);
        foreach (int sides in new[] { 12, 16, 24, 48, 64, 72, 96, 128, 180, 360, 720 })
            yield return C("circolare lati", "C1000-L" + sides, Circle(1000, 16, 24, 70, 10, sides));
        // Circles with two rings of bars (the bars do not enter the concrete mesh today: same hashes as one ring).
        yield return C("circolare 2 anelli", "C800-2A", Circle(800, 14, 24, 60, 10).With(SecondRing(10, 16, 60)));
        yield return C("circolare 2 anelli", "C1000-2A", Circle(1000, 16, 24, 70, 10).With(SecondRing(12, 16, 88)));
        yield return C("circolare 2 anelli", "C1200-2A", Circle(1200, 20, 26, 60, 12).With(SecondRing(16, 20, 50)));
        // Hollow sections: rectangular boxes and tubes.
        yield return C("cava", "R600x800H", Rect(600, 800, 4, 20, 6, 24, 0, 16, 70, 10).With(RectHole(300, 400)));
        yield return C("cava", "R1000x1000H", Rect(1000, 1000, 4, 20, 6, 24, 2, 16, 70, 10).With(RectHole(600, 600)));
        yield return C("cava", "R1200x1000H", Rect(1200, 1000, 4, 20, 6, 20, 0, 16, 40, 10).With(RectHole(700, 500)));
        yield return C("cava", "R800x1200H", Rect(800, 1200, 4, 20, 4, 24, 0, 16, 50, 10).With(RectHole(400, 800)));
        yield return C("cava", "C800H", Circle(800, 16, 24, 70, 10).With(CircleHole(400)));
        yield return C("cava", "C1000H", Circle(1000, 16, 24, 70, 10).With(CircleHole(500)));
        yield return C("cava", "C1000H-L64", Circle(1000, 16, 24, 70, 10, 64).With(CircleHole(500)));
        yield return C("cava", "C1200H", Circle(1200, 16, 24, 70, 10).With(CircleHole(900)));
        yield return C("cava", "C1500H", Circle(1500, 16, 24, 70, 10).With(CircleHole(1000)));
        yield return C("cava", "C1000H-2A", Circle(1000, 16, 24, 70, 10).With(CircleHole(500)).With(SecondRing(12, 16, 88)));
        // Beyond the 1000 boundary points of DelaunayMesh 2.0.0.10 (randomized insertion with a fixed seed): not a usual section.
        yield return C("oltre soglia", "C1000H-L720", Circle(1000, 16, 24, 70, 10, 720).With(CircleHole(500)));
        // Module defaults: RC section (str_palo) and horizontal pile (HorizontalConcreteSection: circle of the pile diameter, pile workspace).
        yield return new("modulo", "str_palo predefinita", () => { var data = SezioneCA.DefaultData(); var ws = SectionWorkspace.Prepare(data); return (data["input"]!.AsObject(), ws); });
        yield return new("modulo", "geo_palo_orizzontale predefinita", () =>
        {
            var data = PaloOrizzontale.Defaults(); var input = (JsonObject)data["sezione"]!.DeepClone();
            input["shape"] = "Circolare"; input["diameter_mm"] = data["generali"].D("diametro") * 1000;
            return (input, ConcreteStandards.PileWorkspace(input));
        });
    }

    // Default cantilever wall: the sections of the structural checks (RetainingWall.SectionInput, one metre strip of the member thickness).
    static IEnumerable<Case> DefaultWall()
    {
        var result = RetainingWall.Calculate(RetainingWall.Defaults());
        string[] members = ["Fusto", "Valle", "Monte"];
        var forces = result.Cases.SelectMany(c => c.Sections).Where(s => s.Position > 0 || s.N != 0 || s.V != 0 || s.M != 0)
            .GroupBy(s => (s.Name, s.Thickness)).Select(g => g.OrderBy(s => s.Position).First())
            .OrderBy(s => Array.IndexOf(members, s.Name)).ThenBy(s => s.Thickness).ToList();
        foreach (var f in forces)
            yield return new("modulo", "geo_muri_sostegno predefinito " + f.Name + " t=" + F(f.Thickness), () =>
            {
                var input = RetainingWall.SectionInput(result.Input, f.Name, f.Thickness, f.Position);
                var data = SezioneCA.DefaultData(); data["input"] = input.DeepClone();
                return (input, SectionWorkspace.Prepare(data));
            });
    }
}
