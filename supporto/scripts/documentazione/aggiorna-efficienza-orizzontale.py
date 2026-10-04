"""Integrate the lateral pile group module in the two canonical guides and rebuild Wiki/Word sources."""
from pathlib import Path
import hashlib, importlib.util, json, re, shutil, subprocess, sys

ROOT = Path(__file__).resolve().parents[3]
SUP = ROOT / 'supporto'
ARCH = SUP / 'SUPERATI/efficienza-orizzontale-rev11-20261002'
registry = []
paths = [SUP / f'docs/guida-{kind}-anthea.{ext}' for kind in ('pratica', 'teorica') for ext in ('md', 'pdf')]
paths += list((SUP / 'documentazione/Guide_ANTHEA').glob('*Rev10.*'))
paths += [SUP / 'installer/Indice-guide.md', SUP / 'installer/Indice-guide.pdf']
for path in paths:
    if not path.exists(): continue
    dest = ARCH / path.relative_to(SUP)
    dest.parent.mkdir(parents=True, exist_ok=True)
    if not dest.exists(): shutil.copy2(path, dest)
    registry.append(dict(origine=str(path.relative_to(ROOT)), archivio=str(dest.relative_to(ROOT)), motivo='Sostituita dalla revisione 11 con efficienza orizzontale delle palificate', sostituzione=str(path.relative_to(ROOT)).replace('Rev10', 'Rev11'), sha256=hashlib.sha256(dest.read_bytes()).hexdigest()))
(ARCH / 'registro.json').write_text(json.dumps(registry, ensure_ascii=False, indent=2), encoding='utf-8')

pratica = r'''
## Efficienza orizzontale della palificata

Il modulo Palificata orizzontale confronta sei modelli di effetto di gruppo a partire da un'unica geometria di pali identici e da una direzione dell'azione orizzontale. Il risultato è un fattore di riduzione: per Davisson riguarda il modulo di reazione kh o nh; per gli altri metodi è il p-multiplier medio. La scheda non esegue un'analisi laterale completa e non fornisce direttamente la resistenza della palificata.

### Apertura e geometria

Aprire Moduli singoli → Geotecnica → Palificata orizzontale, oppure File → Nuova palificata orizzontale. La scheda può essere inserita anche in un progetto. Il motore numerico è GPCChecker.Geotechnics; ANTHEA gestisce input, risultati, rappresentazione e archivio.

Assegnare il diametro comune D in metri e la direzione H in gradi antiorari da +X. Il cursore ruota H a passi di 15 gradi e ricalcola; il campo numerico consente qualsiasi angolo. Genera griglia sostituisce le coordinate con Nx per Ny pali agli interassi Sx e Sy, in metri. La tabella permette di modificare identificativo, x e y, aggiungere pali nella riga vuota ed eliminarli con Canc. Sono ammessi da 1 a 500 pali, identificativi univoci e distanze fra centri almeno pari a D.

La pianta mostra pali, coefficienti del metodo selezionato, direzione H, assi parallelo e ortogonale, diametro e interassi disponibili. La fila 1 è quella con maggiore proiezione lungo il verso del carico. Invertendo H si scambiano file anteriori e posteriori. La geometria non viene ruotata quando cambia soltanto il carico.

### Metodo e applicabilità

| Metodo | Risultato | Impiego nella scheda |
| --- | --- | --- |
| Davisson 1970 | Rg di kh/nh | Griglia allineata; interasse parallelo almeno 3D, trasversale almeno 2.5D |
| AASHTO 2014 | p-multiplier per fila | Interpolazione 3D–5D; oltre 5D mantenimento del valore di 5D segnalato |
| FHWA 2018 | p-multiplier per fila | Interpolazione 3D–6D; oltre 6D coefficienti unitari |
| Rollins e FEMA | p-multiplier per fila | Formule logaritmiche; campo sperimentale circa 3.3D–5.65D |
| Reese e Van Impe | p-multiplier palo per palo | Tutte le coppie, anche per geometrie irregolari e H obliqua |
| Caltrans 2025 | p-multiplier modificato | Interazione palo-palo e correzione alfa per fila |

Il riconoscimento automatico accetta una griglia cartesiana completa e uniforme nel riferimento del carico, anche se ruotata rispetto agli assi globali. Le file vengono distinte con tolleranza D per 10 alla meno 6. Per geometrie irregolari o H obliqua, i metodi per file non vengono applicati automaticamente. Aprire Applicabilità e interassi rappresentativi e, solo dopo una valutazione ingegneristica, accettare l'estensione per file proiettate e assegnare S parallelo e S trasversale. Per Caltrans, accettata l'estensione, la media degli interassi fra file proiettate è disponibile automaticamente; si può assegnare un interasse rappresentativo diverso. L'estensione resta identificata negli avvisi.

I due interassi rappresentativi vuoti mantengono il comportamento automatico. Non sono dimensioni che modificano la pianta: cambiano soltanto la schematizzazione per file. Reese usa sempre le distanze reali fra tutti i pali. Il riepilogo elenca anche gli intervalli geometrici proiettati, per rendere verificabile la scelta.

Consenti estrapolazioni è disattivato inizialmente. Abilitarlo permette di ottenere un risultato fuori dai limiti inferiori delle tabelle o dal campo sperimentale di Rollins, con avviso esplicito e coefficienti limitati a zero–uno. Non supera la condizione trasversale di Davisson: sotto 2.5D il metodo resta non disponibile, perché manca una correzione documentata. Per un solo palo tutti i fattori valgono uno. Per un'unica fila trasversale AASHTO, FHWA e Rollins richiedono un interasse parallelo rappresentativo; preferire Reese quando manca una schematizzazione per file giustificabile.

### Risultati e confronto

Premere Calcola e confronta i sei metodi. Il riepilogo mostra Rg oppure Pm medio. Davisson mostra anche la riduzione percentuale del modulo di reazione e il grafico Rg contro S parallelo su D. Il punto corrente compare nel tratto tabellato 3D–8D. Un trattino significa grandezza non definita, non valore nullo.

La tabella di confronto indica separatamente grandezza, valore e indisponibilità. Selezionare ciascun metodo per leggere fonte, avvisi e dettaglio dei pali. Il dettaglio riporta fila, coordinate proiettate, beta, alfa e coefficiente finale. Alfa riguarda la modifica Caltrans; per gli altri p-multiplier vale uno. I coefficienti di Davisson non devono essere letti come p-multiplier.

Esempio di controllo: griglia 3 per 3, D = 1 m, Sx = Sy = 3 m, H = 0 gradi. Davisson restituisce Rg = 0.25; AASHTO restituisce Pm medio = 0.50; FHWA restituisce circa 0.5167. Rollins a 3D richiede l'abilitazione esplicita dell'estrapolazione e restituisce circa 0.5887. Questi valori non sono quattro stime equivalenti della capacità del gruppo.

Le modifiche invalidano i risultati precedenti; premere Calcola prima di esportare. Salva conserva coordinate, opzioni, metodo e interassi rappresentativi nel normale archivio ANTHEA. Risultati JSON esporta il confronto completo. Report Word include geometria, risultati per metodo e per palo, fonti e limiti; può essere convertito in PDF con il normale flusso documentale. Le due guide globali sono distribuite anche in PDF.

'''
teorica = r'''
## Efficienza orizzontale della palificata

Il modulo calcola fattori di riduzione per pali verticali identici sottoposti ad azione orizzontale con direzione comune. Non risolve l'equilibrio del plinto, la risposta non lineare p-y, la compatibilità degli spostamenti o la capacità laterale ultima. Sono esclusi diametri differenti, pali inclinati e conversioni empiriche automatiche fra rigidezza e capacità.

### Grandezze e geometria

L'efficienza di capacità sarebbe definita come segue.

$$\eta_H=\frac{H_{gruppo}}{N H_{singolo}}$$

La scheda riporta invece la media dei p-multiplier come indicatore di riduzione per pali identici.

$$\overline{P_m}=\frac{1}{N}\sum_{i=1}^{N}P_{m,i}$$

L'identificazione fra questa media ed efficienza di capacità richiede ipotesi sulla risposta dei singoli pali e sulla ripartizione del carico; non è effettuata come verifica di resistenza. Per Davisson il fattore Rg ha una grandezza fisica diversa.

$$u=(\cos\theta,\sin\theta),\qquad v=(-\sin\theta,\cos\theta)$$

$$q_i=r_i\cdot u,\qquad t_i=r_i\cdot v$$

La fila 1 corrisponde al massimo q, quindi al lato avanzato nel verso H. Le coordinate sono proiettate dopo una traslazione dell'origine per migliorare la stabilità numerica. La tolleranza di raggruppamento delle file è D per 10 alla meno 6. Gli interassi automatici di Davisson, AASHTO, FHWA e Rollins richiedono un reticolo completo con interassi uniformi nei due assi del carico. Per un reticolo obliquo o irregolare non esiste un interasse equivalente automatico giustificato da queste formule: il programma richiede accettazione esplicita della schematizzazione proiettata e interassi rappresentativi assegnati.

### Davisson

$$R_g=\frac{k_{h,g}}{k_{h,1}}=\frac{n_{h,g}}{n_{h,1}}$$

$$k_{h,g}=R_g k_{h,1},\qquad n_{h,g}=R_g n_{h,1}$$

| S parallelo su D | Rg |
| --- | --- |
| 3 | 0.25 |
| 4 | 0.40 |
| 5 | 0.55 |
| 6 | 0.70 |
| 7 | 0.85 |
| 8 e oltre | 1.00 |

$$R_g=0.15\frac{S_{\parallel}}{D}-0.20\qquad 3\leq\frac{S_{\parallel}}D\leq8$$

La riduzione del modulo è cento volte uno meno Rg, in percentuale. Sotto 3D il calcolo è indisponibile per default; l'opzione di estrapolazione usa la stessa retta, con limite zero–uno e avviso. L'interazione trasversale può essere trascurata nella schematizzazione citata per S trasversale almeno 2.5D. Sotto tale soglia il calcolo rimane non disponibile: non viene inventato un secondo fattore da moltiplicare. In assenza di file successive si assume assenza di riduzione longitudinale, sempre subordinata alla condizione trasversale.

Rg non coincide automaticamente con eta H. Una futura integrazione con il solutore del palo singolo dovrà ricalcolare il comportamento usando kh o nh ridotti, a parità di EI, lunghezza, vincolo, stratigrafia e criterio di capacità o spostamento. Solo il rapporto fra le capacità ricalcolate potrà essere chiamato efficienza di capacità. Nessuna potenza o conversione empirica di Rg viene utilizzata.

### AASHTO e FHWA

Le due tabelle sono mantenute come metodi distinti e con edizione esplicita. S è l'interasse delle file nella direzione del carico.

| Metodo | S su D | Fila 1 | Fila 2 | Fila 3 e successive |
| --- | --- | --- | --- | --- |
| AASHTO 2014 | 3 | 0.80 | 0.40 | 0.30 |
| AASHTO 2014 | 5 | 1.00 | 0.85 | 0.70 |
| FHWA 2018 | 3 | 0.70 | 0.50 | 0.35 |
| FHWA 2018 | 4 | 0.85 | 0.65 | 0.50 |
| FHWA 2018 | 5 | 1.00 | 0.85 | 0.70 |
| FHWA 2018 | 6 e oltre | 1.00 | 1.00 | 1.00 |

Interpolazione lineare fra nodi. Per AASHTO oltre 5D il software conserva i valori dell'ultimo nodo, con avviso: è una scelta conservativa dichiarata del modulo, non un'estrapolazione normativa fino a uno. Per FHWA da 6D i valori sono unitari. Sotto 3D il calcolo richiede l'opzione esplicita di estrapolazione. Non viene applicata una legge aggiuntiva di interazione trasversale: la sua assenza dalla tabella non prova l'assenza fisica dell'effetto.

### Rollins e FEMA

Ponendo lambda uguale a S su D, il modulo usa logaritmi naturali.

$$P_{m,1}=\min(1,0.26\ln\lambda+0.50)$$

$$P_{m,2}=\min(1,0.52\ln\lambda)$$

$$P_{m,\geq3}=\min(1,0.60\ln\lambda-0.25)$$

Le prove di riferimento riguardano principalmente argilla rigida e interassi circa 3.3D–5.65D. Il programma richiede estrapolazione esplicita al di fuori di tale intervallo e impone anche un limite inferiore nullo. A 3D i tre valori sono 0.785639, 0.571278 e 0.409167; la media su tre file di pari numero di pali è 0.588695. L'indicazione del campo sperimentale non sostituisce la verifica della pertinenza geotecnica.

### Reese e Van Impe

Si considerano tutte le coppie distinte, senza limitarsi ai vicini. La distanza s è quella reale fra i centri, anche per coppie oblique. Per ciascuna coppia, il fattore affiancato è il seguente.

$$\beta_a=0.64(s/D)^{0.34}\qquad 1\leq s/D<3.75$$

Da 3.75D il fattore affiancato è uno. Per l'allineamento nel verso del carico si distinguono palo avanzato e arretrato.

$$\beta_{bl}=0.70(s/D)^{0.26}\qquad 1\leq s/D<4$$

$$\beta_{bt}=0.48(s/D)^{0.38}\qquad 1\leq s/D<7$$

Da 4D il fattore leading è uno; da 7D quello trailing è uno. I rami sono limitati a uno anche immediatamente prima delle soglie, per evitare lievi superamenti dovuti ai coefficienti empirici arrotondati. L'angolo phi è fra congiungente dei pali e carico; leading e trailing dipendono dal segno della differenza di q.

$$\beta_{ji}=\sqrt{\beta_b^2\cos^2\phi+\beta_a^2\sin^2\phi}$$

$$\beta_i=\prod_{j\ne i}\beta_{ji},\qquad P_{m,i}=\beta_i$$

L'interazione è direzionale: beta ji non coincide in generale con beta ij. Geometrie irregolari e carichi obliqui sono trattati direttamente. I pali sovrapposti sono rifiutati. Un solo palo ha prodotto vuoto unitario. La media finale pesa ciascun palo una volta, quindi rispetta anche file con popolazioni diverse.

### Caltrans Modified Reese

Il metodo applica alle interazioni precedenti il coefficiente alfa delle California Amendments di settembre 2025, riferite all'ottava edizione AASHTO LRFD. La fonte attribuisce la base di interazione a Reese et al. 2006. Non è una prescrizione delle NTC italiane.

$$P_{m,i}=\alpha_i\beta_i$$

| Interasse medio fra file su D | Fila 1 | Fila 2 | Fila 3 | Oltre fila 3 |
| --- | --- | --- | --- | --- |
| 2 | 1.0 | 1.0 | 1.0 | 1.0 |
| 3 | 0.9 | 1.0 | 0.8 | 0.8 |
| 5 | 1.0 | 1.0 | 0.8 | 0.9 |
| 7 | 1.0 | 1.0 | 0.9 | 1.0 |
| 8 | 1.0 | 1.0 | 1.0 | 1.0 |

L'interasse è misurato nella direzione del carico; per interassi fra file non uniformi la fonte richiede la media aritmetica degli intervalli fra file. Non si usa la media delle distanze fra tutte le coppie. Si interpola linearmente. Oltre 8D il modulo mantiene alfa unitario; per una sola fila adotta alfa unitario, dichiarandolo. Sotto 2D occorre abilitare l'estrapolazione; alfa è limitato a zero–uno. Per disposizioni oblique o irregolari occorre accettare esplicitamente l'estensione per file proiettate prima di applicare la correzione per fila.

### Fonti e verifica

Il testo allegato dall'utente è la specifica delle formule e delle tabelle implementate. Il collegamento condiviso ChatGPT non era recuperabile durante l'implementazione. Le fonti ufficiali consultabili sono indicate per consentire il controllo dell'edizione: l'applicabilità resta distinta dal semplice corretto calcolo numerico.

- Davisson M T, 1970, Lateral Load Capacity of Piles, Highway Research Record 333, pagine 104–112. La tabella di riduzione è quella ripresa nella specifica allegata; non si afferma una verifica diretta della sua presenza nel lavoro originale.
- Davisson M T e Salley J R, 1970, Model Study of Laterally Loaded Piles, ASCE, volume 96, numero 5, pagine 1605–1627.
- [FHWA GEC 12 volume I, 2016, Design and Construction of Driven Pile Foundations](https://www.fhwa.dot.gov/engineering/geotech/pubs/gec12/nhi16009_v1.pdf): valori AASHTO 2014.
- [FHWA GEC 10, 2018, Drilled Shafts Construction Procedures and Design Methods](https://www.fhwa.dot.gov/engineering/geotech/nhi18024.pdf): tabella 10-41 dei p-multiplier.
- Rollins et al., 2006; FEMA P-751, 2012: relazioni logaritmiche nella specifica allegata.
- [California Amendments settembre 2025](https://dot.ca.gov/-/media/dot-media/programs/engineering/documents/caamendments/202509-aashto-lrfd-ca-amendments-a11y.pdf), paragrafo 10.7.2.4, equazioni 1–6 e tabella 10.7.2.4-2: interazioni palo-palo e modifica Caltrans.

La validazione automatica verifica nodi e interpolazioni, esempi numerici, leading e trailing, soglie, palo isolato, geometrie irregolari, simmetria per inversione del carico, invarianza per traslazione e rotazione congiunta di geometria e carico, archiviazione e rappresentazione WPF. I test sono conservati in supporto/test/HorizontalPileGroup.Checks e le evidenze in supporto/artefatti/efficienza-orizzontale.

'''
for kind, chapter in [('pratica', pratica), ('teorica', teorica)]:
    path = SUP / f'docs/guida-{kind}-anthea.md'
    text = path.read_text(encoding='utf-8-sig').replace('revisione documentale 10', 'revisione documentale 11')
    if '## Efficienza orizzontale della palificata' not in text:
        at = text.find('## Approfondimenti integrati')
        if at < 0: at = len(text)
        text = text[:at] + chapter + text[at:]
    text = re.sub(r'^\$\$(.+)\$\$$', lambda m: '$$ ' + m[1], text, flags=re.M)
    text = text.replace(r'\qquad 3\leq', r'\quad\text{per }3\leq').replace(r'\qquad 1\leq', r'\quad\text{per }1\leq')
    path.write_text(text, encoding='utf-8')
editorial_path = ROOT / 'X.Desktop/Wiki/editorial.json'
editorial = json.loads(editorial_path.read_text(encoding='utf-8'))
for prefix, area in [('guide', 'Moduli singoli'), ('theory', 'Fondazioni e geotecnica')]:
    editorial[prefix + ':Efficienza orizzontale della palificata'] = dict(area=area, id=f'/wiki/{prefix}/palificata-orizzontale', modules=['geo_efficienza_orizzontale'], related=[f'/wiki/{"theory" if prefix == "guide" else "guide"}/palificata-orizzontale'], keywords=['Davisson', 'AASHTO', 'FHWA', 'Rollins', 'Reese', 'Caltrans', 'p-multiplier'])
editorial_path.write_text(json.dumps(editorial, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
builder_path = SUP / 'scripts/Build-AntheaGuides-Itec.py'
text = builder_path.read_text(encoding='utf-8').replace("REVISION = '10'", "REVISION = '11'").replace("REVISION_NOTE = 'FORMULE LATEX E REVISIONE EDITORIALE WIKI'", "REVISION_NOTE = 'EFFICIENZA ORIZZONTALE DELLE PALIFICATE'")
builder_path.write_text(text, encoding='utf-8')
spec = importlib.util.spec_from_file_location('builder', builder_path); builder = importlib.util.module_from_spec(spec); spec.loader.exec_module(builder)
builder.ART.mkdir(parents=True, exist_ok=True)
(builder.ART/'artifact.md').write_text('Aggiornamento delle due guide globali al modulo efficienza orizzontale. Conservare il modello ITEC e verificare PDF e indici. Revisione 11.\n', encoding='utf-8')
for kind in builder.GUIDES: builder.build(kind)
index = SUP / 'installer/Indice-guide.md'
previous_index = index.read_text(encoding='utf-8')
text = previous_index.replace('Revisione 08', 'Revisione 11').replace('Revisione 10', 'Revisione 11')
if '## Efficienza orizzontale delle palificate' not in text:
    text += '\n## Efficienza orizzontale delle palificate\n\nEntrambi i volumi includono il modulo Palificata orizzontale: Davisson, AASHTO, FHWA, Rollins, Reese e Van Impe, Caltrans. Il volume pratico descrive la scheda e quello teorico formule, campi e fonti. Edizione documentale Rev11.\n'
index.write_text(text, encoding='utf-8')
if previous_index != text or not index.with_suffix('.pdf').exists():
    spec = importlib.util.spec_from_file_location('pdf', SUP/'scripts/documentazione/markdown-pdf.py'); pdf = importlib.util.module_from_spec(spec); spec.loader.exec_module(pdf); pdf.build(index)
subprocess.run([sys.executable, str(SUP/'scripts/wiki/build-wiki-index.py')], check=True)
