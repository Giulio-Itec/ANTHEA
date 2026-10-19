# Incremento 2 — filtrazione stazionaria e collegamento ad ANTHEA

19 ottobre 2026. Il modulo complessivo resta in sviluppo; nessuna riga aperta
della matrice è esclusa. Questo incremento riguarda PAR-13, PAR-14 e i relativi
percorsi di persistenza, interfaccia e report.

## Modello implementato

Model contiene dominio, materiali, tensore di permeabilità, condizioni al bordo
e profili di pressione. Geotechnics risolve Darcy/Richards stazionario su
triangoli P1, con tensore simmetrico positivo, legge van Genuchten–Mualem,
iterazione non lineare, ricerca del passo, PCG e bordo atmosferico unilaterale.
La documentazione matematica e le fonti primarie sono in
`Checker/docs/metodi/filtrazione-paratie.md`. Un campo non convergente non
pubblica nodi o flussi utilizzabili per il trasferimento alla paratia.

L'adattatore ANTHEA costruisce un dominio finito a due colonne, con superfici
orizzontali, parete impermeabile centrata su x=0 e passaggio sotto il piede.
Estensioni laterali, fondo impermeabile, spessore, mesh e permeabilità sono input
obbligatori espliciti. I livelli di falda delle fasi prescrivono i carichi sui
bordi laterali. Il terreno affiorante è drenante o soggetto al battente indicato.
Le curve non sature sono richieste se il dominio le necessita; non si inventano
permeabilità, curve di ritenzione o condizioni al contorno.

Ogni fase ha un'analisi idraulica indipendente e stazionaria. Le pressioni sulle
facce della parete sono interpolate da Geotechnics con tutti i cambi di elemento
e gli attraversamenti di pressione nulla, poi trasferite alla storia meccanica.
La suzione resta nel campo FEM; per la meccanica può essere trascurata solo con
scelta esplicita, senza attribuirle resistenza aggiuntiva. La quota di falda
resta attualmente il confine dei pesi naturali/saturi nella meccanica: la
distribuzione del peso per falda curva richiede un'estensione dedicata.

Unità interne: mm, MPa, permeabilità mm/s, portata per lunghezza ortogonale
mm²/s. Interfaccia: m, kPa, m/s e L/s per metro di paratia. Il tensore Kxz è
orientato secondo x verso destra e z verso l'alto. α è riferito al carico di
pressione (1/m in ingresso, 1/mm nel motore).

## Percorso applicativo

- Schema documento 1→2: filtrazione disattivata, nuovi dati idraulici assenti,
  input precedenti conservati. Gli snapshot di vecchie revisioni sono invalidati
  dall'impronta delle versioni e degli input, non convertiti in risultati nuovi.
- Scheda Filtrazione, tabelle dei materiali, diagramma dei valori nodali e mesh
  in scala geometrica, tabella nodi/flussi e bilancio per fase. Il grafico mostra
  valori nodali, senza introdurre un'interpolazione grafica non dichiarata.
- Calcolo asincrono con avanzamento e annullamento. Un fallimento idraulico
  arresta l'orchestrazione prima del calcolo meccanico, conservando le sole fasi
  idrauliche già concluse e la diagnostica.
- Persistenza del risultato completo e CSV di nodi/elementi. Report e vista
  consumano lo stesso snapshot; nessuna formula o soluzione idraulica in WPF.

## Prove eseguite

Evidenze in `supporto/artefatti/paratie/`, salvo runner ANTHEA sotto `ci/`.

| Prova | Esito osservato | Evidenza |
| --- | --- | --- |
| FEM idraulico e regressioni meccaniche | 23 PASS, di cui 12 idrauliche | `seepage-03/embedded-walls.trx` |
| Set candidato completo GPC | 3087 PASS; 17 fallimenti Steel già ammessi, nessun nuovo fallimento | `candidate-05/release-set.json`, `candidate-05.log` |
| Applicazione e MVVM | 43 controlli PASS | `hydraulic-app-03/esito.txt` |
| WPF | 10 controlli PASS; viste chiare/scure ispezionate | `../ci/20261019-081241-paratie-hydraulic-ui/ui_check-paratie-view` |
| Architettura | PASS dopo costante di layout esplicitata | `../ci/20261019-081356-paratie-hydraulic-architecture` |
| Relazione idraulica | PDF A4 di 2 pagine renderizzato e controllato, nessun taglio osservato | `hydraulic-app-03/paratie/filtrazione.pdf`, `hydraulic-report-render/` |

Le prove GPC usano attesi analitici per Darcy, tensore ruotato, strati in serie,
Neumann, equilibrio non saturo, flusso gravitazionale e pressione trasferita a
una mensola. Un campo armonico quadratico controlla il raffinamento della mesh.
Il dominio con parete verifica bilancio e riduzione della portata all'aumentare
dell'infissione. Il caso applicativo verifica h=0 e u=−9,81 z in m/kPa nella fase
iniziale, poi lo scavo con dislivello idraulico, persistenza, invalidazione,
annullamento durante l'analisi e rifiuto delle permeabilità non ammissibili.
Un limite di una iterazione provoca il fallimento della seconda fase e nessun
risultato meccanico, senza falsi esiti soddisfatti.

Le prime esecuzioni hanno individuato un problema nei dati JSON sintetici
(riutilizzo dello stesso nodo) e una costante grafica non nominata: entrambi
corretti senza cambiare attesi indipendenti o liste dei fallimenti ammessi.
La resa PDF è quella del generatore nativo ANTHEA; non è attestata una resa Word
tramite LibreOffice, che non è disponibile nel runtime documentale configurato.

## Revisioni e rilascio

Set locale da Model `7fdde1a09b395a66d0a740cbf844817c4770d20e` (Model 5.7.0.0)
e Checker `e51d54614a4f2d70000d84e5a74430618e38ce28` (Geotechnics 0.6.0.0).
Prodotto da `Release-Set.ps1 -Candidate`, verificato e preparato da
`Update-Snapshot.ps1 -AllowCandidate` in `snapshot-05`. Manifest, versioni e
SHA-256 sono conservati. `lib/Checker` non è stato aggiornato; nessun push o
merge è stato eseguito. Geometry, Utilities e Delaunay conservano le impronte
precedenti. Gmsh rimane escluso dalla produzione.

## Ambito ancora aperto

Restano da coprire domini e opere più generali, condizioni al bordo ulteriori
nell'editor, falda curva per i pesi meccanici, verifiche HYD/UPL e del fondo,
collegamento con la stabilità globale, tampone e tutte le altre righe aperte
della matrice. Il solutore stazionario non rappresenta consolidazione,
filtrazione transitoria o interazione tridimensionale. Le guide globali e il
collaudo complessivo del modulo non sono ancora conclusi.
