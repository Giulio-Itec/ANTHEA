using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Calculations;
using X.Core;

/// <summary>NEd of the detailing rules (user decision of 14/10/2026): the most severe compression of all the combinations, SLE included,
/// with the governing row named and a service value marked as such.</summary>
internal static class DetailingCompressionChecks
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks++; }
        var data = SezioneCA.DefaultData(); var settings = SectionWorkspace.Prepare(data); var input = data["input"]!.AsObject();
        settings["dettagli_costruttivi"]!["elemento"] = "Pilastro";
        settings["taglio"]!["azioni"] = new JsonArray();
        JsonObject Row(string name, string n) => J.Obj(("nome", name), ("N", n), ("Mx", "0"), ("My", "0"));
        var rows = new Dictionary<string, JsonObject[]> { ["SLU"] = [Row("SLU 1", "-100")], ["SLV"] = [], ["SLE"] = [Row("Rara 1", "-500")],
            ["SLE_FREQ"] = [], ["SLE_QP"] = [Row("QP 1", "-300")] };
        var service = ConcreteDetailingAnalysis.Calculate(input, settings, ConcreteDetailingAnalysis.AllRows(rows));
        Check(service.MaximumCompression == 500 && service.CompressionFromServiceability && service.CompressionSource == "Rara 1 · SLE rara",
            $"NEd dalle SLE: {service.MaximumCompression} · {service.CompressionSource}");
        rows["SLV"] = [Row("SLV 1", "-800")];
        var seismic = ConcreteDetailingAnalysis.Calculate(input, settings, ConcreteDetailingAnalysis.AllRows(rows));
        Check(seismic.MaximumCompression == 800 && !seismic.CompressionFromServiceability && seismic.CompressionSource == "SLV 1 · SLV",
            $"NEd dalle SLV: {seismic.MaximumCompression} · {seismic.CompressionSource}");
        var text = ConcreteDetailingPresentation.Calculate(input, settings, rows).DetailsText;
        Check(text.Contains("NEd,max compressione = 800 kN (SLV 1 · SLV)") && !text.Contains("Valore di esercizio"), "Testo di NEd: " + text);
        rows["SLV"] = [];
        Check(ConcreteDetailingPresentation.Calculate(input, settings, rows).DetailsText.Contains("Valore di esercizio"), "Valore di esercizio non segnalato");
        // Rows without limit state (callers before the decision) count as ultimate, as before
        Check(ConcreteDetailingAnalysis.Calculate(input, settings, [J.Obj(("N", "-120"))]).MaximumCompression == 120, "Righe senza stato limite");
        return checks;
    }
}
