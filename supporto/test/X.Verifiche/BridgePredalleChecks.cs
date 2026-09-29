using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using X.Core;

internal static class BridgePredalleChecks
{
    internal static void Run()
    {
        int count = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception("Predalle: " + message); count++; }
        void Near(double a, double b, string message) => Check(Math.Abs(a - b) <= 1e-9 * Math.Max(1, Math.Abs(b)), message);
        void Reject(JsonObject data, string message)
        { try { BridgeSection.Geometry(data); } catch (ArgumentException) { count++; return; } throw new Exception("Predalle: " + message); }
        JsonObject Copy(JsonObject data) => (JsonObject)data.DeepClone();
        var legacy = BridgeSection.Defaults(); legacy.Remove("predalle"); legacy.Remove("h_predalle"); legacy.Remove("rif_ferri_inf");
        var before = legacy.ToJsonString(); var old = BridgeSection.Geometry(legacy);
        Near(old.Bars.Min(b => b.Y), 45, "archivio precedente sposta i ferri");
        Check(before == legacy.ToJsonString(), "lettura geometria modifica l'archivio");
        var data = Copy(legacy); data["predalle"] = true; data["h_predalle"] = 60;
        var shape = BridgeSection.Geometry(data);
        Near(shape.Bars.Min(b => b.Y), 105, "quota inferiore = 60 + 45");
        Near(shape.Bars.Max(b => b.Y), 205, "quota superiore = 250 - 45");
        Near(shape.SlabHeight, 250, "predalle sommata due volte alla soletta");
        Near(shape.SteelArea, old.SteelArea, "carpenteria alterata");
        Check(shape.Bars.Length == old.Bars.Length, "aggiunte barre proprie della predalle");
        Near(shape.Bars.Sum(b => b.Area), old.Bars.Sum(b => b.Area), "area ferri cambiata");
        var face = Copy(data); face["rif_ferri_inf"] = BridgeSection.SlabBottomReference;
        Near(BridgeSection.Geometry(face).Bars.Min(b => b.Y), 45, "riferimento intradosso ignorato");
        data["h_predalle"] = 80;
        Near(BridgeSection.Geometry(data).Bars.Min(b => b.Y), 125, "variazione spessore non muove fila");
        Near(data.D("cover_bottom"), 45, "distanza di ingresso riscritta");
        data["h_predalle"] = 60;
        foreach (string kind in BridgeSection.SectionTypes)
        {
            var withPanel = Copy(data); withPanel["sezione"] = kind;
            if (kind == BridgeSection.SectionTypes[2]) withPanel["b_bottom"] = 1900;
            var manual = Copy(withPanel); manual["predalle"] = false; manual["cover_bottom"] = 105;
            Near(BridgeSection.Geometry(withPanel).Bars.Min(b => b.Y), 105, kind + " quota");
            foreach (string method in BridgeSection.CalculationMethods)
            {
                // Same physical geometry: compare the existing methods with a manually specified absolute bar position.
                withPanel["metodo_analisi"] = method; manual["metodo_analisi"] = method;
                withPanel["classe4"] = false; manual["classe4"] = false;
                withPanel["fasi"] = new JsonArray(BridgeSection.Phase("Geometria", "Composta", -100, 100));
                manual["fasi"] = withPanel["fasi"]!.DeepClone();
                var actual = BridgeSection.Calculate(withPanel); var expected = BridgeSection.Calculate(manual);
                Check(actual.Stages.Count == 1, "aggiunta una fase per la predalle");
                foreach (var (a, b) in actual.Stages[0].Points.Zip(expected.Stages[0].Points)) Near(a.Stress, b.Stress, kind + " / " + method + " identità delle tensioni");
                Near(actual.Stages[0].EffectiveSteel.Area, expected.Stages[0].EffectiveSteel.Area, "nessuna rigidezza aggiuntiva");
                byte[] report = ReportBridge.Create("Predalle", actual, ["geometria"]);
                using var zip = new ZipArchive(new MemoryStream(report));
                using var xml = zip.GetEntry("word/document.xml")!.Open();
                string text = XDocument.Load(xml).Root!.Value;
                Check(text.Contains("predalle: " + EngineeringFormat.Number(60) + " mm") && text.Contains("quota effettiva y = " + EngineeringFormat.Number(105) + " mm") && text.Contains("nessun materiale"), "report: quota o campo di applicazione assenti");
            }
        }
        foreach (var value in new JsonNode?[] { JsonValue.Create(0), JsonValue.Create(-1), JsonValue.Create(250), JsonValue.Create(300), JsonValue.Create("abc"), null })
        { var invalid = Copy(data); invalid["h_predalle"] = value; Reject(invalid, "spessore predalle non valido accettato"); }
        var clash = Copy(data); clash["h_predalle"] = 160; Reject(clash, "collisione fra file accettata");
        clash = Copy(data); clash["cover_bottom"] = 4; Reject(clash, "barra a cavallo dell'estradosso predalle accettata");
        clash = Copy(data); clash["rif_ferri_inf"] = "ignoto"; Reject(clash, "riferimento sconosciuto accettato");
        foreach (bool top in new[] { true, false }) foreach (bool bottom in new[] { true, false })
        {
            var optional = Copy(data); optional["rebars_top"] = top; optional["rebars_bottom"] = bottom;
            if (!bottom) { optional["cover_bottom"] = "dormiente"; optional["rif_ferri_inf"] = "dormiente"; }
            Check(BridgeSection.Geometry(optional).Bars.Length == (top ? 20 : 0) + (bottom ? 20 : 0), "file assenti non rispettate");
        }
        var dormant = Copy(data); dormant["predalle"] = false; dormant["h_predalle"] = "dormiente"; dormant["rif_ferri_inf"] = "dormiente";
        Near(BridgeSection.Geometry(dormant).Bars.Min(b => b.Y), 45, "opzione disattiva legge campi dormienti");
        var archive = Archivio.Documento(BridgeSection.Module); archive["dati"] = Copy(data); Archivio.Valida(archive);
        Check(JsonNode.DeepEquals(archive, JsonNode.Parse(archive.ToJsonString())), "salvataggio perde dati predalle");
        var project = ProjectDocuments.AddProject(ProjectDocuments.CreateArchive());
        var section = ProjectDocuments.AddSection(project); var source = ProjectDocuments.AddSheet(section, BridgeSection.Module); source["dati"] = Copy(data);
        var child = ProjectDocuments.AddSection(section); var target = ProjectDocuments.AddSheet(child, BridgeSection.Module);
        Check(target["dati"].B("predalle") && target["dati"].D("h_predalle") == 60, "geometria non ereditata nel progetto");
        Near(BridgeSection.Geometry(target["dati"]!.AsObject()).Bars.Min(b => b.Y), 105, "eredità cambia significato alla distanza");
        target["dati"]!["h_predalle"] = 70;
        Check(ProjectSharedData.Differences(section).Any(d => d.Key == "Ponte · h_predalle"), "conflitto spessore non rilevato");
        Console.WriteLine($"Predalle: {count} controlli superati (geometria, metodi esistenti, report e progetto).");
    }
}
