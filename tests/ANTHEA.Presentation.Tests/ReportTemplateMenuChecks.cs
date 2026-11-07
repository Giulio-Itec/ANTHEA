using Anthea.Presentation.Services;
using Anthea.Presentation.Shell.Commands;
using Anthea.Reports;

namespace Anthea.Presentation.Tests;

internal static class ReportTemplateMenuChecks
{
    internal static int Run(string output)
    {
        var expect = new Expect();
        string directory = Path.Combine(output, "template-menu"); Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "modello.docx");
        var doc = new DocxDocument();
        doc.SetXml("[Content_Types].xml", "<Types/>");
        doc.SetXml("word/document.xml", "<document/>");
        doc.SetXml("word/styles.xml", "<w:styles xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:style w:type=\"paragraph\" w:styleId=\"Normal\"><w:name w:val=\"Normal\"/></w:style></w:styles>");
        File.WriteAllBytes(path, DocxWriter.Write(doc));
        var store = new ReportTemplateStore(Path.Combine(directory, "preference.json")); store.Reset();
        var files = new ScriptedFileDialogs().Then(null).Then(path).Then(Path.Combine(directory, "missing.docx"));
        var messages = new ScriptedMessages();
        var menu = new ReportTemplateMenuViewModel(store, files, messages);
        expect.That(menu.Current.Contains("ANTHEA"), "Stile iniziale");
        menu.SelectCommand.Execute(null);
        expect.That(store.Current is null && messages.Shown.Count == 0, "Annullamento cambia la preferenza");
        menu.SelectCommand.Execute(null);
        expect.That(menu.Current.Contains("modello.docx") && store.Current is not null, "Selezione non applicata subito");
        expect.That(((OpenFileRequest)files.Requests[0]).Filters[0].Patterns.SequenceEqual(new[] { "*.docx", "*.dotx" }), "Filtro template");
        menu.SelectCommand.Execute(null);
        expect.That(store.Current is not null && messages.Failures.Count == 1, "Errore non mostrato o selezione precedente persa");
        menu.ResetCommand.Execute(null);
        expect.That(store.Current is null && menu.Current.Contains("ANTHEA"), "Ripristino della scelta");
        return expect.Count;
    }
}
