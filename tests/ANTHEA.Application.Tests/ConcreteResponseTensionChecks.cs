using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Application.Documents;
using Anthea.Calculations;

/// <summary>
/// «CLS teso nelle risposte» del M–χ della sezione c.a. (16/10/2026): il campo si salva con il foglio come gli altri del M–χ; i fogli salvati
/// prima (senza la chiave) restano senza la chiave dopo apertura e preparazione e valgono «Trascurato»; il servizio dell'applicazione passa
/// la scelta al calcolo. I valori della curva sono provati in ConcreteLibraryAdapter.Checks (prova 4d).
/// </summary>
internal static class ConcreteResponseTensionChecks
{
    internal static int Run(string root, string output)
    {
        int checks = 0;
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks++; }
        const string Key = ConcreteCurvatureAnalysis.ResponseConcreteTensionKey;
        string path = Path.Combine(output, "cls-teso-risposte.anth");
        var old = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "tests/fixtures/f3/inputs/str_palo--default.json")))!["data"]!.AsObject();
        (JsonObject Data, JsonObject Options) RoundTrip(JsonObject data)
        {
            var document = J.Obj(("formato", "X"), ("versione", 1), ("tipo", "calcolo"), ("modulo_id", "str_palo"), ("dati", data.DeepClone()));
            DocumentService.Save(path, document);
            var opened = DocumentService.Open(path)["dati"]!.AsObject();
            Check(JsonNode.DeepEquals(ConcretePersistenceChecks.EditorData(opened), document["dati"]), "Round trip del foglio c.a. con «CLS teso nelle risposte» alterato");
            var settings = ConcreteEditor.PrepareWorkspace(opened);
            ConcreteEditor.PrepareParameters(opened["input"]!.AsObject(), settings);
            return (opened, settings["momento_curvatura"]!.AsObject());
        }
        Check(ConcreteEditor.ResponseConcreteTensionModes.SequenceEqual(new[] { "Trascurato", "Tension stiffening" }), "Scelte di «CLS teso nelle risposte»");
        Check(CalculationHelp.Field(Key) == ConcreteCurvatureAnalysis.ResponseConcreteTensionHelp, "Aiuto di «CLS teso nelle risposte» assente");

        // Foglio salvato prima del campo: nessuna chiave aggiunta da apertura e preparazione; vale «Trascurato».
        Check(old["workspace_ca"]?["momento_curvatura"]?[Key] is null, "La fixture storica ha già la chiave");
        var (_, oldOptions) = RoundTrip(old);
        Check(oldOptions[Key] is null, "Apertura o preparazione aggiunge «CLS teso nelle risposte» al foglio storico");
        Check(ConcreteCurvatureAnalysis.ResponseConcreteTension(oldOptions) == GPC.Model.Materials.ConcreteMaterial.TensionResponseDiagrams.Default,
            "Foglio storico non «Trascurato»");

        // Ogni scelta resta nel foglio dopo il salvataggio e la riapertura.
        foreach (string mode in ConcreteEditor.ResponseConcreteTensionModes)
        {
            var data = (JsonObject)old.DeepClone();
            ConcreteEditor.PrepareParameters(data["input"]!.AsObject(), ConcreteEditor.PrepareWorkspace(data));
            data["workspace_ca"]!["momento_curvatura"]![Key] = mode;
            var (_, options) = RoundTrip(data);
            Check(options.S(Key) == mode, "Scelta «" + mode + "» persa nel round trip");
        }

        // Servizio dell'applicazione (pannello M–χ e report): la scelta arriva al calcolo; punto limite invariato.
        var (reopened, curveOptions) = RoundTrip(old);
        curveOptions["passi"] = "10"; curveOptions["raffina_snervamento"] = "0";
        var input = reopened["input"]!.AsObject(); var workspace = reopened["workspace_ca"]!.AsObject();
        var neglected = Anthea.Application.Modules.StructuralQueries.ConcreteCurvature(input, workspace, curveOptions);
        var stiffeningOptions = (JsonObject)curveOptions.DeepClone(); stiffeningOptions[Key] = "Tension stiffening";
        var stiffening = Anthea.Application.Modules.StructuralQueries.ConcreteCurvature(input, workspace, stiffeningOptions);
        Check(!neglected.Status.Contains("CLS teso nelle risposte") && stiffening.Status.EndsWith(ConcreteCurvatureAnalysis.ResponseConcreteTensionStatus, StringComparison.Ordinal),
            "Stato della curva senza la scelta di «CLS teso nelle risposte»");
        Check(stiffening.LimitMoment == neglected.LimitMoment && stiffening.UltimateCurvature == neglected.UltimateCurvature,
            "Punto limite diverso con il tension stiffening");
        Check(stiffening.Points.Where(p => !p.Limit && p.Moment > 0).Zip(neglected.Points.Where(p => !p.Limit && p.Moment > 0))
            .All(p => p.First.Curvature < p.Second.Curvature), "Tension stiffening senza effetto sulle risposte del servizio");
        File.Delete(path);
        return checks;
    }
}
