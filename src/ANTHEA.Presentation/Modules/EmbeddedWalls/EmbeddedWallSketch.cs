using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Application.EmbeddedWalls;
using Anthea.Application.Stratigraphy;

namespace Anthea.Presentation.Modules.EmbeddedWalls;

/// <summary>Input geometry for the editor, in metres. No pressures or structural responses are inferred.
/// Support prestress is the lock-off in force in the stage, also when kept from the preceding stage.</summary>
/// <remarks>Organizzazione del modulo paratie (progetto del 16/11), step 5: the header (name, case, phase), the legend of the soils, one tint per
/// soil, the lengths of the anchors when «Sfilamento dei tiranti» gives them, and for every element the data it is drawn from, with their
/// table and units (contract C7). Values are read and formatted, never computed.</remarks>
public sealed record EmbeddedWallSketch(
    double? Top, double? Bottom, double? LeftGround, double? RightGround,
    double? LeftWater, double? RightWater, double? LeftSurcharge, double? RightSurcharge,
    string WaterLabel, string Phase, EmbeddedWallRow? StageRow,
    IReadOnlyList<WallSketchBand> Bands, IReadOnlyList<WallSketchSupport> Supports,
    IReadOnlyList<WallSketchLoad> Loads, IReadOnlyList<string> Notices)
{
    /// <summary>Name of the wall (general data), for the header of the drawing.</summary>
    public string Name { get; init; } = "";
    /// <summary>Case drawn, for the header of the drawing (with <see cref="Phase"/>).</summary>
    public string CaseId { get; init; } = "";
    /// <summary>The legend of the soils: one line per soil of the two sides, as the tables give it.</summary>
    public IReadOnlyList<WallSketchSoil> Soils { get; init; } = [];
    /// <summary>The row of the phase drawn as «Fasi costruttive» holds it (ground, water, surcharges), for the hint on its lines.</summary>
    public string StageDetail { get; init; } = "";
    /// <summary>Step 11: the <see cref="Notices"/> with their level and the row they come from (one per text, in the same order).</summary>
    public IReadOnlyList<WallSketchIssue> Issues { get; init; } = [];

    public static EmbeddedWallSketch Read(JsonObject data, IReadOnlyList<EmbeddedWallTable> tables,
        string caseId, EmbeddedWallRow? stage)
    {
        var issues = new List<WallSketchIssue>();
        // Step 11: every notice with its level and the row being read, for the panel Messaggi (no message is added: the same texts).
        (string Table, EmbeddedWallRow? Row) subject = (EmbeddedWallMessages.GeneralTarget, null);
        void Notice(string text, EmbeddedWallMessageLevel level) => issues.Add(new(text, level, subject.Table, subject.Row));
        double? Number(JsonObject row, string key, string label, bool optional = false)
        {
            try
            {
                var value = StratigraphyDocument.Number(row[key], label);
                if (value is null && !optional) Notice(label + ": dato da completare.", EmbeddedWallMessageLevel.Warning);
                return value;
            }
            catch (ArgumentException) { Notice(label + ": numero non valido, non rappresentato.", EmbeddedWallMessageLevel.Error); return null; }
        }
        EmbeddedWallTable Table(string key) => tables.Single(t => t.Key == key);
        var bands = new List<WallSketchBand>();
        foreach (string key in new[] { "left_soils", "right_soils", "segments" })
        foreach (var row in Table(key).Rows)
        {
            subject = (key, row); string name = row[key == "segments" ? "section" : "name"];
            var top = Number(row.Data, "top", name + " / quota superiore");
            var bottom = Number(row.Data, "bottom", name + " / quota inferiore");
            if (top is not null && bottom is not null)
            {
                if (top <= bottom) Notice(name + ": la quota superiore deve superare quella inferiore.", EmbeddedWallMessageLevel.Error);
                else bands.Add(new(key, row, name, top.Value, bottom.Value) { Detail = Detail(Table(key), row) });
            }
        }
        // One tint per soil, by its name, in the order of the two tables (left first): the same soil has the same colour on both sides.
        var names = bands.Where(b => b.Table != "segments").Select(b => b.Name.Trim()).Distinct(StringComparer.Ordinal).ToList();
        bands = bands.Select(b => b.Table == "segments" ? b : b with { Tint = names.IndexOf(b.Name.Trim()) }).ToList();
        string principal = data["case_id"]?.ToString() ?? "";
        bool Belongs(EmbeddedWallRow r) => (string.IsNullOrWhiteSpace(r["case"]) ? principal : r["case"]) == caseId
            && r["stage"] == stage?["id"];
        var supports = new List<WallSketchSupport>();
        var loads = new List<WallSketchLoad>();
        double? lg = null, rg = null, lw = null, rw = null, lq = null, rq = null;
        if (stage is not null)
        {
            subject = ("stages", stage);
            lg = Number(stage.Data, "left_ground", "Terreno a sinistra");
            rg = Number(stage.Data, "right_ground", "Terreno a destra");
            lw = Number(stage.Data, "left_water", "Falda a sinistra", true);
            rw = Number(stage.Data, "right_water", "Falda a destra", true);
            lq = Number(stage.Data, "left_surcharge", "Sovraccarico a sinistra");
            rq = Number(stage.Data, "right_surcharge", "Sovraccarico a destra");
            IReadOnlyList<EmbeddedWallLockOff> lockOffs;
            try { lockOffs = EmbeddedWallDocument.LockOffs(data, caseId); }
            catch (ArgumentException) { lockOffs = []; }
            var shown = new List<EmbeddedWallLockOff>();
            foreach (var activation in Table("activations").Rows.Where(Belongs))
            {
                subject = ("activations", activation);
                var definitions = Table("supports").Rows.Where(r => r["id"] == activation["support"]).ToArray();
                if (definitions.Length != 1)
                {
                    Notice(activation["support"] + ": sostegno assente o identificativo duplicato.", EmbeddedWallMessageLevel.Error);
                    continue;
                }
                var row = definitions[0];
                subject = ("supports", row);
                var z = Number(row.Data, "elevation", row["id"] + " / quota");
                var angle = Number(row.Data, "angle", row["id"] + " / inclinazione");
                var direction = Number(row.Data, "direction", row["id"] + " / verso");
                subject = ("activations", activation);
                var prestress = Number(activation.Data, "prestress", row["id"] + " / pretensione", true);
                var lockOff = lockOffs.FirstOrDefault(l => ReferenceEquals(l.Row, activation.Data));
                if (lockOff is not null) shown.Add(lockOff);
                if (z is null || angle is null || direction is null) continue;
                if (direction is not (1 or -1) || Math.Abs(angle.Value) >= 90 ||
                    row["kind"] is not ("Anchor" or "Strut" or "Bilateral"))
                { subject = ("supports", row); Notice(row["id"] + ": tipo, verso o inclinazione non validi.", EmbeddedWallMessageLevel.Error); continue; }
                // R08: the lengths of an anchor only as «Sfilamento dei tiranti» gives them (one row, both lengths positive); otherwise a schematic symbol.
                var checks = row["kind"] == "Anchor" ? Table("anchor_checks").Rows.Where(r => r["support"] == row["id"]).ToArray() : Array.Empty<EmbeddedWallRow>();
                double? free = checks.Length == 1 ? Length(checks[0], "free_length") : null, bond = checks.Length == 1 ? Length(checks[0], "bond_length") : null;
                var check = free is > 0 && bond is > 0 ? checks[0] : null;
                double? value = lockOff?.Prestress ?? prestress;
                string origin = !string.IsNullOrWhiteSpace(activation["prestress"]) ? "assegnata nella fase"
                    : lockOff?.Previous is not null ? "cella vuota: mantenuta dalla fase precedente" : "cella vuota: 0 kN all'installazione";
                string lengths = row["kind"] != "Anchor" ? ""
                    : check is not null ? $"{Environment.NewLine}Tratto libero {Format(free)} m e cementato {Format(bond)} m lungo l'asse («{EmbeddedWallFields.Title("anchor_checks")}»): disegnati in scala."
                    : $"{Environment.NewLine}Simbolo schematico: tratto libero e cementato non assegnati in «{EmbeddedWallFields.Title("anchor_checks")}».";
                string detail = Detail(Table("supports"), row) + Environment.NewLine
                    + $"Fase {stage["id"]} («{EmbeddedWallFields.Title("activations")}»): pretensione {Format(value)} kN, {origin}." + lengths;
                supports.Add(new(row, activation, row["id"], row["kind"], z.Value, angle.Value, (int)direction, value)
                    { FreeLength = check is null ? null : free, BondLength = check is null ? null : bond, AnchorCheck = check, Detail = detail });
            }
            if (EmbeddedWallDocument.ReducedLockOffs(shown) is { } reduced)
            {
                var first = shown.First(l => l.Reduced);
                subject = ("activations", Table("activations").Rows.FirstOrDefault(r => ReferenceEquals(r.Data, first.Row)));
                Notice(reduced, EmbeddedWallMessageLevel.Warning);
            }
            foreach (var row in Table("loads").Rows.Where(Belongs))
            {
                subject = ("loads", row);
                var z = Number(row.Data, "elevation", "Carico / quota");
                var h = Number(row.Data, "horizontal", "Carico orizzontale");
                var v = Number(row.Data, "downward", "Carico verticale");
                var m = Number(row.Data, "moment", "Momento applicato");
                if (z is not null) loads.Add(new(row, z.Value, z.Value, h, h, v, m) { Detail = Detail(Table("loads"), row) });
            }
            foreach (var row in Table("distributed_loads").Rows.Where(Belongs))
            {
                subject = ("distributed_loads", row);
                var top = Number(row.Data, "top", row["id"] + " / quota superiore");
                var bottom = Number(row.Data, "bottom", row["id"] + " / quota inferiore");
                var qt = Number(row.Data, "q_top", row["id"] + " / q superiore");
                var qb = Number(row.Data, "q_bottom", row["id"] + " / q inferiore");
                if (top > bottom) loads.Add(new(row, top.Value, bottom!.Value, qt, qb, null, null) { Detail = Detail(Table("distributed_loads"), row) });
                else Notice(row["id"] + ": quote del carico distribuito mancanti o non ordinate.", EmbeddedWallMessageLevel.Error);
            }
            subject = ("stages", stage);
            var uniform = Number(stage.Data, "distributed_load", "Carico uniforme sulla parete");
            if (uniform is not null && uniform != 0)
                Notice("Carico uniforme sulla parete: " + Format(uniform) + " kN/m (intera altezza).", EmbeddedWallMessageLevel.Information);
        }
        else { subject = ("stages", null); Notice("Aggiungere una fase a questo caso per vedere terreno, falda e sostegni.", EmbeddedWallMessageLevel.Warning); }
        subject = (EmbeddedWallMessages.GeneralTarget, null);
        var wallTop = Number(data, "top", "Testa paratia");
        var wallBottom = Number(data, "bottom", "Piede paratia");
        if (wallTop <= wallBottom) { Notice("Testa e piede non ordinati: parete non rappresentata.", EmbeddedWallMessageLevel.Error); wallTop = wallBottom = null; }
        return new(wallTop, wallBottom, lg, rg, lw, rw, lq, rq,
            data["hydraulic_mode"]?.ToString() == "FEM 2D" ? "Carico idraulico assegnato" : "Falda assegnata",
            stage?["id"] ?? "Nessuna fase", stage, bands, supports, loads, issues.Select(i => i.Text).Distinct().ToArray())
        {
            Name = data["name"]?.ToString() ?? "", CaseId = caseId, Soils = Legend(bands), Issues = issues.GroupBy(i => i.Text).Select(g => g.First()).ToArray(),
            StageDetail = stage is null ? "" : $"Fase {stage["id"]} del caso «{caseId}»" + Environment.NewLine + Detail(Table("stages"), stage)
        };
    }

    public static string Format(double? value) => value?.ToString("0.###", CultureInfo.CurrentCulture) ?? "—";

    /// <summary>The data of a row as its table shows them: title of the table, then every assigned field with its unit (contract C7).</summary>
    public static string Detail(EmbeddedWallTable table, EmbeddedWallRow row) => "«" + table.Title + "»" + Environment.NewLine + string.Join(Environment.NewLine,
        table.Columns.Where(f => !string.IsNullOrWhiteSpace(row[f.Key])).Select(f => f.Label + ": "
            + (f.Bool ? (row[f.Key] == "true" ? "sì" : row[f.Key] == "false" ? "no" : row[f.Key]) : row[f.Key]) + (string.IsNullOrEmpty(f.Unit) ? "" : " " + f.Unit)));

    /// <summary>A length of «Sfilamento dei tiranti», null when missing or not a number (the checks report their own data).</summary>
    private static double? Length(EmbeddedWallRow row, string key)
    {
        try { return StratigraphyDocument.Number(row.Data[key], key); }
        catch (ArgumentException) { return null; }
    }

    /// <summary>One line per soil: name, γ and φ′ as written in the tables (unread numbers as written), the sides where it lies, its tint.</summary>
    private static IReadOnlyList<WallSketchSoil> Legend(IReadOnlyList<WallSketchBand> bands)
    {
        static string Value(EmbeddedWallRow row, string key)
        {
            try { return Format(StratigraphyDocument.Number(row.Data[key], key)); }
            catch (ArgumentException) { return row[key].Trim(); }
        }
        return bands.Where(b => b.Table != "segments")
            .GroupBy(b => (Name: b.Name.Trim(), Gamma: Value(b.Row, "gamma"), Phi: Value(b.Row, "phi")))
            .Select(g => new WallSketchSoil(g.Key.Name, g.Key.Gamma, g.Key.Phi,
                string.Join(" · ", g.Select(b => b.Table == "left_soils" ? "sx" : "dx").Distinct()), g.First().Tint, g.ToArray()))
            .ToArray();
    }
}

/// <summary>A notice of the drawing (step 11): its text, its level and the row it comes from (table "general": the settings of the wall;
/// no row: the table only).</summary>
public sealed record WallSketchIssue(string Text, EmbeddedWallMessageLevel Level, string Table, EmbeddedWallRow? Row);
/// <summary>A band of the drawing: a soil layer of one side or a segment of the wall. <see cref="Tint"/> is the index of the soil (by name,
/// the same on both sides) among the identification tints; -1 for the segments.</summary>
public sealed record WallSketchBand(string Table, EmbeddedWallRow Row, string Name, double Top, double Bottom)
{
    public int Tint { get; init; } = -1;
    public string Detail { get; init; } = "";
}
/// <summary>A line of the legend of the soils, with the bands it stands for (the first one opens its row).</summary>
public sealed record WallSketchSoil(string Name, string Gamma, string Phi, string Sides, int Tint, IReadOnlyList<WallSketchBand> Bands);
/// <summary>A support present in the phase. <see cref="FreeLength"/> and <see cref="BondLength"/> are the lengths along the axis of
/// «Sfilamento dei tiranti» (<see cref="AnchorCheck"/>), null when not given: the drawing then shows a schematic symbol.</summary>
public sealed record WallSketchSupport(EmbeddedWallRow Row, EmbeddedWallRow Activation, string Id, string Kind,
    double Elevation, double Angle, int Direction, double? Prestress)
{
    public double? FreeLength { get; init; }
    public double? BondLength { get; init; }
    public EmbeddedWallRow? AnchorCheck { get; init; }
    public string Detail { get; init; } = "";
}
public sealed record WallSketchLoad(EmbeddedWallRow Row, double Top, double Bottom,
    double? HorizontalTop, double? HorizontalBottom, double? Downward, double? Moment)
{
    public string Detail { get; init; } = "";
}
/// <summary>An element chosen in the drawing: its table, its row and the data shown when the pointer is on it.</summary>
public sealed record WallSketchPick(string Table, EmbeddedWallRow? Row, string Detail = "");

/// <summary>Limits of the magnification of the section (state of the view, not saved).</summary>
public static class EmbeddedWallSketchZoom
{
    public const double Minimum = 1, Maximum = 8, Step = 1.25, WheelStep = 1.15;
    public static double Clamp(double zoom) => double.IsFinite(zoom) ? Math.Clamp(zoom, Minimum, Maximum) : Minimum;
}
