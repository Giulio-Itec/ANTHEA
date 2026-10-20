# Sezioni in c.a.: persistenza del modello GPC

Richiesta del 20 ottobre 2026. Base `refactoring/f5` a `aa70a263`;
ramo di lavoro `refactoring/f5-sezioni-persistenza`.

Il file conserva la `ReinforcedConcreteSection` usando il contratto XML
versionato di `GPC.Model.Persistence.ModelArchive.SaveDocument`. Materiali,
barre e trefoli conservano i loro oggetti e identificativi. Mesh e risultati
transitori vengono ricostruiti dai motori esistenti. Nessuna nuova formula,
modifica alle DLL del set AA o dipendenza GPC in ANTHEA.Serialization.

La ricetta dell'editor, i carichi e le impostazioni delle verifiche rimangono
nel documento. Il nuovo progetto senza WPF ANTHEA.ConcreteWorkspace gestisce
soltanto il contratto nativo e le impronte. La ricetta e i trefoli invalidano
il modello salvato quando cambiano. Le impostazioni delle viste e delle
verifiche non sostituiscono la sezione. I dati incompleti si salvano come
bozza, con motivo esplicito e senza un modello precedente.

## Formato e compatibilità

I documenti contenenti sezioni c.a. vengono scritti in versione 3 con
`versione_base` 1 o 2. La versione in memoria resta 1. I file precedenti
restano leggibili e vengono migrati solo al salvataggio; le versioni
precedenti dell'applicazione non devono aprire il nuovo formato.

`input.sezione_gpc` contiene versione del contratto, unità mm–MPa–N, stato,
impronta SHA-256 della ricetta e, per un modello valido, XML e impronta XML.
La posizione dentro input mantiene coerenti le firme delle sessioni di
calcolo esistenti. Non è una copia automatica dei campi del ViewModel.

Il codec lavora su una copia del documento: il salvataggio non prepara né
modifica l'editor vivo. Tutte le revisioni storiche, anche annidate, vengono
trasformate con rimappatura dei riferimenti del pool. I dati degli altri
moduli rimangono invariati. Contratti futuri, XML alterati e corrispondenze
incoerenti vengono rifiutati; il writer atomico conserva il file precedente
quando il salvataggio fallisce.

## Verifiche eseguite

- Prima: `ci/20261020-122913-sezioni-persistenza-prima`, quattro suite PASS.
- Dopo: `ci/20261020-123909-sezioni-persistenza-02`, prove applicative,
  Presentation, ca-data, ca-module e adattatore rapido PASS. I tre rilievi
  Architecture sulla lettura implicita delle versioni sono stati corretti
  usando il parser esplicito, senza eccezioni alla baseline.
- Riesecuzione: `ci/20261020-124814-sezioni-persistenza-03`, cinque suite
  PASS, nessun nuovo fallimento; 2.870 controlli Application.
- Sette confronti di salvataggio/riapertura: rettangolo, cerchio, T,
  rettangolo e cerchio forati, barre manuali, trefolo. Proprietà geometriche,
  coordinate, aree e tensioni di barre e fibre identiche. Aree di rettangolo
  e rettangolo forato controllate anche con espressioni indipendenti.
- Identità native effettivamente usate dal calcolo, istanze indipendenti,
  secondo salvataggio stabile, modifiche e bozze, corruzioni rifiutate,
  preparazione reale dell'editor e revisioni storiche verificate.

Le fixture indipendenti non sono state modificate. Le comparazioni dei
metadati storici ignorano esclusivamente il nuovo campo `sezione_gpc`;
il contenuto nativo è verificato separatamente.

Collaudo esteso e documentazione utente da consolidare insieme all'estensione
dei wizard richiesta nella stessa attività. Nessun push o merge eseguito.
