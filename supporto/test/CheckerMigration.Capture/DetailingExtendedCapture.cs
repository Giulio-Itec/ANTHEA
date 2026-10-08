using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Calculations;

// Refactoring F2.8-A0 (supporto/artefatti/refactoring/f27-f28-progetto/F28-progetto-rivisto.md, §5 A0): extended legacy capture of
// detailing, bond and moment-curvature, written only to new files of the mode 'tutte', after every other capture:
//   detailing-plate-legacy.csv, detailing-plate-sections.xml  ConcreteDetailingCalculator on solid slabs (1000 x h strips) and walls,
//                                                              slab rejections, beams, columns, slabs and walls with -2 stirrup legs (X10)
//   detailing-texts-legacy.csv                                 name, unit, reference and explanation of every check met in this capture
//   detailing-adapter-legacy.jsonl                             ConcreteDetailingAnalysis.Calculate and .Anchorage on complete sheets
//   bond-legacy.csv                                            ConcreteBond.Calculate, rejections with their messages
//   curvature-production-legacy.csv                            ConcreteCurvatureAnalysis with the production settings (64 directions)
// The texts that the legacy formats with the current culture (Status of the curves) are written with it-IT, as in the headless capture.
internal static class DetailingExtendedCapture
{
    static string F(double? v) => v is double d ? d.ToString("R", CultureInfo.InvariantCulture) : "";
    static JsonNode? N(double? v) => v is double d ? JsonValue.Create(d.ToString("R", CultureInfo.InvariantCulture)) : null;
    // Texts in CSV cells are kept exact: '%', ';', '|', CR and LF are percent-encoded (Uri.UnescapeDataString restores them).
    static string Escape(string s) => s.Replace("%", "%25").Replace(";", "%3B").Replace("|", "%7C").Replace("\r", "%0D").Replace("\n", "%0A");
    static readonly JsonSerializerOptions Json = new() { WriteIndented = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    internal static void Run(string output, string commit, string sha)
    {
        var culture = CultureInfo.CurrentCulture; var uiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("it-IT");
        try
        {
            string header = "ANTHEA commit " + commit + "; ANTHEA.Calculations.dll SHA-256 " + sha + "; culture " + CultureInfo.CurrentCulture.Name;
            var texts = new TextCatalog();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            int plate = Plates(output, header, texts);
            int adapter = Adapter(output, header, texts);
            int grid = BeamColumnTexts(texts);
            texts.Write(Path.Combine(output, "detailing-texts-legacy.csv"), header, grid);
            Console.WriteLine($"{plate} dettagli di solette e pareti, {adapter} fogli completi, {texts.Count} testi dei controlli ({grid} calcoli aggiuntivi di travi e pilastri), {watch.Elapsed.TotalSeconds:F1} s");
            watch.Restart();
            int bond = Bond(output, header);
            Console.WriteLine($"{bond} casi di aderenza, {watch.Elapsed.TotalSeconds:F1} s");
            watch.Restart();
            int curves = Curves(output, header);
            Console.WriteLine($"{curves} curve M-χ di produzione, {watch.Elapsed.TotalSeconds:F1} s");
        }
        finally { CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = uiCulture; }
    }

    // ------------------------------------------------------------------ sections
    static Action<JsonObject> Rect(double width, double height, int top, double topPhi, int bottom, double bottomPhi, int side, double sidePhi, double cover, double fck,
        bool stirrups = true, double transverse = 8, Action<JsonObject>? more = null) => i =>
    {
        i["shape"] = "Rettangolare"; i["width_mm"] = F(width); i["height_mm"] = F(height);
        i["top_bar_count"] = top.ToString(CultureInfo.InvariantCulture); i["top_bar_diameter_mm"] = F(topPhi);
        i["bottom_bar_count"] = bottom.ToString(CultureInfo.InvariantCulture); i["bottom_bar_diameter_mm"] = F(bottomPhi);
        i["side_bar_count_per_side"] = side.ToString(CultureInfo.InvariantCulture); i["side_bar_diameter_mm"] = F(sidePhi);
        i["cover_mm"] = F(cover); i["transverse_bar_diameter_mm"] = F(transverse); i["fck_mpa"] = F(fck);
        if (!stirrups) i["staffe_presenti"] = "No";
        more?.Invoke(i);
    };

    static Action<JsonObject> Manual(double width, double height, double cover, double fck, params (double X, double Y, double Phi)[] bars) => i =>
    {
        i["shape"] = "Rettangolare"; i["width_mm"] = F(width); i["height_mm"] = F(height); i["cover_mm"] = F(cover); i["fck_mpa"] = F(fck);
        i["transverse_bar_diameter_mm"] = "8";
        i["barre_manuali"] = new JsonArray(bars.Select(b => (JsonNode)new JsonObject { ["x"] = F(b.X), ["y"] = F(b.Y), ["phi"] = F(b.Phi) }).ToArray());
    };

    static Action<JsonObject> Circle(double diameter, int count, double phi, double cover, double transverse, double fck, (int Count, double Phi, double Gap)? second = null) => i =>
    {
        i["shape"] = "Circolare"; i["diameter_mm"] = F(diameter); i["longitudinal_bar_count"] = count.ToString(CultureInfo.InvariantCulture);
        i["longitudinal_bar_diameter_mm"] = F(phi); i["cover_mm"] = F(cover); i["transverse_bar_diameter_mm"] = F(transverse); i["fck_mpa"] = F(fck);
        if (second is { } s)
        {
            i["second_inner_enabled"] = true; i["second_inner_count"] = s.Count.ToString(CultureInfo.InvariantCulture);
            i["second_inner_diameter"] = F(s.Phi); i["second_inner_gap"] = F(s.Gap);
        }
    };

    // Beams and columns: five sections of DetailingCapture (same names and data) plus two-ring columns and a beam with bars only in the bottom half.
    static readonly (string Name, Action<JsonObject> Edit)[] Sections =
    [
        ("R300x500", Rect(300, 500, 2, 16, 3, 20, 0, 16, 30, 30)),
        ("T1200x800", i => { i["shape"] = "A T"; i["fck_mpa"] = "35"; }),
        ("R400x400", Rect(400, 400, 3, 16, 3, 16, 1, 16, 35, 25)),
        ("C400", Circle(400, 8, 16, 40, 8, 32)),
        ("C600R2", Circle(600, 12, 20, 40, 10, 30, (8, 16, 30))),
        // Two rings with radii next to the 5-digit rounding of TensionBarSpacing (Math.Round(r, 5)): 144.000005 and 110.000005 mm, then 143.999995 and 109.999995 mm (M13).
        ("C400R2-r5a", Circle(400, 8, 16, 39.999995, 8, 30, (6, 12, 20))),
        ("C400R2-r5b", Circle(400, 8, 16, 40.000005, 8, 30, (6, 12, 20))),
        ("R600x800H", i => { i["foro_presente"] = true; i["inner_width_mm"] = "300"; i["inner_height_mm"] = "400"; i["side_bar_count_per_side"] = "0"; i["fck_mpa"] = "28"; }),
        ("R300x500B", Manual(300, 500, 30, 30, (-100, -200, 20), (0, -200, 20), (100, -200, 20))),
        // Solid slabs: strips 1000 x h with bars on both faces, without stirrups, with staggered manual bars (no recognised spacing),
        // with bars only on the bottom face; slab rejections: hollow rectangle (and the circle and the T above).
        ("S1000x200", Rect(1000, 200, 5, 12, 5, 12, 0, 12, 25, 30)),
        ("S1000x300", Rect(1000, 300, 4, 16, 7, 20, 0, 16, 30, 35)),
        ("S1000x250N", Rect(1000, 250, 5, 14, 6, 14, 0, 14, 30, 25, stirrups: false)),
        ("S1000x220M", Manual(1000, 220, 25, 30, (-400, -70, 12), (-275, -72.5, 12), (-150, 70.5, 12), (100, -71, 12), (225, 73, 12), (350, 71.5, 12))),
        ("S1000x200B", Manual(1000, 200, 25, 30, (-400, -60, 12), (-200, -60, 12), (0, -60, 12), (200, -60, 12), (400, -60, 12))),
        ("S1000x400H", Rect(1000, 400, 5, 16, 5, 16, 0, 16, 30, 30, more: i => { i["foro_presente"] = true; i["inner_width_mm"] = "600"; i["inner_height_mm"] = "100"; })),
        // Walls 1000 x 200 (bars on the long faces) and 200 x 1000 (side bars), one without stirrups.
        ("W1000x200", Rect(1000, 200, 6, 12, 6, 12, 0, 12, 30, 30)),
        ("W200x1000", Rect(200, 1000, 2, 14, 2, 14, 5, 14, 30, 30)),
        ("W250x1200N", Rect(250, 1200, 2, 16, 2, 16, 6, 16, 35, 30, stirrups: false)),
    ];

    static Action<JsonObject> Edit(string name) => Sections.Single(s => s.Name == name).Edit;

    // ------------------------------------------------------------------ texts of the checks
    sealed class TextCatalog
    {
        readonly List<string> order = [];
        readonly Dictionary<string, (string Kind, DetailingCheck Check, int Count, int Passed, int Failed, int Pending)> rows = new(StringComparer.Ordinal);
        public int Count => order.Count;

        public void Add(ConcreteMemberKind kind, IEnumerable<DetailingCheck> checks)
        {
            foreach (var c in checks)
            {
                string key = string.Join("\u0001", kind, c.Name, c.Unit, c.Reference, c.Explanation);
                if (!rows.TryGetValue(key, out var r)) { order.Add(key); r = (kind.ToString(), c, 0, 0, 0, 0); }
                rows[key] = (r.Kind, r.Check, r.Count + 1, r.Passed + (c.Passed == true ? 1 : 0), r.Failed + (c.Passed == false ? 1 : 0), r.Pending + (c.Passed is null ? 1 : 0));
            }
        }

        public void Write(string path, string header, int grid)
        {
            var csv = new StringBuilder();
            csv.AppendLine("# ConcreteDetailingCalculator legacy texts: one row per kind and distinct (name, unit, reference, explanation), in order of first appearance; " + header);
            csv.AppendLine($"# Sources: detailing-plate-legacy.csv, detailing-adapter-legacy.jsonl and {grid} extra beam and column calculations (sections of this capture; durability, lap and confirmations varied). Counts: occurrences, passed, failed, pending (Passed null). Texts exact, with % ; | CR LF percent-encoded.");
            csv.AppendLine("id;kind;name;unit;reference;explanation;occurrences;passed;failed;pending");
            int id = 0;
            foreach (var key in order)
            {
                var r = rows[key];
                csv.AppendLine(string.Join(";", id++, r.Kind, Escape(r.Check.Name), Escape(r.Check.Unit), Escape(r.Check.Reference), Escape(r.Check.Explanation), r.Count, r.Passed, r.Failed, r.Pending));
            }
            File.WriteAllText(path, csv.ToString(), new UTF8Encoding(false));
        }
    }

    static string Checks(IEnumerable<DetailingCheck> checks) =>
        string.Join("|", checks.Select(c => string.Join(":", Escape(c.Name), F(c.Actual), F(c.Limit), c.Unit, c.Passed?.ToString() ?? "")));

    // ------------------------------------------------------------------ A. slabs, walls, slab rejections, -2 legs
    static int Plates(string output, string header, TextCatalog texts)
    {
        var model = new GPC.Model.Models.Model("Sezioni dettagli di solette e pareti congelate");
        var built = new Dictionary<string, SezioneCA>(StringComparer.Ordinal);
        SezioneCA Section(string name)
        {
            if (built.TryGetValue(name, out var s)) return s;
            var input = SezioneCA.DefaultInput(); Edit(name)(input);
            s = new SezioneCA(input);
            var prepared = CheckerSection.PrepareModel(input, SectionWorkspace.Prepare(SezioneCA.DefaultData()));
            prepared.Section.Name = name; model.AddProperty(prepared.Section);
            return built[name] = s;
        }
        var csv = new StringBuilder();
        csv.AppendLine("# ConcreteDetailingCalculator legacy outputs (solid slabs, walls, slab rejections, rows with -2 stirrup legs); " + header);
        csv.AppendLine("# Sections in detailing-plate-sections.xml. Lengths mm, areas mm2, NEd N (compression positive), secondary steel mm2/m (0: not given); checks: name:actual:limit:unit:passed separated by '|'; names and messages with % ; | CR LF percent-encoded.");
        csv.AppendLine("id;section;kind;fck;fyk;fyd;areaCls;width;height;topWidth;bottomWidth;webWidth;nominalCover;compression;stirrups;stirrupDiameter;stirrupSpacing;legs;aggregate;durabilityCover;deviation;lap;restrained;endAnchorage;secondarySteel;secondarySpacing;critical;outcome;checks");
        var calculator = new ConcreteDetailingCalculator(); int id = 0;
        void Row(string name, ConcreteMemberKind kind, double nEd, double sd, double sp, int legs, double aggregate, double durability, double deviation, bool lap,
            bool restrained, bool end, double secondary, double secondarySpacing, bool critical)
        {
            var s = Section(name);
            bool stirrups = s.Input.S("staffe_presenti", "Sì") == "Sì";
            double top = s.Shape == "A T" ? s.Input.D("flange_width_mm") : s.Width, bottom = s.Shape == "A T" ? s.Input.D("web_width_mm") : s.Width;
            string head = string.Join(";", id++, name, kind, F(s.Input.D("fck_mpa")), F(s.Input.D("fyk_mpa")), F(s.Fyd), F(s.AreaCls), F(s.Width), F(s.Height), F(top), F(bottom),
                F(s.Shape == "A T" ? s.Input.D("web_width_mm") : s.Width), F(s.Input.D("cover_mm")), F(nEd * 1000), stirrups, F(sd), F(sp), legs, F(aggregate), F(durability), F(deviation),
                lap, restrained, end, F(secondary), F(secondarySpacing), critical);
            try
            {
                var checks = calculator.Calculate(new ConcreteDetailingInput(kind, s, nEd, stirrups, sd, sp, legs, aggregate, durability, deviation, lap, secondary, secondarySpacing,
                    critical, restrained, end));
                texts.Add(kind, checks);
                csv.AppendLine(string.Join(";", head, "ok", Checks(checks)));
            }
            catch (Exception ex) { csv.AppendLine(string.Join(";", head, "error:" + ex.GetType().Name, Escape(ex.Message))); }
        }
        // Grid of slabs and walls: durability cover (NaN, +∞ and -5 pending as in the legacy), critical region, lap zone; the other parameters cycle.
        int variant = 0;
        foreach (var (name, kind) in new[] { ("S1000x200", ConcreteMemberKind.Slab), ("S1000x300", ConcreteMemberKind.Slab), ("S1000x250N", ConcreteMemberKind.Slab),
            ("S1000x220M", ConcreteMemberKind.Slab), ("S1000x200B", ConcreteMemberKind.Slab), ("W1000x200", ConcreteMemberKind.Wall), ("W200x1000", ConcreteMemberKind.Wall),
            ("W250x1200N", ConcreteMemberKind.Wall) })
            foreach (double durability in new[] { double.NaN, double.PositiveInfinity, -5, 25, 45 })
                foreach (bool critical in new[] { false, true })
                    foreach (bool lap in new[] { false, true })
                    {
                        int v = variant++;
                        double secondary = new[] { 0.0, 150, 300, 600, 1200 }[v % 5], spacing = new[] { 0.0, 150, 300, 420, 500 }[(v / 3) % 5];
                        double sd = new[] { 6.0, 8, 10 }[v % 3], sp = new[] { 100.0, 200, 300 }[(v / 2) % 3], aggregate = new[] { 16.0, 20, 40 }[(v / 5) % 3];
                        Row(name, kind, new[] { 0.0, 800, 2500 }[(v / 2) % 3], sd, sp, v % 2 == 0 ? 2 : 4, aggregate, durability, (v / 3) % 2 == 0 ? 10 : 5, lap,
                            v % 4 != 2, v % 3 != 1, secondary, spacing, critical);
                    }
        // Slab rejections (strip message before the numeric validation, as ConcreteDetailing.cs:51-52): hollow rectangle, circle, T.
        foreach (var name in new[] { "S1000x400H", "C400", "T1200x800" })
        {
            Row(name, ConcreteMemberKind.Slab, 0, 8, 200, 2, 20, 25, 10, false, true, true, 300, 200, false);
            Row(name, ConcreteMemberKind.Slab, 500, 8, 200, 2, 20, double.NaN, 10, true, false, false, 0, 0, true);
        }
        Row("T1200x800", ConcreteMemberKind.Slab, 0, 8, 200, 2, 20, 25, 10, false, true, true, -100, 200, false);
        // Numeric rejections reachable from the sheet (negative as_secondaria, passo_secondaria, delta_c).
        Row("S1000x200", ConcreteMemberKind.Slab, 0, 8, 200, 2, 20, 25, 10, false, true, true, -100, 200, false);
        Row("W1000x200", ConcreteMemberKind.Wall, 0, 8, 200, 2, 20, 25, 10, false, true, true, 300, -50, false);
        Row("S1000x200", ConcreteMemberKind.Slab, 0, 8, 200, 2, 20, 25, -1, false, true, true, 300, 200, false);
        // Stirrup legs -2 (rami_y = -2 from the parameters panel, X10) for every kind: the legacy computes with the negative count.
        foreach (var (name, kind) in new[] { ("R300x500", ConcreteMemberKind.Beam), ("R400x400", ConcreteMemberKind.Column), ("S1000x200", ConcreteMemberKind.Slab), ("W1000x200", ConcreteMemberKind.Wall) })
            foreach (var (sp, lap, durability, nEd) in new[] { (150.0, false, 25.0, 0.0), (300.0, true, double.NaN, 1500.0), (100.0, false, 45.0, 400.0) })
                Row(name, kind, nEd, 8, sp, -2, 20, durability, 10, lap, true, false, 300, 200, false);
        using (var stream = File.Create(Path.Combine(output, "detailing-plate-sections.xml"))) GPC.Model.Persistence.ModelArchive.Save(model, stream);
        File.WriteAllText(Path.Combine(output, "detailing-plate-legacy.csv"), csv.ToString(), new UTF8Encoding(false));
        return id;
    }

    // ------------------------------------------------------------------ B. complete sheets through ConcreteDetailingAnalysis
    static JsonObject Projection(ConcreteDetailingResult r) => new()
    {
        ["MaximumCompression"] = N(r.MaximumCompression),
        ["Durability"] = r.Durability is { } d ? new JsonObject
        {
            ["Environment"] = d.Environment, ["Severity"] = d.Severity, ["Cmin"] = N(d.Cmin), ["C0"] = N(d.C0), ["TableCover"] = N(d.TableCover), ["LifeExtra"] = N(d.LifeExtra),
            ["LowStrengthExtra"] = N(d.LowStrengthExtra), ["QualityReduction"] = N(d.QualityReduction),
            ["Cover"] = new JsonObject
            {
                ["Bond"] = N(d.Cover.Bond), ["Durability"] = N(d.Cover.Durability), ["Minimum"] = N(d.Cover.Minimum), ["Nominal"] = N(d.Cover.Nominal),
                ["Lines"] = new JsonArray(d.Cover.Lines.Select(l => (JsonNode)new JsonObject { ["Exposure"] = l.Exposure, ["StructuralClass"] = l.StructuralClass, ["Durability"] = N(l.Durability) }).ToArray())
            }
        } : null,
        ["DurabilityError"] = r.DurabilityError,
        ["Checks"] = new JsonArray(r.Checks.Select(c => (JsonNode)new JsonObject
        {
            ["Name"] = c.Name, ["Actual"] = N(c.Actual), ["Limit"] = N(c.Limit), ["Unit"] = c.Unit, ["Passed"] = c.Passed, ["Reference"] = c.Reference, ["Explanation"] = c.Explanation
        }).ToArray())
    };

    static JsonObject Projection(ConcreteAnchorageResult r) => new()
    {
        ["Diameter"] = N(r.Diameter), ["Stress"] = N(r.Stress), ["Fctk05"] = N(r.Fctk05),
        ["Check"] = new JsonObject
        {
            ["Fbd"] = N(r.Check.Fbd), ["BasicLength"] = N(r.Check.BasicLength), ["RequiredLength"] = N(r.Check.RequiredLength), ["Passed"] = r.Check.Passed,
            ["Expression"] = r.Check.Expression, ["Eta1"] = N(r.Check.Eta1), ["Eta2"] = N(r.Check.Eta2), ["Alpha6"] = N(r.Check.Alpha6),
            ["MaximumLapClearDistance"] = N(r.Check.MaximumLapClearDistance), ["LengthPassed"] = r.Check.LengthPassed, ["LapClearDistancePassed"] = r.Check.LapClearDistancePassed
        }
    };

    static JsonObject Error(Exception ex) => new() { ["error"] = ex.GetType().Name, ["message"] = ex.Message };

    sealed record Scenario(string Name, string Standard, string Exposure, string Legs, string Life, string Quality, string Aggregate, string Deviation, string Lap,
        string Secondary, string SecondarySpacing, string Critical, string Restrained, string EndAnchorage, string[] Slu, string[] Slv,
        string Type, string Diameter, string Stress, string Bond, string Length, string Percent, string Clear, string? Fck = null, string? Diagram = null, string? Element = null);

    static int Adapter(string output, string header, TextCatalog texts)
    {
        string[] compression = ["-500", "200"], seismic = ["-800", "150"];
        var scenarios = new[]
        {
            // s1 base; s2 exposure missing, confirmations, lap zone, critical region, lap of 50 % with clear distance > 4Ø; s3 legs -2, exposure XS3,
            // 100 years, quality control, tension only, empty Ø, σ and length; s4 invalid concrete diagram (M8); s5 standard other than NTC.
            new Scenario("s1-base", "NTC 2018", "XC4", "2", "50", "No", "20", "10", "No", "300", "200", "No", "Da confermare", "Da confermare", compression, seismic,
                "Ancoraggio rettilineo", "", "", "Buona", "1000", "100", "0"),
            new Scenario("s2-esposizione-assente", "NTC 2018", "Da scegliere", "4", "50", "No", "40", "5", "Sì", "0", "0", "Sì", "Confermato", "Confermato", ["-1200"], ["-1500", "-300"],
                "Sovrapposizione rettilinea", "20", "300", "Altre condizioni", "1500", "50", "100"),
            new Scenario("s3-rami-meno-2", "NTC 2018", "XS3", "-2", "100", "Sì", "16", "10", "No", "150", "420", "No", "Da confermare", "Confermato", ["100", "250"], ["50"],
                "Ancoraggio rettilineo", "", "", "Buona", "", "100", "0"),
            new Scenario("s4-diagramma-non-valido", "NTC 2018", "XC2", "2", "50", "No", "20", "10", "No", "300", "200", "No", "Confermato", "Da confermare", compression, seismic,
                "Ancoraggio rettilineo", "", "", "Buona", "800", "100", "0", Diagram: "Diagramma inventato"),
            new Scenario("s5-norma-en", "EN 1992-1-1", "XC4", "2", "50", "No", "20", "10", "No", "300", "200", "No", "Da confermare", "Da confermare", compression, seismic,
                "Ancoraggio rettilineo", "", "", "Buona", "1000", "100", "0"),
        };
        var sheets = new (string Element, string Section)[]
        {
            ("Trave", "R300x500"), ("Trave", "T1200x800"), ("Pilastro", "R400x400"), ("Pilastro", "C400"), ("Pilastro", "C600R2"), ("Pilastro", "C400R2-r5a"), ("Pilastro", "C400R2-r5b"),
            ("Soletta piena", "S1000x250N"), ("Soletta piena", "S1000x200B"), ("Soletta piena", "R600x800H"), ("Soletta piena", "T1200x800"), ("Parete", "W1000x200"), ("Parete", "W200x1000")
        };
        var cases = new List<(string Element, string Section, Scenario Scenario)>();
        foreach (var (element, section) in sheets) foreach (var s in scenarios) cases.Add((element, section, s));
        // Strength sweep (fck 12, 25, 60, 70, 90) with Ø 40 for the C60/75 cap of fctk,0.05; σ > fyd; element not chosen.
        foreach (var (element, section) in new[] { ("Trave", "R300x500"), ("Pilastro", "R400x400") })
            foreach (string fck in new[] { "12", "25", "60", "70", "90" })
                cases.Add((element, section, scenarios[0] with { Name = "fck-" + fck, Fck = fck, Diameter = "40", Stress = "350", Length = "2000" }));
        cases.Add(("Trave", "R300x500", scenarios[0] with { Name = "s6-sigma-oltre-fyd", Stress = "999" }));
        cases.Add(("Trave", "R300x500", scenarios[0] with { Name = "s7-elemento-da-scegliere", Element = "Da scegliere" }));

        using var w = new StreamWriter(Path.Combine(output, "detailing-adapter-legacy.jsonl"), false, new UTF8Encoding(false));
        w.WriteLine(new JsonObject { ["header"] = "ConcreteDetailingAnalysis.Calculate and .Anchorage on complete sheets (SezioneCA.DefaultData, SectionWorkspace.Prepare); numbers as invariant 'R' strings; " + header }.ToJsonString(Json));
        foreach (var (element, section, s) in cases)
        {
            var data = SezioneCA.DefaultData(); var settings = SectionWorkspace.Prepare(data); var input = data["input"]!.AsObject();
            Edit(section)(input);
            if (s.Fck is not null) input["fck_mpa"] = s.Fck;
            if (s.Diagram is not null) input["cls_diagramma"] = s.Diagram;
            settings["normativa"] = s.Standard;
            settings["sle_comuni"]!["esposizione"] = s.Exposure;
            settings["taglio"]!["rami_y"] = s.Legs;
            var o = settings["dettagli_costruttivi"]!.AsObject();
            o["elemento"] = s.Element ?? element; o["vita_durabilita"] = s.Life; o["qualita_copriferro"] = s.Quality; o["aggregato"] = s.Aggregate; o["delta_c"] = s.Deviation;
            o["zona_sovrapposizione"] = s.Lap; o["as_secondaria"] = s.Secondary; o["passo_secondaria"] = s.SecondarySpacing; o["zona_critica"] = s.Critical;
            o["barre_trattenute"] = s.Restrained; o["ancoraggio_appoggi"] = s.EndAnchorage;
            var a = settings["ancoraggi"]!.AsObject();
            a["tipo"] = s.Type; a["diametro"] = s.Diameter; a["sigma"] = s.Stress; a["aderenza"] = s.Bond; a["lunghezza"] = s.Length; a["percentuale"] = s.Percent; a["interferro"] = s.Clear;
            var actions = s.Slu.Concat(s.Slv).Select(n => new JsonObject { ["N"] = n }).ToArray();
            var line = new JsonObject
            {
                ["name"] = element + "/" + section + "/" + s.Name,
                ["input"] = new JsonObject
                {
                    ["section"] = section, ["shape"] = input.S("shape"), ["fck_mpa"] = input.S("fck_mpa"), ["cls_diagramma"] = input.S("cls_diagramma"),
                    ["staffe_presenti"] = input.S("staffe_presenti", "Sì"), ["normativa"] = s.Standard, ["esposizione"] = s.Exposure, ["rami_y"] = s.Legs,
                    ["dettagli_costruttivi"] = o.DeepClone(), ["ancoraggi"] = a.DeepClone(),
                    ["SLU"] = new JsonArray(s.Slu.Select(n => (JsonNode)JsonValue.Create(n)).ToArray()), ["SLV"] = new JsonArray(s.Slv.Select(n => (JsonNode)JsonValue.Create(n)).ToArray())
                }
            };
            try
            {
                var r = ConcreteDetailingAnalysis.Calculate(input, settings, actions);
                if (ConcreteKind(o.S("elemento")) is { } kind) texts.Add(kind, r.Checks);
                line["detailing"] = Projection(r);
            }
            catch (Exception ex) { line["detailing"] = Error(ex); }
            try { line["anchorage"] = Projection(ConcreteDetailingAnalysis.Anchorage(input, settings)); }
            catch (Exception ex) { line["anchorage"] = Error(ex); }
            w.WriteLine(line.ToJsonString(Json));
        }
        return cases.Count;
    }

    static ConcreteMemberKind? ConcreteKind(string element) => element switch
    {
        "Trave" => ConcreteMemberKind.Beam, "Pilastro" => ConcreteMemberKind.Column, "Soletta piena" => ConcreteMemberKind.Slab, "Parete" => ConcreteMemberKind.Wall, _ => null
    };

    // Extra beam and column calculations for the text catalogue only: every branch of ConcreteDetailing.cs for the two kinds.
    static int BeamColumnTexts(TextCatalog texts)
    {
        var calculator = new ConcreteDetailingCalculator(); int count = 0;
        foreach (var name in new[] { "R300x500", "T1200x800", "R400x400", "C400", "C600R2", "C400R2-r5a", "R600x800H", "R300x500B" })
        {
            var input = SezioneCA.DefaultInput(); Edit(name)(input); var s = new SezioneCA(input);
            foreach (var kind in new[] { ConcreteMemberKind.Beam, ConcreteMemberKind.Column })
                foreach (double durability in new[] { double.NaN, 25 })
                    foreach (bool lap in new[] { false, true })
                        foreach (bool confirmed in new[] { false, true })
                            foreach (bool stirrups in new[] { true, false })
                            {
                                try { texts.Add(kind, calculator.Calculate(new ConcreteDetailingInput(kind, s, 1500, stirrups, 8, 200, 2, 20, durability, 10, lap, 0, 0, false, confirmed, confirmed))); }
                                catch (ArgumentException) { }
                                count++;
                            }
        }
        return count;
    }

    // ------------------------------------------------------------------ C. bond strength of the material sheet
    static int Bond(string output, string header)
    {
        var csv = new StringBuilder();
        csv.AppendLine("# ConcreteBond.Calculate legacy outputs (fctk,0.05 with the C60/75 cap, fctd, η2, fbd); " + header);
        csv.AppendLine("# Units MPa, mm. Blocks: a fck × Ø × η1 (αct 1, γc 1.5); b fck × αct × γc (Ø 16, η1 1); c Ø × αct × γc (fck 30, η1 0.7). Rejections: outcome error:<type>, message in the last column (% ; | CR LF percent-encoded).");
        csv.AppendLine("id;block;class;fck;diameter;eta1;alphaCt;gammaC;outcome;fct;fctd;eta2;fbd;message");
        var classes = ConcreteMaterialCatalog.MaterialSheetClasses();
        var strengths = new (string Class, double Fck)[] { ("", 0), ("", -1), ("", double.NaN), ("", double.PositiveInfinity), ("", 5), ("", 10) }
            .Concat(classes.Select(c => (c.Name, c.Fck))).ToArray();
        double[] diameters = [6, 8, 10, 12, 14, 16, 20, 25, 26, 28, 30, 32, 34, 36, 40, 131.9, 132];
        double[] alphas = [1, .85, 0, -.5, double.NaN, 1.2], gammas = [1.5, 1, .9, double.NaN, double.PositiveInfinity];
        int id = 0;
        void Row(string block, string name, double fck, double diameter, double eta1, double alpha, double gamma)
        {
            string head = string.Join(";", id++, block, name, F(fck), F(diameter), F(eta1), F(alpha), F(gamma));
            try
            {
                var r = ConcreteBond.Calculate(fck, diameter, eta1, alpha, gamma);
                csv.AppendLine(string.Join(";", head, "ok", F(r.Fct), F(r.Fctd), F(r.Eta2), F(r.Fbd), ""));
            }
            catch (Exception ex) { csv.AppendLine(string.Join(";", head, "error:" + ex.GetType().Name, "", "", "", "", Escape(ex.Message))); }
        }
        foreach (var (name, fck) in strengths) foreach (double d in diameters) foreach (double eta1 in new[] { 1, .7 }) Row("a", name, fck, d, eta1, 1, 1.5);
        foreach (var (name, fck) in strengths) foreach (double alpha in alphas) foreach (double gamma in gammas) Row("b", name, fck, 16, 1, alpha, gamma);
        foreach (double d in diameters) foreach (double alpha in alphas) foreach (double gamma in gammas) Row("c", "C30/37", 30, d, .7, alpha, gamma);
        File.WriteAllText(Path.Combine(output, "bond-legacy.csv"), csv.ToString(), new UTF8Encoding(false));
        return id;
    }

    // ------------------------------------------------------------------ D. moment-curvature with the production settings
    sealed record Curve(string Section, string Standard, string N, string Theta, Action<JsonObject>? Options = null, bool Strands = false);

    static int Curves(string output, string header)
    {
        var curves = new List<Curve>();
        // Direction sweep, oblique directions included (M14): the legacy curvature is Hypot(χx, χy), the library √(χx² + χy²).
        foreach (string theta in new[] { "0", "90", "180", "30", "60", "135", "217", "333" }) curves.Add(new("R300x500", "NTC 2018", "-200", theta));
        curves.Add(new("R300x500", "NTC 2018", "-200", "30", o => o["trazione_cls"] = "Sì"));
        curves.Add(new("R300x500", "NTC 2018", "-200", "45", o => { o["frazione"] = "0.8"; o["raffina_snervamento"] = "0"; }));
        curves.Add(new("R300x500", "NTC 2018", "0", "60", o => { o["campionamento"] = "Uniforme"; o["passi"] = "20"; }));
        curves.Add(new("R600x800H", "NTC 2018", "-1000", "0"));
        curves.Add(new("R600x800H", "NTC 2018", "-1000", "217"));
        curves.Add(new("C600R2", "NTC 2018", "-800", "0"));
        curves.Add(new("C600R2", "NTC 2018", "-800", "135"));
        curves.Add(new("T1200x800", "NTC 2018", "0", "30"));
        curves.Add(new("T1200x800", "EN 1992-1-1", "-500", "333"));
        curves.Add(new("R400x400", "Model Code 2010", "-1200", "60"));
        // Rejections: tight tolerance on the axial residual, strands, axial force beyond the domain, invalid number of steps.
        curves.Add(new("R300x500", "NTC 2018", "-200", "30", o => o["tolleranza_n"] = "1e-9"));
        curves.Add(new("R300x500", "NTC 2018", "-200", "30", o => o["tolleranza_n"] = "1e-15"));
        curves.Add(new("R300x500", "NTC 2018", "-200", "30", Strands: true));
        curves.Add(new("R300x500", "NTC 2018", "-100000", "0"));
        curves.Add(new("R300x500", "NTC 2018", "-200", "0", o => o["passi"] = "9"));
        curves.Add(new("R300x500", "NTC 2018", "-200", "0", o => o["frazione"] = "1.5"));

        var csv = new StringBuilder();
        csv.AppendLine("# ConcreteCurvatureAnalysis legacy outputs with the production settings (ConcreteCalculationSettings: 60 steps, 64 directions, quadratic, tolerance 1 kN, 12 bisections) unless changed; " + header);
        csv.AppendLine("# Sections of this capture on complete sheets (SezioneCA.DefaultData). Moments kNm, curvatures 1/m, strains per mille. points: M:Mx:My:χ:gx:gy:ε0:εc:εs:yielded:limit separated by '|'. Status and messages exact, with % ; | CR LF percent-encoded.");
        csv.AppendLine("id;section;standard;N_kN;theta;steps;fraction;sampling;tension;angles;tolerance;refine;strands;outcome;limitMoment;yield;ultimate;limitN;residual;status;points");
        int id = 0;
        foreach (var c in curves)
        {
            var data = SezioneCA.DefaultData(); var settings = SectionWorkspace.Prepare(data); var input = data["input"]!.AsObject();
            Edit(c.Section)(input);
            settings["normativa"] = c.Standard;
            if (c.Strands) settings["trefoli"] = new JsonArray(new JsonObject { ["id"] = "T1" });
            var o = settings["momento_curvatura"]!.AsObject(); o["N"] = c.N; o["theta"] = c.Theta; c.Options?.Invoke(o);
            string head = string.Join(";", id++, c.Section, c.Standard, o.S("N"), o.S("theta"), o.S("passi"), o.S("frazione"), o.S("campionamento"), o.S("trazione_cls"), o.S("angoli"),
                o.S("tolleranza_n"), o.S("raffina_snervamento"), c.Strands);
            try
            {
                var r = ConcreteCurvatureAnalysis.Calculate(input, settings, o);
                string points = string.Join("|", r.Points.Select(p => string.Join(":", F(p.Moment), F(p.Mx), F(p.My), F(p.Curvature), F(p.GradientX), F(p.GradientY), F(p.Epsilon0),
                    F(p.ConcreteCompressionStrain), F(p.SteelStrain), p.Yielded, p.Limit)));
                csv.AppendLine(string.Join(";", head, "ok", F(r.LimitMoment), F(r.YieldCurvature), F(r.UltimateCurvature), F(r.LimitAxialKn), F(r.AxialResidualKn), Escape(r.Status), points));
            }
            catch (Exception ex) { csv.AppendLine(string.Join(";", head, "error:" + ex.GetType().Name, "", "", "", "", "", Escape(ex.Message), "")); }
        }
        File.WriteAllText(Path.Combine(output, "curvature-production-legacy.csv"), csv.ToString(), new UTF8Encoding(false));
        return id;
    }
}
