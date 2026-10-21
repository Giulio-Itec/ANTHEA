# Piano della prossima parte del refactoring (dal 21/10/2026)

Punto di partenza:
- `refactoring/f5` a c7fe03dd, con il set ufficiale **AE** installato;
- ANTHEA 1.12.1, mai pubblicata; l'installer viene rifatto sul set AE;
- quota settimanale azzerata il 21/10.

Le regole per gli agenti restano in `Documents\Repository\ANTHEA-f5-ripresa\istruzioni-agenti\`. Gli avanzamenti vanno in
`ANTHEA-f5-ripresa\f5-agenti\<agente>\avanzamento.md`, non più nello scratchpad. Stato e procedure: [f5-ripresa.md](f5-ripresa.md).

## 0. Chiusura del rilascio

1. Fatto il 21/10: collaudo full sul set AE con le prove dell'installer, PASS 169; due prove intermittenti dipendenti dai
   tempi, ripetute con esito PASS ([verbale](installer-1.12.1.md)).
2. Fatto il 21/10: installer 1.12.1 su AE costruito da c7fe03dd, audit PASS (382 file, 13 DLL GPC, 52 esempi, guide Rev56,
   nessun Gmsh né assembly di prova).
3. Push di `refactoring/f5` e tag `v1.12.1` **solo con l'ok dell'utente**.

## 1. Versione di prova unica dei moduli F5 (blocco A, un agente)

- Ramo `prova/f5-viste-2` dal `refactoring/f5` di oggi. Sette moduli a Mvvm: materiali, palo verticale, stratigrafia, muri,
  fondazioni, palo orizzontale con la scheda elastica.
- Versione portatile `1.12.1-prova.f5e` con `strumenti\crea-prova-f5.ps1`.
- `LEGGIMI` con le prove a mano raccolte dai documenti dei moduli: f5-5 §4.7, f5-6, f5-6b, f5-9, f5-9b §5.6, f5-7 §5.7,
  f5-8 §6.7.
- Prima di consegnarla:
  - corsa build, fast, wiki e ui sul ramo di prova; i fallimenti attesi sono solo i controlli «la Release resta storica»;
  - ripetere sul codice di oggi i banchi a differenza zero dei sette moduli.
- Dopo l'ok dell'utente, il passaggio di un modulo alla volta, con la procedura di f5-ripresa.md. Per il palo orizzontale va
  prima riscritta `F3PileSegmentChecks`.

## 2. Sezione c.a., F5.10 S2–S9 (blocco B, un agente alla volta)

1. **S2a, riallineamento.** Il piano dei sottopassi (f5-10-sezione.md §8) va confrontato con il codice di oggi:
   - persistenza nativa GPC (versione 3);
   - dodici wizard;
   - capsula con armature per tratti;
   - dettagli del pilastro.

   Bisogna anche verificare che le tracce congelate `tests/fixtures/f5-sezione` siano ancora quelle della vista attuale. Se
   vanno ricatturate, lo si fa in un commit separato e motivato.
2. **S2:** azioni (`SectionActionsViewModel` in Presentation, C3), insieme al modello di tabella dei pannelli di S3–S5.
3. **S3–S9**, in ordine: domini e 3D, SLE e tensioni, taglio e torsione (resta sul thread dell'interfaccia), dettagli e
   ancoraggi, progetto delle armature, ispezione, report e report breve.
4. Poi la vista XAML per sottopasso, il banco e la prova dell'utente. La Release passa solo alla fine (C10).

## 3. F5.11 e F5.12 (blocco C, in parallelo a B)

- **F5.11, sezione composta del ponte:** 25 file, 2 526 righe, vista staccabile. Preparazione con la ricetta (fixture,
  tracce, derivazioni, ViewModel), poi la vista.
- **F5.12, Bridge Design e prezzari:** 8 file, 1 388 righe, con `PriceCatalogueViewModel` da spostare. Preparazione, poi la
  vista.

## 4. F5.13 shell (dopo B e C, oppure le parti a–c in parallelo)

- Parti a–c: sessioni, coordinatore, storia; ViewModel di navigazione, albero, revisioni, schede, comandi; viste e finestre
  staccate.
- Parti d–g: stato statico E5 e prova dei calcoli paralleli; prove di `MainWindow` trasferite; sessione di prova con
  l'utente; ritiro di `MainWindow` e `SheetEditor`.
- Requisiti dell'utente dell'8/10: fogli in schede, un progetto alla volta, più calcoli singoli aperti, finestre staccabili.

## 5. F5.14 e F5.15

- **F5.14:** `X.Desktop` diventa `ANTHEA.Desktop` e si toglie la configurazione UiTests.
- **F5.15:** revisione con l'utente di tutte le scelte «da decidere» (decisioni del 17/10 e domande dei documenti f5-*) e delle
  voci R4–R21 del registro.

## 6. Paratie e code (blocco D, quando c'è capacità)

- **Righe aperte della matrice:**
  - PAR-06 Culmann;
  - PAR-19 pushover delle paratie affacciate;
  - PAR-20 interazione;
  - PAR-21 effetto arco;
  - PAR-22 cerniere plastiche;
  - PAR-26 altre norme.

  Poi la testa di pali e micropali sotto il piano di campagna (seguito-pali-interrati.md).
- **Ambiente:**
  - con lo schermo a 96 DPI, ripetere `ui/check-contrast` e i passi `mvvm-…`;
  - dopo la verifica di Trend Micro, ripetere le due prove dei PNG;
  - domanda S6.

## Ordine di avvio consigliato

1. Chiusura del rilascio (0), poi il blocco A, che serve all'utente per provare.
2. In parallelo il blocco B (S2a) e il blocco C (preparazione di F5.11).
3. Al massimo tre agenti alla volta. Unire nel ramo `refactoring/f5` dopo ogni blocco e fare la corsa build, fast, wiki e ui
   senza il contrasto.
