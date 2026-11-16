using System.Text.Json.Nodes;
using Anthea.Application.EmbeddedWalls;
using Anthea.Presentation.Modules.EmbeddedWalls;

namespace Anthea.Presentation.Tests;

/// <summary>
/// Organizzazione del modulo paratie (progetto del 16/11), step 5: what the section draws comes from the data, with its table and units (C7).
/// Header (wall, case, phase); legend of the soils (one line per soil as the tables write it, the sides, one tint per name); anchors to scale
/// only with the lengths of «Sfilamento dei tiranti» (R08), otherwise schematic; prestress with its origin; magnification and legend are state
/// of the view: no edit, no new revision (R12 a); the row open in the editor is the element highlighted.
/// </summary>
internal static partial class EmbeddedWallChecks
{
    private static void SectionDrawingChecks(Expect e)
    {
        var data = EmbeddedWallDocument.Create(); data["name"] = "Paratia di prova";
        void Soil(string side, int index, string name, double top, double bottom, double? gamma, double? phi)
        {
            var row = index < data[side]!.AsArray().Count ? data[side]![index]!.AsObject() : EmbeddedWallDocument.NewRow(side);
            row["name"] = name; row["top"] = top; row["bottom"] = bottom; row["gamma"] = gamma; row["phi"] = phi;
            if (index >= data[side]!.AsArray().Count) data[side]!.AsArray().Add(row);
        }
        Soil("left_soils", 0, "Sabbia", 0, -6, 18, 32); Soil("left_soils", 1, "Argilla", -6, -20, 19.5, 24);
        Soil("right_soils", 0, "Sabbia", 0, -6, 18, 32); Soil("right_soils", 1, "Argilla", -6, -20, 20, 24);
        var excavation = EmbeddedWallDocument.NewRow("stages"); excavation["id"] = "F2"; excavation["right_ground"] = -4; data["stages"]!.AsArray().Add(excavation);
        var anchor = EmbeddedWallDocument.NewRow("supports"); anchor["id"] = "T1"; anchor["elevation"] = -1.5; anchor["angle"] = 20; data["supports"]!.AsArray().Add(anchor);
        var strut = EmbeddedWallDocument.NewRow("supports"); strut["id"] = "P1"; strut["kind"] = "Strut"; strut["elevation"] = -0.5; strut["direction"] = -1; data["supports"]!.AsArray().Add(strut);
        foreach (string stage in new[] { "F1", "F2" })
        {
            var tie = EmbeddedWallDocument.NewRow("activations"); tie["stage"] = stage; tie["prestress"] = stage == "F1" ? 60 : null; data["activations"]!.AsArray().Add(tie);
        }
        var prop = EmbeddedWallDocument.NewRow("activations"); prop["stage"] = "F2"; prop["support"] = "P1"; data["activations"]!.AsArray().Add(prop);
        var check = EmbeddedWallDocument.NewRow("anchor_checks"); check["free_length"] = 8; check["bond_length"] = 6; data["anchor_checks"]!.AsArray().Add(check);
        using var sheet = Open(data);
        string original = sheet.Data.ToJsonString(); long revision = sheet.Revision;
        var sketch = sheet.Sketch!;
        e.That(sketch.Name == "Paratia di prova" && sketch.CaseId == "Caso 1" && sketch.Phase == "F2" && sketch.StageDetail.Contains("Terreno destra: -4 m"),
            "Passo 5: intestazione del disegno (paratia, caso, fase) o dati della fase assenti.");
        // Legend: Sabbia identical on both sides (one line, both sides); Argilla with two weights (two lines, same tint: same name).
        var soils = sketch.Soils;
        e.That(soils.Count == 3 && soils[0] is { Name: "Sabbia", Sides: "sx · dx", Tint: 0 } && soils[0].Gamma == EmbeddedWallSketch.Format(18, "kN/m³") && soils[0].Phi == "32"
            && soils[1] is { Name: "Argilla", Sides: "sx", Tint: 1 } && soils[1].Gamma == EmbeddedWallSketch.Format(19.5, "kN/m³") && soils[2] is { Name: "Argilla", Sides: "dx", Tint: 1 },
            "Passo 5: legenda dei terreni non come le tabelle: " + string.Join(" | ", soils.Select(s => $"{s.Name} {s.Gamma} {s.Phi} {s.Sides} {s.Tint}")));
        e.That(sketch.Bands.Where(b => b.Name == "Sabbia").All(b => b.Tint == 0) && sketch.Bands.Where(b => b.Table == "segments").All(b => b.Tint == -1),
            "Passo 5: una tinta per terreno sui due lati non rispettata.");
        var band = sketch.Bands.First(b => b.Table == "left_soils");
        e.That(band.Detail.Contains("«Terreno a sinistra»") && band.Detail.Contains("Peso naturale: 18 kN/m³") && band.Detail.Contains("Attrito efficace: 32 °"),
            "Passo 5: dati dello strato senza tabella o unità: " + band.Detail);
        // R08: the anchor to scale with the lengths of its row; the strut schematic; the prestress kept from F1 says so.
        var tie2 = sketch.Supports.Single(s => s.Id == "T1"); var prop2 = sketch.Supports.Single(s => s.Id == "P1");
        e.That(tie2.FreeLength == 8 && tie2.BondLength == 6 && tie2.AnchorCheck is not null && tie2.Detail.Contains("disegnati in scala")
            && tie2.Prestress == 60 && tie2.Detail.Contains("mantenuta dalla fase precedente") && tie2.Detail.Contains("Quota: -1.5 m"),
            "Passo 5: tirante non in scala con i dati di «Sfilamento dei tiranti» o pretensione senza provenienza: " + tie2.Detail);
        e.That(prop2.FreeLength is null && prop2.AnchorCheck is null && !prop2.Detail.Contains("Simbolo schematico") && prop2.Detail.Contains("0 kN all'installazione"),
            "Passo 5: puntone con lunghezze o pretensione senza provenienza: " + prop2.Detail);
        var checks = sheet.Tables.Single(t => t.Key == "anchor_checks");
        checks.Rows[0]["bond_length"] = "";
        tie2 = sheet.Sketch!.Supports.Single(s => s.Id == "T1");
        e.That(tie2.FreeLength is null && tie2.BondLength is null && tie2.AnchorCheck is null && tie2.Detail.Contains("Simbolo schematico"),
            "Passo 5: tirante disegnato in scala senza il tratto cementato.");
        checks.Rows[0]["bond_length"] = "6"; checks.AddCommand.Execute(null); checks.Rows[1]["free_length"] = "5"; checks.Rows[1]["bond_length"] = "4";
        e.That(sheet.Sketch!.Supports.Single(s => s.Id == "T1").AnchorCheck is null, "Passo 5: due righe di sfilamento per lo stesso tirante scelte a caso.");
        sheet.Reload(JsonNode.Parse(original)!.AsObject());
        string reloaded = sheet.Data.ToJsonString(); revision = sheet.Revision;
        // Magnification and legend: state of the view, limits 1–8, no edit and no revision.
        e.That(sheet.SketchZoom == EmbeddedWallSketchZoom.Minimum && !sheet.FitSketchCommand.CanExecute(null) && !sheet.ZoomOutSketchCommand.CanExecute(null)
            && sheet.ZoomInSketchCommand.CanExecute(null), "Passo 5: stato iniziale dell'ingrandimento errato.");
        sheet.ZoomInSketchCommand.Execute(null);
        e.That(sheet.SketchZoom == EmbeddedWallSketchZoom.Step && sheet.FitSketchCommand.CanExecute(null), "Passo 5: «Ingrandisci» non ingrandisce.");
        for (int i = 0; i < 20; i++) sheet.ZoomInSketchCommand.Execute(null);
        e.That(sheet.SketchZoom == EmbeddedWallSketchZoom.Maximum && !sheet.ZoomInSketchCommand.CanExecute(null), "Passo 5: ingrandimento oltre il massimo.");
        sheet.SketchZoom = double.NaN;
        e.That(sheet.SketchZoom == EmbeddedWallSketchZoom.Minimum, "Passo 5: ingrandimento non numerico accettato.");
        sheet.SketchZoom = 3; sheet.FitSketchCommand.Execute(null);
        e.That(sheet.SketchZoom == EmbeddedWallSketchZoom.Minimum, "Passo 5: «Adatta» non torna alla sezione intera.");
        string text = sheet.SoilLegendCommandText; sheet.ToggleSoilLegendCommand.Execute(null);
        e.That(!sheet.ShowSoilLegend && sheet.SoilLegendCommandText != text, "Passo 5: legenda non nascosta.");
        sheet.ToggleSoilLegendCommand.Execute(null);
        // The element chosen in the drawing is the row of the editor, and the drawing highlights it.
        var support = sheet.Sketch!.Supports.Single(s => s.Id == "T1");
        sheet.PickModelCommand.Execute(new WallSketchPick("supports", support.Row, support.Detail));
        e.That(ReferenceEquals(sheet.SketchSelection, support.Row), "Passo 5: l'elemento scelto nel disegno non è la riga dell'editor.");
        sheet.PickModelCommand.Execute(new WallSketchPick("anchor_checks", support.AnchorCheck, support.Detail));
        e.That(ReferenceEquals(sheet.SketchSelection, support.AnchorCheck) && sheet.InspectorTable?.Key == "anchor_checks", "Passo 5: il bulbo non apre «Sfilamento dei tiranti».");
        e.That(sheet.ShowSoilLegend && sheet.Data.ToJsonString() == reloaded && sheet.Revision == revision,
            "Passo 5: ingrandimento, legenda o selezione modificano dati o revisione.");
    }
}
