"""Rev19: pile parameter sources, explicit checks, compact materials and progress."""
from pathlib import Path
import hashlib,json,shutil,subprocess,sys
ROOT=Path(__file__).resolve().parents[3];SUP=ROOT/'supporto';ARCH=SUP/'SUPERATI/palo-chiarezza-rev19-20261005'
paths=[SUP/f'docs/guida-{k}-anthea.{e}' for k in ('pratica','teorica') for e in ('md','pdf')]
paths+=list((SUP/'documentazione/Guide_ANTHEA').glob('*Rev18.*'))
paths += [SUP/'README.md',SUP/'README.pdf',SUP/'installer/Indice-guide.md',SUP/'installer/Indice-guide.pdf']
registry=[]
for p in paths:
    if not p.exists():continue
    dest=ARCH/p.relative_to(SUP);dest.parent.mkdir(parents=True,exist_ok=True)
    if not dest.exists():shutil.copy2(p,dest)
    registry.append(dict(origine=str(p.relative_to(ROOT)),archivio=str(dest.relative_to(ROOT)),motivo='Rev19: fonti per strato, dettagli espliciti, materiali e avanzamento',sostituzione=str(p.relative_to(ROOT)).replace('Rev18','Rev19'),sha256=hashlib.sha256(dest.read_bytes()).hexdigest()))
(ARCH/'registro.json').write_text(json.dumps(registry,ensure_ascii=False,indent=2),encoding='utf-8')
practical='''### Materiali e parametri per strato

Nel pannello Momento plastico resistente scegliere calcestruzzo e acciaio dalle tendine del catalogo GPC Model. Le proprietà numeriche sono raccolte nei dati opzionali, chiusi inizialmente, insieme a esposizione, vita utile, qualità del copriferro e legami. Gli archivi personalizzati conservano i valori precedenti. N e MRd sono sotto il disegno della sezione. Le opzioni di resistenza manuale ed EJ rimangono accessibili in un pannello chiuso.

Coefficienti unitari è disattivato inizialmente. Attivandolo si usa una configurazione custom della normativa di sezione con tutti i fattori gamma e alpha_cc pari a 1. Disattivandolo si recuperano i coefficienti ordinari conservati nell'archivio. Il comando riguarda la resistenza strutturale: non modifica i fattori geotecnici di Broms. La modalità custom compare nei risultati e nel report.

Le colonne della stratigrafia sono Addensamento, Determinazione, Parametro, Minimo, Massimo e Adottato. Per i granulari si scelgono Sciolto, Medio o Denso; per i coesivi la categoria riporta il terreno effettivo della tabella 14.6. Gli autori sono filtrati per quella categoria. Il pulsante Tabelle nella riga mostra integralmente i cataloghi 14.5 e 14.6 usati da Checker, con unità, intervalli, media software e valori consigliati bibliografici distinti. Cambiare categoria aggiorna una media automatica; un valore manuale è conservato e richiede la conferma della nuova fonte. Il cambio tra granulare e coesivo conserva le scelte precedenti nello storico e propone una modalità compatibile.

### Avanzamento e controlli da completare

La barra indica analisi FEM e convergenza, quindi sezione in elaborazione e coppie MRd positivo/negativo completate rispetto agli N distinti locali. I valori già disponibili nella cache sono conteggiati. Proponi e Dimensiona sono disponibili soltanto con verifiche aggiornate. Anche Dimensiona mostra disposizione in esame e avanzamento della relativa ricerca MRd. Un errore indica la causa e interrompe la barra, senza esportare risultati obsoleti.

Il riepilogo di ogni tratto elenca sempre i controlli non soddisfatti, quelli da completare e quelli esclusi. Il comportamento iniziale è Pilastro, modificabile in Trave oppure Solo controlli comuni nelle ipotesi. È una scelta esplicita dei dettagli da controllare. I minimi specifici dei pali rimangono separati. Esposizione, resistenza del calcestruzzo, vita utile e qualità del copriferro provengono dalla sezione principale; il cmin,dur è calcolato con Checker. Un valore manuale precedente viene conservato come override esplicito. Il foglio c.a. aperto dal tratto riceve esposizione e comportamento impostati.

Fare clic a una quota sul profilo o sui diagrammi: una linea arancione identifica il taglio. La sezione trasversale mostra x dalla testa, z dal piano campagna e il lato dell'interfaccia; la selezione resta invariata dopo il ricalcolo delle armature. Senza clic non viene mostrata una quota implicita. Nel disegno la staffa è verde con richiamo del diametro e assi u/v; ganci e chiusure non sono dettagli esecutivi verificati.

La proposta cerca il primo attraversamento discendente di metà del massimo assoluto del momento, dopo l'ultimo massimo. La quota effettiva è quella realizzabile più vicina su griglia di 0,5 m, con tratti almeno di 3 m e sviluppo entro le barre commerciali assegnate. Oltre il cambio è predisposta una sezione personalizzabile con armatura iniziale copiata dalla principale: usare Dimensiona o modificarla e controllare gli esiti. In assenza di attraversamento realizzabile resta una proposta costruttiva senza riduzione automatica. Quota teorica, quota proposta e criterio sono salvati.

'''
theory='''### Dettagli dei pali e comportamento adottato

Fonte consultata: D.M. 17 gennaio 2018, Gazzetta Ufficiale n. 42, supplemento ordinario n. 8, §7.2.5, pagina PDF 217, pagina stampata 213, disponibile sul portale ufficiale: https://www.gazzettaufficiale.it/eli/gu/2018/02/20/42/so/8/sg/pdf.

Il paragrafo distingue i pali dalle fondazioni superficiali. Per i pali in calcestruzzo richiede lungo il fusto As almeno 0,3% Ac, diametro trasversale almeno 8 mm e passo non oltre 8 diametri longitudinali. Sono i tre controlli specifici disponibili. In presenza delle condizioni dissipative indicate dal testo, sono richiesti ulteriori dettagli e controlli: estensione delle zone, armatura longitudinale almeno 1%, staffe singole a passo massimo 6 diametri, duttilità e condizioni aggiuntive sulle azioni. Questi ultimi non vengono dedotti dall'analisi elastica e restano esplicitamente esclusi. Non si dichiara quindi completa la verifica sismica del palo.

Il default Pilastro aggiunge, per scelta dell'utente, le regole del verificatore esistente per diametri, interassi, armatura minima e massima, staffe e trattenimento delle barre. La compressione per il minimo longitudinale è la massima positiva del tratto, convertita da kN a N. Se si sceglie Solo controlli comuni, l'esclusione delle regole dell'elemento resta nell'elenco dei controlli da completare. Il controllo della durabilità richiama CoverRequirements di Checker, con esposizione e fck condivisi, vita 50/100 anni e qualità dichiarata; nessuna correlazione è duplicata nell'interfaccia.

### Criterio di cambio sezione e tempi delle verifiche

La ricerca costruttiva conserva griglia 0,5 m, lunghezza minima e vincoli commerciali già descritti. Quando disponibile, individua il primo attraversamento discendente di |M|max/2 dopo l'ultimo massimo assoluto e lo interpola fra le ascisse calcolate. Fra le partizioni ammissibili sceglie un confine vicino a tale quota; in parità usa il costo costruttivo. La ricerca controlla sia la parte precedente sia la successiva, senza creare un ultimo tratto troppo corto. Non equivale a dimezzare l'armatura: la sezione successiva deve essere dimensionata sulle azioni N-V-M concomitanti.

Il progresso MRd conta coppie di resistenze completate, una per ciascun N esatto distinto, compresi i valori recuperati dalla cache. I tempi esportati separano preparazione della sezione, costruzione dei solutori indipendenti, calcolo parallelo delle resistenze, verifiche N-M-V e dettagli. Modificare il solo passo delle staffe aggiorna taglio e dettagli riutilizzando il dominio N-M; cambiare diametro delle staffe può spostare le barre e invalida invece le resistenze. Le misure riproducibili sono raccolte in supporto/artefatti/palo-chiarezza.

La configurazione custom Coefficienti unitari modifica un'istanza della normativa GPC, impostando a 1 ogni proprietà gamma disponibile e alpha_cc. Materiali, geometria e azioni restano quelli assegnati; la modalità è tracciata. Non è una verifica con i coefficienti ordinari NTC e non altera EJ lordo né il peso unitario adottato.

'''
for kind,addition in [('pratica',practical),('teorica',theory)]:
    p=SUP/f'docs/guida-{kind}-anthea.md';s=p.read_text(encoding='utf-8-sig')
    marker='### Materiali e parametri per strato' if kind=='pratica' else '### Dettagli dei pali e comportamento adottato'
    if marker not in s:
        pos=s.index('### Tratti colorati e verificatore della sezione') if kind=='pratica' else s.index('### Calcolo parallelo e dipendenze dei risultati')
        s=s[:pos]+addition+s[pos:]
    s=s.replace('revisione documentale 18','revisione documentale 19')
    s=s.replace('Le regole specifiche dei pilastri non sono trasferite automaticamente ai pali.', 'Le regole dei pilastri sono il default modificabile richiesto dall’utente, distinto dai minimi specifici dei pali.')
    s=s.replace('Il copriferro richiede cmin,dur dal progetto di durabilità: senza questo dato il relativo esito rimane da completare.', 'Il copriferro usa cmin,dur calcolato dalla durabilità condivisa o un override esplicito; con dati mancanti rimane da completare.')
    p.write_text(s,encoding='utf-8')
for p in (SUP/'README.md',SUP/'installer/Indice-guide.md'):
    p.write_text(p.read_text(encoding='utf-8-sig').replace('Rev18','Rev19').replace('Revisione 18','Revisione 19'),encoding='utf-8')
p=SUP/'scripts/Build-AntheaGuides-Itec.py';s=p.read_text(encoding='utf-8').replace("REVISION = '18'","REVISION = '19'").replace("REVISION_NOTE = 'PALO ELASTICO ARMATURE E CALCOLO PARALLELO'","REVISION_NOTE = 'PALO MATERIALI FONTI E CONTROLLI ESPLICITI'");p.write_text(s,encoding='utf-8')
p=SUP/'scripts/Render-AntheaGuides-Itec.ps1';p.write_text(p.read_text(encoding='utf-8-sig').replace("$Revision = '18'","$Revision = '19'"),encoding='utf-8-sig')
art=SUP/'artefatti/guide_anthea_itec_rev19';art.mkdir(parents=True,exist_ok=True)
(art/'artifact.md').write_text('Rev19 delle due guide globali sul modello ITEC esistente. Fonti per strato, comportamento dettagli, materiali GPC, coefficienti unitari, avanzamento e proposta Mmax/2. Conservare stile e struttura del modello.',encoding='utf-8')
subprocess.run([sys.executable,str(SUP/'scripts/wiki/build-wiki-index.py')],check=True)
subprocess.run([sys.executable,str(SUP/'scripts/Build-AntheaGuides-Itec.py')],check=True)
for p in (SUP/'README.md',SUP/'installer/Indice-guide.md'):subprocess.run([sys.executable,str(SUP/'scripts/documentazione/markdown-pdf.py'),str(p)],check=True)
