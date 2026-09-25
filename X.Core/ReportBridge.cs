using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace X.Core;

/// <summary>Report of an existing bridge calculation. Never executes or modifies the solver.</summary>
public static class ReportBridge
{
    public static readonly (string Key, string Label)[] Sections = [
        ("normativa", "Normativa e coefficienti"), ("materiali", "Materiali e proprietà"),
        ("geometria", "Geometria e armature"), ("azioni", "Sollecitazioni e fasi"),
        ("omogeneizzazione", "Omogeneizzazione e proprietà per fase"), ("tensioni", "Tensioni totali e contributi"),
        ("classe4", "Sezione efficace e parametri di classe 4"), ("taglio", "Taglio, irrigidimenti e pioli"), ("grafici", "Geometria e diagrammi delle fasi")];
    public static HashSet<string> DefaultSections() => Sections.Select(s => s.Key).ToHashSet();

    public static byte[] Create(string title, BridgeResult result, HashSet<string> options, IReadOnlyList<ImmagineReport>? images = null)
    {
        if (!Sections.Any(s => s.Key != "grafici" && options.Contains(s.Key))) throw new ArgumentException("Selezionare almeno un contenuto del report.");
        if (result.Stages.Count == 0) throw new ArgumentException("Risultati della sezione composta non disponibili.");
        var d = result.Input; var g = result.Geometry; var m = result.Materials;
        var doc = new Document();
        string F(double? x) => x.HasValue ? EngineeringFormat.Number(x.Value) : "—";
        string Input(string key) => J.Number(d[key]) is double value ? F(value) : d.S(key);
        doc.P("Relazione della sezione composta da ponte", "Title"); doc.P(title, "Subtitle");
        doc.P("ANTHEA · " + DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture));
        doc.P("Analisi delle tensioni normali di una soletta in calcestruzzo collegata a una trave in acciaio. " +
            "La relazione riporta i dati e i risultati delle situazioni calcolate, senza eseguire un nuovo calcolo. I rapporti tensionali sono controlli locali della sezione.");
        doc.H("Ambito e convenzioni"); doc.P(result.Scope);
        doc.P("Lunghezze in mm, tensioni in MPa, forze in kN e momenti in kNm. Compressione negativa e trazione positiva. " +
            "L’asse y ha origine all’interfaccia acciaio soletta ed è positivo verso l’alto. Mx positivo comprime la parte superiore. " +
            "Le azioni sono incrementi già combinati e, allo SLU, già coefficientati con γF e ψ. Il modulo non moltiplica nuovamente i carichi: il selettore SLU/SLE cambia limiti e resistenze. Arrotondamenti soltanto nella presentazione.");
        doc.P("Model fornisce materiali, geometria e proprietà. Checker risolve le tensioni elastiche delle fasi composte. " +
            "ANTHEA gestisce le fasi, le larghezze efficaci e l’equilibrio delle fasi di solo acciaio o con soletta interamente esclusa.");
        doc.H("Riepilogo delle situazioni");
        doc.Table(["Situazione", "ΣN [kN]", "ΣMx a y=0 [kNm]", "ΣV [kN]", "η max locale", "Stato locale"], result.Stages.Select((s, i) => {
            var local = (s.Shear?.Checks ?? []).Concat(s.Studs?.Checks ?? []).ToArray();
            double maximum = Math.Max(s.MaxUtilization, local.Where(c => c.Ratio.HasValue).Select(c => c.Ratio!.Value).DefaultIfEmpty().Max());
            bool incomplete = local.Any(c => c.Ratio is null) || s.Points.Any(p => p.Active && p.Utilization is null);
            return new[] { $"{i + 1} · Dopo {s.Name}", F(s.Contributions.Sum(c => c.N)), F(s.Contributions.Sum(c => c.MomentAtInterface)), F(s.Contributions.Sum(c => c.V)), F(maximum),
                maximum > 1 ? incomplete ? "Non verificato / incompleto" : "Non verificato" : incomplete ? "Da completare" : "Entro limiti" }; }), [2.6, .9, 1.1, .8, .9, 1.6]);
        doc.P("η max locale include tensioni, taglio e controlli accessori attivati. Un controllo mancante non riceve un esito favorevole; leggere gli avvisi e il campo delle verifiche. Non è un esito globale del ponte.");
        foreach (var warning in result.Stages.SelectMany(s => s.Warnings).Distinct()) doc.P(warning);

        if (options.Contains("taglio"))
        {
            var accessory = (JsonObject)d.DeepClone(); BridgeSection.EnsureAccessoryDefaults(accessory);
            doc.H("Taglio irrigidimenti e pioli");
            doc.P("Riferimenti: NTC 2018 §§4.2.4.1.2, 4.3.4.2.2 e 4.3.4.3; EN 1993-1-5:2006 §§5, 7.1, 9; EN 1994-2:2005 §§6.2.2, 6.6 e 7.2.2. Valori modificabili: γM1 = " + F(accessory.D("gamma_m1")) + ", η = " + F(accessory.D("eta_taglio")) + ", γV = " + F(accessory.D("gamma_v")) + ".");
            doc.P("Taglio affidato all’anima intera: VRd = min(Vpl,Rd; Vbw,Rd), Vbw,Rd = χw hw tw fy/(√3 γM1). Contributo delle flange omesso. La curva del montante terminale è non rigida salvo qualificazione positiva del terminale inserito. Gli irrigidimenti di appoggio sono verificati se attivati. Le zone inefficaci nel grafico riguardano le tensioni normali.");
            doc.P("Irrigidimenti intermedi: " + (accessory.B("irrigidimenti") ? "presenti" : "assenti") + ". Pioli: " + (accessory.B("pioli") ? "verifica attiva" : "verifica disattivata") + ".");
            var keys = new List<(string Key, string Name)>();
            if (accessory.B("pioli")) keys.AddRange(new[] { ("n_pioli", "Numero per fila"), ("d_pioli", "Diametro gambo [mm]"), ("h_pioli", "Altezza saldata [mm]"), ("passo_pioli", "Passo longitudinale [mm]"), ("passo_trasv_pioli", "Passo trasversale [mm]"), ("fu_pioli", "fu [MPa]"), ("d_testa_pioli", "Diametro testa [mm]"), ("t_testa_pioli", "Spessore testa [mm]"), ("copriferro_pioli", "Copriferro minimo di progetto [mm]") });
            if (keys.Count > 0) doc.Table(["Dato", "Valore"], keys.Select(k => new[] { k.Name, F(accessory.D(k.Key)) }), [3, 1]);
            var detailInputs = BridgeSection.DetailInputRows(accessory).ToArray();
            if (detailInputs.Length > 0) doc.Table(["Dato locale", "Valore", "Unità"], detailInputs, [3.6, 1.4, .7]);
            doc.P("Irrigidimenti mono o bilaterali: sezione con striscia simmetrica di anima limitata dal materiale disponibile, baricentro ed eccentricità reali. Analisi elastica del II ordine con imperfezione equivalente Lcr/200, EN 1993-1-1 §§5.2.2(7)a e 5.3.4. Controlli di rigidezza, torsione e piatti entro classe 3. Le saldature continue, quando attivate, usano fu da Model, βw=1 e γM2=" + F(accessory.D("gamma_m2")) + ".");
            doc.P("Appoggio: reazione R assegnata come inviluppo SLU indipendente dalle fasi, piatti a contatto e impronta interamente sottostante. Il terminale rigido è formato da due coppie bilaterali simmetriche identiche. La curva rigida è adottata soltanto dopo i controlli geometrici, di resistenza e dei collegamenti. Piastra di ripartizione e apparecchio d’appoggio esclusi.");
            doc.P("Soletta: armature trasversali distinte dalle barre longitudinali. Superfici a–a sui due lati e b–b attorno ai gruppi di pioli, traliccio EN 1992-1-1 §6.2.4, armatura minima, interazione con flessione trasversale e ancoraggi. Cot θ limitata a 1–1,25 anche per soletta tesa. Il bordo fisico è un dato separato da b_eff.");
            doc.P("Fatica pioli: ΔqE,2=λv φfat (qmax−qmin), ΔτE,2=ΔqE,2 p/(npioli Agambo). EN 1994-2 §§6.8.3 e 6.8.7.2: categoria 90 per i pioli; categoria 80 e interazione per flangia anche tesa. Gli intervalli sono inviluppi assegnati dal modello globale, comprensivi dei casi fessurati/non fessurati; non provengono dalle fasi costruttive. I controlli non attivati restano da verificare.");
            doc.Figures(images, options, "dettagli");
            doc.P("Pioli: q = Σ(Vi Si/Ii + Δqi), PEd = |q| p/npioli. NTC: proprietà della fase tensionale; EC4: CLS non fessurato con acciaio efficace. Il ritiro uniforme ha q(V)=0; gli effetti locali di estremità richiedono Δq assegnato da un modello longitudinale. PRd è il minore fra resistenza del gambo e del CLS; in combinazione SLE rara si usa 0,75 PRd. Per SLE quasi permanente non si assegna l’esito richiesto alla rara.");
            doc.P("Interazione M–V per N=0, fy≤355 MPa e anima non tutta compressa: EN 1994-2 §6.2.2.4(3), con Mpl e Mf della sezione composta, flange efficaci e anima intera; armature omesse nelle capacità plastiche di riferimento. Negli altri casi, ad alto taglio, si adotta un inviluppo elastico cautelativo: ηnorm+(2ηV−1)²≤1, Mf=0 e CLS limitato a 0,85 fcd. Non si accredita redistribuzione plastica; il criterio può essere più gravoso della verifica con capacità plastiche ridotte per N. I controlli elastici e di classe 4 restano necessari.");
        }

        if (options.Contains("normativa"))
        {
            doc.H("Normativa e coefficienti"); doc.P("Riferimento selezionato: " + d.S("normativa") + ". Limiti tensionali: " + d.S("stato") + ".");
            doc.Table(["Coefficiente", "Applicato"], new[] { new[] { "γM0 acciaio strutturale", Input("gamma_m0") }, new[] { "γc calcestruzzo", Input("gamma_c") }, new[] { "αcc calcestruzzo", Input("alpha_cc") }, new[] { "γs armature", Input("gamma_s") } }, [3, 1]);
            doc.P("Riduzioni locali di classe 4: " + (d.B("classe4") ? "attive" : "disattivate, confronto sulla sezione lorda") + ". Metodo: " + result.Method + ".");
            doc.P("I coefficienti sono quelli assegnati nel foglio. Il rapporto delle larghezze efficaci usa fy caratteristico. " +
                "SLU: limiti αcc fck/γc, fy/γM0 e fyk/γs. SLE: limite CLS 0,60 fck per la rara e 0,45 fck per la quasi permanente; " +
                "limite armature 0,80 fyk e acciaio strutturale fy.");
        }
        if (options.Contains("materiali"))
        {
            doc.H("Materiali"); doc.Table(["Materiale", "Classe", "Resistenza [MPa]", "Modulo E [MPa]"], new[] {
                new[] { "Calcestruzzo", m.Concrete, "fck = " + F(m.Fck), F(m.Ec) }, new[] { "Acciaio strutturale", m.Steel, "fy = " + F(m.Fy), F(m.Ea) },
                new[] { "Armatura ordinaria", m.Rebar, "fyk = " + F(m.Fys), F(m.Es) } }, [1.5, 1, 1.5, 1.4]);
            doc.P(d.B("fy_override") ? "fy strutturale assegnato dall’utente, comune a tutta la carpenteria." : "fy strutturale nominale da catalogo. Il calcolo non determina automaticamente la riduzione per lo spessore delle lamiere.");
        }
        if (options.Contains("geometria"))
        {
            doc.H("Geometria e armature");
            doc.Table(["Componente", "Larghezza [mm]", "Altezza o spessore [mm]"], new[] {
                new[] { "Soletta collaborante", F(g.Width), F(g.SlabHeight) }, new[] { "Anima libera", F(g.WebThickness), F(g.WebHeight) },
                new[] { "Piattabanda superiore", F(g.TopWidth), F(g.TopThickness) }, new[] { "Piattabanda inferiore 1", F(g.Bottom1Width), F(g.Bottom1Thickness) },
                new[] { "Piattabanda inferiore 2", g.Bottom2Thickness > 0 ? F(g.Bottom2Width) : "Assente", g.Bottom2Thickness > 0 ? F(g.Bottom2Thickness) : "—" },
                new[] { "Piattabanda inferiore di calcolo", F(g.BottomEquivalentWidth), F(g.BottomEquivalentThickness) } }, [2.4, 1.3, 1.7]);
            doc.P("La larghezza della soletta è la larghezza collaborante assegnata. La seconda piastra è centrata sotto la prima. " +
                "t equivalente = t1 + t2; b equivalente = (b1 t1 + b2 t2)/(t1 + t2). Questa sostituzione conserva area e spessore complessivo, non in generale baricentro e inerzia.");
            doc.Table(["Proprietà delle piastre inferiori", "Reale", "Equivalente"], new[] {
                new[] { "Area [mm²]", F(g.BottomArea), F(g.BottomArea) }, new[] { "Baricentro y [mm]", F(g.BottomRealCentroid), F(-g.Height + g.BottomEquivalentThickness / 2) },
                new[] { "Ix al proprio baricentro [mm⁴]", F(g.BottomRealInertia), F(g.BottomEquivalentWidth * Math.Pow(g.BottomEquivalentThickness, 3) / 12) } }, [2.6, 1.4, 1.4]);
            doc.Table(["Fila", "Presente", "Ø [mm]", "Passo [mm]", "Faccia asse [mm]", "Barre"], new[] { "top", "bottom" }.Select(side => {
                bool active = d.B("rebars_" + side); double y = side == "top" ? g.SlabHeight - d.D("cover_top") : d.D("cover_bottom");
                return new[] { side == "top" ? "Superiore" : "Inferiore", active ? "Sì" : "No", active ? Input("d_" + side) : "—", active ? Input("pitch_" + side) : "—", active ? Input("cover_" + side) : "—", active ? g.Bars.Count(b => Math.Abs(b.Y - y) < 1e-6).ToString() : "0" };
            }), [1.2, .7, .8, 1, 1.4, .7]);
            doc.P("La distanza inserita è misurata fino all’asse della barra. Le barre sono distribuite e centrate da Model; entrambe le file possono essere assenti. Area totale delle armature: " + F(g.Bars.Sum(b => b.Area)) + " mm².");
            doc.Figures(images, options, "geometria");
        }
        if (options.Contains("azioni"))
        {
            doc.H("Sollecitazioni per fase"); doc.P("Ogni fase sceglie il proprio punto N: quota comune, baricentro omogeneizzato lordo fisso o baricentro efficace aggiornato durante l’iterazione. " +
                "La quota comune, quando selezionata, è y = " + Input("y_ref") + " mm. Il momento assegnato è riferito al punto N della fase. Mx,G = Mx + N (yG − yN); i momenti cumulati sono riportati a y=0 mediante Mx,0 = Mx − N yN, con unità coerenti.");
            doc.Table(["Fase", "Sezione / azione", "Attiva", "ΔN [kN]", "ΔMx [kNm]", "ΔV [kN]", "Δεcs [µε]"], d.Array("fasi").Select((p, i) => new[] {
                $"{i + 1} · {p.S("nome")}", p.S("tipo"), p.B("attiva") ? "Sì" : "No", p.S("tipo") == BridgeSection.ShrinkageKind ? "—" : p.S("N"),
                p.S("tipo") == BridgeSection.ShrinkageKind ? "—" : p.S("Mx"), p.S("tipo") == BridgeSection.ShrinkageKind ? "—" : F(p.D("V")),
                p.S("tipo") == BridgeSection.ShrinkageKind ? p.S("epsilon_cs") : "—" }), [2, 1.4, .6, .8, .9, .8, .9]);
            doc.P("Solo acciaio: soletta e barre non partecipano. Composta: soletta non fessurata, carpenteria e barre. Soletta esclusa: carpenteria e barre. " +
                "Per ciascuna situazione la sezione efficace è comune ai contributi sommati. Non si simula la storia evolutiva di costruzione o la redistribuzione viscosa.");
            if (result.Stages.Any(s => s.Contributions.Any(c => c.IsShrinkage)))
                doc.P("Ritiro: deformazione uniforme imposta al solo calcestruzzo, negativa per accorciamento (−250 µε = −0,25‰). Ogni fase ha propri φ, ψL e n; ψL iniziale = 0,55. " +
                    "Si usa Ac netto delle armature: Neq = Ec,eff Ac Δεcs al baricentro del CLS netto, Meq,0 = −Neq yc. Alla tensione del CLS ottenuta dal carico equivalente si aggiunge −Ec,eff Δεcs. " +
                    "Le tensioni risultano autoequilibrate: N e M esterni della fase sono nulli. Sono inclusi gli effetti primari locali; gli effetti di vincoli esterni richiedono azioni separate. Più fasi rappresentano incrementi assegnati, senza evoluzione temporale automatica.");
        }
        if (options.Contains("omogeneizzazione"))
        {
            doc.H("Criteri di omogeneizzazione");
            doc.P("n0 = Ea/Ecm; n = n0 (1 + ψL φ); φ = (n/n0 − 1)/ψL. Il parametro passato a Model e Checker è ψL φ. Il rapporto Es/Ea è conservato.");
            doc.Table(["Fase composta", "φ", "ψL", "n di calcolo", "Stato"], d.Array("fasi").OfType<JsonObject>().Where(p => BridgeSection.HasConcrete(p.S("tipo"))).Select(p =>
            {
                // Recompute both representations for legacy archives too. Inactive phases may be incomplete.
                try { var h = BridgeSection.Homogenization(d, p); return new[] { p.S("nome"), F(h.Phi), p.S("psi"), F(h.N), p.B("attiva") ? "Inclusa" : "Esclusa" }; }
                catch (ArgumentException) { return new[] { p.S("nome"), "—", p.S("psi"), "—", "Esclusa" }; }
            }), [2.4, 1, .8, 1, 1]);
        }
        for (int index = 0; index < result.Stages.Count; index++)
        {
            var s = result.Stages[index];
            if (!options.Overlaps(["azioni", "omogeneizzazione", "tensioni", "classe4", "taglio"])) continue;
            doc.H($"Situazione {index + 1} dopo {s.Name}");
            foreach (string warning in s.Warnings) doc.P("Avviso: " + warning);
            doc.P($"Convergenza in {s.Iterations} iterazioni. Variazione relativa delle larghezze: {s.Residual:E3}; tolleranza 1E−7. " +
                "Il residuo normalizzato di equilibrio di ciascun contributo deve essere non maggiore di 1E−5.");
            if (options.Contains("azioni"))
            {
                doc.Table(["Contributo", "Riferimento N", "yN [mm]", "ΔN [kN]", "ΔMx al punto N [kNm]", "ΔV [kN]"], s.Contributions.Select((c, i) => new[] {
                    $"{i + 1} · {c.Name}", c.IsShrinkage ? "Deformazione imposta" : c.LoadReference, c.IsShrinkage ? "—" : F(c.LoadY), F(c.N), F(c.Mx), F(c.V) }), [2.1, 1.9, 1, .9, 1.2, .9]);
                if (s.Contributions.Any(c => c.IsShrinkage))
                {
                    doc.Sub("Ritiro · azioni equivalenti interne al calcolo");
                    doc.Table(["Fase", "Δεcs [µε]", "Neq [kN]", "Meq,0 [kNm]", "Correzione σc [MPa]"], s.Contributions.Where(c => c.IsShrinkage).Select(c => new[] {
                        c.Name, F(c.ShrinkageStrain * 1e6), F(c.EquivalentN), F(c.EquivalentMomentAtInterface), F(c.ConcreteStressOffset) }), [2, 1, 1.2, 1.3, 1.4]);
                    doc.P("Neq e Meq,0 sono ausiliari e non vanno sommati ai carichi esterni. La correzione è applicata soltanto al CLS; le barre mantengono la tensione da compatibilità.");
                }
            }
            if (options.Contains("omogeneizzazione"))
            {
                doc.Sub("Rapporti e proprietà per contributo");
                doc.Table(["Contributo", "n0", "n", "φ", "ψL φ", "Ec eff [MPa]"], s.Contributions.Select((c, i) => new[] { $"{i + 1} · {c.Name}",
                    c.HasConcrete ? F(c.N0) : "—", c.HasConcrete ? F(c.HomogenizationN) : "—", c.HasConcrete ? F(c.Phi) : "—", c.HasConcrete ? F(c.EffectivePhi) : "—", c.HasConcrete ? F(m.Ea / c.HomogenizationN) : "—" }), [2.7, .8, .8, .8, .9, 1.3]);
                doc.Table(["Contributo", "A* [mm²]", "yG [mm]", "Ix* [mm⁴]", "Ix integrazione [mm⁴]"], s.Contributions.Select((c, i) => new[] {
                    (i + 1).ToString(), F(c.Area), F(c.Centroid), F(c.Inertia), F(c.SolverInertia) }), [.8, 1.2, 1.1, 1.6, 1.9]);
                doc.Table(["Contributo", "Wsup* [mm³]", "Winf* [mm³]", "yσ zero [mm]", "κ [1/m]", "Residuo equilibrio"], s.Contributions.Select((c, i) => new[] {
                    (i + 1).ToString(), F(c.WTop), F(c.WBottom), F(c.NeutralAxis), F(-c.StressSlope / m.Ea * 1000), c.EquilibriumResidual.ToString("0.###E+0", CultureInfo.GetCultureInfo("it-IT")) }), [.8, 1.3, 1.3, 1.1, 1.2, 1.3]);
                doc.P("A*, Ix* e W* sono riferiti all’acciaio strutturale. Le proprietà geometriche Model includono le inerzie proprie; " +
                    "Checker integra le pareti sottili lungo la linea media e le barre come aree concentrate. L’inerzia di integrazione è quella usata nel controllo indipendente dell’equilibrio.");
            }
            if (options.Contains("tensioni"))
            {
                doc.Sub("Tensioni totali");
                doc.Table(["Punto", "y [mm]", "Σσ [MPa]", "Limite [MPa]", "η", "Esito locale"], s.Points.Select(p => new[] { p.Name, F(p.Y), p.Active ? F(p.Stress) : "—", p.Active ? F(p.Limit) : "—", F(p.Utilization),
                    !p.Active ? "Non attivo" : p.Utilization is null ? "CLS teso" : p.Utilization <= 1 ? "Entro limite" : "Limite superato" }), [2.2, .8, 1, 1.1, .6, 1.4]);
                for (int start = 0; start < s.Contributions.Count; start += 4)
                {
                    int count = Math.Min(4, s.Contributions.Count - start);
                    doc.Sub("Contributi alle tensioni in MPa");
                    doc.Table(["Punto", ..Enumerable.Range(start, count).Select(i => "Δσ " + (i + 1))], s.Points.Select(p => new[] { p.Name }.Concat(p.Contributions.Skip(start).Take(count).Select(v => F(v))).ToArray()), [2.4, ..Enumerable.Repeat(1d, count)]);
                    doc.P(string.Join("; ", Enumerable.Range(start, count).Select(i => $"{i + 1}: {s.Contributions[i].Name}")) + ".");
                }
            }
            if (options.Contains("classe4"))
            {
                doc.Sub("Sezione efficace di classe 4");
                var panels = new[] { ("Anima", s.Effective.Web), ("Sbalzo superiore", s.Effective.Top), ("Sbalzo inferiore equivalente", s.Effective.Bottom) };
                doc.Table(["Pannello", "b [mm]", "t [mm]", "ψ", "kσ", "λp", "ρ"], panels.Select(p => new[] { p.Item1, F(p.Item2.Width), F(p.Item2.Thickness), F(p.Item2.Psi), F(p.Item2.KSigma), F(p.Item2.Lambda), F(p.Item2.Rho) }), [2.2, .9, .7, .8, .8, .8, .8]);
                doc.Table(["Pannello", "σ1 [MPa]", "σ2 [MPa]", "bc [mm]", "b1 eff [mm]", "b2 eff [mm]"], panels.Select(p => new[] { p.Item1, F(p.Item2.StartStress), F(p.Item2.EndStress), F(p.Item2.CompressedWidth), F(p.Item2.EffectiveAtStart), F(p.Item2.EffectiveAtEnd) }), [2.2, 1, 1, 1, 1, 1]);
                doc.Table(["Carpenteria", "Area [mm²]", "yG [mm]", "Ix [mm⁴]"], new[] {
                    new[] { "Lorda equivalente", F(g.SteelArea), F(g.SteelCentroid), F(g.SteelInertia) }, new[] { "Efficace", F(s.EffectiveSteel.Area), F(s.EffectiveSteel.Centroid), F(s.EffectiveSteel.Inertia) } }, [2, 1.3, 1.2, 1.5]);
                double removed = Math.Max(0, g.WebHeight - s.Effective.WebTop - s.Effective.WebBottom);
                doc.P("Anima inefficace: " + F(removed) + " mm" + (removed > .001 ? ", da y = " + F(-g.TopThickness - g.WebHeight + s.Effective.WebBottom) + " a y = " + F(-g.TopThickness - s.Effective.WebTop) + " mm." : ".") +
                    " Aeff/Alorda = " + F(s.EffectiveSteel.Area / g.SteelArea) + "; Ieff/Ilorda = " + F(s.EffectiveSteel.Inertia / g.SteelInertia) + ".");
                doc.P("Anima trattata come pannello interno non irrigidito. Piattabande trattate come sbalzi uniformemente compressi con la tensione più compressiva nello spessore. " +
                    "Per ψ minore di −3, kσ e la riduzione sono valutati a −3, mantenendo la larghezza compressa effettiva. Il tratteggio rappresenta la parte inefficace sotto tensioni normali, non instabilità a taglio.");
            }
            if (options.Contains("taglio") && s.Shear is { } shear)
            {
                doc.Sub("Taglio e connessione della situazione");
                var web = shear.Web;
                doc.Table(["V [kN]", "kτ", "τcr [MPa]", "λw", "χw", "Vpl,Rd [kN]", "Vbw,Rd [kN]"], [new[] {
                    F(shear.V), F(web.KTau), F(web.TauCritical), F(web.Slenderness), F(web.Chi), F(web.PlasticResistance / 1000), F(web.BucklingResistance / 1000) }], [1, .7, 1, .7, .7, 1.2, 1.2]);
                doc.P("τ nominale V/Av = " + F(shear.TauAverage) + " MPa; τ max elastica lorda = " + F(shear.TauMaximum) + " MPa. Beneficio irrigidimenti: " + (shear.UsesStiffeners ? "sì" : "no") + ". Montante terminale adottato: " + (shear.RigidEndPost ? "rigido verificato" : "non rigido") + ".");
                if (s.Studs is { } studs && studs.Enabled)
                {
                    doc.P("q = " + F(studs.Flow) + " kN/m; PEd = " + F(studs.ForcePerStud) + " kN/piolo; PRd adottato = " + F(studs.ResistancePerStud) + " kN/piolo; qRd = " + F(studs.ResistancePerLength) + " kN/m.");
                    doc.P("Resistenze SLU del singolo piolo: acciaio = " + F(studs.Resistance!.SteelResistance / 1000) + " kN; CLS = " + F(studs.Resistance.ConcreteResistance / 1000) + " kN; α = " + F(studs.Resistance.Alpha) + ".");
                    doc.Table(["Fase", "S* [mm³]", "I* [mm⁴]", "q(V) [kN/m]", "Δq [kN/m]"], studs.Contributions.Select(c => new[] { c.Phase, c.StaticMoment.ToString("0.###E+0"), c.Inertia.ToString("0.###E+0"), F(c.Flow), F(c.AdditionalFlow) }), [2, 1, 1, 1, 1]);
                }
                var detailValues = (shear.Details ?? []).Concat(s.Studs?.Details ?? []).ToArray();
                if (detailValues.Length > 0) doc.Table(["Parametro locale", "Valore", "Unità"], detailValues.Select(v => new[] { v.Name, F(v.Value), v.Unit }), [3.2, 1.5, .7]);
                var checks = shear.Checks.Concat(s.Studs?.Checks ?? []).ToArray();
                doc.Table(["Controllo", "Domanda", "Limite", "η", "Esito"], checks.Select(c => new[] { c.Name, F(c.Demand) + " " + c.Unit, F(c.Resistance) + " " + c.Unit, F(c.Ratio), c.Status }), [2.5, 1.2, 1.2, .6, 1.1]);
                foreach (var c in checks.Where(c => c.Note.Length > 0)) doc.P(c.Name + ": " + c.Note + ".");
            }
            doc.Figures(images, options, "fase_" + index);
        }
        return doc.Bytes();
    }

    private sealed class Document
    {
        private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main", R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private readonly XElement body = new(W + "body");
        private readonly List<ImmagineReport> figures = [];
        private static XElement Paragraph(string text, string style = "Normal") => new(W + "p", new XElement(W + "pPr", new XElement(W + "pStyle", new XAttribute(W + "val", style))), new XElement(W + "r", new XElement(W + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), text)));
        internal void P(string text, string style = "Normal") => body.Add(Paragraph(text, style));
        internal void H(string title) => P(title, "Heading1");
        internal void Sub(string title) => P(title, "Heading2");
        internal void Table(string[] headers, IEnumerable<string[]> source, double[] weights)
        {
            var rows = source.ToArray(); if (rows.Length == 0) { P("Nessun dato applicabile."); return; }
            int[] widths = weights.Select(v => (int)Math.Round(9360 * v / weights.Sum())).ToArray(); widths[^1] += 9360 - widths.Sum();
            var table = new XElement(W + "tbl", new XElement(W + "tblPr", new XElement(W + "tblW", new XAttribute(W + "w", 9360), new XAttribute(W + "type", "dxa")), new XElement(W + "tblLayout", new XAttribute(W + "type", "fixed")),
                new XElement(W + "tblCellMar", new[] { "top", "bottom", "left", "right" }.Select(side => new XElement(W + side, new XAttribute(W + "w", 80), new XAttribute(W + "type", "dxa")))),
                new XElement(W + "tblBorders", new[] { "top", "bottom", "left", "right", "insideH", "insideV" }.Select(side => new XElement(W + side, new XAttribute(W + "val", "single"), new XAttribute(W + "sz", 4), new XAttribute(W + "color", "D9D9D9"))))),
                new XElement(W + "tblGrid", widths.Select(width => new XElement(W + "gridCol", new XAttribute(W + "w", width)))));
            foreach (var (cells, row) in new[] { headers }.Concat(rows).Select((cells, row) => (cells, row)))
                table.Add(new XElement(W + "tr", new XElement(W + "trPr", new XElement(W + "cantSplit"), row == 0 ? new XElement(W + "tblHeader") : null), cells.Select((text, column) => {
                    var p = Paragraph(text, row == 0 ? "TableHeader" : "TableText");
                    if (column > 0) p.Element(W + "pPr")!.Add(new XElement(W + "jc", new XAttribute(W + "val", "center")));
                    return new XElement(W + "tc", new XElement(W + "tcPr", new XElement(W + "tcW", new XAttribute(W + "w", widths[column]), new XAttribute(W + "type", "dxa")), new XElement(W + "vAlign", new XAttribute(W + "val", "center")), row == 0 ? new XElement(W + "shd", new XAttribute(W + "fill", "E8EFF7")) : null), p);
                })));
            body.Add(table); P("", "TableGap");
        }
        internal void Figures(IReadOnlyList<ImmagineReport>? images, HashSet<string> options, string category)
        {
            if (!options.Contains("grafici")) return;
            XNamespace wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing", a = "http://schemas.openxmlformats.org/drawingml/2006/main", pic = "http://schemas.openxmlformats.org/drawingml/2006/picture";
            foreach (var image in (images ?? []).Where(i => i.Categoria == category))
            {
                int id = figures.Count + 1; figures.Add(image); P(image.Titolo, "Caption");
                long cx = 5943600, cy = 3169920;
                // PNG IHDR dimensions: elevation sketches have a different aspect from section diagrams.
                if (image.Png.Length >= 24 && image.Png[0] == 137 && image.Png[1] == 80)
                {
                    int width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(image.Png.AsSpan(16, 4));
                    int height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(image.Png.AsSpan(20, 4));
                    if (width > 0 && height > 0) cy = (long)Math.Round((double)cx * height / width);
                }
                body.Add(new XElement(W + "p", new XElement(W + "r", new XElement(W + "drawing", new XElement(wp + "inline", new XElement(wp + "extent", new XAttribute("cx", cx), new XAttribute("cy", cy)), new XElement(wp + "docPr", new XAttribute("id", id), new XAttribute("name", image.Titolo), new XAttribute("descr", image.Titolo)),
                    new XElement(a + "graphic", new XElement(a + "graphicData", new XAttribute("uri", pic.NamespaceName), new XElement(pic + "pic", new XElement(pic + "nvPicPr", new XElement(pic + "cNvPr", new XAttribute("id", id), new XAttribute("name", "image.png")), new XElement(pic + "cNvPicPr")),
                        new XElement(pic + "blipFill", new XElement(a + "blip", new XAttribute(R + "embed", "img" + id)), new XElement(a + "stretch", new XElement(a + "fillRect"))), new XElement(pic + "spPr", new XElement(a + "xfrm", new XElement(a + "off", new XAttribute("x", 0), new XAttribute("y", 0)), new XElement(a + "ext", new XAttribute("cx", cx), new XAttribute("cy", cy))), new XElement(a + "prstGeom", new XAttribute("prst", "rect"), new XElement(a + "avLst")))))))))));
            }
        }
        internal byte[] Bytes()
        {
            body.Add(new XElement(W + "sectPr", new XElement(W + "pgSz", new XAttribute(W + "w", 11906), new XAttribute(W + "h", 16838)), new XElement(W + "pgMar", new XAttribute(W + "top", 1134), new XAttribute(W + "bottom", 1134), new XAttribute(W + "left", 1273), new XAttribute(W + "right", 1273))));
            using var memory = new MemoryStream();
            using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
            {
                void Entry(string name, string text) { using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false)); writer.Write(text); }
                Entry("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Default Extension=\"png\" ContentType=\"image/png\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/><Override PartName=\"/word/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml\"/></Types>");
                Entry("_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"doc\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
                XNamespace rel = "http://schemas.openxmlformats.org/package/2006/relationships";
                var relationships = new XElement(rel + "Relationships", new XElement(rel + "Relationship", new XAttribute("Id", "styles"), new XAttribute("Type", R.NamespaceName + "/styles"), new XAttribute("Target", "styles.xml")));
                for (int i = 0; i < figures.Count; i++)
                {
                    relationships.Add(new XElement(rel + "Relationship", new XAttribute("Id", "img" + (i + 1)), new XAttribute("Type", R.NamespaceName + "/image"), new XAttribute("Target", $"media/image{i + 1}.png")));
                    using var stream = zip.CreateEntry($"word/media/image{i + 1}.png").Open(); stream.Write(figures[i].Png);
                }
                var styles = new XElement(W + "styles");
                foreach (var (id, size, bold, before, after) in new[] { ("Normal", 21, false, 0, 100), ("Title", 36, true, 0, 120), ("Subtitle", 25, false, 0, 160), ("Heading1", 28, true, 240, 120), ("Heading2", 23, true, 180, 80), ("Caption", 19, false, 120, 70), ("TableText", 18, false, 0, 20), ("TableHeader", 18, true, 0, 20), ("TableGap", 8, false, 0, 50) })
                    styles.Add(new XElement(W + "style", new XAttribute(W + "type", "paragraph"), new XAttribute(W + "styleId", id), new XElement(W + "name", new XAttribute(W + "val", id)), new XElement(W + "pPr", new XElement(W + "spacing", new XAttribute(W + "before", before), new XAttribute(W + "after", after)), new XElement(W + "widowControl"), id is "Title" or "Subtitle" or "Heading1" or "Heading2" or "Caption" or "TableHeader" ? new XElement(W + "keepNext") : null, id.StartsWith("Heading") ? new XElement(W + "outlineLvl", new XAttribute(W + "val", id == "Heading1" ? 0 : 1)) : null),
                        new XElement(W + "rPr", new XElement(W + "rFonts", new XAttribute(W + "ascii", "Calibri"), new XAttribute(W + "hAnsi", "Calibri")), new XElement(W + "sz", new XAttribute(W + "val", size)), new XElement(W + "color", new XAttribute(W + "val", "000000")), bold ? new XElement(W + "b") : null)));
                Entry("word/styles.xml", styles.ToString()); Entry("word/_rels/document.xml.rels", relationships.ToString());
                Entry("word/document.xml", new XDocument(new XElement(W + "document", new XAttribute(XNamespace.Xmlns + "w", W), new XAttribute(XNamespace.Xmlns + "r", R), body)).ToString());
            }
            return memory.ToArray();
        }
    }
}
