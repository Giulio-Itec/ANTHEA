"""One-time Rev15 consolidation. All original sources remain in the revision archive."""
from pathlib import Path
import json,re,shutil,hashlib,unicodedata,sys
sys.stdout.reconfigure(encoding='utf-8')
ROOT=Path(__file__).resolve().parents[3]; S=ROOT/'supporto'; W=ROOT/'X.Desktop/Wiki'
ARCH=S/'SUPERATI/wiki-integrazione-rev15-20261004'; ART=S/'artefatti/wiki-integrazione'
def read(p):return p.read_text(encoding='utf-8').replace('\r','')
def dump(p,data):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def slug(s):return re.sub('[^a-z0-9]+','-', ''.join(c for c in unicodedata.normalize('NFD',s.lower()) if unicodedata.category(c)!='Mn')).strip('-')
if not ARCH.exists():
    records=[]
    paths=[S/f'docs/guida-{k}-anthea.{ext}' for k in ['pratica','teorica'] for ext in ['md','pdf']]
    paths+=list((S/'documentazione/Guide_ANTHEA').glob('*Rev14*'))
    paths+=[S/'installer/Indice-guide.md',S/'installer/Indice-guide.pdf',S/'README.md',S/'README.pdf',S/'installer/README.md',S/'installer/README.pdf']
    paths+=list(W.glob('*.json'))+[W/'sources.props']
    for p in paths:
        target=ARCH/p.relative_to(ROOT);target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,target)
        records.append(dict(origine=str(p.relative_to(ROOT)),archivio=str(target.relative_to(ROOT)),sha256=hashlib.sha256(p.read_bytes()).hexdigest(),motivo='Integrazione dei contenuti preesistenti nel Handbook',sostituzione='Guide globali Rev15 e catalogo corrente'))
    dump(ARCH/'registro.json',records)
source=ARCH/'supporto/docs'
old=json.loads(read(ARCH/'X.Desktop/Wiki/index.json'))
meta=json.loads(read(ARCH/'X.Desktop/Wiki/editorial.json'))
by={a['key']:a for a in old};bodies={};titles={}
for a in old:
    data=(source/f'guida-{a["source"].removesuffix(".md")}-anthea.md').read_bytes()
    body=data[a['offset']:a['offset']+a['length']].decode('utf-8').replace('\r','')
    bodies[a['key']]=body;titles[a['key']]=body.splitlines()[0][3:]
historical_theory={
1:'acciaio-armature',2:'tracciabilita-e-riferimenti',3:'sezione-in-calcestruzzo-armato',4:'architettura-del-calcolo-e-convenzioni',
5:'sezione-in-calcestruzzo-armato',6:'bridge-design',7:'guida-bridge-design',8:'bridge-design',9:'sezione-in-calcestruzzo-armato',
10:'sezione-in-calcestruzzo-armato',11:'sezione-in-calcestruzzo-armato',12:'guida-sezione-ca',13:'taglio-irrigidimenti-e-connessione-della-sezione-composta',
14:'architettura-del-calcolo-e-convenzioni',15:'tracciabilita-e-riferimenti',16:'muri-di-sostegno-e-stabilita-globale',17:'profili-calcestruzzo',
18:'capacita-orizzontale-con-broms',19:'capacita-orizzontale-con-broms',20:'sezione-composta-da-ponte',21:'sezione-composta-da-ponte',
22:'sezione-composta-da-ponte',23:'sezione-composta-da-ponte',24:'guida-progetti-e-gestione-del-lavoro',
25:'taglio-irrigidimenti-e-connessione-della-sezione-composta',26:'tracciabilita-e-riferimenti',27:'architettura-del-calcolo-e-convenzioni',
28:'tracciabilita-e-riferimenti',29:'bridge-design'}
targets={a['key']:a['key'] for a in old}
targets.update({'guida-sezione-in-calcestruzzo-armato':'guida-sezione-ca','guida-approfondimenti-integrati':'guida-wiki-e-centro-della-conoscenza',
    'approfondimenti-integrati':'tracciabilita-e-riferimenti','guida-calcestruzzo-armato-collegamento-checker':'guida-sezione-ca',
    'guida-gerarchia-dei-fogli-nei-progetti':'guida-progetti-e-gestione-del-lavoro','guida-progetti-e-revisioni':'guida-progetti-e-gestione-del-lavoro',
    'guida-micropalo-orizzontale-con-chs':'guida-pali-e-micropali-caricati-orizzontalmente','guida-stabilita-globale-guida-rapida':'guida-muri-di-sostegno-con-stratigrafie-di-monte-e-valle',
    'guida-guida-pratica-di-bridge-design':'guida-bridge-design'})
for a in old:
    m=re.match(r'teorica-a(\d+)-',a['key'])
    if m:targets[a['key']]=historical_theory[int(m[1])]
def section(key,title):
    blocks=re.split(r'(?=^### )',bodies[key],flags=re.M)
    matches=[b for b in blocks if b.splitlines()[0].removeprefix('### ')==title]
    assert len(matches)==1,(key,title)
    return matches[0].rstrip()+'\n\n'
def add(key,text):bodies[key]=bodies[key].rstrip()+'\n\n'+text.strip()+'\n\n'
def replace(key,old,new):
    assert old in bodies[key],(key,old[:70]);bodies[key]=bodies[key].replace(old,new)

# Editorial changes follow. This script always reads the archived baseline, never its own output.
add('guida-progetti-e-gestione-del-lavoro', '''### Report del ramo e compatibilità degli archivi

Genera report esporta il progetto o la sezione selezionata con i fogli diretti e le sottosezioni nell'ordine dell'albero. Include i riferimenti necessari degli antenati, senza aggiungere i rami esterni. I dati comuni compatibili vengono riuniti; valori discordanti mantengono provenienza e segnalazioni. Le schede incomplete restano riconoscibili nel documento.

Il report di progetto ricalcola su una copia dei dati. Il report del singolo modulo segue invece le condizioni del suo editor: nella Sezione in c.a. occorre attendere il completamento del calcolo corrente. Prima dell'esportazione controlla il riepilogo e conserva il file riapribile oltre al Word.

Gli archivi con revisioni usano il formato 2; quelli precedenti senza revisioni restano leggibili. I risultati dei report storici sono ricalcolati sui dati della revisione selezionata: conservare anche il report originale è necessario per documentare un risultato ottenuto con un'altra versione del motore.

### Controllo della condivisione

Prova la gerarchia su un progetto semplice: materiale al livello progetto, due sottosezioni e un foglio compatibile in ciascuna. Cambia il riferimento superiore e controlla la propagazione; modifica poi un discendente e verifica che il conflitto resti visibile. Un ramo parallelo non deve diventare riferimento dell'altro.

Consulta [convenzioni e confini del calcolo](wiki:architettura-del-calcolo-e-convenzioni) prima di trasferire risultati fra moduli. La condivisione di un materiale non trasferisce automaticamente combinazioni o verifiche.
''')
add('guida-salvataggio-e-report','''### Conservare dati e risultati insieme

Il file riapribile conserva gli input e, per i progetti, le revisioni. Word, PDF e immagini documentano risultati e configurazione al momento dell'esportazione. Non sostituire il file riapribile con un'immagine della schermata.

Per esportare un ramo usa [Progetti e gestione del lavoro](wiki:guida-progetti-e-gestione-del-lavoro#report-del-ramo-e-compatibilita-degli-archivi). Nella Sezione in c.a. il report distingue gli estremi di tensione dai casi governanti: un inviluppo di massimi non è uno stato simultaneo.
''')
add('guida-sezione-ca', '\n'.join(section('guida-sezione-in-calcestruzzo-armato',t) for t in [
    '7 3 Domini tridimensionali e bidimensionali','7 4 Tensioni e fessurazione','7 5 Taglio torsione e dettagli','7 6 Curva momento curvatura e aggiornamento']))
add('guida-sezione-ca','''### Importare ed esportare le azioni

Le tabelle offrono Template Excel, Importa Excel ed Esporta Excel. Il foglio Azioni usa le colonne Famiglia, Nome, N [kN], Mx [kNm], My [kNm], Vx [kN], Vy [kN] e T [kNm]. N è negativo a compressione. I nomi SLU e SLV nei file corrispondono ai domini Plastico ed Elastico; Rara, Frequente e Quasi permanente conservano azioni distinte.

Nella famiglia Taglio sono richiesti N, Vx e Vy; Mx e My possono accompagnarli, ad esempio per il Model Code, e T identifica la torsione. Nelle altre famiglie si usano N, Mx e My: tagli e torsione devono essere vuoti o nulli. I vecchi file a sette colonne restano leggibili senza torsione; per introdurla occorre l'intestazione T [kNm].

L'importazione controlla tutte le righe prima di modificare il foglio. Si possono aggiungere le righe o sostituire le famiglie presenti. Sono ammessi fino a 10.000 combinazioni e file fino a 20 MB. Le formule Excel non vengono eseguite: servono risultati già calcolati e salvati, oppure valori. Errori di cella e formule prive di risultato memorizzato impediscono l'importazione.

Filtri e ordinamenti cambiano la vista, non eliminano le azioni dal calcolo. Dopo l'importazione verifica numero di combinazioni, unità, segni e corrispondenza dei nomi. Non importare un inviluppo di massimi come se fosse una terna simultanea.

### Leggere asse neutro, mappe e report

La linea dell'asse neutro rappresenta deformazione nulla dello stato selezionato. Nei domini il piano è quello del punto resistente; nelle SLE è quello dell'azione assegnata. Una mappa normalizzata alla resistenza del materiale non sostituisce il controllo dei limiti SLE. Con campo uniforme l'asse può non essere definito; un asse esterno non indica da solo un errore.

Il report raccoglie input, materiali, coefficienti, azioni ed esiti scelti. Gli estremi di tensione/deformazione e i casi governanti possono provenire da combinazioni diverse. Non leggerli come un unico stato. Dati globalmente invalidi o un aggiornamento in corso impediscono l'esportazione dei risultati correnti.

Prima di scegliere una norma consulta [Profili di calcolo del calcestruzzo](wiki:profili-calcestruzzo): il selettore non attesta la copertura di tutte le verifiche della norma.
''')
replace('guida-sezione-ca',"Le superfici dei fori e le disposizioni non supportate non devono essere considerate verificate perché il disegno appare completo.","Le superfici interne dei fori compatibili hanno controlli dedicati; una superficie tesa priva di armatura efficace o di interasse definito lascia la verifica incompleta. Il disegno completo non dimostra la completezza dei controlli.")
add('sezione-in-calcestruzzo-armato','''### Asse neutro e stato resistente

L'asse neutro nel c.a. è il luogo di deformazione nulla del piano affine. Con deformazione uniforme non esiste una retta univoca. L'asse del punto resistente sul dominio e quello della combinazione di esercizio appartengono a stati diversi. Nei tendini la predeformazione può separare lo zero della deformazione geometrica da quello della tensione.

Le superfici interne dei fori sono trattate con fasce di parete o anello, limitate a metà spessore; le facce sono controllate separatamente. Una superficie compressa non richiede apertura delle fessure. Una superficie tesa senza armatura efficace o senza interasse assegnato lascia il controllo incompleto; un superamento già accertato resta sfavorevole.

I coefficienti e i campi dei diversi profili sono distinti nella [matrice del calcestruzzo](wiki:profili-calcestruzzo). La [guida operativa](wiki:guida-sezione-ca) descrive importazioni, stati del calcolo e lettura dei risultati.
''')
replace('sezione-in-calcestruzzo-armato','Disposizioni e superfici interne non supportate restano fuori campo.','Disposizioni non supportate restano fuori campo; per le superfici interne compatibili valgono le fasce di parete o anello descritte più avanti.')
bodies['profili-calcestruzzo']=bodies['teorica-a17-calcestruzzo-ordinario-normative-e-verifiche-di-sezione'].replace('## TEORICA A17 — Calcestruzzo ordinario — normative e verifiche di sezione','## Profili di calcolo del calcestruzzo')
bodies['profili-calcestruzzo']=bodies['profili-calcestruzzo'].replace('Aggiornamento: 28 settembre 2026. Il selettore propone','Questa matrice descrive le implementazioni presenti nel software, non certifica la conformità dell’intero progetto o la vigenza di ogni edizione nazionale. Il selettore propone').replace('sono esclusi da questa attività su indicazione del progettista.','non fanno parte del modello di calcestruzzo ordinario qui descritto.')
bodies['profili-calcestruzzo']=re.sub(r'Per fonti, confronti indipendenti e comandi vedere.*', 'Per usare il modulo consulta [Sezione in c.a.](wiki:guida-sezione-ca); per le formule implementate consulta [Teoria della sezione](wiki:sezione-in-calcestruzzo-armato).\n\nLe edizioni indicate identificano il codice implementato. La scelta dell’edizione e dell’annesso nazionale applicabili all’opera resta un passaggio distinto. Le attribuzioni DIN/DK/NS provenienti dalla documentazione precedente richiedono riscontro sull’annesso applicabile prima dell’uso progettuale: questa integrazione verifica il comportamento del codice, non completa una validazione indipendente di tutti gli annessi.\n', bodies['profili-calcestruzzo'], flags=re.S)
bodies['acciaio-armature']='''## Acciaio per armature: proprietà e diagrammi

Il materiale definisce la risposta delle barre; non verifica da solo una sezione. La scheda propone B450C, B450A, classi storiche FeB e un materiale personalizzato. I valori storici di catalogo non sostituiscono le prove su una struttura esistente.

### Grandezze e unità

| Simbolo | Significato | Unità |
| --- | --- | --- |
| Es | Modulo elastico | MPa |
| fyk, fu | Snervamento caratteristico e resistenza ultima | MPa |
| γs | Coefficiente parziale assegnato | adimensionale |
| εyd, εu | Deformazione di snervamento di progetto e ultima | adimensionale nelle formule |

```math
f_{yd}=\\frac{f_{yk}}{\\gamma_s}
\\varepsilon_{yd}=\\frac{f_{yd}}{E_s}
```

Con fyk = 450 MPa, γs = 1,15 ed Es = 200000 MPa, fyd = 391,304 MPa ed εyd = 0,00195652, cioè 1,95652 per mille. Nei campi che chiedono per mille si inserisce quest'ultimo valore; una deformazione adimensionale non si incolla senza conversione.

### Diagramma e classe storica

Il grafico della scheda materiali rappresenta il legame caratteristico. Il diagramma utilizzato dal calcolo dipende anche dal coefficiente parziale e dalle opzioni della verifica. Per un materiale personalizzato occorrono dati coerenti di modulo, snervamento, resistenza ultima, deformazione ultima e incrudimento.

L'allungamento storico A5 non viene convertito automaticamente nella deformazione ultima εu. Se εu manca, una funzione che la richiede non è completa. La scelta di una classe FeB non dimostra la corrispondenza del materiale esistente ai valori nominali del catalogo.

### Trasferimento fra moduli

Le proprietà compatibili possono essere condivise nel progetto; geometria, barre e carichi restano dati separati. Il tubo CHS del micropalo usa una definizione specifica. Il momento automatico del palo orizzontale conserva un modello elastico perfettamente plastico: l'incrudimento del catalogo non diventa automaticamente resistenza del meccanismo di Broms.

Consulta [Materiali e durabilità](wiki:guida-materiali-e-durabilita) per la compilazione e [Capacità orizzontale con Broms](wiki:capacita-orizzontale-con-broms) per il ruolo del momento resistente.
'''
add('guida-pali-e-micropali-caricati-orizzontalmente','''### Separare tubo e diametro geotecnico

Nel micropalo il diametro geotecnico D è in metri e governa il contatto con il terreno. Diametro esterno De e spessore t del tubo CHS sono in millimetri e governano la sezione metallica. De deve essere minore di D dopo la conversione delle unità. Il catalogo dimensionale non assegna automaticamente l'acciaio né garantisce disponibilità commerciale.

Il momento automatico richiede un CHS di classe 1 e una forza assiale inferiore alla resistenza assiale plastica. Il riempimento non contribuisce al momento resistente adottato. Per classi diverse il programma non attribuisce automaticamente la duttilità necessaria al meccanismo plastico; il momento manuale richiede una provenienza motivata.

Il calcolo di capacità con Broms è distinto dalla [risposta elastica del palo](wiki:guida-palo-elastico). Non usare un carico limite per dedurre direttamente lo spostamento di esercizio.
''')

# Use the later, coherent Bridge Design description instead of the superseded eight-family version.
bridge_source='teorica-a29-teoria-e-validazione-di-bridge-design'
technical=bodies[bridge_source].split('### Come sono stati validati i risultati')[0]
bodies['bridge-design']='## Bridge Design\n\n'+technical.split('\n',1)[1].lstrip()
for heading in ['Cosa rimane da validare prima dell’uso progettuale','Riferimenti']:
    add('bridge-design',section(bridge_source,heading))
add('bridge-design',r'''### Controllo manuale della trave equivalente

Per una campata appoggiata di 20 m con carico uniforme 100 kN/m, ogni reazione vale 1000 kN e il momento massimo è 5000 kNm. Il carico è riferito all'intero impalcato equivalente. Per due campate uguali continue, entrambe caricate:

```math
M_{appoggio}=-\frac{qL^2}{8}=-5000\,\mathrm{kNm}
R_{estremo}=\frac{3qL}{8}=750\,\mathrm{kN}
R_{centrale}=\frac{5qL}{4}=2500\,\mathrm{kN}
M_{positivo}=\frac{9qL^2}{128}=2812{,}5\,\mathrm{kNm}
```

La somma delle reazioni è 4000 kN, uguale al carico sulle due campate. Il controllo riguarda una trave a EI costante: non valida lo schema globale di uno strallato, le fasi costruttive o l'inviluppo di carichi mobili.
''')
bodies['bridge-design']=bodies['bridge-design'].replace('Nella revisione documentata è stata corretta la sensibilità dell’arrotondamento al rumore numerico. Prima della correzione, una quota teorica esatta di 0,80 m poteva essere portata a 0,85 m perché rappresentata internamente come un numero appena superiore a 0,80.','La tolleranza numerica evita che una quota teorica esatta sul passo venga aumentata per il solo rumore di rappresentazione.')
bodies['guida-bridge-design']='## 9 Bridge Design\n\n'+bodies['guida-guida-pratica-di-bridge-design'].split('\n',1)[1].lstrip()

# Preserve useful bridge interpretation without the superseded migration/API discussions.
add('sezione-composta-da-ponte','''### Interpretare l'asse neutro nelle fasi

Nel metodo cumulativo la linea indica lo zero delle tensioni cumulate nell'acciaio. Nello storico lineare e non lineare indica lo zero della deformazione totale del piano di fase. Ritiro, attivazione dei materiali e plasticità possono separare gli zeri delle tensioni nei diversi materiali: non esiste necessariamente un asse tensionale comune a soletta e carpenteria.

La vista delle curve N–ε o M–χ interroga un percorso di sezione. Non rappresenta automaticamente duttilità globale del ponte, instabilità o una legge ciclica. Dati, fase e metodo devono accompagnare il grafico esportato.

La [guida alla sezione composta](wiki:guida-sezione-composta-da-ponte) descrive la sequenza degli input; [taglio e connessione](wiki:taglio-irrigidimenti-e-connessione-della-sezione-composta) hanno controlli separati.
''')

# Replace obsolete references to autonomous guides and historical revision announcements.
replace('muri-di-sostegno-e-stabilita-globale', bodies['muri-di-sostegno-e-stabilita-globale'].split('\n\n')[1], 'Il modulo tratta muri a mensola e a gravità. Le verifiche locali e la stabilità globale hanno modelli e combinazioni distinti. La [guida ai muri](wiki:guida-muri-di-sostegno-con-stratigrafie-di-monte-e-valle) descrive il percorso Input e Verifiche; [portanza, cedimenti e armature](wiki:portanza-sismica-cedimenti-spostamenti-e-armature-rev07) approfondisce i modelli aggiuntivi.')
bodies['muri-di-sostegno-e-stabilita-globale']=re.sub(r'Le relazioni Word includono.*?(?=\n\n###)', 'Le relazioni Word includono stratigrafie e coefficienti utilizzati. Cedimenti e spostamenti sono modelli separati; verifiche idrauliche, liquefazione e completamento esecutivo richiedono analisi dedicate. Il modulo non emette una verifica complessiva dell’opera.\n\n',bodies['muri-di-sostegno-e-stabilita-globale'],count=1,flags=re.S)
bodies['muri-di-sostegno-e-stabilita-globale']=re.sub(r'La revisione ha superato .*?Non è una certificazione complessiva dell’opera\.', 'Nel motore Bishop restano esclusi i meccanismi non circolari e la liquefazione. La ricerca circolare nel dominio assegnato non esaurisce la stabilità generale del versante.',bodies['muri-di-sostegno-e-stabilita-globale'],flags=re.S)

# The same Rev07 block was present verbatim in both books. Keep theory once; provide an operating sequence.
bodies['guida-portanza-sismica-cedimenti-spostamenti-e-armature-rev07']='''## Portanza, cedimenti e armature dei muri: procedura

Le verifiche usano modelli distinti. Prima di attivarle completa geometria, stratigrafie, falda e combinazioni del muro. Un risultato favorevole a scorrimento non sostituisce portanza, cedimenti, stabilità globale o verifica strutturale.

### Scegliere il controllo

| Domanda | Dati da controllare | Risultato da leggere |
| --- | --- | --- |
| Portanza della fondazione | Terreno di posa, carico verticale efficace, eccentricità, inclinazione e sisma | Campo di applicabilità e resistenza per combinazione |
| Cedimento | Parametri deformativi e modello del terreno, combinazione di esercizio | Spostamento e ipotesi, separatamente dalla resistenza |
| Spostamento permanente | Accelerogramma e parametri Newmark richiesti | Spostamento accumulato, soglia e unità temporali |
| Armature | Materiali, sezioni critiche, sollecitazioni, copriferro e disposizione | Proposta, controlli eseguiti e condizioni incomplete |
| Muro a gravità | Materiale, peso e resistenze pertinenti | Equilibrio e verifiche coerenti con calcestruzzo o muratura |

### Procedura e lettura

Apri i controlli pertinenti nella scheda Verifiche e leggi le ipotesi prima dell'esito. Inserisci i dati mancanti; non sostituire un parametro deformativo con un parametro di resistenza. Per il sisma distingue i coefficienti delle spinte da quelli del terreno di fondazione e della stabilità globale.

Calcola armature produce una proposta da controllare, non il dettaglio esecutivo completo. Verifica le sezioni critiche, l'armatura disponibile e le condizioni non coperte. Se trasferisci una sezione al modulo c.a., controlla segni, assi e combinazione del foglio risultante.

Conserva nel report la combinazione, i parametri effettivi e l'eventuale stato fuori campo. Consulta [modelli di portanza, cedimenti e armature](wiki:portanza-sismica-cedimenti-spostamenti-e-armature-rev07) per formule e limiti e [stabilità globale](wiki:muri-di-sostegno-e-stabilita-globale) per il modello Bishop.
'''
key='guida-muri-di-sostegno-con-stratigrafie-di-monte-e-valle'
replace(key,bodies[key].split('\n\n')[1],'La scheda Input definisce muro, terreni e azioni; Verifiche raccoglie gli esiti locali e globali. Prepara separatamente stratigrafie delle spinte, terreno di fondazione e profilo profondo della stabilità globale. Un modello completo per una verifica può essere insufficiente per le altre.')
bodies[key]=re.sub(r'Le relazioni Word includono.*?(?=\n\n###)', 'Conserva nel report le due stratigrafie e i coefficienti effettivi. Per le verifiche aggiuntive consulta [Portanza, cedimenti e armature](wiki:guida-portanza-sismica-cedimenti-spostamenti-e-armature-rev07).\n\n',bodies[key],count=1,flags=re.S)
bodies[key]=bodies[key].replace('La guida rapida contiene proprietà, dominio e risultati completi.','Le proprietà e il dominio completi sono riportati nell’esempio seguente.')
example=section('guida-stabilita-globale-guida-rapida','Esempio salvato e risultati ripercorribili')
example=example.split('Per l’organizzazione dell’input')[0].replace('Strati e proprietà sono quelli della pagina 2.','I pesi γ/γsat sono 18/20, 19/21 e 20/22 kN/m³ rispettivamente per Riempimento, Alluvioni e Ghiaia; φ′k è 30°, 28° e 36°, con c′k nullo. Non sono presenti falda e sisma.')
add(key,example)

# A clear provenance chapter replaces release notes as the entry point to evidence.
bodies['tracciabilita-e-riferimenti']='''## Tracciabilità, fonti e archivio

Un risultato è ripercorribile se conserva input, unità, combinazione, modello, coefficienti e versione del motore. Una schermata del tasso non basta. Il file riapribile conserva i dati; il report documenta il risultato della sessione. Una riapertura con librerie diverse può richiedere un nuovo confronto.

### Tre tipi di fonte

| Fonte | Cosa sostiene | Cosa non dimostra |
| --- | --- | --- |
| Norma ed edizione applicabile | Requisiti e campo della verifica | Che il software implementi ogni clausola |
| Modello teorico o pubblicazione | Ipotesi, derivazione e limiti | Che valga per qualsiasi geometria e terreno |
| Codice e prova del software | Comportamento della versione controllata | La sicurezza dell'opera nel suo insieme |

Le pagine distinguono queste fonti. Il riferimento a una norma identifica un'edizione; non equivale a un aggiornamento automatico alla versione più recente. In particolare, i profili EC2 del modulo c.a. descrivono implementazioni di prima generazione.

### Ritrovare i contenuti precedenti

Le appendici di sviluppo, migrazione e audit sono state consolidate nelle pagine per argomento. I vecchi indirizzi della Wiki portano alla pagina corrente pertinente, senza creare duplicati nella ricerca. I risultati storici restano evidenze della versione e del caso originari, non risultati appena rieseguiti.

Le due guide Rev14 complete, i relativi Word/PDF e il catalogo precedente sono conservati in `supporto/SUPERATI/wiki-integrazione-rev15-20261004/`. Il registro riporta origine, impronta SHA-256 e revisione sostitutiva. L'inventario di integrazione associa ogni vecchia voce alla destinazione e alle fonti del codice. Modelli di esempio e risultati usati dalle attività rimangono nelle loro cartelle.

### Controlli indipendenti

Parti da un caso con soluzione semplice e unità esplicite. Per una trave appoggiata controlla reazioni e momento; per una sezione omogenea area, baricentro e inerzia; per un contributo laterale uniforme di palo controlla superficie per tensione. Estendi poi il confronto al caso completo, registrando scarti e ipotesi.

I test di integrazione confrontano chiamate, dati e visualizzazione; non sono indipendenti dal motore se usano la stessa libreria come riferimento. I benchmark numerici devono dichiarare il modello e la fonte della soluzione. Il numero di test superati non sostituisce questa distinzione.

### Riferimenti normativi identificati

Il [DM 17 gennaio 2018](https://www.gazzettaufficiale.it/eli/id/2018/02/20/18A00716/sg) e la [Circolare 21 gennaio 2019 n. 7](https://www.gazzettaufficiale.it/eli/id/2019/02/11/19A00855/sg) sono i riferimenti italiani citati nelle formulazioni NTC delle guide. Verifica edizione, modifiche e disposizioni applicabili all'opera prima di assegnare coefficienti o limiti. La [matrice dei profili c.a.](wiki:profili-calcestruzzo) specifica ciò che il programma tratta e ciò che resta escluso.
'''

evidence={
    'anthea':['X.Core/ProjectSharedHierarchy.cs','X.Core/ProjectRevisions.cs','X.Core/ProjectReportPlan.cs','X.Desktop/Wpf/ProjectWorkspace.cs','X.Core/SectionActionsExcel.cs','X.Desktop/Wpf/ConcreteLayout.cs','X.Desktop/Wpf/WikiView.cs'],
    'fondamenti':['X.Calculations/CalculationService.cs','X.Calculations/ModuleCatalog.cs','X.Core/ProjectValidation.cs'],
    'materiali':['X.Calculations/RebarMaterial.cs','X.Calculations/ConcreteMaterials.cs','X.Calculations/Materials/NtcCover.cs'],
    'calcestruzzo':['X.Calculations/ConcreteAnalysis.cs','X.Calculations/Ntc2018Checks.cs','X.Calculations/ConcreteInnerCracking.cs','X.Calculations/ConcreteTensionCracking.cs','X.Calculations/ConcreteStandards.cs','X.Calculations/ConcreteShearAnalysis.cs','X.Calculations/ConcreteTorsion.cs'],
    'geotecnica':['X.Calculations/PaloOrizzontale.cs','X.Calculations/MicropaloOrizzontale.cs','X.Calculations/ElasticHorizontalPile.cs','X.Calculations/HorizontalPileGroup.cs','X.Calculations/RetainingWall.GlobalStability.cs','X.Calculations/RetainingWall.Actions.cs'],
    'ponti':['X.Calculations/BridgeConcept.Calculation.cs','X.Calculations/BridgeConcept.AdvancedCalculation.cs','X.Calculations/BridgeConcept.Optimization.cs','X.Calculations/BridgeSection.Analysis.cs','X.Calculations/BridgeSection.Shear.cs','X.Calculations/SectionNeutralAxis.cs'],
    'fem':['supporto/test/wiki-handbook-checks.py'],'meccanica':['supporto/test/wiki-handbook-checks.py'],
    'acciaio':['supporto/test/wiki-handbook-checks.py'],'sismica':['X.Calculations/RetainingWall.Actions.cs'],
    'bim':['X.Desktop/Wiki/references.json'],'computational':['X.Desktop/Wiki/references.json']}

# Remove stale release prose from the retained pages, not the archived evidence.
replace('guida-avvio-e-scelta-del-modulo','ma un valore in MPa è mille volte il corrispondente valore espresso in kPa.','e 1 MPa = 1000 kPa: per esprimere in kPa un valore dato in MPa occorre moltiplicarlo per 1000.')
replace('guida-avvio-e-scelta-del-modulo',"Aprire Avvia ANTHEA.cmd nella cartella dell'applicazione. Il comando avvia app/ANTHEA.exe, quando presente; in ambiente di sviluppo può compilare e avviare il progetto. Compila.cmd aggiorna la distribuzione nella cartella app.","Nel repository, Avvia ANTHEA.cmd ricompila il progetto in Release, pubblica nella cartella app e avvia l'eseguibile soltanto se i passaggi riescono. Richiede quindi l'SDK .NET e ANTHEA chiuso durante l'aggiornamento. La distribuzione già pubblicata può essere avviata direttamente da app/ANTHEA.exe.")
replace('guida-avvio-e-scelta-del-modulo','| Sezione in c a |','| Palificata orizzontale | Come cambiano i fattori di riduzione con geometria e direzione del carico | Posizioni, diametro, teoria e interassi rappresentativi |\n| Muri di sostegno | Quali verifiche locali e globali sono applicabili al muro | Geometria, materiali, terreni, acqua, azioni e combinazioni |\n| Sezione in c a |')
bodies['bridge-design']=re.sub(r'\A## Bridge Design.*?(?=### Ambito)', '## Bridge Design\n\nIl modello costruisce e confronta alternative parametriche. La graduatoria riguarda soltanto le combinazioni ammesse ed esplorate: non dimostra la sicurezza del ponte né l’ottimo fra tutte le soluzioni realizzabili.\n\n',bodies['bridge-design'],flags=re.S)
bodies['guida-bridge-design']=re.sub(r'^Edizione 30 settembre 2026.*\n','',bodies['guida-bridge-design'],flags=re.M).replace('La guida teorica della stessa revisione spiega formule, prove e limiti;','Il [modello teorico di Bridge Design](wiki:bridge-design) spiega formule e limiti;')
replace('bridge-design','Il nuovo riferimento GPC Engine non trasforma il predimensionamento in una verifica strutturale completa.','La presenza delle librerie GPC non trasforma il predimensionamento in una verifica strutturale completa.')
bodies['esempi-trasversali-e-lettura-critica']=bodies['esempi-trasversali-e-lettura-critica'].split('### 10 3 Cosa dimostrano le prove del software')[0]
add('esempi-trasversali-e-lettura-critica','### Interpretare una prova\n\nConfronti analitici, benchmark e controlli dell’interfaccia rispondono a domande diverse. Consulta [Tracciabilità, fonti e archivio](wiki:tracciabilita-e-riferimenti) per conservare il campo del confronto senza estenderlo all’intera opera.')
key='portanza-sismica-cedimenti-spostamenti-e-armature-rev07'
replace(key,bodies[key].split('\n\n')[1],'Il modulo distingue portanza sismica, cedimenti, spostamenti permanenti e verifiche delle armature. Le relazioni seguenti descrivono i modelli implementati e il loro campo. La [procedura operativa](wiki:guida-portanza-sismica-cedimenti-spostamenti-e-armature-rev07) indica quali dati preparare e come leggere gli esiti.')
for heading,starts in [
    ('Dati della portanza sismica',('In Terreno aprire Portanza','In Verifiche scegliere una combinazione')),
    ('Dati dei cedimenti',('In Terreno attivare','Limiti iniziali modificabili')),
    ('Importare una storia Newmark',('In Azioni aprire','Importare un CSV')),
    ('Disporre e proporre le armature',('In Geometria si possono','Ogni zona contiene','Calcola armature cerca','In Vista dei risultati'))]:
    paragraphs=bodies[key].split('\n\n');moved=[p for p in paragraphs if p.startswith(starts)]
    assert moved,heading
    bodies[key]='\n\n'.join(p for p in paragraphs if p not in moved)
    add('guida-'+key,'### '+heading+'\n\n'+'\n\n'.join(moved))
bodies[key]=re.sub(r'I nuovi motori ShallowFoundationSeismic.*?(?=\n\n)', '',bodies[key],flags=re.S)
add(key,r'''### Relazioni degli spostamenti

La stima del cedimento integra l'incremento di tensione verticale nel profilo deformabile. I moduli devono essere coerenti con il campo di tensione del problema.

```math
s=\int\frac{\Delta\sigma_z}{M}\,dz
\theta=\frac{s_{monte}-s_{valle}}{B}
u_{testa}=u_{fusto}+\frac{H}{K}-\theta H_{muro}
```

Nell'ultima relazione H è la risultante orizzontale per metro di muro, non l'altezza; Hmuro è l'altezza geometrica. Con H in kN/m e K in kN/m², H/K è uno spostamento in metri. Somma i contributi conservandone i segni. Il modello disaccoppiato non ricostruisce l'interazione rigida terreno–fondazione.
''')
key='guida-wiki-e-centro-della-conoscenza'
replace(key,'L’ampliamento resta selettivo:' if 'L’ampliamento resta selettivo:' in bodies[key] else "L'ampliamento resta selettivo:","L'integrazione dei contenuti preesistenti è tracciata:")
bodies[key]=re.sub(r"L'integrazione dei contenuti preesistenti è tracciata:.*?(?=\n\n)", 'Le pagine correnti consolidano istruzioni e teoria; i resoconti originali di audit e sviluppo sono conservati nell’archivio Rev14. Fonti e archivio spiega la provenienza. I vecchi collegamenti aprono la destinazione corrente pertinente. Contenuto integrato indica una revisione editoriale; Campo normativo da riscontrare segnala attribuzioni nazionali che richiedono ulteriori fonti indipendenti.',bodies[key],flags=re.S)

replace('guida-problemi-frequenti-e-controlli-finali', 'La Guida teorica di ANTHEA descrive i calcoli della stessa edizione. La documentazione tecnica del repository in supporto/docs contiene approfondimenti su sezioni CA, metodi della sezione composta, taglio e connessione, pali orizzontali, progetti e revisioni. Il file bridge-design.md descrive specificamente il nuovo motore di predimensionamento e le prove eseguite.', 'La [guida teorica](wiki:architettura-del-calcolo-e-convenzioni) descrive i calcoli della stessa edizione. Prosegui con [Sezione in c.a.](wiki:sezione-in-calcestruzzo-armato), [Sezione composta](wiki:sezione-composta-da-ponte), [Bridge Design](wiki:bridge-design) o [Broms](wiki:capacita-orizzontale-con-broms). Le evidenze e le revisioni precedenti sono descritte in [Fonti e archivio](wiki:tracciabilita-e-riferimenti).')
replace('guida-wiki-e-centro-della-conoscenza', 'le appendici storiche sono raccolte separatamente.', 'i contenuti storici utili sono integrati nelle pagine correnti e gli originali restano nell’archivio documentale.')
replace('guida-wiki-e-centro-della-conoscenza', 'le guide storiche integrate mantengono le loro date e il proprio campo di validità.', 'il campo di validità è dichiarato nelle pagine correnti; date e revisioni originarie si trovano nell’archivio documentale.')
replace('guida-wiki-e-centro-della-conoscenza', 'Campo normativo da riscontrare segnala attribuzioni nazionali che richiedono ulteriori fonti indipendenti.', 'Riscontri sulle fonti da completare segnala attribuzioni normative o bibliografiche non ancora confermate sui testi primari.')
replace('guida-sezione-composta-da-ponte', 'Come esempio, impostare altezza libera 1800 mm,', 'Come esempio, impostare Altezza totale H = 1855 mm, da cui il motore ricava altezza libera dell’anima 1800 mm,')
replace('guida-sezione-composta-da-ponte', "Per riprodurre l'esempio usare altezza libera 1800 mm,", 'Per riprodurre l’esempio usare Altezza totale H = 1850 mm, che con le due flange da 25 mm produce altezza libera 1800 mm,')
replace('sezione-composta-da-ponte', "I valori immessi restano hw e tw: non si deve anticipare nell'input la trasformazione, che il motore esegue internamente.", 'Il campo attuale Altezza totale H comprende le piattabande: hw è ricavata sottraendo gli spessori superiore, inferiore e, per H verticale, dell’eventuale seconda piastra. Nell’input si assegnano H e tw; non si deve anticipare la trasformazione dello spessore, che il motore esegue internamente.')
replace('sezione-composta-da-ponte', 'Gli esempi riguardano esclusivamente la carpenteria lorda.', 'Gli esempi riguardano esclusivamente la carpenteria lorda. Per riprodurli nel campo Altezza totale H assegnare 1855 mm per l’H inclinata e 1850 mm per il cassoncino: in entrambi i casi l’altezza libera hw è 1800 mm.')
replace('elementi-shell', "L'asse medio sostituisce il volume", 'La superficie media sostituisce il volume')
add('elementi-shell', '''### Controllo delle unità e fonte

Se il momento di piastra è uniforme e pari a 12 kNm/m, una striscia larga 2,5 m porta 30 kNm. Per una distribuzione variabile occorre integrare lungo la striscia. La documentazione [SCIA sulle risultanti 1D e 2D](https://www.scia.net/en/support/faq/scia-engineer/results/calculation-1d-and-2d-results) distingue le componenti di membrana da quelle flessionali; le convenzioni specifiche vanno lette nel solutore di origine. [COMSOL, Singular Loads](https://doc.comsol.com/6.4/doc/com.comsol.help.sme/sme_ug_modeling.05.071.html) illustra perché un carico concentrato può rendere la tensione locale dipendente dalla mesh.
''')
add('dinamica-e-sisma-del-modello', r'''### Controllo dell’oscillatore

Per m = 1000 kg e k = 40000 N/m si ottengono frequenza angolare 6,3246 rad/s e periodo 0,99346 s. Con massa raddoppiata il periodo diventa 1,40496 s. Questo è un oscillatore elastico non smorzato, non lo spettro di progetto di un sito.

```math
\omega_n=\sqrt{\frac{k}{m}}
T=\frac{2\pi}{\omega_n}
```

La derivazione è nelle [lezioni MIT di dinamica strutturale, Unit 20](https://ocw.mit.edu/courses/16-20-structural-mechanics-fall-2002/609687cf29516e13e864ff310af328a7_unit20.pdf). I coefficienti normativi e lo smorzamento vanno definiti separatamente.
''')
add('releases-e-connettivita', '''### Riferimento del modello

La formulazione [TU Delft dei telai piani](https://interactivetextbooks.citg.tudelft.nl/computational-modelling/structural_linear/space_frame.html) distingue traslazioni e rotazione del nodo e l’assemblaggio delle rigidezze. Questa pagina riguarda l’interpretazione del modello esterno: non introduce un editor generale di release in Anthea.
''')
add('fasi-costruttive-e-percorso-dei-carichi', '''### Riferimento e campo

La trattazione [SCI sulla costruzione composta](https://steelconstruction.info/topics/design/composite-construction/) distingue la fase non puntellata, in cui l’acciaio sostiene il calcestruzzo fresco, dalla fase composta. È un riferimento sul modello; non sostituisce le norme applicabili al ponte né verifica puntelli, stabilità laterale o procedure di montaggio del progetto concreto.
''')

retained=sorted([a['key'] for a in old if targets[a['key']]==a['key']],key=lambda k:(by[k]['source'],by[k]['offset']))+['acciaio-armature','profili-calcestruzzo']
newmeta={}
for key in retained:
    if key in by:
        a=by[key];m=dict(meta[a['type']+':'+titles[key]])
    else:
        a=dict(type='theory',chapterId='materiali' if key=='acciaio-armature' else 'calcestruzzo')
        m=dict(key=key,id='/wiki/manuale/'+a['chapterId']+'/'+key,area='Calcestruzzo armato',modules=['mat_acciaio_armatura'] if key=='acciaio-armature' else ['str_palo'],related=[],prerequisites=[],references=[],level='intermediate',chapterId=a['chapterId'],status='reviewed')
    # Remove release announcements and stale stand-alone-guide citations; equations remain unchanged.
    body=bodies[key]
    body=re.sub(r'^## (?:\d+ )?', '## ',body,count=1)
    body=re.sub(r'^(### )\d+[ .]+\d+\s+',r'\1',body,flags=re.M)
    body=body.replace('Portanza sismica cedimenti spostamenti e armature Rev07','Portanza, cedimenti, spostamenti e armature dei muri')
    body=body.replace('<!-- pagebreak -->','')
    body=re.sub(r'\n{3,}','\n\n',body).strip()+'\n\n'
    bodies[key]=body
    title=body.splitlines()[0][3:];m['title']=title
    m['order']=len(newmeta)
    # Editorial integration is explicitly distinct from an independently completed technical review.
    if m.get('status')!='reviewed':m['status']='integrated'
    if key=='profili-calcestruzzo':m['status']='qualified'
    if key=='acciaio-armature':m['area']='Calcestruzzo armato'
    m['summary']=m.get('summary') or next((x for x in body.splitlines()[1:] if x and not x.startswith(('#','|','!','```','>','-'))),'')[:215]
    if key=='bridge-design':m['summary']='Quattordici famiglie, modelli equivalenti e ricerca discreta. Quantità, carichi, costi, CO₂ e limiti del predimensionamento.'
    if key=='guida-bridge-design':m['summary']='Impostare sito, geometria e ipotesi; confrontare alternative, leggere la ricerca e applicare una soluzione.'
    newmeta[a['type']+':'+title]=m
newby={m['key']:m for m in newmeta.values()}
newby['esempi-trasversali-e-lettura-critica']['chapterId']='fondamenti'
prereqs={'fondamenti':'load-path','meccanica':'load-path','materiali':'load-path','calcestruzzo':'calcestruzzo-armature-e-copriferro','acciaio':'load-path','geotecnica':'architettura-del-calcolo-e-convenzioni','ponti':'bridge','sismica':'azioni-e-combinazioni-del-modello','fem':'load-path','bim':'load-path','computational':'load-path','anthea':'guida-avvio-e-scelta-del-modulo'}
for key,m in newby.items():
    def dest(uri):
        base=uri.split('#')[0].removeprefix('wiki:');a=next((a for a in old if base in [a['id'],a['key']]),None)
        return targets[a['key']] if a else base
    m['related']=list(dict.fromkeys(dest(x) for x in m.get('related',[]) if dest(x)!=key))
    m['prerequisites']=list(dict.fromkeys(dest(x) for x in m.get('prerequisites',[]) if dest(x)!=key))
    if not m['prerequisites'] and prereqs[m['chapterId']]!=key:m['prerequisites']=[prereqs[m['chapterId']]]
    counterpart=key.removeprefix('guida-') if key.startswith('guida-') else 'guida-'+key
    if counterpart in newby and counterpart not in m['related']:m['related'].append(counterpart)
newby['guida-materiali-e-durabilita']['related']=['calcestruzzo-armature-e-copriferro','acciaio-armature']
newby['profili-calcestruzzo']['related']=['sezione-in-calcestruzzo-armato','guida-sezione-ca','cracking']
newby['acciaio-armature']['related']=['calcestruzzo-armature-e-copriferro','guida-materiali-e-durabilita']
summaries={
'guida-avvio-e-scelta-del-modulo':'Scegliere lo strumento, preparare i dati e leggere unità e stato del foglio.',
'guida-progetti-e-gestione-del-lavoro':'Albero, dati comuni, conflitti, revisioni e report: come organizzare un progetto ripercorribile.',
'guida-materiali-e-durabilita':'Compilare le schede di calcestruzzo e acciaio e controllare il trasferimento ai moduli di verifica.',
'guida-palo-verticale':'Stratigrafie, falda, coefficienti e lettura delle resistenze assiali per sondaggio.',
'guida-micropalo-verticale':'Bulbo iniettato, correlazione adottata, inclinazione e significato dei risultati.',
'guida-pali-e-micropali-caricati-orizzontalmente':'Impostare il meccanismo di Broms e distinguere sezione resistente, terreno e spostamenti.',
'guida-sezione-composta-da-ponte':'Geometria, fasi, metodi cumulativo e storico, curve e verifiche locali della sezione da ponte.',
'guida-salvataggio-e-report':'Conservare input, revisioni e risultati; scegliere il report del foglio o del ramo di progetto.',
'guida-percorso-completo-per-un-primo-progetto':'Una sequenza di lavoro dalla scelta dei materiali alla lettura dei risultati e all’archiviazione.',
'guida-problemi-frequenti-e-controlli-finali':'Riconoscere input incompleti, conflitti e risultati obsoleti prima della consegna.',
'guida-muri-di-sostegno-con-stratigrafie-di-monte-e-valle':'Terreni locali e profondi, percorso Bishop ed esempio stratificato con esito anche sfavorevole.',
'guida-portanza-sismica-cedimenti-spostamenti-e-armature-rev07':'Preparare i dati dei controlli aggiuntivi del muro e distinguere esiti resistenti, deformativi e costruttivi.',
'guida-palificata-orizzontale':'Definire geometria e direzione del carico e interpretare i fattori di riduzione del gruppo.',
'guida-palo-elastico':'Preparare rigidezza, terreno e vincoli; leggere deformata, sollecitazioni e convergenza.',
'guida-wiki-e-centro-della-conoscenza':'Navigare fra teoria e strumenti, cercare un argomento e ritrovare i contenuti consolidati.',
'guida-interpretazione-dei-risultati-e-controlli-indipendenti':'Leggere il risultato nel suo modello e confrontarlo con un ordine di grandezza indipendente.',
'guida-tutorial-dal-modello-beam-alla-verifica-di-sezione':'Dalla trave appoggiata alla domanda di momento nel modulo Sezione in c.a.',
'architettura-del-calcolo-e-convenzioni':'Confini dei motori, dati mancanti, unità e convenzioni nel passaggio fra moduli.',
'calcestruzzo-armature-e-copriferro':'Resistenze, legami costitutivi, esposizioni e distinzione fra copriferro minimo e nominale.',
'palo-verticale':'Contributi laterali e di punta, tensioni geostatiche e trattamento di indagini e coefficienti.',
'micropalo-verticale':'Superficie del bulbo, correlazioni, tratti attivi e peso del micropalo inclinato.',
'capacita-orizzontale-con-broms':'Equilibri limite, meccanismi corti e lunghi e ruolo del momento resistente della sezione.',
'sezione-in-calcestruzzo-armato':'Equilibrio di sezione, domini, fessurazione, taglio, torsione e significato degli stati rappresentati.',
'sezione-composta-da-ponte':'Omogeneizzazione, attivazione dei materiali e accumulo delle risposte attraverso le fasi.',
'taglio-irrigidimenti-e-connessione-della-sezione-composta':'Anime, pioli, appoggi e cassoncini: resistenze locali e interazioni nel campo implementato.',
'esempi-trasversali-e-lettura-critica':'Controlli manuali per distinguere domanda, capacità e quantità senza confondere i modelli.',
'tracciabilita-e-riferimenti':'Fonti normative, teoria e prove software; provenienza delle pagine consolidate e accesso agli originali.',
'muri-di-sostegno-e-stabilita-globale':'Equilibrio locale e ricerca circolare Bishop: profili, combinazioni, tassi e limiti distinti.',
'portanza-sismica-cedimenti-spostamenti-e-armature-rev07':'Modelli aggiuntivi del muro: portanza sismica, cedimenti, Newmark e resistenza delle sezioni.',
'palificata-orizzontale':'Riduzioni di gruppo, p-multiplier e rapporti di rigidezza: significati fisici e limiti delle correlazioni.',
'palo-elastico':'Trave su terreno elastico, conversioni del modulo di reazione e condizioni al contorno.',
'elementi-shell':'Membrana e flessione, orientamento, discretizzazione e connessioni degli elementi superficiali.',
'releases-e-connettivita':'Gradi di libertà, rilasci e collegamenti: distinguere coincidenza geometrica e continuità meccanica.',
'load-path':'Seguire le azioni dagli elementi caricati ai vincoli e distinguere equilibrio e verifica.',
'azioni-e-combinazioni-del-modello':'Dai carichi alle azioni di calcolo: coerenza della combinazione e degli stati limite.',
'dinamica-e-sisma-del-modello':'Masse, rigidezze, modi e azioni dinamiche: cosa rappresenta e cosa esclude il modello.',
'fasi-costruttive-e-percorso-dei-carichi':'Materiali attivi, vincoli e fenomeni differiti nella sequenza costruttiva.',
'acciaio-armature':'Modulo, snervamento, duttilità e diagrammi: unità, esempio numerico e trasferimento ai moduli.',
'profili-calcestruzzo':'Matrice delle implementazioni NTC, EC2 e Model Code: geometrie ammesse, dati richiesti e limiti.'}
for key,summary in summaries.items():newby[key]['summary']=summary
review_path=S/'docs/wiki-riscontri.json'
reviews=json.loads(read(review_path)) if review_path.exists() else {}
for key,review in reviews.items():
    newby[key]['status']=review['status']

# Every old article and every old section receives a direct, acyclic destination.
aliases={};inventory=[]
for a in old:
    key=targets[a['key']];m=newby[key]
    headings=re.findall(r'^###? (.+)',bodies[key],re.M)[1:]
    anchors=[slug(x) for x in headings]
    for uri in [a['key'],a['id']]:
        if uri not in [key,m['id']]:aliases[uri]=m['id']
        for anchor in a['sections']:
            clean=re.sub(r'^\d+-\d+-','',anchor)
            selected=clean if clean in anchors else next((v for v in anchors if clean in v or v in clean),None)
            # Removed audit details redirect to the relevant article, never to an unrelated retained anchor.
            aliases[uri+'#'+anchor]=m['id']+('#'+selected if selected else '')
    original=(source/f'guida-{a["source"].removesuffix(".md")}-anthea.md').read_bytes()[a['offset']:a['offset']+a['length']]
    review=reviews.get(key,{})
    inventory.append(dict(key=a['key'],title=a['title'],chapter=m['chapterId'],destination=m['id'],action='conservato e integrato' if a['key']==key else 'accorpato; originale archiviato',originalStatus=a['status'],currentStatus=m['status'],sourceSha256=hashlib.sha256(original).hexdigest(),evidence=review.get('evidence',evidence.get(m['chapterId'],[])),reviewScope=review.get('scope','Coerenza editoriale e percorsi; formule conservate nel campo dichiarato. Le verifiche indipendenti sono registrate nei test, non dedotte dallo stato di catalogo.'),openPoints=review.get('openPoints',[])))
aliases={k:v for k,v in aliases.items() if k!=v}
for kind,sourcekind in [('guide','pratica'),('theory','teorica')]:
    oldtext=read(source/f'guida-{sourcekind}-anthea.md');header=oldtext.split('## ')[0]
    header=header.split('Edizione ')[0]+'''Edizione 5 del 4 ottobre 2026 — revisione documentale 15

Questa edizione integra i contenuti precedenti nel percorso dell'Engineering Handbook. Le procedure correnti e la teoria sono separate dai resoconti di sviluppo. Le fonti integrali e le evidenze storiche restano nell'archivio Rev14; gli indirizzi precedenti della Wiki raggiungono le pagine consolidate. Lo stato editoriale distingue contenuti integrati, pagine revisionate e profili che richiedono ulteriori riscontri normativi.

'''
    text=header+''.join(bodies[m['key']] for k,m in newmeta.items() if k.startswith(kind+':'))
    (S/f'docs/guida-{sourcekind}-anthea.md').write_text(text.rstrip()+'\n',encoding='utf-8')
dump(W/'editorial.json',newmeta);dump(W/'aliases.json',aliases)
refs=json.loads(read(ARCH/'X.Desktop/Wiki/references.json'))
for rid,title,url in [('ntc2018','DM 17 gennaio 2018 — NTC 2018','https://www.gazzettaufficiale.it/eli/id/2018/02/20/18A00716/sg'),('circolare2019','Circolare 21 gennaio 2019 n. 7','https://www.gazzettaufficiale.it/eli/id/2019/02/11/19A00855/sg')]:
    if not any(r['id']==rid for r in refs):refs.append(dict(id=rid,title=title,url=url,kind='normative'))
for k in ['profili-calcestruzzo','sezione-in-calcestruzzo-armato','tracciabilita-e-riferimenti']:
    newby[k]['references']=list(dict.fromkeys(newby[k]['references']+['ntc2018','circolare2019']))
for rid,title,url,keys in [
    ('mit-dynamics','MIT · Structural Mechanics · Unit 20 · 2002','https://ocw.mit.edu/courses/16-20-structural-mechanics-fall-2002/609687cf29516e13e864ff310af328a7_unit20.pdf',['dinamica-e-sisma-del-modello']),
    ('delft-frames','TU Delft · 2D frame analysis','https://interactivetextbooks.citg.tudelft.nl/computational-modelling/structural_linear/space_frame.html',['releases-e-connettivita','load-path']),
    ('comsol-singular','COMSOL 6.4 · Singular Loads','https://doc.comsol.com/6.4/doc/com.comsol.help.sme/sme_ug_modeling.05.071.html',['elementi-shell']),
    ('scia-resultants','SCIA · Calculation of 1D and 2D results','https://www.scia.net/en/support/faq/scia-engineer/results/calculation-1d-and-2d-results',['elementi-shell']),
    ('sci-composite','SCI · Composite construction','https://steelconstruction.info/topics/design/composite-construction/',['fasi-costruttive-e-percorso-dei-carichi'])]:
    refs.append(dict(id=rid,title=title,url=url,kind='Riferimento del modello; non certificazione di ANTHEA'))
    for key in keys:newby[key]['references']=list(dict.fromkeys(newby[key]['references']+[rid]))
newby['azioni-e-combinazioni-del-modello']['references']=['ntc2018','circolare2019']
dump(W/'editorial.json',newmeta);dump(W/'references.json',refs)
dump(ART/'inventario-integrazione.json',inventory)
dump(ARCH/'mappa-destinazioni.json',inventory)
print(f'{len(old)} voci originali, {len(newmeta)} voci consolidate, {len(aliases)} alias; originali archiviati in {ARCH}')
