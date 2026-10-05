"""Rev24: continuità delle singole barre e giunti con finestra unica."""
from pathlib import Path
import hashlib, json, shutil, subprocess, sys

root=Path(__file__).resolve().parents[3]
support=root/'supporto'
archive=support/'SUPERATI/palo-giunti-rev24-20261005'
paths=[support/f'docs/guida-{kind}-anthea.{ext}' for kind in ('pratica','teorica') for ext in ('md','pdf')]
paths+=list((support/'documentazione/Guide_ANTHEA').glob('*Rev23.*'))
paths+=[support/name for name in ('README.md','README.pdf','installer/Indice-guide.md','installer/Indice-guide.pdf')]
paths+=list((support/'esempi/palo-orizzontale-armature').glob('*'))
records=[]
for path in paths:
    if not path.is_file(): continue
    destination=archive/path.relative_to(support)
    destination.parent.mkdir(parents=True,exist_ok=True)
    if not destination.exists(): shutil.copy2(path,destination)
    records.append(dict(origine=str(path.relative_to(root)),archivio=str(destination.relative_to(root)),
        motivo='Superata dalla gestione per singola barra e dai giunti univoci al cambio di armatura',
        sostituzione=str(path.relative_to(root)).replace('Rev23','Rev24'),sha256=hashlib.sha256(destination.read_bytes()).hexdigest()))
(archive/'registro.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')

practical=support/'docs/guida-pratica-anthea.md'
text=practical.read_text(encoding='utf-8-sig').replace('revisione documentale 23','revisione documentale 24')
text=text.replace('Il riquadro confronta lunghezza iniziale, richiesta dal verificatore e adottata, che è la maggiore.',
    'Il riquadro distingue lunghezza iniziale, richiesta dal verificatore, adottata ed effettiva. Per i giunti commerciali si adotta il massimo fra iniziale e richiesta; al cambio di diametro si riserva anche lo sviluppo necessario dei due lati, indicandolo separatamente.')
marker='La vista finale deriva dai risultati:'
addition='''Al cambio di armatura la distinta segue le singole barre. Posizione e diametro coincidenti danno una barra continua; una riduzione 16Ø24 → 8Ø24 mantiene le otto barre comuni e interrompe le altre otto soltanto dopo lo sviluppo necessario. Le barre nuove vengono ancorate prima del tratto che le richiede. Il confine di verifica non è una giunzione automatica. Se due code di ancoraggio dello stesso diametro e sulla stessa retta si sovrappongono dopo una breve assenza, il programma mantiene una sola barra continua.

I giunti effettivi hanno marca J, gruppi collegati, numero di coppie, quote iniziale e finale e quattro lunghezze: iniziale, richiesta, adottata ed effettiva. Un cambio di diametro può generare un giunto tra barre sullo stesso raggio nominale; disposizioni angolari diverse restano avvii e interruzioni ancorati. La finestra arancione del giunto è unica e viene richiamata su entrambe le barre. Un giunto troppo corto, interferente con un altro o con distanza trasversale non ammessa viene segnalato e non rende utilizzabile MRd. La disposizione trasversale esecutiva e il confinamento restano da completare; le sezioni disegnate sono nominali.

'''
if 'Al cambio di armatura la distinta segue le singole barre.' not in text:text=text.replace(marker,addition+marker)
text=text.replace('quattro tratti e due gruppi longitudinali.', 'quattro tratti, otto barre continue per 12 m e otto barre interrotte con ancoraggio oltre 6 m.')
practical.write_text(text,encoding='utf-8')

theory=support/'docs/guida-teorica-anthea.md'
text=theory.read_text(encoding='utf-8-sig').replace('revisione documentale 23','revisione documentale 24')
text=text.replace("L'estensione teorica delle barre oltre il confine del tratto è max(lbd,l0)+a_l, limitata agli sviluppi fisicamente disponibili.",
    "Per una barra che inizia o termina, l'estensione oltre il confine teorico è lbd+a_l, limitata allo sviluppo fisicamente disponibile. La lunghezza l0 non si aggiunge a un'interruzione senza giunto.")
text=text.replace('Tratti contigui con identica disposizione delle barre formano un unico gruppo continuo.',
    'Le barre coincidenti per coordinate e diametro proseguono attraverso i tratti, anche quando cambia il numero complessivo delle barre. I gruppi della distinta raccolgono soltanto barre con lo stesso percorso, diametro, quote e sviluppi. Le barre che cessano o iniziano hanno ancoraggi autonomi; le code coincidenti dello stesso diametro che si intersecano attraverso un breve intervallo privo di domanda vengono riunite in una barra continua.')
text=text.replace('La disposizione raggruppata dei giunti non soddisfa automaticamente una percentuale di sovrapposizione inferiore al 100%:',
    'Il calcolo della lunghezza richiesta assume il 100% coerentemente con i giunti effettivamente raggruppati, anche se l’utente richiede una percentuale inferiore. La disposizione raggruppata non soddisfa automaticamente tale richiesta:')
marker='Per non accreditare capacità a barre insufficientemente sviluppate,'
addition='''Al cambio di diametro o raggio nominale si associano soltanto barre sulla stessa direzione radiale, dopo aver mantenuto le coincidenze esatte continue. Per ciascuna coppia si costruisce una sola finestra di giunto. Si definiscono u=lbd,sup+a_l,sup e v=lbd,inf+a_l,inf. La lunghezza adottata è max(l0 iniziale dei due lati, l0 richiesta dei due lati, u+v), arrotondata per eccesso a 0,10 m. Posto e=[l0 adottata−(u+v)]/2, la barra inferiore inizia a b−v−e e quella superiore termina a b+u+e, dove b è il confine teorico. La loro intersezione misura esattamente l0 adottata, senza sommare due l0 indipendenti.

Il termine u+v è una convenzione conservativa del software per conservare il pieno sviluppo delle due armature nominali alla quota b; non è attribuito alla normativa come formula della lunghezza di sovrapposizione. Le formule di aderenza, ancoraggio, l0 richiesta e traslazione restano quelle del verificatore esistente e delle fonti già indicate. I risultati distinguono l0 richiesta, vincolo di sviluppo, lunghezza adottata e lunghezza fisica disponibile. Sviluppi esterni insufficienti non vengono inventati. Finestre di giunto interferenti e distanze trasversali non ammesse sono segnalate; non si accredita la capacità nominale dei gruppi interessati. Le coordinate delle barre sono nominali: il disegno non certifica piegature o spostamenti trasversali per l’accostamento delle barre.

La distinta include le sovrapposizioni una sola volta attraverso le lunghezze dei pezzi reali. Per otto barre lungo 12 m divise in due pezzi con l0=1,50 m, la quantità è 8×(12+1,50)=108 m. Per 16Ø24 ridotte a 8Ø24 a metà palo, le otto comuni restano continue e le altre otto hanno solo lo sviluppo dell’interruzione; non sono generati otto nuovi ferri al cambio. Le regressioni dirette coprono riduzione, aumento, diametri diversi, disposizioni ruotate, giunti commerciali, spazio insufficiente, giunti interferenti e quantità indipendenti dalle partizioni di verifica.

'''
if 'Al cambio di diametro o raggio nominale' not in text:text=text.replace(marker,addition+marker)
text=text.replace('del gruppo continuo. Fuori da questo intervallo', 'di ciascun gruppo necessario alla sezione, usando gli sviluppi pertinenti ai due estremi. Fuori da questi intervalli')
theory.write_text(text,encoding='utf-8')

for name in ('README.md','installer/Indice-guide.md'):
    path=support/name
    path.write_text(path.read_text(encoding='utf-8-sig').replace('Rev23','Rev24').replace('Revisione 23','Revisione 24'),encoding='utf-8')
path=support/'scripts/Build-AntheaGuides-Itec.py'
path.write_text(path.read_text(encoding='utf-8').replace("REVISION = '23'","REVISION = '24'").replace("REVISION_NOTE = 'TAVOLA ARMATURE DEL PALO E BARRE LATERALI'","REVISION_NOTE = 'CONTINUITÀ DELLE BARRE E GIUNTI DEL PALO'"),encoding='utf-8')
path=support/'scripts/Render-AntheaGuides-Itec.ps1'
path.write_text(path.read_text(encoding='utf-8-sig').replace("$Revision = '23'","$Revision = '24'"),encoding='utf-8-sig')
subprocess.run([sys.executable,str(support/'scripts/wiki/build-wiki-index.py')],check=True)
art=support/'artefatti/guide_anthea_itec_rev24'
art.mkdir(parents=True,exist_ok=True)
(art/'artifact.md').write_text('Aggiornamento delle due guide globali esistenti, preservando il modello ITEC. Documentare continuità delle singole barre, ancoraggi di interruzione e giunti con finestra unica; rendere esplicita la convenzione software sugli sviluppi e i limiti esecutivi.',encoding='utf-8')
subprocess.run([sys.executable,str(support/'scripts/Build-AntheaGuides-Itec.py')],check=True)
for name in ('README.md','installer/Indice-guide.md'):
    subprocess.run([sys.executable,str(support/'scripts/documentazione/markdown-pdf.py'),str(support/name)],check=True)
