using Anthea.Calculations.Geotechnics;
using System.Text.Json.Nodes;

namespace X.Core;

public static partial class ReportRetainingWall
{
    private static void WriteGlobal(WallDocument doc, RetainingWall.Result wall) => WriteGlobal(doc, wall.Input, wall.GlobalStability, wall.GlobalError);
    public static byte[] CreateGlobal(JsonObject input, SlopeResult result, Figure? figure = null)
    {
        var doc = new WallDocument(); doc.P("ANTHEA Stabilità globale del muro", true);
        doc.P("Analisi del complesso muro terreno con il metodo di Bishop semplificato. Questa relazione riguarda soltanto la stabilità globale.");
        if (figure is not null) doc.Image(figure);
        WriteGlobal(doc, input, result, null); return doc.Bytes();
    }
    private static void WriteGlobal(WallDocument doc, JsonObject input, SlopeResult? analysis, string? error)
    {
        doc.P("Stabilità globale del complesso muro–terreno", true);
        if (analysis is not { } result) { doc.P(error ?? "Analisi non attivata. Nessun esito di stabilità globale disponibile."); return; }
        doc.P(RetainingWall.GlobalHelp);
        foreach (var note in result.Notes.Where(n => n != RetainingWall.GlobalHelp)) doc.P(note);
        doc.P("Modello della sezione globale · coordinate in metri dal piede di valle", true);
        doc.Table(["Punto profilo", "x [m]", "y [m]"], result.Section.Surface.Select((p, i) => new[] { (i + 1).ToString(), F(p.X), F(p.Y) }), [2, 1, 1]);
        doc.Table(["Terreno", "Fondo y [m]", "γ / γsat [kN/m³]", "φ′k [°]", "c′ / cu [kPa]"], result.Section.Soils.Select(s => new[] { s.Name, F(s.Bottom), F(s.Gamma) + " / " + F(s.GammaSat), F(s.Phi), F(s.Cohesion) + " / " + F(s.Cu) }), [2, 1, 1.5, 1, 1.2]);
        if (result.Section.Water.Length > 0) doc.Table(["Punto falda", "x [m]", "y [m]"], result.Section.Water.Select((p, i) => new[] { (i + 1).ToString(), F(p.X), F(p.Y) }), [2, 1, 1]); else doc.P("Falda globale assente.");
        var q = result.Search;
        doc.P($"Ricerca: uscita x={F(q.ExitMin)}…{F(q.ExitMax)} m; ingresso x={F(q.EntryMin)}…{F(q.EntryMax)} m; profondità sotto y=0: {F(q.DepthMin)}…{F(q.DepthMax)} m. Griglia {q.Grid}³, {q.Refinements} raffinamenti, {q.Slices} conci iniziali e controllo a {2 * q.Slices} sulla superficie minima.");
        doc.Table(["Combinazione", "γterra / γmuro", "γMφ / γMc / γMcu", "γR", "kh / kv", "Condizione"], result.Cases.Select(c => new[] { c.Factors.Name, F(c.Factors.Soil) + " / " + F(c.Factors.Body), F(c.Factors.MPhi) + " / " + F(c.Factors.MC) + " / " + F(c.Factors.MCu), F(c.Factors.R), F(c.Factors.Kh) + " / " + F(c.Factors.Kv), c.Factors.Undrained ? "Non drenata" : "Drenata" }), [2.5, 1.2, 1.4, .7, 1.2, 1]);
        doc.Table(["Azione", "x₀ / x₁ / y [m]", "V / H / M", "Distribuzione"], result.Section.Loads.Select(l => new[] { input.Array("actions").FirstOrDefault(a => a.S("id") == l.Id).S("name", l.Id), F(l.Left) + " / " + F(l.Right) + " / " + F(l.Y), F(l.Vertical) + " / " + F(l.Horizontal) + " / " + F(l.Moment), l.Distributed ? "Uniforme · kPa" : "Concentrata · kN/m, kNm/m" }), [2.2, 1.8, 1.4, 2]);
        doc.Table(["Combinazione", "Azione", "γ × ψ"], result.Cases.SelectMany(c => c.Factors.Loads.Select(a => new[] { c.Factors.Name, input.Array("actions").FirstOrDefault(x => x.S("id") == a.Key).S("name", a.Key), F(a.Value) })), [2, 3, 1]);
        doc.Table(["Caso", "F / γR", "η", "Risolte / provate", "Esito"], result.Cases.Select(c => new[] { c.Factors.Name, (c.Critical is { } s ? F(s.Factor) : "—") + " / " + F(c.Factors.R), c.Critical is { } q ? F(q.Ratio) : "—", $"{c.Solved}/{c.Tried}", c.Status }), [2, 1, .7, 1, 3]);
        foreach (var c in result.Cases.Where(c => c.Critical is not null))
        {
            var s = c.Critical!; var circle = s.Circle;
            doc.P(c.Factors.Name + " · superficie minima", true);
            doc.P($"Centro ({F(circle.X)}; {F(circle.Y)}) m; raggio {F(circle.Radius)} m; estremi x={F(circle.Left)} / {F(circle.Right)} m. D={F(s.Driving)} kN/m, ΣR={F(s.Resistance)} kN/m, iterazioni {s.Iterations}, residuo {s.Residual:G4}. Superfici geometricamente ammesse {c.GeometricallyValid}; senza soluzione {c.NumericalFailures}. {c.Status}");
        }
        // Full slice detail for the controlling case; all cases are retained in JSON/CSV.
        if (result.Cases.Where(c => c.Critical is not null).OrderByDescending(c => c.Critical!.Ratio).FirstOrDefault() is { } worst)
        {
            doc.P("Conci della combinazione governante · " + worst.Factors.Name, true);
            doc.Table(["n / terreno", "x₀ / x₁ [m]", "α [°]", "W terra / muro [kN/m]", "u [kPa]", "N′ [kN/m]", "R / T [kN/m]"], worst.Critical!.Slices.Select(s => new[] { s.Index + " · " + s.Soil, F(s.Left) + " / " + F(s.Right), F(s.Alpha), F(s.SoilWeight) + " / " + F(s.BodyWeight), F(s.U), F(s.NormalEffective), F(s.Resistance) + " / " + F(s.Mobilized) }), [1.8, 1.3, .7, 1.4, .7, .9, 1.4]);
        }
        doc.P("Il valore F è calcolato con i parametri ridotti della combinazione; η=γR/F. Un minimo sul bordo, una ricerca incompleta o un equilibrio non risolto non costituiscono una verifica soddisfatta. Tutti i conci di tutte le combinazioni sono disponibili nell’export CSV globale e nei risultati JSON.");
        doc.P("Riferimenti: NTC 2018 §§6.5.3.1.1, 6.8.2, 7.11.4 e 7.11.6.2.2. Bishop A.W. (1955), The use of the slip circle in the stability analysis of slopes. USACE EM 1110-2-1902, Slope Stability, appendice C.");
    }
    public static string GlobalCsv(SlopeResult result)
    {
        string Row(IEnumerable<object> values) => string.Join(";", values.Select(v => "\"" + (v is double n ? n.ToString("G17", System.Globalization.CultureInfo.InvariantCulture) : v.ToString() ?? "").Replace("\"", "\"\"") + "\""));
        var lines = new List<string> { Row(new[] { "Caso", "Esito", "F", "gammaR", "eta", "xc", "yc", "R", "Concio", "x0", "x1", "ybase", "alpha", "terreno", "Wterra", "Wmuro", "xG", "yG", "Vext", "Hext", "u", "phi_d", "c_d", "V", "D", "N_eff", "R_resistente", "T_mobilitato", "m_alpha" }) };
        foreach (var c in result.Cases)
            if (c.Critical is not { } r) lines.Add(Row(new object[] { c.Factors.Name, c.Status }));
            else foreach (var s in r.Slices) lines.Add(Row(new object[] { c.Factors.Name, c.Status, r.Factor, c.Factors.R, r.Ratio, r.Circle.X, r.Circle.Y, r.Circle.Radius, s.Index, s.Left, s.Right, s.BaseY, s.Alpha, s.Soil, s.SoilWeight, s.BodyWeight, s.WeightX, s.WeightY, s.VerticalLoad, s.HorizontalLoad, s.U, s.Phi, s.Cohesion, s.Vertical, s.Driving, s.NormalEffective, s.Resistance, s.Mobilized, s.MAlpha }));
        return string.Join("\r\n", lines) + "\r\n";
    }
}
