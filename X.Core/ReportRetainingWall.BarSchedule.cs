using System.Text.Json.Nodes;

namespace X.Core;

public static partial class ReportRetainingWall
{
    public static string BarScheduleCsv(RetainingWall.BarSchedule schedule)
    {
        string Row(IEnumerable<string> values) => string.Join(";", values.Select(s => "\"" + s.Replace("\"", "\"\"") + "\""));
        return string.Join("\r\n", new[] { Row(["Marca", "Zona", "Faccia", "Tipo", "Diametro mm", "Pezzi", "L pezzo m", "L totale m", "Peso kg", "Passo mm", "Mandrino mm", "Sovrapposizione mm", "Tratti all'asse mm", "Note", "Tratto muro m", "Copriferro estremità mm", "Barra commerciale m"]) }
            .Concat(schedule.Bars.Select(b => Row([b.Mark, RetainingWall.RebarZoneName(b.Zone), b.Face, b.Kind, F(b.Diameter), b.Quantity.ToString(), b.CutLength is double l ? F(l) : "da definire", b.TotalLength is double total ? F(total) : "", b.Weight is double weight ? F(weight) : "", F(b.Spacing), F(b.Mandrel), F(b.Lap), string.Join(" / ", b.Legs.Select(l => l.Name + "=" + F(l.Length * 1000))), b.Note, F(schedule.PanelLength), F(schedule.EndCover), F(schedule.StockLength)])))
            .Concat(schedule.Warnings.Select(w => Row(["NOTA", w])))) + "\r\n";
    }
    public static byte[] CreateBarSchedule(JsonObject input, RetainingWall.BarSchedule schedule, IReadOnlyList<Figure>? figures = null)
    {
        var doc = new WallDocument(); doc.P("Distinta ferri del muro a mensola", true);
        ScheduleTables(doc, input, schedule);
        if (figures is not null) foreach (var f in figures) { doc.PageBreak(); doc.Image(f); }
        return doc.Bytes();
    }
    private static void ScheduleTables(WallDocument doc, JsonObject input, RetainingWall.BarSchedule schedule)
    {
        doc.P("Distinta riferita alle armature inserite e al tratto di muro di " + F(schedule.PanelLength) + " m. Quote all’asse; peso netto senza sfridi. Il calcolo strutturale resta riferito a 1 m di muro.");
        doc.P($"CLS: {input["materials"].S("materiale_cls_nome")}; acciaio: {input["materials"].S("materiale_acciaio_nome")}. Copriferro sezione {input["materials"].S("cover")} mm; estremità del tratto {F(schedule.EndCover)} mm. Barra commerciale {F(schedule.StockLength)} m.");
        doc.Table(["Marca / zona / faccia", "Ø [mm] / pezzi", "L pezzo [m]", "L totale [m]", "Peso [kg]"], schedule.Bars.Select(b => new[] { b.Mark + " / " + RetainingWall.RebarZoneName(b.Zone) + " / " + b.Face, F(b.Diameter) + " / " + b.Quantity, b.CutLength is double l ? F(l) : "da definire", b.TotalLength is double total ? F(total) : "—", b.Weight is double kg ? F(kg) : "—" }), [3.2, 1.2, 1.1, 1.1, 1.1]);
        doc.P((schedule.CompleteQuantities ? "Peso delle barre definite" : "Peso parziale delle barre definite") + ": " + F(schedule.KnownWeight) + " kg.", true);
        doc.Table(["Marca", "Tratti all’asse [mm]", "Mandrino / l₀ [mm]", "Passo [mm] / note"], schedule.Bars.Select(b => new[] { b.Mark, string.Join("; ", b.Legs.Select(l => l.Name + " = " + F(l.Length * 1000))), F(b.Mandrel) + " / " + F(b.Lap), F(b.Spacing) + "\n" + b.Note }), [1, 2.4, 1.5, 3]);
        foreach (var warning in schedule.Warnings) doc.P(warning);
    }
}
