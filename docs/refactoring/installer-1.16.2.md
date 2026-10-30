# Preparazione di ANTHEA 1.16.2

Richiesta del 30 ottobre 2026: prima del setup aggiungere il materiale modificabile nella tabella delle barre del modulo Sezione in c.a.; fare lo stesso per i trefoli e mostrare la tensione di tiro iniziale.

## Contenuto

- Base main ada429e2, con le Azioni già integrate e il set ufficiale AO. Branch di lavoro codex/release-1.16.2; nessuna riscrittura di refactoring/f5.
- Barre: scelta del materiale per riga subito dopo ID e coordinate. «Del foglio» segue il materiale generale; catalogo e materiali personalizzati salvati assegnano una copia delle proprietà alla singola barra.
- Il materiale entra nelle classi GPC della sezione, nelle analisi N–M e tensionali e nella persistenza nativa. Cambiare le coordinate invalida l'archivio precedente e ricostruisce la sezione conservando i materiali assegnati. Ripristina barre da wizard elimina anche le assegnazioni individuali.
- Trefoli/cavi: materiale e tensione iniziale σp0 [MPa] accanto alle coordinate; uso della predeformazione GPC già presente. Il cambio di materiale conserva σp0, che viene nuovamente validata.
- Limiti SLE distinti per barra nell'interfaccia e nell'esportazione quando diversi; materiali individuali nella relazione completa e sintetica.
- Le staffe conservano l'acciaio generale. I modelli accessori che accettano un solo parametro rifiutano esplicitamente gli input incompatibili: dettagli, ancoraggi e torsione per fyk diverso dal generale; fessurazione e taglio MC2010 per Es diverso; M–χ per fyk o Es diverso. Non si sostituiscono valori né si fanno medie tra materiali. B450A e B450C, con uguali fyk ed Es, restano compatibili.
- Guide globali Rev64, con Word e PDF; originali Rev63 e istruzioni del setup conservati in supporto/SUPERATI/rilascio-1162-rev64-20261030, con manifest SHA-256.
- Versione 1.16.2 richiesta dall'utente. Librerie AO invariate: nessun nuovo calcolo normativo né nuova build GPC.

## Verifica e differenze

Cattura precedente: full su 02cd4d2d, cartella supporto/artefatti/ci/20261030-182344-azioni-merge-official-full; main ada429e2 contiene solo i successivi verbali. Copia della schermata iniziale e impronte dei sorgenti in supporto/artefatti/rilascio-1.16.2/prima.

R-1162-MAT: nuove colonne e ordine dei trefoli intenzionali. Le tracce storiche mantengono la proiezione delle vecchie colonne e dei vecchi dati di visualizzazione; attesi numerici e file storici non vengono riscritti. Prove dedicate verificano separatamente il contratto nuovo, le celle WPF reali e il passaggio ai materiali GPC.

Prova dedicata ConcreteMaterialEditors.Tests: 17 controlli PASS, corsa 20261030-204922-release-1162-materials-reports2. Comprende σ=Es·ε con tolleranza 1e-7 MPa, equilibrio della trazione pura a 100 kN con tolleranza 0,1 N, materiali distinti, predeformazione del trefolo, persistenza e input non validi. Architettura PASS nella corsa mirata precedente, senza aumento delle baseline.

Collaudo UI mirato: 234 controlli PASS (20261030-203856-release-1162-materials-ui), comprese le celle reali nei tre temi, tracce, disegni ed esportazioni storiche. L'esito è confermato sul commit finale, incluse le ultime larghezze delle colonne: ID, coordinate, materiale e tensione iniziale dei trefoli sono visibili insieme nelle nuove schermate dei tre temi.

Confronto F3 della corsa 20261030-204457-release-1162-f3: 1.201 file, sole differenze editoriali nei due articoli della sezione in c.a. (Markdown e testo). Registrate 613 voci nuove e 595 superate per gli spostamenti delle righe; attesi ingegneristici e ricerche invariati. Riconfronto completo della stessa cattura in 122 gruppi: zero differenze non ammesse, 80.854 transizioni applicate su 80.854. Evidenza: supporto/artefatti/rilascio-1.16.2/confronto-completo.json.

Guide: 123 pagine pratica e 200 teorica; 1.297/2.458 frammenti, 9/1.493 formule e 26 parti del modello ITEC conservate per documento. Controllate graficamente le pagine aggiornate, gli indici, i frontespizi e le istruzioni del setup. Relazioni con materiali distinti esportate anche in PDF (3 e 2 pagine).

## Collaudo del rilascio

Commit dei sorgenti, dei collaudi e del setup: **71c51c1ba6fbe6402da2374c74b060e282bc201e**, albero pulito. Il set AO è invariato rispetto al full già completato; questa campagna comprende profilo standard, prove UI pertinenti alla sezione, confronto F3, controllo completo del contrasto e verifiche del pacchetto. Suite UI registrate: 73 prima e dopo, nessuna eliminata; aggiunta una suite rapida dedicata ai materiali.

- Standard con installer: `20261030-205840-rilascio-1162-standard`, 85 PASS e una prova da ripetere. `installer/safety` aveva incontrato ANTHEA UiTests in esecuzione nella campagna grafica concorrente; la guardia impedisce l'installazione con l'app aperta. Nessun cambiamento al codice o ai fallimenti ammessi. Riprova isolata, dopo la chiusura dei processi grafici: `20261030-213429-rilascio-1162-installer-safety`, PASS, 26 controlli. Esito consolidato dello standard: 86 PASS.
- UI finale: `20261030-210018-rilascio-1162-ui-finale`, 6 PASS (due build e quattro suite): F3, tracce lunghe della sezione, vista della sezione e contrasto. F3: 1.201 file, zero differenze non ammesse, 80.854 transizioni applicate su 80.854; 119 fixture, 82 articoli e 30 ricerche. Tracce: 70 controlli; vista: 234 controlli; contrasto: 1.520 schermate (506 chiaro, 507 scuro, 507 molto scuro), zero difetti nei tre temi.
- Consolidamento per nome della verifica: **91 PASS distinti**, 29 build e 62 suite, nessun fallimento residuo. Le corse restano conservate e la prima interferenza non viene cancellata: `supporto/artefatti/rilascio-1.16.2/collaudo-consolidato.json`. Nessun file versionato modificato dai collaudi. Le tre esclusioni storiche del runner restano esplicite: ElasticPile.Checks e BridgeValidationCurrent passano nei test delle librerie; ElasticPile.Performance è una misura dei tempi.

## Installer

`supporto/installer/ANTHEA-1.16.2-Setup-x64.exe`, prodotto da `Crea-Installer.ps1` sul commit sopra, con pubblicazione self-contained win-x64. Nessuna installazione eseguita sulla copia utilizzata dall'utente.

| Controllo | Esito |
| --- | --- |
| Dimensione e SHA-256 | 72.886.676 byte; `1709074CCF73B1EF2B0E1CB63D3DC9101C5D72CB5377FCB068D437F716B48F55` |
| Versione del prodotto estratto | `1.16.2+71c51c1ba6fbe6402da2374c74b060e282bc201e` |
| Archivio 7-Zip e audit del payload | PASS; 408 file distribuiti, tutti identici allo staging; 331 file applicativi |
| DLL GPC | 13, set AO; manifest `4054B2D6B0164C65E790DAD64579501641A758FABFA95BCCEE7409B607F0FFC9` |
| Guide ed esempi | Guide Rev64; 72 documenti per 25 moduli |
| Worker Straus7 | 4 file distribuiti; St7api.dll assente |
| Codice di prova, legacy, Gmsh, UnsafeEx | Assenti dal payload |

Audit riproducibile ed evidenze: `supporto/artefatti/rilascio-1.16.2/audit-payload.json`, `build-installer.log`, `7z-test.log`, `7z-extract.log`. Il tag `v1.16.2` identifica il commit del setup; il verbale successivo modifica soltanto documenti di sviluppo.

## Limite già aperto

L'allineamento neve EN all'emendamento A1:2015 resta da completare, come accettato dall'utente. Edizione implementata 2003+AC:2009. Non è parte della modifica alle armature.
