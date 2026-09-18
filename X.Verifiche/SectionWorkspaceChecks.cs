using System.Text.Json.Nodes;
using X.Core;

internal static class SectionWorkspaceChecks
{
    internal static int Run()
    {
        int passed = 0;
        void Assert(bool condition, string name) { if (!condition) throw new Exception("Workspace CA: " + name); passed++; }
        void Reject(Action action, string name)
        { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Assert(rejected, name); }
        var old = SezioneCA.DefaultData(); string rare = old["combinazioni"]!["SLE"]![0]!["azioni"]!.ToJsonString();
        SectionWorkspace.Prepare(old);
        Assert(old["combinazioni"]!["SLE"]![0]!["azioni"]!.ToJsonString() == rare, "SLE conservata nella Rara");
        Assert(old["combinazioni"]!.Array("SLE_FREQ").Count == 0 && old["combinazioni"]!.Array("SLE_QP").Count == 0, "Nessuna azione inventata");
        string prepared = old.ToJsonString(); SectionWorkspace.Prepare(old); Assert(prepared == old.ToJsonString(), "Migrazione idempotente");
        var legacyInput = SezioneCA.DefaultInput(); legacyInput["axial_force_kn"] = "4321"; legacyInput["moment_x_knm"] = "321"; legacyInput["moment_y_knm"] = "123";
        var legacy = J.Obj(("input", legacyInput)); SectionWorkspace.Prepare(legacy);
        Assert(legacy["combinazioni"]!["SLU"]![0]!["azioni"]!.ToJsonString() == new JsonArray("4321", "321", "123").ToJsonString(), "Azioni legacy senza tabelle");
        Assert(legacy["combinazioni"]!.Array("SLE").Count == 0 && legacy["combinazioni"]!.Array("SLV").Count == 0, "Solo famiglia legacy esistente");
        legacy["combinazioni"]!.Array("SLU").Add(legacy["combinazioni"]!["SLU"]![0]!.DeepClone()); SectionWorkspace.Prepare(legacy);
        Assert(legacy["combinazioni"]!["SLU"]![0].S("id") != legacy["combinazioni"]!["SLU"]![1].S("id"), "Identificativi duplicati riparati");
        var bad = (JsonObject)old.DeepClone(); bad["workspace_ca"]!["versione"] = 99; Reject(() => SectionWorkspace.Prepare(bad), "Versione futura rifiutata");
        Assert(SectionWorkspace.Number("1,25", "test") == 1.25, "Virgola decimale");
        Reject(() => SectionWorkspace.Number("NaN", "test"), "NaN rifiutato");
        Reject(() => SectionWorkspace.Subdivisions("12.5", "test", 12, 144), "Suddivisione frazionaria rifiutata");
        Reject(() => SectionWorkspace.Subdivisions("10000", "test", 12, 144), "Discretizzazione illimitata rifiutata");
        foreach (string shape in new[] { "Circolare", "Rettangolare", "A T" }) foreach (string mode in new[] { "Plastico", "Elastico" })
        {
            var input = SezioneCA.DefaultInput(); input["shape"] = shape; string snapshot = input.ToJsonString();
            var mesh = SectionDomainMesh.Build(input, mode, 24, 61);
            Assert(input.ToJsonString() == snapshot, shape + mode + " non modifica input");
            Assert(mesh.Triangles.Count > 300 && mesh.Triangles.Count % 3 == 0 && mesh.Triangles.All(i => i >= 0 && i < mesh.Vertices.Count), "Indici mesh " + shape + mode);
            Assert(mesh.Vertices.All(p => double.IsFinite(p.N) && double.IsFinite(p.Mx) && double.IsFinite(p.My)), "Vertici finiti " + shape + mode);
            var force = new ActionPoint(500, 100, 75); var result = mesh.Check(force);
            Assert(result.Resistance is not null && result.Utilization is > 0, "Intersezione " + shape + mode);
            Assert(Math.Abs(mesh.Check(result.Resistance!.Value).Utilization!.Value - 1) < 1e-7, "Resistenza sulla superficie " + shape + mode);
            Assert(Math.Abs(mesh.Check(force * 3).Utilization!.Value - 3 * result.Utilization!.Value) < 1e-7, "Scalatura " + shape + mode);
            var constant = mesh.Check(force, true);
            Assert(constant.Resistance is not null && Math.Abs(constant.Resistance.Value.N - force.N) < 1e-7, "N costante " + shape + mode);
            Assert(mesh.Check(new ActionPoint(mesh.Scale.N * 3, 100, 50), true).Utilization is null, "N esterno non dichiarato verificato " + shape + mode);
            var cuts = mesh.Cut(true, .63);
            Assert(cuts.Count > 3 && cuts.All(s => Math.Abs(-s.A.Mx * Math.Sin(.63) + s.A.My * Math.Cos(.63)) < 1e-6), "Taglio e non proiezione " + shape + mode);
            Assert(mesh.Cut(false, 500).All(s => Math.Abs(s.A.N - 500) < 1e-7) && mesh.Cut(false, mesh.Scale.N * 5).Count == 0, "Taglio a N fissato " + shape + mode);
            Assert(mesh.Check(default).Utilization == 0 && mesh.Check(default).Resistance is null, "Azione nulla " + shape + mode);
        }
        using var cancel = new CancellationTokenSource(); cancel.Cancel(); bool stopped = false;
        try { SectionDomainMesh.Build(SezioneCA.DefaultInput(), "Plastico", cancel: cancel.Token); } catch (OperationCanceledException) { stopped = true; }
        Assert(stopped, "Cancellazione dominio");
        Console.WriteLine($"Controlli workspace CA (archivi e geometria dei domini): {passed} superati."); return passed;
    }
}
