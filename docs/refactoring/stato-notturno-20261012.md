# Stato del lavoro notturno — checkpoint 13 ottobre, 02:51

Mandato fino alle 07:19 Europe/Rome (05:19 UTC): F2 → STG → SEZ → F3 → F4 se completabile. F5 esclusa. Un commit per punto, test/versioni/merge/push autorizzati; nessun agente parallelo. Preservare la finestra primaria con documento non salvato: non chiudere, non riavviare, non compilare nel checkout primario.

## Rilasci conclusi

- F2: main a701af8a, ANTHEA 1.3.0; rapporto chiusura-blocco-f2-20261012.md.
- STG-3: main 22f6a1a, ANTHEA 1.4.0. STG-4 parziale: modello reale senza STAG/STCT; geometria attiva, reazioni stage e lettura nativa Strand7 da completare con un caso reale.
- SEZ-4: main/origin bce1e337, ANTHEA 1.5.0. Full 104 PASS, nessun fallimento nuovo; 478 controlli dati e 18 WPF sezioni. Guide Rev39, 76/144 pagine. SEZ-3b dipende dalla geometria di fase, resta aperto.
- Set V congelato: Model master/origin 7fd1fe4c; Checker master/origin 75e9adaa. 2778 test delle librerie superati, 17 Steel noti e due native skip. Installer 1.5.0 pronto, SHA-256 FC7B90F71807CE0AC038901E947A3D7D400A2D73A1D93ECA298AC6E50C7F2631.

## F3 in corso sul worktree separato

Ramo refactoring/f3-strato-applicativo in model-viewer-draft/ANTHEA; non ancora integrato in main. F3.1 riferimento congelato: 36 fixture, 458 file, 71 articoli Wiki e 30 ricerche. Ultimo riferimento headless prima di F3: corsa 20261012-225933-f2-set-t-full/baseline_cattura/cattura, 548 file. Non usare la vecchia B3 per il confronto corrente.

Punti chiusi e commit:

- F3.1 a35a7413, F3.2 1d1b45e: catture, progetti e regole automatiche.
- F3.3b c35a041: documenti, revisioni e condivisione in Application; Headless validate/inspect.
- F3.4 cadf784: report portabili e DocxWriter unico. 30 DOCX/134 XML/97 immagini equivalenti. Core è solo compatibilità TypeForwardedTo, assente dal prodotto; necessario ai vecchi consumatori esterni.
- F3.5a 0260dd9 e F3.5a.2 c8286da: moduli, scheduler, cache e unico dispatch Application; CLI calculate. 548 file equivalenti, rinomine diagnostiche esatte F3-01 registrate e provate.
- F3.6a 507dc8f e F3.6b c94ac699: pali verticali, acciaio, palificata e fondazioni.
- F3.6c 463f4cb: catalogo, ricerca e parser Wiki portabili; 240 controlli.
- F3.6d d35a20c: ponte/muri e analisi opzionali, 21 prove WPF di aggiornamento e chiusura.
- F3.6e 2565132: orizzontale/elastico; risposta FEM e cache sezioni fuori dalla vista, riuso FEM al cambio armature.
- F3.6f.1 04793a5: presentazione e capitoli materiali portabili, dati iniziali condivisi e mappatura copriferro riusata. 291 controlli Application e adattatori CLS rapidi passati.
- F3.6f.2 4240dfb: vista Materiali accorpata nel Desktop, prove materiali/report e 458 file equivalenti; report brevi WPF passati in 024042-f3-material-move-short-final.

Soglie correnti: 143 TypeRef Calculations, 36 CompositeBridge, 3 Concrete, 6 Geotechnics; 6 async void; 4 DispatcherTimer, solo gerarchia/Wiki. Nessun timer di calcolo residuo. Le fixture ingegneristiche non sono state aggiornate.

## Seguito

1. BridgeDesign F3.6g completato: ricalcolo ritardato, confronto/proposte su servizi e annullamento alla chiusura; 31 prove WPF, smoke e Application superati. Commit nel log.
2. CA: coordinamento di domini, SLE, taglio e cache nello strato Application. Preservare attesi, dettagli, riepiloghi e stato delle tabelle.
3. F3.5b: report progetto autonomo, senza SheetEditor; figure tramite contratto condiviso. Materiali sono già autonomi, altri moduli ancora da completare.
4. F3.7: verificare il confine effettivo, aggiornare guide del tool, suite complete, versione e integrazione/push a fase conclusa. Nessuna dichiarazione di chiusura se rimangono dipendenze concrete.
5. F4 solo dopo F3, entro il mandato. F5 esclusa.

La combo della finestra utente resta intatta. Limite cartesiano 4096 e preflight/scalabilità tracciati nel piano; ENV_SLU originale ha 26168 alternative. Principali e generatore automatico in coda. Nessuna modifica automatica delle combinazioni dal loro nome.
