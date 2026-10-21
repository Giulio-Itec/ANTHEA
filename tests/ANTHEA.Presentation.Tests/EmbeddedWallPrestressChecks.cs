using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Anthea.Application.EmbeddedWalls;
using Anthea.Application.Modules;
using Anthea.Reports;
using GPC.Model.Geotechnics.EmbeddedWalls;

namespace Anthea.Presentation.Tests;

/// <summary>Units of the adapter and lock-off between stages (review of 21/10/2026, finding A1). Expected values are independent:
/// Checker branch claude/verifica-paratie, tools/attesi/paratie_revisione/anthea (cases.py, prestress.py), exact rational arithmetic
/// on the cantilever of <see cref="EmbeddedWallChecks.Example"/>: L 6000 mm, EI 2e14 Nmm², 3EI/L³ 2777.78 N/mm, H 10 kN at the head,
/// head anchor EA/L 10000 kN/m. Never engine output. Every compared value goes to paratie/pretensione-esiti.csv.</summary>
internal static partial class EmbeddedWallChecks
{
    private static async Task PrestressAsync(Expect e, string output)
    {
        var log = new Ledger(e);
        ConversionChecks(e, log);
        HeadAnchorChecks(log);
        StageLockOffChecks(e, log);
        await EditingLockOffAsync(e, log);
        string dir = Path.Combine(output, "paratie"); Directory.CreateDirectory(dir);
        log.Write(Path.Combine(dir, "pretensione-esiti.csv"));
    }

    /// <summary>Records expected, obtained and difference of each compared value, then checks it.</summary>
    private sealed class Ledger(Expect e)
    {
        private readonly List<string> rows = ["prova;grandezza;unità;atteso;ottenuto;scarto;tolleranza"];
        private static string F(double? v) => v?.ToString("R", CultureInfo.InvariantCulture) ?? "";
        /// <summary>Tolerance null: same value within 1e-15 relative (unit conversions).</summary>
        internal void Check(string test, string quantity, string unit, double? expected, double? actual, double? tolerance = null)
        {
            rows.Add(string.Join(';', test, quantity, unit, F(expected), F(actual), F(actual - expected), tolerance is null ? "1e-15 relativa" : F(tolerance)));
            bool same = expected is null ? actual is null : actual is double a && (tolerance is double t ? Math.Abs(a - expected.Value) <= t
                : a == expected || Math.Abs(a - expected.Value) <= 1e-15 * Math.Abs(expected.Value));
            e.That(same, $"{test}, {quantity}: atteso {F(expected)} {unit}, ottenuto {F(actual)}.");
        }
        internal void Write(string path) => File.WriteAllLines(path, rows, new UTF8Encoding(true));
    }

    private static EmbeddedWallReportData Analyse(JsonObject data) => new EmbeddedWallModule().Calculate(data).NativeAs<EmbeddedWallReportData>();
    private static double Head(EmbeddedWallStageData stage) => stage.Points[0].Displacement;
    private static double Tie(EmbeddedWallStageData stage) => stage.Supports.Single().AxialForce;

    private static JsonObject Anchor(JsonObject data, double angle = 0, double spacing = 1)
    {
        var support = EmbeddedWallDocument.NewRow("supports"); support["elevation"] = 0; support["stiffness"] = 10000;
        support["angle"] = angle; support["spacing"] = spacing; data["supports"]!.AsArray().Add(support);
        return data;
    }

    /// <summary>Stages F1–F3 of the guide example, H 10 kN at the head in each one; activations of the head anchor T1.</summary>
    private static JsonObject AnchorHistory(params (string Stage, double? Prestress)[] activations)
    {
        var d = Anchor(Example()); d["name"] = "Tirante in testa — pretensione fra le fasi";
        var stages = d["stages"]!.AsArray(); var loads = d["loads"]!.AsArray();
        foreach (string id in new[] { "F2", "F3" })
        {
            var stage = stages[0]!.DeepClone().AsObject(); stage["id"] = id; stages.Add(stage);
            var load = loads[0]!.DeepClone().AsObject(); load["stage"] = id; loads.Add(load);
        }
        foreach (var (stage, prestress) in activations)
        {
            var row = EmbeddedWallDocument.NewRow("activations"); row["stage"] = stage; row["prestress"] = prestress;
            d["activations"]!.AsArray().Add(row);
        }
        return d;
    }

    private static void ConversionChecks(Expect e, Ledger log)
    {
        var d = Example(); d["width"] = 1.25; d["mesh"] = .25; d["segments"]![0]!["weight"] = 12.5; d["loads"]![0]!["moment"] = 3;
        var left = EmbeddedWallDocument.NewRow("left_soils"); left["name"] = "Sabbia"; left["gamma"] = 18; left["gamma_sat"] = 20; left["phi"] = 30;
        left["c"] = 5; left["k0"] = .5; left["kh"] = .002; left["source"] = "Collaudo unità"; left["stiffness_source"] = "Collaudo unità";
        var right = EmbeddedWallDocument.NewRow("right_soils"); right["name"] = "Limo"; right["gamma"] = 19; right["gamma_sat"] = 20.5; right["phi"] = 24;
        right["c"] = 10; right["cu"] = 40; right["k0"] = .6; right["kh"] = .003; right["source"] = "Collaudo unità"; right["stiffness_source"] = "Collaudo unità";
        d["left_soils"] = new JsonArray(left); d["right_soils"] = new JsonArray(right);
        var stage = d["stages"]![0]!.AsObject(); stage["left_ground"] = 0; stage["right_ground"] = -2; stage["left_water"] = -1;
        stage["left_surcharge"] = 10; stage["distributed_load"] = 1.5; stage["stiffness_factor"] = .8;
        var q = EmbeddedWallDocument.NewRow("distributed_loads"); q["top"] = -1; q["bottom"] = -4.5; q["q_top"] = 1; q["q_bottom"] = 4; q["source"] = "Collaudo unità";
        d["distributed_loads"]!.AsArray().Add(q);
        foreach (var (id, kind, z, angle, direction, spacing, capacity, prestress) in new (string, string, double, double, int, double, double?, double?)[] {
            ("T1", "Anchor", -1, 20, 1, 2.5, 500, 50), ("T2", "Strut", -2, 0, -1, 1, null, null),
            ("T3", "Bilateral", -.5, -10, 1, 3, 250.5, 12.5), ("T4", "Anchor", -3.25, 30, 1, 1.8, 600, 0) })
        {
            var s = EmbeddedWallDocument.NewRow("supports"); s["id"] = id; s["kind"] = kind; s["elevation"] = z; s["stiffness"] = 10000;
            s["angle"] = angle; s["direction"] = direction; s["spacing"] = spacing; s["capacity"] = capacity; d["supports"]!.AsArray().Add(s);
            var a = EmbeddedWallDocument.NewRow("activations"); a["support"] = id; a["prestress"] = prestress; d["activations"]!.AsArray().Add(a);
        }
        var m = EmbeddedWallAdapter.ToModel(d);
        const string T = "C1 conversioni ToModel";
        log.Check(T, "testa 0 m", "mm", 0, m.Top); log.Check(T, "piede −6 m", "mm", -6000, m.Bottom);
        log.Check(T, "larghezza fascia 1,25 m", "mm", 1250, m.TributaryWidth); log.Check(T, "lunghezza elementi 0,25 m", "mm", 250, m.MaximumElementLength);
        var w = m.Segments.Single();
        log.Check(T, "EI 200 000 kNm²", "Nmm²", 2e14, w.FlexuralRigidity); log.Check(T, "peso 12,5 kN/m", "N/mm", 12.5, w.WeightPerLength);
        log.Check(T, "tratto quota inferiore −6 m", "mm", -6000, w.Bottom);
        var sl = m.LeftSoils.Single(); var sr = m.RightSoils.Single();
        log.Check(T, "γ sinistra 18 kN/m³", "N/mm³", 1.8e-5, sl.Soil.UnitWeight); log.Check(T, "γsat sinistra 20 kN/m³", "N/mm³", 2e-5, sl.Soil.SaturatedUnitWeight);
        log.Check(T, "γ destra 19 kN/m³", "N/mm³", 1.9e-5, sr.Soil.UnitWeight); log.Check(T, "γsat destra 20,5 kN/m³", "N/mm³", 2.05e-5, sr.Soil.SaturatedUnitWeight);
        log.Check(T, "φ' 30°", "rad", 0.5235987755982988, sl.Soil.FrictionAngle); log.Check(T, "φ' 24°", "rad", 0.4188790204786391, sr.Soil.FrictionAngle);
        log.Check(T, "c' 5 kPa", "MPa", .005, sl.Soil.EffectiveCohesion); log.Check(T, "c' 10 kPa", "MPa", .01, sr.Soil.EffectiveCohesion);
        log.Check(T, "cu destra 40 kPa", "MPa", .04, sr.Soil.UndrainedShearStrength); log.Check(T, "cu sinistra vuota", "MPa", null, sl.Soil.UndrainedShearStrength);
        log.Check(T, "strato quota inferiore −20 m", "mm", -20000, sl.Bottom);
        log.Check(T, "kh 0,002 N/mm³ (senza conversione)", "N/mm³", .002, sl.HorizontalModulus); log.Check(T, "kh 0,003 N/mm³", "N/mm³", .003, sr.HorizontalModulus);
        log.Check(T, "K0 0,5", "", .5, sl.AtRestCoefficient); log.Check(T, "K0 0,6", "", .6, sr.AtRestCoefficient);
        var f1 = m.Stages.Single();
        log.Check(T, "terreno sinistra 0 m", "mm", 0, f1.Left.Elevation); log.Check(T, "terreno destra −2 m", "mm", -2000, f1.Right.Elevation);
        log.Check(T, "falda sinistra −1 m", "mm", -1000, f1.Left.WaterElevation); log.Check(T, "falda destra assente", "mm", null, f1.Right.WaterElevation);
        log.Check(T, "sovraccarico sinistra 10 kPa", "MPa", .01, f1.Left.Surcharge); log.Check(T, "sovraccarico destra 0 kPa", "MPa", 0, f1.Right.Surcharge);
        log.Check(T, "carico uniforme 1,5 kN/m", "N/mm", 1.5, f1.DistributedLoad); log.Check(T, "moltiplicatore EI 0,8", "", .8, f1.StiffnessFactor);
        var p = f1.PointLoads.Single();
        log.Check(T, "forza orizzontale 10 kN", "N", 1e4, p.HorizontalForce); log.Check(T, "coppia 3 kNm", "Nmm", 3e6, p.Moment);
        log.Check(T, "forza verso il basso 100 kN", "N", 1e5, p.DownwardForce); log.Check(T, "quota del carico 0 m", "mm", 0, p.Elevation);
        var dl = f1.DistributedLoads.Single();
        log.Check(T, "carico distribuito da −1 m", "mm", -1000, dl.Top); log.Check(T, "carico distribuito a −4,5 m", "mm", -4500, dl.Bottom);
        log.Check(T, "q superiore 1 kN/m", "N/mm", 1, dl.TopLoad); log.Check(T, "q inferiore 4 kN/m", "N/mm", 4, dl.BottomLoad);
        EmbeddedWallSupport S(string id) => m.Supports.Single(s => s.Id == id);
        foreach (var (id, z, angle, spacing, capacity) in new (string, double, double, double, double?)[] { ("T1", -1000, 0.3490658503988659, 2500, 5e5),
            ("T2", -2000, 0, 1000, null), ("T3", -500, -0.17453292519943295, 3000, 250500), ("T4", -3250, 0.5235987755982988, 1800, 6e5) })
        {
            log.Check(T, id + " quota", "mm", z, S(id).Elevation); log.Check(T, id + " inclinazione", "rad", angle, S(id).Inclination);
            log.Check(T, id + " interasse", "mm", spacing, S(id).Spacing); log.Check(T, id + " capacità", "N", capacity, S(id).Capacity);
            log.Check(T, id + " EA/L 10 000 kN/m", "N/mm", 10000, S(id).AxialStiffness);
        }
        e.That(S("T1").Kind == EmbeddedWallSupportKind.Anchor && S("T2").Kind == EmbeddedWallSupportKind.Strut && S("T3").Kind == EmbeddedWallSupportKind.Bilateral
            && S("T2").ResistingDirection == -1 && S("T1").ResistingDirection == 1, "Tipo o verso resistente dei sostegni alterati.");
        double Lock(string id) => f1.ActiveSupports.Single(a => a.SupportId == id).Prestress;
        log.Check(T, "T1 pretensione 50 kN", "N", 5e4, Lock("T1")); log.Check(T, "T2 pretensione vuota all'installazione", "N", 0, Lock("T2"));
        log.Check(T, "T3 pretensione 12,5 kN", "N", 12500, Lock("T3")); log.Check(T, "T4 pretensione 0 kN", "N", 0, Lock("T4"));
    }

    private static void HeadAnchorChecks(Ledger log)
    {
        // u = H/(3EI/L³ + EA/L·m·cos²α), F = EA/L·cos α·u per element, m = b/s.
        foreach (var (test, angle, spacing, u, force) in new[] { ("C2 tirante in testa α 0°, s 1 m", 0d, 1d, 0.782608695652174, 7.826086956521739),
            ("C3 tirante in testa, interasse 2,5 m", 0d, 2.5, 1.4754098360655739, 14.754098360655737), ("C4 tirante in testa inclinato 30°", 30d, 1d, 0.972972972972973, 8.426193117902645) })
        {
            var d = Anchor(Example(), angle, spacing); d["activations"]!.AsArray().Add(EmbeddedWallDocument.NewRow("activations"));
            var stage = Analyse(d).Stages.Single();
            log.Check(test, "spostamento in testa", "mm", u, Head(stage), 1e-9); log.Check(test, "forza nel tirante", "kN", force, Tie(stage), 1e-9);
        }
    }

    private static void StageLockOffChecks(Expect e, Ledger log)
    {
        const double u1 = 3.6, u2 = -0.3130434782608696, f2 = 10.869565217391305, tol = 1e-9;
        var kept = AnchorHistory(("F2", 50), ("F3", null)); var keptResult = Analyse(kept); var s = keptResult.Stages;
        e.That(s.Count == 3 && s[0].Supports.Count == 0, "Esempio della guida: storia incompleta o tirante attivo in F1.");
        const string G = "C5 esempio della guida, F3 con cella vuota";
        log.Check(G, "F1 spostamento in testa", "mm", u1, Head(s[0]), tol);
        log.Check(G, "F2 spostamento in testa", "mm", u2, Head(s[1]), tol); log.Check(G, "F2 forza nel tirante", "kN", f2, Tie(s[1]), tol);
        log.Check(G, "F3 spostamento in testa", "mm", u2, Head(s[2]), tol); log.Check(G, "F3 forza nel tirante", "kN", f2, Tie(s[2]), tol);
        e.That(!keptResult.Scope.Any(t => t.Contains("Pretensione ridotta")), "Avviso di riduzione senza riduzione.");
        var lockOffs = EmbeddedWallDocument.LockOffs(kept, null);
        e.That(lockOffs.Count == 2 && lockOffs[0].Previous is null && lockOffs[1].Stage == "F3" && !lockOffs[1].Reduced, "Pretensione mantenuta non risolta dal documento.");
        log.Check(G, "F3 pretensione risolta dal documento", "kN", 50, lockOffs[1].Prestress); log.Check(G, "F3 pretensione della fase precedente", "kN", 50, lockOffs[1].Previous);
        var explicitResult = Analyse(AnchorHistory(("F2", 50), ("F3", 50)));
        e.That(explicitResult.Stages[2].Points.SequenceEqual(s[2].Points) && explicitResult.Stages[2].Supports.SequenceEqual(s[2].Supports),
            "Cella vuota e valore ripetuto devono dare la stessa storia.");
        log.Check("C6 F3 con 50 kN ripetuti", "F3 forza nel tirante, uguale alla cella vuota", "kN", Tie(s[2]), Tie(explicitResult.Stages[2]), 0);
        var released = Analyse(AnchorHistory(("F2", 50), ("F3", 0)));
        log.Check("C7 rilascio esplicito con 0", "F3 spostamento in testa", "mm", u1, Head(released.Stages[2]), tol);
        log.Check("C7 rilascio esplicito con 0", "F3 forza nel tirante", "kN", 0, Tie(released.Stages[2]), tol);
        e.That(released.Scope.Count(t => t.Contains("Pretensione ridotta rispetto alla fase precedente: T1 da 50 a 0 kN nella fase F3")) == 1,
            "Riduzione della pretensione non segnalata nei risultati.");
        var retensioned = Analyse(AnchorHistory(("F2", 50), ("F3", 80)));
        log.Check("C8 ritesatura a 80 kN", "F3 spostamento in testa", "mm", -2.6608695652173915, Head(retensioned.Stages[2]), tol);
        log.Check("C8 ritesatura a 80 kN", "F3 forza nel tirante", "kN", 17.391304347826086, Tie(retensioned.Stages[2]), tol);
        e.That(!retensioned.Scope.Any(t => t.Contains("Pretensione ridotta")), "Ritesatura segnalata come riduzione.");
        // Removed in F2 and listed again in F3: a new installation on the deformed wall, without the old lock-off.
        var reinstalled = AnchorHistory(("F1", 50), ("F3", null)); var r = Analyse(reinstalled).Stages;
        const string R = "C9 rimozione in F2 e reinstallazione in F3";
        log.Check(R, "F1 spostamento in testa", "mm", -3.130434782608696, Head(r[0]), tol); log.Check(R, "F1 forza nel tirante", "kN", 18.695652173913043, Tie(r[0]), tol);
        log.Check(R, "F2 spostamento in testa", "mm", u1, Head(r[1]), tol);
        log.Check(R, "F3 spostamento in testa", "mm", u1, Head(r[2]), tol); log.Check(R, "F3 forza nel tirante", "kN", 0, Tie(r[2]), tol);
        e.That(r[1].Supports.Count == 0 && EmbeddedWallDocument.LockOffs(reinstalled, null)[1].Previous is null,
            "Reinstallazione dopo la rimozione: la pretensione vuota deve valere 0.");
    }

    private static async Task EditingLockOffAsync(Expect e, Ledger log)
    {
        var data = AnchorHistory(("F2", 50)); string principal = data["case_id"]!.ToString();
        var row = EmbeddedWallDocument.NewEditingRow(data, "activations", principal, "F3");
        e.That(row["stage"]?.ToString() == "F3" && row["case"]?.ToString() == "" && row["support"]?.ToString() == "T1" && row["prestress"] is null,
            "Riga di attivazione nuova: fase, caso o pretensione vuota non assegnati.");
        e.That(EmbeddedWallDocument.NewEditingRow(data, "activations", "C2", "F1")["case"]?.ToString() == "C2", "Riga nuova non assegnata al caso visibile.");
        using var sheet = Open(data);
        var activations = sheet.Tables.Single(t => t.Key == "activations");
        e.That(sheet.InputStage?["id"] == "F3" && sheet.Sketch!.Supports.Count == 0, "La fase F3 non parte senza sostegni.");
        activations.AddCommand.Execute(null); var added = activations.Selected!;
        const string U = "C10 riga aggiunta dall'editor in F3";
        e.That(added["stage"] == "F3" && added["prestress"] == "" && !sheet.SketchStatus.Contains("ridotta"), "Riga aggiunta nell'editor: fase o pretensione vuota errate.");
        log.Check(U, "pretensione mostrata nel disegno", "kN", 50, sheet.Sketch.Supports.Single().Prestress);
        e.That(WorkNodes(sheet.WorkTree).Any(n => ReferenceEquals(n.Row, added) && n.Title == "T1 · pretensione mantenuta"), "Albero: pretensione mantenuta non indicata.");
        e.That(await Expect.Within(sheet.CalculateAsync()), "Calcolo dall'editor non pubblicato.");
        log.Check(U, "F3 forza nel tirante", "kN", 10.869565217391305, Tie(sheet.Current!.Stages[2]), 1e-9);
        added["prestress"] = "0";
        e.That(sheet.Current is null && sheet.SketchStatus.Contains("T1 da 50 a 0 kN nella fase F3"), "Disegno: riduzione della pretensione non segnalata.");
        log.Check("C11 stessa riga portata a 0 kN", "pretensione mostrata nel disegno", "kN", 0, sheet.Sketch.Supports.Single().Prestress);
        e.That(await Expect.Within(sheet.CalculateAsync()) && sheet.Scope.Contains("T1 da 50 a 0 kN nella fase F3"), "Risultati: rilascio esplicito non segnalato.");
        log.Check("C11 stessa riga portata a 0 kN", "F3 forza nel tirante", "kN", 0, Tie(sheet.Current!.Stages[2]), 1e-9);
    }
}
