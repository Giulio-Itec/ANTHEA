using System.Text.Json.Nodes;

namespace X.Core;

public static partial class ReportRetainingWall
{
    static string Cm(double metres) => F(Math.Round(metres * 100));
    public static string BarScheduleCsv(RetainingWall.BarSchedule schedule)
    {
        string Row(IEnumerable<string> values) => string.Join(";", values.Select(s => "\"" + s.Replace("\"", "\"\"") + "\""));
        return string.Join("\r\n", new[] { Row(["Pos.", "Posizione", "Zona", "Faccia", "Tipo", "Diametro mm", "n°", "L unitaria cm", "L totale m", "Peso kg", "Passo mm", "Mandrino mm", "Sovrapposizione mm", "Fili", "Pezzi per filo", "Sagoma: tratti all'asse cm", "Note", "Tratto muro m", "Copriferro estremità mm", "Barra commerciale m"]) }
            .Concat(schedule.Bars.Select(b => Row([b.Mark, RetainingWall.BarPositionName(b), RetainingWall.RebarZoneName(b.Zone), b.Face, b.Kind, F(b.Diameter), b.Quantity.ToString(), b.CutLength is double l ? Cm(l) : "", b.TotalLength is double total ? F(total) : "", b.Weight is double weight ? F(weight) : "", F(b.Spacing), F(b.Mandrel), F(b.Lap), b.Lines?.ToString() ?? "", b.Pieces?.ToString() ?? "", RetainingWall.BarShapeText(b), b.Note, F(schedule.PanelLength), F(schedule.EndCover), F(schedule.StockLength)])))
            .Concat([Row(["RIEPILOGO", "Diametro mm", "L totale m", "kg/m", "Peso kg"])])
            .Concat(schedule.Diameters.Select(d => Row(["Ø" + F(d.Diameter), F(d.Diameter), F(d.Length), F(d.KgPerMetre), F(d.Weight)])))
            .Concat([Row(["TOTALE", "", "", "", F(schedule.KnownWeight)]), Row(["CALCESTRUZZO m³", F(schedule.ConcreteVolume)]), Row(["INCIDENZA kg/m³", F(schedule.Incidence)])])
            .Concat(schedule.Warnings.Select(w => Row(["NOTA", w])))) + "\r\n";
    }
    public static byte[] CreateBarSchedule(JsonObject input, RetainingWall.BarSchedule schedule, IReadOnlyList<Figure>? figures = null) => DocxWriter.Write(CreateBarScheduleDocument(input, schedule, figures));
    public static DocxDocument CreateBarScheduleDocument(JsonObject input, RetainingWall.BarSchedule schedule, IReadOnlyList<Figure>? figures = null)
    {
        var doc = new WallDocument(); doc.P("Distinta ferri del muro a mensola", true);
        ScheduleTables(doc, input, schedule);
        if (figures is not null) foreach (var f in figures) { doc.PageBreak(); doc.Image(f); }
        return doc.BuildDocument();
    }
    private static void ScheduleTables(WallDocument doc, JsonObject input, RetainingWall.BarSchedule schedule)
    {
        doc.P("Tavola delle armature del tratto di muro di " + F(schedule.PanelLength) + " m. Quote delle sagome in cm all’asse delle barre con gli archi sviluppati; L unitaria = somma dei tratti. Peso netto, senza sfridi. Il calcolo strutturale resta riferito a 1 m di muro.");
        doc.P($"CLS: {input["materials"].S("materiale_cls_nome")}; acciaio: {input["materials"].S("materiale_acciaio_nome")}. Copriferro sezione {input["materials"].S("cover")} mm; estremità del tratto {F(schedule.EndCover)} mm. Barra commerciale {F(schedule.StockLength)} m.");
        doc.Table(["Pos.", "Posizione", "Sagoma: tratti all’asse [cm]", "Ø [mm]", "n°", "L unitaria [cm]", "L totale [m]", "Peso [kg]"],
            schedule.Bars.Select(b => new[] { b.Mark, RetainingWall.BarPositionName(b) + (b.Note.Length > 0 ? "\n" + b.Note : ""), RetainingWall.BarShapeText(b), F(b.Diameter), b.Quantity.ToString(), b.CutLength is double l ? Cm(l) : "—", b.TotalLength is double total ? F(total) : "—", b.Weight is double kg ? F(kg) : "—" }),
            [.6, 2.4, 2.4, .7, .7, 1, 1, 1]);
        doc.P("Riepilogo per diametro", true);
        doc.Table(["Ø [mm]", "L totale [m]", "kg/m", "Peso [kg]"], schedule.Diameters.Select(d => new[] { F(d.Diameter), F(d.Length), F(d.KgPerMetre), F(d.Weight) }), [1, 1.4, 1.2, 1.4]);
        doc.P($"Peso totale: {F(schedule.KnownWeight)} kg. Calcestruzzo del tratto: {F(schedule.ConcreteVolume)} m³. Incidenza: {F(schedule.Incidence)} kg/m³.", true);
        foreach (var warning in schedule.Warnings) doc.P(warning);
    }
}
