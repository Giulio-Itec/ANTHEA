# Paratie — incremento 1, 19 ottobre 2026

## Implementazione effettiva

Il modulo `geo_paratie` aggiunge un documento e un editor MVVM indipendenti dai
moduli storici. Il calcolo risiede in Geotechnics, le sezioni in Concrete e il
coordinamento in GPC.Design. Il documento usa m, kN e kPa; l'adattatore converte
nelle unità GPC senza formule ingegneristiche. Il modello e le ipotesi numeriche
sono descritti in `Checker/docs/metodi/paratie.md`.

Sono disponibili trave di Eulero–Bernoulli con EI per tratti, molle indipendenti
per lato con limiti attivo/passivo, stratigrafie orizzontali drenate e non drenate,
falda idrostatica per fase, carichi e sostegni inclinati. Il solutore conserva la
memoria plastica fra fasi, installa i sostegni sulla deformata precedente e
gestisce pretensione, rimozione, limiti plastici, suddivisione degli incrementi e
rollback. Una fase fallita non viene pubblicata come convergente.

Il trasferimento delle azioni a Concrete mantiene caso, fase, sezione e taglio
concomitanti. Il primo percorso verifica una fascia rettangolare in c.a. NTC 2018,
senza progetto automatico delle armature o dettagli completi. Le combinazioni
sono assegnate esplicitamente dall'utente; non si sommano analisi non lineari.

ANTHEA offre tabelle, confronto grafico delle fasi, esecuzione asincrona,
annullamento, importazione della stratigrafia e risultati congelati con impronta
degli input e delle versioni delle librerie. Modificare gli input invalida i
risultati. Il report Word/PDF e il CSV leggono gli stessi risultati dell'interfaccia;
la relazione di progetto non ricalcola il modello.

## Evidenze eseguite

- 14 prove specifiche GPC: 11 motore, 2 passaggio a Concrete, 1 combinazioni Model.
- Set `candidate-04`: 3075 test passati, 17 fallimenti Steel già censiti e 2 test
  ignorati in Model UnitTest. Attesi e liste dei fallimenti ammessi invariati.
- 28 controlli applicativi/MVVM in `application-02/esito.txt`, compresi archivio reale di progetto, trasferimento terreno e JSON dopo riapertura.
- 7 controlli WPF, quattro schermate controllate in tema chiaro/scuro:
  `../ci/20261019-072309-paratie-ui-02/ui_check-paratie-view/vista`.
- Compilazioni Release e UiTests senza errori. I log sono in
  `supporto/artefatti/paratie/`, insieme a report e manifest candidati.
- Il PDF aggiornato di prova (`application-02/paratie`) è stato controllato: una
  pagina A4 senza tagli o sovrapposizioni (`report-render-02/page-1.png`).

Il quick della base F5 (`20261018-230925-paratie-base-sdk`) ha 50 PASS e 2 nuovi
fallimenti documentali: sette immagini non versionate mancano nel worktree. Sono
dipendenze della base, non un motivo per aggiungere fallimenti ammessi. Le sette
immagini sono state recuperate dalla cartella originale con identità SHA-256
registrata in `base-assets.json`; resta da ripetere il collaudo generale.

## Revisioni e rilascio

Model `ffcbcfe2`, Checker `848d801d`; revisioni locali, non pushate.
`candidate-04` è stato creato dal runner coerente e validato in `snapshot-04`.
Le compilazioni ANTHEA usano `-p:GpcLibDir=<snapshot-04>`; `lib/Checker` è invariata.
Il rilascio ufficiale resta aperto e richiede le autorizzazioni Git e i collaudi
completi previsti. Non sono state copiate DLL isolate nel prodotto.

## Limiti e seguito

La matrice rimane l'autorità sull'intero ambito. Il primo incremento **non chiude
il modulo**: mancano ancora filtrazione FEM, integrazione della stabilità globale,
fondo scavo, verifiche Steel e dei bulbi, tipologie complesse, sisma avanzato,
interazione fra pareti, cedimenti, progetto completo delle armature, DXF, guide
globali e connettori. La discretizzazione monodimensionale non dimostra effetti 3D.
L'interpolazione degli incrementi di scavo è una variazione delle azioni e delle
molle fra gli stati di fase, non una simulazione geometrica continua del fronte.

## Collaudo generale e correzioni di integrazione

Il runner standard `20261019-073158-paratie-inc01` ha eseguito 71 suite PASS e
segnalato tre NEW-FAIL, senza modifiche agli attesi o ai fallimenti ammessi:

- cataloghi nei test Application/Presentation: preservato il confronto integrale
  dei moduli storici, verificando separatamente i nuovi moduli applicativi;
- regole architetturali: conversioni con unità nominate, esiti `EngineeringOutcome`
  tipizzati, collezioni statiche immutabili e colori esclusivamente dai token;
- tracciato Fondazioni: la versione Geotechnics è un metadato incluso nell'hash.
  L'audit ha provato che cambiare soltanto `library_version` da 0.5.0.0 a 0.4.0.0
  restituisce esattamente l'hash congelato `5C36AA943058382B`.

Nel confronto con le catture F5 si proietta esclusivamente quel metadato e la
sua etichetta, dopo avere verificato la versione realmente caricata. Gli attesi,
le tolleranze e i risultati di produzione restano invariati. Le catture storiche
non usano la proiezione. Registro PARATIE-02.

Riesecuzioni mirate con il runner: Application.Tests PASS (`paratie-inc01-fix`);
Architecture.Tests e Presentation.Tests PASS (`paratie-inc01-fix2`, 0 NEW-FAIL).
Il profilo standard completo sulla revisione successiva resta da eseguire.
