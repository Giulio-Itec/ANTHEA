using System.Text.Json.Nodes;
using Anthea.Calculations;
using GPC.Checkers.Geotechnics.Foundations;
using GPC.Checkers.Geotechnics.Walls;

/// <summary>
/// The adapter of the wall document to GPCChecker.Geotechnics: units of the input (m, kN, kPa, degrees → mm, N, MPa, rad), results equal to the
/// library converted back, names and units of the checks, legacy cases of version 1, errors of the data reported as in the legacy calculation.
/// </summary>
internal static class LibraryAdapterChecks
{
    internal static int Run(string directory)
    {
        var log = new List<string>();
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); log.Add("OK " + message); }
        void Same(double value, double expected, string message) => Check(Math.Abs(value - expected) <= 1e-12 * Math.Max(1, Math.Abs(expected)), message + $" ({value:G17}; atteso {expected:G17})");
        double deg = Math.PI / 180;

        // 1. Input in the units of the library.
        var d = RetainingWall.Defaults();
        foreach (var (type, value) in new[] { ("Forza orizzontale", 3d), ("Momento", 5d), ("Pressione laterale", 7d) })
        {
            var a = RetainingWall.NewAction(d, type); a["value"] = value; a["z0"] = d["geometry"].D("slab"); d.Array("actions").Add(a);
        }
        var input = RetainingWall.ToWallInput(d); var g = d["geometry"]!;
        Same(input.Geometry.Height, g.D("height") * 1000, "Altezza in mm");
        Same(input.Geometry.Toe + input.Geometry.StemBase + input.Geometry.Heel, (g.D("toe") + g.D("stem_base") + g.D("heel")) * 1000, "Larghezza in mm");
        Same(input.UnitWeight, d["materials"].D("gamma") * 1e-6, "Peso del muro in N/mm³");
        Same(input.Backfill.GroundSurface, (g.D("height") + g.D("slab")) * 1000, "Colonna di monte con il piano campagna in testa al muro");
        Same(input.Backfill.Layers[0].Soil.FrictionAngle, d.Array("layers")[0].D("phi") * deg, "φ′ in radianti");
        Same(input.Foundation.SaturatedUnitWeight, d["foundation"].D("gamma_sat") * 1e-6, "γsat del terreno di posa in N/mm³");
        Same(input.Actions.Single(a => a.Type == WallActionType.UniformSurcharge).Value, d.Array("actions")[0].D("value") * 1e-3, "Sovraccarico kPa → MPa");
        Same(input.Actions.Single(a => a.Type == WallActionType.HorizontalForce).Value, 3, "Forza kN/m = N/mm");
        Same(input.Actions.Single(a => a.Type == WallActionType.Moment).Value, 5000, "Momento kNm/m → N·mm/mm");
        Same(input.Actions.Single(a => a.Type == WallActionType.LateralPressure).Value, .007, "Pressione laterale kPa → MPa");
        Check(input.Seismic is null && input.Water is null && input.Valley.Height == 0, "Sisma e falda disattivati, valle libera");

        // 2. Results equal to the library converted back.
        var result = RetainingWall.Calculate(d);
        var rows = WallCombinations.Generate(input); var wall = RetainingWallAnalysis.Calculate(input, rows);
        Check(result.Cases.Count == wall.Cases.Count && result.Cases.Select(c => c.Name).SequenceEqual(rows.Select(c => c.Name)), "Stesse combinazioni della libreria");
        Same(result.Width, wall.Width / 1000, "Larghezza del risultato"); Same(result.Area, wall.Area / 1e6, "Area della sezione m²");
        for (int i = 0; i < wall.Cases.Count; i++)
        {
            var c = result.Cases[i]; var w = wall.Cases[i];
            if (c.Horizontal != w.Horizontal || c.Vertical != w.Vertical || c.Stabilizing != w.Stabilizing / 1000 || c.Overturning != w.Overturning / 1000 || c.X != w.X / 1000
                || c.Contact.Toe != w.Contact.Toe / 1e-3 || c.Contact.End != w.Contact.End / 1000 || c.OverturningResistance != w.OverturningResistance / 1000 || c.BearingResistance != w.BearingResistance
                || c.Sections.Count != w.Sections.Count || c.Sections.Zip(w.Sections).Any(p => p.First.M != p.Second.M / 1000 || p.First.N != p.Second.N || p.First.Position != p.Second.Position / 1000)
                || c.SoilAudit.D("q_ricoprimento_kPa") != w.Soil.Overburden / 1e-3 || c.SoilAudit.D("delta_base_d_gradi") != w.Soil.BaseFriction / deg)
                throw new Exception("Conversione del caso " + c.Name);
        }
        Check(true, "Equilibrio, contatto, resistenze, sollecitazioni e audit di tutte le combinazioni convertiti esattamente");
        var geo = result.Checks.Where(c => c.Name is "Scorrimento" or "Ribaltamento" or "Capacità portante" or "Contatto fondazione").ToList();
        Check(geo.Count == wall.Checks.Count, "Una verifica ANTHEA per ogni verifica della libreria");
        for (int i = 0; i < geo.Count; i++)
        {
            var (a, b) = (geo[i], wall.Checks[i]); double scale = b.Kind is WallCheckKind.Overturning or WallCheckKind.Contact ? 1000 : 1;
            string unit = b.Kind switch { WallCheckKind.Overturning => "kNm/m", WallCheckKind.Contact => "m", _ => "kN/m" };
            if (a.Demand != b.Demand / scale || a.Resistance != b.Resistance / scale || a.Ratio != b.Ratio || a.Status != b.Message || a.Unit != unit || a.Combination != b.Combination) throw new Exception("Verifica " + a.Name + " " + a.Combination);
        }
        Check(true, "Domanda, resistenza, unità, rapporto ed esito delle verifiche geotecniche");
        var generated = RetainingWall.GenerateCombinations(d);
        Check(generated.Count == rows.Count && generated.Zip(rows).All(p => p.First.S("name") == p.Second.Name && p.First.D("wall") == p.Second.Wall && p.First.D("rbearing") == p.Second.BearingFactor
            && d.Array("actions").All(a => p.First!["coefficients"].D(a.S("id")) == p.Second.Coefficients[a.S("id")])), "Righe automatiche del documento = WallCombinations.Generate");
        Check(result.Cases.All(c => c.Factors is not null && c.LiveFactor == 1 && c.Actions.Count == 4), "Documento versione 2: righe e azioni nei casi");

        // 3. Version 1: the fixed cases on the three loads.
        var legacy = RetainingWall.Calculate(RetainingWall.LegacyDefaults());
        Check(legacy.Cases.Select(c => c.Name).SequenceEqual(new[] { "Caratteristica", "Frequente", "Quasi permanente" }.Concat(Enumerable.Range(1, 8).Select(i => "SLU " + i))), "Versione 1: tre SLE e otto SLU");
        Same(legacy.Cases[1].LiveFactor, .5, "Versione 1: ψ1 sul carico frequente"); Same(legacy.Cases[2].LiveFactor, .3, "Versione 1: ψ2 quasi permanente");
        Check(legacy.Cases.All(c => c.Factors is null && c.Actions.Count == 0), "Versione 1: nessuna riga e nessuna azione esposta, come prima");
        var slu = legacy.Cases.First(c => c.State == "SLU");
        Same(slu.SlidingResistance, slu.Vertical * Math.Tan(26 * deg) / 1.1, "Versione 1: γR 1,1 sullo scorrimento con δb del terreno di posa");
        Same(slu.OverturningResistance, slu.Stabilizing / 1.15, "Versione 1: γR 1,15 sul ribaltamento");

        // 4. Methods of the library in the units of the document.
        var contact = RetainingWall.ContactLaw(3, 100, 1.2);
        Same(RetainingWall.Pressure(contact, 0), contact.Toe, "Pressione al piede dalla legge della libreria"); Same(RetainingWall.Pressure(contact, 3.5), 0, "Pressione fuori dalla base");
        Check(contact.Law is not null && contact.Valid, "Contatto con la legge della libreria");
        var integral = RetainingWall.Integrate([new(0, 2, 10, 30)], 0, 2, 0); Same(integral.Force, 40, "Integrale delle pressioni kN/m"); Same(integral.Moment, 10 * 2 + 20 * 4 / 3.0, "Momento delle pressioni kNm/m");
        Same(RetainingWall.InterfaceDelta(d, false), d["foundation"].D("delta"), "Attrito di base assegnato in gradi");
        Check(!result.Json().ToJsonString().Contains("\"Law\""), "La legge della libreria non entra nel JSON del risultato");

        // 5. Seismic: site coefficients and the soil inertia of Annex F with γRd as before.
        var s = RetainingWall.Defaults(); var sd = s["seismic"]!; sd["enabled"] = true; sd["ag_g"] = .2; sd["f0"] = 2.5; sd["soil_class"] = "C";
        var site = RetainingWall.DeriveSeismic(s)!;
        Same(site.Ss, 1.4, "Ss categoria C"); Same(site.AmaxG, .28, "amax/g"); Same(site.Kh, .38 * .28, "kh = βm amax/g"); Same(site.KhOverturning, .57 * .28, "kh ribaltamento con βm,rib = 0,57");
        Check(site.Description.Contains("Categoria C"), "Formula di Ss nella descrizione");
        var seismic = RetainingWall.Calculate(s);
        var bearing = seismic.Cases.First(c => c.State == "SISMA" && c.SeismicBearing is not null).SeismicBearing!;
        Same(bearing.SoilInertia, 1.15 * .28 / Math.Tan(34 * deg), "F̄ con γRd come nel calcolo precedente (RetainingWall.ModelFactorOnSoilInertia)");
        Check(RetainingWall.ModelFactorOnSoilInertia, "Opzione di ANTHEA dichiarata");
        Check(seismic.Cases.Count(c => c.State == "SISMA") == 4 && seismic.Cases.Where(c => c.State == "SISMA").All(c => c.Factors.S("purpose") is "Generale" or "Ribaltamento"), "Sisma dal sito: generale e ribaltamento ±kv");

        // 6. Serviceability: results of the library, data errors as in the legacy calculation.
        var service = RetainingWall.Defaults(); var opt = service["serviceability"]!; opt["settlement"] = true; opt["removed_pressure"] = 0;
        opt["layers"] = new JsonArray(J.Obj(("name", "Sabbia"), ("thickness", 10), ("modulus", 30000)));
        var sr = RetainingWall.Calculate(service); var sinput = RetainingWall.ToWallInput(service); var swall = RetainingWallAnalysis.Calculate(sinput, WallCombinations.Generate(sinput));
        var direct = WallServiceability.Calculate(sinput, swall, new WallServiceOptions(new WallSettlementOptions([new SettlementLayer("Sabbia", 10000, 30)], 0)));
        Check(sr.Serviceability!.Cases.Count == direct.Cases.Count && sr.Serviceability.Cases.Zip(direct.Cases).All(p => p.First.ToeSettlement == p.Second.ToeSettlement && p.First.FoundationRotation == p.Second.Rotation
            && p.First.Slices.Count == p.Second.Slices.Count && p.First.Slices[0].Stress == p.Second.Slices[0].Stress / 1e-3), "Cedimenti e rotazioni della libreria in mm, kPa e m");
        Check(sr.Checks.Count(c => c.Name == "Cedimento edometrico finale" && c.Unit == "mm") == direct.Cases.Count, "Verifica del cedimento per ogni SLE");
        opt["layers"]![0]!["thickness"] = "";
        var invalid = RetainingWall.Calculate(service);
        Check(invalid.Checks.Count(c => c.Name == "Cedimenti" && c.Ratio is null && c.Status.StartsWith("thickness:")) == direct.Cases.Count, "Dato mancante dello strato riportato su ogni SLE");
        Check(invalid.Serviceability!.Cases.All(c => c.ToeSettlement is null && c.Status.StartsWith("thickness:")), "Casi di esercizio senza valori inventati");
        opt["layers"]![0]!["thickness"] = 10; opt["histories"] = new JsonArray(
            J.Obj(("enabled", true), ("name", "Valido"), ("state", "SLV"), ("compatible", true), ("yield_g", .1), ("scale", 1), ("limit_mm", 50), ("samples", new JsonArray(J.Obj(("t", 0), ("a_g", .2)), J.Obj(("t", 1), ("a_g", .2))))),
            J.Obj(("enabled", true), ("name", "Tempo fuori campo"), ("state", "SLV"), ("compatible", true), ("yield_g", .1), ("scale", 1), ("limit_mm", 50), ("samples", new JsonArray(J.Obj(("t", 0), ("a_g", .2)), J.Obj(("t", 20000), ("a_g", .2))))),
            J.Obj(("enabled", true), ("name", "Non confermato"), ("state", "SLV"), ("compatible", false), ("yield_g", .1), ("scale", 1), ("limit_mm", 50), ("samples", new JsonArray(J.Obj(("t", 0), ("a_g", .2)), J.Obj(("t", 1), ("a_g", .2))))));
        var newmark = RetainingWall.Calculate(service).Serviceability!.Earthquakes;
        Check(newmark.Count == 3 && Math.Abs(newmark[0].History!.DisplacementMm - 981) < 1e-6 && newmark[0].Status == "Non soddisfatta", "Newmark della libreria: 981 mm oltre il limite");
        Check(newmark[1].History is null && newmark[1].Status.StartsWith("t: inserire"), "Campione oltre 10000 s rifiutato con il messaggio del documento");
        Check(newmark[2].History is null && newmark[2].Status.StartsWith("Confermare compatibilità"), "Accelerogramma non confermato");

        // 7. Global stability: the result and the checks of the library.
        var gd = RetainingWall.Example("cantilever"); gd["layers"]![0]!["thickness"] = 25; RetainingWall.PrepareGlobalProfile(gd);
        var gs = gd["global_stability"]!; gs["soil_mode"] = "Profilo unico"; gs["enabled"] = true; gs["profile_confirmed"] = true; gs["grid"] = 5; gs["slices"] = 40; gs["refinements"] = 1; gs["depth_max"] = 8;
        var gr = RetainingWall.Calculate(gd); var global = gr.GlobalStability!;
        Check(global.Source is not null && global.Cases.Length == global.Source.Cases.Count, "Risultato globale della libreria conservato");
        var library = WallGlobalStability.Checks(global.Source!); var anthea = gr.Checks.Where(c => c.Name == "Stabilità globale · Bishop").ToList();
        Check(anthea.Count == library.Count && anthea.Zip(library).All(p => p.First.Ratio == p.Second.Ratio && p.First.Resistance == p.Second.Resistance && p.First.Demand == p.Second.Demand), "Verifiche globali della libreria");
        Check(anthea.Zip(global.Cases).All(p => p.First.Status == p.Second.Status && p.First.Status.Length > 0 && !p.First.Status.Contains("Converged")), "Esiti globali in italiano");
        Check(global.Cases.All(c => c.Critical is null || Math.Abs(c.Critical.Circle.Radius - global.Source!.Cases.First(x => x.Factors.Name == c.Factors.Name).Critical!.Circle.Radius / 1000) < 1e-12), "Cerchi critici in metri");
        Check(Math.Abs(global.Section.Soils[0].Gamma - gs.Array("layers")[0].D("gamma")) < 1e-9 && global.Notes.Contains(RetainingWall.GlobalHelp) && global.Notes[0].Contains("In non drenato"), "Sezione e note globali per la relazione");
        Check(gr.Json().ToJsonString().Contains("CoveredBottom"), "JSON globale con le colonne come prima");
        File.WriteAllLines(Path.Combine(directory, "adattatore-libreria.txt"), log); Console.WriteLine($"PASS {log.Count} adattatore GPCChecker.Geotechnics"); return log.Count;
    }
}
