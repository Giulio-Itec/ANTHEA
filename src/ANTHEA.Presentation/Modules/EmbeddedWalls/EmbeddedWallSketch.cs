using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Application.EmbeddedWalls;
using Anthea.Application.Stratigraphy;

namespace Anthea.Presentation.Modules.EmbeddedWalls;

/// <summary>Input geometry for the editor, in metres. No pressures or structural responses are inferred.
/// Support prestress is the lock-off in force in the stage, also when kept from the preceding stage.</summary>
public sealed record EmbeddedWallSketch(
    double? Top, double? Bottom, double? LeftGround, double? RightGround,
    double? LeftWater, double? RightWater, double? LeftSurcharge, double? RightSurcharge,
    string WaterLabel, string Phase, EmbeddedWallRow? StageRow,
    IReadOnlyList<WallSketchBand> Bands, IReadOnlyList<WallSketchSupport> Supports,
    IReadOnlyList<WallSketchLoad> Loads, IReadOnlyList<string> Notices)
{
    public static EmbeddedWallSketch Read(JsonObject data, IReadOnlyList<EmbeddedWallTable> tables,
        string caseId, EmbeddedWallRow? stage)
    {
        var notices = new List<string>();
        double? Number(JsonObject row, string key, string label, bool optional = false)
        {
            try
            {
                var value = StratigraphyDocument.Number(row[key], label);
                if (value is null && !optional) notices.Add(label + ": dato da completare.");
                return value;
            }
            catch (ArgumentException) { notices.Add(label + ": numero non valido, non rappresentato."); return null; }
        }
        var bands = new List<WallSketchBand>();
        foreach (string key in new[] { "left_soils", "right_soils", "segments" })
        foreach (var row in tables.Single(t => t.Key == key).Rows)
        {
            string name = row[key == "segments" ? "section" : "name"];
            var top = Number(row.Data, "top", name + " / quota superiore");
            var bottom = Number(row.Data, "bottom", name + " / quota inferiore");
            if (top is not null && bottom is not null)
            {
                if (top <= bottom) notices.Add(name + ": la quota superiore deve superare quella inferiore.");
                else bands.Add(new(key, row, name, top.Value, bottom.Value));
            }
        }
        string principal = data["case_id"]?.ToString() ?? "";
        bool Belongs(EmbeddedWallRow r) => (string.IsNullOrWhiteSpace(r["case"]) ? principal : r["case"]) == caseId
            && r["stage"] == stage?["id"];
        var supports = new List<WallSketchSupport>();
        var loads = new List<WallSketchLoad>();
        double? lg = null, rg = null, lw = null, rw = null, lq = null, rq = null;
        if (stage is not null)
        {
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
            foreach (var activation in tables.Single(t => t.Key == "activations").Rows.Where(Belongs))
            {
                var definitions = tables.Single(t => t.Key == "supports").Rows.Where(r => r["id"] == activation["support"]).ToArray();
                if (definitions.Length != 1)
                {
                    notices.Add(activation["support"] + ": sostegno assente o identificativo duplicato.");
                    continue;
                }
                var row = definitions[0];
                var z = Number(row.Data, "elevation", row["id"] + " / quota");
                var angle = Number(row.Data, "angle", row["id"] + " / inclinazione");
                var direction = Number(row.Data, "direction", row["id"] + " / verso");
                var prestress = Number(activation.Data, "prestress", row["id"] + " / pretensione", true);
                var lockOff = lockOffs.FirstOrDefault(l => ReferenceEquals(l.Row, activation.Data));
                if (lockOff is not null) shown.Add(lockOff);
                if (z is null || angle is null || direction is null) continue;
                if (direction is not (1 or -1) || Math.Abs(angle.Value) >= 90 ||
                    row["kind"] is not ("Anchor" or "Strut" or "Bilateral"))
                { notices.Add(row["id"] + ": tipo, verso o inclinazione non validi."); continue; }
                supports.Add(new(row, activation, row["id"], row["kind"], z.Value, angle.Value, (int)direction, lockOff?.Prestress ?? prestress));
            }
            if (EmbeddedWallDocument.ReducedLockOffs(shown) is { } reduced) notices.Add(reduced);
            foreach (var row in tables.Single(t => t.Key == "loads").Rows.Where(Belongs))
            {
                var z = Number(row.Data, "elevation", "Carico / quota");
                var h = Number(row.Data, "horizontal", "Carico orizzontale");
                var v = Number(row.Data, "downward", "Carico verticale");
                var m = Number(row.Data, "moment", "Momento applicato");
                if (z is not null) loads.Add(new(row, z.Value, z.Value, h, h, v, m));
            }
            foreach (var row in tables.Single(t => t.Key == "distributed_loads").Rows.Where(Belongs))
            {
                var top = Number(row.Data, "top", row["id"] + " / quota superiore");
                var bottom = Number(row.Data, "bottom", row["id"] + " / quota inferiore");
                var qt = Number(row.Data, "q_top", row["id"] + " / q superiore");
                var qb = Number(row.Data, "q_bottom", row["id"] + " / q inferiore");
                if (top > bottom) loads.Add(new(row, top.Value, bottom!.Value, qt, qb, null, null));
                else notices.Add(row["id"] + ": quote del carico distribuito mancanti o non ordinate.");
            }
            var uniform = Number(stage.Data, "distributed_load", "Carico uniforme sulla parete");
            if (uniform is not null && uniform != 0)
                notices.Add("Carico uniforme sulla parete: " + Format(uniform) + " kN/m (intera altezza).");
        }
        else notices.Add("Aggiungere una fase a questo caso per vedere terreno, falda e sostegni.");
        var wallTop = Number(data, "top", "Testa paratia");
        var wallBottom = Number(data, "bottom", "Piede paratia");
        if (wallTop <= wallBottom) { notices.Add("Testa e piede non ordinati: parete non rappresentata."); wallTop = wallBottom = null; }
        return new(wallTop, wallBottom, lg, rg, lw, rw, lq, rq,
            data["hydraulic_mode"]?.ToString() == "FEM 2D" ? "Carico idraulico assegnato" : "Falda assegnata",
            stage?["id"] ?? "Nessuna fase", stage, bands, supports, loads, notices.Distinct().ToArray());
    }
    public static string Format(double? value) => value?.ToString("0.###", CultureInfo.CurrentCulture) ?? "—";
}

public sealed record WallSketchBand(string Table, EmbeddedWallRow Row, string Name, double Top, double Bottom);
public sealed record WallSketchSupport(EmbeddedWallRow Row, EmbeddedWallRow Activation, string Id, string Kind,
    double Elevation, double Angle, int Direction, double? Prestress);
public sealed record WallSketchLoad(EmbeddedWallRow Row, double Top, double Bottom,
    double? HorizontalTop, double? HorizontalBottom, double? Downward, double? Moment);
public sealed record WallSketchPick(string Table, EmbeddedWallRow? Row);
