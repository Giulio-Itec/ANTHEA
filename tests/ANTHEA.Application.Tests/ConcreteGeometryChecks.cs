using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Application.Concrete;

internal static class ConcreteGeometryChecks
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); checks++; }
        bool Exact(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);
        foreach (string shape in new[] { "Rettangolare", "Circolare", "A T" })
        foreach (bool hole in shape == "A T" ? new[] { false } : new[] { false, true })
        {
            var input = SezioneCA.DefaultInput(); input["shape"] = shape;
            input["foro_presente"] = hole; input["inner_diameter_mm"] = 200; input["inner_width_mm"] = 100; input["inner_height_mm"] = 100;
            var native = new SezioneCA(input); var view = ConcretePreview.PrepareGeometry(input);
            Check(JsonNode.DeepEquals(view.Input, native.Input) && view.Shape == native.Shape && view.CircularSides == native.CircularSides, "Proiezione dell'input");
            Check(Exact(view.Width, native.Width) && Exact(view.Height, native.Height) && Exact(view.AreaCls, native.AreaCls)
                && Exact(view.AreaSteel, native.AreaSteel) && Exact(view.Radius, native.Radius)
                && Exact(view.BarRadius, native.BarRadius) && Exact(view.CentroidY, native.CentroidY), "Quote e aree non identiche bit per bit");
            Check(view.Outline.Count == native.Outline.Count && view.Outline.Zip(native.Outline).All(p => p.First.Zip(p.Second).All(v => Exact(v.First, v.Second))), "Ordine o coordinate del contorno cambiati");
            Check(view.Holes.Count == native.Holes.Count && view.Holes.Zip(native.Holes).All(h => h.First.Length == h.Second.Length
                && h.First.Zip(h.Second).All(p => p.First.Zip(p.Second).All(v => Exact(v.First, v.Second)))), "Fori non identici bit per bit");
            Check(view.Bars.Count == native.Bars.Count && view.Bars.Zip(native.Bars).All(b => Exact(b.First.X,b.Second.X)
                && Exact(b.First.Y,b.Second.Y) && Exact(b.First.Area,b.Second.Area) && Exact(b.First.Diametro,b.Second.Diametro)), "Barre non identiche bit per bit");
            view.Outline[0][0] = 12345; view.Input["shape"] = "modificato"; view.Bars.Clear();
            var next = ConcretePreview.PrepareGeometry(input);
            Check(next.Outline[0][0] != 12345 && next.Shape == shape && next.Bars.Count > 0 && input.S("shape") == shape, "La vista possiede dati condivisi con un'altra preparazione");
        }
        Check(typeof(ConcreteGeometry).GetProperties().All(p => p.PropertyType.Assembly != typeof(SezioneCA).Assembly), "Il contratto grafico espone un tipo del motore");
        return checks;
    }
}
