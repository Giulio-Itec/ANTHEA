using System.Text.Json.Nodes;
using Anthea.Calculations;
using X.Core;

internal static class MaterialSharingChecks
{
    public static int Run(string directory)
    {
        var log = new List<string>();
        void Check(bool test, string label) { if (!test) throw new Exception(label); log.Add("OK " + label); }
        void Near(double a, double b, string label) => Check(Math.Abs(a-b) < 1e-8 * Math.Max(1, Math.Abs(b)), label);
        var legacy = RetainingWall.LegacyDefaults(); legacy["materials"]!["fyk"] = 520;
        string before = legacy.ToJsonString(); var oldInput = RetainingWall.MaterialSectionInput(legacy);
        Check(legacy.ToJsonString() == before, "Lettura materiale non modifica il vecchio archivio");
        Near(oldInput.D("steel_fu_mpa"), 520, "Acciaio legacy conserva fu=fy");
        Near(oldInput.D("steel_eps_u"), 100, "Acciaio legacy conserva deformazione ultima");
        var wall = RetainingWall.Defaults(); var m = wall["materials"]!;
        RetainingWall.ApplyMaterialPreset(wall, ConcreteMaterialCatalog.Steel(false, "NTC 2018").Single(x=>x.S("nome")=="B450A"));
        var sectionInput = RetainingWall.SectionInput(wall, "Fusto", .4);
        foreach(var p in ConcreteMaterialCatalog.Steel(false, "NTC 2018").Single(x=>x.S("nome")=="B450A").Where(p=>p.Key!="nome"))
            Check(J.Equivalent(sectionInput[p.Key], p.Value), "Catalogo acciaio conservato: " + p.Key);
        m["steel_modulus_mpa"] = 180000; m["steel_fu_mpa"] = 600; m["steel_eps_u"] = 90; m["steel_diagramma"] = "Incrudente"; m["gamma_s"] = 1.3;
        m["cls_diagramma"] = "Bilineare"; m["gamma_c"] = 1.6; m["alpha_cc"] = .8;
        sectionInput = RetainingWall.SectionInput(wall, "Fusto", .4);
        var strengths = ConcreteMaterials.DesignValues(sectionInput, J.Obj(("normativa", "NTC 2018")));
        Near(strengths.Fyd, 450/1.3, "γs effettivo nel verificatore"); Near(strengths.Fcd, 30*.8/1.6, "αcc e γc effettivi nel verificatore");
        Near(ConcreteMaterials.Rebar(sectionInput).E, 180000, "Modulo acciaio effettivo");
        var result = RetainingWall.Calculate(wall);
        var export = RetainingWall.ExportSection(result, "Fusto", 3, result.Cases[0].Name);
        foreach(string key in RetainingWall.SectionMaterialKeys.Append("fck_mpa").Append("fyk_mpa")) Check(J.Equivalent(export["input"]![key],sectionInput[key]), "Invio sezione conserva " + key);
        var archive = ProjectDocuments.CreateArchive(); var project = ProjectDocuments.AddProject(archive);
        var cls = ProjectDocuments.AddSheet(project, "mat_calcestruzzo"); var steel = ProjectDocuments.AddSheet(project, RebarMaterial.Module);
        cls["dati"]!["classe"] = "C40/50"; cls["dati"]!["esposizione_principale"] = "XC4";
        cls["dati"]!["numeri"] ??= new JsonObject();
        cls["dati"]!["scelte"] ??= new JsonObject();
        cls["dati"]!["numeri"]!["aggregate"] = "32"; cls["dati"]!["scelte"]!["life"] = "100 anni";
        cls["dati"]!["scelte"]!["ground"] = "Direttamente su terra";
        steel["dati"]!["input"]!["steel_modulus_mpa"] = 190000; steel["dati"]!["input"]!["gamma_s"] = 1.25;
        var branch = ProjectDocuments.AddSection(project); var sheet = ProjectDocuments.AddSheet(branch, RetainingWall.Module);
        var d = sheet["dati"]!.AsObject();
        Near(d["materials"].D("fck"),40,"Muro eredita CLS dal progetto"); Near(d["materials"].D("steel_modulus_mpa"),190000,"Muro eredita Es dal progetto");
        Near(d["materials"].D("gamma_s"),1.25,"Muro eredita γs dal modulo acciaio");
        Check(d["materials"].S("exposure")=="XC4" && d["detailing"].D("aggregate")==32 && d["detailing"].D("life")==100,"Muro eredita esposizione aggregato e vita");
        Check(d["detailing"].S("cover_ground")=="Direttamente su terra","Muro eredita superficie di getto");
        Near(RetainingWall.RequiredCover(d),Materiali.MaterialCover.Required(cls["dati"]!.AsObject(),40,RetainingWall.MaximumBarDiameter(d)),"Minimo muro uguale alla scheda Materiali");
        Check(ProjectValidation.CoverChecks(project,sheet).Any(x=>x.Passed==false),"Copriferro insufficiente del muro segnalato nel progetto");
        d["materials"]!["cover"] = RetainingWall.RequiredCover(d);
        Check(ProjectValidation.CoverChecks(project,sheet).All(x=>x.Passed==true),"Copriferro allineato verificato nel progetto");
        string geometry=d["geometry"]!.ToJsonString(),actions=d["actions"]!.ToJsonString();
        steel["dati"]!["input"]!["steel_modulus_mpa"]=185000; ProjectSharedData.ApplyHierarchy(steel,["Materiali","Coefficienti"]);
        Near(d["materials"].D("steel_modulus_mpa"),190000,"Aggiornamento sostituisce snapshot atomicamente"); d=sheet["dati"]!.AsObject();
        Near(d["materials"].D("steel_modulus_mpa"),185000,"Modifica riferimento si propaga al muro");
        Check(geometry==d["geometry"]!.ToJsonString()&&actions==d["actions"]!.ToJsonString(),"Condivisione non altera geometria o azioni");
        d["materials"]!["steel_modulus_mpa"]=170000;
        Check(ProjectSharedData.Differences(project).Any(x=>x.Key=="steel_modulus_mpa"),"Differenza muro acciaio visibile nel confronto");
        Check(!ProjectSharedData.ReferenceKeys(sheet,["steel_modulus_mpa"]).Contains("steel_modulus_mpa"),"Riferimento superiore protegge il dato condiviso");
        var section = ProjectDocuments.AddSheet(branch,"str_palo");
        Near(section["dati"]!["input"].D("cover_mm"),d["materials"].D("cover"),"Copriferro muro condiviso con la sezione");
        d=sheet["dati"]!.AsObject();
        d["family"]="gravity"; d["gravity_design"]!["type"]="Muratura";
        Check(!ProjectSharedData.Common(cls,sheet).Any() &&
            !ProjectSharedData.Common(steel,sheet).Any(p => p.Source.Key != "materiale_acciaio_nome"),
            "Muratura non eredita proprietà meccaniche CLS o armature dormienti; il nome può restare archiviato");
        File.WriteAllText(Path.Combine(directory,"materiali-progetto.anthea"),archive.ToJsonString(J.Options));
        File.WriteAllBytes(Path.Combine(directory,"materiali-report.docx"),ReportRetainingWall.Create("Materiali del muro e verificatore sezioni",result));
        File.WriteAllLines(Path.Combine(directory,"materiali-test.txt"),log.Append($"PASS {log.Count} controlli materiali")); return log.Count;
    }
}
