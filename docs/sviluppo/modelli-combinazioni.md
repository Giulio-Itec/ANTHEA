# Combinazioni nel modulo Modelli — bozza del 12 ottobre 2026

Branch `codex/model-combinations`, allineato a main con merge nel branch. Non cambia le librerie
sorgente esterne, le verifiche strutturali di ANTHEA o i coefficienti normativi. Nessun push o merge
di ritorno. Il generatore automatico e il calcolo delle principali sono attività successive.

## Gestione e persistenza

La scheda **Combinazioni** ospita il gestore MVVM nella navigazione sinistra. Ordine richiesto:
**Vista · Modello · Combinazioni · Risultati · Verifiche · Report**, mantenuto anche nella barra ridotta.
Le identità dei pannelli restano stabili e distinte dalla posizione grafica. Elenco delle definizioni
importate e locali, nome, tipo, termini e coefficienti;
creazione, duplicazione, modifica, eliminazione, annullamento della bozza e calcolo esplicito.
Le definizioni importate possono essere modificate mediante una sostituzione locale con identità
stabile. Eliminazione e ripristino agiscono sulla copia ANTHEA; le tabelle sorgente rimangono integre.
Una combinazione richiamata da altre definizioni non si elimina finché rimangono quei riferimenti.

La griglia riporta una combinazione per riga e un caso o riferimento per colonna. Identità congelate,
scorrimento orizzontale, ordinamento e copia delle celle. È una vista dei coefficienti salvati:
si seleziona la riga e si modifica nell'editor associato. Termini ripetuti sono separati da `;`, senza
sommarli o nasconderli. La griglia comprende anche riferimenti mancanti o ad analisi non supportate.
Il gestore può essere ridotto, staccato in una finestra autonoma e riagganciato. Entrambe le viste
condividono lo stesso ViewModel; la chiusura della finestra esterna riaggancia il pannello.

I campi opzionali del pacchetto mantengono l'impronta dei vecchi documenti quando assenti.
Le modifiche alle definizioni invalidano tutti i risultati derivati. I quattro aggiornamenti
conservano le definizioni locali; i casi base e la sorgente vengono controllati nuovamente.
Nell'aggiornamento completo prevalgono eventuali definizioni locali presenti nella nuova copia
con la stessa identità. I riferimenti divenuti mancanti restano visibili e impediscono il calcolo.
Le revisioni in sola lettura consentono la consultazione ma impediscono modifiche e calcolo.

## Collegamento al motore

`ANTHEA.ModelWorkspace` converte i dati conservati nel contratto GPC; nessuna somma o trasformazione
strutturale è implementata nella UI. `CombinationExpansion` risolve riferimenti e alternative;
`CivilNxGeometryReader` e `CivilNxResults` normalizzano input e risultati; `CombinationRebuild`
esegue sovrapposizione e inviluppi, conservando gli stati concomitanti.

Sono collegati plate locali, sei componenti beam e traslazioni nodali quando sono disponibili
anche le tre rotazioni richieste dal contratto GPC. L'importazione aggiornata richiede RX/RY/RZ;
i vecchi pacchetti con sole traslazioni restano validi, ma non producono deformate combinate.
Rotazioni in radianti e traslazioni in metri hanno conversioni distinte. Altre famiglie di aste,
principali, analisi non lineari, fasi/step, ABS e SRSS non sono dichiarate come sovrapposizioni supportate.

L'adattatore risolve sempre gli ID sorgente dei nodi a partire dalle identità GPC: il campo GPC
`SourceNodeId` dei risultati plate contiene l'ID interno dopo la mappatura. Il caso con numerazioni
diverse è coperto da test dedicato e confronto della spalla. Non si rimappano punti per prossimità.

Il modello GPC preparato è una copia di lavoro immutabile. Un contenitore di risultato privo di
coefficienti è dichiarato prima dell'analisi; ogni ricostruzione riceve le alternative effettive
e usa `attach:false`. Gli stati ritornati sono ricondotti ai proprietari attraverso i campioni base.
La cache conserva un solo modello normalizzato con tutti i casi statici disponibili; cambi di
coefficienti non cambiano il suo input. Una nuova geometria o nuovi casi ne invalidano l'impronta.
Le definizioni utente e l'impronta completa del calcolo sono conservate nel documento ANTHEA.

L'inviluppo produce due campi min/max per componente e, per ciascun campione, alternativa governante
e vettore simultaneo. Il vettore di estremi visualizzato non viene qualificato come simultaneo.
La tabella dei risultati plate/beam espone governante, alternativa e componenti concomitanti con unità esplicite.
Il contratto rilasciato GPC descrive i governanti nel campo Coverage: l'adattatore ne legge il formato
rigorosamente e rifiuta un formato diverso. Un contratto strutturato è un miglioramento futuro di GPC.

Annullamento ed errore non pubblicano campi parziali. Il pacchetto conserva i campi calcolati e li
riutilizza; per restare entro 256 MiB può rimuovere soltanto cache derivate precedenti, avvisando
l'utente. Casi base e definizioni non vengono rimossi. Se la nuova combinazione da sola non entra,
l'operazione viene rifiutata mantenendo il documento precedente. Espansione limitata esplicitamente
a 4096 alternative, senza troncamenti silenziosi.

## Collaudo e tempi

Attesi manuali indipendenti per plate, beam e nodi; coefficienti negativi, inviluppi annidati,
cambio di governante fra componenti, cicli, riferimenti mancanti, salvataggio/riapertura, aggiornamenti,
ID sparsi, annullamento, revisione di sola lettura e comandi MVVM. Nuova suite WPF nel runner unico:
`ModelViewer.Combinations.UiChecks`, nei tre temi, inclusa griglia e finestra staccata.

Spalla: 3755 nodi, 3616 elementi e 49 casi conservati. `SLU_Q1_1` richiede 16 casi ed espande
48 alternative. Confronto completo dell'estremo massimo plate con la tabella MIDAS indipendente:
13718 vertici × 8 componenti = **109744 valori**, zero differenze oltre `1e-8` assoluto/relativo;
massimo scarto assoluto `1.006e-9`. Gli attesi e la tabella originale non sono stati modificati.
Questo confronto non certifica l'intero ENV_SLU, tutte le aste o i dati non acquisiti dal software.

Prima implementazione senza preparazione condivisa: circa 25 s per preparare 16 casi e costruire
la combo. Implementazione con cache, Release: **30.945 s** una tantum per input e 49 casi,
**6.329 s** di ricostruzione e **0.443 s** di campi (totale **37.716 s**). Ricalcolo forzato con
preparazione riusata: **7.606 s**, dei quali **1.163 s** controlli/impronte, **6.067 s** GPC e
**0.376 s** campi. Sono misure di questa macchina, escluse persistenza nel progetto e disegno WPF.
Il cambio di un campo già calcolato evita la ricostruzione. L'obiettivo di 5 s per nuovi inviluppi
non è ancora raggiunto: profilare GPC e impronte, senza spostare formule dentro ANTHEA.

Evidenze ignorate: `supporto/artefatti/model-viewer/combinations-20261012/real-prepared-02`;
`compare-real-combination.py` nei test ripete il confronto senza scrivere sui riferimenti.
L'errore iniziale di associazione degli ID è stato intercettato dal validatore prima di pubblicare
il risultato e corretto; la prima cartella `real-prepared` conserva la prova fallita.

Collaudo conclusivo: profilo `full`, corsa `20261012-150738-combo-full`, **83 PASS, 1 KNOWN,
zero nuovi fallimenti**. L'anomalia nota è `verifiche/project-calculations`, già censita.
Il controllo finale dei metadati respinge tabelle STLD/LCOM malformate prima di pubblicare
il documento; l'editor conserva lo stato originale della definizione importata. Dopo queste
due correzioni, corsa mirata `20261012-152635-combo-finale`: **426 controlli logici** e tutte
le tre suite WPF Modelli passate, senza errori di binding.
Rifiniture di formattazione dei coefficienti e tema della finestra staccata ricontrollate nella
corsa `20261012-153046-combo-finiture` (426 controlli logici e suite WPF combinazioni). Catture
del modello reale in `combinations-20261012/real-ui-02`; preview con avviatore `Apri-Combinazioni.cmd`.
Spostamento finale nella scheda sinistra: corsa `20261012-154019-combo-tab-sinistra-finale`,
426 controlli logici e tre suite WPF passate, incluso ordine, apertura, riduzione e riaggancio.
La prova del clic contestuale nell'albero chiude prima il gruppo geometria per rendere visibile
la proprietà nel viewport virtualizzato, ridotto dalla nuova intestazione; attesi numerici invariati.
I coefficienti sono formattati senza code decimali superflue; nessun arrotondamento dei dati di calcolo.

Rieseguite le suite Model sui sorgenti del commit rilasciato, estratti in una cartella di prova
isolata e collegati alle DLL del set: UnitTest **1186 PASS, 2 esclusi**, Model.Core.Tests **6 PASS**.
Le sette DLL GPC usate dai test coincidono per SHA-256 con il manifest ANTHEA. TRX e log in
`combinations-20261012/library-checks`; nessuna modifica ai checkout delle librerie.

Le guide globali e il rilascio del modulo restano nella fase di consolidamento già prevista
dal piano della bozza; questo documento descrive l'integrazione di sviluppo, non è una nuova guida utente.
