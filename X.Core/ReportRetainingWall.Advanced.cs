namespace X.Core;

public static partial class ReportRetainingWall
{
    private static void WriteAdvanced(WallDocument doc, RetainingWall.Result result)
    {
        var d = result.Input;
        doc.P("Portanza sismica con inerzia del terreno", true);
        doc.P("EN 1998-5:2004 allegato F, modello drenato per fondazione nastriforme su terreno granulare asciutto. Nmax=0,5 γ(1−av/g) B² Nγ; Nγ=2(Nq−1)tanφd. Ricoprimento favorevole escluso. Il dominio considera insieme N, V e M, senza ulteriori riduzioni di B o inclinazione. F=γRD ah/(g tanφd); N̄=γRD γR N/Nmax; V̄=γRD γR |V|/Nmax; M̄=γRD γR |M|/(B Nmax).");
        doc.P("I=[(1−0,41F)^1,14(2,90V̄)^1,14+(1−0,32F)^1,01(2,80M̄)^1,01] / [N̄^0,92 ((1−0,96F)^0,39−N̄)^1,25] ≤1. 0<N̄<(1−0,96F)^0,39. Il tasso η è l’inverso del moltiplicatore limite lungo il raggio N,V,M; non coincide con I. γR è quello della combinazione; γRD=" + d["bearing_seismic"].S("model_factor") + ". Origine accelerazione: " + d["bearing_seismic"].S("source") + ". Da sito: ah/g=ag/g·Ss·St, |av|/g=0,5 ah/g; assegnata: ah/g=" + d["bearing_seismic"].S("ground_kh") + ", |av|/g=" + d["bearing_seismic"].S("ground_kv") + ".");
        doc.Table(["Caso", "Nmax / F", "N̄ / V̄ / M̄", "I / η", "Esito"], result.Cases.Where(c => c.State == "SISMA").Select(c => c.SeismicBearing is { } b
            ? new[] { c.Name, F(b.NMax) + " kN/m / " + F(b.SoilInertia), F(b.NBar) + " / " + F(b.VBar) + " / " + F(b.MBar), (b.Interaction is double i ? F(i) : "Fuori dominio") + " / " + (b.Ratio is double q ? F(q) : "—"), b.Status }
            : new[] { c.Name, "—", "—", "—", c.SeismicBearingError ?? "Non disponibile" }), [2, 1.5, 2, 1.4, 2.5]);
        doc.P("Cedimenti e spostamenti in esercizio", true);
        var opt = d["serviceability"]!;
        doc.P($"Cedimenti richiesti: {opt.B("settlement")}; spostamenti richiesti: {opt.B("displacement")}. Pressione rimossa={opt.S("removed_pressure")} kPa; limite cedimento={opt.S("settlement_limit")} mm, rotazione={opt.S("rotation_limit")} rad, spostamento testa={opt.S("head_limit")} mm. Rigidezza orizzontale per metro={opt.S("horizontal_stiffness")} kN/m². Substrato rigido documentato: {opt.B("rigid_base")}.");
        doc.P("Cedimento finale edometrico s=∫Δσz/M dz; tensioni Boussinesq della striscia con contatto lineare, al netto del terreno rimosso. Moduli assegnati e costanti per strato; niente storia di consolidazione o tempo di drenaggio. Confronto fra due discretizzazioni; profondità sufficiente a Δσz≤10% del carico netto o substrato rigido documentato. La rotazione deriva dal profilo di cedimento libero; non è una soluzione accoppiata della fondazione rigida. Flessione del fusto: doppia integrazione delle curvature GPC fessurate con viscosità per il c.a.; modello elastico senza trazione per gravità. u totale = u fusto + H/K − θ Hmuro. I limiti sono assegnati dal progettista.");
        doc.Table(["Strato sotto posa", "Spessore [m]", "M edometrico [kPa]"], opt.Array("layers").Select(l => new[] { l.S("name"), l.S("thickness"), l.S("modulus") }), [3, 1.5, 2]);
        if (result.Serviceability is { } service)
        {
            doc.Table(["Caso", "s valle/centro/monte [mm]", "θ [rad]", "u fusto/totale [mm]", "Stato"], service.Cases.Select(c => new[] { c.Combination, $"{c.ToeSettlement:0.###} / {c.CentreSettlement:0.###} / {c.HeelSettlement:0.###}", $"{c.FoundationRotation:0.000000}", $"{c.StemDisplacement:0.###} / {c.HeadDisplacement:0.###}", c.Status }), [2, 2, 1.2, 1.7, 3]);
            doc.P("Contributi degli strati lungo la verticale centrale", true);
            doc.Table(["Caso / strato", "z₀–z₁ [m]", "M [kPa]", "Δs [mm]"], service.Cases.SelectMany(c => c.Slices.GroupBy(s => new { s.Soil, s.Modulus }).Select(group => new[] { c.Combination + " / " + group.Key.Soil, F(group.Min(s => s.Top)) + "–" + F(group.Max(s => s.Bottom)), F(group.Key.Modulus), F(group.Sum(s => s.SettlementMm)) })), [3, 2, 1.5, 1.5]);
            doc.P("Spostamenti sismici permanenti Newmark", true);
            doc.P("Integrazione esatta per tratti lineari di a(t)−ky·g, con velocità non negativa verso valle e arresto dopo il termine della registrazione. Modello di blocco rigido libero; escluso Wood. ky/g e compatibilità delle storie con sito e stato limite sono responsabilità dell’analisi geotecnica. Ogni storia SLD o SLV viene controllata singolarmente: non viene dedotta la conformità normativa dell’insieme di accelerogrammi dal solo ag/g.");
            doc.Table(["Storia / stato", "ky/g / scala / PGA/g", "Campioni", "d / limite [mm]", "Esito"], service.Earthquakes.Select(e => new[] { e.Name + " / " + e.State, F(e.YieldG) + " / " + F(e.Scale) + " / " + (e.History is { } h ? F(h.PgaG) : "—"), e.History?.Points.Count.ToString() ?? "—", (e.History is { } h2 ? F(h2.DisplacementMm) : "—") + " / " + F(e.LimitMm), e.Status }), [2, 2, .8, 1.5, 3]);
            doc.P("I campioni integrati completi e le curvature sono interrogabili nelle tabelle del modulo e conservati nel risultato JSON; gli accelerogrammi di input restano nel file del muro.");
        }
        if (result.Detailing is { } detail)
        {
            doc.P("Distinta ferri del tratto di muro", true);
            try { ScheduleTables(doc, d, RetainingWall.CalculateBarSchedule(result)); }
            catch (ArgumentException ex) { doc.P("Distinta da completare: " + ex.Message); }
            doc.P("Distinta di predimensionamento delle armature", true);
            doc.P(detail.Note + " Dettagli inclusi nel riepilogo: " + d["detailing"].B("enabled") + ". Principale fusto: monte/valle; solette: inferiore/superiore. Barre principali e secondarie disposte sulle due facce.");
            doc.Table(["Marca / zona / faccia", "n Ø / As [mm²/m]", "L [m]", "lbd richiesto/usato [mm]", "l0 richiesto/usato [mm]"], detail.Bars.Select(b => new[] { b.Mark + " / " + RetainingWall.RebarZoneName(b.Zone) + " / " + b.Face, $"{b.Count} Ø{b.Diameter} / {F(b.Area)}", F(b.Length), F(b.RequiredAnchor) + " / " + F(b.Anchor), F(b.RequiredLap) + " / " + F(b.Lap) }), [2.5, 1.6, .8, 1.8, 1.8]);
            doc.Table(["Marca", "Mandrino [mm]", "fbd [MPa]", "Interasse [mm]"], detail.Bars.Select(b => new[] { b.Mark, F(b.Mandrel), F(b.Fbd), F(b.Spacing) }), [1, 2, 2, 2]);
            doc.Table(["Zona", "Rete secondaria Ø / passo [mm]"], new[] { "stem", "stem_upper", "toe", "heel" }.Where(k => k != "stem_upper" || d["reinforcement"].B("two_zones")).Select(k => new[] { k, d["reinforcement"]![k].S("secondary_diameter") + " / " + d["reinforcement"]![k].S("secondary_spacing") }), [3, 4]);
            doc.P($"Quantità complessiva stimata: {F(detail.SteelKg)} kg/m. Aderenza buona: {d["detailing"].B("good_bond")}; aggregato {d["detailing"].S("aggregate")} mm; vita {d["detailing"].S("life")} anni; tolleranza copriferro {d["detailing"].S("cover_deviation")} mm. Giunzioni: {d["detailing"].S("lap_percent")}% alla stessa quota; collegamenti Ø{d["detailing"].S("tie_diameter")}/{d["detailing"].S("tie_spacing")} mm. Ancoraggi calcolati a fyd, α1…α5=1; sovrapposizioni con α6 e minimi NTC. Mandrino controllato per piegatura acciaio e pressione CLS.");
            doc.Table(["Dettaglio", "Richiesto / disponibile", "η", "Esito"], detail.Checks.Select(c => new[] { c.Name, F(c.Demand) + " / " + (c.Resistance is double v ? F(v) : "—") + " " + c.Unit, c.Ratio is double v2 ? F(v2) : "—", c.Status }), [3, 2, 1, 2]);
        }
        if (d.S("family") == "gravity")
        {
            doc.P("Materiale del muro a gravità e stabilità strutturale", true);
            doc.Table(["Parametro", "Input"], d["gravity_design"]!.AsObject().Select(p => new[] { p.Key, p.Value?.ToString() ?? "" }), [3, 4]);
            doc.P("CLS non armato: proprietà GPC, NTC 4.1.11 per compressione e taglio; fct1d=0,85 fctk,0.05/γc. Muratura: blocco compresso 0,85 fk/(γM FC), scorrimento giunti min(fvk0+0,4σ; fvk,lim)/(γM FC). Imperfezione ≥H/200. Mensola libera con lunghezza efficace ≥2H ed EI minimo: amplificazione 1/(1−N/Ncr), limitata a N<0,8Ncr e assenza di trazione; altrimenti il controllo resta fuori campo. Nessuna resistenza a trazione della muratura.");
        }
        doc.P("Fonti dei nuovi modelli: JRC Eurocode 8 Seismic Design of Buildings Worked Examples (2011), §4.8; JRC Eurocode 2 Background and Applications, Arrieta, Anchorage and lap splicing (2011); USACE EM 1110-1-1905 (2025); USGS SIR 2007-5196; NTC 2018 §§4.1.11 e 7.8.2.2.3. I motori geotecnici sono separati dall’interfaccia e dai materiali GPC.");
    }
}
