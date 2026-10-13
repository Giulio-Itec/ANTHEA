using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Anthea.Application.Concrete;
using Anthea.Application.Documents;
using Anthea.Calculations;
using X.Core;

internal static class SectionActionsEditorChecks
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
        void Reject(Action action) { try { action(); } catch (ArgumentException) { checks++; return; } throw new Exception("Excel invalido accettato"); }
        SectionActionsExcel.Row[] expected = [new("SLU", "C1", -123.45, 3.4, -5.6, 7.8, 0, -9.1), new("SLU", "C1", 1, 2, 3, 4, 5, 6, "E-02"), new("SLV", "Sisma", -10, 2, 4, 6, 8, 10, "E-03"), new("SLE", "Rara", 10, 20, 30, 40, 50, 60)];
        byte[] bytes = SectionActionsExcel.Write(expected, unified: true);
        var read = SectionActionsExcel.Read(bytes);
        Check(!read.Legacy && read.Rows.SequenceEqual(expected), "Ordine, segni o associazione elemento errati");
        Check(SectionActionsExcel.Read(SectionActionsExcel.Template()).Rows.Count == 0, "Template inventa combinazioni");
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        byte[] Change(string cell, Action<XElement> change)
        {
            using var memory = new MemoryStream(); memory.Write(bytes);
            using (var zip = new ZipArchive(memory, ZipArchiveMode.Update, true))
            {
                var entry = zip.GetEntry("xl/worksheets/sheet1.xml")!; XDocument xml;
                using (var stream = entry.Open()) xml = XDocument.Load(stream);
                change(xml.Descendants(ns + "c").Single(c => c.Attribute("r")?.Value == cell));
                entry.Delete(); using var output = zip.CreateEntry("xl/worksheets/sheet1.xml").Open(); xml.Save(output);
            }
            return memory.ToArray();
        }
        foreach (string col in new[] { "D", "E", "F", "G", "H", "I" }) Reject(() => SectionActionsExcel.Read(Change(col + "7", c => c.RemoveNodes())));
        Reject(() => SectionActionsExcel.Read(Change("C7", c => c.Element(ns + "is")!.Element(ns + "t")!.Value = "Taglio")));
        Reject(() => SectionActionsExcel.Read(Change("D7", c => c.Element(ns + "v")!.Value = "NaN")));
        Reject(() => SectionActionsExcel.Read(Change("D6", c => c.Element(ns + "is")!.Element(ns + "t")!.Value = "N [N]")));
        Reject(() => SectionActionsExcel.Read(Change("D7", c => { c.RemoveNodes(); c.Add(new XElement(ns + "f", "1+2")); })));
        Check(SectionActionsExcel.Read(Change("D7", c => c.Add(new XElement(ns + "f", "-123.45")))).FormulaCells == 1, "Formule non segnalate");
        var data = DocumentService.CreateSheet("str_palo"); string before = data.ToJsonString();
        var vm = new SectionActionsViewModel(data); vm.Import(bytes, true);
        Check(data.ToJsonString() == before, "Import muta il foglio prima di Applica");
        Check(vm.Apply() && vm.Result is not null, "Applica fallita");
        Check(data.ToJsonString() == before, "VM modifica la sorgente");
        Check(SectionActionInputs.ShearRows(vm.Result!).Length == 3, "Taglio include SLE o omette SLV");
        Check(SectionActionsExcel.Read(vm.Export()).Rows.SequenceEqual(expected), "Export editor perde dati");
        vm.Selected = vm.Rows[0]; vm.DuplicateCommand.Execute(null);
        Check(vm.Rows[0].Id != vm.Rows[^1].Id && vm.Rows[^1].N == "-123.45", "Duplicazione identità o numeri");
        vm.RemoveCommand.Execute(null); Check(vm.Rows.Count == 4, "Rimozione non eseguita");
        Reject(() => vm.Import(Change("G7", c => c.RemoveNodes()), true)); Check(vm.Rows.Count == 4, "Import parziale elimina righe");
        var legacy = SezioneCA.DefaultData(); SectionWorkspace.Prepare(legacy);
        legacy["workspace_ca"]!["taglio"]!["azioni"] = new JsonArray(J.Obj(("nome", "V"), ("N", "-2"), ("Vx", "3"), ("Vy", "4")));
        var migration = new SectionActionsViewModel(legacy);
        Check(migration.Rows.Where(r => r.Origin is "Plastico" or "Elastico" or "Taglio").All(r => r.State == ""), "Conversione indovina SLU/SLV");
        Check(!migration.Apply(), "Migrazione senza stato accettata");
        foreach (var row in migration.Rows) { if (row.State == "") row.State = "SLU"; migration.Selected = row; migration.FillMissingWithZeroCommand.Execute(null); }
        Check(migration.Apply(), "Conversione esplicita non riuscita");
        Check(legacy["workspace_ca"]?[SectionActionInputs.VersionKey] is null, "Conversione muta sorgente senza applicazione host");
        return checks;
    }
}
