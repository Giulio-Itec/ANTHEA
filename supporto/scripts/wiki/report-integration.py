"""Write the reviewable integration record and its PDF companion."""
from pathlib import Path
from collections import Counter
import json, importlib.util
from pypdf import PdfReader

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'supporto/artefatti/wiki-integrazione'
articles = json.loads((ROOT/'X.Desktop/Wiki/index.json').read_text(encoding='utf-8'))
inventory = json.loads((OUT/'inventario-integrazione.json').read_text(encoding='utf-8'))
aliases = json.loads((ROOT/'X.Desktop/Wiki/aliases.json').read_text(encoding='utf-8'))
states = Counter(a['status'] for a in articles)
actions = Counter(a['action'] for a in inventory)
pages={k:len(PdfReader(ROOT/f'supporto/documentazione/Guide_ANTHEA/ANTHEA_Guida_{k}_ITEC_Rev15.pdf').pages) for k in ['pratica','teorica']}
wiki_result=(OUT/'ui-revisionata/completato.txt').read_text(encoding='utf-8')
text = f'''# Integrazione ANTHEA Engineering Handbook — Rev15

4 ottobre 2026

## Esito e perimetro

Le 85 voci iniziali sono mappate su {len(articles)} articoli correnti in 12 capitoli. I {len(aliases)} alias risolvono direttamente vecchi identificativi, percorsi e ancore; le sezioni storiche ritirate rinviano alla pagina corrente pertinente. Non sono mantenute pagine duplicate per conservare un indirizzo.

L'integrazione editoriale comprende gestione dei progetti, materiali, pali e micropali, sezione composta, Bridge Design, muri e fondazioni, sezione in c.a., modellazione e percorsi operativi. I resoconti di sviluppo sono conservati nell'archivio; procedure, limiti ed esempi ancora utili sono stati recuperati nelle pagine pertinenti. Acciaio per armature e profili del calcestruzzo hanno ora pagine autonome nel catalogo corrente.

Correzioni specifiche: conversione MPa/kPa; importazione Excel a otto colonne con torsione e risultati delle formule già memorizzati; distinzione fra deformazione e tensione iniziale nella lettura dell'asse neutro; geometrie con fori e campi ammessi; diametro geotecnico e diametro del tubo CHS; istruzioni di avvio; flusso attuale di Bridge Design; esempio stratificato dei muri, compresi gli esiti non soddisfatti. La Wiki distingue implementazione, modello e attribuzione normativa.

## Tracciabilità delle azioni

L'inventario-integrazione.json registra tutte le 85 origini, destinazione, azione, stato, impronta SHA256 del contenuto precedente e file di codice esaminabili.

| Azione | Voci di origine |
|---|---:|
'''
text += '\n'.join(f'| {action} | {count} |' for action,count in sorted(actions.items()))
text += f'''

## Verifiche eseguite

- Build Desktop: completata, zero errori e zero avvisi.
- Wiki nativa: {wiki_result.strip()}, inclusi risoluzione dei rimandi, collegamenti ai moduli, ricerca, formule e rendering delle pagine.
- Controlli Python Handbook: sette gruppi superati, inclusi casi negativi per alias e metadati, coerenza degli stati con il registro dei riscontri e controlli numerici indipendenti degli esempi selezionati.
- ConcreteCode.Checks: 434 controlli esistenti superati. Sono controlli dell'implementazione; non certificano da soli le attribuzioni agli annessi nazionali.
- Bridge Design: 80.133 confronti numerici superati su 100 travi e una griglia di 26 alternative. Verificati equazioni ideali e ottimo discreto in una famiglia prescritta; non è una validazione normativa o sperimentale di tutte le famiglie.
- Sezione composta: 172 controlli generali, 2.957 sui metodi e curve, 189 sugli esempi delle guide e 191 sulle predalle. I benchmark storici sono stati riallineati al campo Altezza totale H; risultati attesi e tolleranze non sono stati adattati al risultato del programma.
- Broms: 1.080 controlli; estensione stratificata: 1.645; controlli CHS superati. Palo elastico: 101 controlli; palificata: 94; muri: 345; libreria autonoma: 73.
- Confronto con dati attesi salvati: 464 casi e 942.254 valori, nessun caso fallito. Questo confronto di regressione resta distinto da una verifica indipendente.
- Interfaccia: prove progetti, workspace e revisioni, gerarchia, condivisione, report e materiali superate. Aggiornati il selettore della scheda Taglio e i dati di preparazione di due test obsoleti; nessuna modifica ai motori per far passare queste prove.
- Verifica visiva: pagine native nei due temi e a larghezza ridotta; tabelle corrette per andare a capo; rendering di {sum(pages.values())} pagine PDF, controllo delle tavole di insieme e campioni leggibili di formule, tabelle, immagini e indici.
- Word finale: confronto dei frammenti testuali e della struttura delle formule modificabili; 26 parti del modello ITEC identiche per ciascun volume. PDF accanto ai Markdown identici alle edizioni PDF ITEC. I conteggi dettagliati sono negli audit JSON dei volumi.

Le evidenze sono in ui-revisionata/, pdf/, bridge-independent/, revisione-operativa/ e nelle cartelle guide_anthea_itec_rev15. I controlli numerici hanno il campo dichiarato dai rispettivi test.

## Documenti e archivio

Aggiornate le sole due guide globali Markdown, le edizioni Word e PDF Rev15, l'indice delle guide e i README di supporto/installazione. Guida pratica: {pages['pratica']} pagine; guida teorica: {pages['teorica']} pagine, comprese copertine e indici.

Archivio: supporto/SUPERATI/wiki-integrazione-rev15-20261004. registro.json conserva origine, impronta, motivo e revisione sostitutiva; mappa-destinazioni.json conserva la mappatura dei contenuti. La struttura relativa originale è mantenuta. Modelli ed evidenze di calcolo utilizzati non sono stati spostati.

## Stato editoriale e riscontri ancora aperti

Distribuzione del catalogo: {states.get('reviewed',0)} articoli revisionati e {states.get('qualified',0)} con riscontri sulle fonti da completare. Il registro supporto/docs/wiki-riscontri.json descrive campo, evidenze e punti aperti della revisione aggiuntiva delle 39 pagine precedentemente integrate. Revisionato riguarda il contenuto e il campo dichiarato: non certifica la conformità normativa dell'intero programma o del progetto dell'utente.

Profili di calcolo del calcestruzzo: restano da riscontrare puntualmente edizione applicabile, clausole e coefficienti degli annessi DIN, danese e norvegese mediante i testi primari pertinenti. Il comportamento implementato e i limiti geometrici sono descritti; l'attribuzione nazionale non viene presentata come certificata. La pagina esplicita inoltre i modelli non coperti.

Efficienza orizzontale della palificata: la tabella attribuita a Davisson deriva dalla specifica fornita dall'utente e resta da riscontrare nel lavoro originale; va completato anche il riscontro dei testi originali Rollins/FEMA. Formule ed esempi implementati sono stati controllati, ma i test non provano queste attribuzioni bibliografiche.

Le due voci rimangono esplicitamente contrassegnate nella Wiki. Le limitazioni già dichiarate nelle altre pagine restano valide: fra queste l'edizione non identificabile della scansione Viggiani, il campo empirico delle correlazioni, l'assenza di confronto MAX per le nuove funzioni dei muri e la distinzione fra predimensionamento e progetto completo.
'''
path = OUT/'integrazione-handbook.md'
path.write_text(text+'\n', encoding='utf-8')
spec = importlib.util.spec_from_file_location('pdf',ROOT/'supporto/scripts/documentazione/markdown-pdf.py')
pdf = importlib.util.module_from_spec(spec);spec.loader.exec_module(pdf)
pdf.styles['CellGuide'].fontSize = 8.7
pdf.styles['CellGuide'].leading = 10.5
pdf.build(path,path.with_suffix('.pdf'))
print(json.dumps({'articles':len(articles),'aliases':len(aliases),'states':states,'actions':actions},ensure_ascii=False))
