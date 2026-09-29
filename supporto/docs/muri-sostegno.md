# Muri di sostegno

Il modulo **Geotecnica → Muri di sostegno** calcola mensola in c.a. e gravità per metro di sviluppo. Le altre tipologie sono predisposte nello schema e restano senza calcolo. Il riferimento funzionale è [MAX di Aztec](https://www.aztec.it/max-muri-di-sostegno/); non viene dichiarata equivalenza numerica.

## Interfaccia

Due schede: **Input** e **Verifiche**. Gli input seguono l'ordine Terreno, Materiali, Geometria, Azioni. La stratigrafia usa righe colorate collegate al disegno, spessori, profondità progressive, aggiunta, eliminazione e riordino. Il piano di posa deve essere coperto dalla stratigrafia. Il profilo riporta terreno, dimensioni, armature, falda e carichi.

Si possono aggiungere sovraccarichi uniformi, forze orizzontali, forze verticali, momenti, pressioni laterali su tratti e urti equivalenti statici per metro. Ogni azione ha natura G1/G2/Q/A, ψ₀/ψ₁/ψ₂ e gruppo correlato facoltativo. Gruppo vuoto significa indipendenza; nello stesso gruppo natura e ψ devono coincidere. `Calcola` include l'azione nel motore; `Disegna` ne cambia soltanto la visibilità. Le quote delle azioni sono dal piano di posa; x dal bordo a valle. H e pressioni positive verso valle, N a compressione, M ribaltante. La forza verticale deve ricadere nel fusto alla quota assegnata.

Il disegno Input mostra i carichi caratteristici; le viste Verifiche mostrano quelli della combinazione selezionata. Passare sul disegno legge i dati; cliccare un carico apre il relativo editor. Le viste comprendono N/M/V del fusto e delle due mensole, forze resistenti, tassi di lavoro e armature. I diagrammi sono campionati; fra le stazioni il cursore interpola i valori. Il riepilogo inferiore offre inviluppo, combinazione selezionata, tutti i controlli, sollecitazioni numeriche e audit delle spinte.

I tassi di lavoro usano la scala comune degli altri moduli: 0–0,50 blu, 0,50–0,70 verde, 0,70–0,90 giallo, 0,90–1,00 arancio, oltre 1,00 rosso; grigio per controlli incompleti.

## Combinazioni e approcci

Preset locale dei muri NTC 2018: **Approccio 2, A1+M1+R3**. A modifica le azioni; M riduce i parametri del terreno; R divide le resistenze. A1+M1+R1 appartiene alla prima combinazione dell'Approccio 1 e non sostituisce A2+M2+R2. La stabilità globale usa il motore Bishop separato e combinazioni A2+M2+R2 proprie.

Il generatore enumera i contributi favorevoli/sfavorevoli di muro, terreno, acqua e permanenti. Per le variabili alterna la principale, applica ψ₀ alle accompagnatrici e considera l'omissione favorevole. G1: 1/1,3; G2: 0/1,5; Q: 0/1,5 con ψ dove previsto. Rara e frequente alternano la principale; quasi permanente e sisma usano ψ₂. Gli eventi eccezionali indipendenti sono separati, con fattore 1 dell'evento, ψ₂ delle variabili e γR=1. Le resistenze dei materiali rimangono cautelativamente quelle ordinarie.

La matrice permette di modificare abilitazione, nome, stato, γG, fattori γ×ψ di ogni azione, γMφ, γR distinti, kh e kv. La modifica passa a **Personalizzate** e viene conservata nei ricalcoli. Cambiare elenco, natura, ψ o gruppi richiede rigenerazione o conferma di una matrice coerente. `Genera / ripristina automatiche` sostituisce le modifiche col preset. Coefficienti vuoti, matrici incomplete, azioni escluse con fattori non nulli e urti nelle combinazioni ordinarie sono rifiutati. Limiti: 30 azioni e 4096 combinazioni; il generatore non tronca silenziosamente i casi.

GPC.Model.LoadCase e Combination gestiscono le associazioni azione/coefficiente. L'enumerazione è nell'adattatore ANTHEA: il generatore EN1990 disponibile in GPC non espone direttamente ψ individuali, gruppi e gli eventi eccezionali di questa matrice. GPC.Geometry calcola area e baricentro; GPC.Model e GPCChecker.Concrete restano usati per materiali, sezioni, resistenza N–M e tensioni.

## Campo e formule

Fusto trapezio, paramento di monte verticale, riempimento orizzontale granulare drenato, c′=0. Fondazione nastriforme, orizzontale, senza incasso né terreno a valle o resistenza passiva. Base ruvida: φf/2 ≤ δb ≤ φf. Sovraccarichi di estensione finita, carichi di verso opposto e geometrie aggiuntive richiedono estensioni del motore.

La spinta agisce sul piano virtuale al bordo della mensola a monte, altezza Ht=H+t. Ka=tan²(45°−φd/2), con tanφd=tanφk/γMφ. Si integra σ′v con γ sopra falda e γsat−9,81 sotto falda. Ogni strato applica il proprio Ka: è un'estensione locale di Rankine, non una ricerca del cuneo multistrato. Acqua separata e sottospinta lineare integrale; il peso sopra la mensola usa γsat totale. Spinta e peso della stessa sorgente sono correlati.

- Scorrimento: Rd=V′tanδd/γR, γR=1,1 nel preset ordinario.
- Ribaltamento: Mrib≤Mstab/γR, γR=1,15 nel preset ordinario; sottospinta inclusa nel momento ribaltante.
- Contatto: compressione soltanto, trapezio per |e|≤B/6 e triangolo altrimenti; nessuna reazione fittizia se la risultante è esterna o V′≤0.
- Portanza drenata (EN 1997-1 allegato D, c′=0, D=0): Rd=0,5γ′B′²Nγiγ/γR; Nq=exp(πtanφd)tan²(45°+φd/2); Nγ=2(Nq−1)tanφd; iγ=max(0;1−|H|/V′)³; B′=B−2|e|. γR=1,4 nel preset ordinario. Fuori campo per |e|>B/3 o V′≤0. Con falda si usa γ′ su tutta la zona di rottura.

Le verifiche strutturali campionano H/20 e aggiungono sezioni presso i carichi e il cambio armatura. Le mensole hanno 21 stazioni ciascuna. Il fusto può avere due zone verticali, separate da h₁ misurata dal piede, con diametro e numero di barre per metro diversi. Le due facce restano simmetriche. Entrambi i lati della transizione sono verificati anche se lo spessore è costante. Quantità indicative prive di ancoraggi, sovrapposizioni e armatura secondaria.

Per mensola: N–M da GPC, taglio senza staffe NTC senza beneficio della compressione, minimi/massimi di armatura, tensioni SLE e fessurazione. αcc=0,85, γc=1,5, γs=1,15. Per gravità: compressione, assenza di trazione nel fusto, taglio elastico e flessione della fondazione non armata rispetto alle resistenze assegnate. Non è una verifica completa di pietrame o muratura.

## Sisma e audit delle spinte

Scelta fra **Mononobe–Okabe** e **Wood semplificato**, soltanto con terreno omogeneo asciutto. I nuovi muri propongono **Da parametri del sito (SLV)**; gli archivi precedenti conservano **kh e kv assegnati**. Entrambi i segni di kv, direzione orizzontale verso valle. Nessun parametro del sito viene inventato o ricavato dalla sola località.

Il pulsante **Sisma** accanto alla combinazione visualizzata mostra attivazione e numero dei casi SISMA calcolati; apre direttamente **Input → Azioni → Sisma**, all’inizio del pannello Azioni. In modalità automatica l’attivazione genera le combinazioni sismiche. Le matrici personalizzate conservano i propri coefficienti: se non contengono casi SISMA abilitati, il pulsante lo segnala; aggiornare la matrice o rigenerare le automatiche.

Nel percorso guidato si inseriscono **ag/g e F₀ dello SLV del progetto**, categoria di sottosuolo A–E e topografia. La categoria A–E proviene dalla caratterizzazione geotecnica e sismica: non viene dedotta da φ′ o dai pesi degli strati. F₀ non serve per la categoria A. Il periodo T*c non entra nel calcolo di Ss.

Posto x=F₀·ag/g, Ss segue la tabella 3.2.IV NTC 2018: A=1; B=max(1;min(1,2;1,4−0,4x)); C=max(1;min(1,5;1,7−0,6x)); D=max(0,9;min(1,8;2,4−1,5x)); E=max(1;min(1,6;2−1,1x)). È disponibile anche Ss assegnato.

St viene ricavato dalla forma del terreno: pianeggiante o pendenza media ≤15° → T1, St=1; pendio >15° → T2; rilievo a cresta stretta con pendenza >15° e ≤30° → T3, oltre 30° → T4. La cresta stretta e la configurazione prevalentemente bidimensionale sono condizioni da riconoscere nel sito. Per pendii/rilievi oltre 30 m, St=1+(St,max−1)·z/H, con z sopra la base del rilievo, H altezza del rilievo, St,max=1,2 per T2/T3 e 1,4 per T4. Fino a 30 m l’amplificazione topografica semplificata non è richiesta e il preset adotta St=1. Per topografie complesse occorre la risposta sismica locale. È disponibile St assegnato.

amax/g=Ss·St·ag/g; kh=βm·amax/g; kv=±0,5kh. Nel preset SLV: βm=0,38 per muro libero (MO), βm=1 per muro vincolato (Wood). La riduzione presuppone spostamenti compatibili con la funzionalità delle opere interagenti. Per ribaltamento βm,rib=min(1;1,5βm): si generano due casi generali e due casi dedicati al ribaltamento, separati anche nelle verifiche. A kh=kv=0 i casi di segno uguale sono deduplicati. Le resistenze SLV usano la tabella 7.11.III (scorrimento 1; ribaltamento 1; portanza 1,2, con portanza sismica comunque non calcolata). Il percorso automatico riguarda lo SLV; gli spostamenti e lo SLD non vengono verificati. Il motore mantiene il campo kh≤0,4 e |kv|≤0,2: valori derivati superiori fermano il calcolo, senza troncamento.

Le righe della matrice espongono anche **Uso sisma** (Generale / Ribaltamento). Le modifiche dei dati del sito invalidano la firma delle matrici personalizzate: rigenerare o aggiornare e confermare la matrice. I valori delle righe confermate prevalgono sui coefficienti di riferimento mostrati nel pannello. La modalità manuale conserva per compatibilità il vecchio preset con γR statici e senza incremento automatico di kh per ribaltamento; l’avviso viene riportato negli esiti e nella relazione.

MO: θ=atan[kh/(1−kv)]<φd; Kae=cos²(φd−θ)/{cos²θ[1+√(sinφd·sin(φd−θ)/cosθ)]²}. L'incremento Δp=(Kae−Ka)(1−kv)γHt/2 è uniforme, risultante a Ht/2. Il sovraccarico contribuisce con Kae(1−kv)Σfiqi. Sono aggiunte le inerzie di muro, terreno sulla mensola e carichi verticali.

Wood semplificato: muro rigido non cedevole, K₀=1−sinφd per terreno normalmente consolidato; ΔP=khγHt². La distribuzione uniforme a Ht/2 è un'idealizzazione dichiarata del modulo, **non la soluzione elastica completa di Wood**. Il sovraccarico conserva la componente statica K₀q. Quando Wood è attivo si usa K₀ anche nei casi statici; i vincoli necessari a impedire il movimento non sono verificati dal modulo.

L'audit espone z, φk/φd, Ka/K₀/Kae, σ′v, contributi di terreno, sovraccarico, acqua e sisma, totale e integrali. Le pressioni laterali dirette sono integrate separatamente. La portanza sismica resta da verificare: manca l'inerzia del terreno di fondazione nel relativo meccanismo. La stabilità globale è disponibile come analisi separata; cedimenti, spostamenti, liquefazione, verifiche idrauliche e dettagli esecutivi restano esclusi. Nessun esito complessivo dell'opera.

## Trasferimento del terreno

**Invia / carica terreno…**, nel pannello Terreno dei muri e nei moduli compatibili, apre l’anteprima di strati e falda. Si può inviare a un nuovo foglio di **Portanza del palo verticale**, **Palo orizzontale**, **Micropalo orizzontale** o **Muri di sostegno**, oppure salvare e ricaricare un file `*.anthea-terreno.json`. Nel progetto nasce un nuovo foglio nella stessa sezione; per un calcolo autonomo si apre una nuova finestra. La copia è indipendente.

Si trasferiscono spessori, nomi se disponibili, tipologia, γ, γsat, φ′, c′, cu disponibili e falda, mantenendo tutti gli strati, anche sotto il piano di posa del muro. **z=0 deve corrispondere allo stesso riferimento fisico**: sommità del terreno del muro oppure piano campagna/testa palo. Non sono applicate traslazioni di quota e il profilo non viene esteso alla punta del palo. Nel palo vanno completati lunghezza, azioni, addensamento e parametri specifici. Il numero di verticali indagate non viene dedotto dal numero di profili copiati.

L’importazione nel foglio corrente sostituisce il profilo selezionato e la falda; negli altri sondaggi gli strati restano invariati (la falda è un dato generale del modulo pali). Il muro accetta soltanto strati granulari con c′=0 e parametri nel proprio campo: profili coesivi o con coesione non vengono convertiti silenziosamente. Terreno di fondazione e battente a valle del muro restano input separati da controllare. Il micropalo verticale Bustamante–Doix ha uno schema specifico ed è escluso dal trasferimento.

## Stabilità globale implementata

Input → Terreno → Stabilità globale contiene attivazione, precompilazione dal muro, profilo a valle e monte, strati profondi, falda e dominio di ricerca. La precompilazione copia gli strati già noti senza estrapolarli e propone un profilo piano da adattare al sito: va confermato prima del calcolo. La stratigrafia globale è indipendente da quella delle spinte e non modifica i limiti del modello locale. Origine (0;0) al bordo di valle del piano di posa; x verso monte e y verso l’alto. Gli strati hanno fondo a quota assoluta, orizzontale, decrescente. L’ultimo fondo è il limite delle indagini. La falda è una polilinea piezometrica, senza acqua esterna sopra il terreno. Le azioni esistenti sono riutilizzate; il sovraccarico uniforme interessa tutto il monte.

Bishop semplificato impone equilibrio verticale dei conci e globale dei momenti; non impone equilibrio orizzontale e trascura il taglio interconcio. Le superfici sono circolari, ramo inferiore, centro fra ingresso e uscita, anche con tangente verticale all’ingresso, sotto l’intero muro e verso valle. Il muro sostituisce il volume del terreno: il suo peso non viene duplicato. Spinte muro–terreno e reazioni di fondazione sono interne alla massa e non entrano come azioni esterne. Sisma applicato alle masse proprie; i sovraccarichi sono azioni esterne senza ulteriore massa sismica associata.

Ricerca deterministica su ingresso, uscita e profondità, con raffinamento da sei minimi distinti. Default: griglia 9³, 60 conci, quattro raffinamenti. Gli spigoli, i confini del muro e le intersezioni con gli strati suddividono ulteriormente i conci. Integrazione dei pesi a quadratura di Gauss, solutore dell’equazione Bishop con bracket lontano dalle singolarità mα. Verifica del minimo con il doppio dei conci, tolleranza del 2%; ampliare anche la griglia e il dominio per controllare la ricerca. Nessuna garanzia del minimo assoluto.

In statica A2–M2–R2: γG1=1, γG2=0/1,3, γQ=0/1,3 con ψ individuali, γMtanφ=γMc′=1,25, γMcu=1,4, γR=1,10. Il coefficiente restituito F usa già i parametri ridotti; il tasso è η=γR/F. SLV del complesso muro–terreno: γA=γM=1, γR=1,20, βs=0,38, kh=βs·amax/g, kv=±0,5kh. βs non cambia scegliendo Wood. È possibile assegnare kh/kv globali; non vengono copiati implicitamente i coefficienti manuali delle spinte. Preset eccezionale separato M1/R=1.

La matrice globale è modificabile; la firma impedisce il riuso inconsapevole dopo cambi di azioni, ψ o parametri sismici. Per il calcolo indipendente usare **Calcola solo globale**. La vista Verifiche → Stabilità globale ha una propria selezione delle combinazioni, superficie critica ingrandita o intero profilo, conci interrogabili e tabelle complete. Il CSV nella vista globale contiene tutti i conci e tutte le combinazioni; **Word globale** esporta la sola analisi. La relazione completa del muro include il capitolo e il disegno globale.

Le ricerche senza soluzione, con minimo sul bordo, basi in trazione o mancata convergenza non producono un esito favorevole. Una superficie già non verificata conserva il fallimento anche se la ricerca è incompleta. Il caso non drenato della raccolta dimostra la gestione di questo limite. Non sono implementati fessure di trazione, superfici non circolari, strati inclinati, pressioni idrodinamiche, degradazione ciclica, liquefazione o stabilità autonoma del versante naturale.

### Separazione del codice e migrazione GPC

- `X.Calculations/Geotechnics/SlopeGeometry.cs`: sole costruzioni geometriche e interrogazioni di profilo; usa GPC.Geometry per area e baricentro. Candidato per GPC.Geometry.
- `SlopeStability.Models.cs`: contratti indipendenti da JSON e UI; `SlopeSoil` è il candidato da condividere in GPC.Model. Nei sorgenti GPC disponibili non è presente un materiale geotecnico con φ′, c′ e cu; non si riutilizza un materiale strutturale con parametri fittizi.
- `BishopSolver.cs`: solo equilibrio dei conci; `SlopeStability.cs`: pesi, discretizzazione e ricerca. Candidati per un pacchetto GPC di geotecnica.
- `RetainingWall.GlobalStability.cs`: adattatore del documento muro e regole NTC, separati dal solutore. GPC.Model associa azioni e coefficienti. I materiali strutturali del muro restano quelli delle verifiche GPC già esistenti; nella globale serve il loro peso specifico assegnato.

Nessuna modifica è stata apportata ai repository GPC esterni.

### Controlli riproducibili

`supporto/test/GlobalStability.Checks` contiene i controlli analitici, la sensibilità della ricerca e la generazione di dieci archivi con risultati JSON, conci CSV e relazioni. `supporto/test/Desktop/GlobalStabilitySmokeChecks.cs` copre la UI, il calcolo indipendente, le matrici e gli export. Esecuzione: `dotnet run --project supporto/test/GlobalStability.Checks -c Release -- <nuova-cartella-in-supporto/artefatti/stabilita-globale>` e `ANTHEA.exe --smoke-global-stability <cartella output>`.

Le raccolte precedenti alla correzione dei cerchi tangenti sono archiviate in `supporto/SUPERATI/artefatti/stabilita-globale`. I documenti correnti sono elencati in `supporto/artefatti/stabilita-globale/DOCUMENTI-CORRENTI.md`, disponibile anche in PDF. La serie interna ANTHEA e i confronti parziali MAX sono distinti; le prove MAX sono sospese su richiesta dell'utente.

Il confronto con MAX 16 è documentato separatamente dai test interni: prima si confronta lo stesso cerchio, poi la ricerca, uniformando fattori, quote e discretizzazione. `supporto/test/MaxRetainingWall.Compare` ricalcola un cerchio letto da MAX e conserva scarti, fattori e tutti i conci. Richiede il file nativo MAX e il file di riscontro: non genera valori MAX sintetici. Lo stato del confronto è riportato nel rapporto di controllo della raccolta; la sola presenza dei dieci archivi ANTHEA non indica che siano già stati validati con MAX.

## Invio al modulo c.a. e relazione Word

In Verifiche, selezionare una riga strutturale o una stazione nella tabella delle sollecitazioni e premere **Apri sezione in c.a.**. Si copiano fascia di 1000 mm, spessore locale, materiali, armatura effettiva della zona e tutte le combinazioni disponibili nella stessa stazione. N diventa negativo a compressione; Mx è associato a Vy. SISMA ed ECCEZIONALE vanno nel gruppo Plastico (SLU) con lo stato originale nel nome. Sono conservati anche esposizione, viscosità e parametri di fessurazione. È una copia indipendente. In un progetto nasce un nuovo foglio nella stessa sezione; un muro autonomo resta aperto e la sezione c.a. si apre in una nuova finestra. Il trasferimento non è disponibile per muri a gravità o mensole senza reazioni valide.

**Relazione Word** esporta input, stratigrafia, armature, matrice completa, audit delle spinte, equilibrio, inviluppi, sollecitazioni alle radici, metodi e limiti. Include la sezione con i carichi e i diagrammi N/M/V del fusto e delle mensole per la combinazione selezionata; le didascalie identificano il caso rappresentato. JSON e CSV conservano i controlli campionati; JSON contiene anche l'intera matrice e i dettagli. I controlli non disponibili restano espliciti nell'inviluppo.

## Archivi e verifiche

Schema v2: family, geometry, layers, foundation, water, actions, combinations, materials, reinforcement, seismic, extensions. Gli archivi v1 restano leggibili e vengono migrati nel workspace, conservando la correlazione q/H/N nel gruppo legacy. Il contenitore loads resta per compatibilità e non alimenta il motore v2. Ogni componente in extensions e le tipologie future bloccano il calcolo finché non esiste il relativo motore.

Supporto: test in `supporto/test/RetainingWall.Checks` e `supporto/test/Desktop/RetainingWallSmokeChecks.cs`; output in `supporto/artefatti/muri_sostegno/revisione-input`. Comandi:

La revisione del sisma guidato e dello scambio terreno è verificata in `supporto/artefatti/muri_sostegno/sisma-terreno`: controlli numerici, flussi dell’interfaccia e relazione Word con dati del sito. La precedente verifica della scala colori è in `supporto/artefatti/muri_sostegno/tassi-sisma`.

Esito 28/09/2026: 163 controlli numerici, 41 controlli dell’interfaccia e 988 controlli dell’audit progetto superati; compilazione Release senza errori né avvisi. Relazione del caso sismico convertita in PDF (10 pagine) e verificata visivamente.

```powershell
dotnet run --project supporto/test/RetainingWall.Checks -c Release -- supporto/artefatti/muri_sostegno/revisione-input/numerica
dotnet build X.Desktop -c Release
X.Desktop/bin/Release/net8.0-windows/ANTHEA.exe --smoke-retaining-wall supporto/artefatti/muri_sostegno/revisione-input/ui
```

## Fonti

- [NTC 2018, §§4.1, 6.2.4, 6.5 e 7.11](https://www.gazzettaufficiale.it/eli/id/2018/2/20/18A00716/sg).
- [JRC, Geotechnical Design Worked Examples](https://eurocodes.jrc.ec.europa.eu/sites/default/files/2022-06/2013_06_WS_GEO.pdf).
- [Wood 1973, Earthquake-induced soil pressures on structures](https://authors.library.caltech.edu/records/48499-83239).
- [Yi 2013, Seismic Design of Restrained Rigid Walls](https://www.cfms-sols.org/sites/default/files/Actes/3521-3524.pdf).
- [USACE EM 1110-2-1902, Slope Stability](https://www.publications.usace.army.mil/Portals/76/Publications/EngineerManuals/EM_1110-2-1902.pdf), riferimento del metodo Bishop semplificato.
