# Chiusura del blocco F2 concordato — 12 ottobre 2026

Chiusi i punti 1–5 e 5.2 approvati dall’utente. Questa chiusura non dichiara completati tutti i residui storici di F2: R1/R2 delle tracce, ratifiche e rimozione del legacy restano nelle loro voci. Non è la fase 5, esplicitamente esclusa dal lavoro notturno.

## Modifiche concluse

1. API-02: interassi circolari dalla libreria corretta (66a9d351; Checker e1a9c78e).
2. Validazione degli ingressi e recupero dello stato nelle schede SLE (cf33c16e).
3. Interassi NTC visibili e opzione getti sottili UNI approvata (fbedb8ca; A10 in 1e9998e0).
4. Rimozione del solo lock globale di costruzione, dopo prova di equivalenza (c53fd9b).
5.2. Finestra Reologia del calcestruzzo, con phi(t), phi a infinito, Ecm(t), fattore di maturazione, grafici e tabella; ViewModel senza WPF, formule in Model, guide Rev37 (71aaa60b).
5. Collaudo finale, distribuzione del set T e ANTHEA 1.3.0.

## Rilascio e verifiche

Set T non candidato prodotto da checkout puliti: Utilities e57b440b, Geometry 6734eee1, Model 94efc9bc, Checker 8a9021f9, tutti pubblicati su master prima della costruzione. Dodici DLL nel set, dieci usate da ANTHEA, versioni e SHA-256 nel manifest. 2742 test di libreria PASS; soltanto i 17 Steel già censiti e le due prove native Model già escluse. Nessun Gmsh distribuito. Dettagli delle versioni nel README di lib/Checker.

Runner full, con stadio baseline aggiunto, su 71aaa60b: 102 PASS e una differenza inizialmente non classificata nel confronto delle fixture. Si trattava esattamente delle 13 tracce BarSpacing F2-22 già approvate; il controllo fixture non ne aveva le regole puntuali. Trasferite le medesime sei regole (righe, valori e conteggi esatti) al confronto fixture-checker, senza modificare gli attesi o le tolleranze. La prima ripetizione senza stadio build usava ancora la copia precedente del file di classificazione: non valida come collaudo della correzione. Ripetizione con build dello strumento: 3 PASS, zero fallimenti. Non è stata aggiunta alcuna eccezione a known-failures.

- Full: supporto/artefatti/ci/20261012-225933-f2-set-t-full.
- Confronto fixture finale: supporto/artefatti/ci/20261012-232515-f2-set-t-fixture-final.
- Headless: 548 file uguali, solo 21 tempi Stopwatch volatili.
- Denso: 27 file e 50996 righe, 48511 identiche e 2485 con soli identificativi casuali; zero numeri o testi cambiati rispetto alla chiusura SLE.
- Fixture Checker: 27698 righe; sole differenze classificate S4-1/S4-2 già esistenti e 13 F2-22. Nessuna differenza sconosciuta.
- Concorrenza: 326 controlli e 36 stati; 80 mesh identiche bit per bit alla cattura precedente.
- Reologia sul set definitivo: 57 controlli senza WPF e 21 WPF; validazione ingressi 62 e 20 WPF. Inclusi nel full e nella corsa mirata 20261012-225749-reologia-set-t.

Le prove già superate non sono state ripetute per la sola correzione dei metadati di classificazione; il confronto interessato è stato ricompilato e ripetuto attraverso il runner unico. Il pacchetto 1.3.0 comprende le guide pratica e teorica Rev37 (73 e 142 pagine), con controllo delle formule Word, dell’impaginazione e dell’indice. Nessuna finestra utente con documento non salvato è stata chiusa.

Installer prodotto e controllato: supporto/artefatti/rilascio-1.3.0/ANTHEA-1.3.0-Setup-x64.exe, 62350158 byte; SHA-256 78907349D64819168389E94438EBB681B5C07B076FC6996418904E70617AFA38. Pubblicazione Release senza marcatori di prova; tre PDF inclusi (due guide e indice).

## Seguito

Merge e push di questo blocco autorizzati dall’utente. Seguono STG, visualizzazione delle sezioni SEZ, F3 e, se possibile nel tempo disponibile, F4. F5 non va iniziata. Il dominio stage è in un ramo separato di Model e non entra nel set T né nell’installer 1.3.0.
