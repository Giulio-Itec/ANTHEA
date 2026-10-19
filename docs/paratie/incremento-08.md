# Incremento 8 — sezioni circolari in calcestruzzo armato

19 ottobre 2026. Incremento additivo, non chiusura delle righe PAR-03 e
PAR-24 né del modulo complessivo.

## Modello e verifiche

Model contiene `CircularRcSection`: cerchio, corona uniforme con numero
pari di barre, staffe singole, materiali e fonte. Copriferro netto esterno
alla staffa, raggio barre R−c−φst−φL/2. Barre sovrapposte o fuori dal
contorno rifiutate. EI usa il cerchio lordo; l'integrazione delle tensioni
usa un poligono dichiarato, 64 lati predefiniti, da 32 a 256. Materiali e
solutori sono posseduti da ogni sessione; mesh Delaunay storiche invariate.

`CircularRcSectionSession` riusa Checker Concrete: `PileReinforcement`,
solutore N–M a N costante, verifiche tensionali e fessurazione esistenti.
Profilo Italia NTC 2018 ordinario statico, C16–C50, SLU e SLE
caratteristica/frequente/quasi permanente. Taglio circolare equivalente
con staffe singole e conferma esplicita dell'applicabilità. Nessuna capacità
viene attestata quando il modello di taglio non è confermato.

I dettagli sono esiti separati: non trasformano una resistenza nominale
in verifica completa. Sono riutilizzati i controlli geometrici, di
copriferro, armatura minima/massima e staffatura pertinenti alla modalità
trave/pilastro/comune selezionata. I minimi sismici dei pali di fondazione
non vengono applicati automaticamente. Ancoraggi, giunti, instabilità,
dettagli dissipativi e progetto automatico delle armature restano aperti.

GPC.Design trasferisce tutte le azioni concomitanti con s/b una volta,
conservando caso, fase, sezione, quota e lato. N positivo a trazione nel
verificatore; EI_fascia=Ec·I·b/s. Fila unica e ripartizione uniforme
esplicite; non sono dedotti più file, pali secanti o effetto arco.
L'analisi globale resta strutturalmente elastica.

Fonte primaria: [NTC 2018, capitolo 4, G.U.20/02/2018](https://www.gazzettaufficiale.it/do/atto/serie_generale/caricaPdf?art.codiceRedazionale=18A00716&art.dataPubblicazioneGazzetta=2018-02-20&art.num=1&art.tiposerie=SG&cdimg=18A0071600100010110005&dgu=2018-02-20).
Ricontrollati testo e pagine 72, 76, 77, 80: coefficienti dei materiali,
scelta SLE e limiti tensionali, modello resistente a taglio. Il profilo
GPC è riutilizzato senza sovrapporre un annesso nazionale europeo.

## Percorso applicativo

Schema 8: tabella `rc_piles`, EI assegnato o derivato dal palo, profilo e
combinazione specifici. Migrazione 7→8 senza modificare il documento
originale né attivare verifiche. Vista con azioni fisiche, proprietà,
coefficienti, controlli di sezione e dettagli separati. Salvataggio,
riapertura, CSV a 19 colonne e report condividono lo stesso risultato.
Le modifiche invalidano risultati e comandi di esportazione. Sezioni o
combinazioni non supportate sono Non valutato.

## Prove eseguite

- Attesi nuovi creati prima della produzione da
  `Checker/tools/ParatieRcPileExpected.py`: fibre orizzontali indipendenti,
  131072 strisce confrontate con 65536, scarto relativo sotto 1e-7.
  Tre N, entrambi i segni di M, contorni 64/128. D=800 mm, 12φ20,
  c=50 mm, staffe φ10, C30/B450: MRd a N=0 sul contorno a 64 lati
  461314867.0936418 Nmm. Tolleranza 0.3% sul solutore esistente, motivata
  da discretizzazione e criterio assiale. Nessun atteso storico alterato.
- `rc-pile-design-04`: sei test PASS, comprensivi di confronto indipendente,
  proprietà, mensola analitica, trasferimento azioni, SLE analitico in
  compressione, segni della flessione, input invalidi, isolamento
  concorrente e annullamento. Primo errore di namespace e conteggio dei
  controlli SLE frequenti documentati in Checker; fixture numerica invariata.
- `candidate-15`: 3140 PASS, stessi 17 fallimenti storici Steel ammessi;
  due test Model già ignorati. API compatibili. Model
  `d0c6bedaeed62557516fa5b4a297f0fc855a5ab5`, Checker
  `7482bc2a8caa0b28835ab4d35353e2b6d00546fb`. Model 5.13.0.0,
  Concrete 0.12.0.0, Design 0.26.0.0; manifest con versioni e SHA-256.
  Staging integrale `snapshot-15`, `lib/Checker` invariato.
- `rc-pile-app-03`: 483 controlli applicativi/MVVM PASS. Inclusi migrazioni,
  riapertura dei risultati, invalidazione, isolamento, annullamento,
  provenienza, coefficienti e CSV/report. Mensola analitica di 6 m,
  fascia 1.5 m, interasse 1 m: EI circa 990327.6 kNm², spostamento
  0.07270322 mm per forza trasversale di 1 kN. Il caso mostra resistenza
  soddisfatta e staffatura minima insufficiente senza confondere gli esiti.
- `ci/20261019-131617-paratie-inc08-ui2`: build Release/UiTests,
  architettura e 35 controlli WPF PASS. Successiva modifica soltanto
  alle etichette dei dettagli, verificata in `rc-pile-app-03`.
- `rc-pile-app-03/paratie/sezioni-pali-ca.pdf`: tre pagine A4 renderizzate
  e controllate in `rc-pile-report-render-03`; risolte sovrapposizioni delle
  etichette e pagina finale quasi vuota. PDF nativo ANTHEA, senza attestare
  un rendering Word non eseguito.

Tutti gli artefatti sono in `supporto/artefatti/paratie/`, salvo le corse
CI indicate. Regressione standard dell'incremento ancora da eseguire.
Guide globali, righe residue della matrice, rilascio ufficiale e merge
restano aperti. Approfondimento pali/micropali confermato in coda.
