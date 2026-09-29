using System.Text.Json.Nodes;

namespace Anthea.Calculations;

public static partial class RetainingWall
{
    /// <summary>A detached concrete workspace, with all available combinations at the selected station.</summary>
    public static JsonObject ExportSection(Result result, string member, double position, string selectedCombination)
    {
        if (result.Input.S("family") != "cantilever") throw new ArgumentException("Il trasferimento al modulo c.a. è disponibile per i muri a mensola armati.");
        var selected = result.Cases.Single(c => c.Name == selectedCombination);
        var force = selected.Sections.Single(f => f.Name == member && Math.Abs(f.Position - position) < 1e-10);
        if (member != "Fusto" && !selected.Contact.Valid) throw new ArgumentException("Sezione non trasferibile: reazioni di fondazione non disponibili.");
        var d = SezioneCA.DefaultData(); var input = SectionInput(result.Input, member, force.Thickness, position); d["input"] = input;
        d["combinazioni"] = new JsonObject();
        // Establish the native, compression-negative convention before adding the wall actions.
        var ws = SectionWorkspace.Prepare(d); input["axial_force_kn"] = -force.N; input["moment_x_knm"] = force.M; input["moment_y_knm"] = 0;
        input["classe_cls"] = "Personalizzato";
        string zone = ReinforcementKey(result.Input, member, position);
        ws["nota"] = $"Da Muri di sostegno · {member} · z/l={position:G9} m · zona {zone}. Fascia di 1 m. Combinazione selezionata: {selectedCombination}. N convertito a compressione negativa; Mx associato a Vy. Copia indipendente, senza sincronizzazione col muro. SISMA ed ECCEZIONALE sono trasferite nel gruppo Plastico (SLU), conservando lo stato nel nome. Eventuali mensole prive di equilibrio sono escluse e annotate.";
        foreach (string set in SectionWorkspace.Sets) d["combinazioni"]![set] = new JsonArray();
        var shear = ws["taglio"]!.AsObject(); shear["modello"] = "Senza staffe"; shear["azioni"] = new JsonArray();
        foreach (var c in result.Cases.OrderByDescending(c => c.Name == selectedCombination))
        {
            if (member != "Fusto" && !c.Contact.Valid) { ws["nota"] = ws.S("nota") + " Esclusa " + c.Name + ": perdita di equilibrio."; continue; }
            var f = c.Sections.Single(s => s.Name == member && Math.Abs(s.Position - position) < 1e-10);
            string set = c.State.StartsWith("SLE") ? c.State : "SLU", name = c.Name + " [" + c.State + "]";
            d["combinazioni"]![set]!.AsArray().Add(J.Obj(("id", Guid.NewGuid().ToString("N")), ("nome", name), ("azioni", new[] { -f.N, f.M, 0d })));
            if (!c.State.StartsWith("SLE")) shear.Array("azioni").Add(J.Obj(("id", Guid.NewGuid().ToString("N")), ("nome", name), ("N", -f.N), ("Mx", f.M), ("My", 0), ("Vx", 0), ("Vy", f.V), ("T", 0)));
        }
        var sle = ws["sle_comuni"]!.AsObject(); var mat = result.Input["materials"]!;
        sle["modello"] = "Lineare"; sle["trazione_cls"] = "No"; sle["phi"] = mat["creep"]!.DeepClone(); sle["esposizione"] = mat["exposure"]!.DeepClone(); sle["sensibilita"] = "Non sensibile"; sle["aderenza"] = "Migliorata"; sle["durata"] = "Lunga";
        sle["copriferro_fessure"] = mat["cover"]!.DeepClone(); sle["spaziatura_fessure"] = (1000 - 2 * mat.D("cover") - input.D("top_bar_diameter_mm")) / (input.D("top_bar_count") - 1);
        ws["dominio2d"]!["N"] = -force.N;
        d["origine_muro"] = J.Obj(("elemento", member), ("posizione", position), ("combinazione", selectedCombination), ("zona_armatura", zone), ("stato", selected.State), ("taglio_selezionato", force.V));
        SectionWorkspace.Prepare(d); ModuleCatalog.ValidateData("str_palo", d); return d;
    }
}
