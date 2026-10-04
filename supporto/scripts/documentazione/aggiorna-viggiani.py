"""Integrate the visually verified source into the two canonical guides (revision 13)."""
from pathlib import Path
import json, shutil, hashlib, re, importlib.util, subprocess, sys
ROOT=Path(__file__).resolve().parents[3];SUP=ROOT/'supporto'
ARCH=SUP/'SUPERATI/viggiani-rev13-20261003'
paths=[SUP/f'docs/guida-{k}-anthea.{e}' for k in ('pratica','teorica') for e in ('md','pdf')]
paths+=list((SUP/'documentazione/Guide_ANTHEA').glob('*Rev12.*'))
paths += [SUP/'installer/Indice-guide.md',SUP/'installer/Indice-guide.pdf',SUP/'README.md',SUP/'README.pdf',SUP/'installer/README.md',SUP/'installer/README.pdf']
registry=[]
for p in paths:
    if not p.exists():continue
    dest=ARCH/p.relative_to(SUP);dest.parent.mkdir(parents=True,exist_ok=True)
    if not dest.exists():shutil.copy2(p,dest)
    registry.append(dict(origine=str(p.relative_to(ROOT)),archivio=str(dest.relative_to(ROOT)),motivo='Rev13: fonte Viggiani verificata, falda e direzioni negative',sostituzione=str(p.relative_to(ROOT)).replace('Rev12','Rev13'),sha256=hashlib.sha256(dest.read_bytes()).hexdigest()))
(ARCH/'registro.json').write_text(json.dumps(registry,ensure_ascii=False,indent=2),encoding='utf-8')
source="Carlo Viggiani, Fondazioni, scansione locale fornita dall'utente: pagina PDF 237, pp. stampate 464–465 (§14.4.1); PDF 238, pp. 466–467; PDF 244, pp. 478–479 (equazione 14.25 e tabelle 14.5–14.6). Sono state lette visivamente anche le pagine adiacenti PDF 243 e 245, pp. 476–477 e 480–481. L'edizione non è identificabile nella scansione: la prima pagina contiene la fine della prefazione, datata dicembre 1998, senza frontespizio o colophon. Tale data non viene usata per dedurre l'edizione. La trattazione è di Viggiani; le correlazioni sono attribuite agli autori indicati nel libro. Gli articoli originali non sono stati consultati."
practical='''### Assegnazione delle rigidezze

Selezionare uno strato nella tabella: il pannello sottostante mostra soltanto gli input della sua modalità. Assegnare nome, spessore e condizioni di impiego. Il nome del terreno non seleziona una rigidezza. Coprire tutta la lunghezza immersa; il terreno oltre la punta non contribuisce. Sono disponibili:

| Modalità | Input espliciti | Valore adottato |
| --- | --- | --- |
| kh costante assegnato | kh [kN/m³] | k = kh D |
| Reese–Matlock con nh assegnato | nh [kN/m³] | kh = nh z/D; k = nh z |
| Tabella 14.5 per sabbie | Stato di addensamento e falda | nh tabellato, distinto per sabbie immerse e non immerse |
| Tabella 14.6 per coesivi | Riga e autore; valore scelto [N/cm³] | Conversione del valore scelto; nessuna media automatica |
| Correlazione A γ/1,35 | Addensamento, A, γ e γsat [kN/m³] | nh in kN/m³; sotto falda γ′ = γsat − γw |
| k distribuito assegnato | k [kN/m²] | Nessuna ulteriore moltiplicazione per D |

Il testo associa kh costante alle argille sovraconsolidate; la legge lineare di Reese e Matlock (1956) ai terreni incoerenti e alle argille normalmente consolidate o debolmente sovraconsolidate. Indicare nelle condizioni dello strato l'applicabilità, il drenaggio e il livello di deformazione della rigidezza adottata. Queste indicazioni non costituiscono un'assegnazione automatica in base al nome del terreno.

La tabella 14.6 richiede un valore esplicito nell'intervallo della singola riga. Il pannello mostra autore e limiti; anche il valore singolo della torba di Davisson deve essere confermato con l'inserimento. La correlazione richiede A esplicito, mostrando intervallo e valore consigliato della tabella 14.5. Le modalità assistite permettono un override di nh in kN/m³ con motivazione obbligatoria: risultato e report conservano sia il valore di base sia quello adottato. Non mescolare righe di autori diversi.

Attivare la falda e inserire la sua profondità dal piano campagna, oltre a γw (inizialmente 9,81 kN/m³). La tabella delle sabbie passa al valore immerso; la correlazione usa γ′. Le modalità manuali e la tabella dei coesivi conservano il parametro assegnato. Il riepilogo mostra per ogni tratto modalità, profondità, parametro, fonte e applicabilità. Gli archivi precedenti con kh,rif mantengono il valore, ora indicato come nh della legge lineare.

z parte dal piano campagna, esclude il tratto libero e non si azzera alle interfacce. Il raccordo a strati è una convenzione numerica dichiarata, non una prescrizione originale del libro. Il modello crea nodi alla falda quando cambia la legge; i diagrammi conservano i due valori alle discontinuità. La linea tratteggiata azzurra identifica la falda. CSV, JSON e relazione contengono anche la determinazione dei parametri. Il catalogo completo è nella guida teorica.

'''+source+'\n\n'
theory=r'''Sono disponibili kh costante manuale, nh manuale nella legge Reese–Matlock, selezione assistita di nh nelle tabelle 14.5 e 14.6, correlazione A γ/1,35 e k distribuito manuale. Viggiani scrive p = kh y, P = p d ed Es = kh d; il nostro k corrisponde a Es. p e P nel testo sono le intensità resistenti; la reazione sul palo qui ha il segno q = −k y.

$$ k_h(z)=n_h\frac{z}{D},\quad k(z)=E_s=n_h z

kh e nh hanno entrambi dimensioni F/L³, k ha F/L². La legge lineare è attribuita a Reese e Matlock (1956). Il testo la associa a terreni incoerenti, argille normalmente consolidate o debolmente sovraconsolidate; kh costante è associato alle argille sovraconsolidate. Le correlazioni costanti con E50 e cu discusse a p. 466 non sono assegnate automaticamente: questa implementazione mantiene kh costante come input manuale. L'utente documenta drenaggio, stato tensionale e livello di deformazione; non si deduce kh dalla sola classificazione del terreno.

### Parametri delle tabelle di Viggiani

La tabella 14.5 separa A, adimensionale, dai valori direttamente consigliati di nh. Gli intervalli di A non sono intervalli di nh. I valori tabellati di nh sono in N/cm³:

$$ 1\,\mathrm{N/cm^3}=1000\,\mathrm{kN/m^3}

| Addensamento | A orientativo | A consigliato | nh sabbie non immerse [N/cm³] | nh sabbie immerse [N/cm³] |
| --- | --- | --- | --- | --- |
| Sciolto | 100–300 | 200 | 2,5 | 1,5 |
| Medio | 300–1000 | 600 | 7,5 | 5 |
| Denso | 1000–3000 | 1500 | 20 | 12 |

Per la correlazione, equazione 14.25:

$$ n_h=\frac{A\gamma}{1,35},\quad \gamma'=\gamma_{sat}-\gamma_w

Con γ in kN/m³ si ottiene nh in kN/m³. Sotto falda si usa γ′. A deve essere scelto esplicitamente entro l'intervallo del relativo addensamento; il valore consigliato viene mostrato ma non assegnato silenziosamente. La modalità correlazione è distinta dalla selezione dei valori di nh tabellati: ad esempio A=600 e γ=18 danno nh=8000 kN/m³, mentre la riga Medio non immerso della tabella dà 7500 kN/m³. Con γsat=20 e γw=9,81, la correlazione dà nh=4528,888889 kN/m³ sotto falda.

La tabella 14.6 conserva separatamente tutte le righe e gli autori come stampati nel libro. I valori sono orientativi e non intercambiabili fra fonti. Per un intervallo l'utente deve scegliere il valore: non viene adottata la media.

| Terreno | nh [N/cm³] | Fonte indicata nella tabella 14.6 |
| --- | --- | --- |
| Argilla n.c. o lievemente o.c. | 0,2–3,5 | Reese, Matlock, 1956 |
| Argilla n.c. o lievemente o.c. | 0,3–0,5 | Davisson, Prakash, 1963 |
| Argilla organica n.c. | 0,1–1 | Peck, Davisson, 1970 |
| Argilla organica n.c. | 0,1–0,8 | Davisson, 1970 |
| Torba | 0,05 | Davisson, 1970 |
| Torba | 0,03–0,1 | Wilson, Hilts, 1967 |
| Loess | 8–10 | Bowles, 1968 |

Gli override manuali delle modalità assistite richiedono una motivazione e conservano valore di base, valore adottato, intervallo, fonte e condizioni. Il singolo valore della torba non è trasformato in un intervallo. Le considerazioni del testo su non linearità, effetti ciclici e durata del carico non introducono riduzioni automatiche nel modello elastico.

### Estensione a strati e falda

La formulazione di riferimento del libro è riferita a terreno uniforme. Nell'estensione numerica implementata si assegna il parametro locale di ciascuno strato, mantenendo z globale dal piano campagna: k(z)=nh,strato z. Non si azzera z e non si forza la continuità di k. Questa scelta di raccordo non è attribuita a una prescrizione originale di Viggiani. I nodi coincidono con le interfacce; per sabbie tabellate e correlazione A γ/1,35, la falda può suddividere uno strato in due tratti con lo stesso identificativo originario e parametri distinti. Non si modifica il valore manuale o il parametro della tabella 14.6 alla falda. I due lati di ogni salto sono conservati nei risultati. Non si moltiplica nuovamente nh z per D.

'''
validation=r'''### Confronto con le soluzioni di Reese e Matlock riportate nel libro

Alle pp. stampate 466–467 (PDF 238) la lunghezza caratteristica è λ=(EI/nh)^(1/5). Per L/λ > 4, testa libera e solo H, il testo riporta y0=2,40 H/(nh^0,6 EI^0,4) e |θ0|=1,60 H/(nh^0,4 EI^0,6). Per testa con rotazione impedita il coefficiente di y0 è 0,93. Il segno della rotazione del testo viene adattato alla convenzione θ=y′ adottata qui. Le soluzioni pubblicate usano coefficienti approssimati e costituiscono un riferimento distinto dal test di convergenza.

Il test usa EI=50000 kNm², nh=5000 kN/m³, L=30 m, H=100 kN e punta libera. Si ottengono ytesta=0,0193414613 m contro 0,0191091442 m della formula (scarto 1,216%); |θtesta|=0,00813548843 rad contro 0,00803803658 rad (1,212%). La tolleranza dichiarata è 2%. A testa bloccata ytesta=0,00738777995 m contro 0,00740479337 m (0,230%, tolleranza 1%). Il test con solo momento verifica anche i coefficienti 1,60 e 1,74: scarti 1,212% e 0,389%, tolleranza 2%. Questi scarti non sono errori di mesh.

Per L/λ < 2, il riferimento rigido con solo H è y0=18H/(nh L²), |θ0|=24H/(nh L³); con rotazione impedita y0=2H/(nh L²). Il test L=0,2 m, H=0,001 kN confronta il solutore con le espressioni del libro nel limite rigido, con tolleranza 0,1%.

| Passo [m] | ytesta [m] | massimo assoluto M [kNm] | massimo assoluto V [kN] |
| --- | --- | --- | --- |
| 0,50 | 0,0274768019 | 200,317922 | 100 |
| 0,25 | 0,0274769497 | 200,317875 | 100 |
| 0,125 | 0,0274769590 | 200,317872 | 100 |

La tabella usa lo stesso palo lungo con H=100 kN e C=−100 kNm. Le variazioni relative fra le due mesh più fini sono 3,38×10⁻⁷ per ytesta, 1,33×10⁻⁸ per M e meno di 10⁻¹⁰ per V. I controlli diretti Checker comprendono 101 asserzioni: cataloghi, conversione, correlazione e falda, equivalenza manuale/assistita, origine globale di z, discontinuità, equilibrio, riferimenti analitici, mesh, unità, vincoli e labilità. Restano distinti i 32 test preesistenti delle classi Pile, inclusi Broms e capacità stratificata. Le prove non costituiscono una taratura sperimentale delle rigidezze.

'''
for kind in ('pratica','teorica'):
    p=SUP/f'docs/guida-{kind}-anthea.md';s=p.read_text(encoding='utf-8-sig')
    s=s.replace('revisione documentale 12','revisione documentale 13')
    if kind=='pratica':
        s=re.sub(r'### Assegnazione delle rigidezze\n.*?(?=### Lettura e conservazione)',lambda _:practical,s,flags=re.S)
        s=s.replace('X e Y sono attive inizialmente; la direzione personalizzata è facoltativa e disattivata.','X e Y sono attive inizialmente; X− (180°), Y− (270°) e la direzione personalizzata sono facoltative e inizialmente disattivate. Ogni direzione ha risultati e interassi rappresentativi distinti; invertire H può cambiare i coefficienti dei singoli pali anche quando la media della palificata simmetrica resta uguale.')
        s=s.replace('premere Calcola prima di esportare','attendere il ricalcolo automatico prima di esportare')
    else:
        s=re.sub(r'La pagina del catalogo Edizioni Efesto.*?(?=\n\n### Modulo di reazione)',lambda _:source,s,flags=re.S)
        s=re.sub(r'Sono implementate tre leggi assegnate:.*?(?=### Modello e condizioni)',lambda _:theory,s,flags=re.S)
        if '### Confronto con le soluzioni di Reese e Matlock riportate nel libro' not in s:
            s=s.replace('## Approfondimenti integrati',validation+'## Approfondimenti integrati',1)
    p.write_text(s,encoding='utf-8')
for p in [ROOT/'README.md',SUP/'README.md',SUP/'installer/README.md',SUP/'installer/Indice-guide.md']:
    s=p.read_text(encoding='utf-8-sig').replace('Rev12','Rev13').replace('Revisione 12','Revisione 13')
    s=s.replace('L’attribuzione a Viggiani resta da verificare sulle pagine originali.','La Rev13 documenta le pagine Viggiani consultate visivamente, Reese–Matlock, tabelle 14.5–14.6, correlazione A γ/1,35 e falda; l’edizione della scansione resta non identificabile.')
    p.write_text(s,encoding='utf-8')
builder=SUP/'scripts/Build-AntheaGuides-Itec.py';s=builder.read_text(encoding='utf-8').replace("REVISION = '12'","REVISION = '13'").replace("DATE = '02/10/2026'","DATE = '03/10/2026'").replace("CONTENTS = '2 ottobre 2026'","CONTENTS = '3 ottobre 2026'").replace("CONTENTS_ISO = '2026-10-02'","CONTENTS_ISO = '2026-10-03'").replace("REVISION_NOTE = 'RISPOSTA ELASTICA DEL PALO E PALIFICATE'","REVISION_NOTE = 'VIGGIANI E DIREZIONI DELLE PALIFICATE'");builder.write_text(s,encoding='utf-8')
render=SUP/'scripts/Render-AntheaGuides-Itec.ps1';render.write_text(render.read_text(encoding='utf-8-sig').replace("$Revision = '12'","$Revision = '13'"),encoding='utf-8-sig')
spec=importlib.util.spec_from_file_location('builder',builder);B=importlib.util.module_from_spec(spec);spec.loader.exec_module(B);B.ART.mkdir(parents=True,exist_ok=True)
(B.ART/'artifact.md').write_text('Rev13. Aggiornamento delle due guide globali nel modello ITEC esistente: fonte Viggiani verificata visivamente, leggi, parametri, falda, validazione e direzioni X−/Y−. Conservare stile, indice Word e formule OMML. QA delle pagine modificate e dell’indice.\n',encoding='utf-8')
for kind in B.GUIDES:B.build(kind)
spec=importlib.util.spec_from_file_location('pdf',SUP/'scripts/documentazione/markdown-pdf.py');PDF=importlib.util.module_from_spec(spec);spec.loader.exec_module(PDF)
for p in [SUP/'README.md',SUP/'installer/README.md',SUP/'installer/Indice-guide.md']:PDF.build(p)
subprocess.run([sys.executable,str(SUP/'scripts/wiki/build-wiki-index.py')],check=True)
