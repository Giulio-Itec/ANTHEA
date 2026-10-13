using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Application.Concrete;

internal static class ConcretePropertyChecks
{
    internal static async Task<int> Run()
    {
        int checks = 0;
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks++; }
        var data = ModuleCatalog.CreateData("str_palo");
        var workspace = SectionWorkspace.Prepare(data); var input = data["input"]!.AsObject();
        string original = data.ToJsonString();
        using var session = new ConcretePropertySession(input, workspace);
        input["width_mm"] = "invalid"; workspace["trefoli"] = new JsonArray();
        var options = J.Obj(("metodo", "Da φ"), ("phi", "2"));
        var task = session.CalculateAsync(options);
        options["phi"] = "invalid";
        var result = await task;
        Check(result.Error is null && result.Value?.Phi == 2, "Sessione non conserva le fotografie di input e richiesta");
        Check(result.Value!.Text.Contains("Sezione elastica non fessurata."), "Report proprietà incompleto");
        Check(input.S("width_mm") == "invalid" && options.S("phi") == "invalid", "Sessione modifica i dati del chiamante");
        var equivalent = await session.CalculateAsync(J.Obj(("metodo", "Da n"), ("n", result.Value.N)));
        Check(equivalent.Error is null && equivalent.Value?.Text == result.Value.Text, "Conversione φ/n altera il report");
        Check(!result.TryPublish(_ => throw new InvalidOperationException("Risultato obsoleto pubblicato")), "Risultato precedente pubblicabile");
        var invalid = await session.CalculateAsync(J.Obj(("phi", "-1")));
        Check(invalid.Error is ArgumentException && invalid.Value is null, "Input invalido non osservato");
        var recovered = await session.CalculateAsync(J.Obj(("phi", "0")));
        Check(recovered.Error is null && recovered.Value?.Phi == 0, "Sessione non recupera dopo errore locale");
        var cancelled = await session.CalculateAsync(J.Obj(("phi", "1")), new CancellationToken(true));
        Check(cancelled.Cancelled && !cancelled.TryPublish(_ => { }), "Richiesta annullata pubblicata");
        var last = await session.CalculateAsync(J.Obj(("phi", "3")));
        session.Dispose();
        Check(!last.TryPublish(_ => { }), "Sessione chiusa pubblica proprietà");
        bool refused = false;
        try { await session.CalculateAsync(J.Obj(("phi", "1"))); } catch (ObjectDisposedException) { refused = true; }
        Check(refused, "Sessione chiusa accetta nuovo lavoro");
        var untouched = JsonNode.Parse(original)!.AsObject();
        string before = untouched.ToJsonString();
        var preview = ConcretePreview.Read(untouched["input"]!.AsObject(), untouched["workspace_ca"]!.AsObject());
        Check(untouched.ToJsonString() == before, "Anteprima modifica i dati del foglio");
        Check(preview.Geometry.Width == 600 && preview.Geometry.Height == 800 && preview.Geometry.AreaCls == 480000,
            "Geometria rettangolare della preview alterata");
        preview.Geometry.Input["width_mm"] = 10;
        Check(untouched.ToJsonString() == before, "Geometria esposta condivide input del documento");
        return checks;
    }
}
