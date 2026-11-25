using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Application.Documents;
using Anthea.Application.Modules;
using Anthea.Calculations;
using Anthea.ConcreteWorkspace;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.QuickSection;
using Anthea.Presentation.Modules.ConcreteSection;
using netDxf;
using netDxf.Entities;
using netDxf.Tables;
using netDxf.Units;

namespace Anthea.Presentation.Tests;

internal static class ConcreteDxfChecks
{
    // Independent rectangular geometry: 600 x 800 mm, eccentric 100 x 200 hole at (450,400),
    // located at CAD (1000,2000) cm. Concrete centroid: (135000000 / 460000, 400) mm locally.
    private static DxfDocument Drawing(DrawingUnits unit = DrawingUnits.Centimeters)
    {
        var d = new DxfDocument(); d.DrawingVariables.InsUnits = unit;
        Polyline2D Rectangle(double x, double y, double w, double h, string layer) => new(new Vector2[]
            { new(x,y), new(x+w,y), new(x+w,y+h), new(x,y+h) }, true) { Layer = new Layer(layer) };
        d.Entities.Add(Rectangle(1000,2000,60,80,"CONTORNO"));
        d.Entities.Add(Rectangle(1040,2030,10,20,"FORI"));
        foreach (var p in new[] { (1006d,2006d), (1054d,2006d), (1054d,2074d), (1006d,2074d) })
            d.Entities.Add(new Circle(new Vector2(p.Item1,p.Item2),1) { Layer = new Layer("BARRE") });
        return d;
    }
    private static byte[] Bytes(DxfDocument d, bool binary = false)
    { using var s = new MemoryStream(); if (!d.Save(s, binary)) throw new InvalidOperationException("Fixture DXF non scritta"); return s.ToArray(); }
    private static ConcreteDxfSelection Selection(ConcreteDxfDrawing d) => new(d.Unit,
        d.Entities.Single(e => e.Layer == "CONTORNO").Label, "FORI", "BARRE");
    internal static int Run(string root, string output) => SingleThreadContext.Run(async _ =>
    {
        var e = new Expect(); string folder = Path.Combine(output,"dxf"); Directory.CreateDirectory(folder);
        var source = ConcreteQuickSection.Create(); ConcreteQuickSection.ReplaceCombinations(source,[new("SLU",-600,180,0)]); string before = source.ToJsonString();
        var bytes = Bytes(Drawing()); string path = Path.Combine(folder,"sezione-forata-cm.dxf"); File.WriteAllBytes(path,bytes);
        var drawing = ConcreteDxfImport.Read(path); var selected = Selection(drawing);
        var prepared = ConcreteDxfImport.Prepare(source["input"]!.AsObject(),drawing,selected);
        e.That(source.ToJsonString() == before,"DXF: anteprima non modifica il documento");
        e.That(drawing.Unit == "cm" && drawing.Entities.Count == 6 && drawing.Sha256.Length == 64,"DXF: intestazione, entità e impronta del file");
        var geometry = prepared.Geometry;
        e.That(Math.Abs(geometry.AreaCls - 460000) < 1e-6 && geometry.Width == 600 && geometry.Height == 800,"DXF: area indipendente rettangolo meno foro = 460000 mm²");
        double cx = 135000000d/460000;
        e.That(Math.Abs(J.Number(prepared.Input["sezione_dxf"]!["origine_x_mm"])!.Value - (10000+cx)) < 1e-6
            && Math.Abs(J.Number(prepared.Input["sezione_dxf"]!["origine_y_mm"])!.Value-20400) < 1e-6,"DXF: baricentro indipendente e origine CAD lontana");
        e.That(geometry.Holes.Count == 1 && geometry.Bars.Count == 4 && geometry.Bars.All(b => b.Diametro == 20)
            && Math.Abs(geometry.Bars[0].X - (60-cx)) < 1e-6 && Math.Abs(geometry.Bars[0].Y+340) < 1e-6,"DXF: posizioni e diametri scalati e centrati senza perdere il foro");
        e.That(prepared.Input["barre_manuali"] is null && prepared.Input.S("staffe_presenti") == "No","DXF: rimosse barre parametriche e staffe non presenti nel disegno");
        var homonymous = Drawing();
        homonymous.Entities.Polylines2D.Single(p => p.Layer.Name == "FORI").Layer = new Layer(ConcreteDxfImport.NoHoles);
        var namedLikePlaceholder = new Layer(ConcreteDxfImport.NoSelection);
        foreach (var circle in homonymous.Entities.Circles) circle.Layer = namedLikePlaceholder;
        var namedDrawing = ConcreteDxfImport.Read(Bytes(homonymous), "layer-omonimi.dxf");
        var namedDialog = new ConcreteDxfImportViewModel(source["input"]!.AsObject(), namedDrawing);
        string? actualHoleChoice = namedDialog.Form["fori"].Choices.FirstOrDefault(c => c != ConcreteDxfImport.NoHoles && c.Contains(ConcreteDxfImport.NoHoles, StringComparison.Ordinal));
        if (actualHoleChoice is not null) namedDialog.Form["fori"].SelectedChoice = actualHoleChoice;
        var namedGeometry = namedDialog.Prepared?.Geometry;
        e.That(actualHoleChoice is not null && namedGeometry is not null && namedGeometry.Holes.Count == 1 && namedGeometry.Bars.Count == 4
            && Math.Abs(namedGeometry.AreaCls - 460000) < 1e-6
            && namedDialog.Prepared!.Input["sezione_dxf"]!["layer_fori"]?.GetValue<string>() == ConcreteDxfImport.NoHoles
            && namedDialog.Prepared.Input["sezione_dxf"]!["layer_barre"]?.GetValue<string>() == ConcreteDxfImport.NoSelection,
            "DXF: layer chiamati Nessuno e scegliere distinti dalle opzioni UI; foro e barre conservati, area indipendente 460000 mm²");
        namedDialog.Form["fori"].SelectedChoice = ConcreteDxfImport.NoHoles;
        e.That(namedDialog.Prepared is { } withoutHoles && withoutHoles.Geometry.Holes.Count == 0
            && Math.Abs(withoutHoles.Geometry.AreaCls - 480000) < 1e-6 && withoutHoles.Input["sezione_dxf"]!["layer_fori"] is null,
            "DXF: Nessuno esclude i fori esplicitamente anche quando esiste un layer omonimo; area indipendente 480000 mm²");
        e.Throws<ArgumentException>(() => ConcreteDxfImport.Prepare(source["input"]!.AsObject(), drawing, selected with { HolesLayer = "ASSENTE" }),
            "DXF: layer dei fori inesistente rifiutato, senza perdere silenziosamente la geometria");
        var quasi=(JsonObject)source.DeepClone(); quasi["input"]=prepared.Input.DeepClone();
        var receiving = ConcreteQuickSection.Create(); string copiedActions = quasi["combinazioni"]!.ToJsonString();
        var copied = ConcreteQuickSection.CopyFor(quasi, receiving);
        var copiedGeometry = ConcreteQuickSection.Calculate(copied).Preview.Geometry;
        e.That(Math.Abs(copiedGeometry.AreaCls - 460000) < 1e-6 && copiedGeometry.Holes.Count == 1 && copiedGeometry.Bars.Count == 4
            && copiedGeometry.Bars.All(b => b.Diametro == 20) && copied["combinazioni"]!.ToJsonString() == copiedActions,
            "Copia DXF: contorno, foro e barre reali con area indipendente 460000 mm²; combinazioni della sorgente trasferite");
        var parametric = ConcreteQuickSection.CopyFor(receiving, copied);
        var parametricGeometry = ConcreteQuickSection.Calculate(parametric).Preview.Geometry;
        e.That(parametric["input"]!["sezione_dxf"] is null && parametricGeometry.Holes.Count == 0 && parametricGeometry.AreaCls == 480000,
            "Copia parametrica su DXF: nessun contorno o foro residuo, area indipendente 600 × 800 mm²");
        ConcreteQuickSection.ReplaceCombinations(quasi,[new("QP da DXF",0,100,0,"SLE_QP")]);
        ConcreteQuickSection.WriteServiceOption(quasi,ConcreteQuickSection.ExposureKey,"XC3");
        var quasiResult=ConcreteQuickSection.Calculate(quasi);
        var altered=(JsonObject)quasi.DeepClone(); altered["input"]!["cover_mm"]="25";
        altered["input"]!["inner_width_mm"]="123"; altered["input"]!["inner_height_mm"]="321";
        var alteredResult=ConcreteQuickSection.Calculate(altered);
        e.That(quasiResult.Crack?.Width is double crackWidth && crackWidth>0 && alteredResult.Crack?.Width==crackWidth
            && alteredResult.ServiceRatio==quasiResult.ServiceRatio,
            "DXF: tensioni e fessurazione NTC dalla geometria delle barre, indipendenti da copriferro e foro parametrici residui");
        var binary = ConcreteDxfImport.Read(Bytes(Drawing(),true),"binario.dxf");
        e.That(ConcreteDxfImport.Prepare(source["input"]!.AsObject(),binary,Selection(binary)).Geometry.AreaCls == geometry.AreaCls,"DXF binario e testo: stessa area");
        foreach (var (unit,factor) in new[] { ("mm",1d),("cm",10d),("m",1000d),("in",25.4d),("ft",304.8d) })
        {
            var p = ConcreteDxfImport.Prepare(source["input"]!.AsObject(),drawing,selected with { Unit=unit });
            e.That(Math.Abs(p.Geometry.Width-60*factor) < 1e-7,"DXF: conversione unità " + unit);
        }
        e.Throws<ArgumentException>(() => ConcreteDxfImport.Prepare(source["input"]!.AsObject(),drawing,selected with {Unit=ConcreteDxfImport.NoSelection}),"DXF: unità sconosciuta richiede scelta");
        e.Throws<ArgumentException>(() => ConcreteDxfImport.Prepare(source["input"]!.AsObject(),drawing,selected with {BarsLayer="FORI"}),"DXF: polilinee non interpretate come barre");
        e.Throws<ArgumentException>(() => ConcreteDxfImport.Prepare(source["input"]!.AsObject(),drawing,selected with {HolesLayer="BARRE"}),"DXF: fori e barre su layer distinti");
        e.Throws<ArgumentException>(() => ConcreteDxfImport.Read([1,2,3],"errato.dxf"),"DXF: file errato rifiutato");
        e.Throws<ArgumentException>(() => ConcreteDxfImport.Read(new byte[ConcreteDxfImport.MaximumBytes+1],"enorme.dxf"),"DXF: limite dimensione file");
        var open = Drawing(); open.Entities.Polylines2D.First().IsClosed=false;
        var readOpen = ConcreteDxfImport.Read(Bytes(open),"aperto.dxf");
        e.That(!readOpen.Entities.First(p=>p.Layer=="CONTORNO").IsContour && readOpen.Notes.Any(n=>n.Contains("aperta")),"DXF: polilinea aperta segnalata e non selezionabile");
        var outside = Drawing(); outside.Entities.Circles.First().Center = new Vector3(1200,2006,0);
        var readOutside = ConcreteDxfImport.Read(Bytes(outside),"esterno.dxf");
        e.Throws<ArgumentException>(()=>ConcreteDxfImport.Prepare(source["input"]!.AsObject(),readOutside,Selection(readOutside)),"GPC: barra esterna rifiutata");
        var overlap = Drawing(); overlap.Entities.Circles.Last().Center=overlap.Entities.Circles.First().Center;
        var readOverlap = ConcreteDxfImport.Read(Bytes(overlap),"sovrapposto.dxf");
        e.Throws<ArgumentException>(()=>ConcreteDxfImport.Prepare(source["input"]!.AsObject(),readOverlap,Selection(readOverlap)),"GPC: barre sovrapposte rifiutate");
        var crossed = Drawing(); var vertices = crossed.Entities.Polylines2D.First().Vertexes;
        (vertices[1].Position,vertices[2].Position)=(vertices[2].Position,vertices[1].Position);
        var readCrossed=ConcreteDxfImport.Read(Bytes(crossed),"incrociato.dxf");
        e.Throws<ArgumentException>(()=>ConcreteDxfImport.Prepare(source["input"]!.AsObject(),readCrossed,Selection(readCrossed)),"GPC: contorno autointersecante rifiutato");
        // Circle conversion must retain its centre; a circle at the origin would miss this regression.
        var round=Drawing(); round.Entities.Remove(round.Entities.Polylines2D.First());
        round.Entities.Add(new Circle(new Vector2(1030,2040),60){Layer=new Layer("CONTORNO")});
        var rd=ConcreteDxfImport.Read(Bytes(round),"cerchio.dxf");
        var rp=ConcreteDxfImport.Prepare(source["input"]!.AsObject(),rd,Selection(rd));
        e.That(Math.Abs(rp.Geometry.Width-1200)<1e-6 && rp.Geometry.Outline.Count==96,"DXF: contorno circolare lontano dall’origine discretizzato e centrato");
        // Nested INSERT on layer 0: library transformation, parent layer inherited.
        var blockDoc = Drawing(); var outer=blockDoc.Entities.Polylines2D.First(); blockDoc.Entities.Remove(outer);
        outer.Layer=new Layer("0"); var block=new netDxf.Blocks.Block("PROFILO",[outer]);
        blockDoc.Entities.Add(new Insert(block){Layer=new Layer("CONTORNO")});
        var bd=ConcreteDxfImport.Read(Bytes(blockDoc),"blocco.dxf");
        e.That(ConcreteDxfImport.Prepare(source["input"]!.AsObject(),bd,Selection(bd)).Geometry.AreaCls==geometry.AreaCls,"DXF: blocco esploso con layer ereditato");
        var curved=Drawing(); curved.Entities.Polylines2D.First().Vertexes[0].Bulge=0.2;
        var cd=ConcreteDxfImport.Read(Bytes(curved),"arco.dxf");
        e.That(ConcreteDxfImport.Prepare(source["input"]!.AsObject(),cd,Selection(cd)).Geometry.Outline.Count>4,"DXF: arco bulge discretizzato dal lettore CAD");
        var badLayer=Drawing(); badLayer.Entities.Add(new Line(new Vector2(1000,2000),new Vector2(1001,2001)){Layer=new Layer("BARRE")});
        var ld=ConcreteDxfImport.Read(Bytes(badLayer),"linea.dxf");
        e.Throws<ArgumentException>(()=>ConcreteDxfImport.Prepare(source["input"]!.AsObject(),ld,Selection(ld)),"DXF: linea nel layer barre non ignorata");
        var elevation=Drawing(); elevation.Entities.Polylines2D.First().Elevation=5;
        var ed=ConcreteDxfImport.Read(Bytes(elevation),"quota.dxf");
        e.That(ed.Entities.Single(p=>p.Layer=="CONTORNO").Error.Contains("piano XY"),"DXF: contorno fuori piano segnalato");
        var holeOutside=Drawing(); foreach(var v in holeOutside.Entities.Polylines2D.Last().Vertexes) v.Position+=new Vector2(100,0);
        var hd=ConcreteDxfImport.Read(Bytes(holeOutside),"foro-esterno.dxf");
        e.Throws<ArgumentException>(()=>ConcreteDxfImport.Prepare(source["input"]!.AsObject(),hd,Selection(hd)),"GPC: foro esterno rifiutato");
        var dialog = new ConcreteDxfImportViewModel(source["input"]!.AsObject(),drawing);
        e.That(dialog.Prepared is not null && dialog.ApplyCommand.CanExecute(null),"Anteprima: riconosciuti CONTORNO/FORI/BARRE");
        dialog.Form["unita"].SelectedChoice=ConcreteDxfImport.NoSelection;
        e.That(dialog.Prepared is null && dialog.Error.Length>0 && !dialog.ApplyCommand.CanExecute(null),"Anteprima: errore blocca l’importazione");
        var messages=new ScriptedMessages(); var files=new ScriptedFileDialogs().Then(null).Then(path).Then(path).Then(path);
        QuickSectionSheetViewModel? sheet=null;
        var dialogs=new ScriptedDialogs().Then(o=>((ConcreteDxfImportViewModel)o).CancelCommand.Execute(null))
            .Then(o=>((ConcreteDxfImportViewModel)o).ApplyCommand.Execute(null))
            .Then(o=>{sheet!.IsReadOnly=true;((ConcreteDxfImportViewModel)o).ApplyCommand.Execute(null);});
        using (sheet=new(new(source,false,new Host(),Scripted.Services(messages,files:files,dialogs:dialogs),ModuleServices.Get(ConcreteQuickSection.Module),new CalculationLimiter(1))))
        {
            await sheet.PendingCalculation; var original=sheet.Data.ToJsonString();
            await sheet.ImportDxfCommand.ExecuteAsync(null);
            e.That(sheet.Data.ToJsonString()==original,"Annulla selezione file: documento invariato");
            await sheet.ImportDxfCommand.ExecuteAsync(null);
            e.That(sheet.Data.ToJsonString()==original,"Annulla anteprima: documento invariato");
            await sheet.ImportDxfCommand.ExecuteAsync(null); await sheet.PendingCalculation;
            e.That(sheet.IsDxf && !sheet.HasLayers && !sheet.IsParametric && sheet.DxfBars.Count==4 && sheet.HasResults,"Importazione accettata: foglio libero calcolato");
            e.That(sheet.Section?.Holes.Count==1 && !sheet.AddLayerCommand.CanExecute(null),"DXF: nessuno strato parametrico applicato");
            var full=ConcreteAnalysis.Calculate((JsonObject)sheet.Data.DeepClone());
            e.That(sheet.Current?.UltimateCheck?.Utilization is double ratio && J.Number(full["domini"]?["3D:SLU"]?[sheet.SelectedCombinationId!]?["Utilization"]) == ratio,"DXF: stesso rapporto della combinazione nel modulo completo");
            var report=ConcreteQuickReportAnalysis.Create(sheet.Data);
            e.That(report.Report.Geometry.Any(v=>v.Value.Contains("sezione-forata-cm.dxf")),"Relazione: provenienza DXF conservata");
            using (var complete=new ConcreteSheetViewModel(new((JsonObject)sheet.Data.DeepClone(),false,new Host(),Scripted.Services(),ModuleServices.Get(ConcreteSectionSheetModule.Id),new CalculationLimiter(1)),new ConcreteAnalysisSession()))
            {
                e.That(complete.Inputs.IsTemplateSection && complete.Inputs.TemplateGeometryNote.Contains("DXF") && complete.Inputs.Bars.Rows.Count==4,
                    "Modulo completo: contorno DXF protetto, nota corretta e quattro barre editabili");
            }
            string saved=Path.Combine(folder,"sezione-dxf.anthea"); var document=DocumentService.CreateDocument(ConcreteQuickSection.Module);
            document["dati"]=sheet.Data.DeepClone(); DocumentService.Save(saved,document);
            var reopened=DocumentService.Open(saved)["dati"]!.AsObject();
            e.That(JsonNode.DeepEquals(reopened,sheet.Data),"DXF: salvataggio e riapertura esatti");
            var imported=sheet.Data.ToJsonString(); await sheet.ImportDxfCommand.ExecuteAsync(null);
            e.That(sheet.Data.ToJsonString()==imported && !sheet.ImportDxfCommand.CanExecute(null),"Sola lettura durante anteprima: importazione non applicata");
            sheet.IsReadOnly=false;
            var manual=(JsonObject)sheet.Data.DeepClone(); manual["input"]!["barre_manuali"]=manual["input"]!["sezione_dxf"]!["barre"]!.DeepClone();
            manual["input"]!["barre_manuali"]![0]!["phi"]=22;
            sheet.Reload(manual); await sheet.PendingCalculation;
            sheet.Shape=ConcreteQuickSection.Rectangular; await sheet.PendingCalculation;
            sheet.Shape=ConcreteQuickSection.Dxf; await sheet.PendingCalculation;
            e.That(sheet.Section?.Bars.Count==4 && sheet.Section.Holes.Count==1 && sheet.DxfBars[0].Diametro==22,"DXF salvato e barre modificate conservati tornando dalla forma rettangolare");
            File.Delete(path); sheet.Reload(reopened); await sheet.PendingCalculation;
            e.That(sheet.HasResults && sheet.Section?.Bars.Count==4,"DXF originale cancellato: geometria conservata e ricalcolabile");
            e.That(messages.Failures.Count==0 && messages.Shown.Count==0,"Importazione senza errori nascosti");
        }
        // Keep a reviewable file in artifacts for UI and manual inspection.
        File.WriteAllBytes(path,bytes);
        return e.Count;
    });
    private sealed class Host : ISheetHost
    {
        public void OpenModuleCopy(string module,JsonObject data,string name) { }
        public void ReplaceSoil(JsonObject data) => throw new InvalidOperationException();
        public IReadOnlyList<SoilSource> SoilSources()=>[];
        public void RequestReport() { }
    }
}
