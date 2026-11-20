using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Anthea.Application.Bridge;
using Anthea.Application.Documents;
using Anthea.Application.Modules;
using Anthea.Calculations;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.QuickCompositeSection;
using Anthea.Reports;

namespace Anthea.Presentation.Tests;

internal static class QuickCompositeSectionChecks
{
    internal static int Run(string root, string output) => SingleThreadContext.Run(async _ =>
    {
        var expect = new Expect();
        string folder = Path.Combine(output, "sezione-composta-rapida"); Directory.CreateDirectory(folder);
        var messages = new ScriptedMessages(); var host = new Host(); var services = Scripted.Services(messages);
        QuickCompositeSectionSheetViewModel Open(JsonObject data, bool readOnly = false) => new(new(data, readOnly, host, services,
            ModuleServices.Get(BridgeQuickSection.Module), new CalculationLimiter(1)));
        var data = BridgeQuickSection.Create();
        ModuleDirectory.ValidateData(BridgeQuickSection.Module, data); ModuleDirectory.ValidateData(BridgeQuickSection.SourceModule, data);
        expect.That(ModuleDirectory.Get(BridgeQuickSection.Module).Area == "Strutture" && new QuickCompositeSectionSheetModule().Reports.ShortReport,
            "Modulo registrato nel catalogo e con relazione");
        var original = ModuleDirectory.CreateData(BridgeQuickSection.SourceModule);
        expect.That(original.All(p => JsonNode.DeepEquals(p.Value, data[p.Key])), "Tutti i default del modulo completo sono conservati");
        expect.That(BridgeQuickSection.ScopeError(data) == "" && BridgeQuickSection.Combinations(data).Length == 1, "Default compatibile e una combinazione");
        var results = BridgeQuickSection.Calculate(data);
        expect.That(results.Cases.Single().Calculation is not null, "Default calcolato: " + results.Cases.Single().Error);
        EqualEngine(data, results, expect);
        BridgeQuickSection.Replace(data, [new("SLU", 1500, 2000, 3000, 500, -150), new("Rara", 1300, 1600, 2000, 250, -100, "SLE RA"),
            new("QP negativa", 0, -100, -120, -200, 50, "SLE QP")]);
        results = BridgeQuickSection.Calculate(data); EqualEngine(data, results, expect);
        expect.That(results.Cases.All(c => c.Ratio.HasValue) && results.Cases.All(c => c.Checks.Any(k => k.Heading.Contains("Taglio"))), "Tre SL con rapporti e taglio");
        expect.That(results.Cases[1].Checks.Any(k => k.Heading == "Soletta · estradosso") && results.Cases[1].Checks.Any(k => k.Heading == "Piattabanda inferiore"), "SLE: fibre principali");
        var firstId = BridgeQuickSection.Combinations(data)[0].S("id");
        BridgeQuickSection.Select(data, firstId); string added = BridgeQuickSection.Add(data);
        expect.That(BridgeQuickSection.ActionKeys.All(k => BridgeQuickSection.Number(BridgeQuickSection.Action(BridgeQuickSection.Selected(data), k), k) == 0), "Nuova combinazione a zero");
        BridgeQuickSection.Remove(data, added); expect.That(BridgeQuickSection.SelectedId(data) == firstId, "Eliminazione e ritorno alla prima riga");
        var full = BridgeQuickSection.ForCombination(data, BridgeQuickSection.Combinations(data)[1].S("id"));
        full["nota_futura"] = "conservare"; full["fasi"]![1]!["Mx"] = "1750"; full["stato"] = "SLE quasi permanente";
        BridgeQuickSection.Initialize(full);
        expect.That(full.S("nota_futura") == "conservare" && BridgeQuickSection.Combinations(full).Length == 3
            && BridgeQuickSection.Action(BridgeQuickSection.Selected(full), "M_lungo") == "1750" && BridgeQuickSection.Selected(full).S("stato") == "SLE QP",
            "Andata e ritorno completo: azioni e SL aggiornati, altre combinazioni e chiavi conservate");
        expect.Throws<ArgumentException>(() => BridgeQuickSection.State("SLE FR"), "Frequente non supportata: rifiuto esplicito");
        var invalid = (JsonObject)data.DeepClone(); BridgeQuickSection.Write(invalid, firstId, "M_breve", "abc");
        var bad = BridgeQuickSection.Calculate(invalid);
        expect.That(bad.Cases[0].Ratio is null && bad.Cases[0].Error.Length > 0 && bad.Cases[1].Ratio.HasValue, "Errore locale senza rapporto obsoleto; altre righe valide");
        invalid = (JsonObject)data.DeepClone(); invalid["t_web"] = "-1";
        expect.That(BridgeQuickSection.Calculate(invalid).Cases.All(c => c.Ratio is null), "Geometria invalida: tutti i rapporti assenti");
        invalid = (JsonObject)data.DeepClone(); invalid["plate2"] = true; string unsupported = invalid.ToJsonString();
        expect.That(BridgeQuickSection.Calculate(invalid).Cases.All(c => c.Error.Contains("modulo completo")) && invalid.ToJsonString() == unsupported, "Fuori ambito: dati conservati, nessun risultato ingannevole");

        // Independent hand calculation, fixed rational expectations. Coordinates from the slab intrados (y=0).
        // Steel: 500*25=12500 at -12.5; 14*1800=25200 at -925; 700*30=21000 at -1840.
        // A=58700; Q=-62106250. Slab 3000*250 / n=75000 at +125 for manual n=10, no bars.
        // Homogenized A=133700; Q=-52731250. These expectations must never be replaced by engine output.
        var hand = BridgeQuickSection.Create(); hand["rebars_top"] = false; hand["rebars_bottom"] = false; hand["classe4"] = false;
        foreach (var phase in BridgeQuickSection.Selected(hand)["fasi"]!.AsArray().OfType<JsonObject>()) { phase["modo"] = "Da n"; phase["n"] = "10"; }
        BridgeQuickSection.Synchronize(hand);
        var handResult = BridgeQuickSection.Calculate(hand).Cases.Single().Calculation!;
        var composite = handResult.Stages.Last().Contributions.Last();
        expect.That(Math.Abs(handResult.Geometry.SteelArea - 58700) < 1e-8, "Atteso manuale: area acciaio 58700 mm²");
        expect.That(Math.Abs(composite.Area - 133700) < 1e-8, "Atteso manuale: area omogeneizzata 133700 mm²");
        expect.That(Math.Abs(composite.Centroid - (-52731250d / 133700)) < 1e-8, "Atteso manuale: baricentro -52731250/133700 mm");
        File.WriteAllText(Path.Combine(folder, "attesi-indipendenti.txt"), "A acciaio = 58700 mm²; A* = 133700 mm²; yG* = -52731250/133700 mm. n=10, senza armature e senza riduzione classe 4.");

        using var sheet = Open((JsonObject)data.DeepClone());
        await Expect.Within(sheet.PendingCalculation);
        expect.That(sheet.HasResults && sheet.Revision == 0 && sheet.Geometry is not null && sheet.BarRows.Count == 2, "Apertura MVVM senza modifiche");
        sheet.GeometryForm.Set("h_trave", "1955"); sheet.MaterialsForm.Set("classe_cls", "C40/50"); sheet.BarsForm.Set("d_top", "20");
        sheet.CombinationRows[0].ShortTerm = "3250"; await Expect.Within(sheet.PendingCalculation);
        expect.That(sheet.SelectedCase?.Calculation is not null && sheet.Current!.Cases.All(c => c.Ratio.HasValue), "Modifica geometria, catalogo, armature e azioni");
        EqualEngine(sheet.Data, sheet.Current!, expect);
        var current = sheet.Current; sheet.CombinationRows[1].IsSelected = true;
        expect.That(ReferenceEquals(current, sheet.Current) && sheet.ResultTitle == "Rara · SLE RA", "Selezione cambia riepilogo senza ricalcolo");
        sheet.OpenCompleteCommand.Execute(null);
        expect.That(host.Module == BridgeQuickSection.SourceModule && host.Data is not null && JsonNode.DeepEquals(host.Data, sheet.Data), "Apertura nel completo senza perdita del documento");
        sheet.Commit(); var saved = (JsonObject)sheet.Data.DeepClone();
        var document = DocumentService.CreateDocument(BridgeQuickSection.Module); document["dati"] = saved.DeepClone();
        string file = Path.Combine(folder, "sezione-composta-rapida.anthea"); DocumentService.Save(file, document);
        using (var reopened = Open(DocumentService.Open(file)["dati"]!.AsObject()))
        {
            await Expect.Within(reopened.PendingCalculation);
            expect.That(JsonNode.DeepEquals(saved, reopened.Data) && JsonNode.DeepEquals(sheet.Result, reopened.Result), "Salvataggio e riapertura esatti");
        }
        sheet.IsReadOnly = true; sheet.Title = "vietato"; sheet.GeometryForm["t_web"].EditText = "30"; sheet.CombinationRows[0].V = "999"; sheet.CombinationRows[2].IsSelected = true;
        expect.That(JsonNode.DeepEquals(sheet.Data, saved) && !sheet.AddCombinationCommand.CanExecute(null) && !sheet.ImportExcelCommand.CanExecute(null)
            && sheet.SelectedId == sheet.CombinationRows[2].Id, "Sola lettura protegge i dati e permette la selezione");
        sheet.IsReadOnly = false; sheet.Reload(saved); await Expect.Within(sheet.PendingCalculation);
        sheet.CombinationRows[1].ShortTerm = "x"; await Expect.Within(sheet.PendingCalculation);
        expect.That(sheet.SelectedCase?.Ratio is null && sheet.Status.Length > 0 && sheet.Checks.Count == 0, "MVVM invalido: messaggio, nessun vecchio risultato");
        sheet.Reload(saved); await Expect.Within(sheet.PendingCalculation);
        expect.That(sheet.SelectedCase?.Ratio.HasValue == true, "Ricarica ripristina il risultato");

        BridgeQuickActionsExcel.Row[] excel = [new("Prima", 10.5, 20, -30, 50, -100), new("Rara", 5, 8, 10, 15, 20, "SLE RA"), new("QP", 0, 1, 2, 3, 4, "SLE QP")];
        byte[] bytes = BridgeQuickActionsExcel.Write(excel);
        expect.That(BridgeQuickActionsExcel.Read(bytes).SequenceEqual(excel), "Excel: azioni e SL riletti esatti");
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        using (var zip = new ZipArchive(new MemoryStream(bytes)))
        {
            var xml = XDocument.Load(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
            expect.That(xml.Descendants(ns + "row").Single(r => (string?)r.Attribute("r") == "4").Descendants(ns + "t").Select(t => t.Value).SequenceEqual(BridgeQuickActionsExcel.Layout), "Excel: ordine uguale alla finestra");
            expect.That(xml.Descendants(ns + "formula1").Single().Value == "\"SLU,SLE RA,SLE QP\"", "Excel: menu dei tre SL supportati");
        }
        var old = Rewrite(bytes, xml => { foreach (var cell in xml.Descendants(ns + "c").Where(c => ((string?)c.Attribute("r"))?.StartsWith("B") == true).ToArray()) cell.Remove(); });
        expect.That(BridgeQuickActionsExcel.Read(old).All(r => r.State == "SLU"), "Excel senza SL: compatibilità con file vecchi");
        var shuffled = Rewrite(bytes, xml => { foreach (var cell in xml.Descendants(ns + "c")) { string r = (string)cell.Attribute("r")!; if (r.StartsWith("B")) cell.SetAttributeValue("r", "H" + r[1..]); } });
        expect.That(BridgeQuickActionsExcel.Read(shuffled).SequenceEqual(excel), "Excel con SL in fondo: colonne riconosciute per titolo");
        expect.Throws<ArgumentException>(() => BridgeQuickActionsExcel.Read([1, 2, 3]), "Excel corrotto rifiutato");
        sheet.ExcelFolder = Path.Combine(folder, "excel"); sheet.ExportExcelCommand.Execute(null); string path = sheet.ExcelPath!;
        expect.That(File.Exists(path) && ((RecordingLinks)services.Links).Opened.Last().LocalPath == path, "Excel esportato e aperto");
        File.WriteAllBytes(path, bytes); sheet.ImportExcelCommand.Execute(null); await Expect.Within(sheet.PendingCalculation);
        expect.That(sheet.ExcelPath == path && BridgeQuickActionsExcel.Rows(sheet.Data).SequenceEqual(excel), "Excel: reimportazione dello stesso file");
        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read)) { sheet.ImportExcelCommand.Execute(null); sheet.ExportExcelCommand.Execute(null); }
        expect.That(sheet.ExcelPath != path && File.Exists(sheet.ExcelPath), "Excel aperto: lettura condivisa, nuova esportazione senza sovrascrittura bloccata");
        var report = BridgeQuickReportAnalysis.Create(sheet.Data).Report;
        expect.That(report.Cases.Count == 3 && report.Cases.All(c => c.Stresses.Count > 0 && c.Shear.Count > 0) && report.Materials.Any(v => v.Label == "fy" && v.Value.Length > 0), "Report: tutte le combinazioni, valori e tensioni native");
        expect.That(BridgeQuickReport.Governing(report).Select(p => p.Case.State).Distinct().Count() == 3, "Report breve: tre SL rappresentati");
        expect.That(messages.Failures.Count == 0 && messages.Shown.Count == 0, "Nessun errore nei comandi validi: " + string.Join("; ", messages.Failures.Select(f => f.Error.Message)));
        return expect.Count;
    });

    private static void EqualEngine(JsonObject data, BridgeQuickResult result, Expect expect)
    {
        foreach (var item in result.Cases)
        {
            var full = ModuleServices.Get(BridgeQuickSection.SourceModule).Calculate(BridgeQuickSection.ForCombination(data, item.Id));
            var native = BridgeResultView.Read(full);
            expect.That(item.Calculation is not null && JsonNode.DeepEquals(native.Json(), item.Calculation.Json()), "Stesso risultato nativo del modulo completo · " + item.Name);
            double? ratio = native.Stages.SelectMany(s => s.Points.Where(p => p.Active).Select(p => p.Utilization).Concat((s.Shear?.Checks ?? []).Select(c => (double?)c.Ratio))).Max();
            expect.That(item.Ratio == ratio, "Riepilogo: massimo delle verifiche native in tutte le fasi · " + item.Name);
        }
    }
    private static byte[] Rewrite(byte[] input, Action<XDocument> change)
    {
        using var stream = new MemoryStream(); stream.Write(input);
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Update, true))
        {
            var entry = zip.GetEntry("xl/worksheets/sheet1.xml")!; XDocument xml;
            using (var source = entry.Open()) xml = XDocument.Load(source);
            change(xml); entry.Delete(); using var target = zip.CreateEntry("xl/worksheets/sheet1.xml").Open(); xml.Save(target);
        }
        return stream.ToArray();
    }
    private sealed class Host : ISheetHost
    {
        internal string? Module; internal JsonObject? Data;
        public void OpenModuleCopy(string module, JsonObject data, string name) { Module = module; Data = data; }
        public void ReplaceSoil(JsonObject data) => throw new InvalidOperationException();
        public IReadOnlyList<SoilSource> SoilSources() => [];
        public void RequestReport() => throw new InvalidOperationException();
    }
}
