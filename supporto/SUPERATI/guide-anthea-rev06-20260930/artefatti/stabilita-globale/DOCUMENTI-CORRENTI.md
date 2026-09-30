# Documenti ed esempi correnti della stabilità globale

Aggiornamento al 30 settembre 2026. I test MAX restano sospesi. Questa raccolta distingue le evidenze del confronto esterno dagli esempi e dalle regressioni interne ANTHEA. Il percorso guidato e i suoi controlli sono documentati nell’ultima sezione; nessuna nuova prova MAX è stata eseguita.

## Sommario dei confronti MAX

Il documento `confronti-max/SOMMARIO-PARZIALE.pdf`, con sorgente Markdown omonimo, contiene i risultati ottenuti finora, gli scostamenti sullo stesso cerchio, il confronto sugli stessi conci e le correzioni ai sorgenti. Due confronti sono completati; il terzo è calcolato ma ancora da salvare con relazione. Sette casi sono predisposti e non ancora confrontati.

I modelli e le evidenze autentiche MAX rimangono in `confronti-max`. Le relazioni ANTHEA precedenti alla correzione sono state spostate in `supporto/SUPERATI`; gli input e i risultati numerici originali sono conservati per la tracciabilità. La correzione era inizialmente nei soli sorgenti; è inclusa nella pubblicazione del 30 settembre 2026.

## Esempi interni aggiornati

La raccolta successiva alla correzione dei cerchi tangenti è `regressione-tangenti-max/esempi`; per le revisioni seguenti vedere gli aggiornamenti del 30 settembre riportati sotto. Per ogni caso sono presenti modello ANTHEA, risultati JSON, tabella dei conci CSV, relazione Word e PDF, istruzioni Markdown e PDF. Il calcolo interno ha superato 55 controlli numerici e di archivio. Questi esempi sono diversi dalla serie dei dieci confronti MAX.

| Cartella del caso | Descrizione |
|---|---|
| 01-mensola-base | Mensola di 3 m con terreno granulare e sovraccarico |
| 02-mensola-alta | Mensola di 5 m |
| 03-gravita | Muro a gravità |
| 04-fondazione-debole | Strato di fondazione con attrito ridotto |
| 05-coesione-profonda | Coesione nello strato profondo |
| 06-tre-strati | Stratigrafia con tre terreni |
| 07-falda | Presenza di falda |
| 08-sisma | Azione sismica |
| 09-urto | Azione eccezionale di urto |
| 10-non-drenato | Condizione non drenata con convergenza non soddisfatta |

In ogni cartella aprire `riproduzione.pdf` per le istruzioni e `relazione-anthea.pdf` per gli input e i risultati completi. Il modello `modello.anthea` è conservato nella stessa cartella. Gli esiti non soddisfatti o incompleti rimangono espliciti.

## Guide del modulo e guide generali

La documentazione del modulo è `supporto/docs/muri-sostegno.md`, affiancata dal PDF omonimo. Le guide generali correnti, in Word e PDF, sono `ANTHEA_Guida_pratica_ITEC_Rev05` e `ANTHEA_Guida_teorica_ITEC_Rev05`, sotto `supporto/documentazione/Guide_ANTHEA`.

## Revisioni precedenti

Le guide precedenti, le vecchie raccolte di esempi e i relativi PDF sono conservati in `supporto/SUPERATI`, mantenendo la struttura originale. Il registro degli spostamenti descrive i motivi e conserva l'impronta dei file. Nessun documento è stato eliminato definitivamente.

## Aggiornamento del 30 settembre 2026

I test della revisione con terreno di monte e valle sono in `supporto/artefatti/muri-due-colonne-20260930/rilascio`: regressione del muro, regressione Bishop e test UI. Il rapporto `CONTROLLO.md` nella cartella superiore, anche in PDF, identifica i risultati e i due nuovi esempi con due colonne. I dieci casi Bishop sono stati rigenerati in `rilascio/globale/esempi` con relazioni Word e PDF aggiornate. La serie `regressione-tangenti-max` resta evidenza riproducibile per i confronti MAX precedenti; non è stata riscritta o spostata.


## Percorso operativo aggiornato del 30 settembre 2026

Per usare la verifica consultare `supporto/docs/stabilita-globale-guida-rapida.md`, con PDF illustrato e un esempio stratificato salvato. Il pulsante Stabilità globale precompila il primo modello, mostra gli strati sotto il muro e guida al calcolo. Le proprietà profonde restano dati del sito da controllare. La guida completa `supporto/docs/muri-sostegno.md` e PDF è aggiornata allo stesso percorso.

Il rapporto `supporto/artefatti/globale-guidata-20260930/CONTROLLO.md` e PDF documenta 213 controlli del muro, 61 globali e 30 del percorso senza finestre sul desktop. I nuovi output sono regressioni interne ANTHEA: nessun nuovo caso MAX è stato eseguito. La raccolta documentata delle due colonne resta disponibile; gli output di questa regressione non sono un nuovo pacchetto di dieci confronti esterni.
