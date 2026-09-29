using System.Text.Json.Nodes;
using Anthea.Calculations;
using X.Core;

internal static class ActionsChecks
{
    internal static int Run(string directory)
    {
        var log = new List<string>();
        void Check(bool value, string name) { if (!value) throw new Exception(name); log.Add("OK " + name); }
        void Near(double a, double b, string name, double tol = 1e-7) => Check(Math.Abs(a - b) <= tol * Math.Max(1, Math.Abs(b)), name);
        void Reject(JsonObject d, string name) { try { RetainingWall.Calculate(d); } catch (ArgumentException) { Check(true, name); return; } throw new Exception("Accettato: " + name); }
        JsonObject Single(JsonObject d, string state = "SLE")
        {
            var c = (JsonObject)RetainingWall.GenerateCombinations(d).OfType<JsonObject>().First(x => x.S("state") == state).DeepClone();
            d["combinations"] = new JsonArray(c); d["combination_mode"] = "Personalizzate"; d["combination_signature"] = RetainingWall.CombinationSignature(d); return c;
        }
        try
        {
            var legacy = RetainingWall.LegacyDefaults(); legacy["loads"]!["horizontal"] = 7; legacy["loads"]!["vertical"] = 12;
            var old = RetainingWall.Calculate(legacy); RetainingWall.Upgrade(legacy); var upgraded = RetainingWall.Calculate(legacy);
            Near(upgraded.Cases[0].Horizontal, old.Cases[0].Horizontal, "Migrazione conserva la spinta e la forza correlata");
            Near(upgraded.Cases[0].Vertical, old.Cases[0].Vertical, "Migrazione conserva i pesi");
            Near(upgraded.Cases[0].Overturning, old.Cases[0].Overturning, "Migrazione conserva i momenti");
            Check(upgraded.Cases.Count == old.Cases.Count, "Gruppo legacy conserva otto SLU e tre SLE");
            var d = RetainingWall.Example("gravity"); var q1 = d.Array("actions")[0]!; q1["group"] = ""; q1["psi0"] = .6;
            var q2 = RetainingWall.NewAction(d); q2["value"] = 20; q2["psi0"] = .8; q2["psi1"] = .6; d.Array("actions").Add(q2);
            var combinations = RetainingWall.GenerateCombinations(d);
            Check(combinations.Count(c => c.S("state") == "SLU") == 20, "Due variabili: alternanza principali e omissione favorevoli");
            Check(combinations.Count(c => c.S("state") == "SLE") == 2 && combinations.Count(c => c.S("state") == "SLE_FREQ") == 2, "SLE alterna la variabile principale");
            Check(combinations.Any(c => c.S("state") == "SLU" && c!["coefficients"].D(q1.S("id")) == 1.5 && Math.Abs(c!["coefficients"].D(q2.S("id")) - 1.2) < 1e-9), "ψ₀ specifico della seconda azione");
            Check(combinations.Any(c => c.S("state") == "SLU" && Math.Abs(c!["coefficients"].D(q1.S("id")) - .9) < 1e-9 && c!["coefficients"].D(q2.S("id")) == 1.5), "ψ₀ specifico della prima azione");
            var impact = RetainingWall.NewAction(d, "Urto"); impact["value"] = 40; impact["z"] = 2; d.Array("actions").Add(impact);
            var impact2 = RetainingWall.NewAction(d, "Urto"); impact2["name"] = "Altro evento"; d.Array("actions").Add(impact2);
            var r = RetainingWall.Calculate(d); var accidents = r.Cases.Where(c => c.State == "ECCEZIONALE").ToArray();
            Check(accidents.Length == 2 && accidents.All(c => c.Actions.Count(a => a.Type == "Urto" && a.Factor != 0) == 1), "Eventi eccezionali separati");
            Check(r.Cases.Where(c => c.State != "ECCEZIONALE").All(c => c.Actions.Where(a => a.Type == "Urto").All(a => a.Factor == 0)), "Urto assente nelle combinazioni ordinarie");
            Check(accidents.All(c => c.Actions.Where(a => a.Type == "Sovraccarico uniforme").All(a => a.Factor == .3)), "Eccezionali con ψ₂ delle variabili");
            var a1 = accidents.Single(c => c.Actions.Single(a => a.Id == impact.S("id")).Factor == 1);
            var qp = r.Cases.Single(c => c.State == "SLE_QP"); Near(a1.Horizontal - qp.Horizontal, 40, "Urto: somma delle forze"); Near(a1.Overturning - qp.Overturning, 80, "Urto: braccio assegnato");
            impact["visible"] = false; Near(RetainingWall.Calculate(d).Cases[0].Horizontal, r.Cases[0].Horizontal, "Visibilità non modifica il calcolo");
            impact["enabled"] = false; Check(RetainingWall.GenerateCombinations(d).Count(c => c.S("state") == "ECCEZIONALE") == 1, "Azione esclusa non genera casi");
            d = RetainingWall.Example("gravity"); d.Array("actions").Clear();
            var hor = RetainingWall.NewAction(d, "Forza orizzontale"); hor["value"] = 10; hor["z"] = 1.45; d.Array("actions").Add(hor);
            var moment = RetainingWall.NewAction(d, "Momento"); moment["value"] = 8; moment["z"] = 3.45; d.Array("actions").Add(moment);
            var pressure = RetainingWall.NewAction(d, "Pressione laterale"); pressure["value"] = 4; pressure["z0"] = .45; pressure["z"] = 2.45; d.Array("actions").Add(pressure);
            var def = Single(d); foreach (var a in d.Array("actions")) def["coefficients"]![a.S("id")] = 1;
            var direct = RetainingWall.Calculate(d).Cases[0]; var blank = (JsonObject)d.DeepClone(); foreach (var a in blank.Array("actions")) blank["combinations"]![0]!["coefficients"]![a.S("id")] = 0;
            var baseline = RetainingWall.Calculate(blank).Cases[0];
            Near(direct.Horizontal - baseline.Horizontal, 18, "H puntuale e pressione laterale integrate");
            Near(direct.Overturning - baseline.Overturning, 10 * 1.45 + 8 + 8 * 1.45, "Momento globale indipendente dei carichi diretti");
            Near(direct.Sections.Last(s => s.Name == "Fusto").M - baseline.Sections.Last(s => s.Name == "Fusto").M, 10 + 8 + 8, "Momento alla radice del fusto");
            Check(direct.Sections.Where(s => s.Name == "Fusto").Any(s => s.Position == 2) && direct.Sections.Where(s => s.Name == "Valle").Count() == 21, "Discontinuità dei carichi e diagrammi delle mensole");
            def["state"] = "SLU";
            Check(RetainingWall.Calculate(d).Structural.Any(c => c.Name == "Fusto z=0.00 m · assenza trazione" && c.Status.StartsWith("Non soddisfatta")), "Coppia in testa verificata anche con N e V nulli");
            def["mphi"] = 1.25; def["rslide"] = 1.7;
            var custom = RetainingWall.Calculate(d).Cases[0]; double phi = Math.Atan(Math.Tan(30 * Math.PI / 180) / 1.25) * 180 / Math.PI;
            Near(custom.PressureDetails[0].PhiDesign, phi, "γM applicato a tanφ nel motore");
            Near(custom.PressureDetails[0].K, Math.Pow(Math.Tan((45 - phi / 2) * Math.PI / 180), 2), "Ka da φd ridotto");
            Near(custom.SlidingResistance, custom.Vertical * Math.Tan(26 * Math.PI / 180) / 1.25 / 1.7, "γM e γR personalizzati nella resistenza");
            foreach (var p in custom.PressureDetails) { Near(p.Total0, p.Soil0 + p.Surcharge + p.Water0 + p.Dynamic, "Audit somma contributi superiore"); Near(p.Total1, p.Soil1 + p.Surcharge + p.Water1 + p.Dynamic, "Audit somma contributi inferiore"); }
            d.Array("actions")[0]!["psi0"] = .9; Reject(d, "Matrice obsoleta dopo modifica ψ bloccata");
            d["combination_signature"] = RetainingWall.CombinationSignature(d); def["coefficients"]![hor.S("id")] = ""; Reject(d, "Coefficiente vuoto rifiutato");
            var seismic = RetainingWall.Example("gravity"); seismic["seismic"]!["source"] = RetainingWall.SeismicManual; seismic["seismic"]!["enabled"] = true; seismic["seismic"]!["method"] = "Wood semplificato"; seismic["seismic"]!["kv"] = 0;
            var wr = RetainingWall.Calculate(seismic); var wc = wr.Cases.Single(c => c.State == "SISMA"); double ht = 3.45;
            Near(wc.PressureDetails[0].K, .5, "Wood: K₀ per terreno normalmente consolidato");
            Near(wc.PressureDetails[0].Dynamic * ht, .1 * 18 * ht * ht, "Wood: risultante kh γ Ht²");
            Near(wc.PressureDetails[0].Surcharge, .5 * 10 * .3, "Wood: componente statica del sovraccarico esplicita");
            var woodCustom = (JsonObject)seismic.DeepClone(); var woodDef = Single(woodCustom, "SISMA"); woodDef["soil"] = 1.3;
            Near(RetainingWall.Calculate(woodCustom).Cases[0].PressureDetails[0].Dynamic, wc.PressureDetails[0].Dynamic * 1.3, "γ terreno personalizzato applicato anche alla componente sismica");
            seismic["seismic"]!["method"] = "Mononobe–Okabe"; seismic["seismic"]!["kh"] = 0; var mo = RetainingWall.Calculate(seismic).Cases.Single(c => c.State == "SISMA");
            Near(mo.PressureDetails[0].K, 1d / 3, "MO usa Ka"); Near(mo.PressureDetails[0].Dynamic, 0, "MO a kh=0 non genera incremento");
            var rc = RetainingWall.Defaults(); rc["geometry"]!["stem_top"] = .4; rc["reinforcement"]!["two_zones"] = true; rc["reinforcement"]!["lower_height"] = 1.2; rc["reinforcement"]!["stem_upper"]!["diameter"] = 12;
            Near(RetainingWall.SectionInput(rc, "Fusto", .4, 1).D("top_bar_diameter_mm"), 12, "Sezione superiore usa Ø12");
            Near(RetainingWall.SectionInput(rc, "Fusto", .4, 2).D("top_bar_diameter_mm"), 16, "Sezione inferiore usa Ø16");
            var two = RetainingWall.Calculate(rc); var oneInput = (JsonObject)rc.DeepClone(); oneInput["reinforcement"]!["two_zones"] = false; var one = RetainingWall.Calculate(oneInput);
            Near(one.SteelKg - two.SteelKg, 2 * 6 * Math.PI * (16 * 16 - 12 * 12) / 4 * 1e-6 * 1.8 * 7850, "Quantità acciaio per due zone");
            Check(two.Cases[0].Sections.Count(s => s.Name == "Fusto" && Math.Abs(s.Position - 1.8) < 1e-6) >= 2, "Entrambi i lati del cambio armatura campionati");
            var cmp = two.Cases.First(c => c.State == "SLU").Name;
            double upperR = two.Structural.First(c => c.Combination == cmp && c.Name.StartsWith("Fusto z=0.15") && c.Name.EndsWith("N–M GPC")).Resistance!.Value;
            double uniformR = one.Structural.First(c => c.Combination == cmp && c.Name.StartsWith("Fusto z=0.15") && c.Name.EndsWith("N–M GPC")).Resistance!.Value;
            Check(upperR < uniformR, "Motore GPC distingue le armature anche a spessore costante");
            var section = RetainingWall.ExportSection(two, "Fusto", .15, cmp);
            var original = two.Cases.Single(c => c.Name == cmp).Sections.Single(s => s.Name == "Fusto" && s.Position == .15);
            Near(section["input"].D("width_mm"), 1000, "Trasferimento fascia di un metro"); Near(section["input"].D("height_mm"), 400, "Trasferimento spessore in mm");
            Near(section["input"].D("top_bar_diameter_mm"), 12, "Trasferimento armatura superiore effettiva");
            Near(section["input"].D("axial_force_kn"), -original.N, "Trasferimento N negativo a compressione"); Near(section["input"].D("moment_x_knm"), original.M, "Trasferimento Mx senza doppia combinazione");
            Near(section["workspace_ca"]!["taglio"]!["azioni"]![0].D("Vy"), original.V, "Trasferimento Vy associato a Mx");
            Check(SectionWorkspace.Sets.Sum(set => section["combinazioni"]!.Array(set).Count) == two.Cases.Count, "Trasferimento tutte le combinazioni della sezione");
            Near(section["workspace_ca"]!["sle_comuni"].D("phi"), 2, "Trasferimento viscosità SLE");
            var exportCopy = section.ToJsonString(); SectionWorkspace.Prepare(section); Check(section.ToJsonString() == exportCopy, "Riapertura sezione senza doppia inversione di N");
            section["input"]!["fck_mpa"] = 40; Near(two.Input["materials"].D("fck"), 30, "Sezione esportata indipendente dal muro");
            var archive = Archivio.Documento(RetainingWall.Module); archive["dati"] = rc.DeepClone(); string path = Path.Combine(directory, "due-zone.anthea"); Archivio.Scrivi(path, archive);
            Check(J.Equivalent(Archivio.Leggi(path)["dati"], rc), "Archivio v2 conserva azioni e due armature");
            File.WriteAllBytes(Path.Combine(directory, "azioni.docx"), ReportRetainingWall.Create("Muro con azioni e due armature", two));
            File.WriteAllText(Path.Combine(directory, "azioni.json"), two.Json().ToJsonString(J.Options));
            Console.WriteLine($"PASS {log.Count} controlli azioni/combinazioni"); return log.Count;
        }
        finally { File.WriteAllLines(Path.Combine(directory, "azioni-test.txt"), log); }
    }
}

