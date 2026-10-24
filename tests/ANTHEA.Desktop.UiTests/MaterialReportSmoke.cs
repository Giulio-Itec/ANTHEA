using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using X.Core;

namespace X.Desktop;

/// <summary>
/// ui/smoke-material-report: the Word reports of the single concrete and reinforcing steel sheets. Until F5.13 g the first part of
/// MainWindow.SmokeProjectReport (materialsOnly), with the factory of the window; since then on its own, with the factory of the bench (the same one the
/// WPF checks gave MainWindow). The same steps and checks.
/// </summary>
internal static class MaterialReportSmoke
{
    internal static Task Run(string directory)
    {
        Directory.CreateDirectory(directory);
        Check(directory, ModuleHostBench.SheetEditors());
        File.WriteAllText(Path.Combine(directory, "smoke.txt"), "OK: report singoli CLS e acciaio, proprietà correnti e segnalazione dati acciaio non validi.");
        return Task.CompletedTask;
    }

    internal static void Check(string directory, SheetEditorFactory editors)
    {
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        foreach (string module in new[] { "mat_calcestruzzo", RebarMaterial.Module })
        {
            using var materialEditor = editors.Open(module, Archivio.NuovoFoglio(module));
            string materialPath = Path.Combine(directory, module + ".docx");
            materialEditor.ExportReport(materialPath, module == "mat_calcestruzzo" ? "Calcestruzzo" : "Acciaio per armature", []);
            using var package = ZipFile.OpenRead(materialPath);
            using var input = package.GetEntry("word/document.xml")!.Open();
            string contents = XDocument.Load(input).ToString();
            Check(contents.Contains("Scheda materiale") && contents.Contains("Proprietà di calcolo"), "Report materiale incompleto");
            Check(contents.Contains(module == "mat_calcestruzzo" ? "Copriferro nominale" : "Resistenza di progetto fyd"), "Proprietà materiale mancanti");
            if (module == RebarMaterial.Module)
            {
                materialEditor.Data["input"]!["gamma_s"] = "0";
                Check(materialEditor.MaterialReportContent().Error is not null, "Acciaio non valido senza avviso");
            }
        }
    }
}
