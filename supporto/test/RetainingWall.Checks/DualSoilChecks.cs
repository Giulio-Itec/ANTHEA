using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Calculations.Geotechnics;
using X.Core;

internal static class DualSoilChecks
{
    internal static int Run(string directory)
    {
        var log = new List<string>();
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); log.Add("OK " + message); }
        void Near(double value, double expected, string message, double tolerance = 1e-8) => Check(Math.Abs(value - expected) <= tolerance * Math.Max(1, Math.Abs(expected)), message + $" = {value:G12}");
        void Reject(JsonObject input, string message) { try { RetainingWall.Calculate(input); } catch (ArgumentException) { Check(true, message); return; } throw new Exception("Accettato: " + message); }
        JsonObject Base()
        {
            var d = RetainingWall.Defaults(); d["geometry"]!["stem_base"] = .4; d["geometry"]!["stem_top"] = .4;
            foreach (var a in d.Array("actions")) a!["value"] = 0;
            return d;
        }
        var dry = Base(); var original = RetainingWall.Calculate(dry).Cases[0];
        var d = (JsonObject)dry.DeepClone(); d["valley"]!["height_mode"] = "Assegnato"; d["valley"]!["free_height"] = 2.0;
        var r = RetainingWall.Calculate(d); var c = r.Cases[0]; double dv = 1.45, t = .45, aToe = .8;
        Near(RetainingWall.ValleyHeight(d), dv, "Quota di valle dalla parte libera");
        Near(c.Vertical - original.Vertical, 18 * (dv - t) * aToe, "Peso terreno su mensola a valle rettangolare");
        Near(c.Stabilizing - original.Stabilizing, 18 * (dv - t) * aToe * aToe / 2, "Momento indipendente del terreno a valle");
        Near(c.Horizontal, original.Horizontal, "Passiva disattivata non riduce la spinta");
        Near(c.SoilAudit.D("q_ricoprimento_kPa"), 18 * dv, "Ricoprimento efficace nella portanza");
        double f = 34 * Math.PI / 180, nq = Math.Exp(Math.PI * Math.Tan(f)) * Math.Pow(Math.Tan(Math.PI / 4 + f / 2), 2), inc = 1 - c.Horizontal / c.Vertical;
        Near(c.BearingResistance!.Value, 18 * dv * c.EffectiveWidth * nq * inc * inc + .5 * 19 * c.EffectiveWidth * c.EffectiveWidth * 2 * (nq - 1) * Math.Tan(f) * inc * inc * inc, "Portanza con termine q′Nq iq indipendente");
        Check(r.Cases.Where(x => x.State == "SLU").Select(x => (x.SoilFactor, x.Factors.D("valley_soil"))).Distinct().Count() == 4, "γ monte e valle inviluppati indipendentemente");
        var wet = (JsonObject)d.DeepClone(); wet["water"]!["enabled"] = true; wet["water"]!["depth"] = 1; wet["water"]!["front_head"] = 1;
        var wc = RetainingWall.Calculate(wet).Cases[0];
        Near(wc.SoilAudit.D("q_ricoprimento_kPa"), 18 * .45 + (20 - 9.81) * 1, "Ricoprimento stratificato dalla falda di valle");
        Near(wc.SoilAudit.D("peso_valle_kN_m"), .8 * (.45 * 18 + .55 * 20), "Peso saturo totale sopra mensola");
        d["valley"]!["passive"] = true; d["valley"]!["mobilization"] = .4;
        var pc = RetainingWall.Calculate(d).Cases[0];
        double passive = .4 * .5 * 18 * 3 * dv * dv;
        Near(pc.SoilAudit.D("passiva_usata_kN_m"), passive, "Integrale passiva Rankine con quota mobilitata");
        Near(pc.Horizontal, original.Horizontal - passive, "Passiva non duplicata nella resistenza allo scorrimento");
        Near(original.Overturning - pc.Overturning, passive * dv / 3, "Momento della passiva rispetto al piano di posa");
        d["valley"]!["free_height"] = .1; d["valley"]!["mobilization"] = 1;
        var limited = RetainingWall.Calculate(d).Cases[0]; Near(limited.Horizontal, 0, "Passiva limitata senza invertire la spinta netta");
        var wedge = Base(); wedge["geometry"]!["stem_top"] = .2; wedge["valley"]!["height_mode"] = "Assegnato"; wedge["valley"]!["free_height"] = 2;
        var mass = RetainingWall.FrontSoilMass(wedge, RetainingWall.ValleyBands(wedge), wedgeOnly: true);
        Near(mass.Weight, 18 * .5 * (.2 / 3), "Cuneo sopra paramento inclinato: area triangolare GPC");
        Near(mass.MomentX / mass.Weight, .8 + (.2 / 3) / 3, "Baricentro orizzontale indipendente del cuneo");
        Near(mass.MomentY / mass.Weight, .45 + 2d / 3, "Baricentro verticale indipendente del cuneo");
        var friction = Base(); friction["interfaces"]!["base_mode"] = "Prefabbricato liscio"; friction["interfaces"]!["base_phi_cv"] = 30;
        Near(RetainingWall.InterfaceDelta(friction, false, 1.25), 2d / 3 * Math.Atan(Math.Tan(Math.PI / 6) / 1.25) * 180 / Math.PI, "Automatico riduce φcv prima di applicare k");
        var fc = RetainingWall.Calculate(friction).Cases[0]; Near(fc.SlidingResistance, fc.Vertical * Math.Tan(20 * Math.PI / 180), "Scorrimento usa il δ automatico");
        friction["interfaces"]!["base_mode"] = "Liscio"; var smooth = RetainingWall.Calculate(friction); Near(smooth.Cases[0].SlidingResistance, 0, "Interfaccia liscia a resistenza nulla");
        Check(smooth.Cases.All(x => x.BearingResistance is null), "Portanza base liscia dichiarata fuori campo");
        friction["interfaces"]!["base_mode"] = "Assegnato"; friction["interfaces"]!["wall_delta"] = 20;
        var wallRough = RetainingWall.Calculate(friction).Cases[0]; Near(wallRough.Horizontal, original.Horizontal, "Attrito interno non modifica il piano virtuale con mensola");
        Check(wallRough.Sections.Last(x => x.Name == "Fusto").N > original.Sections.Last(x => x.Name == "Fusto").N, "Attrito aggiunge N al fusto");
        Check(wallRough.Sections.Last(x => x.Name == "Fusto").V < original.Sections.Last(x => x.Name == "Fusto").V, "Coulomb riduce H sul fusto");
        foreach (double phi in new[] { 20d, 30, 40 }) Near(RetainingWall.ActiveHorizontal(phi, 0), RetainingWall.Ka(phi), "Coulomb liscio = Rankine φ=" + phi);
        Near(RetainingWall.ActiveHorizontal(30, 20, 0, 0), RetainingWall.ActiveHorizontal(30, 20), "MO a sisma nullo = Coulomb");
        Near(RetainingWall.ActiveHorizontal(30, 0, .12, .05), RetainingWall.SeismicKa(30, .12, .05), "MO liscio compatibile con motore precedente");
        friction["geometry"]!["heel"] = 0; var noHeel = RetainingWall.Calculate(friction).Cases[0];
        Near(noHeel.Horizontal, .5 * 18 * 3.45 * 3.45 * RetainingWall.ActiveHorizontal(30, 20), "Senza mensola Coulomb sul piano reale");
        friction["interfaces"]!["wall_delta"] = 50; Reject(friction, "δ muro maggiore di φ rifiutato");
        friction["interfaces"]!["wall_delta"] = 20; friction["interfaces"]!["base_mode"] = "Gettato in opera"; friction["interfaces"]!["base_phi_cv"] = 40; Reject(friction, "φcv maggiore di φ′ rifiutato");
        var seismic = (JsonObject)d.DeepClone(); seismic["seismic"]!["enabled"] = true; seismic["seismic"]!["source"] = RetainingWall.SeismicManual;
        Check(RetainingWall.Calculate(seismic).Cases.Where(x => x.State == "SISMA").All(x => x.SoilAudit.D("passiva_usata_kN_m") == 0), "Nessuna passiva statica favorevole nel sisma");
        foreach (double free in new[] { -1d, 4d }) { var bad = (JsonObject)d.DeepClone(); bad["valley"]!["free_height"] = free; Reject(bad, "Hlib fuori campo " + free); }
        var shortLayer = (JsonObject)d.DeepClone(); shortLayer["valley"]!["layers"]![0]!["thickness"] = .2; Reject(shortLayer, "Copertura valle insufficiente rifiutata");
        var linked = Base(); linked["valley"]!["linked"] = true; linked["layers"]![0]!["phi"] = 27;
        Near(RetainingWall.ValleyLayers(linked)[0].D("phi"), 27, "Motore risolve la colonna collegata dalla sorgente monte");
        RetainingWall.CopySoilColumn(linked, false); linked["valley"]!["linked"] = false; linked["layers"]![0]!["phi"] = 29;
        Near(RetainingWall.ValleyLayers(linked)[0].D("phi"), 27, "Scollegamento conserva copie indipendenti");
        var profile = SoilProfileTransfer.Extract(RetainingWall.Module, linked, 1); Check(profile.S("datum").Contains("valle"), "Trasferimento identifica il riferimento di valle");
        Near(profile["layers"]![0].D("angolo_attrito"), 27, "Trasferimento usa materiali valle");
        Check(SoilProfileTransfer.SurveyCount(RetainingWall.Module, linked) == 2, "Due profili trasferibili");
        var applied = SoilProfileTransfer.Apply(RetainingWall.Module, linked, profile, 0); Near(applied["layers"]![0].D("phi"), 27, "Importazione nella colonna selezionata");
        // Independent analytic volume under a circular arc, two different densities.
        var section = new SlopeSection([new(-4, 2), new(4, 2)], [new("Monte", -20, 18, 20, 30, 0, 0)], [], [], [], -.1, .1)
            { ValleySoils = [new("Valle", -20, 24, 25, 25, 0, 0)], SoilSplitX = 0 };
        var factors = new SlopeFactors("test", 1, 1, 1, 1, 1, 1, 0, 0, false, new Dictionary<string, double>());
        var slices = SlopeStability.Slices(section, new(0, 5, 5, -4, 4), factors, 100);
        double area = -12 + 25 * Math.Asin(.8);
        Near(slices.Sum(x => x.SoilWeight), area * 21, "Massa delle due colonne Bishop da integrale analitico", 1e-6);
        Check(slices.Where(x => x.Right <= 0).All(x => x.Soil == "Valle" && x.Phi == 25) && slices.Where(x => x.Left >= 0).All(x => x.Soil == "Monte"), "Resistenza alla base dei conci dalla colonna corretta");
        RetainingWall.PrepareGlobalProfile(wedge); var global = wedge["global_stability"]!;
        Near(global["valley"]![1].D("y"), 1.45, "Profilo globale raccordato alla quota di valle");
        Check(global.S("soil_mode") == "Due colonne" && global.Array("valley_layers").Count == 1, "Precompilazione globale conserva le due colonne");
        Check(!global.B("profile_confirmed"), "Precompilazione richiede conferma rilievo");
        global["enabled"] = true; global["profile_confirmed"] = true; global["grid"] = 5; global["slices"] = 40; global["refinements"] = 1; global["depth_max"] = 4;
        global["valley_layers"]![0]!["phi"] = 24; global["valley_layers"]![0]!["gamma"] = 20;
        var both = RetainingWall.Calculate(wedge);
        Check(both.GlobalError is null && both.GlobalStability is { Cases.Length: > 0 }, "Calcolo completo con valle rialzata e due colonne globali");
        Check(both.GlobalStability!.Cases.All(x => x.Critical is not null), "Ricerca Bishop risolta con due colonne");
        Check(both.GlobalStability.Section.Soils[0].Phi == 30 && both.GlobalStability.Section.ValleySoils[0].Phi == 24, "Parametri profondi indipendenti conservati nel risultato");
        var twoSave = Archivio.Documento(RetainingWall.Module); twoSave["dati"] = both.Input.DeepClone(); Archivio.Scrivi(Path.Combine(directory, "due-colonne-globale.anthea"), twoSave);
        File.WriteAllText(Path.Combine(directory, "due-colonne-globale.json"), both.Json().ToJsonString(J.Options));
        File.WriteAllBytes(Path.Combine(directory, "due-colonne-globale.docx"), ReportRetainingWall.Create("Due colonne e stabilità globale", both));
        var save = Archivio.Documento(RetainingWall.Module); save["dati"] = r.Input.DeepClone(); string path = Path.Combine(directory, "due-colonne.anthea"); Archivio.Scrivi(path, save);
        var restored = Archivio.Leggi(path)["dati"]!.AsObject(); Near(RetainingWall.Calculate(restored).Cases[0].Vertical, c.Vertical, "Salvataggio e rilettura numericamente invarianti");
        File.WriteAllText(Path.Combine(directory, "due-colonne-risultati.json"), r.Json().ToJsonString(J.Options));
        File.WriteAllBytes(Path.Combine(directory, "due-colonne.docx"), ReportRetainingWall.Create("Due colonne · caso riproducibile", r));
        File.WriteAllLines(Path.Combine(directory, "due-colonne-test.txt"), log); Console.WriteLine($"PASS {log.Count} due colonne / attriti"); return log.Count;
    }
}
