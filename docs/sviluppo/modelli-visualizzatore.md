# Modelli di calcolo: bozza integrata

Stato: bozza di sviluppo del 9 ottobre 2026, branch `codex/model-viewer-draft`, base ANTHEA `781b6a9`.
Autorizzazione: creare il visualizzatore in un ramo separato; WPF con MVVM; non modificare le altre librerie o i loro progetti.
Questo documento descrive l'implementazione e le attività future, non è una guida di un modulo rilasciato.

## Organizzazione

Il progetto organizza i contenuti. Ogni contenitore della gerarchia (progetto, fase, sottofase) può avere zero o più
modelli di calcolo e zero o più fogli. I modelli sono risorse autonome: non entrano nel catalogo dei fogli di calcolo.
La voce **Modelli** raccoglie quelli dell'archivio aperto; l'albero del progetto li mostra nella sezione di appartenenza.
La Wiki resta trasversale.

Un foglio può riferirsi a un modello della propria sezione o di un suo antenato. Il riferimento salva identità del
modello, impronta della revisione, elemento, caso e componente. Sostituire un modello conserva la sua identità ma
invalida i riferimenti alla vecchia impronta. Rinominare non invalida il riferimento. Duplicare una sezione assegna
identità nuove ai modelli copiati e aggiorna i riferimenti interni alla copia. Spostare un foglio fuori dal ramo del
modello rende visibile il riferimento non risolto.

**Collegare un riferimento non trasferisce carichi e non esegue verifiche strutturali.** I dati numerici dei fogli
esistenti restano invariati. L'importazione effettua controlli di formato, unità, connettività, proprietà e associazione
dei risultati; non sostituisce la verifica ingegneristica del modello.

## Confini dell'implementazione

| Progetto | Responsabilità | Dipendenze principali |
| --- | --- | --- |
| `src/ANTHEA.ModelWorkspace` | Copia normalizzata, controlli di importazione, adattatore offline, archivio dei modelli e riferimenti ai fogli | .NET, nessuna WPF e nessuna nuova formula |
| `src/ANTHEA.ModelViewer.Presentation` | ViewModel, selezioni, comandi asincroni, stati occupato/errore/sola lettura, interrogazione dei dati | ModelWorkspace, CommunityToolkit.Mvvm 8.3.2; nessuna WPF |
| `src/ANTHEA.ModelViewer.Wpf` | XAML con binding, dialoghi tramite servizi, ciclo di vita WPF, renderer Helix, geometria grafica | Presentation, HelixToolkit.Wpf.SharpDX 3.1.2 |
| `X.Desktop/Wpf/ProjectModels.cs` | Collegamento del nuovo modulo alla navigazione e al documento aperto | API dei tre nuovi progetti; nessun calcolo |

Il code-behind della vista collega il ciclo Loaded/Unloaded e le notifiche di scena al renderer. I comandi non aprono
dialoghi direttamente: ricevono `IModelViewerServices`. Il ViewModel si prova senza GPU e senza WPF. La chiusura annulla
l'importazione pendente, impedisce una scrittura tardiva e libera il gestore delle risorse grafiche. Riaprire crea una
vista nuova. L'importazione legge fuori dal thread WPF; l'aggancio al documento avviene al ritorno nel thread UI.

Nessun cambiamento ai sorgenti di X.Core, X.Calculations, X.Materiali, ai repository GPC o allo snapshot `lib/Checker`.
Non si anticipa il refactoring dei moduli esistenti. Gli agganci in X.Desktop sono limitati a navigazione, albero,
apertura della vista, avviso del riferimento sul foglio e nuove identità nella duplicazione di una sezione.

## Cosa si può provare

1. Aprire/creare un archivio progetti e selezionare la fase o sottofase.
2. Usare **+ Importa un modello** nella sezione oppure **Modelli → + Modello**.
3. Importare un pacchetto `.antheamodel`, oppure il `NODE.json` della copia del laboratorio nella stessa cartella
   delle tabelle UNIT, ELEM, THIK, SECT e, facoltativamente, plate-0-nodes.
4. Consultare geometria, volumi e risultati; scegliere caso e componente; ruotare, spostare, inquadrare ed esportare PNG.
5. Interrogare un elemento per numero e collegare un riferimento a un foglio della sezione o delle sottosezioni.
6. Salvare il normale archivio `.programma`: la copia del modello è contenuta nell'archivio.

La bozza accetta piastre triangolari/quadrilatere e aste a due nodi. Per le aste, il volume è una preview delle sezioni
rettangolari piene centrate. I risultati implementati sono le otto componenti nodali di piastra Mxx, Myy, Mxy, Fxx, Fyy,
Fxy, Vxx, Vyy. Nessun risultato mancante viene inventato: l'elemento incompleto è grigio.

Unità della copia: metri, kN/m, kN·m/m. L'adattatore del laboratorio richiede risultati in N e mm e converte i momenti
per unità di larghezza. Non fa richieste al servizio di origine e non conserva credenziali. Un formato non riconosciuto
viene rifiutato senza sostituire il modello già aperto.

Contouring: valori nodali originali distinti per elemento, interpolazione bilineare sui quadrilateri e lineare sui
triangoli, tre livelli di suddivisione grafica (1, 4, 8), scala colori continua e isolinee. Non si mediano valori fra
elementi. La suddivisione grafica non aggiunge accuratezza ai risultati del solutore. Gli estremi di un inviluppo
importato sono dichiarati non simultanei. Non viene calcolato alcun nuovo inviluppo.

Volumi: spessore e offset delle piastre lungo la normale locale; le facce interne coincidenti fra piastre coplanari
della stessa proprietà vengono omesse. La mesh analitica e i dati importati restano invariati. Per coordinate grandi
si sottrae l'origine locale prima della conversione ai float del renderer. Rotazioni e offset delle aste richiedono
ancora una corrispondenza completa con le convenzioni del modello di origine.

## Evidenze e controlli

- `tests/ModelWorkspace.Checks`: importazione e rifiuti, continuità/discontinuità dei dati, più modelli e fogli,
  archivio reale, revisioni, duplicazione, impronta, riferimenti obsoleti, comandi e annullamento del ViewModel.
- `tests/ModelViewer.UiChecks`: apertura nella vera finestra ANTHEA, tre viste, qualità massima, binding, immagini
  prodotte dal renderer, rilascio delle risorse e riapertura.
- Registrate nel runner unico: ModelWorkspace nello stadio fast, ModelViewer.UiChecks nello stadio ui.
- Dati privati, demo e immagini soltanto in `supporto/artefatti/model-viewer/`, non versionati.

Prima misura sul modello del laboratorio: 3.755 nodi, 3.616 elementi, 13.718 risultati nodali per componente.
Preparazione scena con dati già caricati: geometria circa 22 ms, volumi circa 16 ms, risultati alta qualità circa
90 ms (109.238 triangoli). Sono misure della prima prova integrata, non un SLA: escludono importazione, calcolo
dell'inviluppo, trasferimento GPU e tempo effettivo del primo fotogramma. I tempi delle prove successive sono negli
artefatti del relativo run. Gli offset -0,70 / +0,45 / +0,35 m della copia sono verificati dai controlli.

La prova finale sullo stesso modello (`supporto/artefatti/model-viewer/ui-validated`) completa 19 controlli WPF:
circa 27/12/80 ms per cambio stato e preparazione delle tre scene. Dopo la rimozione delle facce interne coincidenti,
la vista solida usa 17.432 triangoli. La prova sintetica verifica anche le quote effettive delle facce rispetto
all'offset e il valore bilineare al centro del quadrilatero. La prova grafica finale usa l'antialiasing delle linee e
il depth bias del renderer; MSAA 4x resta da collaudare (la prova di acquisizione immagini non è arrivata a completamento).

Il primo profilo quick ha completato build e tutte le suite fast; i due controlli Wiki richiedevano sette immagini
locali non versionate del checkout principale. Dopo averle copiate, entrambi i controlli Wiki sono PASS senza cambiare
guide, indici né l'elenco dei fallimenti ammessi. Le risorse copiate restano negli artefatti locali del worktree.

## Attività successive, non implementate negli altri progetti

| Priorità | Attività | Ambito futuro |
| --- | --- | --- |
| 1 | Contratto stabile GPC Model per geometria, materiali, sezioni, assi locali, offset e risultati; adattatori dei vari software e rapporto delle entità non supportate | GPC Model + adattatore ANTHEA |
| 1 | Corrispondenza delle sezioni solide: offset delle aste e dei loro estremi, rotazioni locali, sezioni generiche e variabili, confronti con casi indipendenti | GPC Geometry/Model + renderer |
| 1 | Archivio binario/compresso e caricamento selettivo dei campi: evitare di clonare snapshot JSON grandi nella cronologia di annullamento | Libreria documenti del refactoring |
| 1 | Differenze e conflitti delle risorse modello nel confronto delle revisioni; oggi la copia è conservata ma il riepilogo legacy non descrive le modifiche ai modelli | Libreria progetti, dopo il refactoring |
| 1 | Valutazione combinazioni/inviluppi nelle librerie, cache per campo, caso governante e coerenza delle componenti simultanee | GPC Model/Checker, non WPF |
| 2 | Selezione grafica, evidenziazione, isolamento, clipping, filtri per gruppi/sezioni e preview dedicata delle sezioni | Nuovo modulo WPF |
| 2 | Scala manuale/simmetrica, livelli, etichette delle isolinee, scelte esplicite di estrapolazione e media con trattamento delle discontinuità | Contratto risultati + renderer |
| 2 | Spostamenti/deformata, sollecitazioni delle aste, tensioni e risultati di volume | GPC Model + renderer |
| 2 | Editor/creatore di modello e controlli di consistenza ingegneristica, distinti dai soli controlli del file | GPC Model/Checker + nuovo modulo |
| 2 | Trasferimento controllato delle sollecitazioni ai fogli, con unità, assi, convenzioni dei segni, revisione e provenienza verificabili | Libreria applicativa e moduli di verifica |
| 2 | Gestione dei modelli completa: rimozione, spostamento tra sezioni, esportazione del pacchetto, riferimenti a più elementi/casi | Nuovo modulo e futura libreria progetti |
| 3 | Prove end-to-end su primo fotogramma e cambio inviluppo, modelli grandi, DPI, temi, tastiera e accessibilità; soglie di qualità e prestazioni | Nuovo modulo/test |
| 3 | Integrazione nella Wiki e nelle due guide globali con PDF, al rilascio del modulo dopo l'assestamento del refactoring | Documentazione generale |

Il formato `.antheamodel` è un contratto temporaneo di presentazione (schema 1), non il formato definitivo GPC Model.
La copia JSON incorporata e il confronto revisioni incompleto rendono questa una bozza da collaudare in branch,
non una modifica da distribuire agli archivi operativi prima di completare le attività di priorità 1.
