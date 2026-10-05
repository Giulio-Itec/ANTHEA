"""Rev18: independent FEM/RC updates and reinforcement segment workflow."""
from pathlib import Path
import hashlib,json,shutil,subprocess,sys
ROOT=Path(__file__).resolve().parents[3];SUP=ROOT/'supporto';ARCH=SUP/'SUPERATI/palo-tratti-rev18-20261005'
paths=[SUP/f'docs/guida-{k}-anthea.{e}' for k in ('pratica','teorica') for e in ('md','pdf')]
paths+=list((SUP/'documentazione/Guide_ANTHEA').glob('*Rev17.*'))
paths += [SUP/'README.md',SUP/'README.pdf',SUP/'installer/Indice-guide.md',SUP/'installer/Indice-guide.pdf']
registry=[]
for p in paths:
    if not p.exists():continue
    dest=ARCH/p.relative_to(SUP);dest.parent.mkdir(parents=True,exist_ok=True)
    if not dest.exists():shutil.copy2(p,dest)
    registry.append(dict(origine=str(p.relative_to(ROOT)),archivio=str(dest.relative_to(ROOT)),motivo='Rev18: MRd parallelo, FEM indipendente e gestione costruttiva dei tratti',sostituzione=str(p.relative_to(ROOT)).replace('Rev17','Rev18'),sha256=hashlib.sha256(dest.read_bytes()).hexdigest()))
(ARCH/'registro.json').write_text(json.dumps(registry,ensure_ascii=False,indent=2),encoding='utf-8')
practical='''### Tratti colorati e verificatore della sezione

Le schede dei tratti mostrano quote iniziale e finale, lunghezza, origine dell'armatura, numero e diametro delle barre, diametro e passo delle staffe. Ogni tratto ha un colore, ripreso nel pannello verticale dei diagrammi. A destra, il riquadro Sezione e verifiche permette di selezionare il tratto e il controllo: N–M, taglio, interferro, minimi, copriferro, sviluppo oppure distinta. Gli esiti mancanti restano indicati come da completare. Un esito N–M–V favorevole non certifica automaticamente i dettagli costruttivi.

Apri verificatore c.a. apre il modulo esistente con la sezione selezionata. Le azioni iniziali provengono dalla quota critica e rimangono modificabili nella scheda di approfondimento; N è negativo a compressione in questo foglio. Le combinazioni proprie sono conservate alla chiusura e riutilizzate alla riapertura. Geometria e materiali vengono riallineati al palo all'apertura. Il comando Applica armatura trasferisce esplicitamente soltanto numero/diametri delle barre e diametro/passo delle staffe. Sul primo tratto aggiorna la sezione principale; sugli altri crea un'armatura personalizzata. Le azioni inserite nel foglio c.a. non modificano i carichi del FEM.

Dimensiona espone quattro cataloghi modificabili: quantità di barre, diametri longitudinali, diametri delle staffe e passi. Checker ordina per area longitudinale crescente, quindi per quantità di staffe per metro; la prima disposizione accettabile è verificata con N–V–M concomitanti a tutte le ascisse. Interferro e minimi applicabili partecipano alla scelta. Il copriferro richiede cmin,dur dal progetto di durabilità: senza questo dato il relativo esito rimane da completare.

Proponi suddivisione usa quote su griglia di 0,5 m, mantenendo esatta la punta, e non genera tratti inferiori a 3 m. Considera domanda, sviluppo delle barre e lunghezze commerciali preferite 6/8/10/12 m, comprensive della sovrapposizione. Se i vincoli sono incompatibili presenta una spiegazione, senza introdurre tratti corti. La proposta deve essere applicata esplicitamente e conserva nell'archivio i tratti sostituiti. Le quote e il numero dei tratti restano modificabili.

La sovrapposizione iniziale è 60φ, arrotondata per eccesso al decimetro, per preferenza dell'utente; non è attribuita a una norma. Il riquadro confronta lunghezza iniziale, richiesta dal verificatore e adottata, che è la maggiore. φ24 dà 1,44 m, arrotondati a 1,50 m. La distinta di ciascun tratto indica il gruppo continuo, quantità, diametro, quote fisiche, lunghezza di taglio, barra commerciale e sovrapposizione. Un gruppo che attraversa più tratti è unico: le righe mostrate nei singoli tratti sono richiami, non quantità da sommare nuovamente. Le staffe sono descritte con diametro e passo; forma di chiusura, sviluppo e distinta esecutiva delle staffe restano da definire.

### Ricalcolo delle sollecitazioni e delle resistenze

Il FEM viene reso disponibile prima delle verifiche resistenti. Modificare solo barre o staffe conserva N, V, M, spostamenti e mesh, ricalcolando MRd e i dettagli. Modificare terreni, carichi, geometria, EJ o quote di partizione aggiorna anche il FEM. Le quote di partizione sono nodi del modello numerico. Le verifiche e le esportazioni complete vengono invalidate immediatamente; un calcolo superato non può sostituire quello corrente. MRd usa calcoli paralleli con solutori indipendenti e conserva i risultati già disponibili per la stessa sezione e gli stessi valori esatti di N.

'''
theory='''### Calcolo parallelo e dipendenze dei risultati

La risposta FEM dipende da geometria, EJ, terreno, vincoli, carichi, peso e discretizzazione. Con EJ lordo e peso unitario complessivo del c.a. già adottati, cambiare l'armatura non cambia N–V–M. Le verifiche sono un secondo stadio. Il codice confronta una firma degli input fisici prima di riutilizzare la risposta e blocca le esportazioni fino al completamento delle nuove verifiche. Le quote dei tratti appartengono alla discretizzazione e possono richiedere il ricalcolo FEM.

Per ogni sezione sono raccolti gli N distinti esatti, senza arrotondamenti né interpolazioni del dominio. Ogni worker usa un'istanza indipendente del solutore di sezione; la costruzione delle mesh avviene in sequenza, poi le ricerche MRd nei due versi sono parallele. Un batch viene pubblicato soltanto se completato e ancora valido. I valori già calcolati sono riutilizzati soltanto con la medesima configurazione di sezione/materiali/solutore. Cancellazione e controllo di revisione impediscono la pubblicazione di risultati superati. Il numero di worker è limitato alle CPU disponibili, lasciandone una libera quando possibile.

### Sovrapposizioni iniziali e suddivisione costruttiva

La convenzione software autorizzata dall'utente è l0,iniziale = arrotondamento superiore a 0,10 m di 60φ, con φ convertito in metri. Il fattore è modificabile e registrato. La lunghezza adottata è max(l0,iniziale; l0,richiesta dal motore di aderenza); il default non sostituisce la verifica normativa. I risultati conservano separatamente le tre grandezze e lbd. Le regole del motore di aderenza, il trattamento della percentuale di barre sovrapposte e lo spostamento a_l già descritti restano invariati.

La proposta dei tratti è una ricerca discreta su quote multiple di 0,5 m, oltre alla punta esatta. Ogni tratto misura almeno max(3 m; minimo assegnato). La lunghezza teorica e lo sviluppo max(lbd,l0)+a_l riservato conservativamente a entrambe le estremità devono entrare in una delle barre 6/8/10/12 m consentite dal limite assegnato. Il criterio penalizza sfridi e variazioni interne della domanda, oltre al numero di tratti. Si tratta di una convenzione esecutiva preliminare del software, non di una prescrizione della fonte. L'assenza di soluzione viene segnalata senza ridurre il minimo.

La distinta mantiene continue le barre con identica disposizione su tratti adiacenti. I pezzi preferiscono lunghezze commerciali 6/8/10/12 m; l'ultimo può essere tagliato alla quota fisica richiesta. Gli sfridi non aggiungono capacità. I giunti sono raggruppati e restano da completare per sfalsamento e confinamento. La distinta longitudinale non è ancora una distinta esecutiva completa di sagomatura delle staffe.

Interferro e copriferro richiamano le regole comuni di MemberDetailingCalculator. Le regole specifiche dei pilastri non sono trasferite automaticamente ai pali. L'interferro minimo è max(20 mm, φmax, dg+5 mm); la distanza effettiva viene calcolata su tutte le coppie di barre. Copriferro nominale e margine geometrico richiedono cmin,dur e Δcdev. I minimi pali già documentati rimangono distinti, con conferma del campo applicabile. Il loro mancato rispetto o l'interferro insufficiente impediscono l'accettazione del candidato nel dimensionamento. Le verifiche di sezione, le prescrizioni costruttive e gli esiti pendenti sono presentati separatamente.

I test in supporto/test/ElasticPile.Checks e ElasticPile.UiChecks confrontano MRd seriale/parallelo, conservazione del FEM al cambio armatura, cancellazione, conversione dei segni nel foglio c.a., azioni personali persistenti, arrotondamento di 60φ, griglia e lunghezze minime, sovrapposizioni fisiche e interferro con riferimento indipendente sulla corda di una corona circolare. Le misure riproducibili seriale/parallelo/cache sono in ElasticPile.Performance; evidenze in supporto/artefatti/palo-parallelo.

'''
for kind,addition in [('pratica',practical),('teorica',theory)]:
    p=SUP/f'docs/guida-{kind}-anthea.md';s=p.read_text(encoding='utf-8-sig')
    marker='### Tratti colorati e verificatore della sezione' if kind=='pratica' else '### Calcolo parallelo e dipendenze dei risultati'
    if marker not in s:
        pos=s.index('### Tratti e verifica delle sezioni') if kind=='pratica' else s.index('### Fonti e validazione dell’estensione strutturale') if '### Fonti e validazione dell’estensione strutturale' in s else s.index("### Fonti e validazione dell'estensione strutturale")
        s=s[:pos]+addition+s[pos:]
    s=s.replace('revisione documentale 17','revisione documentale 18')
    s=s.replace('La proposta automatica dei tratti usa variazioni delle classi di utilizzo di ampiezza 0,25 e vincoli di lunghezza, riservando max(lbd,l0)+a_l allo sviluppo: è una scelta software dichiarata.', 'La proposta automatica adotta la griglia, i limiti e il criterio descritti nella sezione sulla suddivisione costruttiva; resta una scelta software dichiarata.')
    s=s.replace('con quantità e passi forniti dall\'utente.', 'con quantità, diametri longitudinali, diametri delle staffe e passi forniti dall\'utente.')
    p.write_text(s,encoding='utf-8')
for p in (SUP/'README.md',SUP/'installer/Indice-guide.md'):
    s=p.read_text(encoding='utf-8-sig').replace('Rev17','Rev18').replace('Revisione 17','Revisione 18');p.write_text(s,encoding='utf-8')
p=SUP/'scripts/Build-AntheaGuides-Itec.py';s=p.read_text(encoding='utf-8').replace("REVISION = '17'","REVISION = '18'").replace("REVISION_NOTE = 'PALO ELASTICO E VERIFICHE PER TRATTO'","REVISION_NOTE = 'PALO ELASTICO ARMATURE E CALCOLO PARALLELO'");p.write_text(s,encoding='utf-8')
p=SUP/'scripts/Render-AntheaGuides-Itec.ps1';s=p.read_text(encoding='utf-8-sig').replace("$Revision = '17'","$Revision = '18'");p.write_text(s,encoding='utf-8-sig')
art=SUP/'artefatti/guide_anthea_itec_rev18';art.mkdir(parents=True,exist_ok=True)
(art/'artifact.md').write_text('Revisione 18 delle due guide globali sul modello ITEC esistente. MRd parallelo, FEM indipendente, verificatore c.a. e tratti. Conservare struttura e stile del modello; controllare resa Word/PDF prima di archiviare Rev17.',encoding='utf-8')
subprocess.run([sys.executable,str(SUP/'scripts/wiki/build-wiki-index.py')],check=True)
subprocess.run([sys.executable,str(SUP/'scripts/Build-AntheaGuides-Itec.py')],check=True)
for p in (SUP/'README.md',SUP/'installer/Indice-guide.md'):subprocess.run([sys.executable,str(SUP/'scripts/documentazione/markdown-pdf.py'),str(p)],check=True)
