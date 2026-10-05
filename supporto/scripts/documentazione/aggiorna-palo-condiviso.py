"""Archive revision 15 and integrate shared lateral-pile inputs into revision 16."""
from pathlib import Path
import hashlib, json, shutil, re, subprocess, sys
ROOT=Path(__file__).resolve().parents[3];SUP=ROOT/'supporto';ARCH=SUP/'SUPERATI/palo-condiviso-rev16-20261005'
paths=[SUP/f'docs/guida-{kind}-anthea.{ext}' for kind in ('pratica','teorica') for ext in ('md','pdf')]
paths+=list((SUP/'documentazione/Guide_ANTHEA').glob('*Rev15.*'))
paths+=[SUP/'README.md',SUP/'README.pdf',SUP/'installer/Indice-guide.md',SUP/'installer/Indice-guide.pdf']
registry=[]
for p in paths:
    if not p.exists():continue
    dest=ARCH/p.relative_to(SUP);dest.parent.mkdir(parents=True,exist_ok=True)
    if not dest.exists():shutil.copy2(p,dest)
    registry.append(dict(origine=str(p.relative_to(ROOT)),archivio=str(dest.relative_to(ROOT)),motivo='Rev16: palo elastico con dati condivisi, EJ e valori iniziali tracciati',sostituzione=str(p.relative_to(ROOT)).replace('Rev15','Rev16'),sha256=hashlib.sha256(dest.read_bytes()).hexdigest()))
(ARCH/'registro.json').write_text(json.dumps(registry,ensure_ascii=False,indent=2),encoding='utf-8')
def replace_once(s,old,new):
    assert s.count(old)==1,old[:90]
    return s.replace(old,new,1)
practical='''## Risposta elastica del palo orizzontale

Il modulo Palo orizzontale comprende la scheda Risposta elastica · trave su molle. Calcola deformazioni e sollecitazioni sotto la forza assegnata con il motore ElasticPile di Checker. La scheda Dati comuni e capacità · Broms / stratificato conserva le verifiche di capacità limite. Le due analisi usano gli stessi input di geometria, carico e terreno; i risultati restano distinti. Il percorso è Moduli singoli → Geotecnica → Palo → Orizzontale; la risposta è disponibile anche nel Micropalo orizzontale.

### Dati iniziali condivisi

Compilare geometria, materiale, carico, falda e stratigrafie nella scheda iniziale. Ogni dato ha un solo punto di modifica. Nella scheda elastica rimangono la scelta esplicita della stratigrafia analizzata, il passo FEM e il vincolo alla punta. Modifica dati e strati richiama l'editor iniziale. Il comando di copia dalla capacità è stato eliminato.

La lunghezza del campo principale rimane la lunghezza infissa. Il tratto libero è una quantità aggiuntiva: la lunghezza totale del modello è infissa più libera. L'eccentricità condivisa indica la quota della forza sopra il piano campagna, come nel modulo precedente. Checker ricava il momento equivalente alla testa C = H (Llibero − e_da_pc), nella convenzione con θ = dy/dx: il braccio del tratto libero non viene contato due volte. Con tratto libero nullo ed eccentricità positiva il momento interno al piano campagna è H e. Non è presente un secondo campo modificabile C nella scheda elastica.

EJ viene mostrato in sola lettura con E, J, unità e origine. Per il palo in calcestruzzo si usa la sezione circolare integra lorda, con Ecm del materiale: sono esclusi il contributo aggiuntivo delle armature, fessurazione e viscosità. Per il micropalo CHS si usa il solo tubo di acciaio; il contributo della malta non è incluso. Il modulo elastico del tubo è nei dati iniziali di sezione. L'opzione avanzata Override EJ, nello stesso editor, richiede valore e motivazione; conserva anche il valore geometrico di base. Il momento resistente My della verifica Broms non sostituisce EJ.

La testa può ruotare liberamente oppure avere rotazione impedita; lo spostamento orizzontale rimane libero. La punta è libera per impostazione iniziale; cerniera e incastro richiedono una scelta esplicita. Carichi negativi invertono i segni della risposta elastica; la capacità Broms mantiene i propri limiti di applicabilità.

### Parametri degli strati

Nella finestra iniziale selezionare uno strato della stratigrafia. Il pannello sottostante contiene legge di rigidezza, addensamento o categoria di consolidazione, riga bibliografica e soli parametri pertinenti. I pesi unitari naturale e saturo sono quelli già nella tabella degli strati. Non esiste una seconda stratigrafia elastica modificabile. Se vi sono più stratigrafie, selezionare quella analizzata nella scheda elastica; aggiungendo o togliendo stratigrafie la selezione deve essere confermata.

| Modalità | Input | Valore adottato |
| --- | --- | --- |
| kh costante assegnato | kh [kN/m³] | k = kh D |
| Reese–Matlock con nh assegnato | nh [kN/m³] | kh = nh z/D; k = nh z |
| Tabella 14.5 per sabbie | Addensamento e falda condivisa | Valore pertinente a sabbia immersa o non immersa |
| Tabella 14.6 per coesivi | Singola riga e autore | Media iniziale dell'intervallo; nh modificabile [N/cm³] |
| Correlazione A γ/1,35 | Addensamento e A; pesi unitari comuni | Media iniziale di A; sotto falda γ′ = γsat − γw |
| k distribuito assegnato | k [kN/m²] | Nessuna seconda moltiplicazione per D |

kh costante è associato nel testo alle argille sovraconsolidate; la legge lineare a incoerenti e argille normalmente consolidate o debolmente sovraconsolidate. Indicare condizioni di impiego, drenaggio e livello di deformazione. Il nome dello strato non è sufficiente per assegnare una rigidezza: addensamento e fonte devono essere completati.

La media aritmetica degli estremi è la convenzione iniziale del software richiesta dall'utente, non una raccomandazione di Viggiani. Il pannello mantiene distinti intervallo, media, eventuale valore consigliato e valore adottato. Per A di sabbia media: intervallo 300–1000, media iniziale 650, consigliato 600. Per la riga Reese–Matlock della tabella 14.6: intervallo 0,2–3,5 e media 1,85 N/cm³. Una riga a valore unico usa quel valore. Non si mediano autori diversi.

Le modifiche manuali restano salvate e non vengono sovrascritte dal ricalcolo. Cambiando teoria o categoria di una scelta già compilata, usare Ripristina media della riga oppure Mantieni valore e conferma nuova fonte. Il parametro adottato deve comunque rispettare l'intervallo della nuova riga; un nh esterno richiede l'override motivato. Le modalità assistite conservano parametro di base, override e motivazione.

La falda condivisa divide gli strati quando cambia il parametro adottato: la tabella delle sabbie passa al valore immerso e la correlazione usa γ′. Le modalità manuali e la tabella 14.6 mantengono il parametro assegnato. z parte dal piano campagna ed esclude il tratto libero, senza azzerarsi alle interfacce. Il raccordo a strati è una convenzione numerica dichiarata, non una prescrizione originale del libro.

### Profilo e diagrammi

Il profilo del palo è a sinistra; k, y, θ, V, M e q sono affiancati con la stessa profondità. Ogni diagramma può essere nascosto. I controlli separati attivano stratigrafia, falda, kh, nodi, molle equivalenti, carichi e vincoli. Per kh variabile sono indicati legge e valori agli estremi dei tratti. Con mesh fitta i dettagli si leggono selezionando una quota; clic ripetuti a un'interfaccia alternano i due lati.

kh e nh sono in kN/m³, k distribuita in kN/m²; K* illustrativa in kN/m è l'integrale di k sulla lunghezza tributaria del nodo. Le molle disegnate non sostituiscono la matrice consistente usata dal FEM. Il dettaglio del nodo riporta i limiti della sua lunghezza tributaria. I salti sono conservati, senza interpolazione attraverso la discontinuità.

Il pannello Estremi, equilibrio e convergenza mostra minimi e massimi con segno, massimi assoluti e quote, reazioni e residui. x è la distanza dalla testa, z dal piano campagna; H e y sono positivi verso destra, θ = dy/dx, M = EJ y″, V = M′, q = −k y. Le spiegazioni estese e le fonti sono nel comando Info.

Il ricalcolo è automatico. Ogni modifica agli input invalida subito i risultati precedenti e blocca l'esportazione fino al nuovo risultato valido. La mesh h è confrontata con h/2; si mostra la soluzione fine. La soglia di confronto è 0,1% per ytesta, massimo assoluto M e massimo assoluto V. Raffinare la mesh segnala il mancato raggiungimento della soglia e non costituisce una verifica geotecnica.

### Archivio e migrazione

Salva conserva dati comuni, stratigrafia selezionata, teorie, parametri, origine dei valori e opzioni di vista. JSON, CSV e report Word includono EJ, intervalli, medie, valori consigliati, override, mesh e risultati per ascissa. L'esportazione generale usa la scheda attiva. I risultati strutturati per sezione sono una predisposizione alle future verifiche resistenti, che non sono ancora implementate.

Un archivio precedente con input elastici separati richiede un confronto esplicito. Usa dati comuni e conserva legacy mantiene l'assetto principale; Importa legacy nei dati comuni traduce lunghezze ed eccentricità, aggiunge una stratigrafia e conserva EJ come override motivato. La stratigrafia importata va classificata prima di usarla in Broms. Gli originali elastici e l'intero stato precedente alla migrazione sono conservati nell'archivio. Nessun ramo viene eliminato silenziosamente. Un momento puro legacy con H = 0 non è rappresentabile mediante la sola coppia condivisa H/e: l'importazione viene bloccata, senza perdere il modello originale. Anche momento ed eccentricità legacy entrambi non nulli richiedono risoluzione.

### Esempio riproducibile e limiti

Nei dati iniziali: D = 1 m, lunghezza infissa 30 m, tratto libero zero, H = 100 kN, e da piano campagna zero, testa libera. Attivare Override EJ di 50000 kN m² con motivazione Benchmark numerico. Uno strato spesso 30 m usa kh costante = 10000 kN/m³, fonte Esempio numerico assegnato. Nella scheda elastica selezionare la stratigrafia, punta libera e passo iniziale 0,50 m. La soluzione fine a 0,25 m fornisce ytesta = 0,0094574084 m e Mmax = 68,178634 kNm a x circa 1,660914 m. Sono parametri per controllare il codice, non valori consigliati per un terreno reale.

Il modello esclude plasticità, distacco, curve p-y, carico assiale, secondo ordine, ciclicità e interazione di gruppo. Le molle reagiscono nei due versi. EJ integra non descrive automaticamente la fessurazione. Nessun fattore di efficienza della palificata è applicato al palo singolo. La convergenza non attesta resistenza strutturale o capacità del terreno.

'''
p=SUP/'docs/guida-pratica-anthea.md';s=p.read_text(encoding='utf-8-sig');start=s.index('## Risposta elastica del palo orizzontale\n');end=s.index('\n## ',start+4);s=s[:start]+practical+s[end:];p.write_text(s,encoding='utf-8')
p=SUP/'docs/guida-teorica-anthea.md';s=p.read_text(encoding='utf-8-sig')
s=replace_once(s,'Tale procedura non è impiegata qui: non si fornisce un fittizio K nodale scalare al posto della matrice consistente.','Qui si calcola K* esclusivamente per la rappresentazione e la tabella dei nodi: il solutore continua a usare la matrice consistente. La lunghezza tributaria va dai punti medi dei due elementi adiacenti; agli estremi è una sola mezza lunghezza. Checker integra k su questi tratti, separatamente sui due lati di eventuali discontinuità. Non si assegnano molle scalari al posto della matrice FEM.')
s=replace_once(s,'A deve essere scelto esplicitamente entro l\'intervallo del relativo addensamento; il valore consigliato viene mostrato ma non assegnato silenziosamente.','A è inizializzato alla media aritmetica dell’intervallo della singola riga, per preferenza esplicita dell’utente: 200, 650 o 2000. Il valore consigliato rimane distinto: 200, 600 o 1500. La media iniziale è una convenzione del software, non un valore consigliato da Viggiani. L’utente può modificarla e il ricalcolo conserva la scelta.')
s=replace_once(s,"Per un intervallo l'utente deve scegliere il valore: non viene adottata la media.",'Per ogni intervallo si adotta inizialmente la sua media aritmetica, modificabile. Una riga a valore unico conserva quel valore. Intervallo, media iniziale, valore adottato e autore sono distinti; non si mediano righe o autori diversi. Cambiando fonte, la scelta precedente deve essere mantenuta o sostituita esplicitamente.')
s=replace_once(s,'Si assume EI positivo e costante, assegnato con origine esplicita.','Si assume EJ positivo e costante, calcolato in Checker dai dati comuni della sezione e del materiale, con origine esplicita.')
s=replace_once(s,"H agisce sulla traslazione della testa; il carico generalizzato rotazionale è C, assegnato direttamente oppure ottenuto da H e. Inserire contemporaneamente C ed e non nulli è vietato. La lunghezza libera produce già il proprio braccio interno: e rappresenta solo un eventuale momento equivalente ulteriore rispetto alla testa.","H agisce sulla traslazione della testa. Nel solutore generale C può essere assegnato oppure ottenuto da un’eccentricità riferita alla testa, senza sommare i due input. Nell’interfaccia con dati condivisi l’eccentricità esistente e è invece la quota della forza sopra il piano campagna. Checker adatta tale convenzione con C = H (Llibero − e); la lunghezza totale è Linfissa + Llibero. Il momento interno al piano campagna dovuto a H è quindi H e, indipendentemente dalla ripartizione del braccio. La migrazione conserva esplicitamente la convenzione dei vecchi modelli separati.")
extra=r'''### Rigidezza della sezione e dati condivisi

ElasticPileSection in Checker riutilizza ConcreteMaterialEN1992.Ecm e le inerzie SectionCircular e SectionCHS di Model. Per la sezione circolare in calcestruzzo:

$$ E_{cm}=22000\left(\frac{f_{ck}+8}{10}\right)^{0.3},\quad J=\frac{\pi D^4}{64}

Ecm è in MPa; con D in mm, J è in mm⁴. Si usa la sezione integra lorda in calcestruzzo, senza aggiungere il contributo delle armature. Fessurazione e viscosità non sono introdotte automaticamente. Per CHS, J = π (De⁴ − Di⁴)/64 ed E è il modulo dell’acciaio assegnato nell’editor iniziale; il contributo della malta è escluso. Non è attiva un’ipotesi di collaborazione composta implicita.

$$ EJ\,[\mathrm{kNm^2}]=\frac{E\,[\mathrm{MPa}]\,J\,[\mathrm{mm^4}]}{10^9}

L’override avanzato richiede un valore positivo e una motivazione. I risultati conservano EJ di base, EJ adottato, E, J, geometria e riferimento della sezione. È una rigidezza assegnata del modello elastico: My della capacità laterale è una grandezza diversa.

Geometria, carichi e strati sono unici nell’archivio ANTHEA. La risposta memorizza riferimenti e un’istantanea dei dati effettivamente risolti per consentire esportazioni verificabili; tale istantanea non è una seconda sorgente modificabile. La selezione della stratigrafia è esplicita. Un archivio legacy conserva i valori originali fino alla scelta dell’utente e archivia lo stato precedente alla migrazione.

Checker restituisce SectionDemands per ascissa e lato, con V e M con segno, riferimento a geometria e materiale e riferimenti agli estremi. Questi dati alimenteranno le verifiche di resistenza lungo il palo. In questa revisione non sono calcolati coefficienti di utilizzo né esiti normativi di sezione.

'''
s=replace_once(s,'### Discretizzazione e recupero delle sollecitazioni\n',extra+'### Discretizzazione e recupero delle sollecitazioni\n')
s=s.replace('comprendono 101 asserzioni','comprendono 124 asserzioni')
s=replace_once(s,'Le evidenze sono in supporto/artefatti/palo-elastico.','Le evidenze originarie sono in supporto/artefatti/palo-elastico; la revisione con dati comuni usa supporto/artefatti/palo-condiviso. I nuovi controlli verificano medie distinte dai valori consigliati, provenienza e override, EJ circolare e tubolare rispetto a riferimenti analitici, conversioni, lunghezze tributarie, risultati per sezione e assenza di doppio conteggio dell’eccentricità. Le prove ANTHEA coprono dati comuni, migrazione con confronto della risposta legacy, ricalcolo, invalidazione, visibilità ed esportazioni; le regressioni esistenti comprendono 1080 controlli del palo orizzontale, CHS e 1645 controlli stratificati.')
p.write_text(s,encoding='utf-8')
for kind in ('pratica','teorica'):
    p=SUP/f'docs/guida-{kind}-anthea.md';s=p.read_text(encoding='utf-8');s=s.replace('Edizione 5 del 4 ottobre 2026 — revisione documentale 15','Edizione 5 aggiornata il 5 ottobre 2026 — revisione documentale 16');p.write_text(s,encoding='utf-8')
for p in (SUP/'README.md',SUP/'installer/Indice-guide.md'):
    s=p.read_text(encoding='utf-8-sig').replace('Rev15','Rev16').replace('Revisione 15','Revisione 16').replace('4 ottobre 2026','5 ottobre 2026');p.write_text(s,encoding='utf-8')
p=SUP/'scripts/Build-AntheaGuides-Itec.py';s=p.read_text(encoding='utf-8');s=s.replace("REVISION = '15'","REVISION = '16'").replace("DATE = '04/10/2026'","DATE = '05/10/2026'").replace("CONTENTS = '4 ottobre 2026'","CONTENTS = '5 ottobre 2026'").replace("CONTENTS_ISO = '2026-10-04'","CONTENTS_ISO = '2026-10-05'").replace("REVISION_NOTE = 'INTEGRAZIONE DEI CONTENUTI NEL HANDBOOK'","REVISION_NOTE = 'PALO ELASTICO DATI CONDIVISI E RIGIDEZZE'");p.write_text(s,encoding='utf-8')
p=SUP/'scripts/Render-AntheaGuides-Itec.ps1';s=p.read_text(encoding='utf-8-sig').replace("$Revision = '15'","$Revision = '16'");p.write_text(s,encoding='utf-8-sig')
art=SUP/'artefatti/guide_anthea_itec_rev16';art.mkdir(parents=True,exist_ok=True)
(art/'artifact.md').write_text('Aggiornamento dei due volumi globali Rev16. Modello ITEC esistente invariato; archiviazione Rev15; fonti e formule verificate conservate. Dati condivisi, medie iniziali, EJ e visualizzazione del palo. Verifica con Word e PDF renderizzati.',encoding='utf-8')
subprocess.run([sys.executable,str(SUP/'scripts/wiki/build-wiki-index.py')],check=True)
subprocess.run([sys.executable,str(SUP/'scripts/Build-AntheaGuides-Itec.py')],check=True)
for p in (SUP/'README.md',SUP/'installer/Indice-guide.md'):
    subprocess.run([sys.executable,str(SUP/'scripts/documentazione/markdown-pdf.py'),str(p)],check=True)
print('Rev16 sources and Word authored; refresh Word fields and validate before archiving superseded Word/PDF.')
