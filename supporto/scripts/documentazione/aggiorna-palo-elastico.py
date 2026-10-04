"""Revision 12: integrate elastic pile and updated pile-group UI into the canonical guides."""
from pathlib import Path
import json, shutil, hashlib, re, importlib.util, subprocess, sys
ROOT=Path(__file__).resolve().parents[3]
SUP=ROOT/'supporto'
ARCH=SUP/'SUPERATI/palo-elastico-rev12-20261002'
paths=[SUP/f'docs/guida-{k}-anthea.{e}' for k in ('pratica','teorica') for e in ('md','pdf')]
paths+=list((SUP/'documentazione/Guide_ANTHEA').glob('*Rev11.*'))
paths += [SUP/'installer/Indice-guide.md',SUP/'installer/Indice-guide.pdf',SUP/'README.md',SUP/'README.pdf',SUP/'installer/README.md',SUP/'installer/README.pdf']
registry=[]
for p in paths:
    if not p.exists(): continue
    dest=ARCH/p.relative_to(SUP);dest.parent.mkdir(parents=True,exist_ok=True)
    if not dest.exists():shutil.copy2(p,dest)
    registry.append(dict(origine=str(p.relative_to(ROOT)),archivio=str(dest.relative_to(ROOT)),motivo='Revisione 12: risposta elastica del palo e UI palificate',sostituzione=str(p.relative_to(ROOT)).replace('Rev11','Rev12'),sha256=hashlib.sha256(dest.read_bytes()).hexdigest()))
(ARCH/'registro.json').write_text(json.dumps(registry,ensure_ascii=False,indent=2),encoding='utf-8')
pratica=r'''
## Risposta elastica del palo orizzontale

Il modulo Palo orizzontale comprende la scheda Risposta elastica · trave su molle. Calcola spostamenti e sollecitazioni sotto la forza assegnata. La scheda Capacità laterale conserva Broms e l'estensione stratificata preesistenti, i cui diagrammi si riferiscono al carico limite. I due risultati rispondono a domande diverse e hanno parametri distinti salvati nello stesso foglio. La nuova analisi è disponibile anche nel modulo Micropalo orizzontale, con EI assegnato esplicitamente.

### Compilazione dei dati

Aprire Moduli singoli → Geotecnica → Palo → Orizzontale, quindi Risposta elastica · trave su molle. Inserire diametro geotecnico D, lunghezza totale testa–punta e lunghezza libera sopra il terreno, in metri. La lunghezza immersa è la differenza fra totale e libera. Il comando Copia geometria, H e stratigrafia dalla capacità riprende diametro, lunghezza infissa, forza, vincolo di testa e prima stratigrafia; imposta tratto libero e momento a zero e lascia vuote le rigidezze del terreno. La copia è esplicita e sostituisce gli strati elastici. Le successive modifiche delle due analisi rimangono indipendenti.

Assegnare EI in kN m² e descriverne l'origine, ad esempio E, I e ipotesi di fessurazione. Non usare il momento resistente My al posto di EI. Il diametro controlla la larghezza di interazione col terreno; non ricalcola automaticamente la rigidezza strutturale. Per un micropalo specificare se EI riguarda il solo tubo o una sezione composta e giustificare l'ipotesi adottata.

H è applicata alla testa del modello. Inserire alternativamente il momento C oppure l'eccentricità e, da cui C = H e; entrambi diversi da zero costituiscono errore. e è un braccio equivalente rispetto alla testa, non la lunghezza libera del palo: questa è già rappresentata geometricamente. Sono ammessi carichi positivi, negativi e nulli. La testa può ruotare liberamente oppure avere rotazione impedita; lo spostamento orizzontale resta libero. La punta è libera per impostazione iniziale; cerniera e incastro devono essere scelti esplicitamente.

### Assegnazione delle rigidezze

Ogni strato richiede nome, spessore, legge, valore e fonte/condizioni di impiego. I valori iniziali sono vuoti: Granulare o Coesivo non selezionano automaticamente un modulo. Coprire tutta la lunghezza immersa; strati più profondi della punta non contribuiscono. Per un tratto di terreno senza sostegno assegnare valore zero.

| Legge assegnata | Valore da inserire | Significato |
| --- | --- | --- |
| kh costante | kh in kN/m³ | Pressione/spostamento costante nello strato |
| kh = kh,rif z/D | kh,rif in kN/m³ | Crescita lineare con z dal piano campagna |
| k distribuito costante | k in kN/m² | Rigidezza per unità di lunghezza del palo già comprensiva della larghezza |

Queste sono leggi esplicitamente assegnate dall'utente, non correlazioni geotecniche verificate di Viggiani. Per attribuire la legge al testo originale occorrono la pagina con la formula, le pagine con definizioni e campo di applicabilità, edizione/frontespizio e relative tabelle. Il comportamento drenato o non drenato e l'idoneità della rigidezza al livello di carico devono essere documentati nella fonte dello strato. z non riparte da zero nelle interfacce. Sopra il piano campagna non sono presenti molle.

### Lettura e conservazione dei risultati

Il ricalcolo è automatico; ogni modifica invalida immediatamente i risultati precedenti. I dati non validi impediscono CSV, JSON e report del risultato attivo. Il programma confronta la mesh iniziale con una mesh dimezzata e mostra i risultati della seconda. Le variazioni relative di y in testa, massimo assoluto M e massimo assoluto V devono essere al più 0,1%; altrimenti compare Raffinare la mesh. Questa è una verifica numerica fra due mesh, non una garanzia assoluta dell'errore o dell'adeguatezza geotecnica.

Il profilo a sinistra mostra terreno, tratto libero e vincoli. I sei diagrammi affiancati hanno la stessa profondità: k, y, θ, V, M e reazione q. Le scale orizzontali sono indipendenti e dichiarate. I salti del terreno vengono conservati con valori sui due lati e tratto puntinato, senza interpolazione attraverso lo strato. Le barre laterali del palo rappresentano schematicamente il sostegno distribuito; non sono molle concentrate impiegate dal solutore.

Il riepilogo riporta minimi, massimi con segno, massimi assoluti e rispettive quote x dalla testa, oltre a y e θ in testa, reazioni dei vincoli e residui di equilibrio. La tabella e il CSV riportano anche z dal piano campagna, indice dello strato, kh e k con unità. Above significa limite dal lato superiore, Below dal lato inferiore, Interior interno all'elemento; strato zero indica il tratto libero. I valori sono riportati da entrambi i lati degli estremi degli elementi, anche quando coincidono.

Esporta tabella CSV conserva la precisione numerica e le convenzioni dei segni. L'esportazione generale JSON/Word usa l'analisi selezionata: per esportare la capacità tornare alla scheda Capacità laterale. Il report elastico conserva input, fonti assegnate, estremi, equilibrio, convergenza e tabelle. Archiviare il foglio ANTHEA conserva entrambe le analisi; il risultato viene ricalcolato all'apertura.

### Esempio riproducibile e limiti

Assegnare D = 1 m, lunghezza totale 30 m, tratto libero nullo, EI = 50000 kN m² con origine Benchmark numerico, H = 100 kN, C = e = 0, testa e punta libere. Un unico strato spesso 30 m ha kh costante = 10000 kN/m³, fonte Esempio numerico assegnato. Con passo iniziale 0,50 m il risultato a passo 0,25 m è ytesta = 0,0094574084 m e Mmax = 68,178634 kNm a x circa 1,660914 m. Questi parametri costituiscono una prova del codice, non valori consigliati per un terreno reale.

La soluzione non comprende plasticità, distacco, curve p-y, effetti della forza assiale, secondo ordine, consolidazione, ciclicità o gruppo. Le molle elastiche reagiscono in entrambi i versi. La convergenza non verifica la capacità del terreno né le resistenze di sezione. I coefficienti della palificata non vengono trasferiti automaticamente: Rg di kh/nh e p-multiplier non sono intercambiabili senza un modello esplicito.

'''
teorica=r'''
## Risposta elastica del palo orizzontale

Il motore ElasticPile in GPCChecker.Geotechnics risolve una trave di Euler–Bernoulli su fondazione elastica di Winkler sotto azioni assegnate. L'esame del metodo stratificato esistente ha accertato che esso costruisce diagrammi limite ed equilibri di capacità, senza un solutore elastico riutilizzabile. Rimane quindi invariato; non è stato creato un secondo motore per la stessa formulazione. ANTHEA gestisce soltanto archivio, presentazione e adattamento degli input.

### Fonti consultate e attribuzione

La formulazione FEM di riferimento consultata il 2 ottobre 2026 è TU Delft, Computational Modelling, capitolo 4.1 Euler–Bernoulli beam elements, pagina web senza numerazione di pagina: https://interactivetextbooks.citg.tudelft.nl/computational-modelling/structural_linear/euler_bernouilli.html . Sono state consultate la relazione cinematica, l'ipotesi di trascurare le deformazioni da taglio e la discretizzazione Hermite con spostamento e rotazione nodali. La convenzione dei segni qui dichiarata viene usata coerentemente nell'implementazione.

La pagina del catalogo Edizioni Efesto https://www.edizioniefesto.it/libri/fondazioni/ conferma l'opera Fondazioni di Carlo Viggiani ma non consente di verificare le pagine della legge del modulo di reazione. Non sono state consultate pagine originali idonee a confermare edizione, numerazione, parametri, drenaggio o applicabilità della legge richiamata dall'utente. Non viene pertanto dichiarata alcuna formulazione Viggiani verificata. Sono richiesti frontespizio/edizione, pagina della formula e pagine delle definizioni, tabelle e limiti. I riferimenti Viggiani del precedente capitolo Broms non costituiscono una verifica della legge elastica.

### Modulo di reazione e conversioni

Si definisce kh come pressione orizzontale divisa per spostamento: unità F/L³. La larghezza di interazione adottata è il diametro geotecnico D, costante. La pressione elastica sul palo è opposta allo spostamento; la reazione per unità di lunghezza q ha unità F/L.

$$ k = D k_h,\quad q=-k y

La rigidezza distribuita k ha unità F/L². Se l'utente assegna direttamente k, il diametro non viene moltiplicato una seconda volta. In tabella si mostra anche kh equivalente = k/D. Per una discretizzazione a molle concentrate sarebbe K_i = integrale di k sulla lunghezza tributaria, con unità F/L. Tale procedura non è impiegata qui: non si fornisce un fittizio K nodale scalare al posto della matrice consistente.

Sono implementate tre leggi assegnate: kh costante per strato; kh = kh,rif z/D; k distribuito costante per strato. La seconda dà k = kh,rif z, con kh,rif in F/L³ e z misurata dal piano campagna. Se si definisce nh come gradiente di k, in questa precisa legge assegnata nh = kh,rif e ha unità F/L³. Non si estende questa identità a simboli nh impiegati con altre definizioni nelle fonti.

Le leggi non sono selezionate dal nome del terreno e non contengono coefficienti empirici. La scelta e la fonte devono documentare stato tensionale, condizioni drenate/non drenate, livello di deformazione e campo di validità. Non si assume che la crescita lineare sia applicabile indistintamente a sabbie e argille. In una sequenza stratificata ogni strato può avere parametri diversi, ma l'origine z resta unica al piano campagna. Non si azzera a ogni interfaccia.

### Modello e condizioni al contorno

x cresce dalla testa verso la punta; z = x − Llibero. Ltotale > Llibero ≥ 0. Il tratto libero ha k = 0; gli strati coprono almeno tutta la lunghezza immersa. Si assume EI positivo e costante, assegnato con origine esplicita. Piccoli spostamenti, sezioni piane, deformabilità a taglio trascurata e molle bilaterali lineari; nessuna forza assiale nel modello.

La convenzione adottata è H e y positivi verso destra, θ = y′, C positivo nel verso della rotazione nodale, M = EI y″, V = M′ e q = −k y. Ne consegue:

$$ EI y^{(4)} + k y = 0,\quad V'=q,\quad M'=V

H agisce sulla traslazione della testa; il carico generalizzato rotazionale è C, assegnato direttamente oppure ottenuto da H e. Inserire contemporaneamente C ed e non nulli è vietato. La lunghezza libera produce già il proprio braccio interno: e rappresenta solo un eventuale momento equivalente ulteriore rispetto alla testa.

Testa libera: spostamento e rotazione incogniti. Testa con rotazione impedita: θ = 0, traslazione libera e reazione rotazionale calcolata. Punta libera: nessun vincolo cinematico e azioni terminali nulle; cerniera: y = 0; incastro: y = θ = 0. Nessun incastro viene aggiunto per eliminare una labilità. In assenza di terreno il modello è stabile con punta incastrata, oppure con cerniera alla punta e rotazione impedita in testa; negli altri casi viene rifiutato. La fattorizzazione controlla inoltre singolarità e cattivo condizionamento.

Alla testa M = −C − Rθ e V = H. Alla punta i segni delle azioni interne sono coerenti con le reazioni esterne. Tutte le reazioni restituite sono azioni esercitate sul palo. Un carico negativo inverte i segni dell'intera risposta elastica.

### Discretizzazione e recupero delle sollecitazioni

Ogni elemento ha due nodi e quattro gradi di libertà, y1, θ1, y2, θ2. Le funzioni Hermite cubiche interpolano y; la rotazione è la derivata analitica del polinomio. Si dispone sempre un nodo al piano campagna, a ogni interfaccia e alle estremità dove agiscono i carichi. Ciascun intervallo è suddiviso in elementi uniformi con lunghezza non superiore al passo richiesto.

$$ K_e=\int_0^{l_e} EI B^T B\,dx+\int_0^{l_e} k N^T N\,dx

B contiene le derivate seconde delle funzioni di forma. Quattro punti di Gauss integrano esattamente la matrice della fondazione per k costante o lineare, perché il massimo grado dell'integrando è sette. Le componenti traslazione/rotazione hanno unità diverse coerenti con i rispettivi gradi di libertà. La matrice cambia con la mesh; non si assegna la stessa molla a ciascun nodo. La soluzione usa Cholesky simmetrica a banda, con scala diagonale e vincoli imposti senza penalità. Sono ammessi al massimo 4000 elementi per soluzione, inclusa la mesh raffinata.

Le forze terminali dell'elemento provengono dalla matrice completa dell'elemento moltiplicata per i suoi gradi di libertà. Da esse si ricostruiscono V e M integrando analiticamente q = −k y e le equazioni di equilibrio. Non si derivano ripetutamente gli spostamenti campionati. Il recupero equilibrato converge alla relazione costitutiva della trave; a mesh finita è distinto dalla semplice curvatura cubica elementare.

Gli estremi vengono ricercati agli estremi degli elementi e alle radici delle derivate dei polinomi di y, θ, V, M e q. Le radici sono isolate mediante gli intervalli monotoni determinati dalle radici della derivata e bisezione. Otto intervalli per elemento servono al disegno, non limitano la ricerca degli estremi. I lati superiore e inferiore sono conservati separatamente: y e θ sono continui, V e M sono continui alle interfacce senza carichi concentrati salvo residui numerici, mentre k, kh e q possono saltare.

### Equilibrio e convergenza

Le reazioni dei vincoli sono ricavate dal residuo della matrice completa prima dell'eliminazione dei gradi di libertà vincolati. Le risultanti del terreno e i loro momenti rispetto alla testa sono integrali analitici dei polinomi elementari.

$$ R_F=H+\int q\,dx+R_{H,p}
$$ R_M=C+R_{\theta,t}+R_{\theta,p}+L R_{H,p}+\int xq\,dx

Entrambi i residui devono tendere a zero. Il report mostra inoltre numero degli elementi e confronto fra mesh h e h/2. Il risultato conservato appartiene alla mesh fine. Si confrontano ytesta, |M|max e |V|max; variazione relativa non superiore a 0,001 per tutti e tre identifica la soglia soddisfatta. Per un valore nullo si usa una scala riferita all'estremo del diagramma; un modello a carico nullo dà variazioni nulle. Il controllo fra due mesh non sostituisce una stima dell'errore geotecnico. Se la soglia non è soddisfatta i risultati sono segnalati come da raffinare, senza trasformarli in una verifica positiva.

### Riferimenti indipendenti e prove riproducibili

Per un palo semi-infinito con k costante, testa libera e solo H, la soluzione decadente dell'equazione differenziale fornisce un riferimento indipendente dalla discretizzazione:

$$ \beta=\left(\frac{k}{4EI}\right)^{1/4},\quad y_0=\frac{H}{2EI\beta^3}
$$ y(x)=y_0 e^{-\beta x}\cos(\beta x)
$$ V(x)=H e^{-\beta x}\left[\cos(\beta x)-\sin(\beta x)\right]
$$ M(x)=\frac{H}{\beta}e^{-\beta x}\sin(\beta x)

Il primo massimo di M è a x = π/(4β). Con rotazione impedita in testa ytesta è metà del valore libero e la reazione rotazionale è H/(2β). Il test usa D=1 m, L=30 m, EI=50000 kNm², H=100 kN, kh=10000 kN/m³; la lunghezza corrisponde a circa 14,19/β e l'influenza del bordo distante è trascurabile rispetto alle tolleranze adottate. Un ulteriore riferimento è la mensola senza molle con punta incastrata: ytesta = HL³/(3EI) − CL²/(2EI), θtesta = −HL²/(2EI) + CL/EI.

| Passo effettivo [m] | ytesta [m] | Mmax [kNm] | Vmax [kN] |
| --- | --- | --- | --- |
| 1,00 | 0,00945552714 | 68,1749852 | 100 |
| 0,50 | 0,00945729422 | 68,1783709 | 100 |
| 0,25 | 0,00945740841 | 68,1786341 | 100 |
| Analitico semi-infinito | 0,0094574161 | 68,178652 | 100 |

Fra 0,50 e 0,25 m le variazioni relative sono 1,2075×10⁻⁵ per y e 3,8612×10⁻⁶ per M; V resta H entro l'errore numerico. Il confronto analitico richiede errori relativi 2×10⁻⁶ per y e θ, 2×10⁻⁵ per Mmax e 10⁻⁴ per la sua profondità; la forma del taglio è controllata sull'intero tratto attivo con errore normalizzato a H inferiore a 10⁻⁶. Le prove di equilibrio impongono residui inferiori a 10⁻⁷ kN e 10⁻⁶ kNm sul benchmark. La conversione N/mm rispetto a kN/m è controllata con tolleranza relativa 10⁻⁸.

I sorgenti supporto/test/ElasticPile.Checks referenziano direttamente il progetto Checker. Coprono anche carico nullo, linearità, inversione, tratto libero, discontinuità degli strati, equivalenza di sottostrati identici, conversione kh–k, origine globale z, unità, vincoli, dati invalidi e labilità. supporto/test/ElasticPile.UiChecks verifica instradamento, invalidazione, interfaccia ed esportazioni. Le evidenze sono in supporto/artefatti/palo-elastico. I test preesistenti Checker filtrati sulle classi Pile sono stati eseguiti: 32 superati, zero fallimenti. Si tratta di validazione numerica del modello elastico dichiarato; non di taratura sperimentale delle leggi del terreno.

'''
for kind,chapter in [('pratica',pratica),('teorica',teorica)]:
    p=SUP/f'docs/guida-{kind}-anthea.md';s=p.read_text(encoding='utf-8-sig').replace('revisione documentale 11','revisione documentale 12')
    if '## Risposta elastica del palo orizzontale' not in s:s=s.replace('## Approfondimenti integrati',chapter+'## Approfondimenti integrati',1)
    if kind=='pratica':
        s=s.replace("Assegnare il diametro comune D in metri e la direzione H in gradi antiorari da +X. Il cursore ruota H a passi di 15 gradi e ricalcola; il campo numerico consente qualsiasi angolo. Genera griglia sostituisce le coordinate con Nx per Ny pali agli interassi Sx e Sy, in metri. La tabella permette di modificare identificativo, x e y, aggiungere pali nella riga vuota ed eliminarli con Canc.","Scegliere in alto a sinistra Rettangolare, Quinconce, Triangolare, Pentagonale, Esagonale oppure Generica. Gli input sottostanti generano automaticamente la disposizione: Nx/Ny e Sx/D/Sy/D per le file; numero di pali sul lato e S/D per il triangolo; anelli, suddivisioni, centro e S/D per i poligoni. Quinconce alterna file di N e N−1 pali con sfalsamento di mezzo interasse. Il triangolo è pieno; i poligoni usano anelli concentrici con suddivisioni crescenti. Le coordinate sono modificabili solo in Generica. X e Y sono attive inizialmente; la direzione personalizzata è facoltativa e disattivata. Il suo angolo è antiorario da +X. La rotazione della geometria è un input distinto.")
        s=s.replace("Premere Calcola e confronta i sei metodi. Il riepilogo mostra Rg oppure Pm medio. Davisson mostra anche la riduzione percentuale del modulo di reazione e il grafico Rg contro S parallelo su D. Il punto corrente compare nel tratto tabellato 3D–8D.","Il ricalcolo è automatico per tutti i sei metodi e tutte le direzioni attive. Selezionare una riga del confronto per vedere sulla pianta il metodo e la direzione corrispondenti. Il riepilogo mostra minimo, media, massimo e riduzione media. La legenda va da 0 rosso, riduzione 100%, a 1 verde, nessuna riduzione. Il basamento è il contorno convesso esterno oppure un rettangolo; il margine asse–bordo è espresso in multipli di D, inizialmente 1D, equivalente a 0,5D libero dal palo. Il basamento è solo rappresentativo e non modifica i coefficienti.")
    p.write_text(s,encoding='utf-8')
editor=ROOT/'X.Desktop/Wiki/editorial.json';items=json.loads(editor.read_text(encoding='utf-8'))
for prefix,area in [('guide','Moduli singoli'),('theory','Fondazioni e geotecnica')]:
    items[prefix+':Risposta elastica del palo orizzontale']=dict(area=area,id=f'/wiki/{prefix}/palo-elastico',modules=['geo_palo_orizzontale','geo_micropalo_orizzontale'],related=[f'/wiki/{"theory" if prefix=="guide" else "guide"}/palo-elastico'],keywords=['Winkler','Viggiani','kh','molle','elementi finiti','taglio','momento'])
editor.write_text(json.dumps(items,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
for p in [ROOT/'README.md',SUP/'README.md',SUP/'installer/README.md',SUP/'installer/Indice-guide.md']:
    s=p.read_text(encoding='utf-8-sig').replace('Rev11','Rev12').replace('Revisione 11','Revisione 12')
    if p.name=='Indice-guide.md' and '## Risposta elastica del palo' not in s:s+='\n## Risposta elastica del palo\n\nI due volumi documentano trave su molle in Checker, leggi assegnate, conversioni, vincoli, segni, convergenza e validazione. L’attribuzione a Viggiani resta da verificare sulle pagine originali. La Rev12 aggiorna anche geometrie, direzioni e ricalcolo automatico della palificata.\n'
    s=s.replace('La Rev12 integra il modulo di efficienza','La Rev11 ha integrato il modulo di efficienza')
    p.write_text(s,encoding='utf-8')
builderPath=SUP/'scripts/Build-AntheaGuides-Itec.py';s=builderPath.read_text(encoding='utf-8').replace("REVISION = '11'","REVISION = '12'").replace("REVISION_NOTE = 'EFFICIENZA ORIZZONTALE DELLE PALIFICATE'","REVISION_NOTE = 'RISPOSTA ELASTICA DEL PALO E PALIFICATE'");builderPath.write_text(s,encoding='utf-8')
render=SUP/'scripts/Render-AntheaGuides-Itec.ps1';render.write_text(render.read_text(encoding='utf-8-sig').replace("$Revision = '11'","$Revision = '12'"),encoding='utf-8-sig')
spec=importlib.util.spec_from_file_location('builder',builderPath);B=importlib.util.module_from_spec(spec);spec.loader.exec_module(B);B.ART.mkdir(parents=True,exist_ok=True)
(B.ART/'artifact.md').write_text('Revisione 12. Preservare modello ITEC, formule OMML, indice Word. Integrare palo elastico e UI palificata; verificare PDF delle pagine aggiornate. Fonte Viggiani non verificata.\n',encoding='utf-8')
for k in B.GUIDES:B.build(k)
spec=importlib.util.spec_from_file_location('pdf',SUP/'scripts/documentazione/markdown-pdf.py');PDF=importlib.util.module_from_spec(spec);spec.loader.exec_module(PDF)
for p in [SUP/'README.md',SUP/'installer/README.md',SUP/'installer/Indice-guide.md']:PDF.build(p)
subprocess.run([sys.executable,str(SUP/'scripts/wiki/build-wiki-index.py')],check=True)
