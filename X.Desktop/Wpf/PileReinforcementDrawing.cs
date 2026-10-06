using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;

/// <summary>Elevation and cutting schedule projected exclusively from Checker results.</summary>
internal sealed class PileReinforcementDrawing : DrawingView
{
    JsonObject? result;
    internal string EmptyMessage { get; private set; } = "Distinta in aggiornamento · vedere avanzamento sopra i tratti.";
    internal sealed record Piece(string Mark, JsonNode Bar, JsonNode Run, int Group);
    internal IReadOnlyList<Piece> Pieces { get; private set; } = [];
    JsonNode[] segments = [], runs = [], joints = [];
    double bodyHeight = 680;
    internal Size SheetSize { get; private set; } = new(1140, 320);
    static Brush Color(int i) => Ui.Brush(new[] { "#1D4ED8", "#B91C1C", "#15803D", "#7E22CE", "#0E7490", "#A16207" }[i % 6]);

    internal void Set(JsonObject? value, string? unavailableReason = null)
    {
        result = value;
        EmptyMessage = unavailableReason ?? "Distinta in aggiornamento · vedere avanzamento sopra i tratti.";
        segments = value?["armature"]?.Array("tratti").OfType<JsonObject>().Cast<JsonNode>().ToArray() ?? [];
        runs = value?["armature"]?.Array("distinta").OfType<JsonObject>().Cast<JsonNode>().ToArray() ?? [];
        joints = value?["armature"]?.Array("giunti").OfType<JsonObject>().Cast<JsonNode>().ToArray() ?? [];
        int mark = 0;
        Pieces = runs.SelectMany((run, i) => run.Array("Pieces").OfType<JsonObject>().Select(bar => new Piece($"B{++mark:00}", bar, run, i))).ToArray();
        if (value != null && Pieces.Count == 0)
        {
            var reasons = segments.Select(t => $"{t.S("id")}: {t.S("errore", t.S("stato", "Dati del tratto non disponibili."))}").ToArray();
            EmptyMessage = "Distinta non disponibile\n" + (reasons.Length > 0 ? string.Join("\n", reasons) : value["armature"].S("stato", "Completare i dati della sezione e dei tratti."));
        }
        bodyHeight = Math.Max(680, segments.Length * 170 + 40);
        SheetSize = Pieces.Count == 0 ? new(1140, 320) : new(Math.Max(1140, 670 + Pieces.Count * 125), 190 + bodyHeight + 165 + (Pieces.Count + segments.Length) * 28 + segments.Length * 48 + runs.Length * 65 + joints.Length * 55);
        MinWidth = SheetSize.Width; Height = SheetSize.Height; InvalidateMeasure(); InvalidateVisual();
    }

    protected override void Render(DrawingContext dc, Size size)
    {
        dc.DrawRectangle(Brushes.White, null, new Rect(size));
        if (result == null || Pieces.Count == 0) { Text(dc, EmptyMessage, 18, 22, 13, width: size.Width - 36); return; }
        // Scaling the sheet never changes physical dimensions or quantities.
        double scale = Math.Min(size.Width / SheetSize.Width, size.Height / SheetSize.Height);
        dc.PushTransform(new ScaleTransform(scale, scale));
        double width = SheetSize.Width, length = result["input"].D("lunghezza"), diameter = result["input"].D("diametro") * 1000;
        double minimum = Math.Min(0, Pieces.Min(p => p.Bar.D("Start"))), maximum = Math.Max(length, Pieces.Max(p => p.Bar.D("End")));
        double top = 145, bottom = top + bodyHeight, pileX = 150, half = 35, sectionsX = width - 220;
        double Y(double x) => top + (x - minimum) / (maximum - minimum) * bodyHeight;
        var dimension = Ui.Brush("#15803D");
        var guide = new Pen(Ui.Brush("#CBD5E1"), .7) { DashStyle = DashStyles.Dash };
        Text(dc, "ARMATURE DEL PALO", 22, 16, 20, Ui.Navy, bold: true);
        Text(dc, "Elevazione · barre sviluppate lateralmente · sezioni trasversali · distinta", 22, 45, 13);
        Text(dc, "PRELIMINARE · Quote x dalla testa e lunghezze in m; diametri, passi e copriferro in mm.", 22, 70, 12, Brushes.DarkRed);
        var seismic=segments.Select(s=>s["sisma_testa"]).FirstOrDefault(s=>s.B("Active"));
        if(seismic!=null)Text(dc,$"Zona dissipativa di testa: 0–{seismic.D("HeadEnd"):0.00} m · NTC §7.2.5 · vedere gli esiti per tratto",22,88,11,Brushes.DarkOrange);
        Text(dc, "Palo e armatura trasversale", 65, 108, 13, Ui.Navy, 245, true);
        Text(dc, "S = staffe · SP = spirale", 200, 129, 10, Ui.Muted, 140);
        Text(dc, "Barre longitudinali e lunghezze di taglio", 320, 108, 13, Ui.Navy, sectionsX - 340, true);
        Text(dc, "Sezioni nominali del tratto", sectionsX, 108, 13, Ui.Navy, 205, true);
        foreach (double x in segments.Select(s => s.D("inizio")).Append(length).Distinct())
        {
            dc.DrawLine(guide, new(pileX - half - 10, Y(x)), new(sectionsX - 30, Y(x)));
            Text(dc, $"x={x:0.00}", 64, Y(x) - 15, 10, Ui.Muted, 82);
        }
        for (int i = 0; i < segments.Length; i++)
        {
            var segment = segments[i]; var section = segment["sezione"]!;
            double a = Y(segment.D("inizio")), b = Y(segment.D("fine"));
            dc.DrawRectangle(PileReinforcementEditor.SegmentColor(i), null, new Rect(pileX - half, a, half * 2, b - a));
            Dimension(dc, 291, a, b, $"{LinkMark(segment,i)} · Ø{section.D("transverse_bar_diameter_mm"):0}/ {section.D("transverse_spacing_mm"):0}", dimension);
            Text(dc, segment.S("id"), pileX - 85, (a + b) / 2 - 10, 11, Ui.Navy, 60, true);
        }
        dc.DrawRectangle(null, new Pen(Ui.Navy, 1.3), new Rect(pileX - half, Y(0), half * 2, Y(length) - Y(0)));
        if(seismic!=null)dc.DrawRectangle(null,new Pen(Brushes.DarkOrange,2){DashStyle=DashStyles.Dash},new Rect(pileX-half-4,Y(0),half*2+8,Y(seismic.D("HeadEnd"))-Y(0)));
        dc.DrawLine(new Pen(Ui.Muted, .7) { DashStyle = DashStyles.DashDot }, new(pileX, top - 15), new(pileX, bottom + 15));
        Dimension(dc, 40, Y(0), Y(length), $"L = {length:0.00} m", dimension);
        Text(dc, $"D = {diameter:0} mm", pileX - 55, bottom + 30, 12, dimension, 150);

        for (int i = 0; i < Pieces.Count; i++)
        {
            var item = Pieces[i]; var bar = item.Bar; var run = item.Run; var color = Color(item.Group);
            double x = 340 + i * 125, a = Y(bar.D("Start")), b = Y(bar.D("End"));
            foreach (var point in run.Array("Positions"))
            {
                double px = pileX + point.D("X") / diameter * (half * 2);
                dc.DrawLine(new Pen(color, .65), new(px, a), new(px, b));
            }
            foreach(var joint in run.Array("Joints").Where(j=>j.D("End")>bar.D("Start")+1e-9&&j.D("Start")<bar.D("End")-1e-9))
            {
                double startLap=Y(Math.Max(joint.D("Start"),bar.D("Start"))),endLap=Y(Math.Min(joint.D("End"),bar.D("End")));
                dc.DrawRectangle(Ui.Brush("#FFF0CF"), new Pen(Brushes.DarkOrange, .7), new Rect(x - 7, startLap, 14, endLap - startLap));
                Text(dc, $"{joint.S("Id")} · {joint.D("ActualLength"):0.00} m", x + 43, (startLap+endLap)/2, 10, Brushes.DarkOrange, 80);
            }
            var development = new Pen(Ui.Brush("#60A5FA"), 1) { DashStyle = DashStyles.Dot };
            if (bar.D("Start") < run.D("TheoreticalStart"))
                dc.DrawRectangle(null, development, new Rect(x - 5, a, 10, Math.Max(0, Y(Math.Min(bar.D("End"), run.D("TheoreticalStart"))) - a)));
            if (bar.D("End") > run.D("TheoreticalEnd"))
            {
                double ya = Y(Math.Max(bar.D("Start"), run.D("TheoreticalEnd")));
                dc.DrawRectangle(null, development, new Rect(x - 5, ya, 10, Math.Max(0, b - ya)));
            }
            dc.DrawLine(new Pen(color, 2.5), new(x, a), new(x, b));
            Text(dc, item.Mark, x - 16, a - 24, 12, color, 80, true);
            VerticalText(dc, $"{bar.D("Count"):0} Ø{bar.D("Diameter"):0}", x - 17, (a + b) / 2, color, 13);
            Dimension(dc, x + 32, a, b, $"{bar.D("CuttingLength"):0.00} m", dimension);
            Text(dc, $"x {Coordinate(bar.D("Start"))} → {Coordinate(bar.D("End"))}", x - 24, b + 8, 10, Ui.Muted, 120);
        }
        // Draw on top of longitudinal bars, also as a separate lateral detail.
        for(int i=0;i<segments.Length;i++)
        {
            var segment=segments[i];var transverse=segment["armatura_trasversale"];var shade=Color(i);
            foreach(double center in new[]{pileX,244d})
            {
                double factor=(center==pileX?2*half:46)/diameter;
                if(segment["sezione"].S("tipo_trasversale","Staffe singole")=="Spirale")
                {
                    var points=transverse.Array("Projection");
                    for(int j=1;j<points.Count;j++)
                    {
                        var point=points[j]!;var previous=points[j-1]!;
                        var pen=new Pen(shade,point.B("Front")?1.25:.65);if(!point.B("Front"))pen.DashStyle=DashStyles.Dot;
                        dc.DrawLine(pen,new(center+previous.D("X")*factor,Y(previous.D("Depth"))),new(center+point.D("X")*factor,Y(point.D("Depth"))));
                    }
                }
                else if(segment["sezione"].S("tipo_trasversale","Staffe singole")=="Staffe singole")
                {
                    double last=double.NegativeInfinity;double radius=segment.D("staffa_raggio_mm")*factor;
                    foreach(var station in segment["distinta_staffe"].Array("quote_m"))
                    {
                        double y=Y(J.Number(station)??0);if(y-last<2)continue;
                        dc.DrawEllipse(null,new Pen(shade,1.1),new(center,y),radius,1.5);last=y;
                    }
                }
                else Text(dc,"?",center-6,Y(segment.D("inizio"))+12,18,Brushes.DarkRed,20,true);
            }
        }
        // Keep section callouts readable even for very short adjacent segments.
        var centers = segments.Select(s => Y((s.D("inizio") + s.D("fine")) / 2)).ToArray();
        for (int i = 0; i < centers.Length; i++) centers[i] = Math.Max(centers[i], i == 0 ? top + 55 : centers[i - 1] + 165);
        for (int i = centers.Length - 1; i >= 0; i--) centers[i] = Math.Min(centers[i], i == centers.Length - 1 ? bottom - 55 : centers[i + 1] - 165);
        for (int i = 0; i < segments.Length; i++)
        {
            var segment = segments[i]; double c = centers[i], at = (segment.D("inizio") + segment.D("fine")) / 2;
            dc.DrawLine(guide, new(sectionsX - 24, Y(at)), new(sectionsX + 3, c));
            Section(dc, segment, new(sectionsX + 65, c), 49, i);
            var s = segment["sezione"]!;
            Text(dc, $"{segment.S("id")} · x={at:0.00} m", sectionsX + 3, c - 73, 12, Ui.Navy, 205, true);
            Text(dc, $"{s.D("longitudinal_bar_count"):0} Ø{s.D("longitudinal_bar_diameter_mm"):0}", sectionsX + 126, c - 14, 13, Ui.Navy, 85, true);
            Text(dc, $"{LinkMark(segment,i)} · {s.S("tipo_trasversale","Staffe singole")}\nØ{s.D("transverse_bar_diameter_mm"):0} / {s.D("transverse_spacing_mm"):0} mm · c = {s.D("cover_mm"):0} mm", sectionsX + 3, c + 54, 11, Ui.Muted, 210);
        }

        double tableY = bottom + 75;
        Text(dc, "DISTINTA FERRI · gruppi definiti dai tratti e dai tagli commerciali", 22, tableY, 14, Ui.Navy, width - 44, true);
        Text(dc, "Arancio: sovrapposizioni J. Ogni gruppo termina alla fine del tratto; i successivi arretrano di l₀. Staffe e sezioni nominali.", 22, tableY + 25, 11, width: width - 44);
        double rowY = tableY + 53;
        double[] columnWidths = [75, 165, 90, 100, 100, 105, 105, 110, width - 894];
        void Row(string[] cells, bool heading = false, Brush? shade = null)
        {
            double x = 22;
            for (int j = 0; j < cells.Length; j++)
            {
                dc.DrawRectangle(heading ? Ui.Brush("#E2E8F0") : shade ?? Brushes.White, new Pen(Ui.Brush("#CBD5E1"), .5), new Rect(x, rowY, columnWidths[j], 28));
                Text(dc, cells[j], x + 5, rowY + 6, 11, Ui.Navy, columnWidths[j] - 10, heading); x += columnWidths[j];
            }
            rowY += 28;
        }
        Row(["Marca", "Tratti", "Q.tà × Ø", "x inizio [m]", "x fine [m]", "L taglio [m]", "Barra [m]", "Giunti", "Tipo / passo"], true);
        foreach (var item in Pieces)
        {
            var b = item.Bar;
            string labels=string.Join(", ",item.Run.Array("Joints").Where(j=>j.D("End")>b.D("Start")+1e-9&&j.D("Start")<b.D("End")-1e-9).Select(j=>j.S("Id")));
            Row([item.Mark, item.Run.S("Segment"), $"{b.D("Count"):0} × {b.D("Diameter"):0}", $"{Coordinate(b.D("Start"))}", $"{Coordinate(b.D("End"))}", $"{b.D("CuttingLength"):0.00}", $"{b.D("StockLength"):0.0}", string.IsNullOrEmpty(labels)?"—":labels, item.Run.S("Id")+" · Longitudinale"]);
        }
        for (int i = 0; i < segments.Length; i++)
        {
            var s = segments[i]; var links = s["distinta_staffe"];
            Row([LinkMark(s,i), s.S("id"), links == null ? "—" : $"{links.D("quantita"):0} × {links.D("diametro_mm"):0}", $"{s.D("inizio"):0.00}", $"{s.D("fine"):0.00}", "da definire", "—", "—", links == null ? "Non disponibile" : $"{links.S("tipo","Staffe")} / {links.D("passo_massimo_mm"):0} mm"], shade: PileReinforcementEditor.SegmentColor(i));
        }
        rowY += 15;
        foreach(var (s,i) in segments.Select((s,i)=>(s,i)))
        {
            var l=s["distinta_staffe"];Text(dc,$"{LinkMark(s,i)} · {l.S("tipo","Staffe singole")} · φ{l.D("diametro_mm"):0} mm · passo massimo {l.D("passo_massimo_mm"):0} / effettivo {l.D("passo_effettivo_mm"):0.#} mm"+(l.S("tipo")=="Spirale"?$" · {l.D("spire"):0} spire":"")+$" · L geometrica totale {l.D("lunghezza_geometrica_m"):0.00} m\nL geometrica all'asse, esclusi ganci, chiusure, ancoraggi e giunti; taglio esecutivo da definire.",22,rowY,11,Color(i),width-44);rowY+=48;
        }
        foreach (var run in runs)
        {
            Text(dc, $"{run.S("Id")} · {run.D("Count"):0} Ø{run.D("Diameter"):0} · {run.S("StartKind")} → {run.S("EndKind")} · lbd {run.D("Anchorage"):0.00} m · aₗ {run.D("Shift"):0.00} m\n{run.S("Status")}", 22, rowY, 11, Brushes.DarkRed, width - 44); rowY += 60;
        }
        foreach(var joint in joints)
        {
            Text(dc,$"{joint.S("Id")} · {joint.S("Kind")} · {joint.S("UpperRun")} ↔ {joint.S("LowerRun")} · {joint.D("MatchedCount"):0}/{joint.D("Count"):0} coppie allineate · x {joint.D("Start"):0.00}–{joint.D("End"):0.00} m\nl₀ iniziale {joint.D("InitialLength"):0.00} / richiesta {joint.D("RequiredLength"):0.00} / adottata {joint.D("AdoptedLength"):0.00} / effettiva {joint.D("ActualLength"):0.00} m · {(joint.B("LengthPassed")&&joint.B("ArrangementPassed")?"lunghezza disponibile; disposizione da completare":"GIUNTO NON UTILIZZABILE")}",22,rowY,11,Brushes.DarkOrange,width-44);rowY+=55;
        }
        Text(dc, "Barre longitudinali rettilinee del modello. Ganci, sagomatura delle staffe, sfalsamento e confinamento dei giunti sono da completare; non sono disegnati dettagli non calcolati.", 22, rowY + 3, 11, Ui.Muted, width - 44);
        dc.Pop();
    }

    static string Coordinate(double x) => (Math.Abs(x)<.0005?0:x).ToString("0.00",CultureInfo.GetCultureInfo("it-IT"));
    static string LinkMark(JsonNode segment,int index)=>(segment["sezione"].S("tipo_trasversale")=="Spirale"?"SP":"S")+$"{index+1:00}";

    static void Dimension(DrawingContext dc, double x, double a, double b, string text, Brush color)
    {
        var pen = new Pen(color, .85); dc.DrawLine(pen, new(x, a), new(x, b));
        foreach (double y in new[] { a, b }) dc.DrawLine(pen, new(x - 5, y + 3), new(x + 5, y - 3));
        VerticalText(dc, text, x - 9, (a + b) / 2, color, 11);
    }
    static void VerticalText(DrawingContext dc, string value, double x, double y, Brush color, double size)
    {
        var label = new FormattedText(value, CultureInfo.GetCultureInfo("it-IT"), FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, color, 1);
        dc.PushTransform(new RotateTransform(-90, x, y));
        dc.DrawRectangle(Brushes.White, null, new Rect(x - label.Width / 2 - 2, y - label.Height - 1, label.Width + 4, label.Height + 2));
        dc.DrawText(label, new Point(x - label.Width / 2, y - label.Height)); dc.Pop();
    }
    static void Section(DrawingContext dc, JsonNode segment, Point center, double radius, int index)
    {
        var section = segment["sezione"]!; double diameter = section.D("diameter_mm"), factor = 2 * radius / diameter;
        dc.DrawEllipse(PileReinforcementEditor.SegmentColor(index), new Pen(Ui.Navy, 1), center, radius, radius);
        double link = segment.D("staffa_raggio_mm") * factor;
        if (link > 0) dc.DrawEllipse(null, new Pen(Brushes.ForestGreen, 1.1), center, link, link);
        foreach (var row in segment.Array("barre_sezione"))
        {
            var values = row!.AsArray(); double r = (J.Number(values[3]) ?? 0) / 2 * factor;
            dc.DrawEllipse(Ui.Navy, null, new(center.X + (J.Number(values[0]) ?? 0) * factor, center.Y - (J.Number(values[1]) ?? 0) * factor), Math.Max(1.3, r), Math.Max(1.3, r));
        }
    }
}
