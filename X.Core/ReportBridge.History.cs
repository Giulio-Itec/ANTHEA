using GPC.Checkers.CompositeBridge.History;

namespace X.Core;

public static partial class ReportBridge
{
    private static byte[] CreateHistory(string title, BridgeResult result, HashSet<string> options, IReadOnlyList<ImmagineReport>? images)
    {
        var doc = new Document(); var m = result.Materials; var g = result.Geometry;
        string F(double x) => EngineeringFormat.Number(x);
        string E(double x) => x.ToString("0.#####E+0", System.Globalization.CultureInfo.GetCultureInfo("it-IT"));
        doc.P("Analisi della sezione composta con storico", "Title"); doc.P(title, "Subtitle");
        doc.P("ANTHEA · " + result.Method);
        doc.P("Il report descrive gli stati successivi della sezione, conservando deformazioni al getto, deformazioni imposte e memoria plastica dell’acciaio. Le risultanti sono equilibrate a ogni fase. L’analisi non assegna un esito globale di verifica del ponte.");
        doc.H("Modello e campo di applicazione"); doc.P(result.Scope);
        doc.P("N positivo in trazione; M positivo comprime le fibre superiori. y=0 all’interfaccia, positivo verso la soletta. ε(y)=ε₀−κy. N e V in kN, M in kNm; tensioni in MPa. Gli ingressi sono incrementi già combinati e coefficientati. Nessun ulteriore γF è applicato.");
        foreach (string warning in result.Stages.SelectMany(s => s.Warnings).Distinct()) doc.P(warning);
        if (options.Contains("normativa"))
        {
            doc.P("Riferimento degli ingressi: " + result.Input.S("normativa") + "; confronto tensionale: " + result.Input.S("stato") + ". Nel non lineare i legami sono caratteristici e i coefficienti di resistenza non modificano le curve. Una tensione inferiore al limite non attesta la capacità della sezione.");
            doc.Table(["Opzione numerica", "Valore"], new[] {
                new[] { "Strisce anima / flange / CLS", result.Input.S("fibre_anima", "160") + " / " + result.Input.S("fibre_flange", "8") + " / " + result.Input.S("fibre_cls", "64") },
                new[] { "Punti di integrazione", "2 per striscia" }, new[] { "Sottopassi per fase", result.Input.S("sottopassi", "8") },
                new[] { "Viscosità non lineare", result.Input.S("viscosita_nl", BridgeSection.NonlinearCreepModes[0]) } }, [2, 3]);
        }
        if (options.Contains("materiali"))
        {
            doc.H("Materiali da Model");
            doc.Table(["Materiale", "Classe", "f caratteristica [MPa]", "E iniziale [MPa]"], new[] {
                new[] { "Carpenteria", m.Steel, F(m.Fy), F(m.Ea) }, new[] { "Calcestruzzo", m.Concrete, F(m.Fck), F(m.Ec) }, new[] { "Armature", m.Rebar, F(m.Fys), F(m.Es) } }, [1.3, 1.3, 1.5, 1.5]);
        }
        if (options.Contains("geometria"))
        {
            doc.H("Geometria della sezione");
            doc.Table(["Parte", "Larghezza [mm]", "Spessore o altezza [mm]"], new[] {
                new[] { "Soletta", F(g.Width), F(g.SlabHeight) }, new[] { "Anima", F(g.WebThickness), F(g.WebHeight) },
                new[] { "Piattabanda superiore", F(g.TopWidth), F(g.TopThickness) }, new[] { "Piattabanda inferiore 1", F(g.Bottom1Width), F(g.Bottom1Thickness) } }.Concat(g.Bottom2Thickness > 0 ? new[] { new[] { "Piattabanda inferiore 2", F(g.Bottom2Width), F(g.Bottom2Thickness) } } : []), [2, 1, 1.3]);
            doc.P("CLS netto delle barre. Le due piastre inferiori, se presenti, sono modellate con la geometria reale.");
            doc.Table(["Fila armature", "Barre", "Diametro [mm]", "Area [mm²]"], g.Bars.GroupBy(b => b.Y).Select(row => new[] { "y = " + F(row.Key) + " mm", row.Count().ToString(), F(row.First().Diameter), F(row.Sum(b => b.Area)) }), [2, 1, 1, 1]);
            doc.Figures(images, options, "geometria");
        }
        foreach (var (stage, index) in result.Stages.Select((s, i) => (s, i)))
        {
            var h = stage.GetHistory()!; var s = h.State; var c = stage.Contributions.Last();
            doc.H($"Fase {index + 1} {s.Name}");
            if (options.Contains("azioni"))
            {
                doc.Table(["Azioni", "N [kN]", "M a y=0 [kNm]", "V [kN]"], new[] {
                    new[] { "Incremento", F(c.N), F(c.MomentAtInterface), F(c.V) }, new[] { "Totale", F(s.N / 1000), F(s.MomentAtOrigin / 1e6), F(s.V / 1000) } }, [1.3, 1, 1.3, 1]);
                doc.P("Quota applicazione incremento N: " + F(s.IncrementApplicationY) + " mm. Riferimento: " + c.LoadReference + ". Incremento di ritiro: " + F(c.ShrinkageStrain * 1e6) + " µε, applicato soltanto al CLS.");
            }
            if (options.Contains("omogeneizzazione"))
            {
                doc.Table(["Piano", "ε₀ [µε]", "κ [1/m]"], new[] { new[] { "Totale", F(s.TotalPlane.AxialStrain * 1e6), E(s.TotalPlane.Curvature * 1000) }, new[] { "Incremento", F(s.IncrementPlane.AxialStrain * 1e6), E(s.IncrementPlane.Curvature * 1000) } }, [1.4, 1.5, 1.5]);
                doc.P("n applicato = " + (c.HasConcrete ? F(c.HomogenizationN) : "—") + "; φ applicato = " + F(c.Phi) + ". A*=" + F(h.TransformedArea) + " mm²; yG=" + F(h.ElasticCentroid) + " mm; Ix*=" + E(h.TransformedInertia) + " mm⁴. Proprietà ai moduli elastici della fase, non rigidezza tangente non lineare.");
            }
            doc.P($"Equilibrio: residuo N={E(s.ForceResidual)} N; residuo M={E(s.MomentResidual)} Nmm. Iterazioni Newton={s.NewtonIterations}; iterazioni aree={s.EffectiveIterations}; variazione aree={E(s.EffectiveResidual)}.");
            if (options.Contains("tensioni"))
            {
                doc.Table(["Punto", "y [mm]", "σ [MPa]", "Ultimo Δσ [MPa]"], stage.Points.Select(p => new[] { p.Name, F(p.Y), p.Active ? F(p.Stress) : "—", F(p.Contributions.Last()) }), [2.8, 1, 1, 1]);
                doc.Sub("Deformazioni meccaniche e plastiche");
                doc.Table(["Componente", "εmecc min [µε]", "εmecc max [µε]", "|εpl|max [µε]"], s.Fibers.Where(f => f.Active).GroupBy(f => f.ComponentId).Select(row => new[] {
                    row.Key, F(row.Min(f => f.MechanicalStrain) * 1e6), F(row.Max(f => f.MechanicalStrain) * 1e6), F(row.Max(f => f.MaterialState is HistoryPlasticState p ? Math.Abs(p.PlasticStrain) : 0) * 1e6) }), [1.6, 1.4, 1.4, 1.4]);
                doc.P("εmecc=εtot−εgetto−εimposta. Δσ è la differenza fra stati consecutivi, incluse le redistribuzioni. Valori alle facce da fibra prossima; estremi integrati riportati separatamente. Il JSON e il CSV delle fibre conservano lo storico completo.");
            }
            if (options.Contains("classe4"))
            {
                doc.P(h.Nonlinear ? "Sezione lorda. Instabilità locale e verifica plastica di classe 4 non incluse." : "Carpenteria efficace: A=" + F(stage.EffectiveSteel.Area) + " mm²; yG=" + F(stage.EffectiveSteel.Centroid) + " mm; Ix=" + E(stage.EffectiveSteel.Inertia) + " mm⁴.");
                if (!h.Nonlinear) doc.Table(["Pannello", "ρ", "b1 eff [mm]", "b2 eff [mm]"], s.Panels.Select(p => new[] { p.Name, F(p.Reduction.Rho), F(p.Reduction.EffectiveAtStart), F(p.Reduction.EffectiveAtEnd) }), [2, 1, 1, 1]);
            }
            if (options.Contains("taglio")) doc.P("V registrato. Taglio, pioli, irrigidimenti e interazione N–M–V non verificati dal metodo con storico.");
            doc.Figures(images, options, "fase_" + index);
        }
        return doc.Bytes();
    }
}
