"""Rev27: materiali condivisi e distinta ferri dei muri. Eseguire una sola volta."""
from pathlib import Path
import hashlib, json, shutil, subprocess, sys
root=Path(__file__).resolve().parents[3]; support=root/'supporto'
builder=support/'scripts/Build-AntheaGuides-Itec.py'
assert "REVISION = '26'" in builder.read_text(encoding='utf-8'), 'Controllare la revisione corrente prima di aggiornare'
archive=support/'SUPERATI/muri-materiali-distinta-rev27-20261005'
paths=[support/f'docs/guida-{kind}-anthea.{ext}' for kind in ('pratica','teorica') for ext in ('md','pdf')]
paths+=list((support/'documentazione/Guide_ANTHEA').glob('*Rev26.*'))
paths+=[support/name for name in ('README.md','README.pdf','installer/Indice-guide.md','installer/Indice-guide.pdf')]
records=[]
for path in paths:
    if not path.is_file(): continue
    target=archive/path.relative_to(support); target.parent.mkdir(parents=True,exist_ok=True)
    assert not target.exists(),target
    shutil.copy2(path,target)
    records.append(dict(origine=str(path.relative_to(root)),archivio=str(target.relative_to(root)),motivo='Materiali condivisi e distinta ferri dei muri',sostituzione=str(path.relative_to(root)).replace('Rev26','Rev27'),sha256=hashlib.sha256(target.read_bytes()).hexdigest()))
(archive/'registro.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')

path=support/'docs/guida-pratica-anthea.md';text=path.read_text(encoding='utf-8-sig').replace('revisione documentale 26','revisione documentale 27')
marker='### Avviare la verifica globale'
addition='''### Materiali e copriferro del muro

La scheda Materiali del muro usa gli stessi cataloghi GPC del verificatore delle sezioni in calcestruzzo. Selezionare la classe del CLS e dell'acciaio; le proprietà di catalogo sono protette. Personalizzato consente di assegnare resistenze, modulo dell'acciaio, deformazione ultima e nome. Il diagramma costitutivo e i coefficienti αcc, γc e γs sono interrogabili; fcd, fyd ed Ecm mostrano i valori effettivi. Il trasferimento Apri sezione in c.a. conserva questi dati.

Nei progetti, le schede Calcestruzzo e Acciaio del ramo forniscono i materiali al muro secondo le normali regole di ereditarietà. Il confronto dei dati comuni include esposizione, aggregato, vita utile, tolleranza e scelte di durabilità. Il copriferro adottato è condivisibile con le sezioni e viene confrontato con il minimo della scheda Materiali, usando il diametro massimo effettivamente presente nel muro. Adotta copriferro minimo applica esplicitamente la proposta. Il controllo non aumenta silenziosamente il copriferro e non modifica geometria, carichi o quantità di barre.

Gli archivi precedenti mantengono le resistenze e il legame dell'acciaio con cui erano stati calcolati; l'apertura non li riclassifica automaticamente come B450C. Nei muri a gravità le proprietà del CLS sono attive quando si sceglie Calcestruzzo non armato; la muratura non eredita proprietà meccaniche del CLS o dell'armatura non utilizzate.

'''
assert marker in text;text=text.replace(marker,addition+marker)
marker='### Gravità in calcestruzzo o muratura'
if marker not in text:
    marker='### Calcolo del muro a gravità'
# Insert immediately after the existing reinforcement-view paragraph, without altering the following modules.
anchor='Restano da definire il disegno esecutivo, i giunti di costruzione, i bordi lungo il muro, le interferenze tridimensionali e gli sfridi: la vista non è una distinta di officina.'
assert anchor in text
addition='''

### Tavola delle armature e distinta ferri del muro

In Input, sotto le armature, aprire Distinta ferri del tratto di muro. Inserire la lunghezza reale del tratto, il copriferro alle sue estremità e la lunghezza commerciale delle barre. La lunghezza del tratto cambia soltanto il computo: le verifiche strutturali rimangono riferite a una striscia di 1 m. Con due zone, indicare anche i collegamenti per fila e per metro. Lo sviluppo di un collegamento comprende tutti i tratti e i ganci della sagoma scelta; zero significa da definire e produce un peso parziale.

Dopo il ricalcolo, premere Tavola armature e distinta ferri nel riquadro di input oppure Distinta ferri in Verifiche. La finestra ha selezione della pagina e zoom. La prima tavola presenta la sezione quotata con le marche delle principali e la tabella dei pezzi del tratto. Le pagine successive separano le sagome e riportano diametro, faccia, tratti A/B/C, arco, mandrino, sviluppo totale, passo e sovrapposizione. P identifica le principali, S le secondarie lungo muro e C i collegamenti. Le due zone del fusto mantengono marche distinte.

PDF completo esporta tutte le tavole in A3 orizzontale. Word completo aggiunge tabelle modificabili; CSV distinta conserva quantità, impostazioni e note; PNG pagina esporta la pagina visualizzata. Anche la relazione Word del muro contiene la distinta del tratto e le tavole. Se si modificano i dati occorre attendere il nuovo calcolo prima di esportare.

Le barre oltre la lunghezza commerciale sono segnalate: il programma non introduce giunti automatici. Le secondarie sono conteggiate come barre rettilinee lungo muro; estremità, raccordi, ganci e disposizione degli strati richiedono il dettaglio esecutivo. Un computo completo non equivale a verifiche soddisfatte: gli esiti mancanti o sfavorevoli restano indicati nella tavola delle note.

![Esempio di distinta del muro con due zone di armatura e tratto di quattro metri](../artefatti/muri-materiali-distinta-20261005/interfaccia/distinta-due-zone-1.png)

Esempio riapribile: supporto/artefatti/muri-materiali-distinta-20261005/interfaccia/distinta-due-zone.anthea. Il muro ha H=3 m, due zone con cambio a 1,5 m e un tratto lungo 4 m. La distinta mostra 23 pezzi per ciascuna marca principale e segnala lo sviluppo mancante dei collegamenti C1. I PDF e i Word della stessa cartella consentono di ripercorrere la lettura. L'esempio illustra input e computo, non costituisce un muro esecutivo verificato. Il riferimento grafico richiesto dall'utente è la tavola SIM-CAD di Madosoft; non è utilizzato come validazione numerica.
'''
text=text.replace(anchor,anchor+addition);path.write_text(text.rstrip()+'\n',encoding='utf-8')

path=support/'docs/guida-teorica-anthea.md';text=path.read_text(encoding='utf-8-sig').replace('revisione documentale 26','revisione documentale 27')
marker='### Gravità in calcestruzzo o muratura';assert marker in text
addition='''### Materiali del muro e condivisione nel progetto

Il muro costruisce l'input del verificatore c.a. con gli stessi materiali GPC: fck, fyk, Es, fu, εu, legami costitutivi e coefficienti αcc, γc e γs. Le verifiche N–M, le curvature SLE, le resistenze a taglio e il calcolo degli ancoraggi utilizzano i parametri effettivi; fcd=αcc fck/γc e fyd=fyk/γs. I cataloghi non sono duplicati nel modulo dei muri. Per gli archivi precedenti, i campi mancanti mantengono il comportamento originario, compreso l'acciaio elastoplastico con fu=fy, senza cambiare le resistenze memorizzate.

Il minimo nominale di copriferro è ricavato dallo stesso motore MaterialCover della scheda Calcestruzzo, con esposizione, vita, aggregato, tolleranza, tipo di elemento, superficie di getto, abrasione e opzioni applicabili al metodo selezionato. Il diametro da usare è il massimo delle barre attive, comprese secondarie e collegamenti quando previsti. Il copriferro adottato resta un input distinto dal minimo richiesto. La gerarchia dei progetti condivide materiali e durabilità senza trasferire geometria o azioni del muro.

### Geometria e quantità della distinta ferri del muro

La distinta è calcolata separatamente dalle resistenze e dal disegno. La lunghezza L del tratto, il copriferro di estremità ce e la lunghezza commerciale sono parametri del computo, non della striscia strutturale di 1 m. Per una marca con n barre/m e diametro φ, il passo del modello è p=(1000−2c−φ)/(n−1), in mm. Nel tratto si usa N=ceil((1000L−2ce−φ)/p)+1 e si ridistribuiscono le N barre con passo non superiore a p. Il peso del tratto non è necessariamente L volte la stima continua kg/m, perché comprende i pezzi alle estremità.

Lo sviluppo delle principali somma i tratti rettilinei all'asse e gli archi. Per una piega di 90° con mandrino Dm si usa r=(Dm+φ)/2 e sviluppo πr/2. La polilinea campionata serve soltanto al disegno: il peso non usa la somma delle corde. L'armatura alta del fusto comprende la sovrapposizione prevista dal modello; le barre delle due zone restano pezzi distinti. Per la faccia inclinata il raccordo al piede è indicato come dettaglio da completare.

Le secondarie sono rettilinee lungo il muro, con lunghezza L−2ce/1000; sono disposte sulle due facce con passo non maggiore di quello inserito. Al cambio di zona nel fusto e alla divisione delle due reti nella fondazione, la barra sul confine appartiene alla zona successiva. Per i collegamenti della giunzione, le file sono ceil(l0/passo)+1 e i pezzi per fila sono ceil(collegamenti_per_metro·L). Lo sviluppo assegnato deve includere i ganci; quando manca, il peso della distinta è esplicitamente parziale. Non viene attribuita una verifica dei ganci al solo sviluppo assegnato.

Per ogni marca, peso=N·Ltaglio·(πφ²/4)·0,00785 kg, con φ in mm e Ltaglio in m. Lo sviluppo oltre la barra commerciale produce una nota, senza aggiungere giunti o frammenti non verificati. Quantità, sviluppi e pesi sono preliminari: sfridi, giunti di costruzione, estremità, interferenze e sagomature esecutive richiedono completamento.

Riscontro indipendente: H=4 m, soletta 0,60 m, copriferro 50 mm, φ16 e mandrino 160 mm danno r=88 mm. Con ancoraggio assegnato di 1 m, A=3,942 m, B=0,454 m, arco=0,138230077 m e coda=0,407769923 m; Ltaglio=4,942 m. Sei barre forniscono 46,800823495 kg. Con tratto di 3 m e copriferro di estremità 50 mm, il numero sale a 18. Le prove verificano anche due zone, collegamenti incompleti, limiti commerciali e salvataggio. Sorgenti in supporto/test/RetainingWall.Checks/BarScheduleChecks.cs; evidenze in supporto/artefatti/muri-materiali-distinta-20261005.

Riferimento grafico: Madosoft, Muri di sostegno, pagina https://www.madosoft.it/prodotti/software-tecnico/cemento-armato/muri-di-sostegno, e immagine SIM-CAD fornita dall'utente. Il riferimento riguarda la disposizione della tavola e delle sagome; non è un confronto dei motori di calcolo.

'''
text=text.replace(marker,addition+marker);path.write_text(text.rstrip()+'\n',encoding='utf-8')
for name in ('README.md','installer/Indice-guide.md'):
    path=support/name;text=path.read_text(encoding='utf-8-sig').replace('Rev26','Rev27').replace('Revisione 26','Revisione 27')
    text=text.replace('- Revisione 27: controlli sismici di testa', '- Revisione 26: controlli sismici di testa')
    text+='\n- Revisione 27: materiali dei muri condivisi con sezioni e schede Materiali; distinta ferri del tratto con tavole, sviluppi, quantità ed esportazioni PDF, Word e CSV.\n'
    path.write_text(text,encoding='utf-8')
builder.write_text(builder.read_text(encoding='utf-8').replace("REVISION = '26'","REVISION = '27'").replace("REVISION_NOTE = 'DETTAGLI SISMICI DELLA TESTA DEI PALI'","REVISION_NOTE = 'MATERIALI E DISTINTA FERRI DEI MURI'"),encoding='utf-8')
path=support/'scripts/Render-AntheaGuides-Itec.ps1';path.write_text(path.read_text(encoding='utf-8-sig').replace("$Revision = '26'","$Revision = '27'"),encoding='utf-8-sig')
art=support/'artefatti/guide_anthea_itec_rev27';art.mkdir(parents=True,exist_ok=True)
(art/'artifact.md').write_text('Aggiornare le due guide globali ITEC con i materiali condivisi dei muri e la distinta ferri, mantenendo tutti gli argomenti della Rev26. Documentare criteri geometrici, input, conteggi e limiti; aggiungere l’immagine reale della tavola generata. Aggiornare indici e PDF.',encoding='utf-8')
subprocess.run([sys.executable,str(support/'scripts/wiki/build-wiki-index.py')],check=True)
subprocess.run([sys.executable,str(builder)],check=True)
for name in ('README.md','installer/Indice-guide.md'):
    subprocess.run([sys.executable,str(support/'scripts/documentazione/markdown-pdf.py'),str(support/name)],check=True)
