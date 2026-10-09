using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using Anthea.Calculations;
using X.Core;

static partial class Program
{
    static int PileExample(string evidence,bool cuttingExample=false,bool updateExample=false)
    {
        // A check run writes the example (archive, drawing, text) into its own folder; the versioned copy in supporto/esempi
        // is rewritten only on explicit request (--update-example).
        var folder=updateExample?Path.GetFullPath("supporto/esempi/palo-orizzontale-armature"):Path.Combine(Path.GetFullPath(evidence),"esempio");Directory.CreateDirectory(folder);
        var root=PaloOrizzontale.Defaults();var g=root["generali"]!;g["lunghezza"]=12;g["diametro"]=1;g["azione_assiale"]=600;g["azione_orizzontale"]=100;
        g["presenza_falda"]=true;g["profondita_falda"]=4;g["eccentricita"]=0;g["tratto_libero"]=0;
        var upper=PaloOrizzontale.Layer();upper["nome"]="Sabbia sciolta · esempio";upper["spessore"]=4;
        var lower=PaloOrizzontale.Layer();lower["nome"]="Sabbia media · esempio";lower["spessore"]=8;lower["angolo_attrito"]=34;
        root["stratigrafie"]=new JsonArray(new JsonArray(upper,lower));ElasticHorizontalPile.PrepareShared(root);
        var soil=ElasticHorizontalPile.EnsureSoil(lower);soil["densita"]="Medio";ElasticHorizontalPile.InitializeMean(soil);
        var section=root["sezione"]!;section["esposizione"]="XC2";section["vita_durabilita"]=50;section["transverse_spacing_mm"]=150;
        var e=root["elastico"]!;e["passo"]=.5;e["stratigrafia"]=0;
        var details=e["dettagli"]!;details["azioni_progetto"]=true;details["taglio_confermato"]=true;details["aderenza_buona"]=true;
        e["tratti"]=new JsonArray(ElasticHorizontalPile.NewSegment("T1",3),ElasticHorizontalPile.NewSegment("T2",6),ElasticHorizontalPile.NewSegment("T3",9),ElasticHorizontalPile.NewSegment("T4",null));
        for(int i=2;i<4;i++){var row=e["tratti"]![i]!;row["collegato"]=false;foreach(string key in ElasticHorizontalPile.ReinforcementKeys)row[key]=section[key]!.DeepClone();row["longitudinal_bar_count"]=8;}
        if(cuttingExample)
        {
            g["lunghezza"]=20;lower["spessore"]=16;
            section["longitudinal_bar_count"]=20;section["longitudinal_bar_diameter_mm"]=20;
            e["tratti"]=new JsonArray(ElasticHorizontalPile.NewSegment("T1",12),ElasticHorizontalPile.NewSegment("T2",18),ElasticHorizontalPile.NewSegment("T3",null));
            for(int i=1;i<3;i++){var row=e["tratti"]![i]!;row["collegato"]=false;foreach(string key in ElasticHorizontalPile.ReinforcementKeys)row[key]=section[key]!.DeepClone();row["longitudinal_bar_count"]=12;row["longitudinal_bar_diameter_mm"]=16;}
        }
        string basename=cuttingExample?"palo-20m-tagli-12-18-20":"palo-12m-quattro-tratti";
        root["vista_orizzontale"]="elastico";
        var document=Archivio.Documento(PaloOrizzontale.Module);document["dati"]=root;document["nome"]=cuttingExample?"Esempio · palo 20 m con tagli 12 / 18 / 20":"Esempio · palo 12 m con quattro tratti di armatura";
        string path=Path.Combine(folder,basename+".programma");Archivio.Scrivi(path,document);
        var reopened=Archivio.Leggi(path)["dati"]!.AsObject();Check(JsonNode.DeepEquals(root,reopened),"example archive opens without changing its settings");
        var result=ElasticHorizontalPile.CalculateShared(reopened);var broms=PaloOrizzontale.Calculate(reopened);
        Check(result["armature"]!.Array("distinta").Count==(cuttingExample?3:4)&&result["armature"]!.Array("tratti").Count==(cuttingExample?3:4),"example preserves every user-defined cutting group");
        if(cuttingExample)
        {
            var runs=result["armature"]!.Array("distinta");
            Check(runs.Select(r=>r.D("End")).SequenceEqual(new[]{12d,18,20}),"20m example preserves cutting ends12/18/20");
            Check(Math.Abs(runs[1].D("Start")-10.8)<1e-8&&Math.Abs(runs[2].D("Start")-17)<1e-8,"20m example starts10.8 and17 with respective laps1.2 and1.0");
        }
        Check(RebarMaterial.Match(reopened["sezione"]!.AsObject())?.Name=="B450C","example uses complete B450C catalogue material");
        File.WriteAllText(Path.Combine(evidence,"esempio-risultati.json"),result.ToJsonString(J.Options));File.WriteAllText(Path.Combine(evidence,"esempio-broms.json"),broms.ToJsonString(J.Options));
        var app=new TestApp();app.LoadStyles();app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
        System.Threading.SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext());
        var hostType=typeof(X.Desktop.MainWindow).Assembly.GetType("X.Desktop.HorizontalWorkspace")!;var host=(FrameworkElement)Activator.CreateInstance(hostType,flags,null,[reopened],null)!;
        Wait((System.Threading.Tasks.Task)hostType.GetMethod("CalculateActiveAsync",flags)!.Invoke(host,null)!);
        Check(hostType.GetProperty("ActiveResult",flags)!.GetValue(host)!=null,"saved example opens and calculates in the real horizontal-pile workspace");((IDisposable)host).Dispose();
        var drawType=typeof(X.Desktop.MainWindow).Assembly.GetType("X.Desktop.PileReinforcementDrawing")!;var drawing=Activator.CreateInstance(drawType,true)!;drawType.GetMethod("Set",flags)!.Invoke(drawing,[result,null]);
        var size=(Size)drawType.GetProperty("SheetSize",flags)!.GetValue(drawing)!;
        File.WriteAllBytes(Path.Combine(folder,basename+".png"),(byte[])drawType.BaseType!.GetMethod("Png",flags)!.Invoke(drawing,[(int)size.Width,(int)size.Height])!);
        var response=result["risposta"]!;var states=string.Join("\n",result["armature"]!.Array("tratti").Select(t=>$"- {t.S("id")}: {t.S("stato")}."));
        if(cuttingExample)
        {
            File.WriteAllText(Path.Combine(folder,basename+".md"),$"""
            # Esempio del palo da 20 m con quote di taglio assegnate

            Aprire **palo-20m-tagli-12-18-20.programma** con File → Apri. Nella Risposta elastica aprire Tavola armature e distinta ferri. Ogni tratto definisce un gruppo distinto; i confini sono quote finali di taglio. L’esempio serve a leggere la geometria dei ferri e gli esiti, non è un progetto esecutivo verificato.

            ## Input condivisi

            Palo D 1,00 m, lunghezza 20 m, testa al piano campagna; testa e punta libere nel modello laterale. H=100 kN, eccentricità zero; N testa=600 kN con compressione positiva e peso proprio 25 kN/m³. C35/45, B450C, copriferro 70 mm, staffe Ø10/150 mm; XC2, vita 50 anni. FEM con passo iniziale 0,50 m ed EI dalla sezione integra. Sabbia sciolta 0–4 m (A=200, φ=30°), sabbia media 4–20 m (A=650, φ=34°), γ=18 e γsat=20 kN/m³; falda a 4 m. Modalità Viggiani A·γ/1,35 con peso immerso sotto falda. Buona aderenza, giunti al 100%, barre commerciali massime 12 m.

            ## Tratti e barre effettive

            | Tratto assegnato | Armatura | Inizio barra | Fine barra | Lunghezza di taglio |
            | --- | --- | --- | --- | --- |
            | T1 0–12 m | 20Ø20, principale | 0,00 m | 12,00 m | 12,00 m |
            | T2 12–18 m | 12Ø16, personalizzata | 10,80 m | 18,00 m | 7,20 m |
            | T3 18–20 m | 12Ø16, personalizzata | 17,00 m | 20,00 m | 3,00 m |

            Al primo giunto governa l’iniziale 60Ø20=1,20 m; al secondo 60Ø16=0,96 m, arrotondato a 1,00 m. I valori richiesti dal verificatore in questo esempio sono inferiori. Il terzo gruppo inizia quindi a 17,00 m: le due sovrapposizioni non sono uguali perché cambiano i diametri. Ogni gruppo entra in una sola barra commerciale da 12, 8 e 6 m rispettivamente: la lunghezza di taglio resta 12, 7,20 e 3 m, senza allungamenti allo stock.

            Se un gruppo supera la lunghezza commerciale, vengono creati pezzi interni con ulteriori sovrapposizioni, conservando l’inizio e la fine del gruppo. Quantità longitudinale qui: 20×12 + 12×7,20 + 12×3 = 362,40 m; sfridi esclusi.

            ## Controlli da leggere

            {states}

            Le corone regolari 20/12 hanno quattro coppie radiali nominali comuni su dodici richieste: il primo giunto segnala l’abbinamento da definire. Le corone 12/12 del secondo giunto sono allineate. I tagli scelti non vengono modificati per risolvere verifiche non soddisfatte. Sviluppo alle estremità, trattenimento delle barre, confinamento, staffe esecutive, SLE e sisma restano da completare. MRd nominale non equivale a dettaglio verificato; dove lo sviluppo o il giunto non sono disponibili, MRd utilizzabile non viene accreditato.

            Provare a spostare la fine di T1: la barra superiore termina esattamente alla nuova quota e T2 arretra da quella quota di l₀. Riducendo la lunghezza commerciale si vedono i soli tagli interni necessari.
            """);
            File.WriteAllText(Path.Combine(evidence,"example-checks.txt"),$"PASS {checks} checks\n{path}");return 0;
        }
        File.WriteAllText(Path.Combine(folder,basename+".md"),$"""
        # Esempio del palo con quattro tratti di armatura

        Aprire **palo-12m-quattro-tratti.programma** con File → Apri in ANTHEA. La scheda iniziale è Risposta elastica · trave su molle; attendere il ricalcolo automatico. Aprire **Tavola armature e distinta ferri → Apri tavola armature** per il disegno ingrandito.

        Questo esempio didattico mostra la relazione fra input condivisi, armature per tratto, sollecitazioni e tavola. I parametri e le azioni sono valori assegnati per l’esempio, senza riferimenti a un sito o a un’opera reale. Non è un progetto esecutivo verificato.

        ## Dati impostati

        | Dato | Valore |
        | --- | --- |
        | Palo | D 1,00 m; lunghezza infissa e totale 12,00 m; tratto libero 0 |
        | Vincoli | Rotazione in testa libera; punta libera nel modello laterale |
        | Carico orizzontale | H = 100 kN; eccentricità 0 m |
        | Carico assiale | N testa = +600 kN, compressione positiva; peso proprio incluso lungo il palo |
        | Materiali | Calcestruzzo C35/45; acciaio B450C; coefficienti unitari disattivati |
        | Sezione principale | 16 Ø24; staffe Ø10/150 mm; copriferro 70 mm |
        | Durabilità | Esposizione XC2; vita 50 anni; nessun override del copriferro minimo |
        | Peso c.a. | 25 kN/m³, già comprensivo dell’acciaio |
        | FEM | Passo iniziale 0,50 m; EI dalla sezione integra lorda e dal materiale |
        | Falda | 4,00 m sotto il piano campagna |

        La falda si trova a **4 m sotto il piano campagna**. In questo esempio testa e piano campagna coincidono; x e z hanno quindi la stessa origine.

        ## Stratigrafia

        | Strato | Quote | Addensamento | Parametro A | Pesi γ / γsat | Attrito φ |
        | --- | --- | --- | --- | --- | --- |
        | Sabbia sciolta | 0–4 m | Sciolto | 200, media di 100–300 | 18 / 20 kN/m³ | 30° |
        | Sabbia media | 4–12 m | Medio | 650, media di 300–1000 | 18 / 20 kN/m³ | 34° |

        Modalità A·γ/1,35 da tabella 14.5 nella trattazione di Viggiani: γ naturale sopra falda, γ′ = γsat − 9,81 sotto falda. A è adimensionale. La legge adottata è kh = nh·z/D, con z globale dal piano campagna. Le medie iniziali sono una convenzione del software. Aprire le tabelle dalla riga dello strato per confrontare intervalli, valori bibliografici e valore adottato.

        ## Armature per tratto

        | Tratto | Quote | Armatura longitudinale | Collegamento |
        | --- | --- | --- | --- |
        | T1 | 0–3 m | 16 Ø24 | Sezione principale |
        | T2 | 3–6 m | 16 Ø24 | Sezione principale |
        | T3 | 6–9 m | 8 Ø24 | Personalizzata |
        | T4 | 9–12 m | 8 Ø24 | Personalizzata |

        Le staffe sono Ø10/150 mm. Ogni tratto genera un gruppo distinto: T1 da 0 a 3 m; T2 da 1,50 a 6 m; T3 da 4,50 a 9 m; T4 da 7,50 a 12 m. Le sovrapposizioni sono di 1,50 m. I gruppi non vengono fusi anche quando hanno armature identiche. Le marche B identificano i pezzi reali e le marche S le zone di staffatura.

        Lunghezza commerciale massima 12 m; preferenze 6/8/10/12 m. Sovrapposizione iniziale 60φ: per Ø24 si arrotonda 1,44 m a 1,50 m. Il calcolo adotta almeno la lunghezza richiesta dalle verifiche disponibili. Buona aderenza assegnata; percentuale sovrapposta 100%. Nessuno sviluppo esterno viene aggiunto automaticamente ai tagli: le estremità restano da dettagliare.

        ## Risultati di riferimento

        - Spostamento della testa: {response.D("HeadDisplacement"):G6} m.
        - Massimo assoluto del momento: {response["Extrema"]!["M"].D("AbsoluteMaximum"):G6} kNm, a x = {response["Extrema"]!["M"].D("AbsoluteMaximumDepth"):G6} m.
        - Massimo assoluto del taglio: {response["Extrema"]!["V"].D("AbsoluteMaximum"):G6} kN.

        {states}

        In T3 e T4 la disposizione 8 Ø24 non soddisfa il controllo di interasse longitudinale delle regole Pilastro adottate. È lasciata esplicitamente visibile per mostrare come leggere un esito non soddisfatto: selezionare il tratto e Dettagli pilastro / trave nel riepilogo, oppure aprire il verificatore c.a.
        
        Le azioni sono dichiarate di progetto per l’esempio; è confermata l’ipotesi di taglio circolare equivalente con staffe chiuse a 90°. Restano da completare disposizione delle staffe a trattenimento delle barre, dettagli di estremità, sagomature/ganci, confinamento dei giunti e verifiche escluse dal modello (SLE e sisma). I rami MRd nominali non certificano tali dettagli. La distinta delle staffe fornisce quantità e passo; la loro lunghezza di taglio è da definire.

        ## Prove da fare nell’interfaccia

        - Modificare H nella scheda principale: i diagrammi e le verifiche si aggiornano.
        - Cambiare le barre della sezione principale: T1 e T2 seguono la modifica; T3 e T4 conservano le armature personalizzate.
        - Cambiare le barre di T3: MRd e distinta cambiano, mentre i diagrammi N, V e M rimangono invariati.
        - Selezionare Distinta ferri o Apri verificatore c.a. sul tratto per approfondire i dati e gli esiti.
        """);
        File.WriteAllText(Path.Combine(evidence,"example-checks.txt"),$"PASS {checks} checks\n{path}");return 0;
    }
}
